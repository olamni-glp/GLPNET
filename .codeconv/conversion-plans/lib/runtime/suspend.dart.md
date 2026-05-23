---
path: lib/runtime/suspend.dart
cycle_group_id: 107
scc_siblings: []
generated_at: 2026-05-21T15:19:52Z
source_sha256: a654fdbd8a51e7a83cc8be4d6a6a653efef4f2e7378c28eebd6ecc7ecc421c8f
schema_version: 1
---

# Conversion Plan: lib/runtime/suspend.dart

## 1. Source Analysis

The file `glp_runtime_net/lib/runtime/suspend.dart` is a 9-line Dart source containing exactly four top-level syntactic elements (verified by direct file inspection):

1. **Line 1**: `import 'machine_state.dart';` — a relative import of the same-package sibling library `lib/runtime/machine_state.dart`. Brings the typedef `ReaderId` (= `int`) into scope; consumed as the field type on line 5. No `show`/`hide` clause.

2. **Line 2**: `import 'hanger.dart';` — a relative import of the same-package sibling library `lib/runtime/hanger.dart`. Brings the class `Hanger` into scope; consumed as the field type on line 6. No `show`/`hide` clause.

3. **Lines 4–8**: `class SuspensionNote { ... }` — a Dart class declaration with:
   - Line 5: `final ReaderId readerId;` — a `final` non-nullable instance field of value-type alias `ReaderId` (= `int`).
   - Line 6: `final Hanger hanger;` — a `final` non-nullable instance field referencing a mutable `Hanger` instance.
   - Line 7: `const SuspensionNote(this.readerId, this.hanger);` — a `const`-qualified positional constructor using Dart's field-formal-parameter shorthand (`this.X`); two required positional parameters; no initialiser list; no body.
   - No `==`/`hashCode` override (default identity equality).
   - No `toString` override; no mutator methods; no other members.

The class is an immutable "suspension note" pairing a reader identity with the `Hanger` that arms a suspended goal. The outer class is immutable (both fields `final`); the inner `Hanger` is mutable (its `Armed` flag flips on the first wake, per `hanger.dart.md`). Identity equality is the only contract the source provides: two distinct attachments with coincidentally identical (`readerId`, `Hanger`-reference) are NOT the same note.

The file has zero methods, zero async surface, zero generics, zero mixins, zero `sealed`/`abstract` modifiers, zero isolate/Stream/Future/Completer usage, zero locks/atomics, and zero trail/choice-point machinery.

## 2. Dart → C#/.NET Conversion Plan

Each Dart construct mapped 1:1 to its C#/.NET counterpart, mirroring the convspec at `.codeconv/conversion-specs/lib/runtime/suspend.dart.md` verbatim.

### Construct 1 → `dart.import_directive.relative-same-package.machine_state`

**Dart**: `import 'machine_state.dart';`

**C#/.NET**: NO standalone target artefact. The converted `lib/runtime/suspend.cs` emits a `using` directive naming the .NET namespace hosting the converted `machine_state.cs` (where the ported `ReaderId` alias lives, per `machine_state.dart.md`'s `global using ReaderId = int;` directive). The namespace name is decided by the downstream depgraph/namespace step. Codegen MUST NOT emit a textual relative-path `using` (e.g. `using ./machine_state.cs`) — not valid C#. The alias `ReaderId` is reached transparently as `int` once the `global using` is in scope. If `suspend.cs` and `machine_state.cs` land in the same namespace (mirroring `lib/runtime/`), the `using` may be elided as redundant — depgraph/namespace stage decides. Nuance: Dart imports a *library/file*; C# imports a *namespace*. No per-symbol `show`/`hide` counterpart in C#. Value-vs-reference / null-safety / async / Stream / isolate: NOT APPLICABLE. Reference-identity: NOT APPLICABLE.

### Construct 2 → `dart.import_directive.relative-same-package.hanger`

**Dart**: `import 'hanger.dart';`

