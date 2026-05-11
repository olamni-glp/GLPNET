using System;
using System.IO;
using Xunit;
using D2Net.PgdbMigrate;

namespace D2Net.PgdbMigrate.Tests;

/// <summary>T032 — second invocation after a successful migration is a no-op (FR-009).</summary>
public class IdempotentTests
{
    private static string MakeTempRepo()
    {
        var d = Path.Combine(Path.GetTempPath(), "d2net-pgdbmigrate-idem-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void SecondInvocation_IsNoOp()
    {
        var repo = MakeTempRepo();
        try
        {
            // Seed and run once.
            var src = Path.Combine(repo, ".D2NET", "pgdb");
            Directory.CreateDirectory(src);
            File.WriteAllText(Path.Combine(src, "x.dat"), "x");

            var opts = new Program.Options(repo, DryRun: false, NoBackup: true, Force: false, Json: false);
            using var sw1 = new StringWriter();
            using var se1 = new StringWriter();
            var first = Program.Run(opts, sw1, se1, DateTime.UtcNow);
            Assert.Equal(Program.ExitSuccess, first);
            Assert.True(Directory.Exists(Path.Combine(repo, ".pgdb")));

            // Re-run; source is now gone (moved). Expect no-op exit 0 with the
            // contracted message.
            using var sw2 = new StringWriter();
            using var se2 = new StringWriter();
            var second = Program.Run(opts, sw2, se2, DateTime.UtcNow);
            Assert.Equal(Program.ExitSuccess, second);
            Assert.Contains("no-op", sw2.ToString(), StringComparison.OrdinalIgnoreCase);
            // Target unchanged.
            Assert.True(File.Exists(Path.Combine(repo, ".pgdb", "x.dat")));
        }
        finally
        {
            try { Directory.Delete(repo, recursive: true); } catch { /* test cleanup */ }
        }
    }
}
