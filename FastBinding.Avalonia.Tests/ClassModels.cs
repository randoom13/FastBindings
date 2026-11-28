using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FastBindings.Interfaces;

namespace FastBindings.Tests
{
    public sealed class ClassRow : Observable
    {
        private bool _picked;
        public bool Picked { get => _picked; set => Set(ref _picked, value); }
    }

    /// <summary>For the <see cref="Views.ClassesView"/> view.</summary>
    public sealed class ClassModel : Observable, IForwardValueConverter
    {
        private bool _isOn = true;
        private bool _isOff;
        private QuietLeaf? _child;
        public bool IsOn { get => _isOn; set => Set(ref _isOn, value); }
        public bool IsOff { get => _isOff; set => Set(ref _isOff, value); }
        public QuietLeaf? Child { get => _child; set => Set(ref _child, value); }
        public ObservableCollection<ClassRow> Rows { get; } = new ObservableCollection<ClassRow> { new ClassRow(), new ClassRow { Picked = true } };

        public object Convert(string? converterName, ConverterArgs args) =>
            converterName == "Not" ? !(args.Values[0] is true) : args.Values[0]!;
    }

    public class ClassProbe : Border { }

    /// <summary>
    /// What Avalonia's XAML compiler hands a bool-returning extension set on <c>Classes.name</c>: the control as the target and the
    /// text "class:name" as the property. BoolCommonBinding rests on it; this keeps an Avalonia upgrade that changes it from going
    /// unnoticed (the classes would then never be bound).
    /// </summary>
    public sealed class ClassTargetProbe
    {
        public static object? LastTarget;
        public static object? LastProperty;

        public bool ProvideValue(IServiceProvider serviceProvider)
        {
            var target = (IProvideValueTarget?)serviceProvider.GetService(typeof(IProvideValueTarget));
            LastTarget = target?.TargetObject;
            LastProperty = target?.TargetProperty;
            return true;
        }
    }
}
