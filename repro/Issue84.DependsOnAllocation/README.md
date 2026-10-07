# Issue #84: warmed DependsOn allocations

Run from the repository root in PowerShell:

```powershell
dotnet build repro/Issue84.DependsOnAllocation -c Release
$env:DOTNET_TieredCompilation = '0'
foreach ($target in 'net8.0', 'net9.0', 'net10.0') {
    dotnet run --project repro/Issue84.DependsOnAllocation -c Release -f $target --no-build
    if ($LASTEXITCODE -ne 0) { throw "Probe failed for $target" }
}
```

The probe uses a real non-empty dependency map and a singleton dependency. Controls
resolve an ordinary transient through native DI and Mammoth, and an equivalent
mapped transient through an explicit native factory. Additional cases exercise an
inherited keyed dependency with ServiceKey injection and an optional value default.
Each case performs 20,000 warm-ups and seven samples of 100,000 resolutions, with
collection/finalizer wait before each sample. Allocation measurement uses
`GC.GetAllocatedBytesForCurrentThread`; the printed result is the median and range.
Service creation is included; delegate creation, provider setup and metadata warm-up
are excluded. Consume the last result with `GC.KeepAlive`.

## Local results, 2026-10-07

Baseline: `d1a86f49cc61489fbc8216b5231f002f91722bfe`. Candidate: the issue #84
working-tree changes. Microsoft.Extensions.DependencyInjection 10.0.0, Release,
tiered compilation disabled in both runs. Values below are median bytes/resolution.

| Case | .NET 8.0.31 before / after | .NET 9.0.20 before / after | .NET 10.0.12 before / after |
| --- | ---: | ---: | ---: |
| Native ordinary | 24 / 24 | 24 / 24 | 24 / 24 |
| Mammoth ordinary | 24 / 24 | 24 / 24 | 24 / 24 |
| Native explicit factory | 32 / 32 | 32 / 32 | 32 / 32 |
| DependsOn | 1,168 / 72 | 1,112 / 72 | 1,112 / 72 |
| Keyed DependsOn | 1,712 / 88 | 1,656 / 88 | 1,656 / 88 |
| Optional DependsOn | 1,448 / 88 | 1,392 / 88 | 1,392 / 88 |

Mapped-case sample ranges matched the displayed medians at one decimal place,
except the baseline keyed case (maximum 0.1 B above the median). Native ordinary
samples occasionally included background container compilation; their medians
remained 24 B. These are manual allocation probes in a shared local environment,
not BenchmarkDotNet confidence estimates or throughput guarantees. No net472
allocation result or cold-start improvement is claimed. The cache moves metadata
construction to first activation; each warm activation still allocates an argument
array and the transient instance.

## Correctness checks

```powershell
dotnet test src/Mammoth.Extensions.DependencyInjection.sln -c Release
```

The candidate passed 1,309 tests per target on net472, net8.0, net9.0 and net10.0.
The library also builds for netstandard2.0. New tests cover provider-specific
constructor availability, mutable and distinct dependency maps, first override
precedence, registered-value precedence over defaults, concurrent first use, and
collectible type release. Existing tests retain keyed lookup, preferred constructors,
ambiguity, nullable enum defaults, decorator context, lifetimes and exception behavior.
The net472 test build retains the existing unsupported-target warnings from
Microsoft.Extensions.Diagnostics.Testing and Telemetry.Abstractions 10.0.0.
