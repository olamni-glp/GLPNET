---
path: lib/runtime/machine_state.dart
cycle_group_id: 29
scc_siblings: []
generated_at: 2026-05-21T14:42:44Z
source_sha256: cd45d43c86ed4e7e5835cbc6feb537c42987fecdcbe7cc5fe097e126ee226d6e
schema_version: 1
---

# Conversion Plan: lib/runtime/machine_state.dart

## 1. Source Analysis

Inspected `glp_runtime_net/lib/runtime/machine_state.dart` (63 lines, sha256 `cd45d43c86ed4e7e5835cbc6feb537c42987fecdcbe7cc5fe097e126ee226d6e`). Top-level entities, in source order:

- `import 'package:collection/collection.dart';` — sole import; only `QueueList<T>` is referenced.
- Four `typedef … = int;` declarations: `GoalId`, `Pc`, `ReaderId`, `WriterId` — transparent identifier-kind aliases (doc comment: "opaque ints for now").
- `enum GoalStatus { active, suspended, failed, succeeded }` — plain tag-only enum, no enhanced surface.
- `class GoalRef` — two-int value carrier with **`==` and `hashCode` overrides** (`other is GoalRef && other.id == id && other.pc == pc`, `Object.hash(id, pc)`); `const` positional ctor; both fields `final`. The **only** class in the file with a value-equality override.
- `typedef SigmaHat = Map<WriterId, Object?>;` — mutable map alias.
- `const int tailRecursionBudgetInit = 26;` — top-level compile-time constant; literal `26` is spec-mandated.
- `class GoalState` — canonical per-goal mutable state container. Fields: `final GoalId goalId`, mutable `Pc pc`, `final Pc kappa`, mutable `GoalStatus status` (default `active`), mutable `int tailBudget` (default `tailRecursionBudgetInit`), mutable `SigmaHat sigmaHat`, `final Object? program` (nullable). Single named-only ctor with three `required` parameters, three defaulted, plus a nullable `SigmaHat? sigmaHat` initialised via `sigmaHat = sigmaHat ?? <WriterId, Object?>{}` in the initialiser list (default-empty-map idiom). No `==`/`hashCode` override → identity equality.
- `class GoalQueue` — thin FIFO wrapper around `final QueueList<GoalRef> _q = QueueList<GoalRef>()`. Surface used: `isEmpty`, `length`, `enqueue` (tail-add), `dequeue` returning `GoalRef?` (`null` on empty), `items` returning `Iterable<GoalRef>`. No `==`/`hashCode` override → identity equality.

No async surface (no `async`, `await`, `Future`, `Stream`, `Completer`, `Isolate`), no locks, no mixins, no `sealed`. Single-threaded; per-goal-owned `GoalState`; runtime-spec-driven mutation surface (PC advances, status transitions, `tailBudget` decrement, `sigmaHat.clear()` at clause_next/suspend, `sigmaHat[w]=v` during HEAD). No trail/choice-point fields here (those live in the runner / heap-FCP layer, not in `GoalState`).

## 2. Dart → C#/.NET Conversion Plan

Mirrors the ratified convspec verbatim. Target code unit: `lib/runtime/machine_state.cs`; namespace mirrors `lib/runtime/`.

- **Identifier typedefs (`GoalId`, `Pc`, `ReaderId`, `WriterId`)** → namespace-scoped `global using GoalId = int;` (and siblings), each transparently aliasing `System.Int32`. NO nominal wrapper struct — the Dart source explicitly defers nominal typing ("opaque ints for now") and never converts between kinds. Width: `Int32`, not `Int64` — counters/PCs fit comfortably and widening would inflate every dictionary bucket and comparison cost.

- **`enum GoalStatus { active, suspended, failed, succeeded }`** → `public enum GoalStatus { Active, Suspended, Failed, Succeeded }`. Members PascalCased per .NET naming guideline; default `int` underlying type; no explicit ordinals (source never observes them); non-nullable value type — matches Dart enum-field default.

- **`class GoalRef`** (the sole value-equal class) → `public readonly record struct GoalRef(int Id, int Pc);`. Value type + value equality + no per-instance heap allocation; matches Dart `==`/`hashCode` override exactly. The synthesised `Equals`, `GetHashCode`, `ToString`, `Deconstruct` substitute for the hand-written Dart overrides. `readonly` mirrors Dart `final`-fields + `const` ctor. NOT `record class` (still allocates); NOT bare `struct` (loses record-syntax brevity); NOT `class` with manual overrides (pays per-instance heap allocation that a 2-int payload does not need).

