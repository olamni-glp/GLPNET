---
path: lib/multiagent/agent_runtime.dart
name: agent_runtime.dart
purpose: 'AgentRuntime — encapsulates GLP agent runtime for UI integration.


  Extracted from glp_multiagent/lib/main.dart.

  Uses GlpEngine (the ONE way to run GLP programs) for compilation,

  MadContext for madGLP messaging, and Scheduler for execution.


  Boot approach: GlpEngine loads stdlib, enableMadGLP loads madPredicates,

  starts agent_init(Id, UserIn, NetIn). Network output goes through

  send_to_net → global_send → MadContext. User output goes through

  send_to_user → _output/1 kernel → outputCallback.

  '
key_idea: 'AgentRuntime — encapsulates GLP agent runtime for UI integration.


  Extracted from glp_multiagent/lib/main.dart.

  Uses GlpEngine (the ONE way to run GLP programs) for compilation,

  MadContext for madGLP messaging, and Scheduler for execution.


  Boot approach: GlpEngine loads stdlib, enableMadGLP loads madPredicates,

  starts agent_init(Id, UserIn, NetIn). Network output goes through

  send_to_net → global_send → MadContext. User output goes through

  send_to_user → _output/1 kernel → outputCallback.

  '
dependencies:
- lib/bytecode/runner.dart
- lib/compiler/ast.dart
- lib/compiler/lexer.dart
- lib/compiler/parser.dart
- lib/engine/glp_engine.dart
- lib/multiagent/mad_context.dart
- lib/multiagent/message_queue.dart
- lib/multiagent/payload_serializer.dart
- lib/runtime/external_io.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/scheduler.dart
- lib/runtime/terms.dart
callers:
- test/debug_four_agents_modules.dart
mtime: '2026-04-27T09:23:50.000Z'
sha256: 16fa2e171ac7e27ce67020b5a539c9e42355fb38c61a305c827133ddbf373b8f
---

AgentRuntime — encapsulates GLP agent runtime for UI integration.

Extracted from glp_multiagent/lib/main.dart.
Uses GlpEngine (the ONE way to run GLP programs) for compilation,
MadContext for madGLP messaging, and Scheduler for execution.

Boot approach: GlpEngine loads stdlib, enableMadGLP loads madPredicates,
starts agent_init(Id, UserIn, NetIn). Network output goes through
send_to_net → global_send → MadContext. User output goes through
send_to_user → _output/1 kernel → outputCallback.