**C#/.NET**: NO standalone target artefact. The converted `lib/runtime/suspend.cs` emits a `using` directive naming the .NET namespace hosting the converted `hanger.cs` (where the ported `Hanger` class lives, per `hanger.dart.md`). The namespace name is decided by the downstream depgraph/namespace step. Codegen MUST NOT emit a textual relative-path `using`. The consumed `Hanger` type is reached as the namespace-qualified `Hanger` class once the `using` is in scope. If `suspend.cs` and `hanger.cs` ultimately land in the same namespace, the `using` may be elided. Nuance: identical to Construct 1 — import-unit asymmetry, no `show`/`hide`. Value-vs-reference / null-safety / async / Stream / isolate: NOT APPLICABLE. Reference-identity: NOT APPLICABLE.

### Construct 3 → `dart.class.immutable-aggregate-aggregating-mutable-reference-two-final-fields-positional-const-ctor-identity-equality`

**Dart**:
```dart
class SuspensionNote {
  final ReaderId readerId;
  final Hanger hanger;
  const SuspensionNote(this.readerId, this.hanger);
}
```

**C#/.NET**: A reference-type `class SuspensionNote` (NOT `record`, NOT `record class`, NOT `struct`, NOT `record struct`) in the namespace mirroring `lib/runtime/`. Shape:

