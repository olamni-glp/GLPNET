---
path: lib/glp_runtime.dart
cycle_group_id: 62
scc_siblings: []
generated_at: 2026-05-21T14:37:11Z
source_sha256: 9812a323185f6a0b680576687c6f33269f32ac398ca24656335235adc630a329
schema_version: 1
---

# Conversion Plan: lib/glp_runtime.dart

## 1. Source Analysis

Source file (`glp_runtime_net/lib/glp_runtime.dart`) is exactly 3 non-blank
lines (4 lines including trailing newline):

```dart
int calculate() {
  return 6 * 7;
}
```

Inspection (verbatim, byte-faithful to source_sha256
`9812a323185f6a0b680576687c6f33269f32ac398ca24656335235adc630a329`):

- One top-level (library-level) function `calculate`.
- Visibility: no leading underscore → Dart library-public.
- Return type: `int` (Dart native 64-bit signed integer on native
  runtimes).
- Parameters: zero.
- Body: a single `return` of a compile-time-constant integer
  arithmetic expression `6 * 7`.
- No class, no fields, no captured state, no side effects, no async,
  no Future/Stream, no isolates, no `late`, no nullable annotations,
  no generics, no extensions, no mixins, no `sealed`, no exhaustive
  switch, no bitwise/shift ops, no `toString`, no constructor.
- Imports: none.
- Dependencies (per tombstone): `[]`.
- Callers (per tombstone): `test/glp_runtime_test.dart`, which
  imports `package:glp_runtime/glp_runtime.dart` and asserts
  `expect(calculate(), 42)`. This is the `dart create -t package`
  default scaffold stub.

Two non-trivial constructs are exercised:

