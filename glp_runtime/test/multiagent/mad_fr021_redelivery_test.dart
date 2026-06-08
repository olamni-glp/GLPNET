/// Regression tests for FR-021 / SC-008: duplicate (redelivered) madGLP
/// assignment is a VERIFIED no-op, not a crash and not a swallowed error.
///
/// Background: at-least-once delivery on a reconnecting link can replay the
/// same `_w(p,i) := T` / `_r(p,i) := T` after its GlobalizeEntry/LocalizeEntry
/// has already been consumed. The first delivery binds the writer and removes
/// the entry. A second delivery for the SAME (recognized) name must:
///   - not throw,
///   - not re-bind (the entry is gone; bindVariable is never reached),
///   - leave the table state unchanged.
/// A delivery for a genuinely-unknown index/key (never delivered) must still
/// throw StateError — SC-008 forbids swallowing real errors.

import 'package:test/test.dart';
import 'package:glp_runtime/runtime/runtime.dart';
import 'package:glp_runtime/runtime/terms.dart';
import 'package:glp_runtime/multiagent/mad_context.dart';
import 'package:glp_runtime/multiagent/mad_helpers.dart';
import 'package:glp_runtime/multiagent/global_send.dart';

void main() {
  group('FR-021/SC-008: redelivered writer assignment', () {
    test('second delivery of _w(p,i) is a verified no-op; unknown index throws',
        () {
      final runtimeP = GlpRuntime();
      final ctxP = MadContext(agentId: 'p', runtime: runtimeP);

      // p globalizes writer V -> GlobalizeEntry (V, q) at index 1, no spawn.
      final (writerV, readerV) = runtimeP.heap.allocateVariable();
      final g = globalize(
        variables: [TermVar.writer(writerV, readerAddr: readerV)],
        localAgent: 'p',
        remoteAgent: 'q',
        table: ctxP.wp,
      );
      final gnW = g.globalNames[0]; // _w(p,1)
      expect(ctxP.wp.lookupByIndex(1), isNotNull);

      // First delivery: binds V, removes the entry.
      ctxP.handleMadAssignment(
        globalName: gnW,
        value: ConstTerm('result'),
        fromAgent: 'q',
      );
      expect(ctxP.wp.lookupByIndex(1), isNull); // entry consumed
      expect((runtimeP.heap.derefAddr(writerV) as ConstTerm).value, 'result');

      // Second delivery of the SAME name: verified no-op (no throw, no re-bind).
      expect(
        () => ctxP.handleMadAssignment(
          globalName: gnW,
          value: ConstTerm('result'),
          fromAgent: 'q',
        ),
        returnsNormally,
      );
      expect(ctxP.wp.lookupByIndex(1), isNull); // still consumed
      expect((runtimeP.heap.derefAddr(writerV) as ConstTerm).value, 'result');

      // Genuinely-unknown index (never delivered): must still surface.
      expect(
        () => ctxP.handleMadAssignment(
          globalName: GlobalName.writer('p', 99),
          value: ConstTerm('x'),
          fromAgent: 'q',
        ),
        throwsStateError,
      );
    });
  });

  group('FR-021/SC-008: redelivered reader assignment', () {
    test('second delivery of _r(p,i) is a verified no-op; unknown key throws',
        () {
      final runtimeP = GlpRuntime();
      final runtimeQ = GlpRuntime();
      final ctxP = MadContext(agentId: 'p', runtime: runtimeP);
      final ctxQ = MadContext(agentId: 'q', runtime: runtimeQ);

      // p globalizes reader Xs? -> spawn, no entry; globalName _r(p,1).
      final (writerXs, readerXs) = runtimeP.heap.allocateVariable();
      final g = globalize(
        variables: [TermVar.reader(readerXs, writerAddr: writerXs)],
        localAgent: 'p',
        remoteAgent: 'q',
        table: ctxP.wp,
      );
      final gnR = g.globalNames[0]; // _r(p,1)

      // q localizes _r(p,1) -> LocalizeEntry (Z_q, p, 1), writer Z_q.
      final lr = localize(
        globalNames: g.globalNames,
        localAgent: 'q',
        table: ctxQ.wp,
        freshAddrAllocator: () => runtimeQ.heap.allocateVariable(),
      );
      final writerZq = lr.freshPairs[0].writerAddr;
      expect(ctxQ.wp.findByRemote('p', 1), isNotNull);

      // First delivery: binds Z_q, removes the entry.
      ctxQ.handleMadAssignment(
        globalName: gnR,
        value: ConstTerm('done'),
        fromAgent: 'p',
      );
      expect(ctxQ.wp.findByRemote('p', 1), isNull); // entry consumed
      expect((runtimeQ.heap.derefAddr(writerZq) as ConstTerm).value, 'done');

      // Second delivery of the SAME name: verified no-op.
      expect(
        () => ctxQ.handleMadAssignment(
          globalName: gnR,
          value: ConstTerm('done'),
          fromAgent: 'p',
        ),
        returnsNormally,
      );
      expect(ctxQ.wp.findByRemote('p', 1), isNull); // still consumed
      expect((runtimeQ.heap.derefAddr(writerZq) as ConstTerm).value, 'done');

      // Genuinely-unknown (remoteAgent, remoteIndex): must still surface.
      expect(
        () => ctxQ.handleMadAssignment(
          globalName: GlobalName.reader('p', 99),
          value: ConstTerm('x'),
          fromAgent: 'p',
        ),
        throwsStateError,
      );
    });
  });
}
