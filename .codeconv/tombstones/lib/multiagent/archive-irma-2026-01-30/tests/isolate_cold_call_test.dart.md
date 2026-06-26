---
path: lib/multiagent/archive-irma-2026-01-30/tests/isolate_cold_call_test.dart
name: isolate_cold_call_test.dart
purpose: 'Isolate-Based Cold-Call Test


  Tests the Network Transaction across actual Dart isolates.

  Alice and Bob each run in their own isolate with separate GlpRuntime instances.

  Communication happens via SendPort/ReceivePort, simulating real network transport.


  Scenario:

  1. Alice creates response variable Resp (writer) and Resp? (reader)

  2. Alice sends msg(bob, ping(Resp)) on her NetOut

  3. Message is serialized and sent via SendPort to Bob''s isolate

  4. Bob receives on NetIn, extracts imported writer for Resp

  5. Bob binds Resp = pong

  6. Assignment message is serialized and sent via SendPort to Alice''s isolate

  7. Alice receives assignment, binds her Resp? reader

  8. Test verifies Alice''s Resp? == pong

  '
key_idea: 'Isolate-Based Cold-Call Test


  Tests the Network Transaction across actual Dart isolates.

  Alice and Bob each run in their own isolate with separate GlpRuntime instances.

  Communication happens via SendPort/ReceivePort, simulating real network transport.


  Scenario:

  1. Alice creates response variable Resp (writer) and Resp? (reader)

  2. Alice sends msg(bob, ping(Resp)) on her NetOut

  3. Message is serialized and sent via SendPort to Bob''s isolate

  4. Bob receives on NetIn, extracts imported writer for Resp

  5. Bob binds Resp = pong

  6. Assignment message is serialized and sent via SendPort to Alice''s isolate

  7. Alice receives assignment, binds her Resp? reader

  8. Test verifies Alice''s Resp? == pong

  '
dependencies:
- lib/multiagent/irma_context.dart
- lib/multiagent/message_queue.dart
- lib/multiagent/payload_serializer.dart
- lib/runtime/runtime.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-05-21T12:38:14.442Z'
sha256: dba63379a7481371d3b5389edafb28650a562ce128922342da7b91c33e9de338
target_path: lib/multiagent/archive-irma-2026-01-30/tests/isolate_cold_call_test.cs
purpose_source: doc
key_idea_source: doc
---

Isolate-Based Cold-Call Test

Tests the Network Transaction across actual Dart isolates.
Alice and Bob each run in their own isolate with separate GlpRuntime instances.
Communication happens via SendPort/ReceivePort, simulating real network transport.

Scenario:
1. Alice creates response variable Resp (writer) and Resp? (reader)
2. Alice sends msg(bob, ping(Resp)) on her NetOut
3. Message is serialized and sent via SendPort to Bob's isolate
4. Bob receives on NetIn, extracts imported writer for Resp
5. Bob binds Resp = pong
6. Assignment message is serialized and sent via SendPort to Alice's isolate
7. Alice receives assignment, binds her Resp? reader
8. Test verifies Alice's Resp? == pong
