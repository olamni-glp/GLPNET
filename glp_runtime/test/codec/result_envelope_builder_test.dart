// Engine→envelope builder + deep-resolve (T017/T018/T019) — feature 038.
//
// Exercises the deep-resolve walk over a REAL runtime heap (allocate + bind), the
// envelope builder (bound→resolvedBindings, unbound→var→writer, blockingReaders→
// suspended), and the end-to-end build→encode→decode round-trip.

import 'package:glp_runtime/codec/result_envelope.dart';
import 'package:glp_runtime/codec/result_envelope_builder.dart';
import 'package:glp_runtime/codec/result_envelope_codec.dart';
import 'package:glp_runtime/runtime/runtime.dart';
import 'package:glp_runtime/runtime/terms.dart' as rt;
import 'package:glp_runtime/runtime/scheduler.dart' as sched;
import 'package:test/test.dart';

const instanceId = 'glpnet-test-0001';

/// Bind a fresh writer to a constant and return a reader VarRef to it (mirrors the
/// engine's struct-arg construction: bind writer, reference via reader).
rt.VarRef _boundConst(GlpRuntime runtime, Object? value) {
  final (writerId, readerId) = runtime.heap.allocateVariable();
  runtime.heap.bindWriterConst(writerId, value);
  return rt.VarRef(readerId);
}

void main() {
  group('T017 deep-resolve over the heap', () {
    test('resolves a bound atom (String → ConstAtom, 0x05)', () {
      final runtime = GlpRuntime();
      final (w, _) = runtime.heap.allocateVariable();
      runtime.heap.bindWriterConst(w, 'foo');
      expect(deepResolveTerm(runtime, rt.VarRef(w), instanceId),
          equals(ConstTerm(ConstAtom('foo'))));
    });

    test('resolves a bound int', () {
      final runtime = GlpRuntime();
      final (w, _) = runtime.heap.allocateVariable();
      runtime.heap.bindWriterConst(w, 42);
      expect(deepResolveTerm(runtime, rt.VarRef(w), instanceId),
          equals(ConstTerm(ConstInt(42))));
    });

    test('resolves a nested struct, recursing into args', () {
      final runtime = GlpRuntime();
      final a1 = _boundConst(runtime, 1);
      final a2 = _boundConst(runtime, 2);
      final (sw, _) = runtime.heap.allocateVariable();
      runtime.heap.bindWriterStruct(sw, 'point', [a1, a2]);
      expect(
          deepResolveTerm(runtime, rt.VarRef(sw), instanceId),
          equals(StructTerm(
              'point', [ConstTerm(ConstInt(1)), ConstTerm(ConstInt(2))])));
    });

    test('an unbound nested var resolves to a global VarRef (not a heap addr)', () {
      final runtime = GlpRuntime();
      final a1 = _boundConst(runtime, 'a');
      final (_, unboundReader) = runtime.heap.allocateVariable(); // never bound
      final (sw, _) = runtime.heap.allocateVariable();
      runtime.heap.bindWriterStruct(sw, 'pair', [a1, rt.VarRef(unboundReader)]);

      final resolved = deepResolveTerm(runtime, rt.VarRef(sw), instanceId);
      expect(resolved, isA<StructTerm>());
      final s = resolved as StructTerm;
      expect(s.functor, 'pair');
      expect(s.args[0], equals(ConstTerm(ConstAtom('a'))));
      expect(s.args[1], isA<VarRef>());
      expect((s.args[1] as VarRef).id.agentId, instanceId);
    });

    test(r'depth bound yields the explicit $truncated marker (never silent)', () {
      final runtime = GlpRuntime();
      final (w, _) = runtime.heap.allocateVariable();
      runtime.heap.bindWriterConst(w, 'x');
      expect(
          deepResolveTerm(runtime, rt.VarRef(w), instanceId,
              depth: deepResolveDepth + 1),
          equals(truncatedMarker()));
      // and the marker is itself a normal decodable term
      final decoded =
          decodeResultEnvelope(encodeResultEnvelope(ResultEnvelope(
        status: ExecutionStatus.success,
        resolvedBindings: {'T': truncatedMarker()},
      )));
      expect(decoded.resolvedBindings['T'], equals(truncatedMarker()));
    });
  });

  group('T018 buildResultEnvelope', () {
    test('success with a bound binding → resolvedBindings, no var→writer', () {
      final runtime = GlpRuntime();
      final (w, _) = runtime.heap.allocateVariable();
      runtime.heap.bindWriterConst(w, 'done');
      final env = buildResultEnvelope(
        runtime: runtime,
        queryVarWriters: {'X': w},
        drainResult: sched.DrainResult(const [], sched.ExecutionStatus.succeeded, const []),
        instanceId: instanceId,
      );
      expect(env.status, ExecutionStatus.success);
      expect(env.resolvedBindings['X'], equals(ConstTerm(ConstAtom('done'))));
      expect(env.varToWriter, isEmpty);
    });

    test('unbound query var → var→writer global id, not a binding', () {
      final runtime = GlpRuntime();
      final (w, _) = runtime.heap.allocateVariable(); // unbound
      final env = buildResultEnvelope(
        runtime: runtime,
        queryVarWriters: {'Y': w},
        drainResult: sched.DrainResult(const [], sched.ExecutionStatus.succeeded, const []),
        instanceId: instanceId,
      );
      expect(env.resolvedBindings, isEmpty);
      expect(env.varToWriter['Y'], equals(GlobalVarId(instanceId, w)));
    });

    test('suspended status + blocking readers → sorted suspended set', () {
      final runtime = GlpRuntime();
      final env = buildResultEnvelope(
        runtime: runtime,
        queryVarWriters: const {},
        // blockingReaders is a Set<int> (already deduped); the builder sorts it
        drainResult: sched.DrainResult(
            const [], sched.ExecutionStatus.suspended, const ['goal'], {5, 3}),
        instanceId: instanceId,
      );
      expect(env.status, ExecutionStatus.suspended);
      expect(
          env.suspended,
          equals([GlobalVarId(instanceId, 3), GlobalVarId(instanceId, 5)]));
    });

    test('T019 end-to-end: a built envelope round-trips through the codec', () {
      final runtime = GlpRuntime();
      final a1 = _boundConst(runtime, 1);
      final a2 = _boundConst(runtime, 'two'); // String → ConstAtom (owner-rule #2)
      final (sw, _) = runtime.heap.allocateVariable();
      runtime.heap.bindWriterStruct(sw, 'pair', [a1, a2]);
      final env = buildResultEnvelope(
        runtime: runtime,
        queryVarWriters: {'P': sw},
        drainResult: sched.DrainResult(const [], sched.ExecutionStatus.succeeded, const []),
        instanceId: instanceId,
      );
      final decoded = decodeResultEnvelope(encodeResultEnvelope(env));
      expect(decoded, equals(env));
      // no heap address leaked: the binding is a fully-resolved value term
      expect(decoded.resolvedBindings['P'],
          equals(StructTerm('pair',
              [ConstTerm(ConstInt(1)), ConstTerm(ConstAtom('two'))])));
    });
  });
}
