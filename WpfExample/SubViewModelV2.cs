using FastBindings.Common;
using FastBindings.Interfaces;
using FastBindings.StateManagerObjects;
using System.Linq;
using System.Windows;

namespace WpfExample
{
    public class SubViewModelV2 : BaseViewModel, IForwardValueConverter, IBackValueConverter
    {
        public override object? GetProperty(string propertyName)
        {
            switch (propertyName)
            {
                case nameof(MyProperty):
                    return MyProperty;

                case nameof(MyProperty2):
                    return MyProperty2;
            }
            return null;
        }

        public override void SetProperty(string propertyName, object? value)
        {
            switch (propertyName)
            {
                case nameof(MyProperty):
                    if (value?.GetType() == MyProperty.GetType())
                        MyProperty = (string)value;
                    break;

                case nameof(MyProperty2):
                    if (value?.GetType() == MyProperty2.GetType())
                        MyProperty2 = (string)value;
                    break;
            }
        }

        // Example property
        private string _myProperty = "zxcv";

        public string MyProperty
        {
            get => _myProperty;
            set
            {
                if (_myProperty != value)
                {
                    _myProperty = value;
                    OnPropertyChanged(nameof(MyProperty)); // Notify that MyProperty has changed
                }
            }
        }

        // Example property
        private string _myProperty2 = "zxwcv";

        public string MyProperty2
        {
            get => _myProperty2;
            set
            {
                if (_myProperty2 != value)
                {
                    _myProperty2 = value;
                    OnPropertyChanged(nameof(MyProperty2)); // Notify that MyProperty has changed
                }
            }
        }
        private string ArgumentCoverter(object? arg)
        {
            var args = arg as EventInfoArgs;
            if (args == null)
                return arg?.ToString() ?? string.Empty;

            return (args.EventArgs as SizeChangedEventArgs)?.NewSize.Height.ToString() ?? string.Empty;
        }

        public object Convert(string? converterName, ConverterArgs args)
        {
            var vals = args.Values?.Select(ArgumentCoverter).OfType<string>().ToList()
                ?? Enumerable.Empty<string>();
            return string.Concat(vals);
        }

        public object?[] ConvertBack(string? converterName, ConverterBackArgs args)
        {
            var result = args.Value?.ToString() ?? string.Empty;
            var len = result.Length / 2;
            object[] resu = new object[3];
            resu[0] = new string(result.ToCharArray().Take(len).ToArray());
            resu[1] = new string(result.ToCharArray().Skip(len).ToArray());
            resu[2] = result.Length * 3;
            return resu;
        }
    }
}
