using System;
using System.Threading.Tasks;
using FastBindings.Interfaces;
using FastBindings.Helpers;

namespace FastBindings.BindingManagers
{

    internal class ConvertersProxy<T>
    {
        public ConvertersProxy(IViewModelTreeHelper<T> tree)
        {
            _viewModelTree = tree ?? throw new ArgumentException(nameof(tree));
        }
        private readonly IViewModelTreeHelper<T> _viewModelTree;

        public string? ConverterPath { get; set; }
        public string? ConverterName { get; set; }

        public IValueConverterBase? Converter { get; set; }

        private TConverter? CalculateConverter<TConverter>(object? dataContext)
    where TConverter : IValueConverterBase
        {
            if (dataContext == null || string.IsNullOrEmpty(ConverterName))
                return default;

            if (string.IsNullOrEmpty(ConverterPath))
            {
                if (!(dataContext is IValueConverterBase))
                    if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.PathNotFound,
                        detail: $"ConverterName '{ConverterName}' without ConverterPath or Converter: the DataContext ({dataContext.GetType().Name}) is no converter");
                return dataContext is TConverter ? (TConverter)dataContext : default;
            }

            var result = _viewModelTree.GetFinalViewModel(dataContext, ConverterPath);
            if (!(result is IValueConverterBase))   // nothing at ConverterPath converts (one direction missing is no problem)
                if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.PathNotFound,
                    detail: $"ConverterPath '{ConverterPath}' on {dataContext.GetType().Name} is {(result == null ? "null" : result.GetType().Name + ", no converter")}");
            return result is TConverter ? (TConverter)result : default;
        }

        // each in a method of its own, so the closure is made only when there is a converter (C# makes a captured local's closure on
        // entry to the method that declares it: every DataContext change paid one with no converter at all)
        private static Func<ConverterArgs, object> Forward(IForwardValueConverter converter, string? name) => args => converter.Convert(name, args);
        private static Func<ConverterArgs, Task<object>> AsyncForward(IAsyncForwardValueConverter converter, string? name) => args => converter.AsyncConvert(name, args);
        private static Func<ConverterBackArgs, object?[]> Back(IBackValueConverter converter, string? name) => args => converter.ConvertBack(name, args);

        internal Func<ConverterArgs, object>? FindForwardConverter(object? dataContext)
        {
            var converter = Converter != null ?
                Converter as IForwardValueConverter : CalculateConverter<IForwardValueConverter>(dataContext);

            return converter != null ? Forward(converter, ConverterName) : null;
        }

        internal Func<ConverterArgs, Task<object>>? FindAsyncForwardConverter(object? dataContext)
        {
            var converter = Converter != null ?
                Converter as IAsyncForwardValueConverter : CalculateConverter<IAsyncForwardValueConverter>(dataContext);

            return converter != null ? AsyncForward(converter, ConverterName) : null;
        }

        internal Func<ConverterBackArgs, object?[]>? FindBackConverter(object? dataContext)
        {
            var converter = Converter != null ?
               Converter as IBackValueConverter : CalculateConverter<IBackValueConverter>(dataContext);

            return converter != null ? Back(converter, ConverterName) : null;
        }
    }
}
