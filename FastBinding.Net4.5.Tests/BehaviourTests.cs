using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using FastBindings.Tests.Views;
using Xunit;

namespace FastBindings.Tests
{
    /// <summary>What a binding does in everyday use.</summary>
    public class BehaviourTests
    {
        [WpfFact]
        public void Values_of_every_kind_arrive_and_follow_changes()
        {
            var vm = new Vm { A = "x", W = 42, V = true };
            using var ui = Ui.Show(new Flat(), vm);
            var text = ui.Find<TextBlock>("Text");
            Assert.Equal(("x", 42.0, true), (text.Text, text.Width, text.IsEnabled));

            vm.A = "y";
            vm.W = 7;
            vm.V = false;
            Ui.Settle();
            Assert.Equal(("y", 7.0, false), (text.Text, text.Width, text.IsEnabled));
        }

        [WpfFact]
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
        [WpfFact]
        public void A_view_has_its_values_before_it_is_shown()
        {
            var view = new Flat { DataContext = new Vm { A = "early" } };
            Assert.Equal("early", ((TextBlock)view.FindName("Text")).Text);
        }

        [WpfFact]
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

        [WpfFact]
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

        [WpfFact]
        public void A_converter_by_name_converts_every_value()
        {
            var vm = new Vm { A = "quiet" };
            using var ui = Ui.Show(new Converted(), vm);
            Assert.Equal("QUIET", ui.Find<TextBlock>("Text").Text);
            vm.A = "loud";
            Ui.Settle();
            Assert.Equal("LOUD", ui.Find<TextBlock>("Text").Text);
        }

        [WpfFact]
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

        [WpfFact]
        public void A_null_value_shows_the_target_null_value()
        {
            var vm = new Vm { Maybe = "something" };
            using var ui = Ui.Show(new NullValue(), vm);
            Assert.Equal("something", ui.Find<TextBlock>("Text").Text);
            vm.Maybe = null;
            Ui.Settle();
            Assert.Equal("none", ui.Find<TextBlock>("Text").Text);
        }

        [WpfFact]
        public void A_change_on_a_worker_thread_reaches_the_control()
        {
            var vm = new Vm { A = "ui" };
            using var ui = Ui.Show(new Flat(), vm);
            var worker = Task.Run(() => vm.A = "worker");
            while (!worker.IsCompleted)
                Ui.Settle();   // 1.1.3 made the worker wait for the UI thread (Dispatcher.Invoke): keep it running
            Ui.Settle();
            Assert.Equal("worker", ui.Find<TextBlock>("Text").Text);
        }

        [WpfFact]
        public void FastBinding_reads_an_IPropertyAccessor_view_model()
        {
            var vm = new AccessorVm { A = "by name" };
            using var ui = Ui.Show(new Accessor(), vm);
            Assert.Equal("by name", ui.Find<TextBlock>("Text").Text);
            vm.A = "changed";
            Ui.Settle();
            Assert.Equal("changed", ui.Find<TextBlock>("Text").Text);
        }

        /// <summary>A view-model that lives on does not keep a view it was shown in alive.</summary>
        [WpfFact]
        public void A_closed_view_is_collected_while_its_view_model_lives()
        {
            var vm = new Vm();
            var weak = ShowAndClose(vm);
            for (var i = 0; i < 3; i++)
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

        /// <summary>A view with a two-way binding is collected too (DependencyPropertyDescriptor.AddValueChanged kept it).</summary>
        [WpfFact]
        public void A_closed_view_with_a_two_way_binding_is_collected()
        {
            var vm = new Vm();
            var weak = ShowAndCloseTwoWay(vm);
            for (var i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Ui.Settle();
            }

            Assert.False(weak.IsAlive);
            GC.KeepAlive(vm);
        }

        private static WeakReference ShowAndCloseTwoWay(Vm vm)
        {
            var view = new TwoWayText();
            using (Ui.Show(view, vm))
            {
            }

            return new WeakReference(view);
        }

        /// <summary>
        /// A change on a worker thread never waits for the UI thread: 1.1.3 called Dispatcher.Invoke, so a UI thread waiting for
        /// that worker (Task.Wait, a lock) deadlocked.
        /// </summary>
        [WpfFact]
        public void A_change_on_a_worker_thread_does_not_wait_for_the_UI_thread()
        {
            var vm = new Vm { A = "ui" };
            using var ui = Ui.Show(new Converted(), vm);    // a converter: the general path
            using var plain = Ui.Show(new Flat(), vm);      // the direct path
            var worker = Task.Run(() => vm.A = "worker");
            var finished = worker.Wait(TimeSpan.FromSeconds(5));   // the UI thread is not pumping meanwhile
            while (!worker.IsCompleted)
                Ui.Settle();
            Assert.True(finished, "the worker waited for the UI thread");
            Ui.Settle();
            Assert.Equal("WORKER", ui.Find<TextBlock>("Text").Text);
            Assert.Equal("worker", plain.Find<TextBlock>("Text").Text);
        }

        /// <summary>A good value is shown as it is when a FallBackValue is set (1.1.3 for WPF showed the fallback instead).</summary>
        [WpfFact]
        public void A_good_value_is_shown_although_a_fallback_is_set()
        {
            var vm = new Vm();
            using var ui = Ui.Show(new Fallback(), vm);
            Assert.Equal("fine", ui.Find<TextBlock>("Text").Text);
        }
    }
}
