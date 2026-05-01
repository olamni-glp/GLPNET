using System.Collections.Generic;
using Npgsql;

namespace D2Net.Scaffold;

/// <summary>
/// Single-transaction database mutator for scaffold (FR-014). Order of
/// statements is fixed by data-model.md:
/// <list type="number">
///   <item><c>ALTER TABLE dart_files ADD COLUMN IF NOT EXISTS target_parent_dir TEXT</c></item>
///   <item><c>ALTER TABLE dart_files ADD COLUMN IF NOT EXISTS target_workdir_name TEXT</c></item>
///   <item><c>CREATE TABLE IF NOT EXISTS scaffold_tracker (...)</c></item>
///   <item>If destructive override: <c>DELETE FROM scaffold_tracker</c> +
///         clear the two new <c>dart_files</c> columns.</item>
///   <item><c>DELETE FROM scaffold_tracker WHERE source_path IN (remove-set)</c></item>
///   <item><c>INSERT INTO scaffold_tracker (...) ON CONFLICT DO UPDATE</c> for every add/update row</item>
///   <item><c>UPDATE dart_files SET target_parent_dir, target_workdir_name</c> per dart file</item>
///   <item>UPSERT <c>phase_status (phase='scaffold', status='IN_PROGRESS')</c></item>
///   <item>Insert <c>phase_sequence</c> row for <c>'scaffold'</c> if missing</item>
///   <item>COMMIT</item>
/// </list>
/// </summary>
public static class ScaffoldDbWriter
{
    public sealed record DbCounts(int DartFilesUpdated, int TrackerInsertsOrUpdates, int TrackerDeletes);

    public static DbCounts ApplyScaffold(
        NpgsqlConnection conn,
        ScaffoldPlan plan,
        bool destructiveOverride)
    {
        using var tx = conn.BeginTransaction();

        // 1. Schema additions (idempotent).
        Exec(conn, tx, @"ALTER TABLE dart_files ADD COLUMN IF NOT EXISTS target_parent_dir   TEXT;");
        Exec(conn, tx, @"ALTER TABLE dart_files ADD COLUMN IF NOT EXISTS target_workdir_name TEXT;");

        // 2. scaffold_tracker table (idempotent).
        Exec(conn, tx, @"
CREATE TABLE IF NOT EXISTS scaffold_tracker (
    source_path        TEXT PRIMARY KEY,
    is_dart            BOOLEAN NOT NULL,
    target_parent_dir  TEXT NOT NULL,
    last_scaffold_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);");

        // 3. Destructive override: wipe tracker + clear dart_files columns.
        if (destructiveOverride)
        {
            Exec(conn, tx, "DELETE FROM scaffold_tracker;");
            Exec(conn, tx, "UPDATE dart_files SET target_parent_dir = NULL, target_workdir_name = NULL;");
        }

        // 4. Reconciliation: DELETE scaffold_tracker rows in remove-set,
        //    NULL out the matching dart_files columns.
        if (plan.RemovePaths.Count > 0)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM scaffold_tracker WHERE source_path = ANY(@paths);";
                var p = cmd.CreateParameter();
                p.ParameterName = "@paths";
                p.Value = plan.RemovePaths.ToArray();
                cmd.Parameters.Add(p);
                cmd.ExecuteNonQuery();
            }
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE dart_files SET target_parent_dir = NULL, target_workdir_name = NULL " +
                                  "WHERE full_path = ANY(@paths);";
                var p = cmd.CreateParameter();
                p.ParameterName = "@paths";
                p.Value = plan.RemovePaths.ToArray();
                cmd.Parameters.Add(p);
                cmd.ExecuteNonQuery();
            }
        }

        // 5. INSERT or UPDATE scaffold_tracker rows for every live source path.
        int insertsOrUpdates = 0;
        // Insert/upsert non-dart rows.
        foreach (var f in plan.NonDartFiles)
        {
            UpsertTracker(conn, tx, f.RelPath, false, f.TargetParentDirAbsNative);
            insertsOrUpdates++;
        }
        // Insert/upsert dart rows.
        foreach (var d in plan.DartFiles)
        {
            UpsertTracker(conn, tx, d.RelPath, true, d.TargetParentDirAbsNative);
            insertsOrUpdates++;
        }

        // 6. UPDATE dart_files for every dart row in the live source.
        int dartFilesUpdated = 0;
        foreach (var d in plan.DartFiles)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
