// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CP.IO.Ports;

/// <summary>
/// Serial Port Rx interface.
/// </summary>
public interface ISerialPortRx : IPortRx
{
    /// <summary>
    /// Gets or sets the baud rate.
    /// </summary>
    /// <value>The baud rate.</value>
    int BaudRate { get; set; }

    /// <summary>
    /// Gets or sets the data bits.
    /// </summary>
    /// <value>The data bits.</value>
    int DataBits { get; set; }

    /// <summary>
    /// Gets the data received as characters.
    /// </summary>
    /// <value>The data received.</value>
    IObservable<char> DataReceived { get; }

    /// <summary>
    /// Gets the raw bytes received from the serial port.
    /// </summary>
    /// <value>The raw bytes received.</value>
    IObservable<byte> DataReceivedBytes { get; }

    /// <summary>
    /// Gets a lazily-created observable sequence of complete lines split by the NewLine sequence.
    /// </summary>
    IObservable<string> Lines { get; }

    /// <summary>
    /// Gets the error received.
    /// </summary>
    /// <value>The error received.</value>
    IObservable<Exception> ErrorReceived { get; }

#if HasWindows
    /// <summary>
    /// Gets the pin changed.
    /// </summary>
    /// <value>
    /// The pin changed.
    /// </value>
    IObservable<SerialPinChangedEventArgs> PinChanged { get; }
#endif

    /// <summary>
    /// Gets or sets the handshake.
    /// </summary>
    /// <value>The handshake.</value>
    Handshake Handshake { get; set; }

    /// <summary>
    /// Gets a value indicating whether this instance is disposed.
    /// </summary>
    /// <value><c>true</c> if this instance is disposed; otherwise, <c>false</c>.</value>
    bool IsDisposed { get; }

    /// <summary>
    /// Gets a value indicating whether gets the is open.
    /// </summary>
    /// <value>
    /// The is open.
    /// </value>
    bool IsOpen { get; }

    /// <summary>
    /// Gets the is open observable.
    /// </summary>
    /// <value>The is open observable.</value>
    IObservable<bool> IsOpenObservable { get; }

    /// <summary>
    /// Gets or sets the parity.
    /// </summary>
    /// <value>The parity.</value>
    Parity Parity { get; set; }

    /// <summary>
    /// Gets or sets the port.
    /// </summary>
    /// <value>The port.</value>
    string PortName { get; set; }

    /// <summary>
    /// Gets or sets the stop bits.
    /// </summary>
    /// <value>The stop bits.</value>
    StopBits StopBits { get; set; }

    /// <summary>
    /// Gets or sets the new line.
    /// </summary>
    /// <value>
    /// The new line.
    /// </value>
    string NewLine { get; set; }

