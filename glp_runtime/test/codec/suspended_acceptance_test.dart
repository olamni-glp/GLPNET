// US1 Acceptance #3 (T025): a suspended goal emits Status=suspended + the blocking-
// reader set, and no heap address leaks — the blocking readers and any remaining
// variable are GlobalVarId(agentId, localId), never a bare heap address. Codec-level
// assertion over the shared corpus (survives encode → decode).

import 'package:glp_runtime/codec/result_envelope.dart';
import 'package:glp_runtime/codec/result_envelope_codec.dart';
import 'package:test/test.dart';

import 'corpus.dart';

void main() {
  group('T025 suspended-status acceptance (Acceptance #3)', () {
    test('suspended: status + blocking-reader set survive encode/decode', () {
      final env = nonGatedCorpus['suspended']!;
      final decoded = decodeResultEnvelope(encodeResultEnvelope(env));

      expect(decoded.status, ExecutionStatus.suspended);
      expect(decoded.suspended,
          const [GlobalVarId('agent1', 3), GlobalVarId('agent2', 5)]);
      // no heap-address leak: every blocking reader is a global id (agentId:localId)
      for (final id in decoded.suspended) {
        expect(id.agentId, isNotEmpty);
      }
    });

    test('suspended_with_binding: partial binding + var→writer + blocking reader', () {
      final env = nonGatedCorpus['suspended_with_binding']!;
      final decoded = decodeResultEnvelope(encodeResultEnvelope(env));

      expect(decoded.status, ExecutionStatus.suspended);
      expect(decoded.suspended, const [GlobalVarId('agent1', 11)]);
      expect(decoded.varToWriter['Q'], const GlobalVarId('agent1', 11));
      // the remaining variable inside the binding is a VarRef carrying a GlobalVarId,
      // never a raw heap address.
      final partial = decoded.resolvedBindings['Partial']! as StructTerm;
      final inner = partial.args.single as VarRef;
      expect(inner.id, const GlobalVarId('agent1', 11));
    });
  });
}
