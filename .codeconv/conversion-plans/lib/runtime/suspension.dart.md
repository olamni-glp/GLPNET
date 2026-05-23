---
path: lib/runtime/suspension.dart
cycle_group_id: 21
scc_siblings: []
generated_at: 2026-05-21T14:42:51Z
source_sha256: b7e9c01b3b5ca5a3922c8a3656221803797fd5b434cecc8d63412d94d9c61319
schema_version: 1
---

# Conversion Plan: lib/runtime/suspension.dart

## 1. Source Analysis

The Dart source (`glp_runtime_net/lib/runtime/suspension.dart`, 43 LOC) declares the canonical FCP shared-suspension-record idiom, anchoring the implementation of double-activation prevention used by the GLP runtime's suspension lists.

Top-of-file structure:
- Three leading `///` doc-comment lines describing intent: "Shared Suspension Records (FCP Design) / One SuspensionRecord shared across multiple lists via wrapper nodes / Activated once, then disarmed to prevent double-activation".
- `library;` directive (line 4, unnamed-library form) anchoring the doc-comments to the library compilation unit.
- No `import` / `export` / `part` / `part of` directives. Zero external dependencies (the tombstone's `dependencies: []` is consistent with the source).

Two class declarations, both reference types by default (Dart classes are heap-allocated reference types):

1. **`class SuspensionRecord`** (lines 8–24) — the shared-state record:
   - Fields: `int? goalId` (nullable, mutable — Process ID used as disarm sentinel), `final int resumePC` (non-nullable, set-once at construction — the resume PC / kappa procedure entry point).
   - Positional constructor `SuspensionRecord(this.goalId, this.resumePC)` — accepts both fields.
   - `void disarm()` — sets `goalId = null;` (the single permitted mutation; one-shot armed→disarmed transition; idempotent on repeat).
   - `bool get armed => goalId != null;` — derived state.
   - `@override String toString() => 'SuspensionRecord(goal=$goalId, pc=$resumePC, armed=$armed)';` — observable trace output consumed by REPL `:trace`/`:debug`.
   - NO `==`/`hashCode` override (default identity equality — load-bearing: two distinct suspended goals with the same `(goalId, resumePC)` must NOT compare equal).
   - NO async surface, NO `Future`/`Stream`/`Completer`, NO locking, NO `Isolate`.

2. **`class SuspensionListNode`** (lines 29–42) — the per-reader wrapper:
   - Fields: `final SuspensionRecord record` (non-nullable, set-once at construction — the shared reference), `SuspensionListNode? next` (nullable, mutable — per-reader list chain pointer).
   - Positional constructor `SuspensionListNode(this.record)` — `next` defaults to null.
   - Three delegating getters: `bool get armed => record.armed;`, `int? get goalId => record.goalId;`, `int get resumePC => record.resumePC;` — each forwards to the shared record on every read (NOT caching — caching would freeze disarm propagation).
   - `@override String toString() => 'SuspensionListNode(record=$record)';` — interpolates `record.toString()`.
   - NO `==`/`hashCode` override (default identity equality).

Design contract (from the leading doc-comments and the convspec's deep-analysis section): one `SuspensionRecord` per suspended goal, multiple `SuspensionListNode` wrapper instances per record (one per reader cell). When any writer eventually binds any of those readers and activates the goal, `disarm()` mutates `goalId = null` on the shared record; every other wrapper that still points to that record observes `armed = false` on its next traversal and skips re-activation. The double-activation prevention is built on reference identity + shared mutable state.

## 2. Dart → C#/.NET Conversion Plan

Mirrors convspec §`constructs` decisions verbatim.

**Construct 1: `library;` directive top-of-file (no name)**
- Dart form: bare `library;` directive after leading doc-comments; anchors the doc-comments to the library compilation unit.
- C#/.NET decision: **Elide** the `library;` directive — .NET has no `library` concept; the compilation unit is the source file plus its namespace declaration. The three Dart `///` doc-comment lines migrate to file-header `///` XML doc comments on the namespace block (mirroring `lib/runtime/`). No semantic loss; no value-vs-reference / null-safety / async surface implicated.

**Construct 2: `class SuspensionRecord` — shared-state record (nullable-int goalId + final int resumePC + disarm + armed-getter + toString-override)**
- Dart form: `class SuspensionRecord { int? goalId; final int resumePC; SuspensionRecord(this.goalId, this.resumePC); void disarm() { goalId = null; } bool get armed => goalId != null; @override String toString() => '…'; }`.
- C#/.NET decision: **`public class SuspensionRecord`** (reference type) in the namespace mirroring `lib/runtime/`. NOT `record class` / `struct` / `record struct` — identity-by-reference is load-bearing (rejection rationale below in nuance).
  - `public int? GoalId { get; private set; }` — nullable mutable property; only `Disarm()` writes it from inside the class (preserves the Dart bare-mutable-field + single-in-class-writer contract).
  - `public int ResumePC { get; }` — Dart `final` → .NET get-only auto-property assigned in the constructor.
  - Constructor `public SuspensionRecord(int? goalId, int resumePC)` — assigns both fields (single non-optional-parameter form, matches Dart positional shape).
  - `public void Disarm() { GoalId = null; }` — one-shot disarm; idempotent on repeat (matches Dart).
  - `public bool Armed => GoalId != null;` — expression-bodied get-only property; derives armed state from the nullable.
  - `public override string ToString() => $"SuspensionRecord(goal={GoalId?.ToString() ?? \"null\"}, pc={ResumePC}, armed={Armed})";` — explicit `?.ToString() ?? "null"` preserves Dart's null-interpolation observable output (Dart `$goalId` on null renders the literal string `"null"`; .NET `{GoalId}` on null `int?` renders empty — see §4 cross-cutting note).
  - NO `Equals`/`GetHashCode` override — default reference equality preserved.

**Construct 3: `class SuspensionListNode` — wrapper (final record reference + mutable next pointer + delegating getters + toString-override)**
- Dart form: `class SuspensionListNode { final SuspensionRecord record; SuspensionListNode? next; SuspensionListNode(this.record); bool get armed => record.armed; int? get goalId => record.goalId; int get resumePC => record.resumePC; @override String toString() => '…'; }`.
- C#/.NET decision: **`public class SuspensionListNode`** (reference type) in the namespace mirroring `lib/runtime/`. NOT `record class` / `struct` / `record struct` — identity-by-reference is load-bearing on BOTH wrapper and shared record.
  - `public SuspensionRecord Record { get; }` — Dart `final` → get-only auto-property assigned in the constructor; non-nullable under NRT-enabled context.
  - `public SuspensionListNode? Next { get; set; }` — mutable nullable property; the list chain is re-linked after construction (when a writer activates the head, the next node is promoted), so `{ get; set; }` is required — NOT `init`-only.
  - Constructor `public SuspensionListNode(SuspensionRecord record)` — assigns `Record`, leaves `Next` null.
  - Three delegating get-only properties (expression-bodied, NOT cached — forwarding on EVERY read preserves disarm propagation):
    - `public bool Armed => Record.Armed;`
    - `public int? GoalId => Record.GoalId;`
    - `public int ResumePC => Record.ResumePC;`
  - `public override string ToString() => $"SuspensionListNode(record={Record})";` — implicit `Record.ToString()` invocation by .NET interpolation matches Dart `$record` invocation of `record.toString()`.
  - NO `Equals`/`GetHashCode` override — default reference equality preserved.

**Cross-cutting nuances (from convspec, mirrored):**
- **Value-vs-reference**: BOTH classes MUST be reference-type `class`. `record class` is rejected because its value-based `Equals`/`GetHashCode` contradict identity equality (two distinct suspended goals with equal `(goalId, resumePC)` are NOT interchangeable). `struct` / `record struct` are categorically rejected because field-copy semantics would silently fork the shared state graph — every `SuspensionListNode.Record` would hold a private copy, and disarming through one wrapper would leave the others armed (silent and catastrophic).
- **Null-safety**: `int? goalId` → `int?` (`System.Nullable<int>`) under NRT-enabled context. NOT `int` with a magic sentinel (`-1`) — non-faithful. `final int resumePC` → plain non-nullable `int`. `final SuspensionRecord record` → non-nullable `SuspensionRecord`. `SuspensionListNode? next` → nullable `SuspensionListNode?`.
- **Mutable-property-with-private-setter**: Dart bare mutable field with exactly one in-class writer → .NET `{ get; private set; }` (preserves public-read / in-class-write contract). Public mutable field rejected — exposes external write access Dart intentionally does not.
- **Delegating-getter forwarding-vs-caching**: the three wrapper getters MUST forward to `Record` on every read; caching at construction would freeze the disarm-propagation contract.
- **Mutable-Next-pointer**: `Next` must be `{ get; set; }`, NOT `init`-only — list chain re-linking requires post-construction mutation.
- **Null-interpolation observable-output**: Dart `$goalId` on null renders `"null"`; .NET `{GoalId}` on null `int?` renders empty. To preserve REPL trace output verbatim, codegen MUST emit `{GoalId?.ToString() ?? "null"}`, NOT bare `{GoalId}`.
- **Identity equality**: neither class overrides `==`/`Equals`/`GetHashCode`; default reference equality preserved on both sides.
- **Async/Stream/Isolate/Mixin/Sealed**: ABSENT. The data structures are purely synchronous in-memory state. Surrounding runtime owns concurrency / ownership boundary for the suspension lists; this file just defines the node shapes.

## 3. Decomposed Task Units

- **T1**: Emit namespace block mirroring `lib/runtime/` with file-header `///` XML doc comments carrying the three Dart library doc-comment lines; elide `library;`.
- **T2**: Emit `public class SuspensionRecord` with `int? GoalId { get; private set; }`, `int ResumePC { get; }`, single non-optional-parameter constructor assigning both, `void Disarm()` setting `GoalId = null`, `bool Armed => GoalId != null`, `ToString()` override using `{GoalId?.ToString() ?? "null"}` interpolation; no equality override.
- **T3**: Emit `public class SuspensionListNode` with `SuspensionRecord Record { get; }`, `SuspensionListNode? Next { get; set; }`, single-parameter constructor assigning `Record` (leaving `Next` null), three expression-bodied delegating get-only properties (`Armed`/`GoalId`/`ResumePC` forwarding to `Record`), `ToString()` override with `{Record}` interpolation; no equality override.

## 4. Research Findings

None required — every construct is fully grounded in the convspec's two research findings (`rf-dart-shared-mutable-record-by-reference-to-csharp-class` and `rf-dart-library-directive-to-csharp-namespace-elision`) plus the cross-cutting null-interpolation observable-output note. Both findings are decided from official Dart language docs (https://dart.dev/language/classes, https://dart.dev/language/libraries) and official .NET docs (reference-types, records, struct, properties, namespaces) — see convspec §"Rationale and research provenance" for the verbatim citations. No additional research required; web research not invoked.

## 5. Consistency Pass

Cross-checked Dart source ↔ convspec ↔ this plan:

- **Library directive**: source line 4 `library;` ↔ convspec construct 1 (elide + migrate doc comments) ↔ plan §2 construct 1 + T1. Consistent.
- **`SuspensionRecord` fields**: source `int? goalId` + `final int resumePC` ↔ convspec `int? GoalId { get; private set; }` + `int ResumePC { get; }` ↔ plan §2 construct 2 + T2. Consistent (nullability, mutability, single-in-class-writer all preserved).
- **`SuspensionRecord.disarm()`**: source sets `goalId = null` ↔ convspec one-shot idempotent disarm ↔ plan §2 construct 2 + T2. Consistent.
- **`SuspensionRecord.armed` getter**: source `goalId != null` ↔ convspec expression-bodied derivation ↔ plan §2 construct 2 + T2. Consistent.
- **`SuspensionRecord.toString`**: source `'SuspensionRecord(goal=$goalId, pc=$resumePC, armed=$armed)'` ↔ convspec `{GoalId?.ToString() ?? "null"}` null-interpolation preservation ↔ plan §2 construct 2 + cross-cutting null-interpolation note + T2. Consistent — derived from convspec's cross-cutting note that explicitly addresses the Dart-vs-.NET interpolation difference.
- **`SuspensionListNode` fields**: source `final SuspensionRecord record` + `SuspensionListNode? next` ↔ convspec `SuspensionRecord Record { get; }` + `SuspensionListNode? Next { get; set; }` ↔ plan §2 construct 3 + T3. Consistent (`Next` mutable, NOT `init`-only — explicitly justified by post-construction list re-linking).
- **`SuspensionListNode` delegating getters**: source three forwarding getters reading `record.*` ↔ convspec expression-bodied forwarding (NOT cached) ↔ plan §2 construct 3 + T3. Consistent.
- **`SuspensionListNode.toString`**: source `'SuspensionListNode(record=$record)'` ↔ convspec `{Record}` interpolation (implicit `Record.ToString()`) ↔ plan §2 construct 3 + T3. Consistent.
- **Identity equality**: source has no `==`/`hashCode` override on either class ↔ convspec rejects `record class` / `struct` / `record struct` on both ↔ plan §2 cross-cutting nuances + constructs 2/3. Consistent — load-bearing on BOTH classes.
- **Async/Stream/Isolate/Mixin/Sealed**: source has none ↔ convspec asserts ABSENT ↔ plan §2 cross-cutting nuances. Consistent.

No gaps; no inconsistencies surfaced.

## 6. Escalations

None.
