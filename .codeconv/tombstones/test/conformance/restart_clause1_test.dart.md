---
path: test/conformance/restart_clause1_test.dart
name: restart_clause1_test.dart
purpose: Verifies that a suspended goal, on wake, restarts execution at its recorded restart point (kappa = clause 1).
key_idea: Allocates an FCP heap variable, suspends a GoalId on its reader with kappa=1 via SuspendOps.suspendGoalFCP, then binds the writer through CommitOps.applySigmaHatFCP and asserts exactly one activation whose id==g and pc==kappa.
dependencies:
- lib/runtime/commit.dart
- lib/runtime/heap_fcp.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/suspend_ops.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-05-21T12:38:15.682Z'
sha256: 15baa98d14a52a37cc739a1867d3ad3a7c68c3c6c5c10ee75ad9ce59b23ac517
topo_level: 5
cycle_group_id: 80
status: pending
target_path: test/conformance/restart_clause1_test.cs
plan_started_at: '2026-05-21T16:24:09Z'
plan_completed_at: '2026-05-21T16:28:39Z'
plan_path: .codeconv/conversion-plans/test/conformance/restart_clause1_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies that a suspended goal, on wake, restarts execution at its recorded restart point (kappa = clause 1).
