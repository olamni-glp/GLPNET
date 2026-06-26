---
path: test/lint/linter_body_precommit_test.dart
name: linter_body_precommit_test.dart
purpose: Verifies the bytecode linter flags body operations emitted before the clause COMMIT point.
key_idea: Builds a clause program via BC asm (TRY, W, then BCONST as a body op before COMMIT, U/END, SUSP) and asserts Linter().lint returns ok=false with an issue whose code is 'BODY_BEFORE_COMMIT'.
dependencies:
- lib/bytecode/asm.dart
- lib/lint/linter.dart
callers: []
mtime: '2026-05-21T12:38:15.810Z'
sha256: ac6c501dfe96836ca19e73d1aab4d30b935aee1700c2328d4769354d872d5bc2
topo_level: 6
cycle_group_id: 91
status: pending
target_path: test/lint/linter_body_precommit_test.cs
plan_started_at: '2026-05-21T16:33:40Z'
plan_completed_at: '2026-05-21T16:38:31Z'
plan_path: .codeconv/conversion-plans/test/lint/linter_body_precommit_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies the bytecode linter flags body operations emitted before the clause COMMIT point.
