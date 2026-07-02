---
path: lib/multiagent/isolate_manager.dart
name: isolate_manager.dart
purpose: 'Isolate Manager for madGLP


  Spawns agent isolates based on BootConfig and routes messages between them.

  Execution is event-driven: agents drain+flush on Start and on each incoming

  NetworkMsg. There is no tick loop or external clock.


  Termination is external: the caller shuts down isolates when done.


  See: docs/ma/agent-runtime-spec.md

  '
key_idea: 'Isolate Manager for madGLP


  Spawns agent isolates based on BootConfig and routes messages between them.

  Execution is event-driven: agents drain+flush on Start and on each incoming

  NetworkMsg. There is no tick loop or external clock.


  Termination is external: the caller shuts down isolates when done.


  See: docs/ma/agent-runtime-spec.md

  '
dependencies:
- lib/bytecode/runner.dart
- lib/engine/glp_engine.dart
- lib/multiagent/boot_loader.dart
- lib/multiagent/message_queue.dart
- lib/multiagent/payload_serializer.dart
- lib/runtime/machine_state.dart
- lib/runtime/scheduler.dart
- lib/runtime/terms.dart
callers:
- bin/glp_repl.dart
- test/multiagent/bonds_v2_isolate_test.dart
- test/multiagent/cssn_v2_isolate_test.dart
- test/multiagent/isolate_manager_test.dart
- test/multiagent/multiagent_glp_test.dart
- test/multiagent/multiagent_modules_test.dart
mtime: '2026-05-21T12:38:13.672Z'
sha256: 4343fad2624fea2621125d360568be4b203ab62d9bc1771bf649cb68ae575c9c
topo_level: 9
cycle_group_id: 63
status: pending
target_path: lib/multiagent/isolate_manager.cs
plan_started_at: '2026-05-21T16:44:02Z'
plan_completed_at: '2026-05-21T16:49:20Z'
plan_path: .codeconv/conversion-plans/lib/multiagent/isolate_manager.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T13:42:22Z'
target_cs_path: out/csharp/lib/multiagent/isolate_manager.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: doc
key_idea_source: doc
---

Isolate Manager for madGLP

Spawns agent isolates based on BootConfig and routes messages between them.
Execution is event-driven: agents drain+flush on Start and on each incoming
NetworkMsg. There is no tick loop or external clock.

Termination is external: the caller shuts down isolates when done.

See: docs/ma/agent-runtime-spec.md
