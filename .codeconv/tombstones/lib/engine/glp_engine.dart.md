---
path: lib/engine/glp_engine.dart
name: glp_engine.dart
purpose: 'GLP Engine - Embeddable GLP Execution Core


  Extracted from glp_repl.dart to provide a single, reusable implementation

  for running GLP programs. Used by:

  - REPL (CLI wrapper)

  - IsolateManager (madGLP agent isolates)

  - Tests


  This is the ONE way to run GLP programs.

  '
key_idea: 'GLP Engine - Embeddable GLP Execution Core


  Extracted from glp_repl.dart to provide a single, reusable implementation

  for running GLP programs. Used by:

  - REPL (CLI wrapper)

  - IsolateManager (madGLP agent isolates)

  - Tests


  This is the ONE way to run GLP programs.

  '
dependencies:
- lib/analysis/type_checker/param_expansion.dart
- lib/analysis/type_checker/type_ast.dart
- lib/analysis/type_checker/type_checker.dart
- lib/analysis/type_checker/type_environment_builder.dart
- lib/bytecode/runner.dart
- lib/compiler/ast.dart
- lib/compiler/compiler.dart
- lib/compiler/lexer.dart
- lib/compiler/parser.dart
- lib/compiler/partial_evaluator.dart
- lib/compiler/project_linker.dart
- lib/multiagent/mad_context.dart
- lib/runtime/glp_activation.dart
- lib/runtime/machine_state.dart
- lib/runtime/module_hierarchy.dart
- lib/runtime/runtime.dart
- lib/runtime/scheduler.dart
- lib/runtime/system_predicates_impl.dart
- lib/runtime/terms.dart
callers:
- bin/glp_repl.dart
- lib/multiagent/agent_runtime.dart
- lib/multiagent/isolate_manager.dart
- test/dynamic_dispatch_test.dart
- test/engine/glp_engine_test.dart
- test/multiagent/output_kernel_test.dart
- test/multiagent/ui_mediator_test.dart
mtime: '2026-05-21T12:38:13.549Z'
sha256: 966bf3b7fa4deb9baca2696f2c221bad3eed61f189de1c7080d409fdcdb5a8df
topo_level: 8
cycle_group_id: 57
status: pending
target_path: lib/engine/glp_engine.cs
plan_started_at: '2026-05-21T16:38:49Z'
plan_completed_at: '2026-05-21T16:43:39Z'
plan_path: .codeconv/conversion-plans/lib/engine/glp_engine.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T13:32:58Z'
target_cs_path: out/csharp/lib/engine/glp_engine.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: doc
key_idea_source: doc
---

GLP Engine - Embeddable GLP Execution Core

Extracted from glp_repl.dart to provide a single, reusable implementation
for running GLP programs. Used by:
- REPL (CLI wrapper)
- IsolateManager (madGLP agent isolates)
- Tests

This is the ONE way to run GLP programs.
