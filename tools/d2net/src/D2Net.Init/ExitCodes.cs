namespace D2Net.Init;

public static class ExitCodes
{
    public const int Success = 0;
    public const int ArgumentError = 1;
    public const int WrongCwd = 2;
    public const int WorkspaceAlreadyExists = 3;
    public const int SourceDirMissing = 4;
    public const int BridgePortInUse = 5;          // retained for backward compatibility; unused after SQLite pivot
    public const int WorkspaceMissingForInspection = 6;
    public const int BridgeStartFailed = 7;        // retained for backward compatibility; unused after SQLite pivot
    public const int DbOpenFailed = 8;
    public const int InteractivePromptCancelled = 9;
}
