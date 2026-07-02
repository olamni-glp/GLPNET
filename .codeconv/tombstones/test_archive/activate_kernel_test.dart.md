---
path: test_archive/activate_kernel_test.dart
name: activate_kernel_test.dart
purpose: 'Unit tests for the _activate/2 body kernel: verifies it dispatches a goal into a target module through that module''s _select/1 dispatch table.'
key_idea: Calls activateKernel directly with a ModuleTerm + StructTerm goal; success enqueues one goal at the _select/1 label and registers a module runner; aborts when _select/1 is absent or arg0 is not a ModuleTerm; unknown goals fall through to otherwise, then drain via Scheduler.drainWithStatus.
dependencies:
- lib/bytecode/runner.dart
- lib/compiler/compiler.dart
- lib/runtime/body_kernels.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/scheduler.dart
- lib/runtime/terms.dart
callers: []
mtime: '2026-05-21T12:38:16.677Z'
sha256: f163f2e17fd463356c5648fd119486665b73065978a097e126d7ee7d9a1877fd
target_path: test_archive/activate_kernel_test.cs
purpose_source: inferred
key_idea_source: inferred
---

Unit tests for the _activate/2 body kernel: verifies it dispatches a goal into a target module through that module's _select/1 dispatch table.
