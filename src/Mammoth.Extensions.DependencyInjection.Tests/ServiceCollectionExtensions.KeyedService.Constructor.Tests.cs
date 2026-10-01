using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class DependsOnConstructorRegressionTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void SelectsShorterSatisfiableConstructor(bool keyed, bool tryAdd)
    {
        var services = new ServiceCollection();
        Dependency[] map = [Dependency.OnValue("label", "configured")];
        if (keyed)
        {
            if (tryAdd) services.TryAddKeyedScoped<MultipleConstructors>("outer", map);
            else services.AddKeyedScoped<MultipleConstructors>("outer", map);
        }
        else
        {
            if (tryAdd) services.TryAddScoped<MultipleConstructors>(map);
            else services.AddScoped<MultipleConstructors>(map);
        }
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var result = scope.ServiceProvider.GetRequiredKeyedService<MultipleConstructors>(keyed ? "outer" : null);
        Assert.AreEqual("configured:short", result.Selected);
    }

    [TestMethod]
    public void ResolvesKeyOverrideAndAvoidsConstructingRejectedDependencies()
    {
        var services = new ServiceCollection();
        int creations = 0;
        services.AddTransient<DependencyService>(_ => { creations++; return new DependencyService(); });
        services.AddKeyedSingleton<DependencyService>("blue");
        services.AddTransient<KeyedCandidate>([Parameter.ForKey("dependency").Eq("blue")]);
        using var provider = services.BuildServiceProvider();
        var candidate = provider.GetRequiredService<KeyedCandidate>();
        Assert.AreSame(provider.GetRequiredKeyedService<DependencyService>("blue"), candidate.Dependency);
        Assert.AreEqual(0, creations);
    }

    [TestMethod]
    public void OptionalDefaultsAndLongestAvailableConstructor()
    {
        var services = new ServiceCollection();
        services.AddSingleton<OptionalCandidate>([Dependency.OnValue("label", "optional")]);
        services.AddSingleton<Missing>();
        services.AddTransient<MultipleConstructors>([Dependency.OnValue("label", "long")]);
        using var provider = services.BuildServiceProvider();
        Assert.AreEqual(7, provider.GetRequiredService<OptionalCandidate>().Count);
        Assert.AreEqual("long:long", provider.GetRequiredService<MultipleConstructors>().Selected);
    }

    [TestMethod]
    public void PreferredConstructorWins()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Missing>();
        services.AddTransient<Preferred>([Dependency.OnValue("label", "preferred")]);
        using var provider = services.BuildServiceProvider();
        Assert.AreEqual("short", provider.GetRequiredService<Preferred>().Selected);
    }

    [TestMethod]
    public void EqualLengthSatisfiableConstructorsAreAmbiguous()
    {
        var services = new ServiceCollection();
        services.AddTransient<Ambiguous>([Dependency.OnValue("label", "value"), Dependency.OnValue("count", 2)]);
        using var provider = services.BuildServiceProvider();
        Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetRequiredService<Ambiguous>());
    }

    [TestMethod]
    public void SameTypeOverridesRemainMappedByParameterName()
    {
        var services = new ServiceCollection();
        services.AddTransient<NamedValues>([Dependency.OnValue("first", "one"), Dependency.OnValue("second", "two")]);
        using var provider = services.BuildServiceProvider();
        Assert.AreEqual("one:two", provider.GetRequiredService<NamedValues>().Value);
    }

    [TestMethod]
    public void InvalidOverrideDoesNotSelectUnusableConstructor()
    {
        var services = new ServiceCollection();
        services.AddTransient<OptionalCandidate>([Dependency.OnValue("label", 42)]);
        using var provider = services.BuildServiceProvider();
        Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetRequiredService<OptionalCandidate>());
    }

    [TestMethod]
    public void NativeActivatorControlUsesSatisfiableAlternative()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        Assert.AreEqual("native:short", ActivatorUtilities.CreateInstance<MultipleConstructors>(provider, "native").Selected);
    }

    public sealed class Missing { }
    public sealed class DependencyService { }
    public sealed class MultipleConstructors
    {
        public string Selected { get; }
        public MultipleConstructors(string label) => Selected = label + ":short";
        public MultipleConstructors(string label, Missing missing) => Selected = label + ":long";
    }
    public sealed class KeyedCandidate
    {
        public DependencyService Dependency { get; }
        public KeyedCandidate(DependencyService dependency) => Dependency = dependency;
        public KeyedCandidate(DependencyService dependency, Missing missing) => Dependency = dependency;
    }
    public sealed class OptionalCandidate(string label, int count = 7)
    {
        public string Label { get; } = label;
        public int Count { get; } = count;
    }
    public sealed class Preferred
    {
        public string Selected { get; }
        [ActivatorUtilitiesConstructor]
        public Preferred(string label) => Selected = "short";
        public Preferred(string label, Missing missing) => Selected = "long";
    }
    public sealed class Ambiguous
    {
        public Ambiguous(string label) { }
        public Ambiguous(int count) { }
    }
    public sealed class NamedValues(string first, string second)
    {
        public string Value { get; } = first + ":" + second;
    }
}
