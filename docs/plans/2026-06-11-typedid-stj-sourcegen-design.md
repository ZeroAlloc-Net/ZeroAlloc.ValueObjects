# [TypedId] + System.Text.Json source-gen integration

**Status:** Partially superseded — see "Postmortem" at the bottom. The accessibility-widening described here shipped; the full automatic source-gen interop did not, because of a Roslyn-level limitation discovered during execution.
**Date:** 2026-06-11
**Tracking:** Session D of the za-cqrs-es template unblock arc.

## Problem

`[TypedId]` structs are documented as compatible with `System.Text.Json`'s
source-generated `JsonSerializerContext` pipeline
([docs/typed-id/json.md:53](../typed-id/json.md)). They are not.

For a Guid-backed ID:

```csharp
[TypedId(Strategy = IdStrategy.Uuid7)]
public readonly partial record struct OrderId;
```

the generator emits, inside the partial:

```csharp
[JsonConverter(typeof(OrderId.TypedIdJsonConverter))]
public readonly partial record struct OrderId
{
    // ...
    internal sealed class TypedIdJsonConverter : JsonConverter<OrderId> { ... }
}
```

When a consumer adds `OrderId` to a `JsonSerializerContext`, the STJ source
generator emits two diagnostics:

- **SYSLIB1220** — converter type is not accessible OR does not contain
  an accessible parameterless constructor.
- **SYSLIB1030** — no serialization metadata generated for the type.

The cause is the converter's `internal` accessibility: when the
`JsonSerializerContext` lives in a different assembly than the TypedId, the
nested converter is unreachable across the assembly boundary.

Verified during the `za-cqrs-es` template Task 1 attempt (commit `0dec880`
in `ZeroAlloc.Templates`), which documents the failure mode and the
plain-record-struct workaround.

## Decision

Emit the nested converter as `public sealed class TypedIdJsonConverter`
instead of `internal sealed class`. Apply to both Guid and Int64 writers.

### Why this shape

- One keyword per writer (~10 lines of emit diff total).
- Preserves the nested type name `OrderId.TypedIdJsonConverter` referenced
  by the `[JsonConverter(typeof(...))]` attribute — no shape change to the
  generated struct API.
- The converter type is already named in a public attribute on a public
  struct, so its existence is metadata-visible regardless; widening
  accessibility just removes the cross-assembly access barrier.
- Mirrors the BCL convention (`JsonStringEnumConverter`, etc. are public).
- No naming-collision risk: nested under the struct, not at namespace root.

### Rejected alternatives

**Top-level sibling type** (e.g. `public sealed class OrderIdJsonConverter`
at namespace root): pollutes the consumer namespace with one converter
type per TypedId, opens collision potential with user-defined types, and
requires more generator changes for zero functional gain over the public
nested form.

**`JsonConverterFactory` wrapper**: defers but does not eliminate the
accessibility requirement — the factory itself must be public. Adds
indirection for no gain.

## Scope

In:

- `TypedIdGuidWriter.AppendJsonConverter` — change `internal` → `public`.
- `TypedIdInt64Writer.AppendJsonConverter` — change `internal` → `public`.
- Generator snapshot tests — regenerate to reflect the new accessibility.
- New integration test asserting STJ source-gen round-trips a TypedId
  through a `JsonSerializerContext`.
- New AOT smoke project under `examples/aot-stj-smoke/` that publishes
  with `PublishAot=true` and round-trips a TypedId through a source-gen
  context at runtime.
- Doc update at [docs/typed-id/json.md:43](../typed-id/json.md) to
  reflect the new accessibility.

Out:

- The nested `TypedIdSerializer` (implements `ZeroAlloc.Serialisation.ISerializer<T>`)
  stays `internal`. `ISerializer` is registered explicitly, not
  auto-discovered, and no consumer has reported a cross-assembly need.
  Revisit only on demand.
- No changes to converter `Read`/`Write` semantics.
- No changes to the `[JsonConverter(typeof(...))]` attribute shape.

## Test strategy

**T1 — Generator snapshot test.** Verify-style snapshot asserting the
emitted converter line is `public sealed class TypedIdJsonConverter` for
both Guid and Int64 backings. Existing snapshots that include the
converter line are regenerated in the same commit as the generator
change.

**T2 — STJ source-gen integration test.** In the test project, declare a
TypedId struct plus a `JsonSerializerContext` with
`[JsonSerializable(typeof(TheTypedId))]`. Before the fix the test project
fails to compile with SYSLIB1220. After the fix it compiles, and an
xUnit test round-trips a value through `JsonSerializer.Serialize`/
`Deserialize` using the source-gen context's typeinfo. Cover both Guid
and Int64 backings.

**T3 — AOT smoke project.** New `examples/aot-stj-smoke/` console
(net9.0, `PublishAot=true`) that uses a `JsonSerializerContext`
containing a `[TypedId]` Guid struct + an Int64 struct, serialises and
deserialises through the context, and asserts round-trip equality.
Exits non-zero on mismatch. A CI workflow publishes the AOT exe and runs
it; failure fails the PR.

## Versioning

`fix(gen): emit public JsonConverter for [TypedId] so STJ source-gen can resolve it`
→ release-please patch bump **1.6.0 → 1.6.1**. No breaking change;
widening accessibility is additive.

## Risks

