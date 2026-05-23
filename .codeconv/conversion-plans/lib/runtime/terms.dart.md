---
path: lib/runtime/terms.dart
cycle_group_id: 22
scc_siblings: []
generated_at: 2026-05-21T14:43:02Z
source_sha256: afe71bc74cd4474271002cce5b0665e0af46c36775f404102f6c3c7fe30e7a61
schema_version: 1
---

# Conversion Plan: lib/runtime/terms.dart

## 1. Source Analysis

`lib/runtime/terms.dart` is a 103-line file defining the GLP term sum
type: an empty abstract base `Term` plus five concrete leaves
(`ConstTerm`, `StructTerm`, `VarRef`, `MutualRefTerm`, `ModuleTerm`).
Inspected constructs:

- `abstract class Term {}` — line 1, empty open marker base; five leaves
  declare `implements Term` (Dart structural typing). Every consumer in
  the codebase enumerates the five concrete leaves by `is`/type-switch,
  so the hierarchy is closed in practice.
- `class ConstTerm implements Term` — lines 3-8. `final Object? value`,
  positional ctor `ConstTerm(this.value)`, `toString` →
  `'Const($value)'`. No `==`/`hashCode` override → reference identity.
- `class StructTerm implements Term` — lines 10-16. `final String
  functor`, `final List<Term> args`, positional ctor, `toString` →
  `'$functor(${args.join(",")})'`. No `==`/`hashCode` override.
- `class VarRef implements Term` — lines 27-46. `final int addr`,
  positional ctor `VarRef(this.addr)`, `toString` → `'Var@$addr'`,
  overrides `operator ==` (by `addr`) and `hashCode`
  (`addr.hashCode`). Carries load-bearing doc-comment lines 18-26 and
  inline `// NOTE:` lines 33-35 about removed `isReader`/`varId`
  properties (per `irmaGLP-spec.md` Section 3.2.1: addresses are
  OPAQUE; reader/writer identity comes from heap cell tags, NOT
  address arithmetic).
- `class MutualRefTerm implements Term` — lines 48-84. Mutable backing
  field `int _currentWriterAddr`, read-only `final int id`, private
  `static int _nextId = 0`, constructor with initializer-list
  post-increment `MutualRefTerm(this._currentWriterAddr) : id =
  _nextId++`, getter+setter for `currentWriterAddr`, `toString` →
  `'MutualRef#$id(@$_currentWriterAddr)'`, `==`/`hashCode` overrides
  comparing `id` only (entity-identity, NOT structural eq over
  mutable `_currentWriterAddr`). Doc-comment lines 48-62 carries the
  SRSW-ground contract and heap-pointer-architecture-spec.md v3.0
  reference.
- `class ModuleTerm implements Term` — lines 86-102. `final Object
  bytecode` (NON-nullable `Object`, NOT `Object?`), `final String
  name`, ctor `ModuleTerm(this.bytecode, {this.name = ''})` with
  named-default param, `toString` → `'Module($name)'`. No
  `==`/`hashCode` override. Comment on line 93 explicitly notes
  `bytecode` is typed as `Object` (not `BytecodeProgram`) to avoid a
  circular import.

Symmetry analysis: only `VarRef` (value-eq on int handle) and
`MutualRefTerm` (entity-eq on auto-id) override `==`/`hashCode`; the
other three keep Dart's default reference identity. This asymmetry is
intentional and load-bearing — it must be preserved verbatim in C#.

External dependencies of this file: **none** (no `import` statements
in the source). This is a leaf in the dependency graph.

Mutability surface: only `MutualRefTerm._currentWriterAddr` (single
field, R/W via property pair). All other fields are `final`.

Concurrency surface: `MutualRefTerm._nextId++` is a non-atomic
post-increment (Dart single-isolate semantics).

## 2. Dart → C#/.NET Conversion Plan

Each Dart construct is mapped to its C#/.NET equivalent below; the
mapping mirrors the ratified convspec verbatim.

- **`abstract class Term {}`** →
  `public abstract class Term { protected Term() {} }`. Closed-sum
  closure expressed by sealing the leaves (`abstract sealed` is
  forbidden in C# per Microsoft Learn — convspec
  `rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves`).
  Dart `implements Term` on each leaf → C# `: Term` inheritance (Dart
  implements-on-empty-base and C# inheritance are observationally
  identical when the base has no members; C# has no structural
  `implements`).

