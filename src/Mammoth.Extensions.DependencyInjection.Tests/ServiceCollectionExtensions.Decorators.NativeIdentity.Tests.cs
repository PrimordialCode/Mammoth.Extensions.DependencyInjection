using Mammoth.DependencyInjection.Regression;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class DecoratedNativeIdentityTests
{
    [TestMethod]
    [DataRow(ServiceLifetime.Transient, false)]
    [DataRow(ServiceLifetime.Transient, true)]
    [DataRow(ServiceLifetime.Scoped, false)]
    [DataRow(ServiceLifetime.Scoped, true)]
    public void NativeOriginalsCompileWithoutCollidingWithOtherRegistrations(ServiceLifetime lifetime, bool keyed)
    {
        IServiceCollection services = new ServiceCollection();
        var counts = new Counts();
        var ordinaryObject = new object();
        services.AddSingleton(counts);
        services.AddSingleton(ordinaryObject);
        services.AddTransient<Original>();
        object? key = keyed ? "blue" : null;
        for (var i = 0; i < 2; i++)
        {
            services.Add(keyed ? ServiceDescriptor.DescribeKeyed(typeof(IWork), key, typeof(Original), lifetime)
                : ServiceDescriptor.Describe(typeof(IWork), typeof(Original), lifetime));
            services.Decorate<IWork, Wrapper>();
        }
        var aliases = services.Where(d => (d.IsKeyedService ? d.KeyedImplementationType : d.ImplementationType) == typeof(Original)
            && d.ServiceType != typeof(Original)).ToArray();
        Assert.HasCount(2, aliases);
        Assert.IsFalse(aliases[0].ServiceType.Equals(aliases[1].ServiceType));
        Assert.IsFalse(typeof(object).Equals(aliases[0].ServiceType));
        Assert.IsFalse(aliases[0].ServiceType.Equals(typeof(object)));
        Assert.IsTrue(aliases[0].ServiceType.IsAssignableFrom(aliases[0].ServiceType));
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        using var scope = provider.CreateScope();
        var first = Resolve(scope.ServiceProvider);
        var compiledDescriptors = aliases.Concat(services.Where(d => d.Lifetime == ServiceLifetime.Scoped && d.ServiceType != typeof(IWork))).ToArray();
        var observers = compiledDescriptors.Select(d => NativeResolverCompilation.ObserveService(provider, d.ServiceType, key)).ToArray();
        // Cached scoped public decorators do not call their originals again.
        // Resolve the private descriptors directly to trigger both native compilers.
        foreach (var descriptor in compiledDescriptors)
            scope.ServiceProvider.GetRequiredKeyedService(descriptor.ServiceType, key);
        foreach (var observer in observers) observer.WaitForReplacement();
        for (var repeat = 0; repeat < 100; repeat++)
        {
            var result = Resolve(scope.ServiceProvider);
            Assert.HasCount(2, result);
            Assert.AreNotSame(result[0].Inner, result[1].Inner);
            for (var index = 0; index < 2; index++)
            {
                Assert.AreEqual(keyed ? key : ordinaryObject, result[index].Inner.Key);
                if (lifetime == ServiceLifetime.Scoped) Assert.AreSame(first[index].Inner, result[index].Inner);
                else Assert.AreNotSame(first[index].Inner, result[index].Inner);
            }
            Assert.AreSame(ordinaryObject, scope.ServiceProvider.GetRequiredService<object>());
            Assert.AreNotSame(result[0].Inner, scope.ServiceProvider.GetRequiredService<Original>());
        }
        using (var other = provider.CreateScope())
        {
            var result = Resolve(other.ServiceProvider);
            Assert.AreNotSame(first[0].Inner, result[0].Inner);
            Assert.AreNotSame(first[1].Inner, result[1].Inner);
        }
        scope.Dispose();
        Assert.IsNotEmpty(counts.Created);
        Assert.IsTrue(counts.Created.All(original => original.Disposals == 1));
        foreach (var alias in aliases)
        {
            Assert.IsFalse(provider.IsServiceRegistered(alias.ServiceType));
            Assert.HasCount(0, services.GetServiceDescriptors(alias.ServiceType).ToArray());
            Assert.IsFalse(services.IsTransientServiceRegistered(alias.ServiceType));
            Assert.IsFalse(services.IsScopedServiceRegistered(alias.ServiceType));
            Assert.IsFalse(services.IsSingletonServiceRegistered(alias.ServiceType));
        }

        Wrapper[] Resolve(IServiceProvider sp) => (keyed ? sp.GetKeyedServices<IWork>(key) : sp.GetServices<IWork>()).Cast<Wrapper>().ToArray();
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void CyclicOriginalGraphsFailBeforeDependencyFactories(bool keyed, bool validateOnBuild)
    {
        var nativeCounts = new Counts();
        var counts = new Counts();
        var native = Configure(nativeCounts);
        var decorated = Configure(counts);
        decorated.Decorate<IWork, Wrapper>();
        var options = new ServiceProviderOptions { ValidateOnBuild = validateOnBuild };
        Exception nativeError, error;
        if (validateOnBuild)
        {
            nativeError = Assert.ThrowsExactly<AggregateException>(() => native.BuildServiceProvider(options));
            error = Assert.ThrowsExactly<AggregateException>(() => decorated.BuildServiceProvider(options));
        }
        else
        {
            using var nativeProvider = native.BuildServiceProvider(options);
            using var provider = decorated.BuildServiceProvider(options);
            nativeError = Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(nativeProvider));
            error = Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(provider));
        }
        StringAssert.Contains(nativeError.ToString(), "circular dependency");
        StringAssert.Contains(error.ToString(), "circular dependency");
        Assert.AreEqual(0, nativeCounts.Parts);
        Assert.AreEqual(0, counts.Parts);
        Assert.HasCount(0, counts.Created);

        IServiceCollection Configure(Counts tracker)
        {
            IServiceCollection sc = new ServiceCollection();
            sc.AddTransient<Part>(_ => { tracker.Parts++; return new Part(); });
            sc.AddTransient(typeof(ICycle<>), typeof(Cycle<>));
            sc.Add(keyed ? ServiceDescriptor.KeyedTransient<IWork, CyclicOriginal>("blue")
                : ServiceDescriptor.Transient<IWork, CyclicOriginal>());
            return sc;
        }
        IWork Resolve(IServiceProvider sp) => keyed ? sp.GetRequiredKeyedService<IWork>("blue") : sp.GetRequiredService<IWork>();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DecoratorKeyedDependenciesStillRequireTheirAvailabilityProbe(bool exposeKeyedProbe)
    {
        var counts = new OrdinaryDecorationProbeRegressionTests.Counts();
        IServiceCollection services = new ServiceCollection();
        services.AddKeyedTransient<IWork, PlainOriginal>("blue");
        services.AddKeyedSingleton("blue", new Part());
        services.Decorate<IWork, KeyedWrapper>();
        var outer = services.Single(d => d.ServiceType == typeof(IWork));
        services[services.IndexOf(outer)] = ServiceDescriptor.DescribeKeyed(typeof(IWork), "blue",
            (sp, key) => outer.KeyedImplementationFactory!(new OrdinaryDecorationProbeRegressionTests.ProbeProvider(sp, counts, exposeKeyedProbe, true), key), outer.Lifetime);
        using var provider = services.BuildServiceProvider();
        if (exposeKeyedProbe) Assert.IsInstanceOfType<KeyedWrapper>(provider.GetRequiredKeyedService<IWork>("blue"));
        else
        {
            var error = Assert.ThrowsExactly<NotSupportedException>(() => provider.GetRequiredKeyedService<IWork>("blue"));
            StringAssert.Contains(error.Message, nameof(IServiceProviderIsKeyedService));
        }
        Assert.IsGreaterThan(0, counts.KeyProbeRequests);
    }

    public interface IWork { object? Key { get; } }
    public sealed class Counts { public int Parts; public List<Original> Created { get; } = []; }
    public sealed class Original : IWork, IDisposable
    {
        public Original(Counts counts, [ServiceKey] object? key = null) { Key = key; counts.Created.Add(this); }
        public object? Key { get; }
        public int Disposals { get; private set; }
        public void Dispose() => Disposals++;
    }
    public sealed class Wrapper(IWork inner) : IWork { public IWork Inner { get; } = inner; public object? Key => Inner.Key; }
    public sealed class Part;
    public interface ICycle<T>;
    public sealed class Cycle<T>(ICycle<T> self) : ICycle<T> { public ICycle<T> Self { get; } = self; }
    public sealed class CyclicOriginal(Part part, ICycle<int> cycle) : IWork { public object? Key => null; public Part Part { get; } = part; public ICycle<int> Cycle { get; } = cycle; }
    public sealed class PlainOriginal : IWork { public object? Key => null; }
    public sealed class KeyedWrapper(IWork inner, [FromKeyedServices] Part part) : IWork { public object? Key => inner.Key; public Part Part { get; } = part; }
}
