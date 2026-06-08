import 'dart:async';
import 'dart:collection';
import 'dart:typed_data';

import '../../multiagent/payload_serializer.dart';
import '../../runtime/inbound_pump.dart';
import '../../runtime/runtime.dart';
import '../../runtime/terms.dart';
import '../reliability/frame_codec.dart';
import '../seam/i_link_endpoint.dart';
import '../seam/link_fault.dart';
import 'link_faults.dart';
import 'link_handle.dart';
import 'link_terms.dart';

/// The inbound pump (feature 025, Option B — design ref
/// `research/inbound-pump-and-isolate-manager.md` §1.3): the bridge that lets
/// asynchronous transports feed the single-threaded GLP runner without breaking the
/// Option-C single-owner-heap invariant.
///
/// Discipline (design ref §1.3): a per-link **background** receive loop decodes
/// arriving frames and does an **enqueue** into the one shared `inbox` — it never
/// touches the heap. The engine's run-to-quiescence driver calls [tryApplyNext]
/// **on the runner thread**, which dequeues one item and extends the target link's
/// `In` stream (allocate a fresh pair, cons the value, bind the writer, enqueue the
/// reactivated goals) — exactly the §1.6 B5/B6 worked example. The `inbox` is the
/// sole cross-thread structure, mirroring Option-C's per-agent channel.
///
/// HOST IDIOM NOTE: the C# original uses a `BlockingCollection` filled by background
/// `Task`s and a synchronously-blocking [tryApplyNext]. Dart runs the recv loops as
/// `Future`s on the single isolate event loop, so [tryApplyNext] cannot block-wait
/// on the inbox synchronously; the engine driver awaits between drains to let the
/// recv-loop microtasks fill the inbox, then [tryApplyNext] applies whatever is
/// buffered. The contract members (HasPendingOrLive / TryApplyNext / AddLink) match
/// the C# `IInboundPump` exactly.
class LinkPump implements IInboundPump {
  /// One decoded inbound event awaiting application on the runner thread: an inbound
  /// data term (`In`-stream extension), a graceful close, or a fault term to fan
  /// out to the link's monitor cursors ([fault], T034).
  final GlpRuntime _engine;
  final Queue<_InboundItem> _inbox = Queue<_InboundItem>();
  final Completer<void> _cancel = Completer<void>();
  final List<Future<void>> _recvLoops = <Future<void>>[];
  final List<LinkHandle> _handles = <LinkHandle>[];
  bool _disposed = false;

  /// Single-shot signal the runner-thread driver awaits ([waitForInbound]): a
  /// background recv loop / fault / close completes it on each enqueue so the
  /// driver wakes, applies the item, and re-drains. Reset after each wake.
  Completer<void>? _waiter;

  /// Enqueue one decoded inbound event and wake the driver. The ONLY mutator of
  /// [_inbox] — every recv-loop/fault/close path routes through here so the
  /// [_waiter] signal can never be missed.
  void _enqueue(_InboundItem item) {
    _inbox.add(item);
    _wake();
  }

  /// Wake the single driver waiter — on an inbound enqueue OR an outstanding-I/O
  /// completion. Reset so the next park re-arms it.
  void _wake() {
    final w = _waiter;
    if (w != null && !w.isCompleted) {
      _waiter = null;
      w.complete();
    }
  }

  /// Count of outstanding OUTBOUND I/O futures (deferred connect, egress sends,
  /// close) — see [trackIo].
  int _outstandingIo = 0;

  /// Count of LIVE links — incremented in [addLink], decremented when a recv loop
  /// ends (the PEER's FIN or pump cancel), mirroring the C# `_liveLinks`. A link stays
  /// live even after WE close our own send side, so we keep RECEIVING the peer's data
  /// until it FINs (bilateral links, FR-003) — closing our sender never stops our
  /// receiver.
  int _liveLinks = 0;

