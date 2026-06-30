/// Lightweight, dependency-free in-language logic IL on the 4 ratified primitives
/// (DECISIONS.md Fork B = b2: "lightweight in-language IR ... verifiers are simple
/// structural checks"). The four primitives are fixed by MLIR-GLP-DIALECT.md §2:
///   head_unify / guard_test / body_spawn / suspend_reactivate.
///
/// This IL sits exactly where DECISIONS.md places it:
///   ... analyze -> 4-primitive logic-IL (+ verifiers) -> v2.16.3 bytecode ...
/// It consumes the glp_runtime AST (read-only) + the analyzer's register
/// assignment, and is lowered to a glp_runtime BytecodeProgram by lowering.dart.
library;

import 'package:glp_runtime/compiler/ast.dart';

/// The three executable phases, ordered. suspend_reactivate is metadata
/// (carried by the head), not a phase in the linear op stream.
enum IlPhase { head, guard, body }

/// Base of the (phased) IL ops. Exactly the first three dialect primitives are
/// phased ops in the linear stream; suspend_reactivate is attached to the clause.
abstract class IlOp {
  IlPhase get phase;
}

/// **head_unify** — tentative unification of one goal argument against the
/// corresponding clause-head argument position (writer/reader binding under the
/// writer-MGU). Carries the AST term + its argument slot; lowered by walking the
/// term exactly as glp_runtime CodeGenerator._generateHeadArgument does.
class HeadUnify extends IlOp {
  final int argSlot;
  final Term term;
  HeadUnify(this.argSlot, this.term);
  @override
  IlPhase get phase => IlPhase.head;
  @override
  String toString() => 'head_unify(A$argSlot, $term)';
}

/// **guard_test** — a pure, side-effect-free guard test gating commitment.
class GuardTest extends IlOp {
  final Guard guard;
  GuardTest(this.guard);
  @override
  IlPhase get phase => IlPhase.guard;
  @override
  String toString() => 'guard_test($guard)';
}

/// **body_spawn** — spawn a concurrent body goal after commitment.
class BodySpawn extends IlOp {
  final Goal goal;
  BodySpawn(this.goal);
  @override
  IlPhase get phase => IlPhase.body;
  @override
  String toString() => 'body_spawn($goal)';
}

/// **suspend_reactivate** — metadata recording the input-reader positions whose
/// unbound state makes the HEAD match suspend (and reactivate when their paired
/// writer binds). In the v2.16.3 ISA this primitive emits NO dedicated opcode:
/// suspension is the three-valued runtime behaviour of the HEAD ops against an
/// unbound reader, finalized by the procedure-trailing NoMoreClauses (Si≠∅ ⇒
/// suspend). We keep it explicit so it is analyzable (the dialect's intent).
class SuspendReactivate {
  /// Names of variables that occur in reader mode anywhere in the HEAD, plus a
  /// flag for whether any head argument is a structure (HeadStructure can
  /// suspend when its goal argument is an unbound reader).
  final List<String> headReaderVars;
  final bool headHasStructureMatch;
  SuspendReactivate(this.headReaderVars, this.headHasStructureMatch);
  @override
  String toString() =>
      'suspend_reactivate(readers=${headReaderVars.join(",")}, structMatch=$headHasStructureMatch)';
}

/// One clause expressed in the IL. `ops` is the linear, phase-ordered stream of
/// the three executable primitives; `suspend` is the head-carried metadata.
class IlClause {
  final String procSig; // e.g. "merge/3"
  final int clauseIndex;
  final List<IlOp> ops;
  final SuspendReactivate suspend;

  IlClause({
    required this.procSig,
    required this.clauseIndex,
    required this.ops,
    required this.suspend,
  });

  List<HeadUnify> get headOps => ops.whereType<HeadUnify>().toList();
  List<GuardTest> get guardOps => ops.whereType<GuardTest>().toList();
  List<BodySpawn> get bodyOps => ops.whereType<BodySpawn>().toList();

  String render() {
    final b = StringBuffer();
    b.writeln('IlClause $procSig #$clauseIndex');
    for (final op in ops) {
      b.writeln('  [${op.phase.name}] $op');
    }
    b.writeln('  $suspend');
    return b.toString();
  }
}

/// A single variable occurrence harvested from the IL for SRSW checking.
class VarOccurrence {
  final String name;
  final bool isReader;
  final IlPhase phase;
  VarOccurrence(this.name, this.isReader, this.phase);
  @override
  String toString() => '$name${isReader ? "?" : ""}@${phase.name}';
}

/// Walk a term, collecting every (name, isReader) variable occurrence.
/// Anonymous `_`/`_?` are skipped (SRSW-exempt, per glp_runtime analyzer:
/// "Skip anonymous variables — they don't participate in SRSW").
void collectTermVars(Term term, IlPhase phase, List<VarOccurrence> out) {
  if (term is VarTerm) {
    out.add(VarOccurrence(term.name, term.isReader, phase));
  } else if (term is ListTerm) {
    if (term.head != null) collectTermVars(term.head!, phase, out);
    if (term.tail != null) collectTermVars(term.tail!, phase, out);
  } else if (term is StructTerm) {
    for (final a in term.args) {
      collectTermVars(a, phase, out);
    }
  }
  // ConstTerm / UnderscoreTerm: no SRSW-tracked variable.
}

/// Collect all SRSW-relevant variable occurrences from a clause's IL
/// (head_unify + body_spawn args; guard occurrences excluded, matching the
/// glp_runtime analyzer's head/body counting for SRSW satisfaction).
List<VarOccurrence> collectClauseVars(IlClause c) {
  final out = <VarOccurrence>[];
  for (final h in c.headOps) {
    collectTermVars(h.term, IlPhase.head, out);
  }
  for (final s in c.bodyOps) {
    for (final a in s.goal.args) {
      collectTermVars(a, IlPhase.body, out);
    }
  }
  return out;
}
