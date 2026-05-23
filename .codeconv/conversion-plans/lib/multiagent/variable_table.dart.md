---
path: lib/multiagent/variable_table.dart
cycle_group_id: 23
scc_siblings: []
generated_at: 2026-05-21T15:00:00Z
source_sha256: 39633ffa950c42f693d1b6053f910084556d05aad2174a2fdc91850d5f3eff83
schema_version: 1
---

# Conversion Plan: lib/multiagent/variable_table.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/lib/multiagent/variable_table.dart` (54 lines, sha256
`39633ffa950c42f693d1b6053f910084556d05aad2174a2fdc91850d5f3eff83`) finds the following Dart constructs:

- L1–L7: leading doc-comment block followed by a no-name `library;` directive that anchors the file-level
  doc-comments to the library compilation unit. Doc-comment content states verbatim: "Minimal Variable
  Entry for multiagent runtime support / Provides VariableEntry for tracking suspensions on imported
  readers. / The full VariableTable (V_p) has been replaced by GlobalWritersTable (W_p) for madGLP. This
  file provides only the entry type needed by the core runtime for suspension management."
- L9–L10: two relative `import` directives — `import '../runtime/suspension.dart';` and
  `import '../runtime/terms.dart';`. No `as`/`show`/`hide`. Pull `SuspensionListNode` and `Term`
  unqualified into the file's scope.
- L12–L15: doc-comment on the `VariableEntry` class: "Entry for tracking variable state (suspensions and
  values) / Used by the core runtime to attach suspension lists to imported reader cells that don't have
  a local writer cell."
- L16–L53: `class VariableEntry { ... }`. Reference type (Dart class instances are always heap
  references). No `==`/`hashCode` override → default identity equality.
  - Six instance fields:
    - L18: `final int varId` (non-nullable, get-only).
    - L21: `final bool isReader` (non-nullable, get-only).
    - L24: `final String creator` (non-nullable, get-only).
    - L27: `final int creatorLocalId` (non-nullable, get-only — initialised via initialiser list, see
      below).
    - L30: `Term? boundValue` (mutable, nullable reference).
    - L33: `int? pairedReaderCreatorLocalId` (mutable, nullable value type).
    - L36: `SuspensionListNode? suspensions` (mutable, nullable reference).
  - L38–L45: named-only constructor — three `required` named params (`varId`, `isReader`, `creator`),
    one optional `int? creatorLocalId` parameter (NOT a `this.` initialiser because its field assignment
    needs the `?? varId` fallback), two optional `this.` initialisers (`this.boundValue`,
    `this.pairedReaderCreatorLocalId`) defaulting to `null`. The initialiser list
    `: creatorLocalId = creatorLocalId ?? varId` encodes the load-bearing default: when the caller omits
    `creatorLocalId` (or passes `null`), the entry's field defaults to the entry's own `varId`. The Dart
    parser cannot express this as a default-value expression (parameter defaults must be compile-time
    constants; `varId` is itself a parameter), so it has to be done in the initialiser list.
  - L47–L52: `@override String toString()`:
    - L49: `final keyStr = isReader ? 'R$varId?' : 'W$varId';` — R-prefix-with-trailing-literal-`?` for
      readers, W-prefix for writers. The trailing `?` is a literal character (maGLP R-key convention),
      NOT a nullable-type marker.
    - L50: `final creatorIdStr = creatorLocalId != varId ? ', creatorLocalId=$creatorLocalId' : '';` —
      conditional creator-local-id suffix, only emitted when `creatorLocalId` is non-default.
    - L51: `return 'VarEntry($keyStr, creator=$creator$creatorIdStr)';` — single interpolated return.

Behavioural summary: the class is a per-cell suspension-and-bound-value anchor. `BoundValue` is overwritten
when the variable becomes bound; `Suspensions` (head of the linked suspension chain) is re-linked as
suspensions are added/removed; `PairedReaderCreatorLocalId` is set when the import pairing is finalised.
Every reference to a given entry must observe the same updates — shared-mutable-state-by-reference is
load-bearing.

## 2. Dart → C#/.NET Conversion Plan

Mirrors the ratified convspec verbatim. Each construct → its .NET counterpart:

### C1. `library;` directive with file-level doc-comments → namespace declaration + header XML doc
- Dart: `library;` (L7) + the preceding doc-comment block (L1–L6).
- .NET: `library;` is elided (no .NET counterpart). The file-level doc-comments migrate to a header XML
  doc / comment on the namespace declaration mirroring `lib/multiagent/` per the workspace's pair-specific
  namespace convention (one .NET namespace per source directory). The historical note "full VariableTable
  (V_p) replaced by GlobalWritersTable (W_p) for madGLP — this file provides only the entry type needed
  by the core runtime for suspension management" is preserved verbatim so downstream readers see the W_p
  table is the live structure and `VariableEntry` is the residual per-cell suspension anchor.
- Provenance: `rf-dart-library-directive-to-csharp-namespace-elision` (convspec construct 1).

