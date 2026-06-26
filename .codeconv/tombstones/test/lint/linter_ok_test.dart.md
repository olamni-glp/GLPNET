---
path: test/lint/linter_ok_test.dart
name: linter_ok_test.dart
purpose: 'Verifies the linter accepts a well-formed bytecode shape: head/guard ops only before commit with a single trailing SuspendEnd.'
key_idea: Builds two clauses (C1, C2) containing only TRY plus reader ops (R) and U jumps pre-commit, ending in one SUSP at END, then asserts Linter().lint returns ok=true.
dependencies:
- lib/bytecode/asm.dart
- lib/lint/linter.dart
callers: []
mtime: '2026-05-21T12:38:15.825Z'
sha256: 75029d51648451ea8ae4049fe8a1f3e64fc07635122432564fdc4c3c45ce99da
topo_level: 6
cycle_group_id: 92
status: pending
target_path: test/lint/linter_ok_test.cs
plan_started_at: '2026-05-21T16:33:41Z'
plan_completed_at: '2026-05-21T16:38:32Z'
plan_path: .codeconv/conversion-plans/test/lint/linter_ok_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies the linter accepts a well-formed bytecode shape: head/guard ops only before commit with a single trailing SuspendEnd.
