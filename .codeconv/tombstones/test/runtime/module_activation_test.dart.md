---
path: test/runtime/module_activation_test.dart
name: module_activation_test.dart
purpose: Verifies activateModule spawns the serve/2 system predicate over a GLP channel to dispatch RPC goals into an exported module.
key_idea: Compiles serveSource + target module; checks serve runner registration; drains scheduler asserting serve suspends on the empty channel; sends single/multiple goals via channel.send to enqueue activations that serve dispatches through _activate; channel.close makes serve terminate; e2e trace contains 'serve'.
dependencies:
- lib/bytecode/runner.dart
- lib/compiler/compiler.dart
- lib/runtime/glp_activation.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/scheduler.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-05-21T12:38:16.631Z'
sha256: 9fd5f3ec7705dda8012f88f4637e0ab09b4fbd78d284f1855867ca8736cd10fb
topo_level: 8
cycle_group_id: 116
status: pending
target_path: test/runtime/module_activation_test.cs
plan_started_at: '2026-05-21T16:43:56Z'
plan_completed_at: '2026-05-21T16:49:14Z'
plan_path: .codeconv/conversion-plans/test/runtime/module_activation_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies activateModule spawns the serve/2 system predicate over a GLP channel to dispatch RPC goals into an exported module.
