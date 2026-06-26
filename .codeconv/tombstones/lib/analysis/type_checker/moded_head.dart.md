---
path: lib/analysis/type_checker/moded_head.dart
name: moded_head.dart
purpose: Constructs the moded head H' (Def 5.5) from a clause head and its procedure declaration for well-typing, and builds produced terms for body atoms.
key_idea: modedHead builds an I/O-moded term (root consume), combining declared arg modes with embedded type modes via involution, then _ensureVariablesMatchModes flips every variable (X<->X?); producedTerm uses root produce, no flip. Unknown types route to _buildOpaqueModedTerm.
dependencies:
- lib/analysis/type_checker/mode.dart
- lib/analysis/type_checker/moded_term.dart
- lib/analysis/type_checker/type_ast.dart
- lib/compiler/ast.dart
callers:
- lib/analysis/type_checker/well_typed_clause.dart
- test/analysis/type_checker/moded_head_test.dart
mtime: '2026-05-21T12:38:12.744Z'
sha256: 8e1cf1a9af1ccc77174921ef4c2df7845bce7406fc3930b69d791cd8f087d4e2
topo_level: 2
cycle_group_id: 7
status: pending
target_path: lib/analysis/type_checker/moded_head.cs
plan_started_at: '2026-05-21T14:58:28Z'
plan_completed_at: '2026-05-21T15:09:11Z'
plan_path: .codeconv/conversion-plans/lib/analysis/type_checker/moded_head.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T09:22:27Z'
target_cs_path: out/csharp/lib/analysis/type_checker/moded_head.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Constructs the moded head H' (Def 5.5) from a clause head and its procedure declaration for well-typing, and builds produced terms for body atoms.
