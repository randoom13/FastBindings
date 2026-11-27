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
    }
}
