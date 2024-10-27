using FastBindings.Common;
using System.Collections.ObjectModel;

namespace WpfExample
{
    public class MainWindowViewModelV2 : BaseViewModel
    {
        public override object? GetProperty(string propertyName)
        {
            switch (propertyName)
            {
                case nameof(MyProperty1):
                    return MyProperty1;

                case nameof(MyProperty2):
                    return MyProperty2;

                case nameof(Items):
                    return Items;

                case nameof(Mod):
                    return Mod;
            }
            return null;
        }

        public override void SetProperty(string propertyName, object? value)
        {
            switch (propertyName)
            {
                case nameof(MyProperty1):
                    if (value?.GetType() == MyProperty1.GetType())
                        MyProperty1 = (string)value;
                    break;

                case nameof(MyProperty2):
                    if (value?.GetType() == MyProperty2.GetType())
                        MyProperty2 = (string)value;
                    break;

                case nameof(Items):
                    if (value?.GetType() == Items.GetType())
                        Items = (ObservableCollection<Item>)value;
                    break;
            }
        }

        public ObservableCollection<Item> Items { get; set; }
        public MainWindowViewModelV2()
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
        private SubViewModelV2 _mod = new SubViewModelV2();
        public SubViewModelV2 Mod => _mod;
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
