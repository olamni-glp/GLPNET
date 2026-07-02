// US3 deref + var→writer fidelity (T033/T034/T037) — Dart is the reference (R9).
// Builds real heap structures, deep-resolves, and pins the depth-32 boundary EXACTLY:
// a 32-deep struct chain fully resolves; a 33-deep chain yields the explicit
// `$truncated` marker at depth 33 (no over/under-resolve). var→writer identity is
// preserved by GlobalVarId. Reference vectors + outcomes:
// specs/038-.../contracts/golden/deref-corpus.md (T035).

import 'package:glp_runtime/codec/result_envelope.dart';
import 'package:glp_runtime/codec/result_envelope_builder.dart';
import 'package:glp_runtime/codec/result_envelope_codec.dart';
import 'package:glp_runtime/runtime/runtime.dart';
import 'package:glp_runtime/runtime/scheduler.dart' as sched;
import 'package:glp_runtime/runtime/terms.dart' as rt;
import 'package:test/test.dart';

const instanceId = 'glpnet-test-0001';

/// Bind a fresh writer to a constant; return a reader VarRef to it.
rt.VarRef _boundConst(GlpRuntime runtime, Object? value) {
  final (w, r) = runtime.heap.allocateVariable();
  runtime.heap.bindWriterConst(w, value);
  return rt.VarRef(r);
}

/// Build a chain of [n] nested single-arg `s(·)` structs over a `ConstInt 0` leaf.
/// Returns a reader/writer VarRef to the top struct; the leaf sits at depth [n].
rt.VarRef _chain(GlpRuntime runtime, int n) {
  var child = _boundConst(runtime, 0); // leaf reader
  rt.VarRef top = child;
  for (var i = 0; i < n; i++) {
    final (w, _) = runtime.heap.allocateVariable();
    runtime.heap.bindWriterStruct(w, 's', [child]);
    top = rt.VarRef(w);
    child = top;
  }
  return top;
}

/// Number of nested single-arg `s(·)` wrappers down to the `$truncated` marker,
/// or -1 if there is no marker on the spine.
int _depthToMarker(Term t) {
  if (t is StructTerm && t.functor == r'$truncated') return 0;
  if (t is StructTerm && t.args.length == 1) {
    final inner = _depthToMarker(t.args.single);
    return inner < 0 ? -1 : 1 + inner;
  }
  return -1;
}

bool _containsTruncated(Term t) =>
    t is StructTerm &&
    (t.functor == r'$truncated' || t.args.any(_containsTruncated));

void main() {
  group('T033 nested-bound deref fidelity (≤ depth 32, no over/under-resolve)', () {
    test('a bound nested struct resolves fully, args in order', () {
      final runtime = GlpRuntime();
      final (sw, _) = runtime.heap.allocateVariable();
      runtime.heap.bindWriterStruct(
          sw, 'point', [_boundConst(runtime, 1), _boundConst(runtime, 2)]);
      final r = deepResolveTerm(runtime, rt.VarRef(sw), instanceId);
      expect(
          r,
          equals(StructTerm(
              'point', [ConstTerm(ConstInt(1)), ConstTerm(ConstInt(2))])));
    });

    test('depth 32: leaf at depth 32 resolves — NO truncation (the resolved bound)',
        () {
      final runtime = GlpRuntime();
      final r = deepResolveTerm(runtime, _chain(runtime, 32), instanceId);
      expect(_containsTruncated(r), isFalse);
      expect(_depthToMarker(r), -1);
    });
  });

  group(r'T037 depth-32 truncation-marker fidelity (marker at the exact bound)', () {
    test(r'depth 33: the $truncated marker appears at exactly depth 33', () {
      final runtime = GlpRuntime();
      final r = deepResolveTerm(runtime, _chain(runtime, 33), instanceId);
      expect(_containsTruncated(r), isTrue);
      expect(_depthToMarker(r), 33);
    });

    test(r'the $truncated marker is a normal, decodable term (never a silent cut)',
        () {
      final decoded = decodeResultEnvelope(encodeResultEnvelope(ResultEnvelope(
        status: ExecutionStatus.success,
        resolvedBindings: {'T': truncatedMarker()},
      )));
      expect(decoded.resolvedBindings['T'], equals(truncatedMarker()));
    });
  });

  group('T034 var→writer identity preserved by GlobalVarId (round-trips)', () {
    test('multiple unbound query vars → ordered var→writer by (agentId, localId)', () {
      final runtime = GlpRuntime();
      final (wx, _) = runtime.heap.allocateVariable();
      final (wy, _) = runtime.heap.allocateVariable();
      final (wz, _) = runtime.heap.allocateVariable();
      final env = buildResultEnvelope(
        runtime: runtime,
        queryVarWriters: {'X': wx, 'Y': wy, 'Z': wz},
        drainResult:
            sched.DrainResult(const [], sched.ExecutionStatus.succeeded, const []),
        instanceId: instanceId,
      );
      expect(env.varToWriter.keys.toList(), ['X', 'Y', 'Z']); // declaration order
      expect(env.varToWriter['X'], equals(GlobalVarId(instanceId, wx)));
      expect(env.varToWriter['Y'], equals(GlobalVarId(instanceId, wy)));
      expect(env.varToWriter['Z'], equals(GlobalVarId(instanceId, wz)));
      final decoded = decodeResultEnvelope(encodeResultEnvelope(env));
      expect(decoded.varToWriter, equals(env.varToWriter)); // identity survives codec
    });

    test('an unbound var nested inside a bound struct keeps its GlobalVarId', () {
      final runtime = GlpRuntime();
      final (_, unbound) = runtime.heap.allocateVariable(); // reader, never bound
      final (sw, _) = runtime.heap.allocateVariable();
      runtime.heap
          .bindWriterStruct(sw, 'pair', [_boundConst(runtime, 'a'), rt.VarRef(unbound)]);
      final r = deepResolveTerm(runtime, rt.VarRef(sw), instanceId) as StructTerm;
      final v = r.args[1] as VarRef;
      expect(v.id.agentId, instanceId); // global id, not a raw heap addr
      final decoded = decodeResultEnvelope(encodeResultEnvelope(ResultEnvelope(
          status: ExecutionStatus.success, resolvedBindings: {'P': r})));
      expect(decoded.resolvedBindings['P'], equals(r));
    });
  });
}
