using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class CollectionKeyEqualityRegressionTests
{
    public static IEnumerable<object[]> EqualKeyCases()
    {
        foreach (var lifetime in new[] { ServiceLifetime.Transient, ServiceLifetime.Scoped, ServiceLifetime.Singleton })
        foreach (var kind in new[] { "int", "guid", "string", "custom", "enum", "struct" })
            yield return new object[] { lifetime, kind };
    }

    [TestMethod]
    [DynamicData(nameof(EqualKeyCases))]
    public void KeyOnlyQueryAgreesWithNativeAndTypedHelpersForEqualKeys(ServiceLifetime lifetime, string kind)
    {
        var (registered, query, missing) = Keys(kind);
        Assert.AreNotSame(registered, query);
        IServiceCollection services = new ServiceCollection();
        services.Add(ServiceDescriptor.DescribeKeyed(typeof(Service), registered, typeof(Service), lifetime));
        Assert.IsTrue(services.IsKeyedServiceRegistered(registered), "The exact registered key is a control.");
        Assert.IsFalse(services.IsKeyedServiceRegistered(missing));
        Assert.AreEqual(lifetime == ServiceLifetime.Transient, services.IsKeyedTransientServiceRegistered<Service>(query));
        Assert.AreEqual(lifetime == ServiceLifetime.Scoped, services.IsKeyedScopedServiceRegistered<Service>(query));
        Assert.AreEqual(lifetime == ServiceLifetime.Singleton, services.IsKeyedSingletonServiceRegistered<Service>(query));
        using (var native = services.BuildServiceProvider())
        using (var scope = native.CreateScope())
        {
            Assert.IsInstanceOfType<Service>(scope.ServiceProvider.GetRequiredKeyedService<Service>(query));
            var probe = native.GetRequiredService<IServiceProviderIsKeyedService>();
            Assert.IsTrue(probe.IsKeyedService(typeof(Service), query));
            Assert.IsFalse(probe.IsKeyedService(typeof(Service), missing));
        }
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        Assert.IsTrue(provider.IsKeyedServiceRegistered(query));
        Assert.IsFalse(provider.IsKeyedServiceRegistered(missing));
        Assert.IsTrue(services.IsKeyedServiceRegistered(query), "The collection key-only query must use normal key equality.");
    }

    [TestMethod]
    public void ReferenceOnlyKeysRequireTheSameObject()
    {
        var registered = new ReferenceKey();
        var query = new ReferenceKey();
        var services = new ServiceCollection();
        services.AddKeyedSingleton<Service>(registered);
        Assert.IsTrue(services.IsKeyedServiceRegistered(registered));
        Assert.IsFalse(services.IsKeyedServiceRegistered(query));
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        Assert.IsTrue(provider.IsKeyedServiceRegistered(registered));
        Assert.IsFalse(provider.IsKeyedServiceRegistered(query));
        Assert.IsFalse(provider.GetRequiredService<IServiceProviderIsKeyedService>().IsKeyedService(typeof(Service), query));
    }

    [TestMethod]
    public void AnyKeyRegistrationDoesNotClaimArbitraryConcreteKeysAreExplicitlyRegistered()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<Service>(KeyedService.AnyKey);
        Assert.IsTrue(services.IsKeyedServiceRegistered(KeyedService.AnyKey));
        Assert.IsFalse(services.IsKeyedServiceRegistered("missing"));
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        Assert.IsTrue(provider.IsKeyedServiceRegistered(KeyedService.AnyKey));
        Assert.IsFalse(provider.IsKeyedServiceRegistered("missing"));
        Assert.IsInstanceOfType<Service>(provider.GetRequiredKeyedService<Service>("missing"));
    }

    [TestMethod]
    public void NullKeyRetainsArgumentValidation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Service>();
        var error = Assert.ThrowsExactly<ArgumentNullException>(() => services.IsKeyedServiceRegistered(null!));
        Assert.AreEqual("serviceKey", error.ParamName);
        Assert.IsFalse(services.IsKeyedServiceRegistered("missing"));
    }

    [TestMethod]
    public void DifferentKeyTypesAndEmptyCollectionsDoNotProduceMatches()
    {
        var services = new ServiceCollection();
        Assert.IsFalse(services.IsKeyedServiceRegistered(42));
        object text = new string("42".ToCharArray());
        object number = 42L;
        services.AddKeyedSingleton<Service>(text);
        services.AddKeyedSingleton<Service>(number);
        services.AddSingleton<Service>();
        Assert.IsTrue(services.IsKeyedServiceRegistered(text));
        Assert.IsTrue(services.IsKeyedServiceRegistered(number));
        Assert.IsFalse(services.IsKeyedServiceRegistered(42));
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        Assert.IsFalse(provider.IsKeyedServiceRegistered(42));
        Assert.IsFalse(provider.GetRequiredService<IServiceProviderIsKeyedService>().IsKeyedService(typeof(Service), 42));
    }

    private static (object Registered, object Query, object Missing) Keys(string kind) => kind switch
    {
        "int" => (42, 42, 43),
        "guid" => (Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), Guid.Empty),
        "string" => (new string("blue".ToCharArray()), new string("blue".ToCharArray()), new string("red".ToCharArray())),
        "custom" => (new EqualKey(42), new EqualKey(42), new EqualKey(43)),
        "enum" => (DayOfWeek.Friday, DayOfWeek.Friday, DayOfWeek.Saturday),
        "struct" => (new StructKey(42), new StructKey(42), new StructKey(43)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    public sealed class Service { }
    public sealed class ReferenceKey { }
    public sealed class EqualKey(int value)
    {
        private int Value => value;
        public override bool Equals(object? obj) => obj is EqualKey other && value == other.Value;
        // Equal and unequal keys collide, so hash equality alone is not sufficient.
        public override int GetHashCode() => 0;
    }
    public readonly struct StructKey(int value) : IEquatable<StructKey>
    {
        private int Value => value;
        public bool Equals(StructKey other) => value == other.Value;
        public override bool Equals(object? obj) => obj is StructKey other && Equals(other);
        public override int GetHashCode() => value;
    }
}
