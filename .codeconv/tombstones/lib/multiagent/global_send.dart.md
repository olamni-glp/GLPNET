---
path: lib/multiagent/global_send.dart
name: global_send.dart
purpose: 'Global Send mechanism for madGLP


  Implements the `global_send` goal that watches a reader and sends

  its value to a remote agent when it becomes known.


  See: madGLP-spec.md Section 4 (The global_send Predicate)

  '
key_idea: 'Global Send mechanism for madGLP


  Implements the `global_send` goal that watches a reader and sends

  its value to a remote agent when it becomes known.


  See: madGLP-spec.md Section 4 (The global_send Predicate)

  '
dependencies:
- lib/multiagent/global_writers_table.dart
- lib/multiagent/mad_helpers.dart
callers:
- lib/multiagent/archive-irma-2026-01-30/irma_context-phase4.dart
- lib/multiagent/mad_context.dart
- test/multiagent/global_send_test.dart
- test/multiagent/mad_cold_call_isolate_test.dart
- test/multiagent/mad_scenarios_test.dart
- test/multiagent/mad_transactions_test.dart
mtime: '2026-04-27T09:23:50.000Z'
sha256: 06d2ab558a9c447dacd6c6766f747bbcc2bb613d4de39bbadcab50089cb08bd1
topo_level: 2
cycle_group_id: 27
status: pending
target_path: lib/multiagent/global_send.cs
plan_started_at: '2026-05-21T15:18:01Z'
plan_completed_at: '2026-05-21T15:23:59Z'
plan_path: .codeconv/conversion-plans/lib/multiagent/global_send.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T09:37:29Z'
target_cs_path: out/csharp/lib/multiagent/global_send.cs
build_status: pass
codegen_open_escalation_count: 0
---

Global Send mechanism for madGLP

Implements the `global_send` goal that watches a reader and sends
its value to a remote agent when it becomes known.

See: madGLP-spec.md Section 4 (The global_send Predicate)
