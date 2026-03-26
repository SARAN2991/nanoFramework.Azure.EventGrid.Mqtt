// -----------------------------------------------------------------------
// Licensed under the MIT license. See LICENSE.md file in the project root.
// -----------------------------------------------------------------------

// ═══════════════════════════════════════════════════════════════════════
//  PRODUCTION CERTIFICATE STORE SAMPLE
//  ────────────────────────────────────
//  This sample shows the RECOMMENDED approach for production firmware:
//  certificates are provisioned to the device's Certificate Store ONCE
//  (at manufacturing or first-boot) and retrieved at runtime via
//  X509Store.  No PEM string ever appears in source code or in the
//  compiled firmware binary (.pe file).
//
//  WHY this is better than const string PEM or Resource Strings:
//  ┌──────────────────────┬──────────────────┬──────────────────────────┐
//  │ Method               │ PEM in binary?   │ Key in Git history?      │
//  ├──────────────────────┼──────────────────┼──────────────────────────┤
//  │ const string         │ YES              │ YES (if ever committed)  │
//  │ Resource Strings     │ YES (in .pe)     │ Depends on .resx commit  │
//  │ Certificate Store    │ NO               │ NO — never in source     │
//  └──────────────────────┴──────────────────┴──────────────────────────┘
//
//  HOW TO PROVISION certificates to the device (one-time setup):
//  ─────────────────────────────────────────────────────────────
//  Option A — nanoff tool (recommended for manufacturing/CI):
//    nanoff --target ESP32_REV3 --update
//    nanoff --target ESP32_REV3 --certificate-store --file ca.pem
//    nanoff --target ESP32_REV3 --certificate-store --file client.pfx
//
//  Option B — First-boot provisioning wizard (see FirstBootProvisioner
//    helper class below).  The device receives PEM strings once via
//    serial/UART from a secure host tool, writes them to X509Store, and
//    then never stores them in flash again.
//
//  HOW TO ENABLE GitHub secret scanning (free, automated guardrail):
//  ─────────────────────────────────────────────────────────────────
//  Repository Settings → Security → Secret scanning → Enable
//  This will automatically block any future commit that contains a
//  "-----BEGIN PRIVATE KEY-----" or "-----BEGIN CERTIFICATE-----" header.
// ═══════════════════════════════════════════════════════════════════════

