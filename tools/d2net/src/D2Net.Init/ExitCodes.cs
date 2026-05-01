namespace D2Net.Init;

public static class ExitCodes
{
    public const int Success = 0;
    public const int ArgumentError = 1;
    public const int WrongCwd = 2;
    public const int WorkspaceAlreadyExists = 3;
    public const int SourceDirMissing = 4;
    public const int BridgePortInUse = 5;

    /// <summary>
    /// No `.D2NET/` workspace at the current working directory.
    /// Applies to inspection commands and to the incremental
    /// add-exclude mode (feature 007) alike.
    /// </summary>
    public const int WorkspaceMissingForInspection = 6;

    public const int BridgeStartFailed = 7;
    public const int DbOpenFailed = 8;
    public const int InteractivePromptCancelled = 9;
    public const int NodeMissing = 10;
    public const int BridgeBundleMissing = 11;

    // ---- Feature 007: incremental --add-exclude error catalogue ----

    /// <summary>One or more --add-exclude paths resolves outside the configured source root.</summary>
    public const int AddExcludePathOutsideSource = 12;

    /// <summary>
    /// The incremental add-exclude database transaction committed, but the
    /// settings JSON rename failed. The workspace database is updated;
    /// re-run with the same arguments to resync the settings file.
    /// </summary>
    public const int AddExcludeSettingsWriteFailed = 13;

    /// <summary>The incremental add-exclude database transaction failed (insert, delete, or commit).</summary>
    public const int AddExcludeDbWriteFailed = 14;

    /// <summary>
    /// The PGLite data directory is locked by another d2net-init process
    /// at bridge startup time. Fail-fast per spec 007 clarification 2026-05-01.
    /// </summary>
    public const int AddExcludeWorkspaceLocked = 15;

    /// <summary>One or more --add-exclude paths refers to a regular file (or has a known file suffix).</summary>
    public const int AddExcludePathIsFile = 16;

    // ---- Feature 008: incremental --remove-exclude error catalogue ----

    /// <summary>One or more --remove-exclude paths resolves outside the configured source root.</summary>
    public const int RemoveExcludePathOutsideSource = 17;

    /// <summary>
    /// The incremental remove-exclude database transaction committed, but the
    /// settings JSON rename failed. The workspace database is updated;
    /// re-run with the same arguments to resync the settings file.
    /// </summary>
    public const int RemoveExcludeSettingsWriteFailed = 18;

    /// <summary>The incremental remove-exclude database transaction failed (delete, insert, or commit).</summary>
    public const int RemoveExcludeDbWriteFailed = 19;

    /// <summary>
    /// The PGLite data directory is locked by another d2net-init process
    /// at bridge startup time. Fail-fast per spec 007 clarification 2026-05-01,
    /// reused by spec 008.
    /// </summary>
    public const int RemoveExcludeWorkspaceLocked = 20;

    /// <summary>
    /// One or more --remove-exclude paths is currently excluded with kind!='manual'
    /// (init-time auto-detected 'tool' or 'pattern' rows) and --allow-system-exclusions
    /// was not supplied. Per spec 008 clarification 2026-05-01: refuse-by-default,
    /// override via explicit flag.
    /// </summary>
    public const int RemoveExcludeSystemKindRefused = 21;
}
