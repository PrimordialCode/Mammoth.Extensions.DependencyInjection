using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class UnkeyedServiceKeyRegressionTests
{
    public static IEnumerable<object[]> OrdinaryCases()
    {
        foreach (var kind in new[] { "native", "snapshot", "diagnostics" })
        foreach (var nullKey in new[] { false, true })
        foreach (var type in new[] { "string", "object", "int", "nullable" })
        foreach (var behavior in new[] { "registered", "missing", "null", "throw" })
        foreach (var lifetime in new[] { ServiceLifetime.Transient, ServiceLifetime.Scoped, ServiceLifetime.Singleton })
            if ((behavior == "registered" || lifetime == ServiceLifetime.Transient) && (behavior != "null" || type != "int"))
                yield return [kind, nullKey, type, behavior, lifetime];
    }

    [TestMethod]
    [DynamicData(nameof(OrdinaryCases))]
    public void NullContextMatchesNativeRegistrationDefaultsAndFailures(
        string kind, bool nullKey, string type, string behavior, ServiceLifetime lifetime)
    {
        var value = Value(type);
        var error = new InvalidOperationException("Key dependency factory failure");
        foreach (var optional in new[] { false, true })
        {
            var nativeCalls = new Calls();
            var calls = new Calls();
            using var native = Services(nativeCalls, false).BuildServiceProvider();
            using var provider = Build(Services(calls, true), kind);
            Assert.AreEqual(0, calls.Keys);
            Assert.AreEqual(0, calls.Markers);
            for (var scopeIndex = 0; scopeIndex < 2; scopeIndex++)
            {
                using var nativeScope = native.CreateScope();
                using var scope = provider.CreateScope();
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    if (behavior == "throw" || (behavior == "missing" && !optional))
                    {
                        var nativeError = Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(nativeScope.ServiceProvider, nullKey, null));
                        var actualError = Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(scope.ServiceProvider, nullKey, null));
                        if (behavior == "throw")
                        {
                            Assert.AreSame(error, nativeError);
                            Assert.AreSame(error, actualError);
                        }
                        else Assert.AreEqual(0, calls.Markers, "Missing required keys must reject selection before unrelated activation.");
                    }
                    else
                    {
                        var nativeResult = Resolve(nativeScope.ServiceProvider, nullKey, null);
                        var result = Resolve(scope.ServiceProvider, nullKey, null);
                        Assert.AreEqual(behavior == "missing" ? Default(type) : behavior == "null" ? null : value, nativeResult.Key);
                        Assert.AreEqual(nativeResult.Key, result.Key);
                        if (type == "object") Assert.AreSame(nativeResult.Key, result.Key);
                        Assert.IsTrue(result.Label);
                    }
                    Assert.AreEqual(nativeCalls.Keys, calls.Keys);
                    Assert.AreEqual(nativeCalls.Markers, calls.Markers);
                }
            }

            IServiceCollection Services(Calls tracker, bool mapped)
            {
                IServiceCollection services = new ServiceCollection();
                services.AddSingleton(typeof(bool), true);
                services.AddTransient<Marker>(_ => { tracker.Markers++; return new Marker(); });
                if (behavior != "missing") services.Add(ServiceDescriptor.Describe(ParameterType(type), _ =>
                {
                    tracker.Keys++;
                    if (behavior == "throw") throw error;
                    return behavior == "null" ? null! : value;
                }, lifetime));
                Register(services, type, optional, mapped, nullKey, null, lifetime, [Dependency.OnValue("label", true)]);
                return services;
            }
        }
    }

    public static IEnumerable<object[]> OverrideCases()
    {
        foreach (var kind in new[] { "native", "snapshot", "diagnostics" })
        foreach (var type in new[] { "string", "object", "int", "nullable" })
        foreach (var context in new[] { "ordinary", "null-key", "non-null" })
        foreach (var namedKey in new[] { false, true })
            yield return [kind, type, context, namedKey];
    }

    [TestMethod]
    [DynamicData(nameof(OverrideCases))]
    public void NamedOverridesWinBeforeServiceKeyAndOrdinaryDependencies(
        string kind, string type, string context, bool namedKey)
    {
        IServiceCollection services = new ServiceCollection();
        var value = Value(type);
        var rejectedCalls = 0;
        services.AddSingleton(typeof(bool), true);
        services.AddSingleton<Marker>();
        services.Add(ServiceDescriptor.Describe(ParameterType(type), _ => { rejectedCalls++; throw new InvalidOperationException("Unselected ordinary dependency"); }, ServiceLifetime.Transient));
        if (namedKey) services.Add(ServiceDescriptor.KeyedSingleton(ParameterType(type), "override", value));
        Dependency[] map = [Dependency.OnValue("label", true), namedKey
            ? Parameter.ForKey("key").Eq("override") : Dependency.OnValue("key", value)];
        // A string context intentionally mismatches value-type key parameters. The
        // explicit named override must take precedence over contextual type checks.
        var serviceKey = context == "non-null" ? "outer" : null;
        var keyed = context != "ordinary";
        Register(services, type, false, true, keyed, serviceKey, ServiceLifetime.Transient, map);
        using var provider = Build(services, kind);
        var result = Resolve(provider, keyed, serviceKey);
        Assert.AreEqual(value, result.Key);
        if (type == "object") Assert.AreSame(value, result.Key);
        Assert.AreEqual(0, rejectedCalls);
    }

    public static IEnumerable<object[]> KeyedCases()
    {
        foreach (var kind in new[] { "native", "snapshot", "diagnostics" })
        foreach (var type in new[] { "string", "object", "int" })
        foreach (var lifetime in new[] { ServiceLifetime.Transient, ServiceLifetime.Scoped, ServiceLifetime.Singleton })
            yield return [kind, type, lifetime];
    }

    [TestMethod]
    [DynamicData(nameof(KeyedCases))]
    public void NonNullContextStillInjectsItsActualTypedKeyWithoutOrdinaryActivation(
        string kind, string type, ServiceLifetime lifetime)
    {
        var serviceKey = type == "int" ? (object)42 : "outer";
        var ordinaryCalls = 0;
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(typeof(bool), true);
        services.AddSingleton<Marker>();
        services.Add(ServiceDescriptor.Describe(ParameterType(type), _ => { ordinaryCalls++; return Value(type); }, ServiceLifetime.Transient));
        Register(services, type, false, false, true, serviceKey, lifetime, []);
        using var native = services.BuildServiceProvider();
        var nativeResult = Resolve(native, true, serviceKey);
        services.Clear();
        services.AddSingleton<Marker>();
        services.Add(ServiceDescriptor.Describe(ParameterType(type), _ => { ordinaryCalls++; return Value(type); }, ServiceLifetime.Transient));
        Register(services, type, false, true, true, serviceKey, lifetime, [Dependency.OnValue("label", true)]);
        using var provider = Build(services, kind);
        using var scope = provider.CreateScope();
        var result = Resolve(scope.ServiceProvider, true, serviceKey);
        Assert.AreEqual(nativeResult.Key, result.Key);
        if (type == "object") Assert.AreSame(serviceKey, result.Key);
        Assert.AreEqual(0, ordinaryCalls);
    }

    [TestMethod]
    [DataRow("native")]
    [DataRow("snapshot")]
    [DataRow("diagnostics")]
    public void NonNullMismatchedKeyTypeStillFailsBeforeActivation(string kind)
    {
        IServiceCollection services = new ServiceCollection();
        var calls = 0;
        services.AddTransient<Marker>(_ => { calls++; return new Marker(); });
        services.AddSingleton(typeof(int), 42);
        Register(services, "int", false, true, true, "outer", ServiceLifetime.Transient, [Dependency.OnValue("label", true)]);
        using var provider = Build(services, kind);
        Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(provider, true, "outer"));
        Assert.AreEqual(0, calls);
    }

    private static void Register(IServiceCollection services, string type, bool optional, bool mapped,
        bool keyed, object? serviceKey, ServiceLifetime lifetime, Dependency[] map)
    {
        if (!optional)
        {
            if (type == "string") Add<Required<string>>();
            else if (type == "object") Add<Required<object>>();
            else if (type == "int") Add<Required<int>>();
            else Add<Required<int?>>();
        }
        else if (type == "string") Add<OptionalString>();
        else if (type == "object") Add<OptionalObject>();
        else if (type == "int") Add<OptionalInt>();
        else Add<OptionalNullable>();

        void Add<T>() where T : class, IValue
        {
            if (!mapped) services.Add(keyed ? ServiceDescriptor.DescribeKeyed(typeof(IValue), serviceKey, typeof(T), lifetime)
                : ServiceDescriptor.Describe(typeof(IValue), typeof(T), lifetime));
            else if (keyed)
            {
                if (lifetime == ServiceLifetime.Singleton) services.AddKeyedSingleton<IValue, T>(serviceKey, map);
                else if (lifetime == ServiceLifetime.Scoped) services.AddKeyedScoped<IValue, T>(serviceKey, map);
                else services.AddKeyedTransient<IValue, T>(serviceKey, map);
            }
            else if (lifetime == ServiceLifetime.Singleton) services.AddSingleton<IValue, T>(map);
            else if (lifetime == ServiceLifetime.Scoped) services.AddScoped<IValue, T>(map);
            else services.AddTransient<IValue, T>(map);
        }
    }

    private static Type ParameterType(string type) => type switch { "string" => typeof(string), "object" => typeof(object), "int" => typeof(int), _ => typeof(int?) };
    private static object Value(string type) => type switch { "string" => "ordinary", "object" => new object(), _ => 42 };
    private static object? Default(string type) => type switch { "string" => "fallback", "int" => 77, "nullable" => 19, _ => null };
    private static IValue Resolve(IServiceProvider provider, bool keyed, object? key) => keyed
        ? provider.GetRequiredKeyedService<IValue>(key) : provider.GetRequiredService<IValue>();
    private static ServiceProvider Build(IServiceCollection services, string kind) => kind switch
    {
        "snapshot" => ServiceProviderFactory.CreateServiceProvider(services),
        "diagnostics" => ServiceProviderFactory.CreateServiceProvider(services,
            new ExtendedServiceProviderOptions { ValidateOnBuild = true, DetectIncorrectUsageOfTransientDisposables = true }),
        _ => services.BuildServiceProvider()
    };
    public sealed class Calls { public int Keys; public int Markers; }
    public sealed class Marker;
    public interface IValue { object? Key { get; } bool Label { get; } Marker Marker { get; } }
    public abstract class Values(object? key, bool label, Marker marker) : IValue
    {
        public object? Key { get; } = key;
        public bool Label { get; } = label;
        public Marker Marker { get; } = marker;
    }
    public sealed class Required<T>([ServiceKey] T key, Marker marker, bool label) : Values(key, label, marker);
    public sealed class OptionalString(Marker marker, bool label, [ServiceKey] string key = "fallback") : Values(key, label, marker);
    public sealed class OptionalObject(Marker marker, bool label, [ServiceKey] object? key = null) : Values(key, label, marker);
    public sealed class OptionalInt(Marker marker, bool label, [ServiceKey] int key = 77) : Values(key, label, marker);
    public sealed class OptionalNullable(Marker marker, bool label, [ServiceKey] int? key = 19) : Values(key, label, marker);
}
