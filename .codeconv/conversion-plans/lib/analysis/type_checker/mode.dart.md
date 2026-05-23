---
path: lib/analysis/type_checker/mode.dart
cycle_group_id: 5
scc_siblings: []
generated_at: 2026-05-21T14:24:58Z
source_sha256: 48ca1f3517f5fd668631dff7c4b48b31567276ca330644b7c24892427aaa8e78
schema_version: 1
---

# Conversion Plan: lib/analysis/type_checker/mode.dart

## 1. Source Analysis

Grounded in direct inspection of `glp_runtime_net/lib/analysis/type_checker/mode.dart` (69 lines, sha256 verified against tombstone). The file is a small, self-contained leaf module of the moded type-checker subsystem (tombstone `dependencies: []`, `topo_level: 0`, `cycle_group_id: 5`). It is consumed by five caller modules (`moded_head.dart`, `moded_term.dart`, `program_dfa.dart`, `well_typed_clause.dart`, `well_typed_term.dart`) plus three test files.

Concrete constructs in source order:

1. **File-level doc comment (lines 1–5)**: `//`-style header explaining the module's purpose (mode system for moded type checking; modes represent data flow direction: input = caller writes / callee reads; output = callee writes / caller reads).
2. **Enhanced enum `Mode` (lines 7–50)**:
   - Two enum instances declared in this order: `output` (line 17, terminated with `,`), `input` (line 18, terminated with `;`).
   - Doc-comment explicitly states `output` is "default, no ? marker" — declaration order is load-bearing for the Dart→C# default-value mapping (the C# default `(Mode)0` must be `Output`).
   - Two static-const aliases bound to enum members: `static const consume = Mode.input;` (line 21) and `static const produce = Mode.output;` (line 24).
   - Instance getter `Mode get dual` (lines 29–36): exhaustive `switch (this)` with one arm per enum member and no `default`. Body returns the opposite member.
   - Instance getter `Mode get flip => dual;` (line 39): expression-bodied forwarder to `dual`.
   - Overridden `String toString()` (lines 41–49): exhaustive `switch (this)` returning lowercase string literals `'output'` / `'input'` (i.e. NOT the Dart `'Mode.output'` default — the override exists specifically to suppress the prefix and lowercase the form).
3. **Top-level function `Mode combineMode(Mode parent, Mode embedded)` (lines 62–68)**: pure function, single `if/else` on value-type `==` equality of two `Mode` values; returns `Mode.output` if equal, `Mode.input` if not. Doc-comment (lines 52–61) records the four-cell involution table and notes "This is XOR on the boolean representation (output=false, input=true)".

The module has zero imports, zero runtime state, zero async, zero collection types, zero generics. All identifiers are value-type-pure. No reference-identity or boxing hazards exist.

## 2. Dart → C#/.NET Conversion Plan

This section mirrors the ratified convspec verbatim in decision content; each convspec construct produces one C# emission unit, and the convspec's `conversion_units:` list is the authoritative output shape. Decisions below are NOT renegotiated.

**Target file**: `lib/analysis/type_checker/mode.cs` (per convspec `target_code_unit` and tombstone `target_path`).

### 2.1 File-level doc comment → C# `//` comments

The Dart `//`-style file header carries forward as identical `//`-style comments at the top of `mode.cs`. Trivial, no research required (convspec §"Trivial / non-construct elements").

### 2.2 Dart enhanced enum `Mode` → C# `enum Mode` + sibling static helpers

Convspec construct `dart.enhanced_enum.with_static_const_aliases_getters_tostring` (research finding `dart-enhanced-enum-to-csharp-enum-plus-extensions`). The enum *values* map 1:1 to a plain C# `enum Mode`; everything Dart attached to the enum body relocates because C# enums are integral value types that **cannot** declare methods, getters, static members, or override `ToString` (Microsoft Learn, authoritative: *"You can't define a method inside the definition of an enumeration type. To add functionality to an enumeration type, create an extension member."*).

Emitted units (verbatim from convspec `conversion_units`):

