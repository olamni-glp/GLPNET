import 'dart:typed_data';

import 'link_fault.dart';
import 'link_id.dart';

/// One established, bilateral link end (FR-058). Carries opaque, self-delimiting
/// frames in FIFO order; the reliability sublayer sits ABOVE this (sequence/dedup,
/// reorder, framing, epoch/fence, GC, backpressure) and `MadContext` above
/// that. The endpoint itself knows nothing about terms or SRSW
/// (architecture-context.md §3).
///
/// Wiring (architecture-context.md §3): [sendBytes] is driven by
/// `MadContext.onMessageReady` (the serialized header + term as one frame);
/// a frame from [recvBytes] is decoded and dispatched to
/// `MadContext.handleMadAssignment`. The async `recv` shape is exactly
/// the convspec-escalated interface; native bodies are hand-authored per leaf, not
/// auto-converted (FR-058).
///
/// Mirrors C# `IAsyncDisposable`: [dispose] tears the endpoint down.
abstract class ILinkEndpoint {
  /// The stable, never-reused identity of this link (FR-007). Both establishment
  /// paths (listen/connect and request/accept) yield endpoints with an
  /// equivalent [LinkId] (FR-002).
  LinkId get id;

  /// Send exactly one self-delimiting frame (an opaque `PayloadSerializer`
  /// blob). Per-link FIFO is preserved by the sublayer above; a leaf MUST deliver
  /// frames it accepts in submission order or report a fault (FR-018).
  ///
  /// [frame]: One complete frame. Treated as opaque bytes.
  /// [cancel]: Cancels the send.
  Future<void> sendBytes(Uint8List frame, {Future<void>? cancel});

  /// Receive the next self-delimiting frame, awaiting one if none is buffered.
  /// One call yields exactly one frame; the sublayer reconstructs per-link FIFO
  /// from these (FR-018/FR-020).
  ///
  /// [cancel]: Cancels the receive.
  ///
  /// Returns one complete frame, or `null` when the peer has cleanly ended the
  /// stream (graceful close → `closed(LinkId, eos)` upstream).
  Future<Uint8List?> recvBytes({Future<void>? cancel});

  /// Tear this end down. Graceful close lets in-flight frames drain and ends the
  /// peer's [recvBytes] with `null`; the sublayer emits the
  /// terminal `closed(LinkId, Reason)` term and runs distributed GC (FR-024).
  Future<void> close();

  /// Raised out-of-band of the data path when the transport faults (FR-008). The
  /// sublayer turns the [LinkFaultSignal] into the
  /// `ok`/`tempFail`/`permFail` monitor-stream terms. A fault here
  /// never becomes a unification verdict (FR-043) and never fails the reader's
  /// data goal (FR-044).
  Stream<LinkFaultSignal> get onFault;

  /// Mirrors C# `IAsyncDisposable.DisposeAsync`.
  Future<void> dispose();
}
