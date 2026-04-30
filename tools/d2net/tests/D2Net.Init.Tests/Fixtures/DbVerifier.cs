using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace D2Net.Init.Tests.Fixtures;

/// <summary>
/// Read-only SQLite verification helper for tests. Opens the workspace DB
/// directly and runs assertion queries.
/// </summary>
public sealed class DbVerifier : IDisposable
{
    private readonly SqliteConnection _conn;

    public DbVerifier(string dbFile)
    {
        _conn = new SqliteConnection($"Data Source={dbFile};Mode=ReadOnly");
        _conn.Open();
    }

    public IReadOnlyList<string> GetTableNames()
    {
        var names = new List<string>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) names.Add(rdr.GetString(0));
        return names;
    }

    public int CountRows(string table)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public string? GetSetting(string key)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM setting WHERE key = $k;";
        cmd.Parameters.AddWithValue("$k", key);
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

    public void Dispose()
    {
        _conn.Dispose();
        SqliteConnection.ClearAllPools();
    }
}
