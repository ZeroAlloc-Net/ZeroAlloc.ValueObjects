# ZeroAlloc.ValueObjects

[![NuGet](https://img.shields.io/nuget/v/ZeroAlloc.ValueObjects.svg)](https://www.nuget.org/packages/ZeroAlloc.ValueObjects)
[![Build](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/actions/workflows/ci.yml/badge.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/ZeroAlloc-Net/ZeroAlloc.ValueObjects/graph/badge.svg)](https://codecov.io/gh/ZeroAlloc-Net/ZeroAlloc.ValueObjects)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/blob/main/LICENSE)
[![AOT](https://img.shields.io/badge/AOT--Compatible-passing-brightgreen)](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/MarcelRoozekrans?style=flat&logo=githubsponsors&color=ea4aaa&label=Sponsor)](https://github.com/sponsors/MarcelRoozekrans)

**Source-generated value objects and strongly-typed IDs for .NET: `record`-speed equality on the classes and structs you already have, with no allocation and no base class, NativeAOT-ready.**

## Why ZeroAlloc.ValueObjects

- **Equality allocates nothing.** `Equals`, `GetHashCode`, `==` and `!=` allocate 0 B on a class and on a struct, asserted by tests. CSharpFunctionalExtensions' `ValueObject` allocates 96 B per `Equals` in the same benchmark.
- **As fast as `record`, without rewriting your types to records.** Equality on a two-field value object runs in the same time as `record` and 14.6× faster than CSharpFunctionalExtensions. Equality on a single-`int` value object is 13× faster than Vogen's.
- **Works under NativeAOT and trimming.** No reflection in generated code, including the EF Core converter registration. A NativeAOT smoke app is published and run in CI.
- **Mistakes fail the build.** An ID strategy that does not fit its backing type, such as Snowflake on a `Guid`, is compile error ZATI001.

## Install

```bash
dotnet add package ZeroAlloc.ValueObjects
```

## Quick start

```csharp
using ZeroAlloc.ValueObjects;

var price = new Money(10m, "EUR");
var prices = new HashSet<Money> { price, new Money(10m, "EUR") };
Console.WriteLine(price == new Money(10m, "EUR"));       // True: compared by value, 0 B
Console.WriteLine(prices.Count);                         // 1: safe as a set or dictionary key

OrderId id = OrderId.New();                              // sortable ULID
Console.WriteLine($"{id} {OrderId.Parse(id.ToString()) == id}");

[ValueObject]
public partial class Money
{
    public decimal Amount { get; }
    public string Currency { get; }
    public Money(decimal amount, string currency) => (Amount, Currency) = (amount, currency);
}

[TypedId(Strategy = IdStrategy.Ulid)]
public readonly partial record struct OrderId;
```

`Money` keeps its constructor, its base class and its shape. The generator adds the equality members and a `ToString`, without the `with`, `Deconstruct` and `EqualityContract` a `record` brings.

## What you get

- **Value objects on existing types**: `[ValueObject]` on any `partial class` or `partial struct`, including classes that inherit from a non-record base.
- **Member control**: opt members in with `[EqualityMember]` or out with `[IgnoreEqualityMember]`. Nullable reference properties compare null-safely.
- **Strongly-typed IDs**: `[TypedId]` with ULID, UUIDv7, Snowflake and Sequential strategies, plus an assembly-wide default.
- **JSON** through a generated `System.Text.Json` converter that reads and writes with 0 B allocated, asserted by tests.
- **ASP.NET Core binding**: IDs implement `IParsable<T>` and `ISpanParsable<T>`, so `(OrderId id) => …` route parameters just work.
- **EF Core**: [ZeroAlloc.ValueObjects.EfCore](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/blob/main/docs/typed-id/efcore.md) maps every typed ID in an assembly with one generated, trim-safe call.
- **Snowflake worker IDs** from a number, an environment variable or a factory. [ZeroAlloc.ValueObjects.Hosting](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/blob/main/docs/typed-id/snowflake-config.md) adds a factory that resolves services.
- **Serialisation**: [ZeroAlloc.Serialisation](https://github.com/ZeroAlloc-Net/ZeroAlloc.Serialisation) writes single-value objects as their underlying value in JSON, MessagePack and MemoryPack.

## Performance

BenchmarkDotNet. Allocations are exact. The time column is the alternative's time divided by ZeroAlloc.ValueObjects', from the same run.

| Operation | ZeroAlloc.ValueObjects | Alternative | Alternative's time |
|---|---:|---:|---:|
| `Equals`, two-field `Money` | 0 B | CSharpFunctionalExtensions, 96 B | 14.6× |
| `GetHashCode`, two-field `Money` | 0 B | CSharpFunctionalExtensions, 88 B | 16.1× |
| `Equals`, single-`int` value object | 0 B | Vogen, 0 B | 13× |

`record` and `record struct` allocate 0 B and run in the same time as ZeroAlloc.ValueObjects on the two-field rows. The [performance page](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/blob/main/docs/performance.md) has every row, including `GetHashCode`, where Vogen ties, and the [typed-ID benchmarks](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/blob/main/docs/typed-id/benchmarks.md) cover generation and parsing per strategy.

## Learn more

- Documentation: [valueobjects.zeroalloc.net](https://valueobjects.zeroalloc.net). Good starting points: [why not just use `record`](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/blob/main/docs/why.md), [typed IDs](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/blob/main/docs/typed-id/index.md), the [production checklist for IDs](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/blob/main/docs/typed-id/production.md) and the diagnostics reference for [value objects](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/blob/main/docs/diagnostics.md) and [typed IDs](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/blob/main/docs/typed-id/diagnostics.md).
- Moving from CSharpFunctionalExtensions or hand-written equality: [migration guide](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/blob/main/docs/migration.md). Release history: [CHANGELOG.md](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/blob/main/CHANGELOG.md).
- Related packages: [ZeroAlloc.Mapping](https://github.com/ZeroAlloc-Net/ZeroAlloc.Mapping), [ZeroAlloc.Results](https://github.com/ZeroAlloc-Net/ZeroAlloc.Results), [ZeroAlloc.Serialisation](https://github.com/ZeroAlloc-Net/ZeroAlloc.Serialisation).
- Issues and pull requests are welcome on [GitHub](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/issues).
- MIT licensed. See [LICENSE](https://github.com/ZeroAlloc-Net/ZeroAlloc.ValueObjects/blob/main/LICENSE).
