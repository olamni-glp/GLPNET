---
path: lib/compiler/token.dart
cycle_group_id: 8
scc_siblings: []
generated_at: 2026-05-21T14:36:45Z
source_sha256: e33eab8e2d1ea0859f8b2ef05c32d4897ef6d3e9070e543f9ef9b7de258541c6
schema_version: 1
---

# Conversion Plan: lib/compiler/token.dart

## 1. Source Analysis

`glp_runtime_net/lib/compiler/token.dart` is a 97-line, dependency-free Dart
file that defines the lexical-token vocabulary for the GLP compiler.

Top-level declarations (exhaustively):

1. **`enum TokenType`** (lines 2–63) — a plain Dart enum with 42 members in
   five thematically grouped sections (identifiers/literals, delimiters,
   punctuation, operators, arithmetic, comparison, special, type-decl, EOF).
   All members are SCREAMING_CASE (e.g. `ATOM`, `GUARD_SEP`, `SLASH_SLASH`,
   `COLONCOLONEQ`, `ARITH_NOT_EQUAL`, `UNIV_DECOMPOSE`). NO methods, NO
   constructors, NO interfaces, NO backing fields — pure-tag enum. Members are
   consumed as opaque tags via `token.type` and rendered into diagnostic text
   via `'$type'` string interpolation inside `Token.toString`.

2. **`class Token`** (lines 66–96) — an immutable reference class with five
   `final` instance fields:
   - `final TokenType type;` (non-nullable)
   - `final String lexeme;` (non-nullable)
   - `final int line;` (non-nullable; Dart 64-bit int; 1-based source line)
   - `final int column;` (non-nullable; Dart 64-bit int; 1-based source column)
   - `final Object? literal;` (nullable top type; carries `int`/`double` for
     `NUMBER`, `String` for `STRING`, `null` otherwise)

3. **Constructor** `Token(this.type, this.lexeme, this.line, this.column,
   [this.literal])` — single positional constructor, with `literal` as a
   trailing optional positional parameter (Dart default `null` because the
   declared type is the nullable `Object?`). This preserves the
   zero-trailing-arg call shape `Token(type, lexeme, line, column)`.

4. **`@override String toString()`** (lines 76–81) — two-branch interpolated
   diagnostic string:
   - If `literal != null` ⇒ `'$type($lexeme=$literal) at $line:$column'`
   - Else ⇒ `'$type($lexeme) at $line:$column'`
   Diagnostic output is exposed in REPL traces and test/log assertions; the
   text is byte-significant.

5. **`@override bool operator ==(Object other)`** (lines 83–92) — hand-written
   structural equality:
   - `identical(this, other) ⇒ true` (reference-identity fast path)
   - Then `other is Token && other.type == type && other.lexeme == lexeme &&
     other.line == line && other.column == column && other.literal == literal`
   Field-by-field; the `other.x == x` argument-order is intentional. Literal
   compare via Dart `==` on `Object?` routes to the boxed value's virtual `==`
   (and is `(null, null) ⇒ true`).

6. **`@override int get hashCode => Object.hash(type, lexeme, line, column,
   literal);`** (line 95) — 5-argument `Object.hash` matching the 5-field
   equality, in declared field order.

**Non-construct trivia**: file/doc comments (`///` and `//`) carry semantic
documentation but no behaviour; `@override` is the standard Dart annotation
on overriding members.

**Absent constructs (correctly NOT asserted)**: no async / `Future` / `Stream`
/ isolate; no `late` / `mixin` / `extension` / generics / bounded types /
`sealed`; no arithmetic / bitwise / shift / overflow path on the integer
fields; no I/O; no external dependencies (imports section is empty).

**Cycle/SCC**: singleton (`cycle_group_id: 8`, no siblings). No mutual recursion
or co-evolved file in scope for this plan.

## 2. Dart → C#/.NET Conversion Plan

Target code unit: `lib/compiler/token.cs`. Mirrors the convspec's seven
ratified constructs verbatim.

**C1. `enum TokenType` (plain pure-tag enum, SCREAMING_CASE preserved)**

