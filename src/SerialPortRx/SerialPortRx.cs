// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IO.Ports;

/// <summary>Serial Port Rx.</summary>
/// <seealso cref="ISerialPortRx"/>
public class SerialPortRx : ISerialPortRx
{
    /// <summary>The placeholder returned when no serial ports are available.</summary>
    private static readonly string[] NoPorts = ["NoPorts"];

    /// <summary>Publishes the current open state.</summary>
    private readonly ReplaySignal<bool> _isOpenValue = new(1);

    /// <summary>Publishes received characters.</summary>
    private readonly ReplaySignal<char> _dataReceived = new(0);

    /// <summary>Publishes received bytes.</summary>
    private readonly ReplaySignal<byte> _dataReceivedBytes = new(0);

    /// <summary>Publishes serial port errors.</summary>
    private readonly ReplaySignal<Exception> _errors = new(0);

    /// <summary>Publishes byte-array write requests.</summary>
    private readonly ReplaySignal<(byte[] byteArray, int offset, int count)> _writeByte = new(0);

    /// <summary>Publishes character-array write requests.</summary>
    private readonly ReplaySignal<(char[] charArray, int offset, int count)> _writeChar = new(0);

    /// <summary>Publishes string write requests.</summary>
    private readonly ReplaySignal<string> _writeString = new(0);

    /// <summary>Publishes string-line write requests.</summary>
    private readonly ReplaySignal<string> _writeStringLine = new(0);

    /// <summary>Publishes discard-in-buffer requests.</summary>
    private readonly ReplaySignal<Unit> _discardInBuffer = new(0);

    /// <summary>Publishes discard-out-buffer requests.</summary>
    private readonly ReplaySignal<Unit> _discardOutBuffer = new(0);

    /// <summary>Publishes read requests.</summary>
    private readonly ReplaySignal<(byte[] buffer, int offset, int count)> _readBytes = new(0);

    /// <summary>Publishes completed read lengths.</summary>
    private readonly ReplaySignal<int> _bytesRead = new(0);

    /// <summary>Publishes bytes received by read operations.</summary>
    private readonly ReplaySignal<int> _bytesReceived = new(0);

    /// <summary>Publishes serial pin change events.</summary>
    private readonly ReplaySignal<SerialPinChangedEventArgs> _pinChanged = new(0);

    /// <summary>Serializes read access.</summary>
    private readonly SemaphoreSlim _readLock = new(1, 1);

    /// <summary>Synchronizes lazy observable cache initialization.</summary>
#if NET9_0_OR_GREATER
    private readonly Lock _observableCacheLock = new();
#else
    private readonly object _observableCacheLock = new();
#endif

    /// <summary>The active port subscription collection.</summary>
    private CompositeDisposable _disposablePort = [];

    /// <summary>The cached data-received observable.</summary>
    private IObservable<char>? _cachedDataReceived;

    /// <summary>The cached byte-received observable.</summary>
    private IObservable<byte>? _cachedDataReceivedBytes;

    /// <summary>The cached bytes-read observable.</summary>
    private IObservable<int>? _cachedBytesReceived;

    /// <summary>The cached error observable.</summary>
    private IObservable<Exception>? _cachedErrorReceived;

    /// <summary>The cached open-state observable.</summary>
    private IObservable<bool>? _cachedIsOpenObservable;

    /// <summary>The cached line observable.</summary>
    private IObservable<string>? _lines;

    /// <summary>The cached async data-received observable.</summary>
    private IObservableAsync<char>? _cachedDataReceivedAsync;

    /// <summary>The cached async byte-received observable.</summary>
    private IObservableAsync<byte>? _cachedDataReceivedBytesAsync;

    /// <summary>The cached async bytes-read observable.</summary>
    private IObservableAsync<int>? _cachedBytesReceivedAsync;

    /// <summary>The cached async error observable.</summary>
    private IObservableAsync<Exception>? _cachedErrorReceivedAsync;

    /// <summary>The cached async open-state observable.</summary>
    private IObservableAsync<bool>? _cachedIsOpenObservableAsync;

    /// <summary>The cached async line observable.</summary>
    private IObservableAsync<string>? _linesAsync;
#if HasWindows
    /// <summary>The cached serial pin change observable.</summary>
    private IObservable<SerialPinChangedEventArgs>? _cachedPinChanged;

    /// <summary>The cached async serial pin change observable.</summary>
    private IObservableAsync<SerialPinChangedEventArgs>? _cachedPinChangedAsync;
#endif

    /// <summary>The wrapped serial port.</summary>
    private SerialPort? _serialPort;

    /// <summary>The cached break-state setting.</summary>
    private bool _breakState;

    /// <summary>The cached discard-null setting.</summary>
    private bool _discardNull;

    /// <summary>The cached DTR-enable setting.</summary>
    private bool _dtrEnable;

    /// <summary>The cached parity replacement byte.</summary>
    private byte _parityReplace = 63;

    /// <summary>The cached read buffer size.</summary>
    private int _readBufferSize = 4096;

    /// <summary>The cached received-bytes threshold.</summary>
    private int _receivedBytesThreshold = 1;

    /// <summary>The cached RTS-enable setting.</summary>
    private bool _rtsEnable;

    /// <summary>The cached write buffer size.</summary>
    private int _writeBufferSize = 2048;

    /// <summary>Initializes a new instance of the <see cref="SerialPortRx"/> class.</summary>
    /// <param name="port">The port.</param>
    /// <param name="baudRate">The baud rate.</param>
    /// <param name="dataBits">The data bits.</param>
    /// <param name="parity">The parity.</param>
    /// <param name="stopBits">The stop bits.</param>
    /// <param name="handshake">The handshake.</param>
    public SerialPortRx(string port, int baudRate, int dataBits, Parity parity, StopBits stopBits, Handshake handshake)
    {
        PortName = port;
        BaudRate = baudRate;
        DataBits = dataBits;
        Parity = parity;
        StopBits = stopBits;
        Handshake = handshake;
    }

    /// <summary>Initializes a new instance of the <see cref="SerialPortRx"/> class.</summary>
    /// <param name="port">The port.</param>
    /// <param name="baudRate">The baud rate.</param>
    /// <param name="dataBits">The data bits.</param>
    /// <param name="parity">The parity.</param>
    /// <param name="stopBits">The stop bits.</param>
    public SerialPortRx(string port, int baudRate, int dataBits, Parity parity, StopBits stopBits)
    {
        PortName = port;
        BaudRate = baudRate;
        DataBits = dataBits;
        Parity = parity;
        StopBits = stopBits;
    }

