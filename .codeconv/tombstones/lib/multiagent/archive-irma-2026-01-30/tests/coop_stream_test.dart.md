---
path: lib/multiagent/archive-irma-2026-01-30/tests/coop_stream_test.dart
name: coop_stream_test.dart
purpose: "Cooperative Stream Multi-Agent Tests\n\nTests different allocations of producers and merger across agents.\nAll patterns use one-way data flow (no circular dependencies).\n\nPattern A: @1 produces Xs, @2 produces Ys and merges\n  @1: producer1(Xs)           -- writes Xs = [1,2,3]\n  @2: producer2(Ys), merge(Xs?, Ys?, Zs)  -- reads Xs?, writes Ys and Zs\n\nPattern B: @1 produces Xs and merges, @2 produces Ys\n  @1: producer1(Xs), merge(Xs?, Ys?, Zs)  -- writes Xs and Zs, reads Ys?\n  @2: producer2(Ys)           -- writes Ys = [a,b,c]\n\nProgram:\n  producer1([1,2,3]).\n  producer2([a,b,c]).\n  merge([X|Xs], Ys, [X?|Zs?]) :- merge(Ys?, Xs?, Zs).\n  merge(Xs, [Y|Ys], [Y?|Zs?]) :- merge(Xs?, Ys?, Zs).\n  merge([], [], []).\n"
key_idea: "Cooperative Stream Multi-Agent Tests\n\nTests different allocations of producers and merger across agents.\nAll patterns use one-way data flow (no circular dependencies).\n\nPattern A: @1 produces Xs, @2 produces Ys and merges\n  @1: producer1(Xs)           -- writes Xs = [1,2,3]\n  @2: producer2(Ys), merge(Xs?, Ys?, Zs)  -- reads Xs?, writes Ys and Zs\n\nPattern B: @1 produces Xs and merges, @2 produces Ys\n  @1: producer1(Xs), merge(Xs?, Ys?, Zs)  -- writes Xs and Zs, reads Ys?\n  @2: producer2(Ys)           -- writes Ys = [a,b,c]\n\nProgram:\n  producer1([1,2,3]).\n  producer2([a,b,c]).\n  merge([X|Xs], Ys, [X?|Zs?]) :- merge(Ys?, Xs?, Zs).\n  merge(Xs, [Y|Ys], [Y?|Zs?]) :- merge(Xs?, Ys?, Zs).\n  merge([], [], []).\n"
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
mtime: '2026-05-21T12:38:14.258Z'
sha256: 4567b4fc9f678dcbb3d120b68f36a8f374a02b60495a12aa19212aa685e8b284
target_path: lib/multiagent/archive-irma-2026-01-30/tests/coop_stream_test.cs
purpose_source: doc
key_idea_source: doc
---

Cooperative Stream Multi-Agent Tests

Tests different allocations of producers and merger across agents.
All patterns use one-way data flow (no circular dependencies).

Pattern A: @1 produces Xs, @2 produces Ys and merges
  @1: producer1(Xs)           -- writes Xs = [1,2,3]
  @2: producer2(Ys), merge(Xs?, Ys?, Zs)  -- reads Xs?, writes Ys and Zs

Pattern B: @1 produces Xs and merges, @2 produces Ys
  @1: producer1(Xs), merge(Xs?, Ys?, Zs)  -- writes Xs and Zs, reads Ys?
  @2: producer2(Ys)           -- writes Ys = [a,b,c]

Program:
  producer1([1,2,3]).
  producer2([a,b,c]).
  merge([X|Xs], Ys, [X?|Zs?]) :- merge(Ys?, Xs?, Zs).
  merge(Xs, [Y|Ys], [Y?|Zs?]) :- merge(Xs?, Ys?, Zs).
  merge([], [], []).
