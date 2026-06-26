---
path: lib/multiagent/payload_serializer.dart
name: payload_serializer.dart
purpose: 'Payload Serialization for irmaGLP


  Serializes terms and messages to bytes for inter-agent transport.

  Uses global variable IDs (creator:localId) for cross-agent routing.


  Specification: /docs/ma/irmaGLP-spec.md Section 6 and 8.3

  '
key_idea: 'Payload Serialization for irmaGLP


  Serializes terms and messages to bytes for inter-agent transport.

  Uses global variable IDs (creator:localId) for cross-agent routing.


  Specification: /docs/ma/irmaGLP-spec.md Section 6 and 8.3

  '
dependencies:
- lib/multiagent/mad_helpers.dart
- lib/multiagent/message_queue.dart
- lib/runtime/terms.dart
callers:
- lib/multiagent/agent_runtime.dart
- lib/multiagent/archive-irma-2026-01-30/helpers-current.dart
- lib/multiagent/archive-irma-2026-01-30/helpers.dart
- lib/multiagent/archive-irma-2026-01-30/irma_context-phase4.dart
- lib/multiagent/archive-irma-2026-01-30/irma_context.dart
- lib/multiagent/archive-irma-2026-01-30/isolate_manager.dart
- lib/multiagent/archive-irma-2026-01-30/tests/bidirectional_exchange_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/bidirectional_stream_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/coop_stream_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/distribute_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/friend_introduction_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/irma_agent_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/isolate_cold_call_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/isolate_friend_introduction_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/isolate_play_alice_bob_charlie_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/network_transaction_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/payload_serializer_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/play_alice_bob_charlie_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/reversed_flow_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/shared_variable_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/simple_imported_reader_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/three_agent_merge_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/three_agent_pipeline_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/two_hop_flow_test.dart
- lib/multiagent/isolate_manager.dart
- lib/multiagent/mad_context.dart
mtime: '2026-05-21T12:38:13.745Z'
sha256: 6291cb396efe81564618f2dd1e207ebda0a7fd3e01e918356a0e2f62282655e0
topo_level: 2
cycle_group_id: 29
status: pending
target_path: lib/multiagent/payload_serializer.cs
plan_started_at: '2026-05-21T15:18:02Z'
plan_completed_at: '2026-05-21T15:24:00Z'
plan_path: .codeconv/conversion-plans/lib/multiagent/payload_serializer.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T09:37:34Z'
target_cs_path: out/csharp/lib/multiagent/payload_serializer.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: doc
key_idea_source: doc
---

Payload Serialization for irmaGLP

Serializes terms and messages to bytes for inter-agent transport.
Uses global variable IDs (creator:localId) for cross-agent routing.

Specification: /docs/ma/irmaGLP-spec.md Section 6 and 8.3
