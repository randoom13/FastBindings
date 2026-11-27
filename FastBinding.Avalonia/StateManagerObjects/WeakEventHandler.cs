using System;
using FastBindings;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace FastBindings.StateManagerObjects
{
    internal class WeakEventHandler<TEventArgs>
    {
        // One compiled "open" delegate per handler method, shared by every subscription of that method: (target, sender, argument)
        // => ((TTarget)target).Method(sender, argument). Compiling one per subscription cost ~0.3-0.8 ms per binding.
        private static readonly ConcurrentDictionary<MethodInfo, Action<object, object, TEventArgs?>> OpenDelegates
            = new ConcurrentDictionary<MethodInfo, Action<object, object, TEventArgs?>>();

        private readonly WeakReference? _targetReference;
        private readonly Action<object, object, TEventArgs?>? _open;
        private readonly EventHandler<TEventArgs>? _static;

        internal WeakEventHandler(EventHandler<TEventArgs> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            Method = handler.Method;
            if (handler.Target == null)
            {
                _static = handler;  // a static method holds nothing to keep alive
                return;
            }
            _targetReference = new WeakReference(handler.Target);
            _open = OpenDelegates.GetOrAdd(handler.Method, CreateOpen);
        }

        /// <summary>
        /// A listener given as its target and an open delegate the caller keeps once (a static field): nothing to look up. Reading
        /// handler.Method is reflection (RuntimeType.GetMethodBase) on every new delegate — the most expensive step of starting a
        /// binding, paid once per binding.
        /// </summary>
        internal WeakEventHandler(object target, Action<object, object, TEventArgs?> open)
        {
            _targetReference = new WeakReference(target);
            _open = open;
        }

        internal MethodInfo? Method { get; }

        private static Action<object, object, TEventArgs?> CreateOpen(MethodInfo method)
        {
            var targetParam = Expression.Parameter(typeof(object), "target");
            var senderParam = Expression.Parameter(typeof(object), "sender");
            var argumentParam = Expression.Parameter(typeof(TEventArgs), "argument");
            var call = Expression.Call(Expression.Convert(targetParam, method.DeclaringType!), method, senderParam, argumentParam);
            return FastBindingsDiagnostics.Compile(Expression.Lambda<Action<object, object, TEventArgs?>>(call, targetParam, senderParam, argumentParam));
        }

        public void Invoke(object sender, TEventArgs? argument)
        {
            if (_static != null)
            {
                _static(sender, argument!);
                return;
            }
            var target = _targetReference!.Target;  // read once: IsAlive then Target could see it collected in between
            if (target != null)
            {
                _open!(target, sender, argument);
            }
        }

        /// <summary>Whether this subscription is the given handler (the same method on the same target).</summary>
        internal bool Is(EventHandler<TEventArgs> handler) =>
            Method != null && Method == handler.Method && (_static != null ? handler.Target == null : ReferenceEquals(_targetReference!.Target, handler.Target));

        /// <summary>Whether this subscription is the given listener.</summary>
        internal bool Is(object target, Action<object, object, TEventArgs?> open) =>
            Method == null && ReferenceEquals(_open, open) && ReferenceEquals(_targetReference!.Target, target);

        public bool IsAlive => _static != null || _targetReference!.IsAlive;
    }
}
