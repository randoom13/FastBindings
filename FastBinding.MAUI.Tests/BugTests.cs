using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using FastBindings.Tests.Views;
using Xunit;

namespace FastBindings.Tests
{
    /// <summary>
    /// One test per bug fixed on this branch, each written so it fails without its fix (each fails on 1.1.3). The commit
    /// that fixed each is named in its summary.
    /// </summary>
    public class BugTests
    {
        /// <summary>
        /// "SourceViewModelStateManager: subscriptions removed at once; a change in the middle of a path subscribes again":
        /// a dotted path follows when the object in its middle is replaced, and leaves the old one.
        /// </summary>
        [Fact]
        public void A_dotted_path_follows_a_replaced_middle_object()
        {
            var vm = new Vm();
            using var ui = Ui.Show(new Dotted(), vm);
            var text = ui.Find<Label>("Text");
            Assert.Equal("leaf", text.Text);

            var old = vm.Child!;
            vm.Child = new Leaf { A = "new" };
            Ui.Settle();
            Assert.Equal("new", text.Text);

            vm.Child.A = "changed";   // the new child is followed
            Ui.Settle();
            Assert.Equal("changed", text.Text);

            old.A = "stale";          // the old one no longer is
            Ui.Settle();
            Assert.Equal("changed", text.Text);
        }

        /// <summary>
        /// "IsValidType: null fits any type but a non-nullable value type, interfaces too": a null reaches an interface-typed
        /// property (ItemsSource is IEnumerable, Command is ICommand) when the source becomes null and when the BindingContext goes.
        /// </summary>
        [Fact]
        public void Null_reaches_interface_typed_properties()
        {
            var vm = new Vm();
            using var ui = Ui.Show(new Lists(), vm);
            var list = ui.Find<CollectionView>("List");
            var button = ui.Find<Button>("Button");
            Assert.NotNull(list.ItemsSource);
            Assert.NotNull(button.Command);

            vm.List = null;
            vm.Command = null;
            Ui.Settle();
            Assert.Null(list.ItemsSource);
            Assert.Null(button.Command);

            vm.List = new[] { "again" };
            vm.Command = new DelegateCommand();
            Ui.Settle();
            Assert.NotNull(list.ItemsSource);

            ui.View.BindingContext = null;
            Ui.Settle();
            Assert.Null(list.ItemsSource);
            Assert.Null(button.Command);
        }

        /// <summary>
        /// "BindingUpdateManager / AsyncBindingUpdateManager: a two-way binding does not write the target's default into the
        /// source while it starts": a picker bound two-way to Index = 1 leaves the view-model at 1 and stays two-way.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void A_two_way_binding_starts_from_the_view_model(bool async)
        {
            var vm = new Vm { Index = 1 };
            using var ui = Ui.Show(async ? new AsyncTwoWayPicker() : (View)new TwoWayPicker(), vm);
            for (var i = 0; i < 10; i++) Ui.Settle();   // the async binding reads its value on the dispatcher
            var picker = ui.View.FindByName<Picker>("Picker")!;
            Assert.Equal(1, vm.Index);
            Assert.Equal(1, picker.SelectedIndex);

            picker.SelectedIndex = 2;
            for (var i = 0; i < 10; i++) Ui.Settle();
            Assert.Equal(2, vm.Index);
        }

        /// <summary>"WeakEventHandler … Unsubscribe removes the handler": it compared a WeakEventHandler with an EventHandler.</summary>
        [Fact]
        public void Unsubscribe_removes_the_handler()
        {
            var type = typeof(CommonBinding).Assembly.GetType("FastBindings.StateManagerObjects.WeakEventPublisher`1", true)!.MakeGenericType(typeof(object));
            var publisher = Activator.CreateInstance(type, nonPublic: true)!;
            var calls = 0;
            var counter = new Counter(() => calls++);
            EventHandler<object> handler = counter.On;
            Invoke(publisher, "Subscribe", handler);
            Invoke(publisher, "RaiseEvent", this, null);
            Assert.Equal(1, calls);

            Invoke(publisher, "Unsubscribe", handler);
            Invoke(publisher, "RaiseEvent", this, null);
            Assert.Equal(1, calls);
        }

        private sealed class Counter
        {
            private readonly Action _count;
            public Counter(Action count) => _count = count;
            public void On(object? sender, object e) => _count();
        }

