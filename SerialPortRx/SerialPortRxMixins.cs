namespace CP.IO.Ports
{
    using System;
    using System.IO.Ports;
    using System.Reactive;
    using System.Reactive.Concurrency;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;
    using System.Text;
    using Reactive.Bindings.Extensions;

    /// <summary>
    /// Serial Port Rx Mixins
    /// </summary>
    public static class SerialPortRxMixins
    {
        /// <summary>
        /// transforms a byte into a single value Observable.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>An Observable char</returns>
        public static IObservable<char> AsObservable(this byte value) => Observable.Return(Convert.ToChar(value));

        /// <summary>
        /// transforms a int into a single value Observable.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>An Observable char</returns>
        public static IObservable<char> AsObservable(this int value) => Observable.Return(Convert.ToChar(value));

        /// <summary>
        /// transforms a short into a single value Observable.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>An Observable char</returns>
        public static IObservable<char> AsObservable(this short value) => Observable.Return(Convert.ToChar(value));

        /// <summary>
        /// Buffers the Char values until the start and end chars have been found within the timeout period.
        /// </summary>
        /// <param name="this">The this.</param>
        /// <param name="startsWith">The starts with.</param>
        /// <param name="endsWith">The ends with.</param>
        /// <param name="timeOut">The time out.</param>
        /// <returns>A string made up from the char values between the start and end chars</returns>
        public static IObservable<string> BufferUntil(this IObservable<char> @this, IObservable<char> startsWith, IObservable<char> endsWith, int timeOut) => Observable.Create<string>(o =>
                {
                    var dis = new CompositeDisposable();
                    var sb = new StringBuilder();

                    bool startFound = false;
                    int elapsedTime = 0;
                    char startsWithL = ' ';
                    startsWith.Subscribe(sw =>
                    {
                        startsWithL = sw;
                        elapsedTime = 0;
                    }).AddTo(dis);
                    char endsWithL = ' ';
                    var ewd = endsWith.Subscribe(ew => endsWithL = ew).AddTo(dis);
                    var sub = @this.Subscribe(s =>
                    {
                        elapsedTime = 0;
                        if (startFound || s == startsWithL)
                        {
                            startFound = true;
                            sb.Append(s);
                            if (s == endsWithL)
                            {
                                o.OnNext(sb.ToString());
                                startFound = false;
                                sb.Clear();
                            }
                        }
                    }).AddTo(dis);
                    Observable.Interval(TimeSpan.FromMilliseconds(1)).Subscribe(_ =>
                    {
                        elapsedTime++;
                        if (elapsedTime > timeOut)
                        {
                            startFound = false;
                            sb.Clear();
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
        /// <returns>A string made up from the char values between the start and end chars</returns>
        public static IObservable<string> BufferUntil(this IObservable<char> @this, IObservable<char> startsWith, IObservable<char> endsWith, IObservable<string> defaultValue, int timeOut) => Observable.Create<string>(o =>
        {
            var dis = new CompositeDisposable();
            var sb = new StringBuilder();

            bool startFound = false;
            int elapsedTime = 0;
            char startsWithL = ' ';
            startsWith.Subscribe(sw =>
            {
                startsWithL = sw;
                elapsedTime = 0;
            }).AddTo(dis);
            char endsWithL = ' ';
            endsWith.Subscribe(ew => endsWithL = ew).AddTo(dis);
            string defaultValueL = string.Empty;
            defaultValue.Subscribe(dv => defaultValueL = dv).AddTo(dis);
            @this.Subscribe(s =>
           {
               elapsedTime = 0;
               if (startFound || s == startsWithL)
               {
                   startFound = true;
                   sb.Append(s);
                   if (s == endsWithL)
                   {
                       o.OnNext(sb.ToString());
                       startFound = false;
                       sb.Clear();
                   }
               }
           }).AddTo(dis);

            Observable.Interval(TimeSpan.FromMilliseconds(1)).Subscribe(_ =>
            {
                elapsedTime++;
                if (elapsedTime > timeOut)
                {
                    o.OnNext(defaultValueL);
                    startFound = false;
                    sb.Clear();
                    elapsedTime = 0;
                }
            }).AddTo(dis);

            return dis;
        });

        /// <summary>
        /// Fors the each.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="this">The this.</param>
        /// <returns></returns>
        public static IObservable<T> ForEach<T>(this IObservable<T[]> @this) =>
                                            Observable.Create<T>(obs =>
            {
                return @this.Subscribe(list =>
                {
                    foreach (var item in list)
                    {
                        if (item != null)
                            obs.OnNext(item);
                    }
                }, obs.OnError, obs.OnCompleted);
            });

        /// <summary>
        /// <para>Repeats the source observable sequence until it successfully terminates.</para>
        /// <para>This is same as Retry().</para>
        /// </summary>
        public static IObservable<TSource> OnErrorRetry<TSource>(this IObservable<TSource> source) => source.Retry();

        /// <summary>
        /// When caught exception, do onError action and repeat observable sequence.
        /// </summary>
        public static IObservable<TSource> OnErrorRetry<TSource, TException>(this IObservable<TSource> source, Action<TException> onError)
where TException : Exception => source.OnErrorRetry(onError, TimeSpan.Zero);

        /// <summary>
        /// When caught exception, do onError action and repeat observable sequence after delay time.
        /// </summary>
        public static IObservable<TSource> OnErrorRetry<TSource, TException>(this IObservable<TSource> source, Action<TException> onError, TimeSpan delay)
where TException : Exception => source.OnErrorRetry(onError, int.MaxValue, delay);

        /// <summary>
        /// When caught exception, do onError action and repeat observable sequence during within retryCount.
        /// </summary>
        public static IObservable<TSource> OnErrorRetry<TSource, TException>(this IObservable<TSource> source, Action<TException> onError, int retryCount)
where TException : Exception => source.OnErrorRetry(onError, retryCount, TimeSpan.Zero);

        /// <summary>
        /// When caught exception, do onError action and repeat observable sequence after delay time
        /// during within retryCount.
        /// </summary>
        public static IObservable<TSource> OnErrorRetry<TSource, TException>(this IObservable<TSource> source, Action<TException> onError, int retryCount, TimeSpan delay)
where TException : Exception => source.OnErrorRetry(onError, retryCount, delay, Scheduler.Default);

        /// <summary>
        /// When caught exception, do onError action and repeat observable sequence after delay
        /// time(work on delayScheduler) during within retryCount.
        /// </summary>
        public static IObservable<TSource> OnErrorRetry<TSource, TException>(this IObservable<TSource> source, Action<TException> onError, int retryCount, TimeSpan delay, IScheduler delayScheduler)
where TException : Exception =>
            Observable.Defer(() =>
                {
                    var dueTime = (delay.Ticks < 0) ? TimeSpan.Zero : delay;
                    var empty = Observable.Empty<TSource>();
                    var count = 0;

                    IObservable<TSource> self = null;
                    self = source.Catch((TException ex) =>
                    {
                        onError(ex);

                        return (++count < retryCount)
                            ? (dueTime == TimeSpan.Zero)
                                ? self.SubscribeOn(Scheduler.CurrentThread)
                                : empty.Delay(dueTime, delayScheduler).Concat(self).SubscribeOn(Scheduler.CurrentThread)
                            : Observable.Throw<TSource>(ex);
                    });
                    return self;
                });

        /// <summary>
        /// Executes while port is open at the given TimeSpan.
        /// </summary>
        /// <param name="this">The serial port.</param>
        /// <param name="timespan">The timespan at which to notify.</param>
        /// <returns></returns>
        public static IObservable<bool> WhileIsOpen(this SerialPortRx @this, TimeSpan timespan) =>
            Observable.Defer(() => Observable.Create<bool>(obs =>
              {
                  var isOpen = from a in Observable.Interval(timespan)
                               from b in @this.IsOpen.DistinctUntilChanged()
                               select b;
                  return isOpen.Where(x => x).Subscribe(obs);
              }));

        /// <summary>
        /// Monitors the received observer.
        /// </summary>
        /// <param name="this">The this.</param>
        /// <returns></returns>
        internal static IObservable<EventPattern<SerialDataReceivedEventArgs>> DataReceivedObserver(this SerialPort @this) => Observable.FromEventPattern<SerialDataReceivedEventHandler, SerialDataReceivedEventArgs>(h => @this.DataReceived += h, h => @this.DataReceived -= h);

        /// <summary>
        /// Monitors the Errors observer.
        /// </summary>
        /// <param name="this">The this.</param>
        /// <returns></returns>
        internal static IObservable<EventPattern<SerialErrorReceivedEventArgs>> ErrorReceivedObserver(this SerialPort @this) => Observable.FromEventPattern<SerialErrorReceivedEventHandler, SerialErrorReceivedEventArgs>(h => @this.ErrorReceived += h, h => @this.ErrorReceived -= h);
    }
}