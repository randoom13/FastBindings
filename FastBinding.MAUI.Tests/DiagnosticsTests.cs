using Microsoft.Maui.Controls;
using FastBindings.Tests.Views;
using Xunit;

namespace FastBindings.Tests
{
    [CollectionDefinition(nameof(DiagnosticsCollection), DisableParallelization = true)]
    public sealed class DiagnosticsCollection
    {
    }

    /// <summary>FastBindingsDiagnostics counts what bindings do while it is enabled, and nothing while it is not.</summary>
    [Collection(nameof(DiagnosticsCollection))]
    public class DiagnosticsTests
    {
        [Fact]
        public void It_counts_starts_and_pushes_only_while_enabled()
        {
            FastBindingsDiagnostics.Reset();
            FastBindingsDiagnostics.Enabled = true;
            try
            {
                var vm = new Vm();
                using var ui = Ui.Show(new Flat(), vm);
                Assert.True(FastBindingsDiagnostics.Started >= 3);   // three bindings in the view

                var before = FastBindingsDiagnostics.DirectPushes;
                vm.A = "x";
                vm.W = 3;
                Ui.Settle();
                Assert.Equal(before + 2, FastBindingsDiagnostics.DirectPushes);

                FastBindingsDiagnostics.Enabled = false;
                vm.A = "y";
                Ui.Settle();
                Assert.Equal(before + 2, FastBindingsDiagnostics.DirectPushes);
            }
            finally
            {
                FastBindingsDiagnostics.Enabled = false;
                FastBindingsDiagnostics.Reset();
            }
        }

        /// <summary>
        /// MAUI raises BindingContextChanged on a content view's children more than once with the same object; a binding already
        /// reading it neither subscribes again nor writes its value again, and still follows its view-model.
        /// </summary>
        [Fact]
        public void The_same_BindingContext_again_does_not_start_the_binding_again()
        {
            FastBindingsDiagnostics.Reset();
            FastBindingsDiagnostics.Enabled = true;
            try
            {
                var vm = new Vm();
                using var ui = Ui.Show(new Flat(), vm);
                var subscriptions = FastBindingsDiagnostics.Resubscriptions;
                var writes = FastBindingsDiagnostics.TargetWrites;
                Assert.True(subscriptions >= 3);

                var label = ui.Find<Label>("Text");
                label.BindingContext = vm;           // the same object again, set on the label itself
                ui.View.BindingContext = vm;
                Ui.Settle();
                Assert.Equal(subscriptions, FastBindingsDiagnostics.Resubscriptions);
                Assert.Equal(writes, FastBindingsDiagnostics.TargetWrites);

                vm.A = "followed";
                Ui.Settle();
                Assert.Equal("followed", label.Text);
            }
            finally
            {
                FastBindingsDiagnostics.Enabled = false;
                FastBindingsDiagnostics.Reset();
            }
        }
    }
}
