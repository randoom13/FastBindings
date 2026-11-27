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

## Results by platform

Commit `1e40938` makes starting a binding cheaper on every platform (`1e40938`):
- a one-name path keeps its source's getter per source type: the first value is one type check and one delegate call, not the path built again and two lookups through a closure (0.6 µs per binding before, timed inside FastBindings on MAUI);
- one weak reference per state manager for all its subscriptions (each subscription made two GC handles, one thrown away); one source needs no split, LINQ or closure;
- MAUI and Avalonia: one start handler per element for all of its bindings, instead of every binding adding and removing its own (on MAUI removing them cost 0.65 µs per binding; on Avalonia each removal allocated a new delegate).

Each table: 6 000 bindings per round, Release, stopwatch medians, no profiler attached; times in ms unless marked µs. MAUI and Avalonia ran in a Linux container, WPF on windows-latest.

**Avalonia** (`FastBinding.Avalonia.Benchmark`, headless Avalonia 12, 2 000 views × 3 bindings, six runs: median [min–max]):

| | create | bind | push | layout | unbind |
|---|---|---|---|---|---|
| compiled `{Binding}` | 20.1 [17.9–22.1] | 11.6 [11.4–14.2] | 24.6 [24.1–27.9] | 77.5 [73.8–79.2] | 13.2 [12.5–14.6] |
| reflection `{ReflectionBinding}` | 17.9 [17.3–20.0] | 11.6 [11.0–12.4] | 24.8 [23.0–29.8] | 76.5 [72.4–80.5] | 13.3 [12.2–14.9] |
| this branch | 14.1 [13.6–14.5] | 12.9 [12.7–13.4] | 25.6 [24.1–26.3] | 75.2 [74.0–82.2] | 11.5 [11.0–12.2] |
| this branch before `1e40938` | 14.0–14.6 | 16.0–17.3 | 28.5–38.7 | 77.6–81.4 | 12.6–13.4 |

FastBindings' bind still allocates more (9.3 MB against 3.7 MB; 13.1 MB before `1e40938`). With a dotted path (`Child.X`) the picture is the same: create 14.3 against 20.5 / 19.0, bind 16.3 against 15.1 / 16.1, push 26.5 against 26.1 / 26.8, unbind 12.8 against 15.6 / 15.3 (compiled / reflection).

**MAUI** (`FastBinding.MAUI.Benchmark`, MAUI 8 without a platform, 200 views × 30 bindings, one view-model shared; two alternating runs):

| | build | bind | push, per binding | detach |
|---|---|---|---|---|
| classic `{Binding}` | 11.1–12.8 | 13.9–17.4 | 2.48–2.52 µs | 196–206 |
| compiled `{Binding}` | 9.6–9.9 | 9.5–9.6 | 2.35–2.66 µs | 187–195 |
| this branch | 17.1–18.4 | 16.1 | 1.81–1.91 µs | 15.7 |
| this branch before `1e40938` | 15.2–17.7 | 19.8–20.3 | 1.94–1.95 µs | 15.2–16.3 |

MAUI's own bindings spend about 85 % of their detach in `TypedBinding.Unapply`/`Unsubscribe` (CPU profile). FastBindings' slow rounds are garbage-collection pauses (measured with `GC.GetTotalPauseDuration`).

**WPF** (`profile` workflow, windows-latest, 200 views × 30 bindings, one view-model per view; two alternating runs, before and after `1e40938`):

| | build | bind | push, per binding | detach |
|---|---|---|---|---|
| .NET 8, no bindings | 24.5–27.2 | 13.1–17.9 | 2.3–3.1 µs | 12.0–13.6 |
| .NET 8, classic `{Binding}` | 28.6–38.7 | 21.7–24.4 | 2.8–3.4 µs | 22.7–24.5 |
| .NET 8, this branch | 53.4–60.8 | 27.0–29.3 | 2.3–2.4 µs | 17.9–18.4 |
| .NET 8, before `1e40938` | 72.6–76.0 | 22.8–26.2 | 2.3–2.4 µs | 18.9–19.2 |
| .NET Framework 4.8, no bindings | 20.8–22.0 | 13.2–22.4 | 2.8–3.3 µs | 10.8–11.8 |
| .NET Framework 4.8, classic `{Binding}` | 56.3–59.4 | 49.0–56.1 | 3.7–3.8 µs | 28.8–31.5 |
| .NET Framework 4.8, this branch | 80.2–81.2 | 31.7–32.9 | 2.9–3.0 µs | 16.0–16.1 |
| .NET Framework 4.8, before `1e40938` | 87.6–93.5 | 30.9–32.0 | 3.0–3.1 µs | 16.3–17.2 |

On WPF a binding starts inside `ProvideValue`, so the change shows in *build* (−18 ms on .NET 8, −10 ms on .NET Framework 4.8). The .NET 8 bind came out 3–4 ms higher after it, within its spread; with two runs that is not settled.

What is left, from CPU profiles and timing inside FastBindings: each binding adds its own permanent DataContext handler, each state manager allocates its event publisher whether used or not, the direct-push delegate is made per binding, and on WPF building a view with a custom markup extension costs more than with `{Binding}`.

## Releasing the packages

`.github/workflows/release.yml` makes the next version of all four packages (`FastBindings` for Avalonia, `FastBindings.MAUI`,
`FastBindings.WPF_50` for WPF on .NET 5+ and `FastBindings.WPF_45` for WPF on .NET Framework 4.5), all with one version:
- **Run it** from the Actions tab: *release* → *Run workflow*, choose which part to raise (patch, minor or major).
- **The version** is the highest stable version on nuget.org (or tag `vX.Y.Z`) with that part raised: `scripts/next-version.py`.
- **What it does:** builds and tests everything on Windows (with the MAUI workload, so `FastBindings.MAUI` has its Android,
  iOS, Mac Catalyst and Windows libraries), packs with `scripts/pack.sh` and checks each package's contents with
  `scripts/check-packages.py`. The packages are kept as the run's artifact.
- **Publish** (a tick when starting it): tags the commit `vX.Y.Z`, makes a GitHub release with the packages and, when the
  repository has a `NUGET_API_KEY` secret (Settings → Secrets and variables → Actions), pushes them to nuget.org.
- Locally: `scripts/pack.sh 1.1.4 out` (`MAUI_CORE_ONLY=1` without the MAUI workload), then `scripts/check-packages.py out 1.1.4`.

`FastBindings.WPF` (`FastBinding.Package`) is not released: the WPF packages are `FastBindings.WPF_50` and `FastBindings.WPF_45`.
