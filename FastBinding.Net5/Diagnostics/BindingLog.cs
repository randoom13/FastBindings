using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace FastBindings.Diagnostics
{
    /// <summary>
    /// The one place FastBindings reports a problem through: every error path calls it (a source scan in the tests keeps it so).
    /// Nothing is made unless <see cref="FastBindingsDiagnostics.Logger"/> is set. The binding a problem belongs to is passed by
    /// the code that knows it, or is the one running on this thread (<see cref="Enter"/>): the helpers deep in a path read a
    /// property without knowing which binding asked.
    /// </summary>
    internal static class BindingLog
    {
        [ThreadStatic] private static BindingDescription? t_current;
        [ThreadStatic] private static object? t_target;

        /// <summary>Counts per logger: a new logger starts counting afresh.</summary>
        private static readonly ConditionalWeakTable<BindingLogger, ConcurrentDictionary<(int, BindingIssueKind, Type?), int>> s_counts =
            new ConditionalWeakTable<BindingLogger, ConcurrentDictionary<(int, BindingIssueKind, Type?), int>>();

        internal static bool IsOn => FastBindingsDiagnostics.Logger != null;

        /// <summary>Marks the binding running on this thread until the scope is disposed. Free while no logger is set.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Scope Enter(BindingDescription? binding, object? target = null)
        {
            if (binding == null || FastBindingsDiagnostics.Logger == null)
                return default;
            var scope = new Scope(t_current, t_target, true);
            t_current = binding;
            t_target = target;
            return scope;
        }

        internal readonly struct Scope : IDisposable
        {
            private readonly BindingDescription? _previous;
            private readonly object? _previousTarget;
            private readonly bool _entered;
            internal Scope(BindingDescription? previous, object? previousTarget, bool entered) { _previous = previous; _previousTarget = previousTarget; _entered = entered; }
            public void Dispose()
            {
                if (_entered)
                {
                    t_current = _previous;
                    t_target = _previousTarget;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Report(BindingIssueKind kind, Exception? exception = null, string? detail = null, object? target = null,
            object? value = null, Type? dataContextType = null, BindingDescription? binding = null)
        {
            var logger = FastBindingsDiagnostics.Logger;
            if (logger != null)
                ReportTo(logger, kind, exception, detail, target, value, dataContextType, binding);
        }

        private static void ReportTo(BindingLogger logger, BindingIssueKind kind, Exception? exception, string? detail, object? target,
            object? value, Type? dataContextType, BindingDescription? binding)
        {
            try
            {
                if (binding == null || ReferenceEquals(binding, t_current))
                    target ??= t_target;
                binding ??= t_current ?? BindingDescription.Unknown;
                dataContextType ??= (target switch
                {
                    System.Windows.FrameworkElement element => element.DataContext,
                    System.Windows.FrameworkContentElement content => content.DataContext,
                    _ => null,
                })?.GetType();
                var counts = s_counts.GetValue(logger, _ => new ConcurrentDictionary<(int, BindingIssueKind, Type?), int>());
                var count = counts.AddOrUpdate((binding.Id, kind, exception?.GetType()), 1, (_, n) => n + 1);
                if (!IsReported(count))
                    return;
                var issue = new BindingIssue(kind, binding, target, dataContextType, value, exception, detail, count);
                switch (issue.Severity)
                {
                    case BindingIssueSeverity.Error: logger.Error(issue); break;
                    case BindingIssueSeverity.Warning: logger.Warning(issue); break;
                    default: logger.Trace(issue); break;
                }
            }
            catch (Exception)
            {
                // a logger never breaks a binding
            }
        }

        /// <summary>The 1st, 10th, 100th, 1 000th … occurrence.</summary>
        private static bool IsReported(int count)
        {
            while (count >= 10 && count % 10 == 0)
                count /= 10;
            return count == 1;
        }
    }
}
