# ZeroAlloc.ValueObjects Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a Roslyn source generator that emits zero-allocation `Equals`/`GetHashCode` for ValueObject types, replacing the boxing+iterator pattern from CSharpFunctionalExtensions.

**Architecture:** Two NuGet packages — a `netstandard2.0` attributes-only runtime package and an `IIncrementalGenerator` that detects `[ValueObject]` partial classes/structs and emits direct member comparisons. No runtime dependencies. Generator uses Verify snapshot testing and BenchmarkDotNet for validation.

**Tech Stack:** .NET 8, Roslyn `Microsoft.CodeAnalysis.CSharp` 4.x, `IIncrementalGenerator`, `Verify.SourceGenerators`, `BenchmarkDotNet`, `CSharpFunctionalExtensions` (benchmark baseline only)

---

### Task 1: Solution & Project Scaffold

**Files:**
- Create: `ZeroAlloc.ValueObjects.sln`
- Create: `src/ZeroAlloc.ValueObjects/ZeroAlloc.ValueObjects.csproj`
- Create: `src/ZeroAlloc.ValueObjects.Generator/ZeroAlloc.ValueObjects.Generator.csproj`
- Create: `tests/ZeroAlloc.ValueObjects.Tests/ZeroAlloc.ValueObjects.Tests.csproj`

**Step 1: Create solution and projects**

```bash
mkdir ZeroAlloc.ValueObjects && cd ZeroAlloc.ValueObjects
dotnet new sln -n ZeroAlloc.ValueObjects
mkdir -p src/ZeroAlloc.ValueObjects src/ZeroAlloc.ValueObjects.Generator tests/ZeroAlloc.ValueObjects.Tests

dotnet new classlib -n ZeroAlloc.ValueObjects -o src/ZeroAlloc.ValueObjects --framework netstandard2.0
dotnet new classlib -n ZeroAlloc.ValueObjects.Generator -o src/ZeroAlloc.ValueObjects.Generator --framework netstandard2.0
dotnet new xunit -n ZeroAlloc.ValueObjects.Tests -o tests/ZeroAlloc.ValueObjects.Tests --framework net8.0

dotnet sln add src/ZeroAlloc.ValueObjects/ZeroAlloc.ValueObjects.csproj
dotnet sln add src/ZeroAlloc.ValueObjects.Generator/ZeroAlloc.ValueObjects.Generator.csproj
dotnet sln add tests/ZeroAlloc.ValueObjects.Tests/ZeroAlloc.ValueObjects.Tests.csproj
```

**Step 2: Configure Generator project**

Replace contents of `src/ZeroAlloc.ValueObjects.Generator/ZeroAlloc.ValueObjects.Generator.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
    <IsRoslynComponent>true</IsRoslynComponent>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.4" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

**Step 3: Configure Attributes project**

Replace contents of `src/ZeroAlloc.ValueObjects/ZeroAlloc.ValueObjects.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\ZeroAlloc.ValueObjects.Generator\ZeroAlloc.ValueObjects.Generator.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false"
                      PrivateAssets="all" />
  </ItemGroup>
</Project>
```

**Step 4: Configure Tests project**

Replace contents of `tests/ZeroAlloc.ValueObjects.Tests/ZeroAlloc.ValueObjects.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" />
    <PackageReference Include="xunit" Version="2.7.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.7" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />
    <PackageReference Include="Verify.SourceGenerators" Version="2.4.0" />
    <PackageReference Include="BenchmarkDotNet" Version="0.13.12" />
    <PackageReference Include="CSharpFunctionalExtensions" Version="2.42.0" />
    <ProjectReference Include="..\..\src\ZeroAlloc.ValueObjects.Generator\ZeroAlloc.ValueObjects.Generator.csproj" />
    <ProjectReference Include="..\..\src\ZeroAlloc.ValueObjects\ZeroAlloc.ValueObjects.csproj" />
  </ItemGroup>
