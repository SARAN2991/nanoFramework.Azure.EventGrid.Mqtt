# nanoFramework.Azure.EventGrid.Mqtt

[![NuGet](https://img.shields.io/nuget/vpre/nanoFramework.Azure.EventGrid.Mqtt.svg)](https://www.nuget.org/packages/nanoFramework.Azure.EventGrid.Mqtt/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.md)
[![Build](https://img.shields.io/github/actions/workflow/status/SARAN2991/nanoFramework.Azure.EventGrid.Mqtt/build.yml?branch=main)](https://github.com/SARAN2991/nanoFramework.Azure.EventGrid.Mqtt/actions)

A production-ready MQTT client library for **.NET nanoFramework** that simplifies connecting embedded devices (ESP32) to the **Azure Event Grid Namespace MQTT broker**. Reduces connection boilerplate from ~150 lines to ~15 lines.

## Features

| Category | Capabilities |
|---|---|
| **Core** | `Connect()`, `Publish()`, `Subscribe()`, `Disconnect()` — simple, developer-friendly API |
| **Architecture** | `IMqttTransport` separates MQTT from Event Grid; `ConnectionState` state machine; `EventGridMqttClientFactory` singleton |
| **Security** | X.509 certificate authentication with PEM string input; TLS 1.2; configurable auth |
| **Reliability** | Automatic reconnection with exponential backoff; publish retry with jitter; subscription persistence; offline message queue |
| **Error Handling** | Structured `ErrorOccurred` event with `ErrorCategory`, recoverability flags, and context |
| **Memory** | ESP32-optimized memory management; payload size limits; auto garbage collection; low-memory detection |
| **Protocol** | MQTT v5.0 and v3.1.1; Last Will & Testament (LWT); JSON auto-serialization |
| **Device Twin** | Desired/reported state synchronization over MQTT topics |
| **Health** | Periodic heartbeat with uptime, free memory, message counters, custom metrics |
| **Certificates** | Expiry monitoring, OTA certificate rotation via MQTT, runtime cert swap |
| **Developer UX** | Fluent `EventGridMqttClientBuilder`; one-liner `ConnectAndSubscribe()`; `PublishTelemetry()` / `PublishStatus()` helpers |
| **Extensibility** | `IEventGridMqttClient` interface; `ILogger` for pluggable logging; `IMqttMessageHandler` for custom modules |

## Prerequisites

| Requirement | Details |
|---|---|
| **Hardware** | ESP32 or any nanoFramework-supported board with network connectivity |
| **Firmware** | nanoFramework firmware flashed via [`nanoff`](https://github.com/nanoframework/nanoFirmwareFlasher) |
| **Azure** | An [Event Grid Namespace](https://learn.microsoft.com/azure/event-grid/mqtt-overview) with MQTT broker enabled |
| **Certificates** | CA root cert (e.g., DigiCert G2), device client certificate + private key |
| **IDE** | Visual Studio 2022 with the [nanoFramework Extension](https://marketplace.visualstudio.com/items?itemName=nanoframework.nanoFramework-VS2022-Extension) |

## Installation

Install the NuGet package in Visual Studio:

```
Install-Package nanoFramework.Azure.EventGrid.Mqtt -Version 0.1.0-preview
```

Or add it via the NuGet Package Manager in your nanoFramework project.

## Quick Start

### Using the Fluent Builder (Recommended)

```csharp
using nanoFramework.Azure.EventGrid.Mqtt;

var client = new EventGridMqttClientBuilder()
    .WithBroker("my-namespace.westeurope-1.ts.eventgrid.azure.net")
    .WithDevice("esp32-device-001")
    .WithCertificates(caCert, clientCert, clientKey)
    .WithAutoReconnect()
    .WithPublishRetry(maxRetries: 3)
    .WithOfflineQueue()
    .BuildAndConnect();

client.ErrorOccurred += (s, e) => Debug.WriteLine($"Error [{e.Category}]: {e.Message}");

client.Subscribe("devices/esp32-device-001/commands");
client.PublishTelemetry(new { temp = 23.5, humidity = 65.0 });

Thread.Sleep(Timeout.Infinite);
```

### One-Liner Connect + Subscribe

```csharp
var client = new EventGridMqttClientBuilder()
    .WithBroker(hostname).WithDevice(deviceId)
    .WithCertificates(ca, cert, key)
    .BuildAndConnect();

client.ConnectAndSubscribe("devices/my-device/commands", "devices/my-device/config");
```

### Manual Configuration

```csharp
using nanoFramework.Azure.EventGrid.Mqtt;

var config = new EventGridMqttConfig
{
    BrokerHostname = "my-namespace.westeurope-1.ts.eventgrid.azure.net",
    DeviceClientId = "esp32-device-001",
    CaCertificatePem   = caCert,     // PEM string
    ClientCertificatePem = clientCert, // PEM string
    ClientPrivateKeyPem  = clientKey,  // PEM string
    AutoReconnect = true,
};

using (var client = new EventGridMqttClient(config))
{
    client.MessageReceived += (s, e) =>
        Debug.WriteLine($"Received on {e.Topic}: {e.Payload}");

    client.Connect();
    client.Subscribe("devices/esp32-device-001/commands");
    client.Publish("devices/esp32-device-001/telemetry",
        "{\"temp\":23.5}", MqttQoSLevel.AtLeastOnce);

    Thread.Sleep(Timeout.Infinite);
}
```

## ESP32 Setup Guide

This section is written for **complete beginners**. Follow all six steps in order and you will have an ESP32 board publishing telemetry to Azure Event Grid over MQTT.

---

### Step 1 — Flash nanoFramework Firmware onto the ESP32

nanoFramework is a tiny .NET runtime that runs on microcontrollers. You have to flash it onto your board before you can deploy any C# code to it.

#### 1a — Install prerequisites

| Prerequisite | What to do |
|---|---|
| **.NET SDK 8** (or newer) | Download and install from [https://dot.net](https://dot.net). Run `dotnet --version` in a terminal to verify. |
| **USB driver for your board** | Most ESP32 boards use a CH340 or CP2102 USB-to-serial chip. Install the driver for your chip (search *"CH340 driver"* or *"CP2102 driver"* for your OS). |
| **USB cable** | Use a **data** cable (not a charge-only cable). Plug the board into your PC. |

#### 1b — Install the firmware flasher tool (nanoff)

Open a terminal (Command Prompt / PowerShell / Terminal) and run:

```bash
dotnet tool install -g nanoff
```

Verify it installed correctly:

```bash
nanoff --version
```

#### 1c — Find your board's COM port

**Windows:** Open **Device Manager → Ports (COM & LPT)**. Look for *USB Serial Device* or *CH340*. Note the port, e.g. `COM3`.

**Linux / macOS:** Run:

```bash
nanoff --listports
```

The port is usually `/dev/ttyUSB0` (Linux) or `/dev/cu.usbserial-*` (macOS).

#### 1d — Flash the firmware

Replace `COM3` with your actual port:

```bash
nanoff --target ESP32_REV0 --serialport COM3 --update
```

You should see progress messages and a final `Device flashed successfully`. The board will reboot automatically.

> **Which target should I use?**
> | Board variant | Target name |
> |---|---|
> | Standard ESP32 | `ESP32_REV0` |
> | ESP32-S3 | `ESP32_S3` |
> | ESP32-C3 | `ESP32_C3` |
> | ESP32-S2 | `ESP32_S2` |
> | WROVER module | `ESP32_WROVER` |
>
> A full list is at the [nanoFirmwareFlasher releases page](https://github.com/nanoframework/nanoFirmwareFlasher/releases).

---

### Step 2 — Create a nanoFramework Project in Visual Studio

#### 2a — Install Visual Studio 2022 and the nanoFramework extension

1. Download **Visual Studio 2022 Community** (free) from [https://visualstudio.microsoft.com](https://visualstudio.microsoft.com).
2. During installation, select the **.NET desktop development** workload.
3. After installation, open Visual Studio and go to **Extensions → Manage Extensions**.
4. Search for **nanoFramework** and install the [nanoFramework VS2022 Extension](https://marketplace.visualstudio.com/items?itemName=nanoframework.nanoFramework-VS2022-Extension).
5. Restart Visual Studio when prompted.

#### 2b — Create a new project

1. Open Visual Studio 2022.
2. Click **Create a new project**.
3. In the search box, type `nanoFramework`.
4. Select **nanoFramework Application** and click **Next**.
5. Set **Project name** to e.g. `MyEventGridDevice` and choose a folder. Click **Create**.

#### 2c — Set the target device

1. In **Solution Explorer**, right-click the project → **Properties**.
2. Click the **nanoFramework** tab on the left.
3. Under **Target**, choose the same target you used when flashing (e.g. `ESP32_REV0`).
4. Save and close the properties window.

> The project already contains a `Program.cs` file with a `Main()` method — this is where all your code will go.

---

### Step 3 — Install the NuGet Package

#### 3a — Via the Package Manager Console (quickest)

Go to **Tools → NuGet Package Manager → Package Manager Console** and run:

```powershell
Install-Package nanoFramework.Azure.EventGrid.Mqtt -Version 0.1.0-preview
```

#### 3b — Via the GUI

1. In **Solution Explorer**, right-click the project → **Manage NuGet Packages**.
2. Click the **Browse** tab.
3. In the search box type `nanoFramework.Azure.EventGrid.Mqtt`.
4. Select the package in the list, choose version `0.1.0-preview`, and click **Install**.
5. Accept the licence prompt.

> **What gets installed?** The package automatically pulls in all required nanoFramework libraries: `nanoFramework.M2Mqtt` (MQTT transport), `nanoFramework.Json` (JSON serialisation), `nanoFramework.System.Net` (networking), and several others. You do not need to install them separately.

---

### Step 4 — Generate Certificates and Embed Them in Code

Azure Event Grid requires every device to authenticate with **X.509 certificates**. You need three things:

| What | Purpose |
|---|---|
| **CA root certificate** | Lets your ESP32 *verify* that it is really talking to Azure (downloaded, not generated) |
| **Device certificate** | Tells Azure *who your ESP32 is* (you generate this) |
| **Device private key** | The secret that proves ownership of the device certificate (you generate this) |

#### 4a — Install OpenSSL

OpenSSL is a command-line tool used to generate certificates.

- **Windows:** Download the installer from [https://slproweb.com/products/Win32OpenSSL.html](https://slproweb.com/products/Win32OpenSSL.html). Install the *full* version (not Light). After installation, open a new Command Prompt and run `openssl version` to verify.
- **macOS:** OpenSSL is usually pre-installed. Run `openssl version` in Terminal to check.
- **Linux (Ubuntu/Debian):** Run `sudo apt install openssl`.

#### 4b — Download the CA Root Certificate

Your device needs the **DigiCert Global Root G2** certificate to verify the Azure Event Grid broker's identity.

1. Open your browser and go to: [https://www.digicert.com/kb/digicert-root-certificates.htm](https://www.digicert.com/kb/digicert-root-certificates.htm)
2. Find **DigiCert Global Root G2** and download the **PEM** file (it will be called something like `DigiCertGlobalRootG2.pem`).
3. Open the downloaded file in any text editor (Notepad, VS Code, etc.). It looks like this:

```
-----BEGIN CERTIFICATE-----
MIIDjjCCAnagAwIBAgIQAzrx5qcRqaC7KGSxHQn65TANBgkqhkiG9w0BAQsFADBh
MQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMRkwFwYDVQQLExB3
d3cuZGlnaWNlcnQuY29tMSAwHgYDVQQDExdEaWdpQ2VydCBHbG9iYWwgUm9vdCBH
... (more base64 lines) ...
-----END CERTIFICATE-----
```

4. Copy the **entire contents** of the file (including the `-----BEGIN CERTIFICATE-----` and `-----END CERTIFICATE-----` lines). You will paste this into your code in step 4e.

#### 4c — Generate a Device Private Key

Open a terminal/command prompt, create a folder for your device certificates, and run:

```bash
mkdir my-device-certs
cd my-device-certs

# Generate a 2048-bit RSA private key
openssl genrsa -out device01.key 2048
```

This creates a file called `device01.key`. **Keep this file private** — never share it or commit it to source control.

#### 4d — Generate a Device Certificate

First, create a Certificate Signing Request (CSR). The **CN** (Common Name) becomes the device client ID you register in Azure — keep it simple, no spaces.

```bash
# Create a Certificate Signing Request (CSR)
openssl req -new -key device01.key -out device01.csr -subj "/CN=esp32-device-001"

# Self-sign the certificate (valid for 365 days — use 730–1095 days for production)
openssl x509 -req -in device01.csr -signkey device01.key -out device01.pem -days 365
```

You now have two important files:
- `device01.pem` — the **device certificate** (public)
- `device01.key` — the **device private key** (secret)

Verify the certificate was created:

```bash
openssl x509 -in device01.pem -noout -text
```

You should see subject, validity dates, and other details.

#### 4e — Register the Device in Azure

You need an Azure subscription and the [Azure CLI](https://learn.microsoft.com/azure/cli/install) installed.

**4e-i: Create an Event Grid Namespace** (skip if you already have one):

```bash
az login

az eventgrid namespace create \
  --name my-eg-namespace \
  --resource-group my-rg \
  --location westeurope \
  --topic-spaces-configuration "{state:Enabled}"
```

**4e-ii: Get the certificate thumbprint**:

```bash
openssl x509 -in device01.pem -noout -fingerprint -sha256
```

This prints something like:
```
SHA256 Fingerprint=A1:B2:C3:D4:...:FF
```

Copy the value **after** `SHA256 Fingerprint=` and remove the colons: `A1B2C3D4...FF`.

**4e-iii: Register the device**:

```bash
az eventgrid namespace client create \
  --resource-group my-rg \
  --namespace-name my-eg-namespace \
  --client-name esp32-device-001 \
  --authentication "{thumbprintMatch:{primary:'A1B2C3D4...FF'}}" \
  --state Enabled
```

**4e-iv: Create a Topic Space and Permission Bindings** so the device is allowed to publish and subscribe:

```bash
# Create a topic space covering all device topics
az eventgrid namespace topic-space create \
  --name device-topics \
  --namespace-name my-eg-namespace \
  --resource-group my-rg \
  --topic-templates "devices/+/telemetry" "devices/+/commands" "devices/+/status"

# Allow all registered clients to publish
az eventgrid namespace permission-binding create \
  --name device-pub \
  --namespace-name my-eg-namespace \
  --resource-group my-rg \
  --client-group-name '$all' \
  --topic-space-name device-topics \
  --permission publisher

# Allow all registered clients to subscribe
az eventgrid namespace permission-binding create \
  --name device-sub \
  --namespace-name my-eg-namespace \
  --resource-group my-rg \
  --client-group-name '$all' \
  --topic-space-name device-topics \
  --permission subscriber
```

**4e-v: Note your broker hostname**. In the Azure Portal, open your Event Grid Namespace → **Overview**. Copy the **MQTT hostname** — it looks like:
```
my-eg-namespace.westeurope-1.ts.eventgrid.azure.net
```

#### 4f — Embed the PEM Strings in Your C# Code

Open your `device01.pem` and `device01.key` files in a text editor, then copy their full contents into `Program.cs` as `const string` constants.

```csharp
// ── CA Root Certificate (downloaded from DigiCert) ──────────────────────
// Paste the ENTIRE content of DigiCertGlobalRootG2.pem here
private const string CaCert =
@"-----BEGIN CERTIFICATE-----
MIIDjjCCAnagAwIBAgIQAzrx5qcRqaC7KGSxHQn65TANBgkqhkiG9w0BAQsFADBh
MQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMRkwFwYDVQQLExB3
d3cuZGlnaWNlcnQuY29tMSAwHgYDVQQDExdEaWdpQ2VydCBHbG9iYWwgUm9vdCBH
... paste all lines from your .pem file here ...
-----END CERTIFICATE-----";

// ── Device Certificate (your device01.pem) ───────────────────────────────
// Paste the ENTIRE content of device01.pem here
private const string ClientCert =
@"-----BEGIN CERTIFICATE-----
MIICpDCCAYwCCQDU9pQpFnhMsTANBgkqhkiG9w0BAQsFADAUMRIwEAYDVQQDDAll
... paste all lines from your device01.pem file here ...
-----END CERTIFICATE-----";

// ── Device Private Key (your device01.key) ───────────────────────────────
// Paste the ENTIRE content of device01.key here
private const string ClientKey =
@"-----BEGIN RSA PRIVATE KEY-----
MIIEowIBAAKCAQEA2a2rwplBQLF29amygykEMmYz0+Kcj3bKBp29ByP9iFyFbENN
... paste all lines from your device01.key file here ...
-----END RSA PRIVATE KEY-----";
```

> **Important embedding rules:**
> - Use a verbatim string literal `@"..."` so that the newlines inside the PEM are preserved.
> - Include the `-----BEGIN ...-----` and `-----END ...-----` header/footer lines.
> - Do **not** add extra spaces or wrap lines. Copy-paste the file content exactly.
> - Do **not** commit private keys to public repositories. For production devices, load them from secure storage at runtime.

---

### Step 5 — Write the Application

Replace the contents of `Program.cs` with the following. Each line has a comment explaining what it does:

```csharp
using nanoFramework.Azure.EventGrid.Mqtt;
using nanoFramework.Networking;
using System.Diagnostics;
using System.Threading;

namespace MyEventGridDevice
{
    public class Program
    {
        // ── Azure Event Grid broker hostname (from Step 4e-v) ──
        private const string BrokerHostname =
            "my-eg-namespace.westeurope-1.ts.eventgrid.azure.net";

        // ── Device client ID — must match the --client-name in Step 4e-iii ──
        private const string DeviceId = "esp32-device-001";

        // ── Wi-Fi credentials ──
        // Note: for production devices, load credentials from secure storage
        // rather than hardcoding them here.
        private const string WifiSsid     = "YOUR_WIFI_SSID";
        private const string WifiPassword = "YOUR_WIFI_PASSWORD";

        public static void Main()
        {
            Debug.WriteLine("=== Starting up ===");

            // ── 1. Connect to Wi-Fi ──────────────────────────────────────────
            // WifiNetworkHelper.ConnectDhcp blocks until connected or the
            // timeout fires.  requiresDateTime:true waits for the device clock
            // to be set via NTP (required for TLS certificate validation).
            // Increase WifiTimeoutMs to 60000 if NTP sync is slow on your network.
            const int WifiTimeoutMs = 30000;
            Debug.WriteLine("[WiFi] Connecting...");
            bool wifiOk = WifiNetworkHelper.ConnectDhcp(
                WifiSsid,
                WifiPassword,
                requiresDateTime: true,
                token: new CancellationTokenSource(WifiTimeoutMs).Token);

            if (!wifiOk)
            {
                Debug.WriteLine("[WiFi] Failed! Check SSID and password.");
                Thread.Sleep(Timeout.Infinite); // halt
                return;
            }

            Debug.WriteLine("[WiFi] Connected.");

            // ── 2. Build the MQTT client ─────────────────────────────────────
            // EventGridMqttClientBuilder uses a fluent (chain) API.
            // Each .WithXxx() call configures one feature.
            // BuildAndConnect() creates the client AND opens the TLS connection.
            using (var client = new EventGridMqttClientBuilder()
                .WithBroker(BrokerHostname)         // Azure endpoint
                .WithDevice(DeviceId)               // device identity
                .WithCertificates(CaCert, ClientCert, ClientKey) // mTLS auth
                .WithAutoReconnect()                // reconnect if Wi-Fi drops
                .WithPublishRetry(maxRetries: 3)    // retry failed publishes
                .BuildAndConnect())                 // connect now
            {
                Debug.WriteLine("[MQTT] Connected to Azure Event Grid.");

                // ── 3. Subscribe to receive commands from the cloud ──────────
                // Any message the cloud sends to this topic will fire the
                // MessageReceived event below.
                client.Subscribe("devices/" + DeviceId + "/commands");

                // ── 4. Wire up an event handler for incoming messages ────────
                client.MessageReceived += (sender, e) =>
                {
                    Debug.WriteLine("[Command] Topic:   " + e.Topic);
                    Debug.WriteLine("[Command] Payload: " + e.Payload);
                };

                // ── 5. Publish telemetry every 10 seconds ────────────────────
                // PublishTelemetry serialises the anonymous object to JSON and
                // sends it to: devices/{DeviceId}/telemetry
                int count = 0;
                while (true)
                {
                    count++;
                    client.PublishTelemetry(new
                    {
                        deviceId    = DeviceId,
                        temperature = 23.5,
                        humidity    = 60,
                        messageNo   = count
                    });
                    Debug.WriteLine("[Telemetry] Sent message #" + count);

                    Thread.Sleep(10000); // wait 10 seconds before next publish
                }
            }
        }

        // ── Paste your certificate PEM strings below ────────────────────────

        private const string CaCert =
@"-----BEGIN CERTIFICATE-----
PASTE_YOUR_DIGICERT_GLOBAL_ROOT_G2_PEM_HERE
-----END CERTIFICATE-----";

        private const string ClientCert =
@"-----BEGIN CERTIFICATE-----
PASTE_YOUR_DEVICE01_PEM_HERE
-----END CERTIFICATE-----";

        private const string ClientKey =
@"-----BEGIN RSA PRIVATE KEY-----
PASTE_YOUR_DEVICE01_KEY_HERE
-----END RSA PRIVATE KEY-----";
    }
}
```

Replace every `PASTE_YOUR_..._HERE` placeholder with the actual PEM content you copied in Step 4f.

---

### Step 6 — Deploy and Run

#### 6a — Connect the board

Plug your ESP32 into your PC with a USB data cable. Windows should show it in **Device Manager → Ports (COM & LPT)** as it did in Step 1c.

#### 6b — Select the device in Visual Studio

1. In Visual Studio, look at the toolbar at the top. There is a dropdown that shows **nanoFramework Device Explorer**.
2. If the board is not listed, go to **View → Other Windows → nanoFramework Device Explorer** and click the refresh icon.
3. Your device will appear as e.g. `ESP32_REV0 @ COM3`. Select it.

#### 6c — Deploy the application

Press **F5** (or **Debug → Start Debugging**). Visual Studio will:
1. Build the project.
2. Push the compiled assembly to the board over the COM port.
3. Start execution automatically.

The first deployment takes up to 30 seconds. Subsequent ones are faster.

#### 6d — Watch the output

Open **View → Output** and select **Debug** in the dropdown. You should see:

```
=== Starting up ===
[WiFi] Connecting...
[WiFi] Connected.
[EventGridMqtt] Parsing certificates...
[EventGridMqtt] Certificates parsed successfully.
[EventGridMqtt] Connecting to my-eg-namespace.westeurope-1.ts.eventgrid.azure.net:8883 as 'esp32-device-001'...
[EventGridMqtt] Connected successfully.
[EventGridMqtt] Subscribing to: devices/esp32-device-001/commands
[MQTT] Connected to Azure Event Grid.
[Telemetry] Sent message #1
[Telemetry] Sent message #2
...
```

#### 6e — Verify messages arrive in Azure

Open the Azure Portal, navigate to your Event Grid Namespace, and use the **MQTT Messages** monitor or subscribe another client to confirm the telemetry is arriving.

#### 6f — Common errors and fixes

| Error message | Likely cause | Fix |
|---|---|---|
| `[WiFi] Failed!` | Wrong SSID/password, or board too far from router | Double-check `WifiSsid` and `WifiPassword` |
| `[MQTT] Connection failed` | Certificates don't match what's registered in Azure | Re-run Step 4e-iii with the correct thumbprint |
| `Connection failed: BadUserNameOrPassword` | Device client ID doesn't match `--client-name` in Azure | Make sure `DeviceId` equals `--client-name` |
| `[EventGridMqtt] ERROR: Certificate ...` | PEM string has extra whitespace or missing lines | Re-copy the PEM file content; check `-----BEGIN`/`-----END` lines are present |
| Output window is empty | `requiresDateTime:true` timed out (no NTP sync) | Set `WifiTimeoutMs` to `60000` (60 s). This can happen on networks with slow or firewalled NTP servers — the device must sync its clock before TLS certificates can be validated. |

---

## Debug Output Window

When you run the application with the default `DebugLogger`, all library messages appear in Visual Studio's **Output** window under the **Debug** category. Below is a representative transcript for each lifecycle phase.

### Startup and Connection

```
[EventGridMqtt] Parsing certificates...
[EventGridMqtt] Certificates parsed successfully.
[EventGridMqtt] Offline message queue enabled (max 20 messages).
[EventGridMqtt] Device Twin enabled.
[EventGridMqtt] Health Reporting enabled.
[EventGridMqtt] Connecting to my-namespace.westeurope-1.ts.eventgrid.azure.net:8883 as 'esp32-device-001'...
[EventGridMqtt] Connected successfully.
[EventGridMqtt] Subscribing to: devices/esp32-device-001/commands
[EventGridMqtt] Health Reporter started: every 30s on 'devices/esp32-device-001/health'
```

### Normal Operation (Telemetry Publishing)

```
[EventGridMqtt] Subscribing to: devices/esp32-device-001/commands
[EventGridMqtt] Message received on 'devices/esp32-device-001/commands' (47 bytes)
[EventGridMqtt] Health report #1 published.
[EventGridMqtt] Health report #2 published.
```

### Retry on Publish Failure

```
[EventGridMqtt] WARN: Publish to 'devices/esp32-device-001/telemetry' failed: socket error
[EventGridMqtt] Publish retry 1/3 for 'devices/esp32-device-001/telemetry' in 1342ms
[EventGridMqtt] Publish to 'devices/esp32-device-001/telemetry' succeeded on retry 1
```

If all retries are exhausted:

```
[EventGridMqtt] WARN: Publish to 'devices/esp32-device-001/telemetry' failed: socket error
[EventGridMqtt] Publish retry 1/3 for 'devices/esp32-device-001/telemetry' in 1342ms
[EventGridMqtt] WARN: Publish retry 1 for 'devices/esp32-device-001/telemetry' failed: timeout
[EventGridMqtt] Publish retry 2/3 for 'devices/esp32-device-001/telemetry' in 2891ms
[EventGridMqtt] WARN: Publish retry 2 for 'devices/esp32-device-001/telemetry' failed: timeout
[EventGridMqtt] Publish retry 3/3 for 'devices/esp32-device-001/telemetry' in 5217ms
[EventGridMqtt] WARN: Publish retry 3 for 'devices/esp32-device-001/telemetry' failed: timeout
[EventGridMqtt] ERROR: Publish to 'devices/esp32-device-001/telemetry' failed after 3 retries.
```

### Auto-Reconnect Sequence

```
[EventGridMqtt] Connection closed.
[EventGridMqtt] Starting auto-reconnection...
[EventGridMqtt] Reconnect attempt 1, waiting 5000ms...
[EventGridMqtt] Reconnect attempt 1 failed.
[EventGridMqtt] Reconnect attempt 2, waiting 10000ms...
[EventGridMqtt] Reconnected successfully after 2 attempt(s).
[EventGridMqtt] Resubscribing to 1 topic(s)...
[EventGridMqtt] Resubscribed to all topics.
```

If max attempts are reached:

```
[EventGridMqtt] ERROR: Max reconnect attempts (5) reached.
```

### Certificate Monitoring

```
[EventGridMqtt] CertRotation: Monitoring started. Warning at 30 days before expiry.
[EventGridMqtt] CertRotation: Certificate expires: 2026-06-01 (28 days remaining)
[EventGridMqtt] WARN: Certificate expiring in 28 days!
[EventGridMqtt] CertRotation: Received new certificate via MQTT.
[EventGridMqtt] CertRotation: New certificate staged. Call ApplyPendingCertificate() to apply.
[EventGridMqtt] Applying certificate rotation...
[EventGridMqtt] Certificate rotation complete. Reconnected.
```

### Memory Pressure (ESP32)

When free heap falls below the configured threshold, the library logs a GC warning:

```
[EventGridMqtt] WARN: Low memory detected (18432 bytes). Running GC.
```

### Suppressing Logs

To silence all library output, pass a `NullLogger`:

```csharp
var client = new EventGridMqttClientBuilder()
    ...
    .WithSilentLogging()   // or .WithLogger(new NullLogger())
    .BuildAndConnect();
```

### Custom Logger

You can integrate with any logging framework by implementing `ILogger`:

```csharp
public class MyLogger : ILogger
{
    public void LogInfo(string message)    => MyApp.Log.Info(message);
    public void LogWarning(string message) => MyApp.Log.Warn(message);
    public void LogError(string message)   => MyApp.Log.Error(message);
}

var client = new EventGridMqttClientBuilder()
    ...
    .WithLogger(new MyLogger())
    .BuildAndConnect();
```

---

## API Overview

### EventGridMqttClient

The main client class implements `IEventGridMqttClient` and `IDisposable`.

| Method | Description |
|---|---|
| `Connect()` | Connects to the Azure Event Grid MQTT broker. Returns `MqttReasonCode`. |
| `Disconnect()` | Graceful disconnect. Stops reconnection and background features. |
| `Publish(topic, payload, qos, retain)` | Publishes a string payload. |
| `PublishJson(topic, data, qos, retain)` | Serializes an object to JSON and publishes. |
| `PublishRaw(topic, bytes, qos, retain)` | Publishes raw byte payload. |
| `Subscribe(topic, qos)` | Subscribes to a topic. Persists across reconnections. |
| `Unsubscribe(topic)` | Unsubscribes from a topic. |
| `RegisterMessageHandler(handler)` | Registers a custom `IMqttMessageHandler` for extensibility. |
| `UpdateTwinProperty(key, value)` | Shortcut to update a reported twin property and publish. |
| `RequestDesiredTwinState()` | Requests the current desired state from the cloud. |
| `ApplyCertificateRotation()` | Applies a pending certificate and reconnects. |
| `ConnectAndSubscribe(topics)` | Connects and subscribes to multiple topics in one call. |
| `PublishTelemetry(data, prefix, qos)` | Serializes + publishes to `devices/{id}/telemetry`. |
| `PublishStatus(status, prefix)` | Publishes a status string to `devices/{id}/status`. |
| `GetFreeMemory()` | Returns current free memory in bytes via GC. |

| Property | Description |
|---|---|
| `IsConnected` | Whether the client is connected. |
| `IsReconnecting` | Whether reconnection is in progress. |
| `State` | Current `ConnectionState` (Disconnected, Connecting, Connected, Reconnecting, Faulted). |
| `DeviceClientId` | The MQTT client ID. |
| `Twin` | `DeviceTwinManager` instance (null if disabled). |
| `Health` | `HealthReporter` instance (null if disabled). |
| `CertRotation` | `CertificateRotationManager` instance (null if disabled). |
| `PublishRetry` | `RetryHandler` instance (null if retry not configured). |
| `OfflineQueue` | `OfflineMessageQueue` instance (null if offline queue disabled). |

| Event | Description |
|---|---|
| `MessageReceived` | Fired when a message arrives on a subscribed topic. |
| `ConnectionStateChanged` | Fired on connect, disconnect, reconnect. |
| `MessagePublished` | Fired when a QoS 1 publish is acknowledged. |
| `ErrorOccurred` | Fired on any error with structured `ClientErrorEventArgs`. |

## Configuration Reference

```csharp
var config = new EventGridMqttConfig
{
    // Required
    BrokerHostname      = "ns.region.ts.eventgrid.azure.net",
    DeviceClientId      = "my-device",
    CaCertificatePem    = "-----BEGIN CERTIFICATE-----...",
    ClientCertificatePem = "-----BEGIN CERTIFICATE-----...",
    ClientPrivateKeyPem  = "-----BEGIN RSA PRIVATE KEY-----...",

    // Connection (optional)
    Port                   = 8883,          // default TLS port
    AutoReconnect          = true,          // auto-reconnect on disconnect
    ReconnectDelayMs       = 5000,          // initial backoff delay
    MaxReconnectDelayMs    = 60000,         // max backoff cap
    MaxReconnectAttempts   = 0,             // 0 = infinite
    KeepAlivePeriodSeconds = 60,
    CleanSession           = true,
    UseMqtt5               = true,

    // Last Will & Testament (optional)
    LwtTopic   = "devices/my-device/status",
    LwtMessage = "{\"status\":\"offline\"}",
    LwtQos     = MqttQoSLevel.AtLeastOnce,

    // Device Twin (optional)
    EnableDeviceTwin = false,
    TwinTopicPrefix  = "devices",

    // Health Reporting (optional)
    EnableHealthReporting  = false,
    HealthReportIntervalMs = 60000,

    // Certificate Rotation (optional)
    EnableCertificateRotation    = false,
    CertWarningDaysBeforeExpiry  = 30,
    CertCheckIntervalMs          = 3600000,

    // Publish Retry (optional)
    PublishMaxRetries       = 3,            // 0 = no retry (default)
    PublishRetryBaseDelayMs = 1000,         // initial retry delay
    PublishRetryMaxDelayMs  = 30000,        // max retry delay

    // Offline Message Queue (optional)
    EnableOfflineQueue     = true,           // queue when disconnected (default)
    MaxOfflineQueueSize    = 20,             // max queued messages

    // ESP32 Memory Management (optional)
    MaxPayloadSize     = 8192,              // 0 = no limit (default)
    AutoGarbageCollect = true,              // GC when memory is low

    // Logging (optional)
    Logger = new DebugLogger(),   // or NullLogger, or custom ILogger
};
```

## Fluent Builder API

The `EventGridMqttClientBuilder` provides a chainable API to configure and create clients:

```csharp
var client = new EventGridMqttClientBuilder()
    .WithBroker("ns.westeurope-1.ts.eventgrid.azure.net", port: 8883)
    .WithDevice("esp32-001")
    .WithCertificates(caCert, clientCert, clientKey)
    .WithAutoReconnect(maxAttempts: 10, initialDelayMs: 5000, maxDelayMs: 60000)
    .WithPublishRetry(maxRetries: 3, baseDelayMs: 1000, maxDelayMs: 30000)
    .WithLastWill("devices/esp32-001/status", "{\"status\":\"offline\"}")
    .WithDeviceTwin()
    .WithHealthReporting(intervalMs: 30000)
    .WithCertificateRotation(warningDays: 30)
    .WithMaxPayloadSize(8192)
    .WithLogger(new DebugLogger())
    .Build();
```

| Method | Description |
|---|---|
| `WithBroker(hostname, port)` | Sets the MQTT broker endpoint |
| `WithDevice(clientId)` | Sets the device client ID |
| `WithCertificates(ca, cert, key)` | Sets all three PEM certificate strings |
| `WithAutoReconnect(...)` | Enables auto-reconnect with backoff configuration |
| `WithPublishRetry(...)` | Enables publish retry with exponential backoff |
| `WithLastWill(topic, message)` | Sets LWT for offline detection |
| `WithDeviceTwin(prefix)` | Enables device twin synchronization |
| `WithHealthReporting(intervalMs)` | Enables periodic health heartbeat |
| `WithCertificateRotation(...)` | Enables certificate lifecycle management |
| `WithOfflineQueue(maxSize)` | Enables offline message queueing (default: 20 messages) |
| `WithoutOfflineQueue()` | Disables offline queue — throws when publishing while disconnected |
| `WithMaxPayloadSize(bytes)` | Sets max publish payload size (ESP32 memory safety) |
| `WithLogger(logger)` / `WithSilentLogging()` | Configures logging |
| `Build()` | Creates the client (does not connect) |
| `BuildAndConnect()` | Creates the client and immediately connects |
| `BuildConfig()` | Returns the `EventGridMqttConfig` without creating a client |

## Publish Retry with Exponential Backoff

When `PublishMaxRetries > 0`, failed publishes are automatically retried with exponential backoff and jitter to prevent thundering herd:

```csharp
var client = new EventGridMqttClientBuilder()
    .WithBroker(hostname).WithDevice(deviceId)
    .WithCertificates(ca, cert, key)
    .WithPublishRetry(maxRetries: 3, baseDelayMs: 1000, maxDelayMs: 30000)
    .BuildAndConnect();

// Publishes are automatically retried on failure
client.Publish("devices/esp32/telemetry", payload, MqttQoSLevel.AtLeastOnce);

// Check retry statistics
Debug.WriteLine($"Total retries: {client.PublishRetry.TotalRetries}");
Debug.WriteLine($"Total failures: {client.PublishRetry.TotalFailures}");
client.PublishRetry.ResetStatistics();
```

**Backoff formula:** `delay = baseDelay * 2^(attempt-1) + jitter`, capped at `maxDelayMs`.

## Connection State Machine

The client uses a proper `ConnectionState` enum instead of loose boolean flags:

```csharp
// Check the current state
if (client.State == ConnectionState.Connected)
{
    client.Publish("devices/esp32/data", payload);
}

// React to state changes
client.ConnectionStateChanged += (s, e) =>
{
    Debug.WriteLine($"State: {client.State}");
};
```

**State transitions:**
| From | To | Trigger |
|---|---|---|
| `Disconnected` | `Connecting` | `Connect()` called |
| `Connecting` | `Connected` | Connection success |
| `Connecting` | `Faulted` | Connection failure |
| `Connected` | `Disconnected` | `Disconnect()` called |
| `Connected` | `Reconnecting` | Unexpected connection drop |
| `Reconnecting` | `Connected` | Reconnect success |
| `Reconnecting` | `Faulted` | Max attempts exhausted |
| `Faulted` | `Connecting` | Manual `Connect()` retry |

## Structured Error Handling

The `ErrorOccurred` event provides structured error information for every failure:

```csharp
client.ErrorOccurred += (s, e) =>
{
    Debug.WriteLine($"[{e.Category}] {e.Message}");
    Debug.WriteLine($"  Recoverable: {e.IsRecoverable}");
    Debug.WriteLine($"  Context: {e.Context}");

    if (e.Exception != null)
    {
        Debug.WriteLine($"  Exception: {e.Exception.Message}");
    }
};
```

**Error categories:** `Connection`, `Publish`, `Subscribe`, `Certificate`, `Network`, `Internal`

## Offline Message Queue

When the connection is lost, messages are queued instead of being dropped. They are automatically flushed on reconnect:

```csharp
var client = new EventGridMqttClientBuilder()
    .WithBroker(hostname).WithDevice(deviceId)
    .WithCertificates(ca, cert, key)
    .WithAutoReconnect()
    .WithOfflineQueue(maxSize: 30)  // default 20
    .BuildAndConnect();

// These will be queued if connection drops, then auto-sent on reconnect
client.Publish("devices/esp32/telemetry", payload);

// Monitor queue stats
if (client.OfflineQueue != null)
{
    Debug.WriteLine($"Queued: {client.OfflineQueue.Count}");
    Debug.WriteLine($"Dropped: {client.OfflineQueue.DroppedCount}");
}
```

- FIFO eviction when full (oldest message dropped)
- Configurable max size (default 20 messages)
- Automatic flush on reconnect with per-message error handling

## Singleton Factory

On ESP32, only one TLS connection should exist to avoid memory exhaustion. Use `EventGridMqttClientFactory`:

```csharp
// Create once at startup
var client = EventGridMqttClientFactory.GetOrCreate(config);

// Access anywhere in the application
var sameClient = EventGridMqttClientFactory.Instance;

// Check if instance exists
if (EventGridMqttClientFactory.HasInstance)
{
    EventGridMqttClientFactory.Instance.PublishTelemetry(data);
}

// Replace or destroy
EventGridMqttClientFactory.Destroy();
```

## Transport Abstraction

The MQTT transport is separated from Event Grid logic via `IMqttTransport`:

```
EventGridMqttClient (Event Grid semantics: topic routing, twins, health, certs)
        │
        ▼
  IMqttTransport (abstract: connect, publish, subscribe)
        │
        ▼
  M2MqttTransport (concrete: nanoFramework.M2Mqtt wrapper)
```

Benefits:
- **Testability** — mock the transport for unit testing
- **Swappability** — replace M2Mqtt without changing Event Grid logic
- **Separation of concerns** — MQTT protocol isolated from business logic

## ESP32 Memory Management

The `MemoryManager` static class helps prevent out-of-memory crashes on memory-constrained ESP32 devices:

```csharp
// Payload size validation (automatic when MaxPayloadSize is set)
config.MaxPayloadSize = 8192; // reject payloads > 8KB

// Auto GC collection before publish when memory is low
config.AutoGarbageCollect = true;

// Manual memory checks
uint freeMemory = MemoryManager.GetFreeMemory();
bool isLow = MemoryManager.IsLowMemory;       // < 32KB free
bool isCritical = MemoryManager.IsCriticalMemory; // < 16KB free

// Safe payload check
if (MemoryManager.IsPayloadSizeSafe(myPayload.Length))
{
    client.Publish(topic, myPayload);
}
```

| Constant | Value | Description |
|---|---|---|
| `DefaultLowMemoryThreshold` | 32,768 bytes | Warning threshold |
| `DefaultCriticalMemoryThreshold` | 16,384 bytes | Critical threshold |
| `DefaultMaxPayloadSize` | 8,192 bytes | Default max payload size |

## Extensibility

### Custom Logger

```csharp
// Suppress all library logging
config.Logger = new NullLogger();

// Or implement your own
public class MyLogger : ILogger
{
    public void LogInfo(string message)    { /* ... */ }
    public void LogWarning(string message) { /* ... */ }
    public void LogError(string message)   { /* ... */ }
}
```

### Custom Message Handler

```csharp
public class CommandHandler : IMqttMessageHandler
{
    public string[] GetSubscriptionTopics() =>
        new[] { "devices/my-device/commands" };

    public bool ProcessMessage(string topic, string payload)
    {
        if (topic.EndsWith("/commands"))
        {
            Debug.WriteLine($"Command: {payload}");
            return true; // handled
        }
        return false;
    }
}

// Register with the client
client.RegisterMessageHandler(new CommandHandler());
```

### Interface for Testing

```csharp
// Use IEventGridMqttClient for dependency injection / mocking
public class MyService
{
    private readonly IEventGridMqttClient _client;

    public MyService(IEventGridMqttClient client)
    {
        _client = client;
    }

    public void SendTelemetry(double temp)
    {
        _client.Publish("telemetry", $"{{\"temp\":{temp}}}");
    }
}
```

## Device Twin

Sync desired and reported state between cloud and device over MQTT topics.

```csharp
config.EnableDeviceTwin = true;

using (var client = new EventGridMqttClient(config))
{
    client.Twin.DesiredStateChanged += (s, e) =>
    {
        Debug.WriteLine($"Desired: {e.PropertyKey} = {e.PropertyValue}");
    };

    client.Connect();
    client.UpdateTwinProperty("firmwareVersion", "0.1.0");
    client.RequestDesiredTwinState();
}
```

| Topic | Direction | Purpose |
|---|---|---|
| `devices/{id}/twin/desired` | Cloud -> Device | Desired state updates |
| `devices/{id}/twin/reported` | Device -> Cloud | Reported state |
| `devices/{id}/twin/get` | Device -> Cloud | Request current desired state |
| `devices/{id}/twin/res` | Cloud -> Device | Response with full desired state |

## Health Reporting

Automatic periodic heartbeat with device metrics.

```csharp
config.EnableHealthReporting = true;
config.HealthReportIntervalMs = 30000;

using (var client = new EventGridMqttClient(config))
{
    client.Health.HealthReportPublishing += (s, e) =>
    {
        e.HealthData["wifiRssi"] = -65;
        e.HealthData["sensorTemp"] = 23.5;
    };

    client.Connect();
    // Auto-publishes to: devices/{deviceId}/health
}
```

**Sample health report:**

```json
{
  "deviceId": "esp32-001",
  "uptimeSeconds": 3600,
  "freeMemoryBytes": 142560,
  "publishedMessages": 245,
  "receivedMessages": 12,
  "connectionDrops": 1,
  "reconnections": 1,
  "isConnected": true,
  "sequenceNumber": 120,
  "timestamp": "2026-03-25T10:30:00Z"
}
```

## Certificate Rotation

Monitor certificate expiry and rotate at runtime.

```csharp
config.EnableCertificateRotation = true;
config.CertWarningDaysBeforeExpiry = 30;

using (var client = new EventGridMqttClient(config))
{
    client.Connect();

    Debug.WriteLine($"Cert expires in {client.CertRotation.DaysUntilExpiry} days");

    // Apply a new cert received via MQTT or manually set
    if (client.CertRotation.HasPendingCertificate)
    {
        client.ApplyCertificateRotation();
        // Disconnects, swaps cert, reconnects, resubscribes automatically
    }
}
```

## Certificate Setup Guide

Azure Event Grid MQTT broker requires **mutual TLS (mTLS)** with X.509 certificates. You need **three** PEM-encoded items:

### 1. CA Root Certificate (Server Validation)

The broker's TLS certificate is issued by **DigiCert Global G2** (as of 2024). Download the root CA:

- **URL:** https://www.digicert.com/kb/digicert-root-certificates.htm
- **File:** DigiCert Global Root G2 (`.pem` format)
- **Purpose:** Your device uses this to validate the Event Grid broker's TLS identity

```text
// Store as a string constant in your firmware:
string caCert = @"-----BEGIN CERTIFICATE-----
MIIDjjCCAnagAwIBAgIQAzrx5qcRqaC7KGSxHQn65T...
-----END CERTIFICATE-----";
```

### 2. Client Certificate (Device Identity)

A device-specific certificate registered with your Event Grid namespace. Generate with OpenSSL:

```bash
# Generate private key
openssl genrsa -out device01.key 2048

# Generate Certificate Signing Request
openssl req -new -key device01.key -out device01.csr \
  -subj "/CN=device01"

# Self-sign (for dev/test) or submit CSR to your CA (for production)
openssl x509 -req -in device01.csr -signkey device01.key \
  -out device01.pem -days 365
```

### 3. Client Private Key

The private key matching your client certificate (generated in step 2 above).

### Register the Certificate in Azure

```bash
# Get the certificate thumbprint
openssl x509 -in device01.pem -noout -fingerprint -sha256

# Register in Event Grid Namespace (Azure CLI)
az eventgrid namespace client create \
  --resource-group <rg> \
  --namespace-name <namespace> \
  --client-name device01 \
  --authentication "{thumbprintMatch:{primary:'<SHA256-THUMBPRINT>'}}"
```

### Using in Code

```csharp
// Load PEM strings (from constants, files, or secure storage)
string caCert     = Resources.GetString(Resources.StringResources.CaCert);
string clientCert = Resources.GetString(Resources.StringResources.ClientCert);
string clientKey  = Resources.GetString(Resources.StringResources.ClientKey);

// Validate before connecting
bool valid = CertificateHelper.ValidateCertificateStrings(caCert, clientCert, clientKey);

// Use with builder
var client = new EventGridMqttClientBuilder()
    .WithBroker("ns.westus2.ts.eventgrid.azure.net")
    .WithDevice("device01")
    .WithCertificates(caCert, clientCert, clientKey)
    .Build();
```

> **Tip:** On ESP32, embed certificates as string resources or constants compiled into firmware.
> Avoid loading from SD card or flash filesystem in production, as it adds latency and failure points.

## Topic Helpers

```csharp
// Template-based topic building
string topic = TopicHelper.BuildTopic("devices/{deviceId}/telemetry", "esp32-001");
// -> "devices/esp32-001/telemetry"

// Wildcards
string wildcard = TopicHelper.BuildWildcardTopic("devices/esp32-001");
// -> "devices/esp32-001/#"

// Validation
bool valid = TopicHelper.ValidateTopic("devices/esp32/data");          // true
bool hostOk = TopicHelper.ValidateBrokerHostname("ns.region.ts.eventgrid.azure.net"); // true
```

## Azure Event Grid Setup

### 1. Create Event Grid Namespace

```bash
az eventgrid namespace create \
  --name my-eg-namespace \
  --resource-group my-rg \
  --location westeurope \
  --topic-spaces-configuration "{state:Enabled}"
```

### 2. Register Device Client

```bash
az eventgrid namespace client create \
  --name esp32-device-001 \
  --namespace-name my-eg-namespace \
  --resource-group my-rg \
  --authentication "{thumbprint:{primary:'<CERT_THUMBPRINT>'}}" \
  --state Enabled
```

### 3. Create Topic Space

```bash
az eventgrid namespace topic-space create \
  --name device-topics \
  --namespace-name my-eg-namespace \
  --resource-group my-rg \
  --topic-templates "devices/+/telemetry" "devices/+/commands" "devices/+/status"
```

### 4. Create Permission Bindings

```bash
az eventgrid namespace permission-binding create \
  --name device-pub \
  --namespace-name my-eg-namespace \
  --resource-group my-rg \
  --client-group-name '$all' \
  --topic-space-name device-topics \
  --permission publisher

az eventgrid namespace permission-binding create \
  --name device-sub \
  --namespace-name my-eg-namespace \
  --resource-group my-rg \
  --client-group-name '$all' \
  --topic-space-name device-topics \
  --permission subscriber
```

## Architecture

```
ESP32 Device                              Azure Event Grid
+-----------------------------+           +------------------------+
|  Your Application           |           |  Namespace             |
|         |                   |           |  +------------------+  |
|  EventGridMqttClientFactory |           |  |  MQTT Broker     |  |
|         |                   |           |  |  +-----------+   |  |
|  EventGridMqttClient        |           |  |  | Topic     |   |  |
|    (Event Grid semantics)   |           |  |  | Spaces    |   |  |
|    +- ConnectionState       |           |  |  +-----------+   |  |
|    +- OfflineMessageQueue   |           |  +------------------+  |
|    +- ErrorOccurred event   |           +------------------------+
|    +- DeviceTwinManager     |
|    +- HealthReporter        |
|    +- CertRotationMgr       |
|    +- RetryHandler          |
|    +- [Custom Handlers]     |
|         |                   |
|    IMqttTransport           |
|         |                   |
|    M2MqttTransport ---------|--- MQTT 5 / TLS 8883 / X.509 --->
+-----------------------------+
```

## Dependencies

| Package | Version |
|---|---|
| nanoFramework.CoreLibrary | 1.17.11 |
| nanoFramework.M2Mqtt | 5.1.206 |
| nanoFramework.Json | 2.2.138 |
| nanoFramework.System.Text | 1.3.42 |
| nanoFramework.Runtime.Events | 1.11.32 |
| nanoFramework.System.Collections | 1.5.67 |
| nanoFramework.System.IO.Streams | 1.1.96 |
| nanoFramework.System.Net | 1.11.47 |
| nanoFramework.System.Threading | 1.1.52 |
| nanoFramework.Runtime.Native | 1.7.11 |

## Building

1. Open `nanoFramework.Azure.EventGrid.Mqtt.sln` in **Visual Studio 2022** with the nanoFramework extension
2. Restore NuGet packages
3. Build in Release mode

### Packing for NuGet

```bash
nuget pack nanoFramework.Azure.EventGrid.Mqtt.nuspec -Version 0.1.0-preview
```

### Publishing to NuGet.org

```bash
nuget push nanoFramework.Azure.EventGrid.Mqtt.0.1.0-preview.nupkg \
  -Source https://api.nuget.org/v3/index.json -ApiKey YOUR_API_KEY
```

## Project Structure

```
source/
  IEventGridMqttClient.cs        # Client interface for DI/testing
  ILogger.cs                     # Logging abstraction + DebugLogger, NullLogger
  IMqttMessageHandler.cs         # Extensible message handler interface
  IMqttTransport.cs              # Transport abstraction (MQTT separated from Event Grid)
  M2MqttTransport.cs             # Concrete IMqttTransport wrapping nanoFramework.M2Mqtt
  EventGridMqttClient.cs         # Main client (Event Grid semantics only)
  EventGridMqttClientBuilder.cs  # Fluent builder for easy configuration
  EventGridMqttClientFactory.cs  # Singleton factory for ESP32 memory safety
  EventGridMqttConfig.cs         # Configuration class
  EventGridMqttEventArgs.cs      # Event argument types
  ClientErrorEventArgs.cs        # Structured error event args + ErrorCategory enum
  ConnectionState.cs             # Connection state machine enum
  ConnectionManager.cs           # Auto-reconnect with exponential backoff
  RetryHandler.cs                # Publish retry with exponential backoff + jitter
  OfflineMessageQueue.cs         # Bounded FIFO queue for disconnected scenarios
  MemoryManager.cs               # ESP32 memory management utilities
  CertificateHelper.cs           # X.509 certificate parsing from PEM
  TopicHelper.cs                 # Topic building and validation
  DeviceTwinManager.cs           # Device twin state synchronization
  HealthReporter.cs              # Periodic health heartbeat
  CertificateRotationManager.cs  # Certificate lifecycle management
samples/
  BasicUsage/                    # Complete sample — manual config
  MinimalSample/                 # Minimal sample — fluent builder + one-liners
  RetryAndResilience/            # Full resilience demo — retry, memory, health
```

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.

## License

This project is licensed under the [MIT License](LICENSE.md).
