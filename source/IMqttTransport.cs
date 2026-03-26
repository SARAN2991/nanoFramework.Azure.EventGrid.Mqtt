// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using nanoFramework.M2Mqtt.Messages;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Delegate for transport-level message received events.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="topic">The MQTT topic.</param>
    /// <param name="payload">The raw byte payload.</param>
    /// <param name="qos">The QoS level.</param>
    /// <param name="retain">Whether the retain flag is set.</param>
    public delegate void TransportMessageReceivedHandler(object sender, string topic, byte[] payload, byte qos, bool retain);

    /// <summary>
    /// Delegate for transport-level message published confirmation events.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="messageId">The message ID.</param>
    /// <param name="isPublished">Whether the publish was acknowledged.</param>
    public delegate void TransportMessagePublishedHandler(object sender, ushort messageId, bool isPublished);

    /// <summary>
    /// Delegate for transport-level connection closed events.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    public delegate void TransportConnectionClosedHandler(object sender);

    /// <summary>
    /// Abstracts the low-level MQTT transport layer from higher-level Event Grid logic.
    /// <para>
    /// This separation of concerns decouples raw MQTT communication (connect, publish bytes,
    /// subscribe) from Event Grid-specific semantics (topic routing, twin sync, health reports).
    /// </para>
    /// </summary>
    /// <remarks>
    /// Benefits:
    /// <list type="bullet">
    ///   <item>Testability: the transport can be mocked for unit testing</item>
    ///   <item>Swappability: replace M2Mqtt with another MQTT client without changing Event Grid logic</item>
    ///   <item>Modularity: MQTT concerns isolated from Event Grid concerns</item>
    /// </list>
    /// </remarks>
    public interface IMqttTransport
    {
        /// <summary>
        /// Gets whether the transport is currently connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Connects to the MQTT broker.
        /// </summary>
        /// <param name="clientId">The MQTT client ID.</param>
        /// <param name="cleanSession">Whether to use a clean session.</param>
        /// <param name="keepAliveSeconds">Keep-alive period in seconds.</param>
        /// <param name="lwtTopic">Optional Last Will topic. Null to disable.</param>
        /// <param name="lwtMessage">Optional Last Will message.</param>
        /// <param name="lwtQos">LWT QoS level.</param>
        /// <param name="lwtRetain">Whether to retain the LWT message.</param>
        /// <returns>The MQTT reason code indicating the result.</returns>
        MqttReasonCode Connect(
            string clientId,
            bool cleanSession,
            ushort keepAliveSeconds,
            string lwtTopic = null,
            string lwtMessage = null,
            MqttQoSLevel lwtQos = MqttQoSLevel.AtMostOnce,
            bool lwtRetain = false);

        /// <summary>
        /// Disconnects from the MQTT broker.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Publishes a raw byte payload to a topic.
        /// </summary>
        /// <param name="topic">The MQTT topic.</param>
        /// <param name="payload">The byte payload.</param>
        /// <param name="qos">QoS level.</param>
        /// <param name="retain">Whether to retain.</param>
        /// <returns>The message ID (0 for QoS 0).</returns>
        ushort Publish(string topic, byte[] payload, MqttQoSLevel qos, bool retain);

        /// <summary>
        /// Subscribes to one or more MQTT topics.
        /// </summary>
        /// <param name="topics">Array of topic strings.</param>
        /// <param name="qosLevels">Array of QoS levels corresponding to topics.</param>
        void Subscribe(string[] topics, MqttQoSLevel[] qosLevels);

        /// <summary>
        /// Unsubscribes from one or more MQTT topics.
        /// </summary>
        /// <param name="topics">Array of topic strings.</param>
        void Unsubscribe(string[] topics);

        /// <summary>
        /// Fired when a message is received from the broker.
        /// </summary>
        event TransportMessageReceivedHandler MessageReceived;

        /// <summary>
        /// Fired when a published message is confirmed by the broker.
        /// </summary>
        event TransportMessagePublishedHandler MessagePublished;

        /// <summary>
        /// Fired when the connection to the broker is closed.
        /// </summary>
        event TransportConnectionClosedHandler ConnectionClosed;
    }
}
