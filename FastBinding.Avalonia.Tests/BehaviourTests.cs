using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia.Headless.XUnit;
using FastBindings.Tests.Views;
using Xunit;

namespace FastBindings.Tests
{
    /// <summary>What a binding does in everyday use.</summary>
    public class BehaviourTests
    {
        [AvaloniaFact]
        public void Values_of_every_kind_arrive_and_follow_changes()
        {
            var vm = new Vm { A = "x", W = 42, V = true };
            using var ui = Ui.Show(new Flat(), vm);
            var text = ui.Find<TextBlock>("Text");
            Assert.Equal(("x", 42.0, true), (text.Text, text.Width, text.IsVisible));

            vm.A = "y";
            vm.W = 7;
            vm.V = false;
            Ui.Settle();
            Assert.Equal(("y", 7.0, false), (text.Text, text.Width, text.IsVisible));
        }

        [AvaloniaFact]
        public void A_DataContext_swap_shows_the_new_view_model()
        {
            using var ui = Ui.Show(new Flat(), new Vm { A = "first" });
            var other = new Vm { A = "second" };
            ui.View.DataContext = other;
            Ui.Settle();
            Assert.Equal("second", ui.Find<TextBlock>("Text").Text);

            other.A = "followed";
            Ui.Settle();
            Assert.Equal("followed", ui.Find<TextBlock>("Text").Text);
        }

        /// <summary>A plain binding gets its value when its control gets a DataContext, before the control is shown.</summary>
        [AvaloniaFact]
        public void A_view_has_its_values_before_it_is_shown()
        {
            var view = new Flat { DataContext = new Vm { A = "early" } };
            Assert.Equal("early", view.FindControl<TextBlock>("Text")!.Text);
        }

        [AvaloniaFact]
        public void A_detached_view_stops_following_its_old_view_model()
        {
            var vm = new Vm { A = "before" };
            using var ui = Ui.Show(new Flat(), vm);
            ui.View.DataContext = null;
            Ui.Settle();
            vm.A = "after";
            Ui.Settle();
            Assert.NotEqual("after", ui.Find<TextBlock>("Text").Text);
        }

        [AvaloniaFact]
        public void A_two_way_text_box_writes_what_is_typed()
        {
            var vm = new Vm { A = "start" };
            using var ui = Ui.Show(new TwoWayText(), vm);
            var box = ui.Find<TextBox>("Box");
            Assert.Equal("start", box.Text);

            box.Text = "typed";
            Ui.Settle();
            Assert.Equal("typed", vm.A);

            vm.A = "from the view-model";
            Ui.Settle();
            Assert.Equal("from the view-model", box.Text);
        }

        [AvaloniaFact]
        public void A_converter_by_name_converts_every_value()
        {
            var vm = new Vm { A = "quiet" };
            using var ui = Ui.Show(new Converted(), vm);
            Assert.Equal("QUIET", ui.Find<TextBlock>("Text").Text);
            vm.A = "loud";
            Ui.Settle();
            Assert.Equal("LOUD", ui.Find<TextBlock>("Text").Text);
        }

        [AvaloniaFact]
        public void A_getter_that_throws_shows_the_fallback_value()
        {
            var vm = new Vm { ThrowOnRead = true };
            using var ui = Ui.Show(new Fallback(), vm);
            Assert.Equal("oops", ui.Find<TextBlock>("Text").Text);

            vm.ThrowOnRead = false;
            Ui.Settle();
            Assert.Equal("fine", ui.Find<TextBlock>("Text").Text);

            vm.ThrowOnRead = true;   // a change that throws, after the start
            Ui.Settle();
            Assert.Equal("oops", ui.Find<TextBlock>("Text").Text);
        }

        [AvaloniaFact]
        public void A_null_value_shows_the_target_null_value()
        {
            var vm = new Vm { Maybe = "something" };
            using var ui = Ui.Show(new NullValue(), vm);
            Assert.Equal("something", ui.Find<TextBlock>("Text").Text);
            vm.Maybe = null;
            Ui.Settle();
            Assert.Equal("none", ui.Find<TextBlock>("Text").Text);
        }

        [AvaloniaFact]
        public void A_change_on_a_worker_thread_reaches_the_control()
        {
            var vm = new Vm { A = "ui" };
            using var ui = Ui.Show(new Flat(), vm);
            Task.Run(() => vm.A = "worker").Wait();
            Ui.Settle();
            Assert.Equal("worker", ui.Find<TextBlock>("Text").Text);
        }

