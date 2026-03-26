// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Helper class for parsing and constructing X509 certificates
    /// from PEM-encoded strings for use with Azure EventGrid MQTT broker.
    /// </summary>
    public static class CertificateHelper
    {
        private const string CertBeginMarker = "-----BEGIN CERTIFICATE-----";
        private const string CertEndMarker = "-----END CERTIFICATE-----";
        private const string RsaKeyBeginMarker = "-----BEGIN RSA PRIVATE KEY-----";
        private const string RsaKeyEndMarker = "-----END RSA PRIVATE KEY-----";
        private const string Pkcs8KeyBeginMarker = "-----BEGIN PRIVATE KEY-----";
        private const string Pkcs8KeyEndMarker = "-----END PRIVATE KEY-----";

        /// <summary>
        /// Creates an X509Certificate from a PEM-encoded CA/root certificate string.
        /// Used for TLS server validation when connecting to the EventGrid MQTT broker.
        /// </summary>
        /// <param name="caCertPem">PEM-encoded CA certificate string including BEGIN/END markers.</param>
        /// <returns>An <see cref="X509Certificate"/> for server validation.</returns>
        /// <exception cref="ArgumentException">Thrown when the PEM string is null, empty, or missing markers.</exception>
        public static X509Certificate CreateCaCertificate(string caCertPem)
        {
            if (caCertPem == null || caCertPem.Length == 0)
            {
                throw new ArgumentException("CA certificate PEM cannot be null or empty.");
            }

            if (caCertPem.IndexOf(CertBeginMarker) < 0 || caCertPem.IndexOf(CertEndMarker) < 0)
            {
                throw new ArgumentException(
                    "CA certificate PEM must contain '-----BEGIN CERTIFICATE-----' and '-----END CERTIFICATE-----' markers.");
            }

            return new X509Certificate(caCertPem);
        }

        /// <summary>
        /// Creates an X509Certificate2 from PEM-encoded client certificate and private key strings.
        /// Used for mutual TLS authentication (thumbprint match) with the EventGrid MQTT broker.
        /// </summary>
        /// <param name="clientCertPem">PEM-encoded client public certificate string.</param>
        /// <param name="clientKeyPem">PEM-encoded client private key string (RSA or PKCS#8 format).</param>
        /// <returns>An <see cref="X509Certificate2"/> containing both public and private key for client authentication.</returns>
        /// <exception cref="ArgumentException">Thrown when PEM strings are null, empty, or missing markers.</exception>
        public static X509Certificate2 CreateClientCertificate(string clientCertPem, string clientKeyPem)
        {
            if (clientCertPem == null || clientCertPem.Length == 0)
            {
                throw new ArgumentException("Client certificate PEM cannot be null or empty.");
            }

            if (clientKeyPem == null || clientKeyPem.Length == 0)
            {
                throw new ArgumentException("Client private key PEM cannot be null or empty.");
            }

            if (clientCertPem.IndexOf(CertBeginMarker) < 0 || clientCertPem.IndexOf(CertEndMarker) < 0)
            {
                throw new ArgumentException(
                    "Client certificate PEM must contain '-----BEGIN CERTIFICATE-----' and '-----END CERTIFICATE-----' markers.");
            }

            bool hasRsaKey = clientKeyPem.IndexOf(RsaKeyBeginMarker) >= 0;
            bool hasPkcs8Key = clientKeyPem.IndexOf(Pkcs8KeyBeginMarker) >= 0;

            if (!hasRsaKey && !hasPkcs8Key)
            {
                throw new ArgumentException(
                    "Client private key PEM must contain '-----BEGIN RSA PRIVATE KEY-----' or '-----BEGIN PRIVATE KEY-----' markers.");
            }

            return new X509Certificate2(clientCertPem, clientKeyPem, "");
        }

        /// <summary>
        /// Validates that only the client certificate and private key strings are present and
        /// properly formatted. Use this when rotating client identity credentials without
        /// changing the CA certificate.
        /// Does not attempt to parse the certificates.
        /// </summary>
        /// <param name="clientCertPem">PEM-encoded client certificate.</param>
        /// <param name="clientKeyPem">PEM-encoded client private key.</param>
        /// <returns>True if both strings appear valid, false otherwise.</returns>
        public static bool ValidateClientCertificateStrings(string clientCertPem, string clientKeyPem)
        {
            if (clientCertPem == null || clientCertPem.Length == 0)
            {
                return false;
            }

            if (clientKeyPem == null || clientKeyPem.Length == 0)
            {
                return false;
            }

            if (clientCertPem.IndexOf(CertBeginMarker) < 0 || clientCertPem.IndexOf(CertEndMarker) < 0)
            {
                return false;
            }

            bool hasRsaKey = clientKeyPem.IndexOf(RsaKeyBeginMarker) >= 0;
            bool hasPkcs8Key = clientKeyPem.IndexOf(Pkcs8KeyBeginMarker) >= 0;

            return hasRsaKey || hasPkcs8Key;
        }

        /// <summary>
        /// Validates that the required certificate strings are present and properly formatted.
        /// Does not attempt to parse the certificates.
        /// </summary>
        /// <param name="caCertPem">PEM-encoded CA certificate.</param>
        /// <param name="clientCertPem">PEM-encoded client certificate.</param>
        /// <param name="clientKeyPem">PEM-encoded client private key.</param>
        /// <returns>True if all certificate strings appear valid, false otherwise.</returns>
        public static bool ValidateCertificateStrings(string caCertPem, string clientCertPem, string clientKeyPem)
        {
            if (caCertPem == null || caCertPem.Length == 0)
            {
                return false;
            }

            if (clientCertPem == null || clientCertPem.Length == 0)
            {
                return false;
            }

            if (clientKeyPem == null || clientKeyPem.Length == 0)
            {
                return false;
            }

            if (caCertPem.IndexOf(CertBeginMarker) < 0 || caCertPem.IndexOf(CertEndMarker) < 0)
            {
                return false;
            }

            if (clientCertPem.IndexOf(CertBeginMarker) < 0 || clientCertPem.IndexOf(CertEndMarker) < 0)
            {
                return false;
            }

            bool hasRsaKey = clientKeyPem.IndexOf(RsaKeyBeginMarker) >= 0;
            bool hasPkcs8Key = clientKeyPem.IndexOf(Pkcs8KeyBeginMarker) >= 0;

            if (!hasRsaKey && !hasPkcs8Key)
            {
                return false;
            }

            return true;
        }
    }
}
