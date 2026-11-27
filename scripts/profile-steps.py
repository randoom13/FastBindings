#!/usr/bin/env python3
"""
Splits a CPU profile of a FastBindings benchmark by step and by what the time was spent in.

The benchmarks run each step inside a method named Step_<Name> (Step_Build, Step_Bind, ...). A sample counts for the step
whose method is on its stack; only the thread those methods run on (the UI thread) is read, and the script refuses a profile
where they ran on more than one. Input: a speedscope JSON (dotnet-trace convert --format speedscope, or ETW stacks written by
tools/etw-stacks in the same shape).

For each step:
- "self": the time of each sample goes to the nearest frame from the top of the stack that belongs to a category (the code
  that was running, or the closest known code it was called from);
- "inclusive": the share of samples with the framework's binding engine, or FastBindings, anywhere on the stack under the step
  (that code and everything it called: SetValue, layout invalidation, ...).

Usage: profile-steps.py PROFILE.speedscope.json --platform wpf|maui|avalonia [--rounds N]
"""
import argparse, collections, json, re, sys

ap = argparse.ArgumentParser()
ap.add_argument("speedscope")
ap.add_argument("--platform", required=True, choices=["wpf", "maui", "avalonia"])
ap.add_argument("--rounds", type=int, default=0, help="rounds the profiled run made, to print ms per round")
ap.add_argument("--label", default="")
args = ap.parse_args()

ENGINE = {
    "wpf": r"MS\.Internal\.Data\.|System\.Windows\.Data\.|BindingExpression|PropertyPathWorker|ClrBindingWorker|BindingOperations",
    "maui": r"Microsoft\.Maui\.Controls\.(Binding|BindingBase|BindingExpression|TypedBinding|Internals\.TypedBinding|BindingExpressionHelper)\b|BindingExpression",
    "avalonia": r"Avalonia\.Data\.|Avalonia\.Markup\.Xaml\.MarkupExtensions\.CompiledBinding|CompiledBindingPath|BindingExpression|ExpressionNode|Avalonia\.Markup\.Parsers|ReflectionBinding",
}[args.platform]

CATS = {
    "wpf": [
        ("FastBindings", r"FastBindings\."),
        ("framework binding engine", ENGINE),
        ("XAML / BAML loading", r"System\.Xaml\.|MS\.Internal\.Xaml|System\.Windows\.Markup\.|Baml|InitializeComponent|XamlReader|LoadComponent"),
        ("property system", r"System\.Windows\.DependencyObject\.|DependencyProperty|EffectiveValue|PropertyMetadata|ExpressionBase|DependentList|UncommonField"),
        ("layout", r"LayoutManager|UIElement\.(Measure|Arrange|InvalidateMeasure|InvalidateArrange|UpdateLayout)|MeasureOverride|ArrangeOverride|MeasureCore|ArrangeCore"),
        ("text", r"TextBlock|MS\.Internal\.Text|System\.Windows\.Media\.TextFormatting|FormattedText|GlyphTypeface|DWrite"),
        ("styles / templates / resources", r"Style|Template|ResourceDictionary|FindResource|FrameworkElement\.(OnStyleChanged|UpdateStyle)"),
        ("rendering / composition", r"System\.Windows\.Media\.|MediaContext|Visual\.|Render|milcore|Dispatcher.*Render"),
        ("window / logical and visual tree", r"Window\.|HwndSource|LogicalTree|VisualTree|AddLogicalChild|OnVisualParentChanged|PresentationSource"),
        ("dispatcher", r"System\.Windows\.Threading\."),
        ("view-model and benchmark code", r"WpfBench\."),
    ],
    "maui": [
        ("FastBindings", r"FastBindings\."),
        ("framework binding engine", ENGINE),
        ("XAML loading", r"Microsoft\.Maui\.Controls\.Xaml|InitializeComponent|XamlServiceProvider"),
        ("property system", r"BindableObject\.|BindableProperty|SetValueCore|SetValueActual|GetOrCreateContext"),
        ("styles / resources", r"MergedStyle|Style|ResourceDictionary|DynamicResource"),
        ("layout / invalidation", r"InvalidateMeasure|Measure|Layout|OnChildAdded|OnPropertyChanged"),
        ("view tree / BindingContext", r"Element\.|VisualElement\.|BindingContext|SetParent|LogicalChild"),
        ("view-model and benchmark code", r"MauiBench\."),
    ],
    "avalonia": [
        ("FastBindings", r"FastBindings\."),
        ("framework binding engine", ENGINE),
        ("XAML loading", r"XamlIl|AvaloniaXamlLoader|!XamlClosure|NameScope|Populate|InitializeComponent"),
        ("property system", r"Avalonia\.PropertyStore\.|AvaloniaObject\.(SetValue|GetValue|RaisePropertyChanged|ClearValue|SetCurrentValue|OnPropertyChanged)|AvaloniaProperty|StyledProperty|DirectProperty|Avalonia\.Reactive\."),
        ("styling", r"Avalonia\.Styling\.|Selector|Classes|StyleInstance|ControlTheme"),
        ("templates", r"ApplyTemplate|ControlTemplate|ContentPresenter"),
        ("text", r"Avalonia\.Media\.TextFormatting|TextLayout|TextBlock|HarfBuzz|GlyphRun|Typeface"),
        ("layout", r"Avalonia\.Layout\.|MeasureOverride|ArrangeOverride|MeasureCore|ArrangeCore|InvalidateMeasure"),
        ("rendering", r"Avalonia\.Rendering\.|Composition|Skia|Render\("),
        ("logical / visual tree", r"LogicalTree|VisualTree|OnAttachedTo|OnDetachedFrom|AttachedToVisualTree|LogicalChildren|VisualChildren"),
        ("view-model and benchmark code", r"AvaloniaBench\."),
    ],
}[args.platform]

