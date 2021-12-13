// <copyright file="SerialPortRxMixins.cs" company="Chris Pulman">
// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace CP.IO.Ports;

/// <summary>
/// Serial Port Rx Mixins.
/// </summary>
public static class SerialPortRxMixins
{
    /// <summary>
    /// transforms a byte into a single value Observable.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>An Observable char.</returns>
    public static IObservable<char> AsObservable(this byte value) => Observable.Return(Convert.ToChar(value));

    /// <summary>
    /// transforms a int into a single value Observable.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>An Observable char.</returns>
    public static IObservable<char> AsObservable(this int value) => Observable.Return(Convert.ToChar(value));

    /// <summary>
    /// transforms a short into a single value Observable.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>An Observable char.</returns>
    public static IObservable<char> AsObservable(this short value) => Observable.Return(Convert.ToChar(value));

    /// <summary>
    /// Buffers the Char values until the start and end chars have been found within the timeout period.
    /// </summary>
    /// <param name="this">The this.</param>
    /// <param name="startsWith">The starts with.</param>
    /// <param name="endsWith">The ends with.</param>
    /// <param name="timeOut">The time out.</param>
    /// <returns>A string made up from the char values between the start and end chars.</returns>
    public static IObservable<string> BufferUntil(this IObservable<char> @this, IObservable<char> startsWith, IObservable<char> endsWith, int timeOut) => Observable.Create<string>(o => {
        var dis = new CompositeDisposable();
        var str = string.Empty;

        var startFound = false;
        var elapsedTime = 0;
        var startsWithL = ' ';
        startsWith.Subscribe(sw => {
            startsWithL = sw;
            elapsedTime = 0;
        }).AddTo(dis);
        var endsWithL = ' ';
        var ewd = endsWith.Subscribe(ew => endsWithL = ew).AddTo(dis);
        var sub = @this.Subscribe(s => {
            elapsedTime = 0;
            if (startFound || s == startsWithL) {
                startFound = true;
                str += s;
                if (s == endsWithL) {
                    o.OnNext(str);
                    startFound = false;
                    str = string.Empty;
                }
            }
        }).AddTo(dis);
        Observable.Interval(TimeSpan.FromMilliseconds(1)).Subscribe(_ => {
            elapsedTime++;
            if (elapsedTime > timeOut) {
                startFound = false;
                str = string.Empty;
                elapsedTime = 0;
            }
        }).AddTo(dis);

        return dis;
    });

    /// <summary>
    /// Buffers the Char values until the start and end chars have been found within the timeout
    /// period other wise returns the default value.
    /// </summary>
    /// <param name="this">The this.</param>
    /// <param name="startsWith">The starts with.</param>
    /// <param name="endsWith">The ends with.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <param name="timeOut">The time out.</param>
    /// <returns>A string made up from the char values between the start and end chars.</returns>
    public static IObservable<string> BufferUntil(this IObservable<char> @this, IObservable<char> startsWith, IObservable<char> endsWith, IObservable<string> defaultValue, int timeOut) => Observable.Create<string>(o => {
        var dis = new CompositeDisposable();
        var str = string.Empty;

        var startFound = false;
        var elapsedTime = 0;
        var startsWithL = ' ';
        startsWith.Subscribe(sw => {
            startsWithL = sw;
            elapsedTime = 0;
        }).AddTo(dis);
        var endsWithL = ' ';
        endsWith.Subscribe(ew => endsWithL = ew).AddTo(dis);
        var defaultValueL = string.Empty;
        defaultValue.Subscribe(dv => defaultValueL = dv).AddTo(dis);
        @this.Subscribe(s => {
            elapsedTime = 0;
            if (startFound || s == startsWithL) {
                startFound = true;
                str += s;
                if (s == endsWithL) {
                    o.OnNext(str);
                    startFound = false;
                    str = string.Empty;
                }
            }
        }).AddTo(dis);

        Observable.Interval(TimeSpan.FromMilliseconds(1)).Subscribe(_ => {
            elapsedTime++;
            if (elapsedTime > timeOut) {
                o.OnNext(defaultValueL);
                startFound = false;
                str = string.Empty;
                elapsedTime = 0;
            }
        }).AddTo(dis);

        return dis;
    });

    /// <summary>
    /// Monitors the received observer.
    /// </summary>
    /// <param name="this">The this.</param>
    /// <returns>Observable value.</returns>
    public static IObservable<EventPattern<SerialDataReceivedEventArgs>> DataReceivedObserver(this SerialPort @this) => Observable.FromEventPattern<SerialDataReceivedEventHandler, SerialDataReceivedEventArgs>(h => @this.DataReceived += h, h => @this.DataReceived -= h);

    /// <summary>
    /// Monitors the Errors observer.
    /// </summary>
    /// <param name="this">The this.</param>
    /// <returns>Observable value.</returns>
    public static IObservable<EventPattern<SerialErrorReceivedEventArgs>> ErrorReceivedObserver(this SerialPort @this) => Observable.FromEventPattern<SerialErrorReceivedEventHandler, SerialErrorReceivedEventArgs>(h => @this.ErrorReceived += h, h => @this.ErrorReceived -= h);

