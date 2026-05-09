using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace D2Net.PgdbMigrate;

/// <summary>
/// One-shot migration: <c>.D2NET/pgdb/</c> → <c>.pgdb/</c>. Idempotent.
/// Contract: <c>specs/012-codeconv-runner/contracts/d2net_pgdb_migration_cli.md</c>.
/// </summary>
public static class Program
{
    public const int ExitSuccess = 0;
    public const int ExitGeneric = 1;
    public const int ExitUsage = 64;
    public const int ExitBackupVerify = 73;
    public const int ExitConflictRefused = 78;

    public const string ToolVersion = "0.1.0";

    public sealed record Options(
        string RepoRoot,
        bool DryRun,
        bool NoBackup,
        bool Force,
        bool Json);

    public static int Main(string[] args)
    {
        var (opts, error) = ParseArgs(args);
        if (error is not null)
        {
            Console.Error.WriteLine(error);
            return ExitUsage;
        }
        return Run(opts!, Console.Out, Console.Error, DateTime.UtcNow);
    }

    public static int Run(Options opts, TextWriter stdout, TextWriter stderr, DateTime now)
    {
        var src = Path.Combine(opts.RepoRoot, ".D2NET", "pgdb");
        var tgt = Path.Combine(opts.RepoRoot, ".pgdb");

        var srcExists = Directory.Exists(src);
        var tgtExists = Directory.Exists(tgt);
        var tgtNonEmpty = tgtExists && Directory.EnumerateFileSystemEntries(tgt).Any();

        // Case (false, *, *) — source absent → no-op (FR-009 idempotence).
        if (!srcExists)
        {
            EmitNoOp(opts, stdout);
            return ExitSuccess;
        }

        // Case (true, true, true) AND NOT --force → REFUSED.
        if (tgtExists && tgtNonEmpty && !opts.Force)
        {
            stderr.WriteLine("d2net-pgdb-migrate: REFUSED. Both .D2NET/pgdb and .pgdb exist with content. Resolve manually.");
            stderr.WriteLine($"  source {src}: {DirEntryCount(src)} entries, {DirByteSize(src)} bytes");
            stderr.WriteLine($"  target {tgt}: {DirEntryCount(tgt)} entries, {DirByteSize(tgt)} bytes");
            stderr.WriteLine("  Re-run with --force after taking your own backup if you intend to overwrite the target.");
            return ExitConflictRefused;
        }

        // Plan: optionally backup target (force only), backup source, move source → target.
        var stamp = now.ToString("yyyyMMddTHHmmssZ", System.Globalization.CultureInfo.InvariantCulture);
        var srcBackup = Path.Combine(opts.RepoRoot, ".D2NET", $"pgdb.bak.{stamp}");
        var tgtBackup = (tgtExists && tgtNonEmpty) ? Path.Combine(opts.RepoRoot, ".D2NET", $"pgdb-target.bak.{stamp}") : null;

        EmitPlan(opts, src, tgt, srcBackup, tgtBackup, stdout);

        if (opts.DryRun) return ExitSuccess;

        try
        {
            // 1. Backup the target first (if it exists non-empty and we're forcing).
            if (tgtExists && tgtNonEmpty && !opts.NoBackup)
            {
                if (tgtBackup is null) throw new InvalidOperationException("internal: tgtBackup null with non-empty target");
                CopyDirectoryRecursive(tgt, tgtBackup);
                stdout.WriteLine($"  → backed up target to {tgtBackup}");
            }

            // 2. Backup the source.
            if (!opts.NoBackup)
            {
                CopyDirectoryRecursive(src, srcBackup);
                if (CountFiles(src) != CountFiles(srcBackup))
                {
                    stderr.WriteLine("d2net-pgdb-migrate: BACKUP VERIFY FAILED — file count mismatch.");
                    return ExitBackupVerify;
                }
                stdout.WriteLine($"  → backed up source to {srcBackup}");
            }
            else
            {
                stderr.WriteLine("WARNING: --no-backup specified. Source will be moved without preserving a copy.");
            }

            // 3. If target exists (force mode), remove it before move.
            if (tgtExists)
            {
                Directory.Delete(tgt, recursive: true);
            }

            // 4. Move source → target. Try atomic rename; fall back to copy+delete on cross-volume.
            try
            {
                Directory.Move(src, tgt);
            }
            catch (IOException)
            {
                CopyDirectoryRecursive(src, tgt);
                Directory.Delete(src, recursive: true);
            }

            stdout.WriteLine($"  → moved {src} → {tgt}");

            // 5. Migration record. data-model.md § 3.
            var record = new
            {
                from = src,
                to = tgt,
                backup_at = opts.NoBackup ? null : srcBackup,
                at = now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture),
                tool_version = ToolVersion,
            };
            var recPath = Path.Combine(tgt, ".migration-record.json");
            File.WriteAllText(recPath, JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            stdout.WriteLine($"  → wrote {recPath}");

            stdout.WriteLine("d2net-pgdb-migrate: SUCCESS");
            return ExitSuccess;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            stderr.WriteLine($"d2net-pgdb-migrate: filesystem error: {ex.Message}");
            return ExitGeneric;
        }
    }

