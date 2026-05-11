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
mtime: '2026-04-27T09:23:50.000Z'
sha256: ffba37a1c2ae6161898532e842040e38b1aaab8a818fe9c60bd4a001952688c4
---

GLP-level module activation.

Creates a GLP channel, spawns serve(Module, ChannelReader?), and
returns a handle for sending goals on the channel.

Phase 4 of dynamic module dispatch (docs/modules/dynamic-dispatch-implementation-plan.md).
