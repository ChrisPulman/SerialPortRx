// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI.Extensions;

namespace CP.IO.Ports;

/// <summary>
/// Serial Port Rx.
/// </summary>
/// <seealso cref="ISerialPortRx"/>
public class SerialPortRx : ISerialPortRx
{
    private static readonly string[] noPorts = ["NoPorts"];
    private readonly ReplaySubject<bool> _isOpenValue = new(1);
    private readonly Subject<char> _dataReceived = new();
    private readonly Subject<Exception> _errors = new();
    private readonly Subject<(byte[] byteArray, int offset, int count)> _writeByte = new();
    private readonly Subject<(char[] charArray, int offset, int count)> _writeChar = new();
    private readonly Subject<string> _writeString = new();
    private readonly Subject<string> _writeStringLine = new();
    private readonly Subject<Unit> _discardInBuffer = new();
    private readonly Subject<Unit> _discardOutBuffer = new();
    private readonly Subject<(byte[] buffer, int offset, int count)> _readBytes = new();
    private readonly Subject<int> _bytesRead = new();
    private readonly Subject<int> _bytesReceived = new();
    private readonly Subject<SerialPinChangedEventArgs> _pinChanged = new();
    private readonly SemaphoreSlim _readLock = new(1, 1);
    private CompositeDisposable _disposablePort = [];

    // Lazily-created line observable for continuous line parsing
    private IObservable<string>? _lines;

    private SerialPort? _serialPort;
    private bool _breakState;
    private bool _discardNull;
    private bool _dtrEnable;
    private byte _parityReplace = 63;
    private int _readBufferSize = 4096;
    private int _receivedBytesThreshold = 1;
    private bool _rtsEnable;
    private int _writeBufferSize = 2048;

