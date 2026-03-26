// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using nanoFramework.TestFramework;

namespace nanoFramework.Azure.EventGrid.Mqtt.Tests
{
    [TestClass]
    public class CertificateHelperTests
    {
        // Valid PEM markers for test inputs (not real certificates)
        private const string ValidCaCert =
            "-----BEGIN CERTIFICATE-----\nMIIDjjCCAnagAwIBAgIQAzrx5qcRqaC7\n-----END CERTIFICATE-----";

        private const string ValidClientCert =
            "-----BEGIN CERTIFICATE-----\nMIICpDCCAYwCCQDU+pQ4pHgSoDANBgkq\n-----END CERTIFICATE-----";

        private const string ValidRsaKey =
            "-----BEGIN RSA PRIVATE KEY-----\nMIIEpAIBAAKCAQEA0Z3VS5JJcds3xfn/\n-----END RSA PRIVATE KEY-----";

        private const string ValidPkcs8Key =
            "-----BEGIN PRIVATE KEY-----\nMIIEvgIBADANBgkqhkiG9w0BAQEFAASC\n-----END PRIVATE KEY-----";

        // ───── ValidateCertificateStrings ─────

        [TestMethod]
        public void ValidateCertificateStrings_AllValid_ReturnsTrue()
        {
            bool result = CertificateHelper.ValidateCertificateStrings(ValidCaCert, ValidClientCert, ValidRsaKey);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ValidateCertificateStrings_Pkcs8Key_ReturnsTrue()
        {
            bool result = CertificateHelper.ValidateCertificateStrings(ValidCaCert, ValidClientCert, ValidPkcs8Key);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ValidateCertificateStrings_NullCaCert_ReturnsFalse()
        {
            bool result = CertificateHelper.ValidateCertificateStrings(null, ValidClientCert, ValidRsaKey);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ValidateCertificateStrings_EmptyCaCert_ReturnsFalse()
        {
            bool result = CertificateHelper.ValidateCertificateStrings("", ValidClientCert, ValidRsaKey);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ValidateCertificateStrings_NullClientCert_ReturnsFalse()
        {
            bool result = CertificateHelper.ValidateCertificateStrings(ValidCaCert, null, ValidRsaKey);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ValidateCertificateStrings_NullKey_ReturnsFalse()
        {
            bool result = CertificateHelper.ValidateCertificateStrings(ValidCaCert, ValidClientCert, null);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ValidateCertificateStrings_InvalidKeyFormat_ReturnsFalse()
        {
            bool result = CertificateHelper.ValidateCertificateStrings(ValidCaCert, ValidClientCert, "not-a-key");
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ValidateCertificateStrings_MissingCaMarkers_ReturnsFalse()
        {
            bool result = CertificateHelper.ValidateCertificateStrings("just-some-text", ValidClientCert, ValidRsaKey);
            Assert.IsFalse(result);
        }

        // ───── CreateCaCertificate ─────

        [TestMethod]
        public void CreateCaCertificate_NullInput_ThrowsArgumentException()
        {
            Assert.ThrowsException(typeof(ArgumentException), () =>
            {
                CertificateHelper.CreateCaCertificate(null);
            });
        }

        [TestMethod]
        public void CreateCaCertificate_EmptyInput_ThrowsArgumentException()
        {
            Assert.ThrowsException(typeof(ArgumentException), () =>
            {
                CertificateHelper.CreateCaCertificate("");
            });
        }

        [TestMethod]
        public void CreateCaCertificate_MissingMarkers_ThrowsArgumentException()
        {
            Assert.ThrowsException(typeof(ArgumentException), () =>
            {
                CertificateHelper.CreateCaCertificate("just-base64-data-with-no-markers");
            });
        }

        // ───── CreateClientCertificate ─────

        [TestMethod]
        public void CreateClientCertificate_NullCert_ThrowsArgumentException()
        {
            Assert.ThrowsException(typeof(ArgumentException), () =>
            {
                CertificateHelper.CreateClientCertificate(null, ValidRsaKey);
            });
        }

        [TestMethod]
        public void CreateClientCertificate_NullKey_ThrowsArgumentException()
        {
            Assert.ThrowsException(typeof(ArgumentException), () =>
            {
                CertificateHelper.CreateClientCertificate(ValidClientCert, null);
            });
        }

        [TestMethod]
        public void CreateClientCertificate_InvalidKeyFormat_ThrowsArgumentException()
        {
            Assert.ThrowsException(typeof(ArgumentException), () =>
            {
                CertificateHelper.CreateClientCertificate(ValidClientCert, "invalid-key-format");
            });
        }
    }
}
