---
path: lib/multiagent/archive-irma-2026-01-30/tests/bidirectional_exchange_test.dart
name: bidirectional_exchange_test.dart
purpose: "Bidirectional Exchange Test\n\nTests bidirectional communication without circular dependency:\n  @1: produces A = [1,2,3], wants to receive B\n  @2: produces B = [a,b,c], wants to receive A\n\nBoth agents produce independently, then exchange results.\nThis tests that data can flow both directions: @1 <-> @2\n\nProgram:\n  produce_numbers([1,2,3]).\n  produce_letters([a,b,c]).\n"
key_idea: "Bidirectional Exchange Test\n\nTests bidirectional communication without circular dependency:\n  @1: produces A = [1,2,3], wants to receive B\n  @2: produces B = [a,b,c], wants to receive A\n\nBoth agents produce independently, then exchange results.\nThis tests that data can flow both directions: @1 <-> @2\n\nProgram:\n  produce_numbers([1,2,3]).\n  produce_letters([a,b,c]).\n"
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
mtime: '2026-05-17T10:36:35.339Z'
sha256: d5dd1cb67d99ccc2d46385b5cee20f68a11b0aae09115b9ceaba7af314af908a
target_path: lib/multiagent/archive-irma-2026-01-30/tests/bidirectional_exchange_test.cs
---

Bidirectional Exchange Test

Tests bidirectional communication without circular dependency:
  @1: produces A = [1,2,3], wants to receive B
  @2: produces B = [a,b,c], wants to receive A

Both agents produce independently, then exchange results.
This tests that data can flow both directions: @1 <-> @2

Program:
  produce_numbers([1,2,3]).
  produce_letters([a,b,c]).
