/// The inbound-pump seam (feature 025, Option B). Lets an asynchronous transport
/// feed the single-threaded runner without violating the Option-C single-owner
/// invariant: a background receive loop enqueues decoded frames into a
/// thread-safe inbox, and the engine's run-to-quiescence driver calls
/// [tryApplyNext] on the RUNNER THREAD to apply them (bind heap cells +
/// enqueue reactivated goals) between scheduler drains.
///
/// HAND-WRITTEN core seam (mirror of `out/csharp/lib/runtime/inbound_pump.cs`'s
/// `IInboundPump`). The implementation lives OUTSIDE the runtime — in the
/// hand-authored link package (`glp_runtime/lib/link`) — so the dependency flows
/// `link -> runtime` only (the engine never references the link layer). When no
/// pump is set (`GlpRuntime.inboundPump == null`) the driver loop is skipped
/// entirely, so ordinary non-link programs are unaffected.
library;

/// The inbound-pump seam (feature 025, Option B).
abstract class IInboundPump {
  /// True while there is buffered inbound input OR at least one open link still
  /// expecting more (so the driver should keep servicing). False once every link
  /// is closed/permFailed and the inbox is drained — the driver then stops.
  bool get hasPendingOrLive;

  /// Apply the next inbound frame ON THE CALLING (runner) THREAD, blocking up to
  /// [wait] for one to arrive. Returns `true` if a frame was applied (heap bound,
  /// reactivated goals enqueued), or `false` if the wait elapsed with nothing
  /// applied (the driver then stops; a still-open but idle link leaves its reader
  /// safely suspended).
  bool tryApplyNext(Duration wait);
}
