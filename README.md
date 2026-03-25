# nanoFramework.Azure.EventGrid.Mqtt

[![NuGet](https://img.shields.io/nuget/vpre/nanoFramework.Azure.EventGrid.Mqtt.svg)](https://www.nuget.org/packages/nanoFramework.Azure.EventGrid.Mqtt/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.md)
[![Build](https://img.shields.io/github/actions/workflow/status/nanoframework/nanoFramework.Azure.EventGrid.Mqtt/build.yml?branch=main)](https://github.com/nanoframework/nanoFramework.Azure.EventGrid.Mqtt/actions)

A production-ready MQTT client library for **.NET nanoFramework** that simplifies connecting embedded devices (ESP32) to the **Azure Event Grid Namespace MQTT broker**. Reduces connection boilerplate from ~150 lines to ~15 lines.

## Features

| Category | Capabilities |
|---|---|
| **Core** | `Connect()`, `Publish()`, `Subscribe()`, `Disconnect()` — simple, developer-friendly API |
| **Security** | X.509 certificate authentication with PEM string input; TLS 1.2; configurable auth |
| **Reliability** | Automatic reconnection with exponential backoff; subscription persistence across reconnects |
| **Protocol** | MQTT v5.0 and v3.1.1; Last Will & Testament (LWT); JSON auto-serialization |
| **Device Twin** | Desired/reported state synchronization over MQTT topics |
| **Health** | Periodic heartbeat with uptime, free memory, message counters, custom metrics |
| **Certificates** | Expiry monitoring, OTA certificate rotation via MQTT, runtime cert swap |
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

| Property | Description |
|---|---|
| `IsConnected` | Whether the client is connected. |
| `IsReconnecting` | Whether reconnection is in progress. |
| `DeviceClientId` | The MQTT client ID. |
| `Twin` | `DeviceTwinManager` instance (null if disabled). |
| `Health` | `HealthReporter` instance (null if disabled). |
| `CertRotation` | `CertificateRotationManager` instance (null if disabled). |

| Event | Description |
|---|---|
| `MessageReceived` | Fired when a message arrives on a subscribed topic. |
| `ConnectionStateChanged` | Fired on connect, disconnect, reconnect. |
| `MessagePublished` | Fired when a QoS 1 publish is acknowledged. |

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

    // Logging (optional)
    Logger = new DebugLogger(),   // or NullLogger, or custom ILogger
};
```

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
+-------------------------+               +------------------------+
|  Your Application       |               |  Namespace             |
|         |               |               |  +------------------+  |
|  EventGridMqttClient    |--- MQTT 5 --->|  |  MQTT Broker     |  |
|    +- CertificateHelper |    TLS 8883   |  |  +-----------+   |  |
|    +- TopicHelper       |    X.509 Auth |  |  | Topic     |   |  |
|    +- ConnectionManager |<-------------|  |  | Spaces    |   |  |
|    +- DeviceTwinManager |               |  |  +-----------+   |  |
|    +- HealthReporter    |               |  +------------------+  |
|    +- CertRotationMgr   |               +------------------------+
|    +- [Custom Handlers] |
+-------------------------+
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
  IEventGridMqttClient.cs     # Client interface for DI/testing
  ILogger.cs                   # Logging abstraction + DebugLogger, NullLogger
  IMqttMessageHandler.cs       # Extensible message handler interface
  EventGridMqttClient.cs       # Main client implementation
  EventGridMqttConfig.cs       # Configuration class
  EventGridMqttEventArgs.cs    # Event argument types
  ConnectionManager.cs         # Auto-reconnect with exponential backoff
  CertificateHelper.cs         # X.509 certificate parsing from PEM
  TopicHelper.cs               # Topic building and validation
  DeviceTwinManager.cs         # Device twin state synchronization
  HealthReporter.cs            # Periodic health heartbeat
  CertificateRotationManager.cs # Certificate lifecycle management
samples/
  BasicUsage/                  # Complete sample application
```

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.

## License

This project is licensed under the [MIT License](LICENSE.md).
