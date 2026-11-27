using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MauiBench.Views;

namespace MauiBench
{
    public sealed class Vm : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private string _s0 = "s0";
        private double _d0 = 10;
        public string S0 { get => _s0; set { _s0 = value; Raise(); } }
        public string S1 { get; set; } = "s1"; public string S2 { get; set; } = "s2"; public string S3 { get; set; } = "s3";
        public string S4 { get; set; } = "s4"; public string S5 { get; set; } = "s5"; public string S6 { get; set; } = "s6";
        public string S7 { get; set; } = "s7"; public string S8 { get; set; } = "s8"; public string S9 { get; set; } = "s9";
        public double D0 { get => _d0; set { _d0 = value; Raise(); } }
        public double D1 { get; set; } = 11; public double D2 { get; set; } = 12;
        public bool B { get; set; } = true;
        private void Raise([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public static class Program
    {
        // Views per round; each view has 30 bindings (10 labels × Text, WidthRequest, IsVisible).
        static int Views = int.Parse(Environment.GetEnvironmentVariable("VIEWS") ?? "200");
        static int Rounds = int.Parse(Environment.GetEnvironmentVariable("ROUNDS") ?? "15");

        public static void Main()
        {
            var only = Environment.GetEnvironmentVariable("ONLY");
            var profile = Environment.GetEnvironmentVariable("PROFILE");
            var kinds = new (string Name, Func<ContentView> Make)[]
            {
                ("classic {Binding}", () => new Classic()),
                ("compiled {Binding} x:DataType", () => new Compiled()),
                ("FastBindings CommonBinding", () => new Fast()),
            };
            if (Environment.GetEnvironmentVariable("DIAG") == "1")
            {
                FastBindings.FastBindingsDiagnostics.Enabled = true;
                Run("diag", () => new Fast(), cold: true);
                Console.WriteLine("[maui-bench] diagnostics for one round of FastBindings: " + FastBindings.FastBindingsDiagnostics.Report());
                return;
            }
            Console.WriteLine($"[maui-bench] {Environment.GetEnvironmentVariable("LABEL")} views={Views} bindings/view=30 rounds={Rounds}");
            if (profile != null)
            {
                // PROFILE=<part of a kind's name>: that kind only, WARMUP then ROUNDS rounds, for a CPU profile of the process
                // (scripts/profile-steps.py splits it by the Step_ methods each step runs in)
                var (pname, pmake) = kinds.Single(k => k.Name.Contains(profile));
                var warmup = int.Parse(Environment.GetEnvironmentVariable("WARMUP") ?? "10");
                Marked = false;
                for (var w = 0; w < warmup; w++) Run(pname, pmake, cold: false);
                Marked = true;
                var measured = Enumerable.Range(0, Rounds).Select(_ => Run(pname, pmake, cold: false)).ToList();
                Console.WriteLine($"[maui-bench] profiled {pname}: {Rounds} rounds after {warmup} unmarked warm-up rounds; stopwatch medians " +
                                  $"build {Med(measured.Select(m => m.Build).ToList()):F2} ms  bind {Med(measured.Select(m => m.Bind).ToList()):F2} ms  " +
                                  $"attach {Med(measured.Select(m => m.Attach).ToList()):F2} ms  push {Med(measured.Select(m => m.Push).ToList()) * 1000:F3} µs/binding  " +
                                  $"detach {Med(measured.Select(m => m.Detach).ToList()):F2} ms");
                string Spread(Func<(double Build, double Bind, double Attach, double Push, double Detach), double> f) =>
                    $"mean {measured.Average(f):F2} max {measured.Max(f):F2}";
                Console.WriteLine($"[maui-bench] spread: build {Spread(m => m.Build)}  bind {Spread(m => m.Bind)}  attach {Spread(m => m.Attach)}  detach {Spread(m => m.Detach)}  " +
                                  $"GC collections gen0 {GC.CollectionCount(0)} gen1 {GC.CollectionCount(1)} gen2 {GC.CollectionCount(2)}");
                return;
            }
            foreach (var (name, make) in kinds)
            {
                if (only != null && !name.Contains(only)) continue;
                Run(name, make, cold: true);   // the first round: the process's first use of this kind
                var build = new List<double>(); var bind = new List<double>(); var attach = new List<double>(); var push = new List<double>(); var detach = new List<double>();
                for (var r = 0; r < Rounds; r++)
                {
                    var m = Run(name, make, cold: false);
                    build.Add(m.Build); bind.Add(m.Bind); attach.Add(m.Attach); push.Add(m.Push); detach.Add(m.Detach);
                }
                Console.WriteLine($"[maui-bench] {name,-32} build {Med(build),7:F2} ms  bind {Med(bind),7:F2} ms  attach {Med(attach),7:F2} ms  " +
                                  $"values {Med(build)+Med(bind)+Med(attach),7:F2} ms  push {Med(push)*1000,6:F3} µs/binding  detach {Med(detach),6:F2} ms  " +
                                  $"(cold first round: values {_cold:F1} ms)");
            }
        }

        // Each step runs inside a method of its own name, so a CPU profile tells the steps apart (and never inlined away).
        // Steps run in their Step_ method only while Marked (PROFILE turns it off for the warm-up rounds, so they leave no marks).
        static bool Marked = true;
        static void In(Action<Action> step, Action a) { if (Marked) step(a); else a(); }
        [MethodImpl(MethodImplOptions.NoInlining)] static void Step_Build(Action a) => a();
        [MethodImpl(MethodImplOptions.NoInlining)] static void Step_Bind(Action a) => a();
        [MethodImpl(MethodImplOptions.NoInlining)] static void Step_Attach(Action a) => a();
        [MethodImpl(MethodImplOptions.NoInlining)] static void Step_Push(Action a) => a();
        [MethodImpl(MethodImplOptions.NoInlining)] static void Step_Detach(Action a) => a();

        static double _cold;
        static double Med(List<double> v) { v.Sort(); return v[v.Count / 2]; }

        static (double Build, double Bind, double Attach, double Push, double Detach) Run(string name, Func<ContentView> make, bool cold)
        {
            var dispatcher = TestDispatcher.ForCurrentThread;
            var vm = new Vm();
            var sw = Stopwatch.StartNew();
            var views = new ContentView[Views];
            In(Step_Build, () => { for (var i = 0; i < Views; i++) views[i] = make(); });
            var build = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            In(Step_Bind, () => { foreach (var v in views) v.BindingContext = vm; dispatcher.RunJobs(); });
            var bind = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            var panel = new VerticalStackLayout();
            Window window = null!;
            In(Step_Attach, () =>
            {
                window = new Window(new ContentPage { Content = panel });
                foreach (var v in views) panel.Children.Add(v);
                dispatcher.RunJobs();
            });
            var attach = sw.Elapsed.TotalMilliseconds;
            // every view must show the view-model's values, whichever way it got them
            var label = (Label)((VerticalStackLayout)views[^1].Content).Children[0];
            if (label.Text != "s0" || ((Label)((VerticalStackLayout)views[^1].Content).Children[1]).WidthRequest != 11) throw new Exception($"{name}: wrong values ({label.Text})");
            if (cold) _cold = build + bind + attach;
            const int changes = 20;
            sw.Restart();
            In(Step_Push, () => { for (var c = 0; c < changes; c++) { vm.S0 = "v" + c; vm.D0 = c; } dispatcher.RunJobs(); });
            var push = sw.Elapsed.TotalMilliseconds / (changes * 2.0 * Views);   // per binding told of a change
            if (label.Text != "v19") throw new Exception($"{name}: push not applied ({label.Text})");
            sw.Restart();
            In(Step_Detach, () => { window.Page = null; foreach (var v in views) v.BindingContext = null; dispatcher.RunJobs(); });
            var detach = sw.Elapsed.TotalMilliseconds;
            return (build, bind, attach, push, detach);
        }
    }
}
