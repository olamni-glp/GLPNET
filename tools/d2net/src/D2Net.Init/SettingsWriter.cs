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

    /// <summary>
    /// Feature 007: read the current <c>source_dir</c> and the current
    /// <c>excluded_directories</c> array from <c>D2NET-Settings.json</c>.
    /// Returns null when the file is missing or unparseable.
    /// </summary>
    public static SettingsSnapshot? TryReadSnapshot(string settingsFilePath)
    {
        if (!File.Exists(settingsFilePath)) return null;
        try
        {
            var raw = File.ReadAllText(settingsFilePath);
            var root = JsonSerializer.Deserialize<SettingsJsonRoot>(raw);
            if (root is null) return null;
            return new SettingsSnapshot(
                SourceDir: root.SourceDir,
                ExcludedDirectories: root.ExcludedDirectories);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Feature 007: rewrite <c>D2NET-Settings.json</c> in place, replacing
    /// only the <c>excluded_directories</c> field while preserving every
    /// other field (including <c>connection.*</c>, <c>created_at</c>,
    /// <c>schema_version</c>). Writes to a sibling
    /// <c>D2NET-Settings.json.tmp</c> file with fsync; the caller then
    /// invokes <see cref="CommitTempFile"/> after the database transaction
    /// commits to perform the atomic rename. Returns the absolute temp
    /// file path so the caller can clean up on failure.
    /// </summary>
    /// <exception cref="IOException">If the existing settings file cannot be read or the temp file cannot be written.</exception>
    public static string PrepareTempSettingsWithExclusions(
        string settingsFilePath,
        IReadOnlyList<string> newExcludedDirectoriesAscending)
    {
        var raw = File.ReadAllText(settingsFilePath);
        var root = JsonSerializer.Deserialize<SettingsJsonRoot>(raw)
                   ?? throw new IOException(
                       $"D2NET-Settings.json at '{settingsFilePath}' could not be deserialised.");
        root.ExcludedDirectories = newExcludedDirectoriesAscending.ToList();
        var json = JsonSerializer.Serialize(root, JsonOpts);

        var tempPath = settingsFilePath + ".tmp";
        // Best-effort fsync — write, flush, close.
        using (var fs = new FileStream(
            tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 4096, FileOptions.WriteThrough))
        using (var sw = new StreamWriter(fs))
        {
            sw.Write(json);
            sw.Flush();
            fs.Flush(flushToDisk: true);
        }
        return tempPath;
    }

    /// <summary>
    /// Feature 007: atomically replace <paramref name="settingsFilePath"/>
    /// with <paramref name="tempPath"/>. On Windows uses
    /// <see cref="File.Replace(string,string,string?)"/> which is the
    /// closest-to-atomic replacement available; on POSIX uses
    /// <see cref="File.Move(string,string,bool)"/> which surfaces as
    /// <c>rename(2)</c>.
    /// </summary>
    /// <exception cref="IOException">If the rename fails.</exception>
    public static void CommitTempFile(string tempPath, string settingsFilePath)
    {
        // File.Replace requires the destination to exist; if it doesn't, fall
        // back to a plain Move(overwrite: true). The destination should always
        // exist for the add-exclude path since the workspace is mandatory.
        if (File.Exists(settingsFilePath))
        {
            File.Replace(tempPath, settingsFilePath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, settingsFilePath, overwrite: true);
        }
    }
}

/// <summary>Feature 007 helper: minimal projection of the settings file.</summary>
public sealed record SettingsSnapshot(
    string SourceDir,
    IReadOnlyList<string> ExcludedDirectories);
