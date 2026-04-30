using System.IO;
using System.Text.Json;

namespace D2Net.Init;

/// <summary>
/// Plain-text vs JSON output formatting helpers for the inspection options.
/// FR-019a: <c>--json</c> emits compact JSON to stdout and routes diagnostics
/// to stderr; plain-text uses tab-separated columns.
/// </summary>
public static class OutputFormat
{
    public static readonly JsonSerializerOptions CompactJson = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = null,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void WriteTsvLine(TextWriter w, params string[] fields)
        => w.WriteLine(string.Join('\t', fields));

    public static void WriteJson<T>(TextWriter w, T value)
    {
        var json = JsonSerializer.Serialize(value, CompactJson);
        w.WriteLine(json);
    }

    /// <summary>FR-019: ISO-8601 UTC with trailing Z, no fractional seconds.</summary>
    public static string FormatTimestamp(DateTimeOffset value)
        => value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
}