1. `dart.top_level_function.pure` — a pure library-public top-level
   function (no equivalent kind in C#; methods must live in a type).
2. `dart.int.literal_arithmetic.compile_time_constant_expression` —
   the first file in the converted corpus that exercises actual
   integer arithmetic, surfacing the int-width / overflow / checked-
   context nuance that prior files (opcodes / token) explicitly
   waived as "NO arithmetic in this file".

## 2. Dart → C#/.NET Conversion Plan

Mirrored verbatim from the ratified convspec
(`.codeconv/conversion-specs/lib/glp_runtime.dart.md`); no decision
changed.

### Construct 1 — `dart.top_level_function.pure` (`int calculate() { ... }`)

- **Source form**: `int calculate() { return 6 * 7; }`
- **Target decision**: C# has no top-level functions in this
  codebase convention; emit as a `public static long Calculate()` on
  a **distinctly-named** static host class. Spec mandate:
  `public static class GlpRuntimeRoot` (or codegen-recorded
  equivalent; the load-bearing requirement is that the host name
  MUST NOT collide with the converted package's root namespace
  `GlpRuntime`). Method PascalCased to `Calculate` per .NET naming
  conventions. Return type `long` (System.Int64) per
  `rf-dart-int-to-csharp-long-width`. Function is pure.
- **research_finding_id**: `dart-top-level-function-to-csharp-static-method`
  (precedent in this corpus:
  `.codeconv/conversion-specs/lib/analysis/type_checker/mode.dart.md`).
- **Nuance** (verbatim from convspec): Dart allows library-level
  (top-level) functions; C# requires every method to live in a type.
  Naming-collision nuance is explicitly addressed — the natural host
  name `GlpRuntime` collides with the converted package's namespace,
  so the spec records the host MUST be a distinctly-named static
  class. Visibility: Dart top-level identifiers without a leading
  underscore are library-public → C# `public`. No captured library
  state → no reference/identity hazard. Zero-arg static call from C#
  preserves the zero-arg call shape from Dart.

Mapping (Dart → C#):

| Dart | → | C#/.NET |
|---|---|---|
| top-level function declaration | → | `public static` method on a distinctly-named static host class (`public static class GlpRuntimeRoot` per spec mandate, NOT `GlpRuntime` to avoid namespace shadowing) |
| `calculate` (camelCase) | → | `Calculate` (PascalCase) |
| `int` return type | → | `long` (`System.Int64`) per `rf-dart-int-to-csharp-long-width` |
| zero-arg call site `calculate()` | → | zero-arg call site `GlpRuntimeRoot.Calculate()` |
| library-public visibility | → | `public` |

### Construct 2 — `dart.int.literal_arithmetic.compile_time_constant_expression` (`return 6 * 7;`)

- **Source form**: `return 6 * 7;`
- **Target decision**: Emit as `return 6L * 7L;`. Both literals MUST
  carry the `L` suffix to type them as `System.Int64`, matching the
  function's `long` return type (per
  `rf-dart-int-to-csharp-long-width`). NO `checked { }` block: the
  result is provably bounded (`42 << long.MaxValue`), so the default
  C# unchecked context is observationally indistinguishable from a
  checked one here. Source form `6 * 7` is preserved (NOT folded to
  `42`) so a spec-diff reviewer can see the original literal pair;
  the resulting Int64 value is `42` either way.
- **research_finding_id**: `rf-dart-int-literal-arithmetic-to-csharp-long-literal-arithmetic`.
- **Nuance** (verbatim from convspec): Dart `int` on native is
  64-bit signed two's-complement; `*` on two Dart int literals is
  exact integer multiplication with two's-complement wrap on
  overflow (native; web/JS is IEEE-754 — not the target here). C#
  `long * long` is exact two's-complement multiplication; unchecked
  context wraps, checked context throws `OverflowException`. For the
  provably-bounded `6 * 7 = 42`, both contexts yield 42 — semantically
  faithful under default unchecked. The `L` suffix is load-bearing:
  an unsuffixed C# integer literal is `int` (Int32) and `int * int`
  would silently narrow the source's 64-bit type. Returning a `long`
  (value type) is a stack-allocated copy — no boxing, no identity,
  matches Dart's value-semantic integer return.

Mapping (Dart → C#):

| Dart | → | C#/.NET |
|---|---|---|
| `int` literal `6` | → | `long` literal `6L` (Int64-typed) |
| `int` literal `7` | → | `long` literal `7L` (Int64-typed) |
| `*` on two `int` literals | → | `*` on two `long` literals in default unchecked context |
| `return <int>` from `int` body | → | `return <long>` from `long` body |
| compile-time `6 * 7 = 42` | → | compile-time `6L * 7L = 42L` (source form preserved, NOT folded) |

### Top-of-file structural mapping

- **Namespace**: `namespace GlpRuntime;` (per the converted package's
  root namespace convention — derived from the Dart package name
  `glp_runtime`, PascalCased).
- **Host type**: `public static class GlpRuntimeRoot` (or codegen-
  recorded equivalent that does NOT collide with the namespace name
  `GlpRuntime`).
- **Imports / `using`**: none required (no Dart imports, no .NET
  types beyond the primitive `long`).
- **File path**: target tombstone records `target_path:
  lib/glp_runtime.cs`.

## 3. Decomposed Task Units

- **T1 — Emit host file and namespace.** Create
  `lib/glp_runtime.cs` with `namespace GlpRuntime;` and the
  distinctly-named static host class declaration
  `public static class GlpRuntimeRoot`.
  - *Done when*: the file exists at `lib/glp_runtime.cs` with the
    namespace declaration and an empty `public static class
    GlpRuntimeRoot { }` body that compiles standalone.
- **T2 — Emit the `Calculate` method skeleton.** Inside
  `GlpRuntimeRoot`, declare `public static long Calculate()` with
  an empty body placeholder.
  - *Done when*: the host class contains a `public static long
    Calculate()` member with the correct signature (visibility,
    static, return-type `long`, zero parameters, PascalCase name).
- **T3 — Emit the arithmetic return.** Replace the body of
  `Calculate` with `return 6L * 7L;` (source-form preserved, `L`
  suffix on each literal, no `checked` block).
  - *Done when*: the method body is exactly `return 6L * 7L;` and
    the file compiles under the default (unchecked) C# arithmetic
    context.
- **T4 — Cross-check companion-test contract.** Confirm that under
  the converted test (the C# port of
  `test/glp_runtime_test.dart`), `GlpRuntimeRoot.Calculate()`
  returns the Int64 value `42`.
  - *Done when*: a static-call expression `GlpRuntimeRoot.Calculate()
    == 42L` is provably true (preserves the Dart companion-test
    assertion `expect(calculate(), 42)`).

## 4. Research Findings

None required. The two non-trivial constructs both reuse already-
ratified research findings recorded in the convspec:

- `dart-top-level-function-to-csharp-static-method` — reuses the
  in-corpus precedent
  `.codeconv/conversion-specs/lib/analysis/type_checker/mode.dart.md`,
  itself authoritatively grounded in
  `https://dart.dev/language/functions` (Dart official) and
  `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/static-classes-and-static-class-members`
  (Microsoft Learn, official). Per FR-024 (no re-research of an
  already-ratified finding), no additional web work is required.
- `rf-dart-int-literal-arithmetic-to-csharp-long-literal-arithmetic` —
  authoritatively grounded in `https://dart.dev/language/built-in-types`,
  `https://dart.dev/language/operators` (Dart official) and
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types`,
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/checked-and-unchecked`
  (Microsoft Learn, official). All citations recorded in the
  convspec's "Rationale and research provenance" section.

No web research performed by this plan (WebSearch / WebFetch
forbidden by the planagent protocol); none needed because every
construct is verbatim-derivable from the ratified convspec.

## 5. Consistency Pass

§2 vs convspec: every construct in §2 mirrors the convspec
`constructs:` block verbatim — `construct_key`, `target_decision`,
`research_finding_id`, and `nuance` are reproduced without
modification. The host-class mandate (`GlpRuntimeRoot` or codegen-
recorded equivalent, NOT `GlpRuntime`) is preserved. The `L` suffix
mandate on integer literals is preserved. Source-form preservation
(`6 * 7`, NOT folded to `42`) is preserved.

§2 vs §3: each decomposed task unit in §3 maps onto a §2 mapping
row — T1 (host file/namespace/class) covers the top-of-file
structural mapping in §2; T2 (method signature) covers Construct 1;
T3 (method body) covers Construct 2; T4 (test contract) covers the
companion-test bind-back recorded in the convspec's Notes section.
No §2 decision is absent from §3; no §3 task introduces a decision
not present in §2.

§4 vs §2/§3: §4 reuses ratified research findings cited by §2; no
new research, no contradiction.

§2 vs convspec `conversion_units`: convspec lists two conversion
units —
`"public static class GlpRuntimeRoot (or distinctly-named non-namespace-shadowing host) — pure static host for top-level function"`
and
`"public static long Calculate() — returns 6L * 7L (= 42)"`.
§2's top-of-file structural mapping plus the two construct mappings
correspond one-to-one with these units (T1 emits the host, T2+T3
emit the method).

§2/§3 vs CLAUDE.md: no GLP-runtime constraints apply (this is the
`dart create -t package` scaffold stub, not GLP runtime code).
No spec ambiguity, no contract gap.

Convspec `escalations: []` and `open_escalation_count: 0` in the
tombstone — confirmed consistent with §6 below.

Result: zero gaps; no fix needed; no escalation needed.

## 6. Escalations

None.
