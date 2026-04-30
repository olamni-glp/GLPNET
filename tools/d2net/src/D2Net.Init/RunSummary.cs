using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace D2Net.Init;

/// <summary>FR-021: human-readable summary at the end of a successful init run.</summary>
public sealed class RunSummary
{
    public string WorkspaceDir { get; init; } = "";
    public string SettingsFile { get; init; } = "";
    public string PgDir { get; init; } = "";
    public string DbFile { get; init; } = "";
    public string SourceDir { get; init; } = "";
    public string TargetExtension { get; init; } = "";
    public string TargetDir { get; init; } = "";
    public IReadOnlyList<ProposedExclusion> ApprovedExclusions { get; init; } = Array.Empty<ProposedExclusion>();
    public int DartFileCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    public void WriteTo(TextWriter w)
    {
        var toolCount = ApprovedExclusions.Count(e => e.Kind == ExclusionKind.Tool);
        var patternCount = ApprovedExclusions.Count(e => e.Kind == ExclusionKind.Pattern);
        var manualCount = ApprovedExclusions.Count(e => e.Kind == ExclusionKind.Manual);

        w.WriteLine($"d2net-init: workspace ready at {WorkspaceDir}");
        w.WriteLine($"  Source           : {SourceDir}");
        w.WriteLine($"  Target extension : {TargetExtension}");
        w.WriteLine($"  Target           : {TargetDir}");
        w.WriteLine($"  Settings file    : {SettingsFile}");
        w.WriteLine($"  Database         : {DbFile} (embedded SQLite, single-user)");
        w.WriteLine($"  Excluded dirs    : {ApprovedExclusions.Count} ({toolCount} well-known tool, {patternCount} archive/backup, {manualCount} manual)");
        w.WriteLine($"  Dart files       : {DartFileCount}");
        w.WriteLine($"  Created at       : {OutputFormat.FormatTimestamp(CreatedAt)}");
    }
}
