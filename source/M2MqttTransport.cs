// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography.X509Certificates;
using nanoFramework.M2Mqtt;
using nanoFramework.M2Mqtt.Messages;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Default <see cref="IMqttTransport"/> implementation backed by the nanoFramework M2Mqtt library.
    /// <para>
    /// Encapsulates all raw MQTT operations (socket, TLS handshake, MQTT protocol) so the
    /// higher-level <see cref="EventGridMqttClient"/> deals only with Event Grid semantics.
    /// </para>
    /// </summary>
    /// <remarks>
    /// This class is the only place that directly references <see cref="MqttClient"/>.
    /// Replacing M2Mqtt with another library requires only a new <see cref="IMqttTransport"/> implementation.
    /// </remarks>
    internal class M2MqttTransport : IMqttTransport
    {
        private MqttClient _mqttClient;
        private readonly string _hostname;
        private readonly int _port;
        private readonly X509Certificate _caCert;
        private X509Certificate2 _clientCert;
        private readonly bool _useMqtt5;
        private readonly ILogger _logger;

        /// <inheritdoc />
        public event TransportMessageReceivedHandler MessageReceived;

        /// <inheritdoc />
        public event TransportMessagePublishedHandler MessagePublished;

        /// <inheritdoc />
        public event TransportConnectionClosedHandler ConnectionClosed;

        /// <inheritdoc />
        public bool IsConnected => _mqttClient != null && _mqttClient.IsConnected;

        /// <summary>
        /// Initializes a new instance of the <see cref="M2MqttTransport"/> class.
        /// </summary>
        /// <param name="hostname">The MQTT broker hostname.</param>
        /// <param name="port">The MQTT broker port.</param>
        /// <param name="caCert">CA certificate for TLS server validation.</param>
        /// <param name="clientCert">Client certificate for mutual TLS authentication.</param>
        /// <param name="useMqtt5">Whether to use MQTT v5.0 (true) or v3.1.1 (false).</param>
        /// <param name="logger">Optional logger.</param>
        public M2MqttTransport(
            string hostname,
            int port,
            X509Certificate caCert,
            X509Certificate2 clientCert,
            bool useMqtt5 = true,
            ILogger logger = null)
        {
            _hostname = hostname;
            _port = port;
            _caCert = caCert;
            _clientCert = clientCert;
            _useMqtt5 = useMqtt5;
            _logger = logger;

            CreateClient();
        }

        /// <inheritdoc />
        public MqttReasonCode Connect(
            string clientId,
            bool cleanSession,
            ushort keepAliveSeconds,
            string lwtTopic = null,
            string lwtMessage = null,
            MqttQoSLevel lwtQos = MqttQoSLevel.AtMostOnce,
            bool lwtRetain = false)
        {
            if (_mqttClient == null)
            {
                CreateClient();
            }

            bool hasLwt = lwtTopic != null && lwtTopic.Length > 0;

            if (hasLwt)
            {
                return _mqttClient.Connect(
                    clientId,
                    null,               // username (not used with X509)
                    null,               // password (not used with X509)
                    lwtRetain,
                    lwtQos,
                    true,               // willFlag
                    lwtTopic,
                    lwtMessage ?? "",
                    cleanSession,
                    keepAliveSeconds);
            }
            else
            {
                return _mqttClient.Connect(
                    clientId,
                    null,
                    null,
                    cleanSession,
                    keepAliveSeconds);
            }
        }

        /// <inheritdoc />
        public void Disconnect()
        {
            if (_mqttClient != null && _mqttClient.IsConnected)
            {
                _mqttClient.Disconnect();
            }
        }

        /// <inheritdoc />
        public ushort Publish(string topic, byte[] payload, MqttQoSLevel qos, bool retain)
        {
            return _mqttClient.Publish(topic, payload, null, null, qos, retain);
        }

        /// <inheritdoc />
        public void Subscribe(string[] topics, MqttQoSLevel[] qosLevels)
        {
            _mqttClient.Subscribe(topics, qosLevels);
        }

        /// <inheritdoc />
        public void Unsubscribe(string[] topics)
        {
            _mqttClient.Unsubscribe(topics);
        }

        /// <summary>
        /// Recreates the underlying MqttClient. Called during reconnection
        /// or certificate rotation when the old socket may be in a bad state.
        /// </summary>
        internal void Recreate()
        {
            DetachEvents();
            CreateClient();
        }

        /// <summary>
        /// Updates the client certificate for certificate rotation.
        /// Call <see cref="Recreate"/> after this to apply the new certificate.
        /// </summary>
        /// <param name="newClientCert">The new client certificate.</param>
        internal void UpdateClientCertificate(X509Certificate2 newClientCert)
        {
            _clientCert = newClientCert;
        }

        /// <summary>
        /// Detaches event handlers from the current MqttClient instance.
        /// Must be called before recreating the client.
        /// </summary>
        internal void DetachEvents()
        {
            if (_mqttClient != null)
            {
                _mqttClient.MqttMsgPublishReceived -= OnMqttMessageReceived;
                _mqttClient.MqttMsgPublished -= OnMqttMessagePublished;
                _mqttClient.ConnectionClosed -= OnMqttConnectionClosed;
            }
        }

        private void CreateClient()
        {
            _mqttClient = new MqttClient(
                _hostname,
                _port,
                true,                       // secure
                _caCert,                    // CA cert for server validation
                _clientCert,                // client cert for authentication
                MqttSslProtocols.TLSv1_2);

            if (_useMqtt5)
            {
                _mqttClient.ProtocolVersion = MqttProtocolVersion.Version_5;
            }

            _mqttClient.MqttMsgPublishReceived += OnMqttMessageReceived;
            _mqttClient.MqttMsgPublished += OnMqttMessagePublished;
            _mqttClient.ConnectionClosed += OnMqttConnectionClosed;
        }

        private void OnMqttMessageReceived(object sender, MqttMsgPublishEventArgs e)
        {
            MessageReceived?.Invoke(this, e.Topic, e.Message, (byte)e.QosLevel, e.Retain);
        }

        private void OnMqttMessagePublished(object sender, MqttMsgPublishedEventArgs e)
        {
            MessagePublished?.Invoke(this, e.MessageId, e.IsPublished);
        }

        private void OnMqttConnectionClosed(object sender, EventArgs e)
        {
            ConnectionClosed?.Invoke(this);
        }
    }
}
