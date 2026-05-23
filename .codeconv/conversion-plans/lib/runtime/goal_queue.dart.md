---
path: lib/runtime/goal_queue.dart
cycle_group_id: 105
scc_siblings: []
generated_at: 2026-05-21T14:42:28Z
source_sha256: 7d4ad1eabd840fae7dea1b40a1f513715c96986c32362860ae2eab0ce9f22db8
schema_version: 1
---

# Conversion Plan: lib/runtime/goal_queue.dart

## 1. Source Analysis

The entire file is a single line (plus a trailing newline):

```dart
export 'machine_state.dart' show GoalRef, GoalQueue;
```

Direct inspection (Read of the source file) confirms:

- **No `library` directive.** No library name declaration; the file is
  an unnamed library.
- **No `import` directives.** The file imports nothing.
- **No type declarations.** No `class`, `mixin`, `enum`, `extension`,
  `typedef`, or `sealed` declaration is present.
- **No top-level declarations.** No top-level function, getter,
  setter, variable, or constant.
- **No executable code.** No statements, no expressions, no bodies.
- **No doc comments / no comments of any kind.**
- **Exactly one directive.** A single `export` directive that
  re-publishes two identifiers (`GoalRef`, `GoalQueue`) from the
  sibling Dart library file `machine_state.dart` (relative URI,
  resolving to `lib/runtime/machine_state.dart`).
- **`show` allow-list.** The `show GoalRef, GoalQueue` clause narrows
  the re-exported surface to exactly those two names. Any other
  public identifier defined in `machine_state.dart` is NOT re-
  exported under this library's import path.

**Role of the file.** A surface-broadening re-publication: the file's
sole effect is to let downstream Dart code that writes
`import 'package:glp_runtime/runtime/goal_queue.dart';` see the
identifiers `GoalRef` and `GoalQueue` as if they were declared at
that import path. The types themselves live in
`lib/runtime/machine_state.dart` (verified by the tombstone+convspec
research already performed for the convspec phase: `class GoalRef` at
machine_state.dart:13 and `class GoalQueue` at machine_state.dart:55).
The re-export does NOT create new types — re-exported `GoalRef` IS the
same class object as the one declared in machine_state.dart;
re-exported `GoalQueue` IS the same class object as the one declared
in machine_state.dart. There is no aliasing, no wrapping, no derived
declaration.

**Why this file exists in Dart.** The pattern (a façade library that
collects and re-exports a curated subset of public names from
sibling implementation files) is a Dart library-surface idiom: it
gives consumers a single, narrow, stable import path while letting
implementers keep declarations in physically separate files. It has
no behavioural content of its own.

**Cycle group note.** This file is registered with
`cycle_group_id: 105` (per orchestrator metadata) and
`scc_siblings: []` (singleton). The tombstone records
`cycle_group_id: 65` — this is a historical depgraph artefact and
does not affect this plan; the orchestrator's authoritative cycle
group id is used in the front-matter.

## 2. Dart → C#/.NET Conversion Plan

The convspec for this file (RATIFIED, see
`.codeconv/conversion-specs/lib/runtime/goal_queue.dart.md`) records
exactly ONE construct and ZERO escalations. The conversion decision
is mirrored here verbatim in spirit; the wording is condensed for
this artefact but introduces no new decision.

### Construct C1 — `dart.export_directive.show_clause.reexport_only_library`

- **Source form.** `export 'machine_state.dart' show GoalRef, GoalQueue;`
  (the sole line in the file).
- **Target decision (mirrored from convspec).** NO target `.cs` file
  is emitted for `lib/runtime/goal_queue.dart`. The faithful
  translation of an export-only Dart library is the **null artefact**
  at file granularity. Any consumer that previously imported
  `package:glp_runtime/runtime/goal_queue.dart` must, in its
  converted form, add a `using` directive (or fully-qualified
  reference) that points directly at the namespace that hosts the
  converted `GoalRef` and `GoalQueue` types — i.e. the namespace of
  the converted `lib/runtime/machine_state.cs`. The Dart `show
  GoalRef, GoalQueue` allow-list does NOT need a .NET counterpart
  because .NET `using Namespace;` already imports the full public
  surface of a namespace and the consuming file references the two
  types by name. Per-symbol narrowing (the `show` semantic) is, when
  desired in C#, achieved via `using Alias = Namespace.Type;` at
  each consumer — but it is not required here, because consumers
  already name `GoalRef` and `GoalQueue` directly. Updating
  consumers' `using` lines is a downstream depgraph/import-rewrite
  concern, NOT a code-emission concern for this file.
