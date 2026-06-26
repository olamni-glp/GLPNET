---
path: lib/runtime/glp_activation.dart
name: glp_activation.dart
purpose: 'GLP-level module activation.


  Creates a GLP channel, spawns serve(Module, ChannelReader?), and

  returns a handle for sending goals on the channel.


  Phase 4 of dynamic module dispatch (docs/modules/dynamic-dispatch-implementation-plan.md).

  '
key_idea: 'GLP-level module activation.


  Creates a GLP channel, spawns serve(Module, ChannelReader?), and

  returns a handle for sending goals on the channel.


  Phase 4 of dynamic module dispatch (docs/modules/dynamic-dispatch-implementation-plan.md).

  '
dependencies:
- lib/bytecode/runner.dart
- lib/runtime/heap_fcp.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/terms.dart
callers:
- lib/engine/glp_engine.dart
- lib/runtime/runtime.dart
- test/dynamic_dispatch_test.dart
- test/runtime/module_activation_test.dart
- test/runtime/rpc_routing_test.dart
- test_archive/cssg_glp_dispatch_test.dart
- test_archive/cssn_modules_test.dart
- test_archive/social_graph_sim_modules_test.dart
mtime: '2026-05-21T12:38:14.955Z'
sha256: ffba37a1c2ae6161898532e842040e38b1aaab8a818fe9c60bd4a001952688c4
topo_level: 4
cycle_group_id: 37
status: pending
target_path: lib/runtime/glp_activation.cs
plan_started_at: '2026-05-21T16:06:17Z'
plan_completed_at: '2026-05-21T16:12:52Z'
plan_path: .codeconv/conversion-plans/lib/runtime/glp_activation.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T12:39:14Z'
target_cs_path: out/csharp/lib/runtime/glp_activation.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: doc
key_idea_source: doc
---

GLP-level module activation.

Creates a GLP channel, spawns serve(Module, ChannelReader?), and
returns a handle for sending goals on the channel.

Phase 4 of dynamic module dispatch (docs/modules/dynamic-dispatch-implementation-plan.md).
