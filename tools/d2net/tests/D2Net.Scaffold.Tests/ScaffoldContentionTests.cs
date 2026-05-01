using System.IO;
using D2Net.Init;
using D2Net.Scaffold.Tests.Fixtures;

namespace D2Net.Scaffold.Tests;

/// <summary>
/// T029a: lock-contention exit-code mapping. Mirrors AddExcludeContentionTests.
/// Drives ScaffoldRunner directly with a fake bridge factory that throws
/// <see cref="BridgeStartException"/> with payloads matching the lock-contention
/// pattern set; asserts exit 28.
/// </summary>
public class ScaffoldContentionTests
{
    private static (int code, string stdout, string stderr) RunWithFakeBridge(
        BridgeStartException toThrow)
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/foo.dart");
        var (initCode, _, _, _) = InitHelper.Init(repo.Root);
        Assert.Equal(D2Net.Init.ExitCodes.Success, initCode);

        var so = new StringWriter();
        var se = new StringWriter();
        var runner = new ScaffoldRunner(new StringReader(""), so, se);
        var opts = new ScaffoldOptions(repo.Root, Json: false, ForceDeleteTarget: false, BridgePortOverride: null);
        var code = runner.RunForTesting(opts, (o, w) => throw toThrow);
        return (code, so.ToString(), se.ToString());
    }

    [Fact]
    public void DataDirInUse_PgliteInitFailed_MapsTo28()
    {
        var ex = new BridgeStartException(
            BridgeStartFailureKind.PgliteInitFailed,
            "pglite_init_failed: EBUSY: data directory in use",
            bridge: null);
        var (code, _, se) = RunWithFakeBridge(ex);
        Assert.Equal(ExitCodes.ScaffoldWorkspaceLocked, code);
        Assert.Contains("locked by another process", se);
    }

    [Fact]
    public void OtherBridgeError_LockMessage_MapsTo28()
    {
        var ex = new BridgeStartException(
            BridgeStartFailureKind.OtherBridgeError,
            "could not lock data directory",
            bridge: null);
        var (code, _, se) = RunWithFakeBridge(ex);
        Assert.Equal(ExitCodes.ScaffoldWorkspaceLocked, code);
        Assert.Contains("locked by another process", se);
    }

    [Fact]
    public void NodeMissing_DoesNotMatchLockPattern()
    {
        var ex = new BridgeStartException(
            BridgeStartFailureKind.NodeMissing,
            "node executable not found",
            bridge: null);
        var (code, _, _) = RunWithFakeBridge(ex);
        Assert.Equal(D2Net.Init.ExitCodes.NodeMissing, code);
    }

    [Fact]
    public void OtherBridgeError_GenericMessage_DoesNotMatchLockPattern()
    {
        var ex = new BridgeStartException(
            BridgeStartFailureKind.OtherBridgeError,
            "something else went wrong",
            bridge: null);
        var (code, _, _) = RunWithFakeBridge(ex);
        Assert.Equal(D2Net.Init.ExitCodes.BridgeStartFailed, code);
    }
}
