---
path: test/analysis/type_checker/well_typed_clause_test.dart
name: well_typed_clause_test.dart
purpose: 'Verifies checkClause well-typed-clause checking (Def 5.7): head well-typedness, body-atom typing, variable duality/same-type conditions, and error detection.'
key_idea: Builds Stream-type TypeEnvironment + hand-built ProgramDFA/Automaton fixtures; checks valid merge head is well-typed, undefined/wrong-arity procedures yield UndefinedProcedureError, body atoms processed, and head dual plus head/body same-type variables pass.
dependencies:
- lib/analysis/type_checker/mode.dart
- lib/analysis/type_checker/moded_term.dart
- lib/analysis/type_checker/program_dfa.dart
- lib/analysis/type_checker/type_ast.dart
- lib/analysis/type_checker/well_typed_clause.dart
- lib/analysis/type_checker/well_typed_term.dart
- lib/compiler/ast.dart
callers: []
mtime: '2026-05-21T12:38:15.483Z'
sha256: e31873cea8b664586eb0f7d6e5eb81aaedb3176fb7c579c20ad5ce40d22836c5
topo_level: 4
cycle_group_id: 69
status: pending
target_path: test/analysis/type_checker/well_typed_clause_test.cs
plan_started_at: '2026-05-21T16:13:07Z'
plan_completed_at: '2026-05-21T16:17:36Z'
plan_path: .codeconv/conversion-plans/test/analysis/type_checker/well_typed_clause_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies checkClause well-typed-clause checking (Def 5.7): head well-typedness, body-atom typing, variable duality/same-type conditions, and error detection.
