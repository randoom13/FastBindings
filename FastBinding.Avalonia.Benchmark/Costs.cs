using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AvaloniaBench;

/// <summary>MODE=costs: what single steps of a binding's set-up cost, measured apart (bytes on this thread, time per step).</summary>
public static class Costs
{
    static (double Us, double Bytes) Measure(int n, Action step)
    {
        for (var i = 0; i < 1000; i++) step();   // warm
        GC.Collect(); GC.WaitForPendingFinalizers();
        var bytes = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < n; i++) step();
        return (Stopwatch.GetElapsedTime(start).TotalMilliseconds * 1000 / n, (GC.GetAllocatedBytesForCurrentThread() - bytes) / (double)n);
    }

    public static int Run()
    {
        const int n = 20000;
        void Show(string what, (double Us, double Bytes) m) => Console.WriteLine($"[costs] {what,-58} {m.Us,8:F3} µs {m.Bytes,8:F0} B");
        Show("new object()", Measure(n, () => _ = new object()));
        Show("new AvaloniaObject()", Measure(n, () => _ = new AvaloniaObject()));
        Show("new CommonBinding(\"A\")", Measure(n, () => _ = new FastBindings.CommonBinding("A")));
        const string pattern = @"\$\[\[(?<source>.+?)\]\.(?<property>.+?)\](\[(?<optional>.+?)\])?";
        var shared = new Regex(pattern);
        Show("new Regex(pattern).Match (per $[[...]] source now)", Measure(n, () => _ = new Regex(pattern).Match("$[[this].Bounds]")));
        Show("static Regex.Match (one Regex kept)", Measure(n, () => _ = shared.Match("$[[this].Bounds]")));
        var text = new TextBlock();
        Show("GetFields(...).Where(Name == ...) (FindDependencyPropertyByName)", Measure(n, () => _ = text.GetType()
            .GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.FlattenHierarchy)
            .Where(it => it.Name == "BoundsProperty").ToList().SingleOrDefault()?.GetValue(text)));
        Show("AvaloniaPropertyRegistry.FindRegistered(obj, name)", Measure(n, () => _ = AvaloniaPropertyRegistry.Instance.FindRegistered(text, "Bounds")));
        // a binding of a control's own property started: the whole $[[this]] path (parse, property lookup, subscription)
        var panel = new StackPanel(); var window = new Window { Content = panel }; window.Show();
        Show("a $[[this].Tag] binding started on an attached TextBlock", Measure(2000, () =>
        {
            var t = new TextBlock(); panel.Children.Add(t);
            new FastBindings.CommonBinding("$[[this].Tag]").ProvideValue(new Target(t, TextBlock.TextProperty));
            panel.Children.Clear();
        }));
        Show("a plain binding started on an attached TextBlock (to compare)", Measure(2000, () =>
        {
            var t = new TextBlock { DataContext = new Vm() }; panel.Children.Add(t);
            new FastBindings.CommonBinding("A").ProvideValue(new Target(t, TextBlock.TextProperty));
            panel.Children.Clear();
        }));
        window.Close(); Dispatcher.UIThread.RunJobs();
        // what a target property's first write costs (TargetSetters compiles an expression per property, once)
        FastBindings.FastBindingsDiagnostics.Enabled = true;
        var props = new AvaloniaProperty[] { Control.TagProperty, TextBlock.FontSizeProperty, Visual.OpacityProperty, Avalonia.Layout.Layoutable.MinWidthProperty,
            Avalonia.Layout.Layoutable.MaxWidthProperty, Avalonia.Layout.Layoutable.HeightProperty, Avalonia.Layout.Layoutable.MinHeightProperty, Visual.IsVisibleProperty, Avalonia.Input.InputElement.IsEnabledProperty, TextBlock.LetterSpacingProperty };
        var before = (FastBindings.FastBindingsDiagnostics.Compiles, FastBindings.FastBindingsDiagnostics.CompileTime);
        var panel2 = new StackPanel(); var w2 = new Window { Content = panel2 }; w2.Show();
        var sw1 = Stopwatch.StartNew();
        foreach (var p in props)
        {
            var t = new TextBlock { DataContext = new Vm() }; panel2.Children.Add(t);
            new FastBindings.CommonBinding(p.PropertyType == typeof(double) ? "W" : p.PropertyType == typeof(bool) ? "V" : "A").ProvideValue(new Target(t, p));
        }
        Console.WriteLine($"[costs] first writes of {props.Length} target properties, bindings started: {sw1.Elapsed.TotalMilliseconds:F1} ms");
        var compiles = FastBindings.FastBindingsDiagnostics.Compiles - before.Compiles;
        var time = FastBindings.FastBindingsDiagnostics.CompileTime - before.CompileTime;
        Console.WriteLine($"[costs] first writes of {props.Length} new target properties: {compiles} expression compiles, {time.TotalMilliseconds:F1} ms ({(compiles > 0 ? time.TotalMilliseconds / compiles : 0):F2} ms each)");
        // ten more, the expression compiler warm now
        var more = new AvaloniaProperty[] { Avalonia.Layout.Layoutable.WidthProperty, Avalonia.Layout.Layoutable.MaxHeightProperty, TextBlock.LineHeightProperty,
            Visual.ZIndexProperty, TextBlock.TextProperty, Visual.ClipToBoundsProperty, Avalonia.Input.InputElement.FocusableProperty, Avalonia.Input.InputElement.IsHitTestVisibleProperty,
            TextBlock.MaxLinesProperty, Avalonia.Input.InputElement.IsTabStopProperty };
        before = (FastBindings.FastBindingsDiagnostics.Compiles, FastBindings.FastBindingsDiagnostics.CompileTime);
        var sw2 = Stopwatch.StartNew();
        foreach (var p in more)
        {
            var t = new TextBlock { DataContext = new Vm() }; panel2.Children.Add(t);
            var path = p.PropertyType == typeof(double) ? "W" : p.PropertyType == typeof(bool) ? "V" : p.PropertyType == typeof(int) ? "I" : "A";
            new FastBindings.CommonBinding(path).ProvideValue(new Target(t, p));
        }
        Console.WriteLine($"[costs] then {more.Length} more: {sw2.Elapsed.TotalMilliseconds:F1} ms");
        compiles = FastBindings.FastBindingsDiagnostics.Compiles - before.Compiles;
        time = FastBindings.FastBindingsDiagnostics.CompileTime - before.CompileTime;
        Console.WriteLine($"[costs] then {more.Length} more: {compiles} expression compiles, {time.TotalMilliseconds:F1} ms ({(compiles > 0 ? time.TotalMilliseconds / compiles : 0):F2} ms each)");
        w2.Close();
        return 0;
    }

    sealed class Target(object targetObject, object targetProperty) : IServiceProvider, Avalonia.Markup.Xaml.IProvideValueTarget
    {
        public object TargetObject => targetObject;
        public object TargetProperty => targetProperty;
        public object? GetService(Type serviceType) => serviceType == typeof(Avalonia.Markup.Xaml.IProvideValueTarget) ? this : null;
    }
}
