using Mammoth.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
Console.WriteLine($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; DI: {typeof(ServiceProvider).Assembly.GetName().Version}");
Console.WriteLine("size,case,keyed,API B/query,array control B/query");
foreach (var size in new[] { 0, 10, 1_000, 10_000 })
foreach (var scenario in new[] { "all", "last", "first", "missing" })
foreach (var keyed in new[] { false, true })
{
    IServiceCollection services = new ServiceCollection();
    object registeredKey = new string("blue".ToCharArray());
    object queryKey = new string("blue".ToCharArray());
    for (var i = 0; i < size; i++)
    {
        var match = scenario == "all" || scenario == "last" && i == size - 1 || scenario == "first" && i == 0;
        services.Add(keyed
            ? ServiceDescriptor.KeyedSingleton(typeof(Marker), match ? registeredKey : "other", typeof(Marker))
            : ServiceDescriptor.Singleton(match ? typeof(Marker) : typeof(Unrelated), match ? typeof(Marker) : typeof(Unrelated)));
    }
    Func<bool> query = keyed
        ? () => services.IsKeyedSingletonServiceRegistered<IMarker>(queryKey)
        : () => services.IsSingletonServiceRegistered<IMarker>();
    Func<bool> control = keyed
        ? () => KeyedArrayControl(services, queryKey)
        : () => ArrayControl(services);
    var expected = size > 0 && scenario != "missing";
    // Keep descriptor visits bounded even for full-scan/all-matching baseline cases.
    var iterations = Math.Min(10_000, Math.Max(100, 1_000_000 / Math.Max(1, size)));
    var actual = Measure(query, expected, iterations);
    var baseline = Measure(control, expected, iterations);
    Console.WriteLine($"{size},{scenario},{keyed},{actual:F1},{baseline:F1}");
}

static bool ArrayControl(IServiceCollection services)
{
    var descriptors = services.GetServiceDescriptors(typeof(IMarker), isKeyedService: false);
    return descriptors.Length > 0 && descriptors[descriptors.Length - 1].Lifetime == ServiceLifetime.Singleton;
}

static bool KeyedArrayControl(IServiceCollection services, object key)
{
    var descriptors = services.GetServiceDescriptors(typeof(IMarker), isKeyedService: true)
        .Where(d => Equals(d.ServiceKey, key)).ToArray();
    return descriptors.Length > 0 && descriptors[descriptors.Length - 1].Lifetime == ServiceLifetime.Singleton;
}

static double Measure(Func<bool> query, bool expected, int iterations)
{
    for (var i = 0; i < 1_000; i++)
        if (query() != expected) throw new InvalidOperationException("Unexpected warm-up result.");
    var samples = new double[7];
    for (var sample = 0; sample < samples.Length; sample++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var matches = 0;
        for (var i = 0; i < iterations; i++) if (query()) matches++;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        if (matches != (expected ? iterations : 0)) throw new InvalidOperationException("Unexpected measured result.");
        samples[sample] = allocated / (double)iterations;
    }
    Array.Sort(samples);
    return samples[3];
}

public interface IMarker;
public sealed class Marker : IMarker;
public sealed class Unrelated;
