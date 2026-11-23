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
| .NET SDK | 10.0.100 or later, or Visual Studio 2026 or later |
| C# language version | 9.0 or later |
| Roslyn | 5.0 or later (included in the .NET 10 SDK) |

The generator itself targets `netstandard2.0` for broad compatibility, so it works in any modern project regardless of your target framework. It is built against Roslyn 5.0, and an older compiler skips it with warning CS9057, after which the generated members are missing. Version 2.4.1 was built against Roslyn 5.9 and loads only in SDK 10.0.4xx or later; on an older .NET 10 SDK, use 2.4.2 or later.

The `ZeroAlloc.Serialisation` package this package depends on ships its own generator. From 2.4.3 the dependency is Serialisation 2.4.8 or later, whose generator is also built against Roslyn 5.0. Versions 2.4.2 and earlier depended on Serialisation 2.4.2, built against Roslyn 5.9: SDK bands before 10.0.4xx skipped it with warning CS9057, which failed a build with `TreatWarningsAsErrors`.

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