    /// <summary>Initializes a new instance of the <see cref="SerialPortRx"/> class.</summary>
    /// <param name="port">The port.</param>
    /// <param name="baudRate">The baud rate.</param>
    /// <param name="dataBits">The data bits.</param>
    /// <param name="parity">The parity.</param>
    public SerialPortRx(string port, int baudRate, int dataBits, Parity parity)
    {
        PortName = port;
        BaudRate = baudRate;
        DataBits = dataBits;
        Parity = parity;
    }

    /// <summary>Initializes a new instance of the <see cref="SerialPortRx"/> class.</summary>
    /// <param name="port">The port.</param>
    /// <param name="baudRate">The baud rate.</param>
    /// <param name="dataBits">The data bits.</param>
    public SerialPortRx(string port, int baudRate, int dataBits)
    {
        PortName = port;
        BaudRate = baudRate;
        DataBits = dataBits;
    }

    /// <summary>Initializes a new instance of the <see cref="SerialPortRx"/> class.</summary>
    /// <param name="port">The port.</param>
    /// <param name="baudRate">The baud rate.</param>
    public SerialPortRx(string port, int baudRate)
    {
        PortName = port;
        BaudRate = baudRate;
    }

    /// <summary>Initializes a new instance of the <see cref="SerialPortRx" /> class.</summary>
    /// <param name="port">The port.</param>
    public SerialPortRx(string port) => PortName = port;

    /// <summary>Initializes a new instance of the <see cref="SerialPortRx"/> class.</summary>
    public SerialPortRx()
    {
    }

    /// <summary>Gets indicates that no timeout should occur.</summary>
    [Browsable(true)]
    [DefaultValue(-1)]
    [MonitoringDescription("InfiniteTimeout")]
    public int InfiniteTimeout => SerialPort.InfiniteTimeout;

    /// <summary>Gets or sets the baud rate.</summary>
    /// <value>The baud rate.</value>
    [Browsable(true)]
    [DefaultValue(9600)]
    [MonitoringDescription("BaudRate")]
    public int BaudRate { get; set; } = 9600;

    /// <summary>Gets or sets the data bits.</summary>
    /// <value>The data bits.</value>
    [Browsable(true)]
    [DefaultValue(8)]
    [MonitoringDescription("DataBits")]
    public int DataBits { get; set; } = 8;

    /// <summary>Gets the data received as characters.</summary>
    /// <value>The data received.</value>
    public IObservable<char> DataReceived => GetOrCreateCachedObservable(ref _cachedDataReceived, _dataReceived);

    /// <summary>Gets the data received as characters via an async observable.</summary>
    /// <value>The data received.</value>
    public IObservableAsync<char> DataReceivedAsync => GetOrCreateCachedAsyncObservable(ref _cachedDataReceivedAsync, DataReceived);

    /// <summary>Gets the raw bytes received from the serial port.</summary>
    /// <value>The raw bytes received.</value>
    public IObservable<byte> DataReceivedBytes => GetOrCreateCachedObservable(ref _cachedDataReceivedBytes, _dataReceivedBytes);

    /// <summary>Gets the raw bytes received from the serial port via an async observable.</summary>
    /// <value>The raw bytes received.</value>
    public IObservableAsync<byte> DataReceivedBytesAsync => GetOrCreateCachedAsyncObservable(ref _cachedDataReceivedBytesAsync, DataReceivedBytes);

    /// <summary>Gets the data received when executing ReadAsync.</summary>
    /// <value>The data received.</value>
    public IObservable<int> BytesReceived => GetOrCreateCachedObservable(ref _cachedBytesReceived, _bytesReceived);

    /// <summary>Gets the data received when executing ReadAsync via an async observable.</summary>
    /// <value>The data received.</value>
    public IObservableAsync<int> BytesReceivedAsync => GetOrCreateCachedAsyncObservable(ref _cachedBytesReceivedAsync, BytesReceived);

    /// <summary>Gets or sets the encoding.</summary>
    /// <value>The encoding.</value>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [MonitoringDescription("Encoding")]
    public Encoding Encoding { get; set; } = Encoding.ASCII;

    /// <summary>Gets the error received.</summary>
    /// <value>The error received.</value>
    public IObservable<Exception> ErrorReceived => GetOrCreateCachedObservable(
        ref _cachedErrorReceived,
        CreateDistinctErrorObservable);

    /// <summary>Gets the error received via an async observable.</summary>
    /// <value>The error received.</value>
    public IObservableAsync<Exception> ErrorReceivedAsync => GetOrCreateCachedAsyncObservable(ref _cachedErrorReceivedAsync, ErrorReceived);

    /// <summary>Gets or sets the handshake.</summary>
    /// <value>The handshake.</value>
    [Browsable(true)]
    [DefaultValue(Handshake.None)]
    [MonitoringDescription("Handshake")]
    public Handshake Handshake { get; set; } = Handshake.None;

    /// <summary>Gets a value indicating whether this instance is disposed.</summary>
    /// <value><c>true</c> if this instance is disposed; otherwise, <c>false</c>.</value>
    [Browsable(true)]
    [MonitoringDescription("IsDisposed")]
    public bool IsDisposed { get; private set; }

    /// <summary>Gets a value indicating whether gets the is open.</summary>
    /// <value>The is open.</value>
    [Browsable(true)]
    [MonitoringDescription("IsOpen")]
    public bool IsOpen => _serialPort?.IsOpen ?? false;

    /// <summary>Gets the is open observable.</summary>
    /// <value>The is open observable.</value>
    public IObservable<bool> IsOpenObservable => GetOrCreateCachedObservable(
        ref _cachedIsOpenObservable,
        () => _isOpenValue.DistinctUntilChanged());

    /// <summary>Gets the is open async observable.</summary>
    /// <value>The is open async observable.</value>
    public IObservableAsync<bool> IsOpenObservableAsync => GetOrCreateCachedAsyncObservable(ref _cachedIsOpenObservableAsync, IsOpenObservable);

    /// <summary>Gets or sets the parity.</summary>
    /// <value>The parity.</value>
    [Browsable(true)]
    [DefaultValue(Parity.None)]
    [MonitoringDescription("Parity")]
    public Parity Parity { get; set; } = Parity.None;