- **`typedef SigmaHat = Map<WriterId, Object?>;`** → `global using SigmaHat = System.Collections.Generic.Dictionary<int, object?>;`. Concrete `Dictionary<,>`, NOT `IDictionary<,>` (would lose the mutate-in-place contract), NOT `ImmutableDictionary` (the source mutates: indexer assignment + `clear()`), NOT `ConcurrentDictionary` (single-owning-context invariant; introducing concurrency would advertise a safety property the runtime does not need and change snapshot/iteration semantics). Value type stays `object?` (nullable reference under enabled NRT); key type is the resolved `int` alias `WriterId`.

- **`const int tailRecursionBudgetInit = 26;`** → `public static class MachineStateConstants { public const int TailRecursionBudgetInit = 26; }`. Top-level Dart bindings must be rehomed into a type in C#; `const` (not `static readonly`) preserves compile-time-constant intent (usable in attribute args, switch labels). Literal `26` preserved verbatim at the declaration site — spec-mandated, do NOT inline at use sites.

- **`class GoalState`** → `public class GoalState` (reference type, identity equality). NEVER `record`/`record class`/`record struct`/`struct` — see Notes below for the load-bearing identity-vs-value reasoning. Properties:
  - `public GoalId GoalId { get; }` (init-only via ctor — mirrors Dart `final`)
  - `public Pc Pc { get; set; }` (mutable — runtime advances PC)
  - `public Pc Kappa { get; }` (init-only)
  - `public GoalStatus Status { get; set; } = GoalStatus.Active;`
  - `public int TailBudget { get; set; } = MachineStateConstants.TailRecursionBudgetInit;`
  - `public SigmaHat SigmaHat { get; set; }` (reference mutable AND contents mutable)
  - `public object? Program { get; }` (init-only, nullable)

- **`GoalState` named-only ctor with `required`/defaults/null-coalesce** → positional ctor with explicit defaults; callers use C# named-argument syntax for readability:
  ```
  public GoalState(GoalId goalId, Pc pc, Pc kappa,
                   GoalStatus status = GoalStatus.Active,
                   int tailBudget = MachineStateConstants.TailRecursionBudgetInit,
                   SigmaHat? sigmaHat = null,
                   object? program = null)
  ```
  Body assigns each property; `SigmaHat = sigmaHat ?? new SigmaHat();` preserves the default-empty-map null-coalesce verbatim (the textbook avoid-shared-mutable-default fix — fresh dictionary per instance, never an aliased singleton). NOT C# 11 `required` modifier + object-initialiser (inconsistent with the surrounding runtime port's constructor convention).

- **`class GoalQueue`** → `public class GoalQueue` (reference type, identity equality). Backing field: `private readonly System.Collections.Generic.Queue<GoalRef> _q = new();`. Surface:
  - `public bool IsEmpty => _q.Count == 0;`
  - `public int Length => _q.Count;`
  - `public void Enqueue(GoalRef r) => _q.Enqueue(r);` (tail insert — matches Dart `add`)
  - `public GoalRef? Dequeue() => _q.Count == 0 ? null : _q.Dequeue();` (explicit `Count == 0 ? null` guard MANDATORY — `Queue<T>.Dequeue` throws `InvalidOperationException` on empty; Dart returns null; the wrapper must preserve the Dart contract). Return type `GoalRef?` is `Nullable<GoalRef>` because `GoalRef` is a value type.
  - `public IEnumerable<GoalRef> Items => _q;` (`Queue<T>` directly implements `IEnumerable<T>` with head-to-tail iteration matching `QueueList` semantics).
  NOT `LinkedList<T>` (deque — larger surface than the source uses); NOT `ConcurrentQueue<T>` (snapshot-iteration semantics differ; synchronisation cost is unmotivated for a single-threaded runtime).

- **`import 'package:collection/collection.dart';`** → `using System.Collections.Generic;` (covers `Queue<T>` and `Dictionary<TKey,TValue>`). The third-party `package:collection` dependency is **REMOVED**, satisfied by the .NET BCL — no NuGet equivalent introduced.

## 3. Decomposed Task Units

