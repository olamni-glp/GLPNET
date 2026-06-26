---
path: lib/analysis/type_checker/well_typed_term.dart
name: well_typed_term.dart
purpose: Checks a single moded term is well-typed by a type automaton (Definition 5.4) through per-path automaton traversal and variable-pair duality.
key_idea: checkModedTerm extracts paths(term) and runs checkPathAgainstAutomaton, following functor(arity,argIndex) transition labels, switching automata at user-type boundaries and accepting whole subterms at wildcard states via a mode-only check; _checkDuality verifies each (X,X?) pair is dual.
dependencies:
- lib/analysis/type_checker/mode.dart
- lib/analysis/type_checker/moded_term.dart
- lib/analysis/type_checker/program_dfa.dart
callers:
- lib/analysis/type_checker/well_typed_clause.dart
- test/analysis/type_checker/well_typed_clause_test.dart
- test/analysis/type_checker/well_typed_term_test.dart
mtime: '2026-05-21T12:38:12.980Z'
sha256: 66cb54044610eb389ff23edc327067588022b814dd99a51a5e100e6515d9442f
topo_level: 2
cycle_group_id: 17
status: pending
target_path: lib/analysis/type_checker/well_typed_term.cs
plan_started_at: '2026-05-21T14:58:33Z'
plan_completed_at: '2026-05-21T15:09:15Z'
plan_path: .codeconv/conversion-plans/lib/analysis/type_checker/well_typed_term.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T09:25:01Z'
target_cs_path: out/csharp/lib/analysis/type_checker/well_typed_term.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Checks a single moded term is well-typed by a type automaton (Definition 5.4) through per-path automaton traversal and variable-pair duality.
