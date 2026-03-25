// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Delegate for reconnection events.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">Event data.</param>
    public delegate void ReconnectEventHandler(object sender, ConnectionStateChangedEventArgs e);

    /// <summary>
    /// Manages automatic reconnection to the Azure EventGrid MQTT broker
    /// with exponential backoff strategy.
    /// </summary>
    internal class ConnectionManager
    {
        private readonly EventGridMqttConfig _config;
        private Thread _reconnectThread;
        private bool _isRunning;
        private readonly object _lock = new object();
        private int _currentAttempt;

        /// <summary>
        /// Fired when a reconnection attempt succeeds.
        /// </summary>
        internal event ReconnectEventHandler Reconnected;

        /// <summary>
        /// Fired when a reconnection attempt fails.
        /// </summary>
        internal event ReconnectEventHandler ReconnectAttemptFailed;

        /// <summary>
        /// Fired when all reconnection attempts are exhausted.
        /// </summary>
        internal event ReconnectEventHandler ReconnectFailed;

        /// <summary>
        /// Gets or sets the function to call to attempt a reconnection.
        /// Must return true if reconnection was successful, false otherwise.
        /// </summary>
        internal ReconnectAction TryReconnect { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionManager"/> class.
        /// </summary>
        /// <param name="config">The EventGrid MQTT configuration containing reconnect settings.</param>
        public ConnectionManager(EventGridMqttConfig config)
        {
            _config = config;
            _isRunning = false;
            _currentAttempt = 0;
        }

        /// <summary>
        /// Starts the automatic reconnection process in a background thread.
        /// Called when a connection loss is detected.
        /// </summary>
        internal void StartReconnecting()
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    return;
                }

                _isRunning = true;
                _currentAttempt = 0;
            }

            _reconnectThread = new Thread(ReconnectLoop);
            _reconnectThread.Start();
        }

        /// <summary>
        /// Stops the reconnection process.
        /// </summary>
        internal void Stop()
        {
            lock (_lock)
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// Gets whether the reconnection process is currently running.
        /// </summary>
        internal bool IsReconnecting
        {
            get
            {
                lock (_lock)
                {
                    return _isRunning;
                }
            }
        }

        private void ReconnectLoop()
        {
            int currentDelay = _config.ReconnectDelayMs;

            while (true)
            {
                lock (_lock)
                {
                    if (!_isRunning)
                    {
                        return;
                    }
                }

                _currentAttempt++;

                // Check if max attempts exceeded
                if (_config.MaxReconnectAttempts > 0 && _currentAttempt > _config.MaxReconnectAttempts)
                {
                    Debug.WriteLine($"[EventGridMqtt] Max reconnect attempts ({_config.MaxReconnectAttempts}) reached.");

                    ReconnectFailed?.Invoke(this, new ConnectionStateChangedEventArgs(
                        false,
                        $"Max reconnect attempts ({_config.MaxReconnectAttempts}) exhausted.",
                        _currentAttempt));

                    lock (_lock)
                    {
                        _isRunning = false;
                    }

                    return;
                }

                Debug.WriteLine($"[EventGridMqtt] Reconnect attempt {_currentAttempt}, waiting {currentDelay}ms...");

                // Wait before attempting
                Thread.Sleep(currentDelay);

                // Check again after sleep
                lock (_lock)
                {
                    if (!_isRunning)
                    {
                        return;
                    }
                }

                // Attempt reconnection
                bool success = false;

                try
                {
                    if (TryReconnect != null)
                    {
                        success = TryReconnect.Invoke();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[EventGridMqtt] Reconnect attempt {_currentAttempt} threw: {ex.Message}");
                    success = false;
                }

                if (success)
                {
                    Debug.WriteLine($"[EventGridMqtt] Reconnected successfully after {_currentAttempt} attempt(s).");

                    Reconnected?.Invoke(this, new ConnectionStateChangedEventArgs(
                        true,
                        $"Reconnected after {_currentAttempt} attempt(s).",
                        _currentAttempt));

                    lock (_lock)
                    {
                        _isRunning = false;
                    }

                    return;
                }
                else
                {
                    Debug.WriteLine($"[EventGridMqtt] Reconnect attempt {_currentAttempt} failed.");

                    ReconnectAttemptFailed?.Invoke(this, new ConnectionStateChangedEventArgs(
                        false,
                        $"Reconnect attempt {_currentAttempt} failed.",
                        _currentAttempt));

                    // Exponential backoff: double the delay, cap at max
                    currentDelay = currentDelay * 2;

                    if (currentDelay > _config.MaxReconnectDelayMs)
                    {
                        currentDelay = _config.MaxReconnectDelayMs;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Delegate for the reconnection action.
    /// </summary>
    /// <returns>True if reconnection succeeded, false otherwise.</returns>
    internal delegate bool ReconnectAction();
}
