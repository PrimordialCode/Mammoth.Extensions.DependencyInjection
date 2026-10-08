using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class AnyKeyGenericActivationRegressionTests
{
    public static IEnumerable<object[]> ActivationCases()
    {
        foreach (var lifetime in new[] { ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient })
        foreach (var diagnostics in new[] { false, true })
        foreach (var mode in new[] { "attribute", "map", "inherit", "decorator" })
        foreach (var keyed in new[] { false, true })
            if (keyed || mode == "attribute" || mode == "map") yield return [lifetime, diagnostics, mode, keyed];
    }

    [TestMethod]
    [DynamicData(nameof(ActivationCases))]
    public void AnyKeyGenericDependenciesResolveWithTheConcreteKey(ServiceLifetime lifetime, bool diagnostics, string mode, bool keyed)
    {
        var counter = new CreationCounter();
        IServiceCollection services = Services(lifetime, counter);
        var resolve = RegisterConsumer(services, lifetime, mode, keyed);
        using var native = Services(lifetime, new CreationCounter()).BuildServiceProvider();
        using var nativeScope = native.CreateScope();
        var expected = nativeScope.ServiceProvider.GetRequiredKeyedService<IBox<int>>("blue");
        using var provider = Build(services, diagnostics);
        using var scope = provider.CreateScope();
        Assert.AreEqual(0, counter.Calls, "Constructor availability checks must not activate dependencies.");
        var result = resolve(scope.ServiceProvider);
        Assert.AreEqual(expected.GetType(), result.Box.GetType());
        Assert.AreEqual("blue", result.Box.Key);
        Assert.AreEqual("configured", result.Label);
        var again = resolve(scope.ServiceProvider);
        if (lifetime == ServiceLifetime.Transient) Assert.AreNotSame(result.Box, again.Box);
        else Assert.AreSame(result.Box, again.Box);
        using var other = provider.CreateScope();
        if (lifetime == ServiceLifetime.Singleton) Assert.AreSame(result.Box, resolve(other.ServiceProvider).Box);
        else Assert.AreNotSame(result.Box, resolve(other.ServiceProvider).Box);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnyKeyFallbackDoesNotReplaceNativeRegistrationPrecedence(bool diagnostics)
    {
        foreach (var registration in new[] { "closed", "generic", "wildcard-closed" })
        {
            var services = Services(ServiceLifetime.Transient, new CreationCounter());
            if (registration == "closed") services.AddKeyedTransient<IBox<int>, SpecialBox<int>>("blue");
            else services.AddKeyedTransient(typeof(IBox<>), "blue", typeof(SpecialBox<>));
            if (registration == "wildcard-closed") services.AddKeyedTransient<IBox<int>, Box<int>>(KeyedService.AnyKey);
            services.AddTransient<AttributeConsumer>([Dependency.OnValue("label", "configured")]);
            using var provider = Build(services, diagnostics);
            using var scope = provider.CreateScope();
            var native = scope.ServiceProvider.GetRequiredKeyedService<IBox<int>>("blue");
            var mapped = scope.ServiceProvider.GetRequiredService<AttributeConsumer>();
            Assert.AreEqual(registration == "wildcard-closed" ? typeof(Box<int>) : typeof(SpecialBox<int>), native.GetType());
            Assert.AreEqual(native.GetType(), mapped.Box.GetType());
            Assert.AreEqual("blue", mapped.Box.Key);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AvailableFallbackPrecedesOptionalDefaultButMissingAndUnkeyedDefaultsRemainNull(bool diagnostics)
    {
        foreach (var registered in new[] { false, true })
        {
            var counter = new CreationCounter();
            var services = Services(ServiceLifetime.Transient, counter, registered);
            Dependency[] map = [Dependency.OnValue("unused", 1)];
            services.AddTransient<OptionalConsumer>(map);
            services.AddTransient<UnkeyedOptional>(map);
            using var provider = Build(services, diagnostics);
            var result = provider.GetRequiredService<OptionalConsumer>();
            if (registered)
            {
                Assert.IsNotNull(result.Box);
                Assert.AreEqual("blue", result.Box.Key);
            }
            else Assert.IsNull(result.Box);
            Assert.IsNull(provider.GetRequiredService<UnkeyedOptional>().Box);
            Assert.AreEqual(registered ? 1 : 0, counter.Calls);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RejectedAndAmbiguousConstructorsDoNotActivateFallbackDependencies(bool diagnostics)
    {
        var counter = new CreationCounter();
        var services = Services(ServiceLifetime.Transient, counter);
        services.AddTransient<Part>(_ => { counter.Calls++; return new Part(); });
        Dependency[] map = [Dependency.OnValue("label", "configured")];
        services.AddTransient<Rejected>(map);
        services.AddTransient<Ambiguous>(map);
        using var provider = Build(services, diagnostics);
        Assert.AreEqual("configured", provider.GetRequiredService<Rejected>().Label);
        Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetRequiredService<Ambiguous>());
        Assert.AreEqual(0, counter.Calls);
    }

    [TestMethod]
    [DataRow(false, "attribute")]
    [DataRow(true, "attribute")]
    [DataRow(false, "map")]
    [DataRow(true, "map")]
    [DataRow(false, "inherit")]
    [DataRow(true, "inherit")]
    public void GenericConstraintErrorsAreNotHiddenByConstructorRejectionOrOptionalDefaults(bool diagnostics, string mode)
    {
        var counter = new CreationCounter();
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(counter);
        services.AddKeyedTransient(typeof(IBox<>), KeyedService.AnyKey, typeof(ClassBox<>));
        var resolve = RegisterConsumer(services, ServiceLifetime.Transient, mode, true);
        services.AddTransient<OptionalConsumer>([Dependency.OnValue("unused", 1)]);
        using var provider = Build(services, diagnostics);
        using var scope = provider.CreateScope();
        var native = Assert.ThrowsExactly<ArgumentException>(() => scope.ServiceProvider.GetRequiredKeyedService<IBox<int>>("blue"));
        var mapped = Assert.ThrowsExactly<ArgumentException>(() => resolve(scope.ServiceProvider));
        var optional = Assert.ThrowsExactly<ArgumentException>(() => scope.ServiceProvider.GetRequiredService<OptionalConsumer>());
        Assert.AreEqual(native.Message, mapped.Message);
        Assert.AreEqual(native.Message, optional.Message);
        Assert.AreEqual(0, counter.Calls);
    }

    private static IServiceCollection Services(ServiceLifetime lifetime, CreationCounter counter, bool fallback = true)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(counter);
        if (fallback) services.Add(new ServiceDescriptor(typeof(IBox<>), KeyedService.AnyKey, typeof(Box<>), lifetime));
        return services;
    }

    private static ServiceProvider Build(IServiceCollection services, bool diagnostics) => diagnostics
        ? ServiceProviderFactory.CreateServiceProvider(services,
            new ExtendedServiceProviderOptions { ValidateOnBuild = true, DetectIncorrectUsageOfTransientDisposables = true })
        : services.BuildServiceProvider();

    private static Func<IServiceProvider, IConsumer> RegisterConsumer(IServiceCollection services, ServiceLifetime lifetime, string mode, bool keyed)
    {
        Dependency[] map = [Dependency.OnValue("label", "configured")];
        if (mode == "map") map = [Dependency.OnValue("label", "configured"), Parameter.ForKey("box").Eq("blue")];
        if (mode == "attribute")
        {
            AddMapped<AttributeConsumer>(services, lifetime, keyed, map);
            return p => p.GetRequiredKeyedService<AttributeConsumer>(keyed ? "blue" : null);
        }
        if (mode == "map")
        {
            AddMapped<NamedConsumer>(services, lifetime, keyed, map);
            return p => p.GetRequiredKeyedService<NamedConsumer>(keyed ? "blue" : null);
        }
        if (mode == "inherit")
        {
            AddMapped<InheritedConsumer>(services, lifetime, true, map);
            return p => p.GetRequiredKeyedService<InheritedConsumer>("blue");
        }
        services.AddKeyedTransient<IConsumer, PlainConsumer>("blue", map);
        services.Decorate<IConsumer, InheritedWrapper>();
        return p => p.GetRequiredKeyedService<IConsumer>("blue");
    }

    private static void AddMapped<T>(IServiceCollection services, ServiceLifetime lifetime, bool keyed, Dependency[] map) where T : class
    {
        if (keyed)
        {
            if (lifetime == ServiceLifetime.Singleton) services.AddKeyedSingleton<T>("blue", map);
            else if (lifetime == ServiceLifetime.Scoped) services.AddKeyedScoped<T>("blue", map);
            else services.AddKeyedTransient<T>("blue", map);
        }
        else
        {
            if (lifetime == ServiceLifetime.Singleton) services.AddSingleton<T>(map);
            else if (lifetime == ServiceLifetime.Scoped) services.AddScoped<T>(map);
            else services.AddTransient<T>(map);
        }
    }

    public sealed class CreationCounter { public int Calls { get; set; } }
    public interface IBox<T> { object Key { get; } }
    public sealed class Box<T> : IBox<T>
    {
        public object Key { get; }
        public Box([ServiceKey] object key, CreationCounter counter) { Key = key; counter.Calls++; }
    }
    public sealed class SpecialBox<T>([ServiceKey] object key) : IBox<T> { public object Key { get; } = key; }
    public sealed class ClassBox<T> : IBox<T> where T : class
    {
        public object Key { get; }
        public ClassBox([ServiceKey] object key, CreationCounter counter) { Key = key; counter.Calls++; }
    }
    public interface IConsumer { IBox<int> Box { get; } string Label { get; } }
    public sealed class AttributeConsumer([FromKeyedServices("blue")] IBox<int> box, string label) : IConsumer
    { public IBox<int> Box { get; } = box; public string Label { get; } = label; }
    public sealed class NamedConsumer(IBox<int> box, string label) : IConsumer
    { public IBox<int> Box { get; } = box; public string Label { get; } = label; }
    public sealed class InheritedConsumer([FromKeyedServices] IBox<int> box, [ServiceKey] object key, string label) : IConsumer
    { public IBox<int> Box { get; } = box; public object Key { get; } = key; public string Label { get; } = label; }
    public sealed class PlainConsumer(string label) : IConsumer
    { public IBox<int> Box => throw new AssertFailedException("Only the decorator provides this dependency."); public string Label { get; } = label; }
    public sealed class InheritedWrapper(IConsumer inner, [FromKeyedServices] IBox<int> box, [ServiceKey] object key) : IConsumer
    { public IBox<int> Box { get; } = box; public object Key { get; } = key; public string Label => inner.Label; }
    public sealed class OptionalConsumer([FromKeyedServices("blue")] IBox<int>? box = null) { public IBox<int>? Box { get; } = box; }
    public sealed class UnkeyedOptional(IBox<int>? box = null) { public IBox<int>? Box { get; } = box; }
    public sealed class Part;
    public sealed class Missing;
    public sealed class Rejected
    {
        public string Label { get; }
        public Rejected(string label) => Label = label;
        public Rejected([FromKeyedServices("blue")] IBox<int> box, Missing missing, string label) => throw new AssertFailedException("Rejected constructor ran.");
    }
    public sealed class Ambiguous
    {
        public Ambiguous([FromKeyedServices("blue")] IBox<int> box, string label) { }
        public Ambiguous(Part part, string label) { }
    }
}
