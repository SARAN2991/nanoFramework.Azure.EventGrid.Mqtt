// -----------------------------------------------------------------------
// Licensed under the MIT license. See LICENSE.md file in the project root.
// -----------------------------------------------------------------------

using nanoFramework.Azure.EventGrid.Mqtt;
using nanoFramework.Networking;
using System;
using System.Diagnostics;
using System.Threading;

namespace RetryAndResilience
{
    /// <summary>
    /// Advanced sample demonstrating retry logic, exponential backoff,
    /// memory-aware publishing, and resilient reconnection on ESP32.
    /// </summary>
    public class Program
    {
        private const string WifiSsid = "YOUR_WIFI_SSID";
        private const string WifiPassword = "YOUR_WIFI_PASSWORD";
        private const string BrokerHostname = "YOUR-NAMESPACE.westeurope-1.ts.eventgrid.azure.net";
        private const string DeviceId = "esp32-resilient-001";

        private static int _publishFailures = 0;
        private static int _publishSuccesses = 0;

        public static void Main()
        {
            Debug.WriteLine("=== Retry & Resilience Sample ===");

            // ── Connect to Wi-Fi ──
            if (!WifiNetworkHelper.ConnectDhcp(WifiSsid, WifiPassword,
                requiresDateTime: true,
                token: new CancellationTokenSource(30000).Token))
            {
                Debug.WriteLine("[WiFi] FAILED");
                Thread.Sleep(Timeout.Infinite);
                return;
            }

            Debug.WriteLine("[WiFi] Connected.");

            // ── Build client with full resilience settings ──
            var client = new EventGridMqttClientBuilder()
                .WithBroker(BrokerHostname)
                .WithDevice(DeviceId)
                .WithCertificates(CaCert, ClientCert, ClientKey)

                // Reconnection: 5s → 10s → 20s → 40s → 60s (cap), infinite retries
                .WithAutoReconnect(
                    initialDelayMs: 5000,
                    maxDelayMs: 60000,
                    maxRetries: 0)            // 0 = infinite

                // Publish retry: 3 attempts with 1s → 2s → 4s backoff
                .WithPublishRetry(
                    maxRetries: 3,
                    baseDelayMs: 1000,
                    maxDelayMs: 30000)

                // Memory safety: reject payloads > 4KB
                .WithMaxPayloadSize(4096)

                // Last Will for offline detection
                .WithLastWill(
                    "devices/" + DeviceId + "/status",
                    "{\"status\":\"offline\",\"deviceId\":\"" + DeviceId + "\"}")

                // Enable health monitoring every 30s
                .WithHealthReporting(intervalMs: 30000)

                // Enable device twin
                .WithDeviceTwin()

                .Build();

            // ── Wire up resilience event handlers ──
            client.ConnectionStateChanged += (s, e) =>
            {
                string state = e.IsConnected ? "CONNECTED" : "DISCONNECTED";
                Debug.WriteLine("[Connection] " + state + " - " + e.Reason);

                if (e.ReconnectAttempt > 0)
                {
                    Debug.WriteLine("[Connection] Reconnect attempt #" + e.ReconnectAttempt);
                }
            };

            client.MessageReceived += (s, e) =>
            {
                Debug.WriteLine("[Message] " + e.Topic + ": " + e.Payload);
            };

            // ── Connect ──
            var result = client.ConnectAndSubscribe(
                "devices/" + DeviceId + "/commands",
                "devices/" + DeviceId + "/config");

            if (result != nanoFramework.M2Mqtt.Messages.MqttReasonCode.Success)
            {
                Debug.WriteLine("[MQTT] Connection failed: " + result);
                client.Dispose();
                Thread.Sleep(Timeout.Infinite);
                return;
            }

            // Report initial state via Device Twin
            if (client.Twin != null)
            {
                client.UpdateTwinProperty("firmwareVersion", "0.1.0");
                client.UpdateTwinProperty("retryEnabled", true);
            }

            // Publish online status
            client.PublishStatus("online");

            // ── Telemetry loop with memory monitoring ──
            Debug.WriteLine("[Telemetry] Starting resilient publish loop...");
            int count = 0;
            Random rnd = new Random();

            while (true)
            {
                count++;

                // Check memory before publishing
                long freeMem = client.GetFreeMemory();

                if (freeMem >= 0 && freeMem < 20000)
                {
                    Debug.WriteLine("[Memory] LOW (" + freeMem + " bytes free). Running GC...");
                    MemoryManager.CollectGarbage(true);
                    freeMem = MemoryManager.GetFreeMemory();
                    Debug.WriteLine("[Memory] After GC: " + freeMem + " bytes free.");
                }

                // PublishTelemetry uses retry handler automatically if configured
                try
                {
                    client.PublishTelemetry(new
                    {
                        Temperature = 20.0 + (rnd.Next(0, 150) / 10.0),
                        Humidity = 40.0 + (rnd.Next(0, 400) / 10.0),
                        FreeMemory = freeMem,
                        MessageCount = count
                    });

                    _publishSuccesses++;
                }
                catch (Exception ex)
                {
                    _publishFailures++;
                    Debug.WriteLine("[Telemetry] Publish error: " + ex.Message);
                }

                // Log retry statistics every 10 messages
                if (count % 10 == 0 && client.PublishRetry != null)
                {
                    Debug.WriteLine(
                        "[Stats] Published: " + _publishSuccesses +
                        " | Failed: " + _publishFailures +
                        " | Retries: " + client.PublishRetry.TotalRetries +
                        " | Free: " + MemoryManager.GetFreeMemory() + "B");
                }

                Thread.Sleep(10000);
            }
        }

        // ───── Certificates — development/prototyping only ─────────────────────
        // Security note: the library zeroes these strings from the managed heap
        // immediately after constructing the X.509 objects — they are not retained
        // for the lifetime of the client. For production use, see CertificateStoreSample.

        private const string CaCert =
@"-----BEGIN CERTIFICATE-----
MIIDjjCCAnagAwIBAgIQAzrx5qcRqaC7KGSxHQn65TANBgkqhkiG9w0BAQsFADBh
... (paste the full content of DigiCertGlobalRootG2.pem here) ...
-----END CERTIFICATE-----";

        private const string ClientCert =
@"-----BEGIN CERTIFICATE-----
MIICpDCCAYwCCQDU9pQpFnhMsTANBgkqhkiG9w0BAQsFADAUMRIwEAYDVQQDDAll
... (paste the full content of device01.pem here) ...
-----END CERTIFICATE-----";

        private const string ClientKey =
@"-----BEGIN RSA PRIVATE KEY-----
MIIEowIBAAKCAQEA2a2rwplBQLzygW5KrSMBcUKHQqfOBxklpR5SJdEEHSmfn59p
... (paste the full content of device01.key here) ...
-----END RSA PRIVATE KEY-----";
    }
}