</Project>
```

**Step 5: Build to verify scaffold compiles**

```bash
dotnet build
```
Expected: Build succeeded, 0 errors.

**Step 6: Commit**

```bash
git init
git add .
git commit -m "chore: scaffold ZeroAlloc.ValueObjects solution"
```

---

### Task 2: Attributes

**Files:**
- Create: `src/ZeroAlloc.ValueObjects/ValueObjectAttribute.cs`
- Create: `src/ZeroAlloc.ValueObjects/EqualityMemberAttribute.cs`
- Create: `src/ZeroAlloc.ValueObjects/IgnoreEqualityMemberAttribute.cs`
- Delete: `src/ZeroAlloc.ValueObjects/Class1.cs`

**Step 1: Write ValueObjectAttribute**

```csharp
// src/ZeroAlloc.ValueObjects/ValueObjectAttribute.cs
namespace ZeroAlloc.ValueObjects;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ValueObjectAttribute : Attribute
{
    /// <summary>
    /// When true, always generates a class even if auto-detection would choose struct.
    /// </summary>
    public bool ForceClass { get; set; }
}
```

**Step 2: Write EqualityMemberAttribute**

```csharp
// src/ZeroAlloc.ValueObjects/EqualityMemberAttribute.cs
namespace ZeroAlloc.ValueObjects;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class EqualityMemberAttribute : Attribute { }
```

**Step 3: Write IgnoreEqualityMemberAttribute**

```csharp
// src/ZeroAlloc.ValueObjects/IgnoreEqualityMemberAttribute.cs
namespace ZeroAlloc.ValueObjects;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class IgnoreEqualityMemberAttribute : Attribute { }
```

**Step 4: Build to verify**

```bash
dotnet build src/ZeroAlloc.ValueObjects/ZeroAlloc.ValueObjects.csproj
```
Expected: Build succeeded.

**Step 5: Commit**

```bash
git add src/ZeroAlloc.ValueObjects/
git commit -m "feat: add ValueObject, EqualityMember, IgnoreEqualityMember attributes"
```

---

### Task 3: Generator Scaffold + First Snapshot Test

**Files:**
- Create: `src/ZeroAlloc.ValueObjects.Generator/ValueObjectGenerator.cs`
- Create: `tests/ZeroAlloc.ValueObjects.Tests/GeneratorTests/MultiPropertyClassTests.cs`
- Create: `tests/ZeroAlloc.ValueObjects.Tests/GeneratorTests/ModuleInitializer.cs`

**Step 1: Write the failing snapshot test**

```csharp
// tests/ZeroAlloc.ValueObjects.Tests/GeneratorTests/ModuleInitializer.cs
using System.Runtime.CompilerServices;
using VerifyXunit;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init() => VerifySourceGenerators.Initialize();
}
```

```csharp
// tests/ZeroAlloc.ValueObjects.Tests/GeneratorTests/MultiPropertyClassTests.cs
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;
using ZeroAlloc.ValueObjects.Generator;

namespace ZeroAlloc.ValueObjects.Tests.GeneratorTests;

public class MultiPropertyClassTests
{
    [Fact]
    public Task GeneratesEquality_ForMultiPropertyClass()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            [ValueObject]
            public partial class Money
            {
                public decimal Amount { get; }
                public string Currency { get; }
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new ValueObjectGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .RunGenerators(compilation);

        return Verifier.Verify(driver);
    }

    private static CSharpCompilation CreateCompilation(string source) =>
        CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ValueObjectAttribute).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
}
```

**Step 2: Run test to verify it fails (generator doesn't exist yet)**

```bash
dotnet test tests/ZeroAlloc.ValueObjects.Tests/ --filter "GeneratesEquality_ForMultiPropertyClass"
```
Expected: FAIL — `ValueObjectGenerator` type not found.

**Step 3: Create minimal generator scaffold**

```csharp
// src/ZeroAlloc.ValueObjects.Generator/ValueObjectGenerator.cs
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.ValueObjects.Generator;

[Generator]
public sealed class ValueObjectGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // stub — will be implemented in next tasks
    }
}
```

Also delete `src/ZeroAlloc.ValueObjects.Generator/Class1.cs`.

**Step 4: Run test — now fails with empty snapshot**

```bash
dotnet test tests/ZeroAlloc.ValueObjects.Tests/ --filter "GeneratesEquality_ForMultiPropertyClass"
```
Expected: FAIL — Verify creates a `.received.txt` showing no generated files. This is expected — the snapshot exists but generator produces nothing yet.

**Step 5: Commit scaffold**

```bash
git add .
git commit -m "test: add first snapshot test for multi-property class generation"
```

---

### Task 4: Implement Generator — Detection Pipeline

**Files:**
- Modify: `src/ZeroAlloc.ValueObjects.Generator/ValueObjectGenerator.cs`
- Create: `src/ZeroAlloc.ValueObjects.Generator/Models/ValueObjectModel.cs`
- Create: `src/ZeroAlloc.ValueObjects.Generator/Pipeline/ValueObjectParser.cs`

**Step 1: Create the model**

```csharp
// src/ZeroAlloc.ValueObjects.Generator/Models/ValueObjectModel.cs
namespace ZeroAlloc.ValueObjects.Generator.Models;