        private static void Invoke(object target, string method, params object?[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic, null,
                args.Length == 1 ? new[] { typeof(EventHandler<object>) } : new[] { typeof(object), typeof(object) }, null)!.Invoke(target, args);

        /// <summary>
        /// "Thread safety: the shared caches…": the type caches first used on many threads at once — a plain Dictionary written
        /// so could throw, lose entries or loop. Every type gets one accessor, the same on every thread.
        /// </summary>
        [Fact]
        public void The_type_caches_take_first_use_from_many_threads()
        {
            var helper = typeof(CommonBinding).Assembly.GetType("FastBindings.Helpers.MembersHelper", true)!;
            var get = helper.GetMethod("GetPropertyAccessor", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Type) }, null)!;
            var arguments = new[] { typeof(int), typeof(long), typeof(short), typeof(byte), typeof(bool), typeof(char), typeof(double), typeof(float), typeof(decimal),
                typeof(string), typeof(object), typeof(Guid), typeof(DateTime), typeof(TimeSpan), typeof(Uri), typeof(Version), typeof(Exception),
                typeof(int?), typeof(long?), typeof(double?), typeof(bool?), typeof(Type), typeof(Array), typeof(Delegate), typeof(Task), typeof(Thread),
                typeof(sbyte), typeof(ushort), typeof(uint), typeof(ulong) };
            // types no other test touches, each used for the first time by every thread at once: all threads start together at
            // a barrier and walk the same list in the same order
            var types = (from a in arguments from b in arguments select typeof(KeyValuePair<,>).MakeGenericType(a, b)).ToArray();
            var instances = types.Select(t => Activator.CreateInstance(t)!).ToArray();
            const int threads = 8;
            var seen = new object?[types.Length];
            var failures = 0;
            using var barrier = new Barrier(threads);
            var workers = Enumerable.Range(0, threads).Select(_ => new Thread(() =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < types.Length; i++)
                {
                    try
                    {
                        var accessor = get.Invoke(null, new object[] { types[i] })!;
                        var first = Interlocked.CompareExchange(ref seen[i], accessor, null) ?? accessor;
                        if (!ReferenceEquals(first, accessor)) Interlocked.Increment(ref failures);   // two accessors for one type
                        accessor.GetType().GetMethod("GetProperty")!.Invoke(accessor, new[] { instances[i], "Key" });
                    }
                    catch (Exception)
                    {
                        Interlocked.Increment(ref failures);
                    }
                }
            })).ToArray();
            foreach (var t in workers) t.Start();
            foreach (var t in workers) Assert.True(t.Join(TimeSpan.FromSeconds(60)), "a thread never finished (a Dictionary written by two threads can loop)");
            Assert.Equal(0, failures);
        }

        /// <summary>
        /// "SourceViewModelStateManager: two races when an object in the middle of a path changes on another thread" and the
        /// direct path's read-again: workers replace the middle of Child.A while the UI thread swaps BindingContexts; once quiet,
        /// every view shows its view-model's value.
        /// </summary>
        [Fact]
        public void Changes_on_worker_threads_and_BindingContext_swaps_end_where_the_view_models_are()
        {
            const int views = 100;
            var vms = Enumerable.Range(0, views).Select(_ => new Vm()).ToArray();
            var spares = Enumerable.Range(0, views).Select(_ => new Vm()).ToArray();
            var panel = new VerticalStackLayout();
            var window = new Window(new ContentPage { Content = panel });
            var controls = new Dotted[views];
            for (var i = 0; i < views; i++)
            {
                controls[i] = new Dotted { BindingContext = vms[i] };
                panel.Children.Add(controls[i]);
            }

            Ui.Settle();
            Exception? failure = null;
            var stop = DateTime.UtcNow.AddSeconds(1.5);
            var workers = Enumerable.Range(0, 4).Select(w => new Thread(() =>
            {
                try
                {
                    var random = new Random(w);
                    while (DateTime.UtcNow < stop)
                    {
                        var vm = random.Next(2) == 0 ? vms[random.Next(views)] : spares[random.Next(views)];
                        vm.Child = new Leaf { A = "t" + random.Next(1000) };
                    }
                }
                catch (Exception ex)
                {
                    failure ??= ex;
                }
            }) { IsBackground = true }).ToArray();
            foreach (var t in workers) t.Start();
            var random = new Random(99);
            while (DateTime.UtcNow < stop)
            {
                var i = random.Next(views);
                controls[i].BindingContext = controls[i].BindingContext == vms[i] ? spares[i] : vms[i];
                TestDispatcher.ForCurrentThread.RunJobs();
            }

            foreach (var t in workers)
                while (!t.Join(10))
                    TestDispatcher.ForCurrentThread.RunJobs();   // a worker may wait for the UI thread
            Ui.Settle();
            Assert.Null(failure);
            var wrong = controls.Count(c => c.FindByName<Label>("Text")!.Text != ((Vm)c.BindingContext!).Child!.A);
            window.Page = null;
            Assert.Equal(0, wrong);
        }

        /// <summary>
        /// "CacheStrategy.Simple: the cache stores and returns values": two bindings reading one property share one read per
        /// change, and every change shows its own value.
        /// </summary>
        [Fact]
        public void CacheStrategy_Simple_reads_a_property_once_per_change()
        {
            var vm = new Vm();
            using var ui = Ui.Show(new Cached(), vm);
            var first = ui.Find<Label>("First");
            var second = ui.Find<Label>("Second");
            Assert.Equal("first", first.Text);
            foreach (var next in new[] { "second", "third" })
            {
                vm.CountedReads = 0;
                vm.Counted = next;
                Ui.Settle();
                Assert.Equal(next, first.Text);
                Assert.Equal(next, second.Text);
                Assert.Equal(1, vm.CountedReads);
            }
        }
    }
}
