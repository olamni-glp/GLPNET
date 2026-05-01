using System.IO;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;

namespace D2Net.Init.Tests;

/// <summary>
/// US3 path-rejection tests. These cases reject the invocation BEFORE the
/// PGLite bridge is spawned, so they're fast (no Node cold-start).
/// </summary>
public class RemoveExcludePathRejectionTests
{
    private static (int code, string stdout, string stderr) Run(string[] args, string cwd, string stdin = "")
    {
        var so = new StringWriter();
        var se = new StringWriter();
        var code = Program.Run(args, new StringReader(stdin), so, se, cwd);
        return (code, so.ToString(), se.ToString());
    }

    private static TempRepoBuilder BuildSkeletonWorkspace()
    {
        var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/foo.dart");
        var workspace = Path.Combine(repo.Root, ".D2NET");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(Path.Combine(workspace, "pgdb"));
        var settings = Path.Combine(workspace, "D2NET-Settings.json");
        File.WriteAllText(settings, """
{
  "schema_version": 1,
  "source_dir": "glp_runtime",
  "target_extension": "_net",
  "target_dir": "glp_runtime_net",
  "excluded_directories": [],
  "connection": {
    "engine": "pglite",
    "host": "127.0.0.1",
    "port": 54400,
    "database": "d2net",
    "user": "d2net",
    "password": "d2net",
    "data_dir": "",
    "connection_string": "",
    "connection_string_odbc": ""
  },
  "created_at": "2026-05-01T00:00:00Z"
}
""");
        return repo;
    }

    [Fact]
    public void PathOutsideSourceRoot_ExitsWith17()
    {
        using var repo = BuildSkeletonWorkspace();
        var (code, _, se) = Run(new[] { "--remove-exclude", "../escape" }, repo.Root);
        Assert.Equal(ExitCodes.RemoveExcludePathOutsideSource, code);
        Assert.Contains("../escape", se);
        Assert.Contains("outside source root", se);
    }

    [Fact]
    public void AbsolutePathOutsideSource_ExitsWith17()
    {
        using var repo = BuildSkeletonWorkspace();
        var outside = Path.Combine(Path.GetTempPath(), "definitely-not-source-" + Guid.NewGuid().ToString("N"));
        var (code, _, _) = Run(new[] { "--remove-exclude", outside.Replace('\\', '/') }, repo.Root);
        Assert.Equal(ExitCodes.RemoveExcludePathOutsideSource, code);
    }

    [Fact]
    public void PathIsAFile_ExitsWith16()
    {
        using var repo = BuildSkeletonWorkspace();
        var (code, _, se) = Run(new[] { "--remove-exclude", "lib/foo.dart" }, repo.Root);
        Assert.Equal(ExitCodes.AddExcludePathIsFile, code);
        Assert.Contains("file", se);
    }

    [Fact]
    public void NonexistentPathWithFileSuffix_ExitsWith16()
    {
        using var repo = BuildSkeletonWorkspace();
        var (code, _, _) = Run(new[] { "--remove-exclude", "phantom.zip" }, repo.Root);
        Assert.Equal(ExitCodes.AddExcludePathIsFile, code);
    }

    [Fact]
    public void AllOrNothing_OnePathOutsideSource_RejectsEntireBatch()
    {
        using var repo = BuildSkeletonWorkspace();
        var settingsBefore = File.ReadAllBytes(Path.Combine(repo.Root, ".D2NET", "D2NET-Settings.json"));
        var (code, _, _) = Run(
            new[] { "--remove-exclude", "lib",
                    "--remove-exclude", "../escape",
                    "--remove-exclude", "extra" },
            repo.Root);
        Assert.Equal(ExitCodes.RemoveExcludePathOutsideSource, code);
        var settingsAfter = File.ReadAllBytes(Path.Combine(repo.Root, ".D2NET", "D2NET-Settings.json"));
        Assert.Equal(settingsBefore, settingsAfter);
    }

    [Fact]
    public void SourceRootSelfRejected_ExitsWith17()
    {
        using var repo = BuildSkeletonWorkspace();
        var (code, _, se) = Run(new[] { "--remove-exclude", "." }, repo.Root);
        Assert.Equal(ExitCodes.RemoveExcludePathOutsideSource, code);
        Assert.Contains("source root", se);
    }
}