        [AvaloniaFact]
        public void FastBinding_reads_an_IPropertyAccessor_view_model()
        {
            var vm = new AccessorVm { A = "by name" };
            using var ui = Ui.Show(new Accessor(), vm);
            Assert.Equal("by name", ui.Find<TextBlock>("Text").Text);
            vm.A = "changed";
            Ui.Settle();
            Assert.Equal("changed", ui.Find<TextBlock>("Text").Text);
        }

        /// <summary>"." binds the DataContext itself, as Avalonia's {Binding} without a path; a binding of DataContext reads the parent's.</summary>
        [AvaloniaFact]
        public void A_dot_binds_the_DataContext_itself_and_a_DataContext_binding_reads_the_parent()
        {
            var vm = new Vm { Items = new[] { "one", "two" }, Child = new Leaf { A = "leaf" }, Maybe = "from the view" };
            using var ui = Ui.Show(new Identity(), vm);
            var texts = ui.Find<ItemsControl>("List").GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToArray();
            Assert.Equal(new[] { "one", "two" }, texts);

            Assert.Same(vm.Child, ui.Find<StackPanel>("Inner").DataContext);
            Assert.Equal("leaf", ui.Find<TextBlock>("Text").Text);
            Assert.Equal("from the view", ui.Find<TextBlock>("ByBase").Text);
            vm.Child = new Leaf { A = "new" };
            Ui.Settle();
            Assert.Same(vm.Child, ui.Find<StackPanel>("Inner").DataContext);
            Assert.Equal("new", ui.Find<TextBlock>("Text").Text);
            string? InTemplate() => ui.Find<ContentControl>("Host").GetVisualDescendants().OfType<TextBlock>().SingleOrDefault()?.Text;
            Assert.Equal("new", InTemplate());
            vm.Child = new Leaf { A = "newer" };
            Ui.Settle();
            Assert.Equal("newer", InTemplate());
        }

        /// <summary>
        /// A view moved to another view-model (a dock reusing one view for its tabs) writes nothing into either: Avalonia's combo
        /// box clears its selection and sets the new one when the DataContext update ends, and neither is written back.
        /// </summary>
        [AvaloniaFact]
        public void Swapping_the_DataContext_writes_no_selection_back()
        {
            var first = new Picker { Name = "first", Items = new[] { "a", "b" }, Selected = "b" };
            var second = new Picker { Name = "second", Items = new[] { "x", "y" }, Selected = "x" };
            first.Changes.Clear(); second.Changes.Clear();
            using var ui = Ui.Show(new SwapCombo(), first);
            var combo = ui.Find<ComboBox>("Combo");
            ui.View.DataContext = second;
            Ui.Settle();
            Assert.True(first.Changes.Count == 0 && second.Changes.Count == 0, $"first: {string.Join(",", first.Changes)}; second: {string.Join(",", second.Changes)}");
        }

        /// <summary>
        /// Another view's DataContext changing in the same dispatcher turn holds back nothing of this one: a key typed while a
        /// list of suggestions beside it got new items reached the view-model (EngineeringStudio's filter lost the last key
        /// typed at a few frames a second).
        /// </summary>
        [AvaloniaFact]
        public void Another_view_changing_its_DataContext_holds_back_no_typing()
        {
            var vm = new Vm { A = "start" };
            using var text = Ui.Show(new TwoWayText(), vm);
            using var other = Ui.Show(new SwapCombo(), new Picker { Name = "first", Items = new[] { "a" }, Selected = "a" });
            other.View.DataContext = new Picker { Name = "second", Items = new[] { "x" }, Selected = "x" };
            text.Find<TextBox>("Box").Text = "typed";
            Assert.Equal("typed", vm.A);
        }

        /// <summary>A view-model that lives on does not keep a view it was shown in alive.</summary>
        [AvaloniaFact]
        public void A_closed_view_is_collected_while_its_view_model_lives()
        {
            var vm = new Vm();
            var weak = ShowAndClose(vm);
            // Up to ten rounds: on a Windows runner the headless dispatcher sometimes still held the closed view after three
            // (one CI run of several); a view kept alive by its bindings stays alive through all of them.
            for (var i = 0; i < 10 && weak.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Ui.Settle();
            }

            vm.A = "still raising";
            Assert.False(weak.IsAlive);
            GC.KeepAlive(vm);
        }

        private static WeakReference ShowAndClose(Vm vm)
        {
            var view = new Flat();
            using (Ui.Show(view, vm))
            {
            }

            return new WeakReference(view);
        }
    }
}
