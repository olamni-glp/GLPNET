using System.Collections.Generic;
using System.Data;
using Npgsql;

namespace D2Net.Init;

/// <summary>
/// Single-transaction mutator for the incremental --add-exclude mode
/// (feature 007). Inserts the supplied paths as <c>kind = 'manual'</c>
/// rows in <c>excluded_directories</c> and removes every <c>dart_files</c>
/// row whose <c>full_path</c> falls under any inserted path.
/// </summary>
public static class ExclusionMutator
{
    public sealed record MutationResult(
        int InsertedRows,
        IReadOnlyDictionary<string, int> RemovedRowsByExclusion);

    /// <summary>
    /// Apply the add-exclude mutation. Throws <see cref="NpgsqlException"/>
    /// (or any other Npgsql failure) on database error; the caller maps to
    /// <see cref="ExitCodes.AddExcludeDbWriteFailed"/>.
    ///
    /// <paramref name="newPathsAscending"/> MUST be canonical source-relative
    /// forward-slash strings, deduplicated and intra-batch-classified;
    /// callers must NOT pass redundant entries.
    /// </summary>
    public static MutationResult ApplyAddExclude(
        NpgsqlConnection conn,
        string sourceDir,
        IReadOnlyList<string> newPathsAscending)
    {
        using var tx = conn.BeginTransaction(IsolationLevel.Serializable);

        // Insert excluded_directories rows. ON CONFLICT DO NOTHING makes
        // this idempotent against any drift between settings.json and the
        // workspace database (research R4).
        int inserted = 0;
        foreach (var p in newPathsAscending)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                "INSERT INTO excluded_directories (path, kind) " +
                "VALUES (@p, 'manual') ON CONFLICT (path) DO NOTHING;";
            cmd.Parameters.AddWithValue("@p", p);
            inserted += cmd.ExecuteNonQuery();
        }

        // Boundary-aware prefix-delete from dart_files. Capture per-path
        // RowsAffected for the run summary.
        var removed = new Dictionary<string, int>(System.StringComparer.Ordinal);
        foreach (var p in newPathsAscending)
        {
            int rows;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText =
                    "DELETE FROM dart_files " +
                    "WHERE full_path = @full " +
                    "   OR full_path LIKE @prefix;";
                var fullEqual = sourceDir + "/" + p;
                var prefix = sourceDir + "/" + p + "/%";
                cmd.Parameters.AddWithValue("@full", fullEqual);
                cmd.Parameters.AddWithValue("@prefix", prefix);
                rows = cmd.ExecuteNonQuery();
            }
            removed[p] = rows;
        }

        tx.Commit();
        return new MutationResult(inserted, removed);
    }
}
