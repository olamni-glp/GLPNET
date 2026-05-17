---
path: test_archive/social_graph_sim_modules_test.dart
name: social_graph_sim_modules_test.dart
purpose: "Social Graph (Simulated UI) — Modular integration test.\n\nTests the social_graph_simulated_ui_modules/ project using both:\n  1. Static linking (project linker: discover → link → compile as flat program)\n  2. Dynamic linking (GLP dispatch: compile each module, activateModule, # dispatch)\n\nVerifies that both modes produce identical output for fplay1/fplay2/fplay3.\n"
key_idea: "Social Graph (Simulated UI) — Modular integration test.\n\nTests the social_graph_simulated_ui_modules/ project using both:\n  1. Static linking (project linker: discover → link → compile as flat program)\n  2. Dynamic linking (GLP dispatch: compile each module, activateModule, # dispatch)\n\nVerifies that both modes produce identical output for fplay1/fplay2/fplay3.\n"
dependencies:
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
- lib/runtime/glp_activation.dart
- lib/runtime/machine_state.dart
- lib/runtime/module_hierarchy.dart
- lib/runtime/runtime.dart
- lib/runtime/scheduler.dart
callers: []
mtime: '2026-05-17T10:36:36.858Z'
sha256: 37c05b285d8fcad0b1194b52bcdf4ea028fc939b19493694123d1b0acea59f04
target_path: test_archive/social_graph_sim_modules_test.cs
---

Social Graph (Simulated UI) — Modular integration test.

Tests the social_graph_simulated_ui_modules/ project using both:
  1. Static linking (project linker: discover → link → compile as flat program)
  2. Dynamic linking (GLP dispatch: compile each module, activateModule, # dispatch)

Verifies that both modes produce identical output for fplay1/fplay2/fplay3.
