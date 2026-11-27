using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FastBindings.Diagnostics;
using FastBindings.Interfaces;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;

namespace FastBindings.Tests
{
    /// <summary>Collects what FastBindings reports (the logger every diagnostics test sets).</summary>
    public sealed class CollectingLogger : BindingLogger
    {
        private readonly object _gate = new object();
        private readonly List<BindingIssue> _issues = new List<BindingIssue>();
        public IReadOnlyList<BindingIssue> Issues { get { lock (_gate) return _issues.ToArray(); } }
        private void Add(BindingIssue issue) { lock (_gate) _issues.Add(issue); }
        public override void Warning(BindingIssue issue) => Add(issue);
        public override void Error(BindingIssue issue) => Add(issue);
        public override void Trace(BindingIssue issue) => Add(issue);
    }

    /// <summary>A logger that throws: a binding must work on regardless.</summary>
    public sealed class ThrowingLogger : BindingLogger
    {
        public int Calls;
        public override void Warning(BindingIssue issue) { Calls++; throw new InvalidOperationException("logger"); }
        public override void Error(BindingIssue issue) { Calls++; throw new InvalidOperationException("logger"); }
    }

    public sealed class PokeArgs : EventArgs
    {
        public string Value => throw new InvalidOperationException("no value in this poke");
    }

    /// <summary>A control with an event whose arguments throw when read.</summary>
    public class Poker : ContentView
    {
        public event EventHandler<PokeArgs>? Poked;
        public void Poke() => Poked?.Invoke(this, new PokeArgs());
    }

    /// <summary>A view-model that fails in every way a binding can meet.</summary>
    public sealed class Faulty : Observable, IForwardValueConverter, IBackValueConverter, INotificationFilter
    {
        private string _a = "a";
        public string A { get => _a; set => Set(ref _a, value); }
        public string Throws => throw new InvalidOperationException("the getter failed");
        public string Rejects { get => "r"; set => throw new ArgumentException("the setter refused " + value); }
        public int Negative => -1;
        public Task<string> Failing => Task.FromException<string>(new TimeoutException("the task failed"));
        public string WriteOnly { set { } }
        public string ReadOnly => "read only";
        private int? _count;
        public int? Count { get => _count; set => Set(ref _count, value); }   // null fits an Entry's text; what is typed does not fit an int?
        public object? Received { get; set; }
        public void Fail(object? value) => throw new NotSupportedException("the method failed with " + value);

        public object? Convert(string? converterName, ConverterArgs args) =>
            converterName == "Explode" ? throw new FormatException("the converter failed") : args.Values[0];
        public object?[] ConvertBack(string? converterName, ConverterBackArgs args) =>
            converterName == "ExplodeBack" ? throw new FormatException("the back converter failed") : new[] { args.Value };
        // asked only by the binding that names a notification (NotificationName=Boom)
        public void Notify(NotificationArgs args) => throw new InvalidOperationException("the notification filter failed");
    }

    /// <summary>An IProvideValueTarget whose parents throw when read: what MAUI's non-public API may do (ParentNotFound).</summary>
    public sealed class ThrowingParents : IProvideValueTarget, IServiceProvider
    {
        public ThrowingParents(object target, object property) { TargetObject = target; TargetProperty = property; }
        public object TargetObject { get; }
        public object TargetProperty { get; }
        public IEnumerable<object> ReadParents() => throw new InvalidOperationException("no parents here");
        public object? GetService(Type serviceType) => serviceType == typeof(IProvideValueTarget) ? this : null;
    }
}

namespace FastBindings.Tests
{
    /// <summary>A control whose Level refuses a negative value from its property-changed handler (MAUI's SetValue does not throw on validation).</summary>
    public class Refuser : Microsoft.Maui.Controls.ContentView
    {
        public static readonly Microsoft.Maui.Controls.BindableProperty LevelProperty = Microsoft.Maui.Controls.BindableProperty.Create(
            nameof(Level), typeof(int), typeof(Refuser), 0,
            propertyChanged: (_, _, value) => { if ((int)value < 0) throw new ArgumentOutOfRangeException(nameof(Level), value, "no negative level"); });

        public int Level { get => (int)GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
    }
}
