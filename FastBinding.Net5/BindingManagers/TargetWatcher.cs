using System;
using System.Windows;
using System.Windows.Data;

namespace FastBindings.BindingManagers
{
    /// <summary>
    /// Tells a two-way binding that its target property changed. It was DependencyPropertyDescriptor.AddValueChanged, which keeps
    /// the target and the handler in a static table until RemoveValueChanged — never called, so a view with a two-way FastBinding
    /// stayed in memory for the life of the process. This is a small object with a one-way WPF Binding from the target's
    /// property: the target, the binding manager and the watcher only reference each other, and go together.
    /// </summary>
    internal sealed class TargetWatcher : DependencyObject
    {
        private static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            "Value", typeof(object), typeof(TargetWatcher), new PropertyMetadata(null, OnValueChanged));

        private readonly Action<object?> _changed;
        private bool _started;

        private TargetWatcher(Action<object?> changed) => _changed = changed;

        public static TargetWatcher Watch(DependencyObject target, DependencyProperty property, Action<object?> changed)
        {
            var watcher = new TargetWatcher(changed);
            BindingOperations.SetBinding(watcher, ValueProperty,
                new Binding { Source = target, Path = new PropertyPath(property), Mode = System.Windows.Data.BindingMode.OneWay });
            watcher._started = true;   // the binding's first value is the target's current one, not a change
            return watcher;
        }

        public void Stop() => BindingOperations.ClearBinding(this, ValueProperty);

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var watcher = (TargetWatcher)d;
            if (watcher._started)
                watcher._changed(e.NewValue);
        }
    }
}
