using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.ValueObjects.Tests.FunctionalTests;

/// <summary>
/// Backs the README's zero-allocation claim for the generated equality members: Equals,
/// GetHashCode, == and != on a multi-property class and struct, and on a single-property struct.
/// ToString is not covered: it returns a new string, so it allocates by design.
/// </summary>
public class EqualityAllocationTests
{
    private const int Iterations = 1_000;

    // Results land in these fields so the JIT cannot drop the measured calls.
    private static bool s_bool;
    private static int s_int;

    // Separate instances with equal values, so Equals compares every member.
    private static readonly Money ClassA = new(10m, "USD");
    private static readonly Money ClassB = new(10m, new string("USD".AsSpan()));
    private static readonly Money ClassC = new(10m, "EUR");

    private static readonly MoneyStruct StructA = new(10m, "USD");
    private static readonly MoneyStruct StructB = new(10m, new string("USD".AsSpan()));
    private static readonly MoneyStruct StructC = new(10m, "EUR");

    private static readonly CustomerId IdA = new(42);
    private static readonly CustomerId IdB = new(42);

    [Fact]
    public void Class_Equals_AllocatesNothing() =>
        AllocationGate.AssertBudget(0, Iterations, () => s_bool = ClassA.Equals(ClassB), "Money.Equals");

    [Fact]
    public void Class_EqualsNotEqual_AllocatesNothing() =>
        AllocationGate.AssertBudget(0, Iterations, () => s_bool = ClassA.Equals(ClassC), "Money.Equals unequal");

    [Fact]
    public void Class_GetHashCode_AllocatesNothing() =>
        AllocationGate.AssertBudget(0, Iterations, () => s_int = ClassA.GetHashCode(), "Money.GetHashCode");

    [Fact]
    public void Class_EqualityOperators_AllocateNothing() =>
        AllocationGate.AssertBudget(0, Iterations, () => s_bool = ClassA == ClassB && ClassA != ClassC, "Money == and !=");

    [Fact]
    public void Struct_Equals_AllocatesNothing() =>
        AllocationGate.AssertBudget(0, Iterations, () => s_bool = StructA.Equals(StructB), "MoneyStruct.Equals");

    [Fact]
    public void Struct_EqualsNotEqual_AllocatesNothing() =>
        AllocationGate.AssertBudget(0, Iterations, () => s_bool = StructA.Equals(StructC), "MoneyStruct.Equals unequal");

    [Fact]
    public void Struct_GetHashCode_AllocatesNothing() =>
        AllocationGate.AssertBudget(0, Iterations, () => s_int = StructA.GetHashCode(), "MoneyStruct.GetHashCode");

    [Fact]
    public void Struct_EqualityOperators_AllocateNothing() =>
        AllocationGate.AssertBudget(0, Iterations, () => s_bool = StructA == StructB && StructA != StructC, "MoneyStruct == and !=");

    [Fact]
    public void SinglePropertyStruct_Equality_AllocatesNothing() =>
        AllocationGate.AssertBudget(
            0, Iterations, () => s_bool = IdA.Equals(IdB) && IdA == IdB && !(IdA != IdB), "CustomerId equality");

    [Fact]
    public void SinglePropertyStruct_GetHashCode_AllocatesNothing() =>
        AllocationGate.AssertBudget(0, Iterations, () => s_int = IdA.GetHashCode(), "CustomerId.GetHashCode");

    [Fact]
    public void ResultsAreObserved()
    {
        // Keeps the result fields read, and checks the gated calls compute what they claim.
        s_bool = ClassA == ClassB && StructA == StructB && IdA == IdB;
        s_int = StructA.GetHashCode();
        Assert.True(s_bool);
        Assert.Equal(StructB.GetHashCode(), s_int);
    }
}
