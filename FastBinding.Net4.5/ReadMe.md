> FastBinding, AsyncFastBinding, CommonBinding, AsyncCommonBinding,
OneWayFastBinding, AsyncOneWayFastBinding, OneWayCommonBinding, AsyncOneWayCommonBinding

- Common - use cached tree expressions without implementing IAccessProperties
- Async - works asynchronously and can operate with Task<?> property.
- Fast - can not operate without implementing IAccessProperties
- OneWay - it works as described in one way, it is completely different from using other bindings 
and it allows binding to a property with a getter only. For this binding type it is possible to interact using
ISubscriber from ViewModel getter. Binding can proper handle this case with next condition: => Target="-Information.Desc-"

Example
```xml
<Label>
 <custom:AttachedOneWayCollection.Collections>
 <custom:OneWayBindinCollection>
 <OneWayCommonBinding Sources="$[[this].SizeChanged][EventArgs.NewSize.Height]" Target="ApplyHeight("/>
 </custom:OneWayBindinCollection>
 </custom:AttachedOneWayCollection.Collections>`
 </Label>`
```


IDEA

One of the bottlenecks where WPF application loses performance is in the bindings.
Unfortunatelly binding access to ViewModel properties through reflection. The existing
mechanism is a monolith that is closed for changes from outside. It can be improved
if access to ViewModel properties is done directly, for example, through some contract:
```xml
   interface IAccessProperties
  {
    object GetProperty(string name);
    void SetProperty(string name, object value);
  }
```

REQUIREMENTS (for -Fast-)
------------------------------------------------------------------------------------------------
To use new -Fast- we need to implement described contract in ViewModel.
Or use class CommonViewModel<T> as parent for ViewModel, if it is possible, or implement it like
it is done in CommonViewModel<T>. Under the hood CommonViewModel works with properties through a
 cached tree expression.

-----------
1) The most effective way
--
If you do not plan to implement IAccessProperties in the future, 
I recommend using -Common- Bindings. 
Under the hood, they use cached tree expressions that work in the same way as the code in 2-3 below.
```xml 
   using FastBindings.Common;
   public class MainWindowViewModelV2 : BaseViewModel
   {
       public override object? GetProperty(string propertyName)
       {
           switch (propertyName)
           {
              case nameof(Text):
                   return Text;
           }
           return null;
       }
       public override void SetProperty(string propertyName, object? value)
       {
          switch (propertyName)
           {
              case nameof(Text):
                   if (value?.GetType() == Text.GetType())
                       Text = (string)value;
                   break;
          }
     }
   }
```
--------
2) Simple and fast way
--
using FastBindings.Common;
public class MainWindowViewModel : CommonBaseViewModel<MainWindowViewModel>{ .....}
-----------
3) If the parent class already exists
--
```xml
using FastBindings.Common;
using FastBindings.Interfaces;
public class MainWindowViewModel  : <ParentClass>, IPropertyAccessor
{
    	private PropertyHolder<MainWindowViewModel> _holder;
    public MainWindowViewModel()
    {
        _holder = new PropertyHolder<MainWindowViewModel>();
    }
    public object? GetProperty(string propertyName)
    {
        return _holder.GetProperty(propertyName);
    }
    public void SetProperty(string propertyName, object? value)
    {
        _holder.SetProperty(propertyName, value);
    }
}
```
[Sources]
----------
Property Sources allows to use one or simultaneously few objects and devider between sources ;
It supports not only familiar same dot notation from ViewModels 
(Outgoing.Notification.Message), but framework properties and events.
P.S. Without forward or back converter target will be interact only with first source in list
P.S. NOW, FOR VIEW MODEL WITH FAMILIAR DOT NATION, THE USER CAN ALSO USE THE FOLLOWING FORMAT
 : < Outgoing.Notification.SetHeight( >, WHICH WILL ATTEMPT TO INVOKE METHOD SetHeight

Formats for framework classes in Sources:
```xml
$[[control_name].dependencyProperty(event)][optional] — control on the target level
$[[ancsestorType/ancestorsLevel].dependencyProperty(event)][optional] — parent control above target
$[[this].dependencyProperty(event)][optional] — target itself
```
p/s For event binding returns result as EventInfoArgs.

optional - An additional optional field that helps get/set values with using 
the same dot notation as for accessing ViewModel properties.
```xml
$[[this].Margin][Left]
```
[DataSourcePath]
------------------
By default for ViewModel we using DataSource from target element.
DataSourcePath property allows us to use DataSource from another place

Formats:
```xml
<control_name> (panel)- on the target level
<ancsestorType/ancestorsLevel>(ListBox/1)— parent control above
```

[Converter], [ConverterPath], [ConverterName]
------------------------------------------
Now converter can be both at StaticResources level (property Converter) and at ViewModel level
(ConverterPath), but not simultaneously at the same time.
Priority goes from staticResource level converter to ViewModel level converter.
And ViewModel level converter requires that converter has concrete name (property ConverterName).
User is not required to create simultaneously back and forward initialization.
P/S Using a converter guarantees that you can receive any exceptions that the ViewModel property might throw.
    [ConverterPath] formats see [DataSourcePath] formats

[CacheStrategy]
-----------------
Property CacheStrategy. It is option to improve efficiency during interaction with ViewModels.
None
Simple. If user uses few bindings from one ViewModel property(no long form),
during update this property it will be read only once


[Notification], [NotificationPath], [NotificationName]
------------------------------------------------------
NotificationFilter (from Source to Target, Target to Source)
Now it allows us to skip update from Source to Target, Target to Source.
[NotificationPath] - path to ViewModel with implemented INotificationFilter
For AsyncBinding we receive events not only before and after the update.

[FallbackValue]
--------------------
If viewModel property returns Exception, WPF will use this value instead

Examples:
```xml
<TextBox Text="{custom:FastBinding Mod.MyProperty;MyProperty1;$[[RRR].ActualWidth], CacheStrategy=Simple,
          ConverterName=Test, ConverterPath=Mod}" />
                <TextBlock Text="{custom:FastBinding Mod.MyProperty;MyProperty2;$[[ListBox/1].ActualWidth], CacheStrategy=Simple,ConverterName=Test, ConverterPath=Mod}" />
<TextBox Text="{custom:AsyncFastBinding Mod.Test, NotificationName =UU, NotificationPath=Mod}" />
```

#FIXED
It will work
```xml
<Label>
<Label.Background>
<SolidColorBrush Color={custom:CommonBinding Color}/>
</Label.Background>
</Label>
```