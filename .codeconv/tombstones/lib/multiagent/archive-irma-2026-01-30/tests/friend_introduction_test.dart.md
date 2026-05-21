---
path: lib/multiagent/archive-irma-2026-01-30/tests/friend_introduction_test.dart
name: friend_introduction_test.dart
purpose: "Friend-Mediated Introduction Test\n\nTests the friend introduction protocol:\n  @1 (Alice): Knows both Bob and Carol, introduces them\n  @2 (Bob): Receives introduction containing Carol's info\n  @3 (Carol): Receives introduction containing Bob's info\n\nProtocol:\n1. Alice creates intro(bob, carol) for Bob\n2. Alice creates intro(carol, bob) for Carol\n3. Bob receives and wraps as got(intro(bob, carol))\n4. Carol receives and wraps as got(intro(carol, bob))\n\nThis tests 3-agent message distribution via a central coordinator.\n"
key_idea: "Friend-Mediated Introduction Test\n\nTests the friend introduction protocol:\n  @1 (Alice): Knows both Bob and Carol, introduces them\n  @2 (Bob): Receives introduction containing Carol's info\n  @3 (Carol): Receives introduction containing Bob's info\n\nProtocol:\n1. Alice creates intro(bob, carol) for Bob\n2. Alice creates intro(carol, bob) for Carol\n3. Bob receives and wraps as got(intro(bob, carol))\n4. Carol receives and wraps as got(intro(carol, bob))\n\nThis tests 3-agent message distribution via a central coordinator.\n"
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
mtime: '2026-05-21T12:38:14.318Z'
sha256: ed1e0cb245426624ddfac156faf8d77d120fe3d5b00bf573e873cb8d0508fc83
target_path: lib/multiagent/archive-irma-2026-01-30/tests/friend_introduction_test.cs
---

Friend-Mediated Introduction Test

Tests the friend introduction protocol:
  @1 (Alice): Knows both Bob and Carol, introduces them
  @2 (Bob): Receives introduction containing Carol's info
  @3 (Carol): Receives introduction containing Bob's info

Protocol:
1. Alice creates intro(bob, carol) for Bob
2. Alice creates intro(carol, bob) for Carol
3. Bob receives and wraps as got(intro(bob, carol))
4. Carol receives and wraps as got(intro(carol, bob))

This tests 3-agent message distribution via a central coordinator.