    /// <summary>
    /// Fors the each.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="this">The this.</param>
    /// <returns>Observable value.</returns>
    public static IObservable<T> ForEach<T>(this IObservable<T[]> @this) =>
                                        Observable.Create<T>(obs => {
                                            return @this.Subscribe(
                                                list => {
                                                    foreach (var item in list) {
                                                        if (!EqualityComparer<T>.Default.Equals(item, default)) {
                                                            obs.OnNext(item);
                                                        }
                                                    }
                                                },
                                                obs.OnError,
                                                obs.OnCompleted);
                                        });

    /// <summary>
    /// <para>Repeats the source observable sequence until it successfully terminates.</para>
    /// <para>This is same as Retry().</para>
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>Observable value.</returns>
    public static IObservable<TSource> OnErrorRetry<TSource>(this IObservable<TSource> source) => source.Retry();

    /// <summary>
    /// When caught exception, do onError action and repeat observable sequence.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TException">The type of the exception.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="onError">The on error.</param>
    /// <returns>Observable value.</returns>
    public static IObservable<TSource> OnErrorRetry<TSource, TException>(this IObservable<TSource> source, Action<TException> onError)
where TException : Exception => source.OnErrorRetry(onError, TimeSpan.Zero);

    /// <summary>
    /// When caught exception, do onError action and repeat observable sequence after delay time.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TException">The type of the exception.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="onError">The on error.</param>
    /// <param name="delay">The delay.</param>
    /// <returns>Observable value.</returns>
    public static IObservable<TSource> OnErrorRetry<TSource, TException>(this IObservable<TSource> source, Action<TException> onError, TimeSpan delay)
where TException : Exception => source.OnErrorRetry(onError, int.MaxValue, delay);

    /// <summary>
    /// When caught exception, do onError action and repeat observable sequence during within retryCount.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TException">The type of the exception.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="onError">The on error.</param>
    /// <param name="retryCount">The retry count.</param>
    /// <returns>Observable value.</returns>
    public static IObservable<TSource> OnErrorRetry<TSource, TException>(this IObservable<TSource> source, Action<TException> onError, int retryCount)
where TException : Exception => source.OnErrorRetry(onError, retryCount, TimeSpan.Zero);

    /// <summary>
    /// When caught exception, do onError action and repeat observable sequence after delay time
    /// during within retryCount.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TException">The type of the exception.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="onError">The on error.</param>
    /// <param name="retryCount">The retry count.</param>
    /// <param name="delay">The delay.</param>
    /// <returns>Observable value.</returns>
    public static IObservable<TSource> OnErrorRetry<TSource, TException>(this IObservable<TSource> source, Action<TException> onError, int retryCount, TimeSpan delay)
where TException : Exception => source.OnErrorRetry(onError, retryCount, delay, Scheduler.Default);

    /// <summary>
    /// When caught exception, do onError action and repeat observable sequence after delay
    /// time(work on delayScheduler) during within retryCount.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TException">The type of the exception.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="onError">The on error.</param>
    /// <param name="retryCount">The retry count.</param>
    /// <param name="delay">The delay.</param>
    /// <param name="delayScheduler">The delay scheduler.</param>
    /// <returns>Observable value.</returns>
    public static IObservable<TSource> OnErrorRetry<TSource, TException>(
                    this IObservable<TSource> source, Action<TException> onError, int retryCount, TimeSpan delay, IScheduler delayScheduler)
                    where TException : Exception
    {
        var result = Observable.Defer(() => {
            var dueTime = (delay.Ticks < 0) ? TimeSpan.Zero : delay;
            var empty = Observable.Empty<TSource>();
            var count = 0;

            IObservable<TSource> self = null;
            self = source.Catch((TException ex) => {
                onError(ex);
                return (++count < retryCount)
                ? (dueTime == TimeSpan.Zero)
                ? self.SubscribeOn(Scheduler.CurrentThread)
                : empty.Delay(dueTime, delayScheduler).Concat(self).SubscribeOn(Scheduler.CurrentThread)
                : Observable.Throw<TSource>(ex);
            });

            return self;
        });

        return result;
    }

    /// <summary>
    /// Executes while port is open at the given TimeSpan.
    /// </summary>
    /// <param name="this">The serial port.</param>
    /// <param name="timespan">The timespan at which to notify.</param>
    /// <returns>Observable value.</returns>
    public static IObservable<bool> WhileIsOpen(this SerialPortRx @this, TimeSpan timespan) =>
        Observable.Defer(() => Observable.Create<bool>(obs => {
            var isOpen = Observable.Interval(timespan).CombineLatest(@this.isOpen, (_, b) => b).Where(x => x);
            return isOpen.Subscribe(obs);
        }));
}
