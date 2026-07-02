---
path: lib/runtime/hanger.dart
name: hanger.dart
purpose: Hanger record guaranteeing a goal suspended on several readers reactivates exactly once.
key_idea: Lightweight class holding goalId + kappa (clause-selection restart Pc) and a boolean armed initialized true; the first wake flips armed to false so subsequent writer bindings on other shared readers don't re-enqueue the same goal.
dependencies:
- lib/runtime/machine_state.dart
callers:
- lib/runtime/suspend.dart
mtime: '2026-05-21T12:38:14.993Z'
sha256: 162457ab2f6db96de5e7e7beb5ae3acd6ed5dea548ea9ef7106b32fa0522403f
topo_level: 1
cycle_group_id: 66
status: pending
target_path: lib/runtime/hanger.cs
plan_started_at: '2026-05-21T14:50:48Z'
plan_completed_at: '2026-05-21T14:56:19Z'
plan_path: .codeconv/conversion-plans/lib/runtime/hanger.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T09:22:17Z'
target_cs_path: out/csharp/lib/runtime/hanger.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Hanger record guaranteeing a goal suspended on several readers reactivates exactly once.
