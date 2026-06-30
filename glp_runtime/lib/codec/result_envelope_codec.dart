// Result-envelope frame codec — feature 038-result-codec-and-framecodec-ride.
//
// Wires the 2-byte frame header (version 0x01 + payloadType 0x11 RESULT_ENVELOPE)
// and the length-prefixed section framing (status, bindings, varToWriter, suspended,
// captured, error) over the term sub-codec. Mirrors the 029 PayloadHeader discipline
// (029 IL programs are payloadType 0x10; the envelope takes the next value 0x11) so
// envelopes and IL programs are never confusable on a shared wire (R3).
//
// decode MUST consume every byte or fail loudly (FR-005, SC-004): bad version,
// bad payloadType, bad status, bad errorPresent, truncation, trailing bytes,
// unknown term tag, or an over-64-bit varint all raise ResultCodecException.

import 'dart:typed_data';

import 'result_envelope.dart';
import 'term_codec.dart';

/// Codec format version (matches shipped 029 PayloadHeader.Version).
const int envelopeVersion = 0x01;

/// Payload-type discriminant for a result envelope (029 IL program = 0x10; next = 0x11).
const int payloadTypeResultEnvelope = 0x11;

Uint8List encodeResultEnvelope(ResultEnvelope env) {
  final w = BytesWriter();

  w.writeByte(envelopeVersion);
  w.writeByte(payloadTypeResultEnvelope);
  w.writeByte(env.status.wireByte);

  // resolvedBindings — canonical (insertion) order
  w.writeVarUInt(env.resolvedBindings.length);
  env.resolvedBindings.forEach((name, term) {
    w.writeString(name);
    encodeTerm(w, term);
  });

  // varToWriter — canonical (insertion) order
  w.writeVarUInt(env.varToWriter.length);
  env.varToWriter.forEach((name, id) {
    w.writeString(name);
    encodeGlobalVarId(w, id);
  });

  // suspended — canonical order, deduped by goal_id at build time
  w.writeVarUInt(env.suspended.length);
  for (final id in env.suspended) {
    encodeGlobalVarId(w, id);
  }

  // captured — carried but excluded from the byte-parity criterion (R4)
  w.writeVarUInt(env.captured.length);
  w.writeBytes(env.captured);

  // error — present iff status==failed
  if (env.error != null) {
    w.writeByte(0x01);
    w.writeString(env.error!);
  } else {
    w.writeByte(0x00);
  }

  return w.takeBytes();
}

ResultEnvelope decodeResultEnvelope(Uint8List bytes) {
  final r = BytesReader(bytes);

  final version = r.readByte();
  if (version != envelopeVersion) {
    throw ResultCodecException(
        'Unsupported envelope version 0x${version.toRadixString(16).padLeft(2, '0')} '
        '(expected 0x${envelopeVersion.toRadixString(16).padLeft(2, '0')})');
  }
  final payloadType = r.readByte();
  if (payloadType != payloadTypeResultEnvelope) {
    throw ResultCodecException(
        'Unexpected payload type 0x${payloadType.toRadixString(16).padLeft(2, '0')} '
        '(expected RESULT_ENVELOPE 0x${payloadTypeResultEnvelope.toRadixString(16).padLeft(2, '0')})');
  }

  final status = ExecutionStatus.fromWireByte(r.readByte());

  final bindingsCount = r.readVarUInt();
  final resolvedBindings = <String, Term>{};
  for (var i = 0; i < bindingsCount; i++) {
    final name = r.readString();
    final term = decodeTerm(r);
    resolvedBindings[name] = term;
  }

  final varToWriterCount = r.readVarUInt();
  final varToWriter = <String, GlobalVarId>{};
  for (var i = 0; i < varToWriterCount; i++) {
    final name = r.readString();
    final id = decodeGlobalVarId(r);
    varToWriter[name] = id;
  }

  final suspendedCount = r.readVarUInt();
  final suspended = <GlobalVarId>[];
  for (var i = 0; i < suspendedCount; i++) {
    suspended.add(decodeGlobalVarId(r));
  }

  final capturedLen = r.readVarUInt();
  final captured = Uint8List.fromList(r.readBytes(capturedLen));

  final errorPresent = r.readByte();
  String? error;
  if (errorPresent == 0x01) {
    error = r.readString();
  } else if (errorPresent != 0x00) {
    throw ResultCodecException(
        'Corrupt envelope: errorPresent flag 0x${errorPresent.toRadixString(16).padLeft(2, '0')} '
        '(expected 0x00 or 0x01)');
  }

  if (!r.atEnd) {
    throw ResultCodecException(
        'Corrupt envelope: ${r.length - r.position} unconsumed trailing byte(s) after envelope');
  }

  return ResultEnvelope(
    status: status,
    resolvedBindings: resolvedBindings,
    varToWriter: varToWriter,
    suspended: suspended,
    captured: captured,
    error: error,
  );
}
