// Quiescence — FR-014 quiescence detection + pending-snapshot parking (T016).
//
// Clarified FR-014: quiescent ⇔ goal queue empty ∧ no reduction in flight ∧
// client transport drained. Suspended goals and armed timers are PERMITTED in
// a quiescent engine and are captured.
//
// Two of the three conjuncts are structural in this host: the request loop
// (EngineServer.ServeClientAsync) is strictly sequential — one request is read,
// dispatched to completion, and answered before the next read — so at the
// moment the dispatcher examines a SNAPSHOT request there is never a reduction
// in flight (RunGoalAsync has returned) and the transport is drained (no other
// request is buffered ahead of the response). The remaining conjunct is the
// scheduler's drain state: the goal queue must be empty (a previous RUN_GOAL
// can leave work queued when it exhausted MaxCycles, and a fired wait() timer
// re-populates the queue asynchronously).
//
// A SNAPSHOT request against a non-quiescent engine is parked as pending and
// reported DEFERRED to the requester (wire rule 5); the parked snapshot fires
// at the next quiescent moment (checked after every subsequent request and at
// graceful shutdown) and its state is visible via STATUS.
//
// Capture consistency vs armed timers: a wait()/wait_until() timer fires on a
// thread-pool thread and mutates the heap. DisarmTimersForCapture() therefore
// disposes every armed timer (waiting out any in-flight callback) BEFORE the
// capture walks the heap, recording each timer's remaining duration (FR-015);
// RearmTimers() re-arms them afterwards. If a timer fired between the
// quiescence check and the disarm, the goal queue is re-checked after the
// disarm and the snapshot is deferred rather than emitted inconsistent.

using GlpRuntime.Bytecode;
using GlpRuntime.Engine;

namespace GlpRuntime.EngineHost;

/// <summary>One disarmed timer: the writer it will bind + its remaining duration.</summary>
public sealed record DisarmedTimer(int WriterAddr, long RemainingMs);

public sealed class Quiescence
{
    private readonly GlpEngine _engine;

    public Quiescence(GlpEngine engine)
    {
        _engine = engine;
    }

    /// <summary>True when a deferred SNAPSHOT request is parked (FR-014).</summary>
    public bool SnapshotPending { get; private set; }

    /// <summary>UTC timestamp of the parked request (STATUS reporting).</summary>
    public DateTimeOffset? PendingSince { get; private set; }

    /// <summary>
    /// FR-014 quiescence: the goal queue is empty. The other two conjuncts (no
    /// reduction in flight, transport drained) hold structurally at every
    /// dispatcher decision point — see the file header.
    /// </summary>
    public bool IsQuiescent => _engine.Runtime.Gq.Length == 0;

    /// <summary>Park a snapshot request until the next quiescent moment.</summary>
    public void ParkSnapshotRequest()
    {
        SnapshotPending = true;
        PendingSince ??= DateTimeOffset.UtcNow;
    }

    /// <summary>Clear the parked request (after the deferred snapshot fires).</summary>
    public void ClearPending()
    {
        SnapshotPending = false;
        PendingSince = null;
    }

    /// <summary>
    /// Disarm every armed wait()/wait_until() timer for a consistent capture:
    /// dispose each timer (waiting out an in-flight callback), and record its
    /// remaining duration (FR-015). Returns null — with every still-armed timer
    /// re-armed — when the engine turned out non-quiescent after the disarm
    /// (a timer fired concurrently and enqueued goals): the snapshot must be
    /// deferred, never emitted inconsistent (FR-014).
    /// </summary>
    public IReadOnlyList<DisarmedTimer>? DisarmTimersForCapture()
    {
        var rt = _engine.Runtime;
        List<KeyValuePair<int, GlpRuntime.Runtime.GlpRuntimeEngine.PendingGlpTimer>> armed;
        lock (rt.PendingGlpTimers)
            armed = rt.PendingGlpTimers.ToList();

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var disarmed = new List<DisarmedTimer>();
        foreach (var (writerAddr, pending) in armed)
        {
            using var done = new ManualResetEvent(false);
            if (pending.Timer.Dispose(done))
                done.WaitOne();

            bool hadNotFired;
            lock (rt.PendingGlpTimers)
                hadNotFired = rt.PendingGlpTimers.Remove(writerAddr);
            if (hadNotFired)
                disarmed.Add(new DisarmedTimer(writerAddr, Math.Max(0, pending.DeadlineUnixMs - now)));
            // else: the timer fired concurrently — its binding + reactivations are
            // already applied; the queue re-check below catches the new work.
        }

        // Consistency gate: a concurrently-fired timer enqueued goals, or the
        // counter disagrees with what we hold — defer, never tear (FR-014).
        if (rt.Gq.Length != 0 || rt.PendingTimers != disarmed.Count)
        {
            RearmTimers(disarmed);
            return null;
        }
        return disarmed;
    }

    /// <summary>
    /// Re-arm disarmed timers on the SAME engine after capture (or after a
    /// deferral). PendingTimers bookkeeping is untouched: a disarmed-unfired
    /// timer was never decremented, and StartGlpTimer's callback decrements
    /// exactly once when it eventually fires.
    /// </summary>
    public void RearmTimers(IReadOnlyList<DisarmedTimer> timers)
    {
        foreach (var t in timers)
            BytecodeRunner.StartGlpTimer((int)Math.Min(t.RemainingMs, int.MaxValue), _engine.Runtime, t.WriterAddr);
    }
}
