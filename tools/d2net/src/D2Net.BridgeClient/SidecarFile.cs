using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace D2Net.BridgeClient;

/// <summary>
/// Reader/writer for <c>.pgdb/bridge.json</c>, the bridge's discovery sidecar.
/// Shape per <c>specs/012-codeconv-runner/data-model.md § 2</c>.
/// Atomic write via tmp + rename (FR-006 lifecycle, R4).
/// </summary>
public sealed record SidecarShape(
    [property: JsonPropertyName("host")] string Host,
    [property: JsonPropertyName("port")] int Port,
    [property: JsonPropertyName("pid")] int Pid,
    [property: JsonPropertyName("started_at")] string StartedAt,
    [property: JsonPropertyName("data_dir")] string DataDir,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("managed_by")] string ManagedBy);

public static class SidecarFile
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null, // honor JsonPropertyName
    };

    public static SidecarShape? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SidecarShape>(json);
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void WriteAtomic(string path, SidecarShape shape)
    {
        var tmp = path + ".tmp." + System.Environment.ProcessId;
        File.WriteAllText(tmp, JsonSerializer.Serialize(shape, _opts) + "\n");
        if (File.Exists(path)) File.Delete(path);
        File.Move(tmp, path);
    }
}
