---
path: lib/lint/linter.dart
name: linter.dart
purpose: Static structural linter validating a BytecodeProgram's clause layout against compiler-contract invariants, returning a LintResult of LintIssues.
key_idea: 'Linter.lint walks ops through a small state machine (inClause/inBody/seenSuspendEnd) enforcing BODY_BEFORE_COMMIT, ILLEGAL_PRECOMMIT_OP, ILLEGAL_BODY_OP and SUSPEND_ONCE_AT_END: body ops barred before Commit, only head/guard ops pre-commit, exactly one predicate-final SuspendEnd.'
dependencies:
- lib/bytecode/opcodes.dart
- lib/bytecode/runner.dart
callers:
- test/lint/linter_body_precommit_test.dart
- test/lint/linter_ok_test.dart
- test/lint/linter_suspend_once_test.dart
mtime: '2026-05-21T12:38:13.573Z'
sha256: 257a66f29065ce82f55ec45df025e87aba1bcaeee0deaf93c42d93f27335bff7
topo_level: 5
cycle_group_id: 59
status: pending
target_path: lib/lint/linter.cs
plan_started_at: '2026-05-21T16:17:55Z'
plan_completed_at: '2026-05-21T16:23:53Z'
plan_path: .codeconv/conversion-plans/lib/lint/linter.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T13:09:18Z'
target_cs_path: out/csharp/lib/lint/linter.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Static structural linter validating a BytecodeProgram's clause layout against compiler-contract invariants, returning a LintResult of LintIssues.
