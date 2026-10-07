using Mammoth.Extensions.DependencyInjection;
using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; DI: {typeof(ServiceProvider).Assembly.GetName().Version}");
var services = new ServiceCollection();
services.AddSingleton<Part>();
services.AddKeyedSingleton<Part>("blue");
services.AddTransient<Plain>();
services.AddTransient(sp => new FactoryMapped(sp.GetRequiredService<Part>(), "x"));
services.AddTransient<Mapped>([Dependency.OnValue("label", "x")]);
services.AddKeyedTransient<KeyedMapped>("blue", [Dependency.OnValue("label", "x")]);
services.AddTransient<OptionalMapped>([Dependency.OnValue("label", "x")]);
using var native = services.BuildServiceProvider();
using var mammoth = ServiceProviderFactory.CreateServiceProvider(services);
Measure("Native ordinary", () => native.GetRequiredService<Plain>());
Measure("Mammoth ordinary", () => mammoth.GetRequiredService<Plain>());
Measure("Native explicit factory", () => native.GetRequiredService<FactoryMapped>());
Measure("DependsOn", () => mammoth.GetRequiredService<Mapped>());
Measure("Keyed DependsOn", () => mammoth.GetRequiredKeyedService<KeyedMapped>("blue"));
Measure("Optional DependsOn", () => mammoth.GetRequiredService<OptionalMapped>());

static void Measure(string label, Func<object> resolve)
{
    const int count = 100_000;
    object? sink = null;
    for (var i = 0; i < 20_000; i++) sink = resolve();
    var allocations = new double[7];
    for (var pass = 0; pass < allocations.Length; pass++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < count; i++) sink = resolve();
        allocations[pass] = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)count;
    }
    Array.Sort(allocations);
    Console.WriteLine($"{label}: {allocations[3]:F1} B/resolve (range {allocations[0]:F1}..{allocations[6]:F1})");
    GC.KeepAlive(sink);
}

public sealed class Part;
public sealed class Plain(Part part) { public Part Part { get; } = part; }
public sealed class FactoryMapped(Part part, string label)
{ public Part Part { get; } = part; public string Label { get; } = label; }
public sealed class Mapped(Part part, string label)
{ public Part Part { get; } = part; public string Label { get; } = label; }
public sealed class KeyedMapped([ServiceKey] object key, [FromKeyedServices] Part part, string label)
{ public object Key { get; } = key; public Part Part { get; } = part; public string Label { get; } = label; }
public sealed class OptionalMapped(Part part, string label, int count = 7)
{ public Part Part { get; } = part; public string Label { get; } = label; public int Count { get; } = count; }
