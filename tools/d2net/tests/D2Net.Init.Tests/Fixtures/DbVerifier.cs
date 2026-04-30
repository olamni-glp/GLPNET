using System.Collections.Generic;
using System.IO;
using D2Net.Init;
using Npgsql;

namespace D2Net.Init.Tests.Fixtures;

/// <summary>
/// Read-only PGLite verification helper for tests. Spawns its own
/// <see cref="PgBridgeProcess"/> against the test's pgdb data directory
/// on a free port, opens an Npgsql connection, and exposes assertion
/// helpers that mirror the shipped 002 SQLite-era DbVerifier API.
///
/// Type name preserved for the migrating tests; underlying engine is now PGLite.
/// </summary>
public sealed class DbVerifier : IDisposable
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

    public string? GetSetting(string key)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM setting WHERE key = @k;";
        cmd.Parameters.AddWithValue("@k", key);
        var v = cmd.ExecuteScalar();
        return v as string;
    }

    public IReadOnlyList<(string Path, string Kind)> GetExclusions()
    {
        var rows = new List<(string, string)>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT path, kind FROM excluded_directories ORDER BY path;";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) rows.Add((rdr.GetString(0), rdr.GetString(1)));
        return rows;
    }

    public IReadOnlyList<(long Id, string Filename, string FullPath)> GetDartFiles()
    {
        var rows = new List<(long, string, string)>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, filename, full_path FROM dart_files ORDER BY full_path;";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) rows.Add((rdr.GetInt64(0), rdr.GetString(1), rdr.GetString(2)));
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
