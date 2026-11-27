using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FastBindings.Interfaces;

namespace FastBindings.Tests
{
    public class Observable : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            Raise(name);
        }

        public void Raise(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class Leaf : Observable
    {
        private string _a = "leaf";
        public string A { get => _a; set => Set(ref _a, value); }
    }

    public sealed class DelegateCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
    }

    /// <summary>The view-model of every view in the tests; each view reads what it needs.</summary>
    public sealed class Vm : Observable, IForwardValueConverter
    {
        private string _a = "a";
        private double _w = 10;
        private bool _v = true;
        private Leaf? _child = new Leaf();
        private IReadOnlyList<string>? _list = new[] { "one", "two" };
        private ICommand? _command = new DelegateCommand();
        private IReadOnlyList<string> _items = new[] { "zero", "one", "two" };
        private int _index = 1;
        private bool _showDetails;
        private string _counted = "first";
        private string? _maybe = "something";
        private bool _throw;

        public string A { get => _a; set => Set(ref _a, value); }
        public double W { get => _w; set => Set(ref _w, value); }
        public bool V { get => _v; set => Set(ref _v, value); }
        public Leaf? Child { get => _child; set => Set(ref _child, value); }
        public IReadOnlyList<string>? List { get => _list; set => Set(ref _list, value); }
        public ICommand? Command { get => _command; set => Set(ref _command, value); }
        public IReadOnlyList<string> Items { get => _items; set => Set(ref _items, value); }
        public int Index { get => _index; set => Set(ref _index, value); }
        public bool ShowDetails { get => _showDetails; set => Set(ref _showDetails, value); }
        public string? Maybe { get => _maybe; set => Set(ref _maybe, value); }

        /// <summary>How often <see cref="Counted"/> was read.</summary>
        public int CountedReads { get; set; }

        public string Counted
        {
            get { CountedReads++; return _counted; }
            set => Set(ref _counted, value);
        }

        /// <summary>Throws while <see cref="ThrowOnRead"/> is set.</summary>
        public string Throwing => _throw ? throw new InvalidOperationException("no value") : "fine";

        public bool ThrowOnRead
        {
            get => _throw;
            set { _throw = value; Raise(nameof(Throwing)); }
        }

        // IForwardValueConverter: ConverterName=Upper with no ConverterPath uses the DataContext itself
        public object Convert(string? converterName, ConverterArgs args) =>
            converterName == "Upper" ? (args.Values[0] as string)?.ToUpperInvariant() ?? "" : args.Values[0] ?? "";
    }

    /// <summary>FastBinding's view-model: it answers by name (IPropertyAccessor), no reflection.</summary>
    public sealed class AccessorVm : Observable, IPropertyAccessor
    {
        private string _a = "a";
        public string A { get => _a; set => Set(ref _a, value); }
        public object? GetProperty(string propertyName) => propertyName == nameof(A) ? _a : null;
        public void SetProperty(string propertyName, object? value) { if (propertyName == nameof(A)) A = (string)value!; }
    }
}
