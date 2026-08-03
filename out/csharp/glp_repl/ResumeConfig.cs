// feature 064 — durable listener service box (gavri variant), T003.
//
// Resume-goal registration reader. Contract:
// specs/064-durable-listener-service-box/contracts/resume-registration.md
//
// The registration is a repo-root convention file `glpservice/resume.json`,
// discovered by walking up from AppContext.BaseDirectory — the same idiom
// SharedCertMaterial.ResolveCertDir uses for glpquick-cert/ and the converted
// REPL uses for programs/self.glp. An absent file means the feature is fully
// inert (SC-005): no output, no delay, no behavior change. All persistence in
// this feature is host-owned — ZERO new GLP language surface (FR-006).

using System;
using System.IO;
using System.Text.Json;

namespace GlpRuntime.Repl.Host;

/// <summary>One parsed, validated resume-goal registration (schema v1).</summary>
internal sealed record ResumeRegistration(
    string Program,
    string Goal,
    bool Enabled,
    bool Replay,
    string SourcePath,
    string RepoRoot);

internal static class ResumeConfig
{
    private const string DirName = "glpservice";
    private const string FileName = "resume.json";

    /// <summary>
    /// Try to discover and parse the registration. Returns false with no output
    /// when the file is absent (the inert path). Parse/validation failures print
    /// one named diagnostic via <paramref name="diag"/> and return false — the
    /// REPL always continues (FR-009).
    /// </summary>
    public static bool TryLoad(Action<string> diag, out ResumeRegistration? registration)
    {
        registration = null;
        var found = Discover();
        if (found is null)
            return false;
        var (path, repoRoot) = found.Value;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            if (!root.TryGetProperty("version", out var v) || v.ValueKind != JsonValueKind.Number)
            {
                diag($"resume: invalid registration at {path}: missing numeric 'version'");
                return false;
            }
            if (v.GetInt32() != 1)
            {
                diag($"resume: unsupported version {v.GetInt32()} in {path}");
                return false;
            }
            if (!root.TryGetProperty("program", out var prog) || prog.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(prog.GetString()))
            {
                diag($"resume: invalid registration at {path}: missing 'program'");
                return false;
            }
            if (!root.TryGetProperty("goal", out var goal) || goal.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(goal.GetString()))
            {
                diag($"resume: invalid registration at {path}: missing 'goal'");
                return false;
            }

            var enabled = !root.TryGetProperty("enabled", out var en) || en.ValueKind != JsonValueKind.False;
            var replay = !root.TryGetProperty("replay", out var rp) || rp.ValueKind != JsonValueKind.False;

            registration = new ResumeRegistration(
                Program: prog.GetString()!,
                Goal: goal.GetString()!,
                Enabled: enabled,
                Replay: replay,
                SourcePath: path,
                RepoRoot: repoRoot);
            return true;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            diag($"resume: invalid registration at {path}: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Walk up from AppContext.BaseDirectory to the first ancestor holding
    /// glpservice/resume.json. Null when no ancestor has one (the common,
    /// silent case). Unknown extra keys in the file are ignored by TryLoad
    /// (forward-compatible per contract).
    /// </summary>
    private static (string path, string repoRoot)? Discover()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, DirName, FileName);
            if (File.Exists(candidate))
                return (candidate, dir.FullName);
            dir = dir.Parent;
        }
        return null;
    }
}
