# SerialPortRx
A Reactive Serial, TCP, and UDP I/O library that exposes incoming data as IObservable streams and accepts writes via simple methods. Ideal for event-driven, message-framed, and polling scenarios.

[![SerialPortRx CI-Build](https://github.com/ChrisPulman/SerialPortRx/actions/workflows/dotnet.yml/badge.svg)](https://github.com/ChrisPulman/SerialPortRx/actions/workflows/dotnet.yml)
![Nuget](https://img.shields.io/nuget/dt/SerialPortRx)
[![NuGet Stats](https://img.shields.io/nuget/v/SerialPortRx.svg)](https://www.nuget.org/packages/SerialPortRx)

## Features
- SerialPortRx: Reactive wrapper for System.IO.Ports.SerialPort
- UdpClientRx and TcpClientRx: Reactive wrappers exposing a common IPortRx interface
- Observables:
  - DataReceived: IObservable<char> for serial text flow
  - BytesReceived: IObservable<int> for byte stream emitted when using ReadAsync
  - IsOpenObservable: IObservable<bool> for connection state
  - ErrorReceived: IObservable<Exception> for errors
- Helpers:
  - PortNames(): reactive port enumeration with change notifications
  - BufferUntil(): message framing between start and end delimiters with timeout
  - WhileIsOpen(): periodic observable that fires only while a port is open
- Cross-targeted: netstandard2.0, net8.0, net9.0, and Windows-specific TFMs

## Installation
- dotnet add package SerialPortRx

## Supported target frameworks
- netstandard2.0
- net8.0, net9.0
- net8.0-windows10.0.19041.0, net9.0-windows10.0.19041.0 (adds Windows-only APIs guarded by HasWindows)

## Quick start (Serial)
```csharp
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using CP.IO.Ports;
using ReactiveMarbles.Extensions;

var disposables = new CompositeDisposable();
var port = new SerialPortRx("COM3", 115200) { ReadTimeout = -1, WriteTimeout = -1 };

// Observe line/state/errors
port.IsOpenObservable.Subscribe(isOpen => Console.WriteLine($"Open: {isOpen}")).DisposeWith(disposables);
port.ErrorReceived.Subscribe(ex => Console.WriteLine($"Error: {ex.Message}")).DisposeWith(disposables);

// Raw character stream
port.DataReceived.Subscribe(ch => Console.Write(ch)).DisposeWith(disposables);

await port.Open();
port.WriteLine("AT");

// Close when done
port.Close();
disposables.Dispose();
```

## Discovering serial ports
```csharp
// Emits the list of available port names whenever it changes
SerialPortRx.PortNames(pollInterval: 500)
    .Subscribe(names => Console.WriteLine(string.Join(", ", names)));
```

To auto-connect when a specific COM port appears:
```csharp
var target = "COM3";
var comDisposables = new CompositeDisposable();

SerialPortRx.PortNames()
    .Do(names =>
    {
        if (comDisposables.Count == 0 && Array.Exists(names, n => string.Equals(n, target, StringComparison.OrdinalIgnoreCase)))
        {
            var port = new SerialPortRx(target, 115200);
            port.DisposeWith(comDisposables);

            port.ErrorReceived.Subscribe(Console.WriteLine).DisposeWith(comDisposables);
            port.IsOpenObservable.Subscribe(open => Console.WriteLine($"{target}: {(open ? "Open" : "Closed")}"))
                .DisposeWith(comDisposables);

            port.Open();
        }
        else if (!Array.Exists(names, n => string.Equals(n, target, StringComparison.OrdinalIgnoreCase)))
        {
            comDisposables.Dispose(); // auto-cleanup if device removed
        }
    })
    .ForEach()
    .Subscribe();
```

## Message framing with BufferUntil
BufferUntil helps extract framed messages from the character stream between a start and end delimiter within a timeout.

```csharp
// Example: messages start with '!' and end with '\n' and must complete within 100ms
var start = 0x21.AsObservable();  // '!'
var end   = 0x0a.AsObservable();  // '\n'

port.DataReceived
    .BufferUntil(start, end, timeOut: 100)
    .Subscribe(msg => Console.WriteLine($"MSG: {msg}"));
```

A variant returns a default message on timeout:
```csharp
port.DataReceived
    .BufferUntil(start, end, defaultValue: Observable.Return("<timeout>"), timeOut: 100)
    .Subscribe(msg => Console.WriteLine($"MSG: {msg}"));
```

## Periodic work while the port is open
```csharp
// Write a heartbeat every 500ms but only while the port remains open
port.WhileIsOpen(TimeSpan.FromMilliseconds(500))
    .Subscribe(_ => port.Write("PING\n"));
```

## Reading raw bytes with ReadAsync
Use ReadAsync for binary protocols or fixed-length reads. Each byte successfully read is also pushed to BytesReceived.

```csharp
var buffer = new byte[64];
int read = await port.ReadAsync(buffer, 0, buffer.Length);
Console.WriteLine($"Read {read} bytes");

port.BytesReceived.Subscribe(b => Console.WriteLine($"Byte: {b:X2}"));
```

Notes:
- DataReceived is a char stream produced from SerialPort.ReadExisting().
- BytesReceived emits bytes read by your ReadAsync calls (not from ReadExisting()).
- Concurrent ReadAsync calls are serialized internally for safety.

## Writing
- port.Write(string text)
- port.WriteLine(string text)
- port.Write(byte[] buffer)
- port.Write(byte[] buffer, int offset, int count)
- port.Write(char[] buffer)
- port.Write(char[] buffer, int offset, int count)

## Error handling and state
- Subscribe to port.ErrorReceived for exceptions and serial errors.
- Subscribe to port.IsOpenObservable to react to open/close transitions.
- Call port.Close() or dispose subscriptions (DisposeWith) to release the port.

## TCP/UDP variants
The TcpClientRx and UdpClientRx classes implement the same IPortRx interface for a similar reactive experience with sockets.

TCP example:
```csharp
var tcp = new TcpClientRx("example.com", 80);
await tcp.Open();
var req = System.Text.Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n");
tcp.Write(req, 0, req.Length);
var buf = new byte[1024];
var n = await tcp.ReadAsync(buf, 0, buf.Length);
Console.WriteLine(System.Text.Encoding.ASCII.GetString(buf, 0, n));
```

UDP example:
```csharp
var udp = new UdpClientRx(12345);
await udp.Open();
var buf = new byte[16];
var n = await udp.ReadAsync(buf, 0, buf.Length);
Console.WriteLine($"UDP read {n} bytes");
```

## Threading and scheduling
- The DataReceived and other streams run on the underlying event threads. Use ObserveOn to marshal to a UI or a dedicated scheduler when needed.
- ReadAsync uses a lightweight lock and offloads blocking reads, avoiding CPU spin.

## Tips and best practices
- Subscribe before calling Open() to ensure you don’t miss events.
- Tune Encoding (default ASCII), BaudRate, Parity, StopBits, and Handshake to match your device.
- Use BufferUntil for delimited protocols. For binary protocols, use ReadAsync with fixed sizes.
- Always dispose subscriptions (DisposeWith) and call Close() when done.

## Example program (complete)
```csharp
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using CP.IO.Ports;
using ReactiveMarbles.Extensions;

internal static class Program
{
    private static async System.Threading.Tasks.Task Main()
    {
        const string comPortName = "COM1";
        const string dataToWrite = "DataToWrite";
        var dis = new CompositeDisposable();

        var startChar = 0x21.AsObservable(); // '!'
        var endChar = 0x0a.AsObservable();   // '\n'

        var comdis = new CompositeDisposable();

        SerialPortRx.PortNames().Do(names =>
        {
            if (comdis.Count == 0 && names.Contains(comPortName))
            {
                var port = new SerialPortRx(comPortName, 9600);
                port.DisposeWith(comdis);

                port.ErrorReceived.Subscribe(Console.WriteLine).DisposeWith(comdis);
                port.IsOpenObservable.Subscribe(open => Console.WriteLine($"{comPortName} {(open ? "Open" : "Closed")}"))
                    .DisposeWith(comdis);

                port.DataReceived
                    .BufferUntil(startChar, endChar, 100)
                    .Subscribe(data => Console.WriteLine($"Data: {data}"))
                    .DisposeWith(comdis);

                port.WhileIsOpen(TimeSpan.FromMilliseconds(500))
                    .Subscribe(_ => port.Write(dataToWrite))
                    .DisposeWith(comdis);

                port.Open().Wait();
            }
            else if (!names.Contains(comPortName))
            {
                comdis.Dispose();
                Console.WriteLine($"Port {comPortName} Disposed");
            }
        }).ForEach().Subscribe(Console.WriteLine).DisposeWith(dis);

        Console.ReadLine();
        comdis.Dispose();
        dis.Dispose();
    }
}
```

## License
MIT. See LICENSE.
