---
path: test/heap/suspension_pointer_test.dart
name: suspension_pointer_test.dart
purpose: 'Tests for suspension and reactivation with Pointer Architecture Heap


  Adapted from: test/conformance/restart_clause1_test.dart

  For spec: docs/heap-pointer-architecture-spec.md v3.0


  Key changes from original:

  - Suspensions now live on WRITER cells (not reader cells)

  - suspendOnWriter/suspendOnReader API

  - bindWriter replaces bindVariable

  '
key_idea: 'Tests for suspension and reactivation with Pointer Architecture Heap


  Adapted from: test/conformance/restart_clause1_test.dart

  For spec: docs/heap-pointer-architecture-spec.md v3.0


  Key changes from original:

  - Suspensions now live on WRITER cells (not reader cells)

  - suspendOnWriter/suspendOnReader API

  - bindWriter replaces bindVariable

  '
dependencies:
- lib/runtime/heap_fcp.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/suspension.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-04-27T09:23:50.000Z'
sha256: 8f0020be2a925a63f498abf316c3e5ea71c60b9a6e5ef23bbffbc1615d4b2a95
topo_level: 5
cycle_group_id: 89
status: pending
target_path: test/heap/suspension_pointer_test.cs
plan_started_at: '2026-05-20T02:25:35Z'
plan_completed_at: '2026-05-20T02:25:35Z'
plan_path: null
open_escalation_count: 0
---

Tests for suspension and reactivation with Pointer Architecture Heap

Adapted from: test/conformance/restart_clause1_test.dart
For spec: docs/heap-pointer-architecture-spec.md v3.0

Key changes from original:
- Suspensions now live on WRITER cells (not reader cells)
- suspendOnWriter/suspendOnReader API
- bindWriter replaces bindVariable
