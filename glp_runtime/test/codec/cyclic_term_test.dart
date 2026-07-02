// T041 cyclic-term defer-to-runtime (FR-008, D5/FORK-1 OPEN): a cyclic term encodes via the
// depth-bounded deref and NEVER loops. This test asserts consistency with the runtime deref
// + the existing depth bound (R5); it does NOT define a codec-local cycle policy — that is an
// OWNER decision (D5/FORK-1), deliberately left open. The test terminating IS the no-loop
// proof; the explicit `$truncated` marker proves the bound was hit (never a silent cut).

import 'package:glp_runtime/codec/result_envelope.dart';
import 'package:glp_runtime/codec/result_envelope_builder.dart';
import 'package:glp_runtime/codec/result_envelope_codec.dart';
import 'package:glp_runtime/runtime/runtime.dart';
import 'package:glp_runtime/runtime/terms.dart' as rt;
import 'package:test/test.dart';

const instanceId = 'glpnet-test-0001';

bool _containsTruncated(Term t) =>
    t is StructTerm &&
    (t.functor == r'$truncated' || t.args.any(_containsTruncated));

void main() {
  group('T041 cyclic term — depth-bounded deref never loops', () {
    test(r'a self-referential struct resolves to $truncated at the depth bound', () {
      final runtime = GlpRuntime();
      final (w, r) = runtime.heap.allocateVariable();
      // s(Self): the struct bound to w references w's own reader → a cycle.
      runtime.heap.bindWriterStruct(w, 's', [rt.VarRef(r)]);
      final resolved = deepResolveTerm(runtime, rt.VarRef(w), instanceId);
      // terminated (no infinite loop) AND surfaced the explicit marker (never a silent cut)
      expect(_containsTruncated(resolved), isTrue);
      // the bounded term is a normal codec value that round-trips
      final decoded = decodeResultEnvelope(encodeResultEnvelope(ResultEnvelope(
          status: ExecutionStatus.success, resolvedBindings: {'C': resolved})));
      expect(decoded.resolvedBindings['C'], equals(resolved));
    });
  });
}
