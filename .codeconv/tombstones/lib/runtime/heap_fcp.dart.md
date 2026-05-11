---
path: lib/runtime/heap_fcp.dart
name: heap_fcp.dart
purpose: 'FCP Two-Cell Heap with Pointer Architecture


  Per heap-pointer-architecture-spec.md v3.0:

  - Reader cells point TO writer cells

  - Writer cells contain: null (unbound), SuspensionListNode (waiting), or Pointer (bound to var)

  - Suspensions live on writer cells, not reader cells

  - ValueTag indicates bound to ground value

  '
key_idea: 'FCP Two-Cell Heap with Pointer Architecture


  Per heap-pointer-architecture-spec.md v3.0:

  - Reader cells point TO writer cells

  - Writer cells contain: null (unbound), SuspensionListNode (waiting), or Pointer (bound to var)

  - Suspensions live on writer cells, not reader cells

  - ValueTag indicates bound to ground value

  '
dependencies: []
callers:
- lib/runtime/commit.dart
- lib/runtime/external_io.dart
- lib/runtime/runtime.dart
- lib/runtime/suspend_ops.dart
mtime: '2026-04-27T09:23:50.000Z'
sha256: 18b5962454f8a7e7d8d1b48c9d711bfe92b3699180dcc4d9ac7a3288a26378f3
---

FCP Two-Cell Heap with Pointer Architecture

Per heap-pointer-architecture-spec.md v3.0:
- Reader cells point TO writer cells
- Writer cells contain: null (unbound), SuspensionListNode (waiting), or Pointer (bound to var)
- Suspensions live on writer cells, not reader cells
- ValueTag indicates bound to ground value