    /// <summary>Gets or sets the port.</summary>
    /// <value>The port.</value>
    [Browsable(true)]
    [DefaultValue("COM1")]
    [MonitoringDescription("PortName")]
    public string PortName { get; set; } = "COM1";

    /// <summary>Gets or sets the read timeout.</summary>
    /// <value>The read timeout.</value>
    [Browsable(true)]
    [DefaultValue(-1)]
    [MonitoringDescription("ReadTimeout")]
    public int ReadTimeout { get; set; } = -1;

    /// <summary>Gets or sets the stop bits.</summary>
    /// <value>The stop bits.</value>
    [Browsable(true)]
    [DefaultValue(StopBits.One)]
    [MonitoringDescription("StopBits")]
    public StopBits StopBits { get; set; } = StopBits.One;

    /// <summary>Gets or sets the write timeout.</summary>
    /// <value>The write timeout.</value>
    [Browsable(true)]
    [DefaultValue(-1)]
    [MonitoringDescription("WriteTimeout")]
    public int WriteTimeout { get; set; } = -1;

    /// <summary>Gets or sets creates new line.</summary>
    /// <value>
    /// The new line.
    /// </value>
    [Browsable(false)]
    [DefaultValue("\n")]
    [MonitoringDescription("NewLine")]
    public string NewLine { get; set; } = "\n";

    /// <summary>Gets or sets a value indicating whether break state.</summary>
    /// <value>The break state.</value>
    [Browsable(true)]
    [DefaultValue(false)]
    [MonitoringDescription("BreakState")]
    public bool BreakState
    {
        get => _serialPort?.BreakState ?? _breakState;
        set
        {
            _breakState = value;
            _serialPort?.BreakState = value;
        }
    }

    /// <summary>Gets the number of bytes of data in the receive buffer.</summary>
    /// <value>The bytes to read.</value>
    [Browsable(false)]
    [MonitoringDescription("BytesToRead")]
    public int BytesToRead => _serialPort?.BytesToRead ?? 0;

    /// <summary>Gets the number of bytes of data in the send buffer.</summary>
    /// <value>The bytes to write.</value>
    [Browsable(false)]
    [MonitoringDescription("BytesToWrite")]
    public int BytesToWrite => _serialPort?.BytesToWrite ?? 0;

    /// <summary>Gets a value indicating whether the Carrier Detect (CD) signal is on.</summary>
    /// <value>The CD holding.</value>
    [Browsable(false)]
    [MonitoringDescription("CDHolding")]
    public bool CDHolding => _serialPort?.CDHolding ?? false;

    /// <summary>Gets a value indicating whether the Clear-to-Send (CTS) signal is on.</summary>
    /// <value>The CTS holding.</value>
    [Browsable(false)]
    [MonitoringDescription("CtsHolding")]
    public bool CtsHolding => _serialPort?.CtsHolding ?? false;

    /// <summary>
    /// Gets or sets a value indicating whether null bytes are ignored when transmitted between the port and the receive buffer.
    /// </summary>
    /// <value>The discard null.</value>
    [Browsable(true)]
    [DefaultValue(false)]
    [MonitoringDescription("DiscardNull")]
    public bool DiscardNull
    {
        get => _serialPort?.DiscardNull ?? _discardNull;
        set
        {
            _discardNull = value;
            _serialPort?.DiscardNull = value;
        }
    }

    /// <summary>Gets a value indicating whether the Data Set Ready (DSR) signal is on.</summary>
    /// <value>The DSR holding.</value>
    [Browsable(false)]
    [MonitoringDescription("DsrHolding")]
    public bool DsrHolding => _serialPort?.DsrHolding ?? false;

    /// <summary>Gets or sets a value indicating whether the Data Terminal Ready (DTR) signal is enabled during serial communication.</summary>
    /// <value>The DTR enable.</value>
    [Browsable(true)]
    [DefaultValue(false)]
    [MonitoringDescription("DtrEnable")]
    public bool DtrEnable
    {
        get => _serialPort?.DtrEnable ?? _dtrEnable;
        set
        {
            _dtrEnable = value;
            _serialPort?.DtrEnable = value;
        }
    }

    /// <summary>Gets or sets the parity replace.</summary>
    /// <value>The parity replace.</value>
    [Browsable(true)]
    [DefaultValue((byte)63)]
    [MonitoringDescription("ParityReplace")]
    public byte ParityReplace
    {
        get => _serialPort?.ParityReplace ?? _parityReplace;
        set
        {
            _parityReplace = value;
            _serialPort?.ParityReplace = value;
        }
    }

    /// <summary>Gets or sets the size of the read buffer.</summary>
    /// <value>The size of the read buffer.</value>
    [Browsable(true)]
    [DefaultValue(4096)]
    [MonitoringDescription("ReadBufferSize")]
    public int ReadBufferSize
    {
        get => _serialPort?.ReadBufferSize ?? _readBufferSize;
        set
        {
            _readBufferSize = value;
            _serialPort?.ReadBufferSize = value;
        }
    }

    /// <summary>Gets or sets the number of bytes in the internal input buffer before a DataReceived event is fired.</summary>
    /// <value>The received bytes threshold.</value>
    [Browsable(true)]
    [DefaultValue(1)]
    [MonitoringDescription("ReceivedBytesThreshold")]
    public int ReceivedBytesThreshold
    {
        get => _serialPort?.ReceivedBytesThreshold ?? _receivedBytesThreshold;
        set
        {
            _receivedBytesThreshold = value;
            _serialPort?.ReceivedBytesThreshold = value;
        }
    }

    /// <summary>Gets or sets a value indicating whether the Request to Send (RTS) signal is enabled during serial communication.</summary>
    /// <value>The RTS enable.</value>
    [Browsable(true)]
    [DefaultValue(false)]
    [MonitoringDescription("RtsEnable")]
    public bool RtsEnable
    {
        get => _serialPort?.RtsEnable ?? _rtsEnable;
        set
        {
            _rtsEnable = value;
            _serialPort?.RtsEnable = value;
        }
    }