### C2. Two Dart relative imports → one .NET `using` directive
- Dart: `import '../runtime/suspension.dart';` (L9) + `import '../runtime/terms.dart';` (L10).
- .NET: a single `using <root>.Runtime;` directive. Both Dart files mirror to the same `lib/runtime/`
  namespace per the workspace's pair-specific namespace convention (one .NET namespace per source
  directory), so the two imports collapse into one `using`. No aliasing (`using X = ...`) — no symbol
  renaming in play. References are types (`Term`, `SuspensionListNode`), not static members, so no
  `using static`.
- Provenance: `rf-dart-import-relative-to-csharp-using-namespace` (convspec construct 2).

### C3. `class VariableEntry` (shared-mutable reference-type anchor) → .NET `class`
- Dart: L16–L53 — `class VariableEntry { ... }`, no `==`/`hashCode` override, four `final` fields, three
  mutable fields (two nullable references, one nullable value type), a named-only constructor with one
  required-trio + one nullable-coalesce-fallback + two optional `this.`-initialised params, and an
  `@override toString()`.
- .NET shape (mirrors convspec construct 3 verbatim):
  - Reference type: `public class VariableEntry` declared in the namespace mirroring `lib/multiagent/`.
  - **NOT** `record class` (value-equality contract contradicts identity equality — two entries with
    structurally identical fields represent distinct variable slots and must NOT compare equal).
  - **NOT** `struct` / `record struct` (categorically rejected: value-copy on assignment would silently
    fork `BoundValue` / `Suspensions` graphs; binding through one reference would leave the others'
    copies unbound — silent and catastrophic).
  - No `==`/`Equals`/`GetHashCode` override — default reference equality preserved.
  - Properties:
    - `public int VarId { get; }` — get-only auto-property (non-nullable value type), assigned in
      constructor.
    - `public bool IsReader { get; }` — get-only auto-property (non-nullable value type), assigned in
      constructor.
    - `public string Creator { get; }` — get-only auto-property (non-nullable reference under enabled
      NRT context), assigned in constructor.
    - `public int CreatorLocalId { get; }` — get-only auto-property (non-nullable value type), assigned
      in constructor body via the `?? VarId` fallback. NOT an auto-property initialiser because the
      default depends on another constructor parameter (`VarId`); .NET optional parameters require
      compile-time constants, not other parameters.
    - `public Term? BoundValue { get; set; }` — mutable nullable-reference property. Public setter
      (NOT `private set` / `init`) — the unification engine rebinds this externally when the variable
      becomes bound.
    - `public int? PairedReaderCreatorLocalId { get; set; }` — mutable nullable-value-type property
      (`System.Nullable<int>`). Public setter — set after construction when the import pairing is
      finalised.
    - `public SuspensionListNode? Suspensions { get; set; }` — mutable nullable-reference property
      (head of the linked suspension chain). Public setter, NOT `init`-only — suspension-list
      maintenance re-links the head externally.
  - Constructor:
    ```
    public VariableEntry(
        int varId,
        bool isReader,
        string creator,
        int? creatorLocalId = null,
        Term? boundValue = null,
        int? pairedReaderCreatorLocalId = null)
    ```
    Body assigns: `VarId = varId; IsReader = isReader; Creator = creator;
    CreatorLocalId = creatorLocalId ?? varId; BoundValue = boundValue;
    PairedReaderCreatorLocalId = pairedReaderCreatorLocalId;`. The three Dart `required` named params
    become non-optional .NET positional params (no default); the three optional Dart named params become
    .NET optional params with compile-time-constant `null` defaults. The `?? VarId` fallback is computed
    in the body (Dart's initialiser-list trick is the only faithful .NET shape — naive
    `int creatorLocalId = varId` is invalid C# because optional parameters require compile-time
    constants).
  - Concurrency: NO `lock`, NO `Interlocked`, NO `volatile`, NO concurrent collections, NO async
    signature. Per the convspec's inherited concurrency model (from `GlobalWritersTable`), the .NET port
    preserves the Dart-isolate single-owning-thread invariant; `VariableEntry` instances are touched
    only from their owning agent's execution context. Concurrent collections / atomics would NOT add
    safety here (per-collection thread-safety does not cover the read-then-write `Suspensions` chain
    maintenance) and would silently advertise a thread-safety property the surrounding logic does not
    provide.
- Provenance: `rf-dart-shared-mutable-record-by-reference-to-csharp-class` (convspec construct 3).

