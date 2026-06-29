// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IO.Ports.Tests;

/// <summary>
/// Unit tests for SerialPortRx.
/// These tests require virtual COM port pairs (COM1-COM2) to be set up.
/// Use a tool like com0com or Virtual Serial Port Driver to create virtual port pairs.
/// </summary>
[Category("Integration")]
[NotInParallel]
public sealed class SerialPortRxTests
{
    /// <summary>The first virtual serial port name.</summary>
    private const string Port1Name = "COM1";

    /// <summary>The second virtual serial port name.</summary>
    private const string Port2Name = "COM2";

    /// <summary>The default baud rate used by integration tests.</summary>
    private const int DefaultBaudRate = 9600;

    /// <summary>Check if test ports are available before running tests.</summary>
    [Before(Class)]
    public static void BeforeClass()
    {
        var availablePorts = SerialPort.GetPortNames();
        if (Array.Exists(availablePorts, p => p.Equals(Port1Name, StringComparison.OrdinalIgnoreCase)) &&
            Array.Exists(availablePorts, p => p.Equals(Port2Name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Skip.Test($"Test requires virtual COM port pair ({Port1Name} and {Port2Name}). " +
                  "Use com0com or Virtual Serial Port Driver to create virtual ports.");
    }

    /// <summary>Verifies that calling Open on a SerialPortRx instance with a valid port sets the IsOpen property to true.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task Open_WithValidPort_SetsIsOpenToTrue(CancellationToken cancellationToken)
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate);

        await port1.Open();

        await Assert.That(port1.IsOpen).IsTrue();
    }

    /// <summary>Verifies that calling Close after opening the serial port sets the IsOpen property to false.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task Close_AfterOpen_SetsIsOpenToFalse(CancellationToken cancellationToken)
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate);
        await port1.Open();

        port1.Close();

