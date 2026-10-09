using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class KeyAttributeOrderRegressionTests
{
    public static IEnumerable<object[]> BindingCases()
    {
        foreach (var kind in new[] { "native", "snapshot", "diagnostics" })
        foreach (var context in new[] { "keyed", "int-key", "ordinary", "null-key" })
        foreach (var fromFirst in new[] { false, true })
        foreach (var lifetime in new[] { ServiceLifetime.Transient, ServiceLifetime.Scoped, ServiceLifetime.Singleton })
            yield return [kind, context, fromFirst, lifetime];
    }

    [TestMethod]
    [DynamicData(nameof(BindingCases))]
    public void UntouchedParametersMatchNativeAttributeOrderAndActivationCounts(
        string kind, string context, bool fromFirst, ServiceLifetime lifetime)
    {
        foreach (var behavior in new[] { "registered", "missing", "null", "throw" })
        foreach (var optional in new[] { false, true })
        foreach (var unrelated in new[] { false, true })
        {
            var nativeCounts = new Counts();
            var counts = new Counts();
            var failure = new InvalidOperationException("Explicit keyed factory failure");
            using var native = Configure(nativeCounts, false).BuildServiceProvider();
            using var provider = Build(Configure(counts, true), kind);
            Assert.AreEqual(0, counts.Dependencies + counts.Markers);
            for (var scopeIndex = 0; scopeIndex < 2; scopeIndex++)
            {
                using var nativeScope = native.CreateScope();
                using var scope = provider.CreateScope();
                IValue? previousNative = null;
                IValue? previous = null;
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    IValue expected;
                    try { expected = Resolve(nativeScope.ServiceProvider, context); }
                    catch (InvalidOperationException error)
                    {
                        var actual = Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(scope.ServiceProvider, context));
                        if (ReferenceEquals(error, failure)) Assert.AreSame(failure, actual);
                        Assert.AreEqual(nativeCounts.Dependencies, counts.Dependencies);
                        Assert.AreEqual(nativeCounts.Markers, counts.Markers);
                        continue;
                    }
                    var result = Resolve(scope.ServiceProvider, context);
                    Assert.AreEqual(expected.Value, result.Value);
                    Assert.AreEqual(expected.Label, result.Label);
                    if (repeat != 0)
                        Assert.AreEqual(ReferenceEquals(previousNative, expected), ReferenceEquals(previous, result));
                    previousNative = expected;
                    previous = result;
                    Assert.AreEqual(nativeCounts.Dependencies, counts.Dependencies);
                    Assert.AreEqual(nativeCounts.Markers, counts.Markers);
                }
            }

            IServiceCollection Configure(Counts tracker, bool mapped)
            {
                var services = Services(tracker);
                if (behavior != "missing")
                    services.Add(ServiceDescriptor.DescribeKeyed(typeof(string), "fixed", (_, _) =>
                    {
                        tracker.Dependencies++;
                        if (behavior == "throw") throw failure;
                        return behavior == "null" ? null! : "dependency";
                    }, ServiceLifetime.Transient));
                Dependency[] map = unrelated ? [Dependency.OnValue("label", "label")] : [Dependency.OnValue("unused", 1)];
                RegisterExplicit(services, fromFirst, optional, mapped, context, lifetime, map);
                return services;
            }
        }
    }

    public static IEnumerable<object[]> OverrideCases()
    {
        foreach (var kind in new[] { "native", "snapshot", "diagnostics" })
        foreach (var context in new[] { "keyed", "int-key", "ordinary", "null-key" })
        foreach (var fromFirst in new[] { false, true })
        foreach (var keyedOverride in new[] { false, true })
            yield return [kind, context, fromFirst, keyedOverride];
    }

    [TestMethod]
    [DynamicData(nameof(OverrideCases))]
    public void NamedOverridesWinBeforeBothAttributesAndKeyTypeChecks(
        string kind, string context, bool fromFirst, bool keyedOverride)
    {
        var counts = new Counts();
        var services = Services(counts);
        services.AddKeyedTransient<string>("fixed", (_, _) =>
        {
            counts.Dependencies++;
            throw new InvalidOperationException("Overridden dependency must not run");
        });
        if (keyedOverride) services.AddKeyedSingleton("override", "configured");
        Dependency[] map = [keyedOverride ? Parameter.ForKey("value").Eq("override") : Dependency.OnValue("value", "configured")];
        RegisterExplicit(services, fromFirst, false, true, context, ServiceLifetime.Transient, map);
        using var provider = Build(services, kind);
        using var scope = provider.CreateScope();
        Assert.AreEqual("configured", Resolve(scope.ServiceProvider, context).Value);
        Assert.AreEqual(0, counts.Dependencies);
        Assert.AreEqual(1, counts.Markers);
    }

    public static IEnumerable<object[]> LookupModeCases()
    {
        foreach (var kind in new[] { "native", "snapshot", "diagnostics" })
        foreach (var context in new[] { "keyed", "ordinary", "null-key" })
        foreach (var mode in new[] { "inherited-first", "inherited-last", "null-first", "null-last" })
            yield return [kind, context, mode];
    }

    [TestMethod]
    [DynamicData(nameof(LookupModeCases))]
    public void AttributeOrderUsesTheEffectiveKeyForInheritedAndNullLookups(string kind, string context, string mode)
    {
        var nativeCounts = new Counts();
        var counts = new Counts();
        using var native = Configure(nativeCounts, false).BuildServiceProvider();
        using var provider = Build(Configure(counts, true), kind);
        using var nativeScope = native.CreateScope();
        using var scope = provider.CreateScope();
        var expected = Resolve(nativeScope.ServiceProvider, context);
        Assert.AreEqual(expected.Value, Resolve(scope.ServiceProvider, context).Value);
        Assert.AreEqual(nativeCounts.Dependencies, counts.Dependencies);
        Assert.AreEqual(nativeCounts.Markers, counts.Markers);

        IServiceCollection Configure(Counts tracker, bool mapped)
        {
            var services = Services(tracker);
            services.AddKeyedTransient<string>("blue", (_, _) => { tracker.Dependencies++; return "inherited"; });
            Dependency[] map = [Dependency.OnValue("unused", 1)];
            if (mode == "inherited-first") Register<InheritedFirst>(services, mapped, context, ServiceLifetime.Transient, map);
            else if (mode == "inherited-last") Register<InheritedLast>(services, mapped, context, ServiceLifetime.Transient, map);
            else if (mode == "null-first") Register<NullFirst>(services, mapped, context, ServiceLifetime.Transient, map);
            else Register<NullLast>(services, mapped, context, ServiceLifetime.Transient, map);
            return services;
        }
    }

    [TestMethod]
    [DataRow("native", false)]
    [DataRow("native", true)]
    [DataRow("snapshot", false)]
    [DataRow("snapshot", true)]
    [DataRow("diagnostics", false)]
    [DataRow("diagnostics", true)]
    public void SharedMetadataPreservesAttributeOrderForContextualDecorators(string kind, bool fromFirst)
    {
        var nativeCounts = new Counts();
        var counts = new Counts();
        var nativeServices = Services(nativeCounts);
        nativeServices.AddSingleton<IValue>(new Plain());
        nativeServices.AddKeyedSingleton("fixed", "dependency");
        RegisterExplicit(nativeServices, fromFirst, false, false, "keyed", ServiceLifetime.Transient, []);
        using var native = nativeServices.BuildServiceProvider();
        var services = Services(counts);
        services.AddKeyedSingleton("fixed", "dependency");
        services.AddKeyedTransient<IValue, Plain>("blue");
        if (fromFirst) services.Decorate<IValue, FromDecorator>();
        else services.Decorate<IValue, ServiceDecorator>();
        using var provider = Build(services, kind);
        Assert.AreEqual(Resolve(native, "keyed").Value, Resolve(provider, "keyed").Value);
        Assert.AreEqual(1, counts.Markers);
    }

    private static IServiceCollection Services(Counts counts)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton("label");
        services.AddTransient<Marker>(_ => { counts.Markers++; return new Marker(); });
        return services;
    }

    private static void RegisterExplicit(IServiceCollection services, bool fromFirst, bool optional,
        bool mapped, string context, ServiceLifetime lifetime, Dependency[] map)
    {
        if (fromFirst && optional) Register<FromOptional>(services, mapped, context, lifetime, map);
        else if (fromFirst) Register<FromFirst>(services, mapped, context, lifetime, map);
        else if (optional) Register<ServiceOptional>(services, mapped, context, lifetime, map);
        else Register<ServiceFirst>(services, mapped, context, lifetime, map);
    }

    private static void Register<T>(IServiceCollection services, bool mapped, string context,
        ServiceLifetime lifetime, Dependency[] map) where T : class, IValue
    {
        var keyed = context != "ordinary";
        var key = Key(context);
        if (!mapped)
            services.Add(keyed ? ServiceDescriptor.DescribeKeyed(typeof(IValue), key, typeof(T), lifetime)
                : ServiceDescriptor.Describe(typeof(IValue), typeof(T), lifetime));
        else if (keyed)
        {
            if (lifetime == ServiceLifetime.Singleton) services.AddKeyedSingleton<IValue, T>(key, map);
            else if (lifetime == ServiceLifetime.Scoped) services.AddKeyedScoped<IValue, T>(key, map);
            else services.AddKeyedTransient<IValue, T>(key, map);
        }
        else if (lifetime == ServiceLifetime.Singleton) services.AddSingleton<IValue, T>(map);
        else if (lifetime == ServiceLifetime.Scoped) services.AddScoped<IValue, T>(map);
        else services.AddTransient<IValue, T>(map);
    }

    private static object? Key(string context) => context switch { "keyed" => "blue", "int-key" => 42, _ => null };
    private static IValue Resolve(IServiceProvider provider, string context) => context == "ordinary"
        ? provider.GetRequiredService<IValue>() : provider.GetRequiredKeyedService<IValue>(Key(context));
    private static ServiceProvider Build(IServiceCollection services, string kind) => kind switch
    {
        "snapshot" => ServiceProviderFactory.CreateServiceProvider(services),
        "diagnostics" => ServiceProviderFactory.CreateServiceProvider(services,
            new ExtendedServiceProviderOptions { ValidateOnBuild = true, DetectIncorrectUsageOfTransientDisposables = true }),
        _ => services.BuildServiceProvider()
    };

    public sealed class Counts { public int Dependencies; public int Markers; }
    public sealed class Marker;
    public interface IValue { string? Value { get; } string Label { get; } }
    public abstract class Values(string? value, string label, Marker? marker = null) : IValue
    {
        public string? Value { get; } = value;
        public string Label { get; } = label;
        public Marker? Marker { get; } = marker;
    }
    public sealed class Plain() : Values("plain", "label");
    public sealed class FromFirst(Marker marker, [FromKeyedServices("fixed"), ServiceKey] string? value, string label) : Values(value, label, marker);
    public sealed class ServiceFirst(Marker marker, [ServiceKey, FromKeyedServices("fixed")] string? value, string label) : Values(value, label, marker);
    public sealed class FromOptional(Marker marker, string label, [FromKeyedServices("fixed"), ServiceKey] string? value = "fallback") : Values(value, label, marker);
    public sealed class ServiceOptional(Marker marker, string label, [ServiceKey, FromKeyedServices("fixed")] string? value = "fallback") : Values(value, label, marker);
    public sealed class InheritedFirst(Marker marker, [FromKeyedServices, ServiceKey] string? value) : Values(value, "label", marker);
    public sealed class InheritedLast(Marker marker, [ServiceKey, FromKeyedServices] string? value) : Values(value, "label", marker);
    public sealed class NullFirst(Marker marker, [FromKeyedServices(null), ServiceKey] string? value) : Values(value, "label", marker);
    public sealed class NullLast(Marker marker, [ServiceKey, FromKeyedServices(null)] string? value) : Values(value, "label", marker);
    public sealed class FromDecorator(IValue inner, Marker marker, [FromKeyedServices("fixed"), ServiceKey] string? value) : Values(value, inner.Label, marker);
    public sealed class ServiceDecorator(IValue inner, Marker marker, [ServiceKey, FromKeyedServices("fixed")] string? value) : Values(value, inner.Label, marker);
}
