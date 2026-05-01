using System.IO;
using D2Net.Scaffold.Tests.Fixtures;

namespace D2Net.Scaffold.Tests;

/// <summary>
/// T014: pre-bridge rejections.
/// </summary>
public class ScaffoldPreflightTests
{
    [Fact]
    public void NoWorkspace_ExitsWith22()
    {
        using var repo = new TempRepoBuilder();
        // Workspace not initialised.
        var (code, _, se) = InitHelper.Scaffold(repo.Root, port: 0);
        Assert.Equal(ExitCodes.ScaffoldWorkspaceMissing, code);
        Assert.Contains("no D2NET workspace", se);
    }

    [Fact]
    public void NoWorkspace_JsonEnvelope()
    {
        using var repo = new TempRepoBuilder();
        var (code, so, _) = InitHelper.Scaffold(repo.Root, port: 0, json: true);
        Assert.Equal(ExitCodes.ScaffoldWorkspaceMissing, code);
        using var doc = System.Text.Json.JsonDocument.Parse(so);
        Assert.Equal("error", doc.RootElement.GetProperty("result").GetString());
        Assert.Equal(22, doc.RootElement.GetProperty("code").GetInt32());
    }

    [Fact]
    public void SourceDirMissing_ExitsWith23()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/foo.dart");
        var (initCode, _, _, port) = InitHelper.Init(repo.Root);
        Assert.Equal(D2Net.Init.ExitCodes.Success, initCode);

        // Delete the source dir AFTER init.
        Directory.Delete(repo.SourceDir, recursive: true);

        var (code, _, se) = InitHelper.Scaffold(repo.Root, port);
        Assert.Equal(ExitCodes.ScaffoldSourceMissing, code);
        Assert.Contains("source directory", se);
        Assert.Contains("glp_runtime", se);
    }
}
