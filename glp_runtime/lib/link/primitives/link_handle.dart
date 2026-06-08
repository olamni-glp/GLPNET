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
  final ILinkEndpoint endpoint;
  final LinkOptions options;

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

  LinkHandle(this.id, this.endpoint, this.options)
      : sequencer = LinkSequencer(),
        window = SendWindow(options.backpressureWindow),
        reassembler = FrameReassembler(),
        ordering = InboundOrdering();
}
