---
path: lib/multiagent/archive-irma-2026-01-30/tests/simple_imported_reader_test.dart
name: simple_imported_reader_test.dart
purpose: "Simple Imported Reader Test\n\nTests one-way data flow with imported reader:\n  @1: p(X)      -- binds X = [a,b]\n  @2: q(X?)     -- receives X? as imported reader, processes list\n\nProgram:\n  p([a,b]).\n  q([X|Xs]) :- q(Xs?).\n  q([]).\n"
key_idea: "Simple Imported Reader Test\n\nTests one-way data flow with imported reader:\n  @1: p(X)      -- binds X = [a,b]\n  @2: q(X?)     -- receives X? as imported reader, processes list\n\nProgram:\n  p([a,b]).\n  q([X|Xs]) :- q(Xs?).\n  q([]).\n"
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
mtime: '2026-05-17T10:36:35.542Z'
sha256: 4ab0702838d0cf655cc0a92f13e29280ceafff3d4882a7702b38a363d9b4d703
target_path: lib/multiagent/archive-irma-2026-01-30/tests/simple_imported_reader_test.cs
---

Simple Imported Reader Test

Tests one-way data flow with imported reader:
  @1: p(X)      -- binds X = [a,b]
  @2: q(X?)     -- receives X? as imported reader, processes list

Program:
  p([a,b]).
  q([X|Xs]) :- q(Xs?).
  q([]).
