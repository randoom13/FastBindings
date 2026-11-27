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

var profile = Environment.GetEnvironmentVariable("PROFILE");
using var allocationSamples = Environment.GetEnvironmentVariable("ALLOCS") == "1" ? new AvaloniaBench.AllocationSamples() : null;
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
    if (profile != null) break;
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
// PROFILE=<a case's name>: that case only, WARMUP unmarked rounds then ROUNDS marked ones, for a CPU profile of the process
// (scripts/profile-steps.py splits it by the Step_ methods each phase runs in).
var profiled = profile == null ? cases : cases.Where(c => c.Name == profile).ToArray();
if (profile != null)
{
    if (profiled.Length != 1) { Console.WriteLine($"PROFILE: no case named '{profile}'"); return 2; }
    var warmup = int.TryParse(Environment.GetEnvironmentVariable("WARMUP"), out var wu) ? wu : 5;
    rounds += warmup;
    Steps.UnmarkedRounds = warmup;
}
for (var round = 0; round < rounds; round++)
{
    Steps.Marked = round >= Steps.UnmarkedRounds;
    foreach (var (name, dotted, make) in profiled)
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
            Steps.Run(phase, work);
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

if (profile != null)
{
    string P(string phase) => $"{Median(ms[profile][phase].Skip(Steps.UnmarkedRounds)):N2} ms";
    Console.WriteLine($"[avalonia-bench] profiled {profile}: {rounds - Steps.UnmarkedRounds} rounds after {Steps.UnmarkedRounds} unmarked warm-up rounds; " +
                      $"stopwatch medians create {P("create")}  bind {P("bind")}  layout {P("layout")}  push {P("push")}  layout after {P("layout after")}  unbind {P("unbind")}");
    AllocationSamples.Print();
    foreach (var (step, t) in Steps.Spent)
        Console.WriteLine($"[avalonia-bench] {step,-12} {t.Total / (rounds - Steps.UnmarkedRounds),8:F2} ms per round, of which the runtime was paused for GC {t.Gc / (rounds - Steps.UnmarkedRounds),8:F2} ms");
    return 0;
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
    /// <summary>Each phase runs inside a method of its own name, so a CPU profile tells the phases apart (never inlined away).</summary>
    public static class Steps
    {
        public static bool Marked = true;
        public static int UnmarkedRounds;

        public static void Run(string phase, Action work)
        {
            if (!Marked) { work(); return; }
            // how much of each step the runtime was stopped for a garbage collection (PROFILE prints it)
            var pause = GC.GetTotalPauseDuration(); var sw = System.Diagnostics.Stopwatch.StartNew();
            AllocationSamples.Step = phase;
            try { Marked1(phase, work); }
            finally
            {
                AllocationSamples.Step = null;
                Spent.TryGetValue(phase, out var t);
                Spent[phase] = (t.Total + sw.Elapsed.TotalMilliseconds, t.Gc + (GC.GetTotalPauseDuration() - pause).TotalMilliseconds);
            }
        }
        public static readonly Dictionary<string, (double Total, double Gc)> Spent = new();

        static void Marked1(string phase, Action work)
        {
            switch (phase)
            {
                case "create": Step_Create(work); break;
                case "bind": Step_Bind(work); break;
                case "layout": Step_Layout(work); break;
                case "push": Step_Push(work); break;
                case "layout after": Step_LayoutAfter(work); break;
                case "unbind": Step_Unbind(work); break;
                default: work(); break;
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] static void Step_Create(Action a) => a();
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] static void Step_Bind(Action a) => a();
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] static void Step_Layout(Action a) => a();
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] static void Step_Push(Action a) => a();
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] static void Step_LayoutAfter(Action a) => a();
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] static void Step_Unbind(Action a) => a();
    }

    public sealed class App : Avalonia.Application
    {
    }
}

namespace AvaloniaBench
{
    /// <summary>
    /// ALLOCS=1 with PROFILE: the runtime's allocation samples (one per ~100 KB allocated, with the type that crossed it) counted
    /// per step, to see what a step allocates.
    /// </summary>
    public sealed class AllocationSamples : System.Diagnostics.Tracing.EventListener
    {
        public static string? Step;
        public static readonly Dictionary<(string Step, string Type), (long Samples, long Bytes)> Seen = new();

        protected override void OnEventSourceCreated(System.Diagnostics.Tracing.EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime")
                EnableEvents(source, System.Diagnostics.Tracing.EventLevel.Verbose, (System.Diagnostics.Tracing.EventKeywords)0x1);
        }

        protected override void OnEventWritten(System.Diagnostics.Tracing.EventWrittenEventArgs e)
        {
            var step = Step;
            if (step == null || e.EventName == null || !e.EventName.StartsWith("GCAllocationTick") || e.Payload == null)
                return;
            var type = e.PayloadNames!.IndexOf("TypeName") is var i and >= 0 ? e.Payload[i] as string ?? "?" : "?";
            var amount = e.PayloadNames.IndexOf("AllocationAmount64") is var j and >= 0 ? Convert.ToInt64(e.Payload[j]) : 0;
            lock (Seen)
            {
                Seen.TryGetValue((step, type), out var t);
                Seen[(step, type)] = (t.Samples + 1, t.Bytes + amount);
            }
        }

        public static void Print()
        {
            lock (Seen)
                foreach (var g in Seen.GroupBy(k => k.Key.Step))
                {
                    var total = g.Sum(x => x.Value.Samples);
                    Console.WriteLine($"[allocs] {g.Key}: {total} samples");
                    foreach (var x in g.OrderByDescending(x => x.Value.Samples).Take(15))
                        Console.WriteLine($"[allocs]   {x.Value.Samples * 100.0 / total,5:F1}%  {x.Key.Type}");
                }
        }
    }
}
