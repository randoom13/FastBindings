using System;
using System.Collections.Generic;

namespace FastBindings.StateManagerObjects
{
    /// <summary>A source state manager a binding manager can listen to without making an event handler (WeakEventPublisher.Listen).</summary>
    internal interface IListenable
    {
        void Listen(object target, Action<object, object, object?> open);
    }

    internal static class Listening
    {
        /// <summary>
        /// Listens through IListenable where the manager has it, else through its event (a handler made only then: every source
        /// of this library listens without one, and making the delegate for each binding only to drop it cost an object each).
        /// </summary>
        public static void ListenTo<TTarget>(this ISourceStateManager manager, TTarget target, Action<object, object, object?> open, Func<TTarget, EventHandler<object>> handler)
            where TTarget : class
        {
            if (manager is IListenable listenable)
                listenable.Listen(target, open);
            else
                manager.PropertyUpdated += handler(target);
        }
    }

    internal class WeakEventPublisher<TEventArgs>
    {
        private List<WeakEventHandler<TEventArgs>>? _handlers;

        // The owner's listener (Listen): the binding that made the source and holds it, the only one who listens to it. Held
        // strongly — the source lives exactly as long as that binding, so a weak reference back kept nothing alive that would
        // otherwise go — and without a list: a WeakReference, a handler and a list fewer per binding.
        private object? _listener;
        private Action<object, object, TEventArgs?>? _open;
        private List<WeakEventHandler<TEventArgs>> Handlers => _handlers ??= new List<WeakEventHandler<TEventArgs>>();

        internal void Subscribe(EventHandler<TEventArgs> handler)
        {
            Handlers.RemoveAll(we => !we.IsAlive);
            Handlers.Add(new WeakEventHandler<TEventArgs>(handler));
        }

        /// <summary>Subscribes a listener held weakly: its target and an open delegate kept once by the caller (see WeakEventHandler).</summary>
        internal void Listen(object target, Action<object, object, TEventArgs?> open)
        {
            if (_listener == null)
            {
                _listener = target;
                _open = open;
                return;
            }
            Handlers.RemoveAll(we => !we.IsAlive);
            Handlers.Add(new WeakEventHandler<TEventArgs>(target, open));
        }

        internal void Unsubscribe(EventHandler<TEventArgs> handler)
        {
            _handlers?.RemoveAll(we => !we.IsAlive || we.Is(handler));  // was we.Equals(handler): never true, nothing was removed
        }

        internal void Unsubscribe()
        {
            _handlers?.Clear();
            _listener = null;
            _open = null;
        }

        internal void RaiseEvent(object sender, TEventArgs? args)
        {
            // by index: a handler may unsubscribe while it is told, which would break a foreach
            var listener = _listener;
            if (listener != null)
                _open!(listener, sender, args);
            var handlers = _handlers;
            if (handlers == null)
                return;
            for (var index = 0; index < handlers.Count; index++)
            {
                handlers[index].Invoke(sender, args);
            }
        }
    }
}
