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
        /// <summary>Listens through IListenable where the manager has it, else through its event.</summary>
        public static void ListenTo(this ISourceStateManager manager, object target, Action<object, object, object?> open, EventHandler<object> handler)
        {
            if (manager is IListenable listenable)
                listenable.Listen(target, open);
            else
                manager.PropertyUpdated += handler;
        }
    }

    internal class WeakEventPublisher<TEventArgs>
    {
        private List<WeakEventHandler<TEventArgs>> _handlers = new List<WeakEventHandler<TEventArgs>>();

        internal void Subscribe(EventHandler<TEventArgs> handler)
        {
            _handlers.RemoveAll(we => !we.IsAlive);
            _handlers.Add(new WeakEventHandler<TEventArgs>(handler));
        }

        /// <summary>Subscribes a listener held weakly: its target and an open delegate kept once by the caller (see WeakEventHandler).</summary>
        internal void Listen(object target, Action<object, object, TEventArgs?> open)
        {
            _handlers.RemoveAll(we => !we.IsAlive);
            _handlers.Add(new WeakEventHandler<TEventArgs>(target, open));
        }

        internal void Unsubscribe(EventHandler<TEventArgs> handler)
        {
            _handlers.RemoveAll(we => !we.IsAlive || we.Is(handler));  // was we.Equals(handler): never true, nothing was removed
        }

        internal void Unsubscribe()
        {
            _handlers.Clear();
        }

        internal void RaiseEvent(object sender, TEventArgs? args)
        {
            // by index: a handler may unsubscribe while it is told, which would break a foreach
            for (var index = 0; index < _handlers.Count; index++)
            {
                _handlers[index].Invoke(sender, args);
            }
        }
    }
}
