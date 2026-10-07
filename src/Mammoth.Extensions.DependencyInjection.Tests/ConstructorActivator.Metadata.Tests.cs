using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class ConstructorActivatorMetadataTests
{
    [TestMethod]
    public void CachedMetadataKeepsConstructorAvailabilityProviderSpecific()
    {
        var shortServices = new ServiceCollection();
        shortServices.AddTransient<Candidate>([Dependency.OnValue("label", "short")]);
        var longServices = new ServiceCollection();
        longServices.AddSingleton<Part>();
        longServices.AddTransient<Candidate>([Dependency.OnValue("label", "long")]);
        using var shortProvider = shortServices.BuildServiceProvider();
        using var longProvider = longServices.BuildServiceProvider();

        for (var i = 0; i < 3; i++)
        {
            Assert.AreEqual("short:short", shortProvider.GetRequiredService<Candidate>().Selected);
            Assert.AreEqual("long:long", longProvider.GetRequiredService<Candidate>().Selected);
        }
    }

    [TestMethod]
    public void CachedMetadataKeepsMapsLiveAndFirstNamedOverrideWins()
    {
        Dependency[] map = [Dependency.OnValue("label", "first"), Dependency.OnValue("label", "second")];
        var services = new ServiceCollection();
        services.AddKeyedSingleton("blue", new Part());
        services.AddTransient<Mapped>(map);
        services.AddKeyedTransient<Mapped>("other", [Dependency.OnValue("label", "other")]);
        using var provider = services.BuildServiceProvider();

        Assert.AreEqual("first", provider.GetRequiredService<Mapped>().Label);
        Assert.AreEqual("other", provider.GetRequiredKeyedService<Mapped>("other").Label);
        map[0].ParameterName = "unused";
        Assert.AreEqual("second", provider.GetRequiredService<Mapped>().Label);
        map[0] = Parameter.ForKey("part").Eq("blue");
        Assert.AreSame(provider.GetRequiredKeyedService<Part>("blue"), provider.GetRequiredService<Mapped>().Part);
        map[0].T = Dependency.DependencyType.Value;
        var replacement = new Part();
        map[0].Value = replacement;
        Assert.AreSame(replacement, provider.GetRequiredService<Mapped>().Part);
        map[1].Value = "changed";
        Assert.AreEqual("changed", provider.GetRequiredService<Mapped>().Label);
    }

    [TestMethod]
    public void CachedOptionalDefaultDoesNotReplaceARegisteredValue()
    {
        var defaultServices = new ServiceCollection();
        defaultServices.AddTransient<Optional>([Dependency.OnValue("label", "default")]);
        var registeredServices = new ServiceCollection();
        registeredServices.AddSingleton(typeof(int), 42);
        registeredServices.AddTransient<Optional>([Dependency.OnValue("label", "registered")]);
        using var defaults = defaultServices.BuildServiceProvider();
        using var registered = registeredServices.BuildServiceProvider();

        Assert.AreEqual(7, defaults.GetRequiredService<Optional>().Count);
        Assert.AreEqual(42, registered.GetRequiredService<Optional>().Count);
        Assert.AreEqual(7, defaults.GetRequiredService<Optional>().Count);
    }

    [TestMethod]
    public void ConcurrentFirstUseKeepsRequestedKeysAndMapsSeparate()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        Parallel.For(0, 100, i =>
        {
            var label = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var result = (ConcurrentMapped)ConstructorActivator.CreateDependsOn(provider, typeof(ConcurrentMapped),
                [Dependency.OnValue("label", label)], serviceKey: label);
            Assert.AreEqual(label, result.Key);
            Assert.AreEqual(label, result.Label);
        });
    }

    [TestMethod]
    public void CachedMetadataDoesNotRetainCollectibleTypes()
    {
        var type = ActivateCollectibleType();
        for (var i = 0; i < 10 && type.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert.IsFalse(type.IsAlive, "The constructor metadata cache must not root a collectible type.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ActivateCollectibleType()
    {
        var name = new AssemblyName("DependsOnCollectible_" + Guid.NewGuid().ToString("N"));
#if NETFRAMEWORK
        var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndCollect);
#else
        var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndCollect);
#endif
        var builder = assembly.DefineDynamicModule(name.Name!).DefineType("Mapped", TypeAttributes.Public);
        var constructor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [typeof(string)]);
        constructor.DefineParameter(1, ParameterAttributes.None, "label").SetCustomAttribute(
            new CustomAttributeBuilder(typeof(ServiceKeyAttribute).GetConstructor(Type.EmptyTypes)!, []));
        var il = constructor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
        il.Emit(OpCodes.Ret);
        var type = builder.CreateType()!;
        using var provider = new ServiceCollection().BuildServiceProvider();
        Assert.IsInstanceOfType(ConstructorActivator.CreateDependsOn(provider, type,
            [Dependency.OnValue("label", "mapped")]), type);
        Assert.IsInstanceOfType(ConstructorActivator.CreateKeyed(provider, type, "key"), type);
        return new WeakReference(type);
    }

    public sealed class Part;
    public sealed class Candidate
    {
        public string Selected { get; }
        public Candidate(string label) => Selected = label + ":short";
        public Candidate(string label, Part part) => Selected = label + ":long";
    }
    public sealed class Mapped(string label, Part? part = null)
    { public string Label { get; } = label; public Part? Part { get; } = part; }
    public sealed class Optional(string label, int count = 7)
    { public string Label { get; } = label; public int Count { get; } = count; }
    public sealed class ConcurrentMapped([ServiceKey] string key, string label)
    { public string Key { get; } = key; public string Label { get; } = label; }
}
