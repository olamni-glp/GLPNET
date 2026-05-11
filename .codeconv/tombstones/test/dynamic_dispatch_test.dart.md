---
path: test/dynamic_dispatch_test.dart
name: dynamic_dispatch_test.dart
purpose: "Dynamic Module Dispatch — Integration Tests\n\nTests the full dispatch chain:\n  caller → channel → serve → _activate → procedure\n\nSpec: docs/type system/dynamic-module-dispatch.md\n"
key_idea: "Dynamic Module Dispatch — Integration Tests\n\nTests the full dispatch chain:\n  caller → channel → serve → _activate → procedure\n\nSpec: docs/type system/dynamic-module-dispatch.md\n"
dependencies:
- lib/analysis/type_checker/type_environment_builder.dart
- lib/bytecode/runner.dart
- lib/compiler/compiler.dart
- lib/compiler/partial_evaluator.dart
- lib/engine/glp_engine.dart
- lib/runtime/glp_activation.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/scheduler.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-04-27T09:23:50.000Z'
sha256: ca1921987062da8ddae88f306a4001b46751dc870b0fcf0d2ad133a9c529d2a4
---

Dynamic Module Dispatch — Integration Tests

Tests the full dispatch chain:
  caller → channel → serve → _activate → procedure

Spec: docs/type system/dynamic-module-dispatch.md
