# Mammoth.Extensions.DependencyInjection

## vNext

- added support for .NET 8.0 and .NET 9.0 (previously only netstandard2.0 was supported).
- **Service Decorators Enhancement** [#11](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/11):
  - **Breaking Performance Improvement**: Eliminated reflection-based proxy creation for factory-registered services, removing `System.Reflection.Emit` overhead.
  - **New Feature**: Added support for decorating concrete classes (not just interfaces). Previously, decorating concrete classes would throw `InvalidOperationException: "Service type X is not an interface."`.
  - **Simplified Implementation**: Decorator logic now works directly with `ServiceDescriptor` information, using `ActivatorUtilities.CreateInstance` for type registrations, direct instance usage for instance registrations, and direct factory invocation for factory registrations.
  - **Full Compatibility**: Maintains complete backward compatibility and support for keyed services.

## 0.5.7

### BugFix

- ServiceProviderFactory / IsRegistered support: Registering a KeyedService with an object key results in System.ArgumentException: Object must be of type String [#9](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/9)

## 0.5.6

- Improved detection of incorrect usage of transient disposable object: InvalidOperationException has more information about the service being resolved (like a resolution context stack, if available).
- Improved detection of incorrect usage of transient disposable object: added an exclusion pattern list for services that should not be checked (like some AspNetCore internal services).

## 0.5.5

- `ServiceProviderFactory`: added a constructor that accepts `ExtendedServiceProviderOptions` to be used in Host Builder initialization like:
  ```csharp
  Host.CreateDefaultBuilder(args).UseServiceProviderFactory(new ServiceProviderFactory(new ExtendedServiceProviderOptions()))
  ```

## 0.5.4

- `ServiceProviderFactory.CreateServiceProvider()` now return `ServiceProvider` instead of `IServiceProvider`.

## 0.5.3

- Improved detection of incorrect usage of transient disposable objects:
  - Log warning when a transient disposable open generic service is registered.

## 0.5.2

- Improved detection of incorrect usage of transient disposable objects:
  - Fixed a bug for keyed service descriptors.
  - Now correctly identifies the resolution for all but open generics.
  - Open generic resolution context cannot be tracked! They will NOT result in errors if they are disposable and registered as transient when resolved by the root scope.
  - Added a new option to throw an exception when an open generic transient disposable service is registered (we cannot track open generics, better to use all closed types to avoid memory leaks).

### Breaking Changes

- Removed `ResolutionContextTrackingServiceProviderDecorator` from compilation (it was bugged and only tracked the root object instead of the entire resolution chain). The implementation file is kept for reference. Resolution context tracking is now implemented with new service decorators that "wrap" the original code and replace the original ones (similar to how the detection of incorrect usage of transient disposables works).

## 0.5.1

- Improved Incorrect Usage of Transient Disposables: we optionally allow Singleton Objects to create Transient Disposable services [#7](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/7).

## 0.5.0

- Detect Incorrect Usage of Transient Disposables services: resolving a transient disposable result in a memory leak [#5](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/5).

## 0.4.0

- Added ServiceProvider extension methods to check if Registered Services are: Transient, Scoped, Singleton [#1](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/1)

### Breaking Changes

- `IServiceCollection` extension methods behavior changes:
  - `IsTransientServiceRegistered`: looks for non-keyed services only.
  - `IsScopedServiceRegistered`: looks for non-keyed services only.
  - `IsSingletonServiceRegistered`: looks for non-keyed services only.
  - Added `IsKeyedTransientServiceRegistered`, `IsKeyedScopedServiceRegistered`, `IsKeyedSingletonServiceRegistered` to check for keyed services.

## 0.3.0

- Improved NuGet package (deterministic, source link).
- Added net9.0 tests.

## 0.2.0

- AssemblyInspector: added Configure() method [#2](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/2).

## Breaking Changes

- Namespace changed for the following classes:

  - `Dependency` -> from `Mammoth.Extensions.DependencyInjection` to `Mammoth.Extensions.DependencyInjection.Configuration`
  - `Parameter` -> from `Mammoth.Extensions.DependencyInjection` to `Mammoth.Extensions.DependencyInjection.Configuration`

- AssemblyInspector lifestyle selectors signature changed; it does not accept "dependsOn" anymore, use the new Configure() method instead:

  ```csharp
  var descriptors = new AssemblyInspector()
    .FromAssemblyContaining<TestService>()
    .BasedOn(typeof(TestService))
    .WithServiceBase()
    .LifestyleTransient(dependsOn: new Dependency[]
    {
        Parameter.ForKey("param").Eq("nonexisting")
    });
  ```
  
  becomes:
  
  ```csharp
  var descriptors = new AssemblyInspector()
    .FromAssemblyContaining<TestService>()
    .BasedOn(typeof(TestService))
    .WithServiceBase()
    .Configure(cfg => cfg.DependsOn = new Dependency[]
    {
        Parameter.ForKey("param").Eq("nonexisting")
    })
    .LifestyleTransient();
  ```

## 0.1.2

- Fixed namespaces.

## 0.1.1

- NuGet Package and GitHub Actions (build, test, publish)

## 0.1.0

Initial Release

- Decorator: support registering decorator pattern.
- DependsOn: register specific dependencies for constructor parameters.
- Registration Checks: allows to check if a service was registered in ServiceCollection and ServiceProvider
- AssemblyInspector: allows to inspect assemblies for types and register them easily.