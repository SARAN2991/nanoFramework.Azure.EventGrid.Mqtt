// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using nanoFramework.M2Mqtt.Messages;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Defines the public API for an Azure Event Grid MQTT client.
    /// Implementations handle connection, publishing, subscribing, and lifecycle management.
    /// </summary>
    /// <remarks>
    /// Use this interface for dependency injection, testing, or to create custom client implementations.
    /// The default implementation is <see cref="EventGridMqttClient"/>.
    /// </remarks>
    public interface IEventGridMqttClient
    {
        /// <summary>
        /// Gets whether the client is currently connected to the MQTT broker.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets whether the client is currently attempting to reconnect.
        /// </summary>
        bool IsReconnecting { get; }

        /// <summary>
        /// Gets the device client ID used for this connection.
        /// </summary>
        string DeviceClientId { get; }

        /// <summary>
        /// Fired when a message is received on a subscribed topic.
        /// </summary>
        event MessageReceivedEventHandler MessageReceived;

        /// <summary>
        /// Fired when the connection state changes (connected, disconnected, reconnecting).
        /// </summary>
        event ConnectionStateChangedEventHandler ConnectionStateChanged;

        /// <summary>
        /// Fired when a published message is confirmed by the broker (QoS 1 only).
        /// </summary>
        event MessagePublishedEventHandler MessagePublished;

        /// <summary>
        /// Connects to the Azure Event Grid MQTT broker.
        /// </summary>
        /// <returns>The MQTT reason code indicating the result.</returns>
        MqttReasonCode Connect();

        /// <summary>
        /// Disconnects from the MQTT broker gracefully.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Publishes a string payload to an MQTT topic.
        /// </summary>
        /// <param name="topic">The topic to publish to.</param>
        /// <param name="payload">The string payload (typically JSON).</param>
        /// <param name="qos">QoS level. Default is AtMostOnce (QoS 0).</param>
        /// <param name="retain">Whether to set the retain flag. Default is false.</param>
        /// <returns>The message ID (0 for QoS 0).</returns>
        ushort Publish(string topic, string payload, MqttQoSLevel qos = MqttQoSLevel.AtMostOnce, bool retain = false);

        /// <summary>
        /// Serializes an object to JSON and publishes it to an MQTT topic.
        /// </summary>
        /// <param name="topic">The topic to publish to.</param>
        /// <param name="data">The object to serialize as JSON.</param>
        /// <param name="qos">QoS level. Default is AtMostOnce (QoS 0).</param>
        /// <param name="retain">Whether to set the retain flag. Default is false.</param>
        /// <returns>The message ID (0 for QoS 0).</returns>
        ushort PublishJson(string topic, object data, MqttQoSLevel qos = MqttQoSLevel.AtMostOnce, bool retain = false);

        /// <summary>
        /// Publishes raw byte array payload to an MQTT topic.
        /// </summary>
        /// <param name="topic">The topic to publish to.</param>
        /// <param name="payload">The raw byte payload.</param>
        /// <param name="qos">QoS level. Default is AtMostOnce (QoS 0).</param>
        /// <param name="retain">Whether to set the retain flag. Default is false.</param>
        /// <returns>The message ID (0 for QoS 0).</returns>
        ushort PublishRaw(string topic, byte[] payload, MqttQoSLevel qos = MqttQoSLevel.AtMostOnce, bool retain = false);

        /// <summary>
        /// Subscribes to an MQTT topic.
        /// </summary>
        /// <param name="topic">The topic to subscribe to. Can include wildcards (# or +).</param>
        /// <param name="qos">The QoS level for the subscription. Default is AtLeastOnce (QoS 1).</param>
        void Subscribe(string topic, MqttQoSLevel qos = MqttQoSLevel.AtLeastOnce);

        /// <summary>
        /// Unsubscribes from an MQTT topic.
        /// </summary>
        /// <param name="topic">The topic to unsubscribe from.</param>
        void Unsubscribe(string topic);
    }
}
