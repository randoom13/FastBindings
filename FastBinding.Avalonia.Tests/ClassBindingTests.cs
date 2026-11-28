using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FastBindings.Diagnostics;
using FastBindings.Tests.Views;
using Avalonia.VisualTree;
using Xunit;

namespace FastBindings.Tests
{
    /// <summary>
    /// <see cref="BoolCommonBinding"/> on <c>Classes.name</c>, where Avalonia takes only its own binding object or a compile-time bool:
    /// the class follows the value, on rows of a template too, through converters and dotted paths, and stops with the view.
    /// </summary>
    [Collection(nameof(DiagnosticsCollection))]
    public class ClassBindingTests
    {
        private static Shown<ClassesView> Show(ClassModel vm) => Ui.Show(new ClassesView(), vm);

        [AvaloniaFact]
        public void The_class_follows_the_value()
        {
            var vm = new ClassModel();
            using var ui = Show(vm);
            var border = ui.Find<Border>("Plain");
            Assert.Contains("on", border.Classes);
            vm.IsOn = false;
            Ui.Settle();
            Assert.DoesNotContain("on", border.Classes);
            vm.IsOn = true;
            Ui.Settle();
            Assert.Contains("on", border.Classes);
        }

        [AvaloniaFact]
        public void Classes_of_one_control_are_bound_apart_and_its_own_class_stays()
        {
            var vm = new ClassModel();
            using var ui = Show(vm);
            var border = ui.Find<Border>("Two");
            Assert.Equal(new[] { "fixed", "on" }, border.Classes.OrderBy(c => c).ToArray());
            vm.IsOn = false;
            vm.IsOff = true;
            Ui.Settle();
            Assert.Equal(new[] { "fixed", "off" }, border.Classes.OrderBy(c => c).ToArray());
        }

        [AvaloniaFact]
        public void A_null_on_the_path_takes_the_class_off_and_a_converter_converts()
        {
            var vm = new ClassModel();
            using var ui = Show(vm);
            var deep = ui.Find<Border>("Deep");
            Assert.DoesNotContain("shown", deep.Classes);
            vm.Child = new QuietLeaf { Shown = true };
            Ui.Settle();
            Assert.Contains("shown", deep.Classes);
            vm.Child = null;
            Ui.Settle();
            Assert.DoesNotContain("shown", deep.Classes);

            var converted = ui.Find<Border>("Converted");
            Assert.Contains("on", converted.Classes);   // Not(IsOff = false)
            vm.IsOff = true;
            Ui.Settle();
            Assert.DoesNotContain("on", converted.Classes);
        }

        [AvaloniaFact]
        public void Each_row_of_a_template_follows_its_own_item()
        {
            var vm = new ClassModel();
            using var ui = Show(vm);
            Border RowBorder(int i) => (Border)ui.Find<ItemsControl>("Rows").ContainerFromIndex(i)!.GetVisualDescendants().OfType<Border>().First();
            Assert.DoesNotContain("picked", RowBorder(0).Classes);
            Assert.Contains("picked", RowBorder(1).Classes);
            vm.Rows[0].Picked = true;
            vm.Rows[1].Picked = false;
            Ui.Settle();
            Assert.Contains("picked", RowBorder(0).Classes);
            Assert.DoesNotContain("picked", RowBorder(1).Classes);
        }

        [AvaloniaFact]
        public void A_new_DataContext_sets_the_classes_again_and_none_takes_them_off()
        {
            var vm = new ClassModel();
            using var ui = Show(vm);
            var border = ui.Find<Border>("Plain");
            ui.View.DataContext = new ClassModel { IsOn = false };
            Ui.Settle();
            Assert.DoesNotContain("on", border.Classes);
            vm.IsOn = false;   // the old view-model is no longer followed
            vm.IsOn = true;
            Ui.Settle();
            Assert.DoesNotContain("on", border.Classes);

            // as every CommonBinding: no DataContext, no value — the class goes off and nothing is followed
            var second = (ClassModel)ui.View.DataContext!;
            second.IsOn = true;
            Ui.Settle();
            Assert.Contains("on", border.Classes);
            ui.View.DataContext = null;
            Ui.Settle();
            Assert.DoesNotContain("on", border.Classes);
            second.IsOn = false;
            second.IsOn = true;
            Ui.Settle();
            Assert.DoesNotContain("on", border.Classes);
        }

        [AvaloniaFact]
        public void On_an_ordinary_bool_property_it_is_an_ordinary_binding()
        {
            var vm = new ClassModel();
            using var ui = Show(vm);
            var check = ui.Find<CheckBox>("Enabled");
            Assert.True(check.IsEnabled);
            vm.IsOn = false;
            Ui.Settle();
            Assert.False(check.IsEnabled);
        }

        [AvaloniaFact]
        public void A_name_that_names_nothing_is_reported_with_its_class()
        {
            var log = new CollectingLogger();
            FastBindingsDiagnostics.Logger = log;
            try
            {
                using var ui = Ui.Show(new Faults(), new Faulty());
                Ui.Settle();
                var issue = log.Issues.Single(i => i.ElementName == "MissingClass");
                Assert.Equal(BindingIssueKind.PathNotFound, issue.Kind);
                Assert.Equal("class:on", issue.Binding.TargetProperty);
                Assert.Equal("BoolCommonBinding", issue.Binding.BindingKind);
                Assert.DoesNotContain("on", ui.Find<Border>("MissingClass").Classes);
            }
            finally
            {
                FastBindingsDiagnostics.Logger = null;
            }
        }

        [AvaloniaFact]
        public void The_XAML_compiler_hands_the_control_and_class_name_to_a_bool_extension()
        {
            using var ui = Show(new ClassModel());
            Assert.Same(ui.Find<ClassProbe>("Probe"), ClassTargetProbe.LastTarget);
            Assert.Equal("class:probed", ClassTargetProbe.LastProperty);
            Assert.Contains("probed", ui.Find<ClassProbe>("Probe").Classes);
        }
    }
}
