---
path: lib/multiagent/archive-irma-2026-01-30/tests/two_hop_flow_test.dart
name: two_hop_flow_test.dart
purpose: "Two-Hop Flow Test\n\nTests non-circular two-way data flow:\n  @1: produces Xs = [1,2,3]\n  @2: consumes Xs?, produces Ys = [got(1), got(2), got(3)]\n  @1: consumes Ys?\n\nThis is a stepping stone to circular merge - tests that data can\nflow @1 -> @2 -> @1 without circularity.\n\nProgram:\n  produce([1,2,3]).\n  transform([X|Xs], [got(X?)|Ys?]) :- transform(Xs?, Ys).\n  transform([], []).\n"
key_idea: "Two-Hop Flow Test\n\nTests non-circular two-way data flow:\n  @1: produces Xs = [1,2,3]\n  @2: consumes Xs?, produces Ys = [got(1), got(2), got(3)]\n  @1: consumes Ys?\n\nThis is a stepping stone to circular merge - tests that data can\nflow @1 -> @2 -> @1 without circularity.\n\nProgram:\n  produce([1,2,3]).\n  transform([X|Xs], [got(X?)|Ys?]) :- transform(Xs?, Ys).\n  transform([], []).\n"
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
mtime: '2026-05-21T12:38:14.799Z'
sha256: 7fac8a19dbd4e45fead5c162494d8fbc7053ac75f2ed2909491f54503e2a699e
target_path: lib/multiagent/archive-irma-2026-01-30/tests/two_hop_flow_test.cs
---

Two-Hop Flow Test

Tests non-circular two-way data flow:
  @1: produces Xs = [1,2,3]
  @2: consumes Xs?, produces Ys = [got(1), got(2), got(3)]
  @1: consumes Ys?

This is a stepping stone to circular merge - tests that data can
flow @1 -> @2 -> @1 without circularity.

Program:
  produce([1,2,3]).
  transform([X|Xs], [got(X?)|Ys?]) :- transform(Xs?, Ys).
  transform([], []).
