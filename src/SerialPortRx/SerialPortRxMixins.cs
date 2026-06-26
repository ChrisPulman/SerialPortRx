// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IO.Ports;

/// <summary>Provides serial port reactive extension methods.</summary>
public static class SerialPortRxMixins
{
    /// <summary>Provides classic character observable extensions.</summary>
    /// <param name="source">The source observable receiver.</param>
    extension(IObservable<char> source)
    {
        /// <summary>Buffers the Char values until the start and end chars have been found within the timeout period.</summary>
        /// <param name="startsWith">The starts with.</param>
        /// <param name="endsWith">The ends with.</param>
        /// <param name="timeOut">The time out.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>
        /// A string made up from the char values between the start and end chars.
        /// </returns>
        public IObservable<string> BufferUntil(IObservable<char> startsWith, IObservable<char> endsWith, int timeOut, IScheduler? scheduler = null) => Observable.Create<string>(o =>
        {
            var dis = new CompositeDisposable();
            var str = string.Empty;

            var startFound = false;
            var elapsedTime = 0;
            var startsWithL = ' ';
            dis.Add(startsWith.Subscribe(sw =>
            {
                startsWithL = sw;
                elapsedTime = 0;
            }));
            var endsWithL = ' ';
            dis.Add(endsWith.Subscribe(ew => endsWithL = ew));
            dis.Add(source.Subscribe(s =>
            {
                elapsedTime = 0;
                if (!startFound && s != startsWithL)
                {
                    return;
                }

                startFound = true;
                str += s;
                if (s != endsWithL)
                {
                    return;
                }

                o.OnNext(str);
                startFound = false;
                str = string.Empty;
            }));

            scheduler ??= DefaultScheduler.Instance;

            dis.Add(Observable.Interval(TimeSpan.FromMilliseconds(1), scheduler).Subscribe(_ =>
            {
                elapsedTime++;
                if (elapsedTime <= timeOut)
                {
                    return;
                }

                startFound = false;
                str = string.Empty;
                elapsedTime = 0;
            }));

            return dis;
        });

        /// <summary>
        /// Buffers the Char values until the start and end chars have been found within the timeout
        /// period otherwise returns the default value.
        /// </summary>
        /// <param name="startsWith">The starts with.</param>
        /// <param name="endsWith">The ends with.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <param name="timeOut">The time out.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>
        /// A string made up from the char values between the start and end chars.
        /// </returns>
        public IObservable<string> BufferUntil(IObservable<char> startsWith, IObservable<char> endsWith, IObservable<string> defaultValue, int timeOut, IScheduler? scheduler = null) =>
            Observable.Create<string>(o =>
            {
                var dis = new CompositeDisposable();
                var str = string.Empty;

                var startFound = false;
                var elapsedTime = 0;
                var startsWithL = ' ';
                dis.Add(startsWith.Subscribe(sw =>
                {
                    startsWithL = sw;
                    elapsedTime = 0;
                }));
                var endsWithL = ' ';
                dis.Add(endsWith.Subscribe(ew => endsWithL = ew));
                var defaultValueL = string.Empty;
                dis.Add(defaultValue.Subscribe(dv => defaultValueL = dv));
                dis.Add(source.Subscribe(s =>
                {
                    elapsedTime = 0;
                    if (!startFound && s != startsWithL)
                    {
                        return;
                    }

                    startFound = true;
                    str += s;
                    if (s != endsWithL)
                    {
                        return;
                    }

                    o.OnNext(str);
                    startFound = false;
                    str = string.Empty;
                }));

                scheduler ??= DefaultScheduler.Instance;

                dis.Add(Observable.Interval(TimeSpan.FromMilliseconds(1), scheduler).Subscribe(_ =>
                {
                    elapsedTime++;
                    if (elapsedTime <= timeOut)
                    {
                        return;
                    }

                    o.OnNext(defaultValueL);
                    startFound = false;
                    str = string.Empty;
                    elapsedTime = 0;
                }));

                return dis;
            });
    }

    /// <summary>Provides async character observable extensions.</summary>
    /// <param name="source">The source async observable receiver.</param>
    extension(IObservableAsync<char> source)
    {
        /// <summary>Buffers async Char values until the start and end chars have been found within the timeout period.</summary>
        /// <param name="startsWith">The starts with.</param>
        /// <param name="endsWith">The ends with.</param>
        /// <param name="timeOut">The time out.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>
        /// An async observable string made up from the char values between the start and end chars.
        /// </returns>
        public IObservableAsync<string> BufferUntil(IObservableAsync<char> startsWith, IObservableAsync<char> endsWith, int timeOut, IScheduler? scheduler = null) =>
            source.ToObservable().BufferUntil(startsWith.ToObservable(), endsWith.ToObservable(), timeOut, scheduler).ToObservableAsync();

