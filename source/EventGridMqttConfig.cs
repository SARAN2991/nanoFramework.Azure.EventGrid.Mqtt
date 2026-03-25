// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using nanoFramework.M2Mqtt.Messages;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Configuration for connecting to Azure EventGrid MQTT broker.
    /// </summary>
    public class EventGridMqttConfig
    {
        /// <summary>
        /// The MQTT hostname of the EventGrid Namespace.
        /// Must contain '.ts.' in the hostname (e.g., "mynamespace.westeurope-1.ts.eventgrid.azure.net").
        /// </summary>
        public string BrokerHostname { get; set; }

        /// <summary>
        /// The MQTT broker port. Default is 8883 (MQTTS).
        /// </summary>
        public int Port { get; set; } = 8883;

        /// <summary>
        /// The device client ID. Must match the client authentication name registered in the EventGrid Namespace.
        /// </summary>
        public string DeviceClientId { get; set; }

        /// <summary>
        /// The PEM-encoded CA/TLS root certificate for server validation (e.g., DigiCert Global Root G3).
        /// Must include the "-----BEGIN CERTIFICATE-----" and "-----END CERTIFICATE-----" markers.
        /// </summary>
        public string CaCertificatePem { get; set; }

        /// <summary>
        /// The PEM-encoded public certificate for the device client.
        /// Must include the "-----BEGIN CERTIFICATE-----" and "-----END CERTIFICATE-----" markers.
        /// </summary>
        public string ClientCertificatePem { get; set; }

        /// <summary>
        /// The PEM-encoded private key for the device client.
        /// Must include the "-----BEGIN RSA PRIVATE KEY-----" and "-----END RSA PRIVATE KEY-----" markers.
        /// </summary>
        public string ClientPrivateKeyPem { get; set; }

        /// <summary>
        /// Delay in milliseconds between reconnect attempts. Default is 5000ms.
        /// The delay doubles after each failed attempt (exponential backoff) up to <see cref="MaxReconnectDelayMs"/>.
        /// </summary>
        public int ReconnectDelayMs { get; set; } = 5000;

        /// <summary>
        /// Maximum delay in milliseconds between reconnect attempts. Default is 60000ms (60 seconds).
        /// </summary>
        public int MaxReconnectDelayMs { get; set; } = 60000;

        /// <summary>
        /// Maximum number of reconnect attempts. Default is 0 (infinite retries).
        /// </summary>
        public int MaxReconnectAttempts { get; set; } = 0;

        /// <summary>
        /// Whether to automatically reconnect on connection loss. Default is true.
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// MQTT keep-alive period in seconds. Default is 60 seconds.
        /// </summary>
        public ushort KeepAlivePeriodSeconds { get; set; } = 60;

        /// <summary>
        /// Whether to use a clean session on connect. Default is true.
        /// </summary>
        public bool CleanSession { get; set; } = true;

        /// <summary>
        /// Whether to use MQTT v5.0 protocol. Default is true.
        /// Set to false to use MQTT v3.1.1.
        /// </summary>
        public bool UseMqtt5 { get; set; } = true;

        /// <summary>
        /// Optional Last Will and Testament (LWT) topic.
        /// When set, the broker publishes the <see cref="LwtMessage"/> to this topic if the client disconnects unexpectedly.
        /// </summary>
        public string LwtTopic { get; set; }

        /// <summary>
        /// Optional Last Will and Testament message payload.
        /// Published to <see cref="LwtTopic"/> on unexpected disconnect.
        /// </summary>
        public string LwtMessage { get; set; }

        /// <summary>
        /// QoS level for the LWT message. Default is AtMostOnce (QoS 0).
        /// </summary>
        public MqttQoSLevel LwtQos { get; set; } = MqttQoSLevel.AtMostOnce;

        /// <summary>
        /// Whether to retain the LWT message. Default is false.
        /// Note: EventGrid Namespace may not support retain flag.
        /// </summary>
        public bool LwtRetain { get; set; } = false;

        // ───── Device Twin ─────

        /// <summary>
        /// Whether to enable device twin (shadow state) synchronization. Default is false.
        /// When enabled, a <see cref="DeviceTwinManager"/> is created and twin topics are auto-subscribed.
        /// </summary>
        public bool EnableDeviceTwin { get; set; } = false;

        /// <summary>
        /// Topic prefix for device twin topics. Default is "devices".
        /// Twin topics will be: {TwinTopicPrefix}/{DeviceClientId}/twin/desired, etc.
        /// </summary>
        public string TwinTopicPrefix { get; set; } = "devices";

        // ───── Health Reporting ─────

        /// <summary>
        /// Whether to enable built-in periodic health reporting. Default is false.
        /// When enabled, a <see cref="HealthReporter"/> publishes device health at the configured interval.
        /// </summary>
        public bool EnableHealthReporting { get; set; } = false;

        /// <summary>
        /// Health report interval in milliseconds. Default is 60000 (60 seconds).
        /// Minimum is 5000 (5 seconds).
        /// </summary>
        public int HealthReportIntervalMs { get; set; } = 60000;

        /// <summary>
        /// Topic prefix for health reporting. Default is "devices".
        /// Health topic will be: {HealthTopicPrefix}/{DeviceClientId}/health
        /// </summary>
        public string HealthTopicPrefix { get; set; } = "devices";

        // ───── Certificate Rotation ─────

        /// <summary>
        /// Whether to enable certificate rotation monitoring. Default is false.
        /// When enabled, a <see cref="CertificateRotationManager"/> monitors cert expiry
        /// and listens for new certificates via MQTT.
        /// </summary>
        public bool EnableCertificateRotation { get; set; } = false;

        /// <summary>
        /// Number of days before certificate expiry to fire the <see cref="CertificateRotationManager.CertificateExpiring"/> event.
        /// Default is 30 days.
        /// </summary>
        public int CertWarningDaysBeforeExpiry { get; set; } = 30;

        /// <summary>
        /// How often to check certificate expiry in milliseconds. Default is 3600000 (1 hour).
        /// </summary>
        public int CertCheckIntervalMs { get; set; } = 3600000;

        /// <summary>
        /// Topic prefix for certificate rotation topics. Default is "devices".
        /// Cert topics will be: {CertTopicPrefix}/{DeviceClientId}/certificates/new, etc.
        /// </summary>
        public string CertTopicPrefix { get; set; } = "devices";

        // ───── Publish Retry ─────

        /// <summary>
        /// Maximum number of retry attempts for failed publish operations. Default is 0 (no retry).
        /// When set to a value greater than 0, publishes that fail will be retried with exponential backoff.
        /// </summary>
        public int PublishMaxRetries { get; set; } = 0;

        /// <summary>
        /// Base delay in milliseconds between publish retry attempts. Default is 1000ms.
        /// The delay doubles after each failed attempt up to <see cref="PublishRetryMaxDelayMs"/>.
        /// </summary>
        public int PublishRetryBaseDelayMs { get; set; } = 1000;

        /// <summary>
        /// Maximum delay in milliseconds between publish retry attempts. Default is 30000ms.
        /// </summary>
        public int PublishRetryMaxDelayMs { get; set; } = 30000;

        // ───── Memory Management ─────

        /// <summary>
        /// Maximum payload size in bytes. Default is 0 (no limit).
        /// Recommended: 8192 (8KB) for ESP32 to prevent out-of-memory during JSON serialization.
        /// Publishes exceeding this limit will throw <see cref="System.ArgumentException"/>.
        /// </summary>
        public int MaxPayloadSize { get; set; } = 0;

        /// <summary>
        /// Whether to run garbage collection between publish operations when memory is low.
        /// Default is true. Helps prevent heap fragmentation on long-running ESP32 devices.
        /// </summary>
        public bool AutoGarbageCollect { get; set; } = true;

        // ───── Logging ─────

        /// <summary>
        /// Logger instance for library diagnostic output. Default is <see cref="DebugLogger"/>.
        /// Set to <see cref="NullLogger"/> to suppress all output, or provide a custom <see cref="ILogger"/> implementation.
        /// </summary>
        public ILogger Logger { get; set; } = new DebugLogger();
    }
}
