---
path: lib/multiagent/archive-irma-2026-01-30/tests/bidirectional_stream_test.dart
name: bidirectional_stream_test.dart
purpose: "Bidirectional Stream Test\n\nTwo isolates running circular merge:\n  Isolate 1: merge(Xs?, [a], Ys)  -- reads Xs from isolate 2, writes Ys\n  Isolate 2: merge(Ys?, [b], Xs)  -- reads Ys from isolate 1, writes Xs\n\nExpected infinite pattern:\n  From clause 2 firing when first arg is still unbound:\n  Ys = [a, b, a, b, ...]\n  Xs = [b, a, b, a, ...]\n\nThis test stops after 20 elements are produced.\n"
key_idea: "Bidirectional Stream Test\n\nTwo isolates running circular merge:\n  Isolate 1: merge(Xs?, [a], Ys)  -- reads Xs from isolate 2, writes Ys\n  Isolate 2: merge(Ys?, [b], Xs)  -- reads Ys from isolate 1, writes Xs\n\nExpected infinite pattern:\n  From clause 2 firing when first arg is still unbound:\n  Ys = [a, b, a, b, ...]\n  Xs = [b, a, b, a, ...]\n\nThis test stops after 20 elements are produced.\n"
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
mtime: '2026-05-17T10:36:35.352Z'
sha256: 0a701a84d49a792dee8bd342ce679ee3698899b4f1b71da19438ab8cac446099
target_path: lib/multiagent/archive-irma-2026-01-30/tests/bidirectional_stream_test.cs
---

Bidirectional Stream Test

Two isolates running circular merge:
  Isolate 1: merge(Xs?, [a], Ys)  -- reads Xs from isolate 2, writes Ys
  Isolate 2: merge(Ys?, [b], Xs)  -- reads Ys from isolate 1, writes Xs

Expected infinite pattern:
  From clause 2 firing when first arg is still unbound:
  Ys = [a, b, a, b, ...]
  Xs = [b, a, b, a, ...]

This test stops after 20 elements are produced.
