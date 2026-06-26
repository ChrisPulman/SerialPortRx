// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IO.Ports.Tests;

/// <summary>Tests for command/response message handling.</summary>
public sealed class SerialPortRxMessageHandlerTests
{
    /// <summary>Verifies commands cannot be sent when the port is closed.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task SendAndRequest_WhenPortIsClosed_Throw()
    {
        using var port = new SerialPortRx();
        using var handler = new SerialPortRxMessageHandler(port);

        await Assert.That(() => handler.SendCommandAsync("G0")).Throws<InvalidOperationException>();
        await Assert.That(() => handler.RequestAsync("G0")).Throws<InvalidOperationException>();
        await Assert.That(() => handler.RequestAsync("G0", _ => { })).Throws<InvalidOperationException>();
    }

    /// <summary>Verifies null polling actions are ignored.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task WithPollingStopped_WhenActionIsNull_Returns()
    {
        using var port = new SerialPortRx();
        using var handler = new SerialPortRxMessageHandler(port);

        await handler.WithPollingStopped(null!);

        await Assert.That(handler.PollingTasks is null).IsTrue();
    }

    /// <summary>Verifies command echoes are ignored and pending requests remain queued.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnLineReceived_WhenLineIsEcho_IgnoresResponse()
    {
        using var port = new SerialPortRx();
        using var handler = new SerialPortRxMessageHandler(port);
        var completion = new TaskCompletionSource<bool>();
        Enqueue(handler, new PendingRequest("MOVE X", _ => { }, completion));

        InvokeLine(handler, "MOVE X");

        await Assert.That(completion.Task.IsCompleted).IsFalse();
        await Assert.That(PendingCount(handler)).IsEqualTo(1);
    }

    /// <summary>Verifies normal responses are applied and complete pending commands.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnLineReceived_WhenLineIsResponse_AppliesAndCompletes()
    {
        using var port = new SerialPortRx();
        using var handler = new SerialPortRxMessageHandler(port);
        var completion = new TaskCompletionSource<bool>();
        var applied = string.Empty;
        Enqueue(handler, new PendingRequest("READ", value => applied = value, completion));

        InvokeLine(handler, "42");

        await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Assert.That(applied).IsEqualTo("42");
        await Assert.That(PendingCount(handler)).IsEqualTo(0);
    }

    /// <summary>Verifies response prefixes are stripped before applying values.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnLineReceived_WithResponsePrefix_StripsPrefix()
    {
        using var port = new SerialPortRx();
        using var handler = new SerialPortRxMessageHandler(port) { ResponsePrefix = "1" };
        var completion = new TaskCompletionSource<bool>();
        var applied = string.Empty;
        Enqueue(handler, new PendingRequest("STATUS", value => applied = value, completion));

        InvokeLine(handler, "1 OK");

        await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Assert.That(applied).IsEqualTo("OK");
    }

    /// <summary>Verifies error responses fault pending commands.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task OnLineReceived_WhenLineIsError_FaultsPendingRequest()
    {
        using var port = new SerialPortRx();
        using var handler = new SerialPortRxMessageHandler(port);
        var completion = new TaskCompletionSource<bool>();
        Enqueue(handler, new PendingRequest("READ", _ => { }, completion));

        InvokeLine(handler, "ERR bad", "ERR");

        await Assert.That(() => completion.Task).Throws<InvalidOperationException>();
        await Assert.That(PendingCount(handler)).IsEqualTo(0);
    }

    /// <summary>Verifies polling can be started and stopped when no polling task is configured.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task StartAndStopPolling_WithoutPollingTask_Completes()
    {
        using var port = new SerialPortRx();
        using var handler = new SerialPortRxMessageHandler(port);

        handler.StartPolling();
        await Task.Delay(25);
        handler.StopPolling();

        await Assert.That(handler.PollingTasks is null).IsTrue();
    }

    /// <summary>Enqueues a pending request into the handler's private queue.</summary>
    /// <param name="handler">The message handler.</param>
    /// <param name="request">The pending request to enqueue.</param>
    private static void Enqueue(SerialPortRxMessageHandler handler, PendingRequest request) =>
        PendingQueue(handler).Enqueue(request);

    /// <summary>Returns the number of pending requests in the handler.</summary>
    /// <param name="handler">The message handler.</param>
    /// <returns>The number of pending requests.</returns>
    private static int PendingCount(SerialPortRxMessageHandler handler) =>
        PendingQueue(handler).Count;

    /// <summary>Returns the handler's private pending request queue.</summary>
    /// <param name="handler">The message handler.</param>
    /// <returns>The pending request queue.</returns>
    private static ConcurrentQueue<PendingRequest> PendingQueue(SerialPortRxMessageHandler handler)
    {
        var field = typeof(SerialPortRxMessageHandler).GetField("_pending", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (ConcurrentQueue<PendingRequest>)field.GetValue(handler)!;
    }

    /// <summary>Invokes the private line-processing method.</summary>
    /// <param name="handler">The message handler.</param>
    /// <param name="line">The line to process.</param>
    /// <param name="errorLines">The configured error prefixes.</param>
    private static void InvokeLine(SerialPortRxMessageHandler handler, string line, params string[] errorLines)
    {
        var method = typeof(SerialPortRxMessageHandler).GetMethod("OnLineReceived", BindingFlags.NonPublic | BindingFlags.Instance)!;
        _ = method.Invoke(handler, [line, errorLines]);
    }
}
