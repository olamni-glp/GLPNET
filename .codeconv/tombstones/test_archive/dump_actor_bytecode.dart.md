---
path: test_archive/dump_actor_bytecode.dart
name: dump_actor_bytecode.dart
purpose: Standalone utility script that disassembles the compiled bytecode of the actor/2 procedure from a social-graph play, for inspecting compiler output.
key_idea: Reads a hardcoded play .glp, regex-strips the boot procedure and clause, compiles via GlpCompiler, locates and sorts actor/2* labels, then prints each op from the actor/2 label up to its actor/2_end label (or +100) PC.
dependencies:
- lib/compiler/compiler.dart
callers: []
mtime: '2026-05-21T12:38:16.792Z'
sha256: 5fbe4be4f5643bedf2d5eef9686d0a08d6ac532c54cf4d10fc75699c38618e31
target_path: test_archive/dump_actor_bytecode.cs
purpose_source: inferred
key_idea_source: inferred
---

Standalone utility script that disassembles the compiled bytecode of the actor/2 procedure from a social-graph play, for inspecting compiler output.
