---
path: lib/analysis/type_checker/mode.dart
name: mode.dart
purpose: Defines the two-valued data-flow Mode (input/output, aka consume/produce) used throughout moded type checking.
key_idea: enum Mode {output,input} with consume/produce aliases, a dual/flip getter that inverts direction at call boundaries, and combineMode implementing mode involution as XOR (equal modes -> output, differing -> input).
dependencies: []
callers:
- lib/analysis/type_checker/moded_head.dart
- lib/analysis/type_checker/moded_term.dart
- lib/analysis/type_checker/program_dfa.dart
- lib/analysis/type_checker/well_typed_clause.dart
- lib/analysis/type_checker/well_typed_term.dart
- test/analysis/type_checker/moded_head_test.dart
- test/analysis/type_checker/well_typed_clause_test.dart
- test/analysis/type_checker/well_typed_term_test.dart
mtime: '2026-05-21T12:38:12.722Z'
sha256: 48ca1f3517f5fd668631dff7c4b48b31567276ca330644b7c24892427aaa8e78
topo_level: 0
cycle_group_id: 5
status: ready
target_path: lib/analysis/type_checker/mode.cs
plan_started_at: '2026-05-21T14:23:17Z'
plan_completed_at: '2026-05-21T14:34:31Z'
plan_path: .codeconv/conversion-plans/lib/analysis/type_checker/mode.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T08:53:55Z'
target_cs_path: out/csharp/lib/analysis/type_checker/mode.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Defines the two-valued data-flow Mode (input/output, aka consume/produce) used throughout moded type checking.
