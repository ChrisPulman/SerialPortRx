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
  - DataReceivedBytes: IObservable<byte> for raw byte stream (auto-receive mode)
  - Lines: IObservable<string> of complete lines split by NewLine
  - BytesReceived: IObservable<int> for byte stream emitted when using ReadAsync
  - IsOpenObservable: IObservable<bool> for connection state
  - ErrorReceived: IObservable<Exception> for errors
  - PinChanged: IObservable<SerialPinChangedEventArgs> for pin state changes (Windows only)
- Synchronous read methods for manual data consumption
- TCP/UDP batched reads:
  - TcpClientRx.DataReceivedBatches: IObservable<byte[]> chunks per read loop
  - UdpClientRx.DataReceivedBatches: IObservable<byte[]> per received datagram
- Helpers:
  - PortNames(): reactive port enumeration with change notifications
  - BufferUntil(): message framing between start and end delimiters with timeout
  - WhileIsOpen(): periodic observable that fires only while a port is open
- Cross-targeted: netstandard2.0, net8.0, net9.0, net10.0, and Windows-specific TFMs

## Installation
- dotnet add package SerialPortRx

## Supported target frameworks
- netstandard2.0
- net8.0, net9.0, net10.0
- net8.0-windows10.0.19041.0, net9.0-windows10.0.19041.0, net10.0-windows10.0.19041.0 (adds Windows-only APIs guarded by HasWindows)

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
- DataReceived is a char stream produced from SerialPort.ReadExisting() when EnableAutoDataReceive is true (default).
- DataReceivedBytes emits raw bytes alongside DataReceived in auto-receive mode.
- BytesReceived emits bytes read by your ReadAsync calls (not from ReadExisting()).
- Concurrent ReadAsync calls are serialized internally for safety.

## Automatic vs Manual Data Reception
By default, `EnableAutoDataReceive = true` automatically feeds incoming data to `DataReceived` and `DataReceivedBytes` observables. Set this to `false` before calling `Open()` if you want to use synchronous read methods instead.

```csharp
// Automatic mode (default) - data flows to observables
var port = new SerialPortRx("COM3", 115200);
port.DataReceived.Subscribe(ch => Console.Write(ch));
await port.Open();

// Manual mode - use synchronous reads
var port = new SerialPortRx("COM3", 115200) { EnableAutoDataReceive = false };
await port.Open();
string data = port.ReadExisting();
```

If you disable auto-receive but later want reactive streaming, call `StartDataReception()`:
```csharp
port.EnableAutoDataReceive = false;
await port.Open();

// Later, enable reactive streaming manually
var reception = port.StartDataReception(pollingIntervalMs: 10);
port.DataReceived.Subscribe(ch => Console.Write(ch));

// Stop when done
reception.Dispose();
```

## Synchronous Read Methods
When `EnableAutoDataReceive = false`, use these synchronous methods for manual data consumption:

```csharp
var port = new SerialPortRx("COM3", 115200) { EnableAutoDataReceive = false, ReadTimeout = 1000 };
await port.Open();

// Read all available data as string
string existing = port.ReadExisting();

// Read a single byte (-1 if none available)
int b = port.ReadByte();

// Read a single character (-1 if none available)
int ch = port.ReadChar();

// Read into a byte buffer
var buffer = new byte[64];
int bytesRead = port.Read(buffer, 0, buffer.Length);

// Read into a char buffer
var charBuffer = new char[64];
int charsRead = port.Read(charBuffer, 0, charBuffer.Length);

// Read until newline (respects NewLine property)
string line = port.ReadLine();

// Read until a specific delimiter
string data = port.ReadTo(">");
```

## Reading lines
Use ReadLineAsync to await a single complete line split by the configured NewLine. Supports single- and multi-character newline sequences and respects ReadTimeout (> 0).

```csharp
port.NewLine = "\r\n"; // optional: default is "\n"
var line = await port.ReadLineAsync();
Console.WriteLine($"Line: {line}");
```

