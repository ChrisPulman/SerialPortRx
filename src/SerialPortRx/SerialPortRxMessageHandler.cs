// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace CP.IO.Ports.Reactive;
#else
namespace CP.IO.Ports;
#endif

/// <summary>Coordinates command requests and responses over a reactive serial port.</summary>
public sealed class SerialPortRxMessageHandler : IDisposable
{
    /// <summary>Pending command requests awaiting response lines.</summary>
    private readonly ConcurrentQueue<PendingRequest> _pending = new();

    /// <summary>The serial port used to send commands and receive responses.</summary>
    private readonly SerialPortRx _port;

    /// <summary>Subscriptions owned by the message handler.</summary>
    private readonly CompositeDisposable _disposables = [];

    /// <summary>Synchronizes request queue and polling state changes.</summary>
#if NET9_0_OR_GREATER
    private readonly Lock _sync = new();
#else
    private readonly object _sync = new();
#endif

    /// <summary>Cancellation source for the active polling loop.</summary>
    private CancellationTokenSource? _pollingCts;

    /// <summary>The active polling task.</summary>
    private Task? _pollingTask;

    /// <summary>Initializes a new instance of the <see cref="SerialPortRxMessageHandler" /> class.</summary>
    /// <param name="port">The port.</param>
    /// <param name="errorLine">The error line.</param>
    public SerialPortRxMessageHandler(SerialPortRx port, params string[]? errorLine)
    {
        _port = port;

        // Parse normal line responses and correlate with pending commands
        var lineSub = _port.Lines
            .Subscribe(line => OnLineReceived(line, errorLine));
        _disposables.Add(lineSub);
    }

    /// <summary>
    /// Gets or sets an optional prefix that the device may prepend to responses (e.g., "1").
    /// When set, echo detection and response normalization will account for it.
    /// </summary>
    public string? ResponsePrefix { get; set; }

    /// <summary>Gets or sets the polling task.</summary>
    /// <value>
    /// The polling task.
    /// </value>
    public Func<Task>? PollingTasks { get; set; }

    /// <summary>Requests the asynchronous.</summary>
    /// <param name="cmd">The command.</param>
    /// <exception cref="InvalidOperationException">Not connected.</exception>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task RequestAsync(string cmd)
    {
        if (!_port.IsOpen)
        {
            throw new InvalidOperationException("Not connected.");
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending.Enqueue(new PendingRequest(cmd, _ => { }, tcs));
        await SendCommandAsync(cmd).ConfigureAwait(false);

        var timeoutMs = Math.Max(100, _port?.ReadTimeout > 0 ? _port!.ReadTimeout : 3000);
        using var cts = new CancellationTokenSource(timeoutMs);
        using (cts.Token.Register(() => tcs.TrySetCanceled(cts.Token)))
        {
            await tcs.Task.ConfigureAwait(false);
        }
    }

    /// <summary>Requests the asynchronous.</summary>
    /// <param name="cmd">The command.</param>
    /// <param name="apply">The apply response action.</param>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    /// <exception cref="InvalidOperationException">Not connected.</exception>
    public async Task RequestAsync(string cmd, Action<string> apply)
    {
        if (!_port.IsOpen)
        {
            throw new InvalidOperationException("Not connected.");
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending.Enqueue(new PendingRequest(cmd, apply, tcs));
        await SendCommandAsync(cmd).ConfigureAwait(false);

        // Respect read timeout if set, else default to 3000ms
        var timeoutMs = Math.Max(100, _port?.ReadTimeout > 0 ? _port!.ReadTimeout : 3000);
        using var cts = new CancellationTokenSource(timeoutMs);
        using (cts.Token.Register(() => tcs.TrySetCanceled(cts.Token)))
        {
            await tcs.Task.ConfigureAwait(false);
        }
    }

    /// <summary>Exectute With the polling stopped.</summary>
    /// <param name="action">The action.</param>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    public async Task WithPollingStopped(Func<Task> action)
    {
        if (action is null)
        {
            return;
        }

        var wasPolling = _pollingTask is not null;
        if (wasPolling)
        {
            StopPolling();
        }

        try
        {
            await action();
        }
        finally
        {
            if (_port.IsOpen && wasPolling)
            {
                StartPolling();
            }
        }
    }

    /// <summary>Starts the polling.</summary>
    public void StartPolling()
    {
        StopPolling();
        _pollingCts = new();
        var token = _pollingCts.Token;
        _pollingTask = Task.Run(
            async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (PollingTasks is not null && _port.IsOpen)
                        {
                            await PollingTasks().ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        CompletePendingWithException(ex);
                    }

                    await Task.Delay(100, token).ConfigureAwait(false);
                }
            },
            token);
    }

