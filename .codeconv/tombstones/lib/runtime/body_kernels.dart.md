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
- lib/runtime/machine_state.dart
- lib/runtime/runtime.dart
- lib/runtime/terms.dart
callers:
- lib/runtime/runtime.dart
mtime: '2026-04-27T09:23:50.000Z'
sha256: 9d360613abdb60c46d883ad215633020e879fefa7f3d422f319dac02fb7063ba
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
