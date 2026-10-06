# Mammoth.Extensions.DependencyInjection

## Build Status

[![.NET](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/actions/workflows/dotnet.yml/badge.svg)](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/actions/workflows/dotnet.yml)

## Introduction

This package offers extensions for the `Microsoft.Extensions.DependencyInjection` library. It requires `Microsoft.Extensions.DependencyInjection` version `10.0.0` or later. This minimum includes the upstream fix for keyed enumerable and open-generic resolver cache identities ([dotnet/runtime#113343](https://github.com/dotnet/runtime/pull/113343), [issue #46](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/46)).

## Installation

Install the package from NuGet:

```bash
dotnet add package Mammoth.Extensions.DependencyInjection
```

The library requires **Microsoft.Extensions.DependencyInjection >=10.0.0** and **Microsoft.Bcl.AsyncInterfaces >=10.0.0**. Consumers pinning DI 8/9 must update their package references; do not suppress a NuGet downgrade conflict. DI 10 contains the [upstream cache identity fix](https://github.com/dotnet/runtime/pull/113343): DI 9.0.0 and 9.0.20 can corrupt reused keyed/unkeyed enumerable accessors after background compilation. Fresh-provider tests cannot establish safety. See the [native reproduction](repro/Issue46.NativeEnumeration/README.md).

| Library compile target | Verified test/consumer runtime |
| --- | --- |
| `netstandard2.0` | .NET Framework application targeting `net472` on Windows |
| `net8.0` | .NET 8 |
| `net9.0` | .NET 9 |
| `net10.0` | .NET 10 |

`netstandard2.0` is a library compatibility target, not a runtime. The DI package upgrade does not require retargeting the verified applications. An SDK supporting your application's C# syntax is still required; the examples use C# 12 collection expressions and primary constructors.

## Usage

Import `Microsoft.Extensions.DependencyInjection` and `Mammoth.Extensions.DependencyInjection`. DependsOn examples also need `Mammoth.Extensions.DependencyInjection.Configuration`; inspectors need `Mammoth.Extensions.DependencyInjection.Inspector`, and HostBuilder examples need `Microsoft.Extensions.Hosting` (and its package). Each example starts with an `IServiceCollection services = new ServiceCollection()` unless it uses a host or `serviceCollection` explicitly. Type definitions and registration blocks in each example belong together.

### Decorator

Use the `Decorator` extension to wrap an existing service with a new implementation without altering the original.

Both interface-based and class-based services can be decorated. The following examples demonstrate how to decorate services.

#### Interface-based decoration

```csharp
public interface ITestService { }

public class TestService : ITestService { }

public class DecoratorService1 : ITestService
{
    private readonly ITestService _service;

    public DecoratorService1(ITestService service)
    {
        _service = service;
    }
}

public class DecoratorService2 : ITestService
{
    private readonly ITestService _service;

    public DecoratorService2(ITestService service)
    {
        _service = service;
    }
}
```

```csharp
services.AddTransient<ITestService, TestService>();
services.Decorate<ITestService, DecoratorService1>(); // innermost decorator
services.Decorate<ITestService, DecoratorService2>(); // outermost decorator
```

#### Class-based decoration

```csharp
public class ConcreteService
{
    public virtual string GetValue() => "ConcreteService";
}

public class ConcreteServiceDecorator : ConcreteService
{
    private readonly ConcreteService _inner;

    public ConcreteServiceDecorator(ConcreteService inner)
    {
        _inner = inner;
    }

    public override string GetValue() => $"Decorated({_inner.GetValue()})";
}
```

```csharp
services.AddTransient<ConcreteService>();
services.Decorate<ConcreteService, ConcreteServiceDecorator>();
```

Decorators preserve Singleton, Scoped and Transient lifetimes and native keys for type, instance and factory registrations. `Decorate<TService, TDecorator>()` wraps only the **last exact service-type registration**, retaining its position. Repeated calls add outer layers; this API does not register open-generic decorators.

Each occurrence in the collection is a separate registration, even when the same `ServiceDescriptor` instance is added more than once. Only the last occurrence is decorated:

```csharp
IServiceCollection services = new ServiceCollection();
var descriptor = ServiceDescriptor.Transient<ITestService, TestService>();
services.Add(descriptor);
services.Add(descriptor);
services.Decorate<ITestService, DecoratorService1>();

using var provider = services.BuildServiceProvider();
var all = provider.GetServices<ITestService>().ToArray(); // TestService, DecoratorService1
var last = provider.GetRequiredService<ITestService>(); // DecoratorService1
```

The same interface example can decorate a keyed registration:

```csharp
services.AddKeyedScoped<ITestService, TestService>("one");
services.Decorate<ITestService, DecoratorService1>();
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var decorated = scope.ServiceProvider.GetRequiredKeyedService<ITestService>("one");
```

Container-created inner services and every decorator are disposed independently. A disposable decorator must dispose its own resources without forwarding disposal to its injected inner service. Caller-supplied instances remain caller-owned. See the [complete ownership example](.agents/skills/mammoth-di/references/usage.md#keyed-decorators-and-caller-owned-instances).

### DependsOn (requires Keyed Services support)

Use the `DependsOn` extensions to register a service that depends on specific instances of other services. For example:

```csharp
public interface ITestService { }

public class TestService : ITestService { }

public class DependentService
{
    private readonly ITestService _service;

    public DependentService(ITestService service)
    {
        _service = service;
    }
}
```

```csharp
services.AddKeyedTransient<ITestService, TestService>("one");
services.AddKeyedTransient<ITestService, TestService>("two");
services.AddTransient<DependentService>(dependsOn: new Dependency[] {
  Parameter.ForKey("service").Eq("one")
});
```

Internally, `DependsOn` creates a factory function to resolve necessary services and build dependent ones.

The current design is limited to some common use case and it's very similar to the one offered by [Castle.Windsor](https://github.com/castleproject), from which we took inspiration:

- Inject a specific instance of a service that will be resolved:

  ```csharp
  services.AddTransient<DependentService>(dependsOn: new Dependency[] {
    Parameter.ForKey("service").Eq("one")
  });
  ```

- Inject a value matching an actual constructor parameter:

  ```csharp
  public class ValueDependentService
  {
      public string Label { get; }

      public ValueDependentService(string label)
      {
          Label = label;
      }
  }
  ```

  ```csharp
  services.AddTransient<ValueDependentService>(dependsOn: new Dependency[] {
    Dependency.OnValue("label", "val1")
  });
  ```

This extension supports Singleton, Scoped, Transient and Keyed registrations. Maps match constructor **parameter names**, not service types; `Eq` accepts string keys and mapped values must be assignable. Unused map names are ignored, so check names carefully.

Non-empty maps choose a constructor at resolution time. A single `[ActivatorUtilitiesConstructor]` constructor must be satisfiable; otherwise the unique longest satisfiable public constructor wins. Equal-length ambiguity fails. Named overrides take precedence over explicit-key `[FromKeyedServices(key)]`, `[ServiceKey]` and ordinary injection; optional defaults apply only to unregistered dependencies, including null and non-null nullable-enum defaults. Rejected constructors do not create dependencies. Empty maps use native DI behavior. Custom providers need ordinary/keyed service probes and keyed resolution. Use explicit keys on the mapped attribute path rather than assuming newer native attribute lookup modes are supported.

For example, use a key attribute and an optional default without registering the optional value:

```csharp
public class AttributeDependentService
{
    public ITestService Service { get; }
    public int Attempts { get; }

    public AttributeDependentService(
        [FromKeyedServices("one")] ITestService service, int attempts = 3)
    {
        Service = service;
        Attempts = attempts;
    }
}
```

```csharp
services.AddKeyedTransient<ITestService, TestService>("one");
services.AddTransient<AttributeDependentService>(dependsOn: new Dependency[] {
    Dependency.OnValue("attempts", 7)
});
```

### Registration Helpers

A set of extension methods provide ways to verify component registrations and manage assemblies for service registration.

#### ServiceCollection

- `GetServiceDescriptors(Type, bool? isKeyedService = null)`: returns assignable service descriptors, with keyed/unkeyed filtering; `null` includes both.
- `IsServiceRegistered`: checks whether the specified service type is registered in the service collection (keyed or not).
- `IsKeyedServiceRegistered`: checks whether the specified service type is registered as keyed in the service collection.
- `IsTransientServiceRegistered`: checks whether the specified service type is registered as transient in the service collection.
- `IsScopedServiceRegistered`: checks whether the specified service type is registered as scoped in the service collection.
- `IsSingletonServiceRegistered`: checks whether the specified service type is registered as singleton in the service collection.
- `IsKeyedTransientServiceRegistered`: checks whether the specified service type is registered as transient in the service collection (keyed services).
- `IsKeyedScopedServiceRegistered`: checks whether the specified service type is registered as scoped in the service collection (keyed services).
- `IsKeyedSingletonServiceRegistered`: checks whether the specified service type is registered as singleton in the service collection (keyed services).

```csharp
services.AddTransient<ITestService, TestService>();
services.AddKeyedScoped<ITestService, TestService>("one");
bool any = services.IsServiceRegistered<ITestService>();
bool transient = services.IsTransientServiceRegistered<ITestService>();
bool keyedScoped = services.IsKeyedScopedServiceRegistered<ITestService>("one");
var descriptors = services.GetServiceDescriptors(typeof(ITestService));
```

#### ServiceProvider

To use these extensions, build the `ServiceProvider` with our custom `ServiceProviderFactory`. It captures authoritative registration metadata even when diagnostics are disabled. Native `BuildServiceProvider()` supports decoration/DependsOn, but does not install this metadata.

```csharp
new HostBuilder().UseServiceProviderFactory(new ServiceProviderFactory(new ExtendedServiceProviderOptions()));

// - or -

var serviceProvider = ServiceProviderFactory.CreateServiceProvider(serviceCollection, new ExtendedServiceProviderOptions());
```

Each build enriches a private copy of the collection. Building repeatedly leaves the caller's descriptors unchanged, and each provider keeps its own registration snapshot. Later registration changes apply only to providers built afterward. Normal DI ownership still applies: caller-supplied singleton instances are shared, and the caller owns their disposal.

```csharp
var services = new ServiceCollection();
services.AddSingleton<ITestService, TestService>();
using var first = ServiceProviderFactory.CreateServiceProvider(services);
// services.Count is still 1; no internal support registrations were appended.

services.AddKeyedSingleton<ITestService, TestService>("later");
using var second = ServiceProviderFactory.CreateServiceProvider(services);
bool firstHasLater = first.IsKeyedServiceRegistered("later");   // false
bool secondHasLater = second.IsKeyedServiceRegistered("later"); // true
```

##### Detect Incorrect Usage of Transient Disposables

Enable detection of transient disposable services resolved by the root scope:

```csharp
new HostBuilder().UseServiceProviderFactory(new ServiceProviderFactory(
  new ExtendedServiceProviderOptions
  {
    DetectIncorrectUsageOfTransientDisposables = true,
    AllowSingletonToResolveTransientDisposables = true,
    ThrowOnOpenGenericTransientDisposable = true,
    DetectIncorrectUsageOfTransientDisposablesExclusionPatterns = ["service", "service2"]
  }));
```

Ordinary implementation-type registrations retain native DI constructor preference and ambiguity rules when diagnostics are enabled, including keyed attributes and optional defaults. The `[ActivatorUtilitiesConstructor]` attribute does not override native type-registration selection; non-empty `DependsOn` maps retain their separate preferred-constructor rules.

Diagnostic messages format service keys that implement `IFormattable` with invariant culture on every library target. For example, a decimal key prints `1234.5` even under `fr-FR`, including when using the `netstandard2.0` library. Keys that supply only their own `ToString()` retain that method's formatting.

Rejected transient factory results remain owned by the root provider until it is disposed. Results already captured by that root are not captured again. Dispose the provider even after a diagnostic failure; use `DisposeAsync` for async-only resources.

WARNING: Use this only in debug/development because it relies on reflection and can affect performance.
Instead of re-implementing a new ServiceProvider from scratch, this approach modifies each ServiceDescriptor to track resolution context and throw exceptions if required.

**Limitations:**

- _Open generic transient disposable services cannot be checked_, a ServiceDescriptor cannot be created with an Open Generic as ServiceType and an ImplementationFactory (we cannot "rewrite" service registrations), so no error is thrown if they are resolved by the root scope.
- _Open generic resolution context cannot be tracked_, a ServiceDescriptor cannot be created with an Open Generic as ServiceType and an ImplementationFactory, so no error is thrown if they are transient and disposable but resolved by the root scope.

Options:

- AllowSingletonToResolveTransientDisposables: Defaults to false. If true, permits the transient when the tracked ancestor chain contains a singleton. Resolution frames are isolated between execution-context branches. A task inherits the ancestry captured when it is scheduled, even if it outlives the originating factory; that inherited singleton ancestor still grants this exemption.
- ThrowOnOpenGenericTransientDisposable: Rejects recognized disposable open-generic type registrations at build time, naming the implementation type in the exception for both keyed and unkeyed registrations. Applies to IDisposable and IAsyncDisposable implementations. When false, these registrations instead produce best-effort warnings through the root ILoggerFactory, if registered (normally via AddLogging).
- DetectIncorrectUsageOfTransientDisposablesExclusionPatterns: list of Regex patterns to exclude services from detection, matching registrations bypass the diagnostic check. All diagnostic flags default to false. Factory-created disposable objects can exist before a diagnostic throws; normal scope disposal remains necessary.

Open-generic startup warnings use the `Mammoth.Extensions.DependencyInjection.ServiceProviderFactory` logging category and event ID 1. They do not create a temporary scope or resolve `ILogger<ServiceProviderFactory>` from DI. A direct registration of that logger alone no longer supplies these warnings; configure standard `ILoggerFactory` logging instead. Failures while resolving the logging factory, creating the logger or writing a warning are ignored, so a warning may be lost. Provider construction, validation and strict open-generic rejection still propagate their errors. Services created while attempting warning delivery remain owned by the returned provider; dispose it normally, using `DisposeAsync` when its services require asynchronous disposal.

###### IsRegistered extension methods

Additional methods for `IServiceProvider`:

- `GetAllServices`: resolves all keyed and non-keyed services of a given service type.
- `IsServiceRegistered`: checks whether the specified service type is registered in the service provider (keyed or non-keyed).
- `IsKeyedServiceRegistered(key)`: checks whether a key is present globally, across service types; it is not a typed key query.
- `IsTransientServiceRegistered`: checks whether the specified service type is registered as transient in the service provider (non keyed services).
- `IsScopedServiceRegistered`: checks whether the specified service type is registered as scoped in the service provider (non keyed services).
- `IsSingletonServiceRegistered`: checks whether the specified service type is registered as singleton in the service provider (non keyed services).
- `IsKeyedTransientServiceRegistered`: checks whether the specified service type is registered as transient in the service provider (keyed services).
- `IsKeyedScopedServiceRegistered`: checks whether the specified service type is registered as scoped in the service provider (keyed services).
- `IsKeyedSingletonServiceRegistered`: checks whether the specified service type is registered as singleton in the service provider (keyed services).

Lifetime checks return false for missing registrations. Keyed and unkeyed identities are independent; provider lookup prefers an exact closed registration before its generic definition within the requested key. Type discovery recognizes closed types from registered generic definitions. Collection assignability matching and provider generic lookup serve different questions.

```csharp
var serviceProvider = ServiceProviderFactory.CreateServiceProvider(
    services, new ExtendedServiceProviderOptions());
using (serviceProvider)
using (var scope = serviceProvider.CreateScope())
{
    bool providerHasService = serviceProvider.IsServiceRegistered<ITestService>();
    bool keyExists = serviceProvider.IsKeyedServiceRegistered("one");
    bool providerHasKeyedScoped = serviceProvider.IsKeyedScopedServiceRegistered<ITestService>("one");
    var all = scope.ServiceProvider.GetAllServices<ITestService>();
}
```

`GetAllServices<T>()` and the Type overload enumerate native unkeyed services first, then merge closed-service and generic-definition keys once per key. Keyed group order is unspecified; native registration order is preserved within each group. Do not use discovery order as a single-service selection rule.

The provider reads a private snapshot: editing the original collection or mutable public `ServiceTypes`, `ServiceKeys`, `ServiceKeys<T>` and `ServiceLifetimes` compatibility copies does not reconfigure queries, enumeration or diagnostics. Keys should have stable equality/hash behavior; arbitrary mutable key objects are not deep-cloned. See the [generic and snapshot example](.agents/skills/mammoth-di/references/usage.md#generic-discovery-and-snapshot-isolation).

#### Inspectors

`AssemblyInspector` inspects assemblies for classes to register.

It is once again inspired by the syntax used in [Castle.Windsor](https://github.com/castleproject) to inspect and register services.

It looks for classes and offers a series of methods that are pretty self explanatory to output one or more `ServiceDescriptor` that
will be registered in the ServiceCollection.

`WithServiceAllInterfaces()` excludes interfaces in `System` and its child namespaces (such as `System.Collections.Generic`), plus interfaces from the assembly named exactly `mscorlib`. Comparisons are ordinal and case-sensitive. `IDisposable` and `IAsyncDisposable` are excluded on every supported runtime, regardless of their defining assembly. Application assembly names such as `Systematic.Contracts` do not affect selection; namespaces such as `Systematic` and `Systems` are not children of `System`. Use `BasedOn<T>().WithServiceBase()` to explicitly register a framework interface.

Implementations with unbound generic parameters are skipped when the selected service is not a generic type definition. This happens before `Configure` is called, so an ordinary marker scan can safely share an assembly with generic implementations:

```csharp
using Mammoth.Extensions.DependencyInjection.Inspector;
using Microsoft.Extensions.DependencyInjection;

IServiceCollection services = new ServiceCollection();
var descriptors = new AssemblyInspector()
    .FromAssemblyContaining<Marker>()
    .BasedOn<IMarker>()
    .WithServiceAllInterfaces() // WithServiceBase() has the same marker-scan behavior.
    .LifestyleTransient();
foreach (var descriptor in descriptors)
{
    services.Add(descriptor);
}

using var provider = services.BuildServiceProvider();
var marker = provider.GetRequiredService<IMarker>(); // Marker

public interface IMarker { }
public class Marker : IMarker { }
public class MarkerGeneric<T> : IMarker { } // Skipped: IMarker cannot supply T.
```

Closed implementations and their closed generic interfaces still register normally. Native open-generic self type registrations are preserved, including `BasedOn(typeof(Repository<>)).WithServiceSelf()` and `WithServiceBase()` for that same concrete definition. The inspector does not infer open-generic interface mappings or extend `BasedOn` assignability. Open-generic registrations require type-based construction; nonempty `DependsOn` maps use factories and remain unsupported for open-generic services.

It supports `DependsOn` for keyed services:

```csharp
public class ServiceWithKeyedDep
{
    public ITestService Service { get; }

    public ServiceWithKeyedDep(ITestService keyedService)
    {
        Service = keyedService;
    }
}
```

```csharp
IServiceCollection serviceCollection = new ServiceCollection();
serviceCollection.AddKeyedSingleton<ITestService, TestService>("one");
var descriptors = new AssemblyInspector()
    .FromAssemblyContaining<ServiceWithKeyedDep>()
    .BasedOn<ServiceWithKeyedDep>()
    .WithServiceSelf()
    .Configure((registration, type) => registration.DependsOn = new Dependency[]
    {
        Parameter.ForKey("keyedService").Eq("one")
    })
    .LifestyleSingleton();
foreach (var descriptor in descriptors)
{
    serviceCollection.Add(descriptor);
}
```

Assign constructor maps through `Configure` before the parameterless lifestyle method. Use precise filters and inspect descriptors when scanning multiple implementations.

## Skill for coding agents

The canonical [mammoth-di skill](.agents/skills/mammoth-di/SKILL.md) teaches application integration, including version checks, disposal, keyed constructor maps, query semantics and diagnostic limits. It follows the [Agent Skills format](https://agentskills.io/specification), with one entrypoint and optional recipes loaded only when needed.

Copy the **whole `mammoth-di` folder**, including `references`, from this repository into your **consumer application's repository**. Choose one supported location per agent; don't maintain duplicate copies for the same agent:

| Agent | Project destination | Invocation |
| --- | --- | --- |
| Codex | `.agents/skills/mammoth-di/` | `$mammoth-di`, or automatic selection from its description; [official discovery docs](https://learn.chatgpt.com/docs/build-skills) |
| Claude Code | `.claude/skills/mammoth-di/` | `/mammoth-di`, or automatic selection; [official skill docs](https://code.claude.com/docs/en/skills) |
| GitHub Copilot | `.github/skills/mammoth-di/`; `.agents/skills` and `.claude/skills` are also supported | Ask to use `mammoth-di`; selection depends on the supported client; [official skill docs](https://docs.github.com/en/copilot/how-tos/copilot-on-github/customize-copilot/customize-cloud-agent/add-skills) |

These project discovery conventions were checked on October 1, 2026. Claude Code's documented project directory differs from Codex's; a standard `SKILL.md` does not imply identical search paths in every agent/client. Other agents can read the folder explicitly if they support Agent Skills or local instructions. Copying instructions does not install Mammoth or change package references. Keep the skill aligned with the library version your application actually uses.

The repository includes no global agent configuration changes or automatic system-wide installer.
