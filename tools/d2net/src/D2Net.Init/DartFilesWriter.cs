using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace D2Net.Init;

public static class DartFilesWriter
{
    public static void WriteRows(SqliteConnection conn, IReadOnlyList<DartFileEntry> entries)
    {
        // Wrap inserts in a transaction for speed.
        using var tx = conn.BeginTransaction();
        foreach (var e in entries)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO dart_files (filename, full_path) VALUES ($f, $p);";
            cmd.Parameters.AddWithValue("$f", e.Filename);
            cmd.Parameters.AddWithValue("$p", e.FullPath);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
