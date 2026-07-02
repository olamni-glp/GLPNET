---
path: test/compiler/partial_evaluator_test.dart
name: partial_evaluator_test.dart
purpose: 'Verifies the partial evaluator''s guard validation: which procedures may be unfolded into guard position.'
key_idea: runPE via transformDefinedGuards accepts single-unit-clause guards (my_guard, new_channel) and builtins (integer/ground/number/<,>,=:=); throws CompileError 'Cannot call X in guard position' for multi-clause/body/guard procedures and 'cannot be negated' for ~defined guards.
dependencies:
- lib/compiler/ast.dart
- lib/compiler/error.dart
- lib/compiler/lexer.dart
- lib/compiler/parser.dart
- lib/compiler/partial_evaluator.dart
callers: []
mtime: '2026-05-21T12:38:15.610Z'
sha256: b6a416de5607acf814c73228a7dd938d0b2de5ce07856b7bf42931c4628d5c2a
topo_level: 5
cycle_group_id: 76
status: pending
target_path: test/compiler/partial_evaluator_test.cs
plan_started_at: '2026-05-21T16:24:07Z'
plan_completed_at: '2026-05-21T16:28:37Z'
plan_path: .codeconv/conversion-plans/test/compiler/partial_evaluator_test.dart.md
open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Verifies the partial evaluator's guard validation: which procedures may be unfolded into guard position.
