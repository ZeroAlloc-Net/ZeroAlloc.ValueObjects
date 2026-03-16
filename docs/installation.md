# Installation

## NuGet

```
dotnet add package ZeroAlloc.ValueObjects
```

The package ships both the marker attributes and the Roslyn source generator in a single NuGet package. No separate analyzer package is required.

## Requirements

| Requirement | Minimum |
|---|---|
| .NET SDK | 6.0 or later |
| C# language version | 9.0 or later |
| Roslyn | 4.x (included in VS 2022 / .NET SDK 6+) |

The generator itself targets `netstandard2.0` for broad compatibility, so it works in any modern project regardless of your target framework.

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
