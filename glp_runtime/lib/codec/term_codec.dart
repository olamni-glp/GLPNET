// Term sub-codec — feature 038-result-codec-and-framecodec-ride.
//
// Byte primitives + Term encode/decode for the Section-15 *term* portion. Byte
// conventions are IDENTICAL to the shipped 029 `GlpRuntime.IlCodec` (`ByteIo` +
// `ConstantCodec`): unsigned LEB128 varints, fixed 8-byte little-endian int64,
// IEEE-754 double bit pattern, varint+UTF-8 strings, term tags 0x00–0x06. The one
// added tag is 0x07 (unbound VarRef → GlobalVarId), outside 029's IL scope
// (data-model §3, contract §2–§3). This is a *parallel* implementation that
// reproduces 029's bytes — it does NOT reuse 029 code (FR-007; 029 is the C#
// oracle only). Dart is the source of truth (R9).
//
// All malformed input fails loudly with ResultCodecException (FR-005, SC-004):
// truncated reads, varints over 64 bits, and unknown term tags.

import 'dart:convert';
import 'dart:typed_data';

import 'result_envelope.dart';

// --- Term tag table (contract §3 / data-model §3) ---
const int tagNull = 0x00; // 029-compat; not in the GLP envelope term model
const int tagBool = 0x01; // 029-compat; not in the GLP envelope term model
const int tagInt64 = 0x02;
const int tagDouble = 0x03; // GATED (ED-6 AtomVM /float)
const int tagString = 0x04;
const int tagAtom = 0x05;
const int tagStruct = 0x06;
const int tagVarRef = 0x07; // new vs 029

/// Append-only byte sink for the codec.
class BytesWriter {
  final BytesBuilder _b = BytesBuilder(copy: false);

  void writeByte(int v) => _b.addByte(v & 0xFF);
  void writeBytes(List<int> bytes) => _b.add(bytes);

  /// Unsigned LEB128 varint (counts / lengths).
  void writeVarUInt(int v) {
    if (v < 0) {
      throw ResultCodecException('Cannot varint-encode a negative count: $v');
    }
    var x = v;
    while (x >= 0x80) {
      writeByte((x & 0x7F) | 0x80);
      x >>= 7;
    }
    writeByte(x);
  }

  /// Fixed 8-byte little-endian int64.
  void writeInt64LE(int v) {
    final bd = ByteData(8)..setInt64(0, v, Endian.little);
    _b.add(bd.buffer.asUint8List());
  }

  /// IEEE-754 double bit pattern, 8 bytes little-endian.
  void writeDoubleBits(double d) {
    final bd = ByteData(8)..setFloat64(0, d, Endian.little);
    _b.add(bd.buffer.asUint8List());
  }

  /// Varint length + UTF-8 bytes (the "0x04-body").
  void writeString(String s) {
    final bytes = utf8.encode(s);
    writeVarUInt(bytes.length);
    _b.add(bytes);
  }

  Uint8List takeBytes() => _b.toBytes();
}

/// Cursor over an immutable byte source; every read bounds-checks and fails loudly.
class BytesReader {
  final Uint8List _data;
  int _pos = 0;

  BytesReader(this._data);

  int get position => _pos;
  int get length => _data.length;
  bool get atEnd => _pos >= _data.length;

  int readByte() {
    if (_pos >= _data.length) {
      throw const ResultCodecException(
          'Truncated payload: reached end of input before a byte was read');
    }
    return _data[_pos++];
  }

  Uint8List readBytes(int n) {
    if (n < 0) {
      throw ResultCodecException('Negative read length: $n');
    }
    if (_pos + n > _data.length) {
      throw ResultCodecException(
          'Truncated payload: need $n byte(s) but only ${_data.length - _pos} remain');
    }
    final out = Uint8List.sublistView(_data, _pos, _pos + n);
    _pos += n;
    return out;
  }

  /// Unsigned LEB128 varint; loud-fail if it exceeds 64 bits (contract §5.7).
  int readVarUInt() {
    var result = 0;
    var shift = 0;
    while (true) {
      final b = readByte();
      result |= (b & 0x7F) << shift;
      if ((b & 0x80) == 0) break;
      shift += 7;
      if (shift >= 64) {
        throw const ResultCodecException('Corrupt payload: varint exceeds 64 bits');
      }
    }
    return result;
  }

  int readInt64LE() {
    final bytes = readBytes(8);
    return ByteData.sublistView(bytes).getInt64(0, Endian.little);
  }

  double readDoubleBits() {
    final bytes = readBytes(8);
    return ByteData.sublistView(bytes).getFloat64(0, Endian.little);
  }

  String readString() {
    final len = readVarUInt();
    final bytes = readBytes(len);
    return utf8.decode(bytes);
  }
}

// --- GlobalVarId wire form (data-model §4): agentId (string body) + localId (int64 LE) ---

void encodeGlobalVarId(BytesWriter w, GlobalVarId id) {
  w.writeString(id.agentId);
  w.writeInt64LE(id.localId);
}

GlobalVarId decodeGlobalVarId(BytesReader r) {
  final agentId = r.readString();
  final localId = r.readInt64LE();
  return GlobalVarId(agentId, localId);
}

// --- Term encode/decode (contract §3) ---

void encodeTerm(BytesWriter w, Term t) {
  switch (t) {
    case ConstTerm(value: final c):
      switch (c) {
        case ConstInt(value: final n):
          w.writeByte(tagInt64);
          w.writeInt64LE(n);
        case ConstReal(value: final d):
          w.writeByte(tagDouble);
          w.writeDoubleBits(d);
        case ConstString(value: final s):
          w.writeByte(tagString);
          w.writeString(s);
        case ConstAtom(name: final a):
          w.writeByte(tagAtom);
          w.writeString(a);
      }
    case StructTerm(functor: final f, args: final args):
      w.writeByte(tagStruct);
      w.writeString(f);
      w.writeVarUInt(args.length);
      for (final arg in args) {
        encodeTerm(w, arg);
      }
    case VarRef(id: final id):
      w.writeByte(tagVarRef);
      encodeGlobalVarId(w, id);
  }
}

Term decodeTerm(BytesReader r) {
  final tag = r.readByte();
  switch (tag) {
    case tagInt64:
      return ConstTerm(ConstInt(r.readInt64LE()));
    case tagDouble:
      return ConstTerm(ConstReal(r.readDoubleBits()));
    case tagString:
      return ConstTerm(ConstString(r.readString()));
    case tagAtom:
      return ConstTerm(ConstAtom(r.readString()));
    case tagStruct:
      final functor = r.readString();
      final arity = r.readVarUInt();
      final args = <Term>[];
      for (var i = 0; i < arity; i++) {
        args.add(decodeTerm(r));
      }
      return StructTerm(functor, args);
    case tagVarRef:
      return VarRef(decodeGlobalVarId(r));
    case tagNull:
    case tagBool:
      // 0x00/0x01 are reserved in the shared 029 tag space but have no representation
      // in the 034 GLP term model (GLP booleans are atoms → 0x05). The result-envelope
      // corpus never produces them. Loud-fail rather than invent a null/bool term.
      throw ResultCodecException(
          'Term tag 0x${tag.toRadixString(16).padLeft(2, '0')} (029-reserved null/bool) '
          'has no representation in the GLP result-envelope term model');
    default:
      throw ResultCodecException(
          'Unknown term tag 0x${tag.toRadixString(16).padLeft(2, '0')}');
  }
}
