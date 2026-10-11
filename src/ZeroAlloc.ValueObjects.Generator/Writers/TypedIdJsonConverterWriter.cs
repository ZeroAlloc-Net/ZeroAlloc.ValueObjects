using System.Text;

namespace ZeroAlloc.ValueObjects.Generator.Writers;

// Emits the nested TypedIdJsonConverter shared by every backing type. Read and Write go through
// stack buffers instead of a string, so neither allocates for any realistic id.
//
// The emitted code needs .NET 7 or later: Utf8JsonReader.CopyString and the span TryFormat
// overloads are .NET 7 APIs, and the generated struct already requires .NET 7 for its
// IParsable and ISpanParsable static abstract members.
internal static class TypedIdJsonConverterWriter
{
    // Sized by raw UTF-8 length, which for an escaped value is up to six bytes per char. 256
    // covers every canonical id form even when fully escaped: a 26-char ULID is 156 bytes and a
    // 36-char UUID 216. Longer raw values, such as whitespace-padded input, use a pooled buffer.
    private const int MaxStackChars = 256;

    /// <param name="sb">The builder to append to.</param>
    /// <param name="name">The identifier of the TypedId struct.</param>
    /// <param name="maxFormattedChars">The longest form the Write format produces.</param>
    /// <param name="tryFormatCall">
    /// An expression writing the id held in <c>value</c> into the span <c>chars</c>, declaring
    /// <c>written</c>, and returning whether it fit.
    /// </param>
    public static void Append(StringBuilder sb, string name, int maxFormattedChars, string tryFormatCall)
    {
        TypedIdDocs.AppendJsonConverterDoc(sb, name);
        sb.AppendLine($"    public sealed class TypedIdJsonConverter : JsonConverter<{name}>");
        sb.AppendLine("    {");
        sb.AppendLine($"        private const int MaxStackChars = {MaxStackChars};");
        sb.AppendLine();
        AppendRead(sb, name);
        sb.AppendLine();
        AppendWrite(sb, name, maxFormattedChars, tryFormatCall);
        sb.AppendLine("    }");
    }

    private static void AppendRead(StringBuilder sb, string name)
    {
        sb.AppendLine("        /// <inheritdoc/>");
        sb.AppendLine($"        public override {name} Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (reader.TokenType != JsonTokenType.String)");
        sb.AppendLine("            {");
        sb.AppendLine("                // A null token reads as null and is rejected below. Any other token makes");
        sb.AppendLine("                // GetString throw the reader's own InvalidOperationException, which");
        sb.AppendLine("                // JsonSerializer reports as a JsonException.");
        sb.AppendLine("                _ = reader.GetString();");
        sb.AppendLine($"                throw new JsonException($\"Invalid {name} value.\");");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // Unescaping and transcoding never yield more chars than the raw value has bytes, so a");
        sb.AppendLine("            // buffer of the raw length always fits. Escaped or whitespace-padded input can exceed");
        sb.AppendLine("            // the stack buffer and then borrows a pooled one.");
        sb.AppendLine("            var rawLength = reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length;");
        sb.AppendLine("            char[]? rented = null;");
        sb.AppendLine("            Span<char> buffer = rawLength <= MaxStackChars");
        sb.AppendLine("                ? stackalloc char[MaxStackChars]");
        sb.AppendLine("                : (rented = global::System.Buffers.ArrayPool<char>.Shared.Rent(checked((int)rawLength)));");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                // CopyString unescapes, joins segments and validates UTF-8 exactly as GetString does.");
        sb.AppendLine("                ReadOnlySpan<char> chars = buffer.Slice(0, reader.CopyString(buffer));");
        sb.AppendLine("                return TryParse(chars, CultureInfo.InvariantCulture, out var id)");
        sb.AppendLine("                    ? id");
        sb.AppendLine($"                    : throw new JsonException($\"Invalid {name} value.\");");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                if (rented is not null) global::System.Buffers.ArrayPool<char>.Shared.Return(rented);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    private static void AppendWrite(StringBuilder sb, string name, int maxFormattedChars, string tryFormatCall)
    {
        sb.AppendLine("        /// <inheritdoc/>");
        sb.AppendLine($"        public override void Write(Utf8JsonWriter writer, {name} value, JsonSerializerOptions options)");
        sb.AppendLine("        {");
        sb.AppendLine($"            Span<char> chars = stackalloc char[{maxFormattedChars}];");
        sb.AppendLine($"            if (!{tryFormatCall})");
        sb.AppendLine($"                throw new InvalidOperationException(\"Failed to format {name}.\");");
        sb.AppendLine("            writer.WriteStringValue(chars.Slice(0, written));");
        sb.AppendLine("        }");
    }
}
