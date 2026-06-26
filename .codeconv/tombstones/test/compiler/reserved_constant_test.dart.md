---
path: test/compiler/reserved_constant_test.dart
name: reserved_constant_test.dart
purpose: Verifies validation of underscore-prefixed reserved constants gated by the -mode(user|system) directive.
key_idea: GlpCompiler.compile rejects '_'-prefixed quoted constants (bare and nested in structures) with 'reserved for system use' in default/explicit user mode, allows them under -mode(system); regular atoms always pass; -mode(invalid) throws 'Invalid mode'.
dependencies:
- lib/compiler/compiler.dart
- lib/compiler/error.dart
callers: []
mtime: '2026-05-21T12:38:15.648Z'
sha256: 28b723fa04ecee639aaec3af6695c57002e00e86f5a4b1fba75776659956e59d
topo_level: 8
cycle_group_id: 78
status: pending
target_path: test/compiler/reserved_constant_test.cs
plan_started_at: '2026-05-21T16:38:53Z'
plan_completed_at: '2026-05-21T16:43:43Z'
plan_path: .codeconv/conversion-plans/test/compiler/reserved_constant_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies validation of underscore-prefixed reserved constants gated by the -mode(user|system) directive.
