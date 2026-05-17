---
path: lib/multiagent/archive-irma-2026-01-30/tests/reversed_flow_test.dart
name: reversed_flow_test.dart
purpose: "Reversed Flow Test\n\nTests one-way data flow with REVERSED direction:\n  @1: q(X?)     -- receives X? as imported reader, processes list\n  @2: p(X)      -- binds X = [a,b]\n\nThis is the opposite direction of simple_imported_reader_test.dart\nto verify data can flow @2 -> @1 as well as @1 -> @2.\n\nProgram:\n  p([a,b]).\n  q([X|Xs]) :- q(Xs?).\n  q([]).\n"
key_idea: "Reversed Flow Test\n\nTests one-way data flow with REVERSED direction:\n  @1: q(X?)     -- receives X? as imported reader, processes list\n  @2: p(X)      -- binds X = [a,b]\n\nThis is the opposite direction of simple_imported_reader_test.dart\nto verify data can flow @2 -> @1 as well as @1 -> @2.\n\nProgram:\n  p([a,b]).\n  q([X|Xs]) :- q(Xs?).\n  q([]).\n"
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
mtime: '2026-05-17T10:36:35.524Z'
sha256: 90e701903439bfa25f0b2af5d4658d692dd946c8e8c766a34a64e658b57a34b2
target_path: lib/multiagent/archive-irma-2026-01-30/tests/reversed_flow_test.cs
---

Reversed Flow Test

Tests one-way data flow with REVERSED direction:
  @1: q(X?)     -- receives X? as imported reader, processes list
  @2: p(X)      -- binds X = [a,b]

This is the opposite direction of simple_imported_reader_test.dart
to verify data can flow @2 -> @1 as well as @1 -> @2.

Program:
  p([a,b]).
  q([X|Xs]) :- q(Xs?).
  q([]).
