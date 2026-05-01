using System.Collections.Generic;
using System.Data;
using Npgsql;

namespace D2Net.Init;

/// <summary>
/// Single-transaction mutator for the incremental --remove-exclude mode
/// (feature 008). Inverse of <see cref="ExclusionMutator"/>:
///   - DELETE the supplied paths from <c>excluded_directories</c>.
///   - INSERT the supplied dart-file rows into <c>dart_files</c> using
///     <c>ON CONFLICT (full_path) DO NOTHING</c>.
/// </summary>
public static class ExclusionRemover
{
    public sealed record MutationResult(int DeletedRows, int InsertedRows);

    public static MutationResult ApplyRemoveExclude(
        NpgsqlConnection conn,
        IReadOnlyList<string> acceptedPathsToDelete,
        IReadOnlyList<DartFileEntry> rowsToInsert)
    {
        using var tx = conn.BeginTransaction(IsolationLevel.Serializable);

        int deleted = 0;
        if (acceptedPathsToDelete.Count > 0)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                "DELETE FROM excluded_directories WHERE path = ANY(@paths);";
            var pathsParam = cmd.Parameters.Add("@paths", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text);
            pathsParam.Value = acceptedPathsToDelete is string[] arr ? arr : System.Linq.Enumerable.ToArray(acceptedPathsToDelete);
            deleted = cmd.ExecuteNonQuery();
        }

        int inserted = 0;
        foreach (var entry in rowsToInsert)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                "INSERT INTO dart_files (filename, full_path) " +
                "VALUES (@f, @p) ON CONFLICT (full_path) DO NOTHING;";
            cmd.Parameters.AddWithValue("@f", entry.Filename);
            cmd.Parameters.AddWithValue("@p", entry.FullPath);
            inserted += cmd.ExecuteNonQuery();
        }

        tx.Commit();
        return new MutationResult(deleted, inserted);
    }
}
