using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Npgsql;

namespace D2Net.Init;

public sealed class InitRunner
{
    private readonly InteractivePrompter _prompter;
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;

    public InitRunner(InteractivePrompter prompter, TextWriter stdout, TextWriter stderr)
    {
        _prompter = prompter;
        _stdout = stdout;
        _stderr = stderr;
    }

    public int Run(InitOptions opts)
    {
        // 1. Validate CWD (FR-002 of 002).
        if (!WorkspaceLayout.LooksLikeRepoRoot(opts.RepoRoot, opts.SourceDir))
        {
            _stderr.WriteLine(
                "current directory does not look like a D2NET repository root " +
                $"(no .git/, no .D2NET/, and no '{opts.SourceDir}' subdirectory at '{opts.RepoRoot}').");
            return ExitCodes.WrongCwd;
        }

        var layout = WorkspaceLayout.Resolve(opts.RepoRoot);

        // 2. Decide create / force-delete / refuse.
        // FR-014: also catch SQLite-era workspaces (workspace.sqlite under pgdb/, or settings JSON
        // recording connection.engine != "pglite").
        var workspaceExists = Directory.Exists(layout.WorkspaceDir)
                              || WorkspaceLayout.LooksLikeSqliteEra(opts.RepoRoot);
        if (workspaceExists && !opts.ForceDeleteRequested)
        {
            _stderr.WriteLine(
                $"workspace already exists at {layout.WorkspaceDir}; " +
                $"use --FORCE --DELETE-EXISTING to recreate it.");
            return ExitCodes.WorkspaceAlreadyExists;
        }

        // 3. Fill missing inputs.
        try
        {
            opts = _prompter.FillMissingInputs(opts);
        }
        catch (PromptCancelledException ex)
        {
            _stderr.WriteLine(ex.Message);
            return ExitCodes.InteractivePromptCancelled;
        }
        catch (ArgumentException ex)
        {
            _stderr.WriteLine(ex.Message);
            return ExitCodes.ArgumentError;
        }

        // 4. Validate source dir exists as direct subdir of repo root.
        var sourceAbs = Path.Combine(opts.RepoRoot, opts.SourceDir!);
        if (!Directory.Exists(sourceAbs))
        {
            _stderr.WriteLine(
                $"source directory '{opts.SourceDir}' does not exist at '{opts.RepoRoot}'.");
            return ExitCodes.SourceDirMissing;
        }

        // 5. Detect proposed exclusions.
        var proposed = ExclusionDetector.Detect(sourceAbs, opts.ManualExclusions);

        // 6. Approve list.
        IReadOnlyList<ProposedExclusion> approved;
        try
        {
            approved = _prompter.ApproveExclusions(proposed, opts.AcceptSuggestedExclusions);
        }
        catch (PromptCancelledException ex)
        {
            _stderr.WriteLine(ex.Message);
            return ExitCodes.InteractivePromptCancelled;
        }

        // 7. Scan dart files.
        var dartFiles = DartFileScanner.Scan(opts.RepoRoot, opts.SourceDir!, approved.Select(e => e.Path));

        // 8. Build the workspace in a temp staging folder, then atomic-rename into place.
        var tempWorkspace = Path.Combine(opts.RepoRoot, $".D2NET.tmp.{Guid.NewGuid():N}");
        var renamedAside = (string?)null;
        var createdAt = DateTimeOffset.UtcNow;
        try
        {
            Directory.CreateDirectory(tempWorkspace);
            var tempLayout = layout.AsTemp(tempWorkspace);
            Directory.CreateDirectory(tempLayout.PgDir);

            // The PGLite data tree lives under tempLayout.PgDir during the build phase.
            // Persisted connection details describe the *post-rename* absolute path so external
            // clients see the right path after the rename completes.
            var tempBridgeOpts = BridgeOptions.ForDataDir(Path.GetFullPath(tempLayout.PgDir), opts.BridgePort);
            var finalBridgeOptsForSettings = BridgeOptions.ForDataDir(Path.GetFullPath(layout.PgDir), opts.BridgePort);
            var finalConnection = DbConnectionSettings.ForBridge(finalBridgeOptsForSettings);

            // Spawn the bridge against the temp pgdir.
            PgBridgeProcess bridge;
            try
            {
                bridge = PgBridgeProcess.StartAsync(tempBridgeOpts.Port, tempBridgeOpts.DataDir, _stderr).GetAwaiter().GetResult();
            }
            catch (BridgeStartException ex)
            {
                EmitBridgeStartFailure(ex, opts.BridgePort);
                return MapBridgeStartExitCode(ex.Kind);
            }

            using (bridge)
            {
                var npgsqlString = DbConnectionStringBuilder.BuildNpgsql(tempBridgeOpts);
                using var conn = new NpgsqlConnection(npgsqlString);
                try { conn.Open(); }
                catch (NpgsqlException ex)
                {
                    _stderr.WriteLine(
                        $"could not open the workspace database via the PGLite bridge at " +
                        $"{tempBridgeOpts.Host}:{tempBridgeOpts.Port}. Original error: {ex.Message}");
                    return ExitCodes.DbOpenFailed;
                }

                SchemaInitializer.Apply(conn);

                SettingsWriter.WriteSettingRows(conn,
                    opts.SourceDir!, opts.TargetExtension ?? "", opts.TargetDir!, finalConnection);
                ExclusionsWriter.WriteRows(conn, approved);
                DartFilesWriter.WriteRows(conn, dartFiles);

                // Close the SQL connection cleanly so the bridge sees a graceful client disconnect
                // before its own stdin-driven shutdown.
                conn.Close();
                NpgsqlConnection.ClearAllPools();
            }

            var sortedExcludedPaths = approved.Select(e => e.Path)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
            SettingsWriter.WriteSettingsFile(
                tempLayout.SettingsFile,
                opts.SourceDir!, opts.TargetExtension ?? "", opts.TargetDir!,
                sortedExcludedPaths,
                finalConnection,
                createdAt);

            // 9. Atomic move into place. If a previous .D2NET exists, rename it aside first.
            if (Directory.Exists(layout.WorkspaceDir))
            {
                renamedAside = Path.Combine(opts.RepoRoot, $".D2NET.deleting.{Guid.NewGuid():N}");
                Directory.Move(layout.WorkspaceDir, renamedAside);
            }
            Directory.Move(tempWorkspace, layout.WorkspaceDir);

            // Success - discard the renamed-aside copy.
            if (renamedAside is not null && Directory.Exists(renamedAside))
                TryDelete(renamedAside);

            // 10. Stdout summary.
            new RunSummary
            {
                WorkspaceDir = layout.WorkspaceDir,
                SettingsFile = layout.SettingsFile,
                PgDir = layout.PgDir,
                SourceDir = opts.SourceDir!,
                TargetExtension = opts.TargetExtension ?? "",
                TargetDir = opts.TargetDir!,
                ApprovedExclusions = approved,
                DartFileCount = dartFiles.Count,
                CreatedAt = createdAt,
                BridgePort = opts.BridgePort,
            }.WriteTo(_stdout);

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            _stderr.WriteLine($"init failed: {ex.Message}");
            return ExitCodes.ArgumentError;
        }
        finally
        {
            // Clean up temp staging if it survived (i.e. we did not move it into place).
            if (Directory.Exists(tempWorkspace)) TryDelete(tempWorkspace);
            // Restore the renamed-aside workspace iff the new one is not in place.
            if (renamedAside is not null && Directory.Exists(renamedAside))
            {
                if (!Directory.Exists(layout.WorkspaceDir))
                {
                    try { Directory.Move(renamedAside, layout.WorkspaceDir); }
                    catch { /* leave the .deleting folder behind for inspection */ }
                }
                else
                {
                    TryDelete(renamedAside);
                }
            }
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

    private static void TryDelete(string path)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try { Directory.Delete(path, recursive: true); return; }
            catch { Thread.Sleep(100); }
        }
    }
}
