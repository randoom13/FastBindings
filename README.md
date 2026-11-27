FastBindings (Avalonia) - https://www.nuget.org/packages/FastBindings  
 FastBindins.MAUI        - https://www.nuget.org/packages/FastBindings.MAUI/  
 FastBindings.WPF        - https://www.nuget.org/packages/FastBindings.WPF_45/ , https://www.nuget.org/packages/FastBindings.WPF_50/

1.1.3 - https://github.com/randoom13/FastBindings/wiki/1.1.3

## Tests

`FastBinding.Avalonia.Tests` runs the bindings in real views on Avalonia's headless platform (Avalonia 11.1, xUnit):

```
dotnet test FastBinding.Avalonia.Tests
```

`BugTests` holds one test per bug fixed on this branch (dotted paths, null to an interface property, the two-way start
for sync and async bindings, hidden parts built, Unsubscribe, the type caches across threads, worker threads with
DataContext swaps, `CacheStrategy.Simple`); each fails on 1.1.3. `BehaviourTests` covers ordinary use: every value kind,
DataContext swaps, values before a view is shown, a detached view, a two-way TextBox, converters, fallback and
TargetNullValue, a change on a worker thread, `IPropertyAccessor` sources, and a closed view being collected.

`FastBinding.MAUI.Tests` does the same for MAUI (.NET 8 MAUI, xUnit). MAUI raises Loaded once a view is in a `Window`, with
no platform behind it, so the views run on any machine; the library's plain `net8.0` target needs no MAUI workload:

```
dotnet test FastBinding.MAUI.Tests -p:MauiCoreOnly=true
```

It has the same bug tests (but the hidden-part one, which is Avalonia's layout) plus a test that values arrive with the
BindingContext, before Loaded; seven fail on 1.1.3.