d = json.load(open(args.speedscope))
frames = [f["name"] for f in d["shared"]["frames"]]
step_of = {}
for i, n in enumerate(frames):
    m = re.search(r"\.Step_(\w+)\(", n)
    if m:
        step_of[i] = m.group(1)
def has_steps(p):
    if p.get("type") == "sampled":
        return any(f in step_of for st in p["samples"] for f in st)
    return any(e["type"] == "O" and e["frame"] in step_of for e in p["events"])
with_steps = [p for p in d["profiles"] if has_steps(p)]
if len(with_steps) != 1:
    sys.exit(f"expected the Step_ methods on exactly one thread, found them on {[p['name'] for p in with_steps]}")
prof = with_steps[0]

cre = [(c, re.compile(r)) for c, r in CATS]
eng = re.compile(ENGINE)
fb = re.compile(r"FastBindings\.")
def cat_of(name):
    for c, r in cre:
        if r.search(name):
            return c
    return None
# classify by the method itself — "Module!Namespace.Type.Method" — never its parameter types: FastBindings' calls to
# SetValue(..., Avalonia.Data.BindingPriority) must not count as the framework's binding engine
methods = [n.split("(")[0] for n in frames]
fcat = [cat_of(n) for n in methods]
is_engine = [bool(eng.search(n)) for n in methods]
is_fb = [bool(fb.search(n)) for n in methods]

total = collections.Counter()
selfc = collections.defaultdict(collections.Counter)
incl = collections.defaultdict(collections.Counter)
order = []

def count(stack, dt):
    """One sample (or one interval of an evented profile): the stack root first."""
    marks = [i for i, f in enumerate(stack) if f in step_of]
    if not marks:
        return
    st = step_of[stack[marks[-1]]]
    if st not in order: order.append(st)
    total[st] += dt
    c = None
    for f in reversed(stack):
        if fcat[f]:
            c = fcat[f]
            break
    selfc[st][c or "runtime / GC / other"] += dt
    under = stack[marks[0]:]
    if any(is_engine[f] for f in under): incl[st]["framework binding engine (and what it called)"] += dt
    if any(is_fb[f] for f in under): incl[st]["FastBindings (and what it called)"] += dt

if prof.get("type") == "sampled":
    for st, w in zip(prof["samples"], prof["weights"]):
        count(st, w)
    prof["events"] = []
stack, steps, last = [], [], None
for e in prof["events"]:
    at = e["at"]
    if last is not None and steps:
        count(stack, at - last)
    last = at
    f = e["frame"]
    if e["type"] == "O":
        stack.append(f)
        if f in step_of:
            steps.append(step_of[f])
            if step_of[f] not in order: order.append(step_of[f])
    else:
        stack.pop()
        if f in step_of:
            steps.pop()

unit = d["profiles"][0].get("unit", "milliseconds")
scale = 1.0 if unit == "milliseconds" else 1e-3 if unit == "microseconds" else 1e-6 if unit == "nanoseconds" else 1.0
print(f"# {args.label} ({args.platform}) — UI thread {prof['name']}")
for st in order:
    t = total[st] * scale
    if not t: continue
    per = f", {t / args.rounds:.2f} ms per round" if args.rounds else ""
    print(f"\n## {st}: {t:.0f} ms sampled{per}")
    for c, v in selfc[st].most_common():
        v *= scale
        print(f"  {v / t * 100:5.1f}%  {v / args.rounds if args.rounds else v:8.2f} {'ms/round' if args.rounds else 'ms'}  {c}")
    print("  inclusive:")
    for k in ("framework binding engine (and what it called)", "FastBindings (and what it called)"):
        v = incl[st][k] * scale
        print(f"  {v / t * 100:5.1f}%  {v / args.rounds if args.rounds else v:8.2f} {'ms/round' if args.rounds else 'ms'}  {k}")
