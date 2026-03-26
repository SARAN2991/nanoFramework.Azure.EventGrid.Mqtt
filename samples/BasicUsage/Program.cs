// -----------------------------------------------------------------------
// Licensed under the MIT license. See LICENSE.md file in the project root.
// -----------------------------------------------------------------------

using nanoFramework.Azure.EventGrid.Mqtt;
using nanoFramework.Networking;
using System;
using System.Diagnostics;
using System.Threading;

namespace BasicUsage
{
    /// <summary>
    /// Basic usage sample for nanoFramework.Azure.EventGrid.Mqtt.
    /// Demonstrates connecting an ESP32 to the Azure Event Grid MQTT broker,
    /// subscribing to command topics, and publishing telemetry.
    /// </summary>
    public class Program
    {
        // ───── Wi-Fi Configuration ─────
        private const string WifiSsid = "YOUR_WIFI_SSID";
        private const string WifiPassword = "YOUR_WIFI_PASSWORD";

        // ───── Azure Event Grid Configuration ─────
        private const string BrokerHostname = "YOUR-NAMESPACE.westeurope-1.ts.eventgrid.azure.net";
        private const string DeviceId = "esp32-device-001";

        // ───── Certificates — development/prototyping only ─────────────────────
        // Security note: the library zeroes these strings from the managed heap
        // immediately after constructing the X.509 objects — they are not retained
        // for the lifetime of the client. For production use, see CertificateStoreSample.
        private const string CaCertPem =
@"-----BEGIN CERTIFICATE-----
MIIDjjCCAnagAwIBAgIQAzrx5qcRqaC7KGSxHQn65TANBgkqhkiG9w0BAQsFADBh
... (paste the full content of DigiCertGlobalRootG2.pem here) ...
-----END CERTIFICATE-----";

        private const string ClientCertPem =
@"-----BEGIN CERTIFICATE-----
MIICpDCCAYwCCQDU9pQpFnhMsTANBgkqhkiG9w0BAQsFADAUMRIwEAYDVQQDDAll
... (paste the full content of device01.pem here) ...
-----END CERTIFICATE-----";

        private const string ClientKeyPem =
@"-----BEGIN RSA PRIVATE KEY-----
MIIEowIBAAKCAQEA2a2rwplBQLzygW5KrSMBcUKHQqfOBxklpR5SJdEEHSmfn59p
... (paste the full content of device01.key here) ...
-----END RSA PRIVATE KEY-----";

        // ───── Topics ─────
        private const string TelemetryTopic = "devices/" + DeviceId + "/telemetry";
        private const string CommandTopic = "devices/" + DeviceId + "/commands";
        private const string StatusTopic = "devices/" + DeviceId + "/status";

        private static int _messageCount = 0;

        public static void Main()
        {
            Debug.WriteLine("=== nanoFramework Azure EventGrid MQTT Sample ===");

            // ── Step 1: Connect to Wi-Fi ──
            Debug.WriteLine("[WiFi] Connecting...");
            bool wifiConnected = WifiNetworkHelper.ConnectDhcp(
                WifiSsid,
                WifiPassword,
                requiresDateTime: true,
                token: new CancellationTokenSource(30000).Token);

            if (!wifiConnected)
            {
                Debug.WriteLine("[WiFi] FAILED. Check SSID/password.");
                Thread.Sleep(Timeout.Infinite);
                return;
            }

            Debug.WriteLine($"[WiFi] Connected. UTC: {DateTime.UtcNow}");

            // ── Step 2: Configure the client ──
            var config = new EventGridMqttConfig
            {
                BrokerHostname = BrokerHostname,
                DeviceClientId = DeviceId,
                CaCertificatePem = CaCertPem,
                ClientCertificatePem = ClientCertPem,
                ClientPrivateKeyPem = ClientKeyPem,

                // Auto-reconnect with exponential backoff
                AutoReconnect = true,
                ReconnectDelayMs = 5000,
                MaxReconnectDelayMs = 60000,

                // Last Will & Testament
                LwtTopic = StatusTopic,
                LwtMessage = "{\"status\":\"offline\",\"deviceId\":\"" + DeviceId + "\"}",

                // Optional features
                EnableDeviceTwin = true,
                EnableHealthReporting = true,
                HealthReportIntervalMs = 30000,
            };

            // ── Step 3: Create client and connect ──
            using (var client = new EventGridMqttClient(config))
            {
                // Wire up events
                client.MessageReceived += OnMessageReceived;
                client.ConnectionStateChanged += OnConnectionStateChanged;

                // Connect
                var result = client.Connect();

                if (result != nanoFramework.M2Mqtt.Messages.MqttReasonCode.Success)
                {
                    Debug.WriteLine($"[MQTT] Connection failed: {result}");
                    Thread.Sleep(Timeout.Infinite);
                    return;
                }

                // Subscribe to commands
                client.Subscribe(CommandTopic);

                // Report initial device state via Device Twin
                if (client.Twin != null)
                {
                    client.Twin.DesiredStateChanged += (s, e) =>
                    {
                        Debug.WriteLine($"[Twin] Desired: {e.PropertyKey} = {e.PropertyValue}");
                    };

                    client.UpdateTwinProperty("firmwareVersion", "0.1.0");
                    client.UpdateTwinProperty("status", "running");
                }

                // Publish online status
                client.Publish(StatusTopic,
                    "{\"status\":\"online\",\"deviceId\":\"" + DeviceId + "\"}",
                    nanoFramework.M2Mqtt.Messages.MqttQoSLevel.AtLeastOnce);

                // ── Step 4: Telemetry loop ──
                Debug.WriteLine("[Telemetry] Starting publish loop (every 10s)...");
                Random rnd = new Random();

                while (true)
                {
                    _messageCount++;

                    double temperature = 20.0 + (rnd.Next(0, 150) / 10.0);
                    double humidity = 40.0 + (rnd.Next(0, 400) / 10.0);

                    var telemetry = new TelemetryData
                    {
                        DeviceId = DeviceId,
                        Temperature = temperature,
                        Humidity = humidity,
                        MessageCount = _messageCount,
                        Timestamp = DateTime.UtcNow.ToString("o")
                    };

                    Debug.WriteLine($"[Telemetry] #{_messageCount} Temp={temperature:F1}C Humidity={humidity:F1}%");
                    client.PublishJson(TelemetryTopic, telemetry, nanoFramework.M2Mqtt.Messages.MqttQoSLevel.AtLeastOnce);

                    Thread.Sleep(10000);
                }
            }
        }

        private static void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            Debug.WriteLine($"[Received] Topic: {e.Topic}");
            Debug.WriteLine($"[Received] Payload: {e.Payload}");
        }

        private static void OnConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
        {
            string state = e.IsConnected ? "CONNECTED" : "DISCONNECTED";
            Debug.WriteLine($"[Connection] {state} - {e.Reason}");
        }
    }

    /// <summary>
    /// Telemetry data model for JSON serialization.
    /// </summary>
    public class TelemetryData
    {
        public string DeviceId { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public int MessageCount { get; set; }
        public string Timestamp { get; set; }
    }
}
