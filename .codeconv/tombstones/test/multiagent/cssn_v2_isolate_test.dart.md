---
path: test/multiagent/cssn_v2_isolate_test.dart
name: cssn_v2_isolate_test.dart
purpose: Verifies the CSSN modules v2 multi-agent plays boot and run across Dart isolates via IsolateManager without crashing.
key_idea: Parameterized over mad_fplay1-13 from cssn_modules_v2/mad_boot with varying agent counts (1-3 three adults, 4-7 four agents, 8 two adults, 9-10 three agents, 11 six agents, 12 five agents, 13 six-agent village). Loads boot via BootLoader, boots/starts manager, delays; 30/45s timeouts.
dependencies:
- lib/multiagent/boot_loader.dart
- lib/multiagent/isolate_manager.dart
callers: []
mtime: '2026-05-21T12:38:16.081Z'
sha256: ced133bbafaf1744fb59e6375e58cd5b7d825e2998c2cdf473a9cf2443b6c23d
topo_level: 10
cycle_group_id: 102
status: pending
target_path: test/multiagent/cssn_v2_isolate_test.cs
plan_started_at: '2026-05-21T16:55:17Z'
plan_completed_at: '2026-05-21T16:58:07Z'
plan_path: .codeconv/conversion-plans/test/multiagent/cssn_v2_isolate_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies the CSSN modules v2 multi-agent plays boot and run across Dart isolates via IsolateManager without crashing.
