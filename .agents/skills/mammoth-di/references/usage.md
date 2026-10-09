# Consumer recipes

Each C# block is a separate complete console program. Use a local develop build or a released package containing the fixed baseline, plus the C# 12-or-newer SDK syntax used here. They require only Mammoth and its DI dependencies. They do not install a package, configure an agent, or build an additional host provider automatically.

## Constructor attributes and optional defaults

Named maps override parameter attributes. A preferred constructor can beat a longer registered candidate, without instantiating that candidate's dependencies.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Mammoth.Extensions.DependencyInjection;
using Mammoth.Extensions.DependencyInjection.Configuration;

IServiceCollection services = new ServiceCollection();
services.AddKeyedSingleton<IStore>("primary", new Store("primary"));
services.AddKeyedSingleton<IStore>("backup", new Store("backup"));
int probeCreations = 0;
services.AddTransient<Probe>(_ => { probeCreations++; return new Probe(); });
services.AddKeyedScoped<Worker>("worker", [
    Parameter.ForKey("store").Eq("primary"),
    Dependency.OnValue("label", "mapped")
]);
using var provider = ServiceProviderFactory.CreateServiceProvider(services);
using var scope = provider.CreateScope();
var worker = scope.ServiceProvider.GetRequiredKeyedService<Worker>("worker");
if (worker.Store.Name != "primary" || !Equals(worker.OwnerKey, "worker")
    || worker.Label != "mapped" || worker.Count != 7 || probeCreations != 0)
    throw new Exception("Constructor mapping contract failed.");
Console.WriteLine("PASS: constructor map, attributes, preference and optional default");

public interface IStore { string Name { get; } }
public sealed class Store(string name) : IStore { public string Name { get; } = name; }
public sealed class Probe { }
public sealed class Worker
{
    public IStore Store { get; }
    public object? OwnerKey { get; }
    public string Label { get; }
    public int Count { get; }
    [ActivatorUtilitiesConstructor]
    public Worker([FromKeyedServices("backup")] IStore store,
        [ServiceKey] object? ownerKey, string label, int count = 7)
    { Store = store; OwnerKey = ownerKey; Label = label; Count = count; }
    public Worker([FromKeyedServices("backup")] IStore store,
        [ServiceKey] object? ownerKey, string label, Probe probe, int count = 7)
        : this(store, ownerKey, label, count) { }
}
```

## Generic discovery and snapshot isolation

Closed and open-generic registrations coexist in native enumeration; exact closed registrations select the lifetime for a single-service query. Mutating public discovery copies cannot reconfigure the built provider. Don't rely on keyed group sorting.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Mammoth.Extensions.DependencyInjection;

IServiceCollection services = new ServiceCollection();
services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
services.AddSingleton<IRepository<string>, ClosedRepository>();
services.AddKeyedScoped(typeof(IRepository<>), "backup", typeof(BackupRepository<>));
services.AddKeyedSingleton<IRepository<string>, ClosedBackupRepository>("backup");
using var provider = ServiceProviderFactory.CreateServiceProvider(services);
provider.GetRequiredService<ServiceTypes>().Clear();
provider.GetRequiredService<ServiceKeys>().Clear();
provider.GetRequiredService<ServiceKeys<IRepository<string>>>().Clear();
provider.GetRequiredService<ServiceLifetimes>().Add(typeof(IRepository<string>), ServiceLifetime.Transient);
using var scope = provider.CreateScope();
var sp = scope.ServiceProvider;
if (!sp.IsServiceRegistered<IRepository<int>>() || !sp.IsKeyedServiceRegistered("backup")
    || !sp.IsSingletonServiceRegistered<IRepository<string>>()
    || !sp.IsKeyedScopedServiceRegistered<IRepository<int>>("backup")
    || !sp.IsKeyedSingletonServiceRegistered<IRepository<string>>("backup"))
    throw new Exception("Snapshot lifetime/discovery contract failed.");
for (int i = 0; i < 100; i++)
{
    var generic = sp.GetAllServices<IRepository<string>>().Select(x => x.Marker).OrderBy(x => x);
    var typed = sp.GetAllServices(typeof(IRepository<string>)).Cast<IRepository<string>>()
        .Select(x => x.Marker).OrderBy(x => x);
    if (!generic.SequenceEqual(new[] { 0, 1, 2, 3 }) || !typed.SequenceEqual(new[] { 0, 1, 2, 3 }))
        throw new Exception("Generic membership contract failed.");
}
Console.WriteLine("PASS: generic discovery and protected metadata");

public interface IRepository<T> { int Marker { get; } }
public sealed class Repository<T> : IRepository<T> { public int Marker => 0; }
public sealed class ClosedRepository : IRepository<string> { public int Marker => 1; }
public sealed class BackupRepository<T> : IRepository<T> { public int Marker => 2; }
public sealed class ClosedBackupRepository : IRepository<string> { public int Marker => 3; }
```

These consumer loops are integration checks. The library's native-only regression separately observes actual compiled accessor replacement; a loop count or a fresh provider is not a compilation-completion guarantee.

## Root and scope diagnostics

