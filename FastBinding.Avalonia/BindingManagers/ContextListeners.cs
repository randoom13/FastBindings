using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FastBindings.BindingManagers
{
    /// <summary>A binding told of its control's DataContext changes through <see cref="ContextListeners"/>.</summary>
    internal interface IContextListener
    {
        void DataContextChanged(object? sender, EventArgs e);
    }

    /// <summary>
    /// One DataContextChanged handler per control for all of its bindings, as <see cref="ElementStarter"/> does for their start. Each
    /// binding added a handler of its own: a delegate per binding, and every += and -= on a control with several bindings made the
    /// control's whole delegate again (its invocation list copied). The bindings are told in the order they were added, as the
    /// event told them; one added while the control's change is being told is told from the next change on, and one removed is
    /// not told again — taken out once the telling is over.
    /// </summary>
    internal sealed class ContextListeners
    {
        private static readonly ConditionalWeakTable<Control, ContextListeners> Table = new ConditionalWeakTable<Control, ContextListeners>();

        private readonly List<IContextListener?> _listeners = new List<IContextListener?>(4);
        private bool _subscribed;
        private int _telling;
        private bool _removedWhileTelling;

        public static void Add(Control control, IContextListener listener)
        {
            var listeners = Table.GetValue(control, static _ => new ContextListeners());
            listeners._listeners.Add(listener);
            if (!listeners._subscribed)
            {
                listeners._subscribed = true;
                control.DataContextChanged += listeners.Changed;
            }
        }

        public static void Remove(Control control, IContextListener listener)
        {
            if (!Table.TryGetValue(control, out var listeners))
                return;
            var index = listeners._listeners.IndexOf(listener);
            if (index < 0)
                return;
            if (listeners._telling > 0)
            {
                listeners._listeners[index] = null;
                listeners._removedWhileTelling = true;
                return;
            }
            listeners._listeners.RemoveAt(index);
            listeners.StopIfEmpty(control);
        }

        private void StopIfEmpty(Control control)
        {
            if (_listeners.Count == 0 && _subscribed)
            {
                _subscribed = false;
                control.DataContextChanged -= Changed;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void Changed(object? sender, EventArgs e)
        {
            var count = _listeners.Count;   // those added while telling are told from the next change on
            _telling++;
            try
            {
                for (var index = 0; index < count; index++)
                    _listeners[index]?.DataContextChanged(sender, e);
            }
            finally
            {
                if (--_telling == 0 && _removedWhileTelling)
                {
                    _removedWhileTelling = false;
                    _listeners.RemoveAll(static listener => listener == null);
                    if (sender is Control control)
                        StopIfEmpty(control);
                }
            }
        }
    }
}
