---
path: lib/multiagent/archive-irma-2026-01-30/tests/three_agent_merge_test.dart
name: three_agent_merge_test.dart
purpose: "Three-Agent Merge Test\n\nTests merge where all three components are on separate agents:\n  @1 (producer1): produces Xs = [1,2,3]\n  @2 (producer2): produces Ys = [a,b,c]\n  @3 (merger): imports Xs? and Ys?, runs merge(Xs?, Ys?, Zs)\n\nData flow: @1 → @3 ← @2 (both feed into @3)\n\nThis tests a pure processor pattern where @3 has no local production.\n"
key_idea: "Three-Agent Merge Test\n\nTests merge where all three components are on separate agents:\n  @1 (producer1): produces Xs = [1,2,3]\n  @2 (producer2): produces Ys = [a,b,c]\n  @3 (merger): imports Xs? and Ys?, runs merge(Xs?, Ys?, Zs)\n\nData flow: @1 → @3 ← @2 (both feed into @3)\n\nThis tests a pure processor pattern where @3 has no local production.\n"
dependencies:
- lib/bytecode/runner.dart
- lib/compiler/compiler.dart
- lib/multiagent/irma_context.dart
- lib/multiagent/message_queue.dart
- lib/multiagent/payload_serializer.dart
- lib/multiagent/variable_table.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/scheduler.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-05-21T12:38:14.735Z'
sha256: 106b578450cb14f3c9336bea57645667863d64b2d392b1ec78327b23bfbe2c23
target_path: lib/multiagent/archive-irma-2026-01-30/tests/three_agent_merge_test.cs
purpose_source: doc
key_idea_source: doc
---

Three-Agent Merge Test

Tests merge where all three components are on separate agents:
  @1 (producer1): produces Xs = [1,2,3]
  @2 (producer2): produces Ys = [a,b,c]
  @3 (merger): imports Xs? and Ys?, runs merge(Xs?, Ys?, Zs)

Data flow: @1 → @3 ← @2 (both feed into @3)

This tests a pure processor pattern where @3 has no local production.
