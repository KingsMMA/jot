using System.Text;
using System.Text.Json;

namespace Jot.Formatting;

/// <summary>
/// Pretty-prints JSON with a configurable indent. Scalars are written from their raw text so
/// numbers keep their exact representation, and object/array order is preserved. Comments and
/// trailing commas are tolerated on input. Throws <see cref="JsonException"/> on invalid JSON so
/// the caller can leave the document untouched and report the error.
/// </summary>
public static class JsonFormatter
{
    public static string Format(string json, string indentUnit)
    {
        var options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        using var document = JsonDocument.Parse(json, options);
        var sb = new StringBuilder(json.Length + 16);
        Write(document.RootElement, sb, indentUnit, 0);
        return sb.ToString();
    }

    private static void Write(JsonElement element, StringBuilder sb, string indentUnit, int depth)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteObject(element, sb, indentUnit, depth);
                break;
            case JsonValueKind.Array:
                WriteArray(element, sb, indentUnit, depth);
                break;
            default:
                sb.Append(element.GetRawText());
                break;
        }
    }

    private static void WriteObject(JsonElement element, StringBuilder sb, string indentUnit, int depth)
    {
        var first = true;
        sb.Append('{');
        foreach (var property in element.EnumerateObject())
        {
            sb.Append(first ? "\n" : ",\n");
            first = false;
            Indent(sb, indentUnit, depth + 1);
            sb.Append(JsonSerializer.Serialize(property.Name));
            sb.Append(": ");
            Write(property.Value, sb, indentUnit, depth + 1);
        }

        if (first) { sb.Append('}'); return; }
        sb.Append('\n');
        Indent(sb, indentUnit, depth);
        sb.Append('}');
    }

    private static void WriteArray(JsonElement element, StringBuilder sb, string indentUnit, int depth)
    {
        var first = true;
        sb.Append('[');
        foreach (var item in element.EnumerateArray())
        {
            sb.Append(first ? "\n" : ",\n");
            first = false;
            Indent(sb, indentUnit, depth + 1);
            Write(item, sb, indentUnit, depth + 1);
        }

        if (first) { sb.Append(']'); return; }
        sb.Append('\n');
        Indent(sb, indentUnit, depth);
        sb.Append(']');
    }

    private static void Indent(StringBuilder sb, string indentUnit, int depth)
    {
        for (var i = 0; i < depth; i++) sb.Append(indentUnit);
    }
}