- Dart `enum TokenType { ATOM, VARIABLE, … EOF }` → C# `public enum TokenType
  { ATOM, VARIABLE, … EOF }`.
- All 42 members preserved in declaration order so the underlying integral
  tags are stable (`ATOM == 0`, `EOF == 41`).
- **SCREAMING_CASE names preserved verbatim** (e.g. `TokenType.GUARD_SEP`,
  `TokenType.SLASH_SLASH`, `TokenType.COLONCOLONEQ`). PascalCase StyleCop
  rule is NOT applied here: `Token.ToString` interpolates `{Type}` whose
  runtime text via `Enum.ToString()` MUST remain byte-identical to Dart's
  `$type` so log/diagnostic/test assertions continue to match.
- No backing fields, no methods, no `ToString` override — pure-tag mapping.
- Default underlying type `int` (System.Int32) is acceptable: enum tags are
  not arithmetic and 42 ≪ Int32 range; this is the C# default and is
  semantically interchangeable with Dart's opaque tag.

**C2. `class Token` (reference class, NOT record, NOT struct)**

- `public sealed class Token : IEquatable<Token>` (sealed — Token has no
  subclassing in Dart and structural equality is well-defined only for the
  exact type).
- Five **get-only auto-properties** initialised by a single constructor
  (write-once-via-ctor matches Dart `final` instance-field semantics; public
  property surface mirrors Dart's public field access):
  - `public TokenType Type { get; }`
  - `public string Lexeme { get; }`
  - `public long Line { get; }`
  - `public long Column { get; }`
  - `public object? Literal { get; }`
- **Record REJECTED** (per convspec): synthesised record equality on the
  `object?` `Literal` member would not preserve the hand-written
  `ReferenceEquals` short-circuit and the explicit `object.Equals` call shape
  observable in line-for-line review.
- **Struct REJECTED** (per convspec): tokens are produced once by the lexer
  and aliased by reference across the parser/checker pipeline; struct
  semantics would force defensive copies and break identity-based
  diagnostics.

**C3. Field-type mappings**

- Dart `TokenType type` → C# `TokenType Type`.
- Dart `String lexeme` → C# `string Lexeme` (non-nullable under enabled NRT).
- Dart `int line` → C# `long Line` (System.Int64). Dart native `int` is
  64-bit; C# `int` is only 32-bit. Type-faithful baseline is `long`; a future
  per-field down-map to `int` requires a recorded bounded-range justification
  (not required by this plan).
- Dart `int column` → C# `long Column` — same reasoning.
- Dart `Object? literal` → C# `object? Literal` (NOT `object`, which would
  assert non-null under NRT). The nullable top type carries `long`/`double`
  for `NUMBER`, `string` for `STRING`, `null` otherwise.

**C4. Constructor**

- Dart `Token(this.type, this.lexeme, this.line, this.column, [this.literal])`
  → C# `public Token(TokenType type, string lexeme, long line, long column,
  object? literal = null) { Type = type; Lexeme = lexeme; Line = line; Column
  = column; Literal = literal; }`.
- Parameter order preserved verbatim.
- The Dart trailing optional positional `[this.literal]` (default `null`
  because the declared type is the nullable `Object?`) maps to a C# optional
  positional parameter with default `null`, preserving the zero-trailing-arg
  call shape `new Token(type, lexeme, line, column)`.

**C5. `ToString` override (two-branch interpolated debug string)**

- Dart `@override String toString()` → C# `public override string ToString()`
  (overriding `System.Object.ToString` — NOT an extension method; extensions
  cannot override a virtual).
- Body preserved with verbatim branch structure:
  ```
  if (Literal is not null)
  {
      return $"{Type}({Lexeme}={Literal}) at {Line}:{Column}";
  }
  return $"{Type}({Lexeme}) at {Line}:{Column}";
  ```
- Punctuation `(`, `=`, `)`, ` at `, `:` preserved byte-identically.
- `Literal is not null` (preferred over `!= null`) engages the C# null-state
  analyser; semantics are identical (per convspec analyser-friendliness note).
- Expression-bodied form NOT used: source carries a control-flow branch and
  1:1 fidelity preserves the if-then/fallthrough shape for review parity.

**C6. Equality (`Equals(object?)` + `IEquatable<Token>.Equals(Token?)`)**

- Dart `@override bool operator ==(Object other)` → C# emits BOTH:
  - `public override bool Equals(object? other)` — required by .NET equality
    contract for any type that overrides `GetHashCode`.
  - `public bool Equals(Token? other)` — `IEquatable<Token>` implementation,
    recommended companion (avoids boxing on generic-collection paths).
- Body of `Equals(object?)`:
  ```
  if (ReferenceEquals(this, other)) return true;
  if (other is not Token o) return false;
  return o.Type == Type
      && o.Lexeme == Lexeme
      && o.Line == Line
      && o.Column == Column
      && object.Equals(o.Literal, Literal);
  ```
- `Equals(Token?)` body shares the same shape (sans the `is not Token`
  pattern; null-check via `other is null ⇒ false`, then `ReferenceEquals`
  short-circuit, then field-by-field).
- **`object.Equals(o.Literal, Literal)`** is load-bearing: `==` between two
  `object?` operands in C# is reference identity, which would regress two
  `NUMBER` tokens that box equal `long`s into separate boxes. The static
  helper handles `(null, null) ⇒ true` symmetrically and delegates to the
  non-null side's virtual `Equals`.
- **Argument order** preserved: `o.X == X` (not `X == o.X`) — line-for-line
  diff parity with Dart `other.x == x`.
- `==` and `!=` operator overloads on `Token`: NOT emitted (per convspec
  decision the override goes through `Equals`/`IEquatable` only; emitting
  operators would diverge from the source surface, which expresses equality
  via `==` operator override but does not expose an operator-call site
  pattern users would rely on in C#).

**C7. `GetHashCode` override (5-arg `HashCode.Combine`)**

- Dart `@override int get hashCode => Object.hash(type, lexeme, line, column,
  literal);` → C# `public override int GetHashCode() => HashCode.Combine(Type,
  Lexeme, Line, Column, Literal);`.
- Argument list AND order preserved verbatim (hash combiners are
  order-sensitive; reordering would silently break hash equality).
- `HashCode.Combine` is null-tolerant on reference arguments (matches
  `Object.hash`'s null tolerance) and uses `EqualityComparer<T>.Default` for
  each argument's own hash, which for `object?` Literal routes to the boxed
  value's virtual `GetHashCode` — matching Dart's `Object.hash` calling each
  value's `hashCode`.
- **Consistency invariant** with C6 preserved: same five fields in same order
  with same element-level equality/hash semantics, so `Equals(a, b) ⇒
  GetHashCode(a) == GetHashCode(b)` holds.

**C8. Trivia**

- File header `/// Token types for GLP lexical analysis` and class header
  `/// Token representing a lexical unit in GLP source code` → C# XML doc
  comments `/// <summary>…</summary>` on the enum and class respectively.
