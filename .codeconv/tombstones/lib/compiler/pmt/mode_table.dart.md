---
path: lib/compiler/pmt/mode_table.dart
name: mode_table.dart
purpose: 'PMT Mode Table: Stores mode declarations for type checking


  Maps predicate signatures (e.g., "merge/3") to their argument modes

  (reader/writer for each position).


  Supports multiple mode alternatives for the same predicate/arity

  (union of mode declarations).

  '
key_idea: 'PMT Mode Table: Stores mode declarations for type checking


  Maps predicate signatures (e.g., "merge/3") to their argument modes

  (reader/writer for each position).


  Supports multiple mode alternatives for the same predicate/arity

  (union of mode declarations).

  '
dependencies:
- lib/compiler/ast.dart
callers:
- lib/compiler/pmt/checker.dart
- lib/compiler/pmt/occurrence.dart
- lib/compiler/pmt/type_checker.dart
- lib/compiler/pmt/validator.dart
mtime: '2026-04-27T09:23:50.000Z'
sha256: 64b7072930ab5bb1260bf1507e236ce16dff7494d456d04154c142fdd4618b21
topo_level: 2
cycle_group_id: 46
status: pending
target_path: lib/compiler/pmt/mode_table.cs
plan_started_at: '2026-05-21T14:58:36Z'
plan_completed_at: '2026-05-21T15:09:17Z'
plan_path: .codeconv/conversion-plans/lib/compiler/pmt/mode_table.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T11:50:03Z'
target_cs_path: out/csharp/lib/compiler/pmt/mode_table.cs
build_status: pass
codegen_open_escalation_count: 0
---

PMT Mode Table: Stores mode declarations for type checking

Maps predicate signatures (e.g., "merge/3") to their argument modes
(reader/writer for each position).

Supports multiple mode alternatives for the same predicate/arity
(union of mode declarations).
