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
- lib/multiagent/variable_table.dart
- lib/runtime/commit.dart
- lib/runtime/heap_fcp.dart
- lib/runtime/suspend_ops.dart
- test/heap/binding_pointer_test.dart
- test/heap/suspension_pointer_test.dart
mtime: '2026-04-27T09:23:50.000Z'
sha256: b7e9c01b3b5ca5a3922c8a3656221803797fd5b434cecc8d63412d94d9c61319
topo_level: 0
cycle_group_id: 22
status: ready
---

Shared Suspension Records (FCP Design)
One SuspensionRecord shared across multiple lists via wrapper nodes
Activated once, then disarmed to prevent double-activation