    /// <summary>Gets or sets the size of the write buffer.</summary>
    /// <value>The size of the write buffer.</value>
    [Browsable(true)]
    [DefaultValue(2048)]
    [MonitoringDescription("WriteBufferSize")]
    public int WriteBufferSize
    {
        get => _serialPort?.WriteBufferSize ?? _writeBufferSize;
        set
        {
            _writeBufferSize = value;
            _serialPort?.WriteBufferSize = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to automatically consume received data
    /// and feed it to the DataReceived and DataReceivedBytes observables.
    /// Set to false if you want to use synchronous Read methods instead.
    /// Must be set before calling Open().
    /// </summary>
    /// <value>True to enable automatic data reception (default), false to use sync reads.</value>
    [Browsable(true)]
    [DefaultValue(true)]
    [MonitoringDescription("EnableAutoDataReceive")]
    public bool EnableAutoDataReceive { get; set; } = true;

#if HasWindows
    /// <summary>Gets the pin changed.</summary>
    /// <value>
    /// The pin changed.
    /// </value>
    public IObservable<SerialPinChangedEventArgs> PinChanged => GetOrCreateCachedObservable(ref _cachedPinChanged, _pinChanged);

    /// <summary>Gets the pin changed async observable.</summary>
    /// <value>
    /// The pin changed async observable.
    /// </value>
    public IObservableAsync<SerialPinChangedEventArgs> PinChangedAsync => GetOrCreateCachedAsyncObservable(ref _cachedPinChangedAsync, PinChanged);
#endif

    /// <summary>Gets a lazily-created observable sequence of complete lines split by the NewLine sequence.</summary>
    public IObservable<string> Lines => _lines ??= CreateLinesObservable();

    /// <summary>Gets a lazily-created async observable sequence of complete lines split by the NewLine sequence.</summary>
    public IObservableAsync<string> LinesAsync => _linesAsync ??= Lines.ToObservableAsync();

    /// <summary>Gets the connection observable that opens and wires the serial port.</summary>
    private IObservable<Unit> Connect => Observable.Create<Unit>(obs =>
    {
        var dis = new CompositeDisposable();

        // Check that the port exists
        if (!PortExists(PortName))
        {
            obs.OnError(new InvalidOperationException($"Serial Port {PortName} does not exist"));
            return dis;
        }
        else
        {
            SerialPort port;
            try
            {
                var newLine = NewLine;
                var handshake = Handshake;
                var readTimeout = ReadTimeout;
                var writeTimeout = WriteTimeout;
                var encoding = Encoding;

                port = new(PortName, BaudRate, Parity, DataBits, StopBits)
                {
                    NewLine = newLine,
                    Handshake = handshake,
                    ReadTimeout = readTimeout,
                    WriteTimeout = writeTimeout,
                    Encoding = encoding,
                    ReadBufferSize = _readBufferSize,
                    WriteBufferSize = _writeBufferSize,
                };
            }
            catch (Exception ex)
            {
                obs.OnError(ex);
                return dis;
            }
#if HasWindows
            dis.Add(port.PinChangedObserver().Subscribe(_pinChanged));
#endif

            dis.Add(port);
            _serialPort = port;
            try
            {
                port.Open();
                port.BreakState = _breakState;
                port.DiscardNull = _discardNull;
                port.DtrEnable = _dtrEnable;
                port.ParityReplace = _parityReplace;
                port.ReceivedBytesThreshold = _receivedBytesThreshold;
                port.RtsEnable = _rtsEnable;
            }
            catch (Exception ex)
            {
                ReportError(ex);
                obs.OnError(ex);
                return dis;
            }

            TryPublishIsOpen(port.IsOpen);
            if (port.IsOpen)
            {
                obs.OnNext(Unit.Default);
            }

            // Clear any existing buffers
            if (IsOpen)
            {
                port.DiscardInBuffer();
                port.DiscardOutBuffer();
            }

            Thread.Sleep(50);

            // Subscribe to port errors
            dis.Add(port.ErrorReceivedObserver().Subscribe(e => ReportError(new InvalidOperationException(e.EventArgs.EventType.ToString()))));

            // Get the stream of data from the serial port using the DataReceived event
            // Only subscribe if EnableAutoDataReceive is true (allows sync reads when false)
            if (EnableAutoDataReceive)
            {
                dis.Add(port.DataReceivedObserver()
                    .SelectMany(_ =>
                    {
                        try
                        {
                            var data = port.ReadExisting();
                            return Observable.FromEnumerable(data.ToCharArray());
                        }
                        catch (OperationCanceledException)
                        {
                            return Observable.Empty<char>();
                        }
                        catch (Exception)
                        {
                            return Observable.Empty<char>();
                        }
                    })
                    .Subscribe(
                        ch =>
                        {
                            _dataReceived.OnNext(ch);
                            _dataReceivedBytes.OnNext((byte)ch);
                        },
                        obs.OnError));
            }

            // setup Write streams
            dis.Add(_writeString.Subscribe(
                x =>
                {
                    try
                    {
                        port.Write(x);
                    }
                    catch (Exception ex)
                    {
                        ReportError(ex);
                    }
                },
                ex => ReportError(ex)));
            dis.Add(_writeStringLine.Subscribe(
                x =>
                {
                    try
                    {
                        port.WriteLine(x);
                    }
                    catch (Exception ex)
                    {
                        ReportError(ex);
                    }
                },
                ex => ReportError(ex)));
            dis.Add(_writeByte.Subscribe(
                x =>
                {
                    try
                    {
                        port.Write(x.byteArray, x.offset, x.count);
                    }
                    catch (Exception ex)
                    {
                        ReportError(ex);
                    }
                },
                ex => ReportError(ex)));
            dis.Add(_writeChar.Subscribe(
                x =>
                {
                    try
                    {
                        port.Write(x.charArray, x.offset, x.count);
                    }
                    catch (Exception ex)
                    {
                        ReportError(ex);
                    }
                },
                ex => ReportError(ex)));
            dis.Add(_discardInBuffer.Subscribe(
                _ =>
                {
                    try
                    {
                        port.DiscardInBuffer();
                    }
                    catch (Exception ex)
                    {
                        ReportError(ex);
                    }
                },
                ex => ReportError(ex)));
            dis.Add(_discardOutBuffer.Subscribe(
                _ =>
                {
                    try
                    {
                        port.DiscardOutBuffer();
                    }
                    catch (Exception ex)
                    {
                        ReportError(ex);
                    }
                },
                ex => ReportError(ex)));
            dis.Add(_readBytes.Subscribe(
                async x =>
                {
                    try
                    {
                        await _readLock.WaitAsync().ConfigureAwait(false);

                        // Yield to avoid blocking the OnNext caller's thread
                        await Task.Yield();

                        // Read only available bytes, up to the requested count
                        var bytesAvailable = port.BytesToRead;
                        if (bytesAvailable == 0)
                        {
                            // No data available, signal 0 bytes read
                            _bytesRead.OnNext(0);
                            return;
                        }

                        var bytesToRead = Math.Min(bytesAvailable, x.count);
                        var br = port.Read(x.buffer, x.offset, bytesToRead);

                        for (var i = 0; i < br; i++)
                        {
                            var item = x.buffer[x.offset + i];
                            _bytesReceived.OnNext(item);
                        }

                        _bytesRead.OnNext(br);
                    }
                    catch (TimeoutException)
                    {
                        _bytesRead.OnNext(0);
                    }
                    catch (Exception ex)
                    {
                        ReportError(ex);
                    }
                    finally
                    {
                        _ = _readLock.Release();
                    }
                },
                ex => ReportError(ex)));
        }

        return Disposable.Create(() =>
        {
            if (IsDisposed)
            {
                return;
            }

            TryPublishIsOpen(false);
            _serialPort = null;
            dis.Dispose();
        });
    });

    /// <summary>Gets the port names.</summary>
    /// <param name="pollInterval">The poll interval.</param>
    /// <param name="pollLimit">The poll limit, once number is reached observable will complete.</param>
    /// <returns>Observable string.</returns>
    /// <value>The port names.</value>
    public static IObservable<string[]> PortNames(int pollInterval = 500, int pollLimit = 0) => Observable.Create<string[]>(obs =>
    {
        string[]? compare = null;
        var numberOfPolls = 0;
        var subscription = Observable.Interval(TimeSpan.FromMilliseconds(pollInterval)).Subscribe(_ =>
        {
            var compareNew = SerialPort.GetPortNames();
            if (compareNew.Length == 0)
            {
                compareNew = NoPorts;
            }

            if (compare is null)
            {
                compare = compareNew;
                obs.OnNext(compareNew);
            }

            if (compare?.SequenceEqual(compareNew) == false)
            {
                obs.OnNext(compareNew);
                compare = compareNew;
            }

            if (pollLimit <= 0)
            {
                return;
            }

            numberOfPolls++;
            if (numberOfPolls < pollLimit)
            {
                return;
            }

            obs.OnCompleted();
        });
        return Disposable.Create(() => subscription.Dispose());
    });

    /// <summary>Closes this instance.</summary>
    public void Close() => _disposablePort?.Dispose();

    /// <summary>Discards the in buffer.</summary>
    public void DiscardInBuffer() => _discardInBuffer.OnNext(Unit.Default);

    /// <summary>Discards the out buffer.</summary>
    public void DiscardOutBuffer() => _discardOutBuffer.OnNext(Unit.Default);

    /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Opens this instance.</summary>
    /// <returns>
    /// A Task.
    /// </returns>
    public Task Open()
    {
        if (_disposablePort?.IsDisposed != false)
        {
            _disposablePort = [];
        }

        if (_disposablePort?.Count != 0)
        {
            return Task.CompletedTask;
        }

        var opened = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _disposablePort.Add(
            Connect.Subscribe(
                _ => opened.TrySetResult(null),
                ex =>
                {
                    ReportError(ex);
                    _ = opened.TrySetException(ex);
                },
                () =>
                {
                    if (!IsOpen)
                    {
                        return;
                    }

                    _ = opened.TrySetResult(null);
                }));

        return opened.Task;
    }

    /// <summary>Writes the specified text.</summary>
    /// <param name="text">The text.</param>
    public void Write(string text) =>
        _writeString?.OnNext(text);

    /// <summary>Writes the specified byte array.</summary>
    /// <param name="buffer">The byte array.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    public void Write(byte[] buffer, int offset, int count) =>
        _writeByte?.OnNext((buffer, offset, count));

    /// <summary>Writes the specified byte array.</summary>
    /// <param name="byteArray">The byte array.</param>
    public void Write(byte[] byteArray)
    {
#if NETFRAMEWORK
        if (byteArray is null)
        {
            throw new ArgumentNullException(nameof(byteArray));
        }
#else
        ArgumentNullException.ThrowIfNull(byteArray);
#endif

        _writeByte?.OnNext((byteArray, 0, byteArray.Length));
    }

    /// <summary>Writes the specified character array.</summary>
    /// <param name="charArray">The character array.</param>
    public void Write(char[] charArray)
    {
#if NETFRAMEWORK
        if (charArray is null)
        {
            throw new ArgumentNullException(nameof(charArray));
        }
#else
        ArgumentNullException.ThrowIfNull(charArray);
#endif

        _writeChar?.OnNext((charArray, 0, charArray.Length));
    }

    /// <summary>Writes the specified character array.</summary>
    /// <param name="charArray">The character array.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    public void Write(char[] charArray, int offset, int count) =>
        _writeChar?.OnNext((charArray, offset, count));

    /// <summary>Writes the line.</summary>
    /// <param name="text">The text.</param>
    public void WriteLine(string text) =>
        _writeStringLine?.OnNext(text);

    /// <summary>Reads the specified buffer.</summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <returns>
    /// The number of bytes read.
    /// </returns>
    public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        EnsureOpen();
        _readBytes.OnNext((buffer, offset, count));

        // Use timeout if configured, otherwise wait indefinitely
        if (ReadTimeout > 0)
        {
            var readTask = FirstValueAsync(_bytesRead);
            var timeoutTask = Task.Delay(ReadTimeout);
            var completed = await Task.WhenAny(readTask, timeoutTask).ConfigureAwait(false);
            if (completed != readTask)
            {
                throw new TimeoutException("ReadAsync timed out.");
            }

            return await readTask.ConfigureAwait(false);
        }

        return await FirstValueAsync(_bytesRead).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads a number of bytes from the SerialPort input buffer and writes those bytes into a byte array at the specified offset.
    /// </summary>
    /// <param name="buffer">The byte array to write the input to.</param>
    /// <param name="offset">The offset in the buffer array to begin writing.</param>
    /// <param name="count">The number of bytes to read.</param>
    /// <returns>The number of bytes read.</returns>
    public int Read(byte[] buffer, int offset, int count)
    {
#if NETFRAMEWORK
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }
#else
        ArgumentNullException.ThrowIfNull(buffer);
#endif

        EnsureOpen();
        _readLock.Wait();
        try
        {
            for (var i = 0; i < count; i++)
            {
                var b = _serialPort!.ReadByte();
                if (b == -1)
                {
                    throw new TimeoutException();
                }

                buffer[offset + i] = (byte)b;
            }

            return count;
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>
    /// Reads a number of characters from the SerialPort input buffer and writes those characters into a character array at the specified offset.
    /// </summary>
    /// <param name="buffer">The character array to write the input to.</param>
    /// <param name="offset">The offset in the buffer array to begin writing.</param>
    /// <param name="count">The number of characters to read.</param>
    /// <returns>The number of characters read.</returns>
    public int Read(char[] buffer, int offset, int count)
    {
        EnsureOpen();
        _readLock.Wait();
        try
        {
            return _serialPort!.Read(buffer, offset, count);
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>Synchronously reads one byte from the SerialPort input buffer.</summary>
    /// <returns>The byte, or -1 if no byte is available.</returns>
    public int ReadByte()
    {
        EnsureOpen();
        _readLock.Wait();
        try
        {
            return _serialPort!.ReadByte();
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>Synchronously reads one character from the SerialPort input buffer.</summary>
    /// <returns>The character, or -1 if no character is available.</returns>
    public int ReadChar()
    {
        EnsureOpen();
        _readLock.Wait();
        try
        {
            return _serialPort!.ReadChar();
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>
    /// Reads all immediately available bytes, based on the encoding, in both the stream and the input buffer of the SerialPort object.
    /// </summary>
    /// <returns>The contents of the input buffer and the stream.</returns>
    public string ReadExisting()
    {
        EnsureOpen();
        _readLock.Wait();
        try
        {
            return _serialPort!.ReadExisting();
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>Reads up to the NewLine value in the input buffer.</summary>
    /// <returns>The contents of the input buffer up to the first occurrence of a NewLine value.</returns>
    public string ReadLine()
    {
        EnsureOpen();
        _readLock.Wait();
        try
        {
            return _serialPort!.ReadLine();
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>Reads a string up to the specified value in the input buffer.</summary>
    /// <param name="value">The value to read up to.</param>
    /// <returns>The contents of the input buffer up to the specified value.</returns>
    public string ReadTo(string value)
    {
        EnsureOpen();
#if NETFRAMEWORK
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentNullException(nameof(value));
        }
#else
        ArgumentException.ThrowIfNullOrEmpty(value);
#endif

        _readLock.Wait();
        try
        {
            var sb = new StringBuilder();
            while (true)
            {
                var c = _serialPort!.ReadChar();
                if (c == -1)
                {
                    break;
                }

                _ = sb.Append((char)c);
                if (sb.Length >= value.Length && sb.ToString(sb.Length - value.Length, value.Length) == value)
                {
                    sb.Length -= value.Length;
                    break;
                }
            }

            return sb.ToString();
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>Reads the line asynchronous.</summary>
    /// <exception cref="InvalidOperationException">Serial port is not open.</exception>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task<string> ReadLineAsync() => ReadLineAsync(CancellationToken.None);

    /// <summary>Reads the line asynchronous with cancellation and respecting ReadTimeout (> 0) as a timeout.</summary>
    /// <param name="cancellationToken">Cancellation token to cancel waiting.</param>
    /// <returns>A Task of string.</returns>
    public async Task<string> ReadLineAsync(CancellationToken cancellationToken)
    {
        EnsureOpen();

        await _readLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sb = new StringBuilder(256);
            var newLineLocal = NewLine ?? "\n";
            var newLineLength = newLineLocal.Length;
            var newLineFirstChar = newLineLocal[0];

            var subscription = DataReceived.Subscribe(
                ch =>
                {
                    _ = sb.Append(ch);
                    if (!TryExtractLine(sb, newLineLocal, newLineLength, newLineFirstChar, ch, out var line))
                    {
                        return;
                    }

                    _ = tcs.TrySetResult(line);
                },
                ex => _ = tcs.TrySetException(ex),
                () => _ = tcs.TrySetException(new InvalidOperationException("Serial port is not open.")));

            CancellationTokenSource? timeoutCts = null;
            CancellationTokenSource? linkedCts = null;
            var token = cancellationToken;

            if (ReadTimeout > 0)
            {
                timeoutCts = new(ReadTimeout);
                if (cancellationToken.CanBeCanceled)
                {
                    linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                    token = linkedCts.Token;
                }
                else
                {
                    token = timeoutCts.Token;
                }
            }

            using (token.Register(() =>
            {
                if (timeoutCts?.IsCancellationRequested == true && ReadTimeout > 0)
                {
                    _ = tcs.TrySetException(new TimeoutException("ReadLineAsync timed out."));
                }
                else
                {
                    _ = tcs.TrySetCanceled(token);
                }
            }))
            {
                try
                {
                    return await tcs.Task.ConfigureAwait(false);
                }
                finally
                {
                    subscription.Dispose();
                    timeoutCts?.Dispose();
                    linkedCts?.Dispose();
                }
            }
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>Reads a string up to the specified value asynchronously.</summary>
    /// <param name="value">The value to read up to.</param>
    /// <param name="cancellationToken">Cancellation token to cancel waiting.</param>
    /// <returns>The contents of the input buffer up to the specified value.</returns>
    public async Task<string> ReadToAsync(string value, CancellationToken cancellationToken = default)
    {
        EnsureOpen();

#if NETFRAMEWORK
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentNullException(nameof(value));
        }
#else
        ArgumentException.ThrowIfNullOrEmpty(value);
#endif

        await _readLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sb = new StringBuilder(128);
            var valueLength = value.Length;

            var subscription = DataReceived.Subscribe(
                ch =>
                {
                    _ = sb.Append(ch);
                    if (sb.Length < valueLength || !TryMatchSuffix(sb, value, valueLength))
                    {
                        return;
                    }

                    sb.Length -= valueLength;
                    _ = tcs.TrySetResult(sb.ToString());
                },
                ex => _ = tcs.TrySetException(ex),
                () => _ = tcs.TrySetException(new InvalidOperationException("Serial port is not open.")));

            CancellationTokenSource? timeoutCts = null;
            CancellationTokenSource? linkedCts = null;
            var token = cancellationToken;

            if (ReadTimeout > 0)
            {
                timeoutCts = new(ReadTimeout);
                if (cancellationToken.CanBeCanceled)
                {
                    linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                    token = linkedCts.Token;
                }
                else
                {
                    token = timeoutCts.Token;
                }
            }

            using (token.Register(() =>
            {
                if (timeoutCts?.IsCancellationRequested == true && ReadTimeout > 0)
                {
                    _ = tcs.TrySetException(new TimeoutException("ReadToAsync timed out."));
                }
                else
                {
                    _ = tcs.TrySetCanceled(token);
                }
            }))
            {
                try
                {
                    return await tcs.Task.ConfigureAwait(false);
                }
                finally
                {
                    subscription.Dispose();
                    timeoutCts?.Dispose();
                    linkedCts?.Dispose();
                }
            }
        }
        finally
        {
            _ = _readLock.Release();
        }
    }

    /// <summary>
    /// Starts continuous data reception that feeds both DataReceived and DataReceivedBytes observables.
    /// Call this after Open() to enable reactive data streaming.
    /// </summary>
    /// <param name="pollingIntervalMs">Polling interval in milliseconds (default: 10ms).</param>
    /// <returns>A disposable that stops the data reception when disposed.</returns>
    public IDisposable StartDataReception(int pollingIntervalMs = 10)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("Serial port must be open before starting data reception.");
        }

        var cts = new CancellationTokenSource();
        var disposable = new CompositeDisposable();

        _ = Task.Run(() => RunDataReceptionLoopAsync(pollingIntervalMs, cts.Token), cts.Token);

        disposable.Add(Disposable.Create(() =>
        {
            cts.Cancel();
            cts.Dispose();
        }));

        return disposable;
    }

#if !NETFRAMEWORK
    /// <summary>Writes the specified data from a ReadOnlySpan.</summary>
    /// <param name="data">The data to write.</param>
    public void Write(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        var array = ArrayPool<byte>.Shared.Rent(data.Length);
        try
        {
            data.CopyTo(array);
            _writeByte?.OnNext((array, 0, data.Length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(array);
        }
    }

    /// <summary>Writes the specified data from a ReadOnlyMemory.</summary>
    /// <param name="data">The data to write.</param>
    public void Write(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        Write(data.Span);
    }

    /// <summary>Writes the specified character data from a ReadOnlySpan.</summary>
    /// <param name="data">The character data to write.</param>
    public void Write(ReadOnlySpan<char> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        var array = ArrayPool<char>.Shared.Rent(data.Length);
        try
        {
            data.CopyTo(array);
            _writeChar?.OnNext((array, 0, data.Length));
        }
        finally
        {
            ArrayPool<char>.Shared.Return(array);
        }
    }
#endif

    /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only
    /// unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed)
        {
            return;
        }

        if (disposing)
        {
            _disposablePort?.Dispose();
            _isOpenValue.Dispose();
            _dataReceived.Dispose();
            _dataReceivedBytes.Dispose();
            _errors.Dispose();
            _writeByte.Dispose();
            _writeChar.Dispose();
            _writeString.Dispose();
            _writeStringLine.Dispose();
            _discardInBuffer.Dispose();
            _discardOutBuffer.Dispose();
            _readBytes.Dispose();
            _bytesRead.Dispose();
            _bytesReceived.Dispose();
            _readLock.Dispose();
            _pinChanged.Dispose();
        }

        IsDisposed = true;
    }

    /// <summary>Attempts to extract a complete line from the StringBuilder, optimized for common cases.</summary>
    /// <param name="sb">The buffer containing received characters.</param>
    /// <param name="newLine">The configured line terminator.</param>
    /// <param name="newLineLength">The configured line terminator length.</param>
    /// <param name="newLineFirstChar">The first character in the configured line terminator.</param>
    /// <param name="lastChar">The last character appended to the buffer.</param>
    /// <param name="line">The extracted line when a complete line is found.</param>
    /// <returns><see langword="true"/> when a complete line was extracted; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryExtractLine(StringBuilder sb, string newLine, int newLineLength, char newLineFirstChar, char lastChar, out string line)
    {
        line = string.Empty;

        // Fast path for single-character newline (most common: \n)
        if (newLineLength == 1)
        {
            if (lastChar == newLineFirstChar)
            {
                sb.Length--;
                line = sb.ToString();
                _ = sb.Clear();
                return true;
            }

            return false;
        }

        // Multi-character newline handling
        if (sb.Length < newLineLength)
        {
            return false;
        }

        // Fast path for two-character newline (\r\n)
        if (newLineLength == 2)
        {
            var len = sb.Length;
            if (sb[len - 2] == newLine[0] && sb[len - 1] == newLine[1])
            {
                sb.Length -= 2;
                line = sb.ToString();
                _ = sb.Clear();
                return true;
            }

            return false;
        }

        // General case for longer newlines
        return TryExtractLineGeneral(sb, newLine, newLineLength, out line);
    }

    /// <summary>General case line extraction for newlines longer than 2 characters.</summary>
    /// <param name="sb">The buffer containing received characters.</param>
    /// <param name="newLine">The configured line terminator.</param>
    /// <param name="newLineLength">The configured line terminator length.</param>
    /// <param name="line">The extracted line when a complete line is found.</param>
    /// <returns><see langword="true"/> when a complete line was extracted; otherwise, <see langword="false"/>.</returns>
    private static bool TryExtractLineGeneral(StringBuilder sb, string newLine, int newLineLength, out string line)
    {
        line = string.Empty;
        var start = sb.Length - newLineLength;

        // Use stackalloc for small newlines, ArrayPool for larger ones
        if (newLineLength <= 256)
        {
            Span<char> tail = stackalloc char[newLineLength];
            for (var i = 0; i < newLineLength; i++)
            {
                tail[i] = sb[start + i];
            }

            if (tail.SequenceEqual(newLine.AsSpan()))
            {
                sb.Length -= newLineLength;
                line = sb.ToString();
                _ = sb.Clear();
                return true;
            }
        }
        else
        {
            var buffer = ArrayPool<char>.Shared.Rent(newLineLength);
            try
            {
                var tail = buffer.AsSpan(0, newLineLength);
                for (var i = 0; i < newLineLength; i++)
                {
                    tail[i] = sb[start + i];
                }

                if (tail.SequenceEqual(newLine.AsSpan()))
                {
                    sb.Length -= newLineLength;
                    line = sb.ToString();
                    _ = sb.Clear();
                    return true;
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        return false;
    }

    /// <summary>Checks if the StringBuilder ends with the specified value.</summary>
    /// <param name="sb">The buffer to inspect.</param>
    /// <param name="value">The suffix value to match.</param>
    /// <param name="valueLength">The suffix value length.</param>
    /// <returns><see langword="true"/> when the buffer ends with the value; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryMatchSuffix(StringBuilder sb, string value, int valueLength)
    {
        var start = sb.Length - valueLength;
        for (var i = 0; i < valueLength; i++)
        {
            if (sb[start + i] != value[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether a serial port name is available on the system.</summary>
    /// <param name="portName">The port name to find.</param>
    /// <returns><see langword="true"/> when the port exists; otherwise, <see langword="false"/>.</returns>
    private static bool PortExists(string portName)
    {
        foreach (var name in SerialPort.GetPortNames())
        {
            if (name.Equals(portName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the first value from an observable sequence as a task.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <returns>A task that completes with the first observed value.</returns>
    private static Task<T> FirstValueAsync<T>(IObservable<T> source)
    {
#if REACTIVE_SHIM
        return source.Take(1).ToTask();
#else
        return source.Take(1).FirstAsync();
#endif
    }

    /// <summary>Ensures the serial port is currently open.</summary>
    private void EnsureOpen()
    {
        if (IsOpen || IsDisposed)
        {
            return;
        }

        throw new InvalidOperationException("Serial port is not open.");
    }

    /// <summary>Publishes a serial port error if the instance can still accept notifications.</summary>
    /// <param name="exception">The exception to publish.</param>
    private void ReportError(Exception exception)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            _errors.OnNext(exception);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>Publishes the current open state if the instance can still accept notifications.</summary>
    /// <param name="isOpen">The open state to publish.</param>
    private void TryPublishIsOpen(bool isOpen)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            _isOpenValue.OnNext(isOpen);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>Creates a cached observable that is thread-safe and avoids repeated Publish/RefCount allocations.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="cached">The cached observable reference.</param>
    /// <param name="subject">The backing subject to cache.</param>
    /// <returns>The cached observable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<T> GetOrCreateCachedObservable<T>(ref IObservable<T>? cached, IObservable<T> subject)
    {
        var current = Volatile.Read(ref cached);
        if (current is not null)
        {
            return current;
        }

        lock (_observableCacheLock)
        {
            current = Volatile.Read(ref cached);
            if (current is not null)
            {
                return current;
            }

            Volatile.Write(ref cached, subject);
            return subject;
        }
    }

    /// <summary>Creates a cached observable from a factory that is thread-safe.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="cached">The cached observable reference.</param>
    /// <param name="factory">The observable factory to invoke when no cached value exists.</param>
    /// <returns>The cached observable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<T> GetOrCreateCachedObservable<T>(ref IObservable<T>? cached, Func<IObservable<T>> factory)
    {
        var current = Volatile.Read(ref cached);
        if (current is not null)
        {
            return current;
        }

        lock (_observableCacheLock)
        {
            current = Volatile.Read(ref cached);
            if (current is not null)
            {
                return current;
            }

            var observable = factory();
            Volatile.Write(ref cached, observable);
            return observable;
        }
    }

    /// <summary>Creates a cached async observable that is thread-safe.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="cached">The cached async observable reference.</param>
    /// <param name="source">The source observable to adapt and cache.</param>
    /// <returns>The cached async observable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservableAsync<T> GetOrCreateCachedAsyncObservable<T>(ref IObservableAsync<T>? cached, IObservable<T> source)
    {
        var current = Volatile.Read(ref cached);
        if (current is not null)
        {
            return current;
        }

        lock (_observableCacheLock)
        {
            current = Volatile.Read(ref cached);
            if (current is not null)
            {
                return current;
            }

            var observable = source.ToObservableAsync();
            Volatile.Write(ref cached, observable);
            return observable;
        }
    }

    /// <summary>Creates the optimized lines observable with efficient line parsing.</summary>
    /// <returns>A cached line observable sequence.</returns>
    private IObservable<string> CreateLinesObservable() =>
        Observable.Defer(() =>
            Observable.Create<string>(obs =>
            {
                var sb = new StringBuilder(256);
                var newLineLocal = NewLine ?? "\n";
                var newLineLength = newLineLocal.Length;
                var newLineFirstChar = newLineLocal[0];

                return DataReceived.Subscribe(
                    ch =>
                    {
                        _ = sb.Append(ch);
                        if (!TryExtractLine(sb, newLineLocal, newLineLength, newLineFirstChar, ch, out var line))
                        {
                            return;
                        }

                        obs.OnNext(line);
                    },
                    obs.OnError);
            }));

    /// <summary>Creates an error observable that suppresses duplicate message values.</summary>
    /// <returns>An observable sequence of distinct serial port errors.</returns>
    private IObservable<Exception> CreateDistinctErrorObservable() => Observable.Create<Exception>(observer =>
    {
        var seenMessages = new HashSet<string>(StringComparer.Ordinal);
        return _errors.Subscribe(
            exception =>
            {
                if (!seenMessages.Add(exception.Message))
                {
                    return;
                }

                observer.OnNext(exception);
            },
            observer.OnError,
            observer.OnCompleted);
    });

    /// <summary>Runs the background data reception loop until cancellation or port closure.</summary>
    /// <param name="pollingIntervalMs">The polling delay used when no bytes are available.</param>
    /// <param name="cancellationToken">The token used to stop reception.</param>
    /// <returns>A task that completes when the data reception loop stops.</returns>
    private async Task RunDataReceptionLoopAsync(int pollingIntervalMs, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ReadBufferSize);
        try
        {
            while (!cancellationToken.IsCancellationRequested && IsOpen)
            {
                try
                {
                    if (!await TryReadAvailableDataAsync(buffer, cancellationToken).ConfigureAwait(false))
                    {
                        await Task.Delay(pollingIntervalMs, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _errors.OnNext(ex);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Reads and publishes any bytes currently available on the serial port.</summary>
    /// <param name="buffer">The reusable receive buffer.</param>
    /// <param name="cancellationToken">The token used to cancel lock acquisition.</param>
    /// <returns><see langword="true"/> when data was available to process; otherwise, <see langword="false"/>.</returns>
    private async Task<bool> TryReadAvailableDataAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var port = _serialPort;
        if (port is null || port.BytesToRead <= 0)
        {
            return false;
        }

        await _readLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var bytesToRead = Math.Min(port.BytesToRead, buffer.Length);
            if (bytesToRead <= 0)
            {
                return true;
            }

            var bytesRead = port.Read(buffer, 0, bytesToRead);
            for (var i = 0; i < bytesRead; i++)
            {
                var b = buffer[i];
                _dataReceivedBytes.OnNext(b);
                _dataReceived.OnNext((char)b);
            }

            return true;
        }
        finally
        {
            _ = _readLock.Release();
        }
    }
}
