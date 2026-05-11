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
callers: []
mtime: '2026-04-27T09:23:50.000Z'
sha256: b3d11b764d4963e6d78f28841aa9bafd9e3032ca39c0457a7340d56180957a52
---

Project linker: static linking of multi-module GLP projects.

Given a project root directory, discovers all modules, type-checks each
independently, then produces a single flat Program AST where all
inter-module calls are resolved to renamed local procedures.

Specification: docs/modules/glp-project-compilation-spec.md
Plan: docs/modules/project-compilation-implementation-plan.md
