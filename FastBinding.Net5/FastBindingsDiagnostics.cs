using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace FastBindings
{
    /// <summary>
    /// Counts what bindings do, for measuring an app (off by default; when off each count is one predictable branch). Set
    /// <see cref="Enabled"/>, run the work, read the counts, <see cref="Reset"/>. Counted on every thread.
    /// </summary>
    public static class FastBindingsDiagnostics
    {
        public static bool Enabled { get; set; }

        /// <summary>
        /// Told of every value a two-way binding writes back into its source while <see cref="Enabled"/>: the target object, the
        /// target property's name and the value — to see which bindings write back, and what.
        /// </summary>
        public static System.Action<object, string, object?>? SourceWriteObserved { get; set; }

        internal static void ObserveSourceWrite(object target, string property, object? value)
        {
            if (Enabled)
                SourceWriteObserved?.Invoke(target, property, value);
        }

        /// <summary>Bindings started (Initialize).</summary>
        public static long Started => _started;
        /// <summary>DataContext changes a binding handled.</summary>
        public static long DataContextChanges => _dataContextChanges;
        /// <summary>Value changes written through the direct, typed path.</summary>
        public static long DirectPushes => _directPushes;
        /// <summary>Value changes through the general path (converters, other threads, types the direct path does not take).</summary>
        public static long GeneralPushes => _generalPushes;
        /// <summary>Values written into the target by any path.</summary>
        public static long TargetWrites => _targetWrites;
        /// <summary>Values a two-way binding wrote back into the source (the view-model).</summary>
        public static long SourceWrites => _sourceWrites;
        /// <summary>Subscriptions along a path made again (DataContext or an object in the middle of the path changed).</summary>
        public static long Resubscriptions => _resubscriptions;
        /// <summary>Subscriptions made again because the path changed while it was being subscribed (another thread).</summary>
        public static long ResubscriptionRetries => _resubscriptionRetries;

        /// <summary>Expression trees compiled (getters, setters, pushers, event handlers), and the time that took.</summary>
        public static long Compiles => _compiles;
        public static System.TimeSpan CompileTime => System.TimeSpan.FromTicks(System.Threading.Interlocked.Read(ref _compileTicks) * System.TimeSpan.TicksPerSecond / System.Diagnostics.Stopwatch.Frequency);

        /// <summary>Accessors made as delegates to the property's own methods, without compiling anything.</summary>
        public static long Delegates => _delegates;

        internal static long _compiles, _compileTicks, _delegates;

        /// <summary>Compiles a lambda, counting it and its time while <see cref="Enabled"/>.</summary>
        internal static TDelegate Compile<TDelegate>(System.Linq.Expressions.Expression<TDelegate> lambda) where TDelegate : System.Delegate
        {
            if (!Enabled)
                return lambda.Compile();
            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            var compiled = lambda.Compile();
            Interlocked.Add(ref _compileTicks, System.Diagnostics.Stopwatch.GetTimestamp() - start);
            Interlocked.Increment(ref _compiles);
            return compiled;
        }

        internal static long _started, _dataContextChanges, _directPushes, _generalPushes, _targetWrites, _sourceWrites, _resubscriptions, _resubscriptionRetries;

        internal static void Count(ref long counter)
        {
            if (Enabled)
                Interlocked.Increment(ref counter);
        }

        // ---- time spent inside FastBindings, measured where WPF (or a view-model) calls into it ----

        /// <summary>In ProvideValue: XAML creating the binding (and starting it when its control already has a DataContext).</summary>
        public static System.TimeSpan ProvideValueTime => Ticks(_provideValueTicks);
        /// <summary>Starting a binding later, on Loaded (bindings that do not start in ProvideValue).</summary>
        public static System.TimeSpan LateStartTime => Ticks(_lateStartTicks);
        /// <summary>Handling DataContext changes (reading the values, subscribing, writing the targets).</summary>
        public static System.TimeSpan DataContextTime => Ticks(_dataContextTicks);
        /// <summary>Handling a view-model's PropertyChanged: every binding told of it, its value read and written into the target.</summary>
        public static System.TimeSpan SourceChangeTime => Ticks(_sourceChangeTicks);
        /// <summary>Reading a path's end again on the UI thread after a change on another thread.</summary>
        public static System.TimeSpan RefreshTime => Ticks(_refreshTicks);
        /// <summary>A two-way binding writing its target's change into the source.</summary>
        public static System.TimeSpan SourceWriteTime => Ticks(_sourceWriteTicks);
        /// <summary>All of the above, each moment counted once (a call made from inside another is not added again).</summary>
        public static System.TimeSpan TotalTime => Ticks(_totalTicks);

        internal static long _provideValueTicks, _lateStartTicks, _dataContextTicks, _sourceChangeTicks, _refreshTicks, _sourceWriteTicks, _totalTicks;

        [System.ThreadStatic] private static int _depth;

        private static System.TimeSpan Ticks(long ticks) =>
            System.TimeSpan.FromTicks(Interlocked.Read(ref ticks) * System.TimeSpan.TicksPerSecond / System.Diagnostics.Stopwatch.Frequency);

        /// <summary>Starts timing an entry into FastBindings (0 while not <see cref="Enabled"/>: nothing is measured).</summary>
        internal static long Begin()
        {
            if (!Enabled)
                return 0;
            _depth++;
            return System.Diagnostics.Stopwatch.GetTimestamp();
        }

        /// <summary>Adds the time since <paramref name="start"/> to <paramref name="ticks"/>, and to the total when it is the outermost entry.</summary>
        internal static void End(ref long ticks, long start)
        {
            if (start == 0)
                return;
            var elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - start;
            Interlocked.Add(ref ticks, elapsed);
            if (--_depth == 0)
                Interlocked.Add(ref _totalTicks, elapsed);
        }

        public static void Reset()
        {
            _provideValueTicks = _lateStartTicks = _dataContextTicks = _sourceChangeTicks = _refreshTicks = _sourceWriteTicks = _totalTicks = 0;
            _started = _dataContextChanges = _directPushes = _generalPushes = _targetWrites = _sourceWrites = _resubscriptions = _resubscriptionRetries = 0;
            _compiles = _compileTicks = _delegates = 0;
        }

        public static string Report() =>
            $"started {Started}, DataContext changes {DataContextChanges}, direct pushes {DirectPushes}, general pushes {GeneralPushes}, " +
            $"target writes {TargetWrites}, writes back to the source {SourceWrites}, resubscriptions {Resubscriptions} (retries {ResubscriptionRetries}), " +
            $"expression trees compiled {Compiles} in {CompileTime.TotalMilliseconds:N1} ms, accessors made without compiling {Delegates}";
    }
}