    /// <summary>
    /// Initializes a new instance of the <see cref="SerialPortRx"/> class.
    /// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="SerialPortRx"/> class.
    /// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="SerialPortRx"/> class.
    /// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="SerialPortRx"/> class.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <param name="baudRate">The baud rate.</param>
    /// <param name="dataBits">The data bits.</param>
    public SerialPortRx(string port, int baudRate, int dataBits)
    {
        PortName = port;
        BaudRate = baudRate;
        DataBits = dataBits;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SerialPortRx"/> class.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <param name="baudRate">The baud rate.</param>
    public SerialPortRx(string port, int baudRate)
    {
        PortName = port;
        BaudRate = baudRate;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SerialPortRx" /> class.
    /// </summary>
    /// <param name="port">The port.</param>
    public SerialPortRx(string port) => PortName = port;

    /// <summary>
    /// Initializes a new instance of the <see cref="SerialPortRx"/> class.
    /// </summary>
    public SerialPortRx()
    {
    }

    /// <summary>
    /// Gets indicates that no timeout should occur.
    /// </summary>
    [Browsable(true)]
    [DefaultValue(-1)]
    [MonitoringDescription("InfiniteTimeout")]
    public int InfiniteTimeout => SerialPort.InfiniteTimeout;

    /// <summary>
    /// Gets or sets the baud rate.
    /// </summary>
    /// <value>The baud rate.</value>
    [Browsable(true)]
    [DefaultValue(9600)]
    [MonitoringDescription("BaudRate")]
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// Gets or sets the data bits.
    /// </summary>
    /// <value>The data bits.</value>
    [Browsable(true)]
    [DefaultValue(8)]
    [MonitoringDescription("DataBits")]
    public int DataBits { get; set; } = 8;

    /// <summary>
    /// Gets the data received.
    /// </summary>
    /// <value>The data received.</value>
    public IObservable<char> DataReceived => _dataReceived.Retry().Publish().RefCount();

    /// <summary>
    /// Gets the data received when executing ReadAsync.
    /// </summary>
    /// <value>The data received.</value>
    public IObservable<int> BytesReceived => _bytesReceived.Retry().Publish().RefCount();

    /// <summary>
    /// Gets or sets the encoding.
    /// </summary>
    /// <value>The encoding.</value>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [MonitoringDescription("Encoding")]
    public Encoding Encoding { get; set; } = Encoding.ASCII;

    /// <summary>
    /// Gets the error received.
    /// </summary>
    /// <value>The error received.</value>
    public IObservable<Exception> ErrorReceived => _errors.Distinct(ex => ex.Message).Retry().Publish().RefCount();

    /// <summary>
    /// Gets or sets the handshake.
    /// </summary>
    /// <value>The handshake.</value>
    [Browsable(true)]
    [DefaultValue(Handshake.None)]
    [MonitoringDescription("Handshake")]
    public Handshake Handshake { get; set; } = Handshake.None;

    /// <summary>
    /// Gets a value indicating whether this instance is disposed.
    /// </summary>
    /// <value><c>true</c> if this instance is disposed; otherwise, <c>false</c>.</value>
    [Browsable(true)]
    [MonitoringDescription("IsDisposed")]
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets a value indicating whether gets the is open.
    /// </summary>
    /// <value>The is open.</value>
    [Browsable(true)]
    [MonitoringDescription("IsOpen")]
    public bool IsOpen => _serialPort?.IsOpen ?? false;

    /// <summary>
    /// Gets the is open observable.
    /// </summary>
    /// <value>The is open observable.</value>
    public IObservable<bool> IsOpenObservable => _isOpenValue.DistinctUntilChanged();

    /// <summary>
    /// Gets or sets the parity.
    /// </summary>
    /// <value>The parity.</value>
    [Browsable(true)]
    [DefaultValue(Parity.None)]
    [MonitoringDescription("Parity")]
    public Parity Parity { get; set; } = Parity.None;

    /// <summary>
    /// Gets or sets the port.
    /// </summary>
    /// <value>The port.</value>
    [Browsable(true)]
    [DefaultValue("COM1")]
    [MonitoringDescription("PortName")]
    public string PortName { get; set; } = "COM1";

    /// <summary>
    /// Gets or sets the read timeout.
    /// </summary>
    /// <value>The read timeout.</value>
    [Browsable(true)]
    [DefaultValue(-1)]
    [MonitoringDescription("ReadTimeout")]
    public int ReadTimeout { get; set; } = -1;

    /// <summary>
    /// Gets or sets the stop bits.
    /// </summary>
    /// <value>The stop bits.</value>
    [Browsable(true)]
    [DefaultValue(StopBits.One)]
    [MonitoringDescription("StopBits")]
    public StopBits StopBits { get; set; } = StopBits.One;

    /// <summary>
    /// Gets or sets the write timeout.
    /// </summary>
    /// <value>The write timeout.</value>
    [Browsable(true)]
    [DefaultValue(-1)]
    [MonitoringDescription("WriteTimeout")]
    public int WriteTimeout { get; set; } = -1;

    /// <summary>
    /// Gets or sets creates new line.
    /// </summary>
    /// <value>
    /// The new line.
    /// </value>
    [Browsable(false)]
    [DefaultValue("\n")]
    [MonitoringDescription("NewLine")]
    public string NewLine { get; set; } = "\n";

    /// <summary>
    /// Gets or sets a value indicating whether break state.
    /// </summary>
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

    /// <summary>
    /// Gets the number of bytes of data in the receive buffer.
    /// </summary>
    /// <value>The bytes to read.</value>
    [Browsable(false)]
    [MonitoringDescription("BytesToRead")]
    public int BytesToRead => _serialPort?.BytesToRead ?? 0;

    /// <summary>
    /// Gets the number of bytes of data in the send buffer.
    /// </summary>
    /// <value>The bytes to write.</value>
    [Browsable(false)]
    [MonitoringDescription("BytesToWrite")]
    public int BytesToWrite => _serialPort?.BytesToWrite ?? 0;

    /// <summary>
    /// Gets a value indicating whether the Carrier Detect (CD) signal is on.
    /// </summary>
    /// <value>The CD holding.</value>
    [Browsable(false)]
    [MonitoringDescription("CDHolding")]
    public bool CDHolding => _serialPort?.CDHolding ?? false;

    /// <summary>
    /// Gets a value indicating whether the Clear-to-Send (CTS) signal is on.
    /// </summary>
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

    /// <summary>
    /// Gets a value indicating whether the Data Set Ready (DSR) signal is on.
    /// </summary>
    /// <value>The DSR holding.</value>
    [Browsable(false)]
    [MonitoringDescription("DsrHolding")]
    public bool DsrHolding => _serialPort?.DsrHolding ?? false;

    /// <summary>
    /// Gets or sets a value indicating whether the Data Terminal Ready (DTR) signal is enabled during serial communication.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the parity replace.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the size of the read buffer.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the number of bytes in the internal input buffer before a DataReceived event is fired.
    /// </summary>
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

    /// <summary>
    /// Gets or sets a value indicating whether the Request to Send (RTS) signal is enabled during serial communication.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the size of the write buffer.
    /// </summary>
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

#if HasWindows
    /// <summary>
    /// Gets the pin changed.
    /// </summary>
    /// <value>
    /// The pin changed.
    /// </value>
    public IObservable<SerialPinChangedEventArgs> PinChanged => _pinChanged.Retry().Publish().RefCount();
#endif

    /// <summary>
    /// Gets a lazily-created observable sequence of complete lines split by the NewLine sequence.
    /// </summary>
    public IObservable<string> Lines => _lines ??= Observable.Defer(() =>
        Observable.Create<string>(obs =>
        {
            var sb = new StringBuilder();
            var newLineLocal = NewLine ?? "\n";

            var sub = DataReceived.Subscribe(
                ch =>
                {
                    sb.Append(ch);
                    if (newLineLocal.Length == 1)
                    {
                        if (ch == newLineLocal[0])
                        {
                            sb.Length--;
                            var line = sb.ToString();
                            sb.Clear();
                            obs.OnNext(line);
                        }
                    }
                    else if (sb.Length >= newLineLocal.Length)
                    {
                        var n = newLineLocal.Length;
                        if (n <= 256)
                        {
                            Span<char> tail = stackalloc char[n];
                            var start = sb.Length - n;
                            for (var i = 0; i < n; i++)
                            {
                                tail[i] = sb[start + i];
                            }

                            if (tail.SequenceEqual(newLineLocal.AsSpan()))
                            {
                                sb.Length -= n;
                                var line = sb.ToString();
                                sb.Clear();
                                obs.OnNext(line);
                            }
                        }
                        else
                        {
                            var buffer = System.Buffers.ArrayPool<char>.Shared.Rent(n);
                            try
                            {
                                var tail = buffer.AsSpan(0, n);
                                var start = sb.Length - n;
                                for (var i = 0; i < n; i++)
                                {
                                    tail[i] = sb[start + i];
                                }

                                if (tail.SequenceEqual(newLineLocal.AsSpan()))
                                {
                                    sb.Length -= n;
                                    var line = sb.ToString();
                                    sb.Clear();
                                    obs.OnNext(line);
                                }
                            }
                            finally
                            {
                                System.Buffers.ArrayPool<char>.Shared.Return(buffer);
                            }
                        }
                    }
                },
                obs.OnError);

            return sub;
        })
        .Publish()
        .RefCount());

    private IObservable<Unit> Connect => Observable.Create<Unit>(obs =>
    {
        var dis = new CompositeDisposable();

        // Check that the port exists
        if (!SerialPort.GetPortNames().Any(name => name.Equals(PortName, StringComparison.OrdinalIgnoreCase)))
        {
            obs.OnError(new Exception($"Serial Port {PortName} does not exist"));
            return dis;
        }
        else
        {
            SerialPort port;
            try
            {
                port = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits)
                {
                    NewLine = NewLine,
                    Handshake = Handshake,
                    ReadTimeout = ReadTimeout,
                    WriteTimeout = WriteTimeout,
                    Encoding = Encoding,
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
            port.PinChangedObserver().Subscribe(_pinChanged).DisposeWith(dis);
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
                _errors.OnNext(ex);
                obs.OnCompleted();
            }

            _isOpenValue.OnNext(port.IsOpen);

            // Clear any existing buffers
            if (IsOpen)
            {
                port.DiscardInBuffer();
                port.DiscardOutBuffer();
            }

            Thread.Sleep(50);

            // Subscribe to port errors
            dis.Add(port.ErrorReceivedObserver().Subscribe(e => obs.OnError(new Exception(e.EventArgs.EventType.ToString()))));

            // Get the stream of Char from the serial port
            var dataStream =
                from events in port.DataReceivedObserver()
                from data in port.ReadExisting()
                select data;
            dis.Add(dataStream.Subscribe(_dataReceived.OnNext, obs.OnError));

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
                        obs.OnError(ex);
                    }
                },
                obs.OnError));
            dis.Add(_writeStringLine.Subscribe(
                x =>
                {
                    try
                    {
                        port.WriteLine(x);
                    }
                    catch (Exception ex)
                    {
                        obs.OnError(ex);
                    }
                },
                obs.OnError));
            dis.Add(_writeByte.Subscribe(
                x =>
                {
                    try
                    {
                        port.Write(x.byteArray, x.offset, x.count);
                    }
                    catch (Exception ex)
                    {
                        obs.OnError(ex);
                    }
                },
                obs.OnError));
            dis.Add(_writeChar.Subscribe(
                x =>
                {
                    try
                    {
                        port.Write(x.charArray, x.offset, x.count);
                    }
                    catch (Exception ex)
                    {
                        obs.OnError(ex);
                    }
                },
                obs.OnError));
            dis.Add(_discardInBuffer.Subscribe(
                _ =>
                {
                    try
                    {
                        port.DiscardInBuffer();
                    }
                    catch (Exception ex)
                    {
                        obs.OnError(ex);
                    }
                },
                obs.OnError));
            dis.Add(_discardOutBuffer.Subscribe(
                _ =>
                {
                    try
                    {
                        port.DiscardOutBuffer();
                    }
                    catch (Exception ex)
                    {
                        obs.OnError(ex);
                    }
                },
                obs.OnError));
            dis.Add(_readBytes.Subscribe(
                async x =>
                {
                    try
                    {
                        await _readLock.WaitAsync().ConfigureAwait(false);

                        // Yield to avoid blocking the OnNext caller's thread
                        await Task.Yield();
                        var br = await Task.Run(() => port.Read(x.buffer, x.offset, x.count)).ConfigureAwait(false);
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
                        obs.OnError(ex);
                    }
                    finally
                    {
                        _readLock.Release();
                    }
                },
                obs.OnError));
        }

