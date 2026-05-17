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
mtime: '2026-05-17T10:36:35.142Z'
sha256: ebe529f88b605e1c33e3158837c2a5a09599572cfe7c195a3f5712e5846dc169
topo_level: 0
cycle_group_id: 64
status: ready
target_path: lib/multiagent/repl_play_runner.cs
---

ReplPlayRunner — runs simulated dGLP plays via REPL subprocess.

Spawns the REPL as a subprocess, pipes load commands and a play goal,
parses tagged output lines, and delivers them via callbacks.

Tagged output format from GLP: tagged(alice, cmd(connect(bob)))
Parsed into: agentId="alice", kind="cmd", content="connect(bob)"

Narrative kinds (play 12): friend, say, act, event
e.g. tagged(alice, friend(bob)) → agentId="alice", kind="friend", content="bob"
