// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI.Extensions;

namespace CP.IO.Ports.Test;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        const string com1 = "COM1";
        const string com2 = "COM2";

        var dis = new CompositeDisposable();

        // Test PortNames
        SerialPortRx.PortNames().Subscribe(names => Console.WriteLine($"Available ports: {string.Join(", ", names)}")).DisposeWith(dis);

        // Create ports
        var port1 = new SerialPortRx(com1, 9600) { NewLine = "\r\n", ReadTimeout = 1000 };
        var port2 = new SerialPortRx(com2, 9600) { NewLine = "\r\n", ReadTimeout = 1000 };
        port1.IsOpenObservable
            .Do(isOpen => Console.WriteLine($"Connection State: Port1 is {(isOpen ? "open" : "closed")}"))
            .CombineLatest(port2.IsOpenObservable.Do(isOpen => Console.WriteLine($"Connection State: Port2 is {(isOpen ? "open" : "closed")}")), (x, y) => x && y).Where(x => x).Subscribe(async _ =>
        {
            Console.WriteLine("Step 1: Both ports are open, starting test...");

            // Subscribe to events for port1
            port1.DataReceived.Subscribe(ch => Console.WriteLine($"Port1 received char: {ch}")).DisposeWith(dis);
            port1.Lines.Subscribe(line => Console.WriteLine($"Port1 line: {line}")).DisposeWith(dis);
            port1.BytesReceived.Subscribe(b => Console.WriteLine($"Port1 byte: {b:X2}")).DisposeWith(dis);
            port1.ErrorReceived.Subscribe(ex => Console.WriteLine($"Port1 error: {ex.Message}")).DisposeWith(dis);
            port1.IsOpenObservable.Subscribe(open => Console.WriteLine($"Port1 is {(open ? "open" : "closed")}")).DisposeWith(dis);

            // Subscribe to events for port2
            // port2.DataReceived.Subscribe(ch => Console.WriteLine($"Port2 received char: {ch}")).DisposeWith(dis);
            // port2.Lines.Subscribe(line => Console.WriteLine($"Port2 line: {line}")).DisposeWith(dis);
            // port2.BytesReceived.Subscribe(b => Console.WriteLine($"Port2 byte: {b:X2}")).DisposeWith(dis);
            port2.ErrorReceived.Subscribe(ex => Console.WriteLine($"Port2 error: {ex.Message}")).DisposeWith(dis);
            port2.IsOpenObservable.Subscribe(open => Console.WriteLine($"Port2 is {(open ? "open" : "closed")}")).DisposeWith(dis);

            // Test writing from port1
            Console.WriteLine("Step 2: Writing from port1");
            port1.WriteLine("Hello from Port1");
            port1.Write("Test data");
            port1.Write([0x01, 0x02, 0x03], 0, 3);

            // Test synchronous reads on port2
            try
            {
                var line = port2.ReadLine();
                Console.WriteLine($"Step 3: Sync ReadLine: {line}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sync ReadLine error: {ex.Message}");
            }

            try
            {
                var existing = port2.ReadExisting();
                Console.WriteLine($"Step 4: ReadExisting: {existing}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ReadExisting error: {ex.Message}");
            }

            // Test async reads
            var readTask = port2.ReadLineAsync();
            await Task.Delay(50); // Short delay to ensure async read is set up
            port1.WriteLine("New data for port2"); // Write in parallel to provide data for async read
            try
            {
                var asyncLine = await readTask;
                Console.WriteLine($"Step 5: Async ReadLine: {asyncLine}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Async ReadLine error: {ex.Message}");
            }

            // Test ReadAsync
            Console.WriteLine("Step 6: Testing ReadAsync");
            var buffer = new byte[10];
            var readAsyncTask = port2.ReadAsync(buffer, 0, 10);
            await Task.Delay(50);
            port1.Write([65, 66, 67], 0, 3);
            var read = await readAsyncTask;
            Console.WriteLine($"Step 7: ReadAsync: {read} bytes, data: {BitConverter.ToString(buffer, 0, read)}");

            // Test writing from port2
            Console.WriteLine("Step 8: Writing from port2");
            port2.WriteLine("Hello from Port2");

            // Test sync read on port1
            Console.WriteLine("Step 9: Sync read on port1");
            try
            {
                var line2 = port1.ReadLine();
                Console.WriteLine($"Port1 Sync ReadLine: {line2}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Port1 Sync ReadLine error: {ex.Message}");
            }

            // Wait a bit
            await Task.Delay(1000);

            Console.WriteLine("Step 10: Test completed, closing ports...");

            // Close ports
            port1?.Close();
            port2?.Close();

            dis?.Dispose();
            Environment.Exit(0);
        }).DisposeWith(dis);

        // Open ports
        await port1.Open();
        await port2.Open();

        Console.ReadLine();

        // Close ports
        port1?.Close();
        port2?.Close();

        dis?.Dispose();
    }
}
