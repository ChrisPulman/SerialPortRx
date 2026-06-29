// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace CP.IO.Ports.Reactive;
#else
namespace CP.IO.Ports;
#endif

/// <summary>Provides a reactive wrapper around <see cref="TcpClient"/>.</summary>
public class TcpClientRx : IPortRx
{
    /// <summary>The wrapped TCP client.</summary>
    private readonly TcpClient _tcpClient;

    /// <summary>Publishes individual byte values read from the stream.</summary>
    private readonly ReplaySignal<int> _bytesReceived = new(0);

    /// <summary>Publishes individual byte values read by the receive loop.</summary>
    private readonly ReplaySignal<int> _dataReceived = new(0);

    /// <summary>Publishes byte chunks read by the receive loop.</summary>
    private readonly ReplaySignal<byte[]> _dataChunks = new(0);

    /// <summary>The cached async observable for received byte values.</summary>
    private IObservableAsync<int>? _dataReceivedAsync;

    /// <summary>The cached async observable for received byte chunks.</summary>
    private IObservableAsync<byte[]>? _dataReceivedBatchesAsync;

    /// <summary>The cached async observable for bytes read by ReadAsync.</summary>
    private IObservableAsync<int>? _bytesReceivedAsync;

    /// <summary>The active connection subscription collection.</summary>
    private CompositeDisposable _disposablePort = [];

    /// <summary>Tracks whether this instance has been disposed.</summary>
    private bool _disposedValue;

    /// <summary>Initializes a new instance of the <see cref="TcpClientRx"/> class.</summary>
    /// <param name="tcpClient">The TCP client.</param>
    public TcpClientRx(TcpClient tcpClient) => _tcpClient = tcpClient;

    /// <summary>Initializes a new instance of the <see cref="TcpClientRx"/> class.</summary>
    /// <param name="localEP">The local ep.</param>
    public TcpClientRx(IPEndPoint localEP) => _tcpClient = new(localEP);

    /// <summary>Initializes a new instance of the <see cref="TcpClientRx"/> class.</summary>
    public TcpClientRx()
            : this(AddressFamily.InterNetwork)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TcpClientRx"/> class.</summary>
    /// <param name="family">The family.</param>
    public TcpClientRx(AddressFamily family) => _tcpClient = new(family);

    /// <summary>Initializes a new instance of the <see cref="TcpClientRx"/> class.</summary>
    /// <param name="hostname">The hostname.</param>
    /// <param name="port">The port.</param>
    public TcpClientRx(string hostname, int port) => _tcpClient = new(hostname, port);

    /// <summary>Gets the infinite timeout.</summary>
    /// <value>
    /// The infinite timeout.
    /// </value>
    public int InfiniteTimeout => Timeout.Infinite;

    /// <summary>Gets or sets the read timeout.</summary>
    /// <value>
    /// The read timeout.
    /// </value>
    public int ReadTimeout
    {
        get => Stream.ReadTimeout;
        set => Stream.ReadTimeout = value;
    }

    /// <summary>Gets the underlying System.Net.Sockets.Socket.</summary>
    /// <value>
    /// The underlying network System.Net.Sockets.Socket.
    /// </value>
    public Socket Client => _tcpClient!.Client;

    /// <summary>Gets the System.Net.Sockets.NetworkStream used to send and receive data.</summary>
    /// <value>
    /// The stream.
    /// </value>
    public NetworkStream Stream => _tcpClient!.GetStream();

    /// <summary>Gets or sets the write timeout.</summary>
    /// <value>
    /// The write timeout.
    /// </value>
    public int WriteTimeout
    {
        get => Stream.WriteTimeout;
        set => Stream.WriteTimeout = value;
    }

    /// <summary>Gets the data received after calling Open.</summary>
    /// <value>The data received.</value>
    public IObservable<int> DataReceived => _dataReceived;

    /// <summary>Gets the data received after calling Open as an async observable.</summary>
    /// <value>The data received.</value>
    public IObservableAsync<int> DataReceivedAsync => _dataReceivedAsync ??= DataReceived.ToObservableAsync();

    /// <summary>Gets stream chunks (byte arrays) produced by the internal read loop.</summary>
    public IObservable<byte[]> DataReceivedBatches => _dataChunks;

    /// <summary>Gets stream chunks produced by the internal read loop as an async observable.</summary>
    public IObservableAsync<byte[]> DataReceivedBatchesAsync => _dataReceivedBatchesAsync ??= DataReceivedBatches.ToObservableAsync();