- **T1**: Emit `global using` aliases (`GoalId`, `Pc`, `ReaderId`, `WriterId` = `int`; `SigmaHat` = `Dictionary<int, object?>`) and `using System.Collections.Generic;` directive in the runtime namespace.
- **T2**: Emit `public static class MachineStateConstants { public const int TailRecursionBudgetInit = 26; }` in the runtime namespace.
- **T3**: Emit `public enum GoalStatus { Active, Suspended, Failed, Succeeded }`.
- **T4**: Emit `public readonly record struct GoalRef(int Id, int Pc);` (value-type record-struct, hot-path-friendly).
- **T5**: Emit `public class GoalState` with init-only properties (`GoalId`, `Kappa`, `Program`) and get/set properties (`Pc`, `Status`, `TailBudget`, `SigmaHat`) with declared default-value initialisers where applicable.
- **T6**: Emit `GoalState` positional ctor with defaults; body preserves `SigmaHat = sigmaHat ?? new SigmaHat();` null-coalesce verbatim.
- **T7**: Emit `public class GoalQueue` wrapping `private readonly Queue<GoalRef> _q = new();` with `IsEmpty`/`Length`/`Enqueue`/`Dequeue`/`Items` surface; `Dequeue` MUST keep the explicit `Count == 0 ? null : _q.Dequeue()` guard to preserve the Dart nullable-on-empty contract.
- **T8**: Verify no `Concurrent*` collection, no `Task`/`async`/`Channel<T>`/`IAsyncEnumerable`, no trail/choice-point fields, no `==`/`GetHashCode` override on `GoalState` or `GoalQueue` were introduced (negative assertions matching FR-009 nuances).

## 4. Research Findings

none required (every construct is resolvable from the ratified convspec's rf-* findings and from authoritative Dart/.NET documentation already cited there: rf-dart-typedef-int-to-csharp-global-using-alias, rf-dart-enum-plain-to-csharp-enum, rf-dart-value-class-equality-override-to-csharp-readonly-record-struct, rf-dart-map-to-csharp-dictionary, rf-dart-top-level-const-to-csharp-static-class-const, rf-dart-mutable-state-class-identity-equality-to-csharp-class, rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults, rf-dart-queuelist-fifo-only-to-csharp-queue, rf-dart-package-collection-queuelist-to-csharp-bcl-queue).

## 5. Consistency Pass

Cross-checked §1 (source inspection), §2 (target plan), §3 (decomposed tasks), and the ratified convspec (`.codeconv/conversion-specs/lib/runtime/machine_state.dart.md`). All Dart constructs enumerated in §1 are addressed in §2 with target shapes mirroring the convspec's `constructs` block byte-identically:

- Identifier typedefs → §2 bullet 1 — fixed — derived from convspec `construct_key: "typedef opaque-int-identifier GoalId Pc ReaderId WriterId"`.
- `GoalStatus` enum → §2 bullet 2 — fixed — derived from convspec `construct_key: "enum GoalStatus plain-value-enum …"`.
- `GoalRef` value class → §2 bullet 3 — fixed — derived from convspec `construct_key: "class GoalRef immutable-value-object …"`.
- `SigmaHat` map alias → §2 bullet 4 — fixed — derived from convspec `construct_key: "typedef SigmaHat Map-WriterId-nullable-object"`.
- `tailRecursionBudgetInit` top-level const → §2 bullet 5 — fixed — derived from convspec `construct_key: "const-int tailRecursionBudgetInit …"`.
- `GoalState` mutable state container → §2 bullets 6–7 — fixed — derived from convspec `construct_key: "class GoalState mutable-state-container …"` and `"named-required-ctor-with-defaulted-args-and-collection-default-init"`.
- `GoalQueue` FIFO wrapper → §2 bullet 8 — fixed — derived from convspec `construct_key: "class GoalQueue fifo-wrapper-private-queuelist …"`.
- `package:collection` import → §2 bullet 9 — fixed — derived from convspec `construct_key: "import package-collection QueueList-only minimal-namespace"`.

Negative nuances (no concurrent collections, no async surface, no trail/choice-points, no value-equality on `GoalState`/`GoalQueue`, no struct on `GoalState`/`GoalQueue`) are mirrored from the convspec's "Notes — well-known nuances explicitly addressed" block and re-asserted as T8 in §3 to keep the codegen stage honest. Identifier casing (Dart camelCase → .NET PascalCase publics; underscore-prefix camelCase for private `_q`) is applied uniformly per .NET capitalisation guideline. The convspec records zero open escalations; §6 accordingly records `None.`

Note on metadata: the tombstone records `cycle_group_id: 30` while the orchestration directive (and this artefact's frontmatter) record `cycle_group_id: 29`. The orchestration value is authoritative for this artefact per the planning prompt; no further reconciliation performed here.

## 6. Escalations

None.
