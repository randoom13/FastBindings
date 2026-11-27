using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AvaloniaBench;

/// <summary>A view-model for the checks: an index for a two-way combo box and a child whose replacement moves a dotted path.</summary>
public sealed class CheckVm : INotifyPropertyChanged
{
    private int _index = 1;
    private Leaf? _child = new();

    public static string[] Items { get; } = ["zero", "one", "two"];

    public int Index { get => _index; set { if (_index == value) return; _index = value; On(); } }

    private string _counted = "first";

    /// <summary>How often <see cref="Counted"/> was read: what CacheStrategy.Simple should save.</summary>
    public int CountedReads { get; set; }

    public string Counted
    {
        get { CountedReads++; return _counted; }
        set { if (_counted == value) return; _counted = value; On(); }
    }

    private IReadOnlyList<string>? _list = ["a", "b"];

    public IReadOnlyList<string>? List { get => _list; set { if (ReferenceEquals(_list, value)) return; _list = value; On(); } }
    public Leaf? Child { get => _child; set { if (ReferenceEquals(_child, value)) return; _child = value; On(); } }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void On([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// MODE=checks (and before every benchmark run): what the FastBindings patches promise, checked rather than assumed.
/// 1. A two-way binding does not write the target's default into the view-model while it starts (patch 0006 for CommonBinding,
///    0009 for AsyncCommonBinding): a combo box bound to Index = 1 must leave Index at 1 and show item 1.
/// 2. Threads (patch 0010): view-models replace the object in the middle of a dotted path on worker threads while the UI thread
///    swaps DataContexts; nothing may throw or hang, and once everything is quiet every control shows its view-model's value.
/// </summary>
public static class Checks
{
    public static int Run()
    {
        var failures = 0;
        failures += TwoWayStart("CommonBinding", () => new FastTwoWay());
        failures += TwoWayStart("AsyncCommonBinding", () => new FastAsyncTwoWay());
        failures += NullToInterface();
        failures += SimpleCache();
        failures += Threads();
        return failures;
    }

    private static int TwoWayStart(string name, Func<Control> make)
    {
        var vm = new CheckVm();
        var control = make();
        var window = new Window { Content = control };
        window.Show();
        control.DataContext = vm;
        for (var i = 0; i < 20; i++) Dispatcher.UIThread.RunJobs();   // the async binding reads its value on the dispatcher
        var combo = (ComboBox)((StackPanel)((UserControl)control).Content!).Children[0];
        var started = (vm.Index, combo.SelectedIndex);
        var ok = vm.Index == 1 && combo.SelectedIndex == 1;
        // and it is still two-way: a selection made in the control reaches the view-model
        combo.SelectedIndex = 2;
        for (var i = 0; i < 20; i++) Dispatcher.UIThread.RunJobs();
        ok &= vm.Index == 2;
        window.Close();
        Console.WriteLine($"{(ok ? "ok  " : "FAIL")} two-way start, {name}: after start view-model {started.Item1}, combo {started.Item2} (expected 1, 1); after selecting 2 view-model {vm.Index} (expected 2)");
        return ok ? 0 : 1;
    }

    /// <summary>
    /// A null reaches a property whose type is an interface (ItemsSource is IEnumerable): when the view-model's list becomes null and
    /// when the DataContext goes away, the list must be cleared — 1.1.3 refused null for interfaces and kept the old list.
    /// </summary>
    private static int NullToInterface()
    {
        var vm = new CheckVm();
        var control = new FastNullCheck();
        var window = new Window { Content = control };
        window.Show();
        control.DataContext = vm;
        Dispatcher.UIThread.RunJobs();
        var list = (ItemsControl)((StackPanel)control.Content!).Children[0];
        var bound = list.ItemsSource is not null;
        vm.List = null;
        Dispatcher.UIThread.RunJobs();
        var clearedByValue = list.ItemsSource is null;
        vm.List = ["c"];
        Dispatcher.UIThread.RunJobs();
        var boundAgain = list.ItemsSource is not null;
        control.DataContext = null;
        Dispatcher.UIThread.RunJobs();
        var clearedByContext = list.ItemsSource is null;
        window.Close();
        var ok = bound && clearedByValue && boundAgain && clearedByContext;
        Console.WriteLine($"{(ok ? "ok  " : "FAIL")} null to an interface property: bound {bound}, cleared when the list became null {clearedByValue}, bound again {boundAgain}, cleared when the DataContext went {clearedByContext}");
        return ok ? 0 : 1;
    }

    /// <summary>
    /// CacheStrategy.Simple: two bindings reading the same property of the same object get one read per change between them, and
    /// each change shows its own value (nothing is reused from an earlier event). 1.1.3 never returned a cached value.
    /// </summary>
    private static int SimpleCache()
    {
        var vm = new CheckVm();
        var control = new FastCacheCheck();
        var window = new Window { Content = control };
        window.Show();
        control.DataContext = vm;
        Dispatcher.UIThread.RunJobs();
        var panel = (StackPanel)control.Content!;
        var a = (TextBlock)panel.Children[0];
        var b = (TextBlock)panel.Children[1];
        var shown = a.Text == "first" && b.Text == "first";
        var reads = new List<int>();
        var values = true;
        foreach (var next in new[] { "second", "third" })
        {
            vm.CountedReads = 0;
            vm.Counted = next;
            Dispatcher.UIThread.RunJobs();
            reads.Add(vm.CountedReads);
            values &= a.Text == next && b.Text == next;
        }

        window.Close();
        var ok = shown && values && reads.All(r => r == 1);
        Console.WriteLine($"{(ok ? "ok  " : "FAIL")} CacheStrategy.Simple: two bindings show each change ({values}), getter reads per change {string.Join(", ", reads)} (expected 1)");
        return ok ? 0 : 1;
    }

    private static int Threads()
    {
        const int views = 200, workers = 4, seconds = 3;
        var panel = new StackPanel();
        var window = new Window { Content = panel };
        window.Show();
        var vms = Enumerable.Range(0, views).Select(_ => new CheckVm()).ToArray();
        var spares = Enumerable.Range(0, views).Select(_ => new CheckVm()).ToArray();
        var controls = new Control[views];
        for (var i = 0; i < views; i++)
        {
            controls[i] = new FastDottedCheck();
            panel.Children.Add(controls[i]);
            controls[i].DataContext = vms[i];
        }

        Dispatcher.UIThread.RunJobs();
        Exception? failure = null;
        var stop = DateTime.UtcNow.AddSeconds(seconds);
        var threads = Enumerable.Range(0, workers).Select(w => new Thread(() =>
        {
            try
            {
                var random = new Random(w);
                while (DateTime.UtcNow < stop)
                {
                    var vm = random.Next(2) == 0 ? vms[random.Next(views)] : spares[random.Next(views)];
                    vm.Child = new Leaf { A = "t" + random.Next(1000) };   // the middle of Child.A changes, on this thread
                }
            }
            catch (Exception ex)
            {
                failure ??= ex;
            }
        }) { IsBackground = true }).ToArray();
        foreach (var t in threads) t.Start();

        // meanwhile the UI thread swaps DataContexts and pumps what the workers hand it (FastBindings invokes onto it)
        var swaps = 0;
        var random = new Random(99);
        while (DateTime.UtcNow < stop)
        {
            var i = random.Next(views);
            controls[i].DataContext = controls[i].DataContext == vms[i] ? spares[i] : vms[i];
            swaps++;
            Dispatcher.UIThread.RunJobs();
        }

        var joined = true;
        foreach (var t in threads)
        {
            while (!t.Join(10)) Dispatcher.UIThread.RunJobs();   // a worker may wait in Dispatcher.Invoke for us
            joined &= !t.IsAlive;
        }

        for (var i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
        var wrong = 0;
        for (var i = 0; i < views; i++)
        {
            var vm = (CheckVm)controls[i].DataContext!;
            var shown = ((TextBlock)((StackPanel)((UserControl)controls[i]).Content!).Children[0]).Text;
            if (shown != vm.Child?.A)
            {
                wrong++;
                var other = ReferenceEquals(vm, vms[i]) ? spares[i] : vms[i];
                if (Environment.GetEnvironmentVariable("CHECKS_VERBOSE") == "1")
                    Console.WriteLine($"     view {i}: shows '{shown}', its view-model '{vm.Child?.A}' ({(ReferenceEquals(vm, vms[i]) ? "first" : "spare")}), the other view-model '{other.Child?.A}'");
            }
        }

        window.Close();
        var ok = failure == null && joined && wrong == 0;
        Console.WriteLine($"{(ok ? "ok  " : "FAIL")} threads: {workers} workers replacing Child for {seconds} s, {swaps} DataContext swaps; {wrong} of {views} views wrong{(failure == null ? "" : $"; {failure.GetType().Name}: {failure.Message}")}");
        return ok ? 0 : 1;
    }
}
