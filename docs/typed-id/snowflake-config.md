---
id: typed-id-snowflake-config
title: Snowflake Worker ID Configuration
slug: /docs/typed-id/snowflake-config
description: Configure Snowflake worker IDs via DI, environment variable, or factory, and coordinate uniqueness across pods.
sidebar_position: 23
---

# Snowflake Worker ID Configuration

Snowflake IDs encode a 10-bit worker ID (0..1023) into every value. The worker ID must be unique across all processes minting the same `[TypedId]` type, or they will silently produce colliding identifiers.

This page documents the three `AddSnowflakeWorkerId` overloads, the env-var fallback, and orchestrator patterns for Kubernetes, Docker Swarm, and Nomad.

## Three overloads

```csharp
// 1. Literal integer — for single-instance deployments or local dev
public static IServiceCollection AddSnowflakeWorkerId(
    this IServiceCollection services, int workerId);

// 2. Environment variable with numeric fallback
public static IServiceCollection AddSnowflakeWorkerId(
    this IServiceCollection services, string envVar, int fallback = 0);

// 3. Factory with access to IServiceProvider
public static IServiceCollection AddSnowflakeWorkerId(
    this IServiceCollection services, Func<IServiceProvider, int> factory);
```

Each overload registers an `IHostedService` that reads the worker ID on `StartAsync` and publishes it to `TypedIdRuntime.SnowflakeProvider`. The first `Snowflake.New()` call after the host has started sees the provider.

### Literal

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSnowflakeWorkerId(workerId: 5);
```

Good for: local development, single-instance services, scheduled jobs where only one replica ever runs.

### Environment variable

```csharp
// Reads $POD_ORDINAL at host start; uses fallback = 0 if unset.
builder.Services.AddSnowflakeWorkerId(envVar: "POD_ORDINAL", fallback: 0);
```

The env var must parse to an integer in `[0, 1023]`. Out-of-range values throw `TypedIdException` during host start.

### Factory

```csharp
builder.Services.AddSnowflakeWorkerId(sp =>
    sp.GetRequiredService<IMachineIdProvider>().Id);
```

Use when the worker ID comes from another service — a central registry, a leader-elected coordinator, or a custom algorithm over hostname.

## Resolution order at runtime

When `Snowflake.New()` runs for the first time in a process:

1. If `TypedIdRuntime.SnowflakeProvider` is set (i.e. `AddSnowflakeWorkerId` ran via the host) → use it.
2. Else read env var `ZA_SNOWFLAKE_WORKER_ID`. If present and parseable → use it.
3. Else throw `TypedIdException` with a message that names both `AddSnowflakeWorkerId` and `ZA_SNOWFLAKE_WORKER_ID`.

This means **console apps and short-lived scripts that cannot run a host** can still mint IDs by setting `ZA_SNOWFLAKE_WORKER_ID` in the environment:

```bash
ZA_SNOWFLAKE_WORKER_ID=7 dotnet run
```

## Without configuration

```csharp
[TypedId(Strategy = IdStrategy.Snowflake)]
public readonly partial record struct MessageId;

var id = MessageId.New();
// throws TypedIdException:
//   "Snowflake worker ID not configured. Call
//    builder.Services.AddSnowflakeWorkerId(...) during startup,
//    or set the ZA_SNOWFLAKE_WORKER_ID environment variable."
```

No random fallback, no default. Duplicate worker IDs across nodes are worse than a startup crash — the generator refuses to pick one for you.

## Kubernetes pod-ordinal pattern

A `StatefulSet` gives each replica a stable ordinal (`0`, `1`, `2`, …). Expose it to the container and pass it in:

```yaml
# k8s-deployment.yaml (StatefulSet)
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: orders
spec:
  replicas: 8
  serviceName: orders
  template:
    spec:
      containers:
        - name: api
          image: orders:latest
          env:
            - name: POD_ORDINAL
              valueFrom:
                fieldRef:
                  fieldPath: metadata.labels['apps.kubernetes.io/pod-index']
```

```csharp
// Program.cs
builder.Services.AddSnowflakeWorkerId(envVar: "POD_ORDINAL");
```

For `Deployment` (not `StatefulSet`), derive a worker ID from the pod name hash or use a centralized allocator — the `metadata.labels['apps.kubernetes.io/pod-index']` field is only set on StatefulSets.

## Docker Swarm

Swarm exposes `{{.Task.Slot}}` per replica:

```yaml
# docker-compose.yml
services:
  orders:
    image: orders:latest
    environment:
      SNOWFLAKE_WORKER_ID: "{{.Task.Slot}}"
    deploy:
      replicas: 4
```

```csharp
builder.Services.AddSnowflakeWorkerId(envVar: "SNOWFLAKE_WORKER_ID");
```

## Nomad

Nomad's alloc index is exposed via `NOMAD_ALLOC_INDEX`:

```hcl
job "orders" {
  group "api" {
    count = 4
    task "orders" {
      env {
        SNOWFLAKE_WORKER_ID = "${NOMAD_ALLOC_INDEX}"
      }
    }
  }
}
```

```csharp
builder.Services.AddSnowflakeWorkerId(envVar: "SNOWFLAKE_WORKER_ID");
```

## Production warning

> Duplicate worker IDs silently produce colliding IDs.

`AddSnowflakeWorkerId` cannot detect duplicates — two pods with the same ordinal will each believe they are worker `3` and emit overlapping ID streams. Concretely:

- Two pods, same worker ID, same wall-clock millisecond, same sequence number → two rows with identical primary keys, one violates the unique constraint at insert time.
- Different wall-clock milliseconds → IDs look distinct but the Snowflake space is silently partitioned into two colliding ranges.

Mitigations, in order of preference:

1. **Use orchestrator ordinals** (StatefulSet, Swarm Slot, Nomad alloc index). They are unique by construction.
2. **Use a central registry** — hand out worker IDs from Redis/Zookeeper at startup and release them on shutdown.
3. **Hash hostnames into `[0, 1023)`** only as a last resort, and monitor for collisions.

Do not hard-code the same literal in every replica's deployment.

## Clock skew and sequence exhaustion

Even with correct worker IDs, Snowflake generation can fail at runtime:

- Small clock rollbacks (≤5s) are absorbed — the generator pins to the last observed millisecond until wall-clock catches up.
- Severe clock rollbacks or a per-millisecond sequence that exceeds 4096 throw `OverflowException`. Caller should retry on next tick.

See the [Production checklist](production.md) for operational guidance.
