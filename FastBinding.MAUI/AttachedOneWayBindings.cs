using System.Collections.ObjectModel;

namespace FastBindings
{
    public abstract class BaseOneWayBinding
    {
        internal abstract void Initialize(BindableObject view);
    }

    public class OneWayBindingCollection : ObservableCollection<BaseOneWayBinding>
    {
        // Custom behavior can be added here if needed
    }

    public static class AttachedOneWayBindings
    {
        public static readonly BindableProperty CollectionsProperty =
            BindableProperty.CreateAttached(
                "Collections",
                typeof(OneWayBindingCollection),
                typeof(AttachedOneWayBindings),
                default(OneWayBindingCollection),
                propertyChanged: OnCollectionsChanged);

        public static OneWayBindingCollection GetCollections(BindableObject element)
        {
            return (OneWayBindingCollection)element.GetValue(CollectionsProperty);
        }

        public static void SetCollections(BindableObject element, OneWayBindingCollection value)
        {
            element.SetValue(CollectionsProperty, value);
            InitializeBindings(element, value);
        }
        private static void OnCollectionsChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (oldValue is OneWayBindingCollection oldCollection)
            {
                // Optionally handle cleanup of old bindings here
            }

            if (newValue is OneWayBindingCollection newCollection)
            {
                InitializeBindings(bindable, newCollection);
            }
        }


        private static void InitializeBindings(BindableObject bindable, OneWayBindingCollection bindings)
        {
            if (bindings != null)
            {
                foreach (var binding in bindings)
                {
                    binding.Initialize(bindable);
                }
            }
        }
    }
}