- **Why an empty .cs file is wrong.** Emitting a placeholder
  `.cs` file (empty namespace, or pretending to re-declare/alias the
  underlying types) would either be a pointless artefact or would
  introduce a SECOND .NET type with the same name as the converted
  `GoalRef`/`GoalQueue`, silently breaking type identity for callers.
- **Why a type-forwarder is wrong at this granularity.**
  `[assembly: TypeForwardedTo(...)]` operates at assembly granularity
  for binary-compat scenarios, not at source-file granularity, and
  would change identity semantics in a way Dart `export` does not.
- **Why a `partial class` is wrong.** A `partial class` adds parts to
  the SAME type; the source file declares no parts and is not the
  declaring file for these types.
- **Mapping table.**

| Dart construct | .NET equivalent |
| --- | --- |
| `export 'machine_state.dart' show GoalRef, GoalQueue;` | No `.cs` file. Depgraph-level note: consumers `using` the converted machine_state.cs namespace. |
| `show` allow-list (per-symbol narrowing) | No emission; .NET equivalent (per-consumer `using Alias = Namespace.Type;`) is only required if a consumer wants per-symbol narrowing, which is NOT required here. |

- **Nuances explicitly addressed (FR-009 / US2 AS4).**
  - **Dart `export` vs .NET namespaces.** Dart `export` re-publishes
    another library's public identifiers under the current library's
    import path; .NET has NO symmetric construct. A type lives in
    exactly one namespace; consumers must `using` THAT namespace (or
    fully-qualify). Documented in convspec; mirrored above.
  - **Value-vs-reference, null-safety, async/Future/Task,
    Stream/IAsyncEnumerable, isolate/Task, generics, sealed, mixins,
    extensions, late, nullable, bitwise/arithmetic, exhaustive
    switch.** ALL NOT APPLICABLE — the file declares no types, no
    values, no executable code. Recorded in convspec Notes.
  - **FIFO / Queue<T> vs LinkedList<T> / ConcurrentQueue<T> /
    snapshot-vs-live iteration / mutation-while-iterating /
    single-threaded contract.** NOT APPLICABLE HERE. These are
    properties of the `GoalQueue` CLASS, declared in
    `lib/runtime/machine_state.dart`, and will be recorded in THAT
    file's convspec/plan. The convspec for this file cites the
    relevant cross-file precedent
    (`.codeconv/conversion-specs/lib/multiagent/message_queue.dart.md`,
    `rf-dart-collection-queue-to-csharp-queue`) so the machine_state
    plan can reuse those idioms without re-derivation.

- **Scope discipline.** The convspec explicitly carves the
  boundary: a re-export does not redefine the re-exported types, and
  the conversion artefact for this file must not duplicate (and risk
  drifting from) the machine_state.dart artefact. This plan honours
  that boundary.

### Aggregate emission summary

- **Target files emitted by this plan:** ZERO.
- **Target file recorded in tombstone (`target_path: lib/runtime/goal_queue.cs`):**
  recorded for bookkeeping; no actual emission occurs. This is the
  faithful and only correct .NET translation of an export-only Dart
  library.
- **Depgraph side-effects (out of scope for this artefact, recorded
  here for traceability):** any consumer Dart file whose import URI
  is `package:glp_runtime/runtime/goal_queue.dart` requires its
  converted form's `using` lines to be redirected to the
  machine_state.cs namespace. This is the depgraph/import-rewrite
  stage's responsibility, NOT this plan's.

## 3. Decomposed Task Units

The decomposition is intentionally minimal because the entire file
collapses to a single conversion decision with NO code emission.

