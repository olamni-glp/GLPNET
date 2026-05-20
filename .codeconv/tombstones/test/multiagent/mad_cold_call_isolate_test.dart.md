---
path: test/multiagent/mad_cold_call_isolate_test.dart
name: mad_cold_call_isolate_test.dart
purpose: 'madGLP Cold-Call Test with Dart Isolates


  Validates the complete madGLP push-based protocol across actual Dart isolates.

  Alice and Bob each run in their own isolate with separate GlpRuntime instances.

  Communication happens via SendPort/ReceivePort, simulating real network transport.


  Adapted from the archived isolate_cold_call_test.dart to use madGLP APIs:

  - globalize/localize instead of registerNetworkOutput/Input

  - registerGlobalSendSpawns instead of registerWriter

  - handleMadAssignment instead of handleAssignment

  - onWriterBound triggers message sending (push-based)


  Scenario (per madGLP-spec.md Section 10.2):

  1. Alice creates response variable Resp (writer) and Resp? (reader)

  2. Alice globalizes Resp (writer) to send to Bob -> creates entry (Resp, bob) at index 1

  3. Bob localizes _w(alice,1) -> gets writer, spawns global_send

  4. Bob binds his writer to "pong"

  5. global_send fires: Bob sends _w(alice,1) := "pong" to Alice

  6. Alice receives assignment, binds her Resp writer

  7. Test verifies Alice''s Resp? == "pong"

  '
key_idea: 'madGLP Cold-Call Test with Dart Isolates


  Validates the complete madGLP push-based protocol across actual Dart isolates.

  Alice and Bob each run in their own isolate with separate GlpRuntime instances.

  Communication happens via SendPort/ReceivePort, simulating real network transport.


  Adapted from the archived isolate_cold_call_test.dart to use madGLP APIs:

  - globalize/localize instead of registerNetworkOutput/Input

  - registerGlobalSendSpawns instead of registerWriter

  - handleMadAssignment instead of handleAssignment

  - onWriterBound triggers message sending (push-based)


  Scenario (per madGLP-spec.md Section 10.2):

  1. Alice creates response variable Resp (writer) and Resp? (reader)

  2. Alice globalizes Resp (writer) to send to Bob -> creates entry (Resp, bob) at index 1

  3. Bob localizes _w(alice,1) -> gets writer, spawns global_send

  4. Bob binds his writer to "pong"

  5. global_send fires: Bob sends _w(alice,1) := "pong" to Alice

  6. Alice receives assignment, binds her Resp writer

  7. Test verifies Alice''s Resp? == "pong"

  '
dependencies:
- lib/multiagent/global_send.dart
- lib/multiagent/mad_context.dart
- lib/multiagent/mad_helpers.dart
- lib/multiagent/message_queue.dart
- lib/runtime/runtime.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-04-27T09:23:50.000Z'
sha256: 5678e45465ebcd43b220dc95a597cf05ff9a5a52ec87990e303802147ddc7555
topo_level: 5
cycle_group_id: 108
status: pending
target_path: test/multiagent/mad_cold_call_isolate_test.cs
plan_started_at: '2026-05-20T02:25:36Z'
plan_completed_at: '2026-05-20T02:25:36Z'
plan_path: null
open_escalation_count: 0
---

madGLP Cold-Call Test with Dart Isolates

Validates the complete madGLP push-based protocol across actual Dart isolates.
Alice and Bob each run in their own isolate with separate GlpRuntime instances.
Communication happens via SendPort/ReceivePort, simulating real network transport.

Adapted from the archived isolate_cold_call_test.dart to use madGLP APIs:
- globalize/localize instead of registerNetworkOutput/Input
- registerGlobalSendSpawns instead of registerWriter
- handleMadAssignment instead of handleAssignment
- onWriterBound triggers message sending (push-based)

Scenario (per madGLP-spec.md Section 10.2):
1. Alice creates response variable Resp (writer) and Resp? (reader)
2. Alice globalizes Resp (writer) to send to Bob -> creates entry (Resp, bob) at index 1
3. Bob localizes _w(alice,1) -> gets writer, spawns global_send
4. Bob binds his writer to "pong"
5. global_send fires: Bob sends _w(alice,1) := "pong" to Alice
6. Alice receives assignment, binds her Resp writer
7. Test verifies Alice's Resp? == "pong"
