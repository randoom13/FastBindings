using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AvaloniaBench;

public class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class Leaf : Observable
{
    private string _a = "a";
    private double _w = 10;
    private bool _v = true;

    public string A { get => _a; set => Set(ref _a, value); }
    public double W { get => _w; set => Set(ref _w, value); }
    public bool V { get => _v; set => Set(ref _v, value); }
}

public sealed class Vm : Observable
{
    private string _a = "a";
    private double _w = 10;
    private bool _v = true;
    private Leaf? _child = new();

    public string A { get => _a; set => Set(ref _a, value); }
    public double W { get => _w; set => Set(ref _w, value); }
    public bool V { get => _v; set => Set(ref _v, value); }
    public Leaf? Child { get => _child; set => Set(ref _child, value); }
}

/// <summary>FastBindings' own fast path: the view-model answers GetProperty/SetProperty by name (IPropertyAccessor), no reflection.</summary>
public sealed class AccVm : Observable, FastBindings.Interfaces.IPropertyAccessor
{
    private string _a = "a";
    private double _w = 10;
    private bool _v = true;

    public string A { get => _a; set => Set(ref _a, value); }
    public double W { get => _w; set => Set(ref _w, value); }
    public bool V { get => _v; set => Set(ref _v, value); }

    public object? GetProperty(string propertyName) => propertyName switch { "A" => _a, "W" => _w, "V" => _v, _ => null };

    public void SetProperty(string propertyName, object? value)
    {
        switch (propertyName)
        {
            case "A": A = (string)value!; break;
            case "W": W = (double)value!; break;
            case "V": V = (bool)value!; break;
        }
    }
}
