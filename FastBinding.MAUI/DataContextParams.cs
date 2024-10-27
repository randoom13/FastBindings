using FastBindings.Interfaces;
using FastBindings.Helpers;

namespace FastBindings
{
    internal class DataContextParams
    {
        public string? DataContextSource { get; set; }

        public BindableObject? GetDataContextTarget(BindableObject target)
        {
            if (string.IsNullOrEmpty(DataContextSource))
            {
                return target;
            }

            return PropertyPathParser.CalculateSource(target, DataContextSource);
        }

        public IPropertyAccessor? GetDataContext(BindableObject? target)
        {
            return target?.BindingContext as IPropertyAccessor;
        }
    }
}
