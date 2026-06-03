```yaml
generated_at: '2026-06-03T00:00:00Z'
metric_score: null
model: claude-in-session
optimizer: seed-authored
provenance_note: >-
  Authored seed for the `runtime-core` subsystem (lib/runtime/ except heap_fcp,
  lib/engine/, lib/glp_runtime.dart), descended from _base.md. Idioms from the
  2026-05-28 bulk drive (GLPRuntime -> GlpRuntimeEngine rename, batch 13).
schema_version: 1
seed_from: _base.md
source: bulk-drive-idioms
subsystem: runtime-core
```

Convert one single-computation runtime source — everything under
`lib/runtime/` EXCEPT the `heap_fcp` core (that is the `heap` subsystem), plus
`lib/engine/` and `lib/glp_runtime.dart` — to real, compilable C#/.NET 10.
Emit REAL C# ONLY. Honor the shared base discipline.

## The `GlpRuntimeEngine` rename (flat-namespace disambiguation — load-bearing)

The Dart class `GLPRuntime` must be emitted as C# **`class GlpRuntimeEngine`**
in `namespace GlpRuntime.Runtime;` — NOT `class GlpRuntime`. `GlpRuntime` is
also the root namespace, so `class GlpRuntime` causes CS0118 ambiguity in every
SCC sibling that declares a `GlpRuntime rt` parameter. Every converted file
(this subsystem and any re-conversion) refers to the engine as
`GlpRuntimeEngine`.

## Confirmed dependency surface

- `CommitOps.ApplySigmaHatFCP(HeapFCP, SigmaHat)→IList<GoalRef>`.
- `GlpRuntimeEngine.SuspendGoalFCP(int, int, ISet<int>)`.
- `GlpChannelHandle.Send(Term)→IReadOnlyList<GoalRef>` (synchronous module RPC).
- `GoalRef` = `readonly record struct (int Id, int Pc)`;
  `SigmaHat` = `Dictionary<int, object?>`.

## Semantics

This is the single-computation runtime that drives the bytecode VM: goal queue,
commit, suspension/reactivation, fairness, the engine entrypoint. Preserve the
FCP wake-on-binding contract and the writer-MGU discipline exactly. The runner
(`bytecode` subsystem) is its largest collaborator; read the built bytecode
`.cs` surfaces before calling into them, and never invent a runner signature.
