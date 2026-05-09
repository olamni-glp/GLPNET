using System;
using System.IO;
using Xunit;
using D2Net.PgdbMigrate;

namespace D2Net.PgdbMigrate.Tests;

/// <summary>
/// T034 — re-run after a simulated mid-move crash. The "crash" simulation here
/// places both .D2NET/pgdb and .pgdb in a partial-rename-like state (both
/// present non-empty, with the same content). Re-running without --force enters
/// case (true,true,true) and refuses → 78. The operator inspects manually,
/// then re-runs with --force which overwrites cleanly. We verify both halves.
/// </summary>
public class CrashRecoveryTests
{
    private static string MakeTempRepo()
    {
        var d = Path.Combine(Path.GetTempPath(), "d2net-pgdbmigrate-crash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void PartialState_RefusesThenForceCompletes()
    {
        var repo = MakeTempRepo();
        try
        {
            // Mid-rename simulation: same file in both source and target.
            var src = Path.Combine(repo, ".D2NET", "pgdb");
            var tgt = Path.Combine(repo, ".pgdb");
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(tgt);
            File.WriteAllText(Path.Combine(src, "PG_VERSION"), "16");
            File.WriteAllText(Path.Combine(tgt, "PG_VERSION"), "16");

            // First run: refuses without --force (FR-008).
            var refuse = Program.Run(
                new Program.Options(repo, false, true, false, false),
                new StringWriter(), new StringWriter(), DateTime.UtcNow);
            Assert.Equal(Program.ExitConflictRefused, refuse);
            Assert.True(Directory.Exists(src) && Directory.Exists(tgt), "both untouched on refuse");

            // Operator inspects, then re-runs with --force.
            var force = Program.Run(
                new Program.Options(repo, false, true, true, false),
                new StringWriter(), new StringWriter(), DateTime.UtcNow);
            Assert.Equal(Program.ExitSuccess, force);
            Assert.False(Directory.Exists(src));
            Assert.True(File.Exists(Path.Combine(tgt, "PG_VERSION")));
        }
        finally
        {
            try { Directory.Delete(repo, recursive: true); } catch { /* test cleanup */ }
        }
    }
}
