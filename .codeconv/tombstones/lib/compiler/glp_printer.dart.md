---
path: lib/compiler/glp_printer.dart
name: glp_printer.dart
purpose: Serializes a GLP AST back into valid GLP source text (unparser/pretty-printer), preserving SRSW reader marks (X?) and every term and operator form.
key_idea: Recursive structural descent (printProgram->printClause->printAtom/Goal/Guard->printTerm) into a StringBuffer; special-cases infix operators, appends '?' for reader vars, flattens proper/improper lists, and regex-distinguishes bare atoms from quoted/escaped strings.
dependencies:
- lib/compiler/ast.dart
callers: []
mtime: '2026-05-21T12:38:13.220Z'
sha256: 5c424c589cb0b27fd7b8b784177837bf743aacd3c6cf239b136201a3483a6def
topo_level: 2
cycle_group_id: 44
status: pending
target_path: lib/compiler/glp_printer.cs
plan_started_at: '2026-05-21T14:58:35Z'
plan_completed_at: '2026-05-21T15:09:16Z'
plan_path: .codeconv/conversion-plans/lib/compiler/glp_printer.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T09:41:53Z'
target_cs_path: out/csharp/lib/compiler/glp_printer.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Serializes a GLP AST back into valid GLP source text (unparser/pretty-printer), preserving SRSW reader marks (X?) and every term and operator form.