        /// <summary>
        /// Buffers async Char values until the start and end chars have been found within the timeout
        /// period otherwise returns the default value.
        /// </summary>
        /// <param name="startsWith">The starts with.</param>
        /// <param name="endsWith">The ends with.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <param name="timeOut">The time out.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>
        /// An async observable string made up from the char values between the start and end chars.
        /// </returns>
        public IObservableAsync<string> BufferUntil(
            IObservableAsync<char> startsWith,
            IObservableAsync<char> endsWith,
            IObservableAsync<string> defaultValue,
            int timeOut,
            IScheduler? scheduler = null) =>
            source.ToObservable()
                .BufferUntil(startsWith.ToObservable(), endsWith.ToObservable(), defaultValue.ToObservable(), timeOut, scheduler)
                .ToObservableAsync();
    }

    /// <summary>Provides generic port extensions.</summary>
    /// <param name="port">The port receiver.</param>
    extension(IPortRx port)
    {
        /// <summary>Gets the data received after opening a receive port as an async observable.</summary>
        /// <returns>An async observable of received byte values.</returns>
        public IObservableAsync<int> BytesReceivedAsync()
        {
            if (port is null)
            {
                throw new ArgumentNullException(nameof(port));
            }

            return port.BytesReceived.ToObservableAsync();
        }
    }

    /// <summary>Provides serial port interface extensions.</summary>
    /// <param name="serialPort">The serial port receiver.</param>
    extension(ISerialPortRx serialPort)
    {
        /// <summary>Gets serial characters as an async observable.</summary>
        /// <returns>An async observable of received characters.</returns>
        public IObservableAsync<char> DataReceivedAsync()
        {
            if (serialPort is null)
            {
                throw new ArgumentNullException(nameof(serialPort));
            }

            return serialPort.DataReceived.ToObservableAsync();
        }

        /// <summary>Gets serial bytes as an async observable.</summary>
        /// <returns>An async observable of received bytes.</returns>
        public IObservableAsync<byte> DataReceivedBytesAsync()
        {
            if (serialPort is null)
            {
                throw new ArgumentNullException(nameof(serialPort));
            }

            return serialPort.DataReceivedBytes.ToObservableAsync();
        }

        /// <summary>Gets serial lines as an async observable.</summary>
        /// <returns>An async observable of received lines.</returns>
        public IObservableAsync<string> LinesAsync()
        {
            if (serialPort is null)
            {
                throw new ArgumentNullException(nameof(serialPort));
            }

            return serialPort.Lines.ToObservableAsync();
        }

        /// <summary>Gets serial errors as an async observable.</summary>
        /// <returns>An async observable of errors.</returns>
        public IObservableAsync<Exception> ErrorReceivedAsync()
        {
            if (serialPort is null)
            {
                throw new ArgumentNullException(nameof(serialPort));
            }

            return serialPort.ErrorReceived.ToObservableAsync();
        }

        /// <summary>Gets serial open-state changes as an async observable.</summary>
        /// <returns>An async observable of open-state changes.</returns>
        public IObservableAsync<bool> IsOpenObservableAsync()
        {
            if (serialPort is null)
            {
                throw new ArgumentNullException(nameof(serialPort));
            }

            return serialPort.IsOpenObservable.ToObservableAsync();
        }

#if HasWindows
        /// <summary>Gets serial pin changes as an async observable.</summary>
        /// <returns>An async observable of pin change events.</returns>
        public IObservableAsync<SerialPinChangedEventArgs> PinChangedAsync()
        {
            if (serialPort is null)
            {
                throw new ArgumentNullException(nameof(serialPort));
            }

            return serialPort.PinChanged.ToObservableAsync();
        }
#endif
    }

    /// <summary>Provides serial port event extensions.</summary>
    /// <param name="serialPort">The serial port receiver.</param>
    extension(SerialPort serialPort)
    {
        /// <summary>Monitors the received observer.</summary>
        /// <returns>Observable value.</returns>
        public IObservable<EventPattern<SerialDataReceivedEventArgs>> DataReceivedObserver() => Observable.FromEventPattern<SerialDataReceivedEventHandler, SerialDataReceivedEventArgs>(h => serialPort.DataReceived += h, h => serialPort.DataReceived -= h);

