---
path: test_archive/cssn_modules_test.dart
name: cssn_modules_test.dart
purpose: "CSSN (Child-Safe Social Network) — Modular integration test.\n\nTests the cssn_modules/ project using both:\n  1. Static linking (project linker: discover → link → compile as flat program)\n  2. Dynamic linking (GLP dispatch: compile each module, activateModule, # dispatch)\n\nVerifies that both modes produce identical output for fplay1.\nTests plays 1-3 (basic social graph), fplay4 (CSSG), fplay8 (CSSN groups).\n"
key_idea: "CSSN (Child-Safe Social Network) — Modular integration test.\n\nTests the cssn_modules/ project using both:\n  1. Static linking (project linker: discover → link → compile as flat program)\n  2. Dynamic linking (GLP dispatch: compile each module, activateModule, # dispatch)\n\nVerifies that both modes produce identical output for fplay1.\nTests plays 1-3 (basic social graph), fplay4 (CSSG), fplay8 (CSSN groups).\n"
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
mtime: '2026-05-17T10:36:36.755Z'
sha256: 785fc7fe9acf7f4ada8770ea304dcfd6c4a96b01c92d7fcef333418353710a7b
target_path: test_archive/cssn_modules_test.cs
---

CSSN (Child-Safe Social Network) — Modular integration test.

Tests the cssn_modules/ project using both:
  1. Static linking (project linker: discover → link → compile as flat program)
  2. Dynamic linking (GLP dispatch: compile each module, activateModule, # dispatch)

Verifies that both modes produce identical output for fplay1.
Tests plays 1-3 (basic social graph), fplay4 (CSSG), fplay8 (CSSN groups).
