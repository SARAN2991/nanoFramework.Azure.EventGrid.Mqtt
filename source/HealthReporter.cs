// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Threading;
using nanoFramework.Json;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Delegate for health report events.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">Event data containing the health report.</param>
    public delegate void HealthReportEventHandler(object sender, HealthReportEventArgs e);

    /// <summary>
    /// Provides data for the <see cref="HealthReporter.HealthReportPublishing"/> event.
    /// Allows the application to add custom health metrics before the report is published.
    /// </summary>
    public class HealthReportEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the health data hashtable. Add custom key-value pairs before publish.
        /// </summary>
        public Hashtable HealthData { get; }

        /// <summary>
        /// Gets the report sequence number.
        /// </summary>
        public int SequenceNumber { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthReportEventArgs"/> class.
        /// </summary>
        public HealthReportEventArgs(Hashtable healthData, int sequenceNumber)
        {
            HealthData = healthData;
            SequenceNumber = sequenceNumber;
        }
    }

    /// <summary>
    /// Provides built-in periodic health reporting for IoT devices.
    /// <para>
    /// Publishes a heartbeat message at a configurable interval containing:
    /// <list type="bullet">
    ///   <item>Uptime (seconds since start)</item>
    ///   <item>Free memory (bytes)</item>
    ///   <item>Total published message count</item>
    ///   <item>Total received message count</item>
    ///   <item>Connection drop count</item>
    ///   <item>Reconnection count</item>
    ///   <item>Current connection status</item>
    ///   <item>Report sequence number</item>
    ///   <item>Custom metrics (via <see cref="HealthReportPublishing"/> event)</item>
    /// </list>
    /// </para>
    /// </summary>
    public class HealthReporter : IDisposable
    {
        private readonly string _deviceId;
        private readonly string _healthTopic;
        private readonly int _intervalMs;
        private readonly ILogger _logger;
        private Thread _reportThread;
        private bool _isRunning;
        private bool _disposed;
        private readonly DateTime _startTime;
        private int _sequenceNumber;

        // Pre-allocated hashtable reused across BuildHealthReport() calls to reduce GC pressure.
        // Access serialized by _buildLock so that direct calls to BuildHealthReport() are safe
        // even if the background reporter thread is also active.
        private readonly Hashtable _healthData;
        private readonly object _buildLock = new object();

        // Counters — incremented from multiple threads; use Interlocked for correctness.
        private int _publishedCount;
        private int _receivedCount;
        private int _connectionDropCount;
        private int _reconnectionCount;
        private bool _isConnected;

        /// <summary>
        /// Fired just before a health report is published.
        /// Subscribe to add custom metrics to the health data.
        /// </summary>
        public event HealthReportEventHandler HealthReportPublishing;

        /// <summary>
        /// Gets the MQTT topic used for health reports.
        /// </summary>
        public string HealthTopic => _healthTopic;

        /// <summary>
        /// Gets the reporting interval in milliseconds.
        /// </summary>
        public int IntervalMs => _intervalMs;

        /// <summary>
        /// Gets whether the health reporter is currently running.
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Gets the uptime in seconds since the reporter was created.
        /// </summary>
        public long UptimeSeconds
        {
            get
            {
                TimeSpan elapsed = DateTime.UtcNow - _startTime;
                return (long)elapsed.TotalSeconds;
            }
        }

        /// <summary>
        /// Gets the total number of messages published (tracked via <see cref="IncrementPublished"/>).
        /// </summary>
        public int PublishedCount => _publishedCount;

        /// <summary>
        /// Gets the total number of messages received (tracked via <see cref="IncrementReceived"/>).
        /// </summary>
        public int ReceivedCount => _receivedCount;

        /// <summary>
        /// Gets the total number of connection drops.
        /// </summary>
        public int ConnectionDropCount => _connectionDropCount;

        /// <summary>
        /// Gets the total number of successful reconnections.
        /// </summary>
        public int ReconnectionCount => _reconnectionCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthReporter"/> class.
        /// </summary>
        /// <param name="deviceId">The device client ID.</param>
        /// <param name="intervalMs">Reporting interval in milliseconds. Default is 60000 (60 seconds).</param>
        /// <param name="topicPrefix">Topic prefix. Default is "devices".</param>
        /// <param name="logger">Optional logger for health reporting diagnostics.</param>
        public HealthReporter(string deviceId, int intervalMs = 60000, string topicPrefix = "devices", ILogger logger = null)
        {
            if (deviceId == null || deviceId.Length == 0)
            {
                throw new ArgumentException("deviceId cannot be null or empty.");
            }

            if (intervalMs < 5000)
            {
                throw new ArgumentException("Health report interval must be at least 5000ms (5 seconds).");
            }

            _deviceId = deviceId;
            _intervalMs = intervalMs;
            _healthTopic = (topicPrefix ?? "devices") + "/" + deviceId + "/health";
            _startTime = DateTime.UtcNow;
            _sequenceNumber = 0;
            _publishedCount = 0;
            _receivedCount = 0;
            _connectionDropCount = 0;
            _reconnectionCount = 0;
            _isConnected = false;
            _isRunning = false;
            _disposed = false;
            _logger = logger;
            _healthData = new Hashtable();
        }

        /// <summary>
        /// Builds a health report JSON payload. Can be called manually
        /// without starting the background reporter.
        /// </summary>
        /// <remarks>
        /// This method is thread-safe; calls are serialized by an internal lock.
        /// The <see cref="HealthReportEventArgs.HealthData"/> hashtable passed to
        /// <see cref="HealthReportPublishing"/> handlers is the shared pre-allocated instance.
        /// Handlers may add custom key-value pairs to it, but must not retain a reference
        /// to it beyond the handler scope — it is cleared at the start of the next call.
        /// </remarks>
        /// <returns>JSON string containing the health report.</returns>
        public string BuildHealthReport()
        {
            lock (_buildLock)
            {
                _sequenceNumber++;

                long freeMemory = 0;

                try
                {
                    // nanoFramework.Runtime.Native.GC.Run(false) returns available memory
                    freeMemory = nanoFramework.Runtime.Native.GC.Run(false);
                }
                catch
                {
                    // GC.Run may not be available on all platforms
                    freeMemory = -1;
                }

                // Reuse pre-allocated hashtable to avoid per-report heap allocation.
                _healthData.Clear();
                _healthData["deviceId"] = _deviceId;
                _healthData["uptimeSeconds"] = UptimeSeconds;
                _healthData["freeMemoryBytes"] = freeMemory;
                _healthData["publishedMessages"] = _publishedCount;
                _healthData["receivedMessages"] = _receivedCount;
                _healthData["connectionDrops"] = _connectionDropCount;
                _healthData["reconnections"] = _reconnectionCount;
                _healthData["isConnected"] = _isConnected;
                _healthData["sequenceNumber"] = _sequenceNumber;
                _healthData["timestamp"] = DateTime.UtcNow.ToString("o");

                // Allow application to add custom metrics.
                // Handlers must not retain a reference to HealthData beyond this call.
                HealthReportPublishing?.Invoke(this, new HealthReportEventArgs(_healthData, _sequenceNumber));

                return JsonConvert.SerializeObject(_healthData);
            }
        }

        /// <summary>
        /// Starts the background health reporting thread.
        /// The caller must provide a publish action to send the report via MQTT.
        /// </summary>
        /// <param name="publishAction">
        /// Action to call with (topic, jsonPayload) when a report is ready.
        /// Typically wired to <c>EventGridMqttClient.Publish</c>.
        /// </param>
        public void Start(PublishHealthAction publishAction)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("HealthReporter");
            }

            if (_isRunning)
            {
                _logger?.LogInfo("Health Reporter is already running.");
                return;
            }

            if (publishAction == null)
            {
                throw new ArgumentNullException("publishAction");
            }

            _isRunning = true;

            _reportThread = new Thread(() => ReportLoop(publishAction));
            _reportThread.Priority = ThreadPriority.BelowNormal;
            _reportThread.Start();

            _logger?.LogInfo("Health Reporter started: every " + (_intervalMs / 1000) + "s on '" + _healthTopic + "'");
        }

        /// <summary>
        /// Stops the background health reporting thread. Wakes the thread immediately.
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _reportThread?.Interrupt();
            _logger?.LogInfo("Health Reporter stopped.");
        }

        /// <summary>
        /// Increments the published message counter. Thread-safe.
        /// </summary>
        public void IncrementPublished()
        {
            Interlocked.Increment(ref _publishedCount);
        }

        /// <summary>
        /// Increments the received message counter. Thread-safe.
        /// </summary>
        public void IncrementReceived()
        {
            Interlocked.Increment(ref _receivedCount);
        }

        /// <summary>
        /// Records a connection drop event. Thread-safe.
        /// </summary>
        public void RecordConnectionDrop()
        {
            Interlocked.Increment(ref _connectionDropCount);
            _isConnected = false;
        }

        /// <summary>
        /// Records a successful reconnection. Thread-safe.
        /// </summary>
        public void RecordReconnection()
        {
            Interlocked.Increment(ref _reconnectionCount);
            _isConnected = true;
        }

        /// <summary>
        /// Updates the connection status.
        /// </summary>
        /// <param name="connected">Whether the device is currently connected.</param>
        public void UpdateConnectionStatus(bool connected)
        {
            _isConnected = connected;
        }

        /// <summary>
        /// Releases all resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Stop();
        }

        #region Private Methods

        private void ReportLoop(PublishHealthAction publishAction)
        {
            while (_isRunning && !_disposed)
            {
                try
                {
                    Thread.Sleep(_intervalMs);
                }
                catch (ThreadInterruptedException)
                {
                    return;
                }

                if (!_isRunning || _disposed)
                {
                    break;
                }

                try
                {
                    if (!_isConnected)
                    {
                        _logger?.LogInfo("Health: skipping report — not connected.");
                        continue;
                    }

                    string report = BuildHealthReport();
                    publishAction(_healthTopic, report);

                    _logger?.LogInfo("Health report #" + _sequenceNumber + " published.");
                }
                catch (Exception ex)
                {
                    _logger?.LogError("Health report failed: " + ex.Message);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Delegate for publishing health reports via MQTT.
    /// </summary>
    /// <param name="topic">The MQTT topic to publish to.</param>
    /// <param name="jsonPayload">The JSON payload to publish.</param>
    public delegate void PublishHealthAction(string topic, string jsonPayload);
}
