using System.Reflection;
using System.Runtime.CompilerServices;
using Mammoth.Extensions.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

// refs::#83
[TestClass]
public class ConstructorExceptionRegressionTests
{
    public static IEnumerable<object[]> MappedCases()
    {
        foreach (var lifetime in new[] { ServiceLifetime.Transient, ServiceLifetime.Scoped, ServiceLifetime.Singleton })
        foreach (var keyed in new[] { false, true })
        foreach (var diagnostics in new[] { false, true })
        foreach (var mapKind in new[] { "unused", "value", "key" })
        foreach (var intentionalWrapper in new[] { false, true })
            yield return new object[] { lifetime, keyed, diagnostics, mapKind, intentionalWrapper };
    }

    [TestMethod]
    [DynamicData(nameof(MappedCases))]
    public void MappedConstructorKeepsOriginalExceptionAndStack(ServiceLifetime lifetime, bool keyed,
        bool diagnostics, string mapKind, bool intentionalWrapper)
    {
        var original = Original(intentionalWrapper);
        var state = new ExceptionState(original);
        var services = Services(mapKind == "key" ? new ExceptionState(new Exception("wrong binding")) : state);
        services.AddKeyedSingleton("chosen", state);
        Dependency[] map = mapKind switch
        {
            "value" => [Dependency.OnValue("label", "configured")],
            "key" => [Parameter.ForKey("state").Eq("chosen")],
            _ => [Dependency.OnValue("unused", 1)]
        };
        RegisterMapped<Throwing>(services, lifetime, keyed, map);
        using var provider = Build(services, diagnostics);
        using var scope = provider.CreateScope();

        AssertOriginal(original, () => Resolve<Throwing>(scope.ServiceProvider, keyed));
        Assert.AreEqual(mapKind == "value" ? "configured" : "default", state.Label);
        Assert.AreEqual(1, state.ConstructorCalls);
        Assert.IsNotNull(scope.ServiceProvider.GetRequiredService<Healthy>());
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void NativeConstructorIsAnExceptionIdentityControl(bool keyed, bool intentionalWrapper)
    {
        var original = Original(intentionalWrapper);
        var state = new ExceptionState(original);
        IServiceCollection services = Services(state);
        services.Add(keyed
            ? ServiceDescriptor.KeyedTransient(typeof(Throwing), "blue", typeof(Throwing))
            : ServiceDescriptor.Transient(typeof(Throwing), typeof(Throwing)));
        using var provider = services.BuildServiceProvider();

        AssertOriginal(original, () => Resolve<Throwing>(provider, keyed));
        Assert.AreEqual("default", state.Label);
        Assert.AreEqual(1, state.ConstructorCalls);
    }

    public static IEnumerable<object[]> DependencyCases()
    {
        foreach (var keyed in new[] { false, true })
        foreach (var diagnostics in new[] { false, true })
        foreach (var namedKey in new[] { false, true })
        foreach (var intentionalWrapper in new[] { false, true })
            yield return new object[] { keyed, diagnostics, namedKey, intentionalWrapper };
    }

    [TestMethod]
    [DynamicData(nameof(DependencyCases))]
    public void MappedDependencyFailureIsNotUnwrapped(bool keyed, bool diagnostics, bool namedKey, bool intentionalWrapper)
    {
        var original = Original(intentionalWrapper);
        var state = new ExceptionState(original);
        var services = Services(state);
        services.AddTransient<Part>(_ => FailPart(state));
        services.AddKeyedTransient<Part>("chosen", (_, _) => FailPart(state));
        Dependency[] map = namedKey
            ? [Parameter.ForKey("part").Eq("chosen")]
            : [Dependency.OnValue("unused", 1)];
        RegisterMapped<Consumer>(services, ServiceLifetime.Transient, keyed, map);
        using var provider = Build(services, diagnostics);
        using var scope = provider.CreateScope();

        AssertOriginal(original, () => Resolve<Consumer>(scope.ServiceProvider, keyed));
        Assert.AreEqual(0, state.ConstructorCalls);
        Assert.IsNotNull(scope.ServiceProvider.GetRequiredService<Healthy>());
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void ContextualDecoratorConstructorKeepsOriginalException(bool diagnostics, bool intentionalWrapper)
    {
        var original = Original(intentionalWrapper);
        var state = new ExceptionState(original);
        var services = Services(state);
        services.AddKeyedSingleton<IWork>("blue", new PlainWork());
        services.Decorate<IWork, ThrowingWrapper>();
        using var provider = Build(services, diagnostics);
        using var scope = provider.CreateScope();

        AssertOriginal(original, () => scope.ServiceProvider.GetRequiredKeyedService<IWork>("blue"));
        Assert.AreEqual(1, state.ConstructorCalls);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void ContextualDecoratorDependencyFailureIsNotUnwrapped(bool diagnostics, bool intentionalWrapper)
    {
        var original = Original(intentionalWrapper);
        var state = new ExceptionState(original);
        var services = Services(state);
        services.AddTransient<Part>(_ => FailPart(state));
        services.AddKeyedSingleton<IWork>("blue", new PlainWork());
        services.Decorate<IWork, DependentWrapper>();
        using var provider = Build(services, diagnostics);
        using var scope = provider.CreateScope();

        AssertOriginal(original, () => scope.ServiceProvider.GetRequiredKeyedService<IWork>("blue"));
        Assert.AreEqual(0, state.ConstructorCalls);
        Assert.IsNotNull(scope.ServiceProvider.GetRequiredService<Healthy>());
    }

    private static Exception Original(bool intentionalWrapper) => intentionalWrapper
        ? new TargetInvocationException("intentional application wrapper", new ArgumentException("inner original"))
        : new ArgumentException("original");

    private static void AssertOriginal(Exception original, Action resolve)
    {
        var error = Assert.Throws<Exception>(resolve);
        Assert.AreSame(original, error, "Only the wrapper introduced by reflection may be removed.");
        Assert.IsNotNull(error.StackTrace);
        StringAssert.Contains(error.StackTrace, nameof(ExceptionState.ThrowOriginal));
    }

    private static ServiceCollection Services(ExceptionState state)
    {
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddTransient<Healthy>();
        return services;
    }

    private static Part FailPart(ExceptionState state)
    {
        state.ThrowOriginal();
        throw new AssertFailedException("Expected dependency failure.");
    }

    private static void RegisterMapped<T>(IServiceCollection services, ServiceLifetime lifetime, bool keyed,
        Dependency[] map) where T : class
    {
        switch (lifetime)
        {
            case ServiceLifetime.Transient:
                if (keyed) services.AddKeyedTransient<T>("blue", map);
                else services.AddTransient<T>(map);
                break;
            case ServiceLifetime.Scoped:
                if (keyed) services.AddKeyedScoped<T>("blue", map);
                else services.AddScoped<T>(map);
                break;
            case ServiceLifetime.Singleton:
                if (keyed) services.AddKeyedSingleton<T>("blue", map);
                else services.AddSingleton<T>(map);
                break;
        }
    }

    private static T Resolve<T>(IServiceProvider provider, bool keyed) where T : notnull =>
        keyed ? provider.GetRequiredKeyedService<T>("blue") : provider.GetRequiredService<T>();

    private static ServiceProvider Build(IServiceCollection services, bool diagnostics) =>
        ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            DetectIncorrectUsageOfTransientDisposables = diagnostics,
            ValidateOnBuild = true,
            ValidateScopes = true
        });

    public sealed class ExceptionState(Exception error)
    {
        public Exception Error { get; } = error;
        public int ConstructorCalls { get; set; }
        public string? Label { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ThrowOriginal() => throw Error;
    }

    public sealed class Throwing
    {
        public Throwing(ExceptionState state, string label = "default")
        {
            state.ConstructorCalls++;
            state.Label = label;
            state.ThrowOriginal();
        }
    }

    public sealed class Consumer
    {
        public Consumer(Part part, ExceptionState state) => state.ConstructorCalls++;
    }

    public interface IWork { }
    public sealed class PlainWork : IWork { }
    public sealed class ThrowingWrapper : IWork
    {
        public ThrowingWrapper(IWork inner, [ServiceKey] object key, ExceptionState state)
        {
            state.ConstructorCalls++;
            state.ThrowOriginal();
        }
    }

    public sealed class DependentWrapper : IWork
    {
        public DependentWrapper(IWork inner, [ServiceKey] object key, Part part, ExceptionState state) =>
            state.ConstructorCalls++;
    }

    public sealed class Part { }
    public sealed class Healthy { }
}