    /// <summary>Gets the data received From ReadAsync.</summary>
    /// <value>The data received.</value>
    public IObservable<int> BytesReceived => _bytesReceived;

    /// <summary>Gets the data received from ReadAsync as an async observable.</summary>
    /// <value>The data received.</value>
    public IObservableAsync<int> BytesReceivedAsync => _bytesReceivedAsync ??= BytesReceived.ToObservableAsync();

    /// <summary>Connects the specified hostname.</summary>
    /// <param name="hostname">The hostname.</param>
    /// <param name="port">The port.</param>
    public void Connect(string hostname, int port) =>
        _tcpClient.Connect(hostname, port);

    /// <summary>Connects the specified address.</summary>
    /// <param name="address">The address.</param>
    /// <param name="port">The port.</param>
    public void Connect(IPAddress address, int port) =>
        _tcpClient.Connect(address, port);

    /// <summary>Connects the specified remote ep.</summary>
    /// <param name="remoteEP">The remote ep.</param>
    public void Connect(IPEndPoint remoteEP) =>
        _tcpClient.Connect(remoteEP);

    /// <summary>Connects the specified IP addresses.</summary>
    /// <param name="addresses">The IP addresses.</param>
    /// <param name="port">The port.</param>
    public void Connect(IPAddress[] addresses, int port) =>
        _tcpClient.Connect(addresses, port);

    /// <summary>Opens this instance.</summary>
    /// <returns>A Task.</returns>
    public Task Open()
    {
        if (_disposablePort?.IsDisposed != false)
        {
            _disposablePort = [];
        }

        return _disposablePort?.Count == 0 ? Task.Run(() => _disposablePort.Add(Connect().Subscribe())) : Task.CompletedTask;
    }

    /// <summary>Closes this instance.</summary>
    public void Close() => _disposablePort?.Dispose();

    /// <summary>Writes the specified buffer.</summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    public void Write(byte[] buffer, int offset, int count) =>
        Stream.Write(buffer, offset, count);

    /// <summary>Reads the specified buffer.</summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <returns>A int.</returns>
    public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
#if NETFRAMEWORK
        var read = await Stream.ReadAsync(buffer, offset, count).ConfigureAwait(false);
#else
        var read = await Stream.ReadAsync(buffer.AsMemory(offset, count)).ConfigureAwait(false);
#endif
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

    /// <summary>Discards the in buffer.</summary>
    public void DiscardInBuffer() =>
        Stream.Flush();

    /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue)
        {
            return;
        }

        if (disposing)
        {
            _bytesReceived.Dispose();
            _dataReceived.Dispose();
            _dataChunks.Dispose();
            _tcpClient.Dispose();
            _disposablePort.Dispose();
        }

        _disposedValue = true;
    }

    /// <summary>Creates the connection observable that drives the TCP receive loop.</summary>
    /// <returns>An observable that signals when the receive loop has started.</returns>
    private IObservable<Unit> Connect() => Observable.Create<Unit>(obs =>
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Start a dedicated async read loop to minimize per-byte overhead and avoid busy waits
        _ = Task.Factory
            .StartNew(
                async () =>
                {
                    try
                    {
                        // Signal subscription succeeded
                        obs.OnNext(Unit.Default);

                        var buffer = new byte[4096];
                        while (!token.IsCancellationRequested)
                        {
                            int read;
                            try
                            {
#if NETFRAMEWORK
                                read = await Stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
#else
                                read = await Stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token).ConfigureAwait(false);
#endif
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }

                            if (read == 0)
                            {
                                // Remote closed
                                break;
                            }

                            // Per-byte stream
                            for (var i = 0; i < read; i++)
                            {
                                _dataReceived.OnNext(buffer[i]);
                            }

                            // Batched chunk stream (copy to a right-sized array)
                            var chunk = new byte[read];
                            Buffer.BlockCopy(buffer, 0, chunk, 0, read);
                            _dataChunks.OnNext(chunk);
                        }
                    }
                    catch (Exception ex)
                    {
                        obs.OnError(ex);
                    }
                },
                token,
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default)
            .Unwrap()
            .ContinueWith(
                t =>
                {
                    var ignored = t.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);

        return Disposable.Create(() =>
        {
            cts.Cancel();
            cts.Dispose();
        });
    });
}