You can also pass a CancellationToken:
```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var line = await port.ReadLineAsync(cts.Token);
```

### ReadToAsync
Read data up to a specific delimiter asynchronously:
```csharp
// Read until '>' delimiter
var data = await port.ReadToAsync(">");
Console.WriteLine($"Received: {data}");

// With cancellation
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var data = await port.ReadToAsync(">", cts.Token);
```

## Line streaming with Lines
Subscribe to Lines to get a continuous stream of complete lines:
```csharp
port.NewLine = "\n";
port.Lines.Subscribe(line => Console.WriteLine($"LINE: {line}"));
```

## Writing
- `port.Write(string text)` - Write a string
- `port.WriteLine(string text)` - Write a string followed by NewLine
- `port.Write(byte[] buffer)` - Write entire byte array
- `port.Write(byte[] buffer, int offset, int count)` - Write portion of byte array
- `port.Write(char[] buffer)` - Write entire char array
- `port.Write(char[] buffer, int offset, int count)` - Write portion of char array

### Modern .NET Write Overloads (net8.0+)
On modern .NET targets, additional Span-based overloads are available:
```csharp
// Write from ReadOnlySpan<byte>
ReadOnlySpan<byte> data = stackalloc byte[] { 0x01, 0x02, 0x03 };
port.Write(data);

// Write from ReadOnlyMemory<byte>
ReadOnlyMemory<byte> memory = new byte[] { 0x01, 0x02, 0x03 };
port.Write(memory);

// Write from ReadOnlySpan<char>
ReadOnlySpan<char> chars = "Hello".AsSpan();
port.Write(chars);
```

## Error handling and state
- Subscribe to `port.ErrorReceived` for exceptions and serial errors.
- Subscribe to `port.IsOpenObservable` to react to open/close transitions.
- Call `port.Close()` or dispose subscriptions (DisposeWith) to release the port.

### Buffer Management
```csharp
// Discard pending input data
port.DiscardInBuffer();

// Discard pending output data
port.DiscardOutBuffer();

// Check buffer sizes
Console.WriteLine($"Bytes to read: {port.BytesToRead}");
Console.WriteLine($"Bytes to write: {port.BytesToWrite}");
```

### Windows-only: Pin Changed Events
On Windows targets, subscribe to pin state changes:
```csharp
#if HasWindows
port.PinChanged.Subscribe(args => 
    Console.WriteLine($"Pin changed: {args.EventType}"));
#endif
```

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

### Batched receive (TCP/UDP)
Subscribe to batched byte arrays for throughput-sensitive pipelines:
```csharp
// TCP batched chunks per read loop
new TcpClientRx("example.com", 80).DataReceivedBatches
    .Subscribe(chunk => Console.WriteLine($"TCP chunk size: {chunk.Length}"));

// UDP per-datagram batches
new UdpClientRx(12345).DataReceivedBatches
    .Subscribe(datagram => Console.WriteLine($"UDP datagram size: {datagram.Length}"));
```

## Threading and scheduling
- The DataReceived and other streams run on the underlying event threads. Use ObserveOn to marshal to a UI or a dedicated scheduler when needed.
- ReadAsync uses a lightweight lock and offloads blocking reads, avoiding CPU spin.

## Tips and best practices
- Subscribe before calling Open() to ensure you don’t miss events.
- Tune Encoding (default ASCII), BaudRate, Parity, StopBits, and Handshake to match your device.
- Use BufferUntil for delimited protocols. For binary protocols, use ReadAsync with fixed sizes.
- Use Lines when dealing with text protocols; use ReadLineAsync when you need a one-shot line.
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


## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

## Sponsorship

If you find this library useful and would like to support its development, consider sponsoring the project on [GitHub Sponsors](https://github.com/sponsors/ChrisPulman).

---

**SerialPortRx** - Empowering Industrial Automation with Reactive Technology ⚡🏭
