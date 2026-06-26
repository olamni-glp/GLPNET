---
path: test/module/module_syntax_v2_test.dart
name: module_syntax_v2_test.dart
purpose: 'Verifies the v2 module syntax: exported/imported procedure declarations and rejection of the legacy -export/-import directives.'
key_idea: 'parseModule checks exported flag, imported procedures with modulePath (single/deep ui#actors/none) and #-qualified arg TypeRefs (isInput), declaration-only imports; old -export([])/-import([]) throw CompileError ''no longer supported''; -module, remote #-goals, type-only files still parse.'
dependencies:
- lib/analysis/type_checker/type_ast.dart
- lib/compiler/ast.dart
- lib/compiler/error.dart
- lib/compiler/lexer.dart
- lib/compiler/parser.dart
callers: []
mtime: '2026-05-21T12:38:15.953Z'
sha256: fb04dca7a515ac9c443e9a2a0e24262ce22a82dfee26382aae9b5131e153363a
topo_level: 4
cycle_group_id: 98
status: pending
target_path: test/module/module_syntax_v2_test.cs
plan_started_at: '2026-05-21T16:13:09Z'
plan_completed_at: '2026-05-21T16:17:38Z'
plan_path: .codeconv/conversion-plans/test/module/module_syntax_v2_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies the v2 module syntax: exported/imported procedure declarations and rejection of the legacy -export/-import directives.
