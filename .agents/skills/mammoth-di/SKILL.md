---
name: mammoth-di
description: Use Mammoth.Extensions.DependencyInjection in .NET applications when configuring decorators, named or keyed constructor dependencies, generic discovery, provider metadata queries, or transient-disposable diagnostics.
---

# Use Mammoth DI in an application

Use the application's existing service collection/host and requested lifetimes. This skill concerns consumer integration, not contributing to Mammoth or replacing another DI framework.

## Establish the applicable contract

Read the consumer's PackageReference/central package versions and resolved assets before assuming these guarantees. This skill describes current develop after the disposal, constructor, snapshot, keyed-generic and DI-cache fixes. They are **unreleased** as of October 1, 2026; latest listed stable release is 0.7.1. Don't assume an unversioned package install includes them. When the app is pinned to older behavior, explain the needed version/fix and respect its upgrade constraints rather than silently changing a major dependency.

The fixed baseline requires Microsoft DI >=10.0.0 and Bcl.AsyncInterfaces >=10.0.0. DI 9.0.0/9.0.20 can corrupt reused keyed/unkeyed enumeration after native compilation. Use a release containing the upstream fix; fresh providers are not a workaround. Targets remain netstandard2.0/net8/net9/net10, verified on net472 Windows and .NET 8/9/10. netstandard is a compile contract, not a runtime. Adapt example C# syntax to the application's SDK.

Imports: `Microsoft.Extensions.DependencyInjection`, `Mammoth.Extensions.DependencyInjection`; maps also need `Mammoth.Extensions.DependencyInjection.Configuration`; inspectors need `.Inspector`.

## Choose registration and resolution deliberately

- Decorate with `Decorate<TService,TDecorator>()` after the intended registration. It wraps only the last exact service registration, preserving its key/lifetime/order; repeat calls nest outer layers. Container-created inner services and decorators are disposed independently. Never forward decorator disposal to the injected inner; caller-supplied instances stay caller-owned. This closed generic API does not register open-generic decorators.
- Map **parameter names** using `Parameter.ForKey("store").Eq("primary")` or `Dependency.OnValue("label", "batch")`. `Eq` accepts a string key; other native keys can use a suitable attribute constant or an explicit factory. Values must be assignable; maps are not global registrations. Unused names are ignored, so verify names against the constructor.
- Non-empty DependsOn maps select at resolution: one preferred `[ActivatorUtilitiesConstructor]` must be satisfiable; otherwise the unique longest satisfiable public constructor wins. Equal-length ambiguity fails. Named overrides take precedence over explicit-key `[FromKeyedServices(key)]`/`[ServiceKey]` and ordinary services; optional defaults apply only to unregistered dependencies. Use explicit attribute keys on this mapped path; do not assume newer native attribute lookup modes are supported just because DI 10 is referenced. Rejected constructors' dependencies are not created. Empty maps delegate to native DI. Custom providers need keyed resolution plus ordinary/keyed service probes.
- Build with `ServiceProviderFactory.CreateServiceProvider(services, options)` when using Mammoth provider queries/discovery. For a host, use its `UseServiceProviderFactory` hook rather than constructing a second provider. Native BuildServiceProvider alone does not create Mammoth's authoritative metadata.

For assembly scanning, constrain `AssemblyInspector` filters and assign maps through `Configure((registration, type) => registration.DependsOn = ...)` before the parameterless `LifestyleSingleton`/Scoped/Transient method. Do not invent a lifestyle overload taking a map.

## Avoid misleading queries and diagnostics

`IsServiceRegistered<T>()` includes any key. `IsKeyedServiceRegistered(key)` checks a key globally: it has no generic type parameter. Use typed keyed lifetime queries for lifetime questions; keyed and unkeyed lookups are independent. Exact closed provider lifetime registrations take precedence over generic definitions within the same key; absent lifetime checks return false.

Both `GetAllServices<T>()` and the Type overload include native unkeyed enumeration, then closed/definition keyed groups with each key discovered once. Keyed group order is unspecified. Native single-service precedence and per-group enumeration order still apply. Test concrete membership/identity instead of assuming a global ordering.

The factory's private snapshot isolates queries, enumeration and diagnostics from mutations to public ServiceTypes/ServiceKeys/ServiceKeys<T>/ServiceLifetimes and the original collection. Those public objects remain compatibility copies, not a way to reconfigure a built provider. Use stable key equality/hash behavior; arbitrary key objects aren't deep-cloned.

Enable transient-disposable diagnostics when requested for development. AllowSingletonToResolveTransientDisposables checks the tracked ancestor chain; ThrowOnOpenGenericTransientDisposable rejects recognized open-generic type registrations at startup, otherwise warning requires a logger. Regex exclusions bypass checks. Open-generic runtime construction/chain tracking is limited, and factory-created disposables may exist before a failure. Don't present diagnostics as comprehensive leak detection or a fix for unsafe lifetimes. Resolve scoped/disposable transients inside scopes and verify their disposal. Formattable diagnostic keys use invariant culture; custom ToString-only keys retain their own formatting.

## Recipes and verification

Read [references/usage.md](references/usage.md) only for the relevant constructor/attribute, generic/snapshot, diagnostic, assembly-inspector, or keyed-decorator ownership recipe. Examples are complete console programs intended for a version containing the fixed baseline; adapt their services and registrations to the user's task.

Validate the consumer's actual chosen lifetimes: key selection and parameter values, scoped/singleton reuse, transient freshness, and exactly-once owned disposal with caller-owned instances retained. For mixed generic discovery, include closed plus definition keys and both helper overloads on one reused provider; don't treat cold/fresh-provider successes as compiled-cache evidence. Use the application's available tests/tools and report which package/runtime was actually exercised. No skill instruction grants permission to publish, merge, install global configuration or broaden the application task.
