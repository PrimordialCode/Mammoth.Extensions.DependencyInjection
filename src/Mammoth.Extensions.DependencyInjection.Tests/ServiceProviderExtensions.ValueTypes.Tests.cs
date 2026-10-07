using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

// refs::#85
[TestClass]
public class ValueTypeEnumerationRegressionTests
{
    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, false, true)]
    [DataRow(true, true, true)]
    public void IntInstancesPreserveNativeGroups(bool unkeyed, bool keyed, bool diagnostics)
    {
        AssertInstanceGroups(42, 24, unkeyed, keyed, diagnostics);
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, false, true)]
    [DataRow(true, true, true)]
    public void StructInstancesPreserveNativeGroups(bool unkeyed, bool keyed, bool diagnostics)
    {
        AssertInstanceGroups(new ValueService(42), new ValueService(24), unkeyed, keyed, diagnostics);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReferenceInstancesKeepOrderingAndMultiplicity(bool diagnostics)
    {
        AssertInstanceGroups("unkeyed", "keyed", unkeyed: true, keyed: true, diagnostics: diagnostics);
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, false, true)]
    [DataRow(true, true, true)]
    public void ExplicitIntEnumerableKeepsPrecedenceAndDuplicates(bool factory, bool empty, bool diagnostics)
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(int), 99);
        services.AddSingleton<IEnumerable<int>>(new[] { -1 });
        var supplied = empty ? Array.Empty<int>() : new[] { 42, 42, 43 };
        if (factory)
            services.AddSingleton<IEnumerable<int>>(_ => supplied);
        else
            services.AddSingleton<IEnumerable<int>>(supplied);
        services.AddKeyedSingleton(typeof(int), "blue", 24);
        services.AddKeyedSingleton(typeof(int), "blue", 25);
        using var provider = Build(services, diagnostics);

        CollectionAssert.AreEqual(supplied, provider.GetServices<int>().ToArray());
        CollectionAssert.AreEqual(new[] { 24, 25 }, provider.GetKeyedServices<int>("blue").ToArray());
        var expected = supplied.Concat(new[] { 24, 25 }).ToArray();
        CollectionAssert.AreEqual(expected, provider.GetAllServices<int>().ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExplicitKeyedIntEnumerableIsResolvedForDiscoveredKey(bool diagnostics)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton(typeof(int), "blue", 99);
        services.AddKeyedSingleton<IEnumerable<int>>("blue", new[] { 24, 24, 25 });
        using var provider = Build(services, diagnostics);

        CollectionAssert.AreEqual(new[] { 24, 24, 25 }, provider.GetKeyedServices<int>("blue").ToArray());
        CollectionAssert.AreEqual(new[] { 24, 24, 25 }, provider.GetAllServices<int>().ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SnapshotKeysSurvivePublicMutationAndExcludeAnyKey(bool diagnostics)
    {
        var wildcardCalls = 0;
        var services = new ServiceCollection();
        services.AddSingleton(typeof(int), 42);
        services.AddKeyedSingleton(typeof(int), "blue", 24);
        services.AddKeyedSingleton(typeof(int), "blue", 24);
        services.AddKeyedSingleton(typeof(int), KeyedService.AnyKey, (_, _) =>
        {
            wildcardCalls++;
            return 99;
        });
        using var provider = Build(services, diagnostics);
        provider.GetRequiredService<ServiceTypes>().Clear();
        provider.GetRequiredService<ServiceKeys>().Clear();
        var keys = provider.GetRequiredService<ServiceKeys<int>>();
        keys.Clear();
        keys.Add("invented");
        keys.Add(KeyedService.AnyKey);

        for (var i = 0; i < 3; i++)
            CollectionAssert.AreEqual(new[] { 42, 24, 24 }, provider.GetAllServices<int>().ToArray());
        Assert.AreEqual(0, wildcardCalls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NativeProviderKeepsLegacyExplicitKeyMetadata(bool keyed)
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(int), 42);
        if (keyed)
        {
            services.AddKeyedSingleton(typeof(int), "blue", 24);
            services.AddSingleton(new ServiceKeys<int>(new object[] { "blue", KeyedService.AnyKey }));
        }
        using var provider = services.BuildServiceProvider();

        CollectionAssert.AreEqual(keyed ? new[] { 42, 24 } : new[] { 42 }, provider.GetAllServices<int>().ToArray());
    }

    private static void AssertInstanceGroups<T>(T unkeyedValue, T keyedValue, bool unkeyed, bool keyed, bool diagnostics)
        where T : notnull
    {
        var services = new ServiceCollection();
        if (unkeyed)
        {
            services.AddSingleton(typeof(T), unkeyedValue);
            services.AddSingleton(typeof(T), unkeyedValue);
        }
        if (keyed)
        {
            services.AddKeyedSingleton(typeof(T), "blue", keyedValue);
            services.AddKeyedSingleton(typeof(T), "blue", keyedValue);
            services.AddKeyedSingleton(typeof(T), "green", keyedValue);
        }
        using var provider = Build(services, diagnostics);
        var nativeUnkeyed = provider.GetServices<T>().ToArray();
        var nativeBlue = provider.GetKeyedServices<T>("blue").ToArray();
        var nativeGreen = provider.GetKeyedServices<T>("green").ToArray();
        CollectionAssert.AreEqual(unkeyed ? new[] { unkeyedValue, unkeyedValue } : Array.Empty<T>(), nativeUnkeyed);
        CollectionAssert.AreEqual(keyed ? new[] { keyedValue, keyedValue } : Array.Empty<T>(), nativeBlue);
        CollectionAssert.AreEqual(keyed ? new[] { keyedValue } : Array.Empty<T>(), nativeGreen);
        var expected = nativeUnkeyed.Concat(nativeBlue).Concat(nativeGreen).ToArray();

        for (var i = 0; i < 3; i++)
        {
            var actual = provider.GetAllServices<T>().ToArray();
            CollectionAssert.AreEquivalent(expected, actual);
            CollectionAssert.AreEqual(nativeUnkeyed, actual.Take(nativeUnkeyed.Length).ToArray());
        }
    }

    private static ServiceProvider Build(IServiceCollection services, bool diagnostics) =>
        ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            DetectIncorrectUsageOfTransientDisposables = diagnostics,
            ValidateOnBuild = true,
            ValidateScopes = true
        });

    private readonly struct ValueService(int value)
    {
        public int Value { get; } = value;
    }
}
