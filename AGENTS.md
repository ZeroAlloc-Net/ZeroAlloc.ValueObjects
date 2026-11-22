# AGENTS.md: ZeroAlloc.ValueObjects

Source generator that adds allocation-free equality to `[ValueObject]` classes and structs and emits
strongly-typed IDs for `[TypedId]` record structs (ULID, UUIDv7, Snowflake, Sequential), with JSON,
ASP.NET Core parsing and EF Core support. Four NuGet packages ship from this repo: `ZeroAlloc.ValueObjects`
(runtime plus the bundled generator), `ZeroAlloc.ValueObjects.Generator` (standalone, for old direct
references), `ZeroAlloc.ValueObjects.EfCore` and `ZeroAlloc.ValueObjects.Hosting`.

## Layout

- `src/ZeroAlloc.ValueObjects.Generator` is the generator (`netstandard2.0`, the code that emits the
  hot path is under `Writers/`). Its DLL is bundled into the core package by the
  `IncludeGeneratorInPackage` target, and also packed on its own.
- `tests/.../Snapshots` holds `*.verified.cs` generator output, compared with Verify. They are data:
  the test project excludes `*.verified.*` and `*.received.*` from compilation. `.gitignore` only covers
  `*.received.txt`, so delete any `*.received.cs` a failing run leaves before committing.
- `samples/ZeroAlloc.ValueObjects.AotSmoke` is the NativeAOT smoke app, not a shipped package.
- Tracking files are enforced: new public API of the core package goes in
  `src/ZeroAlloc.ValueObjects/PublicAPI.Unshipped.txt` (RS0016 and RS0017 are errors). Only the core
  package tracks its API; a companion package symbol, such as one in `Hosting`, does not go there.
  New diagnostics go in the generator's `AnalyzerReleases.Unshipped.md`.
  Do not move entries to the `Shipped` files by hand: the release PR does that.

## Build and test

SDK: `global.json` pins 11.0.100 with `rollForward: latestFeature`. The core, `EfCore` and `Hosting`
packages and the tests target `net10.0` and `net11.0`, so both the .NET 10 and the .NET 11 runtime must
be installed. The generator stays on `netstandard2.0`, as Roslyn requires.
`TreatWarningsAsErrors` is on for every project.

CI `build` job in `.github/workflows/ci.yml`:

```bash
dotnet tool restore
dotnet restore ZeroAlloc.ValueObjects.slnx
dotnet build ZeroAlloc.ValueObjects.slnx --configuration Release --no-restore
dotnet test ZeroAlloc.ValueObjects.slnx --configuration Release --no-build --verbosity normal --logger "trx;LogFilePrefix=test-results" --collect "XPlat Code Coverage" --settings coverage.runsettings --results-directory TestResults
```

The same job then runs a coverage gate (`MarcelRoozekrans/ci-baseline/actions/coverage-gate` over
`TestResults/**/coverage.cobertura.xml`). The commands above do not reproduce that gate.

CI `aot-smoke` job (needs clang and `zlib1g-dev`):

```bash
dotnet restore samples/ZeroAlloc.ValueObjects.AotSmoke/ZeroAlloc.ValueObjects.AotSmoke.csproj
dotnet publish samples/ZeroAlloc.ValueObjects.AotSmoke/ZeroAlloc.ValueObjects.AotSmoke.csproj -r linux-x64 -c Release -o ./aot-out
./aot-out/ZeroAlloc.ValueObjects.AotSmoke
```

Never pass `-p:PublishAot=true` on the command line: it reaches the `netstandard2.0` generator and fails
with NETSDK1207. `PublishAot` is set in the sample's csproj only. The publish runs with
`TreatWarningsAsErrors`, so any IL2xxx or IL3xxx warning from generated code fails it.

CI `full` job in `.github/workflows/benchmarks.yml` (manual only; pull requests run the shared smoke
job instead). Needs the .NET 10 runtime:

```bash
dotnet build benchmarks/ZeroAlloc.ValueObjects.Benchmarks -c Release
dotnet run --project benchmarks/ZeroAlloc.ValueObjects.Benchmarks -c Release --no-build -- --filter "${FILTER:-*}"
```

Fast inner loop, one test class on one framework (run locally, passes):

```bash
dotnet test tests/ZeroAlloc.ValueObjects.Tests -c Release -f net10.0 --filter "FullyQualifiedName~TypedIdAllocationTests"
```

CI job `sdk10-consumer` needs a 10.0.1xx SDK; `tests/ZeroAlloc.ValueObjects.Sdk10Smoke/global.json` pins
10.0.100 with `rollForward: latestPatch`, so a newer band cannot stand in for it. It builds and runs a
consumer of the packed package with Roslyn 5.0:

