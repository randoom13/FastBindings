using System;
using System.Linq.Expressions;

namespace FastBindings.StateManagerObjects
{
    internal class WeakEventHandler<TEventArgs>
    {
        private readonly WeakReference _targetReference;
        private readonly Action<object, TEventArgs?> _delegate;

        internal WeakEventHandler(EventHandler<TEventArgs> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            _targetReference = new WeakReference(handler.Target);
            // Define the parameters for the expression
            var senderParam = Expression.Parameter(typeof(object), "sender");
            var argumentParam = Expression.Parameter(typeof(TEventArgs?), "argument");

            // Create the method call expression
            var target = Expression.Convert(Expression.Property(Expression.Constant(_targetReference), "Target"), handler.Target!.GetType());
            var methodCall = Expression.Call(
                target,
                handler.Method,
                senderParam,
                argumentParam);
            _delegate = Expression.Lambda<Action<object, TEventArgs?>>(methodCall, senderParam, argumentParam).Compile();
        }

        public void Invoke(object sender, TEventArgs? argument)
        {
            if (IsAlive)
            {
                _delegate(sender, argument);
            }
        }

        public bool IsAlive => _targetReference.IsAlive;
    }
}
