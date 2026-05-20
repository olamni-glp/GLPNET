> Conversion-spec artifact for lib/runtime/goal_queue.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/runtime/goal_queue.dart
source_sha256: 7d4ad1eabd840fae7dea1b40a1f513715c96986c32362860ae2eab0ce9f22db8
target_code_unit: lib/runtime/goal_queue.cs
constructs:
  - construct_key: "dart.export_directive.show_clause.reexport_only_library"
    source_form: "export 'machine_state.dart' show GoalRef, GoalQueue;"
    target_decision: >-
      NO target .cs file emitted. The faithful translation of a Dart
      export-only library is NOT a placeholder C# file (which would
      either be empty or pretend to re-declare/alias the underlying
      types and thereby create a SECOND target type — a silent surface
      change). Instead, the spec records: (a) this file produces no
      .cs artifact; (b) any consumer that previously imported
      'package:glp_runtime/runtime/goal_queue.dart' must, in its
      converted form, add a `using` directive (or fully-qualified
      reference) pointing directly at the namespace that hosts the
      converted `GoalRef` and `GoalQueue` types — i.e. the namespace
      of the converted lib/runtime/machine_state.cs. The `show GoalRef,
      GoalQueue` allow-list does NOT need a .NET counterpart because
      .NET `using` already imports the full public surface of a
      namespace and the consuming file references the two types by
      name; the Dart `show` clause was a Dart-library-surface
      narrowing that has no semantic equivalent (and no security or
      visibility consequence) in the .NET model. The downstream
      depgraph rewrite step (NOT this spec) is responsible for
      updating consumers' `using`/import lines. The precedent in
      .codeconv/conversion-specs/lib/engine/claude_adapter.dart.md
      (Notes section, export-vs-namespace prose) records the same
      decision and is reused verbatim here, now formalised as an
      idiom so future export-only files reuse it without re-derive
      (FR-012 / SC-007).
    idiom_id: null
    research_finding_id: rf-dart-export-directive-to-csharp-using-alias
    nuance: >-
      Dart `export` vs .NET namespaces (well-known nuance — explicitly
      addressed, not glossed; FR-009 / US2 AS4). Dart `export
      'other.dart';` re-publishes another library's public identifiers
      under the CURRENT library's import path, so a single
      `import 'package:foo/foo.dart'` can transitively expose names
      defined elsewhere. .NET has NO symmetric mechanism: a C# file
      cannot re-export types from one namespace under a different
      namespace name. A type lives in exactly one namespace; consumers
      must `using` THAT namespace (or fully-qualify). Consequence: an
      export-only Dart library has no .cs counterpart; emitting one
      would either be an empty file (pointless) or a fresh type
      alias (semantic change — a SECOND type with the same name,
      breaking type identity for callers). The `show` allow-list has
      no parallel either: .NET `using` imports the full public surface
      of a namespace; per-symbol narrowing is achieved only via
      explicit `using Alias = Namespace.Type;` aliases at each
      consumer, which is not required when consumers already name the
      types directly. Value-vs-reference, null-safety, async, isolate,
      and Stream nuances are NOT APPLICABLE — this directive defines
      no types and no values. The referenced types `GoalRef` and
      `GoalQueue` are NOT specced here; their semantics
      (queue.length / addLast / removeFirst, FIFO vs deque, mutation
      while iterating, snapshot-vs-live views, Queue<T> vs
      LinkedList<T> vs ConcurrentQueue<T>, single-threaded vs
      multi-threaded contract) belong to the convspec for
      lib/runtime/machine_state.dart where those classes are
      declared. This is the load-bearing scope boundary: a re-export
      does not redefine the re-exported types, and the conversion
      spec must not duplicate (and risk drifting from) the
      downstream type's spec.
conversion_units:
  - "NO target .cs file emitted for this Dart library (export-only stub; conversion is a depgraph/import-rewrite concern, not a code-emission concern)"
  - "Depgraph note: consumers that imported 'package:glp_runtime/runtime/goal_queue.dart' must, in their converted form, `using` the namespace of the converted lib/runtime/machine_state.cs (where GoalRef and GoalQueue actually live)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-export-directive-to-csharp-using-alias — export-only Dart library mapping

- **Deep analysis.** The entire source file is a single line: `export
  'machine_state.dart' show GoalRef, GoalQueue;`. No types, no
  top-level functions, no fields, no imports (no `import` directive at
  all), no `library` directive, no doc comment. The file's role is
  purely a surface-broadening re-publication: it makes the two names
  `GoalRef` and `GoalQueue` (defined in the sibling
  `machine_state.dart`) reachable under the import path
  `package:glp_runtime/runtime/goal_queue.dart`. Verification: grep
  confirms `class GoalRef` and `class GoalQueue` are declared in
  `glp_runtime_net/lib/runtime/machine_state.dart` (lines 13 and 55
  respectively), NOT in this file. The `show` clause exposes exactly
  those two identifiers (no others) from the source library.
