namespace GlpRuntime.Runtime;

/// <summary>
/// The inbound-pump seam (feature 025, Option B). Lets an asynchronous transport
/// feed the single-threaded runner without violating the Option-C single-owner
/// invariant: a background receive thread enqueues decoded frames into a
/// thread-safe inbox, and the engine's run-to-quiescence driver calls
/// <see cref="TryApplyNext"/> on the RUNNER THREAD to apply them (bind heap cells +
/// enqueue reactivated goals) between scheduler drains.
/// </summary>
/// <remarks>
/// HAND-WRITTEN core seam (not a codegen-converted Dart file): listed explicitly in
/// glp_runtime_net.csproj, like equiv_trace.cs. The implementation lives OUTSIDE
/// the runtime — in the hand-authored link package (csharp/glp_link) — so the
/// dependency flows glp_link -> out/csharp only (the engine never references the
/// link layer). When no pump is set (<c>GlpRuntimeEngine.InboundPump == null</c>)
/// the driver loop is skipped entirely, so ordinary non-link programs are
/// unaffected.
/// </remarks>
public interface IInboundPump
{
    /// <summary>
    /// True while there is buffered inbound input OR at least one open link still
    /// expecting more (so the driver should keep servicing). False once every link
    /// is closed/permFailed and the inbox is drained — the driver then stops.
    /// </summary>
    bool HasPendingOrLive { get; }

    /// <summary>
    /// Apply the next inbound frame ON THE CALLING (runner) THREAD, blocking up to
    /// <paramref name="wait"/> for one to arrive. Returns <c>true</c> if a frame was
    /// applied (heap bound, reactivated goals enqueued), or <c>false</c> if the wait
    /// elapsed with nothing applied (the driver then stops; a still-open but idle
    /// link leaves its reader safely suspended).
    /// </summary>
    bool TryApplyNext(TimeSpan wait);
}