- **`enum Mode { Output, Input }`** — value type; `Output` is the first member so the C# default `(Mode)0` equals `Output`, preserving the Dart source's documented "(default, no ? marker)" semantics. No explicit underlying-value overrides. Identifier casing: PascalCase per .NET convention (Dart `output`/`input` → C# `Output`/`Input`).
- **`static class ModeAliases`** — holds the alias bindings `public static readonly Mode Consume = Mode.Input;` and `public static readonly Mode Produce = Mode.Output;` (or equivalent `public const Mode` declarations where the C# const-expression rules permit). Per convspec nuance: these are NOT modelled as additional enum members because doing so would create duplicate-value members and pollute `Enum.GetNames`/round-trip.
- **`static class ModeExtensions`** — extension members on `this Mode`:
  - `Mode Dual(this Mode value)` — implements the Dart `dual` getter as an extension method (see §2.3 for body).
  - `Mode Flip(this Mode value) => value.Dual();` — expression-bodied forwarder to `Dual`, mirroring Dart `Mode get flip => dual;`.
  - `string AsModeString(this Mode value)` — replaces Dart `toString()` override. Convspec is explicit: do NOT attempt to override `System.Enum.ToString` (extension methods cannot override virtuals, and the Dart override returns lowercase `'output'`/`'input'` which differs from the C# default `ToString` member-name output `"Output"`/`"Input"`). The body is an explicit-mapped helper (see §2.3); call sites previously relying on Dart `toString()` are redirected to this helper at scaffold time.
- **`static class ModeOps`** — host for the top-level function (§2.4). Convspec names this class `ModeOps` precisely because `Mode` is occupied by the enum type and would collide.

### 2.3 Dart exhaustive enum switches in `dual` and `toString` → C# switch expressions with throwing discard arm

Convspec construct `dart.enum.exhaustive_switch_no_default` (research finding `dart-exhaustive-enum-switch-to-csharp-switch-expression`). Dart's analyzer enforces exhaustiveness over the closed enum, making `dual` and `toString` total functions. C# does **not** enforce enum-switch exhaustiveness, and an out-of-range cast such as `(Mode)99` is representable (Microsoft Learn). Convspec therefore mandates an explicit unreachable/throw arm to preserve totality.

Emission (inside `ModeExtensions`):

```csharp
public static Mode Dual(this Mode value) => value switch
{
    Mode.Output => Mode.Input,
    Mode.Input  => Mode.Output,
    _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
};

public static string AsModeString(this Mode value) => value switch
{
    Mode.Output => "output",
    Mode.Input  => "input",
    _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
};
```

Choice of `ArgumentOutOfRangeException` vs `UnreachableException`: convspec offers either; project convention selection is deferred to scaffolding without renegotiating the decision (both satisfy the "throws loudly" requirement). The string literals `"output"`/`"input"` are lowercase, exactly mirroring the Dart override.

### 2.4 Dart top-level `combineMode` function → C# `public static` method on `ModeOps`

Convspec construct `dart.top_level_function.pure` (research finding `dart-top-level-function-to-csharp-static-method`). C# has no library-level functions; the function moves to a static host. Convspec mandates the host be a distinctly-named static class (`ModeOps`) because the natural name `Mode` collides with the enum type.

Emission:

```csharp
public static class ModeOps
{
    public static Mode CombineMode(Mode parent, Mode embedded)
        => parent == embedded ? Mode.Output : Mode.Input;
}
```

Semantics: enum `==` in C# is integral comparison of value types — identical to Dart enum `==` (Microsoft Learn confirms). The documented XOR / involution property carries over verbatim. The doc-comment block (lines 52–61) emits as a C# `///` XML-doc comment on `CombineMode` (the four-cell table and XOR note preserve as XML-doc body).

### 2.5 Naming and call-site rewrites

- Dart `Mode.output` / `Mode.input` → C# `Mode.Output` / `Mode.Input`.
- Dart `Mode.consume` / `Mode.produce` → C# `ModeAliases.Consume` / `ModeAliases.Produce` (relocated off the enum per §2.2).
- Dart `someMode.dual` / `someMode.flip` → C# `someMode.Dual()` / `someMode.Flip()` (extension method call syntax requires parentheses).
- Dart `someMode.toString()` → C# `someMode.AsModeString()` (explicit helper, NOT `someMode.ToString()`).
- Dart top-level `combineMode(a, b)` → C# `ModeOps.CombineMode(a, b)`.

These rewrites are mechanical and apply uniformly across the five caller modules and three test modules listed in the tombstone; the per-caller adjustments belong to those callers' own plans, not this one.

## 3. Decomposed Task Units

- **T1**: Emit `mode.cs` file scaffold with namespace and translated file-header comment. *Done when*: file exists at `lib/analysis/type_checker/mode.cs` with the header comment and an empty namespace block matching project convention.
- **T2**: Emit `public enum Mode { Output, Input }` with `Output` first. *Done when*: enum compiles and `(Mode)0 == Mode.Output` holds in a smoke check.
- **T3**: Emit `public static class ModeAliases` with `Consume = Mode.Input` and `Produce = Mode.Output`. *Done when*: `ModeAliases.Consume == Mode.Input && ModeAliases.Produce == Mode.Output` is true at compile/runtime.
- **T4**: Emit `public static class ModeExtensions` containing `Dual`, `Flip`, and `AsModeString` extension methods using switch expressions with a throwing discard arm. *Done when*: all three extensions compile, `Mode.Output.Dual() == Mode.Input`, `Mode.Input.Dual() == Mode.Output`, `Mode.Output.Flip() == Mode.Input`, `Mode.Output.AsModeString() == "output"`, `Mode.Input.AsModeString() == "input"`, and an out-of-range cast `((Mode)99).Dual()` throws.
- **T5**: Emit `public static class ModeOps` with `public static Mode CombineMode(Mode parent, Mode embedded) => parent == embedded ? Mode.Output : Mode.Input;` plus a translated XML-doc block carrying the four-cell involution table. *Done when*: the four involution-table cases evaluate to the documented outputs and the XML-doc renders in IDE tooling.
- **T6**: Translate the file-header doc comment (lines 1–5) and the `combineMode` doc-comment block (lines 52–61) into C# `//` / `///` comments respectively, preserving the input/output / consume/produce mapping table verbatim. *Done when*: comment text in `mode.cs` is a faithful, paraphrase-free port of the Dart prose.

## 4. Research Findings

None required — the convspec already cites two authoritative WebFetch sources (Dart official docs `https://dart.dev/language/enums` for enhanced-enum semantics, and Microsoft Learn `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/enum` for C# enum constraints and value-type equality). Both findings are verbatim-quoted in the convspec's "Rationale & Research Provenance" section and are sufficient to ground every decision in §2 without additional web research. Web research is forbidden by this plan's contract; none is needed.

## 5. Consistency Pass

Cross-checking §2 / §3 / §4 / convspec / project conventions:

- **§2.2 enum member order vs convspec**: convspec says "keep two members so default `(Mode)0` == Output, matching Dart declaration order". §2.2 emits `enum Mode { Output, Input }` with `Output` first. **Consistent — derived from convspec construct `dart.enhanced_enum.with_static_const_aliases_getters_tostring`.**
- **§2.2 alias relocation vs convspec**: convspec mandates aliases on a static helper class (NOT enum members). §2.2 emits `ModeAliases.Consume`/`Produce` on a `static class ModeAliases`. **Consistent — derived from convspec nuance "adding them as enum members would create duplicate-value members and pollute Enum.GetNames/round-trip".**
- **§2.3 switch discard arm vs convspec**: convspec mandates explicit throwing discard to preserve totality. §2.3 emits `_ => throw new ArgumentOutOfRangeException(...)`. **Consistent — derived from convspec construct `dart.enum.exhaustive_switch_no_default`.**
- **§2.3 lowercase string output vs Dart source**: Dart `toString()` returns lowercase `'output'`/`'input'` (source lines 45, 47); §2.3 `AsModeString` emits identical lowercase literals. **Consistent — derived from direct source inspection (lines 41–49).**
- **§2.4 host class name vs convspec**: convspec mandates a distinctly-named host class because `Mode` is occupied. §2.4 uses `ModeOps`. **Consistent — derived from convspec construct `dart.top_level_function.pure` nuance "the natural host name `Mode` is occupied by the enum; the spec records the host must be a distinctly-named static class".**
- **§2.5 call-site rewrites vs convspec**: convspec specifies `toString()` call sites "are redirected to the helper". §2.5 records `someMode.toString()` → `someMode.AsModeString()`. **Consistent — derived from convspec construct `dart.enhanced_enum.with_static_const_aliases_getters_tostring` target decision.**
- **§3 task units vs §2 emission units**: T2/T3/T4/T5 correspond 1:1 to convspec `conversion_units` entries (enum Mode, ModeAliases, ModeExtensions, ModeOps). T1 and T6 cover scaffolding/comments not enumerated as conversion_units but required to produce a complete file. **Consistent — no convspec unit is missing a task and no task introduces an unspec'd emission unit.**
- **Tombstone `cycle_group_id: 5` and `scc_siblings: []`**: this plan treats the file as a singleton (no §7). **Consistent — matches the orchestrator's `scc_siblings: []` directive.**
- **§4 research bound**: no new findings introduced; both convspec findings (`dart-enhanced-enum-to-csharp-enum-plus-extensions`, `dart-exhaustive-enum-switch-to-csharp-switch-expression`, `dart-top-level-function-to-csharp-static-method`) are cited and unchanged. **Consistent.**

No gaps requiring escalation. No design decisions introduced beyond what convspec ratified. No scope growth (the plan covers exactly the file's 69 lines and the four convspec constructs).

## 6. Escalations

None.
