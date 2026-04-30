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
}
