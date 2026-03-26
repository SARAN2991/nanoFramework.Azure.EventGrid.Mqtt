// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using nanoFramework.M2Mqtt.Messages;
using nanoFramework.TestFramework;

namespace nanoFramework.Azure.EventGrid.Mqtt.Tests
{
    [TestClass]
    public class EventGridMqttConfigTests
    {
        [TestMethod]
        public void DefaultConfig_HasCorrectPort()
        {
            var config = new EventGridMqttConfig();
            Assert.AreEqual(8883, config.Port);
        }

        [TestMethod]
        public void DefaultConfig_AutoReconnectIsTrue()
        {
            var config = new EventGridMqttConfig();
            Assert.IsTrue(config.AutoReconnect);
        }

        [TestMethod]
        public void DefaultConfig_UseMqtt5IsTrue()
        {
            var config = new EventGridMqttConfig();
            Assert.IsTrue(config.UseMqtt5);
        }

        [TestMethod]
        public void DefaultConfig_CleanSessionIsTrue()
        {
            var config = new EventGridMqttConfig();
            Assert.IsTrue(config.CleanSession);
        }

        [TestMethod]
        public void DefaultConfig_LwtRetainIsFalse()
        {
            var config = new EventGridMqttConfig();
            Assert.IsFalse(config.LwtRetain, "LwtRetain must default to false for Azure Event Grid compatibility.");
        }

        [TestMethod]
        public void DefaultConfig_KeepAliveIs60()
        {
            var config = new EventGridMqttConfig();
            Assert.AreEqual((ushort)60, config.KeepAlivePeriodSeconds);
        }

        [TestMethod]
        public void DefaultConfig_ReconnectDelayIs5000()
        {
            var config = new EventGridMqttConfig();
            Assert.AreEqual(5000, config.ReconnectDelayMs);
        }

        [TestMethod]
        public void DefaultConfig_MaxReconnectDelayIs60000()
        {
            var config = new EventGridMqttConfig();
            Assert.AreEqual(60000, config.MaxReconnectDelayMs);
        }

        [TestMethod]
        public void DefaultConfig_MaxReconnectAttemptsIsInfinite()
        {
            var config = new EventGridMqttConfig();
            Assert.AreEqual(0, config.MaxReconnectAttempts);
        }

        [TestMethod]
        public void DefaultConfig_DeviceTwinDisabled()
        {
            var config = new EventGridMqttConfig();
            Assert.IsFalse(config.EnableDeviceTwin);
        }

        [TestMethod]
        public void DefaultConfig_HealthReportingDisabled()
        {
            var config = new EventGridMqttConfig();
            Assert.IsFalse(config.EnableHealthReporting);
        }

        [TestMethod]
        public void DefaultConfig_CertRotationDisabled()
        {
            var config = new EventGridMqttConfig();
            Assert.IsFalse(config.EnableCertificateRotation);
        }

        [TestMethod]
        public void DefaultConfig_OfflineQueueEnabled()
        {
            var config = new EventGridMqttConfig();
            Assert.IsTrue(config.EnableOfflineQueue);
        }

        [TestMethod]
        public void DefaultConfig_MaxOfflineQueueSizeIs20()
        {
            var config = new EventGridMqttConfig();
            Assert.AreEqual(20, config.MaxOfflineQueueSize);
        }

        [TestMethod]
        public void DefaultConfig_PublishMaxRetriesIsZero()
        {
            var config = new EventGridMqttConfig();
            Assert.AreEqual(0, config.PublishMaxRetries);
        }

        [TestMethod]
        public void DefaultConfig_AutoGarbageCollectIsTrue()
        {
            var config = new EventGridMqttConfig();
            Assert.IsTrue(config.AutoGarbageCollect);
        }

        [TestMethod]
        public void DefaultConfig_MaxPayloadSizeIsZero()
        {
            var config = new EventGridMqttConfig();
            Assert.AreEqual(0, config.MaxPayloadSize, "Default MaxPayloadSize should be 0 (unlimited).");
        }

        [TestMethod]
        public void Config_SetProperties_AreStored()
        {
            var config = new EventGridMqttConfig
            {
                BrokerHostname = "test.westus2.ts.eventgrid.azure.net",
                DeviceClientId = "device01",
                Port = 1883,
                MaxPayloadSize = 4096,
                PublishMaxRetries = 3,
                EnableDeviceTwin = true,
                EnableHealthReporting = true,
                MaxOfflineQueueSize = 50
            };

            Assert.AreEqual("test.westus2.ts.eventgrid.azure.net", config.BrokerHostname);
            Assert.AreEqual("device01", config.DeviceClientId);
            Assert.AreEqual(1883, config.Port);
            Assert.AreEqual(4096, config.MaxPayloadSize);
            Assert.AreEqual(3, config.PublishMaxRetries);
            Assert.IsTrue(config.EnableDeviceTwin);
            Assert.IsTrue(config.EnableHealthReporting);
            Assert.AreEqual(50, config.MaxOfflineQueueSize);
        }

        [TestMethod]
        public void Config_LwtProperties_SetCorrectly()
        {
            var config = new EventGridMqttConfig
            {
                LwtTopic = "devices/mydev/status",
                LwtMessage = "{\"status\":\"offline\"}",
                LwtQos = MqttQoSLevel.AtLeastOnce
            };

            Assert.AreEqual("devices/mydev/status", config.LwtTopic);
            Assert.AreEqual("{\"status\":\"offline\"}", config.LwtMessage);
            Assert.AreEqual(MqttQoSLevel.AtLeastOnce, (MqttQoSLevel)config.LwtQos);
        }
    }
}
