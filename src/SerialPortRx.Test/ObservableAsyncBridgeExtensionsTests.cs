// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IO.Ports.Tests;

/// <summary>Tests for observable/async-observable bridge helpers.</summary>
public sealed class ObservableAsyncBridgeExtensionsTests
{
    /// <summary>Values used to verify observable forwarding order.</summary>
    private static readonly int[] TestValues = [1, 2, 3];

    /// <summary>Verifies null observable arguments are rejected.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ToObservableAsync_WhenSourceIsNull_Throws()
    {
        IObservable<int>? source = null;

        await Assert.That(() => source!.ToObservableAsync()).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies null async observable arguments are rejected.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ToObservable_WhenSourceIsNull_Throws()
    {
        IObservableAsync<int>? source = null;

        await Assert.That(() => source!.ToObservable()).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies null async observers are rejected.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task SubscribeAsync_WhenObserverIsNull_Throws()
    {
        var source = Observable.Return(42).ToObservableAsync();

        async Task Act() => await source.SubscribeAsync(null!, CancellationToken.None);

        await Assert.That(Act).Throws<ArgumentNullException>();
    }

    /// <summary>Verifies observable values are forwarded to async observers.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ToObservableAsync_ForwardsValuesAndCompletion()
    {
        var observer = new RecordingAsyncObserver<int>();
        var source = Observable.FromEnumerable(TestValues).ToObservableAsync();

        await using var subscription = await source.SubscribeAsync(observer, CancellationToken.None);

        await Assert.That(observer.Values.Count).IsEqualTo(3);
        await Assert.That(observer.Values[0]).IsEqualTo(1);
        await Assert.That(observer.Values[1]).IsEqualTo(2);
        await Assert.That(observer.Values[2]).IsEqualTo(3);
        await Assert.That(observer.IsCompleted).IsTrue();
        await Assert.That(observer.Error).IsNull();
    }

    /// <summary>Verifies canceled subscriptions forward the token to async observers.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ToObservableAsync_WhenCanceled_ForwardsCanceledToken()
    {
        var observer = new RecordingAsyncObserver<int>();
        var source = Observable.Return(42).ToObservableAsync();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await using var subscription = await source.SubscribeAsync(observer, cancellation.Token);

        await Assert.That(observer.Values.Count).IsEqualTo(1);
        await Assert.That(observer.LastOnNextCancellationRequested).IsTrue();
        await Assert.That(observer.IsCompleted).IsTrue();
    }

    /// <summary>Verifies async observable values are forwarded to classic observers.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ToObservable_ForwardsValuesAndCompletion()
    {
        var values = new List<int>();
        var completed = false;
        var source = new ManualAsyncObservable<int>(async (observer, token) =>
        {
            await observer.OnNextAsync(7, token);
            await observer.OnCompletedAsync(Result.Success);
        });

        using var subscription = source.ToObservable().Subscribe(
            values.Add,
            _ => { },
            () => completed = true);

        await Assert.That(values.Count).IsEqualTo(1);
        await Assert.That(values[0]).IsEqualTo(7);
        await Assert.That(completed).IsTrue();
    }

    /// <summary>Verifies async observable errors are forwarded to classic observers.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task ToObservable_ForwardsErrors()
    {
        var receivedErrors = new List<Exception>();
        var expected = new InvalidOperationException("boom");
        var source = new ManualAsyncObservable<int>((observer, token) => observer.OnErrorResumeAsync(expected, token));

        using var subscription = source.ToObservable().Subscribe(
            _ => { },
            receivedErrors.Add);

        await Assert.That(receivedErrors.Count).IsEqualTo(1);
        await Assert.That(receivedErrors[0]).IsEqualTo(expected);
    }

    /// <summary>Records async observer notifications for assertions.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    private sealed class RecordingAsyncObserver<T> : IObserverAsync<T>
    {
        /// <summary>Gets the values received by the observer.</summary>
        public List<T> Values { get; } = [];

        /// <summary>Gets a value indicating whether completion was observed.</summary>
        public bool IsCompleted { get; private set; }

        /// <summary>Gets the last observed error.</summary>
        public Exception? Error { get; private set; }

        /// <summary>Gets a value indicating whether the last OnNext token was canceled.</summary>
        public bool LastOnNextCancellationRequested { get; private set; }

        /// <summary>Disposes the observer.</summary>
        /// <returns>A completed value task.</returns>
        public ValueTask DisposeAsync() => default;

        /// <summary>Records completion.</summary>
        /// <param name="result">The completion result.</param>
        /// <returns>A completed value task.</returns>
        public ValueTask OnCompletedAsync(Result result)
        {
            IsCompleted = result.IsSuccess;
            Error = result.Exception;
            return default;
        }

        /// <summary>Records a resumable error.</summary>
        /// <param name="error">The observed error.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A completed value task.</returns>
        public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
        {
            Error = error;
            return default;
        }

        /// <summary>Records the next value.</summary>
        /// <param name="value">The observed value.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A completed value task.</returns>
        public ValueTask OnNextAsync(T value, CancellationToken cancellationToken)
        {
            LastOnNextCancellationRequested = cancellationToken.IsCancellationRequested;
            Values.Add(value);
            return default;
        }
    }

    /// <summary>Manual async observable used to publish controlled notifications.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="publish">The delegate that publishes notifications.</param>
    private sealed class ManualAsyncObservable<T>(Func<IObserverAsync<T>, CancellationToken, ValueTask> publish) : IObservableAsync<T>
    {
        /// <summary>Subscribes the observer and publishes configured notifications.</summary>
        /// <param name="observer">The async observer.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A disposable subscription.</returns>
        public async ValueTask<IAsyncDisposable> SubscribeAsync(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            await publish(observer, cancellationToken);
            return NoopAsyncDisposable.Instance;
        }
    }

    /// <summary>No-op async disposable used by manual observables.</summary>
    private sealed class NoopAsyncDisposable : IAsyncDisposable
    {
        /// <summary>Gets the singleton no-op async disposable.</summary>
        public static NoopAsyncDisposable Instance { get; } = new();

        /// <summary>Disposes the no-op resource.</summary>
        /// <returns>A completed value task.</returns>
        public ValueTask DisposeAsync() => default;
    }
}
