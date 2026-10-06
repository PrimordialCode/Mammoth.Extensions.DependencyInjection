using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class OptionalEnumDefaultRegressionTests
{
    public static IEnumerable<object[]> ActivationCases()
    {
        foreach (var lifetime in new[] { ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient })
        foreach (var keyed in new[] { false, true })
        foreach (var diagnostics in new[] { false, true })
            yield return [lifetime, keyed, diagnostics];
    }

    public static IEnumerable<object[]> DecoratorCases()
    {
        foreach (var lifetime in new[] { ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient })
        foreach (var diagnostics in new[] { false, true })
        foreach (var part in new[] { "inner", "decorator", "both" })
            yield return [lifetime, diagnostics, part];
    }

    [TestMethod]
    [DynamicData(nameof(ActivationCases))]
    public void MappedDefaultsMatchNative(ServiceLifetime lifetime, bool keyed, bool diagnostics)
    {
        IServiceCollection services = new ServiceCollection();
        AddMapped<Work>(services, lifetime, keyed, [Dependency.OnValue("label", "configured")]);
        using var provider = Build(services, diagnostics);
        using var scope = provider.CreateScope();
        var result = Resolve(scope.ServiceProvider, keyed);
        AssertDefaults(result, keyed, "configured");
        AssertLifetime(result, Resolve(scope.ServiceProvider, keyed), provider, lifetime, keyed);
    }

    [TestMethod]
    [DynamicData(nameof(ActivationCases))]
    public void EmptyMapsKeepNativeDefaults(ServiceLifetime lifetime, bool keyed, bool diagnostics)
    {
        IServiceCollection services = new ServiceCollection();
        AddMapped<Work>(services, lifetime, keyed, []);
        using var provider = Build(services, diagnostics);
        using var scope = provider.CreateScope();
        AssertDefaults(Resolve(scope.ServiceProvider, keyed), keyed);
    }

    [TestMethod]
    [DynamicData(nameof(ActivationCases))]
    public void UnmappedDiagnosticDefaultsMatchNative(ServiceLifetime lifetime, bool keyed, bool diagnostics)
    {
        IServiceCollection services = new ServiceCollection();
        services.Add(new ServiceDescriptor(typeof(IValues), keyed ? "blue" : null, typeof(Work), lifetime));
        using var provider = Build(services, diagnostics);
        using var scope = provider.CreateScope();
        AssertDefaults(Resolve(scope.ServiceProvider, keyed), keyed);
    }

    [TestMethod]
    [DynamicData(nameof(DecoratorCases))]
    public void ContextualKeyedDecoratorDefaultsMatchNative(ServiceLifetime lifetime, bool diagnostics, string part)
    {
        IServiceCollection services = new ServiceCollection();
        services.Add(new ServiceDescriptor(typeof(IValues), "blue", part == "decorator" ? typeof(Plain) : typeof(Work), lifetime));
        if (part == "inner") services.Decorate<IValues, PlainWrapper>();
        else services.Decorate<IValues, OptionalWrapper>();
        using var provider = Build(services, diagnostics);
        using var scope = provider.CreateScope();
        var result = Resolve(scope.ServiceProvider, true);
        AssertDefaults(result, true);
        if (result is OptionalWrapper wrapper && part == "both") AssertDefaults(wrapper.Inner, true);
        AssertLifetime(result, Resolve(scope.ServiceProvider, true), provider, lifetime, true);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void RegisteredServicesAndNamedOverridesStillPrecedeDefaults(bool keyed, bool diagnostics)
    {
        foreach (var overrides in new[] { false, true })
        {
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton(typeof(IntMode?), IntMode.Registered);
            services.AddKeyedSingleton(typeof(IntMode?), "choice", IntMode.Registered);
            Dependency[] map = overrides
                ? [Dependency.OnValue("label", "configured"), Dependency.OnValue("ordinary", IntMode.Mapped),
                    Dependency.OnValue("keyed", IntMode.Mapped), Dependency.OnValue("missing", null!)]
                : [Dependency.OnValue("label", "configured")];
            AddMapped<Precedence>(services, ServiceLifetime.Transient, keyed, map);
            using var provider = Build(services, diagnostics);
            using var scope = provider.CreateScope();
            var result = (Precedence)Resolve(scope.ServiceProvider, keyed);
            Assert.AreEqual(overrides ? IntMode.Mapped : IntMode.Registered, result.Values[0]);
            Assert.AreEqual(overrides ? IntMode.Mapped : IntMode.Registered, result.Values[1]);
            Assert.AreEqual(overrides ? null : (object)IntMode.Default, result.Values[2]);
            Assert.AreEqual("configured", result.Label);
        }
    }

    [TestMethod]
    public void BoxedIntegerMapValuesAreNotConvertedToNullableEnums()
    {
        IServiceCollection services = new ServiceCollection();
        AddMapped<Work>(services, ServiceLifetime.Transient, true, [Dependency.OnValue("intMode", (int)IntMode.Default)]);
        using var provider = services.BuildServiceProvider();
        Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(provider, true));
    }

    private static ServiceProvider Build(IServiceCollection services, bool diagnostics) => diagnostics
        ? ServiceProviderFactory.CreateServiceProvider(services,
            new ExtendedServiceProviderOptions { DetectIncorrectUsageOfTransientDisposables = true })
        : services.BuildServiceProvider();

    private static IValues Resolve(IServiceProvider provider, bool keyed) =>
        provider.GetRequiredKeyedService<IValues>(keyed ? "blue" : null);

    private static void AddMapped<T>(IServiceCollection services, ServiceLifetime lifetime, bool keyed, Dependency[] map)
        where T : class, IValues
    {
        if (keyed)
        {
            if (lifetime == ServiceLifetime.Singleton) services.AddKeyedSingleton<IValues, T>("blue", map);
            else if (lifetime == ServiceLifetime.Scoped) services.AddKeyedScoped<IValues, T>("blue", map);
            else services.AddKeyedTransient<IValues, T>("blue", map);
        }
        else
        {
            if (lifetime == ServiceLifetime.Singleton) services.AddSingleton<IValues, T>(map);
            else if (lifetime == ServiceLifetime.Scoped) services.AddScoped<IValues, T>(map);
            else services.AddTransient<IValues, T>(map);
        }
    }

    private static void AssertDefaults(IValues result, bool keyed, string label = "ordinary")
    {
        IServiceCollection nativeServices = new ServiceCollection();
        nativeServices.Add(new ServiceDescriptor(typeof(IValues), keyed ? "blue" : null, typeof(Work), ServiceLifetime.Transient));
        using var native = nativeServices.BuildServiceProvider();
        var expected = Resolve(native, keyed);
        object?[] defaults = [SByteMode.Default, ByteMode.Default, ShortMode.Default, UShortMode.Default,
            IntMode.Default, UIntMode.Default, LongMode.Default, ULongMode.Default, null, IntMode.Default, 7, 11];
        CollectionAssert.AreEqual(defaults, expected.Values);
        CollectionAssert.AreEqual(expected.Values, result.Values);
        Assert.AreEqual(keyed ? "blue" : null, result.Key);
        Assert.AreEqual(label, result.Label);
    }

    private static void AssertLifetime(IValues first, IValues second, ServiceProvider provider, ServiceLifetime lifetime, bool keyed)
    {
        if (lifetime == ServiceLifetime.Transient) Assert.AreNotSame(first, second);
        else Assert.AreSame(first, second);
        using var otherScope = provider.CreateScope();
        var other = Resolve(otherScope.ServiceProvider, keyed);
        if (lifetime == ServiceLifetime.Singleton) Assert.AreSame(first, other);
        else Assert.AreNotSame(first, other);
    }

    public enum SByteMode : sbyte { Default = sbyte.MinValue }
    public enum ByteMode : byte { Default = byte.MaxValue }
    public enum ShortMode : short { Default = short.MinValue }
    public enum UShortMode : ushort { Default = ushort.MaxValue }
    public enum IntMode { Default = -1234567, Registered = 2, Mapped = 3 }
    public enum UIntMode : uint { Default = uint.MaxValue }
    public enum LongMode : long { Default = long.MinValue }
    public enum ULongMode : ulong { Default = ulong.MaxValue }

    public interface IValues
    {
        object?[] Values { get; }
        object? Key { get; }
        string Label { get; }
    }

    public sealed class Work([ServiceKey] object? key = null, string label = "ordinary",
        SByteMode? sbyteMode = SByteMode.Default, ByteMode? byteMode = ByteMode.Default,
        ShortMode? shortMode = ShortMode.Default, UShortMode? ushortMode = UShortMode.Default,
        IntMode? intMode = IntMode.Default, UIntMode? uintMode = UIntMode.Default,
        LongMode? longMode = LongMode.Default, ULongMode? ulongMode = ULongMode.Default,
        IntMode? nullMode = null, IntMode plainMode = IntMode.Default, int count = 7, int? nullableCount = 11) : IValues
    {
        public object?[] Values { get; } = [sbyteMode, byteMode, shortMode, ushortMode, intMode, uintMode,
            longMode, ulongMode, nullMode, plainMode, count, nullableCount];
        public object? Key { get; } = key;
        public string Label { get; } = label;
    }

    public sealed class OptionalWrapper(IValues inner, [ServiceKey] object? key = null, string label = "ordinary",
        SByteMode? sbyteMode = SByteMode.Default, ByteMode? byteMode = ByteMode.Default,
        ShortMode? shortMode = ShortMode.Default, UShortMode? ushortMode = UShortMode.Default,
        IntMode? intMode = IntMode.Default, UIntMode? uintMode = UIntMode.Default,
        LongMode? longMode = LongMode.Default, ULongMode? ulongMode = ULongMode.Default,
        IntMode? nullMode = null, IntMode plainMode = IntMode.Default, int count = 7, int? nullableCount = 11) : IValues
    {
        public IValues Inner { get; } = inner;
        public object?[] Values { get; } = [sbyteMode, byteMode, shortMode, ushortMode, intMode, uintMode,
            longMode, ulongMode, nullMode, plainMode, count, nullableCount];
        public object? Key { get; } = key;
        public string Label { get; } = label;
    }

    public sealed class Plain : IValues
    {
        public object?[] Values => [];
        public object? Key => null;
        public string Label => "plain";
    }

    public sealed class PlainWrapper(IValues inner, [ServiceKey] object key) : IValues
    {
        public object?[] Values => inner.Values;
        public object? Key { get; } = key;
        public string Label => inner.Label;
    }

    public sealed class Precedence(string label, IntMode? ordinary = IntMode.Default,
        [FromKeyedServices("choice")] IntMode? keyed = IntMode.Default,
        [FromKeyedServices("missing")] IntMode? missing = IntMode.Default) : IValues
    {
        public object?[] Values { get; } = [ordinary, keyed, missing];
        public object? Key => null;
        public string Label { get; } = label;
    }
}
