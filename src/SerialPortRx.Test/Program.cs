// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI.Extensions;

namespace CP.IO.Ports.Test;

internal static class Program
{
    private static void Main(string[] args)
    {
        const string comPortName = "COM1";

        // configure the data to write, this can be a string, a byte array, or a char array
        const string dataToWrite = "DataToWrite";
        var dis = new CompositeDisposable();

        // Setup the start of message and end of message
        var startChar = 0x21.AsObservable();
        var endChar = 0x0a.AsObservable();

        // Create a disposable for each COM port to allow automatic disposal upon loss of COM port
        var comdis = new CompositeDisposable();

        // Subscribe to com ports available
        SerialPortRx.PortNames().Do(x =>
        {
            if (comdis?.Count == 0 && x.Contains(comPortName))
            {
                // Create a port
                var port = new SerialPortRx(comPortName, 9600);
                port.DisposeWith(comdis);

                // Subscribe to Exceptions from port
                port.ErrorReceived.Subscribe(Console.WriteLine).DisposeWith(comdis);
                port.IsOpenObservable.Subscribe(x => Console.WriteLine($"Port {comPortName} is {(x ? "Open" : "Closed")}")).DisposeWith(comdis);

                // Subscribe to the Data Received
                port.DataReceived.BufferUntil(startChar, endChar, 100).Subscribe(data => Console.WriteLine(data)).DisposeWith(comdis);

                // Subscribe to the Is Open @500ms intervals and write to com port
                port.WhileIsOpen(TimeSpan.FromMilliseconds(500)).Subscribe(_ => port.Write(dataToWrite)).DisposeWith(comdis);

                // Open the Com Port after subscriptions created
                port.Open();
            }
            else
            {
                comdis?.Dispose();
                Console.WriteLine($"Port {comPortName} Disposed");
                comdis = [];
            }
        }).ForEach().Subscribe(name =>
        {
            // Show available ports
            Console.WriteLine(name);
        }).DisposeWith(dis);
        Console.ReadLine();

        // Cleanup ports
        comdis.Dispose();
        dis.Dispose();
    }
}
