# [TypedId] + STJ source-gen Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Fix `[TypedId]`-generated structs so they participate in System.Text.Json's source-generated `JsonSerializerContext` pipeline without SYSLIB1220 / SYSLIB1030 diagnostics.

**Architecture:** The generator currently emits a nested `internal sealed class TypedIdJsonConverter`. STJ source-gen requires the converter to be accessible from the `JsonSerializerContext` (cross-assembly internal is not). Widen the emit to `public sealed class` in both Guid and Int64 writers — one keyword each. Add regression coverage at three levels: generator-output unit test, cross-assembly compile-time test, and AOT-published runtime smoke.

**Tech Stack:** C# 12, Roslyn incremental source generator, xUnit, Verify (existing), System.Text.Json source generator, NativeAOT publish.

**Design doc:** [docs/plans/2026-06-11-typedid-stj-sourcegen-design.md](2026-06-11-typedid-stj-sourcegen-design.md)

**Branch:** `fix/typedid-stj-sourcegen` (already created).

**Prerequisite knowledge:**
- The generator entry point is `TypedIdGenerator`; it dispatches to `TypedIdGuidWriter` (Ulid + Uuid7) or `TypedIdInt64Writer` (Snowflake + Sequential) based on the resolved strategy.
- The existing generator-output tests use a simple `Generate(string source)` helper that runs the generator on inline source text and returns the generated text as a string. See [tests/ZeroAlloc.ValueObjects.Tests/GeneratorTests/TypedIdResolutionTests.cs:107-129](../../tests/ZeroAlloc.ValueObjects.Tests/GeneratorTests/TypedIdResolutionTests.cs). Use `Assert.Contains` against the returned string — not Verify snapshots — for new emit assertions.
- The test project (net9.0, xUnit) is `tests/ZeroAlloc.ValueObjects.Tests/`. It already references the generator as an `Analyzer`, so any `[TypedId]` struct declared in this assembly is generated at build time.
- STJ source-gen runs at consumer build time. Adding `[JsonSerializable(typeof(SomeTypedId))]` to a `JsonSerializerContext` declared in the test project triggers it.

---

### Task 1: Add the failing generator-output tests

**Files:**
- Modify: `tests/ZeroAlloc.ValueObjects.Tests/GeneratorTests/TypedIdResolutionTests.cs` — append two new tests inside the existing class (do not create a new file; the `Generate` helper is `private static` inside this class).

**Step 1: Write the failing tests**

Append these two tests inside `public sealed class TypedIdResolutionTests` in [TypedIdResolutionTests.cs](../../tests/ZeroAlloc.ValueObjects.Tests/GeneratorTests/TypedIdResolutionTests.cs), just before the closing brace at the same line where the `Generate` helper sits:

```csharp
[Fact]
public void GuidTypedId_EmitsPublicJsonConverter_ForStjSourceGenInterop()
{
    var source = """
        using ZeroAlloc.ValueObjects;
        namespace MyApp;
        [TypedId(Strategy = IdStrategy.Uuid7)]
        public readonly partial record struct OrderId;
        """;
    var generated = Generate(source);
    Assert.Contains("public sealed class TypedIdJsonConverter", generated, StringComparison.Ordinal);
    Assert.DoesNotContain("internal sealed class TypedIdJsonConverter", generated, StringComparison.Ordinal);
}

[Fact]
public void Int64TypedId_EmitsPublicJsonConverter_ForStjSourceGenInterop()
{
    var source = """
        using ZeroAlloc.ValueObjects;
        namespace MyApp;
        [TypedId(Strategy = IdStrategy.Snowflake)]
        public readonly partial record struct AccountId;
        """;
    var generated = Generate(source);
    Assert.Contains("public sealed class TypedIdJsonConverter", generated, StringComparison.Ordinal);
    Assert.DoesNotContain("internal sealed class TypedIdJsonConverter", generated, StringComparison.Ordinal);
}
```

**Step 2: Run tests to verify they fail**

