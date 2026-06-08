import 'dart:async';

import '../reliability/frame_reassembler.dart';
import '../reliability/inbound_ordering.dart';
import '../reliability/link_sequencer.dart';
import '../reliability/send_window.dart';
import '../seam/i_link_endpoint.dart';
import '../seam/link_id.dart';
import '../seam/link_options.dart';

/// The per-instance state of one established link: its transport endpoint plus the
/// Phase-2 reliability bundle (sequencer, send window, reassembler, inbound
/// ordering). Stored in the [LinkRegistry] keyed by [LinkId]
/// so a re-setup with the same identity reuses it (FR-007). The heap stream cursors
/// (the In/Faults writers the host extends and the Out reader it drains) are wired
/// in by the `'_link_setup'` kernel during establishment.
class LinkHandle {
  final LinkId id;
  final LinkOptions options;

  /// The transport endpoint, DEFERRED until the async connect/listen rendezvous
  /// resolves (Dart's single isolate cannot block on the connect Future the way the
  /// C# reference blocks on `endpointTask.GetAwaiter().GetResult()`). Establishment
  /// wires the heap cursors and registers the link SYNCHRONOUSLY, then kicks off the
  /// connect in the background; this field is `null` until [attachEndpoint] runs.
  /// The egress drainer and the pump's recv loop both gate on [ready] rather than
  /// touch this directly while it may still be null.
  ILinkEndpoint? endpoint;

  /// Completes with the transport endpoint once the async rendezvous resolves. The
  /// egress drainer chains sends on [ready] (preserving FIFO across the connect gate)
  /// and the pump's per-link recv loop awaits it before its receive loop.
  final Completer<ILinkEndpoint> endpointReady = Completer<ILinkEndpoint>();

  /// Set once the link is being / has been torn down (graceful `[]` close, abrupt
  /// `'_link_close'`, or a connect failure). The pump's recv loop checks it between
  /// frames so it stops once the link is closed.
  bool closed = false;

  /// Per-handle egress serialization tail (T031 FIFO across the readiness gate):
  /// each `Out`-stream bind chains its `ready.then(sendBytes)` off the prior frame's
  /// completion so frames ship in SUBMISSION order even though the endpoint may not
  /// have connected yet. Mutated only on the runner thread (the egress drainer).
  Future<void> egressTail = Future<void>.value();

  /// Outbound sequence source (→ frame MessageId).
  final LinkSequencer sequencer;

  /// Bounded backpressure window (N from [LinkOptions.backpressureWindow]).
  final SendWindow window;

  /// Inbound fragment reassembly.
  final FrameReassembler reassembler;

  /// Inbound FIFO reconstruction + transport-level dedup.
  final InboundOrdering ordering;

  // --- heap stream cursors, set by '_link_setup' during establishment ---

  /// The writer the host extends as inbound frames arrive (the program reads `In`).
  int? inWriterAddr;

  /// The reader the host drains as the program writes `Out`.
  int? outReaderAddr;

  /// The writer the host extends with fault terms (the program reads `Faults`).
  int? faultsWriterAddr;

  /// The live per-link MONITOR cursors (feature 025, T034): one writer cell per
  /// independent fault observer. A fault is fanned out to every cursor — the
  /// `Faults` stream minted at establishment AND every later `link_monitor`
  /// stream — so faults are independently observable from the data path and from each
  /// other (FR-008). Each entry advances to a fresh writer as its stream is extended;
  /// mutated only on the runner thread (the kernel and the pump's apply step).
  final List<int> monitorCursors = <int>[];

  /// The endpoint Future the egress drainer and recv loop gate on. Resolves when
  /// [attachEndpoint] runs (the async connect/listen rendezvous completed).
  Future<ILinkEndpoint> get ready => endpointReady.future;

  /// Attach the transport endpoint once the async rendezvous resolves: publish it on
  /// [endpoint] (the synchronous fast path used after connect) and complete [ready]
  /// so the gated egress sends and the recv loop unblock. Idempotent on [ready].
  void attachEndpoint(ILinkEndpoint ep) {
    endpoint = ep;
    if (!endpointReady.isCompleted) {
      endpointReady.complete(ep);
    }
  }

  LinkHandle(this.id, this.options)
      : sequencer = LinkSequencer(),
        window = SendWindow(options.backpressureWindow),
        reassembler = FrameReassembler(),
        ordering = InboundOrdering();
}
