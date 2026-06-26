---
path: lib/analysis/type_checker/type_ast.dart
name: type_ast.dart
purpose: Defines the AST node hierarchy for GLP type declarations and procedure signatures, plus the TypeEnvironment that holds them for type checking.
key_idea: TypeExpr subclasses (TypeRef/ConstantAlt/StructAlt/List*Alt/PrimitiveModeAlt/DiffListAlt) model type alternatives; TypeRef.dual() inverts mode T<->T?; TypeDef.classification scans for internal complementation; ProcDecl keys procedures by name/arity; TypeEnvironment maps and merges types & procedures.
dependencies: []
callers:
- lib/analysis/type_checker/moded_head.dart
- lib/analysis/type_checker/param_expansion.dart
- lib/analysis/type_checker/program_dfa.dart
- lib/analysis/type_checker/type_checker.dart
- lib/analysis/type_checker/type_conversion.dart
- lib/analysis/type_checker/type_environment_builder.dart
- lib/analysis/type_checker/well_typed_clause.dart
- lib/compiler/analyzer.dart
- lib/compiler/ast.dart
- lib/compiler/compiler.dart
- lib/compiler/parser.dart
- lib/compiler/project_linker.dart
- lib/engine/glp_engine.dart
- lib/runtime/module_hierarchy.dart
- test/analysis/type_checker/moded_head_test.dart
- test/analysis/type_checker/well_typed_clause_test.dart
- test/module/cssg_modules_test.dart
- test/module/module_hierarchy_test.dart
- test/module/module_syntax_v2_test.dart
- test_archive/cssn_modules_test.dart
- test_archive/social_graph_sim_modules_test.dart
mtime: '2026-05-21T12:38:12.880Z'
sha256: f80349aefb8cc777764548f29d5c6bc663809f9dfffde921c141ae2f7028d38a
topo_level: 0
cycle_group_id: 1
status: ready
target_path: lib/analysis/type_checker/type_ast.cs
plan_started_at: '2026-05-23T09:31:48Z'
plan_completed_at: '2026-05-23T09:31:49Z'
plan_path: .codeconv/conversion-plans/lib/analysis/type_checker/type_ast.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T08:54:01Z'
target_cs_path: out/csharp/lib/analysis/type_checker/type_ast.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Defines the AST node hierarchy for GLP type declarations and procedure signatures, plus the TypeEnvironment that holds them for type checking.
