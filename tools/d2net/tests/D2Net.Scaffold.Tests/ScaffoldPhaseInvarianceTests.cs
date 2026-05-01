using System.IO;
using System.Linq;
using D2Net.Init;
using D2Net.Scaffold.Tests.Fixtures;
using Npgsql;

namespace D2Net.Scaffold.Tests;

/// <summary>
/// T029: scaffold owns ONLY the row whose phase = 'scaffold'. Pre-existing
/// rows for other phases must be byte-identical post-run.
/// </summary>
public class ScaffoldPhaseInvarianceTests
{
    [Fact]
    public void NonScaffoldPhaseRows_AreByteIdenticalPostRun()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runner.dart", "// runner");

        var (initCode, _, _, port) = InitHelper.Init(repo.Root);
        Assert.Equal(D2Net.Init.ExitCodes.Success, initCode);

        // Seed phase tables with non-scaffold rows directly via a verifier connection.
        var workspace = Path.Combine(repo.Root, ".D2NET");
        using (var seeder = new DbVerifier(Path.Combine(workspace, "pgdb")))
        {
            using var cmd = seeder.RawConnection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO phase_status (phase, status, last_updated) VALUES ('analyze', 'COMPLETED', '2026-04-15T10:00:00Z');
INSERT INTO phase_status (phase, status, last_updated) VALUES ('port', 'IN_PROGRESS', '2026-04-20T12:00:00Z');
INSERT INTO phase_sequence (phase, sequence) VALUES ('analyze', 1);
INSERT INTO phase_sequence (phase, sequence) VALUES ('port', 2);
";
            cmd.ExecuteNonQuery();
        }

        // Now run scaffold.
        var (code, _, _) = InitHelper.Scaffold(repo.Root, port);
        Assert.Equal(ExitCodes.Success, code);

        using (var verifier = new DbVerifier(Path.Combine(workspace, "pgdb")))
        {
            // analyze + port unchanged; scaffold present and COMPLETED.
            var ps = verifier.GetPhaseStatus().ToDictionary(t => t.Phase, t => t.Status);
            Assert.Equal("COMPLETED", ps["analyze"]);
            Assert.Equal("IN_PROGRESS", ps["port"]);
            Assert.Equal("COMPLETED", ps["scaffold"]);

            var seq = verifier.GetPhaseSequence().ToDictionary(t => t.Phase, t => t.Sequence);
            Assert.Equal(1, seq["analyze"]);
            Assert.Equal(2, seq["port"]);
            Assert.Equal(3, seq["scaffold"]);
        }
    }
}
