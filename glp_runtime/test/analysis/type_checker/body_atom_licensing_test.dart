// glp_runtime/test/analysis/type_checker/body_atom_licensing_test.dart
//
// Unit tests for occurrence-pair licensing in body-atom mode derivation.
// Spec: docs/type system/well-typed-clause.md,
//       "Amendment to Definition 5.7 clause 2 — Occurrence-Pair Licensing"
// Acceptance matrix: specs/076-typechecker-body-atom-moding/contracts/body-atom-moding-rule.md
// Feature: 076-typechecker-body-atom-moding (closes known-issues Issue 4)
//
// The matrix rows under test:
//   1 reader X? at ↓                      -> consistent   (unchanged)
//   2 writer X  at ↑                      -> consistent   (unchanged)
//   3 writer X  at ↓, head hole for X     -> CONSISTENT   (NEW — the license)
//   4 writer X  at ↓, no head hole        -> mismatch     (unchanged)
//   5 reader X? at ↑                      -> mismatch     (unchanged, not licensed)
//   6 anonymous writer at ↓               -> mismatch     (unchanged, never paired)

import 'package:test/test.dart';
import 'package:glp_runtime/analysis/type_checker/well_typed_clause.dart';
import 'package:glp_runtime/analysis/type_checker/well_typed_term.dart';
import 'package:glp_runtime/analysis/type_checker/mode.dart';
import 'package:glp_runtime/analysis/type_checker/program_dfa.dart';
import 'package:glp_runtime/analysis/type_checker/type_ast.dart';
import 'package:glp_runtime/compiler/ast.dart' as ast;

