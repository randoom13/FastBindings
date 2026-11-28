using System;
using System.Linq;
using Avalonia;
using Avalonia.Data;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Markup.Xaml.XamlIl.Runtime;
using Avalonia.Styling;

namespace FastBindings.Helpers
{
    /// <summary>
    /// What a binding's <c>ProvideValue</c> hands the XAML compiler so that nothing is set: a FastBinding writes its values itself.
    /// Outside a control template that is <see cref="AvaloniaProperty.UnsetValue"/>. Inside one, Avalonia sets properties with
    /// <c>BindingPriority.Template</c>, and its compiled setter there takes only a binding or a value of the property's type — not
    /// <c>UnsetValue</c> (an <see cref="System.InvalidCastException"/>) — so it gets one shared binding whose value is
    /// <see cref="BindingOperations.DoNothing"/>, which leaves the property as it is. <see cref="Binding"/> is the one binding type
    /// public with this name in Avalonia 11 and 12 alike.
    /// </summary>
    internal static class SetsNothing
    {
        private static readonly Binding InTemplate = new Binding { Source = BindingOperations.DoNothing };

        /// <summary>
        /// The value to return from <c>ProvideValue</c>. An element built from a style's template (a <c>ControlTheme</c> or
        /// <c>Style</c> setting <c>Template</c>) has that style in the XAML compiler's parent stack; its templated parent is not set
        /// yet when the binding is provided.
        /// </summary>
        public static object For(IServiceProvider serviceProvider) =>
            serviceProvider.GetService(typeof(IAvaloniaXamlIlParentStackProvider)) is IAvaloniaXamlIlParentStackProvider stack
            && stack.Parents.Any(p => p is StyleBase || p is ControlTemplate)
                ? InTemplate
                : AvaloniaProperty.UnsetValue;
    }
}
