using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using FastBindings.Diagnostics;
using FastBindings.Interfaces;

namespace FastBindings.Tests
{
    /// <summary>Collects what FastBindings reports (the logger every diagnostics test sets).</summary>
    public sealed class CollectingLogger : BindingLogger
    {
        public List<BindingIssue> Issues { get; } = new List<BindingIssue>();
        public override void Warning(BindingIssue issue) => Issues.Add(issue);
        public override void Error(BindingIssue issue) => Issues.Add(issue);
        public override void Trace(BindingIssue issue) => Issues.Add(issue);
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
    public class Poker : Border
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
        public int? Count { get => _count; set => Set(ref _count, value); }   // null fits a TextBox's text; what is typed does not fit an int?
        public object? Received { get; set; }
        public void Fail(object? value) => throw new NotSupportedException("the method failed with " + value);

        public object Convert(string? converterName, ConverterArgs args) =>
            converterName == "Explode" ? throw new FormatException("the converter failed") : args.Values[0]!;
        public object?[] ConvertBack(string? converterName, ConverterBackArgs args) =>
            converterName == "ExplodeBack" ? throw new FormatException("the back converter failed") : new[] { args.Value };
        public void Notify(NotificationArgs args)
        {
            if (args.Name == "Boom")
                throw new InvalidOperationException("the notification filter failed");
        }
    }
}
