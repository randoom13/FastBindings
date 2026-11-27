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

`FastBinding.MAUI.Benchmark` times classic `{Binding}`, compiled `{Binding}` (`x:DataType`) and `CommonBinding` on the same views
(200 views of 30 bindings by default; build, bind, attach, value changes and detach apart):

```
dotnet run -c Release --project FastBinding.MAUI.Benchmark -p:MauiCoreOnly=true
```

`VIEWS` and `ROUNDS` set the size, `ONLY` runs the kinds whose name contains it, `DIAG=1` prints `FastBindingsDiagnostics` for one
round of `CommonBinding`.

`FastBinding.Avalonia.Benchmark` does the same for Avalonia, headless: compiled `{Binding}`, `{ReflectionBinding}` and
`CommonBinding`, flat and dotted paths (N views of 3 bindings, create, bind, layout, push, layout after and unbind apart, time and
bytes), after checking that every case shows the same values:

```
dotnet run -c Release --project FastBinding.Avalonia.Benchmark                  # N=2000 ROUNDS=7
dotnet run -c Release --project FastBinding.Avalonia.Benchmark -p:BenchAvaloniaVersion=12.0.0   # on Avalonia 12
MODE=checks dotnet run -c Release --project FastBinding.Avalonia.Benchmark      # the fixes' checks only
```

`MODE=warmup CASE=compiled|fastbindings-common|fastbindings-accessor|reflection` shows what a binding costs on its first and later
calls; `MODE=push CASE=…` only pushes, for SECONDS, for a CPU profile.

## WPF

`FastBinding.Net5` (.NET 5+) and `FastBinding.Net4.5` (.NET Framework 4.5) have the same fixes; both build on any machine with
`dotnet build` and are tested on Windows:

```
dotnet test FastBinding.Net5.Tests      # WPF on .NET 8, Windows
dotnet test FastBinding.Net4.5.Tests    # WPF on .NET Framework 4.8, Windows
dotnet run -c Release --project FastBinding.Net5.Benchmark    # classic {Binding} and CommonBinding on the same views
```

`.github/workflows/tests.yml` runs every test project (Avalonia and MAUI on Linux, WPF on Windows), the WPF tests once more against
1.1.3 to show what each catches, and the WPF benchmark.
