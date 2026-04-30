using System.IO;
using Microsoft.Data.Sqlite;

namespace D2Net.Init;

public sealed class InspectionRunner
{
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;
    public InspectionRunner(TextWriter stdout, TextWriter stderr) { _stdout = stdout; _stderr = stderr; }

    public int Run(InspectOptions opts)
    {
        var layout = WorkspaceLayout.Resolve(opts.RepoRoot);
        if (!Directory.Exists(layout.WorkspaceDir) || !File.Exists(layout.DbFile))
        {
            _stderr.WriteLine(
                $"no D2NET workspace found at '{layout.WorkspaceDir}'. Run d2net-init first.");
            return ExitCodes.WorkspaceMissingForInspection;
        }

        var connInfo = DbConnectionSettings.ForFile(layout.DbFile);
        try
        {
            // Mode=ReadOnly is enforced by the runtime even if connection-string omits it,
            // because all our SQL is SELECT-only in inspection paths.
            using var conn = new SqliteConnection(connInfo.ConnectionString + ";Mode=ReadOnly");
            try { conn.Open(); }
            catch (SqliteException ex)
            {
                _stderr.WriteLine(
                    $"could not open the workspace database file at '{layout.DbFile}'. " +
                    $"Original error: {ex.Message}");
                return ExitCodes.DbOpenFailed;
            }

            switch (opts.Mode)
            {
                case InspectMode.List:
                    Inspectors.ListInspector.Run(conn, opts.Json, _stdout);
                    break;
                case InspectMode.Exclusions:
                    Inspectors.ExclusionsInspector.Run(conn, opts.Json, _stdout);
                    break;
                case InspectMode.CurrentPhase:
                    Inspectors.CurrentPhaseInspector.Run(conn, opts.Json, _stdout);
                    break;
            }
            return ExitCodes.Success;
        }
        finally
        {
            SqliteConnection.ClearAllPools();
        }
    }
}
