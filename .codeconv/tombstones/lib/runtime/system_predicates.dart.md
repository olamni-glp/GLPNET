---
path: lib/runtime/system_predicates.dart
name: system_predicates.dart
purpose: 'System predicate execution infrastructure for GLP


  System predicates are external functions (implemented in Dart) that can be

  called from GLP programs via the `execute` instruction. They provide:

  - I/O operations (file, terminal, network)

  - Arithmetic evaluation

  - System information (time, IDs, etc.)

  - Any operation requiring side effects or host interaction


  Inspired by FCP''s execute mechanism but adapted for Dart.

  '
key_idea: 'System predicate execution infrastructure for GLP


  System predicates are external functions (implemented in Dart) that can be

  called from GLP programs via the `execute` instruction. They provide:

  - I/O operations (file, terminal, network)

  - Arithmetic evaluation

  - System information (time, IDs, etc.)

  - Any operation requiring side effects or host interaction


  Inspired by FCP''s execute mechanism but adapted for Dart.

  '
dependencies:
- lib/runtime/runtime.dart
- lib/runtime/terms.dart
callers:
- lib/bytecode/runner.dart
- lib/runtime/runtime.dart
- lib/runtime/system_predicates_impl.dart
- test/circular_term_test.dart
- test/heap/circular_term_pointer_test.dart
mtime: '2026-05-21T12:38:15.172Z'
sha256: ec6e1f4d6555f57c8b7450418b64282524e86e8b2ba6d06323047da3c7a64b05
topo_level: 4
cycle_group_id: 37
status: pending
target_path: lib/runtime/system_predicates.cs
plan_started_at: '2026-05-21T16:06:19Z'
plan_completed_at: '2026-05-21T16:12:54Z'
plan_path: .codeconv/conversion-plans/lib/runtime/system_predicates.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T12:39:23Z'
target_cs_path: out/csharp/lib/runtime/system_predicates.cs
build_status: pass
codegen_open_escalation_count: 0
---

System predicate execution infrastructure for GLP

System predicates are external functions (implemented in Dart) that can be
called from GLP programs via the `execute` instruction. They provide:
- I/O operations (file, terminal, network)
- Arithmetic evaluation
- System information (time, IDs, etc.)
- Any operation requiring side effects or host interaction

Inspired by FCP's execute mechanism but adapted for Dart.
