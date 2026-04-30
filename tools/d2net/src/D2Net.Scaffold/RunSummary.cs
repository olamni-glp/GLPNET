using D2Net.Scaffold.Models;

namespace D2Net.Scaffold;

public sealed class RunSummary
{
    public string Mode { get; set; } = "fresh";
    public string SourceRoot { get; set; } = "";
    public string TargetRoot { get; set; } = "";
    public string TrackerPath { get; set; } = "";
    public int DirectoriesCreated { get; set; }
    public int NonDartFilesCopied { get; set; }
    public int DartSrcFilesWritten { get; set; }
    public int CompanionStubsWritten { get; set; }
    public int TrackerRecordsWritten { get; set; }
    public List<string> NewlyDiscoveredDartFiles { get; } = new();
    public TimeSpan Elapsed { get; set; }

    public void WriteTo(TextWriter writer)
    {
        writer.WriteLine("d2net-scaffold: scaffold complete.");
        writer.WriteLine($"  Source: {SourceRoot}");
        writer.WriteLine($"  Target: {TargetRoot}");
        writer.WriteLine($"  Mode  : {Mode}");
        writer.WriteLine($"  Directories created   : {DirectoriesCreated}");
        writer.WriteLine($"  Non-Dart files copied : {NonDartFilesCopied}");
        writer.WriteLine($"  .dart.src files       : {DartSrcFilesWritten}");
        writer.WriteLine($"  Companion stubs       : {CompanionStubsWritten}");
        writer.WriteLine($"  Tracker records       : {TrackerRecordsWritten}");
        writer.WriteLine($"  Tracker file          : {(string.IsNullOrEmpty(TrackerPath) ? "(not written in refresh mode)" : TrackerPath)}");
        writer.WriteLine($"  Pruned directories    : {string.Join(", ", PrunedDirectories.Names)}");
        writer.WriteLine($"  Elapsed               : {Elapsed.TotalSeconds:0.000}s");

        if (Mode == "refresh")
        {
            if (NewlyDiscoveredDartFiles.Count == 0)
            {
                writer.WriteLine("  New Dart files: (none)");
            }
            else
            {
                writer.WriteLine("  New Dart files (no tracker entry, please update d2net-tracker.json manually):");
                foreach (var p in NewlyDiscoveredDartFiles)
                    writer.WriteLine($"    {p}");
            }
        }
    }
}
