# Mammoth.Extensions.DependencyInjection

## vNext

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