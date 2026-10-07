# Issue #92: collection lifetime query allocations

Run from the repository root in PowerShell:

```powershell
dotnet build repro/Issue92.CollectionLifetime -c Release
$env:DOTNET_TieredCompilation = '0'
foreach ($target in 'net8.0', 'net9.0', 'net10.0') {
    dotnet run --project repro/Issue92.CollectionLifetime -c Release -f $target --no-build
    if ($LASTEXITCODE -ne 0) { throw "Probe failed for $target" }
}
```

The probe compares the public singleton lifetime predicates with array-based
controls that retain the previous implementation. It covers collection sizes 0,
10, 1,000 and 10,000, with all descriptors matching, only the last matching, only
the first matching, and no match. Both keyed and unkeyed queries use assignable
service types; keyed queries use equal-but-distinct string keys, with other keys
as distractors. Both paths check every measured result against the expected boolean.

Each case performs 1,000 warm-ups, then seven samples with collection/finalizer
wait before each sample. Iterations per sample are bounded between 100 and
10,000, targeting approximately one million descriptor visits for full scans.
Median allocations use `GC.GetAllocatedBytesForCurrentThread`. Collection setup,
key creation, delegates and sample storage are outside the measurement.

## Local results, 2026-10-07

Baseline: `4887f25c247e0dd6c47e3b6c6c50bf483fe7330f`. Candidate: the issue #92
working-tree changes. Both runs used Release, Microsoft DI 10.0.0 and disabled
tiered compilation. The post-change array controls match the pre-change public
API measurements. Values below are median bytes/query; .NET 9.0.20 and 10.0.12
gave identical results.

| Size | Matching descriptors | .NET 8.0.31 unkeyed before | .NET 8.0.31 keyed before | .NET 9/10 unkeyed before | .NET 9/10 keyed before |
| ---: | --- | ---: | ---: | ---: | ---: |
| 0 | None | 192 | 280 | 192 | 280 |
| 10 | All | 528 | 1,000 | 296 | 536 |
| 10 | First or last only | 280 | 752 | 224 | 464 |
| 10 | None | 192 | 664 | 192 | 432 |
| 1,000 | All | 16,800 | 33,352 | 8,216 | 16,376 |
| 1,000 | First or last only | 280 | 17,024 | 224 | 8,384 |
| 1,000 | None | 192 | 16,936 | 192 | 8,352 |
| 10,000 | All | 211,928 | 372,728 | 80,216 | 160,376 |
| 10,000 | First or last only | 280 | 212,152 | 224 | 80,384 |
| 10,000 | None | 192 | 212,064 | 192 | 80,352 |

**After: 0 B/query in every measured case on all three runtimes.** This measures
configuration-time queries, with no provider construction or service resolution.
The reverse scan stops at the last matching descriptor and checks its lifetime;
it must not continue looking for an earlier descriptor with the requested lifetime.
A match at the end can return immediately; a match at the beginning or no match
still requires a full scan. Arbitrary custom key equality implementations can
have their own allocation costs.

These are warmed manual allocation probes in a shared local environment, not
BenchmarkDotNet confidence estimates or throughput guarantees. No net472
allocation or timing result is claimed.

## Correctness checks

```powershell
dotnet test src/Mammoth.Extensions.DependencyInjection.sln -c Release
```

The 15 new contract cases passed against both the baseline and candidate on
net472, net8.0, net9.0 and net10.0. The candidate passed all 1,324 tests per target;
the library also builds for netstandard2.0. Coverage includes mixed lifetimes,
interleaved keyed/unkeyed descriptors, equal-but-distinct custom keys, assignable
service types, missing matches, collection mutations, null-key registrations,
AnyKey matching, argument validation/precedence, and the public descriptor array
filtering/order contract. Existing net472 test dependency support warnings remain.
