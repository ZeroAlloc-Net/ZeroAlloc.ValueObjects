using System.Text;

namespace ZeroAlloc.ValueObjects.Generator.Writers;

/// <summary>
/// XML documentation emitted onto the public surface of generated [TypedId] types.
/// Shared by <see cref="TypedIdGuidWriter"/> and <see cref="TypedIdInt64Writer"/>, which emit
/// the same member shape over different backing types.
/// </summary>
/// <remarks>
/// Generated docs never cref a generated or consumer symbol — an unresolved cref raises CS1574,
/// which under TreatWarningsAsErrors would recreate the consumer build break this documentation
/// exists to avoid. crefs to BCL exception types are safe because corelib is always referenced.
/// </remarks>
internal static class TypedIdDocs
{
    public static void AppendParseDoc(StringBuilder sb, string name, bool span, bool nullThrows)
    {
        var input = span ? "character span" : "string";
        sb.AppendLine($"    /// <summary>Parses a {input} into a <c>{name}</c>, accepting the same form produced by ToString.</summary>");
        sb.AppendLine($"    /// <param name=\"s\">The {input} to parse.</param>");
        sb.AppendLine("    /// <param name=\"provider\">Ignored; the format is culture-invariant. Present to satisfy the parsing interfaces.</param>");
        sb.AppendLine($"    /// <returns>The parsed <c>{name}</c>.</returns>");
        if (nullThrows)
            sb.AppendLine("    /// <exception cref=\"System.ArgumentNullException\"><paramref name=\"s\"/> is null.</exception>");
        sb.AppendLine($"    /// <exception cref=\"System.FormatException\"><paramref name=\"s\"/> is not a valid <c>{name}</c>.</exception>");
    }

    public static void AppendTryParseDoc(StringBuilder sb, string name, bool span)
    {
        var input = span ? "character span" : "string";
        var nullNote = span ? "" : " Returns false for a null input rather than throwing.";
        sb.AppendLine($"    /// <summary>Attempts to parse a {input} into a <c>{name}</c>.{nullNote}</summary>");
        sb.AppendLine($"    /// <param name=\"s\">The {input} to parse.</param>");
        sb.AppendLine("    /// <param name=\"provider\">Ignored; the format is culture-invariant. Present to satisfy the parsing interfaces.</param>");
        sb.AppendLine($"    /// <param name=\"result\">The parsed <c>{name}</c> on success; otherwise the default value.</param>");
        sb.AppendLine("    /// <returns><see langword=\"true\"/> if parsing succeeded; otherwise <see langword=\"false\"/>.</returns>");
    }

    public static void AppendCompareToDoc(StringBuilder sb, string name)
    {
        sb.AppendLine($"    /// <summary>Compares this identifier with another by their underlying values.</summary>");
        sb.AppendLine($"    /// <param name=\"other\">The <c>{name}</c> to compare against.</param>");
        sb.AppendLine("    /// <returns>A negative value if this instance precedes <paramref name=\"other\"/>, zero if they are equal, otherwise a positive value.</returns>");
    }

    public static void AppendComparisonOperatorDoc(StringBuilder sb, string name, string relation)
    {
        sb.AppendLine($"    /// <summary>Determines whether one <c>{name}</c> is {relation} another, comparing their underlying values.</summary>");
        sb.AppendLine("    /// <param name=\"left\">The left operand.</param>");
        sb.AppendLine("    /// <param name=\"right\">The right operand.</param>");
        sb.AppendLine($"    /// <returns><see langword=\"true\"/> if the left operand is {relation} the right; otherwise <see langword=\"false\"/>.</returns>");
    }

    public static void AppendJsonConverterDoc(StringBuilder sb, string name)
    {
        sb.AppendLine($"    /// <summary>Converts a <c>{name}</c> to and from its JSON string form.</summary>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine($"    /// Applied to <c>{name}</c> via JsonConverterAttribute, so reflection-based serialization picks it up");
        sb.AppendLine("    /// automatically. Source-generated serialization requires registering it explicitly.");
        sb.AppendLine("    /// </remarks>");
    }
}
