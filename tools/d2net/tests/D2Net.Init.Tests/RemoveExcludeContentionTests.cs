using System;
using System.Collections.Generic;
using System.IO;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;

namespace D2Net.Init.Tests;

/// <summary>
/// US3 / FR-013: lock-contention exit code 20 via fake-bridge injection.
/// Mirrors AddExcludeContentionTests pattern from feature 007.
/// </summary>
public class RemoveExcludeContentionTests
{
    private static (int code, string stdout, string stderr) RunWithFakeBridge(
        string[] args, string cwd, BridgeStartException toThrow)
    {
        var so = new StringWriter();
        var se = new StringWriter();

        var paths = new List<string>();
        bool json = false;
        bool allowSystem = false;
        int? port = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--remove-exclude":
                    if (++i < args.Length) paths.Add(args[i]);
                    break;
                case "--json": json = true; break;
                case "--allow-system-exclusions": allowSystem = true; break;
                case "--bridge-port":
                    if (++i < args.Length && int.TryParse(args[i], out var p)) port = p;
                    break;
            }
        }
        var opts = new RemoveExcludeOptions(cwd, paths, allowSystem, json, port);
        var runner = new RemoveExcludeRunner(so, se);
        var code = runner.RunForTesting(opts, (o, w) => throw toThrow);
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
  "excluded_directories": ["lib"],
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
    public void DataDirInUse_PgliteInitFailed_MapsTo20()
    {
        using var repo = BuildSkeletonWorkspace();
        var ex = new BridgeStartException(
            BridgeStartFailureKind.PgliteInitFailed,
            "pglite_init_failed: EBUSY: data directory in use",
            bridge: null);
        var (code, _, se) = RunWithFakeBridge(
            new[] { "--remove-exclude", "lib", "--bridge-port", "54400" },
            repo.Root, ex);
        Assert.Equal(ExitCodes.RemoveExcludeWorkspaceLocked, code);
        Assert.Contains("locked by another process", se);
    }

    [Fact]
    public void OtherBridgeError_LockMessage_MapsTo20()
    {
        using var repo = BuildSkeletonWorkspace();
        var ex = new BridgeStartException(
            BridgeStartFailureKind.OtherBridgeError,
            "could not lock data directory",
            bridge: null);
        var (code, _, se) = RunWithFakeBridge(
            new[] { "--remove-exclude", "lib", "--bridge-port", "54400" },
            repo.Root, ex);
        Assert.Equal(ExitCodes.RemoveExcludeWorkspaceLocked, code);
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
            new[] { "--remove-exclude", "lib", "--bridge-port", "54400" },
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
            new[] { "--remove-exclude", "lib", "--bridge-port", "54400" },
            repo.Root, ex);
        Assert.Equal(ExitCodes.NodeMissing, code);
    }
}
