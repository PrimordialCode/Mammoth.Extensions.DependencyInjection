using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class DecoratedGraphPlanningRegressionTests
{
    public static IEnumerable<object[]> GraphCases()
    {
        foreach (var type in new[] { typeof(Rejected), typeof(RejectedEnumerable), typeof(Selected), typeof(SelectedEnumerable) })
        foreach (var kind in new[] { "native", "snapshot", "diagnostics" })
        foreach (var lifetime in new[] { ServiceLifetime.Transient, ServiceLifetime.Scoped, ServiceLifetime.Singleton })
        foreach (var keyed in new[] { false, true })
        foreach (var layers in new[] { 1, 2 })
            yield return [type, kind, lifetime, keyed, layers];
    }

    [TestMethod]
    [DynamicData(nameof(GraphCases))]
    public void OriginalGraphsFailBeforeActivationAndValidGraphsActivateOnlyOnce(
        Type type, string kind, ServiceLifetime lifetime, bool keyed, int layers)
    {
        foreach (var closed in new[] { false, true })
        foreach (var valid in new[] { false, true })
        {
            var nativeCounts = new Counts();
            var counts = new Counts();
            var nativeServices = Configure(nativeCounts);
            var services = Configure(counts);
            using var native = nativeServices.BuildServiceProvider();
            for (var index = 0; index < layers; index++) services.Decorate<IWork, Forwarder>();
            // Startup validation correctly catches an invalid closed dependency;
            // open generic graphs are discovered only at runtime on every provider.
            if (closed && !valid && kind == "diagnostics")
            {
                Assert.ThrowsExactly<AggregateException>(() => Build(services, kind));
                Assert.AreEqual(0, counts.Parts + counts.Broken + counts.Roots + counts.Wrappers);
                continue;
            }
            using var provider = Build(services, kind);
            // A caller's later mutation must not repair the already-built graph.
            services.AddSingleton<MissingOther>();
            using var nativeScope = native.CreateScope();
            using var scope = provider.CreateScope();
            for (var repeat = 0; repeat < 2; repeat++)
            {
                if (!valid)
                {
                    var nativeError = Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(nativeScope.ServiceProvider, keyed));
                    var error = Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(scope.ServiceProvider, keyed));
                    StringAssert.Contains(nativeError.Message, nameof(MissingOther));
                    StringAssert.Contains(error.Message, nameof(MissingOther));
                    Assert.AreEqual(0, nativeCounts.Parts + nativeCounts.Broken + nativeCounts.Roots);
                    Assert.AreEqual(0, counts.Parts + counts.Broken + counts.Roots + counts.Wrappers,
                        "Graph errors must be detected before executing any application factories or constructors.");
                }
                else
                {
                    var expected = Resolve(nativeScope.ServiceProvider, keyed);
                    var result = Resolve(scope.ServiceProvider, keyed);
                    Assert.AreEqual(expected.Value, result.Value);
                    Assert.AreEqual(nativeCounts.Parts, counts.Parts);
                    Assert.AreEqual(nativeCounts.Broken, counts.Broken);
                    Assert.AreEqual(nativeCounts.Roots, counts.Roots);
                    Assert.AreEqual(counts.Roots * layers, counts.Wrappers);
                }
            }

            IServiceCollection Configure(Counts tracker)
            {
                var collection = Services(tracker);
                if (valid) collection.AddSingleton<MissingOther>();
                collection.Add(closed ? ServiceDescriptor.Transient<IBroken<int>, Broken<int>>()
                    : ServiceDescriptor.Transient(typeof(IBroken<>), typeof(Broken<>)));
                collection.Add(keyed ? ServiceDescriptor.DescribeKeyed(typeof(IWork), "blue", type, lifetime)
                    : ServiceDescriptor.Describe(typeof(IWork), type, lifetime));
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
    public void NonemptyDependsOnFactoriesKeepTheirSeparateRejectedCandidatePolicy(string kind, bool keyed)
    {
        var counts = new Counts();
        var services = Services(counts);
        services.AddTransient(typeof(IBroken<>), typeof(Broken<>));
        Dependency[] map = [Dependency.OnValue("unused", 1)];
        if (keyed) services.AddKeyedTransient<IWork, Rejected>("blue", map);
        else services.AddTransient<IWork, Rejected>(map);
        services.Decorate<IWork, Forwarder>();
        using var provider = Build(services, kind);
        using var scope = provider.CreateScope();
        Assert.AreEqual("valid", Resolve(scope.ServiceProvider, keyed).Value);
        Assert.AreEqual(1, counts.Parts);
        Assert.AreEqual(0, counts.Broken);
    }

    [TestMethod]
    [DataRow("native", false)]
    [DataRow("native", true)]
    [DataRow("snapshot", false)]
    [DataRow("snapshot", true)]
    [DataRow("diagnostics", false)]
    [DataRow("diagnostics", true)]
    public void OpaqueDependencyFactoriesStillRunOnlyAtRealResolutionAndKeepTheirException(string kind, bool keyed)
    {
        var nativeCounts = new Counts();
        var counts = new Counts();
        var failure = new InvalidOperationException("Application dependency failure");
        using var native = Configure(nativeCounts).BuildServiceProvider();
        var services = Configure(counts);
        services.Decorate<IWork, Forwarder>();
        using var provider = Build(services, kind);
        using var nativeScope = native.CreateScope();
        using var scope = provider.CreateScope();
        Assert.AreSame(failure, Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(nativeScope.ServiceProvider, keyed)));
        Assert.AreSame(failure, Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(scope.ServiceProvider, keyed)));
        Assert.AreEqual(1, counts.Parts);
        Assert.AreEqual(nativeCounts.Parts, counts.Parts);
        Assert.AreEqual(1, counts.Broken);
        Assert.AreEqual(nativeCounts.Broken, counts.Broken);
        Assert.AreEqual(0, counts.Roots + counts.Wrappers);

        IServiceCollection Configure(Counts tracker)
        {
            var collection = Services(tracker);
            collection.AddTransient<IBroken<int>>(_ => { tracker.Broken++; throw failure; });
            if (keyed) collection.AddKeyedTransient<IWork, Selected>("blue");
            else collection.AddTransient<IWork, Selected>();
            return collection;
        }
    }

    [TestMethod]
    [DataRow("native", false)]
    [DataRow("native", true)]
    [DataRow("snapshot", false)]
    [DataRow("snapshot", true)]
    [DataRow("diagnostics", false)]
    [DataRow("diagnostics", true)]
    public void EarlierInvalidEnumerableRegistrationIsNotHiddenByLastValidBinding(string kind, bool keyed)
    {
        var nativeCounts = new Counts();
        var counts = new Counts();
        using var native = Configure(nativeCounts).BuildServiceProvider();
        var services = Configure(counts);
        services.Decorate<IWork, Forwarder>();
        services.Decorate<IWork, Forwarder>();
        using var provider = Build(services, kind);
        using var scope = provider.CreateScope();
        StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(native, keyed)).Message, nameof(MissingOther));
        StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(scope.ServiceProvider, keyed)).Message, nameof(MissingOther));
        Assert.AreEqual(0, nativeCounts.Parts + nativeCounts.Broken + nativeCounts.Roots);
        Assert.AreEqual(0, counts.Parts + counts.Broken + counts.Roots + counts.Wrappers);

        IServiceCollection Configure(Counts tracker)
        {
            var collection = Services(tracker);
            collection.AddTransient(typeof(IBroken<>), typeof(Broken<>));
            collection.AddTransient(typeof(IBroken<>), typeof(Healthy<>));
            if (keyed) collection.AddKeyedTransient<IWork, SelectedEnumerable>("blue");
            else collection.AddTransient<IWork, SelectedEnumerable>();
            return collection;
        }
    }

    [TestMethod]
    [DataRow("native", false)]
    [DataRow("native", true)]
    [DataRow("snapshot", false)]
    [DataRow("snapshot", true)]
    [DataRow("diagnostics", false)]
    [DataRow("diagnostics", true)]
    public void SuccessfulGraphCheckDoesNotLeakAcrossProvidersSharingTheOriginalDescriptor(string kind, bool keyed)
    {
        var original = keyed ? ServiceDescriptor.KeyedTransient<IWork, Selected>("blue")
            : ServiceDescriptor.Transient<IWork, Selected>();
        var validCounts = new Counts();
        var invalidCounts = new Counts();
        using var valid = Build(Configure(validCounts, complete: true), kind);
        using var invalid = Build(Configure(invalidCounts, complete: false), kind);
        using var validScope = valid.CreateScope();
        using var invalidScope = invalid.CreateScope();
        Assert.AreEqual("selected", Resolve(validScope.ServiceProvider, keyed).Value);
        StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(invalidScope.ServiceProvider, keyed)).Message, nameof(MissingOther));
        Assert.AreEqual(0, invalidCounts.Parts + invalidCounts.Broken + invalidCounts.Roots + invalidCounts.Wrappers);

        IServiceCollection Configure(Counts tracker, bool complete)
        {
            var collection = Services(tracker);
            if (complete) collection.AddSingleton<MissingOther>();
            collection.AddTransient(typeof(IBroken<>), typeof(Broken<>));
            collection.Add(original);
            collection.Decorate<IWork, Forwarder>();
            return collection;
        }
    }

    public sealed class Healthy<T> : IBroken<T>
    {
        public Healthy(Counts counts) => counts.Broken++;
    }

    private static IServiceCollection Services(Counts counts)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(counts);
        services.AddTransient<Part>(_ => { counts.Parts++; return new Part(); });
        services.AddSingleton<Other>();
        return services;
    }
    private static IWork Resolve(IServiceProvider provider, bool keyed) => keyed
        ? provider.GetRequiredKeyedService<IWork>("blue") : provider.GetRequiredService<IWork>();
    private static ServiceProvider Build(IServiceCollection services, string kind) => kind switch
    {
        "snapshot" => ServiceProviderFactory.CreateServiceProvider(services),
        "diagnostics" => ServiceProviderFactory.CreateServiceProvider(services,
            new ExtendedServiceProviderOptions { ValidateOnBuild = true, DetectIncorrectUsageOfTransientDisposables = true }),
        _ => services.BuildServiceProvider()
    };
    public sealed class Counts { public int Parts; public int Broken; public int Roots; public int Wrappers; }
    public sealed class Part;
    public sealed class Other;
    public sealed class Missing;
    public sealed class MissingOther;
    public interface IBroken<T>;
    public sealed class Broken<T> : IBroken<T>
    {
        public Broken(MissingOther missing, Counts counts) => counts.Broken++;
    }
    public interface IWork { string Value { get; } }
    public abstract class Work : IWork
    {
        public string Value { get; }
        protected Work(string value, Counts counts) { Value = value; counts.Roots++; }
    }
    public sealed class Rejected : Work
    {
        public Rejected(Part part, Other other, Counts counts) : base("valid", counts) { }
        public Rejected(IBroken<int> broken, Missing missing, Counts counts) : base("invalid", counts) { }
    }
    public sealed class RejectedEnumerable : Work
    {
        public RejectedEnumerable(Part part, Other other, Counts counts) : base("valid", counts) { }
        public RejectedEnumerable(IEnumerable<IBroken<int>> broken, Missing missing, Counts counts) : base("invalid", counts) { }
    }
    public sealed class Selected(Part part, IBroken<int> broken, Counts counts) : Work("selected", counts)
    {
        public Part Part { get; } = part;
        public IBroken<int> Broken { get; } = broken;
    }
    public sealed class SelectedEnumerable(Part part, IEnumerable<IBroken<int>> broken, Counts counts) : Work("selected", counts)
    {
        public Part Part { get; } = part;
        public IEnumerable<IBroken<int>> Broken { get; } = broken;
    }
    public sealed class Forwarder : IWork
    {
        public IWork Inner { get; }
        public string Value => Inner.Value;
        public Forwarder(IWork inner, Counts counts) { Inner = inner; counts.Wrappers++; }
    }
}
