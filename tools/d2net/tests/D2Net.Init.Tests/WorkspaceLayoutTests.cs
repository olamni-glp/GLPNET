using System.IO;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;

namespace D2Net.Init.Tests;

public class WorkspaceLayoutTests
{
    [Fact]
    public void LooksLikeRepoRoot_TrueWhenGitDirExists()
    {
        using var repo = new TempRepoBuilder();
        Assert.True(WorkspaceLayout.LooksLikeRepoRoot(repo.Root, sourceDirHint: null));
    }

    [Fact]
    public void LooksLikeRepoRoot_TrueWhenWorkspaceExists()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "d2net-init-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tmp, ".D2NET"));
        try
        {
            Assert.True(WorkspaceLayout.LooksLikeRepoRoot(tmp, sourceDirHint: null));
        }
        finally { Directory.Delete(tmp, recursive: true); }
    }

    [Fact]
    public void LooksLikeRepoRoot_TrueWhenSourceDirHintExists()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "d2net-init-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tmp, "glp_runtime"));
        try
        {
            Assert.True(WorkspaceLayout.LooksLikeRepoRoot(tmp, sourceDirHint: "glp_runtime"));
            Assert.False(WorkspaceLayout.LooksLikeRepoRoot(tmp, sourceDirHint: "missing"));
            Assert.False(WorkspaceLayout.LooksLikeRepoRoot(tmp, sourceDirHint: null));
        }
        finally { Directory.Delete(tmp, recursive: true); }
    }

    [Fact]
    public void LooksLikeRepoRoot_FalseForRandomEmptyDir()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "d2net-init-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            Assert.False(WorkspaceLayout.LooksLikeRepoRoot(tmp, sourceDirHint: "anything"));
        }
        finally { Directory.Delete(tmp, recursive: true); }
    }

    [Fact]
    public void Resolve_PgDataDirEqualsPgDir()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "d2net-init-tests", Guid.NewGuid().ToString("N"));
        var layout = WorkspaceLayout.Resolve(tmp);
        Assert.Equal(layout.PgDir, layout.PgDataDir);
        Assert.EndsWith("pgdb", layout.PgDir);
    }

    [Fact]
    public void LooksLikeSqliteEra_FalseWhenWorkspaceAbsent()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "d2net-init-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            Assert.False(WorkspaceLayout.LooksLikeSqliteEra(tmp));
        }
        finally { Directory.Delete(tmp, recursive: true); }
    }

    [Fact]
    public void LooksLikeSqliteEra_FalseWhenEnginePglite()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "d2net-init-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tmp, ".D2NET"));
        File.WriteAllText(Path.Combine(tmp, ".D2NET", "D2NET-Settings.json"),
            """{"connection":{"engine":"pglite"}}""");
        try
        {
            Assert.False(WorkspaceLayout.LooksLikeSqliteEra(tmp));
        }
        finally { Directory.Delete(tmp, recursive: true); }
    }

    [Fact]
    public void LooksLikeSqliteEra_TrueWhenLegacyFilePresent()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "d2net-init-tests", Guid.NewGuid().ToString("N"));
        var pgdir = Path.Combine(tmp, ".D2NET", "pgdb");
        Directory.CreateDirectory(pgdir);
        File.WriteAllBytes(Path.Combine(pgdir, "workspace.sqlite"), Array.Empty<byte>());
        try
        {
            Assert.True(WorkspaceLayout.LooksLikeSqliteEra(tmp));
        }
        finally { Directory.Delete(tmp, recursive: true); }
    }

    [Fact]
    public void LooksLikeSqliteEra_TrueWhenEngineSqliteInJson()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "d2net-init-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tmp, ".D2NET"));
        File.WriteAllText(Path.Combine(tmp, ".D2NET", "D2NET-Settings.json"),
            """{"connection":{"engine":"sqlite","db_file":"workspace.sqlite"}}""");
        try
        {
            Assert.True(WorkspaceLayout.LooksLikeSqliteEra(tmp));
        }
        finally { Directory.Delete(tmp, recursive: true); }
    }
}
