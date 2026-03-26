// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Delegate for error events raised by the client.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">Event data containing error details.</param>
    public delegate void ClientErrorEventHandler(object sender, ClientErrorEventArgs e);

    /// <summary>
    /// Categorizes the type of error that occurred.
    /// </summary>
    public enum ErrorCategory
    {
        /// <summary>Connection-related failure (TLS, DNS, socket, authentication).</summary>
        Connection = 0,
        /// <summary>Publish operation failure.</summary>
        Publish = 1,
        /// <summary>Subscribe operation failure.</summary>
        Subscribe = 2,
        /// <summary>Certificate-related error (parsing, expiry, rotation failure).</summary>
        Certificate = 3,
        /// <summary>Network-level failure (Wi-Fi drop, timeout, DNS resolution).</summary>
        Network = 4,
        /// <summary>Internal error in the library.</summary>
        Internal = 5
    }

    /// <summary>
    /// Provides data for the <see cref="EventGridMqttClient.ErrorOccurred"/> event.
    /// <para>
    /// Instead of silently swallowing errors or only logging them, the client now raises
    /// structured error events that applications can handle (retry, alert, degrade gracefully).
    /// </para>
    /// </summary>
    public class ClientErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the error category.
        /// </summary>
        public ErrorCategory Category { get; }

        /// <summary>
        /// Gets the error message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the underlying exception, if available. May be null.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Gets an optional context string (e.g., topic name, operation name).
        /// </summary>
        public string Context { get; }

        /// <summary>
        /// Gets the UTC timestamp when the error occurred.
        /// </summary>
        public DateTime TimestampUtc { get; }

        /// <summary>
        /// Gets whether this error is recoverable (the client can continue operating).
        /// </summary>
        public bool IsRecoverable { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientErrorEventArgs"/> class.
        /// </summary>
        /// <param name="category">The error category.</param>
        /// <param name="message">A human-readable error message.</param>
        /// <param name="exception">The underlying exception, or null.</param>
        /// <param name="context">Optional context like topic or operation name.</param>
        /// <param name="isRecoverable">Whether the client can continue operating.</param>
        public ClientErrorEventArgs(ErrorCategory category, string message, Exception exception = null, string context = null, bool isRecoverable = true)
        {
            Category = category;
            Message = message;
            Exception = exception;
            Context = context;
            IsRecoverable = isRecoverable;
            TimestampUtc = DateTime.UtcNow;
        }
    }
}
