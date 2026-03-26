// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using nanoFramework.M2Mqtt.Messages;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Fluent builder for constructing <see cref="EventGridMqttClient"/> instances
    /// with a clean, readable configuration API.
    /// </summary>
    /// <example>
    /// <code>
    /// var client = new EventGridMqttClientBuilder()
    ///     .WithBroker("mynamespace.westeurope-1.ts.eventgrid.azure.net")
    ///     .WithDevice("esp32-001")
    ///     .WithCertificates(caCert, clientCert, clientKey)
    ///     .WithAutoReconnect(maxRetries: 10)
    ///     .WithDeviceTwin()
    ///     .WithHealthReporting(intervalMs: 30000)
    ///     .Build();
    /// </code>
    /// </example>
    public class EventGridMqttClientBuilder
    {
        private readonly EventGridMqttConfig _config;

        /// <summary>
        /// Creates a new builder with default configuration.
        /// </summary>
        public EventGridMqttClientBuilder()
        {
            _config = new EventGridMqttConfig();
        }

        /// <summary>
        /// Sets the Azure Event Grid MQTT broker hostname.
        /// </summary>
        /// <param name="hostname">Broker hostname (e.g., "mynamespace.westeurope-1.ts.eventgrid.azure.net").</param>
        /// <param name="port">Broker port. Default is 8883.</param>
        /// <returns>This builder for method chaining.</returns>
        public EventGridMqttClientBuilder WithBroker(string hostname, int port = 8883)
        {
            _config.BrokerHostname = hostname;
            _config.Port = port;
            return this;
        }

        /// <summary>
        /// Sets the device client ID for MQTT authentication.
        /// </summary>
        /// <param name="deviceId">Device client ID matching the EventGrid client authentication name.</param>
        /// <returns>This builder for method chaining.</returns>
        public EventGridMqttClientBuilder WithDevice(string deviceId)
        {
            _config.DeviceClientId = deviceId;
            return this;
        }

        /// <summary>
        /// Sets the X.509 certificates for TLS mutual authentication.
        /// </summary>
        /// <param name="caCertPem">PEM-encoded CA/root certificate for server validation.</param>
        /// <param name="clientCertPem">PEM-encoded client public certificate.</param>
        /// <param name="clientKeyPem">PEM-encoded client private key.</param>
        /// <returns>This builder for method chaining.</returns>
        public EventGridMqttClientBuilder WithCertificates(string caCertPem, string clientCertPem, string clientKeyPem)
        {
            _config.CaCertificatePem = caCertPem;
            _config.ClientCertificatePem = clientCertPem;
            _config.ClientPrivateKeyPem = clientKeyPem;
            return this;
        }

        /// <summary>
        /// Enables automatic reconnection with exponential backoff.
        /// </summary>
        /// <param name="initialDelayMs">Initial delay between reconnect attempts (ms). Default is 5000.</param>
        /// <param name="maxDelayMs">Maximum delay cap (ms). Default is 60000.</param>
        /// <param name="maxRetries">Maximum retry attempts (0 = infinite). Default is 0.</param>
        /// <returns>This builder for method chaining.</returns>
        public EventGridMqttClientBuilder WithAutoReconnect(int initialDelayMs = 5000, int maxDelayMs = 60000, int maxRetries = 0)
        {
            _config.AutoReconnect = true;
            _config.ReconnectDelayMs = initialDelayMs;
            _config.MaxReconnectDelayMs = maxDelayMs;
            _config.MaxReconnectAttempts = maxRetries;
            return this;
        }

        /// <summary>
        /// Disables automatic reconnection.
        /// </summary>
        /// <returns>This builder for method chaining.</returns>
        public EventGridMqttClientBuilder WithoutAutoReconnect()
        {
            _config.AutoReconnect = false;
            return this;
        }

        /// <summary>
        /// Enables publish retry with exponential backoff.
        /// </summary>
        /// <param name="maxRetries">Maximum publish retry attempts. Default is 3.</param>
        /// <param name="baseDelayMs">Base delay between retries (ms). Default is 1000.</param>
        /// <param name="maxDelayMs">Maximum retry delay cap (ms). Default is 30000.</param>
        /// <returns>This builder for method chaining.</returns>
        public EventGridMqttClientBuilder WithPublishRetry(int maxRetries = 3, int baseDelayMs = 1000, int maxDelayMs = 30000)
        {
            _config.PublishMaxRetries = maxRetries;
            _config.PublishRetryBaseDelayMs = baseDelayMs;
            _config.PublishRetryMaxDelayMs = maxDelayMs;
            return this;
        }

        /// <summary>
        /// Configures Last Will and Testament for offline detection.
        /// </summary>
        /// <param name="topic">LWT topic (e.g., "devices/myDevice/status").</param>
        /// <param name="message">LWT payload (e.g., "{\"status\":\"offline\"}").</param>
        /// <param name="qos">QoS level for LWT. Default is AtMostOnce.</param>
        /// <returns>This builder for method chaining.</returns>
        public EventGridMqttClientBuilder WithLastWill(string topic, string message, MqttQoSLevel qos = MqttQoSLevel.AtMostOnce)
        {
            _config.LwtTopic = topic;
            _config.LwtMessage = message;
            _config.LwtQos = qos;
            return this;
        }

        /// <summary>
        /// Enables device twin (shadow state) synchronization.
        /// </summary>
        /// <param name="topicPrefix">Topic prefix for twin topics. Default is "devices".</param>
        /// <returns>This builder for method chaining.</returns>
        public EventGridMqttClientBuilder WithDeviceTwin(string topicPrefix = "devices")
        {
            _config.EnableDeviceTwin = true;
            _config.TwinTopicPrefix = topicPrefix;
            return this;
        }

        /// <summary>
        /// Enables periodic health reporting.
        /// </summary>
        /// <param name="intervalMs">Reporting interval in milliseconds. Default is 60000.</param>
        /// <param name="topicPrefix">Topic prefix for health topics. Default is "devices".</param>
        /// <returns>This builder for method chaining.</returns>
        public EventGridMqttClientBuilder WithHealthReporting(int intervalMs = 60000, string topicPrefix = "devices")
        {
            _config.EnableHealthReporting = true;
            _config.HealthReportIntervalMs = intervalMs;
            _config.HealthTopicPrefix = topicPrefix;
            return this;
        }

        /// <summary>
        /// Enables certificate rotation monitoring.
        /// </summary>
        /// <param name="warningDays">Days before expiry to trigger warning. Default is 30.</param>
        /// <param name="checkIntervalMs">Check interval in milliseconds. Default is 3600000 (1 hour).</param>
        /// <returns>This builder for method chaining.</returns>
        public EventGridMqttClientBuilder WithCertificateRotation(int warningDays = 30, int checkIntervalMs = 3600000)
        {
            _config.EnableCertificateRotation = true;
            _config.CertWarningDaysBeforeExpiry = warningDays;
            _config.CertCheckIntervalMs = checkIntervalMs;
            return this;
        }

        /// <summary>
        /// Enables offline message queueing when disconnected.
        /// Messages published while offline are automatically flushed on reconnect.
        /// </summary>
        /// <param name="maxSize">Maximum number of messages to queue. Default is 20.</param>
        /// <returns>This builder for method chaining.</returns>
        public EventGridMqttClientBuilder WithOfflineQueue(int maxSize = 20)
        {
            _config.EnableOfflineQueue = true;
            _config.MaxOfflineQueueSize = maxSize;
            return this;
        }

        /// <summary>
        /// Disables offline message queueing. Messages published while disconnected will throw.
        /// </summary>
        /// <returns>This builder for method chaining.</returns>
        public EventGridMqttClientBuilder WithoutOfflineQueue()
        {
            _config.EnableOfflineQueue = false;
            return this;
        }

        /// <summary>
        /// Sets the MQTT protocol version.
        /// </summary>
        /// <param name="useMqtt5">True for MQTT v5.0 (default), false for v3.1.1.</param>
        /// <returns>This builder for method chaining.</returns>
        public EventGridMqttClientBuilder WithMqttVersion(bool useMqtt5 = true)
        {
            _config.UseMqtt5 = useMqtt5;
            return this;
        }

        /// <summary>
        /// Sets a custom logger for the client.
        /// </summary>
        /// <param name="logger">The logger implementation.</param>
        /// <returns>This builder for method chaining.</returns>
        public EventGridMqttClientBuilder WithLogger(ILogger logger)
        {
            _config.Logger = logger;
            return this;
        }

        /// <summary>
        /// Suppresses all logging output.
        /// </summary>
        /// <returns>This builder for method chaining.</returns>
        public EventGridMqttClientBuilder WithSilentLogging()
        {
            _config.Logger = new NullLogger();
            return this;
        }

        /// <summary>
        /// Sets the maximum payload size in bytes. Publishes exceeding this limit are rejected.
        /// Default is 0 (no limit). Recommended: 8192 for ESP32.
        /// </summary>
        /// <param name="maxBytes">Maximum payload size in bytes. 0 = no limit.</param>
        /// <returns>This builder for method chaining.</returns>
        public EventGridMqttClientBuilder WithMaxPayloadSize(int maxBytes)
        {
            _config.MaxPayloadSize = maxBytes;
            return this;
        }

        /// <summary>
        /// Builds the <see cref="EventGridMqttConfig"/> without creating a client.
        /// Useful when you need to inspect or further modify the config.
        /// </summary>
        /// <returns>The constructed configuration.</returns>
        public EventGridMqttConfig BuildConfig()
        {
            return _config;
        }

        /// <summary>
        /// Builds and returns a new <see cref="EventGridMqttClient"/> with the configured settings.
        /// Does not connect — call <see cref="EventGridMqttClient.Connect"/> separately.
        /// </summary>
        /// <returns>A new client ready to connect.</returns>
        public EventGridMqttClient Build()
        {
            return new EventGridMqttClient(_config);
        }

        /// <summary>
        /// Builds a new <see cref="EventGridMqttClient"/> and immediately connects to the broker.
        /// Throws if connection fails.
        /// </summary>
        /// <returns>A connected client ready to publish and subscribe.</returns>
        /// <exception cref="System.Exception">Thrown if connection fails.</exception>
        public EventGridMqttClient BuildAndConnect()
        {
            var client = new EventGridMqttClient(_config);

            var result = client.Connect();

            if (result != MqttReasonCode.Success)
            {
                client.Dispose();
                throw new System.Exception("Connection failed: " + result);
            }

            return client;
        }
    }
}