A recognized closed disposable transient is rejected at the root and is allowed inside a scope. The scope owns disposal. This example uses a type registration so the root check occurs before creating that disposable; factory registrations can create an instance before the diagnostic fails.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Mammoth.Extensions.DependencyInjection;
using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
IServiceCollection services = new ServiceCollection();
services.AddKeyedTransient<Disposable>(1234.5m);
using var provider = ServiceProviderFactory.CreateServiceProvider(services,
    new ExtendedServiceProviderOptions { DetectIncorrectUsageOfTransientDisposables = true, ValidateOnBuild = true });
bool rejected = false;
try { provider.GetRequiredKeyedService<Disposable>(1234.5m); }
catch (InvalidOperationException error)
{
    rejected = error.Message.Contains("ServiceKey: 1234.5,");
}
if (!rejected) throw new Exception("Root diagnostic contract failed.");
Disposable result;
using (var scope = provider.CreateScope())
    result = scope.ServiceProvider.GetRequiredKeyedService<Disposable>(1234.5m);
if (result.DisposeCount != 1) throw new Exception("Scope ownership contract failed.");
Console.WriteLine("PASS: root rejection, invariant key and scope disposal");

public sealed class Disposable : IDisposable
{
    public int DisposeCount { get; private set; }
    public void Dispose() => DisposeCount++;
}
```

## Assembly inspection

Keep discovery constrained to intended concrete types. Inspector DependsOn maps use the same parameter names and resolution-time constructor rules.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Mammoth.Extensions.DependencyInjection;
using Mammoth.Extensions.DependencyInjection.Configuration;
using Mammoth.Extensions.DependencyInjection.Inspector;

IServiceCollection services = new ServiceCollection();
var descriptors = new AssemblyInspector().FromAssemblyContaining<Scanned>()
    .BasedOn<Scanned>().WithServiceSelf()
    .Configure((registration, _) => registration.DependsOn = [Dependency.OnValue("label", "scanned")])
    .LifestyleSingleton();
foreach (var descriptor in descriptors) services.Add(descriptor);
using var provider = ServiceProviderFactory.CreateServiceProvider(services);
if (provider.GetRequiredService<Scanned>().Label != "scanned")
    throw new Exception("Inspector mapping contract failed.");
Console.WriteLine("PASS: constrained inspector registration");

public sealed class Scanned(string label) { public string Label { get; } = label; }
```

## Keyed decorators and caller-owned instances

> **Decorators must not dispose their injected inner service.** This includes both `Dispose()` and `DisposeAsync()`. DI owns container-created originals and every decorator independently. A decorator releases only resources it creates and owns itself; it never forwards disposal to `Inner`.

Decorate the intended keyed registration immediately, before adding another registration of that service type. Native original type graphs are validated before activation; enabling `ValidateOnBuild` can move graph errors to startup. Factory originals and non-empty maps retain their own activation policies. A caller-supplied singleton registered with the instance overload remains caller-owned and is explicitly disposed by the caller after the provider releases its wrapper. A factory returning that same instance would instead make DI track its disposal.

The recipe below verifies exactly-once disposal for each scoped layer and preservation of caller ownership. Forwarding `Inner.Dispose()` from `StoreDecorator` would break these assertions. For an async decorator, release only its own async resources in `DisposeAsync()` and use async scope/provider disposal when any layer is async-only. The [detailed design](../../../../docs/decorator-architecture.md) includes a complete async scope example and explains the native original registration and scoped holder.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Mammoth.Extensions.DependencyInjection;

IServiceCollection services = new ServiceCollection();
services.AddKeyedScoped<IStore, Store>("primary");
services.Decorate<IStore, StoreDecorator>();
services.Decorate<IStore, StoreDecorator>();
services.AddKeyedScoped<IStore, Store>("backup"); // remains unwrapped
var caller = new Store();
services.AddKeyedSingleton<IStore>("caller", caller);
services.Decorate<IStore, StoreDecorator>();
using var provider = ServiceProviderFactory.CreateServiceProvider(services);
var scope = provider.CreateScope();
var sp = scope.ServiceProvider;
var outer = (StoreDecorator)sp.GetRequiredKeyedService<IStore>("primary");
var middle = (StoreDecorator)outer.Inner;
var inner = (Store)middle.Inner;
var callerWrapper = (StoreDecorator)sp.GetRequiredKeyedService<IStore>("caller");
if (!ReferenceEquals(outer, sp.GetRequiredKeyedService<IStore>("primary"))
    || sp.GetRequiredKeyedService<IStore>("backup") is not Store
    || !ReferenceEquals(caller, callerWrapper.Inner))
    throw new Exception("Keyed decoration contract failed.");
scope.Dispose();
if (outer.DisposeCount != 1 || middle.DisposeCount != 1 || inner.DisposeCount != 1)
    throw new Exception("Scoped layers must be disposed once each.");
provider.Dispose();
if (caller.DisposeCount != 0 || callerWrapper.DisposeCount != 1)
    throw new Exception("Caller ownership must be retained.");
caller.Dispose();
Console.WriteLine("PASS: keyed decorator layers and caller ownership");

public interface IStore : IDisposable { int DisposeCount { get; } }
public sealed class Store : IStore
{
    public int DisposeCount { get; private set; }
    public void Dispose() => DisposeCount++;
}
public sealed class StoreDecorator(IStore inner) : IStore
{
    public IStore Inner { get; } = inner;
    public int DisposeCount { get; private set; }
    public void Dispose() => DisposeCount++; // Own cleanup only; never dispose Inner.
}
```
