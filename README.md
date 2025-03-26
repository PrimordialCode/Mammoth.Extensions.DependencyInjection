# Mammoth.Extensions.DependencyInjection

## Build Status

[![.NET](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/actions/workflows/dotnet.yml/badge.svg)](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/actions/workflows/dotnet.yml)

## Introduction

This package offers extensions for the `Microsoft.Extensions.DependencyInjection` library. It is designed for `Microsoft.Extensions.DependencyInjection` version `8.0.0` or later, using keyed services introduced in that release.

## Installation

```bash
dotnet add package Mammoth.Extensions.DependencyInjection
```

## Usage

### Decorator

Use the `Decorator` extension to wrap an existing service with a new implementation without altering the original.

The following example demonstrates how to decorate an existing service with a new implementation.

**Limitation**

The current implementation requires that the service to be decorated implements an interface.

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
services.Decorate<IService, DecoratorService1>(); // innermost decorator
services.Decorate<IService, DecoratorService2>(); // outermost decorator
```

This extension works with Singleton, Scoped, Transient, Keyed, and other service descriptors.

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
services.AddTransient<ITestService, TestService>("one");
services.AddTransient<ITestService, TestService>("two");
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

- Inject a value:

  ```csharp
  services.AddTransient<DependentService>(dependsOn: new Dependency[] {
    Dependency.OnValue("dep", "val1")
  });
  ```

This extension works with Singleton, Scoped, Transient, Keyed, and other service descriptors.

### Registration Helpers

A set of extension methods provide ways to verify component registrations and manage assemblies for service registration.

#### ServiceCollection

- `GetServiceDescriptors`: returns all the ServiceDescriptors (keyed and non keyed) of a given service type.
- `IsServiceRegistered`: checks whether the specified service type is registered in the service collection (keyed or not).
- `IsKeyedServiceRegistered`: checks whether the specified service type is registered as keyed in the service collection.
- `IsTransientServiceRegistered`: checks whether the specified service type is registered as transient in the service collection.
- `IsScopedServiceRegistered`: checks whether the specified service type is registered as scoped in the service collection.
- `IsSingletonServiceRegistered`: checks whether the specified service type is registered as singleton in the service collection.
- `IsKeyedTransientServiceRegistered`: checks whether the specified service type is registered as transient in the service collection (keyed services).
- `IsKeyedScopedServiceRegistered`: checks whether the specified service type is registered as scoped in the service collection (keyed services).
- `IsKeyedSingletonServiceRegistered`: checks whether the specified service type is registered as singleton in the service collection (keyed services).

#### ServiceProvider

To use these extensions, build the `ServiceProvider` with our custom `ServiceProviderFactory`. It injects services that track transient disposables and registered service usage.

```csharp
new HostBuilder().UseServiceProviderFactory(new ServiceProviderFactory(new ExtendedServiceProviderOptions()));

// - or -

var serviceProvider = ServiceProviderFactory.CreateServiceProvider(serviceCollection, new ExtendedServiceProviderOptions());
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

WARNING: Use this only in debug/development because it relies on reflection and can affect performance.
Instead of re-implementing a new ServiceProvider from scratch, this approach modifies each ServiceDescriptor to track resolution context and throw exceptions if required.

**Limitations:**

- _Open generic transient disposable services cannot be checked_, a ServiceDescriptor cannot be created with an Open Generic as ServiceType and an ImplementationFactory (we cannot "rewrite" service registrations), so no error is thrown if they are resolved by the root scope.
- _Open generic resolution context cannot be tracked_, a ServiceDescriptor cannot be created with an Open Generic as ServiceType and an ImplementationFactory, so no error is thrown if they are transient and disposable but resolved by the root scope.

Options:

- AllowSingletonToResolveTransientDisposables: If false, throws when singleton resolves a transient disposable.
- ThrowOnOpenGenericTransientDisposable: Throws when an open generic transient disposable is registered.
- DetectIncorrectUsageOfTransientDisposablesExclusionPatterns: list of Regex patterns to exclude services from detection, transient disposable services that match any entry in this list will behave be captured if resolved by the root scope.

###### IsRegistered extension methods

Additional methods for `IServiceProvider`:

- `GetAllServices`: resolves all keyed and non-keyed services of a given service type.
- `IsServiceRegistered`: checks whether the specified service type is registered in the service provider (keyed or non-keyed).
- `IsKeyedServiceRegistered`: checks whether the specified service type is registered as keyed in the service provider.
- `IsTransientServiceRegistered`: checks whether the specified service type is registered as transient in the service provider (non keyed services).
- `IsScopedServiceRegistered`: checks whether the specified service type is registered as scoped in the service provider (non keyed services).
- `IsSingletonServiceRegistered`: checks whether the specified service type is registered as singleton in the service provider (non keyed services).
- `IsKeyedTransientServiceRegistered`: checks whether the specified service type is registered as transient in the service provider (keyed services).
- `IsKeyedScopedServiceRegistered`: checks whether the specified service type is registered as scoped in the service provider (keyed services).
- `IsKeyedSingletonServiceRegistered`: checks whether the specified service type is registered as singleton in the service provider (keyed services).

#### Inspectors

`AssemblyInspector` inspects assemblies for classes to register.

It is once again inspired by the syntax used in [Castle.Windsor](https://github.com/castleproject) to inspect and register services.

It looks for classes and offers a series of methods that are pretty self explanatory to output one or more `ServiceDescriptor` that
will be registered in the ServiceCollection.

It supports `DependsOn` for keyed services:

```csharp
serviceCollection.Add(
  new AssemblyInspector()
    .FromAssemblyContaining<ServiceWithKeyedDep>()
    .BasedOn<ServiceWithKeyedDep>()
    .WithServiceSelf()
    .LifestyleSingleton(dependsOn: new Dependency[]
    {
      Parameter.ForKey("keyedService").Eq("one")
    })
);
```