internal sealed record ValueObjectModel(
    string Namespace,
    string TypeName,
    bool IsStruct,
    IReadOnlyList<EqualityProperty> Properties);

internal sealed record EqualityProperty(string Name, string TypeName, bool IsNullable);
```

**Step 2: Create the parser**

```csharp
// src/ZeroAlloc.ValueObjects.Generator/Pipeline/ValueObjectParser.cs
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZeroAlloc.ValueObjects.Generator.Models;

namespace ZeroAlloc.ValueObjects.Generator.Pipeline;

internal static class ValueObjectParser
{
    private const string ValueObjectAttributeName = "ZeroAlloc.ValueObjects.ValueObjectAttribute";
    private const string EqualityMemberAttributeName = "ZeroAlloc.ValueObjects.EqualityMemberAttribute";
    private const string IgnoreEqualityMemberAttributeName = "ZeroAlloc.ValueObjects.IgnoreEqualityMemberAttribute";

    public static bool IsValueObjectCandidate(SyntaxNode node) =>
        node is TypeDeclarationSyntax { AttributeLists.Count: > 0 };

    public static ValueObjectModel? Parse(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol) return null;

        var allProps = typeSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic)
            .ToList();

        // Determine opt-in vs opt-out mode
        bool hasExplicitMembers = allProps.Any(p =>
            p.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == EqualityMemberAttributeName));

        var properties = allProps
            .Where(p =>
            {
                var attrs = p.GetAttributes().Select(a => a.AttributeClass?.ToDisplayString()).ToList();
                if (hasExplicitMembers) return attrs.Contains(EqualityMemberAttributeName);
                return !attrs.Contains(IgnoreEqualityMemberAttributeName);
            })
            .Select(p => new EqualityProperty(
                p.Name,
                p.Type.ToDisplayString(),
                p.NullableAnnotation == NullableAnnotation.Annotated))
            .ToList();

        bool forceClass = ctx.Attributes.FirstOrDefault()
            ?.NamedArguments.FirstOrDefault(a => a.Key == "ForceClass").Value.Value is true;

        // Only emit as struct when the user declared a struct; never auto-promote a class to struct.
        bool isStruct = !forceClass && typeSymbol.TypeKind == TypeKind.Struct;

        return new ValueObjectModel(
            typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : typeSymbol.ContainingNamespace.ToDisplayString(),
            typeSymbol.Name,
            isStruct,
            properties);
    }
}
```

**Step 3: Wire the pipeline in the generator**

```csharp
// src/ZeroAlloc.ValueObjects.Generator/ValueObjectGenerator.cs
using Microsoft.CodeAnalysis;
using ZeroAlloc.ValueObjects.Generator.Models;
using ZeroAlloc.ValueObjects.Generator.Pipeline;

namespace ZeroAlloc.ValueObjects.Generator;

[Generator]
public sealed class ValueObjectGenerator : IIncrementalGenerator
{
    private const string ValueObjectAttributeFqn = "ZeroAlloc.ValueObjects.ValueObjectAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ValueObjectAttributeFqn,
                predicate: (node, _) => ValueObjectParser.IsValueObjectCandidate(node),
                transform: ValueObjectParser.Parse)
            .Where(m => m is not null)
            .Select((m, _) => m!);

        context.RegisterSourceOutput(models, Emit);
    }

    private static void Emit(SourceProductionContext ctx, ValueObjectModel model)
    {
        // stub — implemented in next tasks
    }
}
```

**Step 4: Build to verify**

```bash
dotnet build
```
Expected: Build succeeded.

**Step 5: Commit**

```bash
git add .
git commit -m "feat: add incremental generator detection pipeline and ValueObjectModel"
```

---

### Task 5: Implement Code Emission — Equals & GetHashCode

**Files:**
- Create: `src/ZeroAlloc.ValueObjects.Generator/Writers/SourceWriter.cs`
- Modify: `src/ZeroAlloc.ValueObjects.Generator/ValueObjectGenerator.cs`

**Step 1: Create the source writer**

```csharp
// src/ZeroAlloc.ValueObjects.Generator/Writers/SourceWriter.cs
using System.Text;
using ZeroAlloc.ValueObjects.Generator.Models;

namespace ZeroAlloc.ValueObjects.Generator.Writers;

internal static class SourceWriter
{
    public static string Write(ValueObjectModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(model.Namespace))
        {
            sb.AppendLine($"namespace {model.Namespace};");
            sb.AppendLine();
        }

        string typeKind = model.IsStruct ? "readonly partial struct" : "sealed partial class";
        sb.AppendLine($"{typeKind} {model.TypeName} : System.IEquatable<{model.TypeName}>");
        sb.AppendLine("{");

        WriteEquals(sb, model);
        WriteIEquatable(sb, model);
        WriteGetHashCode(sb, model);
        WriteOperators(sb, model);
        WriteToString(sb, model);

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void WriteEquals(StringBuilder sb, ValueObjectModel model)
    {
        sb.AppendLine($"    public override bool Equals(object? obj) =>");
        sb.AppendLine($"        obj is {model.TypeName} other && Equals(other);");
        sb.AppendLine();
    }

    private static void WriteIEquatable(StringBuilder sb, ValueObjectModel model)
    {
        sb.Append($"    public bool Equals({model.TypeName} other) =>");

        if (model.Properties.Count == 0)
        {
            sb.AppendLine(" true;");
        }
        else
        {
            var comparisons = model.Properties.Select(p =>
                p.IsNullable
                    ? $"(other.{p.Name} is null ? {p.Name} is null : {p.Name} == other.{p.Name})"
                    : $"{p.Name} == other.{p.Name}");
            sb.AppendLine();
            sb.AppendLine("        " + string.Join(" &&\n        ", comparisons) + ";");
        }
        sb.AppendLine();
    }

    private static void WriteGetHashCode(StringBuilder sb, ValueObjectModel model)
    {
        sb.AppendLine("    public override int GetHashCode()");
        sb.AppendLine("    {");

        if (model.Properties.Count == 0)
        {
            sb.AppendLine("        return 0;");
        }
        else if (model.Properties.Count <= 8)
        {
            var args = string.Join(", ", model.Properties.Select(p => p.Name));
            sb.AppendLine($"        return System.HashCode.Combine({args});");
        }
        else
        {
            sb.AppendLine("        var hc = new System.HashCode();");
            foreach (var p in model.Properties)
                sb.AppendLine($"        hc.Add({p.Name});");
            sb.AppendLine("        return hc.ToHashCode();");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void WriteOperators(StringBuilder sb, ValueObjectModel model)
    {
        sb.AppendLine($"    public static bool operator ==({model.TypeName} left, {model.TypeName} right) => left.Equals(right);");
        sb.AppendLine($"    public static bool operator !=({model.TypeName} left, {model.TypeName} right) => !left.Equals(right);");
        sb.AppendLine();
    }

    private static void WriteToString(StringBuilder sb, ValueObjectModel model)
    {
        if (model.Properties.Count == 0)
        {
            sb.AppendLine($"    public override string ToString() => \"{model.TypeName} {{ }}\";");
            return;
        }

        var parts = string.Join(", ", model.Properties.Select(p => $"{p.Name} = {{{p.Name}}}"));
        sb.AppendLine($"    public override string ToString() => $\"{model.TypeName} {{ {parts} }}\";");
    }
}
```

**Step 2: Wire emission into generator**

```csharp
// Replace the Emit method in ValueObjectGenerator.cs
private static void Emit(SourceProductionContext ctx, ValueObjectModel model)
{
    var source = SourceWriter.Write(model);
    ctx.AddSource($"{model.TypeName}.g.cs", source);
}
```

**Step 3: Run snapshot test**

```bash
dotnet test tests/ZeroAlloc.ValueObjects.Tests/ --filter "GeneratesEquality_ForMultiPropertyClass"
```
Expected: FAIL — Verify creates `MultiPropertyClassTests.GeneratesEquality_ForMultiPropertyClass.received.txt`. Open it and inspect: it should contain the generated `Money` class with `Equals`, `GetHashCode`, operators, `ToString`.

**Step 4: Accept the snapshot**

```bash
dotnet test tests/ZeroAlloc.ValueObjects.Tests/ -- --verify-accept-all
```
Or rename `.received.txt` → `.verified.txt` manually.

**Step 5: Run test to confirm it passes**

```bash
dotnet test tests/ZeroAlloc.ValueObjects.Tests/ --filter "GeneratesEquality_ForMultiPropertyClass"
```
Expected: PASS.

**Step 6: Commit**

```bash
git add .
git commit -m "feat: implement source emission - Equals, GetHashCode, operators, ToString"
```

---

### Task 6: Additional Snapshot Tests

**Files:**
- Create: `tests/ZeroAlloc.ValueObjects.Tests/GeneratorTests/SinglePropertyStructTests.cs`
- Create: `tests/ZeroAlloc.ValueObjects.Tests/GeneratorTests/EqualityMemberAttributeTests.cs`
- Create: `tests/ZeroAlloc.ValueObjects.Tests/GeneratorTests/NullablePropertyTests.cs`

**Step 1: Write tests for all scenarios**

```csharp
// tests/ZeroAlloc.ValueObjects.Tests/GeneratorTests/SinglePropertyStructTests.cs
public class SinglePropertyStructTests
{
    [Fact]
    public Task GeneratesClass_ForSingleStringProperty()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            [ValueObject]
            public partial class EmailAddress
            {
                public string Value { get; }
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    [Fact]
    public Task GeneratesReadonlyStruct_ForStructDeclaration()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            [ValueObject]
            public partial struct Amount
            {
                public decimal Value { get; }
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    private static GeneratorDriver RunGenerator(string source)
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
             MetadataReference.CreateFromFile(typeof(ValueObjectAttribute).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return CSharpGeneratorDriver.Create(new ValueObjectGenerator()).RunGenerators(compilation);
    }
}
```

```csharp
// tests/ZeroAlloc.ValueObjects.Tests/GeneratorTests/EqualityMemberAttributeTests.cs
public class EqualityMemberAttributeTests
{
    [Fact]
    public Task GeneratesEquality_OnlyForMarkedMembers()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            [ValueObject]
            public partial class Address
            {
                [EqualityMember] public string Street { get; }
                [EqualityMember] public string City { get; }
                public string Notes { get; }
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    [Fact]
    public Task GeneratesEquality_ExcludingIgnoredMembers()
    {
        var source = """
            using ZeroAlloc.ValueObjects;

            [ValueObject]
            public partial class Product
            {
                public string Name { get; }
                [IgnoreEqualityMember] public string InternalCode { get; }
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    // ... RunGenerator helper same as above
}
```

```csharp
// tests/ZeroAlloc.ValueObjects.Tests/GeneratorTests/NullablePropertyTests.cs
public class NullablePropertyTests
{
    [Fact]
    public Task GeneratesNullSafeComparison_ForNullableProperties()
    {
        var source = """
            #nullable enable
            using ZeroAlloc.ValueObjects;

            [ValueObject]
            public partial class Contact
            {
                public string Name { get; }
                public string? Email { get; }
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }
    // ... RunGenerator helper
}
```

**Step 2: Run all new tests — expect them to fail with missing snapshots**

```bash
dotnet test tests/ZeroAlloc.ValueObjects.Tests/ --filter "FullyQualifiedName~GeneratorTests"
```
Expected: FAIL — new `.received.txt` files created.

**Step 3: Inspect received files and accept if correct**

```bash
dotnet test tests/ZeroAlloc.ValueObjects.Tests/ -- --verify-accept-all
```

**Step 4: Run all tests — confirm all pass**

```bash
dotnet test tests/ZeroAlloc.ValueObjects.Tests/ --filter "FullyQualifiedName~GeneratorTests"
```
Expected: All PASS.

**Step 5: Commit**

```bash
git add .
git commit -m "test: add snapshot tests for struct, EqualityMember, nullable scenarios"
```

---

### Task 7: Functional Correctness Tests

**Files:**
- Create: `tests/ZeroAlloc.ValueObjects.Tests/FunctionalTests/MoneyTests.cs`

These tests use the generator on real types to verify runtime behavior.

**Step 1: Write functional tests**

```csharp
// tests/ZeroAlloc.ValueObjects.Tests/FunctionalTests/MoneyTests.cs
using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Tests.FunctionalTests;

[ValueObject]
public partial class Money
{
    public decimal Amount { get; }
    public string Currency { get; }
    public Money(decimal amount, string currency) => (Amount, Currency) = (amount, currency);
}

public class MoneyTests
{
    [Fact]
    public void Equals_ReturnTrue_WhenPropertiesMatch()
    {
        var a = new Money(10m, "USD");
        var b = new Money(10m, "USD");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenPropertiesDiffer()
    {
        var a = new Money(10m, "USD");
        var b = new Money(20m, "USD");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GetHashCode_ReturnsSameValue_ForEqualObjects()
    {
        var a = new Money(10m, "USD");
        var b = new Money(10m, "USD");
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void CanBeUsedAsDictionaryKey()
    {
        var dict = new Dictionary<Money, string>();
        var key = new Money(10m, "USD");
        dict[key] = "ten dollars";
        Assert.Equal("ten dollars", dict[new Money(10m, "USD")]);
    }

    [Fact]
    public void CanBeUsedInHashSet()
    {
        var set = new HashSet<Money> { new(10m, "USD"), new(10m, "USD"), new(20m, "EUR") };
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void OperatorEquals_Works()
    {
        Assert.True(new Money(10m, "USD") == new Money(10m, "USD"));
        Assert.False(new Money(10m, "USD") == new Money(10m, "EUR"));
    }

    [Fact]
    public void ToString_ContainsPropertyValues()
    {
        var money = new Money(10m, "USD");
        Assert.Contains("10", money.ToString());
        Assert.Contains("USD", money.ToString());
    }
}
```

**Step 2: Run functional tests**

```bash
dotnet test tests/ZeroAlloc.ValueObjects.Tests/ --filter "FullyQualifiedName~FunctionalTests"
```
Expected: All PASS.

**Step 3: Commit**

```bash
git add .
git commit -m "test: add functional correctness tests for generated ValueObject"
```

---

### Task 8: Benchmarks

**Files:**
- Create: `tests/ZeroAlloc.ValueObjects.Tests/Benchmarks/ValueObjectBenchmarks.cs`
- Create: `tests/ZeroAlloc.ValueObjects.Tests/Benchmarks/BenchmarkRunner.cs`

**Step 1: Create baseline CSharpFunctionalExtensions type**

```csharp
// tests/ZeroAlloc.ValueObjects.Tests/Benchmarks/ValueObjectBenchmarks.cs
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using CSharpFunctionalExtensions;
using ZeroAlloc.ValueObjects;

namespace ZeroAlloc.ValueObjects.Tests.Benchmarks;

// CFE baseline
public class CfeMoney : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }
    public CfeMoney(decimal amount, string currency) => (Amount, Currency) = (amount, currency);
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}

// ZeroAlloc generated
[ZeroAlloc.ValueObjects.ValueObject]
public partial class ZaMoney
{
    public decimal Amount { get; }
    public string Currency { get; }
    public ZaMoney(decimal amount, string currency) => (Amount, Currency) = (amount, currency);
}

[MemoryDiagnoser]
public class ValueObjectBenchmarks
{
    private readonly CfeMoney _cfeA = new(10m, "USD");
    private readonly CfeMoney _cfeB = new(10m, "USD");
    private readonly ZaMoney _zaA = new(10m, "USD");
    private readonly ZaMoney _zaB = new(10m, "USD");

    [Benchmark(Baseline = true)]
    public bool CFE_Equals() => _cfeA.Equals(_cfeB);

    [Benchmark]
    public bool ZeroAlloc_Equals() => _zaA.Equals(_zaB);

    [Benchmark]
    public int CFE_GetHashCode() => _cfeA.GetHashCode();

    [Benchmark]
    public int ZeroAlloc_GetHashCode() => _zaA.GetHashCode();
}
```

```csharp
// tests/ZeroAlloc.ValueObjects.Tests/Benchmarks/BenchmarkRunner.cs
// This is a manual test — run with: dotnet run -c Release
// NOT part of xunit suite
```

**Step 2: Add a console project to run benchmarks**

```bash
dotnet new console -n ZeroAlloc.ValueObjects.Benchmarks -o benchmarks/ZeroAlloc.ValueObjects.Benchmarks --framework net8.0
dotnet sln add benchmarks/ZeroAlloc.ValueObjects.Benchmarks/ZeroAlloc.ValueObjects.Benchmarks.csproj
```

Add to `benchmarks/ZeroAlloc.ValueObjects.Benchmarks/ZeroAlloc.ValueObjects.Benchmarks.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="BenchmarkDotNet" Version="0.13.12" />
  <PackageReference Include="CSharpFunctionalExtensions" Version="2.42.0" />
  <ProjectReference Include="..\..\src\ZeroAlloc.ValueObjects\ZeroAlloc.ValueObjects.csproj" />
</ItemGroup>
```

Move benchmark types there and wire up `BenchmarkRunner.Run<ValueObjectBenchmarks>(args)`.

**Step 3: Run benchmarks**

```bash
dotnet run -c Release --project benchmarks/ZeroAlloc.ValueObjects.Benchmarks
```
Expected: ZeroAlloc variants show 0 B allocated and ~10-15x faster than CFE baseline.

**Step 4: Commit**

```bash
git add .
git commit -m "bench: add BenchmarkDotNet comparison vs CSharpFunctionalExtensions"
```

---

### Task 9: NuGet Packaging

**Files:**
- Modify: `src/ZeroAlloc.ValueObjects/ZeroAlloc.ValueObjects.csproj`

**Step 1: Add NuGet metadata**

```xml
<PropertyGroup>
  <PackageId>ZeroAlloc.ValueObjects</PackageId>
  <Version>1.0.0</Version>
  <Authors>Marcel Roozekrans</Authors>
  <Description>Zero-allocation source-generated ValueObject equality for hot paths. Eliminates boxing and iterator allocations from CSharpFunctionalExtensions.ValueObject.GetEqualityComponents().</Description>
  <PackageTags>valueobject;ddd;sourcegenerator;zeroalloc;performance</PackageTags>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <RepositoryUrl>https://github.com/MarcelRoozekrans/ZeroAlloc.ValueObjects</RepositoryUrl>
  <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
</PropertyGroup>
```

**Step 2: Pack and inspect**

```bash
dotnet pack src/ZeroAlloc.ValueObjects/ZeroAlloc.ValueObjects.csproj -c Release -o ./artifacts
```
Expected: `artifacts/ZeroAlloc.ValueObjects.1.0.0.nupkg` created.

Inspect with:
```bash
unzip -l ./artifacts/ZeroAlloc.ValueObjects.1.0.0.nupkg
```
Expected: Generator DLL appears under `analyzers/dotnet/cs/`.

**Step 3: Commit**

```bash
git add .
git commit -m "chore: add NuGet packaging metadata"
```

---

### Task 10: README + Final Check

**Files:**
- Create: `README.md`

**Step 1: Run full test suite**

```bash
dotnet test
```
Expected: All tests PASS.

**Step 2: Write README**

```markdown
# ZeroAlloc.ValueObjects

Zero-allocation source-generated ValueObject equality. Drop-in for CSharpFunctionalExtensions.ValueObject hot paths.

## Install
dotnet add package ZeroAlloc.ValueObjects

## Usage
[ValueObject]
public partial class Money
{
    public decimal Amount { get; }
    public string Currency { get; }
}

// Generated: Equals, GetHashCode (HashCode.Combine), ==, !=, ToString — zero alloc

## Benchmarks
| Method                  | Mean    | Allocated |
|------------------------ |--------:|----------:|
| CFE_Equals              | 45.2 ns | 96 B      |
| ZeroAlloc_Equals        |  3.1 ns | 0 B       |
| CFE_GetHashCode         | 38.7 ns | 88 B      |
| ZeroAlloc_GetHashCode   |  2.4 ns | 0 B       |

## Attributes
- `[ValueObject]` — triggers generation on any partial class or struct
- `[EqualityMember]` — opt-in: only marked properties participate in equality
- `[IgnoreEqualityMember]` — opt-out: exclude this property
- `[ValueObject(ForceClass = true)]` — always emit class, never struct
```

**Step 3: Final commit**

```bash
git add README.md
git commit -m "docs: add README with usage, benchmarks, and attribute reference"
```
