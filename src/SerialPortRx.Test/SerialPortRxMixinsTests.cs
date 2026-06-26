// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IO.Ports.Tests;

/// <summary>Tests for SerialPortRx mixin helpers.</summary>
public sealed class SerialPortRxMixinsTests
{
    /// <summary>Verifies scalar observable helpers emit converted characters.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task AsObservable_ForNumericValues_EmitsCharacters()
    {
        var byteValue = await FirstValueAsync(((byte)65).AsObservable());
        var intValue = await FirstValueAsync(66.AsObservable());
        var shortValue = await FirstValueAsync(((short)67).AsObservable());

        await Assert.That(byteValue).IsEqualTo('A');
        await Assert.That(intValue).IsEqualTo('B');
        await Assert.That(shortValue).IsEqualTo('C');
    }

    /// <summary>Verifies scalar async observable helpers emit converted characters.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task AsObservableAsync_ForNumericValues_EmitsCharacters()
    {
        var byteValue = await FirstValueAsync(((byte)65).AsObservableAsync().ToObservable());
        var intValue = await FirstValueAsync(66.AsObservableAsync().ToObservable());
        var shortValue = await FirstValueAsync(((short)67).AsObservableAsync().ToObservable());

        await Assert.That(byteValue).IsEqualTo('A');
        await Assert.That(intValue).IsEqualTo('B');
        await Assert.That(shortValue).IsEqualTo('C');
    }

    /// <summary>Verifies async extension methods reject null ports.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task AsyncPortExtensions_WhenPortIsNull_Throw()
    {
        IPortRx? port = null;
        ISerialPortRx? serialPort = null;

        await Assert.That(() => port!.BytesReceivedAsync()).Throws<ArgumentNullException>();
        await Assert.That(() => serialPort!.DataReceivedAsync()).Throws<ArgumentNullException>();
        await Assert.That(() => serialPort!.DataReceivedBytesAsync()).Throws<ArgumentNullException>();
        await Assert.That(() => serialPort!.LinesAsync()).Throws<ArgumentNullException>();
        await Assert.That(() => serialPort!.ErrorReceivedAsync()).Throws<ArgumentNullException>();
        await Assert.That(() => serialPort!.IsOpenObservableAsync()).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies BufferUntil emits text between configured markers.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task BufferUntil_WhenMarkersAreFound_EmitsBufferedText()
    {
        using var source = new ReplaySignal<char>(0);
        var values = new List<string>();
        using var subscription = source
            .BufferUntil(Observable.Return('['), Observable.Return(']'), 100)
            .Subscribe(values.Add);

        source.OnNext('x');
        source.OnNext('[');
        source.OnNext('A');
        source.OnNext(']');

        await Assert.That(values.Count).IsEqualTo(1);
        await Assert.That(values[0]).IsEqualTo("[A]");
    }

    /// <summary>Verifies ObservableAsync.Return emits a single value.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ObservableAsyncReturn_EmitsSingleValue()
    {
        var value = await FirstValueAsync(ObservableAsync.Return(123).ToObservable());

        await Assert.That(value).IsEqualTo(123);
    }

    /// <summary>Verifies pending request records store constructor values.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task PendingRequest_StoresConstructorValues()
    {
        var completion = new TaskCompletionSource<bool>();
        var request = new PendingRequest("G0", _ => { }, completion);

        await Assert.That(request.Command).IsEqualTo("G0");
        await Assert.That(request.Completion).IsEqualTo(completion);
    }

    /// <summary>Returns the first value observed from an observable sequence.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="observable">The observable sequence.</param>
    /// <returns>A task that completes with the first observed value.</returns>
    private static Task<T> FirstValueAsync<T>(IObservable<T> observable)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscription = observable.Subscribe(
            value => _ = completion.TrySetResult(value),
            exception => _ = completion.TrySetException(exception),
            () =>
            {
                if (completion.Task.IsCompleted)
                {
                    return;
                }

                _ = completion.TrySetException(new InvalidOperationException("Observable completed without a value."));
            });

        return completion.Task.ContinueWith(
            task =>
            {
                subscription.Dispose();
                return task.GetAwaiter().GetResult();
            },
            TaskScheduler.Default);
    }
}
