using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace FastBindings.Tests
{
    /// <summary>Shows a view in a window and runs the dispatcher; closes the window when disposed.</summary>
    public sealed class Shown<TView> : IDisposable where TView : FrameworkElement
    {
        public Shown(TView view, object? dataContext)
        {
            View = view;
            View.DataContext = dataContext;
            Window = new Window { Content = view, Width = 400, Height = 400, ShowActivated = false, ShowInTaskbar = false };
            Window.Show();
            Ui.Settle();
        }

        public TView View { get; }
        public Window Window { get; }

        public T Find<T>(string name) where T : FrameworkElement => View.FindName(name) as T ?? throw new InvalidOperationException(name + " not found");

        public void Dispose()
        {
            Window.Close();
            Ui.Settle();
        }
    }

    public static class Ui
    {
        /// <summary>Runs what the dispatcher holds, down to the idle priority, a few times (posted work may post more).</summary>
        public static void Settle()
        {
            for (var i = 0; i < 5; i++)
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        }

        public static Shown<TView> Show<TView>(TView view, object? dataContext) where TView : FrameworkElement => new Shown<TView>(view, dataContext);
    }
}