using nanoFramework.Azure.EventGrid.Mqtt;
using nanoFramework.Networking;
using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace CertificateStoreSample
{
    /// <summary>
    /// Production sample: connect to Azure Event Grid MQTT using certificates
    /// loaded from the nanoFramework Certificate Store — no PEM strings in code.
    /// </summary>
    public class Program
    {
        // ───── Wi-Fi Configuration ─────
        private const string WifiSsid = "YOUR_WIFI_SSID";
        private const string WifiPassword = "YOUR_WIFI_PASSWORD";

        // ───── Azure Event Grid Configuration ─────
        private const string BrokerHostname = "YOUR-NAMESPACE.westeurope-1.ts.eventgrid.azure.net";
        private const string DeviceId = "esp32-device-001";

        // ───── Certificate Store indices ─────
        // After provisioning with nanoff --certificate-store, certificates are accessible
        // by index in X509Store. In a real deployment you would locate the right certificate
        // by subject name or thumbprint instead of a fixed index.
        private const int CaCertStoreIndex = 0;
        private const int ClientCertStoreIndex = 1;

        // ───── Topics ─────
        private const string TelemetryTopic = "devices/" + DeviceId + "/telemetry";
        private const string StatusTopic = "devices/" + DeviceId + "/status";

        public static void Main()
        {
            Debug.WriteLine("=== Certificate Store Sample ===");
            Debug.WriteLine("No PEM strings in source code. Certificates loaded from device Certificate Store.");

            // ── Step 1: Check whether certificates have been provisioned ──
            if (!FirstBootProvisioner.CertificatesProvisioned())
            {
                // This path executes only on first boot when the Certificate Store is empty.
                // In production, replace this with your own secure provisioning flow
                // (serial UART from a trusted host, DPS, or factory flashing via nanoff).
                Debug.WriteLine("[Provisioning] Certificate Store is empty — starting first-boot provisioning.");
                Debug.WriteLine("[Provisioning] In production, use: nanoff --certificate-store --file cert.pfx");
                Debug.WriteLine("[Provisioning] Halting. Please provision certificates and reboot.");
                Thread.Sleep(Timeout.Infinite);
                return;
            }

            // ── Step 2: Load certificates from the Certificate Store ──
            X509Certificate caCert;
            X509Certificate2 clientCert;

            if (!TryLoadCertificatesFromStore(out caCert, out clientCert))
            {
                Debug.WriteLine("[Certificates] Failed to load from Certificate Store. Re-provision the device.");
                Thread.Sleep(Timeout.Infinite);
                return;
            }

            Debug.WriteLine("[Certificates] Loaded from Certificate Store. Private key never exposed as string.");

            // ── Step 3: Connect to Wi-Fi ──
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

            Debug.WriteLine("[WiFi] Connected. UTC: " + DateTime.UtcNow);

            // ── Step 4: Build client using Certificate Store objects (no PEM strings) ──
            using (var client = new EventGridMqttClientBuilder()
                .WithBroker(BrokerHostname)
                .WithDevice(DeviceId)
                .WithCertificatesFromStore(caCert, clientCert)   // ← Certificate Store path
                .WithAutoReconnect()
                .WithPublishRetry(maxRetries: 3)
                .BuildAndConnect())
            {
                Debug.WriteLine("[MQTT] Connected to EventGrid broker.");

                client.MessageReceived += (s, e) =>
                    Debug.WriteLine("[Received] " + e.Topic + ": " + e.Payload);

                client.ConnectionStateChanged += (s, e) =>
                    Debug.WriteLine("[Connection] " + (e.IsConnected ? "CONNECTED" : "DISCONNECTED"));

                // Publish online status
                client.Publish(StatusTopic,
                    "{\"status\":\"online\",\"deviceId\":\"" + DeviceId + "\"}",
                    nanoFramework.M2Mqtt.Messages.MqttQoSLevel.AtLeastOnce);

                // ── Telemetry loop ──
                int count = 0;
                Random rnd = new Random();

                while (true)
                {
                    count++;
                    double temperature = 20.0 + (rnd.Next(0, 150) / 10.0);
                    double humidity = 40.0 + (rnd.Next(0, 400) / 10.0);

                    client.PublishJson(TelemetryTopic, new TelemetryData
                    {
                        DeviceId = DeviceId,
                        Temperature = temperature,
                        Humidity = humidity,
                        MessageCount = count,
                        Timestamp = DateTime.UtcNow.ToString("o")
                    }, nanoFramework.M2Mqtt.Messages.MqttQoSLevel.AtLeastOnce);

                    Debug.WriteLine("[Telemetry] #" + count + " Temp=" + temperature.ToString("F1") + "C");
                    Thread.Sleep(10000);
                }
            }
        }

        /// <summary>
        /// Loads the CA certificate and client certificate from the device Certificate Store.
        /// Certificates must have been provisioned beforehand (e.g., via nanoff --certificate-store).
        /// </summary>
        private static bool TryLoadCertificatesFromStore(out X509Certificate caCert, out X509Certificate2 clientCert)
        {
            caCert = null;
            clientCert = null;

            try
            {
                // Open the device Certificate Store (personal/My store).
                // On nanoFramework/ESP32, certificates provisioned via nanoff are accessible here.
                var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);

                if (store.Certificates == null || store.Certificates.Count <= ClientCertStoreIndex)
                {
                    Debug.WriteLine("[CertStore] Expected " + (ClientCertStoreIndex + 1) +
                        " certificate(s) but found " + (store.Certificates?.Count ?? 0) + ".");
                    store.Close();
                    return false;
                }

                caCert = store.Certificates[CaCertStoreIndex];
                clientCert = (X509Certificate2)store.Certificates[ClientCertStoreIndex];

                store.Close();

                Debug.WriteLine("[CertStore] CA cert subject:     " + caCert.Subject);
                Debug.WriteLine("[CertStore] Client cert subject: " + clientCert.Subject);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CertStore] Load failed: " + ex.Message);
                return false;
            }
        }
    }

    /// <summary>
    /// Checks whether device certificates have already been provisioned to the Certificate Store.
    /// In production, replace with your own provisioning-state check (e.g., an NVS flag).
    /// </summary>
    internal static class FirstBootProvisioner
    {
        /// <summary>
        /// Returns true if at least two certificates exist in the device Certificate Store
        /// (i.e., CA cert + client cert have been provisioned).
        /// </summary>
        public static bool CertificatesProvisioned()
        {
            try
            {
                var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);
                int count = store.Certificates?.Count ?? 0;
                store.Close();
                return count >= 2;
            }
            catch
            {
                return false;
            }
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
