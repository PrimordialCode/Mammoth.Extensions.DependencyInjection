using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class ServiceProviderFactoryRepeatedBuildTests
{
    [TestMethod]
    [DataRow("default")]
    [DataRow("validated")]
    [DataRow("diagnostics")]
    [DataRow("validated-diagnostics")]
    public void ReadOnlyCollectionCanBuildRepeatedly(string mode)
    {
        var services = new ServiceCollection();
        services.AddSingleton<Marker>();
        services.MakeReadOnly();
        for (int i = 0; i < 3; i++)
        {
            using var provider = Build(services, mode);
            Assert.IsNotNull(provider.GetRequiredService<Marker>());
            Assert.IsTrue(provider.IsServiceRegistered<Marker>());
            Assert.HasCount(1, services);
            AssertOneMetadataSet(provider);
        }
    }

    [TestMethod]
    [DataRow("default")]
    [DataRow("validated")]
    [DataRow("diagnostics")]
    [DataRow("validated-diagnostics")]
    public void RepeatedEmptyBuildsLeaveCallerCollectionUnchanged(string mode)
    {
        IServiceCollection services = new ServiceCollection();
        for (int i = 0; i < 3; i++)
        {
            using var provider = Build(services, mode);
            Assert.HasCount(0, services);
            Assert.HasCount(0, provider.GetRequiredService<ServiceTypes>());
            Assert.HasCount(0, provider.GetRequiredService<ServiceKeys>());
            Assert.IsFalse(provider.IsServiceRegistered<ServiceTypes>());
            Assert.IsFalse(provider.IsServiceRegistered<ServiceKeys>());
            Assert.IsFalse(provider.IsServiceRegistered<ServiceLifetimes>());
            AssertOneMetadataSet(provider);
        }
    }

    [TestMethod]
    [DataRow("default")]
    [DataRow("validated")]
    [DataRow("diagnostics")]
    [DataRow("validated-diagnostics")]
    public void LaterBuildsDoNotRetainEarlierSupportRegistrations(string mode)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddKeyedSingleton<Marker>("first");
        using var first = Build(services, mode);
        var oldTypes = first.GetRequiredService<ServiceTypes>();
        var oldKeys = first.GetRequiredService<ServiceKeys>();
        var oldTypedKeys = first.GetRequiredService<ServiceKeys<Marker>>();
        var oldLifetimes = first.GetRequiredService<ServiceLifetimes>();
        var oldSnapshot = first.GetRequiredService<ServiceProviderRegistrationSnapshot>();
        oldTypes.Add(typeof(string));
        oldKeys.Add("invented");
        oldTypedKeys.Add("invented");

        using var second = Build(services, mode);
        AssertOneMetadataSet(second);
        Assert.AreNotSame(oldTypes, second.GetRequiredService<ServiceTypes>());
        Assert.AreNotSame(oldKeys, second.GetRequiredService<ServiceKeys>());
        Assert.AreNotSame(oldTypedKeys, second.GetRequiredService<ServiceKeys<Marker>>());
        Assert.AreNotSame(oldLifetimes, second.GetRequiredService<ServiceLifetimes>());
        Assert.AreNotSame(oldSnapshot, second.GetRequiredService<ServiceProviderRegistrationSnapshot>());
        // One closed registration plus the open-generic compatibility fallback, with no old copy.
        Assert.HasCount(2, second.GetServices<ServiceKeys<Marker>>().ToArray());
        Assert.IsFalse(second.IsServiceRegistered<string>());
        Assert.IsFalse(second.IsKeyedServiceRegistered("invented"));
        CollectionAssert.AreEquivalent(new[] { typeof(Marker) }, second.GetRequiredService<ServiceTypes>().ToArray());
        CollectionAssert.AreEquivalent(new object[] { "first" }, second.GetRequiredService<ServiceKeys<Marker>>().ToArray());
        Assert.HasCount(1, services);
    }

    [TestMethod]
    [DataRow("default")]
    [DataRow("validated")]
    [DataRow("diagnostics")]
    [DataRow("validated-diagnostics")]
    public void LaterUserEditsProduceIndependentClosedAndOpenKeyedSnapshots(string mode)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddKeyedScoped(typeof(IRepository<>), "shared", typeof(Repository<>));
        services.AddKeyedScoped(typeof(IRepository<>), "open", typeof(Repository<>));
        var closed = ServiceDescriptor.KeyedSingleton<IRepository<string>, ClosedRepository>("shared");
        services.Add(closed);
        var original = services.ToArray();
        using var first = Build(services, mode);
        CollectionAssert.AreEqual(original, services.ToArray());

        services.Remove(closed);
        services.AddKeyedScoped<IRepository<string>, ClosedRepository>("closed");
        services.AddSingleton<Marker>();
        var edited = services.ToArray();
        using var second = Build(services, mode);
        CollectionAssert.AreEqual(edited, services.ToArray());
        services.Clear();
        using var third = Build(services, mode);
        Assert.HasCount(0, third.GetRequiredService<ServiceTypes>());
        Assert.IsFalse(third.IsServiceRegistered<IRepository<string>>());

        Assert.IsFalse(first.IsServiceRegistered<Marker>());
        Assert.IsTrue(second.IsServiceRegistered<Marker>());
        Assert.IsFalse(first.IsKeyedServiceRegistered("closed"));
        Assert.IsTrue(second.IsKeyedServiceRegistered("closed"));
        Assert.IsTrue(first.IsKeyedSingletonServiceRegistered<IRepository<string>>("shared"));
        Assert.IsTrue(second.IsKeyedScopedServiceRegistered<IRepository<string>>("shared"));
        Assert.IsTrue(first.IsServiceRegistered<IRepository<int>>());
        Assert.IsTrue(second.IsServiceRegistered<IRepository<int>>());
        CollectionAssert.AreEquivalent(new object[] { "shared" }, first.GetRequiredService<ServiceKeys<IRepository<string>>>().ToArray());
        CollectionAssert.AreEquivalent(new object[] { "closed" }, second.GetRequiredService<ServiceKeys<IRepository<string>>>().ToArray());
        Assert.AreEqual(ServiceLifetime.Singleton, first.GetRequiredService<ServiceLifetimes>().GetLifetime(typeof(IRepository<string>), "shared"));
        Assert.AreEqual(ServiceLifetime.Scoped, second.GetRequiredService<ServiceLifetimes>().GetLifetime(typeof(IRepository<string>), "shared"));

        foreach (var provider in new[] { first, second })
        {
            using var scope = provider.CreateScope();
            var sp = scope.ServiceProvider;
            var keys = provider == first ? new[] { "shared", "open" } : new[] { "shared", "open", "closed" };
            var expected = sp.GetServices<IRepository<string>>()
                .Concat(keys.SelectMany(key => sp.GetKeyedServices<IRepository<string>>(key))).Cast<object>().ToArray();
            CollectionAssert.AreEquivalent(expected, sp.GetAllServices<IRepository<string>>().Cast<object>().ToArray());
            CollectionAssert.AreEquivalent(expected, sp.GetAllServices(typeof(IRepository<string>)).ToArray());
            Assert.HasCount(4, expected);
            Assert.HasCount(3, sp.GetAllServices<IRepository<int>>().ToArray());
            AssertOneMetadataSet(provider);
        }
    }

    [TestMethod]
    [DataRow("default")]
    [DataRow("validated")]
    [DataRow("diagnostics")]
    [DataRow("validated-diagnostics")]
    public void CallerMetadataRegistrationsKeepTheirIdentityOrderAndMultiplicity(string mode)
    {
        IServiceCollection services = new ServiceCollection();
        var userTypes = new ServiceTypes { typeof(string) };
        var userKeys = new ServiceKeys { "user" };
        var userTypedKeys = new ServiceKeys<Marker>(["user"]);
        var userLifetimes = new ServiceLifetimes();
        var repeated = ServiceDescriptor.Singleton(userTypes);
        services.Add(repeated);
        services.Add(repeated);
        services.AddSingleton(userKeys);
        services.AddSingleton(userTypedKeys);
        services.AddSingleton(userLifetimes);
        services.AddSingleton(typeof(ServiceKeys<>), typeof(UserKeys<>));
        services.AddKeyedSingleton("user-metadata", userTypes);
        var original = services.ToArray();
        for (int i = 0; i < 3; i++)
        {
            using var provider = Build(services, mode);
            var types = provider.GetServices<ServiceTypes>().ToArray();
            Assert.HasCount(3, types);
            Assert.AreSame(userTypes, types[0]);
            Assert.AreSame(userTypes, types[1]);
            Assert.AreSame(provider.GetRequiredService<ServiceTypes>(), types[2]);
            Assert.AreSame(userTypes, provider.GetRequiredKeyedService<ServiceTypes>("user-metadata"));
            Assert.AreSame(userKeys, provider.GetServices<ServiceKeys>().First());
            Assert.AreSame(userTypedKeys, provider.GetServices<ServiceKeys<Marker>>().First());
            Assert.HasCount(1, provider.GetServices<ServiceKeys<Marker>>().OfType<UserKeys<Marker>>().ToArray());
            Assert.AreSame(userLifetimes, provider.GetServices<ServiceLifetimes>().First());
            Assert.IsTrue(provider.IsServiceRegistered<ServiceTypes>());
            Assert.IsTrue(provider.IsKeyedServiceRegistered("user-metadata"));
            Assert.IsFalse(provider.IsServiceRegistered<string>());
            Assert.IsFalse(provider.IsKeyedServiceRegistered("user"));
            CollectionAssert.AreEqual(original, services.ToArray());
        }
    }

    [TestMethod]
    [DataRow("default")]
    [DataRow("validated")]
    [DataRow("diagnostics")]
    [DataRow("validated-diagnostics")]
    public void RepeatedBuildsPreserveIndependentDisposalAndCallerOwnership(string mode)
    {
        IServiceCollection services = new ServiceCollection();
        var callerOwned = new Owned();
        services.AddSingleton(callerOwned);
        services.AddKeyedSingleton("caller", callerOwned);
        services.AddKeyedSingleton<Owned>("owned");
        services.AddScoped<IRepository<string>, DisposableRepository>();
        services.Decorate<IRepository<string>, RepositoryDecorator>();
        services.AddTransient<TransientOwned>();
        var original = services.ToArray();
        using var first = Build(services, mode);
        using var second = Build(services, mode);
        var firstOwned = first.GetRequiredKeyedService<Owned>("owned");
        var secondOwned = second.GetRequiredKeyedService<Owned>("owned");
        Assert.AreNotSame(firstOwned, secondOwned);
        foreach (var provider in new[] { first, second })
        {
            Assert.AreSame(callerOwned, provider.GetRequiredService<Owned>());
            Assert.AreSame(callerOwned, provider.GetRequiredKeyedService<Owned>("caller"));
            using var scope = provider.CreateScope();
            var decorator = (RepositoryDecorator)scope.ServiceProvider.GetRequiredService<IRepository<string>>();
            var inner = (DisposableRepository)decorator.Inner;
            var transient = scope.ServiceProvider.GetRequiredService<TransientOwned>();
            scope.Dispose();
            Assert.AreEqual(1, inner.Disposals);
            Assert.AreEqual(1, decorator.Disposals);
            Assert.AreEqual(1, transient.Disposals);
        }
        first.Dispose();
        Assert.AreEqual(1, firstOwned.Disposals);
        Assert.AreEqual(0, secondOwned.Disposals);
        Assert.AreEqual(0, callerOwned.Disposals);
        second.Dispose();
        Assert.AreEqual(1, secondOwned.Disposals);
        Assert.AreEqual(0, callerOwned.Disposals);
        CollectionAssert.AreEqual(original, services.ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedValidationLeavesCollectionReusable(bool diagnostics)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<NeedsMissing>();
        var original = services.ToArray();
        var mode = diagnostics ? "validated-diagnostics" : "validated";
        Assert.ThrowsExactly<AggregateException>(() => Build(services, mode));
        CollectionAssert.AreEqual(original, services.ToArray());
        services.AddSingleton<Marker>();
        using var provider = Build(services, mode);
        Assert.IsNotNull(provider.GetRequiredService<NeedsMissing>());
        AssertOneMetadataSet(provider);
        Assert.HasCount(2, services);
    }

    [TestMethod]
    public void FailedDiagnosticPatchingLeavesCollectionReusable()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient(typeof(DisposableGeneric<>));
        var original = services.ToArray();
        Assert.ThrowsExactly<InvalidOperationException>(() => ServiceProviderFactory.CreateServiceProvider(services,
            new ExtendedServiceProviderOptions
            {
                DetectIncorrectUsageOfTransientDisposables = true,
                ThrowOnOpenGenericTransientDisposable = true
            }));
        CollectionAssert.AreEqual(original, services.ToArray());
        services.Remove(original[0]);
        using var provider = Build(services, "validated-diagnostics");
        Assert.HasCount(0, provider.GetRequiredService<ServiceTypes>());
        AssertOneMetadataSet(provider);
    }

    private static ServiceProvider Build(IServiceCollection services, string mode)
    {
        var options = mode == "default" ? null : new ExtendedServiceProviderOptions
        {
            DetectIncorrectUsageOfTransientDisposables = mode.Contains("diagnostics"),
            ValidateOnBuild = mode.Contains("validated"),
            ValidateScopes = true
        };
        var factory = options == null ? new ServiceProviderFactory() : new ServiceProviderFactory(options);
        Assert.AreSame(services, factory.CreateBuilder(services));
        return (ServiceProvider)((IServiceProviderFactory<IServiceCollection>)factory).CreateServiceProvider(services);
    }

    private static void AssertOneMetadataSet(ServiceProvider provider)
    {
        Assert.HasCount(1, provider.GetServices<ServiceProviderRegistrationSnapshot>().ToArray());
        Assert.HasCount(1, provider.GetServices<ServiceTypes>().ToArray());
        Assert.HasCount(1, provider.GetServices<ServiceKeys>().ToArray());
        Assert.HasCount(1, provider.GetServices<ServiceLifetimes>().ToArray());
    }

    public sealed class Marker;
    public sealed class NeedsMissing(Marker marker) { public Marker Marker { get; } = marker; }
    public interface IRepository<T>;
    public class Repository<T> : IRepository<T>;
    public sealed class ClosedRepository : Repository<string>;
    public class Owned : IDisposable
    {
        public int Disposals { get; private set; }
        public void Dispose() => Disposals++;
    }
    public sealed class TransientOwned : Owned;
    public sealed class DisposableGeneric<T> : Owned;
    public sealed class DisposableRepository : Owned, IRepository<string>;
    public sealed class RepositoryDecorator(IRepository<string> inner) : Owned, IRepository<string>
    {
        public IRepository<string> Inner { get; } = inner;
    }
    public sealed class UserKeys<T>() : ServiceKeys<T>(["user-generic"]);
}
