---
id: typed-id-efcore
title: EF Core Integration
slug: /docs/typed-id/efcore
description: ZeroAlloc.ValueObjects.EfCore — assembly-scan conventions, column types, and per-property overrides.
sidebar_position: 26
---

# EF Core Integration

The `ZeroAlloc.ValueObjects.EfCore` package provides a `ModelConfigurationBuilder` extension and a generic `TypedIdValueConverter<TId, TBacking>`. Once you register the convention, every `[TypedId]` struct in the DbContext's assembly is mapped to the appropriate column type.

## Install

```bash
dotnet add package ZeroAlloc.ValueObjects.EfCore
```

The adapter depends on `Microsoft.EntityFrameworkCore`. Pair it with your provider of choice (`Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.SqlServer`, etc.).

## Register the convention

```csharp
using ZeroAlloc.ValueObjects.EfCore;

public sealed class AppDbContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<User>  Users  => Set<User>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.AddTypedIdConventions();
    }
}
```

That is the only setup. No per-property `HasConversion<>()`, no per-entity builder. Run migrations and the columns land with the right types.

## How the assembly-scan works

`AddTypedIdConventions()` defaults to the DbContext's assembly. At model-build time it:

1. Uses `Assembly.GetTypes()` once to enumerate structs annotated with `[TypedId]`.
2. For each hit, instantiates `TypedIdValueConverter<TId, TBacking>` with the resolved backing type (`Guid` or `long`).
3. Calls `builder.Properties<TId>().HaveConversion<...>()` so every property of that type picks up the converter.

The scan runs **once** per `ModelConfigurationBuilder` invocation. After that, EF Core caches the compiled converter — no reflection on the hot path.

### Scanning a different assembly

If your TypedIds live in a separate assembly from the DbContext:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
{
    builder.AddTypedIdConventions(scan: typeof(OrderId).Assembly);
}
```

You can call the extension multiple times for multiple assemblies.

## Default column types

| Strategy | Backing | SQL Server column | PostgreSQL column |
|---|---|---|---|
| `Ulid` | `Guid` | `uniqueidentifier` | `uuid` |
| `Uuid7` | `Guid` | `uniqueidentifier` | `uuid` |
| `Snowflake` | `long` | `bigint` | `bigint` |
| `Sequential` | `long` | `bigint` | `bigint` |

These follow the provider's default mapping for `Guid` and `long`. No `HasColumnType` override is needed.

## Per-property override

The convention sets a default — individual properties can override it:

```csharp
modelBuilder.Entity<Order>()
    .Property(o => o.Id)
    .HasConversion<TypedIdValueConverter<OrderId, Guid>>()
    .HasColumnType("char(36)");   // store as string for debuggability
```

Or swap to a string converter entirely:

```csharp
modelBuilder.Entity<Order>()
    .Property(o => o.Id)
    .HasConversion(
        id => id.ToString(),
        s  => OrderId.Parse(s, null));
```

The per-property configuration wins over the assembly-wide convention.

## Nullable TypedId properties

Nullable TypedId properties compose naturally with the convention:

```csharp
public sealed class Order
{
    public OrderId Id { get; init; }
    public UserId? AssignedTo { get; set; }   // nullable
}
```

EF Core maps `UserId?` to a nullable `uniqueidentifier` column; `null` round-trips through the converter.

## Example — full entity

```csharp
[TypedId(Strategy = IdStrategy.Ulid)]
public readonly partial record struct OrderId;

[TypedId(Strategy = IdStrategy.Ulid)]
public readonly partial record struct UserId;

public sealed class Order
{
    public OrderId Id { get; init; }
    public UserId UserId { get; init; }
    public decimal Total { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
}

public sealed class AppDbContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.AddTypedIdConventions();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Order>(e =>
        {
            e.HasKey(o => o.Id);
            e.HasIndex(o => o.UserId);
        });
    }
}
```

Resulting DDL (SQL Server):

```sql
CREATE TABLE [Orders] (
    [Id]       uniqueidentifier NOT NULL,
    [UserId]   uniqueidentifier NOT NULL,
    [Total]    decimal(18,2)    NOT NULL,
    [PlacedAt] datetimeoffset   NOT NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY ([Id])
);
CREATE INDEX [IX_Orders_UserId] ON [Orders] ([UserId]);
```

## Migration considerations

Adding TypedId to an existing model that used bare `Guid`/`long` columns is a *typed rename*, not a schema change. The column type stays the same:

1. Change the property type from `Guid` to `OrderId` (or `long` to `MessageId`).
2. Add `builder.AddTypedIdConventions()` if you haven't already.
3. Generate migrations — EF Core should produce an empty migration for the column.
4. Update any LINQ that constructed bare `Guid` / `long` values to construct the TypedId instead.

No data migration. No column rewrites. The converter is a symmetric mapping.

## ULID as char(26)

By default ULID-backed IDs store as `uniqueidentifier` / `uuid`. If you want the 26-char Crockford form in the database (e.g. for log correlation), opt in per-property:

```csharp
modelBuilder.Entity<Order>()
    .Property(o => o.Id)
    .HasConversion(
        id => id.ToString(),
        s  => OrderId.Parse(s, null))
    .HasColumnType("char(26)")
    .IsUnicode(false);
```

Trade-offs: strings index less efficiently than `uuid`/`uniqueidentifier`; ordering is still correct because ULID's base32 is lexicographically sortable.

## Testing

Use SQLite in-memory for convention round-trip tests. Keyed entities, query-by-id, and join scenarios all work with the default convention.

## What `AddTypedIdConventions` doesn't do

- It doesn't touch owned-entity configuration, keys, or indexes. Those remain on your `OnModelCreating` as usual.
- It doesn't autogenerate values — `OrderId.New()` is your responsibility, typically in domain logic or entity constructors.
- It doesn't interact with `DbContext.SaveChanges` — the converter runs at materialization time.

For the reflection model and caching behaviour see [Internals](internals.md).
