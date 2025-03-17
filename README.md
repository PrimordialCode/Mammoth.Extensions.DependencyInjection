# Mammoth.Extensions.DependencyInjection

## Build Status

[![.NET](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/actions/workflows/dotnet.yml/badge.svg)](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/actions/workflows/dotnet.yml)

## Introduction

This package provides a set of extensions for the `Microsoft.Extensions.DependencyInjection` package.

It is intended to use with `Microsoft.Extensions.DependencyInjection 8.0.0` and greater, because it relies heavily on
the keyed services support introduced in that version.

## Installation

```bash
dotnet add package Mammoth.Extensions.DependencyInjection
```

## Usage

### Decorator

The `Decorator` extension is used to decorate an existing service with a new implementation. 
This is useful when you want to add new functionality to an existing service without modifying the original implementation.

The following example demonstrates how to decorate an existing service with a new implementation.

The current implementation requires that the sservice to be decorated implements an interface.

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

This extension works with all kinds of Service Descriptors (Singleton, Scoped, Transient, Keyed, etc).

### DependsOn (requires Keyed Services support)

The `DependsOn` registration extensions are used to register a service that depends on specific instances of other services.

The following example demonstrates how to register a service that depends on specific instances of other services.

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

Internally, the `DependsOn` extensions create a factory function that resolves the required services and creates the dependent service instances.

The current syntax is limited to some common use case and it's very similar to the one offered by [Castle.Windsor](https://github.com/castleproject) from which we took inspiration:

- inject a specific instance of a service that will be resolved:

  ```csharp
  services.AddTransient<DependentService>(dependsOn: new Dependency[] {
    Parameter.ForKey("service").Eq("one")
  });
  ```

- inject a value:

  ```csharp
  services.AddTransient<DependentService>(dependsOn: new Dependency[] {
    Dependency.OnValue("dep", "val1")
  });
  ```

This extension works with all kinds of Service Descriptors (Singleton, Scoped, Transient, Keyed, etc).

### Registration Helpers

A series of extensions and helpers methods to check is a component was registered and to 
inspect the assemblies looking for services to be registered.

#### Checks

Helper methods to check if a service was registered or a ServiceDescriptor exists.

##### ServiceCollection

- `GetServiceDescriptors`: returns all the ServiceDescriptors (keyed and non keyed) of a given service type.
- `IsServiceRegistered`: checks whether the specified service type is registered in the service collection (keyed or not).
- `IsKeyedServiceRegistered`: checks whether the specified service type is registered as keyed in the service collection.
- `IsTransientServiceRegistered`
- `IsScopedServiceRegistered`
- `IsSingletonServiceRegistered`
- `IsKeyedTransientServiceRegistered`
- `IsKeyedScopedServiceRegistered`
- `IsKeyedSingletonServiceRegistered`

##### ServiceProvider

To use the following extensions you need to build the ServiceProvider using our `ServiceProviderFactory`.

It will inject some support services that are used to keep tracks of the registered services.

```csharp
new HostBuilder().UseServiceProviderFactory(new ServiceProviderFactory());

// - or -

var serviceProvider = ServiceProviderFactory.CreateServiceProvider(serviceCollection);
```

You can then use the following extensions:

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

The `AssemblyInspector` class is used to inspect the assemblies looking for services to be registered.

It is once again inspired by the syntax used in [Castle.Windsor](https://github.com/castleproject) to inspect and register services.

It looks for classes and offers a series of methods that are pretty self explanatory to output ServiceDescriptos that
will be registered in the ServiceCollection.

It also supports the `DependsOn` registration extensions:

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