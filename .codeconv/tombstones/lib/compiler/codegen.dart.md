---
path: lib/compiler/codegen.dart
name: codegen.dart
purpose: Code generator that lowers the annotated GLP AST into FCP-style bytecode, emitting per-clause HEAD/GUARD/BODY instruction sequences plus module-RPC and spawn ops.
key_idea: CodeGenContext threads PC, labels, temp registers and phase flags per clause; head args emit GetVariable-vs-GetValue by first-occurrence tracking, structures via Push/Pop/PutStructure, ClauseTry/Commit/Proceed frame the phases, body goals Spawn and remote goals Distribute/Transmit.
dependencies:
- lib/bytecode/asm.dart
- lib/bytecode/opcodes.dart
- lib/bytecode/opcodes_v2.dart
- lib/bytecode/runner.dart
- lib/compiler/analyzer.dart
- lib/compiler/ast.dart
- lib/compiler/error.dart
- lib/compiler/result.dart
- lib/runtime/terms.dart
callers:
- lib/compiler/compiler.dart
- test/module/module_compiler_test.dart
mtime: '2026-05-21T12:38:13.152Z'
sha256: fdeeb685673893129e721409ea2b4ceb0e6f356d406efd526ae32d4cae64d3fd
topo_level: 6
cycle_group_id: 42
status: pending
target_path: lib/compiler/codegen.cs
plan_started_at: '2026-05-21T16:28:54Z'
plan_completed_at: '2026-05-21T16:33:21Z'
plan_path: .codeconv/conversion-plans/lib/compiler/codegen.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T13:15:54Z'
target_cs_path: out/csharp/lib/compiler/codegen.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Code generator that lowers the annotated GLP AST into FCP-style bytecode, emitting per-clause HEAD/GUARD/BODY instruction sequences plus module-RPC and spawn ops.
