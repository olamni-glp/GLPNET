using Xunit;

// Disable parallel test execution: tests spawn their own Node.js bridges
// against PGLite WASM, which serialize on the OS-assigned port and on PGLite's
// per-process WASM init. Running them in parallel produces transient flakes
// (port collisions in PortPicker's TOCTOU window, OOM under heavy WASM load).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// Test-only: extend the bridge BRIDGE_READY budget to absorb PGLite WASM
// cold-init times when spawns are rapid-fire across tests. Production stays
// at the FR-005 spec-mandated 15s. Initialised once per test-process.
internal static class TestSetup
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void ExtendBridgeReadyTimeout()
    {
        if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("D2NET_BRIDGE_READY_TIMEOUT_SECONDS")))
        {
            System.Environment.SetEnvironmentVariable("D2NET_BRIDGE_READY_TIMEOUT_SECONDS", "60");
        }
    }
}
