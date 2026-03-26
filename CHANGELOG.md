# Changelog

All notable changes to **nanoFramework.Azure.EventGrid.Mqtt** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.0-preview] - 2026-03-26

### Added
- **Certificate Store support** — `EventGridMqttConfig` gains two new properties:
  - `CaCertificate` (`X509Certificate`) — pre-parsed CA/root certificate
  - `ClientCertificate` (`X509Certificate2`) — pre-parsed client certificate with embedded private key

  When either is set it takes precedence over the corresponding PEM string property.
  Use these to load certificates directly from the nanoFramework `X509Store` so no PEM string
  ever appears in source code or the compiled firmware binary.

- **`EventGridMqttClientBuilder.WithCertificatesFromStore(X509Certificate, X509Certificate2)`** —
  fluent method for the Certificate Store path.  Accepts pre-parsed objects retrieved from
  `X509Store` at runtime.  Full XML doc and code example included.

- **`CertificateStoreSample`** (`samples/CertificateStoreSample/`) — new end-to-end sample
  demonstrating production-recommended certificate provisioning:
  - Checks whether the device Certificate Store has been provisioned (halts with instructions if not)
  - Loads certificates from `X509Store` at runtime
  - Uses `WithCertificatesFromStore()` — no PEM string anywhere in the sample
  - Includes `FirstBootProvisioner` helper and inline comparison table

- **GitHub Actions secret scanning** (`.github/workflows/secret-scan.yml`) — `gitleaks/gitleaks-action@v2`
  runs on every push and every PR across all branches. Detects accidentally committed PEM keys,
  API tokens, and other credentials before they can be merged.

### Changed
- `EventGridMqttClient` constructor: certificate initialisation log message changed from
  `"Parsing certificates..."` / `"Certificates parsed successfully."` to
  `"Initialising certificates..."` / `"Certificates ready."` to reflect that pre-parsed
  objects are now accepted without any parsing step.
- `EventGridMqttClient.ValidateConfig`: updated to accept either PEM strings **or** pre-parsed
  `X509Certificate`/`X509Certificate2` objects — both paths satisfy the certificate requirement.
  Error messages updated to point to `WithCertificatesFromStore()` when neither is provided.
- **README.md** — comprehensive documentation update:
  - Features table: Security row updated to mention Certificate Store path and secret scanning
  - Quick Start: added Certificate Store variant alongside PEM variant
  - Step 4f renamed to "Embed PEM Strings (development only)" with prominent production warning
  - New Step 4g: step-by-step Certificate Store provisioning via `nanoff --certificate-store`
  - Step 5: now shows both Option A (PEM) and Option B (Certificate Store) application templates
  - Step 6d: debug output updated; step 6f error table extended with Certificate Store errors
  - Configuration Reference: `CaCertificate`/`ClientCertificate` documented alongside PEM properties
  - Fluent Builder API table: `WithCertificatesFromStore()` row added
  - Certificate Setup Guide: "Using in Code" split into Development and Production sub-sections
  - Project Structure: `CertificateStoreSample` and `secret-scan.yml` added

## [0.2.0-preview] - 2026-03-26

### Fixed
- **Thread safety** — `_subscribedTopics`, `_subscribedQos`, and `_messageHandlers` are now
  guarded by dedicated `_topicsLock` and `_handlersLock` fields. All reads and writes in
  `Subscribe()`, `Unsubscribe()`, `ResubscribeAll()`, `RegisterMessageHandler()`,
  `AutoSubscribeFeatureTopics()`, `OnTransportMessageReceived()`, and `Dispose()` are
  now protected against concurrent access from the reconnect thread and the M2Mqtt receive
  thread, eliminating a race condition that could cause corrupted state or
  `IndexOutOfRangeException` on-device.
- **PEM string security** — `EventGridMqttClient` now nulls `CaCertificatePem`,
  `ClientCertificatePem`, and `ClientPrivateKeyPem` on the config object immediately after
  the corresponding `X509Certificate`/`X509Certificate2` objects are constructed. This
  prevents the raw private key PEM string from persisting on the managed heap for the
  lifetime of the client, reducing exposure via JTAG or OTA memory-dump attacks on devices
  without OS-level process isolation. XML-doc on the three config properties updated to
  document this behaviour.

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

[Unreleased]: https://github.com/SARAN2991/nanoFramework.Azure.EventGrid.Mqtt/compare/v0.3.0-preview...HEAD
[0.3.0-preview]: https://github.com/SARAN2991/nanoFramework.Azure.EventGrid.Mqtt/compare/v0.2.0-preview...v0.3.0-preview
[0.2.0-preview]: https://github.com/SARAN2991/nanoFramework.Azure.EventGrid.Mqtt/compare/v0.1.0-preview...v0.2.0-preview
[0.1.0-preview]: https://github.com/SARAN2991/nanoFramework.Azure.EventGrid.Mqtt/releases/tag/v0.1.0-preview
