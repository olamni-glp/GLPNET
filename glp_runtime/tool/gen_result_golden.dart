// Golden-corpus generator (T029) — feature 038-result-codec-and-framecodec-ride.
//
// Encodes the byte-parity subset (`goldenCorpus`: non-gated entries, empty captured)
// from the Dart SOURCE OF TRUTH (R9) and emits `<name> <hex>` lines. The output is
// pinned at specs/038-result-codec-and-framecodec-ride/contracts/golden/corpus.hex;
// C# and Gleam reproduce it byte-for-byte (SC-002).
//
// Run (from glp_runtime/):
//   dart run tool/gen_result_golden.dart > ../specs/038-result-codec-and-framecodec-ride/contracts/golden/corpus.hex

import 'package:glp_runtime/codec/result_envelope_codec.dart';

import '../test/codec/corpus.dart';

String _hex(List<int> bytes) =>
    bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();

void main() {
  // Deterministic, name-sorted so the golden is stable across regenerations.
  final names = goldenCorpus.keys.toList()..sort();
  for (final name in names) {
    final bytes = encodeResultEnvelope(goldenCorpus[name]!);
    print('$name ${_hex(bytes)}');
  }
}
