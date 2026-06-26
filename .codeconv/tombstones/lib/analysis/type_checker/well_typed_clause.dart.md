---
path: lib/analysis/type_checker/well_typed_clause.dart
name: well_typed_clause.dart
purpose: 'Checks a GLP clause is well-typed (Definition 5.7): the moded head, each body atom, and cross-clause variable-pair duality/subtyping.'
key_idea: 'checkClause builds moded head/body terms, checks each arg''s paths against its type automaton, then _checkClauseDuality applies location rules: head-head pairs must be dual, body-body need writer<:reader subtyping, head-body must share base type; Case B infers concrete parameterized proc decls at call sites.'
dependencies:
- lib/analysis/type_checker/mode.dart
- lib/analysis/type_checker/moded_head.dart
- lib/analysis/type_checker/moded_term.dart
- lib/analysis/type_checker/prelude.dart
- lib/analysis/type_checker/program_dfa.dart
- lib/analysis/type_checker/subtyping.dart
- lib/analysis/type_checker/type_ast.dart
- lib/analysis/type_checker/well_typed_term.dart
- lib/compiler/ast.dart
callers:
- lib/analysis/type_checker/type_checker.dart
- test/analysis/type_checker/well_typed_clause_test.dart
mtime: '2026-05-21T12:38:12.957Z'
sha256: 66445ae92069c7cdf6bc5871f1666b696eabd8a80a08118cb5114b32fe6cc918
topo_level: 3
cycle_group_id: 18
status: pending
target_path: lib/analysis/type_checker/well_typed_clause.cs
plan_started_at: '2026-05-21T15:24:17Z'
plan_completed_at: '2026-05-21T16:00:08Z'
plan_path: .codeconv/conversion-plans/lib/analysis/type_checker/well_typed_clause.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T09:56:48Z'
target_cs_path: out/csharp/lib/analysis/type_checker/well_typed_clause.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Checks a GLP clause is well-typed (Definition 5.7): the moded head, each body atom, and cross-clause variable-pair duality/subtyping.