- Inline `//` end-of-line member comments (e.g. `// (` after `LPAREN`) →
  preserved as C# `//` comments verbatim.
- `@override` Dart annotation → subsumed by the C# `override` modifier on
  each overriding member.

**Namespace and file layout**

- C# namespace: `GlpRuntime.Compiler` (mirrors `lib/compiler/` subtree; final
  namespace policy is project-wide and confirmed in sibling-pair specs).
- One `token.cs` file; both `enum TokenType` and `class Token` live in the
  same file — mirrors Dart's single-file convention and matches the target
  path `lib/compiler/token.cs` recorded in the convspec.

## 3. Decomposed Task Units

- **T1 — Emit C# file scaffolding.**
  Done when `lib/compiler/token.cs` exists with `#nullable enable`, the file
  header XML doc comment, `using System;` (for `HashCode`, `IEquatable<T>`,
  `ReferenceEquals`, `object.Equals`), and `namespace GlpRuntime.Compiler;`
  (file-scoped namespace).

- **T2 — Emit `enum TokenType` with 42 members.**
  Done when all 42 SCREAMING_CASE members appear in declaration order
  identical to Dart source (verified by side-by-side member-name diff), no
  explicit underlying type, no backing methods, and each Dart inline `//`
  comment is preserved.

- **T3 — Emit `class Token` shell.**
  Done when `public sealed class Token : IEquatable<Token>` exists with its
  XML doc comment carried over from the Dart `///` class header.

- **T4 — Emit five get-only auto-properties.**
  Done when `Type` / `Lexeme` / `Line` / `Column` / `Literal` all exist with
  `{ get; }` only, with types `TokenType`, `string`, `long`, `long`,
  `object?` respectively (NRT-enabled).

- **T5 — Emit the single positional constructor.**
  Done when `public Token(TokenType type, string lexeme, long line, long
  column, object? literal = null)` assigns all five properties in declared
  parameter order and the trailing default-`null` literal parameter compiles.

- **T6 — Emit `ToString` override.**
  Done when `public override string ToString()` returns the two-branch
  interpolated string with byte-identical punctuation/spacing and uses
  `Literal is not null` for the branch predicate.

- **T7 — Emit `Equals(object?)` override.**
  Done when the method matches the convspec body: `ReferenceEquals`
  short-circuit → `is not Token o` early-exit → field-by-field conjunction
  with `object.Equals(o.Literal, Literal)`; preserved `o.X == X` order.

