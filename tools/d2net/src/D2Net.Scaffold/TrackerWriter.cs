using System.Text.Json;
using D2Net.Scaffold.Models;

namespace D2Net.Scaffold;

public static class TrackerWriter
{
    public const string FileName = "d2net-tracker.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static int WriteFreshTracker(string targetRoot, IReadOnlyList<RelFile> dartFiles)
    {
        var records = new List<TrackerRecord>(dartFiles.Count);
        foreach (var dart in dartFiles)
        {
            var record = new TrackerRecord
            {
                Source = $"{dart.RelPath}.src",
                Companions = BuildInitialCompanions(),
            };
            records.Add(record);
        }

        records.Sort((a, b) => string.CompareOrdinal(a.Source, b.Source));

        var path = Path.Combine(targetRoot, FileName);
        Directory.CreateDirectory(targetRoot);
        var json = JsonSerializer.Serialize(records, Options);
        File.WriteAllText(path, json);
        return records.Count;
    }

    public static string TrackerPathFor(string targetRoot) =>
        Path.Combine(targetRoot, FileName);

    private static Dictionary<string, string> BuildInitialCompanions()
    {
        var dict = new Dictionary<string, string>(CompanionExtensions.All.Count, StringComparer.Ordinal);
        foreach (var ext in CompanionExtensions.All)
            dict[ext] = CompanionStatus.Todo;
        return dict;
    }
}
