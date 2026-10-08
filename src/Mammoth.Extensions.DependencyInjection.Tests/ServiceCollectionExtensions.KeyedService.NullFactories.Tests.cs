using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class NullFactoryDependencyRegressionTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var kind in new[] { "native", "snapshot", "diagnostics" })
        foreach (var mode in new[] { "ordinary", "explicit", "null-key", "inherit", "map" })
        foreach (var behavior in new[] { "null", "missing-optional", "missing-required", "throw" })
        foreach (var lifetime in new[] { ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient })
            if ((behavior == "null" || lifetime == ServiceLifetime.Transient)
                && (behavior != "missing-optional" || mode != "map"))
                yield return [kind, mode, behavior, lifetime];
    }

    [TestMethod]
    [DynamicData(nameof(Cases))]
    public void MappedActivationPreservesRegisteredNullDefaultsAndExceptions(
        string kind, string mode, string behavior, ServiceLifetime lifetime)
    {
        var error = new InvalidOperationException("Factory failure");
        var nativeCalls = new int[3];
        var mappedCalls = new int[3];
        using var native = Services(mode, behavior, lifetime, nativeCalls, error, false).BuildServiceProvider();
        using var mapped = Build(Services(mode, behavior, lifetime, mappedCalls, error, true), kind);
        for (var scopeIndex = 0; scopeIndex < 2; scopeIndex++)
        {
            using var nativeScope = native.CreateScope();
            using var mappedScope = mapped.CreateScope();
            for (var repeat = 0; repeat < 2; repeat++)
            {
                if (behavior is "throw" or "missing-required")
                {
                    var nativeError = Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(nativeScope.ServiceProvider));
                    var mappedError = Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(mappedScope.ServiceProvider));
                    if (behavior == "throw")
                    {
                        Assert.AreSame(error, nativeError);
                        Assert.AreSame(error, mappedError);
                    }
                }
                else
                {
                    var nativeResult = Resolve(nativeScope.ServiceProvider);
                    var mappedResult = Resolve(mappedScope.ServiceProvider);
                    Assert.IsNull(nativeResult.Part);
                    Assert.IsNull(mappedResult.Part);
                    Assert.AreEqual(behavior == "missing-optional" ? "fallback" : null, nativeResult.Text);
                    Assert.AreEqual(behavior == "missing-optional" ? (int?)42 : null, nativeResult.Number);
                    Assert.AreEqual(nativeResult.Text, mappedResult.Text);
                    Assert.AreEqual(nativeResult.Number, mappedResult.Number);
                    Assert.IsTrue(mappedResult.Label);
                }
                // Compare actual native factory calls, including null caching across scopes.
                CollectionAssert.AreEqual(nativeCalls, mappedCalls);
                if (behavior == "missing-required") CollectionAssert.AreEqual(new int[3], mappedCalls);
            }
        }

        Values Resolve(IServiceProvider provider) => mode == "inherit"
            ? provider.GetRequiredKeyedService<Values>("blue") : provider.GetRequiredService<Values>();
    }

    private static IServiceCollection Services(string mode, string behavior, ServiceLifetime lifetime,
        int[] calls, Exception error, bool mapped)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(typeof(bool), true);
        var key = mode is "explicit" or "map" ? "part" : mode == "inherit" ? "blue" : null;
        foreach (var pair in new[] { (typeof(Part), 0), (typeof(string), 1), (typeof(int?), 2) })
        {
            if (behavior == "missing-required" && pair.Item2 == 0) continue;
            if (behavior == "missing-optional" && pair.Item2 != 0) continue;
            object Factory()
            {
                calls[pair.Item2]++;
                if (behavior == "throw") throw error;
                return null!;
            }
            services.Add(key == null ? ServiceDescriptor.Describe(pair.Item1, _ => Factory(), lifetime)
                : ServiceDescriptor.DescribeKeyed(pair.Item1, key, (_, _) => Factory(), lifetime));
        }
        // The unrelated label map triggers Mammoth activation. Native controls use the
        // same parameter keys; named maps use the equivalent explicit-key constructor.
        var target = mode switch
        {
            "explicit" => typeof(ExplicitValues), "null-key" => typeof(NullKeyValues),
            "inherit" => typeof(InheritedValues), "map" when !mapped => typeof(ExplicitValues),
            _ => typeof(OrdinaryValues)
        };
        Dependency[] map = mode == "map"
            ? [Dependency.OnValue("label", true), Parameter.ForKey("part").Eq("part"),
                Parameter.ForKey("text").Eq("part"), Parameter.ForKey("number").Eq("part")]
            : [Dependency.OnValue("label", true)];
        if (mode == "inherit")
        {
            if (mapped) services.AddKeyedTransient<Values, InheritedValues>("blue", map);
            else services.AddKeyedTransient<Values, InheritedValues>("blue");
        }
        else if (mapped) services.AddTransient(typeof(Values), target, map);
        else services.AddTransient(typeof(Values), target);
        return services;
    }

    public static IEnumerable<object[]> DecoratorCases()
    {
        foreach (var kind in new[] { "native", "snapshot", "diagnostics" })
        foreach (var mode in new[] { "explicit", "null-key", "inherit" })
        foreach (var lifetime in new[] { ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient })
            yield return [kind, mode, lifetime];
    }

    [TestMethod]
    [DynamicData(nameof(DecoratorCases))]
    public void ContextualDecoratorsReceiveRegisteredNullInsteadOfOptionalDefaults(
        string kind, string mode, ServiceLifetime lifetime)
    {
        var calls = new int[3];
        var services = Services(mode, "null", lifetime, calls, new InvalidOperationException(), false);
        services.AddKeyedSingleton<IWork>("blue", new Work());
        if (mode == "explicit") services.Decorate<IWork, ExplicitDecorator>();
        else if (mode == "null-key") services.Decorate<IWork, NullKeyDecorator>();
        else services.Decorate<IWork, InheritedDecorator>();
        using var provider = Build(services, kind);
        using var scope = provider.CreateScope();
        var result = (Decorator)scope.ServiceProvider.GetRequiredKeyedService<IWork>("blue");
        Assert.IsInstanceOfType<Work>(result.Inner);
        Assert.IsNull(result.Part);
        Assert.IsNull(result.Text);
        Assert.IsNull(result.Number);
        Assert.AreEqual("blue", result.Key);
        CollectionAssert.AreEqual(new[] { 1, 1, 1 }, calls);
    }

    private static ServiceProvider Build(IServiceCollection services, string kind) => kind switch
    {
        "snapshot" => ServiceProviderFactory.CreateServiceProvider(services),
        "diagnostics" => ServiceProviderFactory.CreateServiceProvider(services,
            new ExtendedServiceProviderOptions { ValidateOnBuild = true, DetectIncorrectUsageOfTransientDisposables = true }),
        _ => services.BuildServiceProvider()
    };

    public sealed class Part;
    public abstract class Values(bool label, Part? part, string? text, int? number)
    {
        public bool Label { get; } = label;
        public Part? Part { get; } = part;
        public string? Text { get; } = text;
        public int? Number { get; } = number;
    }
    public sealed class OrdinaryValues(bool label, Part? part, string? text = "fallback", int? number = 42)
        : Values(label, part, text, number);
    public sealed class ExplicitValues(bool label, [FromKeyedServices("part")] Part? part,
        [FromKeyedServices("part")] string? text = "fallback", [FromKeyedServices("part")] int? number = 42)
        : Values(label, part, text, number);
    public sealed class NullKeyValues(bool label, [FromKeyedServices(null)] Part? part,
        [FromKeyedServices(null)] string? text = "fallback", [FromKeyedServices(null)] int? number = 42)
        : Values(label, part, text, number);
    public sealed class InheritedValues(bool label, [FromKeyedServices] Part? part,
        [FromKeyedServices] string? text = "fallback", [FromKeyedServices] int? number = 42)
        : Values(label, part, text, number);
    public interface IWork;
    public sealed class Work : IWork;
    public abstract class Decorator(IWork inner, object key, Part? part, string? text, int? number) : IWork
    {
        public IWork Inner { get; } = inner;
        public object Key { get; } = key;
        public Part? Part { get; } = part;
        public string? Text { get; } = text;
        public int? Number { get; } = number;
    }
    public sealed class ExplicitDecorator(IWork inner, [ServiceKey] object key,
        [FromKeyedServices("part")] Part? part, [FromKeyedServices("part")] string? text = "fallback",
        [FromKeyedServices("part")] int? number = 42) : Decorator(inner, key, part, text, number);
    public sealed class NullKeyDecorator(IWork inner, [ServiceKey] object key,
        [FromKeyedServices(null)] Part? part, [FromKeyedServices(null)] string? text = "fallback",
        [FromKeyedServices(null)] int? number = 42) : Decorator(inner, key, part, text, number);
    public sealed class InheritedDecorator(IWork inner, [ServiceKey] object key,
        [FromKeyedServices] Part? part, [FromKeyedServices] string? text = "fallback",
        [FromKeyedServices] int? number = 42) : Decorator(inner, key, part, text, number);
}
