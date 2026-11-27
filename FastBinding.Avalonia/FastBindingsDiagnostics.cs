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
        /// Where FastBindings reports its bindings' problems (null, the default: nothing is made or reported). Set it before views
        /// load: a binding is described — its parameters, target and view — while its XAML loads, and only while a logger is set.
        /// </summary>
        public static Diagnostics.BindingLogger? Logger { get; set; }

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

        public static void Reset()
        {
            _started = _dataContextChanges = _directPushes = _generalPushes = _targetWrites = _sourceWrites = _resubscriptions = _resubscriptionRetries = 0;
            _compiles = _compileTicks = _delegates = 0;
        }

        public static string Report() =>
            $"started {Started}, DataContext changes {DataContextChanges}, direct pushes {DirectPushes}, general pushes {GeneralPushes}, " +
            $"target writes {TargetWrites}, writes back to the source {SourceWrites}, resubscriptions {Resubscriptions} (retries {ResubscriptionRetries}), " +
            $"expression trees compiled {Compiles} in {CompileTime.TotalMilliseconds:N1} ms, accessors made without compiling {Delegates}";
    }
}
