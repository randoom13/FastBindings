using FastBindings.Common;
using FastBindings.Interfaces;
using FastBindings.StateManagerObjects;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace WpfExample
{
    public class SubViewModel : CommonBaseViewModel<SubViewModel>, IForwardValueConverter, IBackValueConverter, INotificationFilter
    {
        public void Notify(NotificationArgs args)
        {
            if (!args.IsUpdating)
            {
                TimeSpan difference = DateTime.Now - initialDateTime;
                // Get the total seconds from the difference
                double totalSeconds = difference.TotalSeconds;
                int visibleItemsCount = 40;
                if (minVisiblePosition < _fullText.Length)
                {
                    int length = Math.Min(visibleItemsCount, _fullText.Length - minVisiblePosition);
                    string visibleText = _fullText.Substring(minVisiblePosition, length);
                    minVisiblePosition += visibleItemsCount;
                    Test = Tasks(visibleText);
                }
            }
        }

        private int minVisiblePosition = 0;
        private string _fullText = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Quisque consequat est sit amet nisi tincidunt, vitae tristique purus dictum. Vivamus accumsan, justo a vehicula venenatis, dolor dui blandit libero, at venenatis nisi eros sed nunc. Sed interdum aliquam libero, nec dapibus nunc vehicula a. Suspendisse a mi ut nulla dictum facilisis. Morbi vitae ante eros. Sed eget ligula consectetur, fermentum arcu non, tincidunt est. Nulla facilisi. Integer sit amet venenatis ligula, ut viverra dui. Proin volutpat urna eget libero tempor, a finibus est condimentum. Duis fermentum magna et sollicitudin interdum. Suspendisse potenti.";
        private DateTime initialDateTime;

        private async Task<string> Tasks(string text)
        {
            initialDateTime = DateTime.Now;
            await Task.Delay(1000);
            return text;
        }

        private Task<string>? _test;
        public Task<string> Test
        {
            get
            {
                if (_test == null)
                {
                    _test = Tasks("");
                }
                return _test;
            }
            private set
            {
                _test = value;
                OnPropertyChanged(nameof(Test));
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
            var length = result.Length / 2;
            object[] resu = new object[3];
            resu[0] = new string(result.ToCharArray().Take(length).ToArray());
            resu[1] = new string(result.ToCharArray().Skip(length).ToArray());
            resu[2] = result.Length * 3;
            return resu;
        }
    }
}
