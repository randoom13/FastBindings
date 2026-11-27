using System.Diagnostics;
using System.Text.Json;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;

// EtwStacks OUT.speedscope.json EXE [ARGS...]
// Runs EXE under a kernel sampling session (1 ms, with stacks) and the CLR provider (JIT method names), then writes every sampled
// stack of that process as speedscope, one profile per thread (root first). Run the target with COMPlus_ZapDisable=1 so that every
// method is JIT-compiled and named by the CLR's events (native images would need their own symbols).
if (args.Length < 2)
{
    Console.Error.WriteLine("usage: EtwStacks OUT.speedscope.json EXE [ARGS...]");
    return 2;
}
var output = Path.GetFullPath(args[0]);
var etl = Path.ChangeExtension(output, ".etl");
int pid;
using (var session = new TraceEventSession("FastBindingsProfile", etl))
{
    session.BufferSizeMB = 512;
    session.EnableKernelProvider(
        KernelTraceEventParser.Keywords.Profile | KernelTraceEventParser.Keywords.ImageLoad |
        KernelTraceEventParser.Keywords.Process | KernelTraceEventParser.Keywords.Thread,
        KernelTraceEventParser.Keywords.Profile);
    session.EnableProvider(ClrTraceEventParser.ProviderGuid, TraceEventLevel.Verbose,
        (ulong)(ClrTraceEventParser.Keywords.Jit | ClrTraceEventParser.Keywords.Loader | ClrTraceEventParser.Keywords.Stack));
    var start = new ProcessStartInfo(args[1]) { UseShellExecute = false };
    foreach (var a in args.Skip(2)) start.ArgumentList.Add(a);
    using var process = Process.Start(start)!;
    pid = process.Id;
    process.WaitForExit();
    Console.WriteLine($"[etw-stacks] {args[1]} exited with {process.ExitCode}");
}

using var log = TraceLog.OpenOrConvert(etl, new TraceLogOptions { ConversionLog = TextWriter.Null });
var frames = new List<string>();
var frameIndex = new Dictionary<string, int>();
var byThread = new Dictionary<int, List<int[]>>();
foreach (var ev in log.Events)
{
    if (ev is not SampledProfileTraceData sample || sample.ProcessID != pid)
        continue;
    var stack = new List<int>();
    for (var cs = sample.CallStack(); cs != null; cs = cs.Caller)
    {
        var address = cs.CodeAddress;
        var method = address.FullMethodName;
        var name = string.IsNullOrEmpty(method) ? $"{address.ModuleName}!?" : $"{address.ModuleName}!{method}";
        if (!frameIndex.TryGetValue(name, out var index))
        {
            frameIndex[name] = index = frames.Count;
            frames.Add(name);
        }
        stack.Add(index);
    }
    stack.Reverse();   // root first
    if (!byThread.TryGetValue(sample.ThreadID, out var list))
        byThread[sample.ThreadID] = list = new List<int[]>();
    list.Add(stack.ToArray());
}
var document = new
{
    shared = new { frames = frames.Select(f => new { name = f }) },
    profiles = byThread.Select(t => new
    {
        type = "sampled",
        name = $"Thread ({t.Key})",
        unit = "milliseconds",
        startValue = 0,
        endValue = t.Value.Count,
        samples = t.Value,
        weights = t.Value.Select(_ => 1.0),
    }),
};
File.WriteAllText(output, JsonSerializer.Serialize(document));
Console.WriteLine($"[etw-stacks] {byThread.Values.Sum(v => v.Count)} samples on {byThread.Count} threads, {frames.Count} frames -> {output}");
return 0;
