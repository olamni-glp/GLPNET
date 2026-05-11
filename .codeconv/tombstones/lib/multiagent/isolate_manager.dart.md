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
dependencies: []
callers: []
mtime: '2026-04-27T09:23:50.000Z'
sha256: 4343fad2624fea2621125d360568be4b203ab62d9bc1771bf649cb68ae575c9c
---

Isolate Manager for madGLP

Spawns agent isolates based on BootConfig and routes messages between them.
Execution is event-driven: agents drain+flush on Start and on each incoming
NetworkMsg. There is no tick loop or external clock.

Termination is external: the caller shuts down isolates when done.

See: docs/ma/agent-runtime-spec.md
