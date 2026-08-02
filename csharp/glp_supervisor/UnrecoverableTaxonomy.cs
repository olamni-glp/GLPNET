// UnrecoverableTaxonomy — the DEF-F2 classification (T027; FR-023,
// contracts/supervision.md, data-model.md "UnrecoverableClassification").
//
// On classification the supervisor STOPS restart-looping, persists the
// classification on the crash record, and surfaces it loudly to the operator —
// no silent loops.
//
//   repeated_immediate_crash  ≥ CrashThreshold crashes within CrashWindow
//   corrupt_latest_snapshot   latest restore failed AND the one previous-seq
//                             fallback also failed (contract: fall back once)
//   store_unavailable         both snapshot backends down at restart time
//   explicit_poison           the engine self-reported a fatal state — the
//                             documented exit-code convention below; the
//                             current engine has no poison path yet, so this
//                             classification is defined but unreachable until
//                             an engine change introduces one (recorded here
//                             honestly rather than invented).

namespace GlpRuntime.Supervisor;

public enum UnrecoverableReason
{
    RepeatedImmediateCrash,
    CorruptLatestSnapshot,
    StoreUnavailable,
    ExplicitPoison,
}

public static class UnrecoverableTaxonomy
{
    /// <summary>The engine exit code reserved for explicit self-reported poison.</summary>
    public const int PoisonExitCode = 70;

    /// <summary>Lower-snake taxonomy words (data-model naming) for records/operator surface.</summary>
    public static string Word(UnrecoverableReason reason) => reason switch
    {
        UnrecoverableReason.RepeatedImmediateCrash => "repeated_immediate_crash",
        UnrecoverableReason.CorruptLatestSnapshot => "corrupt_latest_snapshot",
        UnrecoverableReason.StoreUnavailable => "store_unavailable",
        UnrecoverableReason.ExplicitPoison => "explicit_poison",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    /// <summary>explicit_poison: the engine's own fatal self-report (exit-code convention).</summary>
    public static bool IsExplicitPoison(int? exitCode) => exitCode == PoisonExitCode;

    /// <summary>
    /// repeated_immediate_crash: at least <paramref name="threshold"/> crashes
    /// inside the trailing <paramref name="window"/> ending at <paramref name="now"/>.
    /// </summary>
    public static bool IsRepeatedImmediateCrash(
        IReadOnlyList<DateTimeOffset> crashTimesUtc, DateTimeOffset now,
        TimeSpan window, int threshold) =>
        crashTimesUtc.Count(t => now - t <= window) >= threshold;
}
