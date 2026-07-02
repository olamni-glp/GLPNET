---
path: lib/analysis/type_checker/program_dfa.dart
name: program_dfa.dart
purpose: Builds and represents the program's type DFA - states/automata for each defined type, its complement, and procedures - and checks leaf-term consistency (Def 4.3).
key_idea: buildProgramDFA creates T and T? states/automata per type (modes flipped + targets dualized for the complement), with mode-labeled transitions via involution; checkLeafConsistency matches a variable's reader/writer mode against the path mode, or a constant against type transitions/acceptedPrimitives.
dependencies:
- lib/analysis/type_checker/mode.dart
- lib/analysis/type_checker/type_ast.dart
callers:
- lib/analysis/type_checker/subtyping.dart
- lib/analysis/type_checker/type_checker.dart
- lib/analysis/type_checker/well_typed_clause.dart
- lib/analysis/type_checker/well_typed_term.dart
- test/analysis/type_checker/well_typed_clause_test.dart
- test/analysis/type_checker/well_typed_term_test.dart
mtime: '2026-05-21T12:38:12.833Z'
sha256: bf0151e2d78f26961d8153beede8211ba2f823b127de7ec7fd673299658a6057
topo_level: 1
cycle_group_id: 10
status: pending
target_path: lib/analysis/type_checker/program_dfa.cs
plan_started_at: '2026-05-21T14:45:39Z'
plan_completed_at: '2026-05-21T14:50:24Z'
plan_path: .codeconv/conversion-plans/lib/analysis/type_checker/program_dfa.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T09:17:57Z'
target_cs_path: out/csharp/lib/analysis/type_checker/program_dfa.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Builds and represents the program's type DFA - states/automata for each defined type, its complement, and procedures - and checks leaf-term consistency (Def 4.3).
