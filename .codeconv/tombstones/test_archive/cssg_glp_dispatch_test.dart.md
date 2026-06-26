---
path: test_archive/cssg_glp_dispatch_test.dart
name: cssg_glp_dispatch_test.dart
purpose: 'CSSG GLP Dispatch Integration Test


  Tests that boot.glp''s cross-module # dispatch works through the GLP

  dispatch chain: Distribute → GLP channel → serve → _activate → _select → procedure.


  Previously, # dispatch was broken ("suspends without output") and only

  boot_direct.glp (direct calls) worked. Phases 1–5 of the dynamic dispatch

  implementation plan should fix this.

  '
key_idea: 'CSSG GLP Dispatch Integration Test


  Tests that boot.glp''s cross-module # dispatch works through the GLP

  dispatch chain: Distribute → GLP channel → serve → _activate → _select → procedure.


  Previously, # dispatch was broken ("suspends without output") and only

  boot_direct.glp (direct calls) worked. Phases 1–5 of the dynamic dispatch

  implementation plan should fix this.

  '
dependencies:
- lib/analysis/type_checker/type_environment_builder.dart
- lib/bytecode/runner.dart
- lib/compiler/compiler.dart
- lib/compiler/partial_evaluator.dart
- lib/runtime/glp_activation.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/scheduler.dart
callers: []
mtime: '2026-05-21T12:38:16.717Z'
sha256: dd59fc1b474856737100512ba9ac9f96e012662716170d1f1e471be3837dd226
target_path: test_archive/cssg_glp_dispatch_test.cs
purpose_source: doc
key_idea_source: doc
---

CSSG GLP Dispatch Integration Test

Tests that boot.glp's cross-module # dispatch works through the GLP
dispatch chain: Distribute → GLP channel → serve → _activate → _select → procedure.

Previously, # dispatch was broken ("suspends without output") and only
boot_direct.glp (direct calls) worked. Phases 1–5 of the dynamic dispatch
implementation plan should fix this.
