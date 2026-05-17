---
path: lib/bytecode/opcodes_v2.dart
name: opcodes_v2.dart
purpose: 'Phase 2: Unified Instruction Set


  This file contains the v2 instruction set with unified variable instructions.

  Key change: Instead of separate Writer/Reader instructions, use a single

  instruction with an isReader flag.


  Benefits:

  - Simpler instruction set (fewer opcodes)

  - Better code reuse in interpreter

  - Clearer semantics

  - Foundation for register allocation optimizations

  '
key_idea: 'Phase 2: Unified Instruction Set


  This file contains the v2 instruction set with unified variable instructions.

  Key change: Instead of separate Writer/Reader instructions, use a single

  instruction with an isReader flag.


  Benefits:

  - Simpler instruction set (fewer opcodes)

  - Better code reuse in interpreter

  - Clearer semantics

  - Foundation for register allocation optimizations

  '
dependencies: []
callers:
- lib/bytecode/asm.dart
- lib/bytecode/runner.dart
- lib/compiler/codegen.dart
mtime: '2026-05-17T10:36:34.761Z'
sha256: c8549ccea9fbe836a1804e62b0164ac312889f3602144e9403938f9aaca206d6
topo_level: 0
cycle_group_id: 21
status: ready
target_path: lib/bytecode/opcodes_v2.cs
---

Phase 2: Unified Instruction Set

This file contains the v2 instruction set with unified variable instructions.
Key change: Instead of separate Writer/Reader instructions, use a single
instruction with an isReader flag.

Benefits:
- Simpler instruction set (fewer opcodes)
- Better code reuse in interpreter
- Clearer semantics
- Foundation for register allocation optimizations
