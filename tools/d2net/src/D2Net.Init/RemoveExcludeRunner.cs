using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Npgsql;

namespace D2Net.Init;

/// <summary>
/// Orchestrator for the incremental --remove-exclude mode (feature 008).
///
/// Pipeline (research R3 + R4):
///   1. Resolve workspace layout; verify .D2NET/ exists.
///   2. Read settings snapshot to obtain source_dir + current exclusions.
///   3. Validate every supplied path: canonicalise, source-relative,
///      file-vs-directory, intra-batch dedupe.
///   4. Spawn the bridge (lock-contention -> exit 20).
///   5. Pre-transaction SELECT path, kind FROM excluded_directories WHERE
///      path = ANY(@supplied) -> classify each path as
///      not-currently-excluded / manual-removable / system-kind.
///   6. If any system-kind AND --allow-system-exclusions absent:
///      exit 21 with stderr listing every offender.
///   7. Compute post-removal exclusion list; classify ancestor-survival.
///   8. DartFileScanner.Scan(post-removal exclusions) -> filter to rows
///      under non-ancestor-covered removed paths.
///   9. Prepare temp settings JSON.
///  10. ExclusionRemover.ApplyRemoveExclude (transactional DELETE + INSERT).
///  11. Atomic settings rename.
///  12. Emit success summary; exit 0.
/// </summary>
public sealed class RemoveExcludeRunner
{
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;

    public RemoveExcludeRunner(TextWriter stdout, TextWriter stderr)
    {
        _stdout = stdout;
        _stderr = stderr;
    }

    public int Run(RemoveExcludeOptions opts) => Run(opts, bridgeFactory: null);

    /// <summary>
    /// Test seam mirroring <see cref="AddExcludeRunner"/>'s pattern.
    /// </summary>
    public int RunForTesting(RemoveExcludeOptions opts, BridgeFactory bridgeFactory)
        => Run(opts, bridgeFactory);

