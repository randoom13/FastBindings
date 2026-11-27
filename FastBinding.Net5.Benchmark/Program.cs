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
    /// changes (20 × two properties of the shared view-model) and detach (window closed, DataContext cleared), median of ROUNDS.
    /// WPF has no compiled bindings: classic {Binding} is what an app has without FastBindings.
    /// </summary>
    public static class Program
    {
        static readonly int Views = int.Parse(Environment.GetEnvironmentVariable("VIEWS") ?? "200");
        static readonly int Rounds = int.Parse(Environment.GetEnvironmentVariable("ROUNDS") ?? "15");

        [STAThread]
        public static int Main()
        {
            var only = Environment.GetEnvironmentVariable("ONLY");
            var kinds = new (string Name, Func<UserControl> Make)[]
            {
                ("classic {Binding}", () => new Classic()),
                ("FastBindings CommonBinding", () => new Fast()),
            };
            Console.WriteLine($"[wpf-bench] {Environment.GetEnvironmentVariable("LABEL")} views={Views} bindings/view=30 rounds={Rounds}");
            foreach (var (name, make) in kinds)
            {
                if (only != null && !name.Contains(only)) continue;
                var cold = Run(name, make);
                var m = Enumerable.Range(0, Rounds).Select(_ => Run(name, make)).ToList();
                double Med(Func<(double Build, double Bind, double Attach, double Push, double Detach), double> f) { var v = m.Select(f).OrderBy(x => x).ToList(); return v[v.Count / 2]; }
                Console.WriteLine($"[wpf-bench] {name,-28} build {Med(x => x.Build),7:F2} ms  bind {Med(x => x.Bind),7:F2} ms  attach {Med(x => x.Attach),7:F2} ms  " +
                                  $"values {Med(x => x.Build + x.Bind + x.Attach),7:F2} ms  push {Med(x => x.Push) * 1000,6:F3} µs/binding  detach {Med(x => x.Detach),7:F2} ms  " +
                                  $"(cold first round: values {cold.Build + cold.Bind + cold.Attach:F1} ms)");
            }
            return 0;
        }

        static void Settle()
        {
            for (var i = 0; i < 3; i++)
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        }

        static (double Build, double Bind, double Attach, double Push, double Detach) Run(string name, Func<UserControl> make)
        {
            var vm = new Vm();
            var sw = Stopwatch.StartNew();
            var views = new UserControl[Views];
            for (var i = 0; i < Views; i++) views[i] = make();
            var build = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            foreach (var v in views) v.DataContext = vm;
            Settle();
            var bind = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            var panel = new StackPanel();
            foreach (var v in views) panel.Children.Add(v);
            var window = new Window { Content = new ScrollViewer { Content = panel }, Width = 400, Height = 400, ShowActivated = false, ShowInTaskbar = false };
            window.Show();
            Settle();
            var attach = sw.Elapsed.TotalMilliseconds;
            var label = (TextBlock)((StackPanel)views[^1].Content).Children[0];
            if (label.Text != "s0" || ((TextBlock)((StackPanel)views[^1].Content).Children[1]).Width != 11) throw new Exception($"{name}: wrong values ({label.Text})");
            const int changes = 20;
            sw.Restart();
            for (var c = 0; c < changes; c++) { vm.S0 = "v" + c; vm.D0 = c; }
            Settle();
            var push = sw.Elapsed.TotalMilliseconds / (changes * 2.0 * Views);
            if (label.Text != "v19") throw new Exception($"{name}: push not applied ({label.Text})");
            sw.Restart();
            window.Close();
            foreach (var v in views) v.DataContext = null;
            Settle();
            var detach = sw.Elapsed.TotalMilliseconds;
            return (build, bind, attach, push, detach);
        }
    }
}
