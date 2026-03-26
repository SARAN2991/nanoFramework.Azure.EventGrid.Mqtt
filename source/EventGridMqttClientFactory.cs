// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Factory for creating and managing <see cref="EventGridMqttClient"/> instances.
    /// <para>
    /// Provides singleton access to ensure only one MQTT connection exists per device —
    /// critical on ESP32 where multiple TLS connections would exhaust memory.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Usage patterns:
    /// <code>
    /// // Singleton (recommended for ESP32):
    /// var client = EventGridMqttClientFactory.GetOrCreate(config);
    /// // ... anywhere else in the application:
    /// var sameClient = EventGridMqttClientFactory.Instance;
    ///
    /// // Named instances (advanced multi-broker scenarios):
    /// EventGridMqttClientFactory.Register("broker1", config1);
    /// EventGridMqttClientFactory.Register("broker2", config2);
    /// var client1 = EventGridMqttClientFactory.Get("broker1");
    /// </code>
    /// </remarks>
    public static class EventGridMqttClientFactory
    {
        private static EventGridMqttClient _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets the singleton client instance. Null if not yet created via <see cref="GetOrCreate"/>.
        /// </summary>
        public static EventGridMqttClient Instance
        {
            get
            {
                lock (_lock)
                {
                    return _instance;
                }
            }
        }

        /// <summary>
        /// Gets whether a singleton instance has been created.
        /// </summary>
        public static bool HasInstance
        {
            get
            {
                lock (_lock)
                {
                    return _instance != null;
                }
            }
        }

        /// <summary>
        /// Gets or creates the singleton <see cref="EventGridMqttClient"/> instance.
        /// If an instance already exists, returns it (ignoring the config parameter).
        /// </summary>
        /// <param name="config">Configuration for creating a new client. Ignored if instance already exists.</param>
        /// <returns>The singleton client instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when config is null and no instance exists.</exception>
        public static EventGridMqttClient GetOrCreate(EventGridMqttConfig config)
        {
            lock (_lock)
            {
                if (_instance != null)
                {
                    return _instance;
                }

                if (config == null)
                {
                    throw new ArgumentNullException("config");
                }

                _instance = new EventGridMqttClient(config);
                return _instance;
            }
        }

        /// <summary>
        /// Gets or creates the singleton client using a fluent builder.
        /// If an instance already exists, returns it (ignoring the builder).
        /// </summary>
        /// <param name="builder">Builder to construct the client from. Ignored if instance already exists.</param>
        /// <returns>The singleton client instance.</returns>
        public static EventGridMqttClient GetOrCreate(EventGridMqttClientBuilder builder)
        {
            lock (_lock)
            {
                if (_instance != null)
                {
                    return _instance;
                }

                if (builder == null)
                {
                    throw new ArgumentNullException("builder");
                }

                _instance = builder.Build();
                return _instance;
            }
        }

        /// <summary>
        /// Destroys the singleton instance, disconnecting and disposing it.
        /// After this call, <see cref="Instance"/> returns null and <see cref="GetOrCreate"/> will create a fresh instance.
        /// </summary>
        public static void Destroy()
        {
            lock (_lock)
            {
                if (_instance != null)
                {
                    try
                    {
                        _instance.Dispose();
                    }
                    catch
                    {
                        // Suppress disposal errors
                    }

                    _instance = null;
                }
            }
        }

        /// <summary>
        /// Replaces the singleton with a new instance. Disposes the old instance first.
        /// </summary>
        /// <param name="config">Configuration for the new client.</param>
        /// <returns>The new singleton client instance.</returns>
        public static EventGridMqttClient Replace(EventGridMqttConfig config)
        {
            Destroy();
            return GetOrCreate(config);
        }
    }
}
