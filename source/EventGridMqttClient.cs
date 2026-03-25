// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using nanoFramework.Json;
using nanoFramework.M2Mqtt;
using nanoFramework.M2Mqtt.Messages;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Delegate for message received events.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">Event data containing the topic and payload.</param>
    public delegate void MessageReceivedEventHandler(object sender, MessageReceivedEventArgs e);

    /// <summary>
    /// Delegate for connection state change events.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">Event data containing the connection state.</param>
    public delegate void ConnectionStateChangedEventHandler(object sender, ConnectionStateChangedEventArgs e);

    /// <summary>
    /// Delegate for message published events.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">Event data containing publish confirmation.</param>
    public delegate void MessagePublishedEventHandler(object sender, MessagePublishedEventArgs e);

    /// <summary>
    /// A high-level MQTT client for connecting .NET nanoFramework devices (ESP32)
    /// to the Azure EventGrid Namespace MQTT broker.
    /// <para>
    /// Handles X509 certificate authentication, MQTT5 protocol, automatic reconnection
    /// with exponential backoff, topic management, and Last Will and Testament (LWT).
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var config = new EventGridMqttConfig
    /// {
    ///     BrokerHostname = "mynamespace.westeurope-1.ts.eventgrid.azure.net",
    ///     DeviceClientId = "myDevice",
    ///     CaCertificatePem = caCert,
    ///     ClientCertificatePem = clientCert,
    ///     ClientPrivateKeyPem = clientKey
    /// };
    ///
    /// using (var client = new EventGridMqttClient(config))
    /// {
    ///     client.MessageReceived += (s, e) =&gt; Debug.WriteLine(e.Payload);
    ///     client.Connect();
    ///     client.Subscribe("devices/myDevice/commands");
    ///     client.Publish("devices/myDevice/telemetry", "{\"temp\":23.5}");
    /// }
    /// </code>
    /// </example>
    public class EventGridMqttClient : IEventGridMqttClient, IDisposable
    {
        private readonly EventGridMqttConfig _config;
        private readonly ILogger _logger;
        private MqttClient _mqttClient;
        private readonly X509Certificate _caCert;
        private X509Certificate2 _clientCert;
        private readonly ConnectionManager _connectionManager;
        private readonly ArrayList _subscribedTopics;
        private readonly ArrayList _subscribedQos;
        private readonly ArrayList _messageHandlers;
        private bool _disposed;
        private bool _intentionalDisconnect;

        // Feature managers
        private DeviceTwinManager _twinManager;
        private HealthReporter _healthReporter;
        private CertificateRotationManager _certRotationManager;

        /// <summary>
        /// Fired when a message is received on a subscribed topic.
        /// </summary>
        public event MessageReceivedEventHandler MessageReceived;

        /// <summary>
        /// Fired when the connection state changes (connected, disconnected, reconnecting).
        /// </summary>
        public event ConnectionStateChangedEventHandler ConnectionStateChanged;

        /// <summary>
        /// Fired when a published message is confirmed by the broker (QoS 1 only).
        /// </summary>
        public event MessagePublishedEventHandler MessagePublished;

        /// <summary>
        /// Gets whether the client is currently connected to the MQTT broker.
        /// </summary>
        public bool IsConnected => _mqttClient != null && _mqttClient.IsConnected;

        /// <summary>
        /// Gets whether the client is currently attempting to reconnect.
        /// </summary>
        public bool IsReconnecting => _connectionManager != null && _connectionManager.IsReconnecting;

        /// <summary>
        /// Gets the Device Twin manager. Null if <see cref="EventGridMqttConfig.EnableDeviceTwin"/> is false.
        /// </summary>
        public DeviceTwinManager Twin => _twinManager;

        /// <summary>
        /// Gets the Health Reporter. Null if <see cref="EventGridMqttConfig.EnableHealthReporting"/> is false.
        /// </summary>
        public HealthReporter Health => _healthReporter;

        /// <summary>
        /// Gets the Certificate Rotation manager. Null if <see cref="EventGridMqttConfig.EnableCertificateRotation"/> is false.
        /// </summary>
        public CertificateRotationManager CertRotation => _certRotationManager;

        /// <summary>
        /// Gets the device client ID used for this connection.
        /// </summary>
        public string DeviceClientId => _config.DeviceClientId;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventGridMqttClient"/> class.
        /// Parses certificates and prepares the MQTT client for connection.
        /// </summary>
        /// <param name="config">Configuration containing broker hostname, certificates, and connection settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null.</exception>
        /// <exception cref="ArgumentException">Thrown when required configuration values are missing or invalid.</exception>
        public EventGridMqttClient(EventGridMqttConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            ValidateConfig(config);

            _config = config;
            _logger = config.Logger ?? new DebugLogger();
            _disposed = false;
            _intentionalDisconnect = false;
            _subscribedTopics = new ArrayList();
            _subscribedQos = new ArrayList();
            _messageHandlers = new ArrayList();

            // Parse certificates
            _logger.LogInfo("Parsing certificates...");

            _caCert = CertificateHelper.CreateCaCertificate(_config.CaCertificatePem);
            _clientCert = CertificateHelper.CreateClientCertificate(_config.ClientCertificatePem, _config.ClientPrivateKeyPem);

            _logger.LogInfo("Certificates parsed successfully.");

            // Create the MQTT client
            CreateMqttClient();

            // Set up connection manager
            if (_config.AutoReconnect)
            {
                _connectionManager = new ConnectionManager(_config);
                _connectionManager.TryReconnect = AttemptReconnect;

                _connectionManager.Reconnected += (s, e) =>
                {
                    _healthReporter?.RecordReconnection();
                    ConnectionStateChanged?.Invoke(this, e);
                };

                _connectionManager.ReconnectAttemptFailed += (s, e) =>
                {
                    ConnectionStateChanged?.Invoke(this, e);
                };

                _connectionManager.ReconnectFailed += (s, e) =>
                {
                    ConnectionStateChanged?.Invoke(this, e);
                };
            }

            // Set up Device Twin manager
            if (_config.EnableDeviceTwin)
            {
                _twinManager = new DeviceTwinManager(_config.DeviceClientId, _config.TwinTopicPrefix);
                _messageHandlers.Add(_twinManager);
                _logger.LogInfo("Device Twin enabled.");
            }

            // Set up Health Reporter
            if (_config.EnableHealthReporting)
            {
                _healthReporter = new HealthReporter(
                    _config.DeviceClientId,
                    _config.HealthReportIntervalMs,
                    _config.HealthTopicPrefix);
                _logger.LogInfo("Health Reporting enabled.");
            }

            // Set up Certificate Rotation manager
            if (_config.EnableCertificateRotation)
            {
                _certRotationManager = new CertificateRotationManager(
                    _config.DeviceClientId,
                    _config.ClientCertificatePem,
                    _config.ClientPrivateKeyPem,
                    _config.CertWarningDaysBeforeExpiry,
                    _config.CertCheckIntervalMs,
                    _config.CertTopicPrefix);

                _certRotationManager.CertificateExpiring += OnCertificateExpiring;
                _certRotationManager.CertificateRotated += OnCertificateRotated;
                _messageHandlers.Add(_certRotationManager);
                _logger.LogInfo("Certificate Rotation enabled.");
            }
        }

        /// <summary>
        /// Connects to the Azure EventGrid MQTT broker.
        /// </summary>
        /// <returns>The MQTT reason code indicating the result of the connection attempt.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is already connected or disposed.</exception>
        public MqttReasonCode Connect()
        {
            ThrowIfDisposed();

            if (IsConnected)
            {
                _logger.LogInfo("Already connected.");
                return MqttReasonCode.Success;
            }

            _logger.LogInfo($"Connecting to {_config.BrokerHostname}:{_config.Port} as '{_config.DeviceClientId}'...");

            MqttReasonCode result;

            bool hasLwt = _config.LwtTopic != null && _config.LwtTopic.Length > 0;

            if (hasLwt)
            {
                result = _mqttClient.Connect(
                    _config.DeviceClientId,
                    null,                   // username (not used with X509)
                    null,                   // password (not used with X509)
                    _config.LwtRetain,
                    _config.LwtQos,
                    true,                   // willFlag
                    _config.LwtTopic,
                    _config.LwtMessage ?? "",
                    _config.CleanSession,
                    _config.KeepAlivePeriodSeconds);
            }
            else
            {
                result = _mqttClient.Connect(
                    _config.DeviceClientId,
                    null,
                    null,
                    _config.CleanSession,
                    _config.KeepAlivePeriodSeconds);
            }

            if (result == MqttReasonCode.Success)
            {
                _logger.LogInfo("Connected successfully.");
                _intentionalDisconnect = false;

                _healthReporter?.UpdateConnectionStatus(true);

                // Auto-subscribe to feature topics
                AutoSubscribeFeatureTopics();

                // Start background features
                StartFeatures();

                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(
                    true, "Connected to EventGrid MQTT broker."));
            }
            else
            {
                _logger.LogError($"Connection failed: {result}");

                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(
                    false, $"Connection failed: {result}"));
            }

            return result;
        }

        /// <summary>
        /// Disconnects from the MQTT broker gracefully.
        /// Stops any active reconnection attempts and background features.
        /// </summary>
        public void Disconnect()
        {
            ThrowIfDisposed();

            _intentionalDisconnect = true;
            _connectionManager?.Stop();
            _healthReporter?.Stop();
            _certRotationManager?.StopMonitoring();

            if (_mqttClient != null && _mqttClient.IsConnected)
            {
                _logger.LogInfo("Disconnecting...");
                _mqttClient.Disconnect();
                _logger.LogInfo("Disconnected.");
            }

            _healthReporter?.UpdateConnectionStatus(false);

            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(
                false, "Disconnected intentionally."));
        }

        /// <summary>
        /// Subscribes to an MQTT topic. The subscription is remembered and automatically
        /// resubscribed after a reconnection.
        /// </summary>
        /// <param name="topic">The topic to subscribe to. Can include wildcards (# or +).</param>
        /// <param name="qos">The QoS level for the subscription. Default is AtLeastOnce (QoS 1).</param>
        /// <exception cref="ArgumentException">Thrown when the topic is null, empty, or invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not connected.</exception>
        public void Subscribe(string topic, MqttQoSLevel qos = MqttQoSLevel.AtLeastOnce)
        {
            ThrowIfDisposed();

            if (topic == null || topic.Length == 0)
            {
                throw new ArgumentException("Topic cannot be null or empty.");
            }

            if (!IsConnected)
            {
                throw new InvalidOperationException("Cannot subscribe: not connected.");
            }

            _logger.LogInfo($"Subscribing to: {topic}");

            _mqttClient.Subscribe(
                new string[] { topic },
                new MqttQoSLevel[] { qos });

            // Remember subscription for resubscribe on reconnect
            if (!_subscribedTopics.Contains(topic))
            {
                _subscribedTopics.Add(topic);
                _subscribedQos.Add(qos);
            }
        }

        /// <summary>
        /// Unsubscribes from an MQTT topic and removes it from the auto-resubscribe list.
        /// </summary>
        /// <param name="topic">The topic to unsubscribe from.</param>
        public void Unsubscribe(string topic)
        {
            ThrowIfDisposed();

            if (topic == null || topic.Length == 0)
            {
                return;
            }

            if (IsConnected)
            {
                _mqttClient.Unsubscribe(new string[] { topic });
            }

            int index = _subscribedTopics.IndexOf(topic);

            if (index >= 0)
            {
                _subscribedTopics.RemoveAt(index);
                _subscribedQos.RemoveAt(index);
            }

            _logger.LogInfo($"Unsubscribed from: {topic}");
        }

        /// <summary>
        /// Publishes a string payload to an MQTT topic.
        /// </summary>
        /// <param name="topic">The topic to publish to.</param>
        /// <param name="payload">The string payload (typically JSON).</param>
        /// <param name="qos">QoS level. Default is AtMostOnce (QoS 0).</param>
        /// <param name="retain">Whether to set the retain flag. Default is false.</param>
        /// <returns>The message ID (0 for QoS 0).</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected.</exception>
        public ushort Publish(string topic, string payload, MqttQoSLevel qos = MqttQoSLevel.AtMostOnce, bool retain = false)
        {
            ThrowIfDisposed();

            if (!IsConnected)
            {
                throw new InvalidOperationException("Cannot publish: not connected.");
            }

            if (topic == null || topic.Length == 0)
            {
                throw new ArgumentException("Topic cannot be null or empty.");
            }

            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload ?? "");

            _healthReporter?.IncrementPublished();

            return _mqttClient.Publish(topic, payloadBytes, null, null, qos, retain);
        }

        /// <summary>
        /// Serializes an object to JSON and publishes it to an MQTT topic.
        /// </summary>
        /// <param name="topic">The topic to publish to.</param>
        /// <param name="data">The object to serialize as JSON.</param>
        /// <param name="qos">QoS level. Default is AtMostOnce (QoS 0).</param>
        /// <param name="retain">Whether to set the retain flag. Default is false.</param>
        /// <returns>The message ID (0 for QoS 0).</returns>
        public ushort PublishJson(string topic, object data, MqttQoSLevel qos = MqttQoSLevel.AtMostOnce, bool retain = false)
        {
            string json = JsonConvert.SerializeObject(data);
            return Publish(topic, json, qos, retain);
        }

        /// <summary>
        /// Publishes raw byte array payload to an MQTT topic.
        /// </summary>
        /// <param name="topic">The topic to publish to.</param>
        /// <param name="payload">The raw byte payload.</param>
        /// <param name="qos">QoS level. Default is AtMostOnce (QoS 0).</param>
        /// <param name="retain">Whether to set the retain flag. Default is false.</param>
        /// <returns>The message ID (0 for QoS 0).</returns>
        public ushort PublishRaw(string topic, byte[] payload, MqttQoSLevel qos = MqttQoSLevel.AtMostOnce, bool retain = false)
        {
            ThrowIfDisposed();

            if (!IsConnected)
            {
                throw new InvalidOperationException("Cannot publish: not connected.");
            }

            _healthReporter?.IncrementPublished();

            return _mqttClient.Publish(topic, payload, null, null, qos, retain);
        }

        /// <summary>
        /// Releases all resources used by the <see cref="EventGridMqttClient"/>.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _intentionalDisconnect = true;
            _connectionManager?.Stop();
            _healthReporter?.Dispose();
            _certRotationManager?.Dispose();

            if (_mqttClient != null)
            {
                try
                {
                    if (_mqttClient.IsConnected)
                    {
                        _mqttClient.Disconnect();
                    }
                }
                catch
                {
                    // Suppress exceptions during disposal
                }

                _mqttClient = null;
            }

            _subscribedTopics.Clear();
            _subscribedQos.Clear();
        }

        #region Private Methods

        private void CreateMqttClient()
        {
            _mqttClient = new MqttClient(
                _config.BrokerHostname,
                _config.Port,
                true,                       // secure
                _caCert,                    // CA cert for server validation
                _clientCert,                // client cert for authentication
                MqttSslProtocols.TLSv1_2);

            // Set protocol version
            if (_config.UseMqtt5)
            {
                _mqttClient.ProtocolVersion = MqttProtocolVersion.Version_5;
            }

            // Wire up internal event handlers
            _mqttClient.MqttMsgPublishReceived += OnMqttMessageReceived;
            _mqttClient.MqttMsgPublished += OnMqttMessagePublished;
            _mqttClient.ConnectionClosed += OnMqttConnectionClosed;
        }

        private void OnMqttMessageReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string topic = e.Topic;
            string payload = Encoding.UTF8.GetString(e.Message, 0, e.Message.Length);

            _logger.LogInfo($"Message received on '{topic}': {payload}");

            _healthReporter?.IncrementReceived();

            // Route to feature managers first
            bool handled = false;

            for (int i = 0; i < _messageHandlers.Count; i++)
            {
                IMqttMessageHandler handler = (IMqttMessageHandler)_messageHandlers[i];

                if (handler.ProcessMessage(topic, payload))
                {
                    handled = true;
                    break;
                }
            }

            // Always fire the event regardless of whether it was handled by a feature
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(
                topic,
                payload,
                e.Message,
                (byte)e.QosLevel,
                e.Retain));
        }

        private void OnMqttMessagePublished(object sender, MqttMsgPublishedEventArgs e)
        {
            MessagePublished?.Invoke(this, new MessagePublishedEventArgs(
                e.MessageId,
                e.IsPublished));
        }

        private void OnMqttConnectionClosed(object sender, EventArgs e)
        {
            _logger.LogInfo("Connection closed.");

            if (_intentionalDisconnect || _disposed)
            {
                return;
            }

            _healthReporter?.RecordConnectionDrop();
            _healthReporter?.UpdateConnectionStatus(false);

            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(
                false, "Connection lost unexpectedly."));

            // Start auto-reconnection if enabled
            if (_config.AutoReconnect && _connectionManager != null)
            {
                _logger.LogInfo("Starting auto-reconnection...");
                _connectionManager.StartReconnecting();
            }
        }

        private bool AttemptReconnect()
        {
            try
            {
                // Recreate the MQTT client (old one may be in bad state)
                if (_mqttClient != null)
                {
                    _mqttClient.MqttMsgPublishReceived -= OnMqttMessageReceived;
                    _mqttClient.MqttMsgPublished -= OnMqttMessagePublished;
                    _mqttClient.ConnectionClosed -= OnMqttConnectionClosed;
                }

                CreateMqttClient();

                // Attempt to connect
                var result = Connect();

                if (result != MqttReasonCode.Success)
                {
                    return false;
                }

                // Resubscribe to previously subscribed topics
                ResubscribeAll();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Reconnect error: {ex.Message}");
                return false;
            }
        }

        private void ResubscribeAll()
        {
            if (_subscribedTopics.Count == 0)
            {
                return;
            }

            _logger.LogInfo($"Resubscribing to {_subscribedTopics.Count} topic(s)...");

            for (int i = 0; i < _subscribedTopics.Count; i++)
            {
                string topic = (string)_subscribedTopics[i];
                MqttQoSLevel qos = (MqttQoSLevel)_subscribedQos[i];

                try
                {
                    _mqttClient.Subscribe(
                        new string[] { topic },
                        new MqttQoSLevel[] { qos });

                    _logger.LogInfo($"Resubscribed to: {topic}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to resubscribe to '{topic}': {ex.Message}");
                }
            }
        }

        private static void ValidateConfig(EventGridMqttConfig config)
        {
            if (config.BrokerHostname == null || config.BrokerHostname.Length == 0)
            {
                throw new ArgumentException("BrokerHostname is required.");
            }

            if (!TopicHelper.ValidateBrokerHostname(config.BrokerHostname))
            {
                throw new ArgumentException(
                    "BrokerHostname must be an EventGrid MQTT endpoint containing '.ts.' and 'eventgrid.azure.net'. " +
                    "Example: 'mynamespace.westeurope-1.ts.eventgrid.azure.net'");
            }

            if (config.DeviceClientId == null || config.DeviceClientId.Length == 0)
            {
                throw new ArgumentException("DeviceClientId is required. Must match the client authentication name in EventGrid.");
            }

            if (config.CaCertificatePem == null || config.CaCertificatePem.Length == 0)
            {
                throw new ArgumentException("CaCertificatePem is required. Provide the TLS root certificate (e.g., DigiCert Global Root G3).");
            }

            if (config.ClientCertificatePem == null || config.ClientCertificatePem.Length == 0)
            {
                throw new ArgumentException("ClientCertificatePem is required. Provide the device public certificate.");
            }

            if (config.ClientPrivateKeyPem == null || config.ClientPrivateKeyPem.Length == 0)
            {
                throw new ArgumentException("ClientPrivateKeyPem is required. Provide the device private key.");
            }

            if (config.Port <= 0 || config.Port > 65535)
            {
                throw new ArgumentException("Port must be between 1 and 65535.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("EventGridMqttClient");
            }
        }

        private void AutoSubscribeFeatureTopics()
        {
            if (!IsConnected)
            {
                return;
            }

            // Subscribe to Device Twin topics
            if (_twinManager != null)
            {
                string[] twinTopics = _twinManager.GetSubscriptionTopics();

                for (int i = 0; i < twinTopics.Length; i++)
                {
                    Subscribe(twinTopics[i], MqttQoSLevel.AtLeastOnce);
                }

                _logger.LogInfo("Subscribed to Device Twin topics.");
            }

            // Subscribe to Certificate Rotation topics
            if (_certRotationManager != null)
            {
                string[] certTopics = _certRotationManager.GetSubscriptionTopics();

                for (int i = 0; i < certTopics.Length; i++)
                {
                    Subscribe(certTopics[i], MqttQoSLevel.AtLeastOnce);
                }

                _logger.LogInfo("Subscribed to Certificate Rotation topics.");
            }

            // Subscribe to any custom registered message handlers
            for (int i = 0; i < _messageHandlers.Count; i++)
            {
                IMqttMessageHandler handler = (IMqttMessageHandler)_messageHandlers[i];

                // Skip built-in handlers already subscribed above
                if (handler == (IMqttMessageHandler)_twinManager ||
                    handler == (IMqttMessageHandler)_certRotationManager)
                {
                    continue;
                }

                string[] handlerTopics = handler.GetSubscriptionTopics();

                for (int j = 0; j < handlerTopics.Length; j++)
                {
                    Subscribe(handlerTopics[j], MqttQoSLevel.AtLeastOnce);
                }
            }
        }

        private void StartFeatures()
        {
            // Start Health Reporter
            if (_healthReporter != null && !_healthReporter.IsRunning)
            {
                _healthReporter.Start((topic, json) =>
                {
                    try
                    {
                        if (IsConnected)
                        {
                            Publish(topic, json, MqttQoSLevel.AtMostOnce);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Health publish failed: {ex.Message}");
                    }
                });
            }

            // Start Certificate Rotation monitoring
            if (_certRotationManager != null && !_certRotationManager.IsMonitoring)
            {
                _certRotationManager.StartMonitoring();
            }
        }

        private void OnCertificateExpiring(object sender, CertificateExpiringEventArgs e)
        {
            _logger.LogWarning($"Certificate expiring in {e.DaysUntilExpiry} days!");

            // Publish cert status if connected
            if (IsConnected && _certRotationManager != null)
            {
                try
                {
                    string status = _certRotationManager.BuildStatusReport();
                    Publish(_certRotationManager.CertStatusTopic, status, MqttQoSLevel.AtLeastOnce);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to publish cert status: {ex.Message}");
                }
            }
        }

        private void OnCertificateRotated(object sender, CertificateRotatedEventArgs e)
        {
            _logger.LogInfo($"Certificate rotation: {e.Reason}");

            if (e.Success && _certRotationManager != null)
            {
                // Publish acknowledgment
                if (IsConnected)
                {
                    try
                    {
                        string ack = _certRotationManager.BuildRotationAck(true);
                        Publish(_certRotationManager.CertAckTopic, ack, MqttQoSLevel.AtLeastOnce);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to publish cert ack: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Applies a pending certificate rotation and reconnects the client.
        /// Call this after <see cref="CertificateRotationManager.SetNewCertificate"/> or
        /// after receiving a new cert via MQTT on the certificates/new topic.
        /// </summary>
        /// <returns>True if rotation succeeded and reconnection was successful.</returns>
        public bool ApplyCertificateRotation()
        {
            ThrowIfDisposed();

            if (_certRotationManager == null)
            {
                _logger.LogWarning("Certificate rotation is not enabled.");
                return false;
            }

            if (!_certRotationManager.HasPendingCertificate)
            {
                _logger.LogWarning("No pending certificate to apply.");
                return false;
            }

            _logger.LogInfo("Applying certificate rotation...");

            // Apply the new certificate
            _certRotationManager.ApplyPendingCertificate();

            // Parse the new client certificate
            _clientCert = CertificateHelper.CreateClientCertificate(
                _certRotationManager.CurrentCertificatePem,
                _certRotationManager.CurrentPrivateKeyPem);

            // Disconnect and reconnect with new cert
            _intentionalDisconnect = true;

            if (_mqttClient != null && _mqttClient.IsConnected)
            {
                _mqttClient.Disconnect();
            }

            _intentionalDisconnect = false;

            // Recreate MQTT client with new cert
            if (_mqttClient != null)
            {
                _mqttClient.MqttMsgPublishReceived -= OnMqttMessageReceived;
                _mqttClient.MqttMsgPublished -= OnMqttMessagePublished;
                _mqttClient.ConnectionClosed -= OnMqttConnectionClosed;
            }

            CreateMqttClient();

            var result = Connect();

            if (result == MqttReasonCode.Success)
            {
                ResubscribeAll();
                _logger.LogInfo("Certificate rotation complete. Reconnected.");
                return true;
            }

            _logger.LogError("Certificate rotation failed — could not reconnect.");
            return false;
        }

        /// <summary>
        /// Updates a reported device twin property and publishes it.
        /// Shortcut for <c>Twin.UpdateReportedProperty()</c> + <c>Publish()</c>.
        /// </summary>
        /// <param name="key">Property name.</param>
        /// <param name="value">Property value.</param>
        public void UpdateTwinProperty(string key, object value)
        {
            ThrowIfDisposed();

            if (_twinManager == null)
            {
                throw new InvalidOperationException("Device Twin is not enabled. Set EnableDeviceTwin = true in config.");
            }

            if (!IsConnected)
            {
                throw new InvalidOperationException("Cannot update twin: not connected.");
            }

            string json = _twinManager.UpdateReportedProperty(key, value);
            Publish(_twinManager.ReportedTopic, json, MqttQoSLevel.AtLeastOnce);
        }

        /// <summary>
        /// Registers a custom <see cref="IMqttMessageHandler"/> to receive and process
        /// incoming messages on specific topics. The handler's subscription topics are
        /// automatically subscribed after connecting.
        /// </summary>
        /// <param name="handler">The message handler to register.</param>
        /// <remarks>
        /// This enables extensibility: add custom message processing modules without
        /// modifying the core client. Messages are routed to handlers in registration order.
        /// </remarks>
        public void RegisterMessageHandler(IMqttMessageHandler handler)
        {
            ThrowIfDisposed();

            if (handler == null)
            {
                throw new ArgumentNullException("handler");
            }

            if (!_messageHandlers.Contains(handler))
            {
                _messageHandlers.Add(handler);
                _logger.LogInfo("Custom message handler registered.");

                // If already connected, subscribe to the handler's topics now
                if (IsConnected)
                {
                    string[] topics = handler.GetSubscriptionTopics();

                    for (int i = 0; i < topics.Length; i++)
                    {
                        Subscribe(topics[i], MqttQoSLevel.AtLeastOnce);
                    }
                }
            }
        }

        /// <summary>
        /// Requests the full desired twin state from the cloud.
        /// Shortcut for <c>Twin.BuildGetDesiredStateRequest()</c> + <c>Publish()</c>.
        /// </summary>
        public void RequestDesiredTwinState()
        {
            ThrowIfDisposed();

            if (_twinManager == null)
            {
                throw new InvalidOperationException("Device Twin is not enabled. Set EnableDeviceTwin = true in config.");
            }

            if (!IsConnected)
            {
                throw new InvalidOperationException("Cannot request twin: not connected.");
            }

            string json = _twinManager.BuildGetDesiredStateRequest();
            Publish(_twinManager.GetTopic, json, MqttQoSLevel.AtLeastOnce);
        }

        #endregion
    }
}
