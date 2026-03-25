// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Provides data for the <see cref="EventGridMqttClient.MessageReceived"/> event.
    /// Contains the parsed topic and decoded UTF-8 payload from an incoming MQTT message.
    /// </summary>
    public class MessageReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the MQTT topic on which the message was received.
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// Gets the UTF-8 decoded payload of the message.
        /// </summary>
        public string Payload { get; }

        /// <summary>
        /// Gets the raw byte array of the message payload.
        /// </summary>
        public byte[] RawPayload { get; }

        /// <summary>
        /// Gets the QoS level of the received message.
        /// </summary>
        public byte QosLevel { get; }

        /// <summary>
        /// Gets whether the message has the retain flag set.
        /// </summary>
        public bool Retain { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="topic">The MQTT topic.</param>
        /// <param name="payload">The decoded string payload.</param>
        /// <param name="rawPayload">The raw byte payload.</param>
        /// <param name="qosLevel">The QoS level.</param>
        /// <param name="retain">Whether the retain flag is set.</param>
        public MessageReceivedEventArgs(string topic, string payload, byte[] rawPayload, byte qosLevel, bool retain)
        {
            Topic = topic;
            Payload = payload;
            RawPayload = rawPayload;
            QosLevel = qosLevel;
            Retain = retain;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="EventGridMqttClient.ConnectionStateChanged"/> event.
    /// </summary>
    public class ConnectionStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets whether the client is currently connected.
        /// </summary>
        public bool IsConnected { get; }

        /// <summary>
        /// Gets the reason for the state change.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Gets the number of reconnect attempts made (0 if initial connection or clean disconnect).
        /// </summary>
        public int ReconnectAttempt { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionStateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="isConnected">Whether connected.</param>
        /// <param name="reason">Description of the state change.</param>
        /// <param name="reconnectAttempt">Current reconnect attempt count.</param>
        public ConnectionStateChangedEventArgs(bool isConnected, string reason, int reconnectAttempt = 0)
        {
            IsConnected = isConnected;
            Reason = reason;
            ReconnectAttempt = reconnectAttempt;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="EventGridMqttClient.MessagePublished"/> event.
    /// </summary>
    public class MessagePublishedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the MQTT message ID.
        /// </summary>
        public ushort MessageId { get; }

        /// <summary>
        /// Gets whether the message was successfully published.
        /// </summary>
        public bool IsPublished { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagePublishedEventArgs"/> class.
        /// </summary>
        /// <param name="messageId">The message ID.</param>
        /// <param name="isPublished">Whether the publish was successful.</param>
        public MessagePublishedEventArgs(ushort messageId, bool isPublished)
        {
            MessageId = messageId;
            IsPublished = isPublished;
        }
    }
}
