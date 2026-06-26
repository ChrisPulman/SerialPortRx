// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IO.Ports.Tests;

/// <summary>Tests for TCP and UDP reactive port adapters.</summary>
[NotInParallel]
public sealed class NetworkPortRxTests
{
    /// <summary>Verifies TCP loopback data is published to byte and batch streams.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task TcpClientRx_Open_ReadsLoopbackBytes()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var client = new TcpClientRx();
        var acceptTask = listener.AcceptTcpClientAsync();

        client.Connect(IPAddress.Loopback, endpoint.Port);
        using var server = await acceptTask;

        var values = new List<int>();
        var batches = new List<byte[]>();
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var dataSubscription = client.DataReceived.Subscribe(value =>
        {
            values.Add(value);
            if (values.Count < 3)
            {
                return;
            }

            _ = received.TrySetResult(true);
        });
        using var batchSubscription = client.DataReceivedBatches.Subscribe(batches.Add);

        await client.Open();
        byte[] payload = [1, 2, 3];
        await server.GetStream().WriteAsync(payload);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await Assert.That(values.Count).IsEqualTo(3);
        await Assert.That(values[0]).IsEqualTo(1);
        await Assert.That(values[1]).IsEqualTo(2);
        await Assert.That(values[2]).IsEqualTo(3);
        await Assert.That(batches.Count).IsEqualTo(1);

        client.Close();
    }

    /// <summary>Verifies TCP Write sends data to the connected socket.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task TcpClientRx_Write_SendsBytes()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var client = new TcpClientRx();
        var acceptTask = listener.AcceptTcpClientAsync();

        client.Connect(IPAddress.Loopback, endpoint.Port);
        using var server = await acceptTask;
        var buffer = new byte[3];

        client.Write([9, 8, 7], 0, 3);
        var bytesRead = await server.GetStream().ReadAsync(buffer);

        await Assert.That(bytesRead).IsEqualTo(3);
        await Assert.That(buffer[0]).IsEqualTo((byte)9);
        await Assert.That(buffer[1]).IsEqualTo((byte)8);
        await Assert.That(buffer[2]).IsEqualTo((byte)7);
    }

    /// <summary>Verifies UDP loopback datagrams are published to byte and batch streams.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    [Timeout(5000)]
    public async Task UdpClientRx_Open_ReadsLoopbackDatagrams()
    {
        using var receiverSocket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = (IPEndPoint)receiverSocket.Client.LocalEndPoint!;
        using var receiver = new UdpClientRx(receiverSocket);
        using var sender = new UdpClient();

        var values = new List<int>();
        var batches = new List<byte[]>();
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var batchReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var dataSubscription = receiver.DataReceived.Subscribe(value =>
        {
            values.Add(value);
            if (values.Count < 2)
            {
                return;
            }

            _ = received.TrySetResult(true);
        });
        using var batchSubscription = receiver.DataReceivedBatches.Subscribe(batch =>
        {
            batches.Add(batch);
            _ = batchReceived.TrySetResult(true);
        });

        await receiver.Open();
        byte[] payload = [4, 5];
        await sender.SendAsync(payload, payload.Length, endpoint);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await batchReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await Assert.That(values.Count).IsEqualTo(2);
        await Assert.That(values[0]).IsEqualTo(4);
        await Assert.That(values[1]).IsEqualTo(5);
        await Assert.That(batches.Count).IsEqualTo(1);

        receiver.Close();
    }

    /// <summary>Verifies UDP ReadAsync validates arguments.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task UdpClientRx_ReadAsync_WhenArgumentsAreInvalid_Throws()
    {
        using var udp = new UdpClientRx(new UdpClient(0));
        var buffer = new byte[4];

        await Assert.That(() => udp.ReadAsync(null!, 0, 1)).Throws<ArgumentNullException>();
        await Assert.That(() => udp.ReadAsync(buffer, -1, 1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => udp.ReadAsync(buffer, 5, 1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => udp.ReadAsync(buffer, 0, -1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => udp.ReadAsync(buffer, 0, 5)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Verifies UDP Write validates arguments.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task UdpClientRx_Write_WhenArgumentsAreInvalid_Throws()
    {
        using var udp = new UdpClientRx(new UdpClient(0));
        var buffer = new byte[4];

        await Assert.That(() => udp.Write(null!, 0, 1)).Throws<ArgumentNullException>();
        await Assert.That(() => udp.Write(buffer, -1, 1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => udp.Write(buffer, 5, 1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => udp.Write(buffer, 0, -1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => udp.Write(buffer, 0, 5)).Throws<ArgumentOutOfRangeException>();
    }
}
