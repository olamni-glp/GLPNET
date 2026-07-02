---
path: test/bytecode/fairness_scheduler_loop_test.dart
name: fairness_scheduler_loop_test.dart
purpose: 'Verifies scheduler fairness: a TailStep self-loop yields after its step quota so concurrent goals interleave in FIFO order.'
key_idea: Enqueues two goals on a LOOP/TailStep program; drain(maxCycles:2) returns [1,2] (each runs to its first 26-step tail yield), and a second drain again returns [1,2], confirming FIFO re-enqueue order.
dependencies:
- lib/bytecode/opcodes.dart
- lib/bytecode/runner.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/scheduler.dart
callers: []
mtime: '2026-05-21T12:38:15.542Z'
sha256: 15f2e909b0910c86fb911fa212eb0812ca811197cf88c5e6cc0c7d9cf981eba8
topo_level: 6
cycle_group_id: 72
status: pending
target_path: test/bytecode/fairness_scheduler_loop_test.cs
plan_started_at: '2026-05-21T16:28:56Z'
plan_completed_at: '2026-05-21T16:33:23Z'
plan_path: .codeconv/conversion-plans/test/bytecode/fairness_scheduler_loop_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies scheduler fairness: a TailStep self-loop yields after its step quota so concurrent goals interleave in FIFO order.
