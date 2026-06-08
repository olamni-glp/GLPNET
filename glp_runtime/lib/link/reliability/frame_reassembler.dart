import 'dart:typed_data';

import 'frame_codec.dart';
import 'frame_exception.dart';

class _Partial {
  final int totalLength;
  final int fragCount;
  final List<Uint8List?> chunks;
  int received = 0;
  int bufferedBytes = 0;

  _Partial(this.fragCount, this.totalLength)
      : chunks = List<Uint8List?>.filled(fragCount, null);
}

/// Reassembles fragmented payloads (FR-022). Feed it each [ParsedFrame]
/// (already CRC-validated by [FrameCodec.parseFrame]); a
/// [FrameKind.whole] frame yields its payload immediately, while
/// fragments are buffered by `MessageId` until the set is complete.
///
/// Hardened against malformed/adversarial fragment streams (FR-028): inconsistent
/// metadata across a message's fragments is rejected; the number of concurrently
/// in-flight partial messages and the total buffered bytes are both bounded, so a
/// peer cannot exhaust memory by opening many partial messages or never completing
/// them. Not thread-safe: one reassembler per link, driven by that link's single
/// receive loop.
class FrameReassembler {
  final int _maxInFlight;
  final int _maxBufferedBytes;
  final Map<int, _Partial> _partials = {};
  int _totalBuffered = 0;

  /// [maxInFlightMessages]: Cap on concurrently incomplete messages.
  /// [maxBufferedBytes]: Cap on total bytes held across all partials.
  FrameReassembler(
      [int maxInFlightMessages = 64, int maxBufferedBytes = 64 * 1024 * 1024])
      : _maxInFlight = maxInFlightMessages,
        _maxBufferedBytes = maxBufferedBytes;

  /// Accept one frame. Returns the complete payload when this frame finishes a
  /// message (or for a whole frame), otherwise `null`. Throws
  /// [FrameException] on inconsistent or over-bound input.
  Uint8List? accept(ParsedFrame frame) {
    if (frame.kind == FrameKind.whole) {
      if (frame.chunk.length != frame.totalLength) {
        throw FrameException(
            'whole frame chunk ${frame.chunk.length} != total ${frame.totalLength}');
      }
      return frame.chunk;
    }

    var partial = _partials[frame.messageId];
    if (partial == null) {
      if (_partials.length >= _maxInFlight) {
        throw FrameException(
            'too many in-flight partial messages (max $_maxInFlight)');
      }
      if (_totalBuffered + frame.totalLength > _maxBufferedBytes) {
        throw FrameException(
            'reassembly buffer would exceed $_maxBufferedBytes bytes');
      }
      partial = _Partial(frame.fragCount, frame.totalLength);
      _partials[frame.messageId] = partial;
    } else if (partial.totalLength != frame.totalLength ||
        partial.fragCount != frame.fragCount) {
      throw FrameException(
          'fragment metadata mismatch for message ${frame.messageId}: '
          '(${frame.totalLength},${frame.fragCount}) vs (${partial.totalLength},${partial.fragCount})');
    }

    if (partial.chunks[frame.fragIndex] == null) {
      partial.chunks[frame.fragIndex] = frame.chunk;
      partial.received++;
      partial.bufferedBytes += frame.chunk.length;
      _totalBuffered += frame.chunk.length;
    }
    // A duplicate fragment (same index re-delivered) is silently ignored here;
    // payload-level dedup is the T022 sublayer's job.

    if (partial.received < partial.fragCount) {
      return null;
    }

    final payload = Uint8List(partial.totalLength);
    int offset = 0;
    for (int i = 0; i < partial.fragCount; i++) {
      final chunk = partial.chunks[i]!;
      payload.setRange(offset, offset + chunk.length, chunk);
      offset += chunk.length;
    }
    if (offset != partial.totalLength) {
      throw FrameException(
          'reassembled $offset bytes != advertised total ${partial.totalLength}');
    }

    _partials.remove(frame.messageId);
    _totalBuffered -= partial.bufferedBytes;
    return payload;
  }

  /// Number of messages currently awaiting more fragments.
  int get inFlightCount => _partials.length;
}
