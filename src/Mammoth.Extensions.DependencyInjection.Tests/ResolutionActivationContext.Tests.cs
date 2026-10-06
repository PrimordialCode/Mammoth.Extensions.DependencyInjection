using Microsoft.Extensions.DependencyInjection;

namespace Mammoth.Extensions.DependencyInjection.Tests;

[TestClass]
public class ResolutionActivationContextTests
{
    [TestMethod]
    [DataRow(ServiceLifetime.Transient)]
    [DataRow(ServiceLifetime.Scoped)]
    [DataRow(ServiceLifetime.Singleton)]
    public void RepeatedSiblingResolutionsAndRegistrationsRemainIndependent(ServiceLifetime lifetime)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<Leaf>();
        services.AddTransient<Pair>();
        var invocation = 0;
        var sharedDescriptor = ServiceDescriptor.Describe(typeof(INode), sp =>
        {
            if (invocation++ == 0) return new Node(sp.GetRequiredService<INode>());
            return new Node(null);
        }, lifetime);
        services.Add(sharedDescriptor);
        services.Add(sharedDescriptor);
        using var provider = Build(services);
        var nodes = provider.GetServices<INode>().ToArray();
        Assert.AreEqual(2, nodes.Length);
        Assert.IsNotNull(((Node)nodes[0]).Inner);
        for (var i = 0; i < 3; i++)
        {
            var pair = provider.GetRequiredService<Pair>();
            Assert.AreNotSame(pair.Left, pair.Right);
        }
        Assert.AreEqual(0, ResolutionContext.CurrentStack.Count);
    }

    [TestMethod]
    public void AnyKeyUsesActualRequestKeyAndEqualKeysDetectReentry()
    {
        var recurse = false;
        var attempts = 0;
        var services = new ServiceCollection();
        services.AddKeyedTransient<INode>(KeyedService.AnyKey, (sp, key) =>
        {
            // Bound the regression even if the guard is removed.
            if (++attempts > 4) throw new InvalidOperationException("Bounded factory re-entry sentinel.");
            if (Equals(key, "red")) return new Node(sp.GetRequiredKeyedService<INode>("blue"));
            if (recurse) return new Node(sp.GetRequiredKeyedService<INode>(new string("red".ToCharArray())));
            return new Node(null);
        });
        using var provider = Build(services);
        Assert.IsNotNull(provider.GetRequiredKeyedService<INode>("red"));
        attempts = 0;
        recurse = true;
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => provider.GetRequiredKeyedService<INode>("red"));
        StringAssert.Contains(error.Message, "red");
        StringAssert.Contains(error.Message, "blue");
        recurse = false;
        attempts = 0;
        Assert.IsNotNull(provider.GetRequiredKeyedService<INode>("red"));
        Assert.AreEqual(0, ResolutionContext.CurrentStack.Count);
    }

    [TestMethod]
    [DataRow(ServiceLifetime.Transient)]
    [DataRow(ServiceLifetime.Scoped)]
    public void NestedScopesCanActivateTheSameRegistration(ServiceLifetime lifetime)
    {
        var depth = 0;
        IServiceCollection services = new ServiceCollection();
        services.Add(ServiceDescriptor.Describe(typeof(INode), sp =>
        {
            if (depth++ == 0)
            {
                try
                {
                    using var scope = sp.CreateScope();
                    return new Node(scope.ServiceProvider.GetRequiredService<INode>());
                }
                finally { depth--; }
            }
            depth--;
            return new Node(null);
        }, lifetime));
        using var provider = Build(services);
        Assert.IsNotNull(((Node)provider.GetRequiredService<INode>()).Inner);
        Assert.AreEqual(0, ResolutionContext.CurrentStack.Count);
    }

    [TestMethod]
    public void SeparatelyBuiltProvidersRemainIndependent()
    {
        ServiceProvider? second = null;
        var enterSecond = true;
        var services = new ServiceCollection();
        services.AddTransient<INode>(_ =>
        {
            if (!enterSecond) return new Node(null);
            enterSecond = false;
            return new Node(second!.GetRequiredService<INode>());
        });
        using var first = Build(services);
        using (second = Build(services))
            Assert.IsNotNull(((Node)first.GetRequiredService<INode>()).Inner);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DecoratorLayersRemainDistinct(bool keyed)
    {
        var services = new ServiceCollection();
        if (keyed) services.AddKeyedTransient<INode, EmptyNode>("decorated");
        else services.AddTransient<INode, EmptyNode>();
        services.Decorate<INode, NodeDecorator>();
        services.Decorate<INode, NodeDecorator>();
        using var provider = Build(services);
        var outer = keyed ? provider.GetRequiredKeyedService<INode>("decorated") : provider.GetRequiredService<INode>();
        Assert.IsInstanceOfType<NodeDecorator>(outer);
        Assert.IsInstanceOfType<NodeDecorator>(((NodeDecorator)outer).Inner);
        Assert.IsInstanceOfType<EmptyNode>(((NodeDecorator)((NodeDecorator)outer).Inner).Inner);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ChildResolutionCanOverlapOrOutliveTheOriginatingFactory(bool outliveParent)
    {
        using var release = new ManualResetEventSlim();
        Task<Leaf>? pending = null;
        var count = 0;
        var services = new ServiceCollection();
        services.AddTransient<Leaf>(sp =>
        {
            if (Interlocked.Increment(ref count) == 1)
            {
                pending = Task.Run(() =>
                {
                    Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(10)));
                    return sp.GetRequiredService<Leaf>();
                });
                if (!outliveParent)
                {
                    release.Set();
                    Assert.IsTrue(pending.Wait(TimeSpan.FromSeconds(10)));
                }
            }
            return new Leaf();
        });
        using var provider = Build(services);
        try { Assert.IsNotNull(provider.GetRequiredService<Leaf>()); }
        finally { release.Set(); }
        Assert.IsNotNull(pending);
        Assert.IsNotNull(await pending);
        Assert.AreEqual(2, count);
        Assert.AreEqual(0, ResolutionContext.CurrentStack.Count);
    }

    [TestMethod]
    public async Task ConcurrentSiblingsDoNotShareActiveFrames()
    {
        using var entered = new CountdownEvent(2);
        using var release = new ManualResetEventSlim();
        var services = new ServiceCollection();
        services.AddTransient<Leaf>(_ =>
        {
            entered.Signal();
            Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(10)));
            return new Leaf();
        });
        using var provider = Build(services);
        // Dedicated workers keep this gate test independent of test-host pool capacity.
        var first = Task.Factory.StartNew(() => provider.GetRequiredService<Leaf>(),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var second = Task.Factory.StartNew(() => provider.GetRequiredService<Leaf>(),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try { Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10))); }
        finally { release.Set(); }
        Assert.AreNotSame(await first, await second);
    }

    [TestMethod]
    public void CaughtCycleRestoresDiagnosticAncestryAndAllowsRecovery()
    {
        var recurse = true;
        var attempts = 0;
        var services = new ServiceCollection();
        services.AddTransient<Leaf>();
        services.AddTransient<INode>(sp =>
        {
            if (++attempts > 3) throw new InvalidOperationException("Bounded factory re-entry sentinel.");
            return recurse ? sp.GetRequiredService<INode>() : new Node(null);
        });
        services.AddTransient<Pair>(sp =>
        {
            var error = Assert.ThrowsExactly<InvalidOperationException>(() => sp.GetRequiredService<INode>());
            StringAssert.Contains(error.Message, "circular dependency");
            Assert.AreEqual(1, ResolutionContext.CurrentStack.Count);
            return new Pair(sp.GetRequiredService<Leaf>(), sp.GetRequiredService<Leaf>());
        });
        using var provider = Build(services);
        Assert.IsNotNull(provider.GetRequiredService<Pair>());
        recurse = false;
        attempts = 0;
        Assert.IsNotNull(provider.GetRequiredService<INode>());
        Assert.AreEqual(0, ResolutionContext.CurrentStack.Count);
    }

    [TestMethod]
    public async Task ConstructorBodyCanStartAnOverlappingResolution()
    {
        var gate = new ConstructorGate();
        var services = new ServiceCollection();
        services.AddSingleton(gate);
        services.AddTransient<BodyResolution>();
        using var provider = Build(services);
        Assert.IsNotNull(provider.GetRequiredService<BodyResolution>());
        Assert.IsNotNull(gate.Pending);
        Assert.IsNotNull(await gate.Pending);
        Assert.AreEqual(2, gate.Count);
        Assert.AreEqual(0, ResolutionContext.CurrentStack.Count);
    }

    public sealed class ConstructorGate
    {
        public int Count;
        public Task<BodyResolution>? Pending;
    }

    public sealed class BodyResolution
    {
        public BodyResolution(IServiceProvider provider, ConstructorGate gate)
        {
            if (Interlocked.Increment(ref gate.Count) != 1) return;
            gate.Pending = Task.Run(() => provider.GetRequiredService<BodyResolution>());
            Assert.IsTrue(gate.Pending.Wait(TimeSpan.FromSeconds(10)));
        }
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, false)]
    [DataRow(false, true, true)]
    [DataRow(true, false, false)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    [DataRow(true, true, true)]
    public async Task OpenGenericConstructorBodyStartsIndependentWork(bool keyed, bool enumerable, bool delayed)
    {
        var type = (keyed, enumerable) switch
        {
            (false, false) => typeof(OpenBodyParent),
            (false, true) => typeof(EnumerableBodyParent),
            (true, false) => typeof(KeyedBodyParent),
            _ => typeof(KeyedEnumerableBodyParent)
        };
        using var gate = new OpenBodyGate(type, delayed);
        var services = new ServiceCollection();
        services.AddSingleton(gate);
        services.AddTransient(type);
        if (keyed) services.AddKeyedTransient(typeof(OpenBody<>), KeyedService.AnyKey, typeof(OpenBody<>));
        else services.AddTransient(typeof(OpenBody<>));
        // Keyed enumerations deliberately use exact-key registrations.
        if (keyed && enumerable) services.AddKeyedTransient(typeof(OpenBody<>), "open", typeof(OpenBody<>));
        using var provider = Build(services);
        try { Assert.IsNotNull(provider.GetRequiredService(type)); }
        finally { gate.Release.Set(); }
        Assert.IsNotNull(gate.Pending);
        Assert.IsNotNull(await gate.Pending);
        Assert.AreEqual(2, gate.Count);
        Assert.AreEqual(0, ResolutionContext.CurrentStack.Count);
    }

    public sealed class OpenBodyGate(Type parent, bool delayed) : IDisposable
    {
        public Type Parent { get; } = parent;
        public bool Delayed { get; } = delayed;
        public ManualResetEventSlim Release { get; } = new();
        public int Count;
        public Task<object>? Pending;
        public void Dispose() => Release.Dispose();
    }

    public sealed class OpenBody<T>
    {
        public OpenBody(IServiceProvider provider, OpenBodyGate gate)
        {
            if (Interlocked.Increment(ref gate.Count) != 1) return;
            gate.Pending = Task.Run(() =>
            {
                Assert.IsTrue(gate.Release.Wait(TimeSpan.FromSeconds(10)));
                return provider.GetRequiredService(gate.Parent);
            });
            if (gate.Delayed) return;
            gate.Release.Set();
            Assert.IsTrue(gate.Pending.Wait(TimeSpan.FromSeconds(10)));
        }
    }
    public sealed class OpenBodyParent { public OpenBodyParent(OpenBody<int> value) { } }
    public sealed class EnumerableBodyParent { public EnumerableBodyParent(IEnumerable<OpenBody<int>> value) { } }
    public sealed class KeyedBodyParent { public KeyedBodyParent([FromKeyedServices("open")] OpenBody<int> value) { } }
    public sealed class KeyedEnumerableBodyParent { public KeyedEnumerableBodyParent([FromKeyedServices("open")] IEnumerable<OpenBody<int>> value) { } }

    [TestMethod]
    public void ConstructorBindingsRespectExactBindingsAndEnumeration()
    {
        var services = new ServiceCollection();
        services.AddKeyedTransient(typeof(GenericNode<>), "red", typeof(GenericNode<>));
        services.AddKeyedTransient<GenericNode<int>>(KeyedService.AnyKey);
        using (var provider = Build(services))
        {
            var snapshot = provider.GetRequiredService<ServiceProviderRegistrationSnapshot>();
            var exact = snapshot.GetConstructorRegistrations(typeof(GenericNode<int>), "red").Single();
            Assert.AreEqual(1, exact.Index, "A closed AnyKey binding precedes an open exact-key binding.");
            Assert.AreEqual("red", exact.Service.ServiceKey);
            var enumerable = snapshot.GetConstructorRegistrations(typeof(IEnumerable<GenericNode<int>>), "red").Single();
            Assert.AreEqual(0, enumerable.Index, "Implicit enumeration excludes AnyKey registrations.");
            Assert.AreEqual(typeof(GenericNode<int>), enumerable.ImplementationType);
        }
        services.AddKeyedTransient<IEnumerable<GenericNode<int>>>(KeyedService.AnyKey, (_, _) => []);
        using (var provider = Build(services))
            Assert.AreEqual(0, provider.GetRequiredService<ServiceProviderRegistrationSnapshot>()
                .GetConstructorRegistrations(typeof(IEnumerable<GenericNode<int>>), "red").Count(),
                "An explicit enumerable factory is an opaque leaf.");

        services = new ServiceCollection();
        services.AddKeyedTransient(typeof(GenericNode<>), KeyedService.AnyKey, typeof(GenericNode<>));
        services.AddKeyedTransient(typeof(GenericNode<>), "blue", typeof(GenericNode<>));
        using (var provider = Build(services))
        {
            var snapshot = provider.GetRequiredService<ServiceProviderRegistrationSnapshot>();
            Assert.AreEqual(1, snapshot.GetConstructorRegistrations(typeof(GenericNode<int>), "red").Count());
            Assert.AreEqual(0, snapshot.GetConstructorRegistrations(typeof(IEnumerable<GenericNode<int>>), "red").Count());
            var binding = snapshot.GetConstructorRegistrations(typeof(IEnumerable<GenericNode<int>>), KeyedService.AnyKey).Single();
            Assert.AreEqual("blue", binding.Service.ServiceKey);
            Assert.AreEqual(0, snapshot.GetConstructorRegistrations(typeof(IEnumerable<GenericNode<int>>), null).Count());
        }
    }

    public sealed class GenericNode<T> { }

    [TestMethod]
    public void RuntimeGuardDoesNotFlowIntoIndependentWork()
    {
        var registration = new object();
        var descriptor = ServiceDescriptor.Transient<Leaf, Leaf>();
        using var provider = new ServiceCollection().BuildServiceProvider();
        using (ResolutionActivationContext.Enter(registration, descriptor, provider, null))
        {
            // Background work is independent; constructor cycles are checked before activation.
            var childTask = new Task(() =>
            {
                using var child = ResolutionActivationContext.Enter(registration, descriptor, provider, null);
            });
            childTask.RunSynchronously();
            childTask.GetAwaiter().GetResult();
        }
    }

    private static ServiceProvider Build(IServiceCollection services) =>
        ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
        {
            DetectIncorrectUsageOfTransientDisposables = true,
            ValidateOnBuild = false
        });

    public sealed class Leaf { }
    public sealed class Pair(Leaf left, Leaf right)
    {
        public Leaf Left { get; } = left;
        public Leaf Right { get; } = right;
    }
    public interface INode { }
    public sealed class Node(INode? inner) : INode { public INode? Inner { get; } = inner; }
    public sealed class EmptyNode : INode { }
    public sealed class NodeDecorator(INode inner) : INode { public INode Inner { get; } = inner; }
}
