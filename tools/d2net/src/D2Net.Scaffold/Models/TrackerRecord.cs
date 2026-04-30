using System.Text.Json.Serialization;

namespace D2Net.Scaffold.Models;

public sealed class TrackerRecord
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("companions")]
    public Dictionary<string, string> Companions { get; set; } = new();
}
