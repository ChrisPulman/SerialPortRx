// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO.Ports;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI.Extensions;

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
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>
    /// A string made up from the char values between the start and end chars.
    /// </returns>
    public static IObservable<string> BufferUntil(this IObservable<char> @this, IObservable<char> startsWith, IObservable<char> endsWith, int timeOut, IScheduler? scheduler = null) => Observable.Create<string>(o =>
    {
        var dis = new CompositeDisposable();
        var str = string.Empty;

        var startFound = false;
        var elapsedTime = 0;
        var startsWithL = ' ';
        startsWith.Subscribe(sw =>
        {
            startsWithL = sw;
            elapsedTime = 0;
        }).DisposeWith(dis);
        var endsWithL = ' ';
        var ewd = endsWith.Subscribe(ew => endsWithL = ew).DisposeWith(dis);
        var sub = @this.Subscribe(s =>
        {
            elapsedTime = 0;
            if (startFound || s == startsWithL)
            {
                startFound = true;
                str += s;
                if (s == endsWithL)
                {
                    o.OnNext(str);
                    startFound = false;
                    str = string.Empty;
                }
            }
        }).DisposeWith(dis);

        scheduler ??= new EventLoopScheduler();

        Observable.Interval(TimeSpan.FromMilliseconds(1), scheduler).Subscribe(_ =>
        {
            elapsedTime++;
            if (elapsedTime > timeOut)
            {
                startFound = false;
                str = string.Empty;
                elapsedTime = 0;
            }
        }).DisposeWith(dis);

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
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>
    /// A string made up from the char values between the start and end chars.
    /// </returns>
    public static IObservable<string> BufferUntil(this IObservable<char> @this, IObservable<char> startsWith, IObservable<char> endsWith, IObservable<string> defaultValue, int timeOut, IScheduler? scheduler = null) =>
        Observable.Create<string>(o =>
        {
            var dis = new CompositeDisposable();
            var str = string.Empty;

            var startFound = false;
            var elapsedTime = 0;
            var startsWithL = ' ';
            startsWith.Subscribe(sw =>
            {
                startsWithL = sw;
                elapsedTime = 0;
            }).DisposeWith(dis);
            var endsWithL = ' ';
            endsWith.Subscribe(ew => endsWithL = ew).DisposeWith(dis);
            var defaultValueL = string.Empty;
            defaultValue.Subscribe(dv => defaultValueL = dv).DisposeWith(dis);
            @this.Subscribe(s =>
            {
                elapsedTime = 0;
                if (startFound || s == startsWithL)
                {
                    startFound = true;
                    str += s;
                    if (s == endsWithL)
                    {
                        o.OnNext(str);
                        startFound = false;
                        str = string.Empty;
                    }
                }
            }).DisposeWith(dis);

            scheduler ??= new EventLoopScheduler();

            Observable.Interval(TimeSpan.FromMilliseconds(1), scheduler).Subscribe(_ =>
            {
                elapsedTime++;
                if (elapsedTime > timeOut)
                {
                    o.OnNext(defaultValueL);
                    startFound = false;
                    str = string.Empty;
                    elapsedTime = 0;
                }
            }).DisposeWith(dis);

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
    /// Executes while port is open at the given TimeSpan.
    /// </summary>
    /// <param name="this">The serial port.</param>
    /// <param name="timespan">The timespan at which to notify.</param>
    /// <returns>Observable value.</returns>
    public static IObservable<bool> WhileIsOpen(this SerialPortRx @this, TimeSpan timespan) =>
        Observable.Defer(() => Observable.Create<bool>(obs =>
        {
            var isOpen = Observable.Interval(timespan).CombineLatest(@this.IsOpenObservable, (_, b) => b).Where(x => x);
            return isOpen.Subscribe(obs);
        }));
}
