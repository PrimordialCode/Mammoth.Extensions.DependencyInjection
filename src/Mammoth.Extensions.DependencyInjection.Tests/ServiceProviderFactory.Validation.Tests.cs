using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class ServiceProviderFactoryValidationTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void DiagnosticsRequireBuildValidationBeforeTouchingRegistrations(bool explicitlyDisabled, bool factoryEntryPoint)
    {
        IServiceCollection services = new ServiceCollection();
        var activations = 0;
        services.AddSingleton<First>(_ => { activations++; return new First(); });
        services.AddTransient<CycleA>();
        services.AddTransient<CycleB>();
        var original = services.ToArray();
        var options = new ExtendedServiceProviderOptions { DetectIncorrectUsageOfTransientDisposables = true };
        if (explicitlyDisabled) options.ValidateOnBuild = false;

        var error = Assert.ThrowsExactly<ArgumentException>(() =>
        {
            if (factoryEntryPoint)
            {
                IServiceProviderFactory<IServiceCollection> factory = new ServiceProviderFactory(options);
                factory.CreateServiceProvider(factory.CreateBuilder(services));
            }
            else ServiceProviderFactory.CreateServiceProvider(services, options);
        });

        Assert.AreEqual("options", error.ParamName);
        StringAssert.Contains(error.Message, "ValidateOnBuild");
        Assert.AreEqual(0, activations);
        Assert.IsFalse(options.ValidateOnBuild, "Do not silently change caller options.");
        CollectionAssert.AreEqual(original, services.ToArray());
        options.ValidateOnBuild = true;
        var cycle = Assert.ThrowsExactly<AggregateException>(() => ServiceProviderFactory.CreateServiceProvider(services, options));
        StringAssert.Contains(cycle.ToString(), "circular dependency");
        Assert.AreEqual(0, activations);
        CollectionAssert.AreEqual(original, services.ToArray());
    }

    public static IEnumerable<object[]> ConstructorCycles()
    {
        foreach (var mode in new[] { "native", "disabled", "enabled" })
        foreach (var lifetime in new[] { ServiceLifetime.Transient, ServiceLifetime.Scoped, ServiceLifetime.Singleton })
        foreach (var keyed in new[] { false, true })
        foreach (var self in new[] { false, true })
            yield return [mode, lifetime, keyed, self];
    }

    [TestMethod]
    [DynamicData(nameof(ConstructorCycles))]
    public void NativeBuildValidationRejectsConstructorCycles(string mode, ServiceLifetime lifetime, bool keyed, bool self)
    {
        IServiceCollection services = new ServiceCollection();
        var types = self ? new[] { typeof(SelfCycle) } : new[] { typeof(CycleA), typeof(CycleB) };
        foreach (var type in types)
            services.Add(keyed ? ServiceDescriptor.DescribeKeyed(type, "cycle", type, lifetime)
                : ServiceDescriptor.Describe(type, type, lifetime));

        var error = Assert.ThrowsExactly<AggregateException>(() => Build(services, mode));
        StringAssert.Contains(error.ToString(), "circular dependency");
        StringAssert.Contains(error.ToString(), types[0].Name);
        if (!self) StringAssert.Contains(error.ToString(), nameof(CycleB));
    }

    public static IEnumerable<object[]> InvalidGraphs()
    {
        foreach (var mode in new[] { "native", "disabled", "enabled" })
        foreach (var keyed in new[] { false, true })
        foreach (var graph in new[] { "missing", "ambiguous", "capture", "indirect-capture" })
            yield return [mode, keyed, graph];
    }

    [TestMethod]
    [DynamicData(nameof(InvalidGraphs))]
    public void ValidateOnBuildRejectsInvalidOriginalGraph(string mode, bool keyed, string graph)
    {
        IServiceCollection services = new ServiceCollection();
        var type = graph switch
        {
            "missing" => typeof(NeedsMissing),
            "ambiguous" => typeof(Ambiguous),
            "capture" => typeof(CapturesScoped),
            _ => typeof(IndirectCapture)
        };
        services.AddSingleton<First>();
        services.AddSingleton<Second>();
        services.AddScoped<Scoped>();
        services.AddTransient<CapturesScoped>();
        services.AddKeyedScoped<Scoped>("dependency");
        services.AddKeyedTransient<KeyedCapture>("dependency");
        if (keyed)
        {
            type = graph switch
            {
                "missing" => typeof(KeyedMissing),
                "capture" => typeof(KeyedCapture),
                "indirect-capture" => typeof(KeyedIndirectCapture),
                _ => type
            };
            services.Add(ServiceDescriptor.DescribeKeyed(type, "consumer", type,
                graph.Contains("capture") ? ServiceLifetime.Singleton : ServiceLifetime.Transient));
        }
        else
            services.Add(ServiceDescriptor.Describe(type, type,
                graph.Contains("capture") ? ServiceLifetime.Singleton : ServiceLifetime.Transient));

        var error = Assert.ThrowsExactly<AggregateException>(() => Build(services, mode));
        StringAssert.Contains(error.ToString(), type.Name);
        StringAssert.Contains(error.ToString(), graph switch
        {
            "missing" => "Unable to resolve service",
            "ambiguous" => "ambiguous",
            _ => "Cannot consume scoped service"
        });
    }

    [TestMethod]
    [DataRow("native")]
    [DataRow("disabled")]
    [DataRow("enabled")]
    public void ValidationDoesNotActivateFactoriesConstructorsOrDisposeCallerInstances(string mode)
    {
        var counts = new Counts();
        var instance = new CallerOwned();
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(counts);
        services.AddSingleton(instance);
        services.AddKeyedSingleton("instance", instance);
        services.AddSingleton<Constructed>();
        services.AddKeyedSingleton<Constructed>("constructed");
        services.AddSingleton(_ => { counts.Factories++; return new First(); });
        services.AddKeyedSingleton("factory", (_, _) => { counts.Factories++; return new Second(); });
        services.AddTransient<MetadataConsumer>();
        // Native validation deliberately treats user factories and open generics as opaque.
        services.AddTransient(typeof(OpenGeneric<>));
        using (var provider = Build(services, mode))
        {
            Assert.AreEqual(0, counts.Constructors);
            Assert.AreEqual(0, counts.Factories);
            Assert.AreEqual(0, instance.Disposals);
            Assert.IsNotNull(provider.GetRequiredService<MetadataConsumer>().Types);
            Assert.AreSame(instance, provider.GetRequiredService<CallerOwned>());
            Assert.AreSame(instance, provider.GetRequiredKeyedService<CallerOwned>("instance"));
            var first = provider.GetRequiredService<Constructed>();
            Assert.AreSame(first, provider.GetRequiredService<Constructed>());
            provider.GetRequiredKeyedService<Constructed>("constructed");
            provider.GetRequiredService<First>();
            provider.GetRequiredKeyedService<Second>("factory");
            Assert.AreEqual(2, counts.Constructors);
            Assert.AreEqual(2, counts.Factories);
        }
        Assert.AreEqual(0, instance.Disposals);
    }

    [TestMethod]
    [DataRow("native", false)]
    [DataRow("disabled", false)]
    [DataRow("native", true)]
    [DataRow("disabled", true)]
    [DataRow("enabled", true)]
    public void ValidationOptionsRemainIndependent(string mode, bool validateOnBuild)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddScoped<Scoped>();
        services.AddSingleton<CapturesScoped>();
        if (!validateOnBuild)
            services.AddTransient<NeedsMissing>();
        using var provider = Build(services, mode, validateOnBuild, validateScopes: false);
        Assert.IsNotNull(provider.GetRequiredService<CapturesScoped>());
    }

    [TestMethod]
    [DataRow("native")]
    [DataRow("disabled")]
    [DataRow("enabled")]
    public void ValidationLeavesUserFactoriesOpaque(string mode)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<NeedsMissing>(_ => throw new InvalidOperationException("factory invoked"));
        services.AddKeyedSingleton<KeyedMissing>("consumer", (_, _) => throw new InvalidOperationException("keyed factory invoked"));
        using var provider = Build(services, mode);
    }

    [TestMethod]
    [DataRow("native")]
    [DataRow("disabled")]
    public void ScopeValidationAloneDoesNotEnableBuildValidation(string mode)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<NeedsMissing>();
        using var provider = Build(services, mode, validateOnBuild: false, validateScopes: true);
    }

    [TestMethod]
    [DataRow("native")]
    [DataRow("disabled")]
    [DataRow("enabled")]
    public void ValidScopedAndKeyedGraphsStillResolveAfterValidation(string mode)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddScoped<Scoped>();
        services.AddScoped<CapturesScoped>();
        services.AddKeyedScoped<Scoped>("dependency");
        services.AddKeyedScoped<KeyedCapture>("consumer");
        using var provider = Build(services, mode);
        using var scope = provider.CreateScope();
        Assert.AreSame(scope.ServiceProvider.GetRequiredService<Scoped>(),
            scope.ServiceProvider.GetRequiredService<CapturesScoped>().Dependency);
        Assert.AreSame(scope.ServiceProvider.GetRequiredKeyedService<Scoped>("dependency"),
            scope.ServiceProvider.GetRequiredKeyedService<KeyedCapture>("consumer").Dependency);
    }

    private static ServiceProvider Build(IServiceCollection services, string mode,
        bool validateOnBuild = true, bool validateScopes = true)
    {
        if (mode == "native")
        {
            // Native control needs the metadata type explicitly; Mammoth supplies it itself.
            services.AddSingleton(new ServiceTypes());
            return services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateOnBuild = validateOnBuild, ValidateScopes = validateScopes });
        }
        return ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            ValidateOnBuild = validateOnBuild,
            ValidateScopes = validateScopes,
            DetectIncorrectUsageOfTransientDisposables = mode == "enabled"
        });
    }

    public interface IMissing;
    public sealed class NeedsMissing(IMissing missing) { public IMissing Missing { get; } = missing; }
    public sealed class KeyedMissing([FromKeyedServices("missing")] IMissing missing) { public IMissing Missing { get; } = missing; }
    public sealed class First;
    public sealed class Second;
    public sealed class Scoped;
    public sealed class CapturesScoped(Scoped dependency) { public Scoped Dependency { get; } = dependency; }
    public sealed class IndirectCapture(CapturesScoped dependency) { public CapturesScoped Dependency { get; } = dependency; }
    public sealed class KeyedCapture([FromKeyedServices("dependency")] Scoped dependency) { public Scoped Dependency { get; } = dependency; }
    public sealed class KeyedIndirectCapture([FromKeyedServices("dependency")] KeyedCapture dependency) { public KeyedCapture Dependency { get; } = dependency; }
    public sealed class Ambiguous
    {
        public Ambiguous(First first) { }
        public Ambiguous(Second second) { }
    }
    public sealed class Counts { public int Constructors; public int Factories; }
    public sealed class Constructed { public Constructed(Counts counts) { counts.Constructors++; } }
    public sealed class CallerOwned : IDisposable { public int Disposals; public void Dispose() => Disposals++; }
    public sealed class MetadataConsumer(ServiceTypes types) { public ServiceTypes Types { get; } = types; }
    public sealed class OpenGeneric<T>(IMissing missing) { public IMissing Missing { get; } = missing; }
    public sealed class SelfCycle { public SelfCycle([FromKeyedServices] SelfCycle dependency) { } }
    public sealed class CycleA { public CycleA([FromKeyedServices] CycleB dependency) { } }
    public sealed class CycleB { public CycleB([FromKeyedServices] CycleA dependency) { } }
}
