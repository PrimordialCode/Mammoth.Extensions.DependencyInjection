using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class OptionalDateTimeDefaultRegressionTests
{
    public static IEnumerable<object[]> ActivationCases()
    {
        foreach (var lifetime in new[] { ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient })
        foreach (var keyed in new[] { false, true })
        foreach (var diagnostics in new[] { false, true })
        foreach (var source in new[] { "default", "value", "service", "key-map", "key-attribute" })
            yield return [lifetime, keyed, diagnostics, source];
    }

    public static IEnumerable<object[]> DecorationCases()
    {
        foreach (var lifetime in new[] { ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient })
        foreach (var diagnostics in new[] { false, true })
        foreach (var contextual in new[] { false, true })
        foreach (var part in new[] { "inner", "decorator", "both" })
            yield return [lifetime, diagnostics, contextual, part];
    }

    [TestMethod]
    [DynamicData(nameof(ActivationCases))]
    public void MappedDateTimeDefaultsPreserveOverrides(ServiceLifetime lifetime, bool keyed, bool diagnostics, string source)
    {
        IServiceCollection services = new ServiceCollection();
        var expected = source == "default" ? default : new DateTime(2025, 1, 1);
        Dependency[] map = [Dependency.OnValue("unused", 1)];
        if (source == "value")
        {
            services.AddSingleton(typeof(DateTime), new DateTime(2024, 1, 1));
            map = [Dependency.OnValue("date", expected)];
        }
        else if (source == "service") services.AddSingleton(typeof(DateTime), expected);
        else if (source == "key-map" || source == "key-attribute")
        {
            services.AddKeyedSingleton(typeof(DateTime), "date", expected);
            if (source == "key-map") map = [Parameter.ForKey("date").Eq("date")];
        }
        if (source == "key-attribute") AddMapped<KeyedDated>(services, lifetime, keyed, map);
        else AddMapped<Dated>(services, lifetime, keyed, map);
        using var provider = Build(services, diagnostics);
        using var scope = provider.CreateScope();
        var result = source == "key-attribute" ? (IWork)Resolve<KeyedDated>(scope.ServiceProvider, keyed)
            : Resolve<Dated>(scope.ServiceProvider, keyed);

        // A native registration supplies the independent baseline for the missing default.
        using var native = NativeProvider();
        if (source == "default") Assert.AreEqual(native.GetRequiredService<Dated>().Date, result.Date);
        Assert.AreEqual(expected, result.Date);
    }

    [TestMethod]
    [DynamicData(nameof(DecorationCases))]
    public void KeyedDecorationNormalizesDateTimeDefaults(ServiceLifetime lifetime, bool diagnostics, bool contextual, string part)
    {
        IServiceCollection services = new ServiceCollection();
        var implementation = part == "decorator" ? typeof(Plain)
            : contextual ? typeof(ContextualDated) : typeof(Dated);
        services.Add(new ServiceDescriptor(typeof(IWork), "blue", implementation, lifetime));
        if (part == "inner") services.Decorate<IWork, PlainWrapper>();
        else if (contextual) services.Decorate<IWork, ContextualWrapper>();
        else services.Decorate<IWork, DatedWrapper>();
        using var provider = Build(services, diagnostics);
        using var scope = provider.CreateScope();
        using var native = NativeProvider();
        var expected = native.GetRequiredService<Dated>().Date;
        var result = Resolve<IWork>(scope.ServiceProvider, true);

        Assert.AreEqual(expected, result.Date);
        if (result is DatedWrapper wrapper && part == "both") Assert.AreEqual(expected, wrapper.Inner.Date);
        if (result is ContextualWrapper contextualWrapper)
        {
            Assert.AreEqual("blue", contextualWrapper.Key);
            if (part == "both") Assert.AreEqual(expected, contextualWrapper.Inner.Date);
        }
        if (contextual && part != "decorator")
        {
            var inner = result is PlainWrapper plain ? plain.Inner : ((ContextualWrapper)result).Inner;
            Assert.AreEqual("blue", ((ContextualDated)inner).Key);
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void UnusedDateTimeConstructorsDoNotBreakSelection(bool keyed, bool diagnostics)
    {
        var services = new ServiceCollection();
        Dependency[] map = [Dependency.OnValue("label", "selected")];
        AddMapped<Preferred>(services, ServiceLifetime.Transient, keyed, map);
        AddMapped<Rejected>(services, ServiceLifetime.Transient, keyed, map);
        using var provider = Build(services, diagnostics);
        using var scope = provider.CreateScope();

        // Cached metadata includes both an ignored constructor and an unsatisfiable one.
        Assert.AreEqual("selected", Resolve<Preferred>(scope.ServiceProvider, keyed).Label);
        Assert.AreEqual("selected", Resolve<Rejected>(scope.ServiceProvider, keyed).Label);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void RequiredDateTimeIsNotTreatedAsOptional(bool keyed, bool diagnostics)
    {
        var services = new ServiceCollection();
        AddMapped<RequiredDate>(services, ServiceLifetime.Transient, keyed, [Dependency.OnValue("unused", 1)]);
        using var provider = Build(services, diagnostics);
        using var scope = provider.CreateScope();
        Assert.ThrowsExactly<InvalidOperationException>(() => Resolve<RequiredDate>(scope.ServiceProvider, keyed));
    }

    [TestMethod]
    public void ValueTypeDefaultsStayZeroInitializedAndNullableDefaultsStayNull()
    {
        var services = new ServiceCollection();
        services.AddTransient<StructDefaults>();
        services.AddKeyedTransient<StructDefaults>("blue", [Dependency.OnValue("unused", 1)]);
        using var provider = services.BuildServiceProvider();
        var native = provider.GetRequiredService<StructDefaults>();
        var mapped = Resolve<StructDefaults>(provider, true);

        // Optional defaults are zeroed data: the struct's user constructor must never run.
        Assert.AreEqual(0, native.Value.Count);
        Assert.AreEqual(native.Value.Count, mapped.Value.Count);
        Assert.IsNull(native.Date);
        Assert.IsNull(mapped.Date);
    }

    private static ServiceProvider NativeProvider()
    {
        var services = new ServiceCollection();
        services.AddTransient<Dated>();
        return services.BuildServiceProvider();
    }

    private static ServiceProvider Build(IServiceCollection services, bool diagnostics) => diagnostics
        ? ServiceProviderFactory.CreateServiceProvider(services,
            new ExtendedServiceProviderOptions { ValidateOnBuild = true, DetectIncorrectUsageOfTransientDisposables = true })
        : services.BuildServiceProvider();

    private static T Resolve<T>(IServiceProvider provider, bool keyed) where T : class =>
        provider.GetRequiredKeyedService<T>(keyed ? "blue" : null);

    private static void AddMapped<T>(IServiceCollection services, ServiceLifetime lifetime, bool keyed, Dependency[] map)
        where T : class
    {
        if (keyed)
        {
            if (lifetime == ServiceLifetime.Singleton) services.AddKeyedSingleton<T>("blue", map);
            else if (lifetime == ServiceLifetime.Scoped) services.AddKeyedScoped<T>("blue", map);
            else services.AddKeyedTransient<T>("blue", map);
        }
        else
        {
            if (lifetime == ServiceLifetime.Singleton) services.AddSingleton<T>(map);
            else if (lifetime == ServiceLifetime.Scoped) services.AddScoped<T>(map);
            else services.AddTransient<T>(map);
        }
    }

    public interface IWork { DateTime Date { get; } }
    public sealed class Dated(DateTime date = default) : IWork { public DateTime Date { get; } = date; }
    public sealed class KeyedDated([FromKeyedServices("date")] DateTime date = default) : IWork
    { public DateTime Date { get; } = date; }
    public sealed class ContextualDated([ServiceKey] string key, DateTime date = default) : IWork
    { public string Key { get; } = key; public DateTime Date { get; } = date; }
    public sealed class Plain : IWork { public DateTime Date => default; }
    public sealed class PlainWrapper(IWork inner) : IWork
    { public IWork Inner { get; } = inner; public DateTime Date => Inner.Date; }
    public sealed class DatedWrapper(IWork inner, DateTime date = default) : IWork
    { public IWork Inner { get; } = inner; public DateTime Date { get; } = date; }
    public sealed class ContextualWrapper(IWork inner, [ServiceKey] string key, DateTime date = default) : IWork
    { public IWork Inner { get; } = inner; public string Key { get; } = key; public DateTime Date { get; } = date; }
    public sealed class Missing;
    public sealed class Preferred
    {
        public string Label { get; }
        [ActivatorUtilitiesConstructor]
        public Preferred(string label) => Label = label;
        public Preferred(DateTime date = default) => throw new AssertFailedException("Unused constructor ran.");
    }
    public sealed class Rejected
    {
        public string Label { get; }
        public Rejected(string label) => Label = label;
        public Rejected(Missing missing, DateTime date = default) => throw new AssertFailedException("Rejected constructor ran.");
    }
    public sealed class RequiredDate(DateTime date) { public DateTime Date { get; } = date; }
    public struct ZeroValue
    {
        public int Count { get; }
        public ZeroValue() => Count = 99;
    }
    public sealed class StructDefaults(ZeroValue value = default, DateTime? date = null)
    { public ZeroValue Value { get; } = value; public DateTime? Date { get; } = date; }
}
