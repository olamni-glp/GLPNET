---
path: test/test_channel_construction.dart
name: test_channel_construction.dart
purpose: Verifies buildChannelTerm constructs the correct ch(Reader, Writer) struct term from an external channel.
key_idea: Builds a HeapFCP + external 'user' channel via createExternalChannel, calls buildChannelTerm; asserts result is StructTerm functor 'ch' arity 2, arg[0] VarRef is a reader equal to inputReaderAddr, arg[1] VarRef is a writer equal to outputWriterAddr.
dependencies:
- lib/runtime/external_io.dart
- lib/runtime/heap_fcp.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-05-21T12:38:15.406Z'
sha256: d74fde5cacb1398422070b6ca4a11ad7325c200fef17c0d9b2f7d76a96fc8b90
topo_level: 4
cycle_group_id: 121
status: pending
target_path: test/test_channel_construction.cs
plan_started_at: '2026-05-21T16:13:10Z'
plan_completed_at: '2026-05-21T16:17:39Z'
plan_path: .codeconv/conversion-plans/test/test_channel_construction.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies buildChannelTerm constructs the correct ch(Reader, Writer) struct term from an external channel.