    private int Run(RemoveExcludeOptions opts, BridgeFactory? bridgeFactory)
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
                rejections.Add($"--remove-exclude path '{raw}': {ex.Message}");
                if (rejectExitCode == ExitCodes.Success) rejectExitCode = ExitCodes.ArgumentError;
                continue;
            }

            var rel = PathValidator.ResolveUnderSource(canonical, sourceRootAbs);
            if (rel is null)
            {
                rejections.Add(
                    $"--remove-exclude path \"{raw}\" resolves outside source root \"{sourceDir}\".");
                rejectExitCode = ExitCodes.RemoveExcludePathOutsideSource;
                continue;
            }

            if (string.Equals(rel, WorkspaceLayout.WorkspaceFolderName, StringComparison.OrdinalIgnoreCase))
            {
                rejections.Add(
                    $"--remove-exclude path \"{raw}\" names the D2NET workspace folder.");
                rejectExitCode = ExitCodes.RemoveExcludePathOutsideSource;
                continue;
            }

            if (PathValidator.LooksLikeFilePath(canonical, sourceRootAbs, rel))
            {
                rejections.Add(
                    $"--remove-exclude path \"{raw}\" is a file. Exclusions are directory-only.");
                if (rejectExitCode == ExitCodes.Success
                    || rejectExitCode == ExitCodes.AddExcludePathIsFile)
                {
                    rejectExitCode = ExitCodes.AddExcludePathIsFile;
                }
                continue;
            }

            if (rel == ".")
            {
                rejections.Add(
                    $"--remove-exclude path \"{raw}\" is the source root itself; refusing.");
                rejectExitCode = ExitCodes.RemoveExcludePathOutsideSource;
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
            var msg = "d2net-init: --remove-exclude requires at least one path.";
            _stderr.WriteLine(msg);
            EmitJsonErrorIfRequested(opts.Json, ExitCodes.ArgumentError, msg);
            return ExitCodes.ArgumentError;
        }

        // Intra-batch dedupe: keep first occurrence, sort ascending.
        var supplied = sourceRelativePaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // ---- Step 4: bridge ----
        var persistedPort = SettingsWriter.TryReadPersistedPort(layout.SettingsFile);
        var port = opts.BridgePortOverride ?? persistedPort ?? BridgeOptions.DefaultPort;
        var bridgeOpts = BridgeOptions.ForDataDir(Path.GetFullPath(layout.PgDir), port);

        try
        {
            using var bridge = (bridgeFactory ?? DefaultBridgeFactory)(bridgeOpts, _stderr);

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

            // ---- Step 5: kind-aware preflight (research R3) ----
            var classified = ClassifyAgainstDatabase(conn, supplied);

            // ---- Step 6: system-kind refusal ----
            var systemKindOffenders = classified
                .Where(c => c.Cls == ClassKind.SystemKind)
                .ToList();
            if (systemKindOffenders.Count > 0 && !opts.AllowSystemExclusions)
            {
                foreach (var c in systemKindOffenders)
                {
                    _stderr.WriteLine(
                        $"d2net-init: --remove-exclude path \"{c.Path}\" has kind='{c.Kind}' which is protected. " +
                        $"Re-issue with --allow-system-exclusions to override.");
                }
                EmitJsonSystemKindRefusedIfRequested(opts.Json, systemKindOffenders);
                conn.Close();
                NpgsqlConnection.ClearAllPools();
                return ExitCodes.RemoveExcludeSystemKindRefused;
            }

            // ---- Step 7: compute post-removal list + ancestor-survival ----
            var toRemove = classified
                .Where(c => c.Cls == ClassKind.ManualRemovable
                            || (c.Cls == ClassKind.SystemKind && opts.AllowSystemExclusions))
                .ToList();

            var toRemovePathSet = new HashSet<string>(
                toRemove.Select(c => c.Path),
                StringComparer.OrdinalIgnoreCase);

            var postRemovalExclusions = existingExclusions
                .Where(e => !toRemovePathSet.Contains(e))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // For each removed path, find a surviving ancestor (if any).
            var ancestorBySurvivor = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in toRemove)
            {
                string? survivingAncestor = null;
                foreach (var e in postRemovalExclusions)
                {
                    if (PathValidator.IsUnder(c.Path, e))
                    {
                        if (survivingAncestor is null || e.Length > survivingAncestor.Length)
                            survivingAncestor = e;
                    }
                }
                ancestorBySurvivor[c.Path] = survivingAncestor;
            }

            var toRemoveNotAncestorCovered = toRemove
                .Where(c => ancestorBySurvivor[c.Path] is null)
                .Select(c => c.Path)
                .ToList();

            // ---- Step 8: file walk + filter ----
            var allDartFiles = DartFileScanner.Scan(
                layout.RepoRoot, sourceDir, postRemovalExclusions);

            // Filter to rows whose full_path lies under one of the
            // not-ancestor-covered removed paths. Without this filter we'd
            // re-INSERT rows for every .dart file in the source tree
            // (the scanner returns the full post-removal universe).
            var prefixes = toRemoveNotAncestorCovered
                .Select(p => sourceDir + "/" + p + "/")
                .ToList();
            var exactPaths = toRemoveNotAncestorCovered
                .Select(p => sourceDir + "/" + p)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var rowsToInsert = new List<DartFileEntry>();
            var rowsByExclusion = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in toRemoveNotAncestorCovered) rowsByExclusion[p] = 0;

            foreach (var entry in allDartFiles)
            {
                string? matchedExclusion = null;
                foreach (var rp in toRemoveNotAncestorCovered)
                {
                    var prefix = sourceDir + "/" + rp + "/";
                    if (entry.FullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        if (matchedExclusion is null || rp.Length > matchedExclusion.Length)
                            matchedExclusion = rp;
                    }
                }
                if (matchedExclusion is not null)
                {
                    rowsToInsert.Add(entry);
                    rowsByExclusion[matchedExclusion] = rowsByExclusion[matchedExclusion] + 1;
                }
            }

            // ---- Step 9: temp settings JSON ----
            string? tempPath = null;
            try
            {
                tempPath = SettingsWriter.PrepareTempSettingsWithExclusions(
                    layout.SettingsFile, postRemovalExclusions);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                var msg = $"d2net-init: failed to prepare temp settings file: {ex.Message}";
                _stderr.WriteLine(msg);
                EmitJsonErrorIfRequested(opts.Json, ExitCodes.RemoveExcludeSettingsWriteFailed, msg);
                conn.Close();
                NpgsqlConnection.ClearAllPools();
                return ExitCodes.RemoveExcludeSettingsWriteFailed;
            }

            // ---- Step 10: transaction ----
            ExclusionRemover.MutationResult mutation;
            try
            {
                mutation = ExclusionRemover.ApplyRemoveExclude(
                    conn,
                    toRemove.Select(c => c.Path).ToList(),
                    rowsToInsert);
            }
            catch (Exception ex) when (ex is NpgsqlException || ex is PostgresException || ex is InvalidOperationException)
            {
                TryDelete(tempPath);
                var msg = $"d2net-init: workspace database update failed: {ex.Message}. No changes applied.";
                _stderr.WriteLine(msg);
                EmitJsonErrorIfRequested(opts.Json, ExitCodes.RemoveExcludeDbWriteFailed, msg);
                conn.Close();
                NpgsqlConnection.ClearAllPools();
                return ExitCodes.RemoveExcludeDbWriteFailed;
            }

            conn.Close();
            NpgsqlConnection.ClearAllPools();

            // ---- Step 11: rename ----
            // Only commit the temp file if the on-disk JSON would actually change.
            try
            {
                if (toRemove.Count > 0)
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
                EmitJsonErrorIfRequested(opts.Json, ExitCodes.RemoveExcludeSettingsWriteFailed, msg);
                return ExitCodes.RemoveExcludeSettingsWriteFailed;
            }

            // ---- Step 12: success summary ----
            EmitSuccessSummary(opts.Json, classified, toRemove, ancestorBySurvivor,
                toRemoveNotAncestorCovered, rowsByExclusion, mutation);
            return ExitCodes.Success;
        }
        catch (BridgeStartException ex)
        {
            var (mappedExit, msg) = MapBridgeStartFailure(ex, port);
            _stderr.WriteLine(msg);
            EmitJsonErrorIfRequested(opts.Json, mappedExit, msg);
            return mappedExit;
        }
    }

    // ---------- helpers ----------

    private enum ClassKind { NotPresent, ManualRemovable, SystemKind }
    private sealed record Classification(string Path, ClassKind Cls, string? Kind);

    private static List<Classification> ClassifyAgainstDatabase(
        NpgsqlConnection conn, IReadOnlyList<string> supplied)
    {
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (supplied.Count > 0)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT path, kind FROM excluded_directories WHERE path = ANY(@paths);";
            var p = cmd.Parameters.Add("@paths",
                NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text);
            p.Value = supplied.ToArray();
            using var r = cmd.ExecuteReader();
            while (r.Read()) found[r.GetString(0)] = r.GetString(1);
        }

        var result = new List<Classification>(supplied.Count);
        foreach (var path in supplied)
        {
            if (!found.TryGetValue(path, out var kind))
                result.Add(new Classification(path, ClassKind.NotPresent, null));
            else if (string.Equals(kind, "manual", StringComparison.OrdinalIgnoreCase))
                result.Add(new Classification(path, ClassKind.ManualRemovable, kind));
            else
                result.Add(new Classification(path, ClassKind.SystemKind, kind));
        }
        return result;
    }

    private void EmitSuccessSummary(
        bool json,
        IReadOnlyList<Classification> classified,
        IReadOnlyList<Classification> toRemove,
        IReadOnlyDictionary<string, string?> ancestorBySurvivor,
        IReadOnlyList<string> toRemoveNotAncestorCovered,
        IReadOnlyDictionary<string, int> rowsByExclusion,
        ExclusionRemover.MutationResult mutation)
    {
        var notPresent = classified
            .Where(c => c.Cls == ClassKind.NotPresent)
            .Select(c => c.Path)
            .ToList();
        var coveredByAncestor = toRemove
            .Where(c => ancestorBySurvivor[c.Path] is not null)
            .Select(c => (Path: c.Path, Ancestor: ancestorBySurvivor[c.Path]!))
            .ToList();

        int totalInserted = mutation.InsertedRows;

        if (json)
        {
            using var stream = new MemoryStream();
            using (var w = new Utf8JsonWriter(stream))
            {
                w.WriteStartObject();
                w.WriteString("result", "applied");

                w.WritePropertyName("removed");
                w.WriteStartArray();
                foreach (var c in toRemove)
                {
                    w.WriteStartObject();
                    w.WriteString("path", c.Path);
                    w.WriteString("kind", c.Kind ?? "unknown");
                    w.WriteEndObject();
                }
                w.WriteEndArray();

                w.WritePropertyName("not_present");
                w.WriteStartArray();
                foreach (var p in notPresent) w.WriteStringValue(p);
                w.WriteEndArray();

                w.WritePropertyName("covered_by_ancestor");
                w.WriteStartArray();
                foreach (var (p, anc) in coveredByAncestor)
                {
                    w.WriteStartObject();
                    w.WriteString("path", p);
                    w.WriteString("ancestor", anc);
                    w.WriteEndObject();
                }
                w.WriteEndArray();

                w.WritePropertyName("inserted_rows");
                w.WriteStartArray();
                foreach (var p in toRemoveNotAncestorCovered)
                {
                    w.WriteStartObject();
                    w.WriteString("exclusion", p);
                    w.WriteNumber("rows", rowsByExclusion.TryGetValue(p, out var n) ? n : 0);
                    w.WriteEndObject();
                }
                w.WriteEndArray();

                w.WritePropertyName("totals");
                w.WriteStartObject();
                w.WriteNumber("removed", toRemove.Count);
                w.WriteNumber("not_present", notPresent.Count);
                w.WriteNumber("covered_by_ancestor", coveredByAncestor.Count);
                w.WriteNumber("inserted_rows", totalInserted);
                w.WriteEndObject();

                w.WriteEndObject();
            }
            _stdout.WriteLine(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
            return;
        }

        // Plain text summary.
        _stdout.WriteLine("d2net-init: incremental exclusions removed.");
        _stdout.WriteLine($"  removed:                {toRemove.Count}");
        _stdout.WriteLine($"  not-currently-excluded: {notPresent.Count}");
        _stdout.WriteLine($"  covered-by-ancestor:    {coveredByAncestor.Count}");
        _stdout.WriteLine($"  inserted:               {totalInserted} dart_files row(s)");

        if (toRemove.Count > 0)
        {
            _stdout.WriteLine();
            _stdout.WriteLine("removed paths:");
            foreach (var c in toRemove)
                _stdout.WriteLine($"  {c.Path} (kind: {c.Kind ?? "unknown"})");
        }

        if (notPresent.Count > 0)
        {
            _stdout.WriteLine();
            _stdout.WriteLine("not-currently-excluded paths:");
            foreach (var p in notPresent) _stdout.WriteLine($"  {p}");
        }

        if (coveredByAncestor.Count > 0)
        {
            _stdout.WriteLine();
            _stdout.WriteLine("covered-by-ancestor paths:");
            foreach (var (p, anc) in coveredByAncestor)
                _stdout.WriteLine($"  {p} -- covered by ancestor \"{anc}\"");
        }

        if (toRemoveNotAncestorCovered.Count > 0)
        {
            _stdout.WriteLine();
            _stdout.WriteLine("inserts by removed exclusion:");
            foreach (var p in toRemoveNotAncestorCovered)
            {
                var rows = rowsByExclusion.TryGetValue(p, out var n) ? n : 0;
                _stdout.WriteLine($"  {p}: {rows} row(s)");
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

    private void EmitJsonSystemKindRefusedIfRequested(bool json, IReadOnlyList<Classification> offenders)
    {
        if (!json) return;
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("result", "error");
            w.WriteNumber("code", ExitCodes.RemoveExcludeSystemKindRefused);
            w.WriteString("message", "system-exclusion-refused");
            w.WritePropertyName("offenders");
            w.WriteStartArray();
            foreach (var c in offenders)
            {
                w.WriteStartObject();
                w.WriteString("path", c.Path);
                w.WriteString("kind", c.Kind ?? "unknown");
                w.WriteEndObject();
            }
            w.WriteEndArray();
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
                    return (ExitCodes.RemoveExcludeWorkspaceLocked,
                        "d2net-init: workspace database is locked by another process. " +
                        "Retry shortly or stop the conflicting invocation.");
                }
            }
        }

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

    /// <summary>Test seam: factory signature for injecting a fake bridge. Identical to <see cref="AddExcludeRunner.BridgeFactory"/>.</summary>
    public delegate IDisposable BridgeFactory(BridgeOptions opts, TextWriter stderr);

    private static readonly BridgeFactory DefaultBridgeFactory =
        (opts, stderr) => PgBridgeProcess.StartAsync(opts.Port, opts.DataDir, stderr).GetAwaiter().GetResult();
}
