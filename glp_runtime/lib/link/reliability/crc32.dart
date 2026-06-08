import 'dart:typed_data';

/// Standard IEEE 802.3 / zlib CRC-32 (reflected, polynomial `0xEDB88320`,
/// init/final `0xFFFFFFFF`). Used by the frame codec (T021) for outer
/// integrity that the Dart serializer never had (corpus 13 §6). Chosen because it
/// is the universally-specified CRC variant, so the Dart mirror computes
/// byte-identical checksums (FR-060/061) with no shared table to keep in sync.
class Crc32 {
  Crc32._();

  static const int _polynomial = 0xEDB88320;
  static final Uint32List _table = _buildTable();

  static Uint32List _buildTable() {
    final table = Uint32List(256);
    for (int i = 0; i < 256; i++) {
      int c = i;
      for (int k = 0; k < 8; k++) {
        c = (c & 1) != 0 ? _polynomial ^ (c >> 1) : c >> 1;
      }
      table[i] = c & 0xFFFFFFFF;
    }
    return table;
  }

  /// CRC-32 of [data].
  static int compute(List<int> data) {
    int crc = 0xFFFFFFFF;
    for (final b in data) {
      crc = _table[(crc ^ b) & 0xFF] ^ (crc >> 8);
    }
    return (crc ^ 0xFFFFFFFF) & 0xFFFFFFFF;
  }
}
