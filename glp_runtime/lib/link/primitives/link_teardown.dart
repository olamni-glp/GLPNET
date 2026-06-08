import '../../runtime/body_kernels.dart' show BodyKernelResult;
import '../../runtime/runtime.dart';
import '../seam/link_id.dart';
import 'link_establish.dart';
import 'link_faults.dart';
import 'link_handle.dart';
import 'link_runtime.dart';
import 'link_terms.dart';

/// The ONE link-teardown core both close paths converge on (feature 025, T035): the
/// ABRUPT `'_link_close'/2` kernel (RST_STREAM-equiv teardown by ground LinkId,
/// regardless of stream state) and the GRACEFUL stream-end `[]` close (the program
/// binds its `Out` writer to `[]`, observed by [LinkEstablish]'s
/// egress drainer). Both emit a terminal `closed(LinkId, Reason)` on every monitor
/// stream, end those streams, tear the transport endpoint down, and run distributed GC —
/// the abrupt path with the caller's reason (default `abrupt`), the graceful path with
/// `eos` (rulings-log "graceful reason eos"; FR-024).
///
/// `closed` is an INTENTIONAL terminal — distinct from the `tempFail`/`permFail`
/// faults — but it rides the same monitor-stream delivery (an ordinary bound ground term,
/// never a fourth verdict and never a logical Fail; FR-043/044). The data path is left
/// untouched: a goal suspended on `In` stays suspended after a close (it learns of the
/// teardown through the monitor stream, FR-044), so teardown never binds the `In` cursor.
/// All steps run on the runner thread — the kernel dispatch and the egress `onBind`
/// callback are both runner-thread — so the heap binds here are safe.
class LinkTeardown {
  LinkTeardown._();

  static const String _who = "'_link_close'/2";

  /// Abrupt close by ground [LinkId] (the `'_link_close'/2` kernel body).
  /// Aborts on an unestablished link — close observes an existing link, it never creates one,
  /// and a repeat close after GC finds nothing (CLAUDE.md "robustness is a workaround").
  static BodyKernelResult close(
      GlpRuntime rt, LinkRuntime link, LinkId id, String reason) {
    final handle = link.links.tryGet(id);
    if (handle == null) {
      return LinkEstablish.abort(_who, 'close of unestablished link $id');
    }
    teardown(rt, link, handle, reason);
    return BodyKernelResult.success;
  }

  /// Tear a link down: emit the terminal `closed(LinkId, Reason)` on every monitor
  /// stream, end those streams with `[]`, close the transport endpoint, and run the
  /// distributed-GC hooks (registry entry → baseline; FR-024). Shared by the abrupt kernel
  /// and the graceful `[]` path. Idempotent at the GC layer: a second teardown for the
  /// same link reclaims nothing (the registry entry is already gone).
  static void teardown(
      GlpRuntime rt, LinkRuntime link, LinkHandle handle, String reason) {
    final heap = rt.heap;

    // 1. Terminal close term on every monitor observer (establishment Faults + each
    //    link_monitor stream, FR-008), then 2. end those streams — a close is terminal.
    LinkFaults.deliverFault(heap, rt, handle, LinkTerms.closed(handle.id, reason));
    LinkFaults.endAll(heap, rt, handle);
    handle.monitorCursors
        .clear(); // streams ended; a late fan-out would extend a dead stream

    // 3. Mark the handle closed so the pump's recv loop exits between frames and the
    //    pump stops counting it as live (hasPendingOrLive). Then transport teardown
    //    (abrupt = RST-equiv per leaf; graceful drains — both via the seam's close,
    //    which is idempotent). The endpoint is DEFERRED until the async connect
    //    resolves: close it now if connected, else chain the close on readiness so a
    //    link torn down mid-connect still releases its socket once it lands.
    handle.closed = true;
    // Close the transport, TRACKED so run-to-quiescence stays live until the socket is
    // actually closed (the single-isolate stand-in for the C#
    // `CloseAsync().GetAwaiter().GetResult()`). GRACEFUL (eos) first drains any queued
    // egress frames by chaining the close on the per-handle egress tail, THEN FINs
    // (flush-then-close, FR-018 FIFO) — otherwise the FIN could race ahead of the
    // still-async data sends and the peer would receive nothing. ABRUPT (RST-equiv)
    // closes immediately regardless of stream state. The endpoint may still be
    // connecting, so the close gates on [LinkHandle.ready].
    final Future<void> closeIo;
    if (reason == LinkTerms.gracefulReason) {
      closeIo = handle.egressTail =
          handle.egressTail.then((_) => _closeEndpoint(handle), onError: (Object _) {});
    } else {
      closeIo = _closeEndpoint(handle);
    }
    (handle.ioTracker ?? link.pump.trackIo)(closeIo);

    // 4. Distributed GC (FR-024): run every registered reclamation hook so the runtime
    //    returns to its pre-link baseline (registry entry removed today). Idempotent.
    link.reclaimer.reclaim(handle.id, reason);
  }

  /// Close the transport endpoint, gating on the deferred connect: close it now if
  /// connected, else once the rendezvous resolves (a link torn down mid-connect still
  /// releases its socket). Swallows a connect that never resolved (already faulted).
  static Future<void> _closeEndpoint(LinkHandle handle) async {
    final ep = handle.endpoint;
    if (ep != null) {
      await ep.close();
    } else {
      try {
        final resolved = await handle.ready;
        await resolved.close();
      } on Object {
        // connect failed / never resolved — nothing to close.
      }
    }
  }
}
