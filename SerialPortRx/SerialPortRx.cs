// <copyright file="SerialPortRx.cs" company="Chris Pulman">
// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

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

namespace CP.IO.Ports;

/// <summary>
/// Serial Port Rx.
/// </summary>
/// <seealso cref="CP.IO.Ports.ISerialPortRx"/>
public class SerialPortRx : ISerialPortRx
{
    private readonly ISubject<bool> _isOpenValue = new ReplaySubject<bool>(1);
    private readonly ISubject<char> _dataReceived = new Subject<char>();
    private readonly ISubject<Exception> _errors = new Subject<Exception>();
    private readonly ISubject<Tuple<byte[], int, int>> _writeByte = new Subject<Tuple<byte[], int, int>>();
    private readonly ISubject<Tuple<char[], int, int>> _writeChar = new Subject<Tuple<char[], int, int>>();
    private readonly ISubject<string> _writeString = new Subject<string>();
    private readonly ISubject<string> _writeStringLine = new Subject<string>();
    private readonly ISubject<Unit> _discardInBuffer = new Subject<Unit>();
    private readonly ISubject<Unit> _discardOutBuffer = new Subject<Unit>();
    private readonly CompositeDisposable _disposablePort = new();

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
    public SerialPortRx(string port)
    {
        PortName = port;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SerialPortRx"/> class.
    /// </summary>
    public SerialPortRx()
    {
    }

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
    /// Gets or sets the encoding.
    /// </summary>
    /// <value>The encoding.</value>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [MonitoringDescription("Encoding")]
    public Encoding Encoding { get; set; } = new ASCIIEncoding();

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
    public bool IsOpen { get; private set; }

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

    private IObservable<Unit> Connect => Observable.Create<Unit>(obs =>
    {
        var dis = new CompositeDisposable();

        // Check that the port exists
        if (!SerialPort.GetPortNames().Any(name => name.Equals(PortName)))
        {
            obs.OnError(new Exception($"Serial Port {PortName} does not exist"));
        }
        else
        {
            // Setup Com Port
            var port = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits);

            dis.Add(port);
            port.Close();
            port.NewLine = NewLine;
            port.Handshake = Handshake;
            port.ReadTimeout = ReadTimeout;
            port.WriteTimeout = WriteTimeout;
            port.Encoding = Encoding;
            try
            {
                port.Open();
            }
            catch (Exception ex)
            {
                _errors.OnNext(ex);
                obs.OnCompleted();
            }

            _isOpenValue.OnNext(port.IsOpen);
            IsOpen = port.IsOpen;

            // Clear any existing buffers
            if (IsOpen)
            {
                port.DiscardInBuffer();
                port.DiscardOutBuffer();
            }

            Thread.Sleep(100);

            // Subscribe to port errors
            dis.Add(port.ErrorReceivedObserver().Subscribe(e => obs.OnError(new Exception(e.EventArgs.EventType.ToString()))));

            // Get the stream of Char from the serial port
            var dataStream =
                from dataRecieved in port.DataReceivedObserver()
                from data in port.ReadExisting()
                select data;
            dis.Add(dataStream.Subscribe(_dataReceived.OnNext, obs.OnError));

            // setup Write streams
            dis.Add(_writeString.Subscribe(
                x =>
                {
                    try
                    {
                        port?.Write(x);
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
                        port?.WriteLine(x);
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
                        port?.Write(x.Item1, x.Item2, x.Item3);
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
                        port?.Write(x.Item1, x.Item2, x.Item3);
                    }
                    catch (Exception ex)
                    {
                        obs.OnError(ex);
                    }
                },
                obs.OnError));
            dis.Add(_discardInBuffer.Subscribe(
                x =>
                {
                    try
                    {
                        port?.DiscardInBuffer();
                    }
                    catch (Exception ex)
                    {
                        obs.OnError(ex);
                    }
                },
                obs.OnError));
            dis.Add(_discardOutBuffer.Subscribe(
                x =>
                {
                    try
                    {
                        port?.DiscardOutBuffer();
                    }
                    catch (Exception ex)
                    {
                        obs.OnError(ex);
                    }
                },
                obs.OnError));
        }

        return Disposable.Create(() =>
        {
            IsOpen = false;
            _isOpenValue.OnNext(false);
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
        return Observable.Interval(TimeSpan.FromMilliseconds(pollInterval)).Subscribe(_ =>
        {
            var compareNew = SerialPort.GetPortNames();
            if (compareNew.Length == 0)
            {
                compareNew = new string[] { "NoPorts" };
            }

            if (compare == null)
            {
                compare = compareNew;
                obs.OnNext(compareNew);
            }

            if (string.Concat(compare) != string.Concat(compareNew))
            {
                obs.OnNext(compareNew);
                compare = compareNew;
            }

            if (numberOfPolls > pollLimit)
            {
                obs.OnCompleted();
            }

            if (pollLimit > 0 && numberOfPolls < pollLimit)
            {
                numberOfPolls++;
            }
        });
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
    public Task Open() =>
        _disposablePort?.Count == 0 ? Task.Run(() => Connect.Subscribe().AddTo(_disposablePort)) : Task.CompletedTask;

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
        _writeByte?.OnNext(new Tuple<byte[], int, int>(byteArray, offset, count));

    /// <summary>
    /// Writes the specified byte array.
    /// </summary>
    /// <param name="byteArray">The byte array.</param>
    public void Write(byte[] byteArray)
    {
        if (byteArray == null)
        {
            throw new ArgumentNullException(nameof(byteArray));
        }

        _writeByte?.OnNext(new Tuple<byte[], int, int>(byteArray, 0, byteArray.Length));
    }

    /// <summary>
    /// Writes the specified character array.
    /// </summary>
    /// <param name="charArray">The character array.</param>
    public void Write(char[] charArray)
    {
        if (charArray == null)
        {
            throw new ArgumentNullException(nameof(charArray));
        }

        _writeChar?.OnNext(new Tuple<char[], int, int>(charArray, 0, charArray.Length));
    }

    /// <summary>
    /// Writes the specified character array.
    /// </summary>
    /// <param name="charArray">The character array.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    public void Write(char[] charArray, int offset, int count) =>
        _writeChar?.OnNext(new Tuple<char[], int, int>(charArray, offset, count));

    /// <summary>
    /// Writes the line.
    /// </summary>
    /// <param name="text">The text.</param>
    public void WriteLine(string text) =>
        _writeStringLine?.OnNext(text);

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
                _disposablePort?.Dispose();
            }

            IsDisposed = true;
        }
    }
}
