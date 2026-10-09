# Native graph planning for decorated originals

Issue #121 regressed from 0.7.1 when #37 moved original services behind private factories to fix disposal ownership. Factories hid constructor graphs: invalid rejected candidates could be skipped, and selected graphs could activate an earlier dependency factory before failing.

## Registration design

`RegisterInnerLayer` keeps original implementation types as native DI type registrations. Each receives a private `DecorationServiceType`, a public `TypeDelegator` over `object` with reference identity and explicit object assignability. `UnderlyingSystemType` retains that private identity, so normal object registrations compare unequal. The original public service key is preserved, including unkeyed/null-key context, rather than replacing it with a private key.

Native DI plans constructors and dependency call sites before constructing the original. It owns that original and the public decorator independently. Caller-supplied instances remain caller-owned. Factory/map originals and preceding decorators retain the existing private factory slots and activation policy. No extra provider is built for runtime graph checking, and no application factory is used as a validation probe.

## Scoped compiled caching

The [native DI 10 IL emitter](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/Microsoft.Extensions.DependencyInjection/src/ServiceLookup/ILEmit/ILEmitResolverBuilder.cs#L248-L256) reconstructs scoped cache keys using a runtime type token. A delegated identity cannot survive that operation. A scoped original therefore uses a transient native type registration plus a scoped, non-disposable holder under a normal private runtime marker type. Native DI creates/captures the inner in that scope; the holder caches its reference without capturing disposal a second time. Singleton and transient originals keep their native lifetimes. Public service and decorator lifetimes are unchanged.

The native original and scoped holder are both hidden from public Mammoth discovery. Keys remain public keys, so a key-only query can still report the original registration's key. Private descriptor lifetimes are implementation details; the public service lifetime stays authoritative.

## Diagnostics and validation timing

Diagnostics preserve the native type descriptor and track resolution through the public decorator factory. A small per-provider options service lets that factory check transient disposable originals before creation, with the existing exclusion and singleton-ancestor rules. The shared caller collection is never reconfigured per provider.

`ValidateOnBuild` can now reject a decorated original at startup, just as it rejects the corresponding undecorated native type. This includes ambiguity, invalid generic constraints and `AnyKey` inherited dependencies that have no wildcard binding at validation time. Use a valid wildcard dependency or concrete registrations when startup validation needs one. Disabling validation for diagnostics remains unsupported.

Factories, non-empty dependency maps and decorator constructors are opaque to native graph planning. This restores native original-type behavior; it does not promise inspection through arbitrary application factories or change DependsOn's separate selection policy.

## Regression evidence

Differential tests compare native and decorated registrations under the same validation settings, require zero factory/constructor calls on graph failures, and cover rejected/selected constructors, open/closed generics, enumerable ordering, cycles, keys, lifetimes, repeated layers and provider isolation. Native-only test instrumentation observes compiled accessor replacement for originals and scoped holders, then checks 100 repeated resolutions, independent caches, ordinary object/implementation registrations and exactly-once disposal across all supported test targets. Instrumentation is never included in the production library.
