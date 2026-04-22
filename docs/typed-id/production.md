---
id: typed-id-production
title: TypedId Production Checklist
slug: /docs/typed-id/production
description: Operational caveats for Snowflake worker IDs, clock skew, Sequential misuse, restart safety, and index behaviour.
sidebar_position: 28
---

# Production Checklist

Everything on this page is about avoiding surprise at 3 a.m. The TypedId generator is zero-config for ULID and UUIDv7, but Snowflake and Sequential have operational constraints that only bite under production load.

## 1. Sequential is not for production

`Sequential` is `Interlocked.Increment` on a static counter. The counter resets on process restart.

What breaks if you use Sequential in production:

- Two replicas each mint IDs starting at `1`. Every insert collides.
- A single replica restarts and re-issues `1, 2, 3, …`, colliding with whatever was already persisted.
- No time ordering. No cross-instance uniqueness. No durability.

Sequential exists so tests can write assertions like `Assert.Equal(OrderId.Parse("5"), order.Id)` without brittleness. Keep it in test projects.

If you need monotonic `long` IDs in production, use **Snowflake** (requires worker ID) or a database sequence (requires a round-trip at insert time).

## 2. Snowflake worker-ID uniqueness

Every process minting Snowflake IDs needs a unique `[0, 1023]` worker ID. Duplicates silently produce collisions:

```
pod-0 (worker 3, ms 1748213984512, seq 0) → 1748213984512000003_bits
pod-1 (worker 3, ms 1748213984512, seq 0) → 1748213984512000003_bits   ← same value
```

Orchestrator ordinals (Kubernetes StatefulSet index, Docker Swarm `{{.Task.Slot}}`, Nomad `NOMAD_ALLOC_INDEX`) are unique by construction and are the preferred source. See [Snowflake configuration](snowflake-config.md) for wiring.

Audit checklist before shipping:

- [ ] Every replica set's worker-ID source is an ordinal, not a literal.
- [ ] The env var / factory is evaluated once at host start, not per request.
- [ ] The workload is a `StatefulSet` (not a `Deployment`) if you're relying on pod index.
- [ ] Total replica count ≤ 1024. Above that, Snowflake can't represent a unique worker per replica.

## 3. Clock skew

Snowflake encodes a 41-bit wall-clock timestamp. Backwards clock drift is a direct threat.

Behaviour:

- Small rollbacks (≤5 s): the generator pins to the last observed millisecond and spins the sequence until wall-clock catches up. IDs remain strictly increasing.
- Larger rollbacks (>5 s) or a per-millisecond sequence that exceeds 4096 throw `OverflowException`. Callers should retry on the next tick, or surface the error as 503.

Operational expectations:

- Run NTP (`chrony`, `ntpd`, `systemd-timesyncd`) with the default gentle-slew behaviour. Monitor `offset` and `step` counters.
- Accept a brief unavailability window on severe skew — it is **correct** for the generator to refuse to mint IDs rather than re-use a previous timestamp.
- If you run on virtualized hosts where the hypervisor jumps the clock at boot, pin VM time sources to the host's reliable clock.

ULID and UUIDv7 use wall-clock milliseconds as well, but they pad with 80 / 74 bits of randomness, so collisions from clock skew are astronomically unlikely. They tolerate any skew a real-world clock produces.

## 4. Restart safety

| Strategy | Restart-safe? | Notes |
|---|---|---|
| `Ulid` | Yes | No in-memory state across generators. Randomness + wall-clock makes every `New()` globally unique. |
| `Uuid7` | Yes | Same as ULID — no shared state. |
| `Snowflake` | Yes **iff** the worker ID is stable | The in-memory last-timestamp + sequence counter resets, but that's fine: a new millisecond will arrive. Same worker ID across restarts is required. |
| `Sequential` | No | Counter resets to zero. |

For Snowflake, ensure the ordinal that feeds the worker ID survives restarts:

- StatefulSet pod ordinals are stable across rescheduling — ✓.
- Deployment replicas are not — avoid for Snowflake producers.

## 5. Backup and restore

TypedId values are just `Guid` or `long` at rest. Standard backup strategies apply:

- Do not "normalize" or regenerate IDs during restore. New IDs would break foreign-key relationships.
- When importing data across environments (staging → prod, or a merge), the values must remain unique in the destination. Snowflake's 41+10+12 layout isolates ranges by worker, so merging two datasets with disjoint worker IDs is safe. Merging datasets that used overlapping worker IDs is not.
- Sequential-generated data cannot safely be merged between environments — accept that as the cost of using Sequential.

## 6. Database index behaviour

Index layout is strategy-specific because each format clusters differently:

| Strategy | Backing | Clustered sort behaviour |
|---|---|---|
| `Ulid` | `Guid` (stored as 16 bytes) | Lexicographic on base32 = chronological. B-tree inserts are near-sequential — minimal page splits, good for range scans on recency. |
| `Uuid7` | `Guid` | Timestamp is in the high bytes, so the numeric sort is chronological too. Same behaviour as ULID. |
| `Snowflake` | `bigint` | Monotonically increasing numeric. Best possible insert pattern — always appended to the right-most leaf page. |
| `Sequential` | `bigint` | Same as Snowflake within a process, but meaningless across processes. |

Note on SQL Server: `uniqueidentifier` columns default to `NEWID()` when generated server-side, which yields random, not monotonic, values. ULID and UUIDv7 give you Guids that sort monotonically — you keep the column type *and* get clustered-index locality. Don't add `HasDefaultValueSql("NEWID()")` — you'd discard that property.

PostgreSQL `uuid` has no such footgun.

## 7. Nullable TypedId for optional relationships

Use `OrderId?` for optional foreign keys. EF Core maps it to a nullable column. The JSON converter also handles `null` correctly. This is normal C# value-type nullability — no special handling needed.

## 8. Observability

- Log `id.ToString()`, not `id.Value`. The string form is the interoperable representation across services and storage formats.
- Snowflake IDs are safe to log — they contain no secrets. ULID/UUID7 are similarly non-sensitive.
- If you use TypedIds as correlation IDs, ULID or UUIDv7 gives you chronological ordering in log aggregators without a separate timestamp column.

## 9. Migration path from untyped IDs

If you're adopting TypedId on an existing system with bare `Guid`/`long` primary keys:

1. Keep the column type unchanged.
2. Introduce the TypedId struct with the matching backing (`Ulid` for `uniqueidentifier`, `Snowflake` if you're moving to `bigint`).
3. Change the C# property type from `Guid` to `OrderId`. EF Core emits an empty migration.
4. Stop generating IDs with `Guid.NewGuid()`; switch to `OrderId.New()`.
5. Old IDs continue to work — any byte pattern is a valid `Guid`, so `OrderId.Parse(existingGuid.ToString("N"))` succeeds for anything stored previously.

No data migration. No downtime. See [EF Core integration](efcore.md#migration-considerations) for the full procedure.

## 10. Quick audit questions

Before shipping a TypedId-heavy service:

- [ ] Which strategies are in use? Sequential in production is a bug.
- [ ] For each Snowflake type, what feeds the worker ID in every deployment target (prod, staging, local)?
- [ ] Do replicas run as StatefulSets (stable ordinals) or Deployments (ephemeral)?
- [ ] Is NTP running and monitored?
- [ ] Are TypedIds logged as their string form?
- [ ] Does the DbContext call `AddTypedIdConventions()` exactly once?

If you can answer all ten in the affirmative, you are probably fine. The remainder is ordinary operations hygiene.
