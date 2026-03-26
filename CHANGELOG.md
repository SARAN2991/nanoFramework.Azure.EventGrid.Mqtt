# Changelog

All notable changes to **nanoFramework.Azure.EventGrid.Mqtt** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0-preview] - 2026-03-26

### Added
- `EventGridMqttClient` — production-ready wrapper over nanoFramework.M2Mqtt for Azure Event Grid MQTT broker
- `EventGridMqttClientBuilder` — fluent builder for quick configuration
- `EventGridMqttClientFactory` — singleton factory for ESP32 memory safety
- `EventGridMqttConfig` — comprehensive configuration class with sensible defaults
- `IEventGridMqttClient` — interface for dependency injection and unit testing
- `IMqttTransport` / `M2MqttTransport` — transport abstraction separating MQTT from Event Grid semantics
- `ConnectionState` — connection state machine enum (Disconnected → Connecting → Connected → Reconnecting → Faulted)
- `ConnectionManager` — automatic reconnection with exponential backoff
- `RetryHandler` — publish retry with exponential backoff and jitter
- `OfflineMessageQueue` — bounded FIFO queue for messages published while disconnected
- `ClientErrorEventArgs` — structured error events with `ErrorCategory` and recoverability flags
- `CertificateHelper` — PEM string → X509Certificate/X509Certificate2 parsing with validation
- `CertificateRotationManager` — certificate expiry monitoring and OTA rotation via MQTT
- `DeviceTwinManager` — desired/reported state synchronization over MQTT topics
- `HealthReporter` — periodic heartbeat with uptime, free memory, and message counters
- `TopicHelper` — topic template building, wildcard construction, and validation
- `MemoryManager` — ESP32 memory management with low-memory detection and auto GC
- `ILogger` / `DebugLogger` / `NullLogger` — pluggable logging abstraction
- `IMqttMessageHandler` — extensible message handler interface for custom modules
- `willRetain=false` constraint guard — Azure Event Grid does not support retained messages
- X.509 mutual TLS authentication with PEM string input
- MQTT v5.0 and v3.1.1 protocol support
- Last Will and Testament (LWT) support
- JSON auto-serialization via `PublishJson()`
- Convenience methods: `ConnectAndSubscribe()`, `PublishTelemetry()`, `PublishStatus()`
- CI/CD pipeline via GitHub Actions (build, pack, publish to NuGet.org)
- Unit tests: TopicHelper, CertificateHelper, EventGridMqttConfig, OfflineMessageQueue
- Sample projects: BasicUsage, MinimalSample, RetryAndResilience
- Comprehensive README with certificate setup guide, API reference, and Azure setup instructions

[Unreleased]: https://github.com/SARAN2991/nanoFramework.Azure.EventGrid.Mqtt/compare/v0.1.0-preview...HEAD
[0.1.0-preview]: https://github.com/SARAN2991/nanoFramework.Azure.EventGrid.Mqtt/releases/tag/v0.1.0-preview
