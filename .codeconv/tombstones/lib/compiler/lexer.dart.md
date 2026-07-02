---
path: lib/compiler/lexer.dart
name: lexer.dart
purpose: Lexical analyzer (class Lexer) that scans raw GLP source characters into a flat List<Token> consumed by the parser.
key_idea: Single forward character scan with switch dispatch and maximal-munch _match lookahead for multi-char operators (=:=, =\=, =?=, ..=, ::=); tracks line/column, skips % line and /* */ block comments, classifies identifiers as VARIABLE/ATOM/READER by leading case plus trailing '?'.
dependencies:
- lib/compiler/error.dart
- lib/compiler/token.dart
callers:
- lib/analysis/type_checker/type_checker.dart
- lib/analysis/type_checker/type_environment_builder.dart
- lib/compiler/compiler.dart
- lib/compiler/partial_evaluator.dart
- lib/compiler/pmt/validator.dart
- lib/compiler/project_linker.dart
- lib/engine/glp_engine.dart
- lib/multiagent/agent_runtime.dart
- lib/runtime/module_hierarchy.dart
- test/compiler/partial_evaluator_test.dart
- test/debug_negative.dart
- test/module/cssg_modules_test.dart
- test/module/module_compiler_test.dart
- test/module/module_hierarchy_test.dart
- test/module/module_parser_test.dart
- test/module/module_syntax_v2_test.dart
- test_archive/cssn_modules_test.dart
- test_archive/social_graph_sim_modules_test.dart
mtime: '2026-05-21T12:38:13.243Z'
sha256: f9c89267ee74e7e9151a0e97e57b00fd9db39e01d949bad680f3a6e51d4abe75
topo_level: 1
cycle_group_id: 13
status: pending
target_path: lib/compiler/lexer.cs
plan_started_at: '2026-05-21T14:45:41Z'
plan_completed_at: '2026-05-21T14:50:27Z'
plan_path: .codeconv/conversion-plans/lib/compiler/lexer.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T09:18:07Z'
target_cs_path: out/csharp/lib/compiler/lexer.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Lexical analyzer (class Lexer) that scans raw GLP source characters into a flat List<Token> consumed by the parser.
