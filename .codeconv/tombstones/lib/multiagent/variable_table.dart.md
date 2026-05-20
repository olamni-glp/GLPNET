---
path: lib/multiagent/variable_table.dart
name: variable_table.dart
purpose: 'Minimal Variable Entry for multiagent runtime support


  Provides VariableEntry for tracking suspensions on imported readers.

  The full VariableTable (V_p) has been replaced by GlobalWritersTable (W_p)

  for madGLP. This file provides only the entry type needed by the core

  runtime for suspension management.

  '
key_idea: 'Minimal Variable Entry for multiagent runtime support


  Provides VariableEntry for tracking suspensions on imported readers.

  The full VariableTable (V_p) has been replaced by GlobalWritersTable (W_p)

  for madGLP. This file provides only the entry type needed by the core

  runtime for suspension management.

  '
dependencies:
- lib/runtime/suspension.dart
- lib/runtime/terms.dart
callers:
- lib/bytecode/runner.dart
- lib/runtime/heap_fcp.dart
- lib/runtime/suspend_ops.dart
mtime: '2026-04-27T09:23:50.000Z'
sha256: 39633ffa950c42f693d1b6053f910084556d05aad2174a2fdc91850d5f3eff83
topo_level: 1
cycle_group_id: 24
status: pending
target_path: lib/multiagent/variable_table.cs
plan_started_at: '2026-05-19T23:36:18Z'
plan_completed_at: '2026-05-19T23:36:18Z'
plan_path: null
open_escalation_count: 0
---

Minimal Variable Entry for multiagent runtime support

Provides VariableEntry for tracking suspensions on imported readers.
The full VariableTable (V_p) has been replaced by GlobalWritersTable (W_p)
for madGLP. This file provides only the entry type needed by the core
runtime for suspension management.
