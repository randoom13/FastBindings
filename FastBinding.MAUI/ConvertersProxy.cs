using FastBindings.Helpers;
using FastBindings.Interfaces;

namespace FastBindings
{
    internal class ConvertersProxy
    {
        public string? ConverterPath { get; set; }
        public string? ConverterName { get; set; }

        public IValueConverterBase? Converter { get; set; }

        private TConverter? CalculateConverter<TConverter>(object? dataContext)
    where TConverter : IValueConverterBase
        {
            if (string.IsNullOrEmpty(ConverterName) || dataContext == null)
                return default;

            if (string.IsNullOrEmpty(ConverterPath))
                return dataContext is TConverter ? (TConverter)dataContext : default;

            var result = ViewModelTreeHelper.GetFinalViewModel(dataContext as IPropertyAccessor, ConverterPath);
            return result is TConverter ? (TConverter)result : default;
        }

        internal Func<ConverterArgs, object?>? FindForwardConverter(object? dataContext)
        {
            var converter = Converter != null ?
                Converter as IForwardValueConverter : CalculateConverter<IForwardValueConverter>(dataContext);
            
            if (converter != null)
                return args => converter.Convert(ConverterName, args);
            
            return null;
        }

        internal Func<ConverterArgs, Task<object>>? FindAsyncForwardConverter(object? dataContext)
        {
            var converter = Converter != null ?
                Converter as IAsyncForwardValueConverter : CalculateConverter<IAsyncForwardValueConverter>(dataContext);

            if (converter != null)
                return args => converter.AsyncConvert(ConverterName, args);

            return null;
        }


        internal Func<ConverterBackArgs, object?[]>? FindBackConverter(object? dataContext)
        {
            var converter = Converter != null ?
               Converter as IBackValueConverter : CalculateConverter<IBackValueConverter>(dataContext);

            if (converter != null)
                return args => converter.ConvertBack(ConverterName, args);

            return null;
        }
    }
}
