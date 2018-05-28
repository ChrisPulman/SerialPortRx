# SerialPortRx
A Reactive Serial Port Library
 This serial port is configured to provide a stream of data Read and accept a stream of Write requests

[![Build status](https://ci.appveyor.com/api/projects/status/mypr79isqnt5x8y8?svg=true)](https://ci.appveyor.com/project/ChrisPulman/serialportrx) [![Travis](https://img.shields.io/badge/SerialPortRx-V1.0.0-blue.svg)](https://www.nuget.org/packages/SerialPortRx/)

## An Example of the usage of SerialPortRx
```csharp
    using System;
    using System.Reactive.Linq;
    using System.Reactive.Disposables;
    using CP.IO.Ports;

    internal class Program
    {
        private static void Main(string[] args)
        {
            // configure the data to write, this can be a string, a byte array, or a char array
            var dataToWrite = "DataToWrite";

            using (var port = new SerialPortRx("COM1", 9600))
            {
                var dis = new CompositeDisposable();
                // Subscribe to com ports available
                SerialPortRx.PortNames.ForEach().Subscribe(name =>
                {
                    Console.WriteLine(name);
                }).AddTo(dis);
                // Setup the start of message and end of message
                var startChar = (0x21).AsObservable();
                var endChar = (0x0a).AsObservable();
                // Subscribe to the Data Received
                port.DataReceived.BufferUntil(startChar, endChar, 100).Subscribe(data =>
                {
                    Console.WriteLine(data);
                }).AddTo(dis);
                // Subscribe to Exceptions
                port.ErrorReceived.Subscribe(Console.WriteLine).AddTo(dis);
                // Open the Com Port
                port.Open();
                // Subscribe to the Is Open @500ms intervals and write to com port
                port.WhileIsOpen(TimeSpan.FromMilliseconds(500)).Subscribe(x =>
                {
                    port.Write(dataToWrite);
                }).AddTo(dis);
                Console.ReadLine();
                // Cleanup port
                dis.Dispose();
            }
        }
    }
```