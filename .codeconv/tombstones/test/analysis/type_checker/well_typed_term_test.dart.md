---
path: test/analysis/type_checker/well_typed_term_test.dart
name: well_typed_term_test.dart
purpose: 'Verifies checkModedTerm well-typed moded-term checking (Def 5.4): primitive-type constants, wildcard variables, writer/reader duality, and DFA path consistency.'
key_idea: 'Runs checkModedTerm against tiny Automaton/ProgramDFA: integer-at-Integer passes, string-at-Integer fails, writer/reader typed at wildcard, dual writer/reader pair OK while same-mode pair fails duality, and missing functor transition raises InconsistentPathError.'
dependencies:
- lib/analysis/type_checker/mode.dart
- lib/analysis/type_checker/moded_term.dart
- lib/analysis/type_checker/program_dfa.dart
- lib/analysis/type_checker/well_typed_term.dart
callers: []
mtime: '2026-05-21T12:38:15.504Z'
sha256: 35b279ae85fe3b9fbf1d952650226551efbab338b524d4171ed9f51fbaf0518c
topo_level: 3
cycle_group_id: 70
status: pending
target_path: test/analysis/type_checker/well_typed_term_test.cs
plan_started_at: '2026-05-21T16:00:28Z'
plan_completed_at: '2026-05-21T16:05:44Z'
plan_path: .codeconv/conversion-plans/test/analysis/type_checker/well_typed_term_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies checkModedTerm well-typed moded-term checking (Def 5.4): primitive-type constants, wildcard variables, writer/reader duality, and DFA path consistency.
