namespace D2Net.Scaffold;

/// <summary>
/// Exit-code catalogue for <c>d2net-scaffold</c> (feature 009).
/// Allocations 22-29 are contiguous after feature 008's 17-21 to avoid
/// collisions across the d2net tool family.
/// </summary>
public static class ExitCodes
{
    /// <summary>Success.</summary>
    public const int Success = 0;

    /// <summary>Usage error: unknown flag, missing flag pair, positional arg supplied.</summary>
    public const int ArgumentError = 1;

    /// <summary>No <c>.D2NET/</c> workspace at the current working directory.</summary>
    public const int ScaffoldWorkspaceMissing = 22;

    /// <summary>The configured source directory does not exist on disk.</summary>
    public const int ScaffoldSourceMissing = 23;

    /// <summary>
    /// Target directory exists with content not produced by a prior scaffold
    /// run, and <c>--FORCE --DELETE-TARGET</c> was not supplied.
    /// </summary>
    public const int ScaffoldTargetNotEmptyAndNotManaged = 24;

    /// <summary>
    /// A planned <c>__&lt;basename&gt;/</c> working directory collides with a
    /// pre-existing real file or non-empty directory at that path.
    /// </summary>
    public const int ScaffoldWorkdirCollision = 25;

    /// <summary>Filesystem IO failure during the staging copy or atomic rename.</summary>
    public const int ScaffoldCopyError = 26;

    /// <summary>
    /// The Postgres transaction failed (DDL / UPDATE / UPSERT / COMMIT).
    /// </summary>
    public const int ScaffoldDbWriteFailed = 27;

    /// <summary>Another <c>d2net-init</c> or <c>d2net-scaffold</c> holds the workspace lock.</summary>
    public const int ScaffoldWorkspaceLocked = 28;

    /// <summary>Operator declined the <c>--FORCE --DELETE-TARGET</c> confirmation prompt (FR-012a).</summary>
    public const int ScaffoldOperatorCancelledTargetDeletion = 29;
}
