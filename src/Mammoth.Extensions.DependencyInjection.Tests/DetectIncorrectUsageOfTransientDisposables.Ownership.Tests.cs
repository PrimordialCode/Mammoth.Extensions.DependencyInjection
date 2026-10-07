using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class TransientDisposableOwnershipTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task RejectedAllocationsRemainOwnedUntilProviderDisposal(bool keyed, bool asyncOnly)
    {
        var created = new List<Resource>();
        var services = new ServiceCollection();
        Register(services, keyed, _ =>
        {
            Resource result = asyncOnly ? new AsyncResource() : new SyncResource();
            created.Add(result);
            return result;
        });
        var provider = Build(services);
        for (int i = 0; i < 3; i++)
            Reject(provider, keyed);
        Assert.HasCount(3, created);
        Assert.IsTrue(created.All(result => result.DisposeCount == 0));
        if (asyncOnly)
            await provider.DisposeAsync();
        else
            provider.Dispose();
        Assert.IsTrue(created.All(result => result.DisposeCount == 1));
        await provider.DisposeAsync();
        Assert.IsTrue(created.All(result => result.DisposeCount == 1));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task RepeatedRejectedSharedResultIsCapturedOnce(bool keyed, bool asyncOnly)
    {
        Resource result = asyncOnly ? new AsyncResource() : new SyncResource();
        var services = new ServiceCollection();
        Register(services, keyed, _ => result);
        var provider = Build(services);
        for (int i = 0; i < 3; i++)
            Reject(provider, keyed);
        Assert.AreEqual(0, result.DisposeCount);
        await provider.DisposeAsync();
        Assert.AreEqual(1, result.DisposeCount);
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, true, false)]
    [DataRow(false, false, true)]
    [DataRow(true, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, true, true)]
    public async Task ProviderResolvedSingletonIsNeitherDisposedEarlyNorCapturedAgain(bool keyed, bool asyncOnly, bool preResolve)
    {
        Resource result = asyncOnly ? new AsyncResource() : new SyncResource();
        var services = new ServiceCollection();
        services.AddKeyedSingleton<Resource>("owned", (_, _) => result);
        Register(services, keyed, sp => sp.GetRequiredKeyedService<Resource>("owned"));
        var provider = Build(services);
        if (preResolve)
            Assert.AreSame(result, provider.GetRequiredKeyedService<Resource>("owned"));
        for (int i = 0; i < 3; i++)
            Reject(provider, keyed);
        Assert.AreEqual(0, result.DisposeCount);
        Assert.AreSame(result, provider.GetRequiredKeyedService<Resource>("owned"));
        await provider.DisposeAsync();
        Assert.AreEqual(1, result.DisposeCount);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task ProviderResolvedScopedResultRetainsItsExistingRootOwner(bool keyed, bool asyncOnly)
    {
        Resource result = asyncOnly ? new AsyncResource() : new SyncResource();
        var services = new ServiceCollection();
        services.AddScoped<Resource>(_ => result);
        Register(services, keyed, sp => sp.GetRequiredService<Resource>());
        var provider = Build(services);
        Reject(provider, keyed);
        Reject(provider, keyed);
        Assert.AreEqual(0, result.DisposeCount);
        await provider.DisposeAsync();
        Assert.AreEqual(1, result.DisposeCount);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task SuccessfulChildScopeKeepsNativeOwnership(bool keyed, bool asyncOnly)
    {
        Resource result = asyncOnly ? new AsyncResource() : new SyncResource();
        var services = new ServiceCollection();
        Register(services, keyed, _ => result);
        var provider = Build(services);
        var scope = provider.CreateAsyncScope();
        Assert.AreSame(result, Resolve(scope.ServiceProvider, keyed));
        Assert.AreEqual(0, result.DisposeCount);
        await scope.DisposeAsync();
        Assert.AreEqual(1, result.DisposeCount);
        await provider.DisposeAsync();
        Assert.AreEqual(1, result.DisposeCount);
    }

    [TestMethod]
    [DataRow(false, false, true)]
    [DataRow(true, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, true, true)]
    [DataRow(false, false, false)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, true, false)]
    public async Task SingletonExemptionPreservesOwnershipAndRejectionWhenDisabled(bool keyed, bool asyncOnly, bool allow)
    {
        Resource result = asyncOnly ? new AsyncResource() : new SyncResource();
        var services = new ServiceCollection();
        Register(services, keyed, _ => result);
        services.AddSingleton(sp => new Consumer(Resolve(sp, keyed)));
        var provider = Build(services, allow);
        if (allow)
            Assert.AreSame(result, provider.GetRequiredService<Consumer>().Resource);
        else
            Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetRequiredService<Consumer>());
        Assert.AreEqual(0, result.DisposeCount);
        await provider.DisposeAsync();
        Assert.AreEqual(1, result.DisposeCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AsyncDisposalPrefersAsyncMethodOnDualDisposable(bool keyed)
    {
        var result = new DualResource();
        var services = new ServiceCollection();
        Register(services, keyed, _ => result);
        var provider = Build(services);
        Reject(provider, keyed);
        await provider.DisposeAsync();
        Assert.AreEqual(1, result.DisposeCount);
        Assert.AreEqual(0, result.SyncDisposeCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ConcurrentRejectionsCaptureSharedInstanceOnce(bool keyed)
    {
        var result = new AsyncResource();
        var services = new ServiceCollection();
        Register(services, keyed, _ => result);
        var provider = Build(services);
        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => Reject(provider, keyed))));
        Assert.AreEqual(0, result.DisposeCount);
        await provider.DisposeAsync();
        Assert.AreEqual(1, result.DisposeCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EqualButDistinctFactoryResultsAreIndependentlyOwned(bool keyed)
    {
        var first = new EqualResource();
        var second = new EqualResource();
        int calls = 0;
        var services = new ServiceCollection();
        Register(services, keyed, _ => calls++ == 0 ? first : second);
        var provider = Build(services);
        Reject(provider, keyed);
        Reject(provider, keyed);
        provider.Dispose();
        Assert.AreEqual(1, first.DisposeCount);
        Assert.AreEqual(1, second.DisposeCount);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task NativeFactoryControlOwnsSuccessfulRootResult(bool keyed, bool asyncOnly)
    {
        Resource result = asyncOnly ? new AsyncResource() : new SyncResource();
        var services = new ServiceCollection();
        Register(services, keyed, _ => result);
        var provider = services.BuildServiceProvider();
        Assert.AreSame(result, Resolve(provider, keyed));
        Assert.AreEqual(0, result.DisposeCount);
        await provider.DisposeAsync();
        Assert.AreEqual(1, result.DisposeCount);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task ProviderResolvedExcludedTransientIsNotCapturedAgain(bool keyed, bool asyncOnly)
    {
        var created = new List<Resource>();
        var services = new ServiceCollection();
        services.AddTransient<Resource>(_ =>
        {
            Resource result = asyncOnly ? new AsyncResource() : new SyncResource();
            created.Add(result);
            return result;
        });
        Register(services, keyed, sp => sp.GetRequiredService<Resource>());
        var provider = ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            ValidateOnBuild = true,
            DetectIncorrectUsageOfTransientDisposables = true,
            DetectIncorrectUsageOfTransientDisposablesExclusionPatterns = ["[+]Resource$"]
        });
        Reject(provider, keyed);
        Reject(provider, keyed);
        Assert.HasCount(2, created);
        Assert.IsTrue(created.All(result => result.DisposeCount == 0));
        await provider.DisposeAsync();
        Assert.IsTrue(created.All(result => result.DisposeCount == 1));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ProviderItselfIsNotCapturedAsARejectedFactoryResult(bool keyed)
    {
        var services = new ServiceCollection();
        if (keyed)
            services.AddKeyedTransient<object>("rejected", (sp, _) => sp);
        else
            services.AddTransient<object>(sp => sp);
        var provider = Build(services);
        Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetRequiredKeyedService<object>(keyed ? "rejected" : null));
        Assert.HasCount(0, provider.GetDisposables().ToArray());
        provider.Dispose();
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, true, false)]
    [DataRow(false, false, true)]
    [DataRow(true, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, true, true)]
    public async Task DisposalRacingFactoryReturnCleansUpExactlyOnce(bool keyed, bool asyncOnly, bool alreadyOwned)
    {
        Resource result = asyncOnly ? new AsyncResource() : new SyncResource();
        using var activated = new ManualResetEventSlim();
        using var returnResult = new ManualResetEventSlim();
        var services = new ServiceCollection();
        services.AddSingleton<Resource>(_ => result);
        Register(services, keyed, sp =>
        {
            if (alreadyOwned)
                Assert.AreSame(result, sp.GetRequiredService<Resource>());
            activated.Set();
            Assert.IsTrue(returnResult.Wait(TimeSpan.FromSeconds(10)));
            return result;
        });
        var provider = Build(services);
        var resolution = Task.Run(() =>
        {
            if (alreadyOwned)
                Reject(provider, keyed);
            else
                Assert.ThrowsExactly<ObjectDisposedException>(() => Resolve(provider, keyed));
            Assert.HasCount(0, ResolutionContext.CurrentStack);
        });
        try
        {
            Assert.IsTrue(activated.Wait(TimeSpan.FromSeconds(10)));
            await provider.DisposeAsync();
            Assert.AreEqual(alreadyOwned ? 1 : 0, result.DisposeCount);
        }
        finally
        {
            returnResult.Set();
        }
        await resolution;
        Assert.AreEqual(1, result.DisposeCount);
    }

    private static ServiceProvider Build(ServiceCollection services, bool allow = false) =>
        ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            ValidateOnBuild = true,
            DetectIncorrectUsageOfTransientDisposables = true,
            AllowSingletonToResolveTransientDisposables = allow
        });

    private static void Register(ServiceCollection services, bool keyed, Func<IServiceProvider, Resource> factory)
    {
        if (keyed)
            services.AddKeyedTransient<FactoryResult>("rejected", (sp, key) =>
            {
                Assert.AreEqual("rejected", key);
                return factory(sp);
            });
        else
            services.AddTransient<FactoryResult>(sp => factory(sp));
    }

    private static FactoryResult Resolve(IServiceProvider provider, bool keyed) => keyed
        ? provider.GetRequiredKeyedService<FactoryResult>("rejected")
        : provider.GetRequiredService<FactoryResult>();

    private static void Reject(IServiceProvider provider, bool keyed)
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => Resolve(provider, keyed));
        StringAssert.Contains(exception.Message, "Trying to resolve Transient Disposable service");
        StringAssert.Contains(exception.Message, "(factory)");
        Assert.HasCount(0, ResolutionContext.CurrentStack);
    }

    private interface FactoryResult;
    private abstract class Resource : FactoryResult
    {
        public int DisposeCount { get; protected set; }
    }

    private class SyncResource : Resource, IDisposable
    {
        public void Dispose() { DisposeCount++; GC.SuppressFinalize(this); }
    }

    private sealed class EqualResource : SyncResource
    {
        public override bool Equals(object? obj) => obj is EqualResource;
        public override int GetHashCode() => 0;
    }

    private sealed class AsyncResource : Resource, IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            DisposeCount++;
            GC.SuppressFinalize(this);
        }
    }

    private sealed class DualResource : Resource, IDisposable, IAsyncDisposable
    {
        public int SyncDisposeCount { get; private set; }
        public void Dispose() { SyncDisposeCount++; GC.SuppressFinalize(this); }
        public ValueTask DisposeAsync() { DisposeCount++; GC.SuppressFinalize(this); return default; }
    }

    private sealed class Consumer(FactoryResult resource)
    {
        public FactoryResult Resource { get; } = resource;
    }
}
