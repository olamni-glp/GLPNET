using System;
using System.IO;
using Xunit;
using D2Net.PgdbMigrate;

namespace D2Net.PgdbMigrate.Tests;

/// <summary>T033 — both source and target present non-empty without --force → exit 78 (FR-008).</summary>
public class RefuseOnConflictTests
{
    private static string MakeTempRepo()
    {
        var d = Path.Combine(Path.GetTempPath(), "d2net-pgdbmigrate-refuse-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void BothPresent_NoForce_Exits78()
    {
        var repo = MakeTempRepo();
        try
        {
            var src = Path.Combine(repo, ".D2NET", "pgdb");
            var tgt = Path.Combine(repo, ".pgdb");
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(tgt);
            File.WriteAllText(Path.Combine(src, "src.dat"), "src");
            File.WriteAllText(Path.Combine(tgt, "tgt.dat"), "tgt");

            var opts = new Program.Options(repo, DryRun: false, NoBackup: false, Force: false, Json: false);
            using var sw = new StringWriter();
            using var se = new StringWriter();
            var exit = Program.Run(opts, sw, se, DateTime.UtcNow);

            Assert.Equal(Program.ExitConflictRefused, exit);
            Assert.Contains("REFUSED", se.ToString(), StringComparison.OrdinalIgnoreCase);
            // Both directories untouched.
            Assert.True(File.Exists(Path.Combine(src, "src.dat")));
            Assert.True(File.Exists(Path.Combine(tgt, "tgt.dat")));
        }
        finally
        {
            try { Directory.Delete(repo, recursive: true); } catch { /* test cleanup */ }
        }
    }

    [Fact]
    public void BothPresent_WithForce_BackupsTargetThenMoves()
    {
        var repo = MakeTempRepo();
        try
        {
            var src = Path.Combine(repo, ".D2NET", "pgdb");
            var tgt = Path.Combine(repo, ".pgdb");
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(tgt);
            File.WriteAllText(Path.Combine(src, "src.dat"), "src");
            File.WriteAllText(Path.Combine(tgt, "tgt.dat"), "tgt");

            var opts = new Program.Options(repo, DryRun: false, NoBackup: false, Force: true, Json: false);
            using var sw = new StringWriter();
            using var se = new StringWriter();
            var now = new DateTime(2026, 5, 9, 0, 0, 0, DateTimeKind.Utc);
            var exit = Program.Run(opts, sw, se, now);

            Assert.Equal(Program.ExitSuccess, exit);
            Assert.False(Directory.Exists(src), "source moved away");
            Assert.True(File.Exists(Path.Combine(tgt, "src.dat")), "target now contains source content");
            Assert.True(Directory.Exists(Path.Combine(repo, ".D2NET", "pgdb-target.bak.20260509T000000Z")),
                "target backup should exist (pre-overwrite snapshot)");
        }
        finally
        {
            try { Directory.Delete(repo, recursive: true); } catch { /* test cleanup */ }
        }
    }
}
