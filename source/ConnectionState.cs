// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace nanoFramework.Azure.EventGrid.Mqtt
{
    /// <summary>
    /// Represents the connection state of the MQTT client.
    /// Provides a clear state machine instead of loose boolean flags.
    /// </summary>
    /// <remarks>
    /// State transitions:
    /// <list type="bullet">
    ///   <item><c>Disconnected</c> → <c>Connecting</c> (on Connect())</item>
    ///   <item><c>Connecting</c> → <c>Connected</c> (on success)</item>
    ///   <item><c>Connecting</c> → <c>Faulted</c> (on failure)</item>
    ///   <item><c>Connected</c> → <c>Disconnected</c> (on Disconnect())</item>
    ///   <item><c>Connected</c> → <c>Reconnecting</c> (on unexpected drop)</item>
    ///   <item><c>Reconnecting</c> → <c>Connected</c> (on reconnect success)</item>
    ///   <item><c>Reconnecting</c> → <c>Faulted</c> (on max attempts exhausted)</item>
    ///   <item><c>Faulted</c> → <c>Connecting</c> (on manual Connect())</item>
    /// </list>
    /// </remarks>
    public enum ConnectionState
    {
        /// <summary>
        /// Client is not connected and not attempting to connect.
        /// Initial state after construction or after intentional disconnect.
        /// </summary>
        Disconnected = 0,

        /// <summary>
        /// Client is actively attempting to establish a connection.
        /// </summary>
        Connecting = 1,

        /// <summary>
        /// Client is connected to the MQTT broker and operational.
        /// </summary>
        Connected = 2,

        /// <summary>
        /// Client lost connection and is automatically attempting to reconnect.
        /// </summary>
        Reconnecting = 3,

        /// <summary>
        /// Client has entered a faulted state after exhausting reconnect attempts
        /// or encountering an unrecoverable error. Manual intervention required.
        /// </summary>
        Faulted = 4
    }
}
