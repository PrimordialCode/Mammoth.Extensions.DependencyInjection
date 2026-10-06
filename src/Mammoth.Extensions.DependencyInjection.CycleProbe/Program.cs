using Mammoth.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Concurrent;

// Run only in a child process: a regression must never strand the test host.
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var mode = args[0];
            var scenario = args[1];
            if (scenario.StartsWith("deep", StringComparison.Ordinal)) return RunDeepCycle(mode, scenario);
            foreach (var lifetime in new[] { ServiceLifetime.Transient, ServiceLifetime.Scoped, ServiceLifetime.Singleton })
            {
                IServiceCollection services = new ServiceCollection();
                Type requested;
                string[] expected;
                object? key = null;
                switch (scenario)
                {
                    case "self":
                        services.Add(ServiceDescriptor.Describe(typeof(Self), typeof(Self), lifetime));
                        requested = typeof(Self);
                        expected = [nameof(Self)];
                        break;
                    case "two":
                        services.Add(ServiceDescriptor.Describe(typeof(CycleA), typeof(CycleA), lifetime));
                        services.Add(ServiceDescriptor.Describe(typeof(CycleB), typeof(CycleB), lifetime));
                        requested = typeof(CycleA);
                        expected = [nameof(CycleA), nameof(CycleB)];
                        break;
                    case "long":
                        services.Add(ServiceDescriptor.Describe(typeof(LongA), typeof(LongA), lifetime));
                        services.Add(ServiceDescriptor.Describe(typeof(LongB), typeof(LongB), lifetime));
                        services.Add(ServiceDescriptor.Describe(typeof(LongC), typeof(LongC), lifetime));
                        requested = typeof(LongA);
                        expected = [nameof(LongA), nameof(LongB), nameof(LongC)];
                        break;
                    case "keyed":
                        services.Add(ServiceDescriptor.DescribeKeyed(typeof(KeyedA), "cycle", typeof(KeyedA), lifetime));
                        services.Add(ServiceDescriptor.DescribeKeyed(typeof(KeyedB), "cycle", typeof(KeyedB), lifetime));
                        requested = typeof(KeyedA);
                        expected = [nameof(KeyedA), nameof(KeyedB)];
                        key = "cycle";
                        break;
                    case "factory":
                        services.Add(ServiceDescriptor.Describe(typeof(CycleA), sp => new CycleA(sp.GetRequiredService<CycleB>()), lifetime));
                        services.Add(ServiceDescriptor.Describe(typeof(CycleB), sp => new CycleB(sp.GetRequiredService<CycleA>()), lifetime));
                        requested = typeof(CycleA);
                        expected = [nameof(CycleA), nameof(CycleB)];
                        break;
                    case "any-key":
                        services.Add(ServiceDescriptor.DescribeKeyed(typeof(Leaf), KeyedService.AnyKey,
                            (sp, requestedKey) => sp.GetRequiredKeyedService<Leaf>(requestedKey), lifetime));
                        requested = typeof(Leaf);
                        expected = [nameof(Leaf), "actual-key"];
                        key = "actual-key";
                        break;
                    case "singleton-scope":
                        services.AddSingleton<Leaf>(sp =>
                        {
                            using var nested = sp.CreateScope();
                            return nested.ServiceProvider.GetRequiredService<Leaf>();
                        });
                        requested = typeof(Leaf);
                        expected = [nameof(Leaf)];
                        break;
                    case "decorator":
                        services.Add(ServiceDescriptor.Describe(typeof(IDecorated), typeof(Decorated), lifetime));
                        services.Add(ServiceDescriptor.Describe(typeof(DecoratorDependency), typeof(DecoratorDependency), lifetime));
                        services.Decorate<IDecorated, RecursiveDecorator>();
                        requested = typeof(IDecorated);
                        expected = [nameof(IDecorated), nameof(DecoratorDependency)];
                        break;
                    case "disposable":
                        services.Add(ServiceDescriptor.Describe(typeof(DisposableA), typeof(DisposableA), lifetime));
                        services.Add(ServiceDescriptor.Describe(typeof(DisposableB), typeof(DisposableB), lifetime));
                        requested = typeof(DisposableA);
                        expected = [nameof(DisposableA), nameof(DisposableB)];
                        break;
                    case "enumerable":
                        services.Add(ServiceDescriptor.Describe(typeof(EnumA), typeof(EnumA), lifetime));
                        services.Add(ServiceDescriptor.Describe(typeof(IEnum), _ => new EnumLeaf(), lifetime));
                        services.Add(ServiceDescriptor.Describe(typeof(IEnum), typeof(EnumB), lifetime));
                        requested = typeof(EnumA);
                        expected = [nameof(EnumA), nameof(IEnum)];
                        break;
                    case "any-enumerable":
                        services.Add(ServiceDescriptor.Describe(typeof(AnyEnumRoot), typeof(AnyEnumRoot), lifetime));
                        services.Add(ServiceDescriptor.DescribeKeyed(typeof(IKeyNode), "red", typeof(KeyLeaf), lifetime));
                        services.Add(ServiceDescriptor.DescribeKeyed(typeof(IKeyNode), "blue", typeof(KeyCycle), lifetime));
                        requested = typeof(AnyEnumRoot);
                        expected = mode == "diagnostics" ? [nameof(IKeyNode), "blue"] : [nameof(IKeyNode)];
                        break;
                    case "keyed-builtin":
                        services.Add(ServiceDescriptor.DescribeKeyed(typeof(IServiceProvider), "provider", typeof(KeyedProvider), lifetime));
                        requested = typeof(IServiceProvider);
                        expected = [nameof(IServiceProvider)];
                        key = "provider";
                        break;
                    case "open-generic":
                        services.Add(ServiceDescriptor.Describe(typeof(GenericA<>), typeof(GenericA<>), lifetime));
                        services.Add(ServiceDescriptor.Describe(typeof(GenericB<>), typeof(GenericB<>), lifetime));
                        requested = typeof(GenericA<int>);
                        expected = ["GenericA", "GenericB"];
                        break;
                    case "mixed-generic":
                        services.Add(ServiceDescriptor.Describe(typeof(Mixed), typeof(Mixed), lifetime));
                        services.Add(ServiceDescriptor.Describe(typeof(GenericBridge<>), typeof(GenericBridge<>), lifetime));
                        requested = typeof(Mixed);
                        // Open-generic activations are not instrumented; do not promise a complete chain.
                        expected = [nameof(Mixed)];
                        break;
                    default: throw new ArgumentException("Unknown scenario.");
                }
                services.AddTransient<Recovery>();
                using var provider = mode == "native"
                    ? services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false })
                    : ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
                    {
                        DetectIncorrectUsageOfTransientDisposables = mode == "diagnostics",
                        ValidateOnBuild = false
                    });
                using var scope = provider.CreateScope();
                var resolvers = scenario == "disposable" && lifetime == ServiceLifetime.Transient
                    ? new[] { scope.ServiceProvider }
                    : new IServiceProvider[] { provider, scope.ServiceProvider };
                foreach (var resolver in resolvers)
                {
                    // Repeat failures to check that failed activations restore their state.
                    for (var attempt = 0; attempt < 2; attempt++)
                    {
                        try
                        {
                            if (key == null) resolver.GetRequiredService(requested);
                            else resolver.GetRequiredKeyedService(requested, key);
                        }
                        catch (InvalidOperationException error)
                        {
                            if (!error.Message.Contains("circular dependency") || !error.Message.Contains(" -> ")
                                || expected.Any(part => !error.Message.Contains(part)))
                                throw new InvalidOperationException("Not an actionable cycle error: " + error.Message, error);
                            if (resolver.GetRequiredService<Recovery>() == null) throw new InvalidOperationException("Recovery failed.");
                            continue;
                        }
                        throw new InvalidOperationException("The dependency cycle resolved without an error.");
                    }
                }
            }
            Console.WriteLine("PASS " + args[0] + " " + args[1]);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static int RunDeepCycle(string mode, string scenario)
    {
        // A large graph on a small stack previously crossed native cache locks.
        // Reject it before any dependency factory or constructor can run.
        const int length = 512;
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("CycleProbeTypes"), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("CycleProbeTypes");
        var valid = scenario.Contains("valid");
        var support = scenario.Contains("support");
        var builders = Enumerable.Range(0, length)
            .Select(index => module.DefineType("Deep" + index, TypeAttributes.Public)).ToArray();
        for (var index = 0; index < builders.Length; index++)
        {
            Type next = index + 1 < length ? builders[index + 1]
                : valid ? typeof(Leaf) : support ? typeof(ServiceKeys<Marker>) : builders[0];
            var constructor = builders[index].DefineConstructor(MethodAttributes.Public, CallingConventions.Standard,
                [typeof(ThreadRecorder), next]);
            var body = constructor.GetILGenerator();
            body.Emit(OpCodes.Ldarg_0);
            body.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
            body.Emit(OpCodes.Ret);
        }
        var types = builders.Select(builder => builder.CreateTypeInfo()!.AsType()).ToArray();
        IServiceCollection services = new ServiceCollection();
        var lifetime = scenario.EndsWith("scoped", StringComparison.Ordinal) ? ServiceLifetime.Scoped
            : scenario.EndsWith("singleton", StringComparison.Ordinal) ? ServiceLifetime.Singleton : ServiceLifetime.Transient;
        foreach (var type in types) services.Add(ServiceDescriptor.Describe(type, type, lifetime));
        if (support) services.AddTransient(typeof(object), types[0]);
        services.AddTransient<Leaf>();
        var threads = new ConcurrentDictionary<int, byte>();
        var activations = 0;
        services.AddTransient<ThreadRecorder>(_ =>
        {
            Interlocked.Increment(ref activations);
            threads.TryAdd(Environment.CurrentManagedThreadId, 0);
            return new ThreadRecorder();
        });
        using var provider = mode == "native"
            ? services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false })
            : ServiceProviderFactory.CreateServiceProvider(services, new ExtendedServiceProviderOptions
            {
                DetectIncorrectUsageOfTransientDisposables = mode == "diagnostics",
                ValidateOnBuild = false
            });
        using var scope = provider.CreateScope();
        Exception? failure = null;
        var resolver = new Thread(() =>
        {
            try { scope.ServiceProvider.GetRequiredService(types[0]); }
            catch (Exception error) { failure = error; }
        }, mode == "diagnostics" ? 256 * 1024 : 0);
        resolver.Start();
        if (!resolver.Join(TimeSpan.FromSeconds(10))) throw new TimeoutException("Deep cycle did not return.");
        if (valid && failure == null)
        {
            if (activations != length) throw new InvalidOperationException("An acyclic graph activated unexpected dependencies.");
            Console.WriteLine("PASS " + mode + " " + scenario);
            return 0;
        }
        if (failure is InvalidOperationException error)
        {
            if (!error.Message.Contains("circular dependency") || !error.Message.Contains("Deep0")
                || !error.Message.Contains("Deep511")) throw error;
            if (mode == "diagnostics" && threads.Count != 0)
                throw new InvalidOperationException("Cycle validation activated a dependency factory.");
            Console.WriteLine("PASS " + mode + " " + scenario + "; activation threads=" + threads.Count);
            return 0;
        }
        throw new InvalidOperationException("Deep dependency cycle resolved.");
    }

    public interface IEnum { }
    public sealed class EnumLeaf : IEnum { }
    public sealed class EnumA { public EnumA(IEnumerable<IEnum> values) { } }
    public sealed class EnumB : IEnum { public EnumB(EnumA dependency) { } }
    public interface IKeyNode { }
    public sealed class KeyLeaf : IKeyNode { }
    public sealed class KeyCycle : IKeyNode { public KeyCycle([FromKeyedServices] IKeyNode dependency) { } }
    public sealed class FromAnyKeyAttribute() : FromKeyedServicesAttribute(KeyedService.AnyKey);
    public sealed class AnyEnumRoot { public AnyEnumRoot([FromAnyKey] IEnumerable<IKeyNode> values) { } }
    public sealed class KeyedProvider : IServiceProvider
    {
        public KeyedProvider([FromKeyedServices("provider")] IServiceProvider dependency) { }
        public object? GetService(Type serviceType) => null;
    }
    public sealed class Marker { }
    public interface IDecorated { }
    public sealed class Decorated : IDecorated { }
    public sealed class RecursiveDecorator : IDecorated
    {
        public RecursiveDecorator(IDecorated inner, DecoratorDependency dependency) { }
    }
    public sealed class DecoratorDependency { public DecoratorDependency(IDecorated value) { } }
    public sealed class DisposableA : IDisposable
    {
        public DisposableA(DisposableB value) { }
        public void Dispose() { }
    }
    public sealed class DisposableB : IDisposable
    {
        public DisposableB(DisposableA value) { }
        public void Dispose() { }
    }
    public sealed class Recovery { }
    public sealed class ThreadRecorder { }
    public sealed class Leaf { }
    public sealed class Self { public Self(Self value) { } }
    public sealed class CycleA { public CycleA(CycleB value) { } }
    public sealed class CycleB { public CycleB(CycleA value) { } }
    public sealed class LongA { public LongA(LongB value) { } }
    public sealed class LongB { public LongB(LongC value) { } }
    public sealed class LongC { public LongC(LongA value) { } }
    public sealed class KeyedA { public KeyedA([FromKeyedServices("cycle")] KeyedB value) { } }
    public sealed class KeyedB { public KeyedB([FromKeyedServices("cycle")] KeyedA value) { } }
    public sealed class GenericA<T> { public GenericA(GenericB<T> value) { } }
    public sealed class GenericB<T> { public GenericB(GenericA<T> value) { } }
    public sealed class Mixed { public Mixed(GenericBridge<int> value) { } }
    public sealed class GenericBridge<T> { public GenericBridge(Mixed value) { } }
}
