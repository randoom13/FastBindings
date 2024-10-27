using System.Collections.Generic;

namespace FastBindings.StateManagerObjects
{

    internal class WeakEventPublisher<TEventArgs>
    {
        private List<WeakEventHandler<TEventArgs>> _handlers = new List<WeakEventHandler<TEventArgs>>();

        internal void Subscribe(EventHandlerEx<TEventArgs> handler)
        {
            _handlers.RemoveAll(we => !we.IsAlive);
            _handlers.Add(new WeakEventHandler<TEventArgs>(handler));
        }

        internal void Unsubscribe(EventHandlerEx<TEventArgs> handler)
        {
            _handlers.RemoveAll(we => !we.IsAlive || we.Equals(handler));
        }

        internal void Unsubscribe()
        {
            _handlers.Clear();
        }

        internal void RaiseEvent(object sender, TEventArgs args)
        {
            foreach (var handler in _handlers)
            {
                handler.Invoke(sender, args);
            }
        }
    }
}
