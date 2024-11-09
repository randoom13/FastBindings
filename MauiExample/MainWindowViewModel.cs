using FastBindings.Common;
using System.Collections.ObjectModel;
using FastBindings.Interfaces;
using FastBindings;
using System.ComponentModel;
namespace MauiExample
{
    public class MainWindowViewModel //: CommonBaseViewModel<MainWindowViewModel>
        : INotifyPropertyChanged
    {
        // This event is part of the INotifyPropertyChanged interface
        public event PropertyChangedEventHandler? PropertyChanged;
        // Method to raise the PropertyChanged event
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

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
        private Color _color = Colors.Green;

        public Color Color
        {
            get => _color;
            set
            {
                _color = value;
                OnPropertyChanged(nameof(Color));
            }
        }

        private SubscriberExample _example = new SubscriberExample();

        public void Check(object d)
        {

        }

        public void GetMargin(object h)
        {
        }

        public SubscriberExample Subscriber => _example;

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
