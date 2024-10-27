using FastBindings.Common;
using System.Collections.ObjectModel;

namespace WpfExample
{
    public class MainWindowViewModel : CommonBaseViewModel<MainWindowViewModel>
    {
        public ObservableCollection<Item> Items { get; set; }
        public MainWindowViewModel()
        {
            Items = new ObservableCollection<Item>
        {
            new Item { Text = "Item 1" },
            new Item { Text = "Item 2" },
            new Item { Text = "Item 3" },
            new Item { Text = "Item 4" }
        };
        }

        public string Greeting => "Welcome to Avalonia!";
        private string _myProperty1 = "111";
        private SubViewModel _mod = new SubViewModel();
        public SubViewModel Mod => _mod;
        public string MyProperty1
        {
            get => _myProperty1;
            set
            {
                if (_myProperty1 != value)
                {
                    _myProperty1 = value;
                    OnPropertyChanged(nameof(MyProperty1));
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
    }
}
