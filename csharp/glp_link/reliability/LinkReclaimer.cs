using GlpRuntime.Link.Seam;

namespace GlpRuntime.Link.Reliability;

/// <summary>
/// Thrown when one or more reclamation steps failed. The reclaimer still runs
/// EVERY registered step (best-effort), then raises this so no subsystem is left
/// un-reclaimed because an earlier one threw (FR-024: return to baseline, no leak).
/// </summary>
public sealed class ReclaimException : AggregateException
{
    public ReclaimException(string message, IEnumerable<Exception> inner) : base(message, inner) { }
}

/// <summary>
/// Distributed GC coordinator (FR-024, SC-014). On a link's <c>permFail</c> or
/// close, every subsystem holding per-link state — the LinkId→handle registry
/// (T030), the send-registry goals, the heap <c>onBind</c> callbacks, the
/// reply/CorrId table (T033), and the <see cref="FencingRegistry"/> (T023) — runs
/// its reclamation hook so the runtime returns to its pre-link baseline. Today
/// removal is one-shot only and LEAKS when the peer dies; this makes teardown
/// explicit and idempotent.
/// </summary>
/// <remarks>
/// "Seam now, wire later": the subsystems register their hooks here in Phase 3
/// (just as the cycle-guard and seam interfaces are consumed later). Reclamation
/// is idempotent — a <c>permFail</c> followed by a <c>close</c> for the same link
/// reclaims exactly once. Hooks run in registration order and are then dropped, so
/// the reclaimer retains no reference to link state (FR-024 "no unreclaimable
/// cycle"). One reclaimer per instance; not thread-safe.
/// </remarks>
public sealed class LinkReclaimer
{
    private readonly Dictionary<LinkId, List<Action>> _hooks = new();
    private readonly HashSet<LinkId> _reclaimed = new();

    /// <summary>
    /// Register a reclamation hook for a link. A subsystem calls this when it
    /// allocates per-link state; the hook frees that state on
    /// <see cref="Reclaim"/>. No-op-registers nothing for an already-reclaimed link
    /// (the hook would never fire) — instead it runs immediately so late allocations
    /// after teardown do not leak.
    /// </summary>
    public void Register(LinkId link, Action reclaim)
    {
        ArgumentNullException.ThrowIfNull(reclaim);
        if (_reclaimed.Contains(link))
        {
            reclaim(); // link already torn down — reclaim this straggler now
            return;
        }
        if (!_hooks.TryGetValue(link, out var list))
            _hooks[link] = list = new List<Action>();
        list.Add(reclaim);
    }

    /// <summary>
    /// Reclaim all state for a link. Runs every registered hook (best-effort,
    /// continuing past a thrower), drops them, and marks the link reclaimed.
    /// Returns <c>true</c> if this call performed the reclamation, <c>false</c> if
    /// the link was already reclaimed (idempotent). Throws
    /// <see cref="ReclaimException"/> if any hook failed — after all have run.
    /// </summary>
    public bool Reclaim(LinkId link, string reason)
    {
        if (!_reclaimed.Add(link))
            return false; // already reclaimed → idempotent no-op

        _hooks.Remove(link, out var hooks);
        if (hooks is null)
            return true;

        List<Exception>? failures = null;
        foreach (var hook in hooks)
        {
            try { hook(); }
            catch (Exception ex) { (failures ??= new()).Add(ex); }
        }

        if (failures is not null)
            throw new ReclaimException($"link {link} reclamation ({reason}): {failures.Count} hook(s) failed", failures);
        return true;
    }

    /// <summary>True once <see cref="Reclaim"/> has run for the link.</summary>
    public bool IsReclaimed(LinkId link) => _reclaimed.Contains(link);

    /// <summary>Links with registered hooks not yet reclaimed.</summary>
    public int PendingLinkCount => _hooks.Count;
}
