using System;
using System.Windows.Media;
using System.Windows;

namespace WpfExample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var model = DataContext as MainWindowViewModel;
            model!.Color = model!.Color == Colors.Green ? Colors.Red : Colors.Green;
            model!.Mod.MyProperty = Guid.NewGuid().ToString();
        }
    }
}
