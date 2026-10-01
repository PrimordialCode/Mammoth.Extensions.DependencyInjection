# Mammoth.Extensions.DependencyInjection

[![.NET](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/actions/workflows/dotnet.yml/badge.svg)](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/actions/workflows/dotnet.yml)

Extensions for Microsoft DI: service decorators, constructor dependencies selected by parameter name/key, assembly registration, registration/lifetime queries, and optional transient-disposable diagnostics.

## Version and installation

This README describes **current `develop` / the next release**. As checked on October 1, 2026, the latest GitHub release and listed stable NuGet package is **0.7.1**. The disposal, constructor-selection, snapshot, generic-query and DI-cache fixes below are in `develop` and [the vNext changelog](Changelog.md); installing 0.7.1 does not include them. Select a released version containing these changes when it becomes available, or use a local build of `develop` for evaluation.

```bash
dotnet add package Mammoth.Extensions.DependencyInjection --version 0.7.1
```

That command installs the existing release, **not** the unreleased behavior documented here. Check your resolved version in `obj/project.assets.json` and its release notes before relying on the new guarantees.

The next release requires **Microsoft.Extensions.DependencyInjection >=10.0.0** and **Microsoft.Bcl.AsyncInterfaces >=10.0.0**. Consumers pinning DI 8/9 must update their package references; do not suppress a NuGet downgrade conflict. DI 10 contains the [upstream cache identity fix](https://github.com/dotnet/runtime/pull/113343): DI 9.0.0 and 9.0.20 can corrupt reused keyed/unkeyed enumerable accessors after background compilation. Fresh-provider tests cannot establish safety. See the [native reproduction](repro/Issue46.NativeEnumeration/README.md).

| Library compile target | Verified test/consumer runtime |
| --- | --- |
| `netstandard2.0` | .NET Framework application targeting `net472` on Windows |
| `net8.0` | .NET 8 |
| `net9.0` | .NET 9 |
| `net10.0` | .NET 10 |

`netstandard2.0` is a library compatibility target, not a runtime. The DI package upgrade does not require retargeting the verified applications. An SDK supporting your application's C# syntax is still required; the examples use C# 12 collection expressions and primary constructors.

## Getting started

Import `Microsoft.Extensions.DependencyInjection`, `Mammoth.Extensions.DependencyInjection`, and, for dependency maps, `Mammoth.Extensions.DependencyInjection.Configuration`.

This complete console example registers a keyed scoped service, adds a decorator, and injects that key plus an explicit constructor value:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Mammoth.Extensions.DependencyInjection;
using Mammoth.Extensions.DependencyInjection.Configuration;

IServiceCollection services = new ServiceCollection();
services.AddKeyedScoped<IStore, Store>("primary");
services.Decorate<IStore, StoreDecorator>();
services.AddScoped<Worker>([
    Parameter.ForKey("store").Eq("primary"),
    Dependency.OnValue("label", "batch")
]);
using var provider = ServiceProviderFactory.CreateServiceProvider(services);
using var scope = provider.CreateScope();
var worker = scope.ServiceProvider.GetRequiredService<Worker>();
Console.WriteLine(worker.Label + ":" + worker.Store.Name); // batch:decorated(store)

public interface IStore { string Name { get; } }
public sealed class Store : IStore { public string Name => "store"; }
public sealed class StoreDecorator(IStore inner) : IStore
{
    public string Name => "decorated(" + inner.Name + ")";
}
public sealed class Worker(IStore store, string label)
{
    public IStore Store { get; } = store;
    public string Label { get; } = label;
}
```

The factory is required for provider metadata queries and complete keyed `GetAllServices` discovery. Decorators and DependsOn registration can also be used with native DI providers that support the required keyed/probe interfaces. For Generic Host integration, use `UseServiceProviderFactory(new ServiceProviderFactory(options))` on an `IHostBuilder` (with `Microsoft.Extensions.Hosting` in your application); avoid building a second provider during registration.

## Decorators and ownership

`services.Decorate<TService, TDecorator>()` wraps the **last registration of exactly `TService`**, preserving its key, lifetime and position. It does not decorate every registration/key at once. Register and decorate each intended service in sequence. Repeated calls nest decorators; the last is outermost. Interfaces and assignable concrete classes work; type, factory and caller-supplied instance registrations are supported.

DI tracks each container-created inner service and decorator independently. Disposable decorators must dispose **only their own resources**, never their injected inner service. Caller-supplied singleton instances remain caller-owned; wrapping them does not transfer their disposal ownership. Private decorator layers do not appear as additional public service/key registrations. See the [keyed decorator and ownership recipe](.agents/skills/use-mammoth-di/references/usage.md#keyed-decorators-and-caller-owned-instances).

Register open generics using native DI descriptors; `Decorate<TService,TDecorator>` is a closed generic API, not a blanket open-generic decorator registration API.

## DependsOn constructor selection

Pass a `Dependency[]` to Mammoth's Add/TryAdd Singleton/Scoped/Transient overloads, including keyed registration variants. `Parameter.ForKey("store").Eq("primary")` maps a **constructor parameter name**, not a service type. `Dependency.OnValue("label", "batch")` injects a value assignable to that parameter; it does not register that value as a global service. The fluent `Eq` overload accepts a string key. For another key type, use native `[FromKeyedServices(key)]` with a valid attribute constant or an explicit keyed factory.

A non-empty map selects a constructor at **resolution time**:

- A single `[ActivatorUtilitiesConstructor]` takes precedence and must be satisfiable. Multiple preferred constructors fail.
- Otherwise, the unique longest satisfiable public constructor wins. Equally long satisfiable constructors are ambiguous; missing required dependencies fail. Dependencies of rejected constructors are not created.
- Named map entries override attributes and ordinary injection. Otherwise `[FromKeyedServices("backup")]` resolves its explicit key, `[ServiceKey]` receives the owning registration's key, and unannotated parameters use ordinary services. Optional defaults apply only when the requested dependency is unregistered. Use explicit-key attributes on Mammoth's mapped factory path; do not infer support for newer native attribute lookup modes from the DI package version.
- Unknown map entries are ignored; incompatible explicit values do not silently fall back to another source. An empty map uses the native DI registration path.

Selection requires `IKeyedServiceProvider`, `IServiceProviderIsService` and `IServiceProviderIsKeyedService`. Keep normal scope/lifetime rules: mapping a scoped dependency into a singleton does not make it safe. See the [constructor and attribute recipe](.agents/skills/use-mammoth-di/references/usage.md#constructor-attributes-and-optional-defaults).

## Queries, generics and snapshots

Before building, `IServiceCollection` offers `GetServiceDescriptors(type, isKeyedService: ...)` (assignable service-type matching; null filter includes both keyed and unkeyed descriptors), `IsServiceRegistered`, a global `IsKeyedServiceRegistered(key)`, and keyed/unkeyed lifetime helpers. Inspect descriptors directly when you need an exact registration identity; do not assume collection matching and provider snapshot lookup are interchangeable.

Use provider helpers after building with `ServiceProviderFactory`:

| Need | Provider API / meaning |
| --- | --- |
| Type present under any key | `IsServiceRegistered<T>()` |
| Key present anywhere | `IsKeyedServiceRegistered(key)`; this is a global key query, **not** a type/key query |
| Unkeyed selected lifetime | `IsSingletonServiceRegistered<T>()` (also Scoped/Transient) |
| Selected lifetime for a type/key | `IsKeyedSingletonServiceRegistered<T>(key)` (also Scoped/Transient) |
| All unkeyed and keyed implementations | `GetAllServices<T>()` or `GetAllServices(typeof(T))` |

Lifetime queries return `false` for missing registrations and keep keyed/unkeyed identities independent. Provider lifetime lookup prefers the exact closed registration, then its generic definition, within the requested key. Type discovery recognizes constructed generics from registered definitions. `GetAllServices` merges closed-service and generic-definition keys once per key, then uses native enumeration for each group. Unkeyed services are first; **keyed group order is unspecified**. Within a group, native registration order is retained. Do not treat all-service discovery as a single-service selection rule.

Provider queries/diagnostics read a private snapshot taken when the factory builds the provider. Public `ServiceTypes`, `ServiceKeys`, `ServiceKeys<T>` and `ServiceLifetimes` remain mutable compatibility copies; modifying them does not change queries, enumeration or diagnostics. Editing the original collection after build does not update the provider. Keys themselves should have stable equality/hash behavior: the snapshot isolates metadata collections, not arbitrary mutable key objects.

[Runnable generic discovery and snapshot example](.agents/skills/use-mammoth-di/references/usage.md#generic-discovery-and-snapshot-isolation).

## Transient-disposable diagnostics

Enable `ExtendedServiceProviderOptions.DetectIncorrectUsageOfTransientDisposables` in development when investigating disposable transients resolved from the root. Diagnostics patch registrations and use native-provider reflection; they are not a replacement for correct application scopes or disposal ownership.

| Option | Effect |
| --- | --- |
| `AllowSingletonToResolveTransientDisposables` | Permits the transient when the tracked resolution chain contains a singleton; default is `false` |
| `ThrowOnOpenGenericTransientDisposable` | Rejects recognized disposable open-generic type registrations at build time; otherwise warns when a logger is available |
| `DetectIncorrectUsageOfTransientDisposablesExclusionPatterns` | Regex patterns against service-type full names; matching transient registrations bypass the check |

Open-generic runtime construction and resolution chains cannot be patched like closed descriptors. Startup detection/logging is not comprehensive root-resolution protection for these cases. Factories can create disposable objects before the diagnostic throws. Normal scope disposal is still necessary.

Diagnostic formattable keys use invariant culture on all current-develop targets; this intentionally changes netstandard diagnostic text (for example `1234.5` under `fr-FR`). Keys with only a custom `ToString()` retain that method's formatting. [Runnable diagnostic example](.agents/skills/use-mammoth-di/references/usage.md#root-and-scope-diagnostics).

## Assembly registration

`AssemblyInspector` in `Mammoth.Extensions.DependencyInjection.Inspector` filters an assembly and creates descriptors via `BasedOn`, `WithServiceSelf`/interface selections and lifestyle methods. Assign DependsOn maps via `Configure((registration, type) => registration.DependsOn = ...)` before calling a parameterless lifestyle method; they follow the same constructor rules. Use precise filters; inspect the resulting descriptors before adding them when discovering multiple implementations. [Runnable inspector example](.agents/skills/use-mammoth-di/references/usage.md#assembly-inspection).

## Skill for coding agents

The canonical [use-mammoth-di skill](.agents/skills/use-mammoth-di/SKILL.md) teaches application integration, including version checks, disposal, keyed constructor maps, query semantics and diagnostic limits. It follows the [Agent Skills format](https://agentskills.io/specification), with one entrypoint and optional recipes loaded only when needed.

Copy the **whole `use-mammoth-di` folder**, including `references`, from this repository into your **consumer application's repository**. Choose one supported location per agent; don't maintain duplicate copies for the same agent:

| Agent | Project destination | Invocation |
| --- | --- | --- |
| Codex | `.agents/skills/use-mammoth-di/` | `$use-mammoth-di`, or automatic selection from its description; [official discovery docs](https://learn.chatgpt.com/docs/build-skills) |
| Claude Code | `.claude/skills/use-mammoth-di/` | `/use-mammoth-di`, or automatic selection; [official skill docs](https://code.claude.com/docs/en/skills) |
| GitHub Copilot | `.github/skills/use-mammoth-di/`; `.agents/skills` and `.claude/skills` are also supported | Ask to use `use-mammoth-di`; selection depends on the supported client; [official skill docs](https://docs.github.com/en/copilot/how-tos/copilot-on-github/customize-copilot/customize-cloud-agent/add-skills) |

These project discovery conventions were checked on October 1, 2026. Claude Code's documented project directory differs from Codex's; a standard `SKILL.md` does not imply identical search paths in every agent/client. Other agents can read the folder explicitly if they support Agent Skills or local instructions. Copying instructions does not install Mammoth or change package references. Keep the skill aligned with the library version your application actually uses.

The repository includes no global agent configuration changes or automatic system-wide installer.
