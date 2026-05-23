---
path: lib/runtime/hanger.dart
cycle_group_id: 106
scc_siblings: []
generated_at: 2026-05-21T14:52:00Z
source_sha256: 162457ab2f6db96de5e7e7beb5ae3acd6ed5dea548ea9ef7106b32fa0522403f
schema_version: 1
---

# Conversion Plan: lib/runtime/hanger.dart

## 1. Source Analysis

Verbatim source (11 lines, sha256 `162457ab2f6db96de5e7e7beb5ae3acd6ed5dea548ea9ef7106b32fa0522403f`):

```dart
import 'machine_state.dart';

/// Hanger: ensures single reactivation when a goal suspended on multiple readers.
class Hanger {
  final GoalId goalId;
  final Pc kappa;     // restart at clause selection
  bool armed;         // true at creation; first wake sets to false

  Hanger({required this.goalId, required this.kappa, this.armed = true});
}
```

Inventory of source constructs (mirrors the four convspec entries):

- **L1 — relative same-package import**: `import 'machine_state.dart';` brings the typedef aliases `GoalId` (= `int`) and `Pc` (= `int`) defined in the sibling `lib/runtime/machine_state.dart` into scope. No `show`/`hide` clause; only the two aliases are referenced.
- **L3 / L6 / L7 — doc-comment + trailing comments**: one `///` doc line above the class (`Hanger: ensures single reactivation when a goal suspended on multiple readers.`); two trailing `//`-line comments inside the class body (`// restart at clause selection` on the `kappa` field, `// true at creation; first wake sets to false` on the `armed` field). The author deliberately chose `///` for the class summary and `//` for the per-field implementation notes.
- **L4–L10 — class declaration**: `class Hanger { ... }` with three instance fields and one constructor:
  - `final GoalId goalId;` — non-nullable, `final` (write-exactly-once), value-type alias of `int`.
  - `final Pc kappa;` — non-nullable, `final`, value-type alias of `int`.
  - `bool armed;` — non-nullable, mutable; default `true` supplied via the constructor parameter.
  - No `==` / `hashCode` / `toString` override → default reference identity.
  - No mutator methods; `armed` is mutated by direct field assignment from the wake path of the runtime (per the doc-comment lifecycle "first wake sets to false").
- **L9 — named-only constructor with two `required` and one defaulted bool param**: `Hanger({required this.goalId, required this.kappa, this.armed = true});`. Uses Dart field-formal-parameter shorthand (`this.X`); no initialiser list, no body, no `assert`. Semantically the call site MUST supply `goalId` and `kappa` and MAY omit `armed` (defaults to `true`).

Cross-file context (relevant only for the `using` directive resolution, not introduced by this plan): the sibling `lib/runtime/machine_state.dart` is the source of the `GoalId` / `Pc` aliases; its converted `.cs` artefact will host `global using GoalId = int;` and `global using Pc = int;` directives per the machine_state convspec.

## 2. Dart → C#/.NET Conversion Plan

The four constructs in the ratified convspec map to the following target decisions (mirrored verbatim from `.codeconv/conversion-specs/lib/runtime/hanger.dart.md`):

### 2.1 `dart.import_directive.relative-same-package.machine_state` → C# `using` of the converted sibling's namespace

The Dart relative-import `import 'machine_state.dart';` becomes a single C# `using <namespace>;` directive in `lib/runtime/hanger.cs`, where `<namespace>` is the .NET namespace of the converted `lib/runtime/machine_state.cs`. The depgraph/namespace stage owns the exact namespace name; this plan does not pin it. The two consumed aliases `GoalId` and `Pc` reach the rest of `hanger.cs` as plain `int` once the `global using GoalId = int;` and `global using Pc = int;` declared by the converted `machine_state.cs` are in scope.

