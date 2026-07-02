---
path: test/conformance/fairness_26_test.dart
name: fairness_26_test.dart
purpose: 'Verifies the runtime''s 26-step tail-recursion fairness budget: when a tail-recursive goal yields and how its budget resets.'
key_idea: 'Single test drives GlpRuntime.tailReduce on one GoalId: 25 reductions return false (no yield), the 26th returns true (yield) and resets budgetOf to 26, then a further reduction returns false with the budget decremented to 25.'
dependencies:
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
callers: []
mtime: '2026-05-21T12:38:15.663Z'
sha256: bb89ae3cfa3df92ffb3305f90fc80250bc658914cb53c211c49157ce5c469a6e
topo_level: 5
cycle_group_id: 79
status: pending
target_path: test/conformance/fairness_26_test.cs
plan_started_at: '2026-05-21T16:24:08Z'
plan_completed_at: '2026-05-21T16:28:38Z'
plan_path: .codeconv/conversion-plans/test/conformance/fairness_26_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies the runtime's 26-step tail-recursion fairness budget: when a tail-recursive goal yields and how its budget resets.
