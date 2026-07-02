// Golden byte-identity — Dart reproduces the pinned corpus.hex (SC-002, T026).
//
// The golden `contracts/golden/corpus.hex` is authored from THIS Dart encoder
// (source of truth, R9); this test guards against drift. C# and Gleam reproduce the
// same file (their golden tests assert `encode(decode(line)) == line`).

import 'dart:io';

import 'package:glp_runtime/codec/result_envelope_codec.dart';
import 'package:test/test.dart';

import 'corpus.dart';

const goldenPath =
    '../specs/038-result-codec-and-framecodec-ride/contracts/golden/corpus.hex';

String _hex(List<int> bytes) =>
    bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();

Map<String, String> _loadGolden() {
  final file = File(goldenPath);
  final map = <String, String>{};
  for (final line in file.readAsLinesSync()) {
    final t = line.trim();
    if (t.isEmpty) continue;
    final sp = t.indexOf(' ');
    map[t.substring(0, sp)] = t.substring(sp + 1);
  }
  return map;
}

void main() {
  group('SC-002 Dart reproduces the golden corpus.hex', () {
    final golden = _loadGolden();

    test('golden names == goldenCorpus names (no drift in the pinned set)', () {
      expect(golden.keys.toSet(), equals(goldenCorpus.keys.toSet()));
    });

    goldenCorpus.forEach((name, env) {
      test(name, () {
        expect(_hex(encodeResultEnvelope(env)), equals(golden[name]),
            reason: 'Dart encoding of "$name" must match the pinned golden');
      });
    });
  });
}
