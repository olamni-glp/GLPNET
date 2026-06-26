---
path: lib/analysis/type_checker/moded_term.dart
name: moded_term.dart
purpose: 'Defines the moded-term data model (Def 4.2): mode-annotated compound/constant/variable nodes plus path extraction and classification ops (isConsumed/isProduced/isIO/dual).'
key_idea: ModedTerm hierarchy (ModedCompound/Constant/Variable) with a visitor; variables carry structural vs implicit mode. Visitor-based isIO validates only consume->produce transitions per root-to-leaf path; dual flips every mode and reader/writer; paths extracts root-to-leaf PathStep sequences.
dependencies:
- lib/analysis/type_checker/mode.dart
callers:
- lib/analysis/type_checker/moded_head.dart
- lib/analysis/type_checker/well_typed_clause.dart
- lib/analysis/type_checker/well_typed_term.dart
- test/analysis/type_checker/moded_head_test.dart
- test/analysis/type_checker/well_typed_clause_test.dart
- test/analysis/type_checker/well_typed_term_test.dart
mtime: '2026-05-21T12:38:12.770Z'
sha256: e1f9f5809ff29101ca4c63e08173c7db6d02257c350d132b6e55e90c4f790fe2
topo_level: 1
cycle_group_id: 6
status: pending
target_path: lib/analysis/type_checker/moded_term.cs
plan_started_at: '2026-05-21T14:45:37Z'
plan_completed_at: '2026-05-21T14:50:22Z'
plan_path: .codeconv/conversion-plans/lib/analysis/type_checker/moded_term.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T09:09:16Z'
target_cs_path: out/csharp/lib/analysis/type_checker/moded_term.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Defines the moded-term data model (Def 4.2): mode-annotated compound/constant/variable nodes plus path extraction and classification ops (isConsumed/isProduced/isIO/dual).
