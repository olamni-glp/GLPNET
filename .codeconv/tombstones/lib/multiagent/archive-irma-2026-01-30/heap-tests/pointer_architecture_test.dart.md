---
path: lib/multiagent/archive-irma-2026-01-30/heap-tests/pointer_architecture_test.dart
name: pointer_architecture_test.dart
purpose: 'Tests for Pointer Architecture Heap (v3.0 spec)


  Derived from: docs/heap-pointer-architecture-spec.md v3.0


  Key properties tested:

  - Reader cells point TO writer cells (Section 3)

  - Writer cells contain null, SuspensionListNode, or Pointer (Section 2.3)

  - Dereferencing follows pointers (Section 4)

  - Suspensions live on writer cells (Section 6)

  - Imported readers point to VariableEntry (Section 10)

  '
key_idea: 'Tests for Pointer Architecture Heap (v3.0 spec)


  Derived from: docs/heap-pointer-architecture-spec.md v3.0


  Key properties tested:

  - Reader cells point TO writer cells (Section 3)

  - Writer cells contain null, SuspensionListNode, or Pointer (Section 2.3)

  - Dereferencing follows pointers (Section 4)

  - Suspensions live on writer cells (Section 6)

  - Imported readers point to VariableEntry (Section 10)

  '
dependencies:
- lib/multiagent/variable_table.dart
- lib/runtime/heap_fcp.dart
- lib/runtime/machine_state.dart
- lib/runtime/suspension.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-05-21T12:38:14.124Z'
sha256: 8704c05f6c2f9ff2c69923eb46cb3eb3d042f22ffd1f760415aaf4e5dfd90b1a
target_path: lib/multiagent/archive-irma-2026-01-30/heap-tests/pointer_architecture_test.cs
purpose_source: doc
key_idea_source: doc
---

Tests for Pointer Architecture Heap (v3.0 spec)

Derived from: docs/heap-pointer-architecture-spec.md v3.0

Key properties tested:
- Reader cells point TO writer cells (Section 3)
- Writer cells contain null, SuspensionListNode, or Pointer (Section 2.3)
- Dereferencing follows pointers (Section 4)
- Suspensions live on writer cells (Section 6)
- Imported readers point to VariableEntry (Section 10)