- `public ReaderId ReaderId { get; }` — get-only auto-property (init via ctor), mirroring Dart `final`; non-nullable (value-type `int` alias).
- `public Hanger Hanger { get; }` — get-only auto-property (init via ctor), non-nullable reference to a `Hanger` instance.
- A single positional constructor `public SuspensionNote(ReaderId readerId, Hanger hanger) { ReaderId = readerId; Hanger = hanger; }` — explicit-assignment body (no field-formal-parameter shorthand in C#).
- NO `==`/`hashCode`/`Equals`/`GetHashCode` override (default reference equality).
- NO `ToString` override; NO mutator methods; NO XML doc-comment (Dart source has none).

PascalCase identifier rename per .NET capitalisation conventions: field `readerId` → property `ReaderId`; field `hanger` → property `Hanger`. Class name `SuspensionNote` already PascalCase, preserved verbatim. Parameter names stay camelCase (`readerId`, `hanger`).

**Rejection rationale** (load-bearing, from convspec):
- NOT `record` / `record class`: would inject value-equality on every field. The runtime stores `SuspensionNote` references in per-(reader, goal) suspension structures and asks "is this *the* note I attached?" — reference identity. Two notes with coincidentally identical (`readerId`, `Hanger`-reference) are distinct attachments (sequential suspend calls reusing the same Hanger). Dart source has NO `==` override and the .NET port MUST preserve that.
- NOT `struct` / `record struct`: would copy-on-assignment, losing the outer attachment-handle identity the runtime uses in its lookup/removal protocol. (The inner `Hanger`-reference mutation channel would still propagate `Armed` flips through a struct copy, but the OUTER identity is also load-bearing.)
- The Dart `const` constructor qualifier is correctly elided — .NET reference classes have NO compile-time-canonicalisation mechanism; the C# port emits a plain constructor and each call site allocates a new instance. No `readonly record struct` (the value-type literal counterpart) because identity-equality + aggregates-a-mutable-reference forbids a value type.
- NOT a primary constructor (C# 12): consistency with `hanger.dart.md`, `machine_state.dart.md`, `cells.dart.md` — explicit-body ctor form is the runtime-port idiom.
- NOT C# 11 `required` properties: would force object-initialiser call sites, inconsistent with the rest of the runtime port.
- Init-only-vs-set: both fields are Dart `final` → C# `{ get; }` (init via ctor, conservative form). C# 9 `{ get; init; }` is acceptable but unnecessary; get-only matches the `final` invariant and avoids exposing a `with`-expression mutation surface the Dart side does not have.
- No defensive copy / no clone-on-construction — the Dart source stores the reference verbatim; the .NET port does the same. The textbook "immutable aggregate of a mutable reference" shape is preserved exactly.

### Construct 4 → `dart.positional-ctor-field-formal-shorthand-no-body`

**Dart**: `const SuspensionNote(this.readerId, this.hanger);`

**C#/.NET**: `public SuspensionNote(ReaderId readerId, Hanger hanger) { ReaderId = readerId; Hanger = hanger; }` — positional parameters in source order (`readerId, hanger`); explicit assignment body (no field-formal shorthand in C#); no defaults (Dart positional without `=` default == required at the call site). Dart `const` qualifier elided (per Construct 3 nuance). Reused verbatim from the project's canonical positional-ctor-with-explicit-assignment idiom (`cells.dart.md` / `WriterCell` + `ReaderCell` ctors).

## 3. Decomposed Task Units

- **T1**: Emit `using <namespace-of-converted-machine_state.cs>;` in `lib/runtime/suspend.cs` (or elide if same namespace; depgraph/namespace stage decides). — done
- **T2**: Emit `using <namespace-of-converted-hanger.cs>;` in `lib/runtime/suspend.cs` (or elide if same namespace; depgraph/namespace stage decides). — done
- **T3**: Declare `public class SuspensionNote` (reference type; NOT record/record class/struct/record struct) in the namespace mirroring `lib/runtime/`. — done
- **T4**: Declare `public ReaderId ReaderId { get; }` get-only auto-property on `SuspensionNote`. — done
- **T5**: Declare `public Hanger Hanger { get; }` get-only auto-property on `SuspensionNote`. — done
- **T6**: Declare `public SuspensionNote(ReaderId readerId, Hanger hanger)` constructor with explicit-assignment body `{ ReaderId = readerId; Hanger = hanger; }`. — done
- **T7**: Confirm absence of `==`/`hashCode`/`Equals`/`GetHashCode`/`ToString` overrides, mutator methods, async surface, generics, mixins, sealed/abstract modifiers, trail/choice-point fields, locks/atomics. — done

## 4. Research Findings

none required — every construct is verbatim-derivable from the ratified convspec at `.codeconv/conversion-specs/lib/runtime/suspend.dart.md` (which itself cites authoritative Dart and Microsoft Learn references and reuses idioms `rf-dart-relative-import-to-csharp-namespace-using`, `rf-dart-final-field-class-to-csharp-getonly-class`, `rf-dart-positional-ctor-with-this-shorthand-to-csharp-positional-ctor-with-explicit-assignment` from sibling convspecs `hanger.dart.md`, `cells.dart.md`, `machine_state.dart.md`).

## 5. Consistency Pass

- Construct 1 (`import 'machine_state.dart';` → `using <namespace>;`): fixed — derived from convspec construct `dart.import_directive.relative-same-package.machine_state` (research finding `rf-dart-relative-import-to-csharp-namespace-using`).
- Construct 2 (`import 'hanger.dart';` → `using <namespace>;`): fixed — derived from convspec construct `dart.import_directive.relative-same-package.hanger` (research finding `rf-dart-relative-import-to-csharp-namespace-using`).
- Construct 3 (`class SuspensionNote` → `public class SuspensionNote` reference type with get-only properties): fixed — derived from convspec construct `dart.class.immutable-aggregate-aggregating-mutable-reference-two-final-fields-positional-const-ctor-identity-equality` (research finding `rf-dart-final-field-class-to-csharp-getonly-class`).
- Construct 4 (`const SuspensionNote(this.readerId, this.hanger);` → `public SuspensionNote(ReaderId readerId, Hanger hanger) { ReaderId = readerId; Hanger = hanger; }`): fixed — derived from convspec construct `dart.positional-ctor-field-formal-shorthand-no-body` (research finding `rf-dart-positional-ctor-with-this-shorthand-to-csharp-positional-ctor-with-explicit-assignment`).
- Identifier-casing convention (Dart camelCase fields → C# PascalCase properties; parameter names stay camelCase): fixed — derived from convspec "Notes" section citing the .NET capitalisation guideline.
- Const-constructor elision (Dart `const` not portable to .NET reference classes): fixed — derived from convspec Construct 3 nuance + Construct 4 nuance.
- Aggregation-of-mutable-reference contract (outer immutable, inner Hanger mutable; no defensive copy): fixed — derived from convspec Construct 3 nuance + "Notes" Aggregation section.
- Reference-identity equality contract (no `==`/`hashCode` override on either side): fixed — derived from convspec Construct 3 nuance + "Notes" Equality contract section.
- Absent constructs (async/Stream/Future/Completer/Task/Channel, mixin/sealed/abstract/generic, trail/choice-points, locks/atomics): fixed — derived from convspec "Notes" sections (correctly not asserted; .NET port MUST NOT introduce these here).

## 6. Escalations

None.
