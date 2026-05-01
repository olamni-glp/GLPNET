using System;
using System.IO;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;

namespace D2Net.Init.Tests;

/// <summary>
/// US3 / FR-011 / clarification 2026-05-01: detect lock contention at
/// bridge startup and emit a distinct exit code so the calling skill can
/// retry. Implemented as a unit test with an injected fake bridge that
/// throws a <see cref="BridgeStartException"/> with a payload matching the
/// research R5 pattern set ("data directory in use").
/// </summary>
public class AddExcludeContentionTests
{
    private static (int code, string stdout, string stderr) RunWithFakeBridge(
        string[] args, string cwd, BridgeStartException toThrow, string stdin = "")
    {
        var so = new StringWriter();
        var se = new StringWriter();

        // Reach into Program via reflection isn't necessary because we can call
        // AddExcludeRunner directly. But we still want to drive through the
        // ArgParser to exercise the same wiring as production.
        // Drive through Program.Run with a custom factory threaded through a
        // static hook. Simplest: invoke AddExcludeRunner.Run directly and skip
        // Program.Run for this contention case.
        var parsed = ArgParserHelper.ParseAddExclude(args, cwd);
        Assert.NotNull(parsed);

        var runner = new AddExcludeRunner(so, se);
        var code = runner.RunForTesting(parsed!, (opts, w) => throw toThrow);
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
    public void DataDirInUse_PgliteInitFailed_MapsTo15()
    {
        using var repo = BuildSkeletonWorkspace();
        var ex = new BridgeStartException(
            BridgeStartFailureKind.PgliteInitFailed,
            "pglite_init_failed: EBUSY: data directory in use",
            bridge: null);
        var (code, _, se) = RunWithFakeBridge(
            new[] { "--add-exclude", "lib", "--bridge-port", "54400" },
            repo.Root, ex);
        Assert.Equal(ExitCodes.AddExcludeWorkspaceLocked, code);
        Assert.Contains("locked by another process", se);
    }

    [Fact]
    public void OtherBridgeError_LockMessage_MapsTo15()
    {
        using var repo = BuildSkeletonWorkspace();
        var ex = new BridgeStartException(
            BridgeStartFailureKind.OtherBridgeError,
            "could not lock data directory",
            bridge: null);
        var (code, _, se) = RunWithFakeBridge(
            new[] { "--add-exclude", "lib", "--bridge-port", "54400" },
            repo.Root, ex);
        Assert.Equal(ExitCodes.AddExcludeWorkspaceLocked, code);
        Assert.Contains("locked by another process", se);
    }

    [Fact]
    public void OtherBridgeError_GenericMessage_DoesNotMatchLockPattern()
    {
        using var repo = BuildSkeletonWorkspace();
        var ex = new BridgeStartException(
            BridgeStartFailureKind.OtherBridgeError,
            "something else went wrong",
            bridge: null);
        var (code, _, _) = RunWithFakeBridge(
            new[] { "--add-exclude", "lib", "--bridge-port", "54400" },
            repo.Root, ex);
        Assert.Equal(ExitCodes.BridgeStartFailed, code);
    }

    [Fact]
    public void NodeMissing_DoesNotMatchLockPattern()
    {
        using var repo = BuildSkeletonWorkspace();
        var ex = new BridgeStartException(
            BridgeStartFailureKind.NodeMissing,
            "node executable not found",
            bridge: null);
        var (code, _, _) = RunWithFakeBridge(
            new[] { "--add-exclude", "lib", "--bridge-port", "54400" },
            repo.Root, ex);
        Assert.Equal(ExitCodes.NodeMissing, code);
    }
}

/// <summary>
/// Test helper for parsing --add-exclude args without invoking Program.Run.
/// Uses internal ArgParser.Parse via InternalsVisibleTo if available; falls
/// back to a minimal manual parse for these focused tests.
/// </summary>
internal static class ArgParserHelper
{
    public static AddExcludeOptions? ParseAddExclude(string[] args, string cwd)
    {
        var paths = new System.Collections.Generic.List<string>();
        bool json = false;
        int? port = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--add-exclude":
                    if (++i < args.Length) paths.Add(args[i]);
                    break;
                case "--json":
                    json = true; break;
                case "--bridge-port":
                    if (++i < args.Length && int.TryParse(args[i], out var p)) port = p;
                    break;
            }
        }
        if (paths.Count == 0) return null;
        return new AddExcludeOptions(cwd, paths, json, port);
    }
}