Run:
```
dotnet test tests/ZeroAlloc.ValueObjects.Tests/ZeroAlloc.ValueObjects.Tests.csproj --filter "FullyQualifiedName~EmitsPublicJsonConverter"
```
Expected: BOTH tests FAIL — the assertion `public sealed class TypedIdJsonConverter` is not present; current emit is `internal sealed class`.

**Step 3: No implementation in this task — leave the tests failing.**

The implementation (one-keyword change per writer) comes in Task 2 and will turn these red tests green.

**Step 4: Commit the failing tests**

```
git add tests/ZeroAlloc.ValueObjects.Tests/GeneratorTests/TypedIdResolutionTests.cs
git commit -m "test(gen): assert TypedId JsonConverter emits as public

Two new tests, one per writer (Guid + Int64), pinning the converter's
emitted accessibility to `public sealed class`. Both fail against the
current `internal sealed class` emit. The accompanying generator fix
in the next commit turns them green.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: Widen converter emit to `public` in both writers

**Files:**
- Modify: [src/ZeroAlloc.ValueObjects.Generator/Writers/TypedIdGuidWriter.cs:142](../../src/ZeroAlloc.ValueObjects.Generator/Writers/TypedIdGuidWriter.cs#L142)
- Modify: [src/ZeroAlloc.ValueObjects.Generator/Writers/TypedIdInt64Writer.cs:163](../../src/ZeroAlloc.ValueObjects.Generator/Writers/TypedIdInt64Writer.cs#L163)

**Step 1: Change `TypedIdGuidWriter` emit**

In [TypedIdGuidWriter.cs:142](../../src/ZeroAlloc.ValueObjects.Generator/Writers/TypedIdGuidWriter.cs#L142), replace:

```csharp
sb.AppendLine($"    internal sealed class TypedIdJsonConverter : JsonConverter<{name}>");
```

with:

```csharp
sb.AppendLine($"    public sealed class TypedIdJsonConverter : JsonConverter<{name}>");
```

**Step 2: Change `TypedIdInt64Writer` emit**

In [TypedIdInt64Writer.cs:163](../../src/ZeroAlloc.ValueObjects.Generator/Writers/TypedIdInt64Writer.cs#L163), replace:

```csharp
sb.AppendLine($"    internal sealed class TypedIdJsonConverter : JsonConverter<{name}>");
```

with:

```csharp
sb.AppendLine($"    public sealed class TypedIdJsonConverter : JsonConverter<{name}>");
```

**Step 3: Run the previously-failing tests**

Run:
```
dotnet test tests/ZeroAlloc.ValueObjects.Tests/ZeroAlloc.ValueObjects.Tests.csproj --filter "FullyQualifiedName~EmitsPublicJsonConverter"
```
Expected: BOTH tests PASS.

**Step 4: Run the full test suite to catch regressions**

Run:
```
dotnet test tests/ZeroAlloc.ValueObjects.Tests/ZeroAlloc.ValueObjects.Tests.csproj
```
Expected: ALL tests PASS. No test elsewhere should fail. If any do, investigate before continuing — but no existing test grep-matches `internal sealed class TypedIdJsonConverter` (see analysis in [Task 1](#task-1-add-the-failing-generator-output-tests) — no Verify snapshots reference the converter), so regressions are unlikely.

**Step 5: Commit the fix**

```
git add src/ZeroAlloc.ValueObjects.Generator/Writers/TypedIdGuidWriter.cs src/ZeroAlloc.ValueObjects.Generator/Writers/TypedIdInt64Writer.cs
git commit -m "fix(gen): emit public JsonConverter for [TypedId] so STJ source-gen can resolve it

