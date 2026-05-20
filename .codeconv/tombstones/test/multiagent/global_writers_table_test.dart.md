---
path: test/multiagent/global_writers_table_test.dart
name: global_writers_table_test.dart
purpose: 'Tests for GlobalWritersTable


  Derived from madGLP-spec.md Section 3: Global Writers Table


  The global writers table tracks local writers that await incoming

  assignments from remote agents. Two entry types:

  - GlobalizeEntry (X, q): created when exporting a reader

  - LocalizeEntry (X, q, i): created when importing a writer global name


  Index 0 is reserved for the serializer (network input stream).

  Regular indices start at 1.

  '
key_idea: 'Tests for GlobalWritersTable


  Derived from madGLP-spec.md Section 3: Global Writers Table


  The global writers table tracks local writers that await incoming

  assignments from remote agents. Two entry types:

  - GlobalizeEntry (X, q): created when exporting a reader

  - LocalizeEntry (X, q, i): created when importing a writer global name


  Index 0 is reserved for the serializer (network input stream).

  Regular indices start at 1.

  '
dependencies:
- lib/multiagent/global_writers_table.dart
callers: []
mtime: '2026-04-27T09:23:50.000Z'
sha256: e94c973b8effdbc9fc3bc538634735c630dab2064acb5ec8dcd9f856a0c5e45e
topo_level: 1
cycle_group_id: 104
status: pending
target_path: test/multiagent/global_writers_table_test.cs
plan_started_at: '2026-05-19T23:41:30Z'
plan_completed_at: '2026-05-19T23:41:30Z'
plan_path: null
open_escalation_count: 0
---

Tests for GlobalWritersTable

Derived from madGLP-spec.md Section 3: Global Writers Table

The global writers table tracks local writers that await incoming
assignments from remote agents. Two entry types:
- GlobalizeEntry (X, q): created when exporting a reader
- LocalizeEntry (X, q, i): created when importing a writer global name

Index 0 is reserved for the serializer (network input stream).
Regular indices start at 1.
