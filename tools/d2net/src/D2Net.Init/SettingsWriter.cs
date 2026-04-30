using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace D2Net.Init;

public sealed record DbConnectionSettings(string Engine, string DbFile, string ConnectionString)
{
    public static DbConnectionSettings ForFile(string absoluteDbFilePath)
        => new(DbConnectionStringBuilder.EngineName,
               absoluteDbFilePath,
               DbConnectionStringBuilder.Build(absoluteDbFilePath));
}

internal sealed class SettingsJsonConnection
{
    [JsonPropertyName("engine")] public string Engine { get; set; } = "";
    [JsonPropertyName("db_file")] public string DbFile { get; set; } = "";
    [JsonPropertyName("connection_string")] public string ConnectionString { get; set; } = "";
}

internal sealed class SettingsJsonRoot
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; set; } = 1;
    [JsonPropertyName("source_dir")] public string SourceDir { get; set; } = "";
    [JsonPropertyName("target_extension")] public string TargetExtension { get; set; } = "";
    [JsonPropertyName("target_dir")] public string TargetDir { get; set; } = "";
    [JsonPropertyName("excluded_directories")] public List<string> ExcludedDirectories { get; set; } = new();
    [JsonPropertyName("connection")] public SettingsJsonConnection Connection { get; set; } = new();
    [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
}

/// <summary>FR-009 + FR-010: writes <c>D2NET-Settings.json</c> and the matching
/// rows in <c>setting</c>. Both must agree on the connection fields.</summary>
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
                DbFile = connection.DbFile,
                ConnectionString = connection.ConnectionString,
            },
            CreatedAt = OutputFormat.FormatTimestamp(createdAt),
        };
        var json = JsonSerializer.Serialize(root, JsonOpts);
        File.WriteAllText(settingsFilePath, json);
    }

    public static void WriteSettingRows(
        SqliteConnection conn,
        string sourceDir,
        string targetExtension,
        string targetDir,
        DbConnectionSettings connection)
    {
        var rows = new (string K, string V)[]
        {
            ("source_dir", sourceDir),
            ("target_extension", targetExtension),
            ("target_dir", targetDir),
            ("db_engine", connection.Engine),
            ("db_file", connection.DbFile),
            ("db_connection_string", connection.ConnectionString),
        };
        foreach (var (k, v) in rows)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO setting (key, value) VALUES ($k, $v);";
            cmd.Parameters.AddWithValue("$k", k);
            cmd.Parameters.AddWithValue("$v", v);
            cmd.ExecuteNonQuery();
        }
    }
}
