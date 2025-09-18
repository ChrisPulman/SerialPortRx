// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;

namespace CP.IO.Ports;

/// <summary>
/// SerialPortRxMessageHandler.
/// </summary>
public sealed class SerialPortRxMessageHandler : IDisposable
{
    private readonly ConcurrentQueue<PendingRequest> _pending = new();
    private readonly SerialPortRx _port;
    private readonly CompositeDisposable _disposables = [];
    private readonly object _sync = new();
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="SerialPortRxMessageHandler" /> class.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the polling task.
    /// </summary>
    /// <value>
    /// The polling task.
    /// </value>
    public Func<Task>? PollingTasks { get; set; }

    /// <summary>
    /// Requests the asynchronous.
    /// </summary>
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

    /// <summary>
    /// Requests the asynchronous.
    /// </summary>
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

    /// <summary>
    /// Exectute With the polling stopped.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    public async Task WithPollingStopped(Func<Task> action)
    {
        if (action == null)
        {
            return;
        }

        var wasPolling = _pollingTask != null;
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

    /// <summary>
    /// Starts the polling.
    /// </summary>
    public void StartPolling()
    {
        StopPolling();
        _pollingCts = new CancellationTokenSource();
        var cts = _pollingCts;
        if (cts == null)
        {
            return;
        }

        var token = cts.Token;
        _pollingTask = Task.Run(
            async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (PollingTasks != null && _port.IsOpen)
                        {
                            await PollingTasks().ConfigureAwait(false);
                        }
                    }
                    catch
                    {
                        // ignore
                    }

                    await Task.Delay(100, token).ConfigureAwait(false);
                }
            },
            token);
    }

    /// <summary>
    /// Stops the polling.
    /// </summary>
    public void StopPolling()
    {
        var cts = _pollingCts;
        _pollingCts = null;
        try
        {
            cts?.Cancel();
            _pollingTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // ignore
        }
        finally
        {
            _pollingTask = null;
            cts?.Dispose();
        }
    }

    /// <summary>
    /// Sends the command asynchronous.
    /// </summary>
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

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        StopPolling();
        _disposables.Dispose();
    }

    private void OnLineReceived(string response, string[]? errorLine)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return;
        }

        var trimmed = response.Trim();

        // Ignore command echo lines (device may echo the exact command before sending data)
        if (_pending.TryPeek(out var head))
        {
            var sent = head.Command?.Trim();
            if (!string.IsNullOrEmpty(sent))
            {
                // Match direct or axis-suffixed echoes
                if (string.Equals(trimmed, sent, StringComparison.OrdinalIgnoreCase) ||
                    (trimmed.Length >= sent!.Length && trimmed.EndsWith(sent, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                // Match echoes without prefix if device omits it
                var prefix = ResponsePrefix;
                if (!string.IsNullOrEmpty(prefix) && sent.StartsWith(prefix, StringComparison.Ordinal))
                {
                    var sentNoAxis = sent[prefix!.Length..].TrimStart();
                    if (string.Equals(trimmed, sentNoAxis, StringComparison.OrdinalIgnoreCase) ||
                        (trimmed.Length >= sentNoAxis.Length && trimmed.EndsWith(sentNoAxis, StringComparison.OrdinalIgnoreCase)))
                    {
                        return;
                    }
                }
            }
        }

        foreach (var error in errorLine ?? [])
        {
            if (!string.IsNullOrWhiteSpace(error) && trimmed.StartsWith(error, StringComparison.OrdinalIgnoreCase))
            {
                if (_pending.TryDequeue(out var errPending))
                {
                    try
                    {
                        errPending.Completion.TrySetException(new InvalidOperationException(trimmed));
                    }
                    catch
                    {
                    }
                }

                return;
            }
        }

        if (_pending.TryDequeue(out var pending))
        {
            try
            {
                // Strip prefix if present
                var toApply = trimmed;
                var prefix = ResponsePrefix;
                if (!string.IsNullOrEmpty(prefix) && toApply.StartsWith(prefix, StringComparison.Ordinal))
                {
                    toApply = toApply[prefix!.Length..].TrimStart();
                }

                pending.Apply(toApply);
            }
            catch
            {
            }

            try
            {
                pending.Completion.TrySetResult(true);
            }
            catch
            {
            }
        }
    }
}
