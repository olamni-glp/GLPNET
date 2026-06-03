---
path: lib/compiler/pmt/occurrence.dart
name: occurrence.dart
purpose: 'PMT Occurrence Classifier: Classifies variable occurrences as reader or writer


  Classification rules (syntactic):

  - Variable with `?` suffix (e.g., `X?`) → **reader** occurrence

  - Variable without `?` suffix (e.g., `X`) → **writer** occurrence


  The syntactic annotation in source code is authoritative for SRSW checking.

  Mode declarations are used for separate mode consistency validation.

  '
key_idea: 'PMT Occurrence Classifier: Classifies variable occurrences as reader or writer


  Classification rules (syntactic):

  - Variable with `?` suffix (e.g., `X?`) → **reader** occurrence

  - Variable without `?` suffix (e.g., `X`) → **writer** occurrence


  The syntactic annotation in source code is authoritative for SRSW checking.

  Mode declarations are used for separate mode consistency validation.

  '
dependencies:
- lib/compiler/ast.dart
- lib/compiler/pmt/mode_table.dart
callers:
- lib/compiler/pmt/checker.dart
mtime: '2026-05-21T12:38:13.449Z'
sha256: cb56e5b79b12f401309ef978dd33b1fdb7ccafd1cd7a202e52f2f797905df6d1
topo_level: 3
cycle_group_id: 47
status: pending
target_path: lib/compiler/pmt/occurrence.cs
plan_started_at: '2026-05-21T15:24:20Z'
plan_completed_at: '2026-05-21T16:00:10Z'
plan_path: .codeconv/conversion-plans/lib/compiler/pmt/occurrence.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T11:54:42Z'
target_cs_path: out/csharp/lib/compiler/pmt/occurrence.cs
build_status: pass
codegen_open_escalation_count: 0
---

PMT Occurrence Classifier: Classifies variable occurrences as reader or writer

Classification rules (syntactic):
- Variable with `?` suffix (e.g., `X?`) → **reader** occurrence
- Variable without `?` suffix (e.g., `X`) → **writer** occurrence

The syntactic annotation in source code is authoritative for SRSW checking.
Mode declarations are used for separate mode consistency validation.
