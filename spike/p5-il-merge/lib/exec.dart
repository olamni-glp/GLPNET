/// Shared execution-equivalence harness: runs goal `merge(As?, Bs?, Cs)` against
/// a given BytecodeProgram on the real glp_runtime runner+scheduler, observing
/// suspend-on-unbound-reader then reactivate+commit-on-bind. Mirrors the
/// goal-setup path of GlpEngine._runSingleGoal (read-only reuse of glp_runtime).
library;

import 'package:glp_runtime/bytecode/runner.dart';
import 'package:glp_runtime/runtime/runtime.dart';
import 'package:glp_runtime/runtime/scheduler.dart';
import 'package:glp_runtime/runtime/machine_state.dart' show GoalRef;
import 'package:glp_runtime/runtime/terms.dart' as rt;

class RunOutcome {
  final String suspendStatus; // status after first drain
  final List<String> suspendedGoals;
  final bool blockedOnInputReader; // is the goal's As? reader among blockers
  final String postBindStatus; // status after binding As? + re-drain
  final String csBinding; // dereferenced Cs after re-drain
  RunOutcome(this.suspendStatus, this.suspendedGoals, this.blockedOnInputReader,
      this.postBindStatus, this.csBinding);
  @override
  String toString() => '  drain#1 status        : $suspendStatus\n'
      '  suspended goals       : $suspendedGoals\n'
      '  blocked on input As?  : $blockedOnInputReader\n'
      '  bind As?=[a|As1?] then re-drain:\n'
      '  drain#2 status        : $postBindStatus\n'
      '  Cs binding            : $csBinding';
}

Future<RunOutcome> runMergeGoal(BytecodeProgram program) async {
  final rtm = GlpRuntime();
  final (wA, rA) = rtm.heap.allocateVariable(); // As (As? unbound reader)
  final (_, rB) = rtm.heap.allocateVariable(); // Bs (Bs? unbound reader)
  final (wC, _) = rtm.heap.allocateVariable(); // Cs (writer output)

  final argSlots = <int, rt.Term>{
    0: rt.VarRef(rA),
    1: rt.VarRef(rB),
    2: rt.VarRef(wC),
  };

  const goalId = 1;
  rtm.setGoalEnv(goalId, CallEnv(args: argSlots));
  rtm.setGoalProgram(goalId, 'main');

  final runner = BytecodeRunner(program);
  final scheduler = Scheduler(rt: rtm, runners: {'main': runner});
  scheduler.resetDisplayNumbering();
  scheduler.setQueryVarNames({'Cs': wC});

  final entryPC = program.labels['merge/3'];
  rtm.gq.enqueue(GoalRef(goalId, entryPC!));

  final d1 =
      await scheduler.drainAsyncWithStatus(maxCycles: 1000, showBindings: false);
  final blockedOnAs = d1.blockingReaders.contains(rA);

  // Bind As? : writer wA := [a | As1?]
  final (waA, raA) = rtm.heap.allocateVariable();
  rtm.heap.bindWriterConst(waA, 'a');
  final (_, rAs1) = rtm.heap.allocateVariable();
  final acts =
      rtm.heap.bindWriterStruct(wA, '.', [rt.VarRef(raA), rt.VarRef(rAs1)]);
  for (final g in acts) {
    rtm.enqueueReactivatedGoal(g);
  }

  final d2 =
      await scheduler.drainAsyncWithStatus(maxCycles: 1000, showBindings: false);

  final cs = rtm.heap.isBound(wC)
      ? fmtHeap(rtm, rtm.heap.dereference(rt.VarRef(wC)))
      : '<unbound>';

  return RunOutcome(
      d1.status.name, d1.suspendedGoals, blockedOnAs, d2.status.name, cs);
}

String fmtHeap(GlpRuntime rtm, rt.Term term, [Set<int>? seen]) {
  seen ??= <int>{};
  if (term is rt.VarRef) {
    if (seen.contains(term.addr)) return 'X${term.addr}(cyc)';
    seen.add(term.addr);
    final d = rtm.heap.dereference(term);
    if (d is rt.VarRef) {
      return rtm.heap.isReader(d.addr) ? 'X${d.addr}?' : 'X${d.addr}';
    }
    return fmtHeap(rtm, d, seen);
  }
  if (term is rt.ConstTerm) {
    if (term.value == null || term.value == 'nil') return '[]';
    return term.value.toString();
  }
  if (term is rt.StructTerm && term.functor == '.' && term.args.length == 2) {
    final h = fmtHeap(rtm, term.args[0], seen);
    final t = fmtHeap(rtm, term.args[1], seen);
    return '[$h | $t]';
  }
  if (term is rt.StructTerm) {
    return '${term.functor}(${term.args.map((a) => fmtHeap(rtm, a, seen)).join(", ")})';
  }
  return term.toString();
}
