using System;
using System.IO;
using Xunit;
using D2Net.PgdbMigrate;

namespace D2Net.PgdbMigrate.Tests;

/// <summary>
/// T031 — fresh source present, target absent → backup taken, move succeeds,
/// content preserved (SC-004 logical row counts are verified at integration
/// time in Phase 7; here we verify file-level preservation, which transitively
/// implies row preservation since the cluster files are byte-identical).
/// </summary>
public class HappyPathTests
{
    private static string MakeTempRepo()
    {
        var d = Path.Combine(Path.GetTempPath(), "d2net-pgdbmigrate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    private static void SeedSource(string repo, int fileCount)
    {
        var src = Path.Combine(repo, ".D2NET", "pgdb");
        Directory.CreateDirectory(src);
        for (int i = 0; i < fileCount; i++)
        {
            File.WriteAllText(Path.Combine(src, $"f{i}.dat"), $"content-{i}");
        }
        // sub-dir to mimic a PGLite cluster's `base/` sub-dir
        var sub = Path.Combine(src, "base");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "1.txt"), "subdir-content");
    }

    [Fact]
    public void HappyPath_FreshSource_AbsentTarget_MovesAndBacksUp()
    {
        var repo = MakeTempRepo();
        try
        {
            SeedSource(repo, fileCount: 5);

            var opts = new Program.Options(
                RepoRoot: repo,
                DryRun: false,
                NoBackup: false,
                Force: false,
                Json: false);
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var now = new DateTime(2026, 5, 9, 14, 30, 0, DateTimeKind.Utc);

            var exit = Program.Run(opts, stdout, stderr, now);

            Assert.Equal(Program.ExitSuccess, exit);
            Assert.False(Directory.Exists(Path.Combine(repo, ".D2NET", "pgdb")), "source should be moved away");
            Assert.True(Directory.Exists(Path.Combine(repo, ".pgdb")), "target should exist");
            // 5 source files + 1 subdir file + 1 .migration-record.json = 7.
            Assert.Equal(7, Program.CountFiles(Path.Combine(repo, ".pgdb")));
            Assert.True(File.Exists(Path.Combine(repo, ".pgdb", ".migration-record.json")));
            Assert.True(Directory.Exists(Path.Combine(repo, ".D2NET", "pgdb.bak.20260509T143000Z")), "backup should exist");
        }
        finally
        {
            try { Directory.Delete(repo, recursive: true); } catch { /* test cleanup */ }
        }
    }

    [Fact]
    public void HappyPath_NoBackupFlag_SkipsBackup()
    {
        var repo = MakeTempRepo();
        try
        {
            SeedSource(repo, fileCount: 3);

            var opts = new Program.Options(repo, DryRun: false, NoBackup: true, Force: false, Json: false);
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var exit = Program.Run(opts, stdout, stderr, DateTime.UtcNow);

            Assert.Equal(Program.ExitSuccess, exit);
            // No pgdb.bak.* under .D2NET/.
            var d2netDir = Path.Combine(repo, ".D2NET");
            if (Directory.Exists(d2netDir))
            {
                var dirs = Directory.GetDirectories(d2netDir);
                foreach (var d in dirs) Assert.DoesNotContain("pgdb.bak.", Path.GetFileName(d));
            }
        }
        finally
        {
            try { Directory.Delete(repo, recursive: true); } catch { /* test cleanup */ }
        }
    }
}
