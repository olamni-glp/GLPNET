using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using D2Net.Init;
using Npgsql;

namespace D2Net.Scaffold;

/// <summary>
/// End-to-end orchestrator for one <c>d2net-scaffold</c> invocation.
///
/// Pipeline (data-model.md A4 sequence):
/// <list type="number">
///   <item>Resolve workspace layout; verify <c>.D2NET/</c> exists.</item>
///   <item>Read settings snapshot (<c>source_dir</c>, <c>target_dir</c>, <c>target_extension</c>,
///         <c>excluded_directories</c>).</item>
///   <item>Verify the configured source directory exists on disk.</item>
///   <item>Spawn the PGLite bridge subprocess; map lock contention to exit 28.</item>
///   <item>Open the workspace database via Npgsql.</item>
///   <item>Walk the source tree + read <c>scaffold_tracker</c>; build the plan.</item>
///   <item>FR-012 + FR-013 rejections (or destructive override branch).</item>
///   <item>Write all planned files into <c>&lt;target&gt;.d2net-tmp/</c>.</item>
///   <item>Apply the database transaction (single COMMIT).</item>
///   <item>Atomic rename staging -&gt; live.</item>
///   <item>Best-effort UPDATE phase_status to COMPLETED.</item>
///   <item>Emit success summary; return 0.</item>
/// </list>
/// </summary>
public sealed class ScaffoldRunner
{
    private readonly TextReader _stdin;
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;

    public ScaffoldRunner(TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        _stdin = stdin;
        _stdout = stdout;
        _stderr = stderr;
    }

    /// <summary>Test seam: factory signature for injecting a fake bridge.</summary>
    public delegate System.IDisposable BridgeFactory(BridgeOptions opts, TextWriter stderr);

    public int Run(ScaffoldOptions opts) => RunInternal(opts, bridgeFactory: null);

    /// <summary>Test seam: pass a non-null factory to override bridge creation.</summary>
    public int RunForTesting(ScaffoldOptions opts, BridgeFactory factory)
        => RunInternal(opts, factory);

