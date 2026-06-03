---
path: lib/multiagent/global_writers_table.dart
name: global_writers_table.dart
purpose: 'Global Writers Table for madGLP


  Tracks local writers that await incoming assignments from remote agents.

  Each entry maps a global name to the local writer that will be assigned

  when a message arrives.


  See: docs/ma/madGLP-spec.md Section 3

  '
key_idea: 'Global Writers Table for madGLP


  Tracks local writers that await incoming assignments from remote agents.

  Each entry maps a global name to the local writer that will be assigned

  when a message arrives.


  See: docs/ma/madGLP-spec.md Section 3

  '
dependencies: []
callers:
- lib/multiagent/archive-irma-2026-01-30/irma_context-phase4.dart
- lib/multiagent/global_send.dart
- lib/multiagent/mad_context.dart
- lib/multiagent/mad_helpers.dart
- test/multiagent/global_send_test.dart
- test/multiagent/global_writers_table_test.dart
- test/multiagent/globalize_test.dart
- test/multiagent/localize_test.dart
mtime: '2026-05-21T12:38:13.650Z'
sha256: 7ebe135c209066d868a420d5df4f3fc0be289656a597faa3e0acb5f6d371b9f1
topo_level: 0
cycle_group_id: 25
status: ready
target_path: lib/multiagent/global_writers_table.cs
plan_started_at: '2026-05-21T14:35:52Z'
plan_completed_at: '2026-05-21T14:41:06Z'
plan_path: .codeconv/conversion-plans/lib/multiagent/global_writers_table.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T09:01:56Z'
target_cs_path: out/csharp/lib/multiagent/global_writers_table.cs
build_status: pass
codegen_open_escalation_count: 0
---

Global Writers Table for madGLP

Tracks local writers that await incoming assignments from remote agents.
Each entry maps a global name to the local writer that will be assigned
when a message arrives.

See: docs/ma/madGLP-spec.md Section 3
