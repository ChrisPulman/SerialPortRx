// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

namespace CP.IO.Ports;

/// <summary>
/// IPortRx.
/// </summary>
/// <seealso cref="IDisposable" />
public interface IPortRx : IDisposable
{
    /// <summary>
    /// Gets the data received after opening the receive port.
    /// </summary>
    /// <value>
    /// The byte read as a stream.
    /// </value>
    IObservable<int> BytesReceived { get; }

    /// <summary>
    ///     Gets indicates that no timeout should occur.
    /// </summary>
    int InfiniteTimeout { get; }

    /// <summary>
    ///     Gets or sets the number of milliseconds before a timeout occurs when a read operation does not finish.
    /// </summary>
    int ReadTimeout { get; set; }

    /// <summary>
    ///     Gets or sets the number of milliseconds before a timeout occurs when a write operation does not finish.
    /// </summary>
    int WriteTimeout { get; set; }

    /// <summary>
    ///     Purges the receive buffer.
    /// </summary>
    void DiscardInBuffer();

    /// <summary>
    ///     Reads a number of bytes from the input buffer and writes those bytes into a byte array at the specified offset.
    /// </summary>
    /// <param name="buffer">The byte array to write the input to.</param>
    /// <param name="offset">The offset in the buffer array to begin writing.</param>
    /// <param name="count">The number of bytes to read.</param>
    /// <returns>The number of bytes read.</returns>
    Task<int> ReadAsync(byte[] buffer, int offset, int count);

    /// <summary>
    ///     Writes a specified number of bytes to the port from an output buffer, starting at the specified offset.
    /// </summary>
    /// <param name="buffer">The byte array that contains the data to write to the port.</param>
    /// <param name="offset">The offset in the buffer array to begin writing.</param>
    /// <param name="count">The number of bytes to write.</param>
    void Write(byte[] buffer, int offset, int count);

    /// <summary>
    /// Opens this instance.
    /// </summary>
    /// <returns>A Task.</returns>
    Task Open();

    /// <summary>
    /// Closes this instance.
    /// </summary>
    void Close();
}
