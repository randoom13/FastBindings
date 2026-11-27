using System;
using Avalonia.Controls;
using Avalonia.Threading;

namespace FastBindings.Tests
{
    /// <summary>Shows a view in a window and runs the dispatcher; closes the window when disposed.</summary>
    public sealed class Shown<TView> : IDisposable where TView : Control
    {
        public Shown(TView view, object? dataContext)
        {
            View = view;
            View.DataContext = dataContext;
            Window = new Window { Content = view, Width = 400, Height = 400 };
            Window.Show();
            Ui.Settle();
        }

        public TView View { get; }
        public Window Window { get; }

        public T Find<T>(string name) where T : Control => View.FindControl<T>(name) ?? throw new InvalidOperationException(name + " not found");

        public void Dispose() => Window.Close();
    }

    public static class Ui
    {
        /// <summary>Runs what the dispatcher holds, a few times (posted work may post more).</summary>
        public static void Settle()
        {
            for (var i = 0; i < 5; i++)
                Dispatcher.UIThread.RunJobs();
        }

        public static Shown<TView> Show<TView>(TView view, object? dataContext) where TView : Control => new Shown<TView>(view, dataContext);
    }
}
