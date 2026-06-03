---
path: lib/multiagent/repl_play_runner.dart
name: repl_play_runner.dart
purpose: 'ReplPlayRunner — runs simulated dGLP plays via REPL subprocess.


  Spawns the REPL as a subprocess, pipes load commands and a play goal,

  parses tagged output lines, and delivers them via callbacks.


  Tagged output format from GLP: tagged(alice, cmd(connect(bob)))

  Parsed into: agentId="alice", kind="cmd", content="connect(bob)"


  Narrative kinds (play 12): friend, say, act, event

  e.g. tagged(alice, friend(bob)) → agentId="alice", kind="friend", content="bob"

  '
key_idea: 'ReplPlayRunner — runs simulated dGLP plays via REPL subprocess.


  Spawns the REPL as a subprocess, pipes load commands and a play goal,

  parses tagged output lines, and delivers them via callbacks.


  Tagged output format from GLP: tagged(alice, cmd(connect(bob)))

  Parsed into: agentId="alice", kind="cmd", content="connect(bob)"


  Narrative kinds (play 12): friend, say, act, event

  e.g. tagged(alice, friend(bob)) → agentId="alice", kind="friend", content="bob"

  '
dependencies: []
callers: []
mtime: '2026-05-21T12:38:13.764Z'
sha256: ebe529f88b605e1c33e3158837c2a5a09599572cfe7c195a3f5712e5846dc169
topo_level: 0
cycle_group_id: 64
status: ready
target_path: lib/multiagent/repl_play_runner.cs
plan_started_at: '2026-05-21T14:41:24Z'
plan_completed_at: '2026-05-21T14:45:17Z'
plan_path: .codeconv/conversion-plans/lib/multiagent/repl_play_runner.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T09:08:47Z'
target_cs_path: out/csharp/lib/multiagent/repl_play_runner.cs
build_status: pass
codegen_open_escalation_count: 0
---

ReplPlayRunner — runs simulated dGLP plays via REPL subprocess.

Spawns the REPL as a subprocess, pipes load commands and a play goal,
parses tagged output lines, and delivers them via callbacks.

Tagged output format from GLP: tagged(alice, cmd(connect(bob)))
Parsed into: agentId="alice", kind="cmd", content="connect(bob)"

Narrative kinds (play 12): friend, say, act, event
e.g. tagged(alice, friend(bob)) → agentId="alice", kind="friend", content="bob"
