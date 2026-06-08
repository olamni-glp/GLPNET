/// Regression test for FR-035 / SC-009: a goal suspended on a genuinely
/// writerless IMPORTED reader must reactivate exactly once when the value
/// arrives through the madGLP assignment ingress.
///
/// Background: `allocateImportedReader` mints a reader cell whose suspensions
/// live in `VariableEntry.suspensions`, drained ONLY by `bindImportedReader`.
/// The ingress `handleMadAssignment` previously called `bindVariable`, which
/// never touches `VariableEntry.suspensions` — so a guard suspended on an
/// imported reader would NEVER reactivate (FR-051 violation). The fix routes
/// the three ingress bind sites through the heap seam `bindAny`, which detects
/// an unbound imported reader and dispatches to `bindImportedReader`.
///
/// (This path has no GLP surface yet — the future link layer mints imported
/// readers per hop — so it is exercised here at the ingress/heap level.)

import 'package:test/test.dart';
import 'package:glp_runtime/runtime/runtime.dart';
import 'package:glp_runtime/runtime/terms.dart';
import 'package:glp_runtime/runtime/suspension.dart';
import 'package:glp_runtime/multiagent/mad_context.dart';
import 'package:glp_runtime/multiagent/mad_helpers.dart';
import 'package:glp_runtime/multiagent/variable_table.dart';

void main() {
  group('FR-035/SC-009: imported-reader reactivation via ingress', () {
    test('handleMadAssignment wakes a goal suspended on an imported reader, once',
        () {
      final runtimeQ = GlpRuntime();
      final ctxQ = MadContext(agentId: 'q', runtime: runtimeQ);

      // q holds a writerless IMPORTED reader (the representation the future link
      // layer mints per hop) with a guard suspended on it.
      final r = runtimeQ.heap.allocateImportedReader();
      runtimeQ.heap.cells[r].content =
          VariableEntry(varId: r, isReader: true, creator: 'q');
      const goalId = 4242;
      const resumePc = 99;
      runtimeQ.heap.suspendOnReader(r, SuspensionRecord(goalId, resumePc));

      // The localize side recorded a LocalizeEntry whose target IS the imported
      // reader (writerAddr == r): an assignment _r(p,1) := T must bind it.
      ctxQ.wp.addLocalizeEntry(r, 'p', 1);
      expect(runtimeQ.gq.isEmpty, true);

      // Deliver the value through the madGLP ingress.
      ctxQ.handleMadAssignment(
        globalName: GlobalName.reader('p', 1),
        value: ConstTerm('done'),
        fromAgent: 'p',
      );

      // The imported reader is bound and the suspended goal reactivated once.
      expect(runtimeQ.heap.isReaderBound(r), true);
      expect((runtimeQ.heap.derefAddr(r) as ConstTerm).value, 'done');
      expect(runtimeQ.gq.length, 1);
      expect(runtimeQ.gq.dequeue()!.id, goalId);

      // Re-delivery of the same assignment is a verified no-op (FR-021 dedup):
      // no double-reactivation, no throw.
      expect(
        () => ctxQ.handleMadAssignment(
          globalName: GlobalName.reader('p', 1),
          value: ConstTerm('done'),
          fromAgent: 'p',
        ),
        returnsNormally,
      );
      expect(runtimeQ.gq.isEmpty, true); // nothing re-enqueued
    });

    test('local-pair (non-imported) ingress path is unchanged', () {
      // A local writer target must still bind via bindVariable (no imported
      // path), proving bindAny does not disturb the existing representation.
      final runtimeQ = GlpRuntime();
      final ctxQ = MadContext(agentId: 'q', runtime: runtimeQ);

      final (writer, reader) = runtimeQ.heap.allocateVariable();
      ctxQ.wp.addLocalizeEntry(writer, 'p', 2);

      ctxQ.handleMadAssignment(
        globalName: GlobalName.reader('p', 2),
        value: ConstTerm('ok'),
        fromAgent: 'p',
      );

      expect(runtimeQ.heap.isReaderBound(reader), true);
      expect((runtimeQ.heap.derefAddr(writer) as ConstTerm).value, 'ok');
    });
  });
}
