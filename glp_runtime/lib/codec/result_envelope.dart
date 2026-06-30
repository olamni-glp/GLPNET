// Result-Envelope value types — feature 038-result-codec-and-framecodec-ride.
//
// The heap-independent result envelope (the result side of the ratified ED-1 seam)
// and the wire-level term model it carries. These are pure, immutable value types:
// no field, transitively, holds a live heap address (SC-003 / data-model §1 invariant
// is structural — there is simply no heap-handle type reachable from here).
//
// The term model mirrors the canonical 034 Gleam model (glp/runtime/terms.gleam,
// data-model §2): Constant = ConstAtom|ConstInt|ConstReal|ConstString;
// Term = ConstTerm(Constant) | StructTerm(functor,args) | VarRef(GlobalVarId).
// Unlike 034's local `VarRef(addr:Int)`, the envelope's VarRef carries a global
// identity (GlobalVarId, data-model §3 tag 0x07) so it is meaningful cross-process.
//
// Dart is the source of truth for the cross-runtime byte codec (R9); C# and Gleam
// mirror these types and reproduce the Dart-authored golden corpus.

import 'dart:typed_data';

/// Loud-fail exception for any malformed input or out-of-model value (FR-005, SC-004).
/// Mirrors C# `ResultCodecException` / Gleam `CodecError`.
class ResultCodecException implements Exception {
  final String message;
  const ResultCodecException(this.message);
  @override
  String toString() => 'ResultCodecException: $message';
}

/// Execution status of a result (data-model §1; wire byte in contract §4).
enum ExecutionStatus {
  success(0x00),
  suspended(0x01),
  failed(0x02);

  final int wireByte;
  const ExecutionStatus(this.wireByte);

  static ExecutionStatus fromWireByte(int b) {
    switch (b) {
      case 0x00:
        return ExecutionStatus.success;
      case 0x01:
        return ExecutionStatus.suspended;
      case 0x02:
        return ExecutionStatus.failed;
      default:
        throw ResultCodecException(
            'Corrupt envelope: status byte 0x${b.toRadixString(16).padLeft(2, '0')} not in {0x00,0x01,0x02}');
    }
  }
}

/// Global, heap-independent identity of a GLP variable's writer (data-model §4, R7).
/// Identity is `(agentId, localId)` — never a local heap address (which is meaningless
/// cross-process, DISPOSITIONS #15). Scheme `agentId:localId` (FB-M1-14).
class GlobalVarId {
  final String agentId;

  /// 64-bit local id (maps from the producing engine's local writer addr).
  final int localId;

  const GlobalVarId(this.agentId, this.localId);

  @override
  bool operator ==(Object other) =>
      other is GlobalVarId &&
      other.agentId == agentId &&
      other.localId == localId;

  @override
  int get hashCode => Object.hash(agentId, localId);

  @override
  String toString() => '$agentId:$localId';
}

/// A GLP ground constant — the four kinds (mirrors 034 `terms.gleam` Constant, FR-001).
sealed class Constant {
  const Constant();
}

/// An atom (GLP identifier; GLP booleans `true`/`false` are atoms). Wire tag 0x05.
class ConstAtom extends Constant {
  final String name;
  const ConstAtom(this.name);

  @override
  bool operator ==(Object other) => other is ConstAtom && other.name == name;
  @override
  int get hashCode => Object.hash(ConstAtom, name);
  @override
  String toString() => "ConstAtom('$name')";
}

/// A 64-bit integer. Wire tag 0x02 (fixed 8-byte little-endian).
class ConstInt extends Constant {
  final int value;
  const ConstInt(this.value);

  @override
  bool operator ==(Object other) => other is ConstInt && other.value == value;
  @override
  int get hashCode => Object.hash(ConstInt, value);
  @override
  String toString() => 'ConstInt($value)';
}

/// A double. Wire tag 0x03 (IEEE-754 bit pattern). **GATED** (ED-6 AtomVM /float).
class ConstReal extends Constant {
  final double value;
  const ConstReal(this.value);

  @override
  // bit-exact equality so NaN / -0.0 round-trip compares correctly
  bool operator ==(Object other) =>
      other is ConstReal && _doubleBitsEqual(value, other.value);
  @override
  int get hashCode => Object.hash(ConstReal, _doubleBits(value));
  @override
  String toString() => 'ConstReal($value)';
}

int _doubleBits(double d) =>
    (ByteData(8)..setFloat64(0, d, Endian.little)).getInt64(0, Endian.little);

bool _doubleBitsEqual(double a, double b) => _doubleBits(a) == _doubleBits(b);

/// A string literal. Wire tag 0x04 (varint length + UTF-8).
class ConstString extends Constant {
  final String value;
  const ConstString(this.value);

  @override
  bool operator ==(Object other) =>
      other is ConstString && other.value == value;
  @override
  int get hashCode => Object.hash(ConstString, value);
  @override
  String toString() => "ConstString('$value')";
}

/// A GLP term (mirrors 034 `terms.gleam` Term). The codec's wire-level term model.
///
/// A deep-resolved binding never holds a *bound* VarRef (it is resolved away);
/// a remaining VarRef is an **unbound** variable, encoded by its GlobalVarId.
sealed class Term {
  const Term();
}

