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
mtime: '2026-05-21T12:38:15.306Z'
sha256: ca1921987062da8ddae88f306a4001b46751dc870b0fcf0d2ad133a9c529d2a4
topo_level: 9
cycle_group_id: 83
status: pending
target_path: test/dynamic_dispatch_test.cs
plan_started_at: '2026-05-21T16:49:32Z'
plan_completed_at: '2026-05-21T16:54:24Z'
plan_path: .codeconv/conversion-plans/test/dynamic_dispatch_test.dart.md
open_escalation_count: 0
purpose_source: doc
key_idea_source: doc
---

Dynamic Module Dispatch — Integration Tests

Tests the full dispatch chain:
  caller → channel → serve → _activate → procedure

Spec: docs/type system/dynamic-module-dispatch.md
