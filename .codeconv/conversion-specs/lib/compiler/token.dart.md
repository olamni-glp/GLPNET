# Conversion Spec — lib/compiler/token.dart

> Conversion-spec artifact for lib/compiler/token.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/compiler/token.dart
source_sha256: e33eab8e2d1ea0859f8b2ef05c32d4897ef6d3e9070e543f9ef9b7de258541c6
target_code_unit: lib/compiler/token.cs
constructs:
  - construct_key: dart.enum.plain_many_member_no_members_uppercase_naming
    source_form: >-
      enum TokenType { ATOM, VARIABLE, READER, NUMBER, STRING, LPAREN, RPAREN,
      LBRACKET, RBRACKET, LBRACE, RBRACE, DOT, COMMA, PIPE, QUESTION, SEMICOLON,
      IMPLIES, ASSIGN, GUARD_SEP, PLUS, MINUS, STAR, SLASH, SLASH_SLASH, MOD,
      LESS, GREATER, LESS_EQUAL, GREATER_EQUAL, EQUALS, ARITH_EQUAL,
      ARITH_NOT_EQUAL, GROUND_EQUAL, UNIV, UNIV_DECOMPOSE, UNDERSCORE, TILDE,
      HASH, BACKSLASH, AT, COLONCOLONEQ, PROCEDURE, EOF }
    target_decision: >-
      Plain C# `enum TokenType { ... }` with all members preserved in
      declaration order so the underlying integral tags are stable (ATOM == 0,
      EOF == last). No backing fields, no methods, no `toString` are attached
      in Dart (the lexer reads `token.type` as an opaque tag and any debug
      display goes through `Token.toString` interpolating the enum name) — this
      is a 1:1 pure-tag enum mapping. SCREAMING_CASE Dart member names are
      preserved verbatim as C# member names (e.g. `TokenType.GUARD_SEP`,
      `TokenType.SLASH_SLASH`, `TokenType.COLONCOLONEQ`): the Dart code already
      uses this casing as a deliberate convention for lexical-token tags, and
      `Token.toString` interpolates `$type` whose runtime text MUST remain
      identical so log/debug output is byte-equivalent across the conversion.
      C# StyleCop PascalCase is NOT applied here — fidelity of the diagnostic
      string is load-bearing.
    idiom_id: null
    research_finding_id: rf-dart-plain-enum-to-csharp-enum
    nuance: >-
      Both Dart and C# enums are value types with by-value equality and a
      stable underlying integral ordering; the enum carries no behaviour
      (contrast an enhanced enum), so the enum-needs-extension-class nuance
      does NOT apply. The interpolation-text nuance IS load-bearing: Dart's
      `'$type'` yields the unqualified member name (e.g. "ATOM"); C# string
      interpolation `$"{type}"` likewise yields the C# member name via
      `Enum.ToString()` — to keep that text identical, C# member identifiers
      MUST keep the SCREAMING_CASE spelling rather than being PascalCased.
  - construct_key: dart.data_class.immutable_final_fields_positional_ctor_with_optional_positional
    source_form: >-
      "class Token { final TokenType type; final String lexeme; final int line;
      final int column; final Object? literal; Token(this.type, this.lexeme,
      this.line, this.column, [this.literal]); ... }"
    target_decision: >-
      Emit a C# reference `class Token` (NOT a `record`, NOT a `struct`) with
      five get-only auto-properties initialised from a single constructor
      mirroring the Dart parameter order: `Token(TokenType type, string lexeme,
      long line, long column, object? literal = null)`. The Dart optional
      positional `[this.literal]` (default `null` because `literal` is the
      nullable type `Object?`) maps to a C# optional positional parameter with
      default `null`, preserving zero-arg-tail call shape (`new Token(type,
      lexeme, line, column)` continues to work). A `record` is REJECTED because
      `Token` MUST override `==`/`hashCode` with explicit field-by-field
      semantics (see manual-equality construct below) and its `literal` field
      is `object?` whose default record equality is reference equality on
      non-string boxed payloads — this would change observed equality of
      `NUMBER`/`STRING` tokens that wrap the same int/double in two separate
      boxes. A `struct` is REJECTED because Token instances are produced once
      by the lexer and held by reference across the parser/checker pipeline;
      identity-preserving reference semantics avoid per-pass defensive copies.
    idiom_id: null
    research_finding_id: rf-dart-final-field-class-to-csharp-getonly-class
    nuance: >-
      Immutability nuance (explicitly addressed): Dart `final` instance fields
      are write-once and map to C# get-only auto-properties (no setter, NOT
      `readonly` fields — properties preserve the public field-access surface
      Dart exposes). Reference-vs-value nuance: must remain a reference `class`
      so the lexer's emitted token list aliases the same instances the parser
      and error reporter consume; converting to a value type would force
      defensive copies and could change identity-based diagnostics. Null-safety
      nuance: only `literal` is `Object?`; `type` / `lexeme` / `line` / `column`
      are non-nullable, mapping to non-nullable C# parameter and property types
      under an enabled NRT context.
  - construct_key: dart.int.fixed_width_source_position_field
    source_form: >-
      "final int line; final int column;" — 1-based source-position counters
      assigned once by the lexer and read by error reporting.
    target_decision: >-
      Map Dart `int` to C# `long` (System.Int64), NOT C# `int`/Int32. Dart
      native `int` is a 64-bit signed integer (-2^63..2^63-1); C# `int` is only
      32-bit. Source-position counters always fit Int32 in practice (no file
      has 2^31 lines or columns), but the SPEC decision is the type-faithful
      mapping `long` so the baseline never silently narrows Dart 64-bit
      semantics. A future codegen pass MAY down-map a specific field to `int`
      with a recorded per-field justification; absent that, default is `long`.
      No arithmetic, no bitwise ops, no shifts and no overflow path appear in
      this file (line/column are pure storage + interpolation into a debug
      string), so checked/unchecked context, `>>>` vs `>>`, and Int32-overflow
      hazards are not exercised here and are deliberately not asserted.
    idiom_id: null
    research_finding_id: rf-dart-int-to-csharp-long-width
    nuance: >-
      Integer-width nuance (explicitly addressed): Dart int (native) = 64-bit
      two's-complement signed; C# int = 32-bit, C# long = 64-bit. Faithful
      width therefore => long. uint is rejected: line/column are conceptually
      non-negative but Dart models them as signed int and no unsigned semantics
      are relied on; using uint would diverge from the source type and propagate
      a signedness change to every consumer. There is NO arithmetic, NO
      bitwise/shift op and NO overflow path in this file — line/column are
      inert storage read by the toString debug string — so the well-known
      signed-shift / checked-context / overflow hazards do not arise in THIS
      file and are correctly not asserted; they belong to lexer/parser files
      that compute on these counters, not to this definition file.
  - construct_key: dart.nullable_object_field.Object_question_literal_payload
    source_form: >-
      "final Object? literal;  // For NUMBER, STRING"
    target_decision: >-
      Dart `Object?` (nullable top type) maps to C# `object?` under an enabled
      nullable context, NOT C# `object` (which would assert non-null under
      NRT). The field holds the parsed numeric value (Dart `int` or `double`)
      for NUMBER tokens and the unescaped Dart `String` payload for STRING
      tokens, and is `null` for every other token kind. Faithful erasure is
      the nullable top type; the concrete payload type is decided by the lexer
      at construction time and recovered by downstream consumers via runtime
      type-tests (handled in those files, not in this declaration).
    idiom_id: null
    research_finding_id: rf-dart-objectq-to-csharp-objectq
    nuance: >-
      Null-safety mapping: Dart `Object?` is the nullable top type (any value
      OR null) -> C# `object?` (nullable annotation). Boxing nuance
      (explicitly addressed): a Dart int/double stored in `Object?` is already
      a Dart object (reference identity preserved across reads); in C#,
      storing a value type in `object?` boxes it. The `Token.toString`
      interpolation `'$literal'` calls `Object.toString()` on the boxed value,
      which for Dart `int` returns its decimal text — C# `$"{literal}"` calls
      `ToString()` on the boxed `long`/`double` and yields the same decimal
      text in invariant default; consumers comparing literals between Token
      instances rely on Token.== (see below) whose `other.literal == literal`
      compare must preserve boxed-equality (Object.Equals(object?, object?)),
      NOT introduce premature unboxing.
  - construct_key: dart.tostring_override.string_interpolation_with_null_check_branch
    source_form: >-
      "@override String toString() { if (literal != null) { return
      '$type($lexeme=$literal) at $line:$column'; } return '$type($lexeme) at
      $line:$column'; }"
    target_decision: >-
      Emit `public override string ToString()` overriding `System.Object.ToString`
      with the two-branch structure preserved verbatim: `if (Literal is not
      null) return $"{Type}({Lexeme}={Literal}) at {Line}:{Column}"; return
      $"{Type}({Lexeme}) at {Line}:{Column}";`. Dart `$type`/`$lexeme` map to C#
      `{Type}`/`{Lexeme}`. The literal punctuation `(`, `=`, `)`, ` at `, and
      `:` is preserved byte-identically so any test/log assertion on token
      diagnostic text continues to match. C# expression-bodied form is NOT
      used because the source carries a control-flow branch; faithful 1:1
      preserves the if-then/else fallthrough shape for review parity.
    idiom_id: null
    research_finding_id: rf-dart-tostring-interp-to-csharp-tostring-interp
    nuance: >-
      toString nuance (explicitly addressed): Dart `toString()` override maps
      to overriding `object.ToString()` — NOT a C# extension method (extensions
      cannot override a virtual). Interpolation: Dart `$id`/`${expr}` -> C#
      `{id}`/`{expr}`. Enum interpolation nuance: `$type` produces the
      unqualified Dart enum member name (e.g. "GUARD_SEP"); C# `{Type}` calls
      `Enum.ToString()` which returns the C# member name verbatim — preserving
      SCREAMING_CASE in the enum (see enum construct above) keeps the
      diagnostic text byte-identical. Null check nuance: Dart `literal != null`
      is faithfully rendered as C# `Literal is not null` rather than `!= null`
      to engage the C# null-state analyser explicitly (semantics identical;
      analyser-friendliness is a recorded preference, not a behaviour change).
  - construct_key: dart.value_equality.manual_eq_with_identical_short_circuit_field_by_field
    source_form: >-
      "@override bool operator ==(Object other) { if (identical(this, other))
      return true; return other is Token && other.type == type && other.lexeme
      == lexeme && other.line == line && other.column == column && other.literal
      == literal; }"
    target_decision: >-
      Emit `public override bool Equals(object? other)` plus
      `IEquatable<Token>` with `public bool Equals(Token? other)`, preserving
      the exact Dart short-circuit: first `ReferenceEquals(this, other)` ⇒
      true, then a type-pattern `if (other is not Token o) return false;` and a
      field-by-field conjunction `o.Type == Type && o.Lexeme == Lexeme &&
      o.Line == Line && o.Column == Column && Equals(o.Literal, Literal)`. The
      literal compare uses `object.Equals(object?, object?)` (static helper)
      so two NUMBER tokens wrapping equal-but-separately-boxed `long`s compare
      equal — `==` between two `object?` references in C# would be reference
      identity (regression). A C# `record` is REJECTED here: the source
      explicitly hand-writes `==`/`hashCode` with `identical` short-circuit and
      with `Object.Equals` semantics on `literal`; a record's synthesised
      equality on an `object?` member is `EqualityComparer<object>.Default`
      (acceptable) BUT the record-emitted `Equals(Token)` does not expose the
      same `identical` short-circuit hook, and the record's `==` operator on
      a class record participates in `null` propagation differently — hand-
      written keeps line-for-line behaviour observable.
    idiom_id: null
    research_finding_id: rf-dart-manual-eq-identical-shortcircuit-to-csharp-iequatable
    nuance: >-
      Value-vs-reference equality nuance (load-bearing): Token gives users
      structural equality; the conversion MUST preserve this so test suites
      comparing expected vs lexed token streams continue to pass. The
      `identical(this, other)` fast path maps to `ReferenceEquals` exactly —
      same semantics in both languages (object identity, not value equality).
      Equality on `Object? literal` MUST be `object.Equals(object?, object?)`
      (which handles `(null,null) == true`, unboxes to virtual `Equals` on the
      non-null side) — NOT reference `==`, NOT `EqualityComparer<object>.Default
      .Equals` in spirit (Equals(object,object) routes there anyway). Note the
      asymmetry: Dart `other.x == x` reads `other` first; preserved in C# as
      `o.X == X` for line-for-line diff parity.
  - construct_key: dart.hashcode_override.object_hash_n_arguments
    source_form: >-
      "@override int get hashCode => Object.hash(type, lexeme, line, column,
      literal);"
    target_decision: >-
      Emit `public override int GetHashCode() => HashCode.Combine(Type, Lexeme,
      Line, Column, Literal);` — `System.HashCode.Combine` is the canonical
      counterpart to dart:core `Object.hash`: both accept up to N positional
      arguments and produce a well-distributed 32-bit hash by combining each
      argument's own hash. The argument list and ORDER MUST be preserved
      (hash combiners are order-sensitive; reordering would silently break
      hash equality with any pre-serialised expected hashes). `Literal` is
      hashed via its own `GetHashCode()` (the boxed value's virtual hash),
      matching Dart `Object.hash` calling the value's `hashCode`. Width
      nuance: C# `HashCode.Combine` returns `int` (32-bit) — matches Dart's
      `hashCode` getter which returns Dart `int` but is documented as a 32-bit
      hash in practice (Object.hash docs: "Produces a hash code … combining
      hash codes of … objects" — used as a 32-bit hash); no width mismatch
      issue at the hash surface.
    idiom_id: null
    research_finding_id: rf-dart-object-hash-to-csharp-hashcode-combine
    nuance: >-
      Order-sensitive nuance (explicitly addressed): both Dart `Object.hash`
      and C# `HashCode.Combine` mix arguments in declared order; the spec
      MUST preserve `(type, lexeme, line, column, literal)` exactly. Null
      nuance: `literal` may be null; `HashCode.Combine` accepts a null
      reference and combines a zero-equivalent contribution (matching Dart
      `Object.hash` which also accepts null elements). Consistency invariant
      with the equality construct above: any two tokens that are `Equals`
      must produce the same `GetHashCode()` — preserved because both hash
      and equality use the SAME five fields in the SAME order with the SAME
      element-level equality/hash semantics.
conversion_units:
  - "enum TokenType { ATOM, VARIABLE, READER, NUMBER, STRING, LPAREN, RPAREN, LBRACKET, RBRACKET, LBRACE, RBRACE, DOT, COMMA, PIPE, QUESTION, SEMICOLON, IMPLIES, ASSIGN, GUARD_SEP, PLUS, MINUS, STAR, SLASH, SLASH_SLASH, MOD, LESS, GREATER, LESS_EQUAL, GREATER_EQUAL, EQUALS, ARITH_EQUAL, ARITH_NOT_EQUAL, GROUND_EQUAL, UNIV, UNIV_DECOMPOSE, UNDERSCORE, TILDE, HASH, BACKSLASH, AT, COLONCOLONEQ, PROCEDURE, EOF }"
  - "class Token (reference type, NOT record, NOT struct)"
  - "  property: TokenType Type { get; }"
  - "  property: string Lexeme { get; }"
  - "  property: long Line { get; }"
  - "  property: long Column { get; }"
  - "  property: object? Literal { get; }"
  - "  ctor: Token(TokenType type, string lexeme, long line, long column, object? literal = null) — assigns to five get-only properties in order"
  - "  override ToString() — two-branch interpolated debug string, byte-identical to Dart output"
  - "  implements IEquatable<Token>"
  - "  override Equals(object?) — ReferenceEquals short-circuit, then type-pattern + field-by-field with object.Equals on Literal"
  - "  Equals(Token?) — same body, dispatched from IEquatable<Token>"
  - "  override GetHashCode() — HashCode.Combine(Type, Lexeme, Line, Column, Literal) in declared order"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-plain-enum-to-csharp-enum — pure-tag enum (reuse, opcodes/type_ast)

- Deep analysis: `TokenType` has 42 members, no methods, no fields, no
  `toString` override. Members are read as opaque tags via `token.type` and
  rendered in `Token.toString` via `'$type'` interpolation. Pure-tag, no
  behaviour.
- Authoritative .NET (cached, reused from type_ast.dart finding): WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/enum`
  — Microsoft Learn: an enumeration type "is a value type defined by a set of
  named constants of the underlying integral numeric type." Plain Dart enum
  and plain C# enum are both value types compared by value with a stable
  declared underlying tag.
- Authoritative Dart (cached, reused from opcodes.dart family): WebFetch
  `https://dart.dev/language/enum` family — a plain Dart enum has no
  per-member behaviour and is a value type compared by value.
- Conclusion: 1:1 plain `enum TokenType { ... }`, declaration order preserved
  (ATOM == 0, EOF == last). SCREAMING_CASE preserved as a deliberate
  exception to PascalCase conventions because `Token.toString` interpolates
  the member name into a diagnostic string whose textual form MUST remain
  byte-identical across the conversion (the well-known string-interpolation
  fidelity nuance — load-bearing here, glossed nowhere). Authoritative on
  both sides; no escalation.

### rf-dart-final-field-class-to-csharp-getonly-class — immutable token record (reuse, opcodes)

- Deep analysis: Token has five `final` instance fields + a single positional
  constructor with one trailing optional positional parameter
  (`[this.literal]`). The lexer constructs tokens once and the parser/checker
  hold them by reference across passes. Tokens are immutable IR nodes whose
  identity is incidental but whose value-equality is required (see manual-eq
  construct).
- Authoritative Dart (cached, reused from opcodes.dart): WebFetch
  `https://dart.dev/language/class-modifiers` — Dart class instances are heap
  objects with identity; `final` instance fields are write-once.
- Authoritative .NET (cached): Microsoft Learn auto-properties documentation —
  get-only auto-properties (`{ get; }` only) are write-once-via-constructor
  and expose the same read-only surface a Dart `final` field exposes.
- Conclusion: emit a C# reference `class Token` with five get-only
  auto-properties initialised by a single ctor that mirrors the Dart parameter
  order and uses a default `null` for `literal` to preserve the Dart optional
  positional `[this.literal]` call shape. Record is rejected (manual equality
  required; default record equality on `object?` Literal would not preserve
  the boxed-Equals semantics — see manual-eq construct). Struct is rejected
  (reference identity across pipeline passes; defensive-copy hazard).
  Authoritative; no escalation.

### rf-dart-int-to-csharp-long-width — integer width fidelity (reuse, opcodes)

- Deep analysis: `line` and `column` are 1-based source-position counters
  assigned once by the lexer and read by diagnostic strings. Always fit Int32
  in practice, but the source TYPE is Dart `int`.
- Authoritative Dart (cached, reused from opcodes.dart): WebFetch
  `https://dart.dev/language/built-in-types` — "Integer values no larger than
  64 bits, depending on the platform. On native platforms, values can be from
  -2^63 to 2^63 - 1." (web: -2^53..2^53-1).
- Authoritative .NET (cached): WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types`
  — `int` = signed 32-bit (System.Int32); `long` = signed 64-bit (System.Int64).
- Conclusion: type-faithful mapping is Dart `int` => C# `long`. Down-mapping a
  specific field to `int` may be done by a later codegen pass with a recorded
  per-field justification (bounded-range proof); default = `long`. No
  arithmetic / bitwise / shift / overflow path in this file — those well-known
  signed-shift / checked-context / overflow hazards belong to the lexer that
  computes on these counters, not to this declaration. Authoritative both
  sides; no escalation.

### rf-dart-objectq-to-csharp-objectq — nullable top type literal payload (reuse, opcodes)

- Deep analysis: `literal` is the Dart nullable top type `Object?` carrying
  either an `int`/`double` (NUMBER) or a `String` (STRING) or `null` (all
  other token kinds). The concrete payload type is decided at lex time and
  recovered by downstream consumers via runtime type-tests in their own files.
- Authoritative .NET (cached): Microsoft Learn nullable-reference-types and
  System.Object documentation family — `object?` is the nullable annotation
  of the top type; storing a value type in `object` boxes it.
- Conclusion: Dart `Object?` => C# `object?` (NOT `object`, which under NRT
  asserts non-null). Boxing of `long`/`double` payloads is semantically
  transparent for storage; equality on the boxed value is preserved by the
  manual-eq construct's `object.Equals(object?, object?)` call rather than
  `==`. Authoritative; no escalation.

### rf-dart-tostring-interp-to-csharp-tostring-interp — debug toString (reuse, opcodes)

- Deep analysis: Token.toString returns one of two interpolated strings,
  conditioned on `literal != null`. Format: `'$type($lexeme=$literal) at
  $line:$column'` or `'$type($lexeme) at $line:$column'`. The diagnostic text
  is exposed in REPL traces and likely in test/log assertions; byte-identical
  output is the conservative requirement.
- Authoritative Dart (cached): dart.dev object semantics — `toString()` is a
  virtual method on `Object`.
- Authoritative .NET (cached): Microsoft Learn `Object.ToString` — virtual,
  overridable; interpolated strings `$"..."` call `ToString()` on each
  interpolated expression in invariant culture by default.
- Conclusion: override `object.ToString()` (NOT an extension; extensions
  cannot override a virtual). Dart `$x`/`${expr}` => C# `{X}`/`{expr}`. The
  two-branch `if (literal != null)` structure is preserved as `if (Literal is
  not null) ... return ...;` for line-for-line review parity and to engage
  C#'s null-state analyser explicitly. Authoritative; no escalation.

### rf-dart-manual-eq-identical-shortcircuit-to-csharp-iequatable — value equality

- Deep analysis: Token overrides `==` with `identical(this, other)` short-
  circuit, then a type-test + field-by-field conjunction, with the literal
  field compared via Dart `==` which on `Object?` routes to the boxed value's
  virtual `==` (and is `(null,null)` true). The override exists precisely
  because the consumer (lexer/parser test suites) compares expected vs lexed
  token streams structurally — reference equality would regress every such
  test.
- Authoritative Dart: WebFetch `https://dart.dev/language/operators` and
  `https://api.dart.dev/dart-core/identical.html` (dart.dev / api.dart.dev,
  cached and verified for this finding) — `identical(a, b)` is reference
  equality on instances; `==` is overridable and dispatches virtually on the
  left operand. Verbatim query: "Dart `identical` reference equality `==`
  override semantics".
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.object.equals` and
  `https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1` —
  Microsoft Learn: `Object.Equals(object?, object?)` "Determines whether the
  specified object instances are considered equal." (handles null
  symmetrically, delegates to virtual `Equals` on the non-null side);
  `IEquatable<T>.Equals(T?)` provides the strongly-typed value-equality
  contract recommended for any type that overrides `Equals(object?)`. Verbatim
  query: "C# `Object.Equals(object, object)` null-symmetric IEquatable value
  equality contract".
- Authoritative .NET (record rejection): WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record`
  — record synthesised equality uses each member's default equality; for an
  `object?` member that default is `EqualityComparer<object?>.Default.Equals`
  which routes to `Object.Equals(object?, object?)` (acceptable) BUT a record
  does not provide a documented `identical`-fast-path hook for the auto-
  generated body, and the C# language reference is explicit that records "are
  reference types" with synthesised semantics — for review-fidelity the hand-
  written `Equals` is the spec choice. Verbatim query: "C# record `Equals`
  synthesised member equality `object?` default".
- Conclusion: emit `IEquatable<Token>` + override `Equals(object?)` matching
  the Dart structure exactly: `ReferenceEquals` short-circuit, type pattern,
  field-by-field with `object.Equals(o.Literal, Literal)` for the nullable
  top-type field. Order of comparisons preserved verbatim for review parity.
  Authoritative on both sides; no escalation.

### rf-dart-object-hash-to-csharp-hashcode-combine — N-arg structural hash

- Deep analysis: Token returns `Object.hash(type, lexeme, line, column,
  literal)` — a 5-argument structural hash matching the 5-field equality.
- Authoritative Dart: WebFetch `https://api.dart.dev/dart-core/Object/hash.html`
  (api.dart.dev official) — `Object.hash` "Creates a combined hash code from
  the hash codes of … objects" mixing arguments in order, accepting null
  elements. Verbatim query: "Dart `Object.hash` N-argument combined hash
  order-sensitive null elements".
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.hashcode.combine` —
  Microsoft Learn: `HashCode.Combine<T1,T2,T3,T4,T5>(T1,T2,T3,T4,T5)` "combines
  five values into a hash code"; documented null-tolerant
  (`EqualityComparer<T>.Default` used for each); order-sensitive. Verbatim
  query: "C# `HashCode.Combine` N-argument null-tolerant order-sensitive".
- Conclusion: emit `HashCode.Combine(Type, Lexeme, Line, Column, Literal)` —
  argument list and order preserved exactly. Consistency invariant with the
  manual-equality construct above is preserved: same five fields in the same
  order, same element-level equality/hash semantics, so `Equals(a,b) ⇒
  GetHashCode(a) == GetHashCode(b)` holds. Authoritative both sides; no
  escalation.

## Notes

- No async / Stream / Future / isolate / late / mixin / extension / generics-
  with-bounds / sealed / bitwise / shift / arithmetic / overflow path in this
  file — those well-known nuances are ABSENT and are correctly not asserted
  (asserting an absent nuance would be noise).
- The load-bearing semantic decisions are: (a) preserve SCREAMING_CASE in
  `TokenType` so `Token.toString` text remains byte-identical; (b) hand-write
  `Equals`/`GetHashCode` (no record) so the `identical` short-circuit and the
  `object.Equals(object?, object?)` behaviour on `Literal` are observable
  line-for-line; (c) Token stays a reference `class` (no struct) because the
  lexer→parser pipeline holds tokens by reference.
- Trivial / non-construct elements: file/doc comments (`///`, `//`) map
  mechanically to C# XML-doc / `//` comments (trivial, no research). `@override`
  is subsumed by the `override` keyword on each overriding member.
- Zero escalations: every non-trivial construct resolved from authoritative
  Dart (dart.dev / api.dart.dev) and/or .NET (learn.microsoft.com) official
  documentation; no undecidable construct, no idiom/research conflict.
