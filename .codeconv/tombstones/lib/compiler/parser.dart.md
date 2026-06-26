---
path: lib/compiler/parser.dart
name: parser.dart
purpose: 'Recursive-descent parser turning the token stream into the AST: a Module/Program of type definitions, procedure declarations, and clauses (head, guards, body).'
key_idea: Recursive descent plus a precedence-climbing (Pratt) expression parser driven by a _precedence table; splits guards from body at '|', enforces clause contiguity and pending procedure-declaration rules, backtracks via saved _current, and runs a parallel term-parse path for '::=' type alternatives.
dependencies:
- lib/analysis/type_checker/prelude.dart
- lib/analysis/type_checker/type_ast.dart
- lib/analysis/type_checker/type_conversion.dart
- lib/compiler/ast.dart
- lib/compiler/error.dart
- lib/compiler/token.dart
callers:
- lib/analysis/type_checker/type_checker.dart
- lib/analysis/type_checker/type_environment_builder.dart
- lib/compiler/compiler.dart
- lib/compiler/partial_evaluator.dart
- lib/compiler/pmt/validator.dart
- lib/compiler/project_linker.dart
- lib/engine/glp_engine.dart
- lib/multiagent/agent_runtime.dart
- lib/runtime/module_hierarchy.dart
- test/compiler/partial_evaluator_test.dart
- test/debug_negative.dart
- test/module/cssg_modules_test.dart
- test/module/module_compiler_test.dart
- test/module/module_hierarchy_test.dart
- test/module/module_parser_test.dart
- test/module/module_syntax_v2_test.dart
- test_archive/cssn_modules_test.dart
- test_archive/social_graph_sim_modules_test.dart
mtime: '2026-05-21T12:38:13.266Z'
sha256: d5b6f4a7c81d0dcfd0fb32be8b28f7da3d3b77dc84571a10f063188114b2e9eb
topo_level: 3
cycle_group_id: 15
status: pending
target_path: lib/compiler/parser.cs
plan_started_at: '2026-05-21T15:24:18Z'
plan_completed_at: '2026-05-21T16:00:09Z'
plan_path: .codeconv/conversion-plans/lib/compiler/parser.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T10:03:50Z'
target_cs_path: out/csharp/lib/compiler/parser.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Recursive-descent parser turning the token stream into the AST: a Module/Program of type definitions, procedure declarations, and clauses (head, guards, body).