  /// Track one outstanding OUTBOUND I/O future so the run-to-quiescence driver stays
  /// live ([hasPendingOrLive]) and parks in [waitForInbound] until it settles. This
  /// is the Dart equivalent of the C# runner thread BLOCKING on
  /// `SendBytesAsync(...).GetAwaiter().GetResult()` / the connect `.GetResult()`:
  /// on a single isolate we cannot block, so instead we keep the run alive until the
  /// async send/connect/close completes. Without it a producer's GLP goal could finish
  /// (and the process exit) before its async I/O ran, so the peer would receive nothing.
  void trackIo(Future<void> io) {
    _outstandingIo++;
    io.whenComplete(() {
      if (_outstandingIo > 0) _outstandingIo--;
      _wake();
    });
  }

  LinkPump(GlpRuntime engine)
      // ignore: unnecessary_null_comparison, prefer_initializing_formals
      : _engine = engine {
    // ignore: unnecessary_null_comparison, dead_code
    if (engine == null) throw ArgumentError.notNull('engine');
  }

  /// Register an established link with the pump: bump the live-link count and start
  /// its background receive loop. Called by `'_link_setup'` after the handle's
  /// [LinkHandle.inWriterAddr] ingress cursor is wired.
  void addLink(LinkHandle handle) {
    // ignore: unnecessary_null_comparison, dead_code
    if (handle == null) throw ArgumentError.notNull('handle');
    if (handle.inWriterAddr == null) {
      throw StateError('addLink before the In-stream ingress cursor was wired');
    }
    // Count the link LIVE from now until its recv loop ends — on the PEER's FIN
    // (recvBytes==null) or pump cancel, NOT on our own send-close. Closing OUR send
    // side must not stop us RECEIVING the peer's data (FR-003).
    _handles.add(handle);
    _liveLinks++;
    // The recv loop gates on the deferred endpoint: it awaits handle.ready, then
    // subscribes to onFault and runs the receive loop. The endpoint may not have
    // connected yet (Dart's single isolate cannot block on the connect Future).
    _recvLoops.add(_recvLoop(handle));
  }

  /// Enqueue an out-of-band transport fault for runner-thread fan-out onto the
  /// link's monitor cursors (FR-008/T034). PUBLIC so the establish path can surface a
  /// connect failure here (its background connect rejecting is a transport fault on
  /// the establishment `Faults` monitor). Builds the GLP lattice term off the heap (a
  /// [Term] is pure data) and only touches the thread-safe inbox — never the heap.
  void enqueueFault(LinkHandle handle, LinkFaultSignal signal) =>
      _enqueueFault(handle, signal);

  /// Surface a path-B request token on the listener's `Requests` writer through the
  /// inbox (feature 025, FR-002). Called by `'_link_listen'`'s background rendezvous
  /// once it has accepted a connection and read the in-band `request(LinkId, FromPeer)`
  /// token: the rendezvous runs OFF the runner-thread drive loop, so — per the §1.3
  /// pump discipline — it must NOT bind the heap itself. Instead it enqueues here; the
  /// runner-thread [tryApplyNext] extends the `Requests` stream and reactivates the
  /// suspended `serve_one`/`accept_link` goal, and the driver re-drains. A request
  /// arriving after the pump is disposed is dropped (the run is being torn down).

  /// Refine an out-of-band seam fault to its GLP lattice term and enqueue it for
  /// runner-thread delivery (T034). Builds the term off the heap (a [Term]
  /// is pure data) and only touches the inbox; a fault arriving after the
  /// pump is disposed is dropped (the link is being torn down anyway).
  void enqueueRequestSurface(int writerAddr, Term requestTerm) {
    if (_disposed) {
      return;
    }
    _enqueue(_InboundItem(null, requestTerm,
        close: false, fault: false, requestWriterAddr: writerAddr));
  }

  void _enqueueFault(LinkHandle handle, LinkFaultSignal signal) {
    final term = LinkTerms.fromSignal(signal);
    if (_disposed) {
      // Pump disposed / inbox completed mid-teardown — nothing left to deliver to.
      return;
    }
    _enqueue(_InboundItem(handle, term, close: false, fault: true));
  }

