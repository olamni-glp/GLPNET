using System.IO;
using System.Net;
using System.Net.Sockets;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;

namespace D2Net.Init.Tests;

/// <summary>
/// Q3 / FR-012: inspection commands default to the persisted connection.port;
/// --bridge-port on a non-init invocation overrides only the live run and does
/// NOT modify D2NET-Settings.json.
/// </summary>
public class InspectionPortLifecycleTests
{
    private static (int code, string stdout, string stderr) Run(string[] args, string cwd)
    {
        var so = new StringWriter();
        var se = new StringWriter();
        var code = Program.Run(args, new StringReader(""), so, se, cwd);
        return (code, so.ToString(), se.ToString());
    }

    [Fact]
    public void InspectionUsesPersistedPortWhenFlagOmitted()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/x.dart");
        var customPort = PortPicker.NextFreePort();
        var initArgs = new[] { "--source", "glp_runtime", "--target-extension", "_net",
                               "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                               "--non-interactive", "--bridge-port", customPort.ToString() };
        Assert.Equal(ExitCodes.Success, Run(initArgs, repo.Root).code);

        // Pre-bind the persisted port so the inspection's bridge-spawn fails with
        // EADDRINUSE -- proving the inspection picked the persisted port (not the
        // hardcoded default 54400).
        var holder = new TcpListener(IPAddress.Loopback, customPort);
        holder.Start();
        try
        {
            var (code, _, stderr) = Run(new[] { "--list" }, repo.Root);
            Assert.NotEqual(ExitCodes.Success, code);
            Assert.True(
                code == ExitCodes.BridgePortInUse || code == ExitCodes.BridgeStartFailed,
                $"expected BridgePortInUse or BridgeStartFailed, got {code} stderr={stderr}");
        }
        finally { holder.Stop(); }
    }

    [Fact]
    public void InspectionBridgePortOverrideDoesNotRewriteSettings()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/x.dart");
        var initPort = PortPicker.NextFreePort();
        var initArgs = new[] { "--source", "glp_runtime", "--target-extension", "_net",
                               "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                               "--non-interactive", "--bridge-port", initPort.ToString() };
        Assert.Equal(ExitCodes.Success, Run(initArgs, repo.Root).code);

        var settingsFile = Path.Combine(repo.Root, ".D2NET", "D2NET-Settings.json");
        var contentBefore = File.ReadAllBytes(settingsFile);
        var mtimeBefore = File.GetLastWriteTimeUtc(settingsFile);

        // Inspection with a different --bridge-port: must succeed (its own bridge spawns
        // on the override port) AND must NOT rewrite settings.
        var overridePort = PortPicker.NextFreePort();
        Assert.NotEqual(initPort, overridePort);
        var (code, _, _) = Run(new[] { "--list", "--bridge-port", overridePort.ToString() }, repo.Root);
        Assert.Equal(ExitCodes.Success, code);

        var contentAfter = File.ReadAllBytes(settingsFile);
        Assert.Equal(contentBefore, contentAfter);
        Assert.Equal(mtimeBefore, File.GetLastWriteTimeUtc(settingsFile));
    }
}
