namespace GlpRuntime.Link.Reliability;

/// <summary>
/// A count of the per-link runtime resources distributed GC must reclaim (FR-024,
/// SC-014): the global-writers table (<c>W_p</c>) entries, the
/// <c>GlobalSendRegistry</c> goals, the heap <c>onBind</c> callbacks, and the
/// request/reply table entries. Used by the SC-014 / T074 probe to assert that,
/// after N <c>permFail</c>s, every counter returns to its pre-link baseline — no
/// leak on peer death (today removal is one-shot only and leaks).
/// </summary>
/// <remarks>
/// The link layer (<c>csharp/glp_link</c>) does not own these subsystems — they
/// live in the <c>out/csharp</c> runtime. The actual counts are read through an
/// <see cref="IResourceProbe"/> the Phase-3 wiring binds to the real structures;
/// this type is the transport-agnostic shape they report in.
/// </remarks>
public readonly record struct ResourceSnapshot(
    int GlobalWriters, int SendRegistryGoals, int BindCallbacks, int ReplyTableEntries)
{
    public static readonly ResourceSnapshot Zero = new(0, 0, 0, 0);

    /// <summary>True when every counter equals the supplied baseline (reclaimed to baseline).</summary>
    public bool IsBaseline(ResourceSnapshot baseline) => this == baseline;

    public override string ToString() =>
        $"{{W_p={GlobalWriters}, send={SendRegistryGoals}, onBind={BindCallbacks}, reply={ReplyTableEntries}}}";
}

/// <summary>
/// Reads the current <see cref="ResourceSnapshot"/> from the live runtime. Bound
/// in Phase 3 to <c>GlobalWritersTable</c>, the <c>GlobalSendRegistry</c>, the heap
/// <c>_bindCallbacks</c>, and the reply table; the SC-014 test takes a baseline
/// before opening links and asserts return-to-baseline after reclamation.
/// </summary>
public interface IResourceProbe
{
    ResourceSnapshot Snapshot();
}
