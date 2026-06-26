---
path: test_archive/serve_test.dart
name: serve_test.dart
purpose: Integration tests for the serve/2 system predicate that streams goals from a list and dispatches each into a target module via _activate.
key_idea: Defines serve/2 in embedded GLP source, builds GLP cons-cell goal lists on the heap, wires CallEnv/GoalRef and drains via Scheduler; covers compile-label checks, single/multiple/empty-stream goals, multi-export targets, and a traced full serve->_activate->_select->procedure chain.
dependencies:
- lib/bytecode/runner.dart
- lib/compiler/compiler.dart
- lib/runtime/body_kernels.dart
- lib/runtime/heap_fcp.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/scheduler.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-05-21T12:38:16.864Z'
sha256: 2f2b567ff4e9997fd2ffbb539a56930225a950b9af89604dc7417d9dc457fbcc
target_path: test_archive/serve_test.cs
purpose_source: inferred
key_idea_source: inferred
---

Integration tests for the serve/2 system predicate that streams goals from a list and dispatches each into a target module via _activate.
