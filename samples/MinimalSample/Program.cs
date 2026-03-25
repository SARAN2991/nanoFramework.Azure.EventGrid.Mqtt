// -----------------------------------------------------------------------
// Licensed under the MIT license. See LICENSE.md file in the project root.
// -----------------------------------------------------------------------

using nanoFramework.Azure.EventGrid.Mqtt;
using nanoFramework.Networking;
using System;
using System.Diagnostics;
using System.Threading;

namespace MinimalSample
{
    /// <summary>
    /// Minimal sample: the absolute simplest way to connect and publish telemetry.
    /// Uses the fluent builder API for clean, readable code.
    /// </summary>
    public class Program
    {
        public static void Main()
        {
            // Step 1: Connect to Wi-Fi
            if (!WifiNetworkHelper.ConnectDhcp("YOUR_SSID", "YOUR_PASSWORD",
                requiresDateTime: true,
                token: new CancellationTokenSource(30000).Token))
            {
                Debug.WriteLine("Wi-Fi failed!");
                Thread.Sleep(Timeout.Infinite);
                return;
            }

            // Step 2: Build client with fluent API → connect in one line
            using (var client = new EventGridMqttClientBuilder()
                .WithBroker("YOUR-NAMESPACE.westeurope-1.ts.eventgrid.azure.net")
                .WithDevice("esp32-device-001")
                .WithCertificates(CaCert, ClientCert, ClientKey)
                .WithAutoReconnect()
                .WithPublishRetry(maxRetries: 3)
                .BuildAndConnect())
            {
                Debug.WriteLine("Connected! Publishing telemetry...");

                // Step 3: Publish telemetry in a loop
                int count = 0;
                Random rnd = new Random();

                while (true)
                {
                    count++;

                    // One-liner telemetry publish with automatic topic + JSON serialization
                    client.PublishTelemetry(new
                    {
                        Temperature = 20.0 + (rnd.Next(0, 150) / 10.0),
                        Humidity = 40.0 + (rnd.Next(0, 400) / 10.0),
                        Count = count
                    });

                    Debug.WriteLine("Published #" + count);
                    Thread.Sleep(10000);
                }
            }
        }

        // ───── Certificates (paste your PEM strings here) ─────

        private const string CaCert =
@"-----BEGIN CERTIFICATE-----
YOUR_DIGICERT_GLOBAL_G2_ROOT_CA_HERE
-----END CERTIFICATE-----";

        private const string ClientCert =
@"-----BEGIN CERTIFICATE-----
YOUR_CLIENT_CERTIFICATE_HERE
-----END CERTIFICATE-----";

        private const string ClientKey =
@"-----BEGIN RSA PRIVATE KEY-----
YOUR_PRIVATE_KEY_HERE
-----END RSA PRIVATE KEY-----";
    }
}
