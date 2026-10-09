using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class OrdinaryDecorationProbeRegressionTests
{
    public static IEnumerable<object[]> OrdinaryCases()
    {
        foreach (var type in new[] { typeof(Parameterless), typeof(Ordinary), typeof(Generic), typeof(ContextKey) })
        foreach (var kind in new[] { "native", "snapshot", "diagnostics" })
        foreach (var lifetime in new[] { ServiceLifetime.Transient, ServiceLifetime.Scoped, ServiceLifetime.Singleton })
        foreach (var layers in new[] { 1, 2 })
            yield return [type, kind, lifetime, layers];
    }

    [TestMethod]
    [DynamicData(nameof(OrdinaryCases))]
    public void OrdinaryOriginalsNeedNoKeyedProbeAndRetainLifetimeAndDisposal(
        Type type, string kind, ServiceLifetime lifetime, int layers)
    {
        // Native controls and decorated originals use identical ordinary, null-key,
        // and non-null-key registrations. Only the factories see the probe wrapper.
        foreach (var context in new[] { "ordinary", "null", "blue" })
        foreach (var ordinaryOnlyProbe in new[] { false, true })
        {
            var counts = new Counts();
            var services = Services(counts);
            Add(services, type, lifetime, context);
            using var native = services.BuildServiceProvider();
            using var nativeScope = native.CreateScope();
            var expected = Resolve(nativeScope.ServiceProvider, context);
            for (var layer = 0; layer < layers; layer++) services.Decorate<IWork, Wrapper>();
            WrapFactories(services, counts, exposeKeyedProbe: false, ordinaryOnlyProbe);
            using var provider = Build(services, kind);
            var originals = new HashSet<IWork>();
            var wrappers = new HashSet<Wrapper>();
            IWork? first = null;
            for (var scopeIndex = 0; scopeIndex < 2; scopeIndex++)
            {
                using (var scope = provider.CreateScope())
                    for (var repeat = 0; repeat < 2; repeat++)
                    {
                        var result = Resolve(scope.ServiceProvider, context);
                        if (first == null) first = result;
                        else if (lifetime == ServiceLifetime.Singleton || (lifetime == ServiceLifetime.Scoped && scopeIndex == 0))
                            Assert.AreSame(first, result);
                        else Assert.AreNotSame(first, result);
                        for (var layer = 0; layer < layers; layer++)
                        {
                            var wrapper = (Wrapper)result;
                            wrappers.Add(wrapper);
                            result = wrapper.Inner;
                        }
                        originals.Add(result);
                        Assert.AreEqual(type, result.GetType());
                        Assert.AreEqual(expected.Selected, result.Selected);
                        Assert.AreEqual(expected.Key, result.Key);
                    }
                foreach (var original in originals)
                    Assert.AreEqual(lifetime == ServiceLifetime.Singleton ? 0 : 1, original.Disposals);
            }
            var instances = lifetime == ServiceLifetime.Singleton ? 1 : lifetime == ServiceLifetime.Scoped ? 2 : 4;
            Assert.HasCount(instances, originals);
            Assert.HasCount(instances * layers, wrappers);
            provider.Dispose();
            foreach (var original in originals) Assert.AreEqual(1, original.Disposals);
            foreach (var wrapper in wrappers) Assert.AreEqual(1, wrapper.Disposals);
            Assert.AreEqual(0, counts.KeyProbeRequests, "Ordinary planning and contextual key injection must not request a keyed probe.");
        }
    }

    [TestMethod]
    [DataRow("native")]
    [DataRow("snapshot")]
    [DataRow("diagnostics")]
    public void HiddenKeyedProbeKeepsNativeConstructorPoliciesAndGenericConstraintPlanning(string kind)
    {
        foreach (var type in new[] { typeof(Preferred), typeof(Subset), typeof(Ambiguous), typeof(InvalidGeneric) })
        {
            var nativeCounts = new Counts();
            var counts = new Counts();
            var nativeServices = Configure(nativeCounts);
            var services = Configure(counts);
            using var native = nativeServices.BuildServiceProvider();
            services.Decorate<IWork, Wrapper>();
            WrapFactories(services, counts, exposeKeyedProbe: false, ordinaryOnlyProbe: false);
            using var provider = Build(services, kind);
            using var scope = provider.CreateScope();
            if (type == typeof(InvalidGeneric))
            {
                Assert.ThrowsExactly<ArgumentException>(() => native.GetRequiredService<IWork>());
                Assert.ThrowsExactly<ArgumentException>(() => scope.ServiceProvider.GetRequiredService<IWork>());
                Assert.AreEqual(0, counts.Parts);
                Assert.AreEqual(0, nativeCounts.Parts);
            }
            else if (type == typeof(Ambiguous))
            {
                Assert.ThrowsExactly<InvalidOperationException>(() => native.GetRequiredService<IWork>());
                var error = Assert.ThrowsExactly<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IWork>());
                StringAssert.Contains(error.Message, "ambiguous");
                Assert.AreEqual(0, counts.Parts);
                Assert.AreEqual(0, nativeCounts.Parts);
            }
            else
            {
                var expected = native.GetRequiredService<IWork>();
                var result = scope.ServiceProvider.GetRequiredService<IWork>();
                Assert.AreEqual(expected.Selected, result.Selected);
                Assert.AreEqual(nativeCounts.Parts, counts.Parts);
            }
            Assert.AreEqual(0, counts.KeyProbeRequests);

            IServiceCollection Configure(Counts tracker)
            {
                var collection = Services(tracker);
                collection.AddSingleton<Other>();
                if (type == typeof(InvalidGeneric))
                    collection.AddTransient(typeof(IOpen<>), typeof(ReferenceOnly<>));
                Add(collection, type, ServiceLifetime.Transient, "ordinary");
                return collection;
            }
        }
    }

    [TestMethod]
    [DataRow("native", false)]
    [DataRow("native", true)]
    [DataRow("snapshot", false)]
    [DataRow("snapshot", true)]
    [DataRow("diagnostics", false)]
    [DataRow("diagnostics", true)]
    public void ActualKeyedDependenciesStillRequireTheirAvailabilityProbe(string kind, bool exposeKeyedProbe)
    {
        foreach (var type in new[] { typeof(Explicit), typeof(Inherited) })
        {
            var counts = new Counts();
            var services = Services(counts);
            services.AddKeyedSingleton("fixed", new Part());
            services.AddKeyedSingleton("blue", new Part());
            Add(services, type, ServiceLifetime.Transient, "blue");
            services.Decorate<IWork, Wrapper>();
            services.Decorate<IWork, Wrapper>();
            WrapFactories(services, counts, exposeKeyedProbe, ordinaryOnlyProbe: false);
            using var provider = Build(services, kind);
            using var scope = provider.CreateScope();
            if (exposeKeyedProbe)
                Assert.AreEqual(type == typeof(Explicit) ? "explicit" : "inherited", Resolve(scope.ServiceProvider, "blue").Selected);
            else
            {
                var error = Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(scope.ServiceProvider, "blue"));
                StringAssert.Contains(error.Message, nameof(IServiceProviderIsKeyedService));
                Assert.AreEqual(0, counts.Parts);
            }
            Assert.IsGreaterThan(0, counts.KeyProbeRequests);
        }
    }

    private static IServiceCollection Services(Counts counts)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<Part>(_ => { counts.Parts++; return new Part(); });
        services.AddTransient(typeof(IOpen<>), typeof(Open<>));
        return services;
    }
    private static void Add(IServiceCollection services, Type type, ServiceLifetime lifetime, string context)
        => services.Add(context == "ordinary" ? ServiceDescriptor.Describe(typeof(IWork), type, lifetime)
            : ServiceDescriptor.DescribeKeyed(typeof(IWork), context == "null" ? null : context, type, lifetime));
    private static IWork Resolve(IServiceProvider provider, string context) => context == "ordinary"
        ? provider.GetRequiredService<IWork>() : provider.GetRequiredKeyedService<IWork>(context == "null" ? null : context);
    private static ServiceProvider Build(IServiceCollection services, string kind) => kind switch
    {
        "snapshot" => ServiceProviderFactory.CreateServiceProvider(services),
        "diagnostics" => ServiceProviderFactory.CreateServiceProvider(services,
            new ExtendedServiceProviderOptions { ValidateOnBuild = true, DetectIncorrectUsageOfTransientDisposables = true }),
        _ => services.BuildServiceProvider()
    };
    private static void WrapFactories(IServiceCollection services, Counts counts, bool exposeKeyedProbe, bool ordinaryOnlyProbe)
    {
        // Model a provider passing its own wrapper to factories. Keyed resolution is
        // retained for Mammoth's private decoration slots; availability is independent.
        for (var index = 0; index < services.Count; index++)
        {
            var descriptor = services[index];
            if (descriptor.IsKeyedService && descriptor.KeyedImplementationFactory is { } keyedFactory)
                services[index] = ServiceDescriptor.DescribeKeyed(descriptor.ServiceType, descriptor.ServiceKey,
                    (provider, key) => keyedFactory(new ProbeProvider(provider, counts, exposeKeyedProbe, ordinaryOnlyProbe), key), descriptor.Lifetime);
            else if (!descriptor.IsKeyedService && descriptor.ImplementationFactory is { } factory)
                services[index] = ServiceDescriptor.Describe(descriptor.ServiceType,
                    provider => factory(new ProbeProvider(provider, counts, exposeKeyedProbe, ordinaryOnlyProbe)), descriptor.Lifetime);
        }
    }
    public sealed class Counts { public int Parts; public int KeyProbeRequests; }
    public sealed class ProbeProvider(IServiceProvider provider, Counts counts, bool exposeKeyedProbe, bool ordinaryOnlyProbe)
        : IServiceProvider, IKeyedServiceProvider
    {
        public object? GetService(Type type)
        {
            if (type == typeof(IServiceProviderIsKeyedService))
            {
                counts.KeyProbeRequests++;
                return exposeKeyedProbe ? provider.GetService(type) : null;
            }
            if (ordinaryOnlyProbe && type == typeof(IServiceProviderIsService))
                return new OrdinaryProbe(provider.GetRequiredService<IServiceProviderIsService>());
            return provider.GetService(type);
        }
        public object? GetKeyedService(Type type, object? key) => ((IKeyedServiceProvider)provider).GetKeyedService(type, key);
        public object GetRequiredKeyedService(Type type, object? key) => ((IKeyedServiceProvider)provider).GetRequiredKeyedService(type, key);
    }
    public sealed class OrdinaryProbe(IServiceProviderIsService probe) : IServiceProviderIsService
    {
        public bool IsService(Type type) => probe.IsService(type);
    }
    public interface IWork { string Selected { get; } object? Key { get; } int Disposals { get; } }
    public abstract class Work(string selected, object? key = null) : IWork, IDisposable
    {
        public string Selected { get; } = selected;
        public object? Key { get; } = key;
        public int Disposals { get; private set; }
        public void Dispose() => Disposals++;
    }
    public sealed class Parameterless() : Work("parameterless");
    public sealed class Ordinary(Part part) : Work("ordinary") { public Part Part { get; } = part; }
    public sealed class Generic(IOpen<int> part) : Work("generic") { public IOpen<int> Part { get; } = part; }
    public sealed class ContextKey([ServiceKey] object? key = null) : Work("context", key);
    public sealed class Explicit([FromKeyedServices("fixed")] Part part) : Work("explicit") { public Part Part { get; } = part; }
    public sealed class Inherited([FromKeyedServices] Part part) : Work("inherited") { public Part Part { get; } = part; }
    public sealed class Preferred : Work
    {
        [ActivatorUtilitiesConstructor] public Preferred() : base("short") { }
        public Preferred(Part part) : base("long") { }
    }
    public sealed class Subset : Work
    {
        public Subset(Part part) : base("short") { }
        public Subset(Part part, Other other) : base("long") { }
    }
    public sealed class Ambiguous : Work
    {
        public Ambiguous(Part part) : base("part") { }
        public Ambiguous(Other other) : base("other") { }
    }
    public sealed class InvalidGeneric : Work
    {
        public InvalidGeneric(Part part) : base("valid") { }
        public InvalidGeneric(IOpen<int> invalid, Missing missing) : base("invalid") { }
    }
    public sealed class Part;
    public sealed class Other;
    public sealed class Missing;
    public interface IOpen<T>;
    public sealed class Open<T> : IOpen<T>;
    public sealed class ReferenceOnly<T> : IOpen<T> where T : class;
    public sealed class Wrapper(IWork inner) : IWork, IDisposable
    {
        public IWork Inner { get; } = inner;
        public string Selected => Inner.Selected;
        public object? Key => Inner.Key;
        public int Disposals { get; private set; }
        public void Dispose() => Disposals++;
    }
}