The nested converter was emitted as \`internal sealed class\`. When a
JsonSerializerContext in a different assembly listed a [TypedId] type
via [JsonSerializable], STJ's source generator emitted SYSLIB1220
(converter inaccessible) followed by SYSLIB1030 (no metadata generated),
contradicting the documented source-gen interop in docs/typed-id/json.md.

Widen the emitted converter to \`public sealed class\`. Backwards
compatible — the type name is unchanged and the [JsonConverter(typeof(...))]
attribute on the struct continues to reference it via the same path.

Closes the third upstream gap blocking the za-cqrs-es template.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: STJ source-gen cross-assembly compile-time + runtime test

This task adds an integration test that proves the fix works end-to-end with the real STJ source generator (not just the textual emit). If the fix were ever regressed, this test project would fail to compile.

**Files:**
- Create: `tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests.csproj`
- Create: `tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests/Ids.cs`
- Create: `tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests/AppJsonContext.cs`
- Create: `tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests/StjSourceGenRoundTripTests.cs`
- Modify: `ZeroAlloc.ValueObjects.sln` (add the new project)

**Why a separate test project?** STJ source-gen accessibility is sensitive to assembly boundaries. The existing `ZeroAlloc.ValueObjects.Tests` is the same assembly as the TypedId declarations; cross-assembly is the real-world consumer scenario and the one the bug breaks. New project = new assembly.

**Step 1: Create the test project file**

Create `tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsAotCompatible>true</IsAotCompatible>
    <!-- Surface STJ source-gen diagnostics (SYSLIB1220, SYSLIB1030) as build errors,
         not warnings, so any regression of the converter accessibility breaks the build. -->
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" PrivateAssets="all" />
    <ProjectReference Include="..\..\src\ZeroAlloc.ValueObjects.Generator\ZeroAlloc.ValueObjects.Generator.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
    <ProjectReference Include="..\..\src\ZeroAlloc.ValueObjects\ZeroAlloc.ValueObjects.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

Note: `ReferenceOutputAssembly="false"` on the generator so we don't link the generator DLL itself into the test assembly; we only want it as an analyzer.

**Step 2: Declare the TypedIds**

Create `tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests/Ids.cs`:

```csharp
using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.SourceGenInterop.Tests;

[TypedId(Strategy = IdStrategy.Uuid7)]
public readonly partial record struct InteropOrderId;

[TypedId(Strategy = IdStrategy.Snowflake)]
public readonly partial record struct InteropAccountId;
```

**Step 3: Declare the JsonSerializerContext**

Create `tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests/AppJsonContext.cs`:

```csharp
using System.Text.Json.Serialization;

namespace ZeroAlloc.ValueObjects.SourceGenInterop.Tests;

public sealed record OrderEnvelope(InteropOrderId Id, InteropAccountId AccountId, decimal Amount);

[JsonSerializable(typeof(InteropOrderId))]
[JsonSerializable(typeof(InteropAccountId))]
[JsonSerializable(typeof(OrderEnvelope))]
public partial class AppJsonContext : JsonSerializerContext;
```

**Step 4: Verify the build fails before the fix is applied (sanity check the test)**

Temporarily revert Task 2's emit change locally (do NOT commit), then run:
```
dotnet build tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests.csproj
```
Expected: BUILD FAILS with SYSLIB1220 (and SYSLIB1030) errors referring to `InteropOrderId` / `InteropAccountId`.

Re-apply Task 2's emit change. Build now succeeds:
```
dotnet build tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests.csproj
```
Expected: BUILD SUCCEEDS, zero warnings.

**Step 5: Add the round-trip xUnit test**

Create `tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests/StjSourceGenRoundTripTests.cs`:

```csharp
using System.Text.Json;

namespace ZeroAlloc.ValueObjects.SourceGenInterop.Tests;

public sealed class StjSourceGenRoundTripTests
{
    [Fact]
    public void GuidTypedId_RoundTripsThroughSourceGenContext()
    {
        var id = InteropOrderId.New();

        string json = JsonSerializer.Serialize(id, AppJsonContext.Default.InteropOrderId);
        var parsed = JsonSerializer.Deserialize(json, AppJsonContext.Default.InteropOrderId);

        Assert.Equal(id, parsed);
    }

    [Fact]
    public void Int64TypedId_RoundTripsThroughSourceGenContext()
    {
        var id = InteropAccountId.New();

        string json = JsonSerializer.Serialize(id, AppJsonContext.Default.InteropAccountId);
        var parsed = JsonSerializer.Deserialize(json, AppJsonContext.Default.InteropAccountId);

        Assert.Equal(id, parsed);
    }

    [Fact]
    public void Envelope_ContainingTypedIds_RoundTripsThroughSourceGenContext()
    {
        var envelope = new OrderEnvelope(InteropOrderId.New(), InteropAccountId.New(), 99.5m);

        string json = JsonSerializer.Serialize(envelope, AppJsonContext.Default.OrderEnvelope);
        var parsed = JsonSerializer.Deserialize(json, AppJsonContext.Default.OrderEnvelope);

        Assert.Equal(envelope, parsed);
    }
}
```

