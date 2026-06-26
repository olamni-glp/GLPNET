---
path: test/lint/linter_suspend_once_test.dart
name: linter_suspend_once_test.dart
purpose: Verifies the linter flags a ClauseTry/SuspendEnd appearing after the program's final SuspendEnd.
key_idea: Builds a program with a clause ending in SUSP at END, then appends an illegal extra ClauseTry (TRY/R/U) plus a second SUSP, and asserts Linter().lint returns ok=false with code 'SUSPEND_ONCE_AT_END'.
dependencies:
- lib/bytecode/asm.dart
- lib/lint/linter.dart
callers: []
mtime: '2026-05-21T12:38:15.845Z'
sha256: ff52d31f145f441b25675a8c7ab295757e470f4981b48748f51126645e61cfda
topo_level: 6
cycle_group_id: 93
status: pending
target_path: test/lint/linter_suspend_once_test.cs
plan_started_at: '2026-05-21T16:33:42Z'
plan_completed_at: '2026-05-21T16:38:33Z'
plan_path: .codeconv/conversion-plans/test/lint/linter_suspend_once_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies the linter flags a ClauseTry/SuspendEnd appearing after the program's final SuspendEnd.
