# Mammoth.Extensions.DependencyInjection

## vNext

### Breaking Changes

- Raise `Microsoft.Extensions.DependencyInjection` to a minimum of **10.0.0**, including the upstream keyed enumerable/open-generic cache identity fix ([dotnet/runtime#113343](https://github.com/dotnet/runtime/pull/113343), [#46](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/46)). Raise its required `Microsoft.Bcl.AsyncInterfaces` dependency to **10.0.0**. Consumers pinning DI 8.x/9.x must upgrade; all existing library target frameworks remain supported.

### Bug Fixes

- Consolidate transient-disposable exception formatting across target frameworks [#24](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/24). Formattable service keys now use invariant culture in the netstandard2.0 library as well as modern targets; preserve message layout, keyed resolution-stack order and factory markers.
- Isolate provider registration, lifetime and key-discovery metadata from external mutation. Preserve public mutable metadata types as compatibility copies while provider helpers and diagnostics use a private snapshot [#28](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/28).
- Preserve container disposal ownership of decorated factory/type services and intermediate decorators, without disposing caller-owned instances or colliding with implementation registrations [#37](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/37).
- Support keyed open-generic provider startup and merge closed/generic-definition keys for all-service discovery and registration/lifetime queries [#38](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/38).
- Keep keyed and unkeyed provider lifetime queries independent, including singleton exemptions in transient-disposable diagnostics [#39](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/39).
- Select a satisfiable DependsOn constructor at resolution time, honor preferred constructors, reject ambiguity, and support optional defaults without constructing rejected dependencies [#23](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/23).

## 0.7.1

### Bug Fixes

- `IServiceCollection` lifetime check methods (`IsTransientServiceRegistered`, `IsScopedServiceRegistered`, `IsSingletonServiceRegistered`, and their keyed variants) now return `false` instead of throwing `InvalidOperationException` when the service is not registered.
- Fixed `ResolutionContext` stack corruption in `PatchForResolutionContextTracking` when a factory delegate throws an exception. Push/Pop calls are now wrapped in `try/finally` for all 4 registration paths (non-keyed/keyed × factory/type).
- Fixed bidirectional `IsAssignableFrom` check in `GetServiceDescriptors` method that incorrectly returned unrelated base-type registrations [#19](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/19).
  - The method now uses unidirectional matching: `serviceType == serviceDescriptor.ServiceType || serviceType.IsAssignableFrom(serviceDescriptor.ServiceType)`.
  - This fixes incorrect lifetime checks in methods like `IsTransientServiceRegistered`, `IsSingletonServiceRegistered`, etc., which depend on `.Last()` to select the correct registration.
- Fixed missing `return` statements in `TryAdd*` overload methods with empty `DependsOn` array, which caused unnecessary reflection calls and potential double-registration attempts [#18](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/18).

## 0.7.0

- Decorators: removed reflection-based proxy creation (`Reflection.Emit`) and allow class decoration [#11](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/11).
  - The `Decorate<TService, TDecorator>` method now supports decorating services registered by concrete class, not just interfaces.
  - Factory-registered services can be decorated without generating dynamic proxy interfaces at runtime.

## 0.6.0

- Added net10.0 support.

### Breaking Changes

- Updated .net assemblies dependencies to 9.0.0.

## 0.5.8

### BugFix

- AssemblyInspector does not allow for proper filter composition [#13](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/13)

## 0.5.7

- added support for .NET 8.0 and .NET 9.0 (previously only netstandard2.0 was supported).

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
