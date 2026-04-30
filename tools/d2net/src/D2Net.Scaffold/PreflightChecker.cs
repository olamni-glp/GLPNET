using D2Net.Scaffold.Models;

namespace D2Net.Scaffold;

public static class PreflightChecker
{
    public static IReadOnlyList<Collision> Detect(WorkPlan plan)
    {
        var collisions = new List<Collision>();

        // Index non-Dart files by their relative path for O(1) collision lookup.
        var nonDartIndex = new HashSet<string>(plan.NonDartFiles.Select(f => f.RelPath), StringComparer.Ordinal);

        foreach (var dart in plan.DartFiles)
        {
            // Strip ".dart" from the end to get the basename (path without extension).
            var basePath = dart.RelPath[..^".dart".Length];
            foreach (var ext in CompanionExtensions.All)
            {
                var candidate = $"{basePath}.{ext}";
                if (nonDartIndex.Contains(candidate))
                    collisions.Add(new Collision(dart.RelPath, ext, candidate));
            }
        }

        return collisions;
    }
}
