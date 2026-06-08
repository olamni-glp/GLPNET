import 'dart:typed_data';

import 'frame_exception.dart';

/// Per-link inbound FIFO reconstruction + transport-level dedup (FR-020/023/053).
/// A single sequence number DETECTS disorder but does not RESTORE it, so a reorder
/// buffer is required (architecture-context.md §4.2): out-of-order frames are held
/// until the gap fills, then released in sequence order. A frame at or below the
/// delivered high-water mark, or one already buffered, is an idempotent no-op —
/// the transport-level half of at-least-once redelivery (FR-021/FR-027), upstream
/// of the global-name dedup gate in `mad_context`.
///
/// One instance per link per direction, driven by that link's single receive loop
/// (not thread-safe). The reorder buffer is bounded (FR-028): a peer cannot force
/// unbounded memory by withholding one early frame while flooding later ones.
class InboundOrdering {
  int _nextExpected;
  final Map<int, Uint8List> _buffer = {};
  final int _maxBufferedFrames;

  /// [start]: The first expected sequence number (must match the peer's sequencer start).
  /// [maxBufferedFrames]: Cap on out-of-order frames held awaiting a gap.
  InboundOrdering([int start = 0, int maxBufferedFrames = 256])
      : _nextExpected = start,
        _maxBufferedFrames = maxBufferedFrames;

  /// The next in-order sequence number awaited.
  int get nextExpected => _nextExpected;

  /// Out-of-order frames currently buffered awaiting a gap.
  int get bufferedCount => _buffer.length;

  /// Accept one sequenced payload. Returns the payloads now deliverable IN ORDER
  /// (empty when the input was a duplicate/old frame, or a future frame buffered
  /// awaiting the gap). Throws [FrameException] if buffering would
  /// exceed the bound.
  List<Uint8List> accept(int seq, Uint8List payload) {
    // Already delivered (seq < high-water) → idempotent no-op.
    if (_seqLess(seq, _nextExpected)) {
      return const [];
    }

    if (seq == _nextExpected) {
      final ready = <Uint8List>[payload];
      _nextExpected = (_nextExpected + 1) & 0xFFFFFFFF;
      // Drain any contiguous run that was buffered ahead of this gap.
      while (_buffer.containsKey(_nextExpected)) {
        ready.add(_buffer[_nextExpected]!);
        _buffer.remove(_nextExpected);
        _nextExpected = (_nextExpected + 1) & 0xFFFFFFFF;
      }
      return ready;
    }

    // Future frame (seq > nextExpected): buffer it, deduping a re-delivered future.
    if (_buffer.containsKey(seq)) {
      return const [];
    }
    if (_buffer.length >= _maxBufferedFrames) {
      throw FrameException(
          'reorder buffer full (max $_maxBufferedFrames); missing seq $_nextExpected');
    }
    _buffer[seq] = payload;
    return const [];
  }

  // Sequence comparison. Plain unsigned compare (no wraparound within a session;
  // a reconnect creates a fresh InboundOrdering, see LinkSequencer remarks).
  static bool _seqLess(int a, int b) => a < b;
}
