---
path: lib/runtime/fairness.dart
name: fairness.dart
purpose: Tail-recursion budget helpers that drive fair scheduler yields between successive goal reductions.
key_idea: 'Two pure functions: nextTailBudget(current) decrements toward 0 (0 signals the scheduler to yield), resetTailBudget() restores tailRecursionBudgetInit (26) after a yield - implementing per-goal bounded tail recursion for fairness.'
dependencies:
- lib/runtime/machine_state.dart
callers:
- lib/runtime/runtime.dart
mtime: '2026-05-21T12:38:14.938Z'
sha256: 6369072893e370601775ebc950258a4d98b7a1b1a66bf89aaa52968216245bb6
topo_level: 1
cycle_group_id: 34
status: pending
target_path: lib/runtime/fairness.cs
plan_started_at: '2026-05-21T14:50:46Z'
plan_completed_at: '2026-05-21T14:56:18Z'
plan_path: .codeconv/conversion-plans/lib/runtime/fairness.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T09:18:26Z'
target_cs_path: out/csharp/lib/runtime/fairness.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Tail-recursion budget helpers that drive fair scheduler yields between successive goal reductions.
