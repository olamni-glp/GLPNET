namespace D2Net.Scaffold;

public sealed record ScaffoldOptions(
    string SourceRoot,
    string TargetRoot,
    bool Refresh);
