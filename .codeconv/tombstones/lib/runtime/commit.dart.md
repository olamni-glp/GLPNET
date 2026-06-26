---
path: lib/runtime/commit.dart
name: commit.dart
purpose: Applies the tentative writer substitution to the heap on COMMIT, binding writers and collecting goal reactivations.
key_idea: 'applySigmaHatFCP iterates the writer substitution: rejects writer-to-writer binds, routes VarRefs via bindWriterToReader or deref, binds ground via bindWriterNoCallback, re-derefs chained pointer cells (W->V->value), then fires deferred callbacks LAST so nested VarRefs resolve fully.'
dependencies:
- lib/runtime/heap_fcp.dart
- lib/runtime/machine_state.dart
- lib/runtime/suspension.dart
- lib/runtime/terms.dart
callers:
- lib/bytecode/runner.dart
- lib/runtime/runtime.dart
- test/conformance/restart_clause1_test.dart
mtime: '2026-05-21T12:38:14.900Z'
sha256: e6f154b9dd2433d9bce1b5112b75ec042c7827d520e8d8b82e85f1c20c7812dd
topo_level: 3
cycle_group_id: 33
status: pending
target_path: lib/runtime/commit.cs
plan_started_at: '2026-05-21T15:24:22Z'
plan_completed_at: '2026-05-21T16:00:13Z'
plan_path: .codeconv/conversion-plans/lib/runtime/commit.dart.md
open_escalation_count: 0
codegen_completed_at: '2026-05-28T11:54:50Z'
target_cs_path: out/csharp/lib/runtime/commit.cs
build_status: pass
codegen_open_escalation_count: 0
purpose_source: inferred
key_idea_source: inferred
---

Applies the tentative writer substitution to the heap on COMMIT, binding writers and collecting goal reactivations.