**Step 6: Run the tests**

```
dotnet test tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests.csproj
```
Expected: 3 tests PASS.

**Step 7: Add the project to the solution**

```
dotnet sln ZeroAlloc.ValueObjects.sln add tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests.csproj
```

**Step 8: Commit**

```
git add tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests/ ZeroAlloc.ValueObjects.sln
git commit -m "test(stj): cross-assembly JsonSerializerContext interop coverage

New test project (separate assembly) declares [TypedId] structs and a
[JsonSerializable] JsonSerializerContext referencing them. With
TreatWarningsAsErrors, any future regression of the converter
accessibility — which is what the prior commit fixed — breaks the
build via SYSLIB1220.

Three runtime round-trip tests exercise serialize/deserialize through
AppJsonContext.Default.* for a Guid-backed TypedId, an Int64-backed
TypedId, and an envelope containing both.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: AOT smoke project

Cross-assembly compile-time + JIT runtime is covered by Task 3. AOT publish + trimmed runtime is a separate failure surface — STJ source-gen, native trim, and the converter accessibility intersect at link time. This task validates the full AOT path.

**Files:**
- Create: `examples/aot-stj-smoke/aot-stj-smoke.csproj`
- Create: `examples/aot-stj-smoke/Program.cs`
- Create: `examples/aot-stj-smoke/Ids.cs`
- Create: `examples/aot-stj-smoke/AppJsonContext.cs`
- Create: `.github/workflows/aot-stj-smoke.yml`

**Step 1: Create the project file**

Create `examples/aot-stj-smoke/aot-stj-smoke.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>true</InvariantGlobalization>
    <!-- Surface STJ source-gen diagnostics as errors at publish time too. -->
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <RootNamespace>AotStjSmoke</RootNamespace>
    <AssemblyName>aot-stj-smoke</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\ZeroAlloc.ValueObjects.Generator\ZeroAlloc.ValueObjects.Generator.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
    <ProjectReference Include="..\..\src\ZeroAlloc.ValueObjects\ZeroAlloc.ValueObjects.csproj" />
  </ItemGroup>
</Project>
```

**Step 2: TypedIds**

Create `examples/aot-stj-smoke/Ids.cs`:

```csharp
using ZeroAlloc.ValueObjects;

namespace AotStjSmoke;

[TypedId(Strategy = IdStrategy.Uuid7)]
public readonly partial record struct OrderId;

[TypedId(Strategy = IdStrategy.Snowflake)]
public readonly partial record struct AccountId;
```

**Step 3: JsonSerializerContext**

Create `examples/aot-stj-smoke/AppJsonContext.cs`:

```csharp
using System.Text.Json.Serialization;

namespace AotStjSmoke;

public sealed record OrderEnvelope(OrderId Id, AccountId AccountId, decimal Amount);

[JsonSerializable(typeof(OrderId))]
[JsonSerializable(typeof(AccountId))]
[JsonSerializable(typeof(OrderEnvelope))]
public partial class AppJsonContext : JsonSerializerContext;
```

**Step 4: Program**

Create `examples/aot-stj-smoke/Program.cs`:

```csharp
using System.Text.Json;
using AotStjSmoke;

var original = new OrderEnvelope(OrderId.New(), AccountId.New(), 42m);

string json = JsonSerializer.Serialize(original, AppJsonContext.Default.OrderEnvelope);
var roundTripped = JsonSerializer.Deserialize(json, AppJsonContext.Default.OrderEnvelope);

