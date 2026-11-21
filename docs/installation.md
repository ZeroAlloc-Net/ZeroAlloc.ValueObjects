---
id: installation
title: Installation
slug: /docs/installation
description: NuGet setup and .NET version requirements for ZeroAlloc.ValueObjects.
sidebar_position: 4
---

# Installation

## NuGet

```
dotnet add package ZeroAlloc.ValueObjects
```

The package ships both the marker attributes and the Roslyn source generator in a single NuGet package. No separate analyzer package is required.

## Requirements

| Requirement | Minimum |
|---|---|
| Target framework | `net10.0` or `net11.0` |
| .NET SDK | 10.0.100 or later, or Visual Studio 2026; 10.0.400 or later with `TreatWarningsAsErrors`, see below |

The packages ship `net10.0` and `net11.0` assemblies; `ZeroAlloc.ValueObjects.EfCore` and `ZeroAlloc.ValueObjects.Hosting` target the same two. Version 2.x also ran on `netstandard2.0`, `netstandard2.1`, .NET 8 and .NET 9; a project on one of those stays on 2.x.

The source generator itself targets `netstandard2.0`, as Roslyn requires; that is the compiler's target, not your project's. It is built against Roslyn 5.0, the compiler of the .NET 10.0.1xx SDK, so every .NET 10 and .NET 11 SDK loads it. Version 2.4.1 referenced Roslyn 5.9, which .NET 10 SDK bands before 10.0.4xx skip with warning CS9057, so nothing was generated there.

**`TreatWarningsAsErrors` on SDK bands before 10.0.4xx.** The `ZeroAlloc.Serialisation` package this package depends on ships its own generator, built against Roslyn 5.9. On .NET 10 SDK bands before 10.0.4xx the compiler skips that generator and reports warning CS9057. The ValueObjects generator still runs, but a build with `TreatWarningsAsErrors` fails on the warning. Use SDK 10.0.4xx or later for such builds until ZeroAlloc.Serialisation ships a generator built against Roslyn 5.0, tracked in [ZeroAlloc-Net/.github#77](https://github.com/ZeroAlloc-Net/.github/issues/77).

## Verifying the install

After installing, rebuild your project. If the generator is active, you can inspect generated files in Visual Studio by expanding:

```
Project → Dependencies → Analyzers → ZeroAlloc.ValueObjects.Generator → ZeroAlloc.ValueObjects.Generator.ValueObjectGenerator
```

Or with the CLI:

```
dotnet build --verbosity normal
```

Look for lines like:

```
ZeroAlloc.ValueObjects.Generator → Money.g.cs
```