Forbidden alternatives: a textual relative-path `using` such as `using ./machine_state.cs` (invalid C#); per-symbol narrowing (no `show`/`hide` counterpart in C#).

### 2.2 `dart.docblock_triple_slash` → C# XML-doc on the class + verbatim `//` trailing comments on properties

- Class `///` summary → C# XML-doc: `/// <summary>Hanger: ensures single reactivation when a goal suspended on multiple readers.</summary>`.
- Two `//`-line trailing comments on `kappa` and `armed` fields → preserved verbatim as trailing `//`-line comments on the converted `Kappa` and `Armed` property declarations (NOT promoted to `///` XML-doc — they are implementation notes, not API documentation, mirroring the Dart-side choice).

### 2.3 `dart.class.mutable-state-container-identity-equality-three-fields-named-only-ctor-two-required-one-defaulted-bool` → reference-type `class Hanger`

The target is a .NET reference-type `class` named `Hanger` — NOT `record`, NOT `record class`, NOT `struct`, NOT `record struct`. Members:

- `public GoalId GoalId { get; }` — get-only (init via ctor), mirroring Dart `final`.
- `public Pc Kappa { get; }` — get-only (init via ctor), mirroring Dart `final`.
- `public bool Armed { get; set; } = true;` — mutable (settable), with property initialiser preserving the Dart `= true` default.

Load-bearing rationale (mirrored from the convspec, §`rf-dart-mutable-state-class-identity-equality-to-csharp-class`):

- **Reference identity is the equality contract.** The Dart class has no `==`/`hashCode` override. The runtime attaches one `Hanger` per (reader, goal) reader-attachment to a suspended goal and races them on wake; the wake path flips `Armed` from `true` to `false` and that transition MUST be observable from every reference to the same `Hanger`. Two `Hanger` instances with coincidentally identical `goalId`/`kappa`/`armed` (e.g. two distinct attachments constructed close in time) are NOT the same hanger.
- **`record`/`record class` is rejected**: would inject value-equality on every field — silently makes the two distinct attachments above compare equal, corrupting any hanger-lookup table keyed by reference.
- **`struct`/`record struct` is rejected**: would copy on assignment — the wake path's `hanger.Armed = false` would mutate a copy, not the canonical scheduler-held state, breaking the single-reactivation invariant.

Identifier casing per .NET capitalisation conventions: Dart `camelCase` instance field names (`goalId`, `kappa`, `armed`) become `PascalCase` public properties (`GoalId`, `Kappa`, `Armed`); constructor parameter names stay `camelCase`. The class name `Hanger` is already PascalCase and is preserved verbatim.

### 2.4 `dart.named-required-ctor-with-default-bool` → positional C# constructor with named-argument call-site convention

The Dart `Hanger({required this.goalId, required this.kappa, this.armed = true});` becomes:

```
public Hanger(GoalId goalId, Pc kappa, bool armed = true)
{
    GoalId = goalId;
    Kappa  = kappa;
    Armed  = armed;
}
```

- Positional parameters (Dart 'required named' has no direct C# counterpart; a positional parameter without a default is required by the C# compiler — the closest faithful counterpart).
- Default `armed = true` preserved verbatim as a C# parameter default (a compile-time-constant `bool` literal, permitted by C# optional-argument rules).
- Body: explicit assignment of each constructor parameter to its same-named backing property.
- Call sites MAY use C# named-argument syntax (`new Hanger(goalId: g, kappa: k)`) to mirror the Dart named-call readability.
- The C# 11 `required` property modifier is REJECTED here per the convspec rationale: it would force callers to use object-initialiser syntax, which the rest of the runtime port does not adopt — the constructor form is the canonical idiom across the runtime port (cf. `GoalState` ctor in `machine_state.dart.md`).

### 2.5 Synthesised target shape

The complete converted `lib/runtime/hanger.cs` (modulo namespace name decided downstream):

```
using <namespace-of-converted-machine_state.cs>;

/// <summary>Hanger: ensures single reactivation when a goal suspended on multiple readers.</summary>
public class Hanger
{
    public GoalId GoalId { get; }
    public Pc Kappa { get; }     // restart at clause selection
    public bool Armed { get; set; } = true;     // true at creation; first wake sets to false

    public Hanger(GoalId goalId, Pc kappa, bool armed = true)
    {
        GoalId = goalId;
        Kappa  = kappa;
        Armed  = armed;
    }
}
```

## 3. Decomposed Task Units

- T1: Emit `using <namespace-of-converted-machine_state.cs>;` at the top of `lib/runtime/hanger.cs` (namespace name resolved by depgraph/namespace stage). done
- T2: Emit XML-doc `/// <summary>Hanger: ensures single reactivation when a goal suspended on multiple readers.</summary>` immediately above the `Hanger` class declaration. done
- T3: Declare `public class Hanger` as a plain reference type (no `record`, no `struct`, no `record class`, no `record struct`, no `sealed`, no `abstract`, no type parameters). done
- T4: Emit `public GoalId GoalId { get; }` as a get-only auto-property. done
- T5: Emit `public Pc Kappa { get; }` as a get-only auto-property with trailing `//`-line comment `// restart at clause selection` preserved verbatim. done
- T6: Emit `public bool Armed { get; set; } = true;` as a mutable auto-property with property initialiser, plus trailing `//`-line comment `// true at creation; first wake sets to false` preserved verbatim. done
- T7: Emit the constructor `public Hanger(GoalId goalId, Pc kappa, bool armed = true)` with positional parameters, the `armed = true` default preserved verbatim, and a body that assigns each parameter to its same-named backing property. done
- T8: Verify no override of `Equals` / `GetHashCode` / `ToString` is emitted (reference-identity contract preserved). done
- T9: Verify identifier casing — field names map `goalId→GoalId`, `kappa→Kappa`, `armed→Armed`; parameter names stay `camelCase`; class name `Hanger` preserved verbatim. done

## 4. Research Findings

none required — every construct in this file is verbatim-derivable from the ratified convspec (`.codeconv/conversion-specs/lib/runtime/hanger.dart.md`). The two non-trivial constructs reuse the project-canonical idioms `rf-dart-mutable-state-class-identity-equality-to-csharp-class` and `rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults` already established in `.codeconv/conversion-specs/lib/runtime/machine_state.dart.md` (GoalState) and reused in `.codeconv/conversion-specs/lib/runtime/fairness.dart.md` (relative-import idiom). The convspec itself records `escalations: []`.

## 5. Consistency Pass

- §2.1 fixed — derived from convspec construct `dart.import_directive.relative-same-package.machine_state` (target_decision verbatim) and the project idiom `rf-dart-relative-import-to-csharp-namespace-using` (carry-over from `fairness.dart.md`).
- §2.2 fixed — derived from convspec construct `dart.docblock_triple_slash` (`trivial: true`) and the same doc-comment idiom applied in `abandon.dart.md` / `machine_state.dart.md`.
- §2.3 fixed — derived from convspec construct `dart.class.mutable-state-container-identity-equality-three-fields-named-only-ctor-two-required-one-defaulted-bool` (target_decision verbatim) and the project idiom `rf-dart-mutable-state-class-identity-equality-to-csharp-class` (carry-over from `machine_state.dart.md` / GoalState).
- §2.4 fixed — derived from convspec construct `dart.named-required-ctor-with-default-bool` (target_decision verbatim) and the project idiom `rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults` (carry-over from `machine_state.dart.md` / GoalState ctor).
- §2.5 fixed — synthesised mechanically from §2.1–§2.4 with no novel construct introduced.
- §3 fixed — each T_n is a one-line execution of a target_decision already ratified in §2.
- Cross-construct consistency: PascalCase rename of `goalId`/`kappa`/`armed` to property names, and `camelCase` retention for the constructor parameter names, are mutually consistent (no parameter-vs-property name collision); the constructor body's `GoalId = goalId; Kappa = kappa; Armed = armed;` resolves unambiguously because C# parameter scope shadows the enclosing property names on the right-hand side of each assignment.

## 6. Escalations

None.
