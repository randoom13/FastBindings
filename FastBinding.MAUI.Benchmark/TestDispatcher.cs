using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Maui.Dispatching;

namespace MauiBench
{
    /// <summary>
    /// MAUI without a platform has no dispatcher. Each thread gets one when asked: a control made on a test's thread keeps
    /// that thread's, and work dispatched to it from other threads waits in its queue until the test runs it
    /// (<see cref="Ui.Settle"/>). Workers in the tests never make controls, so the ones they would get are never used.
    /// </summary>
    public sealed class TestDispatcher : IDispatcher
    {
        private readonly Thread _thread = Thread.CurrentThread;
        private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

        public bool IsDispatchRequired => Thread.CurrentThread != _thread;

        public bool Dispatch(Action action)
        {
            _queue.Enqueue(action);
            return true;
        }

        public bool DispatchDelayed(TimeSpan delay, Action action) => Dispatch(action);

        public IDispatcherTimer CreateTimer() => throw new NotSupportedException();

        /// <summary>Runs what is queued; returns whether anything ran.</summary>
        public bool RunJobs()
        {
            if (IsDispatchRequired)
                throw new InvalidOperationException("RunJobs on another thread");
            var ran = false;
            while (_queue.TryDequeue(out var action))
            {
                action();
                ran = true;
            }
            return ran;
        }

        [ThreadStatic] private static TestDispatcher? _current;

        public static TestDispatcher ForCurrentThread =>
            _current ??= new TestDispatcher();

        private sealed class Provider : IDispatcherProvider
        {
            public IDispatcher? GetForCurrentThread() => ForCurrentThread;
        }

        [ModuleInitializer]
        internal static void Install() => DispatcherProvider.SetCurrent(new Provider());
    }
}
