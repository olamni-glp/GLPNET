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
- lib/multiagent/archive-irma-2026-01-30/heap-tests/pointer_architecture_test.dart
- lib/multiagent/archive-irma-2026-01-30/heap-tests/shared_variable_pointer_test.dart
- lib/multiagent/archive-irma-2026-01-30/helpers-current.dart
- lib/multiagent/archive-irma-2026-01-30/helpers.dart
- lib/multiagent/archive-irma-2026-01-30/irma_context-phase4.dart
- lib/multiagent/archive-irma-2026-01-30/irma_context.dart
- lib/multiagent/archive-irma-2026-01-30/tests/bidirectional_exchange_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/bidirectional_stream_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/coop_stream_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/distribute_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/friend_introduction_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/helpers_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/irma_agent_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/irma_context_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/isolate_friend_introduction_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/isolate_play_alice_bob_charlie_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/play_alice_bob_charlie_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/reversed_flow_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/shared_variable_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/simple_imported_reader_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/three_agent_merge_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/three_agent_pipeline_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/two_hop_flow_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/variable_table_test.dart
- lib/runtime/heap_fcp.dart
- lib/runtime/suspend_ops.dart
mtime: '2026-04-27T09:23:50.000Z'
sha256: 39633ffa950c42f693d1b6053f910084556d05aad2174a2fdc91850d5f3eff83
topo_level: 1
cycle_group_id: 24
status: pending
target_path: lib/multiagent/variable_table.cs
plan_started_at: '2026-05-21T14:45:44Z'
plan_completed_at: '2026-05-21T14:50:30Z'
plan_path: .codeconv/conversion-plans/lib/multiagent/variable_table.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T09:18:16Z'
target_cs_path: out/csharp/lib/multiagent/variable_table.cs
build_status: pass
codegen_open_escalation_count: 0
---

Minimal Variable Entry for multiagent runtime support

Provides VariableEntry for tracking suspensions on imported readers.
The full VariableTable (V_p) has been replaced by GlobalWritersTable (W_p)
for madGLP. This file provides only the entry type needed by the core
runtime for suspension management.