UPDATE dart_files
   SET target_parent_dir   = @parent,
       target_workdir_name = @workdir
 WHERE full_path = @full_path;";
            cmd.Parameters.AddWithValue("@parent", d.TargetParentDirAbsNative);
            cmd.Parameters.AddWithValue("@workdir", d.WorkdirName);
            cmd.Parameters.AddWithValue("@full_path", d.RelPath);
            var n = cmd.ExecuteNonQuery();
            if (n == 0)
            {
                // Row missing (drift case — file appeared since last init / *-exclude).
                using var ins = conn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = @"
INSERT INTO dart_files (filename, full_path, target_parent_dir, target_workdir_name)
VALUES (@filename, @full_path, @parent, @workdir);";
                ins.Parameters.AddWithValue("@filename", System.IO.Path.GetFileName(d.AbsSourcePath));
                ins.Parameters.AddWithValue("@full_path", d.RelPath);
                ins.Parameters.AddWithValue("@parent", d.TargetParentDirAbsNative);
                ins.Parameters.AddWithValue("@workdir", d.WorkdirName);
                ins.ExecuteNonQuery();
            }
            dartFilesUpdated++;
        }

        // 7. Phase tracking — scaffold owns ONLY the row whose phase = 'scaffold'.
        Exec(conn, tx, @"
INSERT INTO phase_status (phase, status, last_updated)
VALUES ('scaffold', 'IN_PROGRESS', now())
ON CONFLICT (phase) DO UPDATE
   SET status       = EXCLUDED.status,
       last_updated = EXCLUDED.last_updated;");

        Exec(conn, tx, @"
INSERT INTO phase_sequence (phase, sequence)
SELECT 'scaffold', COALESCE((SELECT MAX(sequence) FROM phase_sequence), 0) + 1
WHERE NOT EXISTS (SELECT 1 FROM phase_sequence WHERE phase = 'scaffold');");

        tx.Commit();

        return new DbCounts(
            DartFilesUpdated: dartFilesUpdated,
            TrackerInsertsOrUpdates: insertsOrUpdates,
            TrackerDeletes: plan.RemovePaths.Count);
    }

    /// <summary>
    /// Mark the <c>scaffold</c> phase row as COMPLETED. Best-effort, in its
    /// own transaction so a partial failure here does not roll back the
    /// main scaffold work.
    /// </summary>
    public static void MarkPhaseCompleted(NpgsqlConnection conn)
    {
        using var tx = conn.BeginTransaction();
        Exec(conn, tx, @"
UPDATE phase_status SET status='COMPLETED', last_updated=now()
 WHERE phase='scaffold';");
        tx.Commit();
    }

    private static void UpsertTracker(NpgsqlConnection conn, NpgsqlTransaction tx,
        string sourcePath, bool isDart, string targetParentDir)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
INSERT INTO scaffold_tracker (source_path, is_dart, target_parent_dir, last_scaffold_at)
VALUES (@sp, @id, @tp, now())
ON CONFLICT (source_path) DO UPDATE
   SET is_dart           = EXCLUDED.is_dart,
       target_parent_dir = EXCLUDED.target_parent_dir,
       last_scaffold_at  = EXCLUDED.last_scaffold_at;";
        cmd.Parameters.AddWithValue("@sp", sourcePath);
        cmd.Parameters.AddWithValue("@id", isDart);
        cmd.Parameters.AddWithValue("@tp", targetParentDir);
        cmd.ExecuteNonQuery();
    }

    private static void Exec(NpgsqlConnection conn, NpgsqlTransaction tx, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
