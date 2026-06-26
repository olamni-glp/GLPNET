---
path: test/multiagent/bonds_v2_isolate_test.dart
name: bonds_v2_isolate_test.dart
purpose: Verifies the Bonds V2 multi-agent plays boot and run end-to-end across Dart isolates via IsolateManager without crashing.
key_idea: Parameterized over mad_fplayN boot files (fplay1 solo; fplay2-9 two-agent; 4b/10/11 two-agent-with-time; fplay12 six-agent village). Each loads the boot file through BootLoader, sets projectDir, calls manager.boot trace-off, start(), a fixed delay; 30/45s timeouts, shutdown in tearDown.
dependencies:
- lib/multiagent/boot_loader.dart
- lib/multiagent/isolate_manager.dart
callers: []
mtime: '2026-05-21T12:38:16.033Z'
sha256: 18e788ee20ad20f262700ad47895d6a6cdefae27818f7c356f7c719a38512e5c
topo_level: 10
cycle_group_id: 100
status: pending
target_path: test/multiagent/bonds_v2_isolate_test.cs
plan_started_at: '2026-05-21T16:49:38Z'
plan_completed_at: '2026-05-21T16:54:19Z'
plan_path: .codeconv/conversion-plans/test/multiagent/bonds_v2_isolate_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies the Bonds V2 multi-agent plays boot and run end-to-end across Dart isolates via IsolateManager without crashing.
