using Mammoth.Extensions.DependencyInjection.Configuration;
using Mammoth.Extensions.DependencyInjection.Inspector;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class DependsOnMergedFeatureIntegrationTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void NamedGenericDependenciesIgnorePublicMutationAndKeepClosedPrecedence(bool diagnostics, bool keyed)
    {
        var services = new ServiceCollection();
        services.AddTransient(typeof(IRepository<>), typeof(UnkeyedRepository<>));
        services.AddKeyedScoped(typeof(IRepository<>), "blue", typeof(BlueRepository<>));
        services.AddKeyedSingleton(typeof(IRepository<>), "red", typeof(RedRepository<>));
        services.AddKeyedSingleton<IRepository<string>, ClosedRepository>("blue");
        int rejectedCreations = 0;
        services.AddTransient<DisposableDependency>(_ => { rejectedCreations++; return new DisposableDependency(); });
        Dependency[] map = [Dependency.OnValue("label", "configured"),
            Parameter.ForKey("left").Eq("blue"), Parameter.ForKey("right").Eq("red"),
            Parameter.ForKey("closed").Eq("blue")];
        if (keyed) services.AddKeyedScoped<GenericCandidate>("outer", map);
        else services.AddScoped<GenericCandidate>(map);
        using var provider = Build(services, diagnostics);
        Tamper(provider);
        provider.GetRequiredService<ServiceTypes>().Add(typeof(Missing));
        provider.GetRequiredService<ServiceLifetimes>().Add(typeof(IRepository<>), ServiceLifetime.Singleton);
        provider.GetRequiredService<ServiceLifetimes>().Add(typeof(IRepository<string>), ServiceLifetime.Transient, "blue");
        Assert.IsTrue(provider.IsTransientServiceRegistered<IRepository<int>>());
        Assert.IsTrue(provider.IsKeyedScopedServiceRegistered<IRepository<int>>("blue"));
        Assert.IsTrue(provider.IsKeyedSingletonServiceRegistered<IRepository<string>>("blue"));
        Assert.IsFalse(provider.IsServiceRegistered<Missing>());
        using var scope = provider.CreateScope();
        var result = scope.ServiceProvider.GetRequiredKeyedService<GenericCandidate>(keyed ? "outer" : null);
        Assert.AreEqual("short", result.Selected);
        Assert.AreEqual("configured", result.Label);
        Assert.IsInstanceOfType<BlueRepository<int>>(result.Left);
        Assert.IsInstanceOfType<RedRepository<int>>(result.Right);
        Assert.IsInstanceOfType<ClosedRepository>(result.Closed);
        Assert.AreEqual(keyed ? "outer" : null, result.Key);
        Assert.AreEqual(7, result.Count);
        Assert.AreEqual(0, rejectedCreations);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DecoratedDependsOnServicesKeepContainerAndCallerDisposalOwnership(bool diagnostics)
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<IRepository<string>, BlueRepository<string>>("blue");
        services.AddKeyedScoped<ITracked, Tracked>("owned", [Dependency.OnValue("label", "configured")]);
        // Decorate wraps only the last registration. Finish the owned chain first.
        services.Decorate<ITracked, TrackedDecorator>();
        services.Decorate<ITracked, TrackedDecorator>();
        var caller = new Tracked("caller", new BlueRepository<string>());
        services.AddKeyedSingleton<ITracked>("caller", caller);
        services.Decorate<ITracked, TrackedDecorator>();
        services.Decorate<ITracked, TrackedDecorator>();
        var layers = services.Where(d => d.ServiceType != typeof(ITracked)
            && d.ServiceType != typeof(IRepository<string>)).ToArray();
        // Two owned layers and one caller-wrapper layer; the supplied instance needs no slot.
        Assert.HasCount(3, layers);
        using var provider = Build(services, diagnostics);
        Tamper(provider);
        foreach (var layer in layers)
        {
            provider.GetRequiredService<ServiceTypes>().Add(layer.ServiceType);
            provider.GetRequiredService<ServiceKeys>().Add(layer.ServiceKey!);
            Assert.IsFalse(provider.IsServiceRegistered(layer.ServiceType));
            Assert.IsFalse(provider.IsKeyedServiceRegistered(layer.ServiceKey!));
            Assert.AreEqual(layer.Lifetime == ServiceLifetime.Scoped,
                provider.IsKeyedScopedServiceRegistered(layer.ServiceType, layer.ServiceKey!));
            Assert.AreEqual(layer.Lifetime == ServiceLifetime.Singleton,
                provider.IsKeyedSingletonServiceRegistered(layer.ServiceType, layer.ServiceKey!));
        }
        using var scope = provider.CreateScope();
        var ownedOuter = (TrackedDecorator)scope.ServiceProvider.GetRequiredKeyedService<ITracked>("owned");
        var ownedMiddle = (TrackedDecorator)ownedOuter.Inner;
        var owned = (Tracked)ownedMiddle.Inner;
        Assert.AreEqual("short", owned.Selected);
        Assert.AreEqual("configured", owned.Label);
        Assert.IsInstanceOfType<BlueRepository<string>>(owned.Repository);
        Assert.AreSame(ownedOuter, scope.ServiceProvider.GetRequiredKeyedService<ITracked>("owned"));
        var callerOuter = (TrackedDecorator)scope.ServiceProvider.GetRequiredKeyedService<ITracked>("caller");
        var callerMiddle = (TrackedDecorator)callerOuter.Inner;
        Assert.AreSame(caller, callerMiddle.Inner);
        scope.Dispose();
        Assert.AreEqual(1, owned.DisposeCount);
        Assert.AreEqual(1, ownedMiddle.DisposeCount);
        Assert.AreEqual(1, ownedOuter.DisposeCount);
        Assert.AreEqual(0, caller.DisposeCount);
        Assert.AreEqual(0, callerOuter.DisposeCount);
        provider.Dispose();
        Assert.AreEqual(1, owned.DisposeCount);
        Assert.AreEqual(0, caller.DisposeCount);
        Assert.AreEqual(1, callerMiddle.DisposeCount);
        Assert.AreEqual(1, callerOuter.DisposeCount);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void DependsOnDiagnosticsUseActualKeyedLifetimeAfterDecorationAndMutation(bool keyedSingleton, bool decorated)
    {
        var services = new ServiceCollection();
        services.AddTransient<DisposableDependency>();
        Dependency[] map = [Dependency.OnValue("label", "diagnostic")];
        if (keyedSingleton)
        {
            services.AddScoped<DiagnosticCandidate>(map);
            services.AddKeyedSingleton<DiagnosticCandidate>("blue", map);
        }
        else
        {
            services.AddSingleton<DiagnosticCandidate>(map);
            services.AddKeyedScoped<DiagnosticCandidate>("blue", map);
        }
        if (decorated) services.Decorate<DiagnosticCandidate, DiagnosticDecorator>();
        using var provider = ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            DetectIncorrectUsageOfTransientDisposables = true,
            AllowSingletonToResolveTransientDisposables = true
        });
        Tamper(provider);
        var lifetimes = provider.GetRequiredService<ServiceLifetimes>();
        lifetimes.Add(typeof(DiagnosticCandidate), keyedSingleton ? ServiceLifetime.Singleton : ServiceLifetime.Scoped);
        lifetimes.Add(typeof(DiagnosticCandidate), keyedSingleton ? ServiceLifetime.Scoped : ServiceLifetime.Singleton, "blue");
        Assert.AreEqual(keyedSingleton, provider.IsKeyedSingletonServiceRegistered<DiagnosticCandidate>("blue"));
        if (keyedSingleton)
        {
            var candidate = provider.GetRequiredKeyedService<DiagnosticCandidate>("blue");
            Assert.AreEqual("short", candidate.Selected);
            Assert.AreEqual("diagnostic", candidate.Label);
            if (decorated) Assert.IsInstanceOfType<DiagnosticDecorator>(candidate);
            Assert.AreSame(candidate, provider.GetRequiredKeyedService<DiagnosticCandidate>("blue"));
            provider.Dispose();
            Assert.AreEqual(1, candidate.Dependency.DisposeCount);
        }
        else
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetRequiredKeyedService<DiagnosticCandidate>("blue"));
            using var scope = provider.CreateScope();
            var candidate = scope.ServiceProvider.GetRequiredKeyedService<DiagnosticCandidate>("blue");
            Assert.AreEqual("short", candidate.Selected);
            if (decorated) Assert.IsInstanceOfType<DiagnosticDecorator>(candidate);
            scope.Dispose();
            Assert.AreEqual(1, candidate.Dependency.DisposeCount);
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void InspectorHonorsPreferredConstructorAndNamedAttributeOverrides(bool diagnostics, bool keyed)
    {
        var services = new ServiceCollection();
        services.AddSingleton<Available>();
        services.AddKeyedSingleton<IRepository<string>, RedRepository<string>>("red");
        services.AddKeyedSingleton<IRepository<string>, BlueRepository<string>>("blue");
        var descriptors = new AssemblyInspector().FromAssemblyContaining<ScannedCandidate>()
            .BasedOn<ScannedCandidate>().If(t => t == typeof(ScannedCandidate)).WithServiceSelf()
            .Configure((registration, _) =>
            {
                registration.ServiceKey = keyed ? "outer" : null;
                registration.DependsOn = [Dependency.OnValue("label", "inspected"),
                    Dependency.OnValue("key", "configured-key"), Parameter.ForKey("repository").Eq("blue")];
            }).LifestyleScoped();
        foreach (var descriptor in descriptors) ((IServiceCollection)services).Add(descriptor);
        using var provider = Build(services, diagnostics);
        Tamper(provider);
        using var scope = provider.CreateScope();
        var result = scope.ServiceProvider.GetRequiredKeyedService<ScannedCandidate>(keyed ? "outer" : null);
        Assert.AreEqual("preferred", result.Selected);
        Assert.AreEqual("inspected", result.Label);
        Assert.AreEqual("configured-key", result.Key);
        Assert.IsInstanceOfType<BlueRepository<string>>(result.Repository);
    }

    private static ServiceProvider Build(IServiceCollection services, bool diagnostics) =>
        ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            DetectIncorrectUsageOfTransientDisposables = diagnostics
        });

    private static void Tamper(ServiceProvider provider)
    {
        provider.GetRequiredService<ServiceTypes>().Clear();
        provider.GetRequiredService<ServiceKeys>().Clear();
        provider.GetRequiredService<ServiceKeys<IRepository<string>>>().Clear();
        provider.GetRequiredService<ServiceKeys<IRepository<string>>>().Add("invented");
    }

    public interface IRepository<T> { }
    public sealed class UnkeyedRepository<T> : IRepository<T> { }
    public sealed class BlueRepository<T> : IRepository<T> { }
    public sealed class RedRepository<T> : IRepository<T> { }
    public sealed class ClosedRepository : IRepository<string> { }
    public sealed class Missing { }
    public sealed class Available { }
    public sealed class DisposableDependency : IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
    public sealed class GenericCandidate
    {
        public string Selected { get; } = "short";
        public string Label { get; }
        public IRepository<int> Left { get; }
        public IRepository<int> Right { get; }
        public IRepository<string> Closed { get; }
        public object? Key { get; }
        public int Count { get; }
        public GenericCandidate(string label, [FromKeyedServices("red")] IRepository<int> left,
            IRepository<int> right, IRepository<string> closed, [ServiceKey] object? key, int count = 7)
        { Label = label; Left = left; Right = right; Closed = closed; Key = key; Count = count; }
        public GenericCandidate(string label, IRepository<int> left, IRepository<int> right,
            IRepository<string> closed, [ServiceKey] object? key, DisposableDependency rejected, Missing missing)
            : this(label, left, right, closed, key) => Selected = "long";
    }
    public interface ITracked : IDisposable { }
    public sealed class Tracked : ITracked
    {
        public string Label { get; }
        public string Selected { get; } = "short";
        public IRepository<string> Repository { get; }
        public int DisposeCount { get; private set; }
        public Tracked(string label, [FromKeyedServices("blue")] IRepository<string> repository)
        { Label = label; Repository = repository; }
        public Tracked(string label, IRepository<string> repository, Missing missing)
            : this(label, repository) => Selected = "long";
        public void Dispose() => DisposeCount++;
    }
    public sealed class TrackedDecorator(ITracked inner) : ITracked
    {
        public ITracked Inner { get; } = inner;
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
    public class DiagnosticCandidate
    {
        public string Label { get; }
        public string Selected { get; } = "short";
        public DisposableDependency Dependency { get; }
        public DiagnosticCandidate(string label, DisposableDependency dependency) { Label = label; Dependency = dependency; }
        public DiagnosticCandidate(string label, DisposableDependency dependency, Missing missing)
            : this(label, dependency) => Selected = "long";
    }
    public sealed class DiagnosticDecorator(DiagnosticCandidate inner) : DiagnosticCandidate(inner.Label, inner.Dependency);
    public sealed class ScannedCandidate
    {
        public string Selected { get; } = "preferred";
        public string Label { get; }
        public object? Key { get; }
        public IRepository<string> Repository { get; }
        [ActivatorUtilitiesConstructor]
        public ScannedCandidate(string label, [FromKeyedServices("red")] IRepository<string> repository, [ServiceKey] object? key)
        { Label = label; Repository = repository; Key = key; }
        public ScannedCandidate(string label, IRepository<string> repository, object? key, Available available)
            : this(label, repository, key) => Selected = "long";
    }
}
