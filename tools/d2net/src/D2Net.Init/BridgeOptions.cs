namespace D2Net.Init;

/// <summary>
/// Resolved bridge configuration for a single D2NET command invocation.
/// FR-009 / FR-010: every field is persisted both in
/// <c>D2NET-Settings.json</c>'s <c>connection</c> block and as a row in the
/// <c>setting</c> table.
/// </summary>
public sealed record BridgeOptions(
    string Host,
    int Port,
    string Database,
    string User,
    string Password,
    string DataDir)
{
    public const string DefaultHost = "127.0.0.1";
    public const int DefaultPort = 54400;
    public const string DefaultDatabase = "d2net";
    public const string DefaultUser = "d2net";
    public const string DefaultPassword = "d2net";

    public static BridgeOptions ForDataDir(string absoluteDataDir, int port = DefaultPort)
        => new(DefaultHost, port, DefaultDatabase, DefaultUser, DefaultPassword, absoluteDataDir);
}
