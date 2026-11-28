using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using System.Linq;

namespace FastBindings
{
    public abstract class BaseOneWayBinding : AvaloniaObject
    {
        internal abstract void Initialize(Control control);
    }

    public class OneWayBindingCollection : AvaloniaList<BaseOneWayBinding>
    {
        // Custom behavior can be added here if needed
    }

    public class AttachedOneWayBindings
    {
        public static readonly AttachedProperty<OneWayBindingCollection> CollectionsProperty =
        AvaloniaProperty.RegisterAttached<AttachedOneWayBindings, Control, OneWayBindingCollection>("Collections");

        // The bindings start when the property is set, however it is set: inside a control template Avalonia's XAML compiler sets it
        // directly (BindingPriority.Template), never through SetCollections, so starting them there left them idle.
        static AttachedOneWayBindings()
        {
            CollectionsProperty.Changed.AddClassHandler<Control>((element, e) =>
            {
                if (e.NewValue is not OneWayBindingCollection value) return;
                foreach (var binding in value.OfType<BaseOneWayBinding>())
                {
                    binding.Initialize(element);
                }
            });
        }

        public static OneWayBindingCollection GetCollections(Control element)
        {
            return element.GetValue(CollectionsProperty);
        }

        public static void SetCollections(Control element, OneWayBindingCollection value)
        {
            element.SetValue(CollectionsProperty, value);
        }
    }
}
