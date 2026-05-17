---
path: lib/runtime/body_kernels.dart
name: body_kernels.dart
purpose: 'Body kernel infrastructure for GLP arithmetic


  Body kernels are runtime-implemented predicates that:

  - Execute inline (not spawned as separate goals)

  - Have two-valued semantics (success or abort)

  - Are only accessible to system predicates (assign.glp)

  - Expect all preconditions met (guards should verify before calling)


  Per heap-pointer-architecture-spec.md v3.0:

  - VarRef has only addr field

  - Use heap.isWriter/isReader to check cell type

  '
key_idea: 'Body kernel infrastructure for GLP arithmetic


  Body kernels are runtime-implemented predicates that:

  - Execute inline (not spawned as separate goals)

  - Have two-valued semantics (success or abort)

  - Are only accessible to system predicates (assign.glp)

  - Expect all preconditions met (guards should verify before calling)


  Per heap-pointer-architecture-spec.md v3.0:

  - VarRef has only addr field

  - Use heap.isWriter/isReader to check cell type

  '
dependencies:
- lib/bytecode/runner.dart
- lib/multiagent/mad_context.dart
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/terms.dart
callers:
- lib/bytecode/runner.dart
- lib/runtime/runtime.dart
- test/bytecode/arithmetic_test.dart
- test/heap/arithmetic_pointer_test.dart
- test_archive/activate_kernel_test.dart
- test_archive/serve_test.dart
mtime: '2026-05-17T10:36:35.625Z'
sha256: 9d360613abdb60c46d883ad215633020e879fefa7f3d422f319dac02fb7063ba
topo_level: 4
cycle_group_id: 37
status: pending
target_path: lib/runtime/body_kernels.cs
---

Body kernel infrastructure for GLP arithmetic

Body kernels are runtime-implemented predicates that:
- Execute inline (not spawned as separate goals)
- Have two-valued semantics (success or abort)
- Are only accessible to system predicates (assign.glp)
- Expect all preconditions met (guards should verify before calling)

Per heap-pointer-architecture-spec.md v3.0:
- VarRef has only addr field
- Use heap.isWriter/isReader to check cell type