- **T8 — Emit `Equals(Token?)` (IEquatable).**
  Done when the strongly-typed companion exists with equivalent logic
  (null-check on `other` → `ReferenceEquals` → field-by-field) and is bound
  via `IEquatable<Token>` on the class header (already in T3).

- **T9 — Emit `GetHashCode` override.**
  Done when `public override int GetHashCode() => HashCode.Combine(Type,
  Lexeme, Line, Column, Literal);` is present with argument order verbatim.

- **T10 — Verify diagnostic-string fidelity.**
  Done when a side-by-side comparison shows that for a sample token
  `Token(TokenType.GUARD_SEP, "|", 3, 12, null)`, both Dart and C# produce
  byte-identical `ToString()` output (`GUARD_SEP(|) at 3:12`).

- **T11 — Verify equality/hash consistency invariant.**
  Done when, for two `Token` instances with identical field values (including
  identical `Literal` payloads constructed via separate box sites for a
  `long` literal), `Equals` returns true AND `GetHashCode` returns the same
  value.

## 4. Research Findings

None required.

The convspec's seven `research_finding_id`s
(`rf-dart-plain-enum-to-csharp-enum`,
`rf-dart-final-field-class-to-csharp-getonly-class`,
`rf-dart-int-to-csharp-long-width`,
`rf-dart-objectq-to-csharp-objectq`,
`rf-dart-tostring-interp-to-csharp-tostring-interp`,
`rf-dart-manual-eq-identical-shortcircuit-to-csharp-iequatable`,
`rf-dart-object-hash-to-csharp-hashcode-combine`)
are all cached findings backed by authoritative Dart and .NET official
documentation (dart.dev / api.dart.dev / learn.microsoft.com), already
ratified upstream and reused here. No undecidable construct surfaced; no
external web research required.

## 5. Consistency Pass

- **§2 C1 vs convspec `dart.enum.plain_many_member_no_members_uppercase_naming`**:
  matches — SCREAMING_CASE preserved, 42 members, declaration order
  preserved, pure-tag, default underlying tag. No gap.
- **§2 C2 vs convspec
  `dart.data_class.immutable_final_fields_positional_ctor_with_optional_positional`**:
  matches — `class` (not record/struct), `sealed`, get-only properties,
  single ctor with default-null trailing literal. No gap.
- **§2 C3 (int → long) vs convspec
  `dart.int.fixed_width_source_position_field`**: matches — `long` baseline,
  no down-map applied at this stage. No gap.
- **§2 C3 (Object? → object?) vs convspec
  `dart.nullable_object_field.Object_question_literal_payload`**: matches —
  `object?` under enabled NRT, boxing semantics noted for `long`/`double`
  payloads. No gap.
- **§2 C5 vs convspec
  `dart.tostring_override.string_interpolation_with_null_check_branch`**:
  matches — two-branch preserved, byte-identical text, `Literal is not null`
  preferred. No gap.
- **§2 C6 vs convspec
  `dart.value_equality.manual_eq_with_identical_short_circuit_field_by_field`**:
  matches — `ReferenceEquals` short-circuit, `is not Token o` early-exit,
  field-by-field with `object.Equals(o.Literal, Literal)`, argument order
  `o.X == X` preserved, record/struct rejected. §2 ADDS the
  `IEquatable<Token>.Equals(Token?)` companion that the convspec
  `conversion_units` block already enumerates (lines 247–249), so this is
  derived from the convspec, not a new decision.
- **§2 C7 vs convspec `dart.hashcode_override.object_hash_n_arguments`**:
  matches — `HashCode.Combine(Type, Lexeme, Line, Column, Literal)` in
  declared order. No gap.
- **§2 C8 (trivia) vs convspec "Notes" §**: matches — `///` → XML doc, `//`
  preserved, `@override` subsumed by C# `override`. No gap.
- **§3 task units vs §2 constructs**: every construct C1–C8 maps onto at
  least one task unit T1–T9; T10/T11 are post-emit verification tasks that
  exercise the two load-bearing observable invariants (diagnostic-string
  fidelity, equality/hash consistency) called out in the convspec "Notes"
  bullet (b)/(a). No gap.
- **§4 vs §2/§3**: research is "none required" because every decision in §2
  is verbatim-derived from a ratified convspec construct whose research
  finding is itself authoritative-cached. No gap.
- **CLAUDE.md / project conventions**: namespace `GlpRuntime.Compiler` is
  consistent with the `lib/compiler/` subtree; target path
  `lib/compiler/token.cs` matches the convspec `target_code_unit` and the
  tombstone `target_path`. No gap.

All consistency checks passed; no escalation derived from cross-checking.

## 6. Escalations

None.
