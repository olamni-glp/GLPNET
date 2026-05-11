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
dependencies: []
callers: []
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
