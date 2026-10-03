using System.Collections;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class ExplicitEnumerableRegressionTests
{
    [TestMethod]
    [DataRow(false, false, false, false)]
    [DataRow(false, false, false, true)]
    [DataRow(false, false, true, false)]
    [DataRow(false, false, true, true)]
    [DataRow(false, true, false, false)]
    [DataRow(false, true, false, true)]
    [DataRow(false, true, true, false)]
    [DataRow(false, true, true, true)]
    [DataRow(true, false, false, false)]
    [DataRow(true, false, false, true)]
    [DataRow(true, false, true, false)]
    [DataRow(true, false, true, true)]
    [DataRow(true, true, false, false)]
    [DataRow(true, true, false, true)]
    [DataRow(true, true, true, false)]
    [DataRow(true, true, true, true)]
    public void LastExplicitEnumerableKeepsNativePrecedenceAndMultiplicity(bool typeOverload, bool factory, bool ordinaryElement, bool keyedMixture)
    {
        var first = new Foo("first");
        var second = new Foo("second");
        var supplied = new IFoo[] { first, first, second };
        var keyed = new Foo("blue");
        var enumerableCalls = 0;
        var ordinaryCalls = 0;
        var wildcardCalls = 0;
        var services = new ServiceCollection();
        if (ordinaryElement)
            services.AddSingleton<IFoo>(_ => { ordinaryCalls++; return new Foo("ordinary"); });
        services.AddSingleton<IEnumerable<IFoo>>(new[] { new Foo("superseded") });
        if (factory)
            services.AddSingleton<IEnumerable<IFoo>>(_ => { enumerableCalls++; return supplied; });
        else
            services.AddSingleton<IEnumerable<IFoo>>(supplied);
        if (keyedMixture)
        {
            services.AddKeyedSingleton<IFoo>("blue", keyed);
            services.AddKeyedSingleton<IFoo>(KeyedService.AnyKey, (_, key) => { wildcardCalls++; return new Foo(key!.ToString()!); });
        }
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        var native = provider.GetServices<IFoo>().ToArray();
        CollectionAssert.AreEqual(supplied, native, "The last explicit IEnumerable registration must take native precedence.");
        Assert.AreEqual(0, ordinaryCalls);
        var expected = keyedMixture ? native.Concat(new[] { keyed }).ToArray() : native;
        for (var i = 0; i < 3; i++)
        {
            var actual = Enumerate<IFoo>(provider, typeOverload);
            CollectionAssert.AreEquivalent(expected, actual);
            CollectionAssert.AreEqual(native, actual.Take(native.Length).ToArray());
            Assert.AreEqual(0, ordinaryCalls);
            Assert.AreEqual(factory ? 1 : 0, enumerableCalls);
            Assert.AreEqual(0, wildcardCalls);
        }
    }

    [TestMethod]
    [DataRow(false, ServiceLifetime.Transient)]
    [DataRow(false, ServiceLifetime.Scoped)]
    [DataRow(false, ServiceLifetime.Singleton)]
    [DataRow(true, ServiceLifetime.Transient)]
    [DataRow(true, ServiceLifetime.Scoped)]
    [DataRow(true, ServiceLifetime.Singleton)]
    public void ExplicitEnumerableFactoryKeepsItsNativeLifetime(bool typeOverload, ServiceLifetime lifetime)
    {
        var calls = 0;
        IServiceCollection services = new ServiceCollection();
        services.Add(ServiceDescriptor.Describe(typeof(IEnumerable<IFoo>), _ =>
        {
            calls++;
            return new IFoo[] { new Foo("first"), new Foo("second") };
        }, lifetime));
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var native = sp.GetServices<IFoo>().ToArray();
        Assert.HasCount(2, native);
        Assert.AreEqual(1, calls);
        Assert.IsFalse(sp.IsServiceRegistered<IFoo>());
        for (var i = 0; i < 3; i++)
        {
            var before = calls;
            var actual = Enumerate<IFoo>(sp, typeOverload);
            Assert.HasCount(2, actual);
            Assert.AreEqual(lifetime == ServiceLifetime.Transient ? 1 : 0, calls - before);
            CollectionAssert.AreEqual(native.Select(x => x.Name).ToArray(), actual.Select(x => x.Name).ToArray());
            if (lifetime == ServiceLifetime.Transient)
                Assert.AreNotSame(native[0], actual[0]);
            else
                Assert.AreSame(native[0], actual[0]);
        }
        Assert.IsFalse(sp.IsServiceRegistered<IFoo>(), "Enumerable availability does not imply an element registration.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PublicMetadataMutationCannotSuppressTheExplicitEnumerableOrInventKeys(bool typeOverload)
    {
        var supplied = new Foo("explicit");
        var keyed = new Foo("blue");
        var wildcardCalls = 0;
        var services = new ServiceCollection();
        services.AddSingleton<IEnumerable<IFoo>>(_ => new[] { supplied });
        services.AddKeyedSingleton<IFoo>("blue", keyed);
        services.AddKeyedSingleton<IFoo>(KeyedService.AnyKey, (_, key) => { wildcardCalls++; return new Foo(key!.ToString()!); });
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        var native = provider.GetServices<IFoo>().ToArray();
        Assert.AreSame(supplied, native.Single());
        provider.GetRequiredService<ServiceTypes>().Clear();
        provider.GetRequiredService<ServiceTypes>().Add(typeof(IFoo));
        provider.GetRequiredService<ServiceKeys>().Clear();
        provider.GetRequiredService<ServiceKeys<IFoo>>().Clear();
        provider.GetRequiredService<ServiceKeys<IFoo>>().Add("invented");
        provider.GetRequiredService<ServiceKeys<IFoo>>().Add(KeyedService.AnyKey);
        for (var i = 0; i < 3; i++)
            CollectionAssert.AreEquivalent(new IFoo[] { supplied, keyed }, Enumerate<IFoo>(provider, typeOverload));
        Assert.AreEqual(0, wildcardCalls);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void AnExplicitEmptyEnumerableOverridesOrdinaryElementsButRetainsKeyedElements(bool typeOverload, bool factory)
    {
        var services = new ServiceCollection();
        var ordinaryCalls = 0;
        services.AddSingleton<IFoo>(_ => { ordinaryCalls++; return new Foo("ordinary"); });
        if (factory)
            services.AddSingleton<IEnumerable<IFoo>>(_ => Array.Empty<IFoo>());
        else
            services.AddSingleton<IEnumerable<IFoo>>(Array.Empty<IFoo>());
        var keyed = new Foo("blue");
        services.AddKeyedSingleton<IFoo>("blue", keyed);
        services.AddKeyedSingleton<IFoo>(KeyedService.AnyKey, new Foo("fallback"));
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        Assert.HasCount(0, provider.GetServices<IFoo>().ToArray());
        Assert.AreSame(keyed, Enumerate<IFoo>(provider, typeOverload).Single());
        Assert.AreEqual(0, ordinaryCalls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AKeyedEnumerableDoesNotReplaceTheUnkeyedEnumerable(bool typeOverload)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IEnumerable<IFoo>>("enumerable", new[] { new Foo("keyed-enumerable") });
        var keyed = new Foo("blue");
        services.AddKeyedSingleton<IFoo>("blue", keyed);
        services.AddKeyedSingleton<IFoo>(KeyedService.AnyKey, new Foo("fallback"));
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        Assert.HasCount(0, provider.GetServices<IFoo>().ToArray());
        Assert.AreSame(keyed, Enumerate<IFoo>(provider, typeOverload).Single());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExplicitEnumerablesOfEnumerableElementsUseNativeResolution(bool typeOverload)
    {
        IEnumerable<IFoo> inner = new[] { new Foo("inner") };
        var services = new ServiceCollection();
        services.AddSingleton<IEnumerable<IEnumerable<IFoo>>>(new[] { inner });
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        Assert.AreSame(inner, provider.GetServices<IEnumerable<IFoo>>().Single());
        Assert.AreSame(inner, Enumerate<IEnumerable<IFoo>>(provider, typeOverload).Single());
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void EvenAnExplicitEnumerableCannotExposeAPrivateDecoratorIdentity(bool typeOverload, bool diagnostics)
    {
        var services = new ServiceCollection();
        var innerCalls = 0;
        services.AddScoped<IFoo>(_ => { innerCalls++; return new Foo("inner"); });
        services.Decorate<IFoo, FooDecorator>();
        services.Decorate<IFoo, FooDecorator>();
        var privateType = services.First(d => d.ServiceType != typeof(IFoo)).ServiceType;
        var markerArray = Array.CreateInstance(privateType, 1);
        markerArray.SetValue(Activator.CreateInstance(privateType, nonPublic: true), 0);
        services.AddSingleton(typeof(IEnumerable<>).MakeGenericType(privateType), markerArray);
        using var provider = ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            DetectIncorrectUsageOfTransientDisposables = diagnostics
        });
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        Assert.HasCount(1, sp.GetServices(privateType).ToArray(), "The native private marker control is explicitly registered.");
        provider.GetRequiredService<ServiceTypes>().Add(privateType);
        provider.GetRequiredService<ServiceKeys<IFoo>>().Add(services.First(d => d.ServiceType == privateType).ServiceKey!);
        var hidden = typeOverload ? sp.GetAllServices(privateType).ToArray() : InvokeGenericEnumeration(sp, privateType);
        Assert.HasCount(0, hidden);
        Assert.AreEqual(0, innerCalls);
        var outer = Enumerate<IFoo>(sp, typeOverload).Single();
        Assert.IsInstanceOfType<FooDecorator>(outer);
        Assert.AreEqual("inner", outer.Name);
        Assert.AreEqual(1, innerCalls);
    }

    private static T[] Enumerate<T>(IServiceProvider provider, bool typeOverload) => typeOverload
        ? provider.GetAllServices(typeof(T)).Cast<T>().ToArray()
        : provider.GetAllServices<T>().ToArray();

    private static object[] InvokeGenericEnumeration(IServiceProvider provider, Type serviceType)
    {
        var method = typeof(ServiceProviderExtensions).GetMethods()
            .Single(m => m.Name == nameof(ServiceProviderExtensions.GetAllServices) && m.IsGenericMethodDefinition);
        return ((IEnumerable)method.MakeGenericMethod(serviceType).Invoke(null, new object[] { provider })!).Cast<object>().ToArray();
    }

    public interface IFoo { string Name { get; } }
    public class Foo(string name) : IFoo
    {
        public string Name { get; } = name;
    }
    public sealed class FooDecorator(IFoo inner) : IFoo
    {
        public string Name => inner.Name;
    }
}
