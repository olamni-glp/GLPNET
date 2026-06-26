---
path: lib/compiler/analyzer.dart
name: analyzer.dart
purpose: 'Semantic analyzer for GLP: validates SRSW single-writer/single-reader constraints, expands defined guards, and annotates clauses with per-variable register assignments.'
key_idea: Per-clause VariableTable tallies writer/reader occurrences (head/body vs guard-only); guards and constant-type proc-decls mark vars grounded to relax the multi-reader rule; SRSW is checked pre-partial-eval, then DefinedGuardEvaluator unfolds unit-clause guards via a fixpoint unification pass.
dependencies:
- lib/analysis/type_checker/type_ast.dart
- lib/compiler/ast.dart
- lib/compiler/error.dart
- lib/compiler/partial_evaluator.dart
- lib/compiler/unify_result.dart
callers:
- lib/compiler/codegen.dart
- lib/compiler/compiler.dart
- test/module/module_compiler_test.dart
mtime: '2026-05-21T12:38:13.108Z'
sha256: 531b9f57edc68a07f95f78381c3c38b6953c8506cc799a21dfec8bc73dca32d7
topo_level: 5
cycle_group_id: 40
status: pending
target_path: lib/compiler/analyzer.cs
plan_started_at: '2026-05-21T16:17:52Z'
plan_completed_at: '2026-05-21T16:23:50Z'
plan_path: .codeconv/conversion-plans/lib/compiler/analyzer.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T10:19:25Z'
target_cs_path: out/csharp/lib/compiler/analyzer.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Semantic analyzer for GLP: validates SRSW single-writer/single-reader constraints, expands defined guards, and annotates clauses with per-variable register assignments.
