// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using nanoFramework.Json;
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
    /// Architecture: Separates MQTT transport concerns (<see cref="IMqttTransport"/>) from
    /// Event Grid semantics (topic routing, twin sync, health, certificates).
    /// Handles X509 certificate authentication, MQTT5 protocol, automatic reconnection
    /// with exponential backoff, offline message queue, and structured error handling.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var client = new EventGridMqttClientBuilder()
    ///     .WithBroker("mynamespace.westeurope-1.ts.eventgrid.azure.net")
    ///     .WithDevice("myDevice")
    ///     .WithCertificates(caCert, clientCert, clientKey)
    ///     .WithAutoReconnect()
    ///     .WithOfflineQueue()
    ///     .Build();
    ///
    /// client.ErrorOccurred += (s, e) =&gt; Debug.WriteLine("Error: " + e.Message);
    /// client.Connect();
    /// client.Subscribe("devices/myDevice/commands");
    /// client.Publish("devices/myDevice/telemetry", "{\"temp\":23.5}");
    /// </code>
    /// </example>
    public class EventGridMqttClient : IEventGridMqttClient, IDisposable
    {
        private readonly EventGridMqttConfig _config;
        private readonly ILogger _logger;
        private M2MqttTransport _transport;
        private readonly X509Certificate _caCert;
        private X509Certificate2 _clientCert;
        private readonly ConnectionManager _connectionManager;
        private readonly RetryHandler _publishRetryHandler;
        private readonly ArrayList _subscribedTopics;
        private readonly ArrayList _subscribedQos;
        private readonly ArrayList _messageHandlers;
        private bool _disposed;
        private bool _intentionalDisconnect;
        private ConnectionState _state;
        private OfflineMessageQueue _offlineQueue;

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
        /// Fired when an error occurs during any client operation.
        /// Provides structured error information including category, recoverability, and context.
        /// </summary>
        public event ClientErrorEventHandler ErrorOccurred;

        /// <summary>
        /// Gets the current connection state of the client.
        /// </summary>
        public ConnectionState State => _state;

        /// <summary>
        /// Gets whether the client is currently connected to the MQTT broker.
        /// </summary>
        public bool IsConnected => _transport != null && _transport.IsConnected;

        /// <summary>
        /// Gets whether the client is currently attempting to reconnect.
        /// </summary>
        public bool IsReconnecting => _state == ConnectionState.Reconnecting;

        /// <summary>
        /// Gets the Device Twin manager. Null if not enabled.
        /// </summary>
        public DeviceTwinManager Twin => _twinManager;

        /// <summary>
        /// Gets the Health Reporter. Null if not enabled.
        /// </summary>
        public HealthReporter Health => _healthReporter;

        /// <summary>
        /// Gets the Certificate Rotation manager. Null if not enabled.
        /// </summary>
        public CertificateRotationManager CertRotation => _certRotationManager;

        /// <summary>
        /// Gets the device client ID used for this connection.
        /// </summary>
        public string DeviceClientId => _config.DeviceClientId;

        /// <summary>
        /// Gets the offline message queue. Null if offline queuing is not enabled.
        /// </summary>
        public OfflineMessageQueue OfflineQueue => _offlineQueue;

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

            // Set up publish retry handler
            if (_config.PublishMaxRetries > 0)
            {
                _publishRetryHandler = new RetryHandler(
                    _config.PublishMaxRetries,
                    _config.PublishRetryBaseDelayMs,
                    _config.PublishRetryMaxDelayMs,
                    _logger);
                _logger.LogInfo("Publish retry enabled: " + _config.PublishMaxRetries + " max retries.");
            }

            // Parse certificates
            _logger.LogInfo("Parsing certificates...");

            _caCert = CertificateHelper.CreateCaCertificate(_config.CaCertificatePem);
            _clientCert = CertificateHelper.CreateClientCertificate(_config.ClientCertificatePem, _config.ClientPrivateKeyPem);

            _logger.LogInfo("Certificates parsed successfully.");

            // Create the MQTT transport layer
            _transport = new M2MqttTransport(
                _config.BrokerHostname,
                _config.Port,
                _caCert,
                _clientCert,
                _config.UseMqtt5,
                _logger);

            _transport.MessageReceived += OnTransportMessageReceived;
            _transport.MessagePublished += OnTransportMessagePublished;
            _transport.ConnectionClosed += OnTransportConnectionClosed;

            // Set up offline queue
            if (_config.EnableOfflineQueue)
            {
                _offlineQueue = new OfflineMessageQueue(_config.MaxOfflineQueueSize, _logger);
                _logger.LogInfo("Offline message queue enabled (max " + _config.MaxOfflineQueueSize + " messages).");
            }

            _state = ConnectionState.Disconnected;

            // Set up connection manager
            if (_config.AutoReconnect)
            {
                _connectionManager = new ConnectionManager(_config, _logger);
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
                _twinManager = new DeviceTwinManager(_config.DeviceClientId, _config.TwinTopicPrefix, _logger);
                _messageHandlers.Add(_twinManager);
                _logger.LogInfo("Device Twin enabled.");
            }

            // Set up Health Reporter
            if (_config.EnableHealthReporting)
            {
                _healthReporter = new HealthReporter(
                    _config.DeviceClientId,
                    _config.HealthReportIntervalMs,
                    _config.HealthTopicPrefix,
                    _logger);
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
                    _config.CertTopicPrefix,
                    _logger);

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

            _state = ConnectionState.Connecting;
            _logger.LogInfo("Connecting to " + _config.BrokerHostname + ":" + _config.Port + " as '" + _config.DeviceClientId + "'...");

            MqttReasonCode result;

            try
            {
                bool hasLwt = _config.LwtTopic != null && _config.LwtTopic.Length > 0;

                result = _transport.Connect(
                    _config.DeviceClientId,
                    _config.CleanSession,
                    _config.KeepAlivePeriodSeconds,
                    hasLwt ? _config.LwtTopic : null,
                    hasLwt ? (_config.LwtMessage ?? "") : null,
                    _config.LwtQos,
                    _config.LwtRetain);
            }
            catch (Exception ex)
            {
                _state = ConnectionState.Faulted;
                _logger.LogError("Connection exception: " + ex.Message);
                RaiseError(ErrorCategory.Connection, "Connection failed: " + ex.Message, ex, _config.BrokerHostname, false);
                return MqttReasonCode.UnspecifiedError;
            }

            if (result == MqttReasonCode.Success)
            {
                _state = ConnectionState.Connected;
                _logger.LogInfo("Connected successfully.");
                _intentionalDisconnect = false;

                _healthReporter?.UpdateConnectionStatus(true);

                // Flush any queued offline messages
                FlushOfflineQueue();

                // Auto-subscribe to feature topics
                AutoSubscribeFeatureTopics();

                // Start background features
                StartFeatures();

                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(
                    true, "Connected to EventGrid MQTT broker."));
            }
            else
            {
                _state = ConnectionState.Faulted;
                _logger.LogError("Connection failed: " + result);
                RaiseError(ErrorCategory.Connection, "Connection failed: " + result, null, _config.BrokerHostname, true);

                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(
                    false, "Connection failed: " + result));
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

            if (_transport != null && _transport.IsConnected)
            {
                _logger.LogInfo("Disconnecting...");
                _transport.Disconnect();
                _logger.LogInfo("Disconnected.");
            }

            _state = ConnectionState.Disconnected;
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

            _transport.Subscribe(
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
                _transport.Unsubscribe(new string[] { topic });
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
        /// If publish retry is enabled (see <see cref="EventGridMqttConfig.PublishMaxRetries"/>),
        /// failed publishes are automatically retried with exponential backoff.
        /// </summary>
        /// <param name="topic">The topic to publish to.</param>
        /// <param name="payload">The string payload (typically JSON).</param>
        /// <param name="qos">QoS level. Default is AtMostOnce (QoS 0).</param>
        /// <param name="retain">Whether to set the retain flag. Default is false.</param>
        /// <returns>The message ID (0 for QoS 0).</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected.</exception>
        /// <exception cref="ArgumentException">Thrown when topic is empty or payload exceeds max size.</exception>
        public ushort Publish(string topic, string payload, MqttQoSLevel qos = MqttQoSLevel.AtMostOnce, bool retain = false)
        {
            ThrowIfDisposed();

            // Azure Event Grid MQTT broker does not support retained messages.
            // Silently force retain=false to prevent broker rejection.
            if (retain)
            {
                _logger.LogWarning("Retain flag ignored — Azure Event Grid MQTT does not support retained messages.");
                retain = false;
            }

            if (topic == null || topic.Length == 0)
            {
                throw new ArgumentException("Topic cannot be null or empty.");
            }

            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload ?? "");

            // Validate payload size for ESP32 memory safety
            if (_config.MaxPayloadSize > 0 && payloadBytes.Length > _config.MaxPayloadSize)
            {
                throw new ArgumentException(
                    "Payload size (" + payloadBytes.Length + " bytes) exceeds maximum (" + _config.MaxPayloadSize + " bytes).");
            }

            // Queue message if disconnected and offline queue is enabled
            if (!IsConnected)
            {
                if (_offlineQueue != null)
                {
                    _offlineQueue.Enqueue(topic, payloadBytes, qos, retain);
                    return 0;
                }

                throw new InvalidOperationException("Cannot publish: not connected.");
            }

            // Auto GC if memory is low
            if (_config.AutoGarbageCollect)
            {
                MemoryManager.CollectIfNeeded();
            }

            _healthReporter?.IncrementPublished();

            try
            {
                // Use retry handler if configured
                if (_publishRetryHandler != null)
                {
                    ushort msgId = 0;
                    string topicCapture = topic;
                    byte[] bytesCapture = payloadBytes;
                    MqttQoSLevel qosCapture = qos;
                    bool retainCapture = retain;

                    bool success = _publishRetryHandler.ExecuteWithRetry(
                        () =>
                        {
                            msgId = _transport.Publish(topicCapture, bytesCapture, qosCapture, retainCapture);
                            return true;
                        },
                        "Publish to " + topic);

                    if (!success)
                    {
                        _logger.LogError("Publish failed after retries: " + topic);
                        RaiseError(ErrorCategory.Publish, "Publish failed after retries", null, topic, true);
                    }

                    return msgId;
                }

                return _transport.Publish(topic, payloadBytes, qos, retain);
            }
            catch (Exception ex)
            {
                _logger.LogError("Publish error: " + ex.Message);
                RaiseError(ErrorCategory.Publish, "Publish failed: " + ex.Message, ex, topic, true);
                return 0;
            }
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
        /// Supports retry with exponential backoff if configured.
        /// </summary>
        /// <param name="topic">The topic to publish to.</param>
        /// <param name="payload">The raw byte payload.</param>
        /// <param name="qos">QoS level. Default is AtMostOnce (QoS 0).</param>
        /// <param name="retain">Whether to set the retain flag. Default is false.</param>
        /// <returns>The message ID (0 for QoS 0).</returns>
        /// <exception cref="ArgumentException">Thrown when payload exceeds max size.</exception>
        public ushort PublishRaw(string topic, byte[] payload, MqttQoSLevel qos = MqttQoSLevel.AtMostOnce, bool retain = false)
        {
            ThrowIfDisposed();

            // Azure Event Grid MQTT broker does not support retained messages.
            if (retain)
            {
                _logger.LogWarning("Retain flag ignored — Azure Event Grid MQTT does not support retained messages.");
                retain = false;
            }

            // Validate payload size for ESP32 memory safety
            if (_config.MaxPayloadSize > 0 && payload != null && payload.Length > _config.MaxPayloadSize)
            {
                throw new ArgumentException(
                    "Payload size (" + payload.Length + " bytes) exceeds maximum (" + _config.MaxPayloadSize + " bytes).");
            }

            // Queue message if disconnected and offline queue is enabled
            if (!IsConnected)
            {
                if (_offlineQueue != null)
                {
                    _offlineQueue.Enqueue(topic, payload, qos, retain);
                    return 0;
                }

                throw new InvalidOperationException("Cannot publish: not connected.");
            }

            if (_config.AutoGarbageCollect)
            {
                MemoryManager.CollectIfNeeded();
            }

            _healthReporter?.IncrementPublished();

            try
            {
                if (_publishRetryHandler != null)
                {
                    ushort msgId = 0;
                    string topicCapture = topic;
                    byte[] bytesCapture = payload;
                    MqttQoSLevel qosCapture = qos;
                    bool retainCapture = retain;

                    _publishRetryHandler.ExecuteWithRetry(
                        () =>
                        {
                            msgId = _transport.Publish(topicCapture, bytesCapture, qosCapture, retainCapture);
                            return true;
                        },
                        "PublishRaw to " + topic);

                    return msgId;
                }

                return _transport.Publish(topic, payload, qos, retain);
            }
            catch (Exception ex)
            {
                _logger.LogError("PublishRaw error: " + ex.Message);
                RaiseError(ErrorCategory.Publish, "PublishRaw failed: " + ex.Message, ex, topic, true);
                return 0;
            }
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
            _state = ConnectionState.Disconnected;
            _connectionManager?.Stop();
            _healthReporter?.Dispose();
            _certRotationManager?.Dispose();

            if (_transport != null)
            {
                try
                {
                    _transport.DetachEvents();
                    if (_transport.IsConnected)
                    {
                        _transport.Disconnect();
                    }
                }
                catch
                {
                    // Suppress exceptions during disposal
                }

                _transport = null;
            }

            _offlineQueue?.Clear();
            _subscribedTopics.Clear();
            _subscribedQos.Clear();
        }

        #region Private Methods

        private void OnTransportMessageReceived(object sender, string topic, byte[] rawPayload, byte qos, bool retain)
        {
            string payload = Encoding.UTF8.GetString(rawPayload, 0, rawPayload.Length);

            _logger.LogInfo("Message received on '" + topic + "': " + payload);

            _healthReporter?.IncrementReceived();

            // Route to feature managers first
            for (int i = 0; i < _messageHandlers.Count; i++)
            {
                IMqttMessageHandler handler = (IMqttMessageHandler)_messageHandlers[i];

                if (handler.ProcessMessage(topic, payload))
                {
                    break;
                }
            }

            // Always fire the event regardless of whether it was handled by a feature
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(
                topic,
                payload,
                rawPayload,
                qos,
                retain));
        }

        private void OnTransportMessagePublished(object sender, ushort messageId, bool isPublished)
        {
            MessagePublished?.Invoke(this, new MessagePublishedEventArgs(
                messageId,
                isPublished));
        }

        private void OnTransportConnectionClosed(object sender)
        {
            _logger.LogInfo("Connection closed.");

            if (_intentionalDisconnect || _disposed)
            {
                return;
            }

            _state = ConnectionState.Reconnecting;
            _healthReporter?.RecordConnectionDrop();
            _healthReporter?.UpdateConnectionStatus(false);

            RaiseError(ErrorCategory.Network, "Connection lost unexpectedly.", null, null, true);

            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(
                false, "Connection lost unexpectedly."));

            // Start auto-reconnection if enabled
            if (_config.AutoReconnect && _connectionManager != null)
            {
                _logger.LogInfo("Starting auto-reconnection...");
                _connectionManager.StartReconnecting();
            }
        }

        private void FlushOfflineQueue()
        {
            if (_offlineQueue == null || !_offlineQueue.HasPendingMessages)
            {
                return;
            }

            _offlineQueue.Flush((topic, payload, qos, retain) =>
            {
                try
                {
                    _transport.Publish(topic, payload, qos, retain);
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        private void RaiseError(ErrorCategory category, string message, Exception ex, string context, bool isRecoverable)
        {
            ErrorOccurred?.Invoke(this, new ClientErrorEventArgs(category, message, ex, context, isRecoverable));
        }

        private bool AttemptReconnect()
        {
            try
            {
                _state = ConnectionState.Reconnecting;

                // Recreate the transport (old socket may be in bad state)
                _transport.Recreate();

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
                _logger.LogError("Reconnect error: " + ex.Message);
                RaiseError(ErrorCategory.Connection, "Reconnect failed: " + ex.Message, ex, null, true);
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
                    _transport.Subscribe(
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

            if (_transport != null && _transport.IsConnected)
            {
                _transport.Disconnect();
            }

            _intentionalDisconnect = false;

            // Update transport certificate and recreate
            _transport.UpdateClientCertificate(_clientCert);
            _transport.Recreate();

            var result = Connect();

            if (result == MqttReasonCode.Success)
            {
                ResubscribeAll();
                _logger.LogInfo("Certificate rotation complete. Reconnected.");
                return true;
            }

            _logger.LogError("Certificate rotation failed — could not reconnect.");
            RaiseError(ErrorCategory.Certificate, "Certificate rotation failed", null, null, true);
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

        // ───── Convenience Methods ─────

        /// <summary>
        /// Connects to the broker and subscribes to one or more topics in a single call.
        /// Simplifies the common pattern of connect → subscribe → ready.
        /// </summary>
        /// <param name="topics">Topics to subscribe to after connecting.</param>
        /// <returns>The MQTT reason code from the connection attempt.</returns>
        /// <example>
        /// <code>
        /// client.ConnectAndSubscribe("devices/myDevice/commands", "devices/myDevice/config");
        /// </code>
        /// </example>
        public MqttReasonCode ConnectAndSubscribe(params string[] topics)
        {
            var result = Connect();

            if (result == MqttReasonCode.Success && topics != null)
            {
                for (int i = 0; i < topics.Length; i++)
                {
                    if (topics[i] != null && topics[i].Length > 0)
                    {
                        Subscribe(topics[i], MqttQoSLevel.AtLeastOnce);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Publishes telemetry data as JSON to the standard telemetry topic.
        /// Topic format: <c>{prefix}/{deviceId}/telemetry</c>.
        /// </summary>
        /// <param name="data">The telemetry object to serialize as JSON.</param>
        /// <param name="topicPrefix">Topic prefix. Default is "devices".</param>
        /// <param name="qos">QoS level. Default is AtMostOnce.</param>
        /// <returns>The message ID (0 for QoS 0).</returns>
        /// <example>
        /// <code>
        /// client.PublishTelemetry(new { Temperature = 23.5, Humidity = 67.2 });
        /// </code>
        /// </example>
        public ushort PublishTelemetry(object data, string topicPrefix = "devices", MqttQoSLevel qos = MqttQoSLevel.AtMostOnce)
        {
            string topic = (topicPrefix ?? "devices") + "/" + _config.DeviceClientId + "/telemetry";
            return PublishJson(topic, data, qos);
        }

        /// <summary>
        /// Publishes a simple status message to the standard status topic.
        /// Topic format: <c>{prefix}/{deviceId}/status</c>.
        /// </summary>
        /// <param name="status">Status string (e.g., "online", "offline", "maintenance").</param>
        /// <param name="topicPrefix">Topic prefix. Default is "devices".</param>
        /// <returns>The message ID.</returns>
        public ushort PublishStatus(string status, string topicPrefix = "devices")
        {
            string topic = (topicPrefix ?? "devices") + "/" + _config.DeviceClientId + "/status";
            string json = "{\"deviceId\":\"" + _config.DeviceClientId + "\",\"status\":\"" + (status ?? "unknown") +
                          "\",\"timestamp\":\"" + DateTime.UtcNow.ToString("o") + "\"}";
            return Publish(topic, json, MqttQoSLevel.AtLeastOnce);
        }

        /// <summary>
        /// Gets the publish retry handler for monitoring retry statistics.
        /// Null if publish retry is not enabled.
        /// </summary>
        public RetryHandler PublishRetry => _publishRetryHandler;

        /// <summary>
        /// Gets the current free memory on the device in bytes.
        /// Convenience wrapper for <see cref="MemoryManager.GetFreeMemory"/>.
        /// </summary>
        /// <returns>Free memory in bytes, or -1 if unavailable.</returns>
        public long GetFreeMemory()
        {
            return MemoryManager.GetFreeMemory();
        }

        #endregion
    }
}
