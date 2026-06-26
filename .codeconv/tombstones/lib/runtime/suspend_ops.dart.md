---
path: lib/runtime/suspend_ops.dart
name: suspend_ops.dart
purpose: 'FCP-exact goal suspension (SuspendOps): attaches a suspended goal to every unbound variable it blocks on so binding any one can reactivate it.'
key_idea: suspendGoalFCP builds ONE shared SuspensionRecord and, per reader address, derefs to the terminal unbound writer; local writers get it via heap.suspendOnWriter, imported readers get a SuspensionListNode pushed onto the VariableEntry's list; already-ground vars are skipped.
dependencies:
- lib/multiagent/variable_table.dart
- lib/runtime/heap_fcp.dart
- lib/runtime/machine_state.dart
- lib/runtime/suspension.dart
- lib/runtime/terms.dart
callers:
- lib/runtime/runtime.dart
- test/conformance/restart_clause1_test.dart
mtime: '2026-05-21T12:38:15.134Z'
sha256: b557f12dac0174dffbd0ebd4fc417e345711aed7c8ea434784d4e64ac7288069
topo_level: 3
cycle_group_id: 35
status: pending
target_path: lib/runtime/suspend_ops.cs
plan_started_at: '2026-05-21T16:00:26Z'
plan_completed_at: '2026-05-21T16:05:42Z'
plan_path: .codeconv/conversion-plans/lib/runtime/suspend_ops.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T11:55:03Z'
target_cs_path: out/csharp/lib/runtime/suspend_ops.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

FCP-exact goal suspension (SuspendOps): attaches a suspended goal to every unbound variable it blocks on so binding any one can reactivate it.