        await Assert.That(port1.IsOpen).IsFalse();
    }

    /// <summary>Verifies that the IsOpenObservable property emits the correct sequence of values.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task IsOpenObservable_EmitsCorrectValues(CancellationToken cancellationToken)
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate);
        using var disposables = new CompositeDisposable();
        var values = new List<bool>();
        disposables.Add(port1.IsOpenObservable.Subscribe(values.Add));

        await port1.Open();
        await Task.Delay(100, cancellationToken);
        port1.Close();
        await Task.Delay(100, cancellationToken);

        await Assert.That(values.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(values).Contains(true);
        await Assert.That(values[^1]).IsFalse();
    }

    /// <summary>Verifies that the DataReceived event emits the correct sequence of received characters.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task DataReceived_WhenDataWritten_EmitsReceivedCharacters(CancellationToken cancellationToken)
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = true };
        using var port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = true };
        using var cleanup = new SerialPortCleanup(port1, port2);
        using var disposables = new CompositeDisposable();

        await OpenAndClearAsync(port1, port2, cancellationToken);

        var receivedChars = new List<char>();
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        disposables.Add(port2.DataReceived.Subscribe(ch =>
        {
            receivedChars.Add(ch);
            if (receivedChars.Count < 5)
            {
                return;
            }

            _ = received.TrySetResult(true);
        }));

        await Task.Delay(100, cancellationToken);
        port1.Write("Hello");
        await received.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

        await Assert.That(receivedChars.Count).IsGreaterThanOrEqualTo(5);
        await Assert.That(string.Join(string.Empty, receivedChars)).StartsWith("Hello");
    }

    /// <summary>Verifies that when a line is written, the Lines observable emits the complete line.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task Lines_WhenLineWritten_EmitsCompleteLine(CancellationToken cancellationToken)
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { NewLine = "\r\n", EnableAutoDataReceive = true };
        using var port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { NewLine = "\r\n", EnableAutoDataReceive = true };
        using var cleanup = new SerialPortCleanup(port1, port2);
        using var disposables = new CompositeDisposable();

        await OpenAndClearAsync(port1, port2, cancellationToken);

        string? receivedLine = null;
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        disposables.Add(port2.Lines.Take(1).Subscribe(line =>
        {
            receivedLine = line;
            _ = received.TrySetResult(line);
        }));

        await Task.Delay(100, cancellationToken);
        port1.WriteLine("Test Message");
        await received.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

        await Assert.That(receivedLine).IsEqualTo("Test Message");
    }

    /// <summary>Verifies that the DataReceivedBytes observable emits the correct sequence of bytes.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task DataReceivedBytes_WhenBytesWritten_EmitsReceivedBytes(CancellationToken cancellationToken)
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = true };
        using var port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = true };
        using var cleanup = new SerialPortCleanup(port1, port2);
        using var disposables = new CompositeDisposable();

        await OpenAndClearAsync(port1, port2, cancellationToken);

        var receivedBytes = new List<byte>();
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        disposables.Add(port2.DataReceivedBytes.Subscribe(b =>
        {
            receivedBytes.Add(b);
            if (receivedBytes.Count < 3)
            {
                return;
            }

            _ = received.TrySetResult(true);
        }));

        await Task.Delay(100, cancellationToken);
        port1.Write([0x01, 0x02, 0x03], 0, 3);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

        await Assert.That(receivedBytes.Count).IsGreaterThanOrEqualTo(3);
        await Assert.That(receivedBytes[0]).IsEqualTo((byte)0x01);
        await Assert.That(receivedBytes[1]).IsEqualTo((byte)0x02);
        await Assert.That(receivedBytes[2]).IsEqualTo((byte)0x03);
    }

    /// <summary>Verifies that the ReadLine method returns the expected line when data is available.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ReadLine_WhenDataAvailable_ReturnsLine(CancellationToken cancellationToken)
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { NewLine = "\r\n", EnableAutoDataReceive = false };
        using var port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { NewLine = "\r\n", EnableAutoDataReceive = false, ReadTimeout = 2000 };
        using var cleanup = new SerialPortCleanup(port1, port2);

        await OpenAndClearAsync(port1, port2, cancellationToken);

        port1.WriteLine("Sync Test");
        await Task.Delay(200, cancellationToken);

        var line = await Task.Run(() => port2.ReadLine(), cancellationToken);

        await Assert.That(line).IsEqualTo("Sync Test");
    }

    /// <summary>Verifies that the ReadExisting method returns all available data.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ReadExisting_WhenDataAvailable_ReturnsAllData(CancellationToken cancellationToken)
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = false };
        using var port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = false };
        using var cleanup = new SerialPortCleanup(port1, port2);

        await OpenAndClearAsync(port1, port2, cancellationToken);

        port1.Write("Test Data");
        await Task.Delay(200, cancellationToken);

        var data = port2.ReadExisting();

        await Assert.That(data).IsEqualTo("Test Data");
    }

    /// <summary>Verifies that reading a byte array returns the correct bytes.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task Read_ByteArray_ReturnsCorrectBytes(CancellationToken cancellationToken)
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = false, ReadTimeout = 2000 };
        using var port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = false, ReadTimeout = 2000 };
        using var cleanup = new SerialPortCleanup(port1, port2);

        await OpenAndClearAsync(port1, port2, cancellationToken);

        port1.Write([65, 66, 67], 0, 3);
        await Task.Delay(200, cancellationToken);

        var buffer = new byte[10];
        var bytesRead = await Task.Run(() => port2.Read(buffer, 0, 3), cancellationToken);

        await Assert.That(bytesRead).IsEqualTo(3);
        await Assert.That(buffer[0]).IsEqualTo((byte)65);
        await Assert.That(buffer[1]).IsEqualTo((byte)66);
        await Assert.That(buffer[2]).IsEqualTo((byte)67);
    }

    /// <summary>Verifies that ReadLineAsync returns the expected line when data is available.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ReadLineAsync_WhenDataAvailable_ReturnsLine(CancellationToken cancellationToken)
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { NewLine = "\r\n", EnableAutoDataReceive = true };
        using var port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { NewLine = "\r\n", EnableAutoDataReceive = true, ReadTimeout = 3000 };
        using var cleanup = new SerialPortCleanup(port1, port2);

        await OpenAndClearAsync(port1, port2, cancellationToken);

        const string TestMessage = "AsyncLineTestMessage";
        var readTask = port2.ReadLineAsync(cancellationToken);

        await Task.Delay(50, cancellationToken);
        port1.WriteLine(TestMessage);

        var line = await readTask;

        await Assert.That(line).IsEqualTo(TestMessage);
    }

    /// <summary>Verifies that ReadLineAsync throws when canceled.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ReadLineAsync_WithCancellation_ThrowsOperationCanceled(CancellationToken cancellationToken)
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = true };
        using var port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = true };
        using var cleanup = new SerialPortCleanup(port1, port2);

        await OpenAndClearAsync(port1, port2, cancellationToken);

        using var cts = new CancellationTokenSource(500);

        async Task Act() => _ = await port2.ReadLineAsync(cts.Token);

        await Assert.That(Act).Throws<OperationCanceledException>();
    }

    /// <summary>Verifies that ReadToAsync returns data up to the specified delimiter.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ReadToAsync_WhenDelimiterFound_ReturnsDataUpToDelimiter(CancellationToken cancellationToken)
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = true };
        using var port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = true, ReadTimeout = 3000 };
        using var cleanup = new SerialPortCleanup(port1, port2);

        await OpenAndClearAsync(port1, port2, cancellationToken);

        var readTask = port2.ReadToAsync(">", cancellationToken);
        await Task.Delay(50, cancellationToken);
        port1.Write("Hello>World");

        var result = await readTask;

        await Assert.That(result).IsEqualTo("Hello");
    }

    /// <summary>Verifies that writing a string sends data to the other port.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task Write_String_DataReceivedOnOtherPort(CancellationToken cancellationToken)
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = false };
        using var port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = false };
        using var cleanup = new SerialPortCleanup(port1, port2);

        await OpenAndClearAsync(port1, port2, cancellationToken);

        port1.Write("Test String");
        await Task.Delay(200, cancellationToken);

        var received = port2.ReadExisting();
        await Assert.That(received).IsEqualTo("Test String");
    }

    /// <summary>Verifies that WriteLine appends the newline character.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task WriteLine_AddsNewLine(CancellationToken cancellationToken)
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { NewLine = "\n", EnableAutoDataReceive = false };
        using var port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { NewLine = "\n", EnableAutoDataReceive = false };
        using var cleanup = new SerialPortCleanup(port1, port2);

        await OpenAndClearAsync(port1, port2, cancellationToken);

        port1.WriteLine("Test");
        await Task.Delay(200, cancellationToken);

        var received = port2.ReadExisting();
        await Assert.That(received).IsEqualTo("Test\n");
    }

    /// <summary>Verifies that writing a byte array sends data correctly.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task Write_ByteArray_DataReceivedCorrectly(CancellationToken cancellationToken)
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = false };
        using var port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = false, ReadTimeout = 2000 };
        using var cleanup = new SerialPortCleanup(port1, port2);

        await OpenAndClearAsync(port1, port2, cancellationToken);

        byte[] dataToSend = [0xAA, 0xBB, 0xCC, 0xDD];

        port1.Write(dataToSend, 0, 4);
        await Task.Delay(200, cancellationToken);

        var buffer = new byte[10];
        var bytesRead = await Task.Run(() => port2.Read(buffer, 0, 4), cancellationToken);

        await Assert.That(bytesRead).IsEqualTo(4);
        await Assert.That(buffer[0]).IsEqualTo((byte)0xAA);
        await Assert.That(buffer[1]).IsEqualTo((byte)0xBB);
        await Assert.That(buffer[2]).IsEqualTo((byte)0xCC);
        await Assert.That(buffer[3]).IsEqualTo((byte)0xDD);
    }

    /// <summary>Verifies that writing a character array sends data correctly.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task Write_CharArray_DataReceivedCorrectly(CancellationToken cancellationToken)
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = false };
        using var port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = false };
        using var cleanup = new SerialPortCleanup(port1, port2);

        await OpenAndClearAsync(port1, port2, cancellationToken);

        char[] chars = ['A', 'B', 'C'];

        port1.Write(chars);
        await Task.Delay(200, cancellationToken);

        var received = port2.ReadExisting();
        await Assert.That(received).IsEqualTo("ABC");
    }

    /// <summary>Verifies default property values.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DefaultValues_AreSetCorrectly()
    {
        using var port = new SerialPortRx();

        using (Assert.Multiple())
        {
            await Assert.That(port.BaudRate).IsEqualTo(9600);
            await Assert.That(port.DataBits).IsEqualTo(8);
            await Assert.That(port.Parity).IsEqualTo(Parity.None);
            await Assert.That(port.StopBits).IsEqualTo(StopBits.One);
            await Assert.That(port.Handshake).IsEqualTo(Handshake.None);
            await Assert.That(port.NewLine).IsEqualTo("\n");
            await Assert.That(port.ReadTimeout).IsEqualTo(-1);
            await Assert.That(port.WriteTimeout).IsEqualTo(-1);
            await Assert.That(port.EnableAutoDataReceive).IsTrue();
        }
    }

    /// <summary>Verifies constructor with all parameters.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithAllParameters_SetsValuesCorrectly()
    {
        using var port = new SerialPortRx("COM3", 115_200, 7, Parity.Even, StopBits.Two, Handshake.RequestToSend);

        using (Assert.Multiple())
        {
            await Assert.That(port.PortName).IsEqualTo("COM3");
            await Assert.That(port.BaudRate).IsEqualTo(115_200);
            await Assert.That(port.DataBits).IsEqualTo(7);
            await Assert.That(port.Parity).IsEqualTo(Parity.Even);
            await Assert.That(port.StopBits).IsEqualTo(StopBits.Two);
            await Assert.That(port.Handshake).IsEqualTo(Handshake.RequestToSend);
        }
    }

    /// <summary>Verifies that opening a non-existent port throws an exception.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task Open_WithNonExistentPort_ThrowsException(CancellationToken cancellationToken)
    {
        using var port1 = new SerialPortRx("COMNONEXISTENT", DefaultBaudRate);

        await Assert.That(port1.Open).Throws<Exception>();
        await Assert.That(port1.IsOpen).IsFalse();
    }

    /// <summary>Verifies that ReadLine throws when port is not open.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ReadLine_WhenPortNotOpen_ThrowsInvalidOperationException()
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate);

        await Assert.That(port1.ReadLine).Throws<InvalidOperationException>();
    }

    /// <summary>Verifies that ReadExisting throws when port is not open.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ReadExisting_WhenPortNotOpen_ThrowsInvalidOperationException()
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate);

        await Assert.That(port1.ReadExisting).Throws<InvalidOperationException>();
    }

    /// <summary>Verifies that PortNames returns available ports.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task PortNames_ReturnsAvailablePorts(CancellationToken cancellationToken)
    {
        using var disposables = new CompositeDisposable();
        var receivedPorts = new List<string[]>();
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        disposables.Add(SerialPortRx.PortNames(100, 1).Subscribe(ports =>
        {
            receivedPorts.Add(ports);
            _ = received.TrySetResult(true);
        }));

        await received.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

        await Assert.That(receivedPorts.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(receivedPorts[0]).Contains(Port1Name).Or.Contains(Port2Name);
    }

    /// <summary>Verifies that Dispose closes the port and sets IsDisposed.</summary>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task Dispose_ClosesPortAndSetsIsDisposed(CancellationToken cancellationToken)
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate);
        await port1.Open();

        port1.Dispose();

        await Assert.That(port1.IsDisposed).IsTrue();
        await Assert.That(port1.IsOpen).IsFalse();
    }

    /// <summary>Verifies that Dispose can be called multiple times.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Dispose_CanBeCalledMultipleTimes()
    {
        using var port1 = new SerialPortRx(Port1Name, DefaultBaudRate);

        await Assert.That(() =>
        {
            port1.Dispose();
            port1.Dispose();
            port1.Dispose();
        }).ThrowsNothing();
    }

    /// <summary>Opens a pair of ports and clears any stale data before a test action writes new data.</summary>
    /// <param name="port1">The first port.</param>
    /// <param name="port2">The second port.</param>
    /// <param name="cancellationToken">The TUnit timeout cancellation token.</param>
    /// <returns>A task representing the asynchronous open operation.</returns>
    private static async Task OpenAndClearAsync(SerialPortRx port1, SerialPortRx port2, CancellationToken cancellationToken)
    {
        await port1.Open();
        await port2.Open();
        DiscardBuffers(port1);
        DiscardBuffers(port2);
        await Task.Delay(50, cancellationToken);
    }

    /// <summary>Attempts to discard input and output buffers for an open test port.</summary>
    /// <param name="port">The port to clean.</param>
    private static void DiscardBuffers(SerialPortRx port)
    {
        if (!port.IsOpen)
        {
            return;
        }

        try
        {
            port.DiscardInBuffer();
            port.DiscardOutBuffer();
        }
        catch (InvalidOperationException)
        {
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Discards serial-port buffers and closes ports when a test scope exits.</summary>
    private sealed class SerialPortCleanup : IDisposable
    {
        /// <summary>The ports to clean when the scope exits.</summary>
        private readonly SerialPortRx[] _ports;

        /// <summary>Initializes a new instance of the <see cref="SerialPortCleanup"/> class.</summary>
        /// <param name="ports">The ports to clean when the scope exits.</param>
        public SerialPortCleanup(params SerialPortRx[] ports) => _ports = ports;

        /// <summary>Discards pending data and closes each registered port.</summary>
        public void Dispose()
        {
            foreach (var port in _ports)
            {
                DiscardBuffers(port);
                port.Close();
            }
        }
    }
}
