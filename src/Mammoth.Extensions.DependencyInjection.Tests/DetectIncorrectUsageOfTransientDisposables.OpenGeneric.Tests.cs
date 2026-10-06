using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class OpenGenericTransientDisposableDiagnosticTests
{
    [TestMethod]
    [DataRow(false, typeof(DisposableResource<>))]
    [DataRow(true, typeof(DisposableResource<>))]
    [DataRow(false, typeof(AsyncDisposableResource<>))]
    [DataRow(true, typeof(AsyncDisposableResource<>))]
    public void RejectionNamesTheImplementation(bool keyed, Type implementationType)
    {
        var services = CreateServices(keyed, implementationType, ServiceLifetime.Transient);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            using var provider = ServiceProviderFactory.CreateServiceProvider(services, CreateOptions());
        });

        Assert.AreEqual(
            $"Trying to register an open generic transient disposable service {implementationType.Name}.",
            error.Message);
    }

    [TestMethod]
    [DataRow(false, typeof(DisposableResource<>))]
    [DataRow(true, typeof(DisposableResource<>))]
    [DataRow(false, typeof(AsyncDisposableResource<>))]
    [DataRow(true, typeof(AsyncDisposableResource<>))]
    public async Task WarningModeNamesTheImplementationAndAllowsResolution(bool keyed, Type implementationType)
    {
        var services = CreateServices(keyed, implementationType, ServiceLifetime.Transient);
        services.AddLogging(builder => builder.AddFakeLogging());
        var options = CreateOptions();
        options.ThrowOnOpenGenericTransientDisposable = false;

        await using var provider = ServiceProviderFactory.CreateServiceProvider(services, options);

        var collector = provider.GetFakeLogCollector();
        Assert.AreEqual(1, collector.Count);
        Assert.AreEqual(LogLevel.Warning, collector.LatestRecord.Level);
        Assert.AreEqual(
            $"Open generic transient disposable registration detected, ServiceKey: {(keyed ? "resource" : "(null)")}, " +
            $"ServiceType: {typeof(IResource<>)}, ImplementationType: {implementationType}",
            collector.LatestRecord.Message);
        await AssertResolution(provider, keyed, implementationType, ServiceLifetime.Transient);
    }

    [TestMethod]
    [DataRow(false, typeof(DisposableResource<>), ServiceLifetime.Scoped)]
    [DataRow(true, typeof(DisposableResource<>), ServiceLifetime.Scoped)]
    [DataRow(false, typeof(AsyncDisposableResource<>), ServiceLifetime.Scoped)]
    [DataRow(true, typeof(AsyncDisposableResource<>), ServiceLifetime.Scoped)]
    [DataRow(false, typeof(DisposableResource<>), ServiceLifetime.Singleton)]
    [DataRow(true, typeof(DisposableResource<>), ServiceLifetime.Singleton)]
    [DataRow(false, typeof(AsyncDisposableResource<>), ServiceLifetime.Singleton)]
    [DataRow(true, typeof(AsyncDisposableResource<>), ServiceLifetime.Singleton)]
    public async Task NonTransientDisposablesAreAllowed(bool keyed, Type implementationType, ServiceLifetime lifetime)
    {
        var services = CreateServices(keyed, implementationType, lifetime);
        await using var provider = ServiceProviderFactory.CreateServiceProvider(services, CreateOptions());

        await AssertResolution(provider, keyed, implementationType, lifetime);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NonDisposableTransientsAreAllowed(bool keyed)
    {
        var services = CreateServices(keyed, typeof(Resource<>), ServiceLifetime.Transient);
        await using var provider = ServiceProviderFactory.CreateServiceProvider(services, CreateOptions());

        await AssertResolution(provider, keyed, typeof(Resource<>), ServiceLifetime.Transient);
    }

    [TestMethod]
    [DataRow(false, typeof(DisposableResource<>))]
    [DataRow(true, typeof(DisposableResource<>))]
    [DataRow(false, typeof(AsyncDisposableResource<>))]
    [DataRow(true, typeof(AsyncDisposableResource<>))]
    public async Task DisabledDiagnosticsAllowDisposableTransients(bool keyed, Type implementationType)
    {
        var services = CreateServices(keyed, implementationType, ServiceLifetime.Transient);
        var options = CreateOptions();
        options.DetectIncorrectUsageOfTransientDisposables = false;
        await using var provider = ServiceProviderFactory.CreateServiceProvider(services, options);

        await AssertResolution(provider, keyed, implementationType, ServiceLifetime.Transient);
    }

    [TestMethod]
    [DataRow(false, typeof(DisposableResource<>))]
    [DataRow(true, typeof(DisposableResource<>))]
    [DataRow(false, typeof(AsyncDisposableResource<>))]
    [DataRow(true, typeof(AsyncDisposableResource<>))]
    public async Task ExcludedDisposableTransientsAreAllowed(bool keyed, Type implementationType)
    {
        var services = CreateServices(keyed, implementationType, ServiceLifetime.Transient);
        var options = CreateOptions();
        options.DetectIncorrectUsageOfTransientDisposablesExclusionPatterns =
            [$"^{Regex.Escape(typeof(IResource<>).FullName!)}$"];
        await using var provider = ServiceProviderFactory.CreateServiceProvider(services, options);

        await AssertResolution(provider, keyed, implementationType, ServiceLifetime.Transient);
    }

    private static IServiceCollection CreateServices(bool keyed, Type implementationType, ServiceLifetime lifetime)
    {
        IServiceCollection services = new ServiceCollection();
        services.Add(keyed
            ? ServiceDescriptor.DescribeKeyed(typeof(IResource<>), "resource", implementationType, lifetime)
            : ServiceDescriptor.Describe(typeof(IResource<>), implementationType, lifetime));
        return services;
    }

    private static ExtendedServiceProviderOptions CreateOptions() => new()
    {
        DetectIncorrectUsageOfTransientDisposables = true,
        ThrowOnOpenGenericTransientDisposable = true,
        ValidateOnBuild = true,
        ValidateScopes = true
    };

    private static async Task AssertResolution(ServiceProvider provider, bool keyed, Type implementationType,
        ServiceLifetime lifetime)
    {
        await using var scope = provider.CreateAsyncScope();
        var first = Resolve(scope.ServiceProvider, keyed);
        var second = Resolve(scope.ServiceProvider, keyed);
        Assert.AreEqual(implementationType.MakeGenericType(typeof(int)), first.GetType());
        Assert.AreEqual(first.GetType(), second.GetType());
        if (lifetime == ServiceLifetime.Transient)
            Assert.AreNotSame(first, second);
        else
            Assert.AreSame(first, second);
    }

    private static IResource<int> Resolve(IServiceProvider provider, bool keyed) => keyed
        ? provider.GetRequiredKeyedService<IResource<int>>("resource")
        : provider.GetRequiredService<IResource<int>>();

    public interface IResource<T>;
    public sealed class Resource<T> : IResource<T>;
    public sealed class DisposableResource<T> : IResource<T>, IDisposable
    {
        public void Dispose() { }
    }
    public sealed class AsyncDisposableResource<T> : IResource<T>, IAsyncDisposable
    {
        public ValueTask DisposeAsync() => default;
    }
}
