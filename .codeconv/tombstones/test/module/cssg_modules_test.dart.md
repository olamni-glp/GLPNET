---
path: test/module/cssg_modules_test.dart
name: cssg_modules_test.dart
purpose: "Validation test: cssg_modules project against Phase 1-3 module system.\n\nValidates that each module file in programs/cssg_modules/ parses and\ntype-checks correctly using the real pipeline from GlpEngine.loadSource:\n  1. Parse → 2. Partial evaluation → 3. Type check with ancestor scope\n\nThis test does NOT modify any .glp files or runtime code.\n"
key_idea: "Validation test: cssg_modules project against Phase 1-3 module system.\n\nValidates that each module file in programs/cssg_modules/ parses and\ntype-checks correctly using the real pipeline from GlpEngine.loadSource:\n  1. Parse → 2. Partial evaluation → 3. Type check with ancestor scope\n\nThis test does NOT modify any .glp files or runtime code.\n"
dependencies:
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
sha256: fece36ea3f927a1077c5c1a176b2281d71cc9049947063c871d6dbc53d423a05
topo_level: 6
cycle_group_id: 94
status: pending
target_path: test/module/cssg_modules_test.cs
plan_started_at: '2026-05-21T16:33:43Z'
plan_completed_at: '2026-05-21T16:38:34Z'
plan_path: .codeconv/conversion-plans/test/module/cssg_modules_test.dart.md
open_escalation_count: 0
---

Validation test: cssg_modules project against Phase 1-3 module system.

Validates that each module file in programs/cssg_modules/ parses and
type-checks correctly using the real pipeline from GlpEngine.loadSource:
  1. Parse → 2. Partial evaluation → 3. Type check with ancestor scope

This test does NOT modify any .glp files or runtime code.
