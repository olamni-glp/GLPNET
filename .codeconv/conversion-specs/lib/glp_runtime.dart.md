> Conversion-spec artifact for lib/glp_runtime.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/glp_runtime.dart
source_sha256: 9812a323185f6a0b680576687c6f33269f32ac398ca24656335235adc630a329
target_code_unit: lib/glp_runtime.cs
constructs:
  - construct_key: dart.top_level_function.pure
    source_form: "int calculate() { return 6 * 7; }"
    target_decision: >-
      C# has no top-level functions in this codebase convention; emit as a
      `public static long Calculate()` on a static host class. The package
      name `glp_runtime` is the obvious host candidate but it is also the
      ROOT namespace for the converted package — using `static class
      GlpRuntime` directly at namespace root would shadow the namespace.
      Per the established precedent in
      .codeconv/conversion-specs/lib/analysis/type_checker/mode.dart.md
      (`dart-top-level-function-to-csharp-static-method` — distinctly-named
      host static class to avoid collision with an occupied type name),
      the host MUST be a distinctly-named static class. Spec mandate:
      `public static class GlpRuntimeRoot` (or codegen-recorded equivalent;
      the load-bearing requirement is: NOT a name that collides with the
      converted package's namespace). Method PascalCased to `Calculate` per
      .NET naming conventions. Return type `long` per
      rf-dart-int-to-csharp-long-width (faithful 64-bit mapping of Dart
      `int`). The function is pure (no side effects, no captured state).
    idiom_id: null
    research_finding_id: dart-top-level-function-to-csharp-static-method
    nuance: >-
      Dart allows library-level (top-level) functions; C# requires every
      method to live in a type. Naming collision (explicitly addressed,
      same nuance as the mode.dart precedent): the natural host name
      `GlpRuntime` collides with the converted package's namespace — the
      spec records the host must be a distinctly-named static class.
      Visibility: Dart top-level identifiers without a leading underscore
      are library-public; mapped to C# `public`. Method is pure with no
      captured library state, so no reference/identity hazard. Calling
      convention preserved (zero-arg static call from C# = zero-arg call
      from Dart).
  - construct_key: dart.int.literal_arithmetic.compile_time_constant_expression
    source_form: "return 6 * 7;"
    target_decision: >-
      Body is a single `return` of a compile-time-constant integer
      arithmetic expression (`6 * 7`). Emit as `return 6L * 7L;` (or
      equivalently `return 42L;` — both are evaluatable at compile time;
      spec mandates preserving the SOURCE FORM `6 * 7` so the conversion
      is byte-faithful and a future spec-diff reviewer can see the
      original literal pair, not a folded constant). Literals carry the
      `L` suffix to type them as `System.Int64`, matching the function's
      `long` return type (per rf-dart-int-to-csharp-long-width). NO
      `checked { }` block: see nuance — the result is provably bounded
      (42 << long.MaxValue), so the default C# unchecked context is
      indistinguishable from a checked one here. The companion test
      asserts `calculate() == 42`, so the converted method MUST return
      exactly 42 as Int64.
    idiom_id: null
    research_finding_id: rf-dart-int-literal-arithmetic-to-csharp-long-literal-arithmetic
    nuance: >-
      Integer-arithmetic nuance (explicitly addressed — this is the FIRST
      file in the corpus with actual arithmetic, so the opcodes/token
      idioms' "no arithmetic" caveat does not cover this case): Dart
      `int` is native 64-bit signed two's-complement; the operator `*` on
      two Dart int literals at compile time is exact integer
      multiplication with two's-complement overflow on overflow (Dart
      spec: integer overflow on native is two's-complement wrap; on web
      it's IEEE-754). C# `long * long` is exact two's-complement
      multiplication; in an *unchecked* context it wraps, in a *checked*
      context it throws OverflowException. For the provably-bounded
      literal pair `6 * 7 = 42`, both contexts yield 42 with no
      observable difference, so the default unchecked context is
      semantically faithful here. The `L` suffix on each literal is the
      load-bearing detail: an unsuffixed C# integer literal is `int`
      (Int32), and `int * int` would silently narrow the source's 64-bit
      type. Boxing/identity: returning a `long` (value type) from a
      static method is a stack-allocated copy on the callsite — no
      boxing, no identity, matches Dart's value-semantic integer return.
conversion_units:
  - "public static class GlpRuntimeRoot (or distinctly-named non-namespace-shadowing host) — pure static host for top-level function"
  - "public static long Calculate() — returns 6L * 7L (= 42)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### dart-top-level-function-to-csharp-static-method — top-level function host

- **Deep analysis.** `calculate()` is a Dart library-level (top-level)
  function exported by the `glp_runtime` package (its companion test
  `test/glp_runtime_test.dart` imports it as
  `package:glp_runtime/glp_runtime.dart` and calls `calculate()`
  unqualified, confirming top-level/library-public visibility). The
  function is pure — no captured state, no side effects, no nullable
  reference parameters or return.
- **Precedent (in-repo, authoritative for THIS corpus).** The convspec
  for `lib/analysis/type_checker/mode.dart` records the same Dart→C#
  decision under research_finding_id
  `dart-top-level-function-to-csharp-static-method`: host on a
  distinctly-named static class, PascalCased method name, value-type
  return semantics preserved, naming-collision nuance explicitly
  addressed. That precedent is reused verbatim here (same source-side
  construct, same target shape).
- **Authoritative Dart.** WebFetch `https://dart.dev/language/functions`
  (Dart official) — Dart documents library-level (top-level)
  functions as first-class declarations with library-public
  visibility absent a leading underscore. Reused from the mode.dart
  research finding; no re-research (FR-024).
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/static-classes-and-static-class-members`
  (Microsoft Learn, official) — C# methods must be members of a type;
  static classes / static members are the canonical host for
  free-functions. Reused from the mode.dart research finding; no
  re-research (FR-024).
- **Conclusion.** Authoritative both sides; reused idiom (precedent in
  this corpus). No escalation. Naming-collision nuance: the package's
  own namespace name (`GlpRuntime`) is the natural host but cannot also
  be the type name — the spec mandates a distinctly-named host class to
  avoid the C# namespace/type-name clash, identically to how the
  mode.dart spec resolves the `Mode` enum-vs-host collision.

### rf-dart-int-literal-arithmetic-to-csharp-long-literal-arithmetic — int literal `*`

- **Deep analysis.** The function body is exactly `return 6 * 7;` — a
  compile-time-constant Dart `int` multiplication of two unsigned
  decimal literals. This is the FIRST file in the converted corpus that
  contains any arithmetic at all: every prior convspec
  (opcodes.dart / opcodes_v2.dart / token.dart) explicitly recorded
  "NO arithmetic, NO bitwise op, NO overflow path in this file" as the
  reason the int-width idiom did not have to address overflow.
  This file flips that caveat — arithmetic IS exercised — so the
  overflow/checked-context nuance must now be explicitly addressed
  rather than waived.
- **Authoritative Dart.** WebFetch
  `https://dart.dev/language/built-in-types` and
  `https://dart.dev/language/operators` (Dart official) — Dart `int` is
  64-bit signed two's-complement on native (range -2^63..2^63-1); the
  binary `*` operator is integer multiplication on `int`; integer
  overflow wraps in two's-complement on native runtimes. (Web/JS
  runtimes use IEEE-754 doubles for `int`, but the C# target is
  native-equivalent, and `6 * 7 = 42` is exact under both.) Verbatim
  from the Dart language tour: "Integer values no larger than 64 bits,
  depending on the platform. On native platforms, values can be from
  -2^63 to 2^63 - 1."
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types`
  (Microsoft Learn, official) — `long` is `System.Int64`, signed 64-bit,
  range -9,223,372,036,854,775,808 to 9,223,372,036,854,775,807; binary
  `*` on two `long` operands is `long` multiplication; the integer
  literal suffix `L` types a literal as `long`. The default arithmetic
  context for non-constant integer expressions is unchecked (wrapping);
  constant-expression overflow at compile time is a compile-time error
  (but `6 * 7` does not overflow Int64, so this does not apply). Also
  WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/checked-and-unchecked`
  (Microsoft Learn, official) — explicit `checked { }` would throw on
  overflow; not required here because `42` is provably representable.
- **Conclusion.** Authoritative both sides; no escalation. The `L`
  suffix on each literal is the load-bearing translation detail (an
  unsuffixed C# literal is `int`/Int32 and would silently narrow the
  Dart 64-bit type). Source-form preservation (`6 * 7`, NOT folded to
  `42`) is the spec's explicit choice so review can trace the
  byte-faithful translation; the resulting Int64 value is identical
  either way and the companion test asserts that value is `42`.

## Notes

- This file is the `dart create -t package` default scaffold (the
  Dart pub "calculate returns 42" stub). Its companion test
  `glp_runtime_net/test/glp_runtime_test.dart` asserts
  `expect(calculate(), 42)`. The conversion preserves that
  contract: a zero-arg public static method returning `long 42`,
  reachable from the converted test under the same package-level
  identifier (modulo PascalCase + static-host prefix).
- ABSENT constructs (explicitly NOT asserted, to avoid noise): no
  class, no fields, no constructor, no toString, no async, no Stream,
  no Future, no isolates, no `late`, no nullable annotations, no
  generics, no extensions, no mixins, no `sealed`, no exhaustive
  switch, no bitwise/shift ops. The arithmetic nuance IS exercised
  (and recorded above) — that is the one well-known nuance this file
  touches.
- Trivial constructs (no idiom, no research): the return-statement
  syntax itself, brace-block body syntax, and PascalCase naming
  convention map mechanically and are subsumed by the two recorded
  constructs.
- Zero escalations: every non-trivial construct resolved from
  authoritative Dart (dart.dev) and .NET (learn.microsoft.com) official
  documentation; the top-level-function idiom reuses an existing
  in-corpus precedent (mode.dart) verbatim; no undecidable construct,
  no idiom/research conflict.
