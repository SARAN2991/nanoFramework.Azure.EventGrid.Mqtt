// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Provides memory management utilities optimized for ESP32 devices
    /// with limited RAM (~320KB total, ~160-200KB available for application).
    /// </summary>
    /// <remarks>
    /// ESP32 memory considerations:
    /// <list type="bullet">
    ///   <item>Heap fragmentation is the #1 cause of crashes on long-running devices</item>
    ///   <item>Each string allocation creates GC pressure</item>
    ///   <item>Large payloads can cause out-of-memory on devices with &lt;50KB free</item>
    ///   <item>Periodic GC.Run() helps compact the heap</item>
    /// </list>
    /// </remarks>
    public static class MemoryManager
    {
        /// <summary>
        /// Default low-memory threshold in bytes (32KB).
        /// Below this threshold, non-critical operations should be deferred.
        /// </summary>
        public const long DefaultLowMemoryThreshold = 32768;

        /// <summary>
        /// Default critical memory threshold in bytes (16KB).
        /// Below this threshold, GC is forced and only essential operations proceed.
        /// </summary>
        public const long DefaultCriticalMemoryThreshold = 16384;

        /// <summary>
        /// Maximum recommended payload size for ESP32 (8KB).
        /// Larger payloads risk out-of-memory, especially during JSON serialization
        /// which temporarily holds both the object and the string.
        /// </summary>
        public const int DefaultMaxPayloadSize = 8192;

        private static long _lowMemoryThreshold = DefaultLowMemoryThreshold;
        private static long _criticalMemoryThreshold = DefaultCriticalMemoryThreshold;

        /// <summary>
        /// Gets or sets the low memory threshold in bytes.
        /// When free memory drops below this, <see cref="IsLowMemory"/> returns true.
        /// </summary>
        public static long LowMemoryThreshold
        {
            get => _lowMemoryThreshold;
            set => _lowMemoryThreshold = value > 0 ? value : DefaultLowMemoryThreshold;
        }

        /// <summary>
        /// Gets or sets the critical memory threshold in bytes.
        /// When free memory drops below this, <see cref="IsCriticalMemory"/> returns true.
        /// </summary>
        public static long CriticalMemoryThreshold
        {
            get => _criticalMemoryThreshold;
            set => _criticalMemoryThreshold = value > 0 ? value : DefaultCriticalMemoryThreshold;
        }

        /// <summary>
        /// Gets the currently available free memory in bytes.
        /// Uses nanoFramework's GC.Run(false) which returns free bytes without performing collection.
        /// </summary>
        /// <returns>Free memory in bytes, or -1 if unavailable.</returns>
        public static long GetFreeMemory()
        {
            try
            {
                return nanoFramework.Runtime.Native.GC.Run(false);
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Gets whether the device is in a low-memory state.
        /// When true, non-critical operations (health reporting, logging) should be reduced.
        /// </summary>
        public static bool IsLowMemory
        {
            get
            {
                long free = GetFreeMemory();
                return free >= 0 && free < _lowMemoryThreshold;
            }
        }

        /// <summary>
        /// Gets whether the device is in a critical memory state.
        /// When true, only essential MQTT operations should proceed.
        /// </summary>
        public static bool IsCriticalMemory
        {
            get
            {
                long free = GetFreeMemory();
                return free >= 0 && free < _criticalMemoryThreshold;
            }
        }

        /// <summary>
        /// Runs the garbage collector to compact the heap and free memory.
        /// On ESP32, this is important for long-running applications to prevent
        /// heap fragmentation crashes.
        /// </summary>
        /// <param name="compact">If true, runs a full compacting GC (slower but frees more memory). Default is true.</param>
        /// <returns>Free memory in bytes after GC, or -1 if unavailable.</returns>
        public static long CollectGarbage(bool compact = true)
        {
            try
            {
                return nanoFramework.Runtime.Native.GC.Run(compact);
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Checks if a payload size is within safe limits for ESP32.
        /// Accounts for the fact that JSON serialization temporarily doubles memory usage.
        /// </summary>
        /// <param name="payloadSize">The payload size in bytes.</param>
        /// <param name="maxSize">Maximum allowed size. Default is <see cref="DefaultMaxPayloadSize"/>.</param>
        /// <returns>True if the payload is within safe limits.</returns>
        public static bool IsPayloadSizeSafe(int payloadSize, int maxSize = DefaultMaxPayloadSize)
        {
            if (payloadSize <= 0)
            {
                return true;
            }

            if (payloadSize > maxSize)
            {
                return false;
            }

            // Check if we have enough free memory for the serialization overhead
            // (roughly 2x the payload size: original + serialized string)
            long free = GetFreeMemory();

            if (free >= 0 && free < payloadSize * 3)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Conditionally triggers GC if memory is below the low threshold.
        /// Call this periodically in idle loops or between publish operations.
        /// </summary>
        /// <returns>True if GC was triggered, false if memory was sufficient.</returns>
        public static bool CollectIfNeeded()
        {
            if (IsLowMemory)
            {
                CollectGarbage(true);
                return true;
            }

            return false;
        }
    }
}
