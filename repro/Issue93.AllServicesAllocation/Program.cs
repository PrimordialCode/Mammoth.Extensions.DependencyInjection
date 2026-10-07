using Mammoth.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
Console.WriteLine($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; DI: {typeof(ServiceProvider).Assembly.GetName().Version}");
Console.WriteLine("case,generic B/call,Type B/call,known-key typed list B/call,group-count B/call,count");
foreach (var scenario in new[] { "empty", "unkeyed", "one-key", "ten-keys", "open-only", "merged" })
{
    var services = new ServiceCollection();
    var keys = new List<object>();
    if (scenario != "empty") services.AddSingleton<IBox<string>, Box<string>>();
    if (scenario is "one-key" or "ten-keys")
        for (var i = 0; i < (scenario == "one-key" ? 1 : 10); i++)
        {
            keys.Add(i);
            services.AddKeyedSingleton<IBox<string>, Box<string>>(i);
        }
    if (scenario is "open-only" or "merged")
    {
        services.AddKeyedSingleton(typeof(IBox<>), "open", typeof(Box<>));
        keys.Add("open");
    }
    if (scenario == "merged")
    {
        services.AddKeyedSingleton(typeof(IBox<>), "shared", typeof(Box<>));
        services.AddKeyedSingleton<IBox<string>, ClosedBox>(new string("shared".ToCharArray()));
        services.AddKeyedSingleton<IBox<string>, ClosedBox>("closed");
        keys.Add("shared");
        keys.Add("closed");
    }
    using var provider = ServiceProviderFactory.CreateServiceProvider(services);
    int GroupCount()
    {
        var count = provider.GetServices<IBox<string>>().Count();
        foreach (var key in keys) count += provider.GetKeyedServices<IBox<string>>(key).Count();
        return count;
    }
    int TypedList()
    {
        var all = new List<IBox<string>>(provider.GetServices<IBox<string>>());
        foreach (var key in keys) all.AddRange(provider.GetKeyedServices<IBox<string>>(key));
        return all.Count;
    }
    var expected = GroupCount();
    var generic = Measure(() => provider.GetAllServices<IBox<string>>().Count(), expected);
    var typed = Measure(() => provider.GetAllServices(typeof(IBox<string>)).Count(), expected);
    var control = Measure(TypedList, expected);
    var groups = Measure(GroupCount, expected);
    Console.WriteLine($"{scenario},{generic:F1},{typed:F1},{control:F1},{groups:F1},{expected}");
}

static double Measure(Func<int> query, int expected)
{
    const int iterations = 100_000;
    for (var i = 0; i < 1_000; i++)
        if (query() != expected) throw new InvalidOperationException("Unexpected warm-up count.");
    var samples = new double[7];
    for (var sample = 0; sample < samples.Length; sample++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var total = 0;
        for (var i = 0; i < iterations; i++) total += query();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        if (total != expected * iterations) throw new InvalidOperationException("Unexpected measured count.");
        samples[sample] = allocated / (double)iterations;
    }
    Array.Sort(samples);
    return samples[3];
}

public interface IBox<T>;
public class Box<T> : IBox<T>;
public sealed class ClosedBox : Box<string>;
