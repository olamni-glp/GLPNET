---
path: lib/compiler/pmt/checker.dart
name: checker.dart
purpose: 'PMT SRSW Checker: Verifies Single-Reader/Single-Writer constraint


  SRSW Rules:

  - Each variable must have exactly 1 writer occurrence

  - Each variable must have at least 1 reader occurrence

  - Multiple reader occurrences allowed only if variable is grounded by a guard


  Supports multiple mode alternatives (union of mode declarations).

  A clause is valid if it satisfies SRSW for at least one declared mode.

  '
key_idea: 'PMT SRSW Checker: Verifies Single-Reader/Single-Writer constraint


  SRSW Rules:

  - Each variable must have exactly 1 writer occurrence

  - Each variable must have at least 1 reader occurrence

  - Multiple reader occurrences allowed only if variable is grounded by a guard


  Supports multiple mode alternatives (union of mode declarations).

  A clause is valid if it satisfies SRSW for at least one declared mode.

  '
dependencies:
- lib/compiler/ast.dart
- lib/compiler/pmt/errors.dart
- lib/compiler/pmt/mode_table.dart
- lib/compiler/pmt/occurrence.dart
callers:
- lib/compiler/pmt/validator.dart
mtime: '2026-05-21T12:38:13.393Z'
sha256: 2cdf947748a1e9b0f92210357cda90b7f453ebb6b9111c75db0445a7ade131ef
topo_level: 4
cycle_group_id: 48
status: pending
target_path: lib/compiler/pmt/checker.cs
plan_started_at: '2026-05-21T16:13:07Z'
plan_completed_at: '2026-05-21T16:17:35Z'
plan_path: .codeconv/conversion-plans/lib/compiler/pmt/checker.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T12:34:05Z'
target_cs_path: out/csharp/lib/compiler/pmt/checker.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: doc
key_idea_source: doc
---

PMT SRSW Checker: Verifies Single-Reader/Single-Writer constraint

SRSW Rules:
- Each variable must have exactly 1 writer occurrence
- Each variable must have at least 1 reader occurrence
- Multiple reader occurrences allowed only if variable is grounded by a guard

Supports multiple mode alternatives (union of mode declarations).
A clause is valid if it satisfies SRSW for at least one declared mode.
