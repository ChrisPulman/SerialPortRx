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
public sealed class SerialPortRxTests : IDisposable
{
    /// <summary>The first virtual serial port name.</summary>
    private const string Port1Name = "COM1";

    /// <summary>The second virtual serial port name.</summary>
    private const string Port2Name = "COM2";

    /// <summary>The default baud rate used by integration tests.</summary>
    private const int DefaultBaudRate = 9600;

    /// <summary>The first test port.</summary>
    private SerialPortRx? _port1;

    /// <summary>The second test port.</summary>
    private SerialPortRx? _port2;

    /// <summary>Subscriptions owned by the current test.</summary>
    private CompositeDisposable? _disposables;

    /// <summary>Indicates whether this test instance has been disposed.</summary>
    private bool _disposed;

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

    /// <summary>Initializes resources required for each test.</summary>
    [Before(Test)]
    public void BeforeTest() => _disposables = new();

    /// <summary>Performs cleanup operations after each test.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous cleanup operation.</returns>
    [After(Test)]
    public async Task AfterTest()
    {
        await Task.Delay(100);
        DiscardBuffers(_port1);
        DiscardBuffers(_port2);
        _port1?.Close();
        _port2?.Close();
        Dispose();
    }

    /// <summary>Releases resources used by the test instance.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DiscardBuffers(_port1);
        DiscardBuffers(_port2);
        _port1?.Close();
        _port2?.Close();
        _port1?.Dispose();
        _port2?.Dispose();
        _disposables?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>Verifies that calling Open on a SerialPortRx instance with a valid port sets the IsOpen property to true.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task Open_WithValidPort_SetsIsOpenToTrue()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate);

        // Act
        await _port1.Open();

        // Assert
        await Assert.That(_port1.IsOpen).IsTrue();
    }

    /// <summary>Verifies that calling Close after opening the serial port sets the IsOpen property to false.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task Close_AfterOpen_SetsIsOpenToFalse()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate);
        await _port1.Open();

        // Act
        _port1.Close();

        // Assert
        await Assert.That(_port1.IsOpen).IsFalse();
    }

    /// <summary>Verifies that the IsOpenObservable property emits the correct sequence of values.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task IsOpenObservable_EmitsCorrectValues()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate);
        var values = new List<bool>();
        _disposables!.Add(_port1.IsOpenObservable.Subscribe(v => values.Add(v)));

        // Act
        await _port1.Open();
        await Task.Delay(100);
        _port1.Close();
        await Task.Delay(100);

        // Assert
        await Assert.That(values.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(values).Contains(true);
        await Assert.That(values[^1]).IsFalse();
    }

    /// <summary>Verifies that the DataReceived event emits the correct sequence of received characters.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task DataReceived_WhenDataWritten_EmitsReceivedCharacters()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = true };
        _port2 = new(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = true };

        await _port1.Open();
        await _port2.Open();

        var receivedChars = new List<char>();
        var tcs = new TaskCompletionSource<bool>();

        _disposables!.Add(_port2.DataReceived.Subscribe(ch =>
        {
            receivedChars.Add(ch);
            if (receivedChars.Count < 5)
            {
                return;
            }

            _ = tcs.TrySetResult(true);
        }));

        // Act
        await Task.Delay(100); // Allow subscriptions to settle
        _port1.Write("Hello");
        await Task.WhenAny(tcs.Task, Task.Delay(2000));

        // Assert
        await Assert.That(receivedChars.Count).IsGreaterThanOrEqualTo(5);
        await Assert.That(string.Join(string.Empty, receivedChars)).StartsWith("Hello");
    }

    /// <summary>Verifies that when a line is written, the Lines observable emits the complete line.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task Lines_WhenLineWritten_EmitsCompleteLine()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate) { NewLine = "\r\n", EnableAutoDataReceive = true };
        _port2 = new(Port2Name, DefaultBaudRate) { NewLine = "\r\n", EnableAutoDataReceive = true };

        await _port1.Open();
        await _port2.Open();

        string? receivedLine = null;
        var tcs = new TaskCompletionSource<string>();

        _disposables!.Add(_port2.Lines.Take(1).Subscribe(line =>
        {
            receivedLine = line;
            _ = tcs.TrySetResult(line);
        }));

        // Act
        await Task.Delay(100); // Allow subscriptions to settle
        _port1.WriteLine("Test Message");
        await Task.WhenAny(tcs.Task, Task.Delay(2000));

        // Assert
        await Assert.That(receivedLine).IsEqualTo("Test Message");
    }

    /// <summary>Verifies that the DataReceivedBytes observable emits the correct sequence of bytes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task DataReceivedBytes_WhenBytesWritten_EmitsReceivedBytes()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = true };
        _port2 = new(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = true };

        await _port1.Open();
        await _port2.Open();

        var receivedBytes = new List<byte>();
        var tcs = new TaskCompletionSource<bool>();

        _disposables!.Add(_port2.DataReceivedBytes.Subscribe(b =>
        {
            receivedBytes.Add(b);
            if (receivedBytes.Count < 3)
            {
                return;
            }

            _ = tcs.TrySetResult(true);
        }));

        // Act
        await Task.Delay(100); // Allow subscriptions to settle
        _port1.Write([0x01, 0x02, 0x03], 0, 3);
        await Task.WhenAny(tcs.Task, Task.Delay(2000));

        // Assert
        await Assert.That(receivedBytes.Count).IsGreaterThanOrEqualTo(3);
        await Assert.That(receivedBytes[0]).IsEqualTo((byte)0x01);
        await Assert.That(receivedBytes[1]).IsEqualTo((byte)0x02);
        await Assert.That(receivedBytes[2]).IsEqualTo((byte)0x03);
    }

    /// <summary>Verifies that the ReadLine method returns the expected line when data is available.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ReadLine_WhenDataAvailable_ReturnsLine()
    {
        // Arrange - disable auto receive to use sync reads
        _port1 = new(Port1Name, DefaultBaudRate) { NewLine = "\r\n", EnableAutoDataReceive = false };
        _port2 = new(Port2Name, DefaultBaudRate) { NewLine = "\r\n", EnableAutoDataReceive = false, ReadTimeout = 2000 };

        await _port1.Open();
        await _port2.Open();

        // Act
        _port1.WriteLine("Sync Test");
        await Task.Delay(200);

        var line = _port2.ReadLine();

        // Assert
        await Assert.That(line).IsEqualTo("Sync Test");
    }

    /// <summary>Verifies that the ReadExisting method returns all available data.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ReadExisting_WhenDataAvailable_ReturnsAllData()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = false };
        _port2 = new(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = false };

        await _port1.Open();
        await _port2.Open();

        // Act
        _port1.Write("Test Data");
        await Task.Delay(200);

        var data = _port2.ReadExisting();

        // Assert
        await Assert.That(data).IsEqualTo("Test Data");
    }

    /// <summary>Verifies that reading a byte array returns the correct bytes.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task Read_ByteArray_ReturnsCorrectBytes()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = false, ReadTimeout = 2000 };
        _port2 = new(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = false, ReadTimeout = 2000 };

        await _port1.Open();
        await _port2.Open();

        // Act
        _port1.Write([65, 66, 67], 0, 3);
        await Task.Delay(200);

        var buffer = new byte[10];
        var bytesRead = _port2.Read(buffer, 0, 3);

        // Assert
        await Assert.That(bytesRead).IsEqualTo(3);
        await Assert.That(buffer[0]).IsEqualTo((byte)65);
        await Assert.That(buffer[1]).IsEqualTo((byte)66);
        await Assert.That(buffer[2]).IsEqualTo((byte)67);
    }

    /// <summary>Verifies that ReadLineAsync returns the expected line when data is available.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ReadLineAsync_WhenDataAvailable_ReturnsLine()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate) { NewLine = "\r\n", EnableAutoDataReceive = true };
        _port2 = new(Port2Name, DefaultBaudRate) { NewLine = "\r\n", EnableAutoDataReceive = true, ReadTimeout = 3000 };

        await _port1.Open();
        await _port2.Open();

        // Ensure clean buffer state
        _port1.DiscardInBuffer();
        _port1.DiscardOutBuffer();
        _port2.DiscardInBuffer();
        _port2.DiscardOutBuffer();
        await Task.Delay(200); // Allow buffers to clear

        // Act - start the read task first, then send data
        const string TestMessage = "AsyncLineTestMessage";
        var readTask = _port2.ReadLineAsync();

        // Small delay to ensure subscription is active
        await Task.Delay(50);
        _port1.WriteLine(TestMessage);

        var line = await readTask;

        // Assert
        await Assert.That(line).IsEqualTo(TestMessage);
    }

    /// <summary>Verifies that ReadLineAsync throws when canceled.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ReadLineAsync_WithCancellation_ThrowsOperationCanceled()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = true };
        _port2 = new(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = true };

        await _port1.Open();
        await _port2.Open();
        await Task.Delay(100); // Allow ports to settle

        using var cts = new CancellationTokenSource(500);

        // Act & Assert - TaskCanceledException inherits from OperationCanceledException.
        async Task Act() => _ = await _port2.ReadLineAsync(cts.Token);

        await Assert.That(Act).Throws<OperationCanceledException>();
    }

    /// <summary>Verifies that ReadToAsync returns data up to the specified delimiter.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task ReadToAsync_WhenDelimiterFound_ReturnsDataUpToDelimiter()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = true };
        _port2 = new(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = true, ReadTimeout = 3000 };

        await _port1.Open();
        await _port2.Open();

        // Act
        await Task.Delay(100); // Allow subscriptions to settle
        _port1.Write("Hello>World");

        var result = await _port2.ReadToAsync(">");

        // Assert
        await Assert.That(result).IsEqualTo("Hello");
    }

    /// <summary>Verifies that writing a string sends data to the other port.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task Write_String_DataReceivedOnOtherPort()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = false };
        _port2 = new(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = false };

        await _port1.Open();
        await _port2.Open();

        // Act
        _port1.Write("Test String");
        await Task.Delay(200);

        // Assert
        var received = _port2.ReadExisting();
        await Assert.That(received).IsEqualTo("Test String");
    }

    /// <summary>Verifies that WriteLine appends the newline character.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task WriteLine_AddsNewLine()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate) { NewLine = "\n", EnableAutoDataReceive = false };
        _port2 = new(Port2Name, DefaultBaudRate) { NewLine = "\n", EnableAutoDataReceive = false };

        await _port1.Open();
        await _port2.Open();

        // Act
        _port1.WriteLine("Test");
        await Task.Delay(200);

        // Assert
        var received = _port2.ReadExisting();
        await Assert.That(received).IsEqualTo("Test\n");
    }

    /// <summary>Verifies that writing a byte array sends data correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task Write_ByteArray_DataReceivedCorrectly()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = false };
        _port2 = new(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = false, ReadTimeout = 2000 };

        await _port1.Open();
        await _port2.Open();

        // Ensure clean buffer state
        _port1.DiscardInBuffer();
        _port1.DiscardOutBuffer();
        _port2.DiscardInBuffer();
        _port2.DiscardOutBuffer();
        await Task.Delay(50);

        byte[] dataToSend = [0xAA, 0xBB, 0xCC, 0xDD];

        // Act
        _port1.Write(dataToSend, 0, 4);
        await Task.Delay(200);

        var buffer = new byte[10];
        var bytesRead = _port2.Read(buffer, 0, 4);

        // Assert
        await Assert.That(bytesRead).IsEqualTo(4);
        await Assert.That(buffer[0]).IsEqualTo((byte)0xAA);
        await Assert.That(buffer[1]).IsEqualTo((byte)0xBB);
        await Assert.That(buffer[2]).IsEqualTo((byte)0xCC);
        await Assert.That(buffer[3]).IsEqualTo((byte)0xDD);
    }

    /// <summary>Verifies that writing a character array sends data correctly.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task Write_CharArray_DataReceivedCorrectly()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = false };
        _port2 = new(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = false };

        await _port1.Open();
        await _port2.Open();

        char[] chars = ['A', 'B', 'C'];

        // Act
        _port1.Write(chars);
        await Task.Delay(200);

        // Assert
        var received = _port2.ReadExisting();
        await Assert.That(received).IsEqualTo("ABC");
    }

    /// <summary>Verifies default property values.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var port = new SerialPortRx();

        // Assert
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

        port.Dispose();
    }

    /// <summary>Verifies constructor with all parameters.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Constructor_WithAllParameters_SetsValuesCorrectly()
    {
        // Arrange & Act
        var port = new SerialPortRx("COM3", 115_200, 7, Parity.Even, StopBits.Two, Handshake.RequestToSend);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(port.PortName).IsEqualTo("COM3");
            await Assert.That(port.BaudRate).IsEqualTo(115_200);
            await Assert.That(port.DataBits).IsEqualTo(7);
            await Assert.That(port.Parity).IsEqualTo(Parity.Even);
            await Assert.That(port.StopBits).IsEqualTo(StopBits.Two);
            await Assert.That(port.Handshake).IsEqualTo(Handshake.RequestToSend);
        }

        port.Dispose();
    }

    /// <summary>Verifies that opening a non-existent port throws an exception.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task Open_WithNonExistentPort_ThrowsException()
    {
        // Arrange
        _port1 = new("COMNONEXISTENT", DefaultBaudRate);

        // Act & Assert - Open throws for non-existent port.
        await Assert.That(async () => await _port1.Open()).Throws<Exception>();
        await Assert.That(_port1.IsOpen).IsFalse();
    }

    /// <summary>Verifies that ReadLine throws when port is not open.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ReadLine_WhenPortNotOpen_ThrowsInvalidOperationException()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate);

        // Act & Assert
        await Assert.That(() => _port1.ReadLine()).Throws<InvalidOperationException>();
    }

    /// <summary>Verifies that ReadExisting throws when port is not open.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ReadExisting_WhenPortNotOpen_ThrowsInvalidOperationException()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate);

        // Act & Assert
        await Assert.That(() => _port1.ReadExisting()).Throws<InvalidOperationException>();
    }

    /// <summary>Verifies that PortNames returns available ports.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task PortNames_ReturnsAvailablePorts()
    {
        // Arrange
        var receivedPorts = new List<string[]>();
        var tcs = new TaskCompletionSource<bool>();

        // Act
        _disposables!.Add(SerialPortRx.PortNames(100, 1).Subscribe(ports =>
        {
            receivedPorts.Add(ports);
            _ = tcs.TrySetResult(true);
        }));

        await Task.WhenAny(tcs.Task, Task.Delay(2000));

        // Assert
        await Assert.That(receivedPorts.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(receivedPorts[0]).Contains(Port1Name).Or.Contains(Port2Name);
    }

    /// <summary>Verifies that Dispose closes the port and sets IsDisposed.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task Dispose_ClosesPortAndSetsIsDisposed()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate);
        await _port1.Open();

        // Act
        _port1.Dispose();

        // Assert
        await Assert.That(_port1.IsDisposed).IsTrue();
        await Assert.That(_port1.IsOpen).IsFalse();
    }

    /// <summary>Verifies that Dispose can be called multiple times.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        _port1 = new(Port1Name, DefaultBaudRate);

        // Act & Assert - should not throw
        await Assert.That(() =>
        {
            _port1.Dispose();
            _port1.Dispose();
            _port1.Dispose();
        }).ThrowsNothing();
    }

    /// <summary>Attempts to discard input and output buffers for an open test port.</summary>
    /// <param name="port">The port to clean.</param>
    private static void DiscardBuffers(SerialPortRx? port)
    {
        if (port?.IsOpen != true)
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
}
