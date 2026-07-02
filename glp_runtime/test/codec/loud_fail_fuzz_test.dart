// T038 loud-fail fuzz (SC-004, V4): trailing/garbage bytes, unknown term tags, corrupt
// version/payloadType/status/errorPresent, and EVERY truncation of a valid encoding MUST
// be rejected — asserts ZERO silent acceptances across the non-gated corpus.

import 'dart:typed_data';

import 'package:glp_runtime/codec/result_envelope.dart';
import 'package:glp_runtime/codec/result_envelope_codec.dart';
import 'package:test/test.dart';

import 'corpus.dart';

/// A decode that never throws to the caller — true iff the bytes were REJECTED.
bool _rejects(List<int> bytes) {
  try {
    decodeResultEnvelope(Uint8List.fromList(bytes));
    return false;
  } catch (_) {
    return true;
  }
}

void main() {
  group('T038 loud-fail fuzz — 0 silent acceptances (SC-004)', () {
    test('trailing garbage + every truncation of every corpus entry rejects', () {
      var silent = 0;
      final bad = <List<int>>[];
      for (final env in nonGatedCorpus.values) {
        final valid = encodeResultEnvelope(env).toList();
        expect(_rejects(valid), isFalse); // the valid encoding must decode
        bad.add([...valid, 0xFF]); // trailing garbage
        bad.add([...valid, 0x00, 0x01]);
        for (var k = 1; k < valid.length; k++) {
          bad.add(valid.sublist(0, k)); // every strict prefix (truncation)
        }
      }
      for (final b in bad) {
        if (!_rejects(b)) silent++;
      }
      expect(silent, 0, reason: '$silent malformed inputs silently accepted');
    });

    test('corrupt header bytes (version/payloadType/status/errorPresent) reject', () {
      // empty_success: [ver, ptype, status, 0,0,0,0, errPresent].
      final base = encodeResultEnvelope(nonGatedCorpus['empty_success']!).toList();
      List<int> withByte(int i, int v) => [...base]..[i] = v;
      var silent = 0;
      for (final v in [0x00, 0x02, 0x10, 0xFF]) {
        if (!_rejects(withByte(0, v))) silent++; // bad version
      }
      for (final p in [0x00, 0x10, 0x12, 0xFF]) {
        if (!_rejects(withByte(1, p))) silent++; // bad payloadType
      }
      for (final s in [0x03, 0x04, 0xFF]) {
        if (!_rejects(withByte(2, s))) silent++; // bad status
      }
      for (final e in [0x02, 0x05, 0xFF]) {
        if (!_rejects(withByte(7, e))) silent++; // bad errorPresent
      }
      expect(silent, 0);
    });

    test('unknown / reserved term tags reject', () {
      // success_atom: the term tag is at byte index 6 (0x05 atom).
      final base = encodeResultEnvelope(nonGatedCorpus['success_atom']!).toList();
      expect(base[6], 0x05); // guard the layout assumption
      var silent = 0;
      for (final t in [0x00, 0x08, 0x09, 0x20, 0xFF]) {
        if (!_rejects([...base]..[6] = t)) silent++;
      }
      expect(silent, 0);
    });
  });
}
