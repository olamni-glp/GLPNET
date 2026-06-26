---
path: test/module/module_parser_test.dart
name: module_parser_test.dart
purpose: 'Verifies lexing and parsing of GLP module syntax: -module declarations, exported/procedure declarations, and remote (#) goals.'
key_idea: Asserts lexer tokens (HASH, MINUS+module, exported); parseModule builds module name/exportedSignatures/procDeclarations; RemoteGoal distinguishes static vs dynamic (VarTerm, isReader) modules, parses chained goals, rejects module-with-args, handles nullary/arg procedure decls.
dependencies:
- lib/compiler/ast.dart
- lib/compiler/lexer.dart
- lib/compiler/parser.dart
- lib/compiler/token.dart
callers: []
mtime: '2026-05-21T12:38:15.935Z'
sha256: 474ecc4372558bbbf10fc78a96bb1fb3f4eaf64c5b23d034bf0d40b3096689dc
topo_level: 4
cycle_group_id: 97
status: pending
target_path: test/module/module_parser_test.cs
plan_started_at: '2026-05-21T16:13:08Z'
plan_completed_at: '2026-05-21T16:17:37Z'
plan_path: .codeconv/conversion-plans/test/module/module_parser_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies lexing and parsing of GLP module syntax: -module declarations, exported/procedure declarations, and remote (#) goals.
