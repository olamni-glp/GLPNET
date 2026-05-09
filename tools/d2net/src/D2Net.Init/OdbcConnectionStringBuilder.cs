namespace D2Net.Init;

/// <summary>
/// Composes both Npgsql and psqlODBC connection strings for the PGLite-backed
/// workspace database. FR-009 / FR-010: every field is persisted both in
/// <c>D2NET-Settings.json</c> and as a row in the <c>setting</c> table.
/// File name persists from the SQLite era for minimal churn; the type is now
/// PGLite-flavoured.
/// </summary>
public static class DbConnectionStringBuilder
{
    public const string EngineName = "pglite";

    // FR-027: Pooling=false on Npgsql; Pooling=false + UseDeclareFetch=0 on
    // psqlODBC. Don't call NpgsqlCommand.Prepare() anywhere downstream.
    public static string BuildNpgsql(BridgeOptions o)
        => $"Host={o.Host};Port={o.Port};Database={o.Database};Username={o.User};Password={o.Password};SSL Mode=Disable;Pooling=false";

    public static string BuildOdbc(BridgeOptions o)
        => $"Driver={{PostgreSQL ODBC Driver(UNICODE)}};Server={o.Host};Port={o.Port};Database={o.Database};Uid={o.User};Pwd={o.Password};SSLmode=disable;Pooling=false;UseDeclareFetch=0;";
}
