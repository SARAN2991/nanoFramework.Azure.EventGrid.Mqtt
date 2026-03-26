// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using nanoFramework.M2Mqtt.Messages;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Provides retry logic with exponential backoff for MQTT publish operations
    /// and other transient failure scenarios on constrained ESP32 devices.
    /// </summary>
    /// <remarks>
    /// The retry handler uses a simple exponential backoff algorithm:
    /// <code>
    /// delay = baseDelay * 2^(attempt-1), capped at maxDelay
    /// </code>
    /// A small jitter is added to prevent thundering herd when multiple devices retry simultaneously.
    /// </remarks>
    public class RetryHandler
    {
        private readonly int _maxRetries;
        private readonly int _baseDelayMs;
        private readonly int _maxDelayMs;
        private readonly ILogger _logger;
        private static int _jitterSeed;

        /// <summary>
        /// Gets the total number of retries attempted across all operations since creation.
        /// </summary>
        public int TotalRetries { get; private set; }

        /// <summary>
        /// Gets the total number of operations that failed after exhausting all retries.
        /// </summary>
        public int TotalFailures { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RetryHandler"/> class.
        /// </summary>
        /// <param name="maxRetries">Maximum number of retry attempts. Default is 3.</param>
        /// <param name="baseDelayMs">Base delay in milliseconds before first retry. Default is 1000ms.</param>
        /// <param name="maxDelayMs">Maximum delay cap in milliseconds. Default is 30000ms.</param>
        /// <param name="logger">Optional logger for retry diagnostics.</param>
        public RetryHandler(int maxRetries = 3, int baseDelayMs = 1000, int maxDelayMs = 30000, ILogger logger = null)
        {
            _maxRetries = maxRetries < 0 ? 0 : maxRetries;
            _baseDelayMs = baseDelayMs < 100 ? 100 : baseDelayMs;
            _maxDelayMs = maxDelayMs < _baseDelayMs ? _baseDelayMs : maxDelayMs;
            _logger = logger;
            TotalRetries = 0;
            TotalFailures = 0;
        }

        /// <summary>
        /// Executes an action with retry logic and exponential backoff.
        /// Returns true if the action succeeds within the allowed attempts.
        /// </summary>
        /// <param name="action">The action to execute. Must return true on success, false on failure.</param>
        /// <param name="operationName">A descriptive name for logging (e.g., "Publish telemetry").</param>
        /// <returns>True if the action succeeded within allowed retries, false if all retries exhausted.</returns>
        public bool ExecuteWithRetry(RetryAction action, string operationName)
        {
            if (action == null)
            {
                return false;
            }

            // First attempt (not a retry)
            try
            {
                if (action.Invoke())
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("" + operationName + " failed: " + ex.Message);
            }

            // Retry loop
            int delay = _baseDelayMs;

            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                TotalRetries++;

                // Add small jitter (0-25% of delay) to avoid thundering herd
                int jitter = GetJitter(delay / 4);
                int actualDelay = delay + jitter;

                _logger?.LogInfo("Retry " + attempt + "/" + _maxRetries + " for " + operationName + " in " + actualDelay + "ms");

                Thread.Sleep(actualDelay);

                try
                {
                    if (action.Invoke())
                    {
                        _logger?.LogInfo("" + operationName + " succeeded on retry " + attempt);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning("" + operationName + " retry " + attempt + " failed: " + ex.Message);
                }

                // Exponential backoff: double the delay, cap at max
                delay = delay * 2;

                if (delay > _maxDelayMs)
                {
                    delay = _maxDelayMs;
                }
            }

            TotalFailures++;
            _logger?.LogError("" + operationName + " failed after " + _maxRetries + " retries.");
            return false;
        }

        /// <summary>
        /// Executes a publish operation with retry logic and exponential backoff.
        /// Unlike <see cref="ExecuteWithRetry"/>, this overload accepts publish parameters
        /// directly to avoid per-call closure/delegate allocation on the embedded heap.
        /// </summary>
        /// <param name="action">The pre-allocated publish delegate (typically <c>transport.Publish</c>).</param>
        /// <param name="topic">The MQTT topic to publish to.</param>
        /// <param name="payload">The byte payload to publish.</param>
        /// <param name="qos">QoS level for the publish.</param>
        /// <param name="retain">Retain flag for the publish.</param>
        /// <param name="messageId">Receives the message ID returned by the transport on success, or 0 on failure.</param>
        /// <returns>True if the publish succeeded within allowed retries, false if all retries exhausted.</returns>
        public bool ExecutePublishWithRetry(
            DirectPublishAction action,
            string topic,
            byte[] payload,
            MqttQoSLevel qos,
            bool retain,
            out ushort messageId)
        {
            messageId = 0;

            if (action == null)
            {
                return false;
            }

            // First attempt (not a retry)
            try
            {
                messageId = action(topic, payload, qos, retain);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Publish to '" + topic + "' failed: " + ex.Message);
            }

            // Retry loop
            int delay = _baseDelayMs;

            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                TotalRetries++;

                int jitter = GetJitter(delay / 4);

                _logger?.LogInfo("Publish retry " + attempt + "/" + _maxRetries + " for '" + topic + "' in " + (delay + jitter) + "ms");

                Thread.Sleep(delay + jitter);

                try
                {
                    messageId = action(topic, payload, qos, retain);
                    _logger?.LogInfo("Publish to '" + topic + "' succeeded on retry " + attempt);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning("Publish retry " + attempt + " for '" + topic + "' failed: " + ex.Message);
                }

                delay = delay * 2;

                if (delay > _maxDelayMs)
                {
                    delay = _maxDelayMs;
                }
            }

            TotalFailures++;
            _logger?.LogError("Publish to '" + topic + "' failed after " + _maxRetries + " retries.");
            return false;
        }

        /// <summary>
        /// Calculates the delay in milliseconds for a specific retry attempt.
        /// Uses exponential backoff: baseDelay * 2^(attempt-1), capped at maxDelay.
        /// </summary>
        /// <param name="attempt">The retry attempt number (1-based).</param>
        /// <returns>Delay in milliseconds.</returns>
        public int CalculateDelay(int attempt)
        {
            if (attempt <= 0)
            {
                return _baseDelayMs;
            }

            int delay = _baseDelayMs;

            for (int i = 1; i < attempt; i++)
            {
                delay = delay * 2;

                if (delay > _maxDelayMs)
                {
                    return _maxDelayMs;
                }
            }

            return delay;
        }

        /// <summary>
        /// Resets the retry statistics counters.
        /// </summary>
        public void ResetStatistics()
        {
            TotalRetries = 0;
            TotalFailures = 0;
        }

        private static int GetJitter(int maxJitter)
        {
            if (maxJitter <= 0)
            {
                return 0;
            }

            // Simple pseudo-random jitter using tick count
            // Avoids allocating a Random object on constrained devices
            _jitterSeed = (_jitterSeed + Environment.TickCount) & 0x7FFFFFFF;
            return _jitterSeed % maxJitter;
        }
    }

    /// <summary>
    /// Delegate for retry-able actions. Must return true if the operation succeeded.
    /// </summary>
    /// <returns>True if the operation succeeded, false otherwise.</returns>
    public delegate bool RetryAction();

    /// <summary>
    /// Delegate for a direct MQTT publish operation used by <see cref="RetryHandler.ExecutePublishWithRetry"/>.
    /// Accepts publish parameters directly to avoid closure allocation on each publish call.
    /// </summary>
    /// <param name="topic">The MQTT topic.</param>
    /// <param name="payload">The byte payload.</param>
    /// <param name="qos">QoS level.</param>
    /// <param name="retain">Retain flag.</param>
    /// <returns>The message ID assigned by the transport.</returns>
    public delegate ushort DirectPublishAction(string topic, byte[] payload, MqttQoSLevel qos, bool retain);
}
