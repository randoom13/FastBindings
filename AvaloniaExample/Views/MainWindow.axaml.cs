using Avalonia.Controls;
using AvaloniaExample.ViewModels;
using System;

namespace AvaloniaExample.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {

            var model = GetValue(Control.DataContextProperty) as MainWindowViewModel;
            model!.Mod.MyProperty = Guid.NewGuid().ToString();
        }
    }
}