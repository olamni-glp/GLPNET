using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;

namespace D2Net.Init;

/// <summary>
/// PGLite-flavoured connection settings persisted both in
/// <c>D2NET-Settings.json</c> and as <c>db_*</c> rows in the workspace
/// <c>setting</c> table. FR-009 / FR-010 / Q5 clarification: includes
/// both an Npgsql connection string and an ODBC connection string.
/// </summary>
public sealed record DbConnectionSettings(
    string Engine,
    string Host,
    int Port,
    string Database,
    string User,
    string Password,
    string DataDir,
    string ConnectionString,
    string ConnectionStringOdbc)
{
    public static DbConnectionSettings ForBridge(BridgeOptions o)
        => new(
            Engine: DbConnectionStringBuilder.EngineName,
            Host: o.Host,
            Port: o.Port,
            Database: o.Database,
            User: o.User,
            Password: o.Password,
            DataDir: o.DataDir,
            ConnectionString: DbConnectionStringBuilder.BuildNpgsql(o),
            ConnectionStringOdbc: DbConnectionStringBuilder.BuildOdbc(o));
}

internal sealed class SettingsJsonConnection
{
    [JsonPropertyName("engine")]                  public string Engine { get; set; } = "";
    [JsonPropertyName("host")]                    public string Host { get; set; } = "";
    [JsonPropertyName("port")]                    public int Port { get; set; }
    [JsonPropertyName("database")]                public string Database { get; set; } = "";
    [JsonPropertyName("user")]                    public string User { get; set; } = "";
    [JsonPropertyName("password")]                public string Password { get; set; } = "";
    [JsonPropertyName("data_dir")]                public string DataDir { get; set; } = "";
    [JsonPropertyName("connection_string")]       public string ConnectionString { get; set; } = "";
    [JsonPropertyName("connection_string_odbc")]  public string ConnectionStringOdbc { get; set; } = "";
}

internal sealed class SettingsJsonRoot
{
    [JsonPropertyName("schema_version")]       public int SchemaVersion { get; set; } = 1;
    [JsonPropertyName("source_dir")]           public string SourceDir { get; set; } = "";
    [JsonPropertyName("target_extension")]     public string TargetExtension { get; set; } = "";
    [JsonPropertyName("target_dir")]           public string TargetDir { get; set; } = "";
    [JsonPropertyName("excluded_directories")] public List<string> ExcludedDirectories { get; set; } = new();
    [JsonPropertyName("connection")]           public SettingsJsonConnection Connection { get; set; } = new();
    [JsonPropertyName("created_at")]           public string CreatedAt { get; set; } = "";
}

/// <summary>FR-009 + FR-010: writes <c>D2NET-Settings.json</c> and the matching
/// rows in the <c>setting</c> table. Both must agree on the connection fields.</summary>
public static class SettingsWriter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void WriteSettingsFile(
        string settingsFilePath,
        string sourceDir,
        string targetExtension,
        string targetDir,
        IReadOnlyList<string> excludedDirectoriesAscending,
        DbConnectionSettings connection,
        DateTimeOffset createdAt)
    {
        var root = new SettingsJsonRoot
        {
            SchemaVersion = 1,
            SourceDir = sourceDir,
            TargetExtension = targetExtension,
            TargetDir = targetDir,
            ExcludedDirectories = excludedDirectoriesAscending.ToList(),
            Connection = new SettingsJsonConnection
            {
                Engine = connection.Engine,
                Host = connection.Host,
                Port = connection.Port,
                Database = connection.Database,
                User = connection.User,
                Password = connection.Password,
                DataDir = connection.DataDir,
                ConnectionString = connection.ConnectionString,
                ConnectionStringOdbc = connection.ConnectionStringOdbc,
            },
            CreatedAt = OutputFormat.FormatTimestamp(createdAt),
        };
        var json = JsonSerializer.Serialize(root, JsonOpts);
        File.WriteAllText(settingsFilePath, json);
    }

    public static void WriteSettingRows(
        NpgsqlConnection conn,
        string sourceDir,
        string targetExtension,
        string targetDir,
        DbConnectionSettings connection)
    {
        var rows = new (string K, string V)[]
        {
            ("source_dir",                sourceDir),
            ("target_extension",          targetExtension),
            ("target_dir",                targetDir),
            ("db_engine",                 connection.Engine),
            ("db_host",                   connection.Host),
            ("db_port",                   connection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("db_database",               connection.Database),
            ("db_user",                   connection.User),
            ("db_password",               connection.Password),
            ("db_data_dir",               connection.DataDir),
            ("db_connection_string",      connection.ConnectionString),
            ("db_connection_string_odbc", connection.ConnectionStringOdbc),
        };
        foreach (var (k, v) in rows)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO setting (key, value) VALUES (@k, @v);";
            cmd.Parameters.AddWithValue("@k", k);
            cmd.Parameters.AddWithValue("@v", v);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// FR-014 / Q3 helper: read a previously-persisted <c>connection.port</c>
    /// from <c>D2NET-Settings.json</c>. Used by inspection commands when no
    /// explicit <c>--bridge-port</c> override is supplied.
    /// </summary>
    public static int? TryReadPersistedPort(string settingsFilePath)
    {
        if (!File.Exists(settingsFilePath)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(settingsFilePath));
            if (doc.RootElement.TryGetProperty("connection", out var conn)
                && conn.TryGetProperty("port", out var portEl)
                && portEl.TryGetInt32(out var port))
            {
                return port;
            }
        }
        catch (JsonException) { /* fall through */ }
        return null;
    }
}