if (roundTripped != original)
{
    Console.Error.WriteLine($"FAIL: round-trip mismatch. Original={original}, RoundTripped={roundTripped}");
    return 1;
}

Console.WriteLine($"OK: round-trip succeeded. JSON={json}");
return 0;
```

**Step 5: Publish AOT locally and run**

```
dotnet publish examples/aot-stj-smoke/aot-stj-smoke.csproj -c Release
```
Expected: PUBLISH SUCCEEDS with zero warnings.

Then run the published binary (path varies by OS — Windows shown):
```
./examples/aot-stj-smoke/bin/Release/net9.0/win-x64/publish/aot-stj-smoke.exe
```
Expected: prints `OK: round-trip succeeded. JSON=...`, exits 0.

**Step 6: Add CI workflow**

Create `.github/workflows/aot-stj-smoke.yml`:

```yaml
name: AOT STJ Smoke

on:
  pull_request:
    paths:
      - 'src/ZeroAlloc.ValueObjects.Generator/**'
      - 'src/ZeroAlloc.ValueObjects/**'
      - 'examples/aot-stj-smoke/**'
      - '.github/workflows/aot-stj-smoke.yml'
  push:
    branches: [main]
    paths:
      - 'src/ZeroAlloc.ValueObjects.Generator/**'
      - 'src/ZeroAlloc.ValueObjects/**'
      - 'examples/aot-stj-smoke/**'

jobs:
  smoke:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - name: Publish AOT smoke
        run: dotnet publish examples/aot-stj-smoke/aot-stj-smoke.csproj -c Release
      - name: Run AOT smoke
        run: ./examples/aot-stj-smoke/bin/Release/net9.0/linux-x64/publish/aot-stj-smoke
```

**Step 7: Commit**

```
git add examples/aot-stj-smoke/ .github/workflows/aot-stj-smoke.yml
git commit -m "test(aot): smoke project for [TypedId] + STJ source-gen under PublishAot

NativeAOT + STJ source-gen + the [TypedId] generator intersect at link
time in ways the JIT-mode integration test cannot exercise. A small
console project publishes with PublishAot=true, round-trips a Guid +
Int64 TypedId envelope through AppJsonContext.Default, and exits
non-zero on mismatch. CI workflow runs on changes to the generator,
runtime, or the smoke project itself.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: Documentation update

