using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Npgsql;

namespace D2Net.Init;

/// <summary>
/// Orchestrator for the incremental --add-exclude mode (feature 007).
///
/// Pipeline (research R4):
///   1. Resolve workspace layout; verify <c>.D2NET/</c> exists.
///   2. Read settings snapshot to obtain <c>source_dir</c> and the current
///      <c>excluded_directories</c> list.
///   3. Validate every supplied path: canonicalise, resolve under source root,
///      file-vs-directory check, intra-batch dedupe, classify against existing.
///   4. Reject the entire invocation on any path-level error
///      (<see cref="ExitCodes.AddExcludePathOutsideSource"/> /
///       <see cref="ExitCodes.AddExcludePathIsFile"/>).
///   5. Compute the new exclusion list (existing ∪ accepted, ascending).
///   6. Prepare a temp settings JSON file with the new list.
///   7. Start the PGLite bridge subprocess; map lock-contention to exit 15.
///   8. Apply the database transaction; map any failure to exit 14.
///   9. Atomically rename the temp settings file; rename failure → exit 13.
///  10. Emit success summary (text or --json) and return exit 0.
/// </summary>
public sealed class AddExcludeRunner
{
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;

    public AddExcludeRunner(TextWriter stdout, TextWriter stderr)
    {
        _stdout = stdout;
        _stderr = stderr;
    }

    public int Run(AddExcludeOptions opts) => Run(opts, bridgeFactory: null);

    /// <summary>
    /// Test seam: pass a non-null <paramref name="bridgeFactory"/> to override
    /// bridge creation (e.g. to inject a fake that simulates lock contention
    /// without spawning Node). Production callers use <see cref="Run(AddExcludeOptions)"/>.
    /// </summary>
    public int RunForTesting(AddExcludeOptions opts, BridgeFactory bridgeFactory)
        => Run(opts, bridgeFactory);

