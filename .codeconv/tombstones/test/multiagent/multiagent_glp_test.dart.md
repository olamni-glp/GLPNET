---
path: test/multiagent/multiagent_glp_test.dart
name: multiagent_glp_test.dart
purpose: 'Multi-agent GLP tests via IsolateManager


  These tests convert the old IRMA-era multi-isolate tests into GLP programs

  using the goal@isolate boot format. Each test loads a .glp file with a boot

  clause and runs it through the IsolateManager.


  Execution is event-driven: boot, start, wait for protocol to settle, shutdown.


  Original IRMA tests: lib/multiagent/archive-irma-2026-01-30/tests/

  Converted GLP programs: programs/typed_book/multiagent_tests/

  '
key_idea: 'Multi-agent GLP tests via IsolateManager


  These tests convert the old IRMA-era multi-isolate tests into GLP programs

  using the goal@isolate boot format. Each test loads a .glp file with a boot

  clause and runs it through the IsolateManager.


  Execution is event-driven: boot, start, wait for protocol to settle, shutdown.


  Original IRMA tests: lib/multiagent/archive-irma-2026-01-30/tests/

  Converted GLP programs: programs/typed_book/multiagent_tests/

  '
dependencies:
- lib/multiagent/boot_loader.dart
- lib/multiagent/isolate_manager.dart
callers: []
mtime: '2026-04-27T09:23:50.000Z'
sha256: 859b800ec1e014185b1b52775980da78d2c260421f07700ff9ce1ad742d94aea
topo_level: 10
cycle_group_id: 112
status: pending
target_path: test/multiagent/multiagent_glp_test.cs
plan_started_at: '2026-05-21T16:55:19Z'
plan_completed_at: '2026-05-21T16:58:09Z'
plan_path: .codeconv/conversion-plans/test/multiagent/multiagent_glp_test.dart.md
open_escalation_count: 0
---

Multi-agent GLP tests via IsolateManager

These tests convert the old IRMA-era multi-isolate tests into GLP programs
using the goal@isolate boot format. Each test loads a .glp file with a boot
clause and runs it through the IsolateManager.

Execution is event-driven: boot, start, wait for protocol to settle, shutdown.

Original IRMA tests: lib/multiagent/archive-irma-2026-01-30/tests/
Converted GLP programs: programs/typed_book/multiagent_tests/
