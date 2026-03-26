// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using nanoFramework.M2Mqtt.Messages;
using nanoFramework.TestFramework;

namespace nanoFramework.Azure.EventGrid.Mqtt.Tests
{
    [TestClass]
    public class OfflineMessageQueueTests
    {
        [TestMethod]
        public void NewQueue_HasZeroCount()
        {
            var queue = new OfflineMessageQueue(10);
            Assert.AreEqual(0, queue.Count);
        }

        [TestMethod]
        public void NewQueue_HasNoPendingMessages()
        {
            var queue = new OfflineMessageQueue(10);
            Assert.IsFalse(queue.HasPendingMessages);
        }

        [TestMethod]
        public void NewQueue_DroppedCountIsZero()
        {
            var queue = new OfflineMessageQueue(10);
            Assert.AreEqual(0, queue.DroppedCount);
        }

        [TestMethod]
        public void Enqueue_IncreasesCount()
        {
            var queue = new OfflineMessageQueue(10);
            byte[] payload = new byte[] { 1, 2, 3 };

            queue.Enqueue("test/topic", payload, MqttQoSLevel.AtMostOnce, false);

            Assert.AreEqual(1, queue.Count);
            Assert.IsTrue(queue.HasPendingMessages);
        }

        [TestMethod]
        public void Enqueue_NullTopic_DoesNotQueue()
        {
            var queue = new OfflineMessageQueue(10);
            queue.Enqueue(null, new byte[] { 1 }, MqttQoSLevel.AtMostOnce, false);
            Assert.AreEqual(0, queue.Count);
        }

        [TestMethod]
        public void Enqueue_EmptyTopic_DoesNotQueue()
        {
            var queue = new OfflineMessageQueue(10);
            queue.Enqueue("", new byte[] { 1 }, MqttQoSLevel.AtMostOnce, false);
            Assert.AreEqual(0, queue.Count);
        }

        [TestMethod]
        public void Enqueue_BeyondCapacity_DropsOldest()
        {
            var queue = new OfflineMessageQueue(2);

            queue.Enqueue("topic/1", new byte[] { 1 }, MqttQoSLevel.AtMostOnce, false);
            queue.Enqueue("topic/2", new byte[] { 2 }, MqttQoSLevel.AtMostOnce, false);
            queue.Enqueue("topic/3", new byte[] { 3 }, MqttQoSLevel.AtMostOnce, false);

            Assert.AreEqual(2, queue.Count, "Count should stay at max capacity");
            Assert.AreEqual(1, queue.DroppedCount, "One message should have been dropped");
        }

        [TestMethod]
        public void Dequeue_ReturnsMessageInFifoOrder()
        {
            var queue = new OfflineMessageQueue(10);
            queue.Enqueue("topic/first", new byte[] { 1 }, MqttQoSLevel.AtMostOnce, false);
            queue.Enqueue("topic/second", new byte[] { 2 }, MqttQoSLevel.AtLeastOnce, false);

            QueuedMessage msg = queue.Dequeue();

            Assert.IsNotNull(msg);
            Assert.AreEqual("topic/first", msg.Topic);
            Assert.AreEqual(1, queue.Count);
        }

        [TestMethod]
        public void Dequeue_EmptyQueue_ReturnsNull()
        {
            var queue = new OfflineMessageQueue(10);
            QueuedMessage msg = queue.Dequeue();
            Assert.IsNull(msg);
        }

        [TestMethod]
        public void QueuedMessage_StoresAllProperties()
        {
            byte[] payload = new byte[] { 0xAB, 0xCD };
            var msg = new QueuedMessage("test/topic", payload, MqttQoSLevel.ExactlyOnce, true);

            Assert.AreEqual("test/topic", msg.Topic);
            Assert.AreEqual(2, msg.Payload.Length);
            Assert.AreEqual(MqttQoSLevel.ExactlyOnce, (MqttQoSLevel)msg.Qos);
            Assert.IsTrue(msg.Retain);
        }

        [TestMethod]
        public void MaxSize_ReportsConfiguredCapacity()
        {
            var queue = new OfflineMessageQueue(42);
            Assert.AreEqual(42, queue.MaxSize);
        }

        [TestMethod]
        public void MaxSize_MinimumIsOne()
        {
            var queue = new OfflineMessageQueue(0);
            Assert.AreEqual(1, queue.MaxSize, "MaxSize should be at least 1");
        }

        [TestMethod]
        public void Clear_ResetsCount()
        {
            var queue = new OfflineMessageQueue(10);
            queue.Enqueue("t", new byte[] { 1 }, MqttQoSLevel.AtMostOnce, false);
            queue.Clear();

            Assert.AreEqual(0, queue.Count);
            Assert.IsFalse(queue.HasPendingMessages);
        }

        [TestMethod]
        public void Flush_PublishesAllMessagesAndClearsQueue()
        {
            var queue = new OfflineMessageQueue(10);
            queue.Enqueue("topic/1", new byte[] { 1 }, MqttQoSLevel.AtMostOnce, false);
            queue.Enqueue("topic/2", new byte[] { 2 }, MqttQoSLevel.AtMostOnce, false);

            int publishedCount = 0;

            int sent = queue.Flush((topic, payload, qos, retain) =>
            {
                publishedCount++;
                return true;
            });

            Assert.AreEqual(2, sent);
            Assert.AreEqual(2, publishedCount);
            Assert.AreEqual(0, queue.Count);
        }

        [TestMethod]
        public void Flush_NullAction_ReturnsZero()
        {
            var queue = new OfflineMessageQueue(10);
            queue.Enqueue("topic/1", new byte[] { 1 }, MqttQoSLevel.AtMostOnce, false);

            int sent = queue.Flush(null);
            Assert.AreEqual(0, sent);
        }

        [TestMethod]
        public void Flush_EmptyQueue_ReturnsZero()
        {
            var queue = new OfflineMessageQueue(10);
            int sent = queue.Flush((t, p, q, r) => true);
            Assert.AreEqual(0, sent);
        }
    }
}
