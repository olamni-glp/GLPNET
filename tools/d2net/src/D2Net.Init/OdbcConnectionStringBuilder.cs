namespace D2Net.Init;

/// <summary>
/// Composes a Microsoft.Data.Sqlite connection string for the embedded
/// workspace database. FR-010: every field is persisted both in
/// <c>D2NET-Settings.json</c> and as a row in the <c>setting</c> table.
/// (File retains its original name to minimise churn; the type is now
/// SQLite-flavoured per the Q6 clarification.)
/// </summary>
public static class DbConnectionStringBuilder
{
    public const string EngineName = "sqlite";

    public static string Build(string absoluteDbFilePath)
        => $"Data Source={absoluteDbFilePath}";
}
