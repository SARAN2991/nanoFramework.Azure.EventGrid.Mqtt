// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using nanoFramework.Json;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Delegate for certificate expiry warning events.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">Event data about the expiring certificate.</param>
    public delegate void CertificateExpiringEventHandler(object sender, CertificateExpiringEventArgs e);

    /// <summary>
    /// Delegate for certificate rotation completed events.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">Event data about the rotation.</param>
    public delegate void CertificateRotatedEventHandler(object sender, CertificateRotatedEventArgs e);

    /// <summary>
    /// Provides data for the <see cref="CertificateRotationManager.CertificateExpiring"/> event.
    /// </summary>
    public class CertificateExpiringEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the certificate expiry date (UTC).
        /// </summary>
        public DateTime ExpiryDateUtc { get; }

        /// <summary>
        /// Gets the number of days until the certificate expires.
        /// </summary>
        public int DaysUntilExpiry { get; }

        /// <summary>
        /// Gets the certificate subject name for identification.
        /// </summary>
        public string Subject { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CertificateExpiringEventArgs"/> class.
        /// </summary>
        public CertificateExpiringEventArgs(DateTime expiryDateUtc, int daysUntilExpiry, string subject)
        {
            ExpiryDateUtc = expiryDateUtc;
            DaysUntilExpiry = daysUntilExpiry;
            Subject = subject;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="CertificateRotationManager.CertificateRotated"/> event.
    /// </summary>
    public class CertificateRotatedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets whether the rotation was successful.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets the new certificate expiry date, if rotation was successful.
        /// </summary>
        public DateTime NewExpiryDateUtc { get; }

        /// <summary>
        /// Gets the reason/description of the rotation result.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CertificateRotatedEventArgs"/> class.
        /// </summary>
        public CertificateRotatedEventArgs(bool success, DateTime newExpiryDateUtc, string reason)
        {
            Success = success;
            NewExpiryDateUtc = newExpiryDateUtc;
            Reason = reason;
        }
    }

    /// <summary>
    /// Manages X.509 certificate lifecycle for EventGrid MQTT connections.
    /// <para>
    /// Features:
    /// <list type="bullet">
    ///   <item>Monitors certificate expiry and fires warnings before expiration</item>
    ///   <item>Accepts new certificates via MQTT topic for OTA certificate rotation</item>
    ///   <item>Supports runtime certificate swap and reconnection</item>
    ///   <item>Publishes certificate acknowledgment after successful rotation</item>
    /// </list>
    /// </para>
    /// <para>
    /// MQTT topic conventions:
    /// <list type="bullet">
    ///   <item><c>devices/{deviceId}/certificates/new</c> — Receive new certificate (subscribe)</item>
    ///   <item><c>devices/{deviceId}/certificates/ack</c> — Acknowledge rotation (publish)</item>
    ///   <item><c>devices/{deviceId}/certificates/status</c> — Report cert status (publish)</item>
    /// </list>
    /// </para>
    /// </summary>
    public class CertificateRotationManager : IMqttMessageHandler, IDisposable
    {
        private readonly string _deviceId;
        private readonly string _certNewTopic;
        private readonly string _certAckTopic;
        private readonly string _certStatusTopic;
        private readonly ILogger _logger;

        private string _currentCertPem;
        private string _currentKeyPem;
        private DateTime _certExpiryUtc;
        private readonly int _warningDaysBeforeExpiry;
        private readonly int _checkIntervalMs;
        private Thread _monitorThread;
        private bool _isMonitoring;
        private bool _disposed;
        private bool _warningFired;

        /// <summary>
        /// Fired when the client certificate is approaching expiry.
        /// </summary>
        public event CertificateExpiringEventHandler CertificateExpiring;

        /// <summary>
        /// Fired when a certificate rotation is completed (successfully or with error).
        /// </summary>
        public event CertificateRotatedEventHandler CertificateRotated;

        /// <summary>
        /// Gets the MQTT topic for receiving new certificates.
        /// </summary>
        public string CertNewTopic => _certNewTopic;

        /// <summary>
        /// Gets the MQTT topic for acknowledging certificate rotation.
        /// </summary>
        public string CertAckTopic => _certAckTopic;

        /// <summary>
        /// Gets the MQTT topic for publishing certificate status.
        /// </summary>
        public string CertStatusTopic => _certStatusTopic;

        /// <summary>
        /// Gets the current certificate expiry date (UTC).
        /// </summary>
        public DateTime CertExpiryUtc => _certExpiryUtc;

        /// <summary>
        /// Gets the number of days until the current certificate expires.
        /// Returns -1 if expiry is unknown.
        /// </summary>
        public int DaysUntilExpiry
        {
            get
            {
                if (_certExpiryUtc == DateTime.MinValue)
                {
                    return -1;
                }

                TimeSpan remaining = _certExpiryUtc - DateTime.UtcNow;
                return (int)remaining.TotalDays;
            }
        }

        /// <summary>
        /// Gets whether the certificate monitoring thread is running.
        /// </summary>
        public bool IsMonitoring => _isMonitoring;

        /// <summary>
        /// Gets whether the current certificate has expired.
        /// </summary>
        public bool IsExpired => _certExpiryUtc != DateTime.MinValue && DateTime.UtcNow > _certExpiryUtc;

        /// <summary>
        /// Gets the new client certificate PEM after a rotation. Null if no rotation has occurred.
        /// </summary>
        public string NewCertificatePem { get; private set; }

        /// <summary>
        /// Gets the new client private key PEM after a rotation. Null if no rotation has occurred.
        /// </summary>
        public string NewPrivateKeyPem { get; private set; }

        /// <summary>
        /// Gets whether a new certificate is pending (received but not yet applied).
        /// </summary>
        public bool HasPendingCertificate => NewCertificatePem != null;

        /// <summary>
        /// Initializes a new instance of the <see cref="CertificateRotationManager"/> class.
        /// </summary>
        /// <param name="deviceId">The device client ID.</param>
        /// <param name="currentCertPem">The current client certificate PEM.</param>
        /// <param name="currentKeyPem">The current client private key PEM.</param>
        /// <param name="warningDaysBeforeExpiry">Days before expiry to fire warning. Default is 30.</param>
        /// <param name="checkIntervalMs">How often to check expiry in ms. Default is 3600000 (1 hour).</param>
        /// <param name="topicPrefix">Topic prefix. Default is "devices".</param>
        /// <param name="logger">Optional logger for certificate rotation diagnostics.</param>
        public CertificateRotationManager(
            string deviceId,
            string currentCertPem,
            string currentKeyPem,
            int warningDaysBeforeExpiry = 30,
            int checkIntervalMs = 3600000,
            string topicPrefix = "devices",
            ILogger logger = null)
        {
            if (deviceId == null || deviceId.Length == 0)
            {
                throw new ArgumentException("deviceId cannot be null or empty.");
            }

            _deviceId = deviceId;
            _currentCertPem = currentCertPem;
            _currentKeyPem = currentKeyPem;
            _warningDaysBeforeExpiry = warningDaysBeforeExpiry;
            _checkIntervalMs = checkIntervalMs < 60000 ? 60000 : checkIntervalMs; // Minimum 1 minute
            _disposed = false;
            _isMonitoring = false;
            _warningFired = false;
            _logger = logger;
            NewCertificatePem = null;
            NewPrivateKeyPem = null;

            // Build topic strings
            string basePath = (topicPrefix ?? "devices") + "/" + _deviceId + "/certificates";
            _certNewTopic = basePath + "/new";
            _certAckTopic = basePath + "/ack";
            _certStatusTopic = basePath + "/status";

            // Try to extract expiry date from current certificate
            _certExpiryUtc = ExtractExpiryDate(currentCertPem);

            if (_certExpiryUtc != DateTime.MinValue)
            {
                _logger?.LogInfo("Certificate expires: " + _certExpiryUtc.ToString("yyyy-MM-dd") + " (" + DaysUntilExpiry + " days remaining)");
            }
            else
            {
                _logger?.LogWarning("Could not determine certificate expiry date.");
            }
        }

        /// <summary>
        /// Gets all MQTT topics that this manager needs to subscribe to.
        /// </summary>
        /// <returns>Array of topics: [certNew].</returns>
        public string[] GetSubscriptionTopics()
        {
            return new string[] { _certNewTopic };
        }

        /// <summary>
        /// Starts monitoring the certificate expiry in a background thread.
        /// </summary>
        public void StartMonitoring()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("CertificateRotationManager");
            }

            if (_isMonitoring)
            {
                return;
            }

            _isMonitoring = true;
            _monitorThread = new Thread(MonitorLoop);
            _monitorThread.Priority = ThreadPriority.BelowNormal;
            _monitorThread.Start();

            _logger?.LogInfo("CertRotation: Monitoring started. Warning at " + _warningDaysBeforeExpiry + " days before expiry.");
        }

        /// <summary>
        /// Stops monitoring certificate expiry.
        /// </summary>
        public void StopMonitoring()
        {
            _isMonitoring = false;
            _logger?.LogInfo("CertRotation: Monitoring stopped.");
        }

        /// <summary>
        /// Processes an incoming message on a certificate topic.
        /// Call this when a message is received on the <see cref="CertNewTopic"/>.
        /// </summary>
        /// <param name="topic">The MQTT topic.</param>
        /// <param name="payload">The JSON payload containing the new certificate.</param>
        /// <returns>True if the message was processed, false if not a cert topic.</returns>
        public bool ProcessMessage(string topic, string payload)
        {
            if (topic == null || payload == null)
            {
                return false;
            }

            if (topic == _certNewTopic)
            {
                ProcessNewCertificate(payload);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Manually sets a new certificate for rotation. Use this if you receive
        /// the certificate through a channel other than MQTT.
        /// </summary>
        /// <param name="newCertPem">The new client certificate PEM.</param>
        /// <param name="newKeyPem">The new client private key PEM.</param>
        /// <returns>True if the certificate is valid and accepted, false otherwise.</returns>
        public bool SetNewCertificate(string newCertPem, string newKeyPem)
        {
            if (!CertificateHelper.ValidateCertificateStrings("dummy-ca", newCertPem, newKeyPem))
            {
                _logger?.LogError("CertRotation: New certificate validation failed.");
                return false;
            }

            NewCertificatePem = newCertPem;
            NewPrivateKeyPem = newKeyPem;

            _logger?.LogInfo("CertRotation: New certificate accepted and pending application.");
            return true;
        }

        /// <summary>
        /// Applies the pending certificate. After calling this, the caller should
        /// update the <see cref="EventGridMqttConfig"/> and reconnect the MQTT client.
        /// </summary>
        /// <returns>True if a pending certificate was applied, false if none pending.</returns>
        public bool ApplyPendingCertificate()
        {
            if (!HasPendingCertificate)
            {
                _logger?.LogWarning("CertRotation: No pending certificate to apply.");
                return false;
            }

            _currentCertPem = NewCertificatePem;
            _currentKeyPem = NewPrivateKeyPem;

            // Update expiry
            DateTime newExpiry = ExtractExpiryDate(_currentCertPem);
            DateTime oldExpiry = _certExpiryUtc;
            _certExpiryUtc = newExpiry;
            _warningFired = false;

            // Clear pending
            NewCertificatePem = null;
            NewPrivateKeyPem = null;

            _logger?.LogInfo("CertRotation: Certificate applied. New expiry: " + _certExpiryUtc.ToString("yyyy-MM-dd"));

            CertificateRotated?.Invoke(this, new CertificateRotatedEventArgs(
                true, _certExpiryUtc, "Certificate rotated successfully."));

            return true;
        }

        /// <summary>
        /// Gets the current certificate PEM (after any rotations).
        /// </summary>
        public string CurrentCertificatePem => _currentCertPem;

        /// <summary>
        /// Gets the current private key PEM (after any rotations).
        /// </summary>
        public string CurrentPrivateKeyPem => _currentKeyPem;

        /// <summary>
        /// Builds a certificate status JSON payload for publishing.
        /// </summary>
        /// <returns>JSON string with certificate status.</returns>
        public string BuildStatusReport()
        {
            var status = new Hashtable();
            status["deviceId"] = _deviceId;
            status["daysUntilExpiry"] = DaysUntilExpiry;
            status["isExpired"] = IsExpired;
            status["hasPendingCert"] = HasPendingCertificate;
            status["timestamp"] = DateTime.UtcNow.ToString("o");

            if (_certExpiryUtc != DateTime.MinValue)
            {
                status["expiryDate"] = _certExpiryUtc.ToString("o");
            }

            return JsonConvert.SerializeObject(status);
        }

        /// <summary>
        /// Builds an acknowledgment JSON payload after certificate rotation.
        /// Publish this to <see cref="CertAckTopic"/> to confirm rotation.
        /// </summary>
        /// <param name="success">Whether the rotation was successful.</param>
        /// <param name="reason">Optional reason/description.</param>
        /// <returns>JSON acknowledgment payload.</returns>
        public string BuildRotationAck(bool success, string reason = null)
        {
            var ack = new Hashtable();
            ack["deviceId"] = _deviceId;
            ack["success"] = success;
            ack["reason"] = reason ?? (success ? "Certificate rotation successful" : "Certificate rotation failed");
            ack["timestamp"] = DateTime.UtcNow.ToString("o");

            if (success && _certExpiryUtc != DateTime.MinValue)
            {
                ack["newExpiryDate"] = _certExpiryUtc.ToString("o");
            }

            return JsonConvert.SerializeObject(ack);
        }

        /// <summary>
        /// Releases all resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopMonitoring();
        }

        #region Private Methods

        private void MonitorLoop()
        {
            while (_isMonitoring && !_disposed)
            {
                try
                {
                    Thread.Sleep(_checkIntervalMs);

                    if (!_isMonitoring || _disposed)
                    {
                        break;
                    }

                    if (_certExpiryUtc == DateTime.MinValue)
                    {
                        continue;
                    }

                    int daysLeft = DaysUntilExpiry;

                    if (daysLeft <= _warningDaysBeforeExpiry && !_warningFired)
                    {
                        _warningFired = true;

                        _logger?.LogWarning("CertRotation: Certificate expires in " + daysLeft + " days!");

                        CertificateExpiring?.Invoke(this, new CertificateExpiringEventArgs(
                            _certExpiryUtc, daysLeft, _deviceId));
                    }

                    if (daysLeft <= 0)
                    {
                        _logger?.LogError("CertRotation: CRITICAL — Certificate has EXPIRED!");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError("CertRotation: Monitor error: " + ex.Message);
                }
            }
        }

        private void ProcessNewCertificate(string payload)
        {
            try
            {
                _logger?.LogInfo("CertRotation: Received new certificate via MQTT.");

                Hashtable certData = (Hashtable)JsonConvert.DeserializeObject(payload, typeof(Hashtable));

                if (certData == null)
                {
                    _logger?.LogError("CertRotation: Failed to parse certificate payload.");
                    CertificateRotated?.Invoke(this, new CertificateRotatedEventArgs(
                        false, DateTime.MinValue, "Failed to parse certificate payload."));
                    return;
                }

                string newCertPem = certData.Contains("certificate") ? (string)certData["certificate"] : null;
                string newKeyPem = certData.Contains("privateKey") ? (string)certData["privateKey"] : null;

                if (newCertPem == null || newKeyPem == null)
                {
                    _logger?.LogError("CertRotation: Certificate payload missing 'certificate' or 'privateKey' fields.");
                    CertificateRotated?.Invoke(this, new CertificateRotatedEventArgs(
                        false, DateTime.MinValue, "Missing 'certificate' or 'privateKey' in payload."));
                    return;
                }

                if (SetNewCertificate(newCertPem, newKeyPem))
                {
                    _logger?.LogInfo("CertRotation: New certificate staged. Call ApplyPendingCertificate() to apply.");
                }
                else
                {
                    CertificateRotated?.Invoke(this, new CertificateRotatedEventArgs(
                        false, DateTime.MinValue, "Certificate validation failed."));
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("CertRotation: Error processing new certificate: " + ex.Message);
                CertificateRotated?.Invoke(this, new CertificateRotatedEventArgs(
                    false, DateTime.MinValue, "Error: " + ex.Message));
            }
        }

        private static DateTime ExtractExpiryDate(string certPem)
        {
            if (certPem == null || certPem.Length == 0)
            {
                return DateTime.MinValue;
            }

            try
            {
                // nanoFramework X509Certificate has GetExpirationDate() which returns DateTime
                var cert = new X509Certificate(certPem);
                return cert.GetExpirationDate();
            }
            catch
            {
                // Could not extract expiry date from certificate
            }

            return DateTime.MinValue;
        }

        #endregion
    }
}
