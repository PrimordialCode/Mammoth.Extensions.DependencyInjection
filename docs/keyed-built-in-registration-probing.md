# Keyed built-in registration probing

Mammoth uses a guarded private-DI metadata fallback for [issue #108](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/108). It preserves existing support for ordinary `services.BuildServiceProvider()` while checking constructor availability without activating dependencies.

## Decorators and DependsOn do not already require Mammoth's provider factory

Both features register ordinary service factory delegates that native DI can execute. The [repository README](../README.md#serviceprovider) explicitly documents native-provider support for decoration/DependsOn. Mammoth's provider factory supplies the authoritative registration snapshot used by its provider metadata queries and enables its optional diagnostics.

Consequently, requiring that factory for this constructor-selection case would introduce a new configuration/compatibility requirement for applications that currently use native `BuildServiceProvider()`. The maintainer approved preserving that support with a guarded reflection fallback.

## The public keyed probe cannot answer the question we need to ask

The affected types are:

- `IServiceProvider`
- `IServiceScopeFactory`
- `IServiceProviderIsService`
- `IServiceProviderIsKeyedService`

DI 10's [`IsKeyedService` implementation](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/Microsoft.Extensions.DependencyInjection/src/ServiceLookup/CallSiteFactory.cs#L691-L725) checks exact and AnyKey registrations, but then reports these four built-in types as available regardless of the requested key. Their implicit built-in registrations are actually unkeyed. Thus `IsKeyedService(typeof(IServiceProvider), "missing")` returns true both when an explicit keyed registration exists and when none exists.

Constructor selection must distinguish those cases. For example, with these constructors:

```csharp
public Consumer(string label) { /* valid shorter constructor */ }

public Consumer(
    string label,
    [FromKeyedServices("missing")] IServiceProvider dependency)
{
    /* requires an explicit keyed registration */
}
```

The public probe makes the longer constructor appear satisfiable even when the dependency cannot resolve. Before this fix, DependsOn selects it and subsequently throws. Native DI's own registration-based constructor activation can see the real registration graph and select the shorter constructor instead.

The same availability check serves named DependsOn key maps, explicit/inherited-key parameters, and contextual keyed decorator activation. Optional dependencies must also use their default when the keyed registration is absent.

## Why other approaches were not used

- **Calling `GetKeyedService` to test availability:** this resolves the dependency. It can run application factories, construct services, capture disposable instances, encounter scope-validation errors, or throw application exceptions before a constructor is selected. Even a constructor that is later rejected or found ambiguous would have caused side effects. A registered factory can also return null, so a null result does not reliably mean that no registration exists.
- **Rejecting all keyed built-in dependencies:** explicit concrete-key and AnyKey registrations of these types are valid and must remain usable.
- **Capturing the caller's collection when DependsOn/decoration is registered:** registrations can be added or removed afterward, before provider creation. An early snapshot can miss them; retaining and inspecting the live collection can instead observe changes made after a provider was built. Neither reproduces that provider's immutable registration state.
- **Always requiring Mammoth's provider factory:** this would supply the metadata without private-DI reflection, but it is the compatibility tradeoff described above. An explicit new metadata/setup API would similarly add a configuration requirement.

For an already-built, unmodified native DI 10 provider, the available public APIs do not expose the explicit descriptor information needed for this non-activating check.

## What the reflection calls do, and their limits

The [implementation](../src/Mammoth.Extensions.DependencyInjection/ConstructorActivator.cs) uses three operations:

1. `typeof(ServiceProvider).Assembly.GetType(...CallSiteFactory)` locates the native probe type in the actual loaded DI assembly.
2. `GetField("_descriptors", Instance | NonPublic)` locates its descriptor field. These two lookups are cached in static fields.
3. `FieldInfo.GetValue(nativeProbe)` reads that recognized native probe's copied descriptor array when the fallback cache is populated.

The descriptor array is [copied during native provider construction](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/Microsoft.Extensions.DependencyInjection/src/ServiceLookup/CallSiteFactory.cs#L15-L30), so it represents the built provider rather than the subsequently mutable caller collection. The fallback stores only explicit, non-null-key identities for the four affected built-in types, then checks the requested key and AnyKey fallback. It does not invoke dependency factories or change native registrations, resolution, lifetimes, or caching.

Mammoth's existing `ServiceProviderRegistrationSnapshot` is preferred. When it exists, the method answers from that snapshot without reading native descriptors. Custom probe implementations retain their public availability contract; the private descriptor fallback is used only for the recognized native probe type. The static type/field lookups still occur when `ConstructorActivator` initializes.

The identities are cached in a `ConditionalWeakTable` keyed by the native probe. This keeps registration availability provider-specific and permits the probe/cache entry to be collected. Subsequent checks use the cached identities.

Before reading the descriptor array, the code verifies that the field exists and has the expected `ServiceDescriptor[]` shape. If the recognized native probe's metadata is unavailable or incompatible, it throws a clear `NotSupportedException` directing the caller to Mammoth's provider factory instead of guessing availability or resolving a dependency as a probe. This remains a dependency on private DI implementation details, not a public API guarantee; future DI changes require revalidation.

This explanation concerns the new private-DI registration fallback. Constructor/parameter reflection used by the existing activator is a separate mechanism.

## Original type activation in decorators

[Issue #111](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/issues/111) reuses the native-compatible activator for original implementation-type descriptors hidden by decoration. It ignores `ActivatorUtilitiesConstructorAttribute`, accepts satisfiable constructor parameter permutations/subsets, and rejects unrelated parameter-type sets before activating dependencies. New decorators and nonempty DependsOn factories keep their existing construction policies.

This activator uses the same keyed built-in availability helper described above. It also validates open-generic constraints for every examined constructor candidate, including candidates that would not ultimately be selected. Native DI performs that validation while constructing argument call sites; its public availability probes report a registration without testing the implementation's generic constraints. Resolving the dependency to check constraints would activate factories too early.

Mammoth providers supply their existing registration snapshot. For a recognized native probe, the guarded `ReadNativeDescriptors` helper reads the same copied descriptor array through the existing cached field lookup, then builds an internal snapshot cached separately in a `ConditionalWeakTable`. This additional snapshot retains descriptor identities, lifetimes and implementation metadata needed for constraint validation. The keyed-built-in identity cache still stores only the four built-in types. Both caches are provider-specific and weakly keyed; neither reads the subsequently mutable caller collection or activates a service. Exact closed registrations, including AnyKey, take precedence over open-generic bindings.

Custom probes continue to supply their public availability contract. Missing or incompatible metadata on the recognized native probe fails clearly with the existing recommendation to use Mammoth's provider factory. This reuse adds no new private member lookup; it broadens the descriptor fallback's use to native-compatible generic constraint validation.

## Verification of the compatibility choice

The initial implementation in [PR #115](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/pull/115), commit `3c6942bfea31dfb1dcd2e3446cb05808a3835810`, was verified as follows. Revalidate these contracts when changing the fallback or upgrading the DI dependency.

- Regression tests were committed before the fix: 96 failed and 84 passed on each supported test target.
- The suite passed 1,687 tests on each of Windows net472, net8.0, net9.0 and net10.0, including native providers, Mammoth snapshot providers, diagnostics, named/attribute/inherited keys, optional defaults, exact/AnyKey registrations, provider isolation, custom probes, decoration, and activation timing.
- Rejected/ambiguous constructor tests verify zero dependency-factory activations; an explicitly registered keyed factory runs only after its constructor is selected.
- Native DI hot-cache probes passed on all four targets.
- [CI run 37776545648](https://github.com/PrimordialCode/Mammoth.Extensions.DependencyInjection/actions/runs/37776545648) passed for that exact implementation commit. Only the two existing net472 test-package support warnings remain.

The reflection can be removed if we deliberately require a registration-metadata setup path for the affected native-provider use cases. The current design implements the approved choice to preserve their existing setup.
