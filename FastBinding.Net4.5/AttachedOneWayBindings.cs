using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Markup;

namespace FastBindings
{
    public abstract class BaseOneWayBinding : MarkupExtension
    {
        internal abstract void Initialize(DependencyObject view);
    }

    public class OneWayBindingCollection : ObservableCollection<BaseOneWayBinding>
    {
        // Custom behavior can be added here if needed
    }

    public static class AttachedOneWayBindings
    {
        // Register an attached DependencyProperty
        public static readonly DependencyProperty CollectionsProperty =
            DependencyProperty.RegisterAttached(
                "Collections",
                typeof(OneWayBindingCollection),
                typeof(AttachedOneWayBindings),
                new PropertyMetadata(null, OnCollectionsChanged));

        // Get the collection from the attached property
        public static OneWayBindingCollection GetCollections(DependencyObject element)
        {
            return (OneWayBindingCollection)element.GetValue(CollectionsProperty);
        }

        // Set the collection to the attached property
        public static void SetCollections(DependencyObject element, OneWayBindingCollection value)
        {
            element.SetValue(CollectionsProperty, value);
            InitializeBindings(element, value);
        }

        // Handle property changes to re-initialize bindings
        private static void OnCollectionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is OneWayBindingCollection oldCollection)
            {
                // Optionally handle cleanup of old bindings here
            }

            if (e.NewValue is OneWayBindingCollection newCollection)
            {
                InitializeBindings(d, newCollection);
            }
        }

        // Initialize the bindings for the collection
        private static void InitializeBindings(DependencyObject element, OneWayBindingCollection bindings)
        {
            if (bindings != null)
            {
                foreach (var binding in bindings)
                {
                    binding.Initialize(element);
                }
            }
        }
    }
}