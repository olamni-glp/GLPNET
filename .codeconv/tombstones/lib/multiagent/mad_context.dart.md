---
path: lib/multiagent/mad_context.dart
name: mad_context.dart
purpose: 'madGLP Agent Context


  Provides agent-level context for multiagent GLP communication.

  Each agent has W_p (global writers table) and M_p (message queue).


  Specification: /docs/ma/madGLP-spec.md

  '
key_idea: 'madGLP Agent Context


  Provides agent-level context for multiagent GLP communication.

  Each agent has W_p (global writers table) and M_p (message queue).


  Specification: /docs/ma/madGLP-spec.md

  '
dependencies:
- lib/multiagent/global_send.dart
- lib/multiagent/global_writers_table.dart
- lib/multiagent/mad_helpers.dart
- lib/multiagent/message_queue.dart
- lib/multiagent/payload_serializer.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/terms.dart
callers:
- lib/engine/glp_engine.dart
- lib/multiagent/agent_runtime.dart
- lib/multiagent/archive-irma-2026-01-30/heap-tests/shared_variable_pointer_test.dart
- lib/runtime/body_kernels.dart
- test/multiagent/mad_cold_call_isolate_test.dart
- test/multiagent/mad_scenarios_test.dart
- test/multiagent/mad_transactions_test.dart
mtime: '2026-05-17T10:36:35.097Z'
sha256: 2c0667ab13bae6919551df9dce3ba00ec6ac90d4037534aa7f62a39c486cca88
topo_level: 4
cycle_group_id: 37
status: pending
target_path: lib/multiagent/mad_context.cs
---

madGLP Agent Context

Provides agent-level context for multiagent GLP communication.
Each agent has W_p (global writers table) and M_p (message queue).

Specification: /docs/ma/madGLP-spec.md
