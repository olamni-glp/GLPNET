---
path: test/runtime/rpc_routing_test.dart
name: rpc_routing_test.dart
purpose: Verifies cross-module '#' RPC calls route through GLP channels to an activated target module (Phase 5 routing).
key_idea: 'Compiles target B (exported procs), serveSource, and caller A using ''target_b # process(X)''; activateModule registers the channel in rt.glpChannels; sets up the caller with imports{1->target_b}, enqueues, drains asserting serve processes the routed RPC and re-suspends. Also: multiple RPCs and close terminates.'
dependencies:
- lib/bytecode/runner.dart
- lib/compiler/compiler.dart
- lib/runtime/glp_activation.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/scheduler.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-05-21T12:38:16.653Z'
sha256: 3dedc5b118a3b9b0a1a2e94a6ddc7abceb28811e6c7d07f381ff1493ae5a98bb
topo_level: 8
cycle_group_id: 117
status: pending
target_path: test/runtime/rpc_routing_test.cs
plan_started_at: '2026-05-21T16:43:57Z'
plan_completed_at: '2026-05-21T16:49:15Z'
plan_path: .codeconv/conversion-plans/test/runtime/rpc_routing_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies cross-module '#' RPC calls route through GLP channels to an activated target module (Phase 5 routing).
