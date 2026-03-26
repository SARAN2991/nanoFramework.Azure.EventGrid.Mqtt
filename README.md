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
    .WithDeviceId("device01")
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
