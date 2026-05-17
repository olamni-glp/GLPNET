---
path: lib/multiagent/archive-irma-2026-01-30/tests/distribute_test.dart
name: distribute_test.dart
purpose: "Distribution Test (1 Producer, 2 Consumers)\n\nTests broadcast distribution:\n  @1 (producer): produces [1, 2, 3], distributes to both @2 and @3\n  @2 (consumer1): receives copy Y = [1, 2, 3]\n  @3 (consumer2): receives copy Z = [1, 2, 3]\n\nData flow: @1 → @2 AND @1 → @3 (broadcast)\n\nBased on GLP distribute/3:\n  distribute([X|Xs], [X?|Ys?], [X?|Zs?]) :- ground(X?) | distribute(Xs?, Ys, Zs).\n  distribute([], [], []).\n"
key_idea: "Distribution Test (1 Producer, 2 Consumers)\n\nTests broadcast distribution:\n  @1 (producer): produces [1, 2, 3], distributes to both @2 and @3\n  @2 (consumer1): receives copy Y = [1, 2, 3]\n  @3 (consumer2): receives copy Z = [1, 2, 3]\n\nData flow: @1 → @2 AND @1 → @3 (broadcast)\n\nBased on GLP distribute/3:\n  distribute([X|Xs], [X?|Ys?], [X?|Zs?]) :- ground(X?) | distribute(Xs?, Ys, Zs).\n  distribute([], [], []).\n"
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
mtime: '2026-05-17T10:36:35.376Z'
sha256: 1df6d645a7d8eab129c2e8cfc5bd4572b23d0705368c95dd3bac6483e9e843db
target_path: lib/multiagent/archive-irma-2026-01-30/tests/distribute_test.cs
---

Distribution Test (1 Producer, 2 Consumers)

Tests broadcast distribution:
  @1 (producer): produces [1, 2, 3], distributes to both @2 and @3
  @2 (consumer1): receives copy Y = [1, 2, 3]
  @3 (consumer2): receives copy Z = [1, 2, 3]

Data flow: @1 → @2 AND @1 → @3 (broadcast)

Based on GLP distribute/3:
  distribute([X|Xs], [X?|Ys?], [X?|Zs?]) :- ground(X?) | distribute(Xs?, Ys, Zs).
  distribute([], [], []).
