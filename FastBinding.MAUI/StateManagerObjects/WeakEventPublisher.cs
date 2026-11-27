namespace FastBindings.StateManagerObjects
{

    internal class WeakEventPublisher<TEventArgs>
    {
        private List<WeakEventHandler<TEventArgs>> _handlers = new List<WeakEventHandler<TEventArgs>>();

        internal void Subscribe(EventHandler<TEventArgs> handler)
        {
            _handlers.RemoveAll(we => !we.IsAlive);
            _handlers.Add(new WeakEventHandler<TEventArgs>(handler));
        }

        internal void Unsubscribe(EventHandler<TEventArgs> handler)
        {
            _handlers.RemoveAll(we => !we.IsAlive || we.Is(handler));  // was we.Equals(handler): never true, nothing was removed
        }

        internal void Unsubscribe()
        {
            _handlers.Clear();
        }

        internal void RaiseEvent(object? sender, TEventArgs? args)
        {
            // by index: a handler may unsubscribe while it is told, which would break a foreach
            for (var index = 0; index < _handlers.Count; index++)
            {
                _handlers[index].Invoke(sender, args);
            }
        }
    }
}