### C4. `toString()` override with conditional key prefix and conditional creator-local-id suffix
- Dart: L47–L52 (see §1 for the literal lines).
- .NET shape (mirrors convspec construct 4 verbatim):
  ```
  public override string ToString()
  {
      string keyStr = IsReader ? $"R{VarId}?" : $"W{VarId}";
      string creatorIdStr = CreatorLocalId != VarId
          ? $", creatorLocalId={CreatorLocalId}"
          : string.Empty;
      return $"VarEntry({keyStr}, creator={Creator}{creatorIdStr})";
  }
  ```
  - The trailing literal `?` in `$"R{VarId}?"` is preserved verbatim — maGLP R-key convention, NOT a
    nullable marker.
  - The conditional suffix is preserved verbatim — emitting it unconditionally would change the
    observable diagnostic output and break diff-based REPL `:trace` / integration-test fixtures.
  - NO `StringBuilder` — fixed-shape small interpolation, NOT loop-driven accumulation. The convspec's
    explicit rule: use `StringBuilder` only for unbounded / loop-driven accumulation; use interpolation
    for fixed-shape small strings. This construct is the latter.
  - All four interpolation arguments (`VarId`, `IsReader` via the ternary, `Creator`, `CreatorLocalId`)
    are non-nullable, so no `?.ToString() ?? "null"` workaround is needed (unlike
    `SuspensionRecord.ToString()`).
- Provenance: `rf-dart-string-interpolation-conditional-suffix-to-csharp-interpolation` (convspec
  construct 4).

## 3. Decomposed Task Units

- **T1.** Emit namespace declaration mirroring `lib/multiagent/` per workspace pair-specific convention;
  carry the file-level Dart doc-comments verbatim into a header XML doc / comment on the namespace
  (including the historical "V_p replaced by W_p" note). `library;` itself is elided.
- **T2.** Emit one `using <root>.Runtime;` directive (collapses both Dart imports
  `../runtime/suspension.dart` and `../runtime/terms.dart` because both mirror to the same `lib/runtime/`
  namespace). No aliasing, no `using static`.
- **T3.** Emit `public class VariableEntry` (reference type) in the multiagent namespace; carry the
  class-level doc-comment verbatim. NO `record`/`record class`/`struct`/`record struct`. NO `==`/`Equals`/
  `GetHashCode` override.
- **T4.** Emit get-only auto-properties `int VarId { get; }`, `bool IsReader { get; }`,
  `string Creator { get; }`, `int CreatorLocalId { get; }` — all four assigned in the constructor body.
- **T5.** Emit mutable properties with public setters: `Term? BoundValue { get; set; }`,
  `int? PairedReaderCreatorLocalId { get; set; }`, `SuspensionListNode? Suspensions { get; set; }` —
  all three with `{ get; set; }`, NOT `init`, NOT `private set`. Preserve nullability exactly.
- **T6.** Emit the single constructor
  `VariableEntry(int varId, bool isReader, string creator, int? creatorLocalId = null,
  Term? boundValue = null, int? pairedReaderCreatorLocalId = null)` whose body assigns the six
  properties and computes `CreatorLocalId = creatorLocalId ?? varId;`.
- **T7.** Emit the `public override string ToString()` body verbatim per §2 C4 — two local string
  variables computed by ternary, single interpolated return; preserve the `R{VarId}?` trailing-`?`
  literal and the conditional `, creatorLocalId={CreatorLocalId}` suffix; NO `StringBuilder`.
- **T8.** Verify the emitted class introduces NO `lock` / `Interlocked` / `volatile` / concurrent
  collection / async member — the convspec-inherited single-owning-thread invariant from
  `GlobalWritersTable` MUST be preserved.

## 4. Research Findings

None required. Every construct's translation is verbatim-derivable from the ratified convspec, which
cites four authoritative findings (`rf-dart-library-directive-to-csharp-namespace-elision`,
`rf-dart-import-relative-to-csharp-using-namespace`,
`rf-dart-shared-mutable-record-by-reference-to-csharp-class`,
`rf-dart-string-interpolation-conditional-suffix-to-csharp-interpolation`) backed by official Dart
language-tour pages and Microsoft Learn references for C# reference types, records, structs, the `using`
directive, interpolated strings, and the conditional operator. Concurrency-model invariant is inherited
from `lib/multiagent/global_writers_table.dart.md` and is documented in the convspec's class-level nuance.

## 5. Consistency Pass

Cross-checked the §2 plan against:

- **Source file** (the four constructs identified in §1 are exactly the four constructs §2 maps).
- **Convspec** (each §2 construct C1–C4 mirrors the convspec's corresponding `constructs:` entry
  verbatim — the same target_decision wording, same nuance gates, same provenance ids).
- **Tombstone** (`open_escalation_count: 0`, `status: pending`, `target_path:
  lib/multiagent/variable_table.cs` — consistent with §2's namespace and file shape).
- **Inherited specs** (`lib/multiagent/global_writers_table.dart.md` for the single-owning-thread
  concurrency invariant; `lib/runtime/suspension.dart.md` for the shared-mutable-by-reference idiom
  symmetric with `SuspensionRecord` / `SuspensionListNode`). Both are inherited verbatim — the entry
  participates in the same design unit.
- **CLAUDE.md / GLP language invariants** — the SRSW / writer-MGU / suspension-list machinery the entry
  serves is preserved; the entry is purely an in-memory mutable anchor with no language-level surface
  to change.

No gaps found. No items needing fixing-by-derivation. No items escalated.

## 6. Escalations

None.