- **`class ConstTerm implements Term`** → `public sealed class
  ConstTerm : Term` with `public object? Value { get; }` set via
  `public ConstTerm(object? value) { Value = value; }`. Equality
  DELIBERATELY NOT overridden — preserves Dart reference identity
  (NEVER `record` — would silently synthesise structural equality;
  Microsoft Learn Records). `Object?` → `object?` (NRT).

- **`class StructTerm implements Term`** → `public sealed class
  StructTerm : Term` with `public string Functor { get; }` and
  `public IReadOnlyList<Term> Args { get; }`, both set via
  constructor. Backing list ALIASED (not defensively copied) to
  match Dart `this.args = args`. Equality NOT overridden. `ToString`
  uses `string.Join(",", Args)` to match `args.join(",")` exactly
  (no surrounding `[ ]`).

- **`class VarRef implements Term`** → `public sealed class VarRef :
  Term, IEquatable<VarRef>` with `public int Addr { get; }`. Manual
  `Equals(object?)`, `Equals(VarRef?)`, `GetHashCode()`, and
  `==`/`!=` operator overloads comparing `Addr` only. NOT a `record
  class` (avoids `EqualityContract`/`with`-baggage). NOT a `record
  struct` (would break shared-aliasing inside `StructTerm.Args`).
  Load-bearing doc-comment about address opacity carried as XML
  `<summary>`; the inline `// NOTE:` is carried verbatim as a `//`
  comment.

- **`class MutualRefTerm implements Term`** → `public sealed class
  MutualRefTerm : Term, IEquatable<MutualRefTerm>` with
  `private int _currentWriterAddr` backing field exposed as R/W
  property `public int CurrentWriterAddr { get; set; }`, read-only
  `public int Id { get; }`, and `private static int _nextId = 0`.
  Constructor: `public MutualRefTerm(int currentWriterAddr) {
  _currentWriterAddr = currentWriterAddr; Id = _nextId++; }`.
  `Equals`/`GetHashCode`/`==`/`!=` compare `Id` ONLY (entity
  equality, stable across `_currentWriterAddr` mutation).
  `_nextId++` kept NON-atomic (NO `Interlocked.Increment`) to
  preserve Dart single-isolate semantics verbatim.

- **`class ModuleTerm implements Term`** → `public sealed class
  ModuleTerm : Term` with `public object Bytecode { get; }`
  (NON-nullable `object` matching Dart non-nullable `Object`) and
  `public string Name { get; }`. Constructor:
  `public ModuleTerm(object bytecode, string name = "") { Bytecode =
  bytecode; Name = name; }`. Dart named-default param `{this.name =
  ''}` → C# default-valued positional param; call sites
  `new ModuleTerm(bc, name: "foo")` work identically (Microsoft
  Learn named/optional arguments). Equality NOT overridden.
  `bytecode` typed `object` per the source's explicit
  circular-import-avoidance choice.

- **`toString` overrides** → `public override string ToString()`
  returning the same `$"…"` interpolations:
  - `ConstTerm` → `$"Const({Value?.ToString() ?? "null"})"` (literal
    `null` rendering matches Dart `'$null'` → `"null"`; plain
    `{Value}` would emit empty string for nulls — Microsoft Learn
    interpolated strings).
  - `StructTerm` → `$"{Functor}({string.Join(",", Args)})"`.
  - `VarRef` → `$"Var@{Addr}"`.
  - `MutualRefTerm` → `$"MutualRef#{Id}(@{_currentWriterAddr})"`.
  - `ModuleTerm` → `$"Module({Name})"`.

File header / using directives:
`#nullable enable` (or rely on project-wide NRT setting per .NET 10
default), `using System;`, `using System.Collections.Generic;`,
namespace `GlpRuntime.Runtime` (or workspace convention — single
authoritative namespace decision deferred to the workspace-level
namespace policy, not this file). Target file `lib/runtime/terms.cs`.

## 3. Decomposed Task Units

- T1: Create file `lib/runtime/terms.cs` with `#nullable enable`,
  required `using` directives, and namespace declaration.
- T2: Emit `public abstract class Term { protected Term() {} }` as
  the sum-type root.
- T3: Emit `public sealed class ConstTerm : Term` with `object?
  Value` read-only auto-property, positional ctor, and
  null-preserving `ToString` override using
  `Value?.ToString() ?? "null"`.
