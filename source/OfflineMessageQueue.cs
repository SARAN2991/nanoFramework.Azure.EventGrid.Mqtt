// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using nanoFramework.M2Mqtt.Messages;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// A bounded, memory-safe message queue for offline/disconnected scenarios on ESP32.
    /// <para>
    /// When the MQTT connection is lost, messages are queued instead of being dropped.
    /// Once the connection is restored, queued messages are automatically flushed (published).
    /// The queue has a configurable maximum size to prevent out-of-memory on constrained devices.
    /// </para>
    /// </summary>
    /// <remarks>
    /// ESP32 memory considerations:
    /// <list type="bullet">
    ///   <item>Default capacity is 20 messages (~8KB payload max = ~160KB worst case)</item>
    ///   <item>When the queue is full, the oldest message is discarded (FIFO eviction)</item>
    ///   <item>Messages track their estimated memory footprint</item>
    /// </list>
    /// </remarks>
    public class OfflineMessageQueue
    {
        private readonly ArrayList _queue;
        private readonly int _maxSize;
        private readonly ILogger _logger;
        private int _droppedCount;

        /// <summary>
        /// Gets the current number of queued messages.
        /// </summary>
        public int Count => _queue.Count;

        /// <summary>
        /// Gets the maximum queue capacity.
        /// </summary>
        public int MaxSize => _maxSize;

        /// <summary>
        /// Gets the total number of messages dropped due to queue overflow.
        /// </summary>
        public int DroppedCount => _droppedCount;

        /// <summary>
        /// Gets whether the queue has any messages waiting to be sent.
        /// </summary>
        public bool HasPendingMessages => _queue.Count > 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="OfflineMessageQueue"/> class.
        /// </summary>
        /// <param name="maxSize">Maximum number of messages to queue. Default is 20.</param>
        /// <param name="logger">Optional logger.</param>
        public OfflineMessageQueue(int maxSize = 20, ILogger logger = null)
        {
            _maxSize = maxSize < 1 ? 1 : maxSize;
            _queue = new ArrayList();
            _logger = logger;
            _droppedCount = 0;
        }

        /// <summary>
        /// Enqueues a message for later delivery. If the queue is full,
        /// the oldest message is dropped to make room.
        /// </summary>
        /// <param name="topic">The MQTT topic.</param>
        /// <param name="payload">The byte payload.</param>
        /// <param name="qos">QoS level.</param>
        /// <param name="retain">Retain flag.</param>
        public void Enqueue(string topic, byte[] payload, MqttQoSLevel qos, bool retain)
        {
            if (topic == null || topic.Length == 0)
            {
                return;
            }

            // Evict oldest if at capacity
            if (_queue.Count >= _maxSize)
            {
                _queue.RemoveAt(0);
                _droppedCount++;
                _logger?.LogWarning("OfflineQueue: Queue full, oldest message dropped. Total dropped: " + _droppedCount);
            }

            _queue.Add(new QueuedMessage(topic, payload, qos, retain));
            _logger?.LogInfo("OfflineQueue: Message queued for '" + topic + "' (" + _queue.Count + "/" + _maxSize + ")");
        }

        /// <summary>
        /// Dequeues the next message (FIFO). Returns null if the queue is empty.
        /// </summary>
        /// <returns>The next queued message, or null.</returns>
        public QueuedMessage Dequeue()
        {
            if (_queue.Count == 0)
            {
                return null;
            }

            QueuedMessage msg = (QueuedMessage)_queue[0];
            _queue.RemoveAt(0);
            return msg;
        }

        /// <summary>
        /// Flushes all queued messages by invoking the publish action for each.
        /// Returns the number of messages successfully published.
        /// </summary>
        /// <param name="publishAction">The action to publish each message. Returns true on success.</param>
        /// <returns>Number of successfully published messages.</returns>
        public int Flush(QueuedPublishAction publishAction)
        {
            if (publishAction == null || _queue.Count == 0)
            {
                return 0;
            }

            int sent = 0;
            int total = _queue.Count;

            _logger?.LogInfo("OfflineQueue: Flushing " + total + " queued message(s)...");

            while (_queue.Count > 0)
            {
                QueuedMessage msg = (QueuedMessage)_queue[0];

                try
                {
                    bool success = publishAction(msg.Topic, msg.Payload, msg.Qos, msg.Retain);

                    if (success)
                    {
                        _queue.RemoveAt(0);
                        sent++;
                    }
                    else
                    {
                        // Stop flushing on failure (connection may be lost again)
                        _logger?.LogWarning("OfflineQueue: Flush stopped — publish failed. " + sent + "/" + total + " sent.");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError("OfflineQueue: Flush error: " + ex.Message);
                    break;
                }
            }

            if (sent == total)
            {
                _logger?.LogInfo("OfflineQueue: All " + sent + " queued messages published.");
            }

            return sent;
        }

        /// <summary>
        /// Clears all queued messages without publishing them.
        /// </summary>
        public void Clear()
        {
            _queue.Clear();
        }

        /// <summary>
        /// Resets the dropped message counter.
        /// </summary>
        public void ResetStatistics()
        {
            _droppedCount = 0;
        }
    }

    /// <summary>
    /// Represents a message waiting in the offline queue.
    /// </summary>
    public class QueuedMessage
    {
        /// <summary>Gets the MQTT topic.</summary>
        public string Topic { get; }

        /// <summary>Gets the byte payload.</summary>
        public byte[] Payload { get; }

        /// <summary>Gets the QoS level.</summary>
        public MqttQoSLevel Qos { get; }

        /// <summary>Gets whether the retain flag is set.</summary>
        public bool Retain { get; }

        /// <summary>Gets the UTC time when the message was queued.</summary>
        public DateTime QueuedAtUtc { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueuedMessage"/> class.
        /// </summary>
        public QueuedMessage(string topic, byte[] payload, MqttQoSLevel qos, bool retain)
        {
            Topic = topic;
            Payload = payload;
            Qos = qos;
            Retain = retain;
            QueuedAtUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Delegate for publishing a queued message. Returns true on success.
    /// </summary>
    /// <param name="topic">The MQTT topic.</param>
    /// <param name="payload">The byte payload.</param>
    /// <param name="qos">QoS level.</param>
    /// <param name="retain">Retain flag.</param>
    /// <returns>True if published successfully, false otherwise.</returns>
    public delegate bool QueuedPublishAction(string topic, byte[] payload, MqttQoSLevel qos, bool retain);
}
