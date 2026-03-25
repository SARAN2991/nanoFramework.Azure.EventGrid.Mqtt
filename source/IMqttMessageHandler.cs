// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Interface for feature modules that process incoming MQTT messages.
    /// <para>
    /// Implementing this interface allows feature managers (e.g., Device Twin, Certificate Rotation)
    /// to be registered with the <see cref="EventGridMqttClient"/> and automatically receive
    /// messages on their subscribed topics.
    /// </para>
    /// </summary>
    /// <remarks>
    /// This interface enables a modular, extensible architecture where additional features
    /// can be added without modifying the core client class. Each handler declares which
    /// topics it needs and processes messages independently.
    /// </remarks>
    public interface IMqttMessageHandler
    {
        /// <summary>
        /// Gets the MQTT topics that this handler needs to subscribe to.
        /// The client will auto-subscribe to these topics after connecting.
        /// </summary>
        /// <returns>Array of topic strings to subscribe to.</returns>
        string[] GetSubscriptionTopics();

        /// <summary>
        /// Processes an incoming MQTT message. The client routes messages to all
        /// registered handlers until one returns true (indicating it handled the message).
        /// </summary>
        /// <param name="topic">The MQTT topic the message was received on.</param>
        /// <param name="payload">The decoded UTF-8 string payload.</param>
        /// <returns>True if the message was handled by this handler, false to pass to the next handler.</returns>
        bool ProcessMessage(string topic, string payload);
    }
}
