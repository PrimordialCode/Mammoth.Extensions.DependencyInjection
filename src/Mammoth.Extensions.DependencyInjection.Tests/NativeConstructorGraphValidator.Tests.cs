using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class NativeConstructorGraphValidatorTests
{
    [TestMethod]
    [DataRow(ServiceLifetime.Transient)]
    [DataRow(ServiceLifetime.Scoped)]
    [DataRow(ServiceLifetime.Singleton)]
    public void SharedDiamondAndRepeatedParametersAreAcyclic(ServiceLifetime lifetime)
    {
        IServiceCollection services = new ServiceCollection();
        foreach (var type in new[] { typeof(Diamond), typeof(Left), typeof(Right), typeof(Leaf) })
            services.Add(ServiceDescriptor.Describe(type, type, lifetime));
        using var provider = Build(services);
        using var scope = provider.CreateScope();
        var result = scope.ServiceProvider.GetRequiredService<Diamond>();
        Assert.IsNotNull(result.Left.Leaf);
        Assert.IsNotNull(result.Right.Leaf);
        if (lifetime == ServiceLifetime.Transient) Assert.AreNotSame(result.First, result.Second);
        else Assert.AreSame(result.First, result.Second);
        Assert.AreEqual(0, ResolutionContext.CurrentStack.Count);
    }

    [TestMethod]
    [DataRow(ServiceLifetime.Transient)]
    [DataRow(ServiceLifetime.Scoped)]
    [DataRow(ServiceLifetime.Singleton)]
    public void AnyKeyConstructorPlansKeepRequestedKeysAndScopes(ServiceLifetime lifetime)
    {
        IServiceCollection services = new ServiceCollection();
        services.Add(ServiceDescriptor.DescribeKeyed(typeof(KeyedRoot), KeyedService.AnyKey, typeof(KeyedRoot), lifetime));
        services.Add(ServiceDescriptor.DescribeKeyed(typeof(KeyedLeaf), KeyedService.AnyKey, typeof(KeyedLeaf), lifetime));
        using var provider = Build(services);
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        foreach (var scope in new[] { first, second })
        {
            var red = scope.ServiceProvider.GetRequiredKeyedService<KeyedRoot>("red");
            var blue = scope.ServiceProvider.GetRequiredKeyedService<KeyedRoot>("blue");
            Assert.AreEqual("red", red.First.Key);
            Assert.AreEqual("red", red.Second.Key);
            Assert.AreEqual("blue", blue.First.Key);
            Assert.AreNotSame(red.First, blue.First);
        }
    }

    [TestMethod]
    public void ConstrainedGenericEnumerationSkipsIncompatibleBindings()
    {
        var services = new ServiceCollection();
        services.AddTransient(typeof(IChoice<>), typeof(ClassChoice<>));
        services.AddTransient(typeof(IChoice<>), typeof(StructChoice<>));
        services.AddTransient<Choices>();
        using var provider = Build(services);
        var result = provider.GetRequiredService<Choices>();
        Assert.HasCount(1, result.Values);
        Assert.IsInstanceOfType<StructChoice<int>>(result.Values[0]);
    }

    [TestMethod]
    public void ExplicitEnumerableFactoryHidesAnOtherwiseCyclicImplicitGraph()
    {
        var services = new ServiceCollection();
        services.AddTransient(typeof(IChoice<>), typeof(CyclicChoice<>));
        services.AddTransient<IEnumerable<IChoice<int>>>(_ => []);
        services.AddTransient<Choices>();
        using var provider = Build(services);
        Assert.IsEmpty(provider.GetRequiredService<Choices>().Values);
    }

    [TestMethod]
    public void FactoriesAndInstancesAreOpaqueAndUnrelatedInvalidGraphsStayDeferred()
    {
        var services = new ServiceCollection();
        var instance = new Opaque(null);
        services.AddSingleton(instance);
        services.AddTransient<FactoryRoot>();
        services.AddTransient<FactoryLeaf>(_ => new FactoryLeaf(null));
        services.AddTransient<UnrelatedCycle>();
        using var provider = Build(services);
        Assert.AreSame(instance, provider.GetRequiredService<FactoryRoot>().Value);
    }

    [TestMethod]
    public void UnkeyedBuiltInProviderRemainsAConstructorGraphLeaf()
    {
        var services = new ServiceCollection();
        services.AddTransient<IServiceProvider, PretendProvider>();
        services.AddTransient<ProviderConsumer>();
        using var provider = Build(services);
        Assert.IsFalse(provider.GetRequiredService<ProviderConsumer>().Provider is PretendProvider);
    }

    [TestMethod]
    public void PlansAreReusedWithinOneActivationWithoutRetainingScopes()
    {
        CountingAttribute.Count = 0;
        var services = new ServiceCollection();
        services.AddTransient<CountedRoot>();
        services.AddScoped<CountedChild>();
        using var provider = Build(services);
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var firstValue = first.ServiceProvider.GetRequiredService<CountedRoot>();
        Assert.AreEqual(1, CountingAttribute.Count, "Activation should execute the plan already validated for its child.");
        var secondValue = second.ServiceProvider.GetRequiredService<CountedRoot>();
        Assert.AreNotSame(firstValue.Child, secondValue.Child);
        Assert.AreEqual(2, CountingAttribute.Count);
    }

    [TestMethod]
    public void EqualKeyPlanReusePreservesTheNativeFactorysActualKeyObject()
    {
        var key = new string("red".ToCharArray());
        var services = new ServiceCollection();
        services.AddKeyedTransient<KeyedLeaf>(KeyedService.AnyKey);
        services.AddTransient<ExplicitRedRoot>();
        using var provider = Build(services);
        Assert.AreSame(key, provider.GetRequiredKeyedService<KeyedLeaf>(key).Key);
        Assert.AreSame(key, provider.GetRequiredService<ExplicitRedRoot>().Leaf.Key);
    }

    public sealed class ExplicitRedRoot([FromKeyedServices("red")] KeyedLeaf leaf)
    {
        public KeyedLeaf Leaf { get; } = leaf;
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PreflightDefersKeyTypeChecksUntilTheNativeEffectiveKeyIsKnown(bool diagnostics)
    {
        var first = new KeyA();
        var second = new KeyB();
        var services = new ServiceCollection();
        services.AddKeyedTransient<TypedKeyLeaf>(KeyedService.AnyKey);
        services.AddKeyedTransient<InheritedTypedKeyRoot>(KeyedService.AnyKey);
        using var provider = ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            DetectIncorrectUsageOfTransientDisposables = diagnostics
        });
        Assert.AreSame(first, provider.GetRequiredKeyedService<TypedKeyLeaf>(first).Key);
        Assert.AreSame(first, provider.GetRequiredKeyedService<InheritedTypedKeyRoot>(second).Leaf.Key);
    }

    public abstract class EquivalentKey
    {
        public override bool Equals(object? value) => value is EquivalentKey;
        public override int GetHashCode() => 1;
    }
    public sealed class KeyA : EquivalentKey { }
    public sealed class KeyB : EquivalentKey { }
    public sealed class TypedKeyLeaf([ServiceKey] KeyA key) { public KeyA Key { get; } = key; }
    public sealed class InheritedTypedKeyRoot([FromKeyedServices] TypedKeyLeaf leaf)
    {
        public TypedKeyLeaf Leaf { get; } = leaf;
    }

    [TestMethod]
    public void ReusedGraphPlansValidateKeyTypesOnEveryConsideredConstructor()
    {
        var services = new ServiceCollection();
        services.AddSingleton("value");
        services.AddKeyedTransient<MultipleKeyConstructors>(42);
        services.AddTransient<MultipleKeyConstructorRoot>();
        using var provider = Build(services);
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetRequiredService<MultipleKeyConstructorRoot>());
        StringAssert.Contains(error.Message, "ServiceKey parameter type");
    }

    public sealed class MultipleKeyConstructors
    {
        public MultipleKeyConstructors(string first, string second) { }
        public MultipleKeyConstructors([ServiceKey] string key) { }
    }
    public sealed class MultipleKeyConstructorRoot
    {
        public MultipleKeyConstructorRoot([FromKeyedServices(42)] MultipleKeyConstructors value) { }
    }

    private static ServiceProvider Build(IServiceCollection services) =>
        ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            DetectIncorrectUsageOfTransientDisposables = true,
            ValidateOnBuild = false
        });

    public sealed class Leaf { }
    public sealed class Left(Leaf leaf) { public Leaf Leaf { get; } = leaf; }
    public sealed class Right(Leaf leaf) { public Leaf Leaf { get; } = leaf; }
    public sealed class Diamond(Left left, Right right, Leaf first, Leaf second)
    {
        public Left Left { get; } = left;
        public Right Right { get; } = right;
        public Leaf First { get; } = first;
        public Leaf Second { get; } = second;
    }
    public sealed class KeyedRoot([FromKeyedServices] KeyedLeaf first, [FromKeyedServices] KeyedLeaf second)
    {
        public KeyedLeaf First { get; } = first;
        public KeyedLeaf Second { get; } = second;
    }
    public sealed class KeyedLeaf([ServiceKey] object key) { public object Key { get; } = key; }
    public interface IChoice<T> { }
    public sealed class ClassChoice<T> : IChoice<T> where T : class { }
    public sealed class StructChoice<T> : IChoice<T> where T : struct { }
    public sealed class CyclicChoice<T> : IChoice<T> { public CyclicChoice(IChoice<T> dependency) { } }
    public sealed class Choices(IEnumerable<IChoice<int>> values) { public IChoice<int>[] Values { get; } = values.ToArray(); }
    public sealed class FactoryRoot
    {
        public Opaque Value { get; }
        public FactoryRoot(Opaque value, FactoryLeaf leaf) { Value = value; }
    }
    public sealed class FactoryLeaf { public FactoryLeaf(FactoryLeaf? value) { } }
    public sealed class Opaque { public Opaque(Opaque? value) { } }
    public sealed class UnrelatedCycle { public UnrelatedCycle(UnrelatedCycle value) { } }
    public sealed class PretendProvider(IServiceProvider provider) : IServiceProvider
    {
        public object? GetService(Type serviceType) => provider.GetService(serviceType);
    }
    public sealed class ProviderConsumer(IServiceProvider provider) { public IServiceProvider Provider { get; } = provider; }
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class CountingAttribute : Attribute
    {
        public static int Count;
        public CountingAttribute() => Interlocked.Increment(ref Count);
    }
    public sealed class CountedRoot(CountedChild child) { public CountedChild Child { get; } = child; }
    public sealed class CountedChild { public CountedChild([Counting] int value = 0) { } }
}
