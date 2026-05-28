### E1: Export-only Dart library — no .cs emission per spec/plan

- **Kind**: undecidable
- **File(s)**: lib/runtime/goal_queue.dart
- **Detail**: The ratified convspec (`.codeconv/conversion-specs/lib/runtime/goal_queue.dart.md`) and plan (`.codeconv/conversion-plans/lib/runtime/goal_queue.dart.md`) state unambiguously: NO target .cs file is emitted for this Dart library. The source is a single `export 'machine_state.dart' show GoalRef, GoalQueue;` directive — no types, no members, no executable code. Emitting any C# type would introduce a SECOND definition of `GoalRef` or `GoalQueue` and break type identity for all callers. The codeconv ingest's "real C# + ≥1 top-level construct" gate is structurally incompatible with this legitimate null-artefact outcome.
- **Needs**: Conversion-blocked by design. Resolution requires either (a) a system change that records `no_emit` as a first-class file status (orthogonal to `escalated`/`built`), with the readiness gate treating it as "satisfied for downstream"; or (b) confirmation that this file's downstream consumers will be rewritten by the depgraph/import-rewrite step to reference `GlpRuntime.Runtime` (the namespace hosting the converted `machine_state.cs`), at which point this escalation can be marked resolved without emitting a `.cs`.
- **Status**: open
