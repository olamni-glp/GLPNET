---
path: lib/analysis/analysis_phase.dart
name: analysis_phase.dart
purpose: Defines the pluggable analysis-phase framework (error/warning types, shared context, multi-phase runner) for type/SRSW/guard checks run during or independent of compilation.
key_idea: AnalysisPhase interface + AnalysisRunner.run/runPhases iterate phases over a shared AnalysisContext, aggregating AnalysisError/Warning (success = no errors); the standard TypeCheck/SRSW/DefinedGuards phases are stub placeholders returning empty lists, assembled by createStandardRunner.
dependencies: []
callers: []
mtime: '2026-05-21T12:38:12.681Z'
sha256: d322a2608cddcee827d4c360ba15b5ac5c7a8a2c5e43b2a690da8b2711e51d78
topo_level: 0
cycle_group_id: 0
status: ready
target_path: lib/analysis/analysis_phase.cs
plan_started_at: '2026-05-21T14:23:16Z'
plan_completed_at: '2026-05-21T14:34:25Z'
plan_path: .codeconv/conversion-plans/lib/analysis/analysis_phase.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T08:56:10Z'
target_cs_path: out/csharp/lib/analysis/analysis_phase.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Defines the pluggable analysis-phase framework (error/warning types, shared context, multi-phase runner) for type/SRSW/guard checks run during or independent of compilation.
