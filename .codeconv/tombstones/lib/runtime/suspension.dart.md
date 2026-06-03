---
path: lib/runtime/suspension.dart
name: suspension.dart
purpose: 'Shared Suspension Records (FCP Design)

  One SuspensionRecord shared across multiple lists via wrapper nodes

  Activated once, then disarmed to prevent double-activation

  '
key_idea: 'Shared Suspension Records (FCP Design)

  One SuspensionRecord shared across multiple lists via wrapper nodes

  Activated once, then disarmed to prevent double-activation

  '
dependencies: []
callers:
- lib/multiagent/archive-irma-2026-01-30/heap-tests/pointer_architecture_test.dart
- lib/multiagent/archive-irma-2026-01-30/irma_context-phase4.dart
- lib/multiagent/archive-irma-2026-01-30/irma_context.dart
- lib/multiagent/variable_table.dart
- lib/runtime/commit.dart
- lib/runtime/heap_fcp.dart
- lib/runtime/suspend_ops.dart
- test/heap/binding_pointer_test.dart
- test/heap/suspension_pointer_test.dart
mtime: '2026-05-21T12:38:15.150Z'
sha256: b7e9c01b3b5ca5a3922c8a3656221803797fd5b434cecc8d63412d94d9c61319
topo_level: 0
cycle_group_id: 22
status: ready
target_path: lib/runtime/suspension.cs
plan_started_at: '2026-05-21T14:41:29Z'
plan_completed_at: '2026-05-21T14:45:18Z'
plan_path: .codeconv/conversion-plans/lib/runtime/suspension.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T09:09:05Z'
target_cs_path: out/csharp/lib/runtime/suspension.cs
build_status: pass
codegen_open_escalation_count: 0
---

Shared Suspension Records (FCP Design)
One SuspensionRecord shared across multiple lists via wrapper nodes
Activated once, then disarmed to prevent double-activation
