// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO.Ports;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI.Extensions;
using ReactiveUI.Extensions.Async;

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
    /// Transforms a byte into a single value async observable.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>An async observable char.</returns>
    public static IObservableAsync<char> AsObservableAsync(this byte value) => ObservableAsync.Return(Convert.ToChar(value));

    /// <summary>
    /// transforms a int into a single value Observable.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>An Observable char.</returns>
    public static IObservable<char> AsObservable(this int value) => Observable.Return(Convert.ToChar(value));

    /// <summary>
    /// Transforms an int into a single value async observable.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>An async observable char.</returns>
    public static IObservableAsync<char> AsObservableAsync(this int value) => ObservableAsync.Return(Convert.ToChar(value));

    /// <summary>
    /// transforms a short into a single value Observable.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>An Observable char.</returns>
    public static IObservable<char> AsObservable(this short value) => Observable.Return(Convert.ToChar(value));

    /// <summary>
    /// Transforms a short into a single value async observable.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>An async observable char.</returns>
    public static IObservableAsync<char> AsObservableAsync(this short value) => ObservableAsync.Return(Convert.ToChar(value));

    /// <summary>
    /// Gets the data received after opening a receive port as an async observable.
    /// </summary>
    /// <param name="this">The port.</param>
    /// <returns>An async observable of received byte values.</returns>
    public static IObservableAsync<int> BytesReceivedAsync(this IPortRx @this)
    {
        if (@this == null)
        {
            throw new ArgumentNullException(nameof(@this));
        }

        return @this.BytesReceived.ToObservableAsync();
    }

    /// <summary>
    /// Gets serial characters as an async observable.
    /// </summary>
    /// <param name="this">The serial port.</param>
    /// <returns>An async observable of received characters.</returns>
    public static IObservableAsync<char> DataReceivedAsync(this ISerialPortRx @this)
    {
        if (@this == null)
        {
            throw new ArgumentNullException(nameof(@this));
        }

        return @this.DataReceived.ToObservableAsync();
    }

    /// <summary>
    /// Gets serial bytes as an async observable.
    /// </summary>
    /// <param name="this">The serial port.</param>
    /// <returns>An async observable of received bytes.</returns>
    public static IObservableAsync<byte> DataReceivedBytesAsync(this ISerialPortRx @this)
    {
        if (@this == null)
        {
            throw new ArgumentNullException(nameof(@this));
        }

        return @this.DataReceivedBytes.ToObservableAsync();
    }

    /// <summary>
    /// Gets serial lines as an async observable.
    /// </summary>
    /// <param name="this">The serial port.</param>
    /// <returns>An async observable of received lines.</returns>
    public static IObservableAsync<string> LinesAsync(this ISerialPortRx @this)
    {
        if (@this == null)
        {
            throw new ArgumentNullException(nameof(@this));
        }

        return @this.Lines.ToObservableAsync();
    }

    /// <summary>
    /// Gets serial errors as an async observable.
    /// </summary>
    /// <param name="this">The serial port.</param>
    /// <returns>An async observable of errors.</returns>
    public static IObservableAsync<Exception> ErrorReceivedAsync(this ISerialPortRx @this)
    {
        if (@this == null)
        {
            throw new ArgumentNullException(nameof(@this));
        }

        return @this.ErrorReceived.ToObservableAsync();
    }

    /// <summary>
    /// Gets serial open-state changes as an async observable.
    /// </summary>
    /// <param name="this">The serial port.</param>
    /// <returns>An async observable of open-state changes.</returns>
    public static IObservableAsync<bool> IsOpenObservableAsync(this ISerialPortRx @this)
    {
        if (@this == null)
        {
            throw new ArgumentNullException(nameof(@this));
        }

        return @this.IsOpenObservable.ToObservableAsync();
    }

#if HasWindows
    /// <summary>
    /// Gets serial pin changes as an async observable.
    /// </summary>
    /// <param name="this">The serial port.</param>
    /// <returns>An async observable of pin change events.</returns>
    public static IObservableAsync<SerialPinChangedEventArgs> PinChangedAsync(this ISerialPortRx @this)
    {
        if (@this == null)
        {
            throw new ArgumentNullException(nameof(@this));
        }

        return @this.PinChanged.ToObservableAsync();
    }
#endif

    /// <summary>
    /// Emits the list of available port names whenever it changes as an async observable.
    /// </summary>
    /// <param name="pollInterval">The poll interval.</param>
    /// <param name="pollLimit">The poll limit.</param>
    /// <returns>An async observable of port name arrays.</returns>
    public static IObservableAsync<string[]> PortNamesAsync(int pollInterval = 500, int pollLimit = 0) =>
        SerialPortRx.PortNames(pollInterval, pollLimit).ToObservableAsync();

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
    /// Buffers async Char values until the start and end chars have been found within the timeout period.
    /// </summary>
    /// <param name="this">The this.</param>
    /// <param name="startsWith">The starts with.</param>
    /// <param name="endsWith">The ends with.</param>
    /// <param name="timeOut">The time out.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>
    /// An async observable string made up from the char values between the start and end chars.
    /// </returns>
    public static IObservableAsync<string> BufferUntil(this IObservableAsync<char> @this, IObservableAsync<char> startsWith, IObservableAsync<char> endsWith, int timeOut, IScheduler? scheduler = null) =>
        @this.ToObservable().BufferUntil(startsWith.ToObservable(), endsWith.ToObservable(), timeOut, scheduler).ToObservableAsync();

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
    /// Buffers async Char values until the start and end chars have been found within the timeout
    /// period otherwise returns the default value.
    /// </summary>
    /// <param name="this">The this.</param>
    /// <param name="startsWith">The starts with.</param>
    /// <param name="endsWith">The ends with.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <param name="timeOut">The time out.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>
    /// An async observable string made up from the char values between the start and end chars.
    /// </returns>
    public static IObservableAsync<string> BufferUntil(
        this IObservableAsync<char> @this,
        IObservableAsync<char> startsWith,
        IObservableAsync<char> endsWith,
        IObservableAsync<string> defaultValue,
        int timeOut,
        IScheduler? scheduler = null) =>
        @this.ToObservable()
            .BufferUntil(startsWith.ToObservable(), endsWith.ToObservable(), defaultValue.ToObservable(), timeOut, scheduler)
            .ToObservableAsync();

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

#if HasWindows
    /// <summary>
    /// Monitors the PinChanged observer.
    /// </summary>
    /// <param name="this">The this.</param>
    /// <returns>Observable value.</returns>
    public static IObservable<SerialPinChangedEventArgs> PinChangedObserver(this SerialPort @this) => Observable.FromEvent<SerialPinChangedEventHandler, SerialPinChangedEventArgs>(h => @this.PinChanged += h, h => @this.PinChanged -= h);
#endif

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

    /// <summary>
    /// Executes while port is open at the given TimeSpan via an async observable.
    /// </summary>
    /// <param name="this">The serial port.</param>
    /// <param name="timespan">The timespan at which to notify.</param>
    /// <returns>Async observable value.</returns>
    public static IObservableAsync<bool> WhileIsOpenAsync(this SerialPortRx @this, TimeSpan timespan) =>
        @this.WhileIsOpen(timespan).ToObservableAsync();
}
