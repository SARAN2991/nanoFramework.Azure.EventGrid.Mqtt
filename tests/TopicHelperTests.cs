// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using nanoFramework.TestFramework;

namespace nanoFramework.Azure.EventGrid.Mqtt.Tests
{
    [TestClass]
    public class TopicHelperTests
    {
        [TestMethod]
        public void BuildTopic_ReplacesDeviceIdPlaceholder()
        {
            string result = TopicHelper.BuildTopic("devices/{deviceId}/telemetry", "esp32-001");
            Assert.AreEqual("devices/esp32-001/telemetry", result);
        }

        [TestMethod]
        public void BuildTopic_ReplacesClientNamePlaceholder()
        {
            string result = TopicHelper.BuildTopic("devices/${client.name}/data", "myDevice");
            Assert.AreEqual("devices/myDevice/data", result);
        }

        [TestMethod]
        public void BuildTopic_NoPlaceholder_ReturnsTemplateUnchanged()
        {
            string result = TopicHelper.BuildTopic("devices/hardcoded/telemetry", "esp32");
            Assert.AreEqual("devices/hardcoded/telemetry", result);
        }

        [TestMethod]
        public void BuildWildcardTopic_AppendsHash()
        {
            string result = TopicHelper.BuildWildcardTopic("devices/esp32-001");
            Assert.AreEqual("devices/esp32-001/#", result);
        }

        [TestMethod]
        public void BuildWildcardTopic_TrimsTrailingSlash()
        {
            string result = TopicHelper.BuildWildcardTopic("devices/esp32-001/");
            Assert.AreEqual("devices/esp32-001/#", result);
        }

        [TestMethod]
        public void BuildSingleLevelWildcardTopic_AppendsPlus()
        {
            string result = TopicHelper.BuildSingleLevelWildcardTopic("devices/esp32-001/commands");
            Assert.AreEqual("devices/esp32-001/commands/+", result);
        }

        [TestMethod]
        public void ValidateTopic_ValidTopic_ReturnsTrue()
        {
            Assert.IsTrue(TopicHelper.ValidateTopic("devices/esp32/data"));
        }

        [TestMethod]
        public void ValidateTopic_EmptyTopic_ReturnsFalse()
        {
            Assert.IsFalse(TopicHelper.ValidateTopic(""));
        }

        [TestMethod]
        public void ValidateTopic_NullTopic_ReturnsFalse()
        {
            Assert.IsFalse(TopicHelper.ValidateTopic(null));
        }

        [TestMethod]
        public void ValidateBrokerHostname_ValidHostname_ReturnsTrue()
        {
            Assert.IsTrue(TopicHelper.ValidateBrokerHostname("myns.westeurope-1.ts.eventgrid.azure.net"));
        }

        [TestMethod]
        public void ValidateBrokerHostname_MissingTsSegment_ReturnsFalse()
        {
            Assert.IsFalse(TopicHelper.ValidateBrokerHostname("myns.westeurope-1.eventgrid.azure.net"));
        }

        [TestMethod]
        public void MaxTopicLength_Is256()
        {
            Assert.AreEqual(256, TopicHelper.MaxTopicLength);
        }
    }
}
