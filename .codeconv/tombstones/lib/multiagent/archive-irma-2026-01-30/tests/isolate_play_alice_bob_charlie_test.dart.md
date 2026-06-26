---
path: lib/multiagent/archive-irma-2026-01-30/tests/isolate_play_alice_bob_charlie_test.dart
name: isolate_play_alice_bob_charlie_test.dart
purpose: "Full Play Alice-Bob-Charlie with Three Isolates\n\nSTATUS: WORK IN PROGRESS - Goals now execute but protocol doesn't complete\n\nFIXED: Goals no longer fail immediately. The fix was to create proper\nreader cells for goal arguments (allocateVariable + bindVariable) instead\nof using storeTermOnHeap which creates ValueTag cells. GLP expects reader\nreferences for input arguments (Channel? in procedure declarations).\n\nREMAINING ISSUES:\n1. No messages are exchanged between agents (all \"flushed 0 messages\")\n2. Goals complete too quickly without running the full protocol\n3. Warnings: \"got local VarRef at X, expected VariableEntry\" suggest\n   IRMA doesn't recognize locally-created variables\n\nROOT CAUSE: The channel variables (userInWriter, userOutWriter, etc.)\nare not registered with IRMA's variable table, so when the protocol\ntries to write to UserOut or read from UserIn?, IRMA doesn't know\nthese are variables that need network routing.\n\nNEXT STEPS:\n1. Register channel variables with IrmaContext (registerWriter, etc.)\n2. Verify the network output stream (NetOut) is properly monitored\n3. Add debug tracing to see where the protocol gets stuck\n\nFor now, the cold-call and friend-introduction isolate tests demonstrate\nthat IRMA routing works correctly at the lower level.\n\nRuns the complete social graph protocol across three Dart isolates:\n1. Alice cold-calls Bob (Bob accepts)\n2. Alice sends \"Hi Bob, this is Alice\" to Bob\n3. Bob cold-calls Charlie (Charlie accepts)\n4. Charlie sends \"Hi Bob, this is Charlie\" to Bob\n5. Bob introduces Alice to Charlie (both accept)\n6. Alice sends \"Hi Charlie, this is Alice\" to Charlie\n7. Charlie responds \"Hi Alice, this is Charlie\" to Alice\n\nArchitecture:\n- Each agent runs in its own isolate with independent GlpRuntime\n- IRMA Network Transaction replaces network3 GLP procedure\n- Main isolate coordinates message routing between agents\n- Agents use registerNetworkOutput/handleNetworkMessage for cold-calls\n- Friend channels use standard IRMA Communicate Transaction\n"
key_idea: "Full Play Alice-Bob-Charlie with Three Isolates\n\nSTATUS: WORK IN PROGRESS - Goals now execute but protocol doesn't complete\n\nFIXED: Goals no longer fail immediately. The fix was to create proper\nreader cells for goal arguments (allocateVariable + bindVariable) instead\nof using storeTermOnHeap which creates ValueTag cells. GLP expects reader\nreferences for input arguments (Channel? in procedure declarations).\n\nREMAINING ISSUES:\n1. No messages are exchanged between agents (all \"flushed 0 messages\")\n2. Goals complete too quickly without running the full protocol\n3. Warnings: \"got local VarRef at X, expected VariableEntry\" suggest\n   IRMA doesn't recognize locally-created variables\n\nROOT CAUSE: The channel variables (userInWriter, userOutWriter, etc.)\nare not registered with IRMA's variable table, so when the protocol\ntries to write to UserOut or read from UserIn?, IRMA doesn't know\nthese are variables that need network routing.\n\nNEXT STEPS:\n1. Register channel variables with IrmaContext (registerWriter, etc.)\n2. Verify the network output stream (NetOut) is properly monitored\n3. Add debug tracing to see where the protocol gets stuck\n\nFor now, the cold-call and friend-introduction isolate tests demonstrate\nthat IRMA routing works correctly at the lower level.\n\nRuns the complete social graph protocol across three Dart isolates:\n1. Alice cold-calls Bob (Bob accepts)\n2. Alice sends \"Hi Bob, this is Alice\" to Bob\n3. Bob cold-calls Charlie (Charlie accepts)\n4. Charlie sends \"Hi Bob, this is Charlie\" to Bob\n5. Bob introduces Alice to Charlie (both accept)\n6. Alice sends \"Hi Charlie, this is Alice\" to Charlie\n7. Charlie responds \"Hi Alice, this is Charlie\" to Alice\n\nArchitecture:\n- Each agent runs in its own isolate with independent GlpRuntime\n- IRMA Network Transaction replaces network3 GLP procedure\n- Main isolate coordinates message routing between agents\n- Agents use registerNetworkOutput/handleNetworkMessage for cold-calls\n- Friend channels use standard IRMA Communicate Transaction\n"
dependencies:
- lib/bytecode/runner.dart
- lib/compiler/compiler.dart
- lib/multiagent/irma_context.dart
- lib/multiagent/message_queue.dart
- lib/multiagent/payload_serializer.dart
- lib/multiagent/variable_table.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/scheduler.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-05-21T12:38:14.497Z'
sha256: b7819dde39257dddd7b82343a2a48b732c1d318d7e558d2a8ad01ba68fc0e119
target_path: lib/multiagent/archive-irma-2026-01-30/tests/isolate_play_alice_bob_charlie_test.cs
purpose_source: doc
key_idea_source: doc
---

Full Play Alice-Bob-Charlie with Three Isolates

STATUS: WORK IN PROGRESS - Goals now execute but protocol doesn't complete

FIXED: Goals no longer fail immediately. The fix was to create proper
reader cells for goal arguments (allocateVariable + bindVariable) instead
of using storeTermOnHeap which creates ValueTag cells. GLP expects reader
references for input arguments (Channel? in procedure declarations).

REMAINING ISSUES:
1. No messages are exchanged between agents (all "flushed 0 messages")
2. Goals complete too quickly without running the full protocol
3. Warnings: "got local VarRef at X, expected VariableEntry" suggest
   IRMA doesn't recognize locally-created variables

ROOT CAUSE: The channel variables (userInWriter, userOutWriter, etc.)
are not registered with IRMA's variable table, so when the protocol
tries to write to UserOut or read from UserIn?, IRMA doesn't know
these are variables that need network routing.

NEXT STEPS:
1. Register channel variables with IrmaContext (registerWriter, etc.)
2. Verify the network output stream (NetOut) is properly monitored
3. Add debug tracing to see where the protocol gets stuck

For now, the cold-call and friend-introduction isolate tests demonstrate
that IRMA routing works correctly at the lower level.

Runs the complete social graph protocol across three Dart isolates:
1. Alice cold-calls Bob (Bob accepts)
2. Alice sends "Hi Bob, this is Alice" to Bob
3. Bob cold-calls Charlie (Charlie accepts)
4. Charlie sends "Hi Bob, this is Charlie" to Bob
5. Bob introduces Alice to Charlie (both accept)
6. Alice sends "Hi Charlie, this is Alice" to Charlie
7. Charlie responds "Hi Alice, this is Charlie" to Alice

Architecture:
- Each agent runs in its own isolate with independent GlpRuntime
- IRMA Network Transaction replaces network3 GLP procedure
- Main isolate coordinates message routing between agents
- Agents use registerNetworkOutput/handleNetworkMessage for cold-calls
- Friend channels use standard IRMA Communicate Transaction
