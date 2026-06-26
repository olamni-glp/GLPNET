---
path: test/module/module_hierarchy_test.dart
name: module_hierarchy_test.dart
purpose: Verifies hierarchical self.glp ancestor-chain discovery and assembly of the type/procedure scope visible to a descendant GLP module.
key_idea: 'Builds temp dir trees and checks discoverSelfChain (root to target, missing/skipped self.glp, target-is-self) then assembleTypeScope: ancestor types/procedures visible, child & module-own defs shadow parents, siblings isolated, prelude types/procs always present.'
dependencies:
- lib/analysis/type_checker/type_ast.dart
- lib/analysis/type_checker/type_environment_builder.dart
- lib/compiler/ast.dart
- lib/compiler/lexer.dart
- lib/compiler/parser.dart
- lib/runtime/module_hierarchy.dart
callers: []
mtime: '2026-05-21T12:38:15.917Z'
sha256: 21a38c8225f5824cc125308c58c06dd808d7db35dfcb085086d0a265fba780aa
topo_level: 6
cycle_group_id: 96
status: pending
target_path: test/module/module_hierarchy_test.cs
plan_started_at: '2026-05-21T16:33:44Z'
plan_completed_at: '2026-05-21T16:38:35Z'
plan_path: .codeconv/conversion-plans/test/module/module_hierarchy_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies hierarchical self.glp ancestor-chain discovery and assembly of the type/procedure scope visible to a descendant GLP module.
