// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Defines the logging contract for the EventGrid MQTT library.
    /// Implement this interface to integrate with your preferred logging framework.
    /// </summary>
    /// <remarks>
    /// The default implementation (<see cref="DebugLogger"/>) writes to
    /// <see cref="Debug.WriteLine(string)"/>. To use a custom logger, set
    /// <see cref="EventGridMqttConfig.Logger"/> before creating the client.
    /// </remarks>
    public interface ILogger
    {
        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogInfo(string message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        void LogWarning(string message);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        void LogError(string message);
    }

    /// <summary>
    /// Default logger implementation that writes to <see cref="Debug.WriteLine(string)"/>.
    /// Suitable for development and debugging on nanoFramework devices.
    /// </summary>
    public class DebugLogger : ILogger
    {
        private readonly string _prefix;

        /// <summary>
        /// Initializes a new instance of the <see cref="DebugLogger"/> class.
        /// </summary>
        /// <param name="prefix">Optional prefix prepended to all messages. Default is "EventGridMqtt".</param>
        public DebugLogger(string prefix = "EventGridMqtt")
        {
            _prefix = prefix ?? "EventGridMqtt";
        }

        /// <inheritdoc />
        public void LogInfo(string message)
        {
            Debug.WriteLine($"[{_prefix}] {message}");
        }

        /// <inheritdoc />
        public void LogWarning(string message)
        {
            Debug.WriteLine($"[{_prefix}] WARN: {message}");
        }

        /// <inheritdoc />
        public void LogError(string message)
        {
            Debug.WriteLine($"[{_prefix}] ERROR: {message}");
        }
    }

    /// <summary>
    /// A no-op logger implementation that discards all messages.
    /// Use this to suppress all library logging output.
    /// </summary>
    public class NullLogger : ILogger
    {
        /// <inheritdoc />
        public void LogInfo(string message) { }

        /// <inheritdoc />
        public void LogWarning(string message) { }

        /// <inheritdoc />
        public void LogError(string message) { }
    }
}
