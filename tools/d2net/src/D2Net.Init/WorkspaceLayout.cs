using System.IO;

namespace D2Net.Init;

/// <summary>
/// Resolved file-system paths for a D2NET workspace at a given repo root.
/// FR-002 requires CWD to be the repo root and refuses to walk up.
/// </summary>
public sealed record WorkspaceLayout(
    string RepoRoot,
    string WorkspaceDir,
    string SettingsFile,
    string PgDir,
    string DbFile)
{
    public const string WorkspaceFolderName = ".D2NET";
    public const string SettingsFileName = "D2NET-Settings.json";
    public const string PgDirName = "pgdb";
    public const string DbFileName = "workspace.sqlite";

    public static WorkspaceLayout Resolve(string repoRoot)
    {
        var workspace = Path.Combine(repoRoot, WorkspaceFolderName);
        var pgdir = Path.Combine(workspace, PgDirName);
        return new WorkspaceLayout(
            RepoRoot: Path.GetFullPath(repoRoot),
            WorkspaceDir: workspace,
            SettingsFile: Path.Combine(workspace, SettingsFileName),
            PgDir: pgdir,
            DbFile: Path.Combine(pgdir, DbFileName));
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

    public WorkspaceLayout AsTemp(string tempWorkspaceDir)
    {
        var pgdir = Path.Combine(tempWorkspaceDir, PgDirName);
        return new WorkspaceLayout(
            RepoRoot,
            tempWorkspaceDir,
            Path.Combine(tempWorkspaceDir, SettingsFileName),
            pgdir,
            Path.Combine(pgdir, DbFileName));
    }
}
