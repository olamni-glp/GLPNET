---
path: test/bytecode/arithmetic_test.dart
name: arithmetic_test.dart
purpose: 'Verifies arithmetic body kernels and the := system predicate: direct kernel calls plus end-to-end compile/merge/execute of Z := 5+3.'
key_idea: Calls _add/_sub/_mul/_div/_neg/_sqrt kernels via lookup on heap vars (asserts 8,6,42,3.75,-42,4.0; div-by-zero aborts), checks all kernels registered, then merges self.glp stdlib with user compute_sum(Z?):-Z:=5+3 and drains scheduler to bind Z=8.
dependencies:
- lib/bytecode/runner.dart
- lib/compiler/compiler.dart
- lib/runtime/body_kernels.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/scheduler.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-05-21T12:38:15.522Z'
sha256: 6c536bfb10977451326c73eaa01a2b0537043da88cfc65d6f3c36fe05b39c11a
topo_level: 8
cycle_group_id: 71
status: pending
target_path: test/bytecode/arithmetic_test.cs
plan_started_at: '2026-05-21T16:38:50Z'
plan_completed_at: '2026-05-21T16:43:40Z'
plan_path: .codeconv/conversion-plans/test/bytecode/arithmetic_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies arithmetic body kernels and the := system predicate: direct kernel calls plus end-to-end compile/merge/execute of Z := 5+3.
