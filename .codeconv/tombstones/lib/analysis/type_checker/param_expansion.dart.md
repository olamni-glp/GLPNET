---
path: lib/analysis/type_checker/param_expansion.dart
name: param_expansion.dart
purpose: Monomorphizes a Module's parameterized types (Def 8.1) by expanding each concrete instantiation into a distinct monomorphic type definition before automaton construction.
key_idea: Separates templates from monomorphic defs, collects instantiations from type-def bodies and proc decls, then worklist-expands each by substituting type params and renaming to Name<Args>. Parameterized proc decls keep a preserved template; all-wildcard args collapse to the base name (Stream(_) == Stream).
dependencies:
- lib/analysis/type_checker/type_ast.dart
- lib/compiler/ast.dart
callers:
- lib/analysis/type_checker/type_checker.dart
- lib/analysis/type_checker/type_environment_builder.dart
- lib/compiler/project_linker.dart
- lib/engine/glp_engine.dart
- lib/runtime/module_hierarchy.dart
mtime: '2026-05-21T12:38:12.793Z'
sha256: c716e6969f9947cf137f59e5a597ce359d062829a4a3c6f810b76d263c83a64c
topo_level: 2
cycle_group_id: 8
status: pending
target_path: lib/analysis/type_checker/param_expansion.cs
plan_started_at: '2026-05-21T14:58:30Z'
plan_completed_at: '2026-05-21T15:09:12Z'
plan_path: .codeconv/conversion-plans/lib/analysis/type_checker/param_expansion.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T09:24:51Z'
target_cs_path: out/csharp/lib/analysis/type_checker/param_expansion.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Monomorphizes a Module's parameterized types (Def 8.1) by expanding each concrete instantiation into a distinct monomorphic type definition before automaton construction.
