// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Helper class for constructing and validating MQTT topics
    /// compatible with Azure EventGrid Namespace topic spaces.
    /// </summary>
    public static class TopicHelper
    {
        /// <summary>
        /// Maximum allowed topic length for EventGrid MQTT broker.
        /// </summary>
        public const int MaxTopicLength = 256;

        /// <summary>
        /// Builds a publish topic by replacing {deviceId} placeholder with the actual device ID.
        /// </summary>
        /// <param name="topicTemplate">Topic template string (e.g., "devices/{deviceId}/telemetry").</param>
        /// <param name="deviceId">The device client ID to substitute.</param>
        /// <returns>The fully resolved topic string.</returns>
        /// <example>
        /// <code>
        /// string topic = TopicHelper.BuildTopic("devices/{deviceId}/telemetry", "myDevice");
        /// // Returns: "devices/myDevice/telemetry"
        /// </code>
        /// </example>
        public static string BuildTopic(string topicTemplate, string deviceId)
        {
            if (topicTemplate == null || topicTemplate.Length == 0)
            {
                throw new ArgumentException("Topic template cannot be null or empty.");
            }

            if (deviceId == null || deviceId.Length == 0)
            {
                throw new ArgumentException("Device ID cannot be null or empty.");
            }

            // Replace {deviceId} placeholder
            string result = topicTemplate;
            int placeholderIndex = result.IndexOf("{deviceId}");

            if (placeholderIndex >= 0)
            {
                result = result.Substring(0, placeholderIndex) + deviceId + result.Substring(placeholderIndex + 10);
            }

            // Replace ${client.name} placeholder (EventGrid native format)
            placeholderIndex = result.IndexOf("${client.name}");

            if (placeholderIndex >= 0)
            {
                result = result.Substring(0, placeholderIndex) + deviceId + result.Substring(placeholderIndex + 14);
            }

            return result;
        }

        /// <summary>
        /// Builds a wildcard subscription topic by appending /# to the base topic.
        /// </summary>
        /// <param name="baseTopic">The base topic path (e.g., "devices/myDevice/commands").</param>
        /// <returns>The topic with multi-level wildcard appended (e.g., "devices/myDevice/commands/#").</returns>
        public static string BuildWildcardTopic(string baseTopic)
        {
            if (baseTopic == null || baseTopic.Length == 0)
            {
                throw new ArgumentException("Base topic cannot be null or empty.");
            }

            // Remove trailing slash if present
            if (baseTopic[baseTopic.Length - 1] == '/')
            {
                baseTopic = baseTopic.Substring(0, baseTopic.Length - 1);
            }

            return baseTopic + "/#";
        }

        /// <summary>
        /// Builds a single-level wildcard subscription topic by appending /+ to the base topic.
        /// </summary>
        /// <param name="baseTopic">The base topic path.</param>
        /// <returns>The topic with single-level wildcard appended.</returns>
        public static string BuildSingleLevelWildcardTopic(string baseTopic)
        {
            if (baseTopic == null || baseTopic.Length == 0)
            {
                throw new ArgumentException("Base topic cannot be null or empty.");
            }

            if (baseTopic[baseTopic.Length - 1] == '/')
            {
                baseTopic = baseTopic.Substring(0, baseTopic.Length - 1);
            }

            return baseTopic + "/+";
        }

        /// <summary>
        /// Validates that a topic string conforms to EventGrid MQTT broker constraints.
        /// </summary>
        /// <param name="topic">The topic string to validate.</param>
        /// <returns>True if the topic is valid, false otherwise.</returns>
        /// <remarks>
        /// EventGrid MQTT topics must:
        /// - Not be null or empty
        /// - Not exceed <see cref="MaxTopicLength"/> characters
        /// - Not start with '$' (reserved for system topics)
        /// - Not contain null characters
        /// </remarks>
        public static bool ValidateTopic(string topic)
        {
            if (topic == null || topic.Length == 0)
            {
                return false;
            }

            if (topic.Length > MaxTopicLength)
            {
                return false;
            }

            // Topics starting with $ are reserved by the broker
            if (topic[0] == '$')
            {
                return false;
            }

            // Check for null characters
            if (topic.IndexOf('\0') >= 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates that the EventGrid MQTT broker hostname is correctly formatted.
        /// The hostname must contain '.ts.' to indicate the MQTT endpoint (as opposed to the HTTP endpoint).
        /// </summary>
        /// <param name="hostname">The broker hostname to validate.</param>
        /// <returns>True if the hostname appears to be a valid EventGrid MQTT hostname.</returns>
        public static bool ValidateBrokerHostname(string hostname)
        {
            if (hostname == null || hostname.Length == 0)
            {
                return false;
            }

            // Must contain .ts. to be the MQTT endpoint
            if (hostname.IndexOf(".ts.") < 0)
            {
                return false;
            }

            // Must end with eventgrid.azure.net
            if (hostname.IndexOf("eventgrid.azure.net") < 0)
            {
                return false;
            }

            return true;
        }
    }
}
