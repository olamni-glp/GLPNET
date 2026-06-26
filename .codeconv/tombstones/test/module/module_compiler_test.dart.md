---
path: test/module/module_compiler_test.dart
name: module_compiler_test.dart
purpose: Verifies the module compiler's import-index allocation and codegen of remote (#) goals into Distribute/Transmit bytecode opcodes.
key_idea: Exercises ImportTable (1-based indices, dedup to same index, getIndex null, size, orderedImports, contains); compiles static math#factorial->Distribute and dynamic M?#foo->Transmit, asserting importIndex/functor/arity reuse and Distribute/Transmit toString formats.
dependencies:
- lib/bytecode/opcodes.dart
- lib/compiler/analyzer.dart
- lib/compiler/ast.dart
- lib/compiler/codegen.dart
- lib/compiler/lexer.dart
- lib/compiler/parser.dart
callers: []
mtime: '2026-05-21T12:38:15.895Z'
sha256: 112ccd7b1688a462b205b63e4ad4082a0088432a2921539cee0597b8c8f7c2dd
topo_level: 7
cycle_group_id: 95
status: pending
target_path: test/module/module_compiler_test.cs
plan_started_at: '2026-05-21T16:38:48Z'
plan_completed_at: '2026-05-21T16:43:38Z'
plan_path: .codeconv/conversion-plans/test/module/module_compiler_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies the module compiler's import-index allocation and codegen of remote (#) goals into Distribute/Transmit bytecode opcodes.
