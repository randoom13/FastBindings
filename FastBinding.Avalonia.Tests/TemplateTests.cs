using System.Linq;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FastBindings.Tests.Views;
using Xunit;

namespace FastBindings.Tests
{
    /// <summary>A templated control whose look is a <c>ControlTheme</c>.</summary>
    public sealed class Panelled : TemplatedControl
    {
    }

    public sealed class PanelledModel : Observable
    {
        private string _title = "First";
        private bool _shown = true;
        public string Title { get => _title; set => Set(ref _title, value); }
        public bool Shown { get => _shown; set => Set(ref _shown, value); }
        public ICommand Go { get; } = new Command();
        public int Pasted { get; private set; }
        public void NotePasted() => Pasted++;

        private sealed class Command : ICommand
        {
            public event System.EventHandler? CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object? parameter) => true;
            public void Execute(object? parameter) { }
        }
    }

    /// <summary>
    /// Bindings inside a control template: Avalonia's compiled setter there (<c>BindingPriority.Template</c>) takes a binding or a value
    /// of the property's type, so a binding gives it one that sets nothing, then writes its values as everywhere else.
    /// </summary>
    public class TemplateTests
    {
        [AvaloniaFact]
        public void Bindings_in_a_control_template_build_and_follow_the_view_model()
        {
            var vm = new PanelledModel();
            using var ui = Ui.Show(new Templated(), vm);
            var parts = ui.View.GetVisualDescendants().OfType<Control>().ToList();
            var title = parts.OfType<TextBlock>().Single(t => t.Name == "PART_Title");
            var go = parts.OfType<Button>().Single(b => b.Name == "PART_Go");
            Assert.Equal("First", title.Text);
            Assert.Same(vm.Go, go.Command);
            Assert.True(go.IsVisible);

            vm.Title = "Second";
            vm.Shown = false;
            Ui.Settle();
            Assert.Equal("Second", title.Text);
            Assert.False(go.IsVisible);
        }

        [AvaloniaFact]
        public void An_event_binding_in_a_control_template_calls_its_method()
        {
            var vm = new PanelledModel();
            using var ui = Ui.Show(new Templated(), vm);
            var field = ui.View.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "PART_Field");
            Assert.Equal(0, vm.Pasted);

            field.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(TextBox.PastingFromClipboardEvent));
            Ui.Settle();
            Assert.Equal(1, vm.Pasted);
        }
    }
}
