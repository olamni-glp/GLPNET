---
path: lib/compiler/unify_result.dart
name: unify_result.dart
purpose: Defines the three-valued result type for compile-time GLP unification used by the partial evaluator.
key_idea: 'A sealed UnifyResult hierarchy with three variants modeling GLP''s three-valued unification: UnifySuccess (Map<String,Term> substitution), UnifyFail (reason string), and UnifySuspend (Set<String> of unbound readers), enabling exhaustive switch dispatch.'
dependencies:
- lib/compiler/ast.dart
callers:
- lib/compiler/analyzer.dart
- lib/compiler/partial_evaluator.dart
mtime: '2026-05-21T12:38:13.375Z'
sha256: 34a1261c94414c63dcf23e281a8ad8b6417c58d7157f3f491d3c87b2e4f82c52
plan_started_at: '2026-05-21T15:18:00Z'
plan_completed_at: '2026-05-21T15:23:58Z'
plan_path: .codeconv/conversion-plans/lib/compiler/unify_result.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T09:41:29Z'
target_cs_path: out/csharp/lib/compiler/unify_result.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Defines the three-valued result type for compile-time GLP unification used by the partial evaluator.
