// Runtime-defined guard clause specifications (feature 049 Deliverable A, form (a)/(a1)).
//
// §1.14 ruling recorded in specs/049-wave1-guard-link-acceptance/spec.md Clarifications
// (2026-07-08): form (a) is realized by the (a1) compiler extension — a declared guard
// procedure whose clauses are all test-only (body empty or `true`; guards drawn from the
// builtin subset or recursively test-only) compiles to a side table of clause specs,
// evaluated three-valued at runtime by the Guard opcode handler (suspension on unbound
// readers). Design: specs/049-wave1-guard-link-acceptance/evidence/guard/a1-design.md.

/// Neutral term shape for guard clause specs. Lists are encoded as '.'/2
/// structures with a 'nil' terminator, matching the runtime representation.
sealed class GTerm {
  const GTerm();
}

class GConst extends GTerm {
  final Object? value;
  const GConst(this.value);
  @override
  String toString() => 'GConst($value)';
}

class GVar extends GTerm {
  final String name;
  final bool isReader;
  const GVar(this.name, this.isReader);

  /// Anonymous variables ('_'-prefixed) match anything and bind nothing;
  /// each occurrence is independent.
  bool get isAnonymous => name.startsWith('_');

  @override
  String toString() => isReader ? '$name?' : name;
}

class GStruct extends GTerm {
  final String functor;
  final List<GTerm> args;
  const GStruct(this.functor, this.args);
  int get arity => args.length;
  @override
  String toString() => '$functor(${args.join(",")})';
}

/// One guard conjunct of a test-only clause: a builtin-subset guard
/// (ground/1, known/1, =?=/2 — negation per the builtin's own verdict matrix)
/// or a recursive call to another runtime-defined guard (never negated).
class GGuardSpec {
  final String predicate;
  final List<GTerm> args;
  final bool negated;
  const GGuardSpec(this.predicate, this.args, {this.negated = false});
  String get key => '$predicate/${args.length}';
  @override
  String toString() => '${negated ? '~' : ''}$predicate(${args.join(",")})';
}

/// One clause of a runtime-defined guard: head argument patterns + guard
/// conjuncts. Test-only by construction (body empty or `true`), so no body
/// is represented.
class GuardClauseSpec {
  final List<GTerm> headArgs;
  final List<GGuardSpec> guards;
  const GuardClauseSpec(this.headArgs, this.guards);
}

/// A runtime-defined guard procedure (keyed as "name/arity" in the
/// BytecodeProgram.definedGuards side table).
class GuardProcSpec {
  final String name;
  final int arity;
  final List<GuardClauseSpec> clauses;
  const GuardProcSpec(this.name, this.arity, this.clauses);
  String get key => '$name/$arity';
}
