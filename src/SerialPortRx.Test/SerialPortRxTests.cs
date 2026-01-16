// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace CP.IO.Ports.Tests;

/// <summary>
/// Unit tests for SerialPortRx.
/// These tests require virtual COM port pairs (COM1-COM2) to be set up.
/// Use a tool like com0com or Virtual Serial Port Driver to create virtual port pairs.
/// </summary>
[TestFixture]
[Category("Integration")]
[NonParallelizable]
public class SerialPortRxTests
{
    private const string Port1Name = "COM1";
    private const string Port2Name = "COM2";
    private const int DefaultBaudRate = 9600;

    private SerialPortRx? _port1;
    private SerialPortRx? _port2;
    private CompositeDisposable? _disposables;

    /// <summary>
    /// Check if test ports are available before running tests.
    /// </summary>
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var availablePorts = SerialPort.GetPortNames();
        if (!Array.Exists(availablePorts, p => p.Equals(Port1Name, StringComparison.OrdinalIgnoreCase)) ||
            !Array.Exists(availablePorts, p => p.Equals(Port2Name, StringComparison.OrdinalIgnoreCase)))
        {
            Assert.Ignore($"Test requires virtual COM port pair ({Port1Name} and {Port2Name}). " +
                         "Use com0com or Virtual Serial Port Driver to create virtual ports.");
        }
    }

    /// <summary>
    /// Initializes resources required for each test.
    /// </summary>
    [SetUp]
    public void SetUp() => _disposables = new CompositeDisposable();

    /// <summary>
    /// Performs cleanup operations after each test.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        _port1?.Close();
        _port2?.Close();
        _port1?.Dispose();
        _port2?.Dispose();
        _disposables?.Dispose();
    }

    /// <summary>
    /// Verifies that calling Open on a SerialPortRx instance with a valid port sets the IsOpen property to true.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task Open_WithValidPort_SetsIsOpenToTrue()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate);

        // Act
        await _port1.Open();

        // Assert
        Assert.That(_port1.IsOpen, Is.True);
    }

    /// <summary>
    /// Verifies that calling Close after opening the serial port sets the IsOpen property to false.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task Close_AfterOpen_SetsIsOpenToFalse()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate);
        await _port1.Open();

        // Act
        _port1.Close();

        // Assert
        Assert.That(_port1.IsOpen, Is.False);
    }

    /// <summary>
    /// Verifies that the IsOpenObservable property emits the correct sequence of values.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task IsOpenObservable_EmitsCorrectValues()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate);
        var values = new List<bool>();
        _port1.IsOpenObservable.Subscribe(v => values.Add(v)).DisposeWith(_disposables!);

        // Act
        await _port1.Open();
        await Task.Delay(100);
        _port1.Close();
        await Task.Delay(100);

        // Assert
        Assert.That(values, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(values, Does.Contain(true));
        Assert.That(values[^1], Is.False);
    }

    /// <summary>
    /// Verifies that the DataReceived event emits the correct sequence of received characters.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task DataReceived_WhenDataWritten_EmitsReceivedCharacters()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = true };
        _port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = true };

        await _port1.Open();
        await _port2.Open();

        var receivedChars = new List<char>();
        var tcs = new TaskCompletionSource<bool>();

        _port2.DataReceived.Subscribe(ch =>
        {
            receivedChars.Add(ch);
            if (receivedChars.Count >= 5)
            {
                tcs.TrySetResult(true);
            }
        }).DisposeWith(_disposables!);

        // Act
        await Task.Delay(100); // Allow subscriptions to settle
        _port1.Write("Hello");
        await Task.WhenAny(tcs.Task, Task.Delay(2000));

        // Assert
        Assert.That(receivedChars.Count, Is.GreaterThanOrEqualTo(5));
        Assert.That(string.Join(string.Empty, receivedChars), Does.StartWith("Hello"));
    }

    /// <summary>
    /// Verifies that when a line is written, the Lines observable emits the complete line.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task Lines_WhenLineWritten_EmitsCompleteLine()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { NewLine = "\r\n", EnableAutoDataReceive = true };
        _port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { NewLine = "\r\n", EnableAutoDataReceive = true };

        await _port1.Open();
        await _port2.Open();

        string? receivedLine = null;
        var tcs = new TaskCompletionSource<string>();

        _port2.Lines.Take(1).Subscribe(line =>
        {
            receivedLine = line;
            tcs.TrySetResult(line);
        }).DisposeWith(_disposables!);

        // Act
        await Task.Delay(100); // Allow subscriptions to settle
        _port1.WriteLine("Test Message");
        await Task.WhenAny(tcs.Task, Task.Delay(2000));

        // Assert
        Assert.That(receivedLine, Is.EqualTo("Test Message"));
    }

    /// <summary>
    /// Verifies that the DataReceivedBytes observable emits the correct sequence of bytes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task DataReceivedBytes_WhenBytesWritten_EmitsReceivedBytes()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = true };
        _port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = true };

        await _port1.Open();
        await _port2.Open();

        var receivedBytes = new List<byte>();
        var tcs = new TaskCompletionSource<bool>();

        _port2.DataReceivedBytes.Subscribe(b =>
        {
            receivedBytes.Add(b);
            if (receivedBytes.Count >= 3)
            {
                tcs.TrySetResult(true);
            }
        }).DisposeWith(_disposables!);

        // Act
        await Task.Delay(100); // Allow subscriptions to settle
        _port1.Write(new byte[] { 0x01, 0x02, 0x03 }, 0, 3);
        await Task.WhenAny(tcs.Task, Task.Delay(2000));

        // Assert
        Assert.That(receivedBytes.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(receivedBytes[0], Is.EqualTo(0x01));
        Assert.That(receivedBytes[1], Is.EqualTo(0x02));
        Assert.That(receivedBytes[2], Is.EqualTo(0x03));
    }

    /// <summary>
    /// Verifies that the ReadLine method returns the expected line when data is available.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task ReadLine_WhenDataAvailable_ReturnsLine()
    {
        // Arrange - disable auto receive to use sync reads
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { NewLine = "\r\n", EnableAutoDataReceive = false };
        _port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { NewLine = "\r\n", EnableAutoDataReceive = false, ReadTimeout = 2000 };

        await _port1.Open();
        await _port2.Open();

        // Act
        _port1.WriteLine("Sync Test");
        await Task.Delay(200);

        var line = _port2.ReadLine();

        // Assert
        Assert.That(line, Is.EqualTo("Sync Test"));
    }

    /// <summary>
    /// Verifies that the ReadExisting method returns all available data.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task ReadExisting_WhenDataAvailable_ReturnsAllData()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = false };
        _port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = false };

        await _port1.Open();
        await _port2.Open();

        // Act
        _port1.Write("Test Data");
        await Task.Delay(200);

        var data = _port2.ReadExisting();

        // Assert
        Assert.That(data, Is.EqualTo("Test Data"));
    }

    /// <summary>
    /// Verifies that reading a byte array returns the correct bytes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task Read_ByteArray_ReturnsCorrectBytes()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = false, ReadTimeout = 2000 };
        _port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = false, ReadTimeout = 2000 };

        await _port1.Open();
        await _port2.Open();

        // Act
        _port1.Write(new byte[] { 65, 66, 67 }, 0, 3);
        await Task.Delay(200);

        var buffer = new byte[10];
        var bytesRead = _port2.Read(buffer, 0, 3);

        // Assert
        Assert.That(bytesRead, Is.EqualTo(3));
        Assert.That(buffer[0], Is.EqualTo(65));
        Assert.That(buffer[1], Is.EqualTo(66));
        Assert.That(buffer[2], Is.EqualTo(67));
    }

    /// <summary>
    /// Verifies that ReadLineAsync returns the expected line when data is available.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task ReadLineAsync_WhenDataAvailable_ReturnsLine()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { NewLine = "\r\n", EnableAutoDataReceive = true };
        _port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { NewLine = "\r\n", EnableAutoDataReceive = true, ReadTimeout = 3000 };

        await _port1.Open();
        await _port2.Open();

        // Ensure clean buffer state
        _port1.DiscardInBuffer();
        _port1.DiscardOutBuffer();
        _port2.DiscardInBuffer();
        _port2.DiscardOutBuffer();
        await Task.Delay(200); // Allow buffers to clear

        // Act - start the read task first, then send data
        var testMessage = "AsyncLineTestMessage";
        var readTask = _port2.ReadLineAsync();

        // Small delay to ensure subscription is active
        await Task.Delay(50);
        _port1.WriteLine(testMessage);

        var line = await readTask;

        // Assert
        Assert.That(line, Is.EqualTo(testMessage));
    }

    /// <summary>
    /// Verifies that ReadLineAsync throws when canceled.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task ReadLineAsync_WithCancellation_ThrowsOperationCanceled()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = true };
        _port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = true };

        await _port1.Open();
        await _port2.Open();
        await Task.Delay(100); // Allow ports to settle

        using var cts = new CancellationTokenSource(500);

        // Act & Assert - TaskCanceledException inherits from OperationCanceledException
        Exception? caughtException = null;
        try
        {
            await _port2.ReadLineAsync(cts.Token);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        Assert.That(caughtException, Is.InstanceOf<OperationCanceledException>());
    }

    /// <summary>
    /// Verifies that ReadToAsync returns data up to the specified delimiter.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task ReadToAsync_WhenDelimiterFound_ReturnsDataUpToDelimiter()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = true };
        _port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = true, ReadTimeout = 3000 };

        await _port1.Open();
        await _port2.Open();

        // Act
        await Task.Delay(100); // Allow subscriptions to settle
        _port1.Write("Hello>World");

        var result = await _port2.ReadToAsync(">");

        // Assert
        Assert.That(result, Is.EqualTo("Hello"));
    }

    /// <summary>
    /// Verifies that writing a string sends data to the other port.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task Write_String_DataReceivedOnOtherPort()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = false };
        _port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = false };

        await _port1.Open();
        await _port2.Open();

        // Act
        _port1.Write("Test String");
        await Task.Delay(200);

        // Assert
        var received = _port2.ReadExisting();
        Assert.That(received, Is.EqualTo("Test String"));
    }

    /// <summary>
    /// Verifies that WriteLine appends the newline character.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task WriteLine_AddsNewLine()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { NewLine = "\n", EnableAutoDataReceive = false };
        _port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { NewLine = "\n", EnableAutoDataReceive = false };

        await _port1.Open();
        await _port2.Open();

        // Act
        _port1.WriteLine("Test");
        await Task.Delay(200);

        // Assert
        var received = _port2.ReadExisting();
        Assert.That(received, Is.EqualTo("Test\n"));
    }

    /// <summary>
    /// Verifies that writing a byte array sends data correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task Write_ByteArray_DataReceivedCorrectly()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = false };
        _port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = false, ReadTimeout = 2000 };

        await _port1.Open();
        await _port2.Open();

        // Ensure clean buffer state
        _port1.DiscardInBuffer();
        _port1.DiscardOutBuffer();
        _port2.DiscardInBuffer();
        _port2.DiscardOutBuffer();
        await Task.Delay(50);

        var dataToSend = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

        // Act
        _port1.Write(dataToSend, 0, 4);
        await Task.Delay(200);

        var buffer = new byte[10];
        var bytesRead = _port2.Read(buffer, 0, 4);

        // Assert
        Assert.That(bytesRead, Is.EqualTo(4));
        Assert.That(buffer[0], Is.EqualTo(0xAA));
        Assert.That(buffer[1], Is.EqualTo(0xBB));
        Assert.That(buffer[2], Is.EqualTo(0xCC));
        Assert.That(buffer[3], Is.EqualTo(0xDD));
    }

    /// <summary>
    /// Verifies that writing a character array sends data correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task Write_CharArray_DataReceivedCorrectly()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate) { EnableAutoDataReceive = false };
        _port2 = new SerialPortRx(Port2Name, DefaultBaudRate) { EnableAutoDataReceive = false };

        await _port1.Open();
        await _port2.Open();

        var chars = new char[] { 'A', 'B', 'C' };

        // Act
        _port1.Write(chars);
        await Task.Delay(200);

        // Assert
        var received = _port2.ReadExisting();
        Assert.That(received, Is.EqualTo("ABC"));
    }

    /// <summary>
    /// Verifies default property values.
    /// </summary>
    [Test]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var port = new SerialPortRx();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(port.BaudRate, Is.EqualTo(9600));
            Assert.That(port.DataBits, Is.EqualTo(8));
            Assert.That(port.Parity, Is.EqualTo(Parity.None));
            Assert.That(port.StopBits, Is.EqualTo(StopBits.One));
            Assert.That(port.Handshake, Is.EqualTo(Handshake.None));
            Assert.That(port.NewLine, Is.EqualTo("\n"));
            Assert.That(port.ReadTimeout, Is.EqualTo(-1));
            Assert.That(port.WriteTimeout, Is.EqualTo(-1));
            Assert.That(port.EnableAutoDataReceive, Is.True);
        });

        port.Dispose();
    }

    /// <summary>
    /// Verifies constructor with all parameters.
    /// </summary>
    [Test]
    public void Constructor_WithAllParameters_SetsValuesCorrectly()
    {
        // Arrange & Act
        var port = new SerialPortRx("COM3", 115200, 7, Parity.Even, StopBits.Two, Handshake.RequestToSend);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(port.PortName, Is.EqualTo("COM3"));
            Assert.That(port.BaudRate, Is.EqualTo(115200));
            Assert.That(port.DataBits, Is.EqualTo(7));
            Assert.That(port.Parity, Is.EqualTo(Parity.Even));
            Assert.That(port.StopBits, Is.EqualTo(StopBits.Two));
            Assert.That(port.Handshake, Is.EqualTo(Handshake.RequestToSend));
        });

        port.Dispose();
    }

    /// <summary>
    /// Verifies that opening a non-existent port throws an exception.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task Open_WithNonExistentPort_ThrowsException()
    {
        // Arrange
        _port1 = new SerialPortRx("COMNONEXISTENT", DefaultBaudRate);

        // Act & Assert - Open throws for non-existent port
        Exception? caughtException = null;
        try
        {
            await _port1.Open();
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        Assert.That(caughtException, Is.Not.Null);
        Assert.That(_port1.IsOpen, Is.False);
    }

    /// <summary>
    /// Verifies that ReadLine throws when port is not open.
    /// </summary>
    [Test]
    public void ReadLine_WhenPortNotOpen_ThrowsInvalidOperationException()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _port1.ReadLine());
    }

    /// <summary>
    /// Verifies that ReadExisting throws when port is not open.
    /// </summary>
    [Test]
    public void ReadExisting_WhenPortNotOpen_ThrowsInvalidOperationException()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _port1.ReadExisting());
    }

    /// <summary>
    /// Verifies that PortNames returns available ports.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task PortNames_ReturnsAvailablePorts()
    {
        // Arrange
        var receivedPorts = new List<string[]>();
        var tcs = new TaskCompletionSource<bool>();

        // Act
        SerialPortRx.PortNames(100, 1).Subscribe(ports =>
        {
            receivedPorts.Add(ports);
            tcs.TrySetResult(true);
        }).DisposeWith(_disposables!);

        await Task.WhenAny(tcs.Task, Task.Delay(2000));

        // Assert
        Assert.That(receivedPorts, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(receivedPorts[0], Does.Contain(Port1Name).Or.Contain(Port2Name));
    }

    /// <summary>
    /// Verifies that Dispose closes the port and sets IsDisposed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    [CancelAfter(5000)]
    public async Task Dispose_ClosesPortAndSetsIsDisposed()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate);
        await _port1.Open();

        // Act
        _port1.Dispose();

        // Assert
        Assert.That(_port1.IsDisposed, Is.True);
        Assert.That(_port1.IsOpen, Is.False);
    }

    /// <summary>
    /// Verifies that Dispose can be called multiple times.
    /// </summary>
    [Test]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        _port1 = new SerialPortRx(Port1Name, DefaultBaudRate);

        // Act & Assert - should not throw
        Assert.DoesNotThrow(() =>
        {
            _port1.Dispose();
            _port1.Dispose();
            _port1.Dispose();
        });
    }
}
