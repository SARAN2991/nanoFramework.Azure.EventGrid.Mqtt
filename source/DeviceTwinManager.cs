// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using nanoFramework.Json;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Delegate for device twin desired state change events.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">Event data containing the desired state.</param>
    public delegate void DesiredStateChangedEventHandler(object sender, DesiredStateChangedEventArgs e);

    /// <summary>
    /// Provides data for the <see cref="DeviceTwinManager.DesiredStateChanged"/> event.
    /// </summary>
    public class DesiredStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the desired state as a JSON string.
        /// </summary>
        public string DesiredStateJson { get; }

        /// <summary>
        /// Gets the property key that changed, if a single property was updated.
        /// Null if the entire state was replaced.
        /// </summary>
        public string PropertyKey { get; }

        /// <summary>
        /// Gets the property value that changed, if a single property was updated.
        /// Null if the entire state was replaced.
        /// </summary>
        public string PropertyValue { get; }

        /// <summary>
        /// Gets the version number of the desired state.
        /// </summary>
        public int Version { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DesiredStateChangedEventArgs"/> class.
        /// </summary>
        public DesiredStateChangedEventArgs(string desiredStateJson, string propertyKey, string propertyValue, int version)
        {
            DesiredStateJson = desiredStateJson;
            PropertyKey = propertyKey;
            PropertyValue = propertyValue;
            Version = version;
        }
    }

    /// <summary>
    /// Manages device twin (shadow state) synchronization via EventGrid MQTT topics.
    /// <para>
    /// Implements a virtual device twin pattern over MQTT, similar to Azure IoT Hub
    /// device twins or AWS IoT Device Shadow. The cloud can set "desired" state,
    /// and the device reports "reported" state — both synced over EventGrid MQTT topics.
    /// </para>
    /// <para>
    /// Topic conventions:
    /// <list type="bullet">
    ///   <item><c>devices/{deviceId}/twin/desired</c> — Cloud → Device: desired state updates</item>
    ///   <item><c>devices/{deviceId}/twin/reported</c> — Device → Cloud: reported state</item>
    ///   <item><c>devices/{deviceId}/twin/get</c> — Device → Cloud: request current desired state</item>
    ///   <item><c>devices/{deviceId}/twin/res</c> — Cloud → Device: response with full desired state</item>
    /// </list>
    /// </para>
    /// </summary>
    public class DeviceTwinManager : IMqttMessageHandler
    {
        private readonly string _deviceId;
        private readonly Hashtable _reportedState;
        private readonly Hashtable _desiredState;
        private readonly object _stateLock = new object();
        private int _reportedVersion;
        private int _desiredVersion;

        // Topic templates
        private readonly string _desiredTopic;
        private readonly string _reportedTopic;
        private readonly string _getTopic;
        private readonly string _responseTopic;

        /// <summary>
        /// Fired when the desired state is updated from the cloud.
        /// </summary>
        public event DesiredStateChangedEventHandler DesiredStateChanged;

        /// <summary>
        /// Gets the current reported state version.
        /// </summary>
        public int ReportedVersion => _reportedVersion;

        /// <summary>
        /// Gets the current desired state version.
        /// </summary>
        public int DesiredVersion => _desiredVersion;

        /// <summary>
        /// Gets the MQTT topic for desired state (subscribe to this).
        /// </summary>
        public string DesiredTopic => _desiredTopic;

        /// <summary>
        /// Gets the MQTT topic for reported state (publish to this).
        /// </summary>
        public string ReportedTopic => _reportedTopic;

        /// <summary>
        /// Gets the MQTT topic for requesting current desired state (publish to this).
        /// </summary>
        public string GetTopic => _getTopic;

        /// <summary>
        /// Gets the MQTT topic for desired state response (subscribe to this).
        /// </summary>
        public string ResponseTopic => _responseTopic;

        /// <summary>
        /// Gets the topic prefix used for twin topics.
        /// </summary>
        public string TopicPrefix { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceTwinManager"/> class.
        /// </summary>
        /// <param name="deviceId">The device client ID.</param>
        /// <param name="topicPrefix">Optional topic prefix. Default is "devices".</param>
        public DeviceTwinManager(string deviceId, string topicPrefix = "devices")
        {
            if (deviceId == null || deviceId.Length == 0)
            {
                throw new ArgumentException("deviceId cannot be null or empty.");
            }

            _deviceId = deviceId;
            TopicPrefix = topicPrefix ?? "devices";
            _reportedState = new Hashtable();
            _desiredState = new Hashtable();
            _reportedVersion = 0;
            _desiredVersion = 0;

            // Build topic strings
            string basePath = TopicPrefix + "/" + _deviceId + "/twin";
            _desiredTopic = basePath + "/desired";
            _reportedTopic = basePath + "/reported";
            _getTopic = basePath + "/get";
            _responseTopic = basePath + "/res";
        }

        /// <summary>
        /// Gets all MQTT topics that this twin manager needs to subscribe to.
        /// </summary>
        /// <returns>Array of topics: [desired, response].</returns>
        public string[] GetSubscriptionTopics()
        {
            return new string[] { _desiredTopic, _responseTopic };
        }

        /// <summary>
        /// Updates a single property in the reported state and returns the
        /// JSON payload to publish to the reported topic.
        /// </summary>
        /// <param name="key">The property name.</param>
        /// <param name="value">The property value (string, int, bool, or any serializable type).</param>
        /// <returns>JSON payload string to publish.</returns>
        public string UpdateReportedProperty(string key, object value)
        {
            if (key == null || key.Length == 0)
            {
                throw new ArgumentException("Property key cannot be null or empty.");
            }

            lock (_stateLock)
            {
                _reportedState[key] = value;
                _reportedVersion++;
            }

            var payload = new Hashtable();
            payload["key"] = key;
            payload["value"] = value;
            payload["version"] = _reportedVersion;
            payload["deviceId"] = _deviceId;
            payload["timestamp"] = DateTime.UtcNow.ToString("o");

            string json = JsonConvert.SerializeObject(payload);
            Debug.WriteLine($"[DeviceTwin] Reported property updated: {key} = {value}");
            return json;
        }

        /// <summary>
        /// Updates multiple properties in the reported state and returns
        /// the JSON payload to publish to the reported topic.
        /// </summary>
        /// <param name="properties">Hashtable of key-value pairs to update.</param>
        /// <returns>JSON payload string to publish.</returns>
        public string UpdateReportedProperties(Hashtable properties)
        {
            if (properties == null)
            {
                throw new ArgumentNullException("properties");
            }

            lock (_stateLock)
            {
                foreach (DictionaryEntry entry in properties)
                {
                    _reportedState[(string)entry.Key] = entry.Value;
                }

                _reportedVersion++;
            }

            var payload = new Hashtable();
            payload["properties"] = properties;
            payload["version"] = _reportedVersion;
            payload["deviceId"] = _deviceId;
            payload["timestamp"] = DateTime.UtcNow.ToString("o");

            string json = JsonConvert.SerializeObject(payload);
            Debug.WriteLine($"[DeviceTwin] Reported {properties.Count} properties (v{_reportedVersion})");
            return json;
        }

        /// <summary>
        /// Gets the full reported state as a JSON payload to publish.
        /// </summary>
        /// <returns>JSON payload string containing all reported properties.</returns>
        public string GetFullReportedStateJson()
        {
            var payload = new Hashtable();

            lock (_stateLock)
            {
                payload["properties"] = _reportedState.Clone();
                payload["version"] = _reportedVersion;
            }

            payload["deviceId"] = _deviceId;
            payload["timestamp"] = DateTime.UtcNow.ToString("o");

            return JsonConvert.SerializeObject(payload);
        }

        /// <summary>
        /// Gets a specific reported property value.
        /// </summary>
        /// <param name="key">The property name.</param>
        /// <returns>The property value, or null if not found.</returns>
        public object GetReportedProperty(string key)
        {
            lock (_stateLock)
            {
                return _reportedState.Contains(key) ? _reportedState[key] : null;
            }
        }

        /// <summary>
        /// Gets a specific desired property value.
        /// </summary>
        /// <param name="key">The property name.</param>
        /// <returns>The property value, or null if not found.</returns>
        public object GetDesiredProperty(string key)
        {
            lock (_stateLock)
            {
                return _desiredState.Contains(key) ? _desiredState[key] : null;
            }
        }

        /// <summary>
        /// Builds the JSON payload for a "get desired state" request.
        /// Publish this to <see cref="GetTopic"/> to request the full desired state.
        /// </summary>
        /// <returns>JSON request payload.</returns>
        public string BuildGetDesiredStateRequest()
        {
            var request = new Hashtable();
            request["deviceId"] = _deviceId;
            request["action"] = "getDesired";
            request["timestamp"] = DateTime.UtcNow.ToString("o");
            return JsonConvert.SerializeObject(request);
        }

        /// <summary>
        /// Processes an incoming message on a twin topic. Call this when a message is received
        /// on any of the twin subscription topics.
        /// </summary>
        /// <param name="topic">The MQTT topic the message was received on.</param>
        /// <param name="payload">The decoded JSON payload string.</param>
        /// <returns>True if the message was a twin message and was processed, false if not a twin topic.</returns>
        public bool ProcessMessage(string topic, string payload)
        {
            if (topic == null || payload == null)
            {
                return false;
            }

            if (topic == _desiredTopic || topic == _responseTopic)
            {
                ProcessDesiredStateUpdate(payload);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Clears all reported state properties. Does not publish the change —
        /// call <see cref="GetFullReportedStateJson"/> and publish manually.
        /// </summary>
        public void ClearReportedState()
        {
            lock (_stateLock)
            {
                _reportedState.Clear();
                _reportedVersion++;
            }

            Debug.WriteLine("[DeviceTwin] Reported state cleared.");
        }

        #region Private Methods

        private void ProcessDesiredStateUpdate(string payload)
        {
            try
            {
                // Parse the incoming desired state JSON
                Hashtable incoming = (Hashtable)JsonConvert.DeserializeObject(payload, typeof(Hashtable));

                if (incoming == null)
                {
                    Debug.WriteLine("[DeviceTwin] Failed to parse desired state payload.");
                    return;
                }

                string propertyKey = null;
                string propertyValue = null;

                lock (_stateLock)
                {
                    // Check if it's a single property update or full state
                    if (incoming.Contains("key") && incoming.Contains("value"))
                    {
                        // Single property update
                        propertyKey = incoming["key"]?.ToString();
                        propertyValue = incoming["value"]?.ToString();

                        if (propertyKey != null)
                        {
                            _desiredState[propertyKey] = incoming["value"];
                        }
                    }
                    else if (incoming.Contains("properties"))
                    {
                        // Full state or multi-property update
                        Hashtable props = incoming["properties"] as Hashtable;

                        if (props != null)
                        {
                            foreach (DictionaryEntry entry in props)
                            {
                                _desiredState[(string)entry.Key] = entry.Value;
                            }
                        }
                    }
                    else
                    {
                        // Treat entire payload as desired properties
                        foreach (DictionaryEntry entry in incoming)
                        {
                            string key = (string)entry.Key;

                            // Skip metadata keys
                            if (key == "version" || key == "timestamp" || key == "deviceId")
                            {
                                continue;
                            }

                            _desiredState[key] = entry.Value;
                        }
                    }

                    if (incoming.Contains("version"))
                    {
                        _desiredVersion = (int)incoming["version"];
                    }
                    else
                    {
                        _desiredVersion++;
                    }
                }

                Debug.WriteLine($"[DeviceTwin] Desired state updated (v{_desiredVersion})");

                DesiredStateChanged?.Invoke(this, new DesiredStateChangedEventArgs(
                    payload, propertyKey, propertyValue, _desiredVersion));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DeviceTwin] Error processing desired state: {ex.Message}");
            }
        }

        #endregion
    }
}
