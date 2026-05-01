using System.Collections.Generic;
using System.IO;
using D2Net.Init;
using Npgsql;

namespace D2Net.Scaffold.Tests.Fixtures;

/// <summary>
/// Read-only PGLite verification helper for scaffold tests. Spawns its own
/// bridge subprocess against the test's pgdb on a free port, opens an
/// Npgsql connection, exposes assertion helpers.
/// </summary>
public sealed class DbVerifier : System.IDisposable
{
    private readonly PgBridgeProcess _bridge;
    private readonly NpgsqlConnection _conn;

    public DbVerifier(string dataDir)
    {
        var port = PortPicker.NextFreePort();
        _bridge = PgBridgeProcess.StartAsync(port, dataDir, TextWriter.Null).GetAwaiter().GetResult();
        var bridgeOpts = BridgeOptions.ForDataDir(dataDir, port);
        _conn = new NpgsqlConnection(DbConnectionStringBuilder.BuildNpgsql(bridgeOpts));
        _conn.Open();
    }

    public IReadOnlyList<string> GetTableNames()
    {
        var names = new List<string>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename;";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) names.Add(rdr.GetString(0));
        return names;
    }

    public int CountRows(string table)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table};";
        return System.Convert.ToInt32(cmd.ExecuteScalar());
    }

    public IReadOnlyList<(string FullPath, string? TargetParentDir, string? TargetWorkdirName)> GetDartFilesWithScaffoldColumns()
    {
        var rows = new List<(string, string?, string?)>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"SELECT full_path, target_parent_dir, target_workdir_name
                              FROM dart_files
                          ORDER BY full_path;";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            rows.Add((
                rdr.GetString(0),
                rdr.IsDBNull(1) ? null : rdr.GetString(1),
                rdr.IsDBNull(2) ? null : rdr.GetString(2)));
        }
        return rows;
    }

    public IReadOnlyList<(string SourcePath, bool IsDart, string TargetParentDir)> GetTrackerRows()
    {
        var rows = new List<(string, bool, string)>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT source_path, is_dart, target_parent_dir FROM scaffold_tracker ORDER BY source_path;";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) rows.Add((rdr.GetString(0), rdr.GetBoolean(1), rdr.GetString(2)));
        return rows;
    }

    public IReadOnlyList<(string Phase, string Status)> GetPhaseStatus()
    {
        var rows = new List<(string, string)>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT phase, status FROM phase_status ORDER BY phase;";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) rows.Add((rdr.GetString(0), rdr.GetString(1)));
        return rows;
    }

    public IReadOnlyList<(string Phase, int Sequence)> GetPhaseSequence()
    {
        var rows = new List<(string, int)>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT phase, sequence FROM phase_sequence ORDER BY phase;";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) rows.Add((rdr.GetString(0), rdr.GetInt32(1)));
        return rows;
    }

    public NpgsqlConnection RawConnection => _conn;

    public void Dispose()
    {
        try { _conn.Dispose(); } catch { }
        NpgsqlConnection.ClearAllPools();
        try { _bridge.Dispose(); } catch { }
    }
}
