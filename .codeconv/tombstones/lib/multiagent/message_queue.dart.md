---
path: lib/multiagent/message_queue.dart
name: message_queue.dart
purpose: 'Message Queue (M_p) for madGLP


  Manages outbound messages from an agent to other agents.

  Messages are queued per destination with FIFO ordering.


  Specification: /docs/ma/madGLP-spec.md Section 6.1

  '
key_idea: 'Message Queue (M_p) for madGLP


  Manages outbound messages from an agent to other agents.

  Messages are queued per destination with FIFO ordering.


  Specification: /docs/ma/madGLP-spec.md Section 6.1

  '
dependencies: []
callers:
- lib/multiagent/agent_runtime.dart
- lib/multiagent/isolate_manager.dart
- lib/multiagent/mad_context.dart
- lib/multiagent/payload_serializer.dart
- test/multiagent/mad_cold_call_isolate_test.dart
- test/multiagent/mad_scenarios_test.dart
- test/multiagent/mad_transactions_test.dart
mtime: '2026-04-27T09:23:50.000Z'
sha256: 619550d11a54842ba4948fd5cd8ac86d742fbaddf26fcd138ddc39c26da696d7
topo_level: 0
cycle_group_id: 28
status: ready
target_path: lib/multiagent/message_queue.cs
plan_started_at: '2026-05-19T23:04:36Z'
plan_completed_at: '2026-05-19T23:04:36Z'
plan_path: null
open_escalation_count: 0
---

Message Queue (M_p) for madGLP

Manages outbound messages from an agent to other agents.
Messages are queued per destination with FIFO ordering.

Specification: /docs/ma/madGLP-spec.md Section 6.1
