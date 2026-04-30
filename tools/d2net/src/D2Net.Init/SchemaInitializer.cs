using System.IO;
using System.Reflection;
using Npgsql;

namespace D2Net.Init;

/// <summary>
/// Applies <c>contracts/db-schema.sql</c> (embedded as a resource) to a fresh
/// PGLite database via an open <c>NpgsqlConnection</c>. The connection
/// targets the per-invocation bridge subprocess listening on
/// <c>127.0.0.1:&lt;port&gt;</c>.
/// </summary>
public static class SchemaInitializer
{
    private const string ResourceName = "D2Net.Init.Schema.db-schema.sql";

    public static void Apply(NpgsqlConnection openConnection)
    {
        var sql = LoadSchemaSql();
        using var tx = openConnection.BeginTransaction();
        using (var cmd = openConnection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public static string LoadSchemaSql()
    {
        var asm = typeof(SchemaInitializer).Assembly;
        using var stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded schema resource '{ResourceName}' not found in {asm.FullName}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
