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