        /// <summary>Monitors the Errors observer.</summary>
        /// <returns>Observable value.</returns>
        public IObservable<EventPattern<SerialErrorReceivedEventArgs>> ErrorReceivedObserver() => Observable.FromEventPattern<SerialErrorReceivedEventHandler, SerialErrorReceivedEventArgs>(h => serialPort.ErrorReceived += h, h => serialPort.ErrorReceived -= h);

#if HasWindows
        /// <summary>Monitors the PinChanged observer.</summary>
        /// <returns>Observable value.</returns>
        public IObservable<SerialPinChangedEventArgs> PinChangedObserver() => Observable.Create<SerialPinChangedEventArgs>(observer =>
        {
            SerialPinChangedEventHandler handler = (_, args) => observer.OnNext(args);
            serialPort.PinChanged += handler;
            return Disposable.Create(() => serialPort.PinChanged -= handler);
        });
#endif
    }

    /// <summary>Provides concrete serial port reactive extensions.</summary>
    /// <param name="serialPort">The serial port receiver.</param>
    extension(SerialPortRx serialPort)
    {
        /// <summary>Executes while port is open at the given TimeSpan.</summary>
        /// <param name="timespan">The timespan at which to notify.</param>
        /// <returns>Observable value.</returns>
        public IObservable<bool> WhileIsOpen(TimeSpan timespan) =>
            Observable.Defer(() => Observable.Create<bool>(obs =>
            {
                var isOpen = Observable.Interval(timespan).CombineLatest(serialPort.IsOpenObservable, (_, b) => b).Where(x => x);
                return isOpen.Subscribe(obs);
            }));

        /// <summary>Executes while port is open at the given TimeSpan via an async observable.</summary>
        /// <param name="timespan">The timespan at which to notify.</param>
        /// <returns>Async observable value.</returns>
        public IObservableAsync<bool> WhileIsOpenAsync(TimeSpan timespan) =>
            serialPort.WhileIsOpen(timespan).ToObservableAsync();
    }

    /// <summary>Provides byte conversion extensions.</summary>
    /// <param name="value">The byte receiver.</param>
    extension(byte value)
    {
        /// <summary>Transforms a byte into a single value observable.</summary>
        /// <returns>An observable char.</returns>
        public IObservable<char> AsObservable() => Observable.Return(Convert.ToChar(value));

        /// <summary>Transforms a byte into a single value async observable.</summary>
        /// <returns>An async observable char.</returns>
        public IObservableAsync<char> AsObservableAsync() => ObservableAsync.Return(Convert.ToChar(value));
    }

    /// <summary>Provides integer conversion extensions.</summary>
    /// <param name="value">The integer receiver.</param>
    extension(int value)
    {
        /// <summary>Transforms an int into a single value observable.</summary>
        /// <returns>An observable char.</returns>
        public IObservable<char> AsObservable() => Observable.Return(Convert.ToChar(value));

        /// <summary>Transforms an int into a single value async observable.</summary>
        /// <returns>An async observable char.</returns>
        public IObservableAsync<char> AsObservableAsync() => ObservableAsync.Return(Convert.ToChar(value));
    }

    /// <summary>Provides short conversion extensions.</summary>
    /// <param name="value">The short receiver.</param>
    extension(short value)
    {
        /// <summary>Transforms a short into a single value observable.</summary>
        /// <returns>An observable char.</returns>
        public IObservable<char> AsObservable() => Observable.Return(Convert.ToChar(value));

        /// <summary>Transforms a short into a single value async observable.</summary>
        /// <returns>An async observable char.</returns>
        public IObservableAsync<char> AsObservableAsync() => ObservableAsync.Return(Convert.ToChar(value));
    }

    /// <summary>Emits the list of available port names whenever it changes as an async observable.</summary>
    /// <param name="pollInterval">The poll interval.</param>
    /// <param name="pollLimit">The poll limit.</param>
    /// <returns>An async observable of port name arrays.</returns>
    public static IObservableAsync<string[]> PortNamesAsync(int pollInterval = 500, int pollLimit = 0) =>
        SerialPortRx.PortNames(pollInterval, pollLimit).ToObservableAsync();
}
