using System.Windows;
using FastBindings.Interfaces;
using FastBindings.Helpers;

namespace FastBindings
{
    internal class DataContextParams
    {
        public string? DataContextSource { get; set; }

        public DependencyObject? GetDataContextTarget(DependencyObject target)
        {
            if (string.IsNullOrEmpty(DataContextSource))
            {
                return target;
            }

            return PropertyPathParser.CalculateSource(target, DataContextSource);
        }

        public IPropertyAccessor? GetDataContext(DependencyObject? target)
        {
            return target?.GetValue(FrameworkElement.DataContextProperty) as IPropertyAccessor;
        }
    }
}
