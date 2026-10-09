---
name: mammoth-di
description: Integrate Mammoth.Extensions.DependencyInjection into consuming .NET applications using decorators, named or keyed constructor dependencies, assembly scanning, provider metadata queries, or transient-disposable diagnostics.
---

# Use Mammoth DI in an application

Configure the application's existing service collection or host and preserve its requested lifetimes. Use this skill to register, resolve and manage application services.

Imports: `Microsoft.Extensions.DependencyInjection`, `Mammoth.Extensions.DependencyInjection`; maps also need `Mammoth.Extensions.DependencyInjection.Configuration`; inspectors need `.Inspector`.

Keep explanations and reference links within this skill package. Read only the bundled recipes relevant to the application's task.

## Decorators and disposal ownership

> **Never dispose the injected inner service from a decorator.** Do not forward `Dispose()` or `DisposeAsync()` to it. DI disposes each container-created original and decorator independently; forwarding disposal can dispose a layer twice or prematurely dispose a caller-owned instance.

Decorators release only resources they create and own themselves. If a decorator owns no resources, it need not implement disposal merely because its inner service does. Instances supplied through instance registration overloads remain caller-owned; objects returned by factories are owned by DI. Use async scope/provider disposal when any layer is async-only.

See the [async ownership example](references/integration.md#async-decorators-own-only-their-resources) for a decorator that owns an async resource.

Call `Decorate<TService,TDecorator>()` immediately after the intended registration. It wraps only the last exact service-type registration, preserving its key, lifetime and enumeration position. It has no separate key-selection argument. Repeated calls add outer layers. Closed generic services are supported; open-generic decorator definitions are not. See the [ownership recipe](references/usage.md#keyed-decorators-and-caller-owned-instances) for keyed layers and caller-supplied instances.

## Constructor dependencies and provider setup

- Map **parameter names** using `Parameter.ForKey("store").Eq("primary")` or `Dependency.OnValue("label", "batch")`. `Eq` accepts a string key; other native keys can use a suitable attribute constant or an explicit factory. Values must be assignable; maps are not global registrations. Unused names are ignored, so verify names against the constructor.
- Non-empty DependsOn maps choose constructors at resolution: a single preferred `[ActivatorUtilitiesConstructor]` must be satisfiable; otherwise the unique longest satisfiable public constructor wins. Equal-length ambiguity fails. Named overrides take precedence over attributes and ordinary injection; optional defaults apply only to unregistered dependencies. Empty maps use native DI constructor rules.
- `[FromKeyedServices(key)]` selects an explicit key; `[FromKeyedServices]` inherits the current key; `[FromKeyedServices(null)]` uses unkeyed lookup. `[ServiceKey]` injects a non-null current key. Prefer one binding attribute per parameter to avoid dependence on attribute order. Resolve a concrete key for inherited dependencies on `AnyKey` registrations.
- Build with `ServiceProviderFactory.CreateServiceProvider(services, options)` for Mammoth provider queries, discovery and diagnostics. For a host, use `UseServiceProviderFactory(new ServiceProviderFactory(options))` on its existing builder. Native `BuildServiceProvider()` supports decoration and DependsOn, but does not install Mammoth registration metadata.

For assembly scanning, constrain `AssemblyInspector` filters and assign maps through `Configure((registration, type) => registration.DependsOn = ...)` before the parameterless `LifestyleSingleton`/Scoped/Transient method. Non-empty maps cannot construct open-generic services.

For application setup, use the bundled [named mapping](references/integration.md#named-mapping-and-scoped-reuse), [keyed attributes](references/integration.md#explicit-inherited-and-unkeyed-dependencies) or [host integration](references/integration.md#use-the-existing-host) example.

## Registration queries and discovery

`IsServiceRegistered<T>()` includes any key. `IsKeyedServiceRegistered(key)` checks a key globally: it has no generic type parameter. Use typed keyed lifetime queries for lifetime questions; keyed and unkeyed lookups are independent. Exact closed provider lifetime registrations take precedence over generic definitions within the same key; absent lifetime checks return false.

Both `GetAllServices<T>()` and the Type overload include native unkeyed enumeration, then closed/definition keyed groups with each key discovered once. Keyed group order is unspecified. Native single-service precedence and per-group enumeration order still apply. Test concrete membership/identity instead of assuming a global ordering.

Treat `ServiceTypes`, `ServiceKeys`, `ServiceKeys<T>` and `ServiceLifetimes` as metadata copies. Mutating them or the original collection does not reconfigure a built provider. Use keys with stable equality and hash behavior. `GetAllServices` does not enumerate `AnyKey` as a concrete key.

## Transient-disposable diagnostics

Use diagnostics in development/debug builds when requested. Set both `DetectIncorrectUsageOfTransientDisposables = true` and `ValidateOnBuild = true`; omitting build validation throws `ArgumentException` during provider creation. Recognized disposable transients resolved from the root are rejected. Resolve them inside scopes and dispose those scopes.

`AllowSingletonToResolveTransientDisposables` permits singleton capture when explicitly intended. `ThrowOnOpenGenericTransientDisposable` rejects recognized open-generic disposable type registrations at startup; otherwise warnings need a registered `ILoggerFactory`. Exclusion regexes match the registered public service type and its decorator layers; separately registered dependencies remain checked.

Native build validation catches invalid closed type graphs, including cycles and unused registrations. Factories and open generics retain native validation limits; diagnostics do not add runtime cycle detection or comprehensive leak detection. Factory-created disposables may exist before a diagnostic fails, so normal scope disposal is still required.

## Recipes and verification

With build validation enabled, `AnyKey` implementation types with inherited-key dependencies may fail startup validation under the wildcard key; use concrete registrations in that case.

Read [references/usage.md](references/usage.md) for the relevant constructor, generic discovery, diagnostic, assembly-inspector or keyed-decorator recipe. Adapt its complete console examples to the application's services and registrations.

Verify the application's key selection, mapped values, requested lifetimes and disposal ownership. For decorators, check each owned layer is disposed exactly once and caller-supplied instances remain usable after provider disposal. Use the application's available tests and report what was actually exercised.
