---
path: lib/multiagent/archive-irma-2026-01-30/heap-tests/shared_variable_pointer_test.dart
name: shared_variable_pointer_test.dart
purpose: "Tests for shared logic variables with Pointer Architecture Heap\n\nAdapted from: test/multiagent/shared_variable_test.dart\nFor spec: docs/heap-pointer-architecture-spec.md v3.0\n\nTests the multiagent scenario where:\n  @1 has writer X, runs p(X)\n  @2 has imported reader X?, runs q(X?)\n\nKey changes from original:\n- allocateVariable returns (writerAddr, readerAddr) tuple\n- VarRef has just addr field\n- Imported readers have VariableEntry as content\n- Suspensions live on writer cells\n"
key_idea: "Tests for shared logic variables with Pointer Architecture Heap\n\nAdapted from: test/multiagent/shared_variable_test.dart\nFor spec: docs/heap-pointer-architecture-spec.md v3.0\n\nTests the multiagent scenario where:\n  @1 has writer X, runs p(X)\n  @2 has imported reader X?, runs q(X?)\n\nKey changes from original:\n- allocateVariable returns (writerAddr, readerAddr) tuple\n- VarRef has just addr field\n- Imported readers have VariableEntry as content\n- Suspensions live on writer cells\n"
dependencies:
- lib/bytecode/runner.dart
- lib/compiler/compiler.dart
- lib/multiagent/mad_context.dart
- lib/multiagent/message_queue.dart
- lib/multiagent/variable_table.dart
- lib/runtime/heap_fcp.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/scheduler.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-05-21T12:38:14.159Z'
sha256: 72e0165798d73303437fed0898a6bff52a16ac76ee75ba0b8a841c192140541f
target_path: lib/multiagent/archive-irma-2026-01-30/heap-tests/shared_variable_pointer_test.cs
---

Tests for shared logic variables with Pointer Architecture Heap

Adapted from: test/multiagent/shared_variable_test.dart
For spec: docs/heap-pointer-architecture-spec.md v3.0

Tests the multiagent scenario where:
  @1 has writer X, runs p(X)
  @2 has imported reader X?, runs q(X?)

Key changes from original:
- allocateVariable returns (writerAddr, readerAddr) tuple
- VarRef has just addr field
- Imported readers have VariableEntry as content
- Suspensions live on writer cells
