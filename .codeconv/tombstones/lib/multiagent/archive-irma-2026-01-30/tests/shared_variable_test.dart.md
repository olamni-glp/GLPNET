---
path: lib/multiagent/archive-irma-2026-01-30/tests/shared_variable_test.dart
name: shared_variable_test.dart
purpose: "Minimal test for shared logic variables between two isolates\n\nSetup:\n  @1 has writer X, runs p(X)\n  @2 has imported reader X?, runs q(X?)\n  \nProgram:\n  p(a).\n  q(a).\n\nExpected flow:\n  1. @1 runs p(X), matches p(a), binds X = a\n  2. @1 sends assignment X? := a to @2\n  3. @2 receives assignment, binds X? = a\n  4. @2 runs q(X?), X? is now bound to a, matches q(a)\n\nDesign doc: /docs/ma/shared-variable-test-design.md\n"
key_idea: "Minimal test for shared logic variables between two isolates\n\nSetup:\n  @1 has writer X, runs p(X)\n  @2 has imported reader X?, runs q(X?)\n  \nProgram:\n  p(a).\n  q(a).\n\nExpected flow:\n  1. @1 runs p(X), matches p(a), binds X = a\n  2. @1 sends assignment X? := a to @2\n  3. @2 receives assignment, binds X? = a\n  4. @2 runs q(X?), X? is now bound to a, matches q(a)\n\nDesign doc: /docs/ma/shared-variable-test-design.md\n"
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
mtime: '2026-05-21T12:38:14.656Z'
sha256: cb33bde9c954057355da6e5a2b5497612a5ad298ada2277e1680c60d2e4225d3
target_path: lib/multiagent/archive-irma-2026-01-30/tests/shared_variable_test.cs
---

Minimal test for shared logic variables between two isolates

Setup:
  @1 has writer X, runs p(X)
  @2 has imported reader X?, runs q(X?)
  
Program:
  p(a).
  q(a).

Expected flow:
  1. @1 runs p(X), matches p(a), binds X = a
  2. @1 sends assignment X? := a to @2
  3. @2 receives assignment, binds X? = a
  4. @2 runs q(X?), X? is now bound to a, matches q(a)

Design doc: /docs/ma/shared-variable-test-design.md
