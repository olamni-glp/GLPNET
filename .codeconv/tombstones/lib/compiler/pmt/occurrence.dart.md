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
mtime: '2026-04-27T09:23:50.000Z'
sha256: cb56e5b79b12f401309ef978dd33b1fdb7ccafd1cd7a202e52f2f797905df6d1
---

PMT Occurrence Classifier: Classifies variable occurrences as reader or writer

Classification rules (syntactic):
- Variable with `?` suffix (e.g., `X?`) → **reader** occurrence
- Variable without `?` suffix (e.g., `X`) → **writer** occurrence

The syntactic annotation in source code is authoritative for SRSW checking.
Mode declarations are used for separate mode consistency validation.