  /// True while there is buffered inbound input OR at least one open link still
  /// expecting more (so the driver should keep servicing). False once every link
  /// is closed/permFailed and the inbox is drained — the driver then stops.
  @override
  bool get hasPendingOrLive =>
      _inbox.isNotEmpty || _outstandingIo > 0 || _liveLinks > 0;

  /// Apply the next inbound frame ON THE CALLING (runner) THREAD. Returns `true`
  /// if a frame was applied (heap bound, reactivated goals enqueued), or `false`
  /// if nothing was buffered (the driver then stops; a still-open but idle link
  /// leaves its reader safely suspended).
  @override
  bool tryApplyNext(Duration wait) {
    if (_inbox.isEmpty) {
      return false;
    }
    final item = _inbox.removeFirst();

    final heap = _engine.heap;

    if (item.requestWriterAddr != null) {
      // Path-B request surfacing (FR-002): extend the listener's `Requests` writer by
      // one `request(LinkId, FromPeer)` term — mint a fresh (writer, reader) pair, cons
      // [request | reader], bind the writer, wake the suspended serve_one/accept_link
      // goal. Base-MVP is one request per rendezvous, so the fresh writer is left for
      // the stream tail (open but unbound — serve_one reads only the head).
      final (_, freshReader) = heap.allocateVariable();
      final cons = StructTerm('.', <Term>[item.value!, VarRef(freshReader)]);
      for (final act in heap.bindVariable(item.requestWriterAddr!, cons)) {
        _engine.enqueueReactivatedGoal(act);
      }
      return true;
    }

    if (item.fault) {
      // Fan the refined fault term out to every monitor cursor of this link — the
      // establishment `Faults` stream and each `link_monitor` stream (FR-008). A
      // fault is delivered as a bound term, never a verdict (FR-043) and never a
      // logical Fail (FR-044).
      LinkFaults.deliverFault(heap, _engine, item.handle!, item.value!);
      return true;
    }

    final int cursor = item.handle!.inWriterAddr!;

    if (item.close) {
      // Graceful peer close: end the In stream with [] (nil). A consumer reading
      // the stream sees end-of-stream and reduces its `[]` clause (design ref §1.6 Bn).
      for (final act in heap.bindVariable(cursor, ConstTerm('nil'))) {
        _engine.enqueueReactivatedGoal(act);
      }
      item.handle!.inWriterAddr = null; // stream terminated; no further extension
      return true;
    }

    // Extend the In stream by one ground term (design ref §1.6 B6): mint a fresh
    // (writer, reader) pair, cons [value | reader], bind the current writer, wake
    // any suspended reader, then advance the cursor to the fresh writer.
    final (freshWriter, freshReader) = heap.allocateVariable();
    final cons = StructTerm('.', <Term>[item.value!, VarRef(freshReader)]);
    for (final act in heap.bindVariable(cursor, cons)) {
      _engine.enqueueReactivatedGoal(act);
    }
    item.handle!.inWriterAddr = freshWriter;
    return true;
  }

  /// Yield to the event loop and wait for the next inbound item (the Dart driver's
  /// replacement for the C# blocking [tryApplyNext]). Completes `true` the moment an
  /// item is buffered — immediately if one already is, else when a background recv
  /// loop / deferred connect enqueues one (or anything arrives within [timeout]).
  /// Completes `false` when [timeout] elapses with an empty inbox but a still-open
  /// link (its reader stays safely suspended), or immediately when nothing is live
  /// and the inbox is empty (nothing more can ever arrive).
  @override
  Future<bool> waitForInbound(Duration timeout) async {
    if (_inbox.isNotEmpty) return true;
    if (!hasPendingOrLive || _disposed) return false;
    final waiter = _waiter ??= Completer<void>();
    // Race the enqueue signal against the timeout; awaiting either yields the isolate
    // so the background recv/connect microtasks run. A Timer-backed delay (not
    // Future.delayed alone) lets us stop waiting deterministically.
    var timedOut = false;
    final timer = Timer(timeout, () {
      timedOut = true;
      if (!waiter.isCompleted) {
        _waiter = null;
        waiter.complete();
      }
    });
    try {
      await waiter.future;
    } finally {
      timer.cancel();
    }
    // True iff an item actually landed (a timeout wake leaves the inbox empty).
    return !timedOut || _inbox.isNotEmpty;
  }

