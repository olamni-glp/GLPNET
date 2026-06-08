import 'dart:typed_data';

import 'crc32.dart';
import 'frame_exception.dart';

/// The on-wire frame kind.
enum FrameKind {
  /// A whole payload in one frame (fits under MTU).
  whole, // 0

  /// One fragment of a payload split across several frames (FR-022).
  fragment, // 1
}

/// A parsed, CRC-validated frame: its header fields plus its chunk bytes.
///
/// [kind]: Whole or Fragment.
/// [messageId]: Groups the fragments of one logical payload.
/// [totalLength]: Full payload byte length (identical across a message's fragments).
/// [fragIndex]: 0-based fragment position (0 for [FrameKind.whole]).
/// [fragCount]: Number of fragments (1 for [FrameKind.whole]).
/// [chunk]: This frame's payload slice (already CRC-verified).
class ParsedFrame {
  final FrameKind kind;
  final int messageId;
  final int totalLength;
  final int fragIndex;
  final int fragCount;
  final Uint8List chunk;

  const ParsedFrame(this.kind, this.messageId, this.totalLength, this.fragIndex,
      this.fragCount, this.chunk);

  @override
  bool operator ==(Object other) =>
      other is ParsedFrame &&
      kind == other.kind &&
      messageId == other.messageId &&
      totalLength == other.totalLength &&
      fragIndex == other.fragIndex &&
      fragCount == other.fragCount &&
      _bytesEqual(chunk, other.chunk);

  @override
  int get hashCode => Object.hash(
      kind, messageId, totalLength, fragIndex, fragCount, Object.hashAll(chunk));

  static bool _bytesEqual(Uint8List a, Uint8List b) {
    if (a.length != b.length) return false;
    for (int i = 0; i < a.length; i++) {
      if (a[i] != b[i]) return false;
    }
    return true;
  }
}

/// The wire-format frame codec (T021, FR-022). Wraps the opaque
/// `PayloadSerializer` blob with what corpus 13 §6 flags as ABSENT today: a
/// version byte (forward-compat), an outer length + CRC-32 (the Dart path relied
/// on in-process object integrity), and fragmentation/reassembly for under-MTU
/// leaves (CoAP ~1 KB, BLE GATT 20 B). Big-endian throughout to match the
/// serializer's endian-independent TLV and so the Dart mirror is byte-identical
/// (FR-060/061).
///
/// The `messageId` is supplied by the caller (the link's outbound counter,
/// related to the T022 sequence number), NOT generated here — encoding is
/// therefore deterministic and replayable for the seeded loopback/parity tests.
class FrameCodec {
  FrameCodec._();

  /// Current wire version. A frame with any other version byte is rejected.
  static const int version = 0x01;

  /// Fixed header size in bytes (see field layout in [encode]).
  static const int headerSize = 22;

  /// Upper bound on a single payload (FR-028 deserializer hardening): a frame
  /// advertising a larger `totalLength` is rejected before any allocation,
  /// so a forged length cannot exhaust memory.
  static const int maxPayloadBytes = 64 * 1024 * 1024;

  // Header field offsets (big-endian):
  //   0      Version   (1)
  //   1      Kind      (1)
  //   2..5   MessageId (u32)
  //   6..9   TotalLen  (u32)
  //   10..11 FragIndex (u16)
  //   12..13 FragCount (u16)
  //   14..17 ChunkCrc  (u32)  CRC-32 of the chunk bytes
  //   18..21 ChunkLen  (u32)
  //   22..   chunk
  static const int _offKind = 1;
  static const int _offMessageId = 2;
  static const int _offTotalLen = 6;
  static const int _offFragIndex = 10;
  static const int _offFragCount = 12;
  static const int _offChunkCrc = 14;
  static const int _offChunkLen = 18;

  /// Encode [payload] into one or more self-delimiting frames.
  /// A single [FrameKind.whole] frame when it fits under
  /// [maxFrameBytes] (or when that is `null`), otherwise
  /// [FrameKind.fragment] frames each ≤ the MTU.
  ///
  /// [payload]: The opaque serialized payload.
  /// [messageId]: Caller-supplied id grouping this payload's frames.
  /// [maxFrameBytes]: MTU including header, or `null` for unbounded.
  static List<Uint8List> encode(List<int> payload, int messageId,
      [int? maxFrameBytes]) {
    if (payload.length > maxPayloadBytes) {
      throw FrameException(
          'payload ${payload.length} exceeds MaxPayloadBytes $maxPayloadBytes');
    }

    final int total = payload.length;

    final bool fitsWhole =
        maxFrameBytes == null || total + headerSize <= maxFrameBytes;
    if (fitsWhole) {
      return [
        _buildFrame(FrameKind.whole, messageId, total, 0, 1, payload, 0, total)
      ];
    }

    final int maxChunk = maxFrameBytes - headerSize;
    if (maxChunk <= 0) {
      throw FrameException(
          'maxFrameBytes $maxFrameBytes too small for $headerSize-byte header');
    }

    final int fragCountL =
        total == 0 ? 1 : (total + maxChunk - 1) ~/ maxChunk;
    if (fragCountL > 0xFFFF) {
      throw FrameException(
          'payload $total needs $fragCountL fragments under MTU $maxFrameBytes (max ${0xFFFF})');
    }
    final int fragCount = fragCountL;

    final frames = <Uint8List>[];
    for (int i = 0; i < fragCount; i++) {
      final int start = i * maxChunk;
      final int len = maxChunk < total - start ? maxChunk : total - start;
      frames.add(_buildFrame(FrameKind.fragment, messageId, total, i, fragCount,
          payload, start, len));
    }
    return frames;
  }

