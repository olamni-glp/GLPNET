---
path: lib/compiler/project_linker.dart
name: project_linker.dart
purpose: 'Project linker: static linking of multi-module GLP projects.


  Given a project root directory, discovers all modules, type-checks each

  independently, then produces a single flat Program AST where all

  inter-module calls are resolved to renamed local procedures.


  Specification: docs/modules/glp-project-compilation-spec.md

  Plan: docs/modules/project-compilation-implementation-plan.md

  '
key_idea: 'Project linker: static linking of multi-module GLP projects.


  Given a project root directory, discovers all modules, type-checks each

  independently, then produces a single flat Program AST where all

  inter-module calls are resolved to renamed local procedures.


  Specification: docs/modules/glp-project-compilation-spec.md

  Plan: docs/modules/project-compilation-implementation-plan.md

  '
dependencies:
- lib/analysis/type_checker/param_expansion.dart
- lib/analysis/type_checker/type_ast.dart
- lib/analysis/type_checker/type_checker.dart
- lib/analysis/type_checker/type_environment_builder.dart
- lib/compiler/ast.dart
- lib/compiler/lexer.dart
- lib/compiler/parser.dart
- lib/compiler/partial_evaluator.dart
- lib/runtime/module_hierarchy.dart
callers:
- lib/engine/glp_engine.dart
- test/compiler/project_linker_test.dart
- test_archive/cssn_modules_test.dart
- test_archive/social_graph_sim_modules_test.dart
mtime: '2026-05-17T10:36:34.894Z'
sha256: b3d11b764d4963e6d78f28841aa9bafd9e3032ca39c0457a7340d56180957a52
topo_level: 6
cycle_group_id: 53
status: pending
target_path: lib/compiler/project_linker.cs
---

Project linker: static linking of multi-module GLP projects.

Given a project root directory, discovers all modules, type-checks each
independently, then produces a single flat Program AST where all
inter-module calls are resolved to renamed local procedures.

Specification: docs/modules/glp-project-compilation-spec.md
Plan: docs/modules/project-compilation-implementation-plan.md
