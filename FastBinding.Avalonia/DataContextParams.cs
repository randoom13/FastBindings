using Avalonia;
using Avalonia.Controls;
using FastBindings.Helpers;
using FastBindings.Interfaces;

namespace FastBindings
{
    internal class DataContextParams
    {
        public string? DataContextSource { get; set; }

        public AvaloniaObject GetDataContextTarget(AvaloniaObject target)
        {
            if (string.IsNullOrEmpty(DataContextSource))
            {
                return target;
            }

            return PropertyPathParser.CalculateSource(target, DataContextSource) ?? target;
        }

        public IPropertyAccessor? GetDataContext(AvaloniaObject target)
        {
            return target?.GetValue(Control.DataContextProperty) as IPropertyAccessor;
        }
    }
}
