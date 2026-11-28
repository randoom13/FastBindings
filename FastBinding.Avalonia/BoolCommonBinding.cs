using Avalonia;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Concurrent;

namespace FastBindings
{
    /// <summary>
    /// A <see cref="CommonBinding"/> whose markup extension returns <c>bool</c>, for the places Avalonia's XAML compiler takes only its
    /// own binding object or a compile-time <c>bool</c>: <c>Classes.name="{f:BoolCommonBinding IsBlocking}"</c> (a plain CommonBinding
    /// there stops the build with AVLN3000). The compiler calls <see cref="ProvideValue"/> with the control as the target and the
    /// text <c>"class:name"</c> as the property, and sets the class to what it returns; the binding then keeps the class following
    /// its value. It binds, through all of CommonBinding (DataContext, converters, DataContextSource, start and stop, diagnostics), a
    /// hidden bool attached property of its own per class name, whose every change sets the class. One-way: a class has nothing to
    /// write back. A null, unset or failed value takes the class off, as Avalonia's class bindings do.
    /// </summary>
    public class BoolCommonBinding : CommonBinding
    {
        /// <summary>What the XAML compiler gives as the target property of <c>Classes.name</c>: <c>"class:name"</c>.</summary>
        internal const string ClassPrefix = "class:";

        public BoolCommonBinding()
        {
            Mode = BindingMode.OneWay;
        }

        public BoolCommonBinding(string sources) : base(sources)
        {
            Mode = BindingMode.OneWay;
        }

        /// <summary>
        /// Starts the binding on the class (or on an ordinary bool property) and returns its value now: the XAML compiler applies
        /// what this returns after it returns, over anything the binding set already, so it must be the binding's own value.
        /// </summary>
        public new bool ProvideValue(IServiceProvider serviceProvider)
        {
            var valueTarget = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
            if (valueTarget?.TargetObject is StyledElement element && valueTarget.TargetProperty is string text
                && text.StartsWith(ClassPrefix, StringComparison.Ordinal) && text.Length > ClassPrefix.Length)
            {
                var property = ClassProperties.For(text.Substring(ClassPrefix.Length));
                base.ProvideValue(new ClassTarget(serviceProvider, element, property));
                return element.GetValue(property);
            }
            // anywhere else a bool is set: an ordinary binding of that property, and its value now for the compiler to set
            base.ProvideValue(serviceProvider);
            return valueTarget?.TargetObject is AvaloniaObject target && valueTarget.TargetProperty is AvaloniaProperty bound
                && target.GetValue(bound) is true;
        }

        /// <summary>The service provider the compiler gave, with the hidden property of the class as the target property.</summary>
        private sealed class ClassTarget : IServiceProvider, IProvideValueTarget
        {
            private readonly IServiceProvider _inner;

            public ClassTarget(IServiceProvider inner, StyledElement element, AvaloniaProperty property)
            {
                _inner = inner;
                TargetObject = element;
                TargetProperty = property;
            }

            public object TargetObject { get; }
            public object TargetProperty { get; }

            public object? GetService(Type serviceType) =>
                serviceType == typeof(IProvideValueTarget) ? this : _inner.GetService(serviceType);
        }
    }

    /// <summary>One hidden bool attached property per class name, its every change setting the class on its control.</summary>
    internal static class ClassProperties
    {
        private static readonly ConcurrentDictionary<string, AttachedProperty<bool>> s_byName = new ConcurrentDictionary<string, AttachedProperty<bool>>();

        public static AttachedProperty<bool> For(string className) => s_byName.GetOrAdd(className, Register);

        private static AttachedProperty<bool> Register(string className)
        {
            // named as the compiler names the target ("class:name"), so a binding's description says which class it sets
            var property = AvaloniaProperty.RegisterAttached<BoolCommonBinding, StyledElement, bool>(BoolCommonBinding.ClassPrefix + className);
            property.Changed.AddClassHandler<StyledElement>((element, change) => element.Classes.Set(className, change.GetNewValue<bool>()));
            return property;
        }
    }
}
