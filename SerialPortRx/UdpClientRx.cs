// <copyright file="UdpClientRx.cs" company="Chris Pulman">
// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;

namespace CP.IO.Ports;

/// <summary>
/// UdpClientRx.
/// </summary>
public class UdpClientRx : IPortRx
{
    private const int MaxBufferSize = ushort.MaxValue;
    private readonly UdpClient? _udpClient;
    private readonly byte[] _buffer = new byte[MaxBufferSize];
    private readonly ISubject<int> _bytesReceived = new Subject<int>();
    private readonly ISubject<int> _dataReceived = new Subject<int>();
    private CompositeDisposable _disposablePort = new();
    private bool _disposedValue;
    private int _bufferOffset;

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpClientRx"/> class.
    /// </summary>
    /// <param name="udpClient">The UDP client.</param>
    public UdpClientRx(UdpClient udpClient) => _udpClient = udpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpClientRx"/> class.
    /// </summary>
    /// <param name="localEP">The local ep.</param>
    public UdpClientRx(IPEndPoint localEP) => _udpClient = new(localEP);

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpClientRx"/> class.
    /// </summary>
    public UdpClientRx()
            : this(AddressFamily.InterNetwork)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpClientRx"/> class.
    /// </summary>
    /// <param name="family">The family.</param>
    public UdpClientRx(AddressFamily family) => _udpClient = new(family);

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpClientRx"/> class.
    /// </summary>
    /// <param name="hostname">The hostname.</param>
    /// <param name="port">The port.</param>
    public UdpClientRx(string hostname, int port) => _udpClient = new(hostname, port);

    /// <summary>
    /// Gets the infinite timeout.
    /// </summary>
    /// <value>
    /// The infinite timeout.
    /// </value>
    public int InfiniteTimeout => Timeout.Infinite;

    /// <summary>
    /// Gets the underlying System.Net.Sockets.Socket.
    /// </summary>
    /// <value>
    /// The underlying network System.Net.Sockets.Socket.
    /// </value>
    public Socket Client => _udpClient!.Client;

    /// <summary>
    /// Gets or sets the read timeout.
    /// </summary>
    /// <value>
    /// The read timeout.
    /// </value>
    public int ReadTimeout
    {
        get => Client.ReceiveTimeout;
        set => Client.ReceiveTimeout = value;
    }

    /// <summary>
    /// Gets or sets the write timeout.
    /// </summary>
    /// <value>
    /// The write timeout.
    /// </value>
    public int WriteTimeout
    {
        get => Client.SendTimeout;
        set => Client.SendTimeout = value;
    }

    /// <summary>
    /// Gets the data received.
    /// </summary>
    /// <value>The data received.</value>
    public IObservable<int> DataReceived => _dataReceived.Retry().Publish().RefCount();

    /// <summary>
    /// Gets the data received.
    /// </summary>
    /// <value>The data received.</value>
    public IObservable<int> BytesReceived => _bytesReceived.Retry().Publish().RefCount();

    private IObservable<Unit> Connect => Observable.Create<Unit>(obs =>
    {
        var dis = new CompositeDisposable
        {
            _udpClient!.ReceiveAsync()
            .ToObservable()
            .Select(x => x.Buffer)
            .ForEach()
            .Retry()
            .Subscribe(d => _dataReceived.OnNext(d), obs.OnError)
        };

        obs.OnNext(Unit.Default);
        return Disposable.Create(() =>
        {
            dis.Dispose();
        });
    }).Publish().RefCount();

    /// <summary>
    /// Returns a UDP datagram asynchronously that was sent by a remote host.
    /// </summary>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public Task<UdpReceiveResult> ReceiveAsync() => _udpClient?.ReceiveAsync()!;

    /// <summary>
    /// Opens this instance.
    /// </summary>
    /// <returns>A Task.</returns>
    public Task Open()
    {
        if (_disposablePort?.IsDisposed != false)
        {
            _disposablePort = new();
        }

        return _disposablePort?.Count == 0 ? Task.Run(() => Connect.Subscribe().AddTo(_disposablePort)) : Task.CompletedTask;
    }

    /// <summary>
    /// Closes this instance.
    /// </summary>
    public void Close() => _disposablePort?.Dispose();

    /// <summary>
    /// Writes the specified buffer.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    public void Write(byte[] buffer, int offset, int count)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                "Argument offset must be greater than or equal to 0.");
        }

        if (offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                "Argument offset cannot be greater than the length of buffer.");
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                "Argument count must be greater than or equal to 0.");
        }

        if (count > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                "Argument count cannot be greater than the length of buffer minus offset.");
        }

        Client.Send(buffer.Skip(offset).Take(count).ToArray());
    }

    /// <summary>
    /// Sends a UDP datagram asynchronously to a remote host.
    /// </summary>
    /// <param name="dataGram">The data gram.</param>
    /// <param name="bytes">The bytes.</param>
    /// <param name="endPoint">The end point.</param>
    /// <returns>A Task of int.</returns>
    public Task<int> SendAsync(byte[] dataGram, int bytes, IPEndPoint endPoint) =>
        _udpClient!.SendAsync(dataGram, bytes, endPoint);

    /// <summary>
    /// Reads the specified buffer.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <returns>A int.</returns>
    public Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                "Argument offset must be greater than or equal to 0.");
        }

        if (offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                "Argument offset cannot be greater than the length of buffer.");
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                "Argument count must be greater than or equal to 0.");
        }

        if (count > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                "Argument count cannot be greater than the length of buffer minus offset.");
        }

        var ret = Task.Factory.StartNew(
            () =>
             {
                 if (_bufferOffset == 0)
                 {
                     _bufferOffset = Client.Receive(_buffer);
                 }

                 if (_bufferOffset < count)
                 {
                     throw new Exception("Not enough bytes in the bytes received.");
                 }

                 Buffer.BlockCopy(_buffer, 0, buffer, offset, count);
                 _bufferOffset -= count;
                 Buffer.BlockCopy(_buffer, count, _buffer, 0, _bufferOffset);

                 for (var i = 0; i < count; i++)
                 {
                     var item = buffer[i];
                     _bytesReceived.OnNext(item);
                 }

                 return count;
             },
            CancellationToken.None,
            TaskCreationOptions.None,
            TaskScheduler.Current);

        return ret!;
    }

    /// <summary>
    /// Discards the in buffer.
    /// </summary>
    public void DiscardInBuffer()
    {
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _udpClient?.Dispose();
                _disposablePort.Dispose();
            }

            _disposedValue = true;
        }
    }
}
