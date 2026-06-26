// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IO.Ports;

/// <summary>Compatibility bridge between classic observables and ReactiveUI.Primitives async observables.</summary>
public static class ObservableAsyncBridgeExtensions
{
    /// <summary>Provides conversion extensions for classic observables.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="source">The source observable receiver.</param>
    extension<T>(IObservable<T> source)
    {
        /// <summary>Converts a classic observable into an async observable.</summary>
        /// <returns>An async observable that forwards source notifications.</returns>
        public IObservableAsync<T> ToObservableAsync()
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new ObservableAsyncAdapter<T>(source);
        }
    }

    /// <summary>Provides conversion extensions for async observables.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="source">The source async observable receiver.</param>
    extension<T>(IObservableAsync<T> source)
    {
        /// <summary>Converts an async observable into a classic observable.</summary>
        /// <returns>A classic observable that forwards async source notifications.</returns>
        public IObservable<T> ToObservable()
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return Observable.Create<T>(observer =>
            {
                var cancellation = new CancellationTokenSource();
                var asyncObserver = new ObserverAsyncAdapter<T>(observer);
                var subscription = source.SubscribeAsync(asyncObserver, cancellation.Token).AsTask().GetAwaiter().GetResult();

                return Disposable.Create(() =>
                {
                    cancellation.Cancel();
                    subscription.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    cancellation.Dispose();
                });
            });
        }
    }

    /// <summary>Completes a value task synchronously when required.</summary>
    /// <param name="valueTask">The value task to complete.</param>
    private static void Complete(ValueTask valueTask)
    {
        if (valueTask.IsCompletedSuccessfully)
        {
            return;
        }

        valueTask.AsTask().GetAwaiter().GetResult();
    }

    /// <summary>Adapts a classic observable to the async observable contract.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="source">The source observable.</param>
    private sealed class ObservableAsyncAdapter<T>(IObservable<T> source) : IObservableAsync<T>
    {
        /// <summary>Subscribes an async observer to the adapted observable.</summary>
        /// <param name="observer">The async observer.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The async subscription.</returns>
        public ValueTask<IAsyncDisposable> SubscribeAsync(IObserverAsync<T> observer, CancellationToken cancellationToken)
        {
            if (observer is null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            var subscription = source.Subscribe(
                value => Complete(observer.OnNextAsync(value, cancellationToken)),
                error => Complete(observer.OnErrorResumeAsync(error, cancellationToken)),
                () => Complete(observer.OnCompletedAsync(Result.Success)));

            return new ValueTask<IAsyncDisposable>(new AsyncSubscription(subscription));
        }
    }

    /// <summary>Adapts a classic disposable subscription to an async disposable subscription.</summary>
    /// <param name="subscription">The wrapped subscription.</param>
    private sealed class AsyncSubscription(IDisposable subscription) : IAsyncDisposable
    {
        /// <summary>Disposes the wrapped subscription.</summary>
        /// <returns>A completed value task.</returns>
        public ValueTask DisposeAsync()
        {
            subscription.Dispose();
            return default;
        }
    }

    /// <summary>Adapts a classic observer to the async observer contract.</summary>
    /// <typeparam name="T">The observed value type.</typeparam>
    /// <param name="observer">The wrapped observer.</param>
    private sealed class ObserverAsyncAdapter<T>(IObserver<T> observer) : IObserverAsync<T>
    {
        /// <summary>Disposes the observer adapter.</summary>
        /// <returns>A completed value task.</returns>
        public ValueTask DisposeAsync() => default;

        /// <summary>Forwards completion or failure to the wrapped observer.</summary>
        /// <param name="result">The completion result.</param>
        /// <returns>A completed value task.</returns>
        public ValueTask OnCompletedAsync(Result result)
        {
            if (result.IsFailure)
            {
                observer.OnError(result.Exception);
            }
            else
            {
                observer.OnCompleted();
            }

            return default;
        }

        /// <summary>Forwards an error to the wrapped observer.</summary>
        /// <param name="error">The observed error.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A completed value task.</returns>
        public ValueTask OnErrorResumeAsync(Exception error, CancellationToken cancellationToken)
        {
            observer.OnError(error);
            return default;
        }

        /// <summary>Forwards a value to the wrapped observer.</summary>
        /// <param name="value">The observed value.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A completed value task.</returns>
        public ValueTask OnNextAsync(T value, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return default;
            }

            observer.OnNext(value);
            return default;
        }
    }
}