        return Disposable.Create(() =>
        {
            _isOpenValue.OnNext(false);
            _serialPort = null;
            dis.Dispose();
        });
    }).OnErrorRetry((Exception ex) => _errors.OnNext(ex)).Publish().RefCount();

    /// <summary>
    /// Gets the port names.
    /// </summary>
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
                compareNew = noPorts;
            }

            if (compare == null)
            {
                compare = compareNew;
                obs.OnNext(compareNew);
            }

            if (compare?.SequenceEqual(compareNew) == false)
            {
                obs.OnNext(compareNew);
                compare = compareNew;
            }

            if (pollLimit > 0)
            {
                numberOfPolls++;
                if (numberOfPolls >= pollLimit)
                {
                    obs.OnCompleted();
                }
            }
        });
        return Disposable.Create(() => subscription.Dispose());
    }).Retry().Publish().RefCount();

    /// <summary>
    /// Closes this instance.
    /// </summary>
    public void Close() => _disposablePort?.Dispose();

    /// <summary>
    /// Discards the in buffer.
    /// </summary>
    public void DiscardInBuffer() => _discardInBuffer.OnNext(Unit.Default);

    /// <summary>
    /// Discards the out buffer.
    /// </summary>
    public void DiscardOutBuffer() => _discardOutBuffer.OnNext(Unit.Default);

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting
    /// unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Opens this instance.
    /// </summary>
    /// <returns>
    /// A Task.
    /// </returns>
    public Task Open()
    {
        if (_disposablePort?.IsDisposed != false)
        {
            _disposablePort = [];
        }

        return _disposablePort?.Count == 0 ? Task.Run(() => Connect.Subscribe().DisposeWith(_disposablePort)) : Task.CompletedTask;
    }

    /// <summary>
    /// Writes the specified text.
    /// </summary>
    /// <param name="text">The text.</param>
    public void Write(string text) =>
        _writeString?.OnNext(text);

    /// <summary>
    /// Writes the specified byte array.
    /// </summary>
    /// <param name="byteArray">The byte array.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    public void Write(byte[] byteArray, int offset, int count) =>
        _writeByte?.OnNext((byteArray, offset, count));

    /// <summary>
    /// Writes the specified byte array.
    /// </summary>
    /// <param name="byteArray">The byte array.</param>
    public void Write(byte[] byteArray)
    {
#if NETSTANDARD
        if (byteArray == null)
        {
            throw new ArgumentNullException(nameof(byteArray));
        }
#else
        ArgumentNullException.ThrowIfNull(byteArray);
#endif

        _writeByte?.OnNext((byteArray, 0, byteArray.Length));
    }

    /// <summary>
    /// Writes the specified character array.
    /// </summary>
    /// <param name="charArray">The character array.</param>
    public void Write(char[] charArray)
    {
#if NETSTANDARD
        if (charArray == null)
        {
            throw new ArgumentNullException(nameof(charArray));
        }
#else
        ArgumentNullException.ThrowIfNull(charArray);
#endif

        _writeChar?.OnNext((charArray, 0, charArray.Length));
    }

    /// <summary>
    /// Writes the specified character array.
    /// </summary>
    /// <param name="charArray">The character array.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    public void Write(char[] charArray, int offset, int count) =>
        _writeChar?.OnNext((charArray, offset, count));

    /// <summary>
    /// Writes the line.
    /// </summary>
    /// <param name="text">The text.</param>
    public void WriteLine(string text) =>
        _writeStringLine?.OnNext(text);

    /// <summary>
    /// Reads the specified buffer.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <returns>
    /// The number of bytes read.
    /// </returns>
    public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        _readBytes.OnNext((buffer, offset, count));
        return await _bytesRead.FirstAsync();
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
        EnsureOpen();
        _readLock.Wait();
        try
        {
            return _serialPort!.Read(buffer, offset, count);
        }
        finally
        {
            _readLock.Release();
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
            _readLock.Release();
        }
    }

    /// <summary>
    /// Synchronously reads one byte from the SerialPort input buffer.
    /// </summary>
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
            _readLock.Release();
        }
    }

    /// <summary>
    /// Synchronously reads one character from the SerialPort input buffer.
    /// </summary>
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
            _readLock.Release();
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
            _readLock.Release();
        }
    }

    /// <summary>
    /// Reads up to the NewLine value in the input buffer.
    /// </summary>
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
            _readLock.Release();
        }
    }

    /// <summary>
    /// Reads a string up to the specified value in the input buffer.
    /// </summary>
    /// <param name="value">The value to read up to.</param>
    /// <returns>The contents of the input buffer up to the specified value.</returns>
    public string ReadTo(string value)
    {
        EnsureOpen();
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentNullException(nameof(value));
        }

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

                sb.Append((char)c);
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
            _readLock.Release();
        }
    }

    /// <summary>
    /// Reads the line asynchronous.
    /// </summary>
    /// <exception cref="InvalidOperationException">Serial port is not open.</exception>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task<string> ReadLineAsync() => ReadLineAsync(CancellationToken.None);

    /// <summary>
    /// Reads the line asynchronous with cancellation and respecting ReadTimeout (> 0) as a timeout.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel waiting.</param>
    /// <returns>A Task of string.</returns>
    public async Task<string> ReadLineAsync(CancellationToken cancellationToken)
    {
        EnsureOpen();

        await _readLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sb = new StringBuilder();
            var newLineLocal = NewLine ?? "\n";

            var subscription = DataReceived.Subscribe(
                ch =>
                {
                    sb.Append(ch);
                    if (newLineLocal.Length == 1)
                    {
                        if (ch == newLineLocal[0])
                        {
                            sb.Length--;
                            var line = sb.ToString();
                            sb.Clear();
                            tcs.TrySetResult(line);
                        }
                    }
                    else if (sb.Length >= newLineLocal.Length)
                    {
                        var n = newLineLocal.Length;
                        if (n <= 256)
                        {
                            Span<char> tail = stackalloc char[n];
                            var start = sb.Length - n;
                            for (var i = 0; i < n; i++)
                            {
                                tail[i] = sb[start + i];
                            }

                            if (tail.SequenceEqual(newLineLocal.AsSpan()))
                            {
                                sb.Length -= n;
                                var line = sb.ToString();
                                sb.Clear();
                                tcs.TrySetResult(line);
                            }
                        }
                        else
                        {
                            var buffer = System.Buffers.ArrayPool<char>.Shared.Rent(n);
                            try
                            {
                                var tail = buffer.AsSpan(0, n);
                                var start = sb.Length - n;
                                for (var i = 0; i < n; i++)
                                {
                                    tail[i] = sb[start + i];
                                }

                                if (tail.SequenceEqual(newLineLocal.AsSpan()))
                                {
                                    sb.Length -= n;
                                    var line = sb.ToString();
                                    sb.Clear();
                                    tcs.TrySetResult(line);
                                }
                            }
                            finally
                            {
                                System.Buffers.ArrayPool<char>.Shared.Return(buffer);
                            }
                        }
                    }
                },
                ex => tcs.TrySetException(ex),
                () => tcs.TrySetException(new InvalidOperationException("Serial port is not open.")));

            CancellationTokenSource? timeoutCts = null;
            var token = cancellationToken;
            if (ReadTimeout > 0)
            {
                timeoutCts = new CancellationTokenSource(ReadTimeout);
                token = cancellationToken.CanBeCanceled
                    ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token).Token
                    : timeoutCts.Token;
            }

            using (token.Register(() =>
            {
                if (timeoutCts?.IsCancellationRequested == true && ReadTimeout > 0)
                {
                    tcs.TrySetException(new TimeoutException("ReadLineAsync timed out."));
                }
                else
                {
                    tcs.TrySetCanceled(token);
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
                }
            }
        }
        finally
        {
            _readLock.Release();
        }
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only
    /// unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                _isOpenValue.Dispose();
                _dataReceived.Dispose();
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
                _disposablePort?.Dispose();
                _readLock.Dispose();
                _pinChanged.Dispose();
            }

            IsDisposed = true;
        }
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("Serial port is not open.");
        }
    }
}