- **Public API expansion.** The nested converter becomes part of the
  public surface. Mitigation: the name `TypedIdJsonConverter` is stable
  and the shape (`JsonConverter<T>` with parameterless ctor) is mandated
  by STJ — there's no meaningful surface a consumer could lock onto
  beyond what's already implied by the `[JsonConverter]` attribute.
- **Snapshot churn.** Every Verify snapshot that includes the converter
  line will diff. Mitigation: accept all in a single commit alongside
  the generator change.
- **AOT smoke adds CI cost.** One extra publish step per PR. Mitigation:
  scope the workflow narrowly (only run on changes touching the
  generator or the smoke project).

## Documentation updates

- [docs/typed-id/json.md:43](../typed-id/json.md) — current text claims
  the converter is `private sealed`, which is doubly inaccurate (it was
  `internal`, will become `public`). Replace with: *"The converter is
  `public sealed` — emitted as a public nested type so System.Text.Json's
  source generator can resolve it from a `JsonSerializerContext`."*
- [docs/typed-id/internals.md:138](../typed-id/internals.md) — reread
  during implementation; update only if it contradicts the new shape.

## Downstream

After this ships and a 1.6.1 NuGet is cut:

1. `ZeroAlloc.Templates` branch `feat/za-cqrs-es-template` resumes Task 1
   of `docs/plans/2026-06-10-za-cqrs-es-implementation.md`.
2. The workaround at commit `fef3f2f` (plain `readonly record struct`
   instead of `[TypedId]`) can be replaced with `[TypedId]` plus a
   one-line `options.Converters.Add(new OrderId.TypedIdJsonConverter())`
   per ID type in the composition root. See the postmortem below for
   why the template can't go further than that with this release.

---

## Postmortem (2026-06-11, after Tasks 1–2 shipped)

The design above assumed widening the converter from `internal` to
`public` would enable the documented `JsonSerializerContext`
source-gen interop scenario. Execution proved that assumption wrong.

### What we found

Once Tasks 1–2 were green (`fix(gen): emit public JsonConverter for [TypedId]`,
commit `cd84a93`), Task 3 stood up a separate test project with a
`[JsonSerializable(typeof(InteropOrderId))]` `JsonSerializerContext`. All
three runtime round-trip tests failed silently — values serialized to
`{}` and deserialized to `default`. Inspecting the STJ-emitted source
under `obj/.../System.Text.Json.SourceGeneration/` revealed that STJ's
generator was emitting `JsonMetadataServices.CreateObjectInfo<T>` (POCO
mode) for the TypedId, not `CreateValueInfo` (converter mode). The
runtime fallback `TryGetTypeInfoForRuntimeCustomConverter` only inspects
`options.Converters` at runtime, never `[JsonConverter]` attributes —
so the attribute the TypedId generator emitted was effectively dead.

Sanity check: reverting the writer to `internal sealed class` did **not**
trip SYSLIB1220. STJ never reaches the converter to check accessibility;
it's looking for the attribute and not finding it.

### Root cause

Roslyn runs source generators against the *original* compilation in
parallel. Generators do not see each other's emitted output. The
TypedId generator's partial declaration carrying
`[JsonConverter(typeof(...))]` is invisible to STJ's source generator,
so STJ falls through to its POCO path. This is a Roslyn-level
isolation, not something the TypedId generator can fix from inside its
own emit.

### Original failure-mode claim (SYSLIB1220 / SYSLIB1030)

The problem statement cited diagnostics observed during the
`za-cqrs-es` Task 1 attempt. We could not reproduce those diagnostics
from a fresh `[JsonSerializable]` + `[TypedId]` shape — STJ silently
goes POCO instead. The original report was likely a different
configuration (e.g. `JsonSourceGenerationMode.Metadata`) or an
artifact of partially-staged code at that time. The bug as
*re-diagnosed* is "STJ source-gen silently produces wrong output," not
"STJ emits SYSLIB1220."

### What shipped

The `public` widening (commit `cd84a93`) stayed. It is still required
for the only currently-working source-gen interop pattern:

```csharp
options.Converters.Add(new OrderId.TypedIdJsonConverter());
options.TypeInfoResolver = AppJsonContext.Default;
```

Cross-assembly consumers need the converter to be instantiable from
outside its declaring assembly — which `internal` doesn't allow. The
docs at `docs/typed-id/json.md` and `docs/typed-id/internals.md` were
rewritten to describe this explicit-registration pattern honestly and
to remove the "auto-discovered via source-gen" claim, which never
held.

### Cancelled scope

Tasks 3 (cross-assembly compile-time + runtime test project), 4 (AOT
smoke + CI workflow), and 6 (final verification of the four-test
posture) of the original plan assumed the auto-discovery scenario
worked. With the explicit-registration pattern the right test surface
is a small xUnit fact registering the converter on options and
round-tripping — not a full new test project with `TreatWarningsAsErrors`
as a SYSLIB1220 trap. That trap doesn't fire under the actual failure
mode.

### Future work — proper source-gen interop

If the design's original goal (zero-config STJ source-gen interop)
remains valuable, it requires a different approach — a candidate is to
emit an `IJsonTypeInfoResolver` extension from the TypedId generator
that consumers chain into their context via
`JsonSerializerOptions.TypeInfoResolverChain.Add(...)`. That resolver
would manufacture `JsonTypeInfo<T>` with the converter pre-bound,
sidestepping cross-generator attribute visibility. This is a separate,
larger initiative and out of scope for 1.6.1.
