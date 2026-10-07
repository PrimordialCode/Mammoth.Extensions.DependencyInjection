using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class KeyedOpenGenericProviderTests
{
    [TestMethod]
    [DataRow(ServiceLifetime.Transient, false)]
    [DataRow(ServiceLifetime.Scoped, false)]
    [DataRow(ServiceLifetime.Singleton, false)]
    [DataRow(ServiceLifetime.Transient, true)]
    [DataRow(ServiceLifetime.Scoped, true)]
    [DataRow(ServiceLifetime.Singleton, true)]
    public void BuildsResolvesAndDiscoversOpenGenerics(ServiceLifetime lifetime, bool diagnostics)
    {
        IServiceCollection services = new ServiceCollection();
        services.Add(new ServiceDescriptor(typeof(IRepository<>), typeof(Repository<>), lifetime));
        services.Add(new ServiceDescriptor(typeof(IRepository<>), "db", typeof(Repository<>), lifetime));
        services.Add(new ServiceDescriptor(typeof(IRepository<>), "other", typeof(Repository<>), lifetime));
        using var provider = ServiceProviderFactory.CreateServiceProvider(services,
            diagnostics ? new ExtendedServiceProviderOptions { ValidateOnBuild = true, DetectIncorrectUsageOfTransientDisposables = true } : null);
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var first = sp.GetRequiredKeyedService<IRepository<string>>("db");
        var second = sp.GetRequiredKeyedService<IRepository<string>>("db");
        Assert.IsInstanceOfType<Repository<string>>(first);
        if (lifetime == ServiceLifetime.Transient) Assert.AreNotSame(first, second);
        else Assert.AreSame(first, second);
        Assert.HasCount(3, sp.GetAllServices<IRepository<string>>().ToArray());
        Assert.HasCount(3, sp.GetAllServices(typeof(IRepository<string>)).ToArray());
        Assert.IsTrue(sp.IsServiceRegistered<IRepository<string>>());
        Assert.AreEqual(lifetime, sp.GetRequiredService<ServiceLifetimes>().GetLifetime(typeof(IRepository<string>), "db"));
    }

    [TestMethod]
    public void ClosedAndOpenKeysMergeWithoutDuplicatingEnumeration()
    {
        var services = new ServiceCollection();
        services.AddKeyedTransient(typeof(IRepository<>), "db", typeof(Repository<>));
        services.AddKeyedScoped<IRepository<string>, ClosedRepository>("db");
        services.AddKeyedScoped<IRepository<string>, ClosedRepository>("closed");
        using var provider = ServiceProviderFactory.CreateServiceProvider(services);
        using var scope = provider.CreateScope();
        var expected = scope.ServiceProvider.GetKeyedServices<IRepository<string>>("db").Count() + 1;
        var first = scope.ServiceProvider.GetAllServices<IRepository<string>>().ToArray();
        var second = scope.ServiceProvider.GetAllServices(typeof(IRepository<string>)).ToArray();
        Assert.HasCount(expected, first);
        Assert.HasCount(expected, second);
        Assert.IsTrue(scope.ServiceProvider.IsKeyedScopedServiceRegistered<IRepository<string>>("db"));
        Assert.IsTrue(scope.ServiceProvider.IsKeyedTransientServiceRegistered<IRepository<int>>("db"));
        Assert.IsFalse(scope.ServiceProvider.IsKeyedScopedServiceRegistered<IRepository<int>>("missing"));
    }

    [TestMethod]
    public void NativeControlAndMetadataTypeExplainStartupFailure()
    {
        Assert.IsTrue(typeof(ServiceKeys<>).MakeGenericType(typeof(IRepository<>)).ContainsGenericParameters);
        var services = new ServiceCollection();
        services.AddKeyedScoped(typeof(IRepository<>), "db", typeof(Repository<>));
        using var native = services.BuildServiceProvider();
        using var scope = native.CreateScope();
        Assert.IsInstanceOfType<Repository<string>>(scope.ServiceProvider.GetRequiredKeyedService<IRepository<string>>("db"));
    }

    [TestMethod]
    public void OpenGenericDisposableDiagnosticPolicyIsPreserved()
    {
        var services = new ServiceCollection();
        services.AddKeyedTransient(typeof(IRepository<>), "db", typeof(DisposableRepository<>));
        var options = new ExtendedServiceProviderOptions
        {
            ValidateOnBuild = true,
            DetectIncorrectUsageOfTransientDisposables = true,
            ThrowOnOpenGenericTransientDisposable = true
        };
        Assert.ThrowsExactly<InvalidOperationException>(() => ServiceProviderFactory.CreateServiceProvider(services, options));
        var permissiveServices = new ServiceCollection();
        permissiveServices.AddKeyedTransient(typeof(IRepository<>), "db", typeof(DisposableRepository<>));
        options.ThrowOnOpenGenericTransientDisposable = false;
        using var provider = ServiceProviderFactory.CreateServiceProvider(permissiveServices, options);
        using var scope = provider.CreateScope();
        Assert.IsNotNull(scope.ServiceProvider.GetRequiredKeyedService<IRepository<int>>("db"));
    }

    [TestMethod]
    public void NativeRepeatedEnumerationControl()
    {
        var services = new ServiceCollection();
        services.AddKeyedTransient(typeof(IRepository<>), "db", typeof(Repository<>));
        services.AddKeyedScoped<IRepository<string>, ClosedRepository>("db");
        services.AddKeyedScoped<IRepository<string>, ClosedRepository>("closed");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        for (int i = 0; i < 8; i++)
        {
            var all = scope.ServiceProvider.GetServices(typeof(IRepository<string>))
                .Concat(scope.ServiceProvider.GetKeyedServices(typeof(IRepository<string>), "db"))
                .Concat(scope.ServiceProvider.GetKeyedServices(typeof(IRepository<string>), "closed")).ToArray();
            Assert.HasCount(3, all);
        }
    }

    public interface IRepository<T> { }
    public class Repository<T> : IRepository<T> { }
    public sealed class ClosedRepository : IRepository<string> { }
    public sealed class DisposableRepository<T> : Repository<T>, IDisposable
    {
        public void Dispose() { }
    }
}
