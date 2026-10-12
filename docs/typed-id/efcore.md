---
id: typed-id-efcore
title: EF Core Integration
slug: /docs/typed-id/efcore
description: ZeroAlloc.ValueObjects.EfCore — generated, NativeAOT-safe converter registration, the assembly-scan convention, column types, and per-property overrides.
sidebar_position: 26
---

# EF Core Integration

The `ZeroAlloc.ValueObjects.EfCore` package provides a generic `TypedIdValueConverter<TId, TBacking>` and two ways to register it for every `[TypedId]` struct, so EF Core maps each to its backing column type without a per-property `HasConversion`:

- a **generated registration**, which names every struct at compile time. It needs no reflection and no assembly scan, so it is trim-safe and NativeAOT-safe. Use it by default.
- `AddTypedIdConventions()`, which **scans an assembly** with reflection at model-build time. It is marked `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`, so a trimmed or NativeAOT app gets IL2026 and IL3050 warnings for it.

## Install

```bash
dotnet add package ZeroAlloc.ValueObjects.EfCore
```

The adapter depends on `Microsoft.EntityFrameworkCore`. Pair it with your provider of choice (`Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.SqlServer`, etc.).

## Register the converters

```csharp
public sealed class AppDbContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<User>  Users  => Set<User>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.AddMyAppTypedIdConventions();   // generated for the assembly MyApp
    }
}
```

That is the only setup. No per-property `HasConversion<>()`, no per-entity builder. Run migrations and the columns land with the right types.

## The generated registration

In a project that references `ZeroAlloc.ValueObjects.EfCore`, the `[TypedId]` generator adds two things. A project without the reference gets neither, so the core package never needs EF Core.

**On each `[TypedId]` struct**, a static method that registers its converter:

```csharp
// generated for [TypedId(Strategy = IdStrategy.Ulid)] public readonly partial record struct OrderId;
public static ModelConfigurationBuilder AddTypedIdConvention(ModelConfigurationBuilder builder)
{
    ArgumentNullException.ThrowIfNull(builder);
    builder.Properties<OrderId>().HaveConversion<TypedIdValueConverter<OrderId, Guid>>();
    return builder;
}
```

`Properties<TId>()` covers nullable `TId?` properties too. Call it directly to register a single ID: `OrderId.AddTypedIdConvention(builder);`.

**For the assembly**, an extension method on `ModelConfigurationBuilder` that calls every struct's `AddTypedIdConvention`:

```csharp
namespace Microsoft.EntityFrameworkCore;

public static class MyAppTypedIdModelConfigurationExtensions
{
    public static ModelConfigurationBuilder AddMyAppTypedIdConventions(this ModelConfigurationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        global::MyApp.Orders.OrderId.AddTypedIdConvention(builder);
        global::MyApp.Users.UserId.AddTypedIdConvention(builder);
        return builder;
    }
}
```

The method is named after the assembly with every character that cannot appear in an identifier removed, as ZeroAlloc.Inject names `Add{Assembly}Services`: assembly `Shop.Domain` gets `AddShopDomainTypedIdConventions()` in class `ShopDomainTypedIdModelConfigurationExtensions`. It lives in the `Microsoft.EntityFrameworkCore` namespace, so it needs no extra `using`. The assembly name is part of it, so the classes that two assemblies generate never clash.

### TypedIds in another assembly

The generator runs in the project that declares the structs. If your TypedIds live in a domain project and the DbContext in another, reference `ZeroAlloc.ValueObjects.EfCore` from the domain project and call its generated method:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
{
    builder.AddShopDomainTypedIdConventions();
    builder.AddShopBillingTypedIdConventions();   // one call per assembly that declares TypedIds
}
```

### Keeping the class internal

The generated class is `public` by default, so another assembly can call it. To keep it out of a library's public API, set the org-wide `ZeroAllocGeneratedAccessibility` MSBuild property, which ZeroAlloc generators honour:

```xml
<PropertyGroup>
  <ZeroAllocGeneratedAccessibility>Internal</ZeroAllocGeneratedAccessibility>
</PropertyGroup>
```

`Internal` emits the class and its method `internal`. `Public`, or leaving the property unset, keeps them public; any other value is error [ZATI010](diagnostics.md#zati010). The `AddTypedIdConvention` member on each struct keeps the struct's own accessibility.

### Private nested TypedIds

A struct that is `private`, `protected` or `private protected`, or nested in such a type, cannot be named from the generated class and is left out of it. Register it with its own `AddTypedIdConvention`, called from inside its containing type.

## The assembly scan

`AddTypedIdConventions()` is the reflection-based alternative for apps that are neither trimmed nor published with NativeAOT. It defaults to the calling assembly. At model-build time it:

1. Uses `Assembly.GetTypes()` once to enumerate structs annotated with `[TypedId]`.
2. For each hit, instantiates `TypedIdValueConverter<TId, TBacking>` with the resolved backing type (`Guid` or `long`).
3. Calls `builder.Properties<TId>().HaveConversion<...>()` so every property of that type picks up the converter.

```csharp
using ZeroAlloc.ValueObjects.EfCore;

protected override void ConfigureConventions(ModelConfigurationBuilder builder)
{
    builder.AddTypedIdConventions(scan: typeof(OrderId).Assembly);
}
```

You can call it multiple times for multiple assemblies. The scan runs **once** per `ModelConfigurationBuilder` invocation. After that, EF Core caches the compiled converter, with no reflection on the hot path. Trimming can remove the structs it looks for, and NativeAOT cannot create the generic instantiations it closes at run time, which is why the generated registration is the default.

## NativeAOT

The generated registration and `TypedIdValueConverter<TId, TBacking>` are trim-safe and NativeAOT-safe. The repo's AOT smoke publishes them with `TreatWarningsAsErrors` and runs the converter both ways. EF Core 9 itself is not fully NativeAOT-compatible: it marks the `DbContext` constructor `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`, and needs a compiled model and precompiled queries under NativeAOT. Those requirements are EF Core's; registering the TypedId converters adds none.

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

The per-property configuration wins over the convention.

## Nullable TypedId properties

Nullable TypedId properties compose naturally with either registration:

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
        builder.AddMyAppTypedIdConventions();
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
2. Add `builder.Add{Assembly}TypedIdConventions()` if you haven't already.
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

## What the conventions don't do

- They don't touch owned-entity configuration, keys, or indexes. Those remain on your `OnModelCreating` as usual.
- They don't autogenerate values — `OrderId.New()` is your responsibility, typically in domain logic or entity constructors.
- They don't interact with `DbContext.SaveChanges` — the converter runs at materialization time.

For the reflection model and caching behaviour see [Internals](internals.md).
