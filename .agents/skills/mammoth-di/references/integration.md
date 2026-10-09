# Application integration examples

Each C# block is a separate complete console program. Enable implicit usings and adapt collection expressions and primary constructors to the application's SDK. The host example also requires `Microsoft.Extensions.Hosting` in the application.

## Named mapping and scoped reuse

Maps bind constructor parameter names, including an ordinary service's dependency on a keyed registration. The mapped service keeps the lifetime selected by `AddScoped`; each scope gets its own instance.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Mammoth.Extensions.DependencyInjection;
using Mammoth.Extensions.DependencyInjection.Configuration;

IServiceCollection services = new ServiceCollection();
services.AddKeyedSingleton<IStore>("primary", new Store());
services.AddScoped<Worker>([
    Parameter.ForKey("store").Eq("primary"),
    Dependency.OnValue("label", "batch")
]);
using var provider = ServiceProviderFactory.CreateServiceProvider(services);
using var firstScope = provider.CreateScope();
using var secondScope = provider.CreateScope();
var first = firstScope.ServiceProvider.GetRequiredService<Worker>();
var again = firstScope.ServiceProvider.GetRequiredService<Worker>();
var second = secondScope.ServiceProvider.GetRequiredService<Worker>();
if (first.Label != "batch" || !ReferenceEquals(first, again)
    || ReferenceEquals(first, second) || !ReferenceEquals(first.Store, second.Store))
    throw new Exception("Mapped values and scoped reuse must be preserved.");
Console.WriteLine("PASS: named mapping and scoped reuse");

public interface IStore { }
public sealed class Store : IStore { }
public sealed class Worker(IStore store, string label)
{
    public IStore Store { get; } = store;
    public string Label { get; } = label;
}
```

## Explicit, inherited and unkeyed dependencies

An explicit key selects a particular registration. A parameterless `[FromKeyedServices]` inherits the resolving service's key, while `[FromKeyedServices(null)]` selects the ordinary registration. `[ServiceKey]` exposes the current non-null key. These attributes can be used without a DependsOn map.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Mammoth.Extensions.DependencyInjection;

IServiceCollection services = new ServiceCollection();
services.AddSingleton<IStore>(new Store("default"));
services.AddKeyedSingleton<IStore>("primary", new Store("primary"));
services.AddKeyedSingleton<IStore>("backup", new Store("backup"));
services.AddKeyedScoped<Worker>("primary");
using var provider = ServiceProviderFactory.CreateServiceProvider(services);
using var scope = provider.CreateScope();
var worker = scope.ServiceProvider.GetRequiredKeyedService<Worker>("primary");
if (worker.Explicit.Name != "backup" || worker.Inherited.Name != "primary"
    || worker.Default.Name != "default" || worker.Key != "primary")
    throw new Exception("Constructor attributes must select the intended bindings.");
Console.WriteLine("PASS: explicit, inherited and unkeyed dependencies");

public interface IStore { string Name { get; } }
public sealed class Store(string name) : IStore { public string Name { get; } = name; }
public sealed class Worker(
    [FromKeyedServices("backup")] IStore explicitStore,
    [FromKeyedServices] IStore inheritedStore,
    [FromKeyedServices(null)] IStore defaultStore,
    [ServiceKey] string key)
{
    public IStore Explicit { get; } = explicitStore;
    public IStore Inherited { get; } = inheritedStore;
    public IStore Default { get; } = defaultStore;
    public string Key { get; } = key;
}
```

## Use the existing host

Configure Mammoth's factory on the application's host builder and add registrations to its existing service collection. Resolve scoped services through a scope from the built host. For an application that already has a builder, apply the factory and registration calls to that builder.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mammoth.Extensions.DependencyInjection;

var builder = Host.CreateDefaultBuilder();
builder.UseServiceProviderFactory(new ServiceProviderFactory(
    new ExtendedServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }));
builder.ConfigureServices((_, services) => services.AddScoped<Worker>());
using var host = builder.Build();
using var scope = host.Services.CreateScope();
var worker = scope.ServiceProvider.GetRequiredService<Worker>();
if (!scope.ServiceProvider.IsScopedServiceRegistered<Worker>()
    || !ReferenceEquals(worker, scope.ServiceProvider.GetRequiredService<Worker>()))
    throw new Exception("The host must provide Mammoth metadata and scoped reuse.");
Console.WriteLine("PASS: host integration and registration queries");

public sealed class Worker { }
```

## Async decorators own only their resources

The decorator below creates and owns a private async resource. Its `DisposeAsync()` releases that resource and never calls `Inner.DisposeAsync()`. The DI scope disposes both the decorator and original service independently. An async-only layer requires async scope and provider disposal.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Mammoth.Extensions.DependencyInjection;

IServiceCollection services = new ServiceCollection();
services.AddScoped<IWorker, Worker>();
services.Decorate<IWorker, BufferedWorker>();
await using var provider = ServiceProviderFactory.CreateServiceProvider(services);
Worker original;
BufferedWorker decorator;
await using (var scope = provider.CreateAsyncScope())
{
    decorator = (BufferedWorker)scope.ServiceProvider.GetRequiredService<IWorker>();
    original = (Worker)decorator.Inner;
    decorator.Run();
}
if (original.DisposeCount != 1 || decorator.DisposeCount != 1
    || decorator.ResourceDisposeCount != 1)
    throw new Exception("Every owned layer and private resource must be disposed once.");
Console.WriteLine("PASS: independent async disposal and private resource ownership");

public interface IWorker { void Run(); }
public sealed class Worker : IWorker, IAsyncDisposable
{
    public int DisposeCount { get; private set; }
    public void Run() { }
    public ValueTask DisposeAsync() { DisposeCount++; return default; }
}
public sealed class BufferedWorker(IWorker inner) : IWorker, IAsyncDisposable
{
    private readonly AsyncBuffer _buffer = new(); // Created and owned by this decorator.
    public IWorker Inner { get; } = inner;
    public int DisposeCount { get; private set; }
    public int ResourceDisposeCount => _buffer.DisposeCount;
    public void Run() { _buffer.Write(); Inner.Run(); }
    public async ValueTask DisposeAsync()
    {
        await _buffer.DisposeAsync();
        DisposeCount++;
        // Never dispose Inner; the DI scope owns it.
    }
}
public sealed class AsyncBuffer : IAsyncDisposable
{
    private readonly MemoryStream _stream = new();
    public int DisposeCount { get; private set; }
    public void Write() => _stream.WriteByte(1);
    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync();
        DisposeCount++;
    }
}
```