- **T1** — Record the "no .cs emitted" decision in the conversion
  tracker for `lib/runtime/goal_queue.dart` (set
  `target_path: lib/runtime/goal_queue.cs` to a sentinel/empty state
  per the codeconv tracker convention; no file write at the target
  path).
- **T2** — Emit a depgraph annotation for downstream import-rewrite:
  every Dart import targeting
  `package:glp_runtime/runtime/goal_queue.dart` must, in its converted
  form, become a `using` of the namespace that hosts the converted
  `GoalRef`/`GoalQueue` (i.e. the namespace of converted
  `lib/runtime/machine_state.cs`). This task is FILED for the
  depgraph/import-rewrite stage; it is NOT a code-write task in this
  file's converter.
- **T3** — Cross-link the recorded idiom
  `rf-dart-export-directive-to-csharp-using-alias` (introduced by
  this file's convspec; `idiom_id: null` in the convspec because this
  file was the first-seen export-only library) so subsequent
  export-only Dart files in the corpus (e.g. the three `export` lines
  at `glp_runtime_net/lib/compiler/compiler.dart` lines 14–16 cited
  by the convspec) REUSE the same decision rather than re-deriving
  it. This is a knowledge-base entry, not a target-tree write.

(No T4+; the file's conversion surface is exhausted.)

## 4. Research Findings

none required.

The convspec is RATIFIED, cites authoritative Dart and .NET official
documentation
(`https://dart.dev/language/libraries`,
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive`)
and an in-corpus precedent
(`.codeconv/conversion-specs/lib/engine/claude_adapter.dart.md`
Notes section). Zero escalations in the convspec; no additional
research is needed to produce this plan. Web research is forbidden
in this stage per the orchestrator's instructions; none is needed.

## 5. Consistency Pass

Cross-checks performed:

- **Source file ↔ plan §1.** Plan §1 transcribes the file's sole line
  and lists every absent construct (imports, types, members,
  comments, executable code). VERIFIED — derived from direct Read of
  `glp_runtime_net/lib/runtime/goal_queue.dart` (sha256
  `7d4ad1ea…22db8`, matching tombstone and convspec).
- **Convspec ↔ plan §2.** Plan §2 mirrors the single recorded
  construct (`dart.export_directive.show_clause.reexport_only_library`)
  and its target decision (no .cs emission; depgraph rewrites
  consumers' `using` lines). The convspec's nuances (export vs
  namespace; value-vs-reference / async / null-safety / generics
  ABSENT or NOT APPLICABLE; FIFO/Queue properties belong to
  machine_state.dart) are mirrored. VERIFIED — derived from convspec
  YAML block + Rationale + Notes sections.
- **Tombstone ↔ plan front-matter.** Tombstone's
  `target_path: lib/runtime/goal_queue.cs` is honoured as a
  bookkeeping record only; plan §2 explicitly notes that NO target
  file is emitted, consistent with the convspec's
  `target_code_unit: lib/runtime/goal_queue.cs` value (the convspec
  uses the same bookkeeping convention). Tombstone's
  `cycle_group_id: 65` is superseded by the orchestrator's
  `cycle_group_id: 105` used in this plan's front-matter; this is a
  metadata version delta, not a content conflict. NO content
  inconsistency.
- **Cycle / singleton ↔ §7 omission.** Orchestrator instructions
  state "singleton — NO §7". This plan omits §7 accordingly. VERIFIED.
- **Section headers ↔ orchestrator template.** All required section
  headers present (`## 1.` through `## 6.`) using the literal Unicode
  arrow U+2192 (`→`) in `## 2.`, NOT ASCII `->`. VERIFIED.
- **Escalations ↔ convspec.** Convspec records `escalations: []` and
  Notes section "Zero escalations". This plan accordingly records
  `None.` in §6 and the front-matter omits any open-count beyond
  what the output line will declare. VERIFIED.
- **Forbidden-research check.** No WebSearch / WebFetch / Agent /
  Task tool invocations occurred while producing this plan;
  authoritative sources were sourced from the already-completed
  convspec research provenance. VERIFIED.

No gaps; all decisions verbatim-derivable from the convspec, the
source file, the tombstone, and the orchestrator's instructions.

## 6. Escalations

None.
