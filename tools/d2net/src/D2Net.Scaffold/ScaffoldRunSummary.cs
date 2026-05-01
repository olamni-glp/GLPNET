using System.IO;
using System.Text.Json;

namespace D2Net.Scaffold;

/// <summary>
/// Success-summary for one scaffold run. Renders text (default) or JSON
/// (<c>--json</c>) per <c>contracts/scaffold-cli-contract.md</c>.
/// </summary>
public sealed class ScaffoldRunSummary
{
    public string SourceDir { get; set; } = "";
    public string TargetDir { get; set; } = "";
    public string TargetAbs { get; set; } = "";
    public string Extension { get; set; } = "";
    public int Exclusions { get; set; }
    public int FilesCopied { get; set; }
    public int WorkdirsCreated { get; set; }
    public int DartFilesUpdated { get; set; }
    public int AddedPaths { get; set; }
    public int RemovedPaths { get; set; }
    public bool DestructiveOverrideUsed { get; set; }
    public System.TimeSpan Elapsed { get; set; }

    public void WriteText(TextWriter writer)
    {
        writer.WriteLine($"d2net-scaffold: target tree scaffolded at {TargetAbs}");
        writer.WriteLine($"  source            : {SourceDir}");
        writer.WriteLine($"  target            : {TargetDir}");
        writer.WriteLine($"  extension         : {Extension}");
        writer.WriteLine($"  exclusions        : {Exclusions} directories");
        writer.WriteLine($"  files copied      : {FilesCopied}");
        writer.WriteLine($"  __ working dirs   : {WorkdirsCreated}");
        writer.WriteLine($"  dart_files updated: {DartFilesUpdated}");
        writer.WriteLine($"  duration          : {Elapsed.TotalSeconds:0.000} seconds");
        writer.WriteLine();
        writer.WriteLine("reconciliation summary:");
        writer.WriteLine($"  added paths   : {AddedPaths}");
        writer.WriteLine($"  removed paths : {RemovedPaths}");
        if (DestructiveOverrideUsed)
        {
            writer.WriteLine();
            writer.WriteLine("destructive override:");
            writer.WriteLine($"  deleted target tree at {TargetAbs}");
        }
    }

    public void WriteJson(TextWriter writer)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("result", "applied");
            w.WriteString("source", SourceDir);
            w.WriteString("target", TargetDir);
            w.WriteString("target_abs", TargetAbs);
            w.WriteString("extension", Extension);
            w.WriteBoolean("destructive_override_used", DestructiveOverrideUsed);
            w.WritePropertyName("totals");
            w.WriteStartObject();
            w.WriteNumber("exclusions", Exclusions);
            w.WriteNumber("files_copied", FilesCopied);
            w.WriteNumber("workdirs_created", WorkdirsCreated);
            w.WriteNumber("dart_files_updated", DartFilesUpdated);
            w.WriteNumber("added_paths", AddedPaths);
            w.WriteNumber("removed_paths", RemovedPaths);
            w.WriteNumber("duration_seconds", System.Math.Round(Elapsed.TotalSeconds, 3));
            w.WriteEndObject();
            w.WriteEndObject();
        }
        writer.WriteLine(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    public static void WriteJsonError(TextWriter writer, int code, string message, params (string Key, string Value)[] extras)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("result", "error");
            w.WriteNumber("code", code);
            w.WriteString("message", FirstLine(message));
            foreach (var (k, v) in extras) w.WriteString(k, v);
            w.WriteEndObject();
        }
        writer.WriteLine(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    public static void WriteJsonCollisions(TextWriter writer, int code, string message,
        System.Collections.Generic.IReadOnlyList<ScaffoldCollision> collisions)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("result", "error");
            w.WriteNumber("code", code);
            w.WriteString("message", FirstLine(message));
            w.WritePropertyName("collisions");
            w.WriteStartArray();
            foreach (var c in collisions)
            {
                w.WriteStartObject();
                w.WriteString("source_path", c.DartFileRelPath);
                w.WriteString("conflict", c.ConflictingRelPath);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        writer.WriteLine(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static string FirstLine(string s)
    {
        var idx = s.IndexOfAny(new[] { '\r', '\n' });
        return idx < 0 ? s : s[..idx];
    }
}
