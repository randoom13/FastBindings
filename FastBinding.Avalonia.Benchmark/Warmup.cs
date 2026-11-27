using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AvaloniaBench;

/// <summary>
/// MODE=warmup: does a binding get cheaper after its first call? One case per process (CASE=…), so the first view is really the first
/// of its kind. Three curves on the same views: views made one after another (is view 2 cheaper than view 1: a cache across
/// instances?), the same bindings pushed again and again (is push 2 or 100 cheaper than push 1: a cache per binding?) and the
/// DataContext swapped between two view-models (does a binding re-pay its start?). Every push is checked to have reached the
/// control synchronously, so the timed loop holds all of the binding's work.
/// </summary>
public static class Warmup
{
    private static readonly (string Name, Func<Control> Make, Func<int, object> Vm, Action<object, string, double> Push, Func<object, string> Pushed)[] Cases =
    [
        ("compiled", () => new CompiledFlat(), i => new Vm { A = "v" + i }, (o, a, w) => { var v = (Vm)o; v.A = a; v.W = w; }, o => ((Vm)o).A),
        ("fastbindings-common", () => new FastFlat(), i => new Vm { A = "v" + i }, (o, a, w) => { var v = (Vm)o; v.A = a; v.W = w; }, o => ((Vm)o).A),
        ("fastbindings-accessor", () => new FastAccessorFlat(), i => new AccVm { A = "v" + i }, (o, a, w) => { var v = (AccVm)o; v.A = a; v.W = w; }, o => ((AccVm)o).A),
        ("reflection", () => new ReflectionFlat(), i => new Vm { A = "v" + i }, (o, a, w) => { var v = (Vm)o; v.A = a; v.W = w; }, o => ((Vm)o).A),
    ];

    private static TextBlock Text(Control c) => (TextBlock)((StackPanel)((UserControl)c).Content!).Children[0];

    /// <summary>MODE=push: only pushes, for SECONDS (default 5), on 300 bound views — for a CPU profile of a binding's update path.</summary>
    public static int Push(string caseName)
    {
        var c = Cases.Single(x => x.Name == caseName);
        const int views = 300;
        var panel = new StackPanel();
        var window = new Window { Content = panel };
        window.Show();
        var vms = new object[views];
        for (var i = 0; i < views; i++)
        {
            vms[i] = c.Vm(i);
            var control = c.Make();
            panel.Children.Add(control);
            control.DataContext = vms[i];
        }

        Dispatcher.UIThread.RunJobs();
        var seconds = double.TryParse(Environment.GetEnvironmentVariable("SECONDS"), out var sec) ? sec : 5;
        var texts = Enumerable.Range(0, 16).Select(k => "p" + k).ToArray();
        var watch = Stopwatch.StartNew();
        long pushes = 0;
        for (var k = 0; watch.Elapsed.TotalSeconds < seconds; k++)
        {
            for (var i = 0; i < views; i++) c.Push(vms[i], texts[k & 15], k & 15);
            pushes += 2 * views;
        }

        Console.WriteLine($"{caseName}: {pushes} pushes, {watch.Elapsed.TotalMilliseconds * 1000 / pushes:N3} µs per push");
        window.Close();
        return 0;
    }

    public static int Run(string caseName)
    {
        var c = Cases.Single(x => x.Name == caseName);
        const int views = 300;
        var panel = new StackPanel();
        var window = new Window { Content = panel };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var controls = new Control[views];
        var vms = new object[views];
        var others = new object[views];
        for (var i = 0; i < views; i++) { vms[i] = c.Vm(i); others[i] = c.Vm(-i); }

        // 1. Views one after another: make, add, set the DataContext, run the dispatcher (FastBindings starts on Loaded).
        var perView = new double[views];
        for (var i = 0; i < views; i++)
        {
            var t = Stopwatch.GetTimestamp();
            controls[i] = c.Make();
            panel.Children.Add(controls[i]);
            controls[i].DataContext = vms[i];
            Dispatcher.UIThread.RunJobs();
            perView[i] = Stopwatch.GetElapsedTime(t).TotalMicroseconds;
            if (Text(controls[i]).Text != c.Pushed(vms[i])) { Console.WriteLine($"{caseName}: view {i} not bound"); return 1; }
        }

        // 2. The same 300 × 2 bindings pushed again and again, no layout between: the time of each whole round.
        const int pushRounds = 200;
        var perPush = new double[pushRounds];
        for (var k = 0; k < pushRounds; k++)
        {
            var text = "p" + k;
            var t = Stopwatch.GetTimestamp();
            for (var i = 0; i < views; i++) c.Push(vms[i], text, k);
            perPush[k] = Stopwatch.GetElapsedTime(t).TotalMicroseconds / (2 * views);
            for (var i = 0; i < views; i++)
                if (Text(controls[i]).Text != text) { Console.WriteLine($"{caseName}: push {k} did not reach view {i} synchronously"); return 1; }
        }

        Dispatcher.UIThread.RunJobs();

        // 3. DataContext swapped between two view-models of the same type, again and again (each swap restarts three bindings).
        const int swaps = 50;
        var perSwap = new double[swaps];
        for (var k = 0; k < swaps; k++)
        {
            var t = Stopwatch.GetTimestamp();
            for (var i = 0; i < views; i++) controls[i].DataContext = k % 2 == 0 ? others[i] : vms[i];
            Dispatcher.UIThread.RunJobs();
            perSwap[k] = Stopwatch.GetElapsedTime(t).TotalMicroseconds / (3 * views);
            var expected = c.Pushed(k % 2 == 0 ? others[0] : vms[0]);
            if (Text(controls[0]).Text != expected) { Console.WriteLine($"{caseName}: swap {k} not applied"); return 1; }
        }

        window.Close();
        static string At(double[] a, params int[] idx) => string.Join(" | ", idx.Select(i => $"{a[i]:N2}"));
        static double Avg(double[] a, int from, int to) => a.Skip(from).Take(to - from).Average();
        Console.WriteLine($"| {caseName} | {At(perView, 0, 1, 2, 9)} | {Avg(perView, 100, views):N2} | {At(perPush, 0, 1, 2, 9)} | {Avg(perPush, 100, pushRounds):N3} | {At(perSwap, 0, 1, 2, 9)} | {Avg(perSwap, 20, swaps):N2} |");
        return 0;
    }
}
