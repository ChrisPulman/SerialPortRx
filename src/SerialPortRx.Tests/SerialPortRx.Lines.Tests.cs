// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CP.IO.Ports.Tests;

public class SerialPortRx_Lines_Tests
{
    [Fact]
    public async Task Lines_Emits_Complete_Lines_SingleChar_Newline()
    {
        var sut = new SerialPortRx.SerialPortRx("COM1");
        var dataReceivedField = typeof(SerialPortRx.SerialPortRx).GetField("_dataReceived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var subject = (System.Reactive.Subjects.Subject<char>)dataReceivedField!.GetValue(sut)!;
        var backingField = typeof(SerialPortRx.SerialPortRx).GetField("<IsOpen>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        backingField!.SetValue(sut, true);
        sut.NewLine = "\n";

        string? line1 = null;
        string? line2 = null;
        using var sub = sut.Lines.Subscribe(l =>
        {
            if (line1 == null) line1 = l; else line2 = l;
        });

        subject.OnNext('a');
        subject.OnNext('\n');
        subject.OnNext('b');
        subject.OnNext('\n');

        await Task.Delay(10);

        Assert.Equal("a", line1);
        Assert.Equal("b", line2);
    }

    [Fact]
    public async Task Lines_Emits_Complete_Lines_MultiChar_Newline()
    {
        var sut = new SerialPortRx.SerialPortRx("COM1");
        var dataReceivedField = typeof(SerialPortRx.SerialPortRx).GetField("_dataReceived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var subject = (System.Reactive.Subjects.Subject<char>)dataReceivedField!.GetValue(sut)!;
        var backingField = typeof(SerialPortRx.SerialPortRx).GetField("<IsOpen>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        backingField!.SetValue(sut, true);
        sut.NewLine = "\r\n";

        string? line = null;
        using var sub = sut.Lines.Subscribe(l => line = l);

        subject.OnNext('x');
        subject.OnNext('\r');
        subject.OnNext('\n');

        await Task.Delay(10);
        Assert.Equal("x", line);
    }
}
