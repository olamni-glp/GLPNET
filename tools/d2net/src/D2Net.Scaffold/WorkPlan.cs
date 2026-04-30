using D2Net.Scaffold.Models;

namespace D2Net.Scaffold;

public sealed record WorkPlan(
    IReadOnlyList<RelDir> Directories,
    IReadOnlyList<RelFile> NonDartFiles,
    IReadOnlyList<RelFile> DartFiles,
    IReadOnlyList<Collision> Collisions)
{
    public static WorkPlan Build(ScaffoldOptions options)
    {
        var walk = DirectoryWalker.Walk(options.SourceRoot);

        var nonDart = new List<RelFile>();
        var dart = new List<RelFile>();
        foreach (var f in walk.Files)
        {
            if (f.RelPath.EndsWith(".dart", StringComparison.Ordinal))
                dart.Add(f);
            else
                nonDart.Add(f);
        }

        return new WorkPlan(walk.Directories, nonDart, dart, Array.Empty<Collision>());
    }

    public WorkPlan WithCollisions(IReadOnlyList<Collision> collisions) =>
        new(Directories, NonDartFiles, DartFiles, collisions);
}
