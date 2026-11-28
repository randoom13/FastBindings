using System;
using System.Threading;

namespace FastBindings.Diagnostics
{
    /// <summary>
    /// For tests only (internal, seen by FastBinding.Avalonia.Tests): makes the next value change posted to the UI thread for one
    /// target fail, so the guard round a posted update — which nothing else reaches, every step inside reporting its own
    /// failures — can be shown to report <see cref="BindingIssueKind.UpdateFailed"/> and leave the binding working. Costs a
    /// posted update one read of a static field while nothing is armed.
    /// </summary>
    internal static class TestFaults
    {
        private static object? s_target;
        private static Exception? s_fault;

        /// <summary>The next posted update whose target is <paramref name="target"/> throws <paramref name="fault"/>, once.</summary>
        public static void FailNextUpdate(object target, Exception fault)
        {
            Volatile.Write(ref s_fault, fault);
            Volatile.Write(ref s_target, target);
        }

        public static void ThrowIfArmed(object target)
        {
            if (Volatile.Read(ref s_target) == null || !ReferenceEquals(Volatile.Read(ref s_target), target))
                return;
            if (Interlocked.CompareExchange(ref s_target, null, target) != target)
                return;
            var fault = Interlocked.Exchange(ref s_fault, null);
            if (fault != null)
                throw fault;
        }
    }
}