    private int RunInternal(ScaffoldOptions opts, BridgeFactory? bridgeFactory)
    {
        var sw = Stopwatch.StartNew();

        // ---- Step 1: workspace layout ----
        var layout = WorkspaceLayout.Resolve(opts.RepoRoot);
        if (!Directory.Exists(layout.WorkspaceDir) || !File.Exists(layout.SettingsFile))
        {
            var msg = $"d2net-scaffold: no D2NET workspace at {layout.WorkspaceDir}. Run d2net-init first.";
            _stderr.WriteLine(msg);
            EmitJsonErrorIfRequested(opts.Json, ExitCodes.ScaffoldWorkspaceMissing, msg);
            return ExitCodes.ScaffoldWorkspaceMissing;
        }

        // ---- Step 2: settings snapshot ----
        var snapshot = SettingsWriter.TryReadSnapshot(layout.SettingsFile);
        if (snapshot is null)
        {
            var msg = $"d2net-scaffold: D2NET-Settings.json at '{layout.SettingsFile}' is missing or unreadable.";
            _stderr.WriteLine(msg);
            EmitJsonErrorIfRequested(opts.Json, ExitCodes.ScaffoldWorkspaceMissing, msg);
            return ExitCodes.ScaffoldWorkspaceMissing;
        }

        var sourceDir = snapshot.SourceDir;
        var targetDir = ReadTargetDir(layout.SettingsFile);
        var targetExtension = ReadTargetExtension(layout.SettingsFile);
        var excludedDirs = snapshot.ExcludedDirectories;

        if (string.IsNullOrEmpty(targetDir))
        {
            var msg = $"d2net-scaffold: target_dir not configured in {layout.SettingsFile}. Re-run d2net-init.";
            _stderr.WriteLine(msg);
            EmitJsonErrorIfRequested(opts.Json, ExitCodes.ScaffoldWorkspaceMissing, msg);
            return ExitCodes.ScaffoldWorkspaceMissing;
        }

        var sourceRootAbs = Path.GetFullPath(Path.Combine(layout.RepoRoot, sourceDir));
        var targetRootAbs = Path.GetFullPath(Path.Combine(layout.RepoRoot, targetDir));

        // ---- Step 3: source dir exists ----
        if (!Directory.Exists(sourceRootAbs))
        {
            var msg = $"d2net-scaffold: source directory '{sourceDir}' not found at '{sourceRootAbs}'.";
            _stderr.WriteLine(msg);
            EmitJsonErrorIfRequested(opts.Json, ExitCodes.ScaffoldSourceMissing, msg);
            return ExitCodes.ScaffoldSourceMissing;
        }

        // ---- Steps 4-11: bridge + DB + plan + write + commit + rename ----
        var persistedPort = SettingsWriter.TryReadPersistedPort(layout.SettingsFile);
        var port = opts.BridgePortOverride ?? persistedPort ?? BridgeOptions.DefaultPort;
        var bridgeOpts = BridgeOptions.ForDataDir(Path.GetFullPath(layout.PgDir), port);

        var stagingDir = StagingMutator.ComputeStagingDir(targetRootAbs);

        try
        {
            using var bridge = (bridgeFactory ?? DefaultBridgeFactory)(bridgeOpts, _stderr);

            // Feature 012: the unified bridge listens on an ephemeral port. If
            // bridge is a real PgBridgeProcess, prefer its actual port over the
            // (now-meaningless) persisted port from settings. Test fakes that
            // don't expose Port fall back to the requested opts.
            var actualPort = (bridge as D2Net.Init.PgBridgeProcess)?.Port ?? bridgeOpts.Port;
            var actualBridgeOpts = BridgeOptions.ForDataDir(bridgeOpts.DataDir, actualPort);
            var npgsqlString = DbConnectionStringBuilder.BuildNpgsql(actualBridgeOpts);
            using var conn = new NpgsqlConnection(npgsqlString);
            try { conn.Open(); }
            catch (NpgsqlException ex)
            {
                var msg = $"d2net-scaffold: could not open the workspace database via the PGLite bridge at " +
                          $"{bridgeOpts.Host}:{bridgeOpts.Port}. Original error: {ex.Message}";
                _stderr.WriteLine(msg);
                EmitJsonErrorIfRequested(opts.Json, ExitCodes.ScaffoldDbWriteFailed, msg);
                return ExitCodes.ScaffoldDbWriteFailed;
            }

            try
            {
                // ---- Step 6: plan ----
                var plan = TargetTreePlanner.Plan(layout.RepoRoot, sourceDir, targetDir, excludedDirs, conn);

                // ---- Step 7a: FR-013 collisions ----
                if (plan.Collisions.Count > 0)
                {
                    var first = plan.Collisions[0];
                    foreach (var c in plan.Collisions)
                    {
                        _stderr.WriteLine(
                            $"d2net-scaffold: __<basename> collision detected at {c.ConflictingRelPath}. " +
                            $"Source contains both {c.DartFileRelPath} and an unrelated entry that would be overwritten by the working directory.");
                    }
                    var msg = $"d2net-scaffold: __<basename> collision detected at {first.ConflictingRelPath}.";
                    if (opts.Json)
                        ScaffoldRunSummary.WriteJsonCollisions(_stdout, ExitCodes.ScaffoldWorkdirCollision, msg, plan.Collisions);
                    return ExitCodes.ScaffoldWorkdirCollision;
                }

                // ---- Step 7b: FR-012 (with FR-012a destructive override) ----
                bool destructiveOverride = false;
                if (plan.TargetExists && !plan.TargetIsManaged)
                {
                    if (!opts.ForceDeleteTarget)
                    {
                        var msg = $"d2net-scaffold: target directory {targetRootAbs} exists but was not produced " +
                                  "by a prior scaffold run. Re-issue with --FORCE --DELETE-TARGET to delete it " +
                                  "(interactive confirmation required).";
                        _stderr.WriteLine(msg);
                        if (opts.Json)
                            ScaffoldRunSummary.WriteJsonError(_stdout, ExitCodes.ScaffoldTargetNotEmptyAndNotManaged, msg,
                                ("target_abs", targetRootAbs));
                        return ExitCodes.ScaffoldTargetNotEmptyAndNotManaged;
                    }

                    // FR-012a: hard-gated interactive prompt.
                    if (!DestructiveTargetGate.Confirm(targetRootAbs, _stdin, _stderr))
                    {
                        var msg = "d2net-scaffold: --FORCE --DELETE-TARGET cancelled by operator. No changes made.";
                        _stderr.WriteLine(msg);
                        EmitJsonErrorIfRequested(opts.Json, ExitCodes.ScaffoldOperatorCancelledTargetDeletion, msg);
                        return ExitCodes.ScaffoldOperatorCancelledTargetDeletion;
                    }

                    // Confirmed: delete the live target tree before the staging rename.
                    try
                    {
                        StagingMutator.DeleteTargetTree(targetRootAbs);
                    }
                    catch (System.Exception ex) when (ex is IOException || ex is System.UnauthorizedAccessException)
                    {
                        var msg = $"d2net-scaffold: filesystem error during destructive delete of {targetRootAbs}: {ex.Message}.";
                        _stderr.WriteLine(msg);
                        EmitJsonErrorIfRequested(opts.Json, ExitCodes.ScaffoldCopyError, msg);
                        return ExitCodes.ScaffoldCopyError;
                    }
                    destructiveOverride = true;
                }

                // ---- Step 8: staging copy ----
                StagingMutator.StagingCounts counts;
                try
                {
                    counts = StagingMutator.WriteStaging(plan, stagingDir);
                }
                catch (System.Exception ex) when (ex is IOException || ex is System.UnauthorizedAccessException)
                {
                    var msg = $"d2net-scaffold: filesystem error during copy: {ex.Message}. " +
                              $"Staging directory {stagingDir} may have been retained for inspection.";
                    _stderr.WriteLine(msg);
                    EmitJsonErrorIfRequested(opts.Json, ExitCodes.ScaffoldCopyError, msg);
                    SafeCleanupStaging(stagingDir);
                    return ExitCodes.ScaffoldCopyError;
                }

                // ---- Step 9: DB transaction ----
                ScaffoldDbWriter.DbCounts dbCounts;
                try
                {
                    dbCounts = ScaffoldDbWriter.ApplyScaffold(conn, plan, destructiveOverride);
                }
                catch (System.Exception ex) when (ex is NpgsqlException || ex is PostgresException
                                                  || ex is System.InvalidOperationException)
                {
                    var msg = $"d2net-scaffold: workspace database update failed: {ex.Message}. " +
                              "No changes applied; staging directory removed.";
                    _stderr.WriteLine(msg);
                    EmitJsonErrorIfRequested(opts.Json, ExitCodes.ScaffoldDbWriteFailed, msg);
                    SafeCleanupStaging(stagingDir);
                    return ExitCodes.ScaffoldDbWriteFailed;
                }

                // ---- Step 10: atomic rename staging -> live ----
                try
                {
                    StagingMutator.AtomicRenameStagingToLive(stagingDir, targetRootAbs);
                }
                catch (System.Exception ex) when (ex is IOException || ex is System.UnauthorizedAccessException)
                {
                    var msg = $"d2net-scaffold: filesystem error during rename of staging '{stagingDir}' " +
                              $"to live target '{targetRootAbs}': {ex.Message}. " +
                              "Workspace database has the new state; re-run scaffold to reconcile.";
                    _stderr.WriteLine(msg);
                    EmitJsonErrorIfRequested(opts.Json, ExitCodes.ScaffoldCopyError, msg);
                    return ExitCodes.ScaffoldCopyError;
                }

                // ---- Step 11: best-effort UPDATE phase_status COMPLETED ----
                try { ScaffoldDbWriter.MarkPhaseCompleted(conn); } catch { /* best effort */ }

                // ---- Step 12: success summary ----
                sw.Stop();
                var summary = new ScaffoldRunSummary
                {
                    SourceDir = sourceDir,
                    TargetDir = targetDir,
                    TargetAbs = targetRootAbs,
                    Extension = targetExtension,
                    Exclusions = excludedDirs.Count,
                    FilesCopied = counts.FilesCopied,
                    WorkdirsCreated = counts.WorkdirsCreated,
                    DartFilesUpdated = dbCounts.DartFilesUpdated,
                    AddedPaths = plan.AddPaths.Count,
                    RemovedPaths = plan.RemovePaths.Count,
                    DestructiveOverrideUsed = destructiveOverride,
                    Elapsed = sw.Elapsed,
                };
                if (opts.Json) summary.WriteJson(_stdout); else summary.WriteText(_stdout);

                return ExitCodes.Success;
            }
            finally
            {
                conn.Close();
                NpgsqlConnection.ClearAllPools();
            }
        }
        catch (BridgeStartException ex)
        {
            var (mappedExit, msg) = MapBridgeStartFailure(ex, port);
            _stderr.WriteLine(msg);
            EmitJsonErrorIfRequested(opts.Json, mappedExit, msg);
            SafeCleanupStaging(stagingDir);
            return mappedExit;
        }
    }