```bash
unset v; r=$(od -An -N4 -tx4 /dev/urandom | tr -d " ") && [ -n "$r" ] && v=9.9.9-local.sha$(git rev-parse --short HEAD).$(date +%s).r$r
dotnet pack src/ZeroAlloc.ValueObjects -c Release -o artifacts -p:Version="${v:?no random suffix}"
cd tests/ZeroAlloc.ValueObjects.Sdk10Smoke/Consumer
dotnet run -c Release -p:ZeroAllocValueObjectsVersion="${v:?no random suffix}"
```

Each step stops when the random suffix cannot be generated. Runs in one checkout share its `obj/` and
`bin/` folders, so they must not overlap: give each concurrent run its own worktree.

CI job `roslyn-floor` calls the shared `roslyn-floor.yml` from ZeroAlloc-Net/.github: it packs the
solution and fails when a packed DLL references Roslyn above 5.0.0.

Packing: a pack fails unless a version is supplied (`ZAVERSION001`), and fails when the built assembly
does not carry it (`ZAVERSION002`). Every command that builds before a `--no-build` pack takes
`-p:Version=...` too; see the org block for the local version format.

## Conventions and gotchas

- Exception to the org rule that benchmarks target the library's LTS frameworks: the benchmark project
  targets `net10.0` only. It also compares against CSharpFunctionalExtensions, Vogen and plain records,
  so allocations in those benchmark classes belong to the alternatives, not to this library.
- Generated code is the hot path. A change to a writer changes the snapshots; review the diff of the
  `*.verified.cs` files as the real change, and keep emitted code free of LINQ and reflection.
- The generator targets `netstandard2.0` with `EnforceExtendedAnalyzerRules`: no APIs missing from
  that target, and no work in the incremental pipeline that breaks caching. The incrementality tests
  (`*IncrementalityTests`) guard this.
- The core package references `ZeroAlloc.Serialisation` on every target framework, so the generated
  `ISerializer<T>` code is emitted unconditionally.
- `Microsoft.Extensions.Hosting.Abstractions` must not come back into the core package (it dragged
  five dependencies into every consumer); the one overload that needs it lives in the `Hosting` package.
- `ZeroAllocGeneratedAccessibility` is a compiler-visible MSBuild property declared in three props files:
  the core package's `build/` and `buildTransitive/` copies and
  `src/ZeroAlloc.ValueObjects.Generator/build/ZeroAlloc.ValueObjects.Generator.props`, which has no
  `buildTransitive/` counterpart. A change to one needs the same change in all three.
- Diagnostic IDs are `ZATI0xx` (typed IDs) and `ZAVO0xx` (value objects), listed in
  `AnalyzerReleases.*.md`; a new one needs a docs entry in `docs/diagnostics.md` or `docs/typed-id/diagnostics.md`.
- The generator references `Microsoft.CodeAnalysis.CSharp` 5.0.0, the compiler of SDK band 10.0.1xx,
  the oldest supported SDK. Raise it only together with the documented minimum SDK: a compiler older
  than the Roslyn the generator references skips it with `CS9057`, and nothing is generated.
  `renovate.json` holds it below 5.1, and the CI `sdk10-consumer` job builds a consumer of the packed
  package with SDK 10.0.1xx (`tests/ZeroAlloc.ValueObjects.Sdk10Smoke`). The test project may use a
  newer Roslyn.
- `tests/ZeroAlloc.ValueObjects.Sdk10Smoke/global.json` stays on 10.0.100 with `rollForward: latestPatch`.
  Moving it to a newer patch makes the smoke build with a newer compiler and check nothing; the shared
  Renovate preset ignores that file.
- Commit scopes are limited by `.commitlintrc.yml`: `core`, `generator`, `benchmarks`, `ci`, `deps`.

<!-- org-core:start sha256=8faf417fa86f11bd8ad9bc98dfa4683fba44e440291e84c6cb9b0ec3ca0c0e7e (generated from docker-nuc scripts/agents-md/org-core-zeroalloc.md; do not edit here) -->
## Org rules

These rules hold in every ZeroAlloc-Net repo. Each library makes two promises: it works under
NativeAOT and trimming, and its hot path allocates nothing. The 2026-10-11 sweep found 16 of 28 repos
allocating on the hot path, most with docs claiming 0 B.

### Hot path

The operation a consumer runs repeatedly: `Send`, a cache hit, `Validate`, `Map`, `Serialize`, a
resilience call, a state transition, an append, a worker poll, including the idle poll. Startup and
registration are not hot path. For a source generator, the hot path is the code it emits: judge the
snapshots and the generated output.

