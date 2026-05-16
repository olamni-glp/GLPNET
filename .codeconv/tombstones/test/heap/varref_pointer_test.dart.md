---
path: test/heap/varref_pointer_test.dart
name: varref_pointer_test.dart
purpose: 'Tests for VarRef structure with Pointer Architecture


  For spec: docs/heap-pointer-architecture-spec.md v3.0


  In the new architecture, VarRef has only an addr field.

  The cell''s tag determines whether it''s a reader or writer.

  '
key_idea: 'Tests for VarRef structure with Pointer Architecture


  For spec: docs/heap-pointer-architecture-spec.md v3.0


  In the new architecture, VarRef has only an addr field.

  The cell''s tag determines whether it''s a reader or writer.

  '
dependencies:
- lib/runtime/heap_fcp.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-04-27T09:23:50.000Z'
sha256: 574fc311c885281ba54ebf6d357e14982f727a622c7707e0a5e5a67aa61b1ed7
topo_level: 3
cycle_group_id: 90
status: pending
---

Tests for VarRef structure with Pointer Architecture

For spec: docs/heap-pointer-architecture-spec.md v3.0

In the new architecture, VarRef has only an addr field.
The cell's tag determines whether it's a reader or writer.