    private static string ReadTargetDir(string settingsFilePath)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(settingsFilePath));
            if (doc.RootElement.TryGetProperty("target_dir", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String)
                return v.GetString() ?? "";
        }
        catch { /* fall through */ }
        return "";
    }

    private static string ReadTargetExtension(string settingsFilePath)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(settingsFilePath));
            if (doc.RootElement.TryGetProperty("target_extension", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String)
                return v.GetString() ?? "";
        }
        catch { /* fall through */ }
        return "";
    }

    private (int Exit, string Msg) MapBridgeStartFailure(BridgeStartException ex, int port)
    {
        // Lock-contention detection per research R5 — same pattern set as 007/008.
        if (ex.Kind == BridgeStartFailureKind.PgliteInitFailed
            || ex.Kind == BridgeStartFailureKind.OtherBridgeError
            || ex.Kind == BridgeStartFailureKind.UnexpectedExit)
        {
            var blob = (ex.Message ?? "") + " " + (ex.Bridge?.LastBridgeError ?? "");
            string[] markers = { "EBUSY", "EACCES", "data directory in use", "could not lock", "another process" };
            foreach (var m in markers)
            {
                if (blob.IndexOf(m, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return (ExitCodes.ScaffoldWorkspaceLocked,
                        "d2net-scaffold: workspace database is locked by another process. " +
                        "Retry shortly or stop the conflicting invocation.");
                }
            }
        }

        return ex.Kind switch
        {
            BridgeStartFailureKind.NodeMissing =>
                (D2Net.Init.ExitCodes.NodeMissing,
                 "d2net-scaffold: the PGLite bridge requires Node.js >= 20 on PATH. " +
                 "Install Node.js LTS from https://nodejs.org/ and retry."),
            BridgeStartFailureKind.BundleMissing =>
                (D2Net.Init.ExitCodes.BridgeBundleMissing,
                 "d2net-scaffold: the PGLite bridge bundle is missing or corrupt. Reinstall d2net-scaffold. " +
                 $"detail: {ex.Message}"),
            BridgeStartFailureKind.PortInUse =>
                (D2Net.Init.ExitCodes.BridgePortInUse,
                 $"d2net-scaffold: PGLite bridge port {port} is already in use. " +
                 "Either stop the conflicting process, or supply --bridge-port <n>."),
            BridgeStartFailureKind.PgliteInitFailed =>
                (D2Net.Init.ExitCodes.DbOpenFailed,
                 "d2net-scaffold: PGLite bridge failed to open the workspace database: " +
                 $"BRIDGE_ERROR {ex.Message}"),
            _ => (D2Net.Init.ExitCodes.BridgeStartFailed,
                  $"d2net-scaffold: PGLite bridge failed to start: {ex.Message}"),
        };
    }

    private void EmitJsonErrorIfRequested(bool json, int code, string message)
    {
        if (!json) return;
        ScaffoldRunSummary.WriteJsonError(_stdout, code, message);
    }

    private static void SafeCleanupStaging(string stagingDir)
    {
        try { if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, recursive: true); }
        catch { /* best effort */ }
    }

    private static readonly BridgeFactory DefaultBridgeFactory =
        (opts, stderr) => PgBridgeProcess.StartAsync(opts.Port, opts.DataDir, stderr).GetAwaiter().GetResult();
}