    private int Run(AddExcludeOptions opts, BridgeFactory? bridgeFactory)
    {
        // ---- Step 1: workspace layout ----
        var layout = WorkspaceLayout.Resolve(opts.RepoRoot);
        if (!Directory.Exists(layout.WorkspaceDir) || !File.Exists(layout.SettingsFile))
        {
            var msg = $"d2net-init: no D2NET workspace at '{layout.WorkspaceDir}'. " +
                      "Run d2net-init --source <name> --target-extension <ext> --target <name> first.";
            _stderr.WriteLine(msg);
            EmitJsonErrorIfRequested(opts.Json, ExitCodes.WorkspaceMissingForInspection, msg);
            return ExitCodes.WorkspaceMissingForInspection;
        }

        // ---- Step 2: settings snapshot ----
        var snapshot = SettingsWriter.TryReadSnapshot(layout.SettingsFile);
        if (snapshot is null)
        {
            var msg = $"d2net-init: D2NET-Settings.json at '{layout.SettingsFile}' is missing or unreadable.";
            _stderr.WriteLine(msg);
            EmitJsonErrorIfRequested(opts.Json, ExitCodes.WorkspaceMissingForInspection, msg);
            return ExitCodes.WorkspaceMissingForInspection;
        }

        var sourceDir = snapshot.SourceDir;
        var sourceRootAbs = Path.GetFullPath(Path.Combine(layout.RepoRoot, sourceDir));
        var existingExclusions = snapshot.ExcludedDirectories.ToList();

        // ---- Step 3: validate paths ----
        var sourceRelativePaths = new List<string>(opts.RawPaths.Count);
        var rejections = new List<string>();
        int rejectExitCode = ExitCodes.Success;

        foreach (var raw in opts.RawPaths)
        {
            string canonical;
            try { canonical = PathValidator.Canonicalise(raw); }
            catch (Exception ex)
            {
                rejections.Add($"--add-exclude path '{raw}': {ex.Message}");
                if (rejectExitCode == ExitCodes.Success) rejectExitCode = ExitCodes.ArgumentError;
                continue;
            }

            var rel = PathValidator.ResolveUnderSource(canonical, sourceRootAbs);
            if (rel is null)
            {
                rejections.Add(
                    $"--add-exclude path \"{raw}\" resolves outside source root \"{sourceDir}\".");
                rejectExitCode = ExitCodes.AddExcludePathOutsideSource;
                continue;
            }

            // Disallow the workspace folder name itself (".D2NET") even if it
            // happened to live under the source root for some reason.
            if (string.Equals(rel, WorkspaceLayout.WorkspaceFolderName, StringComparison.OrdinalIgnoreCase))
            {
                rejections.Add(
                    $"--add-exclude path \"{raw}\" names the D2NET workspace folder.");
                rejectExitCode = ExitCodes.AddExcludePathOutsideSource;
                continue;
            }

            if (PathValidator.LooksLikeFilePath(canonical, sourceRootAbs, rel))
            {
                rejections.Add(
                    $"--add-exclude path \"{raw}\" is a file. Exclusions are directory-only.");
                if (rejectExitCode == ExitCodes.Success
                    || rejectExitCode == ExitCodes.AddExcludePathIsFile)
                {
                    rejectExitCode = ExitCodes.AddExcludePathIsFile;
                }
                continue;
            }

            // Disallow excluding the source root itself ("."). This collapses
            // dart_files entirely; while permitted by the spec edge case, it
            // is almost always a misuse and would silently invalidate the
            // workspace. Treat it as outside-source for safety.
            if (rel == ".")
            {
                rejections.Add(
                    $"--add-exclude path \"{raw}\" is the source root itself; refusing to exclude everything.");
                rejectExitCode = ExitCodes.AddExcludePathOutsideSource;
                continue;
            }

            sourceRelativePaths.Add(rel);
        }

        if (rejections.Count > 0)
        {
            foreach (var r in rejections) _stderr.WriteLine("d2net-init: " + r);
            EmitJsonErrorIfRequested(opts.Json, rejectExitCode, rejections[0]);
            return rejectExitCode;
        }

        if (sourceRelativePaths.Count == 0)
        {
            var msg = "d2net-init: --add-exclude requires at least one path.";
            _stderr.WriteLine(msg);
            EmitJsonErrorIfRequested(opts.Json, ExitCodes.ArgumentError, msg);
            return ExitCodes.ArgumentError;
        }

        // ---- Classify ----
        var classification = PathValidator.ClassifyBatch(sourceRelativePaths, existingExclusions);

        // ---- Step 5: compute new exclusion list ----
        var newExcluded = existingExclusions
            .Concat(classification.AddedAscending)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // ---- Step 6: temp settings JSON ----
        string? tempPath = null;
        try
        {
            tempPath = SettingsWriter.PrepareTempSettingsWithExclusions(layout.SettingsFile, newExcluded);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            var msg = $"d2net-init: failed to prepare temp settings file: {ex.Message}";
            _stderr.WriteLine(msg);
            EmitJsonErrorIfRequested(opts.Json, ExitCodes.AddExcludeSettingsWriteFailed, msg);
            return ExitCodes.AddExcludeSettingsWriteFailed;
        }

        // ---- Steps 7–9: bridge, mutator, rename ----
        var persistedPort = SettingsWriter.TryReadPersistedPort(layout.SettingsFile);
        var port = opts.BridgePortOverride ?? persistedPort ?? BridgeOptions.DefaultPort;
        var bridgeOpts = BridgeOptions.ForDataDir(Path.GetFullPath(layout.PgDir), port);

        ExclusionMutator.MutationResult? mutation = null;
        int dbExitCode = ExitCodes.Success;
        bool committed = false;

        try
        {
            using var bridge = (bridgeFactory ?? DefaultBridgeFactory)(bridgeOpts, _stderr);

            try
            {
                var npgsqlString = DbConnectionStringBuilder.BuildNpgsql(bridgeOpts);
                using var conn = new NpgsqlConnection(npgsqlString);
                try { conn.Open(); }
                catch (NpgsqlException ex)
                {
                    var msg = $"d2net-init: could not open the workspace database via the PGLite bridge at " +
                              $"{bridgeOpts.Host}:{bridgeOpts.Port}. Original error: {ex.Message}";
                    _stderr.WriteLine(msg);
                    EmitJsonErrorIfRequested(opts.Json, ExitCodes.DbOpenFailed, msg);
                    return ExitCodes.DbOpenFailed;
                }

                if (classification.AddedAscending.Count > 0)
                {
                    try
                    {
                        mutation = ExclusionMutator.ApplyAddExclude(
                            conn, sourceDir, classification.AddedAscending);
                        committed = true;
                    }
                    catch (Exception ex) when (ex is NpgsqlException || ex is PostgresException || ex is InvalidOperationException)
                    {
                        var msg = $"d2net-init: workspace database update failed: {ex.Message}. No changes applied.";
                        _stderr.WriteLine(msg);
                        dbExitCode = ExitCodes.AddExcludeDbWriteFailed;
                    }
                }
                else
                {
                    // All-redundant batch: nothing to insert or delete; exit 0.
                    mutation = new ExclusionMutator.MutationResult(
                        InsertedRows: 0,
                        RemovedRowsByExclusion: new Dictionary<string, int>());
                    committed = true;
                }

                conn.Close();
                NpgsqlConnection.ClearAllPools();
            }
            catch
            {
                throw;
            }
        }
        catch (BridgeStartException ex)
        {
            var (mappedExit, msg) = MapBridgeStartFailure(ex, port);
            _stderr.WriteLine(msg);
            EmitJsonErrorIfRequested(opts.Json, mappedExit, msg);
            // No commit happened. Clean up temp settings file.
            TryDelete(tempPath);
            return mappedExit;
        }

        if (!committed)
        {
            // DB transaction failed and rolled back. Clean up temp JSON; return.
            TryDelete(tempPath);
            EmitJsonErrorIfRequested(opts.Json, dbExitCode, "workspace database update failed");
            return dbExitCode;
        }

        // ---- Step 9: rename ----
        try
        {
            // Only commit the temp file if the on-disk JSON would actually change.
            // (For all-redundant runs the JSON is byte-identical so we can skip.)
            if (classification.AddedAscending.Count > 0)
            {
                SettingsWriter.CommitTempFile(tempPath!, layout.SettingsFile);
            }
            else
            {
                TryDelete(tempPath);
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            var msg = "d2net-init: workspace database updated, but D2NET-Settings.json could not be rewritten: " +
                      ex.Message + ". Re-run with the same arguments to resync the settings file.";
            _stderr.WriteLine(msg);
            EmitJsonErrorIfRequested(opts.Json, ExitCodes.AddExcludeSettingsWriteFailed, msg);
            // The temp file remains on disk for diagnostic purposes; the
            // re-run path will overwrite it.
            return ExitCodes.AddExcludeSettingsWriteFailed;
        }

        // ---- Step 10: success summary ----
        EmitSuccessSummary(opts.Json, classification, mutation!);
        return ExitCodes.Success;
    }

    private void EmitSuccessSummary(
        bool json,
        PathValidator.BatchClassification classification,
        ExclusionMutator.MutationResult mutation)
    {
        var added = classification.AddedAscending;
        var redundant = classification.PerInput
            .Where(t => t.Cls != PathValidator.Classification.Added)
            .Select(t => (t.Path, t.Cls, t.Ancestor))
            .ToList();
        var removalRows = added
            .Select(p => (Exclusion: p, Rows: mutation.RemovedRowsByExclusion.TryGetValue(p, out var r) ? r : 0))
            .ToList();
        int totalRemoved = removalRows.Sum(t => t.Rows);

        if (json)
        {
            using var stream = new MemoryStream();
            using (var w = new Utf8JsonWriter(stream))
            {
                w.WriteStartObject();
                w.WriteString("result", "applied");

                w.WritePropertyName("added");
                w.WriteStartArray();
                foreach (var a in added) w.WriteStringValue(a);
                w.WriteEndArray();

                w.WritePropertyName("redundant");
                w.WriteStartArray();
                foreach (var (path, cls, anc) in redundant)
                {
                    w.WriteStartObject();
                    w.WriteString("path", path);
                    w.WriteString("reason",
                        cls == PathValidator.Classification.RedundantUnderAncestor
                            ? "covered-by-ancestor"
                            : "already-excluded");
                    if (cls == PathValidator.Classification.RedundantUnderAncestor && anc is not null)
                        w.WriteString("ancestor", anc);
                    w.WriteEndObject();
                }
                w.WriteEndArray();

                w.WritePropertyName("removed_rows");
                w.WriteStartArray();
                foreach (var (excl, rows) in removalRows)
                {
                    w.WriteStartObject();
                    w.WriteString("exclusion", excl);
                    w.WriteNumber("rows", rows);
                    w.WriteEndObject();
                }
                w.WriteEndArray();

                w.WritePropertyName("totals");
                w.WriteStartObject();
                w.WriteNumber("added", added.Count);
                w.WriteNumber("redundant", redundant.Count);
                w.WriteNumber("removed_rows", totalRemoved);
                w.WriteEndObject();

                w.WriteEndObject();
            }
            _stdout.WriteLine(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
            return;
        }

        // Plain-text summary per contracts/add-exclude-cli-contract.md
        _stdout.WriteLine("d2net-init: incremental exclusions applied.");
        _stdout.WriteLine($"  added:      {added.Count}");
        _stdout.WriteLine($"  redundant:  {redundant.Count}");
        _stdout.WriteLine($"  removed:    {totalRemoved} dart_files row(s)");

        if (added.Count > 0)
        {
            _stdout.WriteLine();
            _stdout.WriteLine("added paths:");
            foreach (var a in added) _stdout.WriteLine($"  {a}");
        }

        if (redundant.Count > 0)
        {
            _stdout.WriteLine();
            _stdout.WriteLine("redundant paths (already excluded or covered by an ancestor):");
            foreach (var (path, cls, anc) in redundant)
            {
                if (cls == PathValidator.Classification.RedundantUnderAncestor && anc is not null)
                    _stdout.WriteLine($"  {path} -- covered by ancestor \"{anc}\"");
                else
                    _stdout.WriteLine($"  {path} -- already excluded");
            }
        }

        if (added.Count > 0)
        {
            _stdout.WriteLine();
            _stdout.WriteLine("removals by exclusion:");
            foreach (var (excl, rows) in removalRows)
            {
                _stdout.WriteLine($"  {excl}: {rows} row(s)");
            }
        }
    }

    private void EmitJsonErrorIfRequested(bool json, int code, string message)
    {
        if (!json) return;
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("result", "error");
            w.WriteNumber("code", code);
            w.WriteString("message", FirstLine(message));
            w.WriteEndObject();
        }
        _stdout.WriteLine(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static string FirstLine(string s)
    {
        var idx = s.IndexOfAny(new[] { '\r', '\n' });
        return idx < 0 ? s : s[..idx];
    }

    private (int Exit, string Msg) MapBridgeStartFailure(BridgeStartException ex, int port)
    {
        // Lock-contention detection per research R5.
        if (ex.Kind == BridgeStartFailureKind.PgliteInitFailed
            || ex.Kind == BridgeStartFailureKind.OtherBridgeError
            || ex.Kind == BridgeStartFailureKind.UnexpectedExit)
        {
            var blob = (ex.Message ?? "") + " " + (ex.Bridge?.LastBridgeError ?? "");
            string[] markers = { "EBUSY", "EACCES", "data directory in use", "could not lock", "another process" };
            foreach (var m in markers)
            {
                if (blob.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return (ExitCodes.AddExcludeWorkspaceLocked,
                        "d2net-init: workspace database is locked by another process. " +
                        "Retry shortly or stop the conflicting invocation.");
                }
            }
        }

        // Fall through to existing mappings.
        return ex.Kind switch
        {
            BridgeStartFailureKind.NodeMissing =>
                (ExitCodes.NodeMissing,
                 "The PGLite bridge requires Node.js >= 20 on PATH. " +
                 "Install Node.js LTS from https://nodejs.org/ and retry."),
            BridgeStartFailureKind.BundleMissing =>
                (ExitCodes.BridgeBundleMissing,
                 "The PGLite bridge bundle is missing or corrupt. Reinstall d2net-init. " +
                 $"detail: {ex.Message}"),
            BridgeStartFailureKind.PortInUse =>
                (ExitCodes.BridgePortInUse,
                 $"PGLite bridge port {port} is already in use. " +
                 $"Either stop the conflicting process, or supply --bridge-port <n>."),
            BridgeStartFailureKind.PgliteInitFailed =>
                (ExitCodes.DbOpenFailed,
                 "PGLite bridge failed to open the workspace database: " +
                 $"BRIDGE_ERROR {ex.Message}"),
            _ => (ExitCodes.BridgeStartFailed,
                  $"PGLite bridge failed to start: {ex.Message}"),
        };
    }

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }

    /// <summary>Test seam: factory signature for injecting a fake bridge.</summary>
    public delegate IDisposable BridgeFactory(BridgeOptions opts, TextWriter stderr);

    private static readonly BridgeFactory DefaultBridgeFactory =
        (opts, stderr) => PgBridgeProcess.StartAsync(opts.Port, opts.DataDir, stderr).GetAwaiter().GetResult();
}