### Allocation traps the sweep found

| Pattern | Instead |
|---|---|
| `async ValueTask` that only awaits and returns | `return new ValueTask<T>(inner.XAsync(ct));` when the method only forwards a `Task<T>` (return a forwarded `ValueTask<T>` directly). The wrapper stores a synchronous throw in the returned task; the direct return lets it, and the cancellation status of the callee, reach the caller. Keep the `async` wrapper when callers depend on either |
| `Task.WhenAll` over tasks that already completed | skip the await only when every task has `IsCompletedSuccessfully` (`Status == TaskStatus.RanToCompletion` on `netstandard2.0`); otherwise `await Task.WhenAll`, which also reports faults of completed tasks |
| `TagList` or log `params object[]` built before `Enabled`/`IsEnabled` | check first, or `LoggerMessage.Define` |
| event args allocated before checking for subscribers | copy the handler to a local, check, then allocate |
| `new Serializer()` / `new Utf8JsonWriter()` per call | cache stateless instances, reuse writers with `Reset` |
| `GetString()` / `ToString()` round-trips in JSON converters | parse and format through spans |
| `ToArray()` / `ToList()` copies between layers | pass `ReadOnlyMemory<T>` or spans, copy once at most |
| LINQ in emitted code | emit `for` loops |
| `yield` iterator behind a public `GetEnumerator` | public struct enumerator, from the first release |
| `Enum.IsDefined(typeof(E), v)` with a boxed value, `decimal.GetBits(v)` to read the scale | `Enum.IsDefined<E>(v)` when `v` has type `E` (.NET 5 or later, so not on .NET Framework, `netstandard2.0`, `netstandard2.1` or .NET Core 3.1); `v.Scale` for a scale read only (.NET 7 or later, never for the coefficient or sign). Keep the old call on older targets |
| configuration parsed or read per call | parse once and cache |
| an `AsyncLocal` write per call | pass state explicitly |
| a capturing lambda or method-group delegate per call | cache the delegate in a field, or a static lambda |
| a per-key object that is never removed | remove on release; otherwise it is a leak |

Inherent allocations are fine when documented: the consumer's own result, an exception on a failure
path, a one-time cache fill.

### Tests and benchmarks

- Every hot path has an allocation gate test (`AllocationGate` from ZeroAlloc.TestHelpers) that measures
  the whole public call, including any state setup.
- Budgets are exact. If a path allocates, assert that number and comment what it is.
- Test the generated code, not a hand-written stand-in, and cover every shape: each attribute, nested
  collections, and the asynchronous case with a fake that returns pending tasks.
- Benchmarks run in CI through the shared `benchmark-smoke.yml` and target the library's LTS
  frameworks. The benchmark body allocates nothing besides the library itself. Run locally with
  `--inProcess`.
- Publish exact allocations and same-run ratios against a `Baseline`, never absolute timings. Run with
  `--launchCount 3`, pin CPUs with `taskset`, give the spread, and label every table.

### NativeAOT and trimming

- Packable runtime projects, and projects consumers publish, set `IsAotCompatible=true` on modern
  target frameworks. Analyzers and source generators target `netstandard2.0`.
- No reflection or runtime code generation on a supported path. An opt-in reflection fallback carries
  honest `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`. Every suppression must be true;
  remove dead ones.
- The AOT smoke sample publishes with `TreatWarningsAsErrors=true`. If a third-party warning really
  blocks it, use a narrow, justified suppression for that warning. `WarningsNotAsErrors` selects codes,
  not assemblies, so it would also downgrade the same warning from generated code.
- The smoke runs the hot path of every package in the repo. A package without a smoke has unknown AOT
  status. Adapters run against real services in CI service containers; publish-only is a last resort.
  App and template smokes make authenticated requests.

### Docs and shipping

- Docs match measurements. After a hot-path change, re-measure and update `docs/performance.md` and the
  README. Never write "0 B" without a test that asserts it.
- New public API is `feat:`, even for a performance change. Public API changes are additive: never add
  an optional parameter to a shipped signature. A breaking end state goes in a `next-major` issue.
- A behaviour change gets a `## Behaviour change` section in the PR body.
- Conventional Commits with release-please. Subjects start lowercase, body lines stay within 100
  characters, and a body never contains nested parentheses: release-please drops the whole commit.
- Local packs use `9.9.9-local.<sha>`. After a release, verify the package on NuGet.
- Fix review findings in the PR; file an issue only for what cannot go in it.

Full rules, template wiring lessons and the reasons behind each rule: https://github.com/ZeroAlloc-Net/.github/blob/main/AGENTS.md
<!-- org-core:end -->
