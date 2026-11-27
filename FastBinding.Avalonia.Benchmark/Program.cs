using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using AvaloniaBench;

AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
if (Environment.GetEnvironmentVariable("MODE") == "checks") return Checks.Run();
if (Environment.GetEnvironmentVariable("MODE") == "push") return Warmup.Push(Environment.GetEnvironmentVariable("CASE") ?? "compiled");
if (Environment.GetEnvironmentVariable("MODE") == "warmup") return Warmup.Run(Environment.GetEnvironmentVariable("CASE") ?? "compiled");

var cases = new (string Name, bool Dotted, Func<Control> Make)[]
{
    ("compiled {Binding}", false, () => new CompiledFlat()),
    ("FastBindings CommonBinding", false, () => new FastFlat()),
    ("reflection {ReflectionBinding}", false, () => new ReflectionFlat()),
    ("compiled {Binding}, Child.X", true, () => new CompiledDotted()),
    ("FastBindings CommonBinding, Child.X", true, () => new FastDotted()),
    ("reflection {ReflectionBinding}, Child.X", true, () => new ReflectionDotted()),
};

static TextBlock Text(Control c) => (TextBlock)((StackPanel)((UserControl)c).Content!).Children[0];
static string Show(TextBlock t) => $"{t.Text}/{t.Width}/{t.IsVisible}";

// Correctness first: every case must show the same thing after the same steps.
var failures = 0;
foreach (var (name, dotted, make) in cases)
{
    var vm = new Vm();
    var control = make();
    var window = new Window { Content = control };
    window.Show();
    control.DataContext = vm;
    Dispatcher.UIThread.RunJobs();
    var t = Text(control);
    var seen = new List<string> { Show(t) };
    void Set(string a, double w, bool v)
    {
        if (dotted) { vm.Child!.A = a; vm.Child.W = w; vm.Child.V = v; }
        else { vm.A = a; vm.W = w; vm.V = v; }
        Dispatcher.UIThread.RunJobs();
        seen.Add(Show(t));
    }

    Set("b", 42, false);
    Set("c", 7, true);
    control.DataContext = new Vm { A = "other", Child = new Leaf { A = "other" } };
    Dispatcher.UIThread.RunJobs();
    seen.Add(Show(t));
    if (dotted)
    {
        ((Vm)control.DataContext!).Child = new Leaf { A = "swapped", W = 3 };   // an intermediate segment changes
        Dispatcher.UIThread.RunJobs();
        seen.Add(Show(t));
    }

    window.Close();
    var expected = dotted ? "a/10/True b/42/False c/7/True other/10/True swapped/3/True" : "a/10/True b/42/False c/7/True other/10/True";
    var got = string.Join(' ', seen);
    var ok = got == expected;
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {name}: {got}{(ok ? "" : $"  (expected {expected})")}");
}

// Then the cost, phase by phase, so the bindings' own work is timed apart from making controls and from layout:
// create (N views made and added, no DataContext), bind (each DataContext set: the bindings start and push their first value —
// synchronous for all of them), first layout, push (10 rounds of two property changes on every view-model: the bindings' updates,
// synchronous), layout after the pushes, detach (DataContext cleared: the bindings let go).
var n = int.TryParse(Environment.GetEnvironmentVariable("N"), out var parsed) ? parsed : 2000;
var rounds = int.TryParse(Environment.GetEnvironmentVariable("ROUNDS"), out var r) ? r : 7;
string[] phases = ["create", "bind", "layout", "push", "layout after", "unbind"];
var ms = cases.ToDictionary(c => c.Name, _ => phases.ToDictionary(p => p, _ => new List<double>()));
var kb = cases.ToDictionary(c => c.Name, _ => phases.ToDictionary(p => p, _ => new List<double>()));
var texts = Enumerable.Range(0, 10).Select(k => "x" + k).ToArray();
for (var round = 0; round < rounds; round++)
{
    foreach (var (name, dotted, make) in cases)
    {
        var panel = new StackPanel();
        var window = new Window { Content = panel };
        window.Show();
        var vms = new Vm[n];
        for (var i = 0; i < n; i++) vms[i] = new Vm();
        var controls = new Control[n];
        GC.Collect();
        GC.WaitForPendingFinalizers();
        void Phase(string phase, Action work)
        {
            var bytes = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            work();
            ms[name][phase].Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            kb[name][phase].Add((GC.GetAllocatedBytesForCurrentThread() - bytes) / 1024.0);
        }

        Phase("create", () => { for (var i = 0; i < n; i++) panel.Children.Add(controls[i] = make()); });
        Phase("bind", () => { for (var i = 0; i < n; i++) controls[i].DataContext = vms[i]; });
        Phase("layout", () => Dispatcher.UIThread.RunJobs());
        Phase("push", () =>
        {
            for (var k = 0; k < 10; k++)
                for (var i = 0; i < n; i++)
                {
                    if (dotted) { vms[i].Child!.A = texts[k]; vms[i].Child!.W = k; }
                    else { vms[i].A = texts[k]; vms[i].W = k; }
                }
        });
        Phase("layout after", () => Dispatcher.UIThread.RunJobs());
        Phase("unbind", () => { for (var i = 0; i < n; i++) controls[i].DataContext = null; });
        window.Close();
        Dispatcher.UIThread.RunJobs();
    }
}

static double Median(IEnumerable<double> values) { var s = values.Order().ToArray(); return s[s.Length / 2]; }
Console.WriteLine();
Console.WriteLine($"{n} views × 3 bindings = {3 * n} bindings, {20 * n} pushes; median of rounds 2–{rounds} (round 1 = cold)");
Console.WriteLine("| case | create | bind | per binding | layout | push | per push | layout after | unbind |");
Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
foreach (var (name, _, _) in cases)
{
    string P(string phase) => $"{Median(ms[name][phase].Skip(1)):N1} ms / {Median(kb[name][phase].Skip(1)) / 1024:N1} MB";
    var bind = Median(ms[name]["bind"].Skip(1));
    var push = Median(ms[name]["push"].Skip(1));
    Console.WriteLine($"| {name} | {P("create")} | {P("bind")} | {bind * 1000 / (3 * n):N2} µs | {P("layout")} | {P("push")} | {push * 1000 / (20 * n):N3} µs | {P("layout after")} | {P("unbind")} |");
}

return failures;

namespace AvaloniaBench
{
    public sealed class App : Avalonia.Application
    {
    }
}