**Files:**
- Modify: [docs/typed-id/json.md:43](../typed-id/json.md#L43)
- Modify: [docs/typed-id/internals.md](../typed-id/internals.md) (only if it contradicts new shape — read first)

**Step 1: Read internals.md to check for stale accessibility claims**

Read [docs/typed-id/internals.md](../typed-id/internals.md). Look for any mention of `internal sealed`, `private sealed`, or general claims about the converter's visibility around line 138 (the line referenced in the design doc) and elsewhere. Note the locations of anything that needs updating.

**Step 2: Update json.md**

In [docs/typed-id/json.md:43](../typed-id/json.md#L43), replace:

```markdown
The converter is `private sealed` — it is an implementation detail of the struct.
```

with:

```markdown
The converter is emitted as `public sealed` — a public nested type so System.Text.Json's source generator can resolve it from a `JsonSerializerContext` declared in any assembly. The accessibility is intentional and stable.
```

Also update the code example block at [docs/typed-id/json.md:18-40](../typed-id/json.md#L18-L40): change `private sealed class JsonConv` to `public sealed class TypedIdJsonConverter` (matching the actual emitted name and accessibility).

**Step 3: Update internals.md if needed**

Apply any updates found in Step 1 to keep the docs consistent with the new emit.

**Step 4: Verify there are no other stale references**

Run:
```
git grep -n "internal sealed class TypedIdJsonConverter\|private sealed.*JsonConv" docs/ README.md
```
Expected: no matches.

**Step 5: Commit**

```
git add docs/
git commit -m "docs(typed-id): document JsonConverter as public sealed for STJ source-gen

The json.md page claimed the nested converter was \`private sealed\`,
which was doubly inaccurate — it was emitted as \`internal sealed\`,
and after the source-gen interop fix is now \`public sealed\`. Bring
the prose and the code example in line with the actual emit and
explain why the accessibility is intentional.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: Final verification + PR

**Step 1: Run the entire solution build + tests**

```
dotnet build ZeroAlloc.ValueObjects.sln -c Release
dotnet test ZeroAlloc.ValueObjects.sln -c Release
```
Expected: BUILD SUCCEEDS with zero warnings, ALL tests PASS.

**Step 2: Confirm the AOT smoke publishes and runs**

```
dotnet publish examples/aot-stj-smoke/aot-stj-smoke.csproj -c Release
./examples/aot-stj-smoke/bin/Release/net9.0/<RID>/publish/aot-stj-smoke[.exe]
```
Expected: exits 0, prints `OK: round-trip succeeded.`

**Step 3: Check commit log shape**

```
git log --oneline origin/main..HEAD
```
Expected commits, in this order:
1. `docs(plans): design for [TypedId] + STJ source-gen interop fix`
2. `test(gen): assert TypedId JsonConverter emits as public`
3. `fix(gen): emit public JsonConverter for [TypedId] so STJ source-gen can resolve it`
4. `test(stj): cross-assembly JsonSerializerContext interop coverage`
5. `test(aot): smoke project for [TypedId] + STJ source-gen under PublishAot`
6. `docs(typed-id): document JsonConverter as public sealed for STJ source-gen`

The single `fix:` commit is what release-please picks up for the patch bump 1.6.0 → 1.6.1.

**Step 4: Push and open the PR**

```
git push -u origin fix/typedid-stj-sourcegen
gh pr create --title "fix(gen): public JsonConverter for [TypedId] STJ source-gen interop" --body "<see below>"
```

PR body:

```markdown
## Summary
- Widens the generator's emitted `JsonConverter<T>` from `internal sealed` to `public sealed` (Guid + Int64 writers) so `System.Text.Json`'s source generator can resolve it from a cross-assembly `JsonSerializerContext`. Closes the documented promise in `docs/typed-id/json.md`.
- Adds three layers of regression coverage: generator-output unit tests, cross-assembly compile-time + JIT-runtime test project (`TreatWarningsAsErrors` traps any future SYSLIB1220 regression), and an AOT-published smoke that round-trips through a source-gen context.
- Patch bump 1.6.0 → 1.6.1 (release-please picks up the single `fix:` commit).

## Design
See [docs/plans/2026-06-11-typedid-stj-sourcegen-design.md](docs/plans/2026-06-11-typedid-stj-sourcegen-design.md).

## Test plan
- [x] `dotnet test ZeroAlloc.ValueObjects.sln` — all green
- [x] `dotnet publish examples/aot-stj-smoke -c Release` succeeds, binary runs, exits 0
- [x] New `tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests` compiles with `TreatWarningsAsErrors` (would fail before the fix)
- [x] Manual: confirm `git grep "internal sealed class TypedIdJsonConverter"` returns no hits in `src/`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```

---

## Out of scope (deferred)

- Widening `TypedIdSerializer` (the `ZeroAlloc.Serialisation.ISerializer<T>` nested type) to `public`. ISerializer is registered explicitly and no consumer has reported a cross-assembly need. Revisit only on demand.
- Touching converter `Read`/`Write` semantics.
- Changing the `[JsonConverter(typeof(...))]` attribute shape.

## Notes for the executor

- The single `fix:` commit message is load-bearing for release-please. Do not split the writer changes across two commits (one per file) — they're a single fix and belong in one atomic commit. The plan groups them in Task 2 deliberately.
- The `tests/ZeroAlloc.ValueObjects.SourceGenInterop.Tests` project is structured as a separate assembly on purpose; do not move its contents into `ZeroAlloc.ValueObjects.Tests`. Same-assembly internal access would hide the cross-assembly bug.
- `TreatWarningsAsErrors=true` on both the new test project and the AOT smoke is the regression trap. Do not drop it for convenience if a warning surfaces — investigate the warning instead.
