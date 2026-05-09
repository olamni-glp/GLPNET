using System.IO;
using System.Text.Json;

namespace D2Net.Init;

/// <summary>
/// Resolved file-system paths for a D2NET workspace at a given repo root.
/// FR-002 requires CWD to be the repo root and refuses to walk up.
///
/// 005 update: <c>DbFile</c> (single SQLite file) is replaced by
/// <c>PgDataDir</c> (the PGLite data directory). The PGLite data tree is a
/// multi-file Postgres data layout managed by <c>@electric-sql/pglite</c>;
/// the bridge subprocess is the only process that opens it directly.
/// </summary>
public sealed record WorkspaceLayout(
    string RepoRoot,
    string WorkspaceDir,
    string SettingsFile,
    string PgDir,
    string PgDataDir)
{
    public const string WorkspaceFolderName = ".D2NET";
    public const string SettingsFileName = "D2NET-Settings.json";
    /// <summary>Feature 012: PGLite cluster moved from <c>.D2NET/pgdb/</c> to repo-level <c>.pgdb/</c>.</summary>
    public const string PgDirName = ".pgdb";
    /// <summary>Legacy: <c>.D2NET/pgdb/</c> path used pre-feature-012, still relevant for migration detection.</summary>
    public const string LegacyPgDirName = "pgdb";
    /// <summary>SQLite-era marker file, used only by FR-014 detection.</summary>
    public const string LegacySqliteFileName = "workspace.sqlite";

    public static WorkspaceLayout Resolve(string repoRoot)
    {
        var workspace = Path.Combine(repoRoot, WorkspaceFolderName);
        var pgdir = Path.Combine(repoRoot, PgDirName);
        return new WorkspaceLayout(
            RepoRoot: Path.GetFullPath(repoRoot),
            WorkspaceDir: workspace,
            SettingsFile: Path.Combine(workspace, SettingsFileName),
            PgDir: pgdir,
            PgDataDir: pgdir);
    }

    /// <summary>FR-002: returns true iff the CWD looks like a D2NET repo root.</summary>
    public static bool LooksLikeRepoRoot(string cwd, string? sourceDirHint)
    {
        if (Directory.Exists(Path.Combine(cwd, ".git"))) return true;
        if (Directory.Exists(Path.Combine(cwd, WorkspaceFolderName))) return true;
        if (!string.IsNullOrEmpty(sourceDirHint)
            && Directory.Exists(Path.Combine(cwd, sourceDirHint!))) return true;
        return false;
    }

    /// <summary>
    /// FR-014 / Q5 clarification: detect a workspace produced by the shipped
    /// 002 SQLite implementation. Returns true if either:
    ///   (a) <c>.D2NET/pgdb/workspace.sqlite</c> exists, OR
    ///   (b) <c>.D2NET/D2NET-Settings.json</c> exists and parses with
    ///       <c>connection.engine</c> not equal to "pglite".
    /// </summary>
    public static bool LooksLikeSqliteEra(string repoRoot)
    {
        var layout = Resolve(repoRoot);
        // Pre-012, the cluster lived at .D2NET/pgdb/. The SQLite marker was
        // .D2NET/pgdb/workspace.sqlite. Probe the legacy location only — the
        // new .pgdb/ never had SQLite content.
        var legacyFile = Path.Combine(layout.WorkspaceDir, LegacyPgDirName, LegacySqliteFileName);
        if (File.Exists(legacyFile)) return true;

        if (File.Exists(layout.SettingsFile))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(layout.SettingsFile));
                if (doc.RootElement.TryGetProperty("connection", out var conn)
                    && conn.TryGetProperty("engine", out var engine)
                    && engine.ValueKind == JsonValueKind.String
                    && engine.GetString() is { } e
                    && !string.Equals(e, "pglite", System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
            catch (JsonException)
            {
                // Corrupt settings JSON is ambiguous; the existence check still triggers
                // the gate via Directory.Exists(WorkspaceDir) in the caller.
            }
        }
        return false;
    }

    /// <summary>
    /// Returns a temp-workspace layout. Feature 012: the PGLite cluster
    /// (<c>PgDir</c>) is repo-shared and is NOT relocated under the temp
    /// workspace; only the settings folder is staged.
    /// </summary>
    public WorkspaceLayout AsTemp(string tempWorkspaceDir)
    {
        return new WorkspaceLayout(
            RepoRoot,
            tempWorkspaceDir,
            Path.Combine(tempWorkspaceDir, SettingsFileName),
            PgDir: PgDir,
            PgDataDir: PgDataDir);
    }
}
