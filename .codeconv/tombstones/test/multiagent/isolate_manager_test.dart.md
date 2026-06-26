---
path: test/multiagent/isolate_manager_test.dart
name: isolate_manager_test.dart
purpose: Verifies IsolateManager boots and starts multi-agent configs across isolates for trivial and full social_graph plays.
key_idea: 'Three tests: boots three trivial agent_init agents (boot+start don''t crash); runs a no-UI actor-script play; runs a UI-mediator+UI-actors play. Play tests read social_graph .glp (skip if absent), assert config parses 3 directives (alice/bob/charlie), set sharedSources, boot+start, shutdown in tearDown.'
dependencies:
- lib/multiagent/boot_loader.dart
- lib/multiagent/isolate_manager.dart
callers: []
mtime: '2026-05-21T12:38:16.179Z'
sha256: 431a81bed721f5801c63d58ef60dc4af936f654617fd1a7f4aaab28dcfd30da0
topo_level: 10
cycle_group_id: 106
status: pending
target_path: test/multiagent/isolate_manager_test.cs
plan_started_at: '2026-05-21T16:55:18Z'
plan_completed_at: '2026-05-21T16:58:08Z'
plan_path: .codeconv/conversion-plans/test/multiagent/isolate_manager_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies IsolateManager boots and starts multi-agent configs across isolates for trivial and full social_graph plays.
