// No-heap-address (SC-003 / data-model V1) — feature 038.
//
// Reconstruct every field from bytes (no heap handle) and assert that no field,
// transitively, references a live heap address. The envelope's only variable
// representation is GlobalVarId (global identity agentId:localId), never a raw
// local heap addr (R7). This is the property that makes the front/back ED-1 seam
// real: a consumer reads the full result with zero access to the producer's heap.

import 'package:glp_runtime/codec/result_envelope.dart';
import 'package:glp_runtime/codec/result_envelope_codec.dart';
import 'package:test/test.dart';

import 'corpus.dart';

/// Walk a term; assert every node is a value-type term and every variable is a
/// GlobalVarId (not a raw heap addr). Returns the count of variable references seen.
int _assertTermNoHeap(Term t) {
  switch (t) {
    case ConstTerm(value: final c):
      // a Constant is one of the four ground kinds — never a heap handle
      expect(c, isA<Constant>());
      return 0;
    case StructTerm(args: final args):
      var vars = 0;
      for (final arg in args) {
        vars += _assertTermNoHeap(arg);
      }
      return vars;
    case VarRef(id: final id):
      // the ONLY var representation is a global identity, not a local heap address
      expect(id, isA<GlobalVarId>());
      expect(id.agentId, isA<String>());
      expect(id.localId, isA<int>());
      return 1;
  }
}

void main() {
  group('SC-003 — no live heap address in any reconstructed envelope', () {
    nonGatedCorpus.forEach((name, env) {
      test(name, () {
        // reconstruct purely from bytes — a consumer with no heap handle
        final decoded = decodeResultEnvelope(encodeResultEnvelope(env));

        // every binding term is heap-independent; every var is a GlobalVarId
        decoded.resolvedBindings.forEach((_, term) => _assertTermNoHeap(term));

        // var→writer references are global identities
        decoded.varToWriter.forEach((_, id) {
          expect(id, isA<GlobalVarId>());
        });

        // suspended set entries are global identities
        for (final id in decoded.suspended) {
          expect(id, isA<GlobalVarId>());
        }

        // the reconstructed value equals the original yet shares no object graph
        expect(decoded, equals(env));
        expect(identical(decoded, env), isFalse);
      });
    });

    test('a var→writer envelope reconstructs the writer by global id, 0 heap addrs', () {
      final env = nonGatedCorpus['var_to_writer']!;
      final decoded = decodeResultEnvelope(encodeResultEnvelope(env));
      var totalVars = 0;
      decoded.resolvedBindings.forEach((_, term) {
        totalVars += _assertTermNoHeap(term);
      });
      expect(totalVars, equals(1), reason: 'exactly one unbound VarRef in the corpus entry');
      expect(decoded.varToWriter['W'], equals(const GlobalVarId('agent1', 9)));
    });
  });
}
