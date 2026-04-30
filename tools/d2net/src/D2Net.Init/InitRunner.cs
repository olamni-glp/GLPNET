using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

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
        // 1. Validate CWD
        if (!WorkspaceLayout.LooksLikeRepoRoot(opts.RepoRoot, opts.SourceDir))
        {
            _stderr.WriteLine(
                "current directory does not look like a D2NET repository root " +
                $"(no .git/, no .D2NET/, and no '{opts.SourceDir}' subdirectory at '{opts.RepoRoot}').");
            return ExitCodes.WrongCwd;
        }

        var layout = WorkspaceLayout.Resolve(opts.RepoRoot);

        // 2. Decide create / force-delete / refuse
        var workspaceExists = Directory.Exists(layout.WorkspaceDir);
        if (workspaceExists && !opts.ForceDeleteRequested)
        {
            _stderr.WriteLine(
                $"workspace already exists at {layout.WorkspaceDir}; " +
                $"use --FORCE --DELETE-EXISTING to recreate it.");
            return ExitCodes.WorkspaceAlreadyExists;
        }

        // 3. Fill missing inputs
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

        // 4. Validate source dir exists as direct subdir of repo root
        var sourceAbs = Path.Combine(opts.RepoRoot, opts.SourceDir!);
        if (!Directory.Exists(sourceAbs))
        {
            _stderr.WriteLine(
                $"source directory '{opts.SourceDir}' does not exist at '{opts.RepoRoot}'.");
            return ExitCodes.SourceDirMissing;
        }

        // 5. Detect proposed exclusions
        var proposed = ExclusionDetector.Detect(sourceAbs, opts.ManualExclusions);

        // 6. Approve list
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

        // 7. Scan dart files
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

            // Build the DB inside the temp folder, then move the whole folder into place.
            // The connection details persisted to the DB and JSON describe the *final*
            // post-move file path so external clients see the right path.
            var tempConnection = DbConnectionSettings.ForFile(tempLayout.DbFile);
            using (var conn = new SqliteConnection(tempConnection.ConnectionString))
            {
                try { conn.Open(); }
                catch (SqliteException ex)
                {
                    _stderr.WriteLine(
                        $"could not open the workspace database file at '{tempLayout.DbFile}'. " +
                        $"Original error: {ex.Message}");
                    return ExitCodes.DbOpenFailed;
                }

                SchemaInitializer.Apply(conn);

                // Persist final-form connection details (as if the rename had already happened)
                // so settings reflect the post-move db_file path.
                var finalConnection = DbConnectionSettings.ForFile(layout.DbFile);
                SettingsWriter.WriteSettingRows(conn,
                    opts.SourceDir!, opts.TargetExtension ?? "", opts.TargetDir!, finalConnection);
                ExclusionsWriter.WriteRows(conn, approved);
                DartFilesWriter.WriteRows(conn, dartFiles);
            }
            // Force release of any pooled SQLite handles before the directory move.
            SqliteConnection.ClearAllPools();

            var sortedExcludedPaths = approved.Select(e => e.Path)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
            SettingsWriter.WriteSettingsFile(
                tempLayout.SettingsFile,
                opts.SourceDir!, opts.TargetExtension ?? "", opts.TargetDir!,
                sortedExcludedPaths,
                DbConnectionSettings.ForFile(layout.DbFile),
                createdAt);

            // 9. Atomic move into place. If a previous .D2NET exists, rename it aside first.
            if (workspaceExists)
            {
                renamedAside = Path.Combine(opts.RepoRoot, $".D2NET.deleting.{Guid.NewGuid():N}");
                Directory.Move(layout.WorkspaceDir, renamedAside);
            }
            Directory.Move(tempWorkspace, layout.WorkspaceDir);

            // Success — discard the renamed-aside copy.
            if (renamedAside is not null && Directory.Exists(renamedAside))
                TryDelete(renamedAside);

            // 10. Stdout summary
            new RunSummary
            {
                WorkspaceDir = layout.WorkspaceDir,
                SettingsFile = layout.SettingsFile,
                PgDir = layout.PgDir,
                DbFile = layout.DbFile,
                SourceDir = opts.SourceDir!,
                TargetExtension = opts.TargetExtension ?? "",
                TargetDir = opts.TargetDir!,
                ApprovedExclusions = approved,
                DartFileCount = dartFiles.Count,
                CreatedAt = createdAt,
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

    private static void TryDelete(string path)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try { Directory.Delete(path, recursive: true); return; }
            catch { Thread.Sleep(100); }
        }
    }
}
