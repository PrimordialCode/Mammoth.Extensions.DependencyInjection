# Mammoth.Extensions.DependencyInjection

## vNext

### Breaking Changes

- Open-generic startup warnings now use the root `ILoggerFactory` rather than a directly registered `ILogger<ServiceProviderFactory>`. Configure standard logging (for example, `AddLogging`) to receive them. Warning-delivery failures no longer fail provider creation; warnings are best-effort [#90](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/90).

- Raise `Microsoft.Extensions.DependencyInjection` to a minimum of **10.0.0**, including the upstream keyed enumerable/open-generic cache identity fix ([dotnet/runtime#113343](https://github.com/dotnet/runtime/pull/113343), [#46](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/46)). Raise its required `Microsoft.Bcl.AsyncInterfaces` dependency to **10.0.0**. Consumers pinning DI 8.x/9.x must upgrade; all existing library target frameworks remain supported.

### Bug Fixes

- Preserve original constructor exception identity and stack traces in `DependsOn` activation by sharing reflection-wrapper unwrapping with contextual keyed activation, while leaving dependency-resolution exceptions unchanged [#83](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/83).

- Restore typed resolution in `GetAllServices<T>()` for value-type services such as `int`, preserving explicit enumerable registrations, snapshot-backed key merging, AnyKey exclusion and private decorator filtering [#85](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/85).

- Preserve exact public-service diagnostic exclusions through every private decorator layer, including repeated keyed decoration, while retaining unrelated dependency checks and container disposal ownership [#91](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/91).

- Normalize optional nullable-enum defaults in mapped activation and contextual keyed decorators, preserving null defaults, named overrides and registered-service precedence [#82](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/82).

- Skip incompatible open-generic implementations in ordinary assembly scans before configuration, preventing invalid marker/interface descriptors while preserving closed implementations and native open-generic self type registrations [#87](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/87).

- Keep fallback `ServiceKeys<T>` metadata empty without activating unrelated application `object` services or creating scoped-to-singleton dependencies, while preserving mutable compatibility collections and snapshot-backed discovery [#86](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/86).

- Remove the temporary startup-warning scope and return the provider even when optional warning delivery fails, preserving ownership of logging dependencies until provider disposal and avoiding forced synchronous disposal of async-only scoped loggers [#90](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/90).

- Preserve native DI constructor preference and parameter-type ambiguity rules when transient-disposable diagnostics instrument ordinary type registrations, including keyed context, optional defaults, null factory results and disposal ownership [#88](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/88).

- Build provider metadata on a private collection copy so repeated or failed builds leave caller registrations unchanged, avoid accumulating support services, and keep each provider's discovery and compatibility metadata independent [#64](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/64).

- Decorate the last registration occurrence when the same ServiceDescriptor instance is added more than once, preserving keyed and unkeyed registration order, repeated decorator layers, lifetimes and disposal ownership [#63](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/63).

- Preserve implementation-specific rejection messages for unkeyed open-generic transient disposable registrations, including IDisposable and IAsyncDisposable implementations [#62](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/62).

- Stop local package builds immediately after a failed native command and preserve its exit code, preventing stale-output packaging after failed validation [#61](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/61).

- Apply precise System namespace boundaries and the exact mscorlib assembly name when selecting framework interfaces, preserving application contracts from Systematic assemblies and excluding IAsyncDisposable consistently across runtimes [#60](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/60).

- Update the private SourceLink build dependency to 10.0.401, resolving patched Microsoft.Build.Tasks.Git for CVE-2026-62900 [#65](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/65).

- Preserve native ValidateOnBuild graph validation when transient-disposable diagnostics are enabled, without activating user services during validation [#59](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/59).

- Isolate transient-disposable diagnostic resolution frames across execution-context branches, preserving inherited ancestry and nested exception cleanup [#58](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/58).

- Use the active descriptor lifetime for diagnostic singleton exemptions when duplicate registrations have different lifetimes, preserving last-registration-wins metadata queries [#57](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/57).

- Retain root disposal ownership of rejected transient factory results in diagnostic mode, including async-only disposables, without recapturing results already owned by the root [#56](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/56).
- Preserve native explicit IEnumerable registrations in GetAllServices, including instance/factory precedence and keyed mixtures, while retaining private decorator filtering and AnyKey exclusion [#55](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/55).
- Exclude the AnyKey sentinel from GetAllServices per-key enumeration, avoiding duplicate concrete results and transient activations while preserving wildcard metadata, fallback resolution and registration multiplicity [#54](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/54).
- Respect namespace boundaries and exact global-namespace selection in AssemblyInspector; explicitly include all namespaces when children of the global namespace are requested [#53](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/53).
- Use value equality for collection key-only registration queries, matching typed helpers for boxed values, equal strings and custom keys [#52](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/52).
- Preserve ServiceKey injection and inherited/explicit/null dependency lookup through keyed decorators, diagnostics and DependsOn activation, retaining named overrides and disposal ownership [#51](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/51).
- Preserve actual requested keys and per-key inner lifetime caches through AnyKey decorator layers, retaining container disposal ownership [#50](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/50).
- Consolidate transient-disposable exception formatting across target frameworks [#24](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/24). Formattable service keys now use invariant culture in the netstandard2.0 library as well as modern targets; preserve message layout, keyed resolution-stack order and factory markers.
- Isolate provider registration, lifetime and key-discovery metadata from external mutation. Preserve public mutable metadata types as compatibility copies while provider helpers and diagnostics use a private snapshot [#28](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/28).
- Preserve container disposal ownership of decorated factory/type services and intermediate decorators, without disposing caller-owned instances or colliding with implementation registrations [#37](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/37).
- Support keyed open-generic provider startup and merge closed/generic-definition keys for all-service discovery and registration/lifetime queries [#38](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/38).
- Keep keyed and unkeyed provider lifetime queries independent, including singleton exemptions in transient-disposable diagnostics [#39](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/39).
- Select a satisfiable DependsOn constructor at resolution time, honor preferred constructors, reject ambiguity, and support optional defaults without constructing rejected dependencies [#23](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/23).

### Documentation

- Refresh consumer guidance for current provider guarantees and add the portable `mammoth-di` Agent Skill with validated application recipes and agent-specific discovery instructions.

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