- T4: Emit `public sealed class StructTerm : Term` with `string
  Functor`, `IReadOnlyList<Term> Args` (aliased not copied),
  positional ctor, and `ToString` using `string.Join(",", Args)`.
- T5: Emit `public sealed class VarRef : Term, IEquatable<VarRef>`
  with `int Addr`, ctor, `Equals(object?)`, `Equals(VarRef?)`,
  `GetHashCode()`, `==`/`!=` operator overloads, `ToString`
  override, and XML doc-comment carrying the address-opacity contract.
- T6: Emit `public sealed class MutualRefTerm : Term,
  IEquatable<MutualRefTerm>` with private mutable
  `_currentWriterAddr`, read-only `Id`, non-atomic static `_nextId`
  counter, R/W `CurrentWriterAddr` property, ctor with `Id =
  _nextId++`, equality-by-`Id` overrides, and `ToString` override.
- T7: Emit `public sealed class ModuleTerm : Term` with non-nullable
  `object Bytecode`, `string Name` (default `""`), ctor with
  default-valued positional `name`, and `ToString` override.
- T8: Carry all `///` doc-comments forward as C# XML `<summary>`
  blocks (referencing `irmaGLP-spec.md` Section 3.2.1 and
  `heap-pointer-architecture-spec.md` v3.0 verbatim); carry inline
  `// NOTE:` comments as `//` comments.
- T9: Verify against convspec — five sealed leaves, two with
  `IEquatable<T>` overrides, three with default reference identity,
  no `record` anywhere, `_nextId` kept non-atomic.

## 4. Research Findings

none required (convspec already carries seven authoritative
Microsoft Learn `WebFetch` research findings — rf-dart-abstract-
marker-base-to-csharp-abstract-sealed-leaves, rf-dart-sumleaf-no-eq-
to-csharp-class-no-record, rf-dart-sumleaf-with-list-no-eq-to-
csharp-class-ireadonlylist, rf-dart-class-eq-on-single-int-field-to-
csharp-iequatable, rf-dart-entity-eq-by-id-mutable-field-to-csharp-
class-iequatable, rf-dart-named-default-param-to-csharp-default-
positional, rf-dart-string-interpolation-join-to-csharp-
interpolation-string-join — all leveraged by §2 above).

## 5. Consistency Pass

Cross-check between source inspection (§1), construct mapping (§2),
and task units (§3):

- Five leaves in source → five `sealed class` declarations in §2 → T3,
  T4, T5, T6, T7 in §3. Match.
- Two leaves with `==`/`hashCode` overrides in source (`VarRef`,
  `MutualRefTerm`) → two `IEquatable<T>` mappings in §2 → T5, T6 in §3.
  Match.
- Three leaves WITHOUT `==`/`hashCode` (`ConstTerm`, `StructTerm`,
  `ModuleTerm`) → three plain `sealed class` (no `IEquatable<T>`) in
  §2 → T3, T4, T7 in §3. Match — fixed: derived from convspec
  rf-dart-sumleaf-no-eq-to-csharp-class-no-record.
- `MutualRefTerm._nextId++` non-atomic — preserved as non-atomic in
  §2 and T6 — fixed: derived from convspec rf-dart-entity-eq-by-id-
  mutable-field-to-csharp-class-iequatable and CLAUDE.md
  "single-thread isolate model" reference.
- `ConstTerm.ToString` null-rendering subtlety — §2 specifies
  `Value?.ToString() ?? "null"` and T3 calls it out explicitly —
  fixed: derived from convspec rf-dart-string-interpolation-join-
  to-csharp-interpolation-string-join.
- `ModuleTerm.bytecode` typing — source line 93 comment says
  "untyped to avoid circular import"; §2 and T7 preserve `object`
  typing — fixed: derived from convspec.
- `StructTerm.Args` aliasing-vs-copy — convspec mandates ALIASED
  backing list to match Dart `this.args = args`; §2 and T4 specify
  no defensive copy — fixed: derived from convspec
  rf-dart-sumleaf-with-list-no-eq-to-csharp-class-ireadonlylist.
- Namespace decision — §2 notes deferral to workspace-level policy;
  this is the single non-derivable item but it is a cross-cutting
  decision shared by all 130 target files, not a per-file question
  (handled at the workspace level, not escalated here).

No gaps remain that affect this file's conversion.

## 6. Escalations

None.
