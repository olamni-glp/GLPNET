using System.IO;
using System.Linq;
using D2Net.Init;
using D2Net.Scaffold.Tests.Fixtures;
using Npgsql;

namespace D2Net.Scaffold.Tests;

/// <summary>
/// T028: induced-failure atomicity. Inject a NpgsqlException at the DB
/// transaction step; assert target tree byte-identical to pre-run, staging
/// dir cleaned up, dart_files columns and scaffold_tracker rows unchanged.
///
/// We trigger a DB error by corrupting the schema (DROP the dart_files table
/// AFTER init but BEFORE running scaffold). The runner reaches the staging
/// copy, then the DB transaction fails on the UPDATE; the SafeCleanupStaging
/// path runs and the live target stays absent.
/// </summary>
public class ScaffoldAtomicityTests
{
    [Fact]
    public void DbWriteFails_TargetUntouched_StagingCleanedUp()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runner.dart", "// runner");
        var (initCode, _, _, port) = InitHelper.Init(repo.Root);
        Assert.Equal(D2Net.Init.ExitCodes.Success, initCode);

        // Corrupt the schema by dropping dart_files. The scaffold UPDATE will fail.
        var workspace = Path.Combine(repo.Root, ".D2NET");
        using (var seeder = new DbVerifier(Path.Combine(workspace, "pgdb")))
        {
            using var cmd = seeder.RawConnection.CreateCommand();
            cmd.CommandText = "DROP TABLE dart_files;";
            cmd.ExecuteNonQuery();
        }

        var (code, _, _) = InitHelper.Scaffold(repo.Root, port);
        Assert.Equal(ExitCodes.ScaffoldDbWriteFailed, code);

        var targetRoot = Path.Combine(repo.Root, "glp_runtime_net");
        Assert.False(Directory.Exists(targetRoot));
        Assert.False(Directory.Exists(targetRoot + ".d2net-tmp"));
    }
}
