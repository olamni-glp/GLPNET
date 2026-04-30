using System.IO;
using Npgsql;

namespace D2Net.Init;

public sealed class InspectionRunner
{
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;
    public InspectionRunner(TextWriter stdout, TextWriter stderr) { _stdout = stdout; _stderr = stderr; }

    public int Run(InspectOptions opts)
    {
        var layout = WorkspaceLayout.Resolve(opts.RepoRoot);
        if (!Directory.Exists(layout.WorkspaceDir) || !File.Exists(layout.SettingsFile))
        {
            _stderr.WriteLine(
                $"no D2NET workspace found at '{layout.WorkspaceDir}'. Run d2net-init first.");
            return ExitCodes.WorkspaceMissingForInspection;
        }

        // FR-012 / Q3: read persisted port from settings; --bridge-port overrides for this
        // invocation only and does NOT modify settings.
        var persistedPort = SettingsWriter.TryReadPersistedPort(layout.SettingsFile);
        var port = opts.BridgePortOverride ?? persistedPort ?? BridgeOptions.DefaultPort;

        var bridgeOpts = BridgeOptions.ForDataDir(Path.GetFullPath(layout.PgDir), port);

        PgBridgeProcess bridge;
        try
        {
            bridge = PgBridgeProcess.StartAsync(bridgeOpts.Port, bridgeOpts.DataDir, _stderr).GetAwaiter().GetResult();
        }
        catch (BridgeStartException ex)
        {
            EmitBridgeStartFailure(ex, port);
            return MapBridgeStartExitCode(ex.Kind);
        }

        try
        {
            using (bridge)
            {
                var npgsqlString = DbConnectionStringBuilder.BuildNpgsql(bridgeOpts);
                using var conn = new NpgsqlConnection(npgsqlString);
                try { conn.Open(); }
                catch (NpgsqlException ex)
                {
                    _stderr.WriteLine(
                        $"could not open the workspace database via the PGLite bridge at " +
                        $"{bridgeOpts.Host}:{bridgeOpts.Port}. Original error: {ex.Message}");
                    return ExitCodes.DbOpenFailed;
                }

                switch (opts.Mode)
                {
                    case InspectMode.List:
                        Inspectors.ListInspector.Run(conn, opts.Json, _stdout);
                        break;
                    case InspectMode.Exclusions:
                        Inspectors.ExclusionsInspector.Run(conn, opts.Json, _stdout);
                        break;
                    case InspectMode.CurrentPhase:
                        Inspectors.CurrentPhaseInspector.Run(conn, opts.Json, _stdout);
                        break;
                }

                conn.Close();
                NpgsqlConnection.ClearAllPools();
            }
            return ExitCodes.Success;
        }
        catch
        {
            try { bridge.Dispose(); } catch { }
            throw;
        }
    }

    private void EmitBridgeStartFailure(BridgeStartException ex, int port)
    {
        switch (ex.Kind)
        {
            case BridgeStartFailureKind.NodeMissing:
                _stderr.WriteLine("The PGLite bridge requires Node.js >= 20 on PATH.");
                _stderr.WriteLine("Install Node.js LTS from https://nodejs.org/ and retry.");
                break;
            case BridgeStartFailureKind.BundleMissing:
                _stderr.WriteLine("The PGLite bridge bundle is missing or corrupt. Reinstall d2net-init.");
                _stderr.WriteLine($"detail: {ex.Message}");
                break;
            case BridgeStartFailureKind.PortInUse:
                _stderr.WriteLine($"PGLite bridge port {port} is already in use. " +
                                  $"Either stop the conflicting process, or supply --bridge-port <n>.");
                break;
            case BridgeStartFailureKind.PgliteInitFailed:
                _stderr.WriteLine("PGLite bridge failed to open the workspace database:");
                _stderr.WriteLine($"BRIDGE_ERROR {ex.Message}");
                _stderr.WriteLine("The workspace database appears to be unreadable. To rebuild from the source tree, re-run with:");
                _stderr.WriteLine("  d2net-init --FORCE --DELETE-EXISTING [other flags...]");
                break;
            default:
                _stderr.WriteLine($"PGLite bridge failed to start: {ex.Message}");
                break;
        }
    }

    private static int MapBridgeStartExitCode(BridgeStartFailureKind k) => k switch
    {
        BridgeStartFailureKind.NodeMissing      => ExitCodes.NodeMissing,
        BridgeStartFailureKind.BundleMissing    => ExitCodes.BridgeBundleMissing,
        BridgeStartFailureKind.PortInUse        => ExitCodes.BridgePortInUse,
        BridgeStartFailureKind.PgliteInitFailed => ExitCodes.DbOpenFailed,
        _                                       => ExitCodes.BridgeStartFailed,
    };
}
