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
dependencies:
- lib/multiagent/variable_table.dart
- lib/runtime/machine_state.dart
- lib/runtime/suspension.dart
- lib/runtime/terms.dart
callers:
- lib/multiagent/archive-irma-2026-01-30/heap-tests/pointer_architecture_test.dart
- lib/multiagent/archive-irma-2026-01-30/heap-tests/shared_variable_pointer_test.dart
- lib/runtime/commit.dart
- lib/runtime/external_io.dart
- lib/runtime/glp_activation.dart
- lib/runtime/runtime.dart
- lib/runtime/suspend_ops.dart
- test/conformance/restart_clause1_test.dart
- test/heap/binding_pointer_test.dart
- test/heap/circular_term_pointer_test.dart
- test/heap/suspension_pointer_test.dart
- test/heap/varref_pointer_test.dart
- test/test_channel_construction.dart
- test_archive/serve_test.dart
mtime: '2026-05-21T12:38:15.016Z'
sha256: 18b5962454f8a7e7d8d1b48c9d711bfe92b3699180dcc4d9ac7a3288a26378f3
topo_level: 2
cycle_group_id: 32
status: pending
target_path: lib/runtime/heap_fcp.cs
plan_started_at: '2026-05-21T15:18:03Z'
plan_completed_at: '2026-05-21T15:24:01Z'
plan_path: .codeconv/conversion-plans/lib/runtime/heap_fcp.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T11:48:53Z'
target_cs_path: out/csharp/lib/runtime/heap_fcp.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: doc
key_idea_source: doc
---

FCP Two-Cell Heap with Pointer Architecture

Per heap-pointer-architecture-spec.md v3.0:
- Reader cells point TO writer cells
- Writer cells contain: null (unbound), SuspensionListNode (waiting), or Pointer (bound to var)
- Suspensions live on writer cells, not reader cells
- ValueTag indicates bound to ground value