  static Uint8List _buildFrame(FrameKind kind, int messageId, int totalLen,
      int fragIndex, int fragCount, List<int> source, int chunkStart,
      int chunkLength) {
    final frame = Uint8List(headerSize + chunkLength);
    final h = ByteData.sublistView(frame);
    frame[0] = version;
    frame[_offKind] = kind.index;
    h.setUint32(_offMessageId, messageId & 0xFFFFFFFF, Endian.big);
    h.setUint32(_offTotalLen, totalLen & 0xFFFFFFFF, Endian.big);
    h.setUint16(_offFragIndex, fragIndex & 0xFFFF, Endian.big);
    h.setUint16(_offFragCount, fragCount & 0xFFFF, Endian.big);
    final chunkView = source is Uint8List
        ? Uint8List.sublistView(source, chunkStart, chunkStart + chunkLength)
        : Uint8List.fromList(
            source.sublist(chunkStart, chunkStart + chunkLength));
    h.setUint32(_offChunkCrc, Crc32.compute(chunkView), Endian.big);
    h.setUint32(_offChunkLen, chunkLength & 0xFFFFFFFF, Endian.big);
    frame.setRange(headerSize, headerSize + chunkLength, chunkView);
    return frame;
  }

  /// Parse and validate one frame: version byte, CRC-32 over the chunk, and
  /// header/length consistency. Throws [FrameException] on any
  /// mismatch (FR-022 bad-version/bad-CRC rejected) — never a silent mis-decode.
  static ParsedFrame parseFrame(Uint8List frame) {
    if (frame.length < headerSize) {
      throw FrameException(
          'frame ${frame.length} shorter than $headerSize-byte header');
    }

    final int versionByte = frame[0];
    if (versionByte != version) {
      throw FrameException(
          'unsupported wire version 0x${_hex2(versionByte)} (expected 0x${_hex2(version)})');
    }

    final int kindByte = frame[_offKind];
    if (kindByte != FrameKind.whole.index &&
        kindByte != FrameKind.fragment.index) {
      throw FrameException('unknown frame kind $kindByte');
    }
    final kind = FrameKind.values[kindByte];

    final h = ByteData.sublistView(frame);
    final int messageId = h.getUint32(_offMessageId, Endian.big);
    final int totalLen = h.getUint32(_offTotalLen, Endian.big);
    final int fragIndex = h.getUint16(_offFragIndex, Endian.big);
    final int fragCount = h.getUint16(_offFragCount, Endian.big);
    final int chunkCrc = h.getUint32(_offChunkCrc, Endian.big);
    final int chunkLen = h.getUint32(_offChunkLen, Endian.big);

    if (totalLen > maxPayloadBytes) {
      throw FrameException(
          'advertised total length $totalLen exceeds MaxPayloadBytes $maxPayloadBytes');
    }
    if (fragCount == 0) {
      throw FrameException('fragment count 0');
    }
    if (fragIndex >= fragCount) {
      throw FrameException(
          'fragment index $fragIndex out of range for count $fragCount');
    }
    if (kind == FrameKind.whole && fragCount != 1) {
      throw FrameException('whole frame with fragment count $fragCount');
    }

    final int expectedFrameLen = headerSize + chunkLen;
    if (frame.length != expectedFrameLen) {
      throw FrameException(
          'frame length ${frame.length} != header+chunk $expectedFrameLen');
    }

    final chunk =
        Uint8List.fromList(frame.sublist(headerSize, headerSize + chunkLen));
    final int actualCrc = Crc32.compute(chunk);
    if (actualCrc != chunkCrc) {
      throw FrameException(
          'CRC mismatch: got 0x${_hex8(actualCrc)}, frame says 0x${_hex8(chunkCrc)}');
    }

    return ParsedFrame(kind, messageId, totalLen, fragIndex, fragCount, chunk);
  }

  // Mirrors C# "{value:X2}" — upper-case hex, min width 2, zero-padded.
  static String _hex2(int value) =>
      value.toRadixString(16).toUpperCase().padLeft(2, '0');

  // Mirrors C# "{value:X8}" — upper-case hex, min width 8, zero-padded.
  static String _hex8(int value) =>
      value.toRadixString(16).toUpperCase().padLeft(8, '0');
}