/// A constant term (atom/int/real/string). Wire tags 0x02–0x05 by inner kind.
class ConstTerm extends Term {
  final Constant value;
  const ConstTerm(this.value);

  @override
  bool operator ==(Object other) => other is ConstTerm && other.value == value;
  @override
  int get hashCode => Object.hash(ConstTerm, value);
  @override
  String toString() => 'ConstTerm($value)';
}

/// A compound term: functor + arity + recursive args. Wire tag 0x06.
/// Lists are `StructTerm(".", [head, tail])`; `nil` is `ConstTerm(ConstAtom("nil"))`.
class StructTerm extends Term {
  final String functor;
  final List<Term> args;
  const StructTerm(this.functor, this.args);

  @override
  bool operator ==(Object other) =>
      other is StructTerm &&
      other.functor == functor &&
      _termListEquals(args, other.args);
  @override
  int get hashCode => Object.hash(StructTerm, functor, Object.hashAll(args));
  @override
  String toString() => '$functor(${args.join(",")})';
}

/// An unbound variable reference by global identity. Wire tag 0x07.
class VarRef extends Term {
  final GlobalVarId id;
  const VarRef(this.id);

  @override
  bool operator ==(Object other) => other is VarRef && other.id == id;
  @override
  int get hashCode => Object.hash(VarRef, id);
  @override
  String toString() => 'Var($id)';
}

/// The heap-independent result envelope (FR-001, data-model §1).
///
/// `resolvedBindings` / `varToWriter` are **ordered** (insertion order is the
/// producing engine's canonical declaration/binding order — the parity invariant;
/// map iteration order MUST NOT leak). `suspended` is an ordered, `goal_id`-deduped
/// list of blocking-reader / suspended-goal ids (R10). `captured` is carried but
/// EXCLUDED from the byte-parity criterion (R4). `error` is present iff status==failed.
class ResultEnvelope {
  final ExecutionStatus status;
  final Map<String, Term> resolvedBindings;
  final Map<String, GlobalVarId> varToWriter;
  final List<GlobalVarId> suspended;
  final Uint8List captured;
  final String? error;

  ResultEnvelope({
    required this.status,
    Map<String, Term>? resolvedBindings,
    Map<String, GlobalVarId>? varToWriter,
    List<GlobalVarId>? suspended,
    Uint8List? captured,
    this.error,
  })  : resolvedBindings = resolvedBindings ?? <String, Term>{},
        varToWriter = varToWriter ?? <String, GlobalVarId>{},
        suspended = suspended ?? <GlobalVarId>[],
        captured = captured ?? Uint8List(0);

  @override
  bool operator ==(Object other) =>
      other is ResultEnvelope &&
      other.status == status &&
      _orderedMapTermEquals(resolvedBindings, other.resolvedBindings) &&
      _orderedMapVarEquals(varToWriter, other.varToWriter) &&
      _varListEquals(suspended, other.suspended) &&
      _bytesEqual(captured, other.captured) &&
      other.error == error;

  @override
  int get hashCode => Object.hash(
        status,
        Object.hashAll(resolvedBindings.keys),
        Object.hashAll(varToWriter.keys),
        Object.hashAll(suspended),
        error,
      );

  @override
  String toString() =>
      'ResultEnvelope(status: $status, bindings: $resolvedBindings, '
      'varToWriter: $varToWriter, suspended: $suspended, '
      'capturedLen: ${captured.length}, error: $error)';
}

// --- deep-equality helpers (value semantics for round-trip field-by-field) ---

bool _termListEquals(List<Term> a, List<Term> b) {
  if (a.length != b.length) return false;
  for (var i = 0; i < a.length; i++) {
    if (a[i] != b[i]) return false;
  }
  return true;
}

bool _varListEquals(List<GlobalVarId> a, List<GlobalVarId> b) {
  if (a.length != b.length) return false;
  for (var i = 0; i < a.length; i++) {
    if (a[i] != b[i]) return false;
  }
  return true;
}

/// Order-sensitive map equality (canonical order is part of the value / parity).
bool _orderedMapTermEquals(Map<String, Term> a, Map<String, Term> b) {
  if (a.length != b.length) return false;
  final ka = a.keys.toList();
  final kb = b.keys.toList();
  for (var i = 0; i < ka.length; i++) {
    if (ka[i] != kb[i]) return false;
    if (a[ka[i]] != b[kb[i]]) return false;
  }
  return true;
}

bool _orderedMapVarEquals(Map<String, GlobalVarId> a, Map<String, GlobalVarId> b) {
  if (a.length != b.length) return false;
  final ka = a.keys.toList();
  final kb = b.keys.toList();
  for (var i = 0; i < ka.length; i++) {
    if (ka[i] != kb[i]) return false;
    if (a[ka[i]] != b[kb[i]]) return false;
  }
  return true;
}

bool _bytesEqual(Uint8List a, Uint8List b) {
  if (a.length != b.length) return false;
  for (var i = 0; i < a.length; i++) {
    if (a[i] != b[i]) return false;
  }
  return true;
}
