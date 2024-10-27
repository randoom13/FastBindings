using System;
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
            model!.Mod.MyProperty = Guid.NewGuid().ToString();
        }
    }
}
