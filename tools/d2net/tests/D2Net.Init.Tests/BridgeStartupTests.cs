using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;

namespace D2Net.Init.Tests;

/// <summary>
/// SC-006 (port released after exit), SC-007 (port-in-use fail-fast),
/// SC-008 (Node missing fail-fast). Bridge-bundle-missing is exercised
/// transitively when the build output's pgbridge directory is intact;
/// a deliberate-corruption test would require restoring state, omitted for v1.
/// </summary>
public class BridgeStartupTests
{
    private static (int code, string stdout, string stderr) Run(
        string[] args, string cwd, string? path = null)
    {
        var so = new StringWriter();
        var se = new StringWriter();
        if (path is not null)
        {
            var prev = System.Environment.GetEnvironmentVariable("PATH");
            System.Environment.SetEnvironmentVariable("PATH", path);
            try
            {
                return (Program.Run(args, new StringReader(""), so, se, cwd), so.ToString(), se.ToString());
            }
            finally
            {
                System.Environment.SetEnvironmentVariable("PATH", prev);
            }
        }
        return (Program.Run(args, new StringReader(""), so, se, cwd), so.ToString(), se.ToString());
    }

    [Fact]
    public void PortInUse_FailsFastWithoutCreatingWorkspace()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/x.dart");
        var port = PortPicker.NextFreePort();
        var holder = new TcpListener(IPAddress.Loopback, port);
        holder.Start();
        try
        {
            var (code, _, stderr) = Run(
                new[] { "--source", "glp_runtime", "--target-extension", "_net",
                        "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                        "--non-interactive", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.True(
                code == ExitCodes.BridgePortInUse || code == ExitCodes.BridgeStartFailed,
                $"expected BridgePortInUse or BridgeStartFailed; got {code}; stderr={stderr}");
            // SC-007: no .D2NET created.
            Assert.False(Directory.Exists(Path.Combine(repo.Root, ".D2NET")),
                "no workspace should exist when bridge fails to start");
        }
        finally { holder.Stop(); }
    }

    [Fact]
    public void NodeMissing_FailsFastWithoutCreatingWorkspace()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/x.dart");
        // Set PATH to a single empty directory so node cannot be resolved.
        var emptyDir = Path.Combine(Path.GetTempPath(), "d2net-empty-path-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyDir);
        try
        {
            var (code, _, stderr) = Run(
                new[] { "--source", "glp_runtime", "--target-extension", "_net",
                        "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                        "--non-interactive", "--bridge-port", PortPicker.NextFreePort().ToString() },
                repo.Root, path: emptyDir);
            Assert.Equal(ExitCodes.NodeMissing, code);
            Assert.Contains("Node.js", stderr);
            Assert.Contains("nodejs.org", stderr);
            Assert.False(Directory.Exists(Path.Combine(repo.Root, ".D2NET")),
                "no workspace should exist when node is missing");
        }
        finally { try { Directory.Delete(emptyDir, true); } catch { } }
    }

    [Fact]
    public void BridgePortReleasedAfterCommandExits()
    {
        // SC-006: after a D2NET command exits, the bridge port is released.
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/x.dart");
        var port = PortPicker.NextFreePort();
        var args = new[] { "--source", "glp_runtime", "--target-extension", "_net",
                           "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                           "--non-interactive", "--bridge-port", port.ToString() };
        Assert.Equal(ExitCodes.Success, Run(args, repo.Root).code);

        // Allow a brief grace for OS-level socket teardown (TIME_WAIT, etc.).
        // We re-bind in a small loop to be tolerant of the OS's TCP state machine.
        TcpListener? rebind = null;
        for (int i = 0; i < 20; i++)
        {
            try
            {
                rebind = new TcpListener(IPAddress.Loopback, port);
                rebind.Start();
                rebind.Stop();
                rebind = null;
                return;
            }
            catch (SocketException)
            {
                System.Threading.Thread.Sleep(100);
            }
        }
        Assert.Fail($"bridge port {port} not released within ~2s after command exit");
    }
}
