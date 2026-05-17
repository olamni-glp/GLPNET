---
path: lib/multiagent/archive-irma-2026-01-30/tests/three_agent_pipeline_test.dart
name: three_agent_pipeline_test.dart
purpose: "Three-Agent Pipeline Test\n\nTests data flow through three agents in a pipeline:\n  @1 (producer): produces [1, 2, 3]\n  @2 (transformer): transforms to [got(1), got(2), got(3)]\n  @3 (consumer): receives and wraps as done(List)\n\nData flow: @1 → @2 → @3\n\nThis tests multi-hop data flow with more than two agents.\n"
key_idea: "Three-Agent Pipeline Test\n\nTests data flow through three agents in a pipeline:\n  @1 (producer): produces [1, 2, 3]\n  @2 (transformer): transforms to [got(1), got(2), got(3)]\n  @3 (consumer): receives and wraps as done(List)\n\nData flow: @1 → @2 → @3\n\nThis tests multi-hop data flow with more than two agents.\n"
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
mtime: '2026-05-17T10:36:35.573Z'
sha256: 1b8fad1e92fd9f4b5a484d9886767a5f3389b9159e0966397170ce35c87c050d
target_path: lib/multiagent/archive-irma-2026-01-30/tests/three_agent_pipeline_test.cs
---

Three-Agent Pipeline Test

Tests data flow through three agents in a pipeline:
  @1 (producer): produces [1, 2, 3]
  @2 (transformer): transforms to [got(1), got(2), got(3)]
  @3 (consumer): receives and wraps as done(List)

Data flow: @1 → @2 → @3

This tests multi-hop data flow with more than two agents.
