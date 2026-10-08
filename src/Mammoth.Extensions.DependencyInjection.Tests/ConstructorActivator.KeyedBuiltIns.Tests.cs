using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class KeyedBuiltInActivationRegressionTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var type in new[] { "provider", "scope", "ordinary-probe", "keyed-probe" })
        foreach (var provider in new[] { "native", "snapshot", "diagnostics" })
        foreach (var mode in new[] { "attribute", "map", "inherit" })
        foreach (var registration in new[] { "missing", "exact", "wildcard", "other" })
            yield return [type, provider, mode, registration];
    }

    public static IEnumerable<object[]> ProviderCases()
    {
        foreach (var type in new[] { "provider", "scope", "ordinary-probe", "keyed-probe" })
        foreach (var provider in new[] { "native", "snapshot", "diagnostics" })
            yield return [type, provider];
    }

    [TestMethod]
    [DynamicData(nameof(Cases))]
    public void ConstructorSelectionMatchesNativeRegistrationAvailability(string type, string provider, string mode, string registration)
        => Dispatch(type, new SelectionCheck(provider, mode, registration));

    [TestMethod]
    [DynamicData(nameof(ProviderCases))]
    public void OptionalDefaultsAndUnkeyedBuiltInsMatchNative(string type, string provider)
        => Dispatch(type, new OptionalCheck(provider));

    [TestMethod]
    [DynamicData(nameof(ProviderCases))]
    public void RegistrationSnapshotsAreProviderSpecificAndImmutable(string type, string provider)
        => Dispatch(type, new SnapshotCheck(provider));

    [TestMethod]
    [DynamicData(nameof(ProviderCases))]
    public void RejectedAndAmbiguousConstructorsNeverActivateDependencies(string type, string provider)
        => Dispatch(type, new ActivationCheck(provider));

    private interface ICheck { void Run<T>() where T : class; }
    private static void Dispatch(string type, ICheck check)
    {
        switch (type)
        {
            case "provider": check.Run<IServiceProvider>(); break;
            case "scope": check.Run<IServiceScopeFactory>(); break;
            case "ordinary-probe": check.Run<IServiceProviderIsService>(); break;
            default: check.Run<IServiceProviderIsKeyedService>(); break;
        }
    }

    private sealed class SelectionCheck(string kind, string mode, string registration) : ICheck
    {
        public void Run<T>() where T : class
        {
            var services = Services<T>(registration);
            services.AddTransient<AttributeConsumer<T>>([Dependency.OnValue("label", "configured")]);
            services.AddTransient<NamedConsumer<T>>([Dependency.OnValue("label", "configured"), Parameter.ForKey("dependency").Eq("blue")]);
            services.AddKeyedTransient<InheritedConsumer<T>>("blue", [Dependency.OnValue("label", "configured")]);
            using var native = NativeServices<T>(registration).BuildServiceProvider();
            using var provider = Build(services, kind);
            using var scope = provider.CreateScope();
            IResult expected = mode == "inherit"
                ? native.GetRequiredKeyedService<InheritedConsumer<T>>("blue")
                : native.GetRequiredService<AttributeConsumer<T>>();
            IResult result = mode switch
            {
                "map" => scope.ServiceProvider.GetRequiredService<NamedConsumer<T>>(),
                "inherit" => scope.ServiceProvider.GetRequiredKeyedService<InheritedConsumer<T>>("blue"),
                _ => scope.ServiceProvider.GetRequiredService<AttributeConsumer<T>>()
            };
            Assert.AreEqual(expected.Choice, result.Choice);
            Assert.AreEqual("configured", result.Label);
            Assert.AreEqual(expected.Dependency?.GetType(), result.Dependency?.GetType());
            Assert.AreEqual(registration is "exact" or "wildcard" ? "long" : "short", result.Choice);
        }
    }

    private sealed class OptionalCheck(string kind) : ICheck
    {
        public void Run<T>() where T : class
        {
            foreach (var registration in new[] { "missing", "exact", "wildcard" })
            {
                var services = Services<T>(registration);
                Dependency[] map = [Dependency.OnValue("label", "configured")];
                services.AddTransient<OptionalConsumer<T>>(map);
                services.AddKeyedTransient<OptionalInherited<T>>("blue", map);
                services.AddTransient<UnkeyedConsumer<T>>(map);
                var nativeServices = Services<T>(registration);
                nativeServices.AddTransient<OptionalConsumer<T>>();
                nativeServices.AddKeyedTransient<OptionalInherited<T>>("blue");
                using var native = nativeServices.BuildServiceProvider();
                using var provider = Build(services, kind);
                Assert.AreEqual(native.GetRequiredService<OptionalConsumer<T>>().Dependency?.GetType(),
                    provider.GetRequiredService<OptionalConsumer<T>>().Dependency?.GetType());
                Assert.AreEqual(native.GetRequiredKeyedService<OptionalInherited<T>>("blue").Dependency?.GetType(),
                    provider.GetRequiredKeyedService<OptionalInherited<T>>("blue").Dependency?.GetType());
                Assert.IsNotNull(provider.GetRequiredService<UnkeyedConsumer<T>>().Dependency);
            }
        }
    }

    private sealed class SnapshotCheck(string kind) : ICheck
    {
        public void Run<T>() where T : class
        {
            var services = Services<T>("missing");
            services.AddTransient<AttributeConsumer<T>>([Dependency.OnValue("label", "configured")]);
            using var before = Build(services, kind);
            services.AddKeyedSingleton(typeof(T), "blue", new BuiltIns());
            using var after = Build(services, kind);
            Assert.AreEqual("short", before.GetRequiredService<AttributeConsumer<T>>().Choice);
            Assert.AreEqual("long", after.GetRequiredService<AttributeConsumer<T>>().Choice);
            services.RemoveAt(services.Count - 1);
            using var removed = Build(services, kind);
            Assert.AreEqual("short", removed.GetRequiredService<AttributeConsumer<T>>().Choice);
            Assert.AreEqual("long", after.GetRequiredService<AttributeConsumer<T>>().Choice);
        }
    }

    private sealed class ActivationCheck(string kind) : ICheck
    {
        public void Run<T>() where T : class
        {
            var calls = 0;
            var services = Services<T>("missing");
            services.AddKeyedTransient(typeof(T), "blue", (_, _) => { calls++; return new BuiltIns(); });
            services.AddTransient<Part>(_ => { calls++; return new Part(); });
            Dependency[] map = [Dependency.OnValue("label", "configured")];
            services.AddTransient<Rejected<T>>(map);
            services.AddTransient<Ambiguous<T>>(map);
            using var provider = Build(services, kind);
            Assert.AreEqual("configured", provider.GetRequiredService<Rejected<T>>().Label);
            Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetRequiredService<Ambiguous<T>>());
            Assert.AreEqual(0, calls, "Availability checks must not resolve even explicitly registered dependencies.");
        }
    }

    private static IServiceCollection Services<T>(string registration)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton("configured");
        if (registration != "missing")
            services.AddKeyedSingleton(typeof(T), registration == "wildcard" ? KeyedService.AnyKey : registration == "other" ? "other" : "blue", new BuiltIns());
        return services;
    }

    private static IServiceCollection NativeServices<T>(string registration) where T : class
    {
        var services = Services<T>(registration);
        services.AddTransient<AttributeConsumer<T>>();
        services.AddKeyedTransient<InheritedConsumer<T>>("blue");
        return services;
    }

    private static ServiceProvider Build(IServiceCollection services, string kind) => kind switch
    {
        "snapshot" => ServiceProviderFactory.CreateServiceProvider(services),
        "diagnostics" => ServiceProviderFactory.CreateServiceProvider(services,
            new ExtendedServiceProviderOptions { ValidateOnBuild = true, DetectIncorrectUsageOfTransientDisposables = true }),
        _ => services.BuildServiceProvider()
    };

    public sealed class BuiltIns : IServiceProvider, IServiceScopeFactory, IServiceProviderIsService, IServiceProviderIsKeyedService
    {
        public object? GetService(Type type) => null;
        public IServiceScope CreateScope() => throw new AssertFailedException("The keyed scope factory must not be invoked.");
        public bool IsService(Type type) => false;
        public bool IsKeyedService(Type type, object? key) => false;
    }
    public interface IResult { string Choice { get; } string Label { get; } object? Dependency { get; } }
    public sealed class AttributeConsumer<T> : IResult where T : class
    {
        public string Choice { get; }
        public string Label { get; }
        public object? Dependency { get; }
        public AttributeConsumer(string label) { Choice = "short"; Label = label; }
        public AttributeConsumer(string label, [FromKeyedServices("blue")] T dependency) { Choice = "long"; Label = label; Dependency = dependency; }
    }
    public sealed class NamedConsumer<T> : IResult where T : class
    {
        public string Choice { get; }
        public string Label { get; }
        public object? Dependency { get; }
        public NamedConsumer(string label) { Choice = "short"; Label = label; }
        public NamedConsumer(string label, T dependency) { Choice = "long"; Label = label; Dependency = dependency; }
    }
    public sealed class InheritedConsumer<T> : IResult where T : class
    {
        public string Choice { get; }
        public string Label { get; }
        public object? Dependency { get; }
        public InheritedConsumer(string label, [ServiceKey] object key) { Choice = "short"; Label = label; }
        public InheritedConsumer(string label, [ServiceKey] object key, [FromKeyedServices] T dependency) { Choice = "long"; Label = label; Dependency = dependency; }
    }
    public sealed class OptionalConsumer<T>(string label, [FromKeyedServices("blue")] T? dependency = null) where T : class
    { public T? Dependency { get; } = dependency; }
    public sealed class OptionalInherited<T>(string label, [ServiceKey] object key, [FromKeyedServices] T? dependency = null) where T : class
    { public T? Dependency { get; } = dependency; }
    public sealed class UnkeyedConsumer<T>(string label, [FromKeyedServices(null)] T dependency) where T : class
    { public T Dependency { get; } = dependency; }
    public sealed class Part;
    public sealed class Missing;
    public sealed class Rejected<T> where T : class
    {
        public string Label { get; }
        public Rejected(string label) => Label = label;
        public Rejected(string label, [FromKeyedServices("blue")] T dependency, Missing missing) => throw new AssertFailedException("Unavailable constructor selected.");
    }
    public sealed class Ambiguous<T> where T : class
    {
        public Ambiguous(string label, [FromKeyedServices("blue")] T dependency) => throw new AssertFailedException("Ambiguous constructor selected.");
        public Ambiguous(string label, Part part) => throw new AssertFailedException("Ambiguous constructor selected.");
    }
}
