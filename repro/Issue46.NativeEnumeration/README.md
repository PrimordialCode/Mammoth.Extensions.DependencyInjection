# Native DI cache regression (issue #46)

This console project references only Microsoft DI, with no Mammoth project/package reference. It reproduces the upstream bug with an unkeyed scoped open generic, an unkeyed singleton closed generic, and a keyed transient open generic. It retains one provider/scope, observes the actual background compiled resolver replacement, then checks counts, concrete implementation markers and instance identity over 100 interleaved enumerations.

From the repository root on Windows, run each command with `-f net472`, `-f net8.0`, `-f net9.0` and `-f net10.0`:

```powershell
dotnet run --project repro/Issue46.NativeEnumeration -c Release -f net8.0 -p:DiVersion=9.0.0
dotnet run --project repro/Issue46.NativeEnumeration -c Release -f net8.0 -p:DiVersion=9.0.20
dotnet run --project repro/Issue46.NativeEnumeration -c Release -f net8.0 -p:DiVersion=10.0.0
```

The default DI version is 9.0.0 so the original failing control remains runnable. Both tested 9.x versions fail after compilation: unkeyed membership becomes the keyed implementation. Version 10.0.0 passes, containing [dotnet/runtime#113343](https://github.com/dotnet/runtime/pull/113343). CI runs the 10.0.0 control on all four test frameworks.

`test-support/NativeResolverCompilation.cs` is read-only test/reproduction instrumentation. It observes private native accessor delegates and, for the broken version, the call-site cache key. It polls for actual accessor replacement with a hard 30-second timeout, not a fixed delay or retrying failed assertions. It fails if the expected dynamic engine/private layout changes. It is never compiled into the production Mammoth library and does not switch engines, clear caches or mutate DI internals.

The main suite adds reused-provider native/generic-helper/Type-helper interleaving, two keys, mixed open/closed registrations and lifetimes, scope/singleton identity, disposal ownership, metadata snapshot mutation, DependsOn constructor selection and decorator chains. The original fresh-provider coverage remains in place.
