---
path: lib/runtime/module_hierarchy.dart
name: module_hierarchy.dart
purpose: 'Module hierarchy: self.glp chain discovery and type scope assembly.


  Implements GLP module scoping per docs/modules/glp-module-system-spec.md:

  - Directory-based hierarchy (Section 2)

  - Implicit ancestor scoping (Section 3.1)

  - Shadowing (Section 3.2)

  - Sibling isolation (Section 3.3)


  Specification: docs/modules/glp-module-system-spec.md Sections 2-3

  '
key_idea: 'Module hierarchy: self.glp chain discovery and type scope assembly.


  Implements GLP module scoping per docs/modules/glp-module-system-spec.md:

  - Directory-based hierarchy (Section 2)

  - Implicit ancestor scoping (Section 3.1)

  - Shadowing (Section 3.2)

  - Sibling isolation (Section 3.3)


  Specification: docs/modules/glp-module-system-spec.md Sections 2-3

  '
dependencies:
- lib/analysis/type_checker/param_expansion.dart
- lib/analysis/type_checker/type_ast.dart
- lib/analysis/type_checker/type_environment_builder.dart
- lib/compiler/ast.dart
- lib/compiler/lexer.dart
- lib/compiler/parser.dart
callers:
- lib/compiler/project_linker.dart
- lib/engine/glp_engine.dart
- test/module/cssg_modules_test.dart
- test/module/module_hierarchy_test.dart
- test_archive/cssn_modules_test.dart
- test_archive/social_graph_sim_modules_test.dart
mtime: '2026-04-27T09:23:50.000Z'
sha256: db87dd95891c91cf5d37ba3d1e17349102b04226388a6a81e45f00fe59513298
topo_level: 5
cycle_group_id: 52
status: pending
target_path: lib/runtime/module_hierarchy.cs
plan_started_at: '2026-05-20T02:12:48Z'
plan_completed_at: '2026-05-20T02:12:48Z'
plan_path: null
open_escalation_count: 0
---

Module hierarchy: self.glp chain discovery and type scope assembly.

Implements GLP module scoping per docs/modules/glp-module-system-spec.md:
- Directory-based hierarchy (Section 2)
- Implicit ancestor scoping (Section 3.1)
- Shadowing (Section 3.2)
- Sibling isolation (Section 3.3)

Specification: docs/modules/glp-module-system-spec.md Sections 2-3
