---
path: lib/analysis/type_checker/type_checker.dart
name: type_checker.dart
purpose: 'The main GLP type checker: verifies a program is well-typed per Definition 4.10 via covariance (each clause) and contravariance (input coverage).'
key_idea: check() groups clauses by name/arity, runs per-clause covariance through well_typed_clause, then walks each input type's DFA automaton recursively (_checkStateCoverage) confirming every transition/alternative is matched by some clause-head term at the structural path, else emits a CoverageError.
dependencies:
- lib/analysis/type_checker/clause_validation.dart
- lib/analysis/type_checker/param_expansion.dart
- lib/analysis/type_checker/program_dfa.dart
- lib/analysis/type_checker/type_ast.dart
- lib/analysis/type_checker/type_environment_builder.dart
- lib/analysis/type_checker/well_typed_clause.dart
- lib/compiler/ast.dart
- lib/compiler/error.dart
- lib/compiler/lexer.dart
- lib/compiler/parser.dart
callers:
- lib/compiler/compiler.dart
- lib/compiler/project_linker.dart
- lib/engine/glp_engine.dart
- test/debug_negative.dart
- test/module/cssg_modules_test.dart
- test/module/module_typecheck_test.dart
- test_archive/cssn_modules_test.dart
- test_archive/social_graph_sim_modules_test.dart
mtime: '2026-05-21T12:38:12.901Z'
sha256: 1a6728683d8d3b0f7ae0e912eb459829b529ddbd1444a687da1ebb9cd560d28a
topo_level: 5
cycle_group_id: 19
status: pending
target_path: lib/analysis/type_checker/type_checker.cs
plan_started_at: '2026-05-21T16:13:11Z'
plan_completed_at: '2026-05-21T16:17:40Z'
plan_path: .codeconv/conversion-plans/lib/analysis/type_checker/type_checker.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T10:19:17Z'
target_cs_path: out/csharp/lib/analysis/type_checker/type_checker.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

The main GLP type checker: verifies a program is well-typed per Definition 4.10 via covariance (each clause) and contravariance (input coverage).