    private static void EmitNoOp(Options opts, TextWriter stdout)
    {
        if (opts.Json)
        {
            stdout.Write(JsonSerializer.Serialize(new { result = "no-op", reason = ".D2NET/pgdb absent" }));
            stdout.WriteLine();
        }
        else
        {
            stdout.WriteLine("d2net-pgdb-migrate: no-op — .D2NET/pgdb absent (already migrated or never present).");
        }
    }

    private static void EmitPlan(Options opts, string src, string tgt, string srcBackup, string? tgtBackup, TextWriter stdout)
    {
        if (opts.Json) return; // dry-run JSON output isn't a contract requirement.
        stdout.WriteLine("d2net-pgdb-migrate: planning…");
        stdout.WriteLine($"  source {src}: present ({DirByteSize(src)} bytes, {CountFiles(src)} files)");
        stdout.WriteLine($"  target {tgt}: {(Directory.Exists(tgt) ? "present" : "absent")}");
        if (tgtBackup is not null)
            stdout.WriteLine($"  plan: backup target → backup source → move (--force)");
        else if (!opts.NoBackup)
            stdout.WriteLine($"  plan: backup source → move");
        else
            stdout.WriteLine($"  plan: move (--no-backup)");
    }

    public static (Options? opts, string? error) ParseArgs(string[] args)
    {
        string repoRoot = Directory.GetCurrentDirectory();
        bool dryRun = false, noBackup = false, force = false, json = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--repo-root":
                    if (++i >= args.Length) return (null, "--repo-root requires a value");
                    repoRoot = Path.GetFullPath(args[i]);
                    break;
                case "--dry-run":
                    dryRun = true; break;
                case "--no-backup":
                    noBackup = true; break;
                case "--force":
                    force = true; break;
                case "--json":
                    json = true; break;
                case "--help":
                case "-h":
                    return (null, "usage: d2net-pgdb-migrate [--repo-root <p>] [--dry-run] [--no-backup] [--force] [--json]");
                default:
                    return (null, $"unknown flag: {args[i]}");
            }
        }
        return (new Options(repoRoot, dryRun, noBackup, force, json), null);
    }

    public static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectoryRecursive(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }
    }

    public static int CountFiles(string path)
    {
        if (!Directory.Exists(path)) return 0;
        return Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length;
    }

    public static long DirByteSize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        long total = 0;
        foreach (var f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            try { total += new FileInfo(f).Length; } catch { /* unreadable file — skip */ }
        }
        return total;
    }

    public static int DirEntryCount(string path)
    {
        if (!Directory.Exists(path)) return 0;
        return Directory.GetFileSystemEntries(path).Length;
    }
}
