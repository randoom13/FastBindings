using System;
using Microsoft.Maui.Controls;

namespace FastBindings.Tests
{
    /// <summary>Shows a view in a window (MAUI raises Loaded then) and runs the dispatcher; closes the window when disposed.</summary>
    public sealed class Shown<TView> : IDisposable where TView : View
    {
        public Shown(TView view, object? bindingContext)
        {
            View = view;
            View.BindingContext = bindingContext;
            Page = new ContentPage { Content = view };
            Window = new Window(Page);
            Ui.Settle();
        }

        public TView View { get; }
        public ContentPage Page { get; }
        public Window Window { get; }

        public T Find<T>(string name) where T : Element => View.FindByName<T>(name) ?? throw new InvalidOperationException(name + " not found");

        /// <summary>Takes the page out of its window (MAUI raises Unloaded).</summary>
        public void Dispose() => Window.Page = null;
    }

    public static class Ui
    {
        /// <summary>Runs what the dispatcher holds, a few times (posted work may post more).</summary>
        public static void Settle()
        {
            for (var i = 0; i < 5; i++)
                TestDispatcher.ForCurrentThread.RunJobs();
        }

        public static Shown<TView> Show<TView>(TView view, object? bindingContext) where TView : View => new Shown<TView>(view, bindingContext);
    }
}
