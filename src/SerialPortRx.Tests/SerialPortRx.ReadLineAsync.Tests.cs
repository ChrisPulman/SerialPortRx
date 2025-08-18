// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CP.IO.Ports.Tests;

public class SerialPortRx_ReadLineAsync_Tests
{
    [Fact]
    public async Task ReadLineAsync_Completes_On_Single_Char_NewLine()
    {
        var sut = new SerialPortRx.SerialPortRx("COM1");

        // Arrange: make port appear open and push data into the DataReceived subject via reflection
        var isOpenField = typeof(SerialPortRx.SerialPortRx).GetProperty("IsOpen");
        Assert.NotNull(isOpenField);

        // We cannot truly open hardware here; instead, simulate by invoking the internal _dataReceived
        var dataReceivedField = typeof(SerialPortRx.SerialPortRx).GetField("_dataReceived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(dataReceivedField);
        var subject = (System.Reactive.Subjects.Subject<char>)dataReceivedField!.GetValue(sut)!;

        // Pretend it's open
        var isOpenProp = typeof(SerialPortRx.SerialPortRx).GetProperty("IsOpen");
        var backingField = typeof(SerialPortRx.SerialPortRx).GetField("<IsOpen>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(backingField);
        backingField!.SetValue(sut, true);

        sut.NewLine = "\n";

        var task = sut.ReadLineAsync();

        subject.OnNext('H');
        subject.OnNext('i');
        subject.OnNext('\n');

        var result = await task.ConfigureAwait(false);
        Assert.Equal("Hi", result);
    }

    [Fact]
    public async Task ReadLineAsync_Completes_On_Multi_Char_NewLine()
    {
        var sut = new SerialPortRx.SerialPortRx("COM1");
        var dataReceivedField = typeof(SerialPortRx.SerialPortRx).GetField("_dataReceived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var subject = (System.Reactive.Subjects.Subject<char>)dataReceivedField!.GetValue(sut)!;
        var backingField = typeof(SerialPortRx.SerialPortRx).GetField("<IsOpen>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        backingField!.SetValue(sut, true);

        sut.NewLine = "\r\n";

        var task = sut.ReadLineAsync();

        subject.OnNext('O');
        subject.OnNext('K');
        subject.OnNext('\r');
        subject.OnNext('\n');

        var result = await task.ConfigureAwait(false);
        Assert.Equal("OK", result);
    }

    [Fact]
    public async Task ReadLineAsync_Times_Out_When_No_NewLine()
    {
        var sut = new SerialPortRx.SerialPortRx("COM1") { ReadTimeout = 50 };
        var dataReceivedField = typeof(SerialPortRx.SerialPortRx).GetField("_dataReceived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var subject = (System.Reactive.Subjects.Subject<char>)dataReceivedField!.GetValue(sut)!;
        var backingField = typeof(SerialPortRx.SerialPortRx).GetField("<IsOpen>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        backingField!.SetValue(sut, true);

        sut.NewLine = "\n";

        var task = sut.ReadLineAsync();

        subject.OnNext('A');
        // No newline provided -> expect timeout

        var ex = await Assert.ThrowsAsync<TimeoutException>(async () => await task.ConfigureAwait(false));
        Assert.Equal("ReadLineAsync timed out.", ex.Message);
    }
}
