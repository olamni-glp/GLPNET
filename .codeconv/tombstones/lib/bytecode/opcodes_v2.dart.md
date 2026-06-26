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
mtime: '2026-05-21T12:38:13.057Z'
sha256: c8549ccea9fbe836a1804e62b0164ac312889f3602144e9403938f9aaca206d6
topo_level: 0
cycle_group_id: 21
status: ready
target_path: lib/bytecode/opcodes_v2.cs
plan_started_at: '2026-05-21T14:23:22Z'
plan_completed_at: '2026-05-21T14:34:26Z'
plan_path: .codeconv/conversion-plans/lib/bytecode/opcodes_v2.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T08:56:14Z'
target_cs_path: out/csharp/lib/bytecode/opcodes_v2.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: doc
key_idea_source: doc
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
