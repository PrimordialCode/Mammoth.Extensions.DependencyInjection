# Issue #93: warmed GetAllServices discovery allocations

Run from the repository root in PowerShell:

```powershell
dotnet build repro/Issue93.AllServicesAllocation -c Release
$env:DOTNET_TieredCompilation = '0'
foreach ($target in 'net8.0', 'net9.0', 'net10.0') {
    dotnet run --project repro/Issue93.AllServicesAllocation -c Release -f $target --no-build
    if ($LASTEXITCODE -ne 0) { throw "Probe failed for $target" }
}
```

The probe measures both public overloads with warmed singleton instances, across
empty, unkeyed-only, one-key, ten-key, open-generic-only key discovery, and merged
closed/open-generic cases. Nonempty fixtures include one unkeyed singleton. The
merged case includes distinct equal string keys shared by a closed and an open
registration, plus one key unique to each. Expected counts come from native DI
groups enumerated once per known key; every measured total must match that count.

Controls use the same native groups: an eager typed list with keys known ahead of
time, and a group-count-only query. The latter returns no aggregate collection;
neither implements dynamic key discovery and neither is a complete replacement
for GetAllServices.

Each case performs 1,000 warm-ups followed by seven samples of 100,000 calls, with
collection/finalizer wait before each sample. Median allocations use
`GC.GetAllocatedBytesForCurrentThread`. Setup, keys, delegates and sample storage
are outside measurement. Native singleton enumerable caches are warmed.

## Local results, 2026-10-07

Baseline: `c8af7f850c08e612401bef83b899fe8b26a3af51`. Candidate: the issue #93
working-tree changes. Release, Microsoft DI 10.0.0, tiered compilation disabled
for both runs. .NET 8.0.31, 9.0.20 and 10.0.12 produced identical medians below,
in bytes/call.

| Case | Generic before / after | Type before / after | Known-key typed list | Group count | Result count |
| --- | ---: | ---: | ---: | ---: | ---: |
| Empty | 96 / 32 | 160 / 96 | 32 | 0 | 0 |
| Unkeyed-only | 152 / 88 | 216 / 152 | 64 | 0 | 1 |
| One key | 336 / 120 | 464 / 248 | 104 | 0 | 2 |
| Ten keys | 1,160 / 360 | 1,864 / 1,064 | 400 | 0 | 11 |
| Open-generic keys only | 336 / 120 | 464 / 248 | 104 | 0 | 2 |
| Merged closed/open keys | 456 / 456 | 712 / 712 | 248 | 0 | 5 |

The typed generic path was already implemented by #85 before this baseline.
This change reuses the snapshot's copied read-only key collection when only one
key group exists, and an empty singleton when neither group contains keys.
Two nonempty groups still allocate a fresh deduplicated union per call. There is
no closed-type union cache and no service-instance cache. The merged fixture
therefore retains its original allocation cost.

The existing aggregation sizing and eager native group resolution are preserved.
The known-key typed-list control can cost more than the public generic query for
ten keys because seeding a list from one unkeyed item causes extra growth steps.
An initial-sizing candidate was measured and rejected after it increased the
merged case to 464/720 B (generic/Type); it is absent from the final change.

These are warmed manual allocation probes in a shared local environment, not
BenchmarkDotNet confidence estimates or throughput guarantees. No net472
allocation or cold-provider result is claimed.

## Correctness checks

```powershell
dotnet test src/Mammoth.Extensions.DependencyInjection.sln -c Release
```

The 14 new contract cases pass against both baseline and candidate on net472,
net8.0, net9.0 and net10.0. The final candidate passes all 1,338 tests per target;
the library also builds for netstandard2.0. New coverage checks returned-key and
public-metadata mutation, exact/open key deduplication, eager explicit iterator
consumption and failure timing, repeated transient resolution and scope isolation.
Existing tests retain registration order, closed/native enumeration semantics,
AnyKey exclusion, hidden decorator identities, value-type services and explicit
enumerable precedence. Existing net472 test dependency support warnings remain.