  /// The per-link background receive loop (never touches the heap): pull a frame,
  /// CRC-validate + reassemble + reorder it through the Phase-2 sublayer, decode the
  /// ground payload, and hand it to the inbox. A `null` frame is the peer's
  /// graceful close. The decoder rejects any embedded variable — the base layer is
  /// pure ground-relay (FR-010), so a non-ground payload is a wire-contract
  /// violation, not something to localize.
  Future<void> _recvLoop(LinkHandle handle) async {
    final deserializer = PayloadSerializer('');
    try {
      // Gate on the deferred endpoint: the async connect/listen rendezvous must resolve
      // before any receive can run (Dart's single isolate cannot block on it). If the
      // connect FAILED, `ready` rejects — the establish path already surfaced the
      // transport fault and marked the handle closed, so just stop without faulting twice.
      final ILinkEndpoint ep;
      try {
        ep = await handle.ready;
      } on Object {
        handle.closed = true;
        return;
      }

      // Out-of-band transport faults (FR-008): refine the seam signal to its GLP
      // lattice term and enqueue it for runner-thread fan-out onto the monitor cursors.
      // The handler runs on the transport's thread but only touches the thread-safe
      // inbox — never the heap (T034). Subscribed only after the endpoint exists.
      final faultSub = ep.onFault.listen((signal) => _enqueueFault(handle, signal));
      try {
        // Loop until the PEER FINs (recvBytes==null) or the pump is cancelled — NOT on
        // our own send-close (handle.closed). Mirrors the C# RecvLoopAsync, which runs
        // `while (!ct.IsCancellationRequested)` and ends only on a null frame: closing
        // our sender must never stop our receiver (bilateral, FR-003).
        while (!_cancel.isCompleted) {
          final Uint8List? frame = await ep.recvBytes(cancel: _cancel.future);
          if (frame == null) {
            _enqueue(_InboundItem(handle, null, close: true, fault: false));
            return;
          }

          final parsed = FrameCodec.parseFrame(frame);
          final Uint8List? payload = handle.reassembler.accept(parsed);
          if (payload == null) {
            continue; // awaiting more fragments
          }

          for (final ordered in handle.ordering.accept(parsed.messageId, payload)) {
            final Term term = deserializer.deserializeAgentMessagePayload(
              ordered,
              (_) => throw StateError(
                  'ground-relay base received a non-ground payload (embedded variable)'),
            );
            _enqueue(_InboundItem(handle, term, close: false, fault: false));
          }
        }
      } finally {
        await faultSub.cancel();
      }
    } finally {
      // The recv loop has ended (peer FIN / pump cancel / connect failure): the link is
      // no longer live. Decrement so hasPendingOrLive can settle and wake the driver.
      _liveLinks--;
      _wake();
    }
  }

  void dispose() {
    _disposed = true;
    if (!_cancel.isCompleted) {
      _cancel.complete();
    }
  }
}

/// One decoded inbound event awaiting application on the runner thread (the C#
/// `InboundItem` readonly record struct).
class _InboundItem {
  /// The owning link handle for data/close/fault items; `null` for a path-B
  /// request-surface item (no link is established yet — see [requestWriterAddr]).
  final LinkHandle? handle;
  final Term? value;
  final bool close;
  final bool fault;

  /// Path-B only: the listener's `Requests` writer address to extend with the
  /// surfaced `request(...)` term. Non-null marks a request-surface item; mutually
  /// exclusive with [handle]/[close]/[fault].
  final int? requestWriterAddr;

  const _InboundItem(this.handle, this.value,
      {required this.close, required this.fault, this.requestWriterAddr});
}