- **Authoritative Dart.** The Dart language specification on libraries
  and imports (https://dart.dev/language/libraries — Dart official)
  defines `export 'other.dart' show A, B;` as re-publishing the
  listed public names from `other.dart` as part of the current
  library's exported surface; consumers importing the current library
  see `A` and `B` as if they were declared here. The `show` clause is
  a surface-narrowing filter on the re-exported names; identifiers
  not listed are not re-exported. The directive defines no new types
  and creates no new identity — `GoalRef` re-exported is the same
  class as `GoalRef` defined in `machine_state.dart`.
- **Authoritative .NET.** The C# language reference on namespaces and
  the `using` directive
  (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive
  — Microsoft Learn, official) defines `using Namespace;` as
  importing the full set of public types in a namespace into the
  current compilation unit's lookup scope; `using Alias =
  Namespace.Type;` creates a per-file alias for a single type. There
  is NO C# construct that re-publishes a type from one namespace
  under another namespace's name. A `partial class` is not a
  re-export (it adds parts to the SAME type), and a derived class /
  type-forwarding-attribute scenario applies only at assembly
  granularity, not source-file granularity, and would change type
  identity. Therefore the faithful conversion of an export-only Dart
  library is a NULL artifact at file granularity (no .cs file), with
  the surface effect realised at consumer sites via `using` lines
  pointing at the namespace of the converted underlying types.
- **Precedent in this corpus.** The convspec for
  `lib/engine/claude_adapter.dart`
  (`.codeconv/conversion-specs/lib/engine/claude_adapter.dart.md`,
  Notes section, "Dart `export` versus .NET namespaces") records the
  same decision verbatim as an anticipatory note even though that
  file itself contained no `export` directive. THIS file is the first
  in the corpus that actually exercises the construct — so the prior
  prose note is now formalised as `rf-dart-export-directive-to-csharp-using-alias`
  and the convspec records `idiom_id: null` (first-seen, this row
  defines it) so subsequent export-only Dart files (e.g.
  `glp_runtime_net/lib/compiler/compiler.dart` lines 14-16, which has
  three `export` lines mixed with code) can REUSE this idiom rather
  than re-derive (FR-012 / SC-007). The conflict-check (FR-014)
  passes trivially: no existing active idiom contradicts this
  decision; the claude_adapter prose note is the same conclusion.
- **Authoritative both sides; no escalation.** Dart official docs
  authoritative for the source-side surface-publication semantics;
  Microsoft Learn authoritative for the absence of a symmetric .NET
  construct. No corroborating web source needed.

## Notes — well-known nuances explicitly addressed (FR-009 / US2 AS4)

- **Stream / IAsyncEnumerable / Future / Task / async / await /
  isolate**: ABSENT — the file declares no executable code at all,
  let alone asynchronous code. No conversion question to answer.
- **Value-vs-reference**: NOT APPLICABLE — the file declares no
  fields and no instances. The re-exported `GoalRef` and `GoalQueue`
  classes have their own value/reference decisions recorded in the
  convspec for `lib/runtime/machine_state.dart` (the file where they
  are declared), NOT here.
- **Null-safety mapping**: NOT APPLICABLE — no declarations, no
  parameters, no return types in this file.
- **Generics, sealed, mixins, extensions, late, nullable annotations,
  bitwise/arithmetic, exhaustive switch**: ALL ABSENT from this
  file's source.
- **FIFO / priority / Queue<T> vs LinkedList<T> / addLast /
  removeFirst / snapshot-vs-live iteration / mutation while
  iterating** (brief checklist): these are properties of the
  `GoalQueue` CLASS, not of this re-export file. The class is
  declared in `glp_runtime_net/lib/runtime/machine_state.dart` (line
  55: `class GoalQueue { final QueueList<GoalRef> _q = QueueList<GoalRef>(); ... }`)
  and uses the `package:quiver` `QueueList<T>` (a deque-shaped list
  with O(1) head/tail mutation). Recording the FIFO / Queue<T> vs
  LinkedList<T> / fairness / snapshot-vs-live decisions HERE would
  be a scope error (FR-024-style scope discipline): they belong to
  the machine_state.dart convspec where the storage type is chosen,
  the method bodies are visible, and the mutation-while-iterating
  surface is verifiable from the actual operation set. The
  message_queue.dart convspec
  (`.codeconv/conversion-specs/lib/multiagent/message_queue.dart.md`,
  research_finding_id `rf-dart-collection-queue-to-csharp-queue`)
  already records the Dart `dart:collection Queue` → .NET
  `System.Collections.Generic.Queue<T>` decision with the FIFO
  contract, Queue<T>-vs-LinkedList<T>-vs-ConcurrentQueue<T> trichotomy,
  snapshot-vs-live (`List.from` → `ToList`, `List.unmodifiable` →
  `ReadOnlyCollection<T>`), and single-threaded-contract preservation,
  precisely so the eventual machine_state.dart convspec can reuse
  that idiom directly. This file (an export stub) has nothing to add
  on those topics.
- **Trivial subsumed**: the bare `;` statement terminator and the
  `show` allow-list syntax are subsumed by the single recorded
  construct (the `show` clause's lack of a .NET counterpart is the
  load-bearing nuance and is recorded in the construct's `nuance`
  field).
- **Zero escalations**: the construct is resolved from authoritative
  Dart (dart.dev language/libraries) and .NET (learn.microsoft.com
  csharp/language-reference/keywords/using-directive) official
  documentation, plus an in-corpus precedent (claude_adapter.dart
  Notes) that is now formalised into a first-seen idiom (FR-012 /
  SC-007). No undecidable construct; no idiom-vs-research conflict;
  no idiom-vs-idiom conflict.
