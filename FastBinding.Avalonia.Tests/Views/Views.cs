using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FastBindings.Tests.Views
{
    public partial class Flat : UserControl { public Flat() => AvaloniaXamlLoader.Load(this); }
    public partial class Dotted : UserControl { public Dotted() => AvaloniaXamlLoader.Load(this); }
    public partial class Lists : UserControl { public Lists() => AvaloniaXamlLoader.Load(this); }
    public partial class TwoWayCombo : UserControl { public TwoWayCombo() => AvaloniaXamlLoader.Load(this); }
    public partial class AsyncTwoWayCombo : UserControl { public AsyncTwoWayCombo() => AvaloniaXamlLoader.Load(this); }
    public partial class HiddenList : UserControl { public HiddenList() => AvaloniaXamlLoader.Load(this); }
    public partial class Cached : UserControl { public Cached() => AvaloniaXamlLoader.Load(this); }
    public partial class TwoWayText : UserControl { public TwoWayText() => AvaloniaXamlLoader.Load(this); }
    public partial class Converted : UserControl { public Converted() => AvaloniaXamlLoader.Load(this); }
    public partial class Fallback : UserControl { public Fallback() => AvaloniaXamlLoader.Load(this); }
    public partial class NullValue : UserControl { public NullValue() => AvaloniaXamlLoader.Load(this); }
    public partial class Accessor : UserControl { public Accessor() => AvaloniaXamlLoader.Load(this); }
    public partial class Identity : UserControl { public Identity() => AvaloniaXamlLoader.Load(this); }
    public partial class SwapCombo : UserControl { public SwapCombo() => AvaloniaXamlLoader.Load(this); }
}
