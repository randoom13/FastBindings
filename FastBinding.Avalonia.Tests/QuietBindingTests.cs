using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FastBindings.Tests.Views;
using Xunit;

namespace FastBindings.Tests
{
    /// <summary>
    /// What a platform binding does without complaint is no problem to report either: a null object in the middle of a path (the path
    /// has no value), no value for a property that cannot hold null (it goes back to its default), and a control's binding meeting the
    /// DataContext it inherits before its own arrives (the name is looked up again on the DataContext it ends up with).
    /// </summary>
    [Collection(nameof(DiagnosticsCollection))]
    public class QuietBindingTests
    {
        [AvaloniaFact]
        public void A_null_on_the_path_shows_nothing_and_reports_nothing()
        {
            var log = new CollectingLogger();
            FastBindingsDiagnostics.Logger = log;
            try
            {
                var vm = new QuietModel();
                using var ui = Ui.Show(new Quiet(), vm);
                var text = ui.Find<TextBlock>("NullPath");
                var border = ui.Find<Border>("NullBool");
                Assert.Null(text.Text);
                Assert.True(border.IsVisible);
                Assert.Equal(1, border.Opacity);

                vm.Child = new QuietLeaf { Name = "leaf", Shown = false, Shade = 0.5 };
                Ui.Settle();
                Assert.Equal("leaf", text.Text);
                Assert.False(border.IsVisible);
                Assert.Equal(0.5, border.Opacity);

                vm.Child = null;   // back to no value: the defaults again, not what the last child had
                Ui.Settle();
                Assert.Null(text.Text);
                Assert.True(border.IsVisible);
                Assert.Equal(1, border.Opacity);

                Assert.Empty(log.Issues);
            }
            finally
            {
                FastBindingsDiagnostics.Logger = null;
            }
        }

        [AvaloniaFact]
        public void The_DataContext_a_control_inherits_before_its_own_is_not_reported()
        {
            var log = new CollectingLogger();
            FastBindingsDiagnostics.Logger = log;
            try
            {
                using var ui = Ui.Show(new Quiet(), new QuietModel());
                Ui.Settle();
                Assert.Equal("other", ui.Find<TextBlock>("FromOther").Text);
                Assert.DoesNotContain(log.Issues, i => i.ElementName == "FromOther");
            }
            finally
            {
                FastBindingsDiagnostics.Logger = null;
            }
        }

        [AvaloniaFact]
        public void Unset_puts_the_default_back_and_DoNothing_leaves_the_value()
        {
            var log = new CollectingLogger();
            FastBindingsDiagnostics.Logger = log;
            try
            {
                var vm = new QuietModel();
                using var ui = Ui.Show(new Quiet(), vm);
                var border = ui.Find<Border>("Unset");
                Assert.False(border.IsVisible);
                vm.Visibility = Avalonia.AvaloniaProperty.UnsetValue;
                Ui.Settle();
                Assert.True(border.IsVisible);
                vm.Visibility = false;
                Ui.Settle();
                vm.Visibility = Avalonia.Data.BindingOperations.DoNothing;
                Ui.Settle();
                Assert.False(border.IsVisible);
                Assert.Empty(log.Issues);
            }
            finally
            {
                FastBindingsDiagnostics.Logger = null;
            }
        }

        [AvaloniaFact]
        public void A_view_whose_bindings_start_before_it_is_shown_reports_nothing_once_shown()
        {
            var log = new CollectingLogger();
            FastBindingsDiagnostics.Logger = log;
            try
            {
                var view = new Quiet { DataContext = new QuietModel() };   // its plain bindings start now; the panel's DataContext on showing
                Ui.Settle();
                Assert.Empty(log.Issues);
                using var ui = Ui.Show(view, view.DataContext);
                Ui.Settle();
                Assert.Equal("other", ui.Find<TextBlock>("FromOther").Text);
                Assert.Empty(log.Issues);
            }
            finally
            {
                FastBindingsDiagnostics.Logger = null;
            }
        }
    }
}
