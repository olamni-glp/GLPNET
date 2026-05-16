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
mtime: '2026-04-27T09:23:50.000Z'
sha256: ec6e1f4d6555f57c8b7450418b64282524e86e8b2ba6d06323047da3c7a64b05
topo_level: 4
cycle_group_id: 37
status: pending
---

System predicate execution infrastructure for GLP

System predicates are external functions (implemented in Dart) that can be
called from GLP programs via the `execute` instruction. They provide:
- I/O operations (file, terminal, network)
- Arithmetic evaluation
- System information (time, IDs, etc.)
- Any operation requiring side effects or host interaction

Inspired by FCP's execute mechanism but adapted for Dart.