    /// <summary>
    /// Gets or sets the encoding.
    /// </summary>
    /// <value>The encoding.</value>
    Encoding Encoding { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether break state.
    /// </summary>
    /// <value>The break state.</value>
    bool BreakState { get; set; }

    /// <summary>
    /// Gets the number of bytes of data in the receive buffer.
    /// </summary>
    /// <value>The bytes to read.</value>
    int BytesToRead { get; }

    /// <summary>
    /// Gets the number of bytes of data in the send buffer.
    /// </summary>
    /// <value>The bytes to write.</value>
    int BytesToWrite { get; }

    /// <summary>
    /// Gets a value indicating whether the Carrier Detect (CD) signal is on.
    /// </summary>
    /// <value>The CD holding.</value>
    bool CDHolding { get; }

    /// <summary>
    /// Gets a value indicating whether the Clear-to-Send (CTS) signal is on.
    /// </summary>
    /// <value>The CTS holding.</value>
    bool CtsHolding { get; }

    /// <summary>
    /// Gets or sets a value indicating whether null bytes are ignored when transmitted between the port and the receive buffer.
    /// </summary>
    /// <value>The discard null.</value>
    bool DiscardNull { get; set; }

    /// <summary>
    /// Gets a value indicating whether the Data Set Ready (DSR) signal is on.
    /// </summary>
    /// <value>The DSR holding.</value>
    bool DsrHolding { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the Data Terminal Ready (DTR) signal is enabled during serial communication.
    /// </summary>
    /// <value>The DTR enable.</value>
    bool DtrEnable { get; set; }

    /// <summary>
    /// Gets or sets the parity replace.
    /// </summary>
    /// <value>The parity replace.</value>
    byte ParityReplace { get; set; }

    /// <summary>
    /// Gets or sets the size of the read buffer.
    /// </summary>
    /// <value>The size of the read buffer.</value>
    int ReadBufferSize { get; set; }

    /// <summary>
    /// Gets or sets the number of bytes in the internal input buffer before a DataReceived event is fired.
    /// </summary>
    /// <value>The received bytes threshold.</value>
    int ReceivedBytesThreshold { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Request to Send (RTS) signal is enabled during serial communication.
    /// </summary>
    /// <value>The RTS enable.</value>
    bool RtsEnable { get; set; }

    /// <summary>
    /// Gets or sets the size of the write buffer.
    /// </summary>
    /// <value>The size of the write buffer.</value>
    int WriteBufferSize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to automatically consume received data
    /// and feed it to the DataReceived and DataReceivedBytes observables.
    /// Set to false if you want to use synchronous Read methods instead.
    /// Must be set before calling Open().
    /// </summary>
    /// <value>True to enable automatic data reception (default), false to use sync reads.</value>
    bool EnableAutoDataReceive { get; set; }

    /// <summary>
    /// Discards the out buffer.
    /// </summary>
    void DiscardOutBuffer();

    /// <summary>
    /// Writes the specified byte array.
    /// </summary>
    /// <param name="byteArray">The byte array.</param>
    void Write(byte[] byteArray);

    /// <summary>
    /// Writes the specified text.
    /// </summary>
    /// <param name="text">The text.</param>
    void Write(string text);

    /// <summary>
    /// Writes the specified character array.
    /// </summary>
    /// <param name="charArray">The character array.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    void Write(char[] charArray, int offset, int count);

    /// <summary>
    /// Writes the specified character array.
    /// </summary>
    /// <param name="charArray">The character array.</param>
    void Write(char[] charArray);

    /// <summary>
    /// Writes the line.
    /// </summary>
    /// <param name="text">The text.</param>
    void WriteLine(string text);

    /// <summary>
    /// Reads the line asynchronous.
    /// </summary>
    /// <returns>A Task of string.</returns>
    Task<string> ReadLineAsync();

    /// <summary>
    /// Reads the line asynchronous with cancellation and respecting ReadTimeout (> 0) as a timeout.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel waiting.</param>
    /// <returns>A Task of string.</returns>
    Task<string> ReadLineAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads the specified buffer.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <returns>An integer.</returns>
    int Read(byte[] buffer, int offset, int count);

    /// <summary>
    /// Reads the specified buffer.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <returns>An integer.</returns>
    int Read(char[] buffer, int offset, int count);

    /// <summary>
    /// Reads the byte.
    /// </summary>
    /// <returns>An integer.</returns>
    int ReadByte();

    /// <summary>
    /// Reads the character.
    /// </summary>
    /// <returns>An integer.</returns>
    int ReadChar();

    /// <summary>
    /// Reads the existing.
    /// </summary>
    /// <returns>A string.</returns>
    string ReadExisting();

    /// <summary>
    /// Reads the line.
    /// </summary>
    /// <returns>A string.</returns>
    string ReadLine();

    /// <summary>
    /// Reads to.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A string.</returns>
    string ReadTo(string value);

    /// <summary>
    /// Reads a string up to the specified value asynchronously.
    /// </summary>
    /// <param name="value">The value to read up to.</param>
    /// <param name="cancellationToken">Cancellation token to cancel waiting.</param>
    /// <returns>The contents of the input buffer up to the specified value.</returns>
    Task<string> ReadToAsync(string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts continuous data reception that feeds both DataReceived and DataReceivedBytes observables.
    /// Call this after Open() to enable reactive data streaming.
    /// </summary>
    /// <param name="pollingIntervalMs">Polling interval in milliseconds (default: 10ms).</param>
    /// <returns>A disposable that stops the data reception when disposed.</returns>
    IDisposable StartDataReception(int pollingIntervalMs = 10);

#if !NETFRAMEWORK
    /// <summary>
    /// Writes the specified data from a ReadOnlySpan.
    /// </summary>
    /// <param name="data">The data to write.</param>
    void Write(ReadOnlySpan<byte> data);

    /// <summary>
    /// Writes the specified data from a ReadOnlyMemory.
    /// </summary>
    /// <param name="data">The data to write.</param>
    void Write(ReadOnlyMemory<byte> data);

    /// <summary>
    /// Writes the specified character data from a ReadOnlySpan.
    /// </summary>
    /// <param name="data">The character data to write.</param>
    void Write(ReadOnlySpan<char> data);
#endif
}
