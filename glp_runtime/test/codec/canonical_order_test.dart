// T036 canonical serialization order: bindings / varToWriter / suspended serialize in
// the producing engine's declaration/insertion order — deterministically and identically
// across runtimes (data-model §1 parity invariant; map iteration order MUST NOT leak).
// Cross-runtime identity of this order is additionally pinned by the golden `multi_binding`
// / `var_to_writer` vectors (byte-identical across Dart/C#/Gleam).

import 'package:glp_runtime/codec/result_envelope.dart';
import 'package:glp_runtime/codec/result_envelope_codec.dart';
import 'package:test/test.dart';

void main() {
  // Non-alphabetical insertion order — if map iteration leaked, a sort would reorder these.
  final env = ResultEnvelope(
    status: ExecutionStatus.success,
    resolvedBindings: {
      'C': ConstTerm(ConstInt(3)),
      'A': ConstTerm(ConstInt(1)),
      'B': ConstTerm(ConstInt(2)),
    },
    varToWriter: {
      'Y': const GlobalVarId('a', 2),
      'X': const GlobalVarId('a', 1),
    },
  );

  group('T036 canonical serialization order', () {
    test('encode is deterministic', () {
      expect(encodeResultEnvelope(env), equals(encodeResultEnvelope(env)));
    });

    test('bindings + varToWriter keep declaration order (map order MUST NOT leak)', () {
      final decoded = decodeResultEnvelope(encodeResultEnvelope(env));
      expect(decoded.resolvedBindings.keys.toList(), ['C', 'A', 'B']);
      expect(decoded.varToWriter.keys.toList(), ['Y', 'X']);
    });
  });
}