    /// <summary>Stops the polling.</summary>
    public void StopPolling()
    {
        var cts = _pollingCts;
        _pollingCts = null;
        try
        {
            cts?.Cancel();
            _pollingTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException ex)
        {
            CompletePendingWithException(ex.Flatten());
        }
        finally
        {
            _pollingTask = null;
            cts?.Dispose();
        }
    }

    /// <summary>Sends the command asynchronous.</summary>
    /// <param name="fullCmd">The full command.</param>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    /// <exception cref="InvalidOperationException">Not connected.</exception>
    public Task SendCommandAsync(string fullCmd)
    {
        if (_port?.IsOpen != true)
        {
            throw new InvalidOperationException("Not connected.");
        }

        lock (_sync)
        {
            // Write command with terminator matching NewLine.
            _port.WriteLine(fullCmd);
        }

        return Task.CompletedTask;
    }

    /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
    public void Dispose()
    {
        StopPolling();
        _disposables.Dispose();
    }

    /// <summary>Determines whether a response matches a sent command echo.</summary>
    /// <param name="trimmed">The trimmed response line.</param>
    /// <param name="sent">The sent command text.</param>
    /// <returns><see langword="true"/> when the response matches the sent command.</returns>
    private static bool IsEchoMatch(string trimmed, string sent) =>
        string.Equals(trimmed, sent, StringComparison.OrdinalIgnoreCase) ||
        (trimmed.Length >= sent.Length && trimmed.EndsWith(sent, StringComparison.OrdinalIgnoreCase));

    /// <summary>Processes a received response line.</summary>
    /// <param name="response">The received response line.</param>
    /// <param name="errorLine">The configured error prefixes.</param>
    private void OnLineReceived(string response, string[]? errorLine)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return;
        }

        var trimmed = response.Trim();
        if (IsEchoLine(trimmed))
        {
            return;
        }

        if (TryCompleteError(trimmed, errorLine))
        {
            return;
        }

        CompleteNextPending(trimmed);
    }

    /// <summary>Determines whether a received line is an echo for the pending command.</summary>
    /// <param name="trimmed">The trimmed response line.</param>
    /// <returns><see langword="true"/> when the response is an echo line.</returns>
    private bool IsEchoLine(string trimmed)
    {
        if (!_pending.TryPeek(out var head))
        {
            return false;
        }

        var sentCandidate = head.Command?.Trim();
        if (string.IsNullOrEmpty(sentCandidate))
        {
            return false;
        }

        var sent = sentCandidate!;
        if (IsEchoMatch(trimmed, sent))
        {
            return true;
        }

        var prefixCandidate = ResponsePrefix;
        if (string.IsNullOrEmpty(prefixCandidate))
        {
            return false;
        }

        var prefix = prefixCandidate!;
        if (!sent.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var sentNoAxis = sent[prefix.Length..].TrimStart();
        return IsEchoMatch(trimmed, sentNoAxis);
    }

    /// <summary>Completes the next pending request as an error when the response matches an error prefix.</summary>
    /// <param name="trimmed">The trimmed response line.</param>
    /// <param name="errorLine">The configured error prefixes.</param>
    /// <returns><see langword="true"/> when an error response was handled.</returns>
    private bool TryCompleteError(string trimmed, string[]? errorLine)
    {
        foreach (var error in errorLine ?? [])
        {
            if (!string.IsNullOrWhiteSpace(error) && trimmed.StartsWith(error!, StringComparison.OrdinalIgnoreCase))
            {
                if (_pending.TryDequeue(out var errPending))
                {
                    _ = errPending.Completion.TrySetException(new InvalidOperationException(trimmed));
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>Applies a normal response to the next pending request.</summary>
    /// <param name="trimmed">The trimmed response line.</param>
    private void CompleteNextPending(string trimmed)
    {
        if (!_pending.TryDequeue(out var pending))
        {
            return;
        }

        try
        {
            var toApply = trimmed;
            var prefix = ResponsePrefix;
            if (!string.IsNullOrEmpty(prefix) && toApply.StartsWith(prefix!, StringComparison.Ordinal))
            {
                toApply = toApply[prefix!.Length..].TrimStart();
            }

            pending.Apply(toApply);
        }
        catch (Exception ex)
        {
            _ = pending.Completion.TrySetException(ex);
            return;
        }

        _ = pending.Completion.TrySetResult(true);
    }

    /// <summary>Completes all pending requests with an exception.</summary>
    /// <param name="exception">The exception to publish to pending requests.</param>
    private void CompletePendingWithException(Exception exception)
    {
        while (_pending.TryDequeue(out var pending))
        {
            _ = pending.Completion.TrySetException(exception);
        }
    }
}
