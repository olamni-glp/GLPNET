---
path: test/heap/circular_term_pointer_test.dart
name: circular_term_pointer_test.dart
purpose: 'Tests for circular term handling with Pointer Architecture Heap


  Adapted from: test/circular_term_test.dart

  For spec: docs/heap-pointer-architecture-spec.md v3.0


  Circular terms can form through cross-goal communication when two goals

  share variables and bind them in ways that create cycles. These tests

  verify that the runtime handles such terms gracefully with the new

  pointer-based heap architecture.

  '
key_idea: 'Tests for circular term handling with Pointer Architecture Heap


  Adapted from: test/circular_term_test.dart

  For spec: docs/heap-pointer-architecture-spec.md v3.0


  Circular terms can form through cross-goal communication when two goals

  share variables and bind them in ways that create cycles. These tests

  verify that the runtime handles such terms gracefully with the new

  pointer-based heap architecture.

  '
dependencies:
- lib/runtime/heap_fcp.dart
- lib/runtime/runtime.dart
- lib/runtime/system_predicates.dart
- lib/runtime/system_predicates_impl.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-04-27T09:23:50.000Z'
sha256: b239cd5fda24cb63efa4f4406ee8b94ad54679a79761f06f188268f7cbd60dff
topo_level: 6
cycle_group_id: 88
status: pending
target_path: test/heap/circular_term_pointer_test.cs
plan_started_at: '2026-05-20T02:43:36Z'
plan_completed_at: '2026-05-20T02:43:36Z'
plan_path: null
open_escalation_count: 0
---

Tests for circular term handling with Pointer Architecture Heap

Adapted from: test/circular_term_test.dart
For spec: docs/heap-pointer-architecture-spec.md v3.0

Circular terms can form through cross-goal communication when two goals
share variables and bind them in ways that create cycles. These tests
verify that the runtime handles such terms gracefully with the new
pointer-based heap architecture.
