---
path: lib/runtime/scheduler.dart
name: scheduler.dart
purpose: 'Goal-queue scheduler (Scheduler): drains queued goals through BytecodeRunner, classifies the run as succeeded/failed/suspended, and formats terms/goals for trace output.'
key_idea: drainWithStatus dequeues until empty/maxCycles, runs each goal via runner.runWithStatus with an onReduction callback; a goal that terminates without reducing = failed (stops drain), suspended goals are tracked and final status excludes infrastructure goals; blocking readers come from rt.suspended.
dependencies:
- lib/bytecode/runner.dart
- lib/runtime/runtime.dart
- lib/runtime/terms.dart
callers:
- bin/glp_repl.dart
- lib/engine/glp_engine.dart
- lib/multiagent/agent_runtime.dart
- lib/multiagent/archive-irma-2026-01-30/heap-tests/shared_variable_pointer_test.dart
- lib/multiagent/archive-irma-2026-01-30/isolate_manager.dart
- lib/multiagent/archive-irma-2026-01-30/tests/bidirectional_exchange_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/bidirectional_stream_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/coop_stream_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/distribute_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/friend_introduction_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/isolate_play_alice_bob_charlie_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/play_alice_bob_charlie_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/reversed_flow_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/shared_variable_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/simple_imported_reader_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/social_agent_integration_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/three_agent_merge_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/three_agent_pipeline_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/trace_social_graph_test.dart
- lib/multiagent/archive-irma-2026-01-30/tests/two_hop_flow_test.dart
- lib/multiagent/isolate_manager.dart
- test/bytecode/arithmetic_test.dart
- test/bytecode/fairness_scheduler_loop_test.dart
- test/bytecode/utility_instructions_test.dart
- test/compiler/project_linker_test.dart
- test/dynamic_dispatch_test.dart
- test/engine/glp_engine_test.dart
- test/heap/arithmetic_pointer_test.dart
- test/runtime/module_activation_test.dart
- test/runtime/rpc_routing_test.dart
- test/test_agent_init_goal.dart
- test_archive/activate_kernel_test.dart
- test_archive/actor_single_isolate_test.dart
- test_archive/cssg_glp_dispatch_test.dart
- test_archive/cssn_modules_test.dart
- test_archive/debug_goal_args_test.dart
- test_archive/direct_structterm_test.dart
- test_archive/serve_test.dart
- test_archive/social_graph_sim_modules_test.dart
mtime: '2026-05-21T12:38:15.097Z'
sha256: 6e6b012c5e9b5262847879644915ac2b9e75fd509e8cc5be8f6882a8515f2b03
topo_level: 5
cycle_group_id: 55
status: pending
target_path: lib/runtime/scheduler.cs
plan_started_at: '2026-05-21T16:17:57Z'
plan_completed_at: '2026-05-21T16:23:55Z'
plan_path: .codeconv/conversion-plans/lib/runtime/scheduler.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T13:09:13Z'
target_cs_path: out/csharp/lib/runtime/scheduler.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Goal-queue scheduler (Scheduler): drains queued goals through BytecodeRunner, classifies the run as succeeded/failed/suspended, and formats terms/goals for trace output.
