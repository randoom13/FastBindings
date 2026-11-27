using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfBench.Views;

namespace WpfBench
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

    /// <summary>
    /// The MAUI benchmark's views in WPF: VIEWS views (200) of 30 bindings (10 TextBlocks × Text, Width, IsEnabled), classic
    /// {Binding} and FastBindings' CommonBinding; build, bind (DataContext set), attach (shown in a window, laid out), value
    /// changes (20 × S0 and D0, which 5 bindings of each view read) and detach (window closed, DataContext cleared), median of
    /// ROUNDS. WPF has no compiled bindings: classic {Binding} is what an app has without FastBindings.
    ///
    /// What the bindings themselves cost, apart from WPF's work (controls, SetValue, layout, rendering):
    /// - "no bindings": the same views without bindings, the benchmark setting the very values the bindings would, the same way
    ///   (SetValue on the same properties, in the same steps). A kind's step minus this one is its bindings' cost — the only way
    ///   to see it for classic {Binding}, whose code cannot hold a stopwatch.
    /// - DIAG=1: FastBindings' own stopwatches (FastBindingsDiagnostics' times, at every call into the library) in a pass of
    ///   their own, so the timings above are not touched by them.
    /// </summary>
    public static class Program
    {
        static readonly int Views = int.Parse(Environment.GetEnvironmentVariable("VIEWS") ?? "200");
        static readonly int Rounds = int.Parse(Environment.GetEnvironmentVariable("ROUNDS") ?? "15");
        // SHARED=0: a view-model per view (an app's usual case) instead of one view-model shared by every view
        static readonly bool Shared = Environment.GetEnvironmentVariable("SHARED") != "0";
        const int Changes = 20;
        const int BindingsPerChange = 5;   // S0: Text of TextBlock 0; D0: Width of TextBlocks 0, 3, 6, 9

        record struct Steps(double Build, double Bind, double Attach, double Push, double Detach)
        {
            public double Values => Build + Bind + Attach;
        }

        [STAThread]
        public static int Main()
        {
            var only = Environment.GetEnvironmentVariable("ONLY");
            var kinds = new (string Name, Func<UserControl> Make, bool Plain)[]
            {
                ("no bindings (baseline)", () => new Plain(), true),
                ("classic {Binding}", () => new Classic(), false),
                ("FastBindings CommonBinding", () => new Fast(), false),
            };
            Console.WriteLine($"[wpf-bench] {Environment.GetEnvironmentVariable("LABEL")} views={Views} view-models={(Shared ? "one shared" : "one per view")} bindings/view=30 rounds={Rounds}");
            var results = new Dictionary<string, Steps>();
            foreach (var (name, make, plain) in kinds)
            {
                if (only != null && !name.Contains(only) && !plain) continue;
                var cold = Run(name, make, plain);
                var m = Enumerable.Range(0, Rounds).Select(_ => Run(name, make, plain)).ToList();
                var med = new Steps(Med(m, x => x.Build), Med(m, x => x.Bind), Med(m, x => x.Attach), Med(m, x => x.Push), Med(m, x => x.Detach));
                results[name] = med;
                Console.WriteLine($"[wpf-bench] {name,-28} build {med.Build,7:F2} ms  bind {med.Bind,7:F2} ms  attach {med.Attach,7:F2} ms  " +
                                  $"values {Med(m, x => x.Values),7:F2} ms  push {med.Push * 1000,6:F3} µs per binding updated  detach {med.Detach,7:F2} ms  " +
                                  $"(cold first round: values {cold.Values:F1} ms)");
            }
            if (results.TryGetValue("no bindings (baseline)", out var baseline))
                foreach (var (name, steps) in results)
                    if (!name.StartsWith("no bindings"))
                        Console.WriteLine($"[wpf-bench] {name,-28} minus no bindings: build {steps.Build - baseline.Build,7:F2} ms  bind {steps.Bind - baseline.Bind,7:F2} ms  " +
                                          $"attach {steps.Attach - baseline.Attach,7:F2} ms  values {steps.Values - baseline.Values,7:F2} ms  " +
                                          $"push {(steps.Push - baseline.Push) * 1000,6:F3} µs per binding updated  detach {steps.Detach - baseline.Detach,7:F2} ms");
            if (Environment.GetEnvironmentVariable("DIAG") == "1")
                Diagnose();
            return 0;
        }

        static double Med(List<Steps> m, Func<Steps, double> f) { var v = m.Select(f).OrderBy(x => x).ToList(); return v[v.Count / 2]; }

        /// <summary>
        /// A pass with FastBindings' stopwatches on: for each step, the time spent inside FastBindings (median of ROUNDS). Read
        /// through reflection, so the benchmark still builds against a FastBindings without them (1.1.3).
        /// </summary>
        static void Diagnose()
        {
            var diag = typeof(FastBindings.CommonBinding).Assembly.GetType("FastBindings.FastBindingsDiagnostics");
            if (diag?.GetProperty("TotalTime") == null)
            {
                Console.WriteLine("[wpf-bench] DIAG: this FastBindings has no stopwatches");
                return;
            }
            double Total() => ((TimeSpan)diag.GetProperty("TotalTime")!.GetValue(null)!).TotalMilliseconds;
            diag.GetProperty("Enabled")!.SetValue(null, true);
            var inside = new List<Steps>();
            var whole = new List<Steps>();
            for (var r = 0; r <= Rounds; r++)
            {
                var times = new double[5];
                var steps = Run("diag", () => new Fast(), false, (step, before) => times[step] = (Total() - before) / (step == 3 ? Changes * BindingsPerChange * Views : 1), Total);
                if (r == 0) continue;   // the cold round
                inside.Add(new Steps(times[0], times[1], times[2], times[3], times[4]));
                whole.Add(steps);
            }
            diag.GetProperty("Enabled")!.SetValue(null, false);
            Console.WriteLine($"[wpf-bench] inside FastBindings (its stopwatches)  build {Med(inside, x => x.Build),7:F2} ms  bind {Med(inside, x => x.Bind),7:F2} ms  " +
                              $"attach {Med(inside, x => x.Attach),7:F2} ms  push {Med(inside, x => x.Push) * 1000,6:F3} µs per binding updated  detach {Med(inside, x => x.Detach),7:F2} ms");
            Console.WriteLine($"[wpf-bench] the same pass, whole steps          build {Med(whole, x => x.Build),7:F2} ms  bind {Med(whole, x => x.Bind),7:F2} ms  " +
                              $"attach {Med(whole, x => x.Attach),7:F2} ms  push {Med(whole, x => x.Push) * 1000,6:F3} µs per binding updated  detach {Med(whole, x => x.Detach),7:F2} ms");
        }

        static void Settle()
        {
            for (var i = 0; i < 3; i++)
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        }

        static TextBlock Block(UserControl view, int i) => (TextBlock)((StackPanel)view.Content).Children[i];

        static readonly Func<Vm, string>[] Strings = { v => v.S0, v => v.S1, v => v.S2, v => v.S3, v => v.S4, v => v.S5, v => v.S6, v => v.S7, v => v.S8, v => v.S9 };
        static readonly Func<Vm, double>[] Doubles = { v => v.D0, v => v.D1, v => v.D2 };

        /// <summary>The baseline's "binding": the values the bindings would write, written with the same SetValue.</summary>
        static void Fill(UserControl view, Vm vm)
        {
            for (var i = 0; i < 10; i++)
            {
                var block = Block(view, i);
                block.Text = Strings[i](vm);
                block.Width = Doubles[i % 3](vm);
                block.IsEnabled = vm.B;
            }
        }

        /// <param name="step">told after each step (0 build … 4 detach) with the stopwatch's reading before it (DIAG only)</param>
        static Steps Run(string name, Func<UserControl> make, bool plain, Action<int, double>? step = null, Func<double>? reading = null)
        {
            var vms = Enumerable.Range(0, Shared ? 1 : Views).Select(_ => new Vm()).ToArray();
            var before = reading?.Invoke() ?? 0;
            var sw = Stopwatch.StartNew();
            var views = new UserControl[Views];
            for (var i = 0; i < Views; i++) views[i] = make();
            var build = sw.Elapsed.TotalMilliseconds;
            step?.Invoke(0, before); before = reading?.Invoke() ?? 0;
            sw.Restart();
            for (var i = 0; i < Views; i++)
            {
                var vm = vms[Shared ? 0 : i];
                if (plain) Fill(views[i], vm);
                else views[i].DataContext = vm;
            }
            Settle();
            var bind = sw.Elapsed.TotalMilliseconds;
            step?.Invoke(1, before); before = reading?.Invoke() ?? 0;
            sw.Restart();
            var panel = new StackPanel();
            foreach (var v in views) panel.Children.Add(v);
            var window = new Window { Content = new ScrollViewer { Content = panel }, Width = 400, Height = 400, ShowActivated = false, ShowInTaskbar = false };
            window.Show();
            Settle();
            var attach = sw.Elapsed.TotalMilliseconds;
            step?.Invoke(2, before); before = reading?.Invoke() ?? 0;
            var label = Block(views[^1], 0);
            if (label.Text != "s0" || Block(views[^1], 1).Width != 11) throw new Exception($"{name}: wrong values ({label.Text})");
            sw.Restart();
            for (var c = 0; c < Changes; c++)
            {
                var text = "v" + c;
                foreach (var vm in vms) { vm.S0 = text; vm.D0 = c; }
                if (plain)
                    foreach (var view in views)
                    {
                        Block(view, 0).Text = text;
                        for (var i = 0; i < 10; i += 3) Block(view, i).Width = c;
                    }
            }
            Settle();
            var push = sw.Elapsed.TotalMilliseconds / (Changes * BindingsPerChange * Views);
            step?.Invoke(3, before); before = reading?.Invoke() ?? 0;
            if (label.Text != "v19") throw new Exception($"{name}: push not applied ({label.Text})");
            sw.Restart();
            window.Close();
            foreach (var v in views) v.DataContext = null;
            Settle();
            var detach = sw.Elapsed.TotalMilliseconds;
            step?.Invoke(4, before);
            return new Steps(build, bind, attach, push, detach);
        }
    }
}