void main() {
  // ---------------------------------------------------------------------------
  // Leaf-level: the licensing predicate itself, one test per matrix row.
  // ---------------------------------------------------------------------------
  group('checkLeafConsistency — acceptance matrix', () {
    final produced = DFAState('_', isDual: false, isFinal: true);
    final consumed = DFAState('_', isDual: true, isFinal: true);
    final dfa = ProgramDFA(
      {'_': produced, '_?': consumed},
      {'_': Automaton(produced, {}), '_?': Automaton(consumed, {})},
    );

    test('row 1: reader at consume is consistent (licensing irrelevant)', () {
      final leaf = LeafTerm.reader('X?', mode: Mode.consume);
      expect(checkLeafConsistency(leaf, consumed, dfa).isConsistent, isTrue);
      expect(
        checkLeafConsistency(leaf, consumed, dfa, licensedWriters: {'X'}).isConsistent,
        isTrue,
      );
    });

    test('row 2: writer at produce is consistent (licensing irrelevant)', () {
      final leaf = LeafTerm.writer('X', mode: Mode.produce);
      expect(checkLeafConsistency(leaf, produced, dfa).isConsistent, isTrue);
      expect(
        checkLeafConsistency(leaf, produced, dfa, licensedWriters: const <String>{}).isConsistent,
        isTrue,
      );
    });

    test('row 3 (NEW): writer at consume is consistent when licensed', () {
      final leaf = LeafTerm.writer('X', mode: Mode.consume);
      final result =
          checkLeafConsistency(leaf, consumed, dfa, licensedWriters: {'X'});
      expect(result.isConsistent, isTrue);
      expect(result.reason, isNull);
    });

    test('row 4: writer at consume is a mismatch when the licence set omits it', () {
      final leaf = LeafTerm.writer('X', mode: Mode.consume);
      // Licence set present (body atom) but this variable is not in it.
      final result =
          checkLeafConsistency(leaf, consumed, dfa, licensedWriters: {'Other'});
      expect(result.isConsistent, isFalse);
    });

    test('row 4: writer at consume is a mismatch when licensing does not apply', () {
      // licensedWriters == null is the head / standalone-term call site.
      final leaf = LeafTerm.writer('X', mode: Mode.consume);
      expect(checkLeafConsistency(leaf, consumed, dfa).isConsistent, isFalse);
    });

    test('row 5: reader at produce stays a mismatch even if the name is licensed', () {
      // The symmetric combination is deliberately NOT licensed.
      final leaf = LeafTerm.reader('X?', mode: Mode.produce);
      final result =
          checkLeafConsistency(leaf, produced, dfa, licensedWriters: {'X', 'X?'});
      expect(result.isConsistent, isFalse);
    });

    test('row 6: anonymous writer at consume stays a mismatch', () {
      // modedHead names each `_` freshly (_#1, _#2, ...), so an anonymous writer
      // can never appear in the head-hole set.
      final leaf = LeafTerm.writer('_#1', mode: Mode.consume);
      final result =
          checkLeafConsistency(leaf, consumed, dfa, licensedWriters: {'X'});
      expect(result.isConsistent, isFalse);
    });
  });

  // ---------------------------------------------------------------------------
  // Diagnostics contract (FR-006 / T012).
  // ---------------------------------------------------------------------------
  group('checkLeafConsistency — diagnostics', () {
    final produced = DFAState('_', isDual: false, isFinal: true);
    final consumed = DFAState('_', isDual: true, isFinal: true);
    final dfa = ProgramDFA(
      {'_': produced, '_?': consumed},
      {'_': Automaton(produced, {}), '_?': Automaton(consumed, {})},
    );

    test('row 4 names the surface form and the expected vs actual mode', () {
      final result = checkLeafConsistency(
          LeafTerm.writer('X', mode: Mode.consume), consumed, dfa,
          licensedWriters: const <String>{});
      expect(result.reason, contains('writer'));
      expect(result.reason, contains('requires ↑ (produce)'));
      expect(result.reason, contains('got ↓ (consume)'));
    });

    test('row 4 adds the absent-licence context', () {
      final result = checkLeafConsistency(
          LeafTerm.writer('X', mode: Mode.consume), consumed, dfa,
          licensedWriters: const <String>{});
      expect(result.reason,
          contains('no head-flipped reader pair in head licenses this occurrence'));
    });

    test('head diagnostics keep their original wording (no licence context)', () {
      // Heads pass licensedWriters == null; licensing never applies there, so the
      // absent-licence phrase would be misleading.
      final result = checkLeafConsistency(
          LeafTerm.writer('X', mode: Mode.consume), consumed, dfa);
      expect(result.reason, contains('requires ↑ (produce)'));
      expect(result.reason, isNot(contains('licenses this occurrence')));
    });

    test('row 5 carries no licence context (it is not a licensable combination)', () {
      final result = checkLeafConsistency(
          LeafTerm.reader('X?', mode: Mode.produce), produced, dfa,
          licensedWriters: const <String>{});
      expect(result.reason, contains('reader'));
      expect(result.reason, isNot(contains('licenses this occurrence')));
    });
  });

  // ---------------------------------------------------------------------------
  // Clause-level: the head-hole evidence is derived and threaded correctly.
  // ---------------------------------------------------------------------------
  group('checkClause — licensing end to end', () {
    ast.Goal goal(String f, List<ast.Term> args) => ast.Goal(f, args, 0, 0);
    ast.VarTerm writer(String name) => ast.VarTerm(name, false, 0, 0);
    ast.VarTerm reader(String name) => ast.VarTerm(name, true, 0, 0);
    ast.ConstTerm konst(String v) => ast.ConstTerm(v, 0, 0);

    /// hole/1  : procedure hole(_).   — argument at produce, the head's output hole
    /// capture/1: procedure capture(_?). — argument at consume, the licensed target
    /// emit/1  : procedure emit(_).   — argument at produce (row 2 control)
    TypeEnvironment wildcardEnv() {
      final env = TypeEnvironment.empty();
      env.addProcedure(
          ProcDecl('hole', [PrimitiveModeAlt(false, 0, 0)], 0, 0));
      env.addProcedure(
          ProcDecl('capture', [PrimitiveModeAlt(true, 0, 0)], 0, 0));
      env.addProcedure(
          ProcDecl('emit', [PrimitiveModeAlt(false, 0, 0)], 0, 0));
      return env;
    }

    ProgramDFA wildcardDFA() {
      final produced = DFAState('_', isDual: false, isFinal: true);
      final consumed = DFAState('_', isDual: true, isFinal: true);
      final finalState = DFAState('_FINAL_', isDual: false, isFinal: true);
      return ProgramDFA(
        {'_': produced, '_?': consumed, '_FINAL_': finalState},
        {'_': Automaton(produced, {}), '_?': Automaton(consumed, {})},
      );
    }

    test('licensed: head hole + body writer at a consume position type-checks', () {
      // hole(X?) :- capture(X).
      final result = checkClause(
        TypedClause(
          head: goal('hole', [reader('X')]),
          bodyAtoms: [goal('capture', [writer('X')])],
        ),
        wildcardDFA(),
        wildcardEnv(),
      );
      expect(result.errors.map((e) => e.message).join('\n'), isEmpty);
      expect(result.isWellTyped, isTrue);
    });

    test('unlicensed: no head hole, so the same body atom is rejected', () {
      // hole(ok) :- capture(X).   — X has no head occurrence at all
      final result = checkClause(
        TypedClause(
          head: goal('hole', [konst('ok')]),
          bodyAtoms: [goal('capture', [writer('X')])],
        ),
        wildcardDFA(),
        wildcardEnv(),
      );
      expect(result.isWellTyped, isFalse);
      expect(result.errors.map((e) => e.message).join('\n'),
          contains('no head-flipped reader pair in head licenses this occurrence'));
    });

    test('unlicensed: head occurrence at a consume position is not a hole', () {
      // capture(X) :- capture(X).
      // The head writer X sits at a consume position, so it is recorded under the
      // reader-form key X? — not the writer-form key the licence requires.
      final result = checkClause(
        TypedClause(
          head: goal('capture', [writer('X')]),
          bodyAtoms: [goal('capture', [writer('X')])],
        ),
        wildcardDFA(),
        wildcardEnv(),
      );
      expect(result.isWellTyped, isFalse);
    });

    test('row 2 control: writer at a produce position still type-checks', () {
      // hole(X?) :- emit(X).   — the pre-076 workaround shape, unchanged
      final result = checkClause(
        TypedClause(
          head: goal('hole', [reader('X')]),
          bodyAtoms: [goal('emit', [writer('X')])],
        ),
        wildcardDFA(),
        wildcardEnv(),
      );
      expect(result.isWellTyped, isTrue);
    });

    test('head-occurrence records are never rewritten by licensing', () {
      // The head hole X? is complemented to a writer at produce (Definition 5.5).
      // The licensed body occurrence is a writer at consume; if licensing rewrote
      // the head record, X would come back as consume/reader here.
      final result = checkClause(
        TypedClause(
          head: goal('hole', [reader('X')]),
          bodyAtoms: [goal('capture', [writer('X')])],
        ),
        wildcardDFA(),
        wildcardEnv(),
      );
      final info = result.variableTypes['X'];
      expect(info, isNotNull);
      expect(info!.mode, equals(Mode.produce));
      expect(info.isReader, isFalse);
    });

    test('head-head bind pattern is unaffected (dual-type path unchanged)', () {
      // bind(X, X?) under procedure bind(_?, _): both occurrences are in the head,
      // so Definition 5.7 clause 3's dual-type rule decides it, not the licence.
      final env = TypeEnvironment.empty();
      env.addProcedure(ProcDecl(
        'bind',
        [PrimitiveModeAlt(true, 0, 0), PrimitiveModeAlt(false, 0, 0)],
        0,
        0,
      ));
      final result = checkClause(
        TypedClause(head: goal('bind', [writer('X'), reader('X')])),
        wildcardDFA(),
        env,
      );
      expect(result.isWellTyped, isTrue);
      expect(result.variableTypes.containsKey('X'), isTrue);
      expect(result.variableTypes.containsKey('X?'), isTrue);
    });
  });

  // ---------------------------------------------------------------------------
  // Parameterized procedures: licensing sits AFTER call-site instantiation
  // (Case B, _inferConcreteDecl), and the inference-failure skip is unchanged.
  // ---------------------------------------------------------------------------
  group('checkClause — parameterized (Case B) path', () {
    ast.Goal goal(String f, List<ast.Term> args) => ast.Goal(f, args, 0, 0);
    ast.VarTerm writer(String name) => ast.VarTerm(name, false, 0, 0);
    ast.VarTerm reader(String name) => ast.VarTerm(name, true, 0, 0);
    ast.ConstTerm konst(String v) => ast.ConstTerm(v, 0, 0);

    final rProduce = DFAState('R', isDual: false, isFinal: false);
    final rConsume = DFAState('R', isDual: true, isFinal: false);
    final finalState = DFAState('_FINAL_', isDual: false, isFinal: true);
    final produced = DFAState('_', isDual: false, isFinal: true);
    final consumed = DFAState('_', isDual: true, isFinal: true);

    ProgramDFA paramDFA() => ProgramDFA(
          {
            'R': rProduce,
            'R?': rConsume,
            '_': produced,
            '_?': consumed,
            '_FINAL_': finalState,
          },
          {
            'R': Automaton(rProduce, {
              (rProduce, TransitionLabel.constant('ok')): finalState,
            }),
            'R?': Automaton(rConsume, {
              (rConsume, TransitionLabel.constant('ok')): finalState,
            }),
            '_': Automaton(produced, {}),
            '_?': Automaton(consumed, {}),
          },
        );

    /// rhole/1 : procedure rhole(R).            — caller head, produce
    /// psink/1 : procedure psink(T?).           — PARAMETERIZED template
    TypeEnvironment paramEnv() {
      final env = TypeEnvironment.empty();
      env.addType(TypeDef('R', [ConstantAlt('ok', 0, 0)], 0, 0));
      env.addProcedure(ProcDecl(
          'rhole', [TypeRef('R', 0, 0, isInput: false)], 0, 0));
      final template = ProcDecl(
        'psink',
        [TypeRef('T', 0, 0, isInput: true)],
        0,
        0,
        typeParams: const ['T'],
      );
      env.addProcedure(template);
      env.paramProcDecls[template.key] = template;
      return env;
    }

    test('licensing applies after type-parameter instantiation', () {
      // rhole(X?) :- psink(X).
      // T binds to R from the caller's head record, giving the concrete decl
      // psink(R?) — a consume position — where the writer X is licensed.
      final result = checkClause(
        TypedClause(
          head: goal('rhole', [reader('X')]),
          bodyAtoms: [goal('psink', [writer('X')])],
        ),
        paramDFA(),
        paramEnv(),
      );
      expect(result.errors.map((e) => e.message).join('\n'), isEmpty);
      expect(result.isWellTyped, isTrue);
      // Instantiation really happened: the body atom's moded term was built.
      expect(result.modedBodyAtoms, isNotEmpty);
    });

    test('inference-failure skip path is behaviourally unchanged', () {
      // rhole(ok) :- psink(Y).
      // The head binds no variables, so there are no caller types to infer T
      // from; the body atom is skipped (Case A covers psink's own clauses) and
      // no moded term is produced. Licensing must not change that.
      final result = checkClause(
        TypedClause(
          head: goal('rhole', [konst('ok')]),
          bodyAtoms: [goal('psink', [writer('Y')])],
        ),
        paramDFA(),
        paramEnv(),
      );
      expect(result.isWellTyped, isTrue);
      expect(result.modedBodyAtoms, isEmpty);
    });
  });
}
