// <copyright file="TcpClientRx.cs" company="Chris Pulman">
// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Net;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace CP.IO.Ports;

/// <summary>
/// TcpClientRx.
/// </summary>
public class TcpClientRx : IPortRx
{
    private readonly TcpClient _tcpClient;
    private readonly ISubject<int> _bytesReceived = new Subject<int>();
    private readonly ISubject<int> _dataReceived = new Subject<int>();
    private CompositeDisposable _disposablePort = new();
    private bool _disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpClientRx"/> class.
    /// </summary>
    /// <param name="localEP">The local ep.</param>
    public TcpClientRx(IPEndPoint localEP) => _tcpClient = new(localEP);

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpClientRx"/> class.
    /// </summary>
    public TcpClientRx()
            : this(AddressFamily.InterNetwork)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpClientRx"/> class.
    /// </summary>
    /// <param name="family">The family.</param>
    public TcpClientRx(AddressFamily family) => _tcpClient = new(family);

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpClientRx"/> class.
    /// </summary>
    /// <param name="hostname">The hostname.</param>
    /// <param name="port">The port.</param>
    public TcpClientRx(string hostname, int port) => _tcpClient = new(hostname, port);

    /// <summary>
    /// Gets the infinite timeout.
    /// </summary>
    /// <value>
    /// The infinite timeout.
    /// </value>
    public int InfiniteTimeout => Timeout.Infinite;

    /// <summary>
    /// Gets or sets the read timeout.
    /// </summary>
    /// <value>
    /// The read timeout.
    /// </value>
    public int ReadTimeout
    {
        get => _tcpClient!.GetStream().ReadTimeout;
        set => _tcpClient!.GetStream().ReadTimeout = value;
    }

    /// <summary>
    /// Gets or sets the write timeout.
    /// </summary>
    /// <value>
    /// The write timeout.
    /// </value>
    public int WriteTimeout
    {
        get => _tcpClient!.GetStream().WriteTimeout;
        set => _tcpClient!.GetStream().WriteTimeout = value;
    }

    /// <summary>
    /// Gets the data received after calling Open.
    /// </summary>
    /// <value>The data received.</value>
    public IObservable<int> DataReceived => _dataReceived.Retry().Publish().RefCount();

    /// <summary>
    /// Gets the data received From ReadAsync.
    /// </summary>
    /// <value>The data received.</value>
    public IObservable<int> BytesReceived => _bytesReceived.Retry().Publish().RefCount();

    private IObservable<Unit> Connect => Observable.Create<Unit>(obs =>
    {
        var dis = new CompositeDisposable();
        var lastValue = -1;
        dis.Add(Observable.While(() => true, Observable.Return(_tcpClient.GetStream().ReadByte())).Retry()
        .Subscribe(
            d =>
            {
                if (lastValue != -1 || d > -1)
                {
                    lastValue = d;
                    _dataReceived.OnNext(d);
                }
                else
                {
                    lastValue = -1;
                }
            },
            obs.OnError));

        obs.OnNext(Unit.Default);
        return Disposable.Create(() =>
        {
            dis.Dispose();
        });
    }).Publish().RefCount();

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
    public void Write(byte[] buffer, int offset, int count) =>
        _tcpClient!.GetStream().Write(buffer, offset, count);

    /// <summary>
    /// Reads the specified buffer.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <returns>A int.</returns>
    public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
#pragma warning disable CA1835 // Change the 'ReadAsync' method call to use the 'Stream.ReadAsync(Memory<byte>, CancellationToken)' overload.
        var read = await _tcpClient!.GetStream().ReadAsync(buffer, offset, count);
#pragma warning restore CA1835 // Change the 'ReadAsync' method call to use the 'Stream.ReadAsync(Memory<byte>, CancellationToken)' overload.
        if (buffer?.Length > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var item = buffer[i];
                _bytesReceived.OnNext(item);
            }
        }

        return read;
    }

    /// <summary>
    /// Discards the in buffer.
    /// </summary>
    public void DiscardInBuffer() =>
        _tcpClient!.GetStream().Flush();

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
                _tcpClient.Dispose();
                _disposablePort.Dispose();
            }

            _disposedValue = true;
        }
    }
}
