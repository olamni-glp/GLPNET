// Round-trip (SC-001) + in-process-vs-bytes (US1 Acceptance #2) — feature 038.

import 'dart:typed_data';

import 'package:glp_runtime/codec/result_envelope.dart';
import 'package:glp_runtime/codec/result_envelope_codec.dart';
import 'package:test/test.dart';

import 'corpus.dart';

void main() {
  group('SC-001 round-trip decode(encode(R)) == R', () {
    nonGatedCorpus.forEach((name, env) {
      test(name, () {
        final bytes = encodeResultEnvelope(env);
        final decoded = decodeResultEnvelope(bytes);
        expect(decoded, equals(env),
            reason: 'field-by-field round-trip must reproduce the original');
        // captured value is part of round-trip equality (R4: excluded from *byte*
        // parity only, included in value round-trip)
        expect(decoded.captured, equals(env.captured));
      });
    });
  });

  group('US1 Acceptance #2 — in-process value == decoded-from-bytes value', () {
    nonGatedCorpus.forEach((name, env) {
      test(name, () {
        // the value a consumer reads in-process
        final inProcess = env;
        // the value a consumer reconstructs purely from bytes
        final fromBytes = decodeResultEnvelope(encodeResultEnvelope(env));
        expect(fromBytes, equals(inProcess));
        // distinct object graphs — the byte-decoded value shares nothing with the
        // in-process one (it was rebuilt from bytes, no heap handle carried over)
        expect(identical(fromBytes, inProcess), isFalse);
      });
    });
  });

  group('frame self-describing header', () {
    test('encodes version 0x01 + payloadType 0x11 first', () {
      final bytes = encodeResultEnvelope(
          ResultEnvelope(status: ExecutionStatus.success));
      expect(bytes[0], equals(envelopeVersion));
      expect(bytes[1], equals(payloadTypeResultEnvelope));
    });
  });

  // A single trailing-byte loud-fail smoke check (the full fuzz suite is T038).
  group('SC-004 loud-fail smoke', () {
    test('trailing bytes are rejected', () {
      final bytes = encodeResultEnvelope(
          ResultEnvelope(status: ExecutionStatus.success));
      final padded = Uint8List.fromList([...bytes, 0x00]);
      expect(() => decodeResultEnvelope(padded),
          throwsA(isA<ResultCodecException>()));
    });

    test('bad payload type is rejected', () {
      final bytes = encodeResultEnvelope(
          ResultEnvelope(status: ExecutionStatus.success));
      final corrupt = Uint8List.fromList(bytes)..[1] = 0x10; // IL program type
      expect(() => decodeResultEnvelope(corrupt),
          throwsA(isA<ResultCodecException>()));
    });
  });
}
