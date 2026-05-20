> Conversion-spec artifact for lib/bytecode/opcodes_v2.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/bytecode/opcodes_v2.dart
source_sha256: c8549ccea9fbe836a1804e62b0164ac312889f3602144e9403938f9aaca206d6
target_code_unit: lib/bytecode/opcodes_v2.cs
constructs:
  - construct_key: dart.library_directive.named_library
    source_form: "library;"
    target_decision: >-
      Dart `library;` (unnamed library directive — declares this file is a
      library compilation unit, no name body) has no direct C# counterpart and
      MUST emit nothing. C# has no per-file library directive: the file's
      logical grouping is determined by its (file-scoped) namespace, which is
      decided at the assembly / project level by the codegen stage, not by any
      token in this Dart file. Specifically, do NOT translate `library;` into
      a C# `namespace`, `using`, attribute, or comment marker.
    idiom_id: null
    research_finding_id: rf-dart-library-directive-elided
    trivial: false
    nuance: >-
      Visibility nuance (explicitly addressed): Dart library boundary +
      leading-underscore privacy is the unit of `private` for top-level
      declarations; here NO declaration in this file starts with `_` (every
      class is `OpV2`, `HeadVariable`, etc., public), so eliding `library;`
      changes no visibility. C# privacy is per-type (`public`/`internal`/etc.)
      and the codegen stage decides those modifiers on the target classes; the
      `library;` directive itself contributes no member-level semantics to
      preserve.
  - construct_key: dart.abstract_class.empty_marker_base_non_sealed_implemented
    source_form: >-
      "abstract class OpV2 {}" implemented by every v2 opcode class via
      `class X implements OpV2 { ... }` (no extends, no shared state/members);
      doc comment says it exists "to distinguish them from v1 Op".
    target_decision: >-
      Model `OpV2` as a C# interface `IOpV2` (empty marker interface), each v2
      opcode class implementing it. Do NOT emit it as a C# `abstract class`
      base: Dart `implements` here is implicit-interface conformance with zero
      inherited state or behaviour, and a C# `abstract class` would consume
      the single base-class slot and imply an is-a relationship the Dart
      source does not have. The v1 `Op` (separate file) maps to its own
      `IOp` interface (per the existing opcodes.dart convspec), and the two
      interfaces are deliberately disjoint to preserve the v1/v2 distinction
      the doc comment calls out. CRITICAL: do NOT add `sealed` / exhaustiveness
      — see nuance.
    idiom_id: null
    research_finding_id: rf-dart-abstract-marker-to-csharp-interface
    trivial: false
    nuance: >-
      Exhaustiveness nuance (explicitly addressed, not glossed): Dart
      `abstract class OpV2` is NOT declared `sealed`, so Dart provides NO
      compiler exhaustiveness guarantee over OpV2's subtypes; the v2
      interpreter switching on opcode kind must already have a default /
      fallback path. Converting to a C# `sealed`-style closed hierarchy or
      pattern-match exhaustiveness would manufacture a guarantee the source
      never had and could mask an unhandled-opcode bug. Reference semantics:
      OpV2 subtypes are Dart classes = heap reference objects with identity;
      targets must be C# `class` (reference type), never `struct`, since the
      interpreter stores/queues and pattern-switches op instances by
      reference. Separateness from v1 `Op`/`IOp` is intentional (the doc
      comment makes it normative): no shared base, no implicit conversion
      between `IOp` and `IOpV2`.
  - construct_key: dart.data_class.final_fields_positional_and_named_required_ctor (v2 opcode IR node)
    source_form: >-
      Repeated v2 shape: `class HeadVariable implements OpV2 { final int
      varIndex; final bool isReader; HeadVariable(this.varIndex, {required
      this.isReader}); ... }`. Variants: (varIndex, {required isReader}) —
      HeadVariable / UnifyVariable / SetVariable; (varIndex, argSlot,
      {required isReader}) — GetVariable / GetValue / PutVariable;
      (varIndex) — Unknown (no isReader). All fields `final`.
    target_decision: >-
      Each becomes a C# reference `class` (NOT record struct) implementing
      IOpV2, with get-only properties initialised from a constructor that
      mirrors the Dart parameter shape: positional `this.field` -> C#
      positional ctor parameter; Dart `{required this.isReader}` -> a C#
      constructor parameter `bool isReader` (no optional default). Whether
      the codegen surfaces `isReader` as named-only at the call site is a
      codegen choice; semantically it must be (a) required and (b) callable
      by name `isReader: true` to faithfully preserve every Dart call site.
      Multiple positional params keep declaration order. Classes with no
      fields do not occur in this file (every v2 op has at least varIndex).
      Dart `final` => C# get-only auto-property (compile-time immutability),
      not a writable field.
    idiom_id: null
    research_finding_id: rf-dart-required-named-param-to-csharp-required-arg
    trivial: false
    nuance: >-
      Required-named nuance (explicitly addressed): Dart `{required
      this.isReader}` is a named parameter that is mandatory at every call
      site (omitting it is a compile error). C# has no syntactic "required
      named parameter" — the faithful mapping is a regular constructor
      parameter (with no default), which forces the caller to supply a value;
      C# named-argument call syntax (`new HeadVariable(varIndex, isReader:
      true)`) preserves the call shape. Do NOT use a defaulted optional
      parameter (would silently relax mandatoriness — semantic drift).
      Value-vs-reference: must remain reference classes (identity-bearing IR
      nodes the v2 interpreter holds in instruction lists / continuations);
      a record struct would change equality to structural value-equality and
      introduce copy semantics on every queue/store.
  - construct_key: dart.bool.field
    source_form: >-
      "final bool isReader;" on HeadVariable / GetVariable / GetValue /
      UnifyVariable / PutVariable / SetVariable — a mode flag selecting
      reader vs writer behaviour.
    target_decision: >-
      Dart `bool` maps to C# `bool` (System.Boolean) — both are two-valued
      true/false primitives with identical semantics. Emit as a get-only
      `bool` auto-property. The field is non-nullable (no `?`) in both
      sides under enabled nullable context.
    idiom_id: null
    research_finding_id: rf-dart-bool-to-csharp-bool
    trivial: false
    nuance: >-
      Value-vs-reference: Dart `bool` is a primitive-with-class-wrapper
      semantic (true/false only — no `null` here); C# `bool` is a value type
      (System.Boolean). Storing inside a reference class (IR node) is fine;
      no boxing concern because the field lives directly on the class.
      Boolean operators (`?:` in `isReader ? 'head_reader' : 'head_writer'`,
      etc.) behave identically in both languages.
  - construct_key: dart.int.fixed_width_index_and_arity_field
    source_form: >-
      "final int varIndex; final int argSlot;" — all `int` fields are clause
      variable indices or argument register slots.
    target_decision: >-
      Map Dart `int` to C# `long` (System.Int64), NOT C# `int`/Int32. Dart
      native `int` is a signed 64-bit integer (-2^63..2^63-1); C# `int` is
      only 32-bit. Although every value here is a small bounded
      index/slot/arity that fits in Int32 in practice, the SPEC decision is
      the type-faithful mapping `long` so the codegen baseline cannot
      silently narrow Dart 64-bit integer semantics. Codegen MAY, with an
      explicit recorded per-field justification (range provably bounded),
      down-map a specific field to `int`; absent that justification the
      default is `long`. No arithmetic, no bit operations, and no
      overflow-sensitive expressions occur in this file (pure field storage +
      string interpolation), so checked/unchecked and shift-sign behaviour
      are not exercised here.
    idiom_id: null
    research_finding_id: rf-dart-int-to-csharp-long-width
    trivial: false
    nuance: >-
      Integer-width nuance (explicitly addressed, MANDATORY for v2 opcode
      file): Dart int (native) = 64-bit two's-complement signed; C# int =
      32-bit signed; C# long = 64-bit signed; C# uint = 32-bit UNSIGNED.
      Faithful width = `long`. uint is rejected: indices are conceptually
      non-negative but Dart models them as signed int and no unsigned
      semantics are relied on; using `uint` would diverge from the source
      type AND change overflow behaviour (uint wraps around 0/2^32 whereas
      signed int saturates with negative-result semantics on the .NET checked
      side). There is NO bitwise op, NO shift (`>>`, `>>>`, `<<`), NO
      arithmetic and NO overflow path in opcodes_v2.dart (fields are inert
      storage + interpolation), so the well-known signed-shift / overflow /
      checked-context hazards do not arise in THIS file and are deliberately
      not asserted; they belong to files that compute on these ids, not to
      this v2 opcode definition file. (This v2 file is a unification of v1
      writer/reader opcodes via an isReader flag; the unification adds no
      arithmetic — it adds a bool, not a bitfield.)
  - construct_key: dart.getter_expression_body.string_ternary
    source_form: >-
      "String get mnemonic => isReader ? 'head_reader' : 'head_writer';"
      (HeadVariable) and the analogous getters on GetVariable
      ('get_reader_variable'/'get_writer_variable'), GetValue, UnifyVariable
      ('unify_reader'/'unify_writer'), PutVariable ('put_reader'/'put_writer'),
      SetVariable ('set_reader'/'set_writer'); plus
      "String get mnemonic => 'unknown';" on Unknown.
    target_decision: >-
      Each Dart getter `String get mnemonic => expr;` maps to a C# read-only
      expression-bodied property `string Mnemonic => expr;` (NOT a method,
      because there are no parameters and the Dart source uses getter
      syntax). The ternary `isReader ? 'reader' : 'writer'` maps to the C#
      conditional `IsReader ? "reader" : "writer"` with the exact same string
      literals (so the textual mnemonic is byte-identical to the Dart
      output). Unknown's `=> 'unknown'` maps to `=> "unknown";`. PascalCase
      casing for the property name (`Mnemonic`) is a codegen-stage convention.
    idiom_id: null
    research_finding_id: rf-dart-getter-to-csharp-property
    trivial: false
    nuance: >-
      Getter-vs-method nuance (explicitly addressed): Dart `T get name => e;`
      is a property-style read-only accessor (no parens at call sites). C#
      read-only properties are also paren-less at call sites; mapping to a
      method would change call-site syntax (`Mnemonic()` vs `Mnemonic`) — a
      faithful conversion preserves the property style. The expression body
      `=> expr;` exists in both languages with identical semantics (eager
      evaluation each call; no memoisation). No null-safety concern — both
      sides return non-nullable `string`/`String`.
  - construct_key: dart.tostring_override.string_interpolation
    source_form: >-
      "@override String toString() => '$mnemonic($varIndex)';" (HeadVariable,
      UnifyVariable, SetVariable, Unknown) and
      "@override String toString() => '$mnemonic(X$varIndex, A$argSlot)';"
      (GetVariable, GetValue, PutVariable).
    target_decision: >-
      Each `@override String toString()` becomes an `override string
      ToString()` of System.Object.ToString. Dart string interpolation
      `'$mnemonic($varIndex)'` becomes a C# interpolated string
      `$"{Mnemonic}({VarIndex})"`. The `X` / `A` prefix literals are
      preserved verbatim so debug output is byte-identical to the Dart
      original. Note `$mnemonic` is the property getter defined above, not a
      field — interpolation invokes the getter once per ToString call (same
      as Dart).
    idiom_id: null
    research_finding_id: rf-dart-tostring-interp-to-csharp-tostring-interp
    trivial: false
    nuance: >-
      toString nuance (explicitly addressed): Dart `toString()` override maps
      to overriding `object.ToString()` (NOT a C# extension method, which
      cannot override a virtual). Interpolation: Dart `$id`/`${expr}` -> C#
      `{id}`/`{expr}`; numeric interpolation for `varIndex`/`argSlot` (long)
      uses invariant culture in C# interpolated strings by default (no `:N`
      / locale formatter is applied), and Dart `int.toString()` is also
      culture-invariant, so output text is stable across both. The classes
      ALL define this toString (no inheritance fallback case in this file).
conversion_units:
  - "(elide) library; — no C# emission"
  - "interface IOpV2 (empty, non-sealed marker — NO exhaustiveness; disjoint from IOp)"
  - "class HeadVariable : IOpV2 (long VarIndex, bool IsReader; ctor (varIndex, isReader); string Mnemonic => IsReader ? \"head_reader\" : \"head_writer\"; override ToString => $\"{Mnemonic}({VarIndex})\")"
  - "class GetVariable : IOpV2 (long VarIndex, long ArgSlot, bool IsReader; ctor (varIndex, argSlot, isReader); string Mnemonic => IsReader ? \"get_reader_variable\" : \"get_writer_variable\"; override ToString => $\"{Mnemonic}(X{VarIndex}, A{ArgSlot})\")"
  - "class GetValue : IOpV2 (long VarIndex, long ArgSlot, bool IsReader; ctor (varIndex, argSlot, isReader); string Mnemonic => IsReader ? \"get_reader_value\" : \"get_writer_value\"; override ToString => $\"{Mnemonic}(X{VarIndex}, A{ArgSlot})\")"
  - "class UnifyVariable : IOpV2 (long VarIndex, bool IsReader; ctor (varIndex, isReader); string Mnemonic => IsReader ? \"unify_reader\" : \"unify_writer\"; override ToString => $\"{Mnemonic}({VarIndex})\")"
  - "class PutVariable : IOpV2 (long VarIndex, long ArgSlot, bool IsReader; ctor (varIndex, argSlot, isReader); string Mnemonic => IsReader ? \"put_reader\" : \"put_writer\"; override ToString => $\"{Mnemonic}(X{VarIndex}, A{ArgSlot})\")"
  - "class SetVariable : IOpV2 (long VarIndex, bool IsReader; ctor (varIndex, isReader); string Mnemonic => IsReader ? \"set_reader\" : \"set_writer\"; override ToString => $\"{Mnemonic}({VarIndex})\")"
  - "class Unknown : IOpV2 (long VarIndex; ctor (varIndex); string Mnemonic => \"unknown\"; override ToString => $\"unknown(X{VarIndex})\")"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-library-directive-elided — `library;` has no C# counterpart

- Deep analysis: `library;` is an unnamed library directive declaring this
  file is its own library compilation unit. It carries no member-level
  semantics; with no leading-underscore declarations in this file, it
  contributes no privacy boundary the conversion must preserve.
- Authoritative Dart: WebFetch `https://dart.dev/language/libraries`
  (Dart official). Verbatim relevant text: "Use the `library` directive to
  explicitly mark a file as a library and provide a library-level
  documentation comment or metadata annotation" — purely a marker /
  doc-attachment point, no semantic content for code generation.
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/namespace`
  — C# has no file-level library directive; compilation units are grouped
  by `namespace` only, and namespace selection is a project/codegen
  decision, not a per-file token.
- Conclusion: elide. The codegen stage will choose the C# namespace at
  assembly level; no token in this Dart file translates to any C# emission.
  Authoritative both sides; no escalation.

### rf-dart-abstract-marker-to-csharp-interface — empty marker base (v2)

- Deep analysis: `abstract class OpV2 {}` has no members, is only used via
  `implements OpV2`, and is NOT declared `sealed`. The doc comment makes
  the v1/v2 separation explicit ("to distinguish them from v1 Op"), so
  `OpV2` must remain a distinct type from `Op`.
- Authoritative Dart: WebFetch `https://dart.dev/language/class-modifiers`
  (Dart official). Verbatim: an abstract class without `sealed` does not
  give exhaustiveness; only `sealed` does — "The compiler is aware of any
  possible direct subtypes because they can only exist in the same
  library. This allows the compiler to alert you when a switch does not
  exhaustively handle all possible subtypes." OpV2 is not sealed => no such
  guarantee in the source.
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/interface`
  — empty marker interfaces are the idiomatic C# expression of an empty
  Dart `abstract class` used only via `implements`. Conclusion: emit `IOpV2`
  as a plain (non-sealed) marker interface, each v2 opcode a reference
  class implementing it. Do NOT manufacture a closed/exhaustive hierarchy.
  Disjoint from `IOp` (per the existing opcodes.dart convspec).
  Authoritative both sides; no escalation.

### rf-dart-required-named-param-to-csharp-required-arg — `{required this.isReader}`

- Deep analysis: `{required this.isReader}` is a named initialising-formal
  marked `required`, i.e. omitting `isReader:` at any call site is a Dart
  compile error. The value is then bound directly to the `final bool
  isReader` field.
- Authoritative Dart: WebFetch `https://dart.dev/language/functions`
  (Dart official). Verbatim: "Use `required` to indicate that a named
  parameter is mandatory" — non-optional, no default value permitted.
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-7.2/non-trailing-named-arguments`
  and the named-and-optional-arguments family on Microsoft Learn — C# has
  no syntactic "required named parameter", but a regular constructor
  parameter with no default forces the caller to supply a value, and C#
  named-argument call syntax (`new HeadVariable(varIndex, isReader: true)`)
  preserves the exact call shape used in Dart.
- Conclusion: map to a regular C# constructor parameter (no default).
  Reject any defaulted optional parameter (would silently relax
  mandatoriness — semantic drift, FR-013 territory if codegen ever tried
  it). Authoritative both sides; no escalation.

### rf-dart-bool-to-csharp-bool — boolean field & mode flag

- Deep analysis: `final bool isReader;` is a two-valued mode flag selecting
  reader vs writer behaviour for each unified opcode; null is not a valid
  value (no `?`).
- Authoritative Dart: WebFetch `https://dart.dev/language/built-in-types`
  (Dart official). Verbatim: "Dart has a type named `bool`. Only two
  objects have type bool: the Boolean literals `true` and `false`, which
  are both compile-time constants."
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/bool`
  (Microsoft Learn). Verbatim: "`bool` is an alias of the .NET
  `System.Boolean` structure type that represents a Boolean value, which
  can be either `true` or `false`."
- Conclusion: Dart `bool` ⇔ C# `bool` (System.Boolean), get-only
  auto-property; identical truthiness semantics; no boxing concern when
  stored as a property on a reference class. Authoritative both sides; no
  escalation.

### rf-dart-int-to-csharp-long-width — integer width fidelity (v2 opcode)

- Deep analysis: every `int` field in this file is a clause variable index
  or argument register slot. All fit Int32 in practice, but the source
  TYPE is Dart `int`, and this is a v2 opcode file where width fidelity is
  load-bearing for the surrounding interpreter contract.
- Authoritative Dart: WebFetch `https://dart.dev/language/built-in-types`
  (Dart official). Verbatim: "Integer values no larger than 64 bits,
  depending on the platform. On native platforms, values can be from -2^63
  to 2^63 - 1."
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types`
  (Microsoft Learn). Verbatim table: `int` = "-2,147,483,648 to
  2,147,483,647 / Signed 32-bit integer / System.Int32"; `long` =
  "-9,223,372,036,854,775,808 to 9,223,372,036,854,775,807 / Signed 64-bit
  integer / System.Int64"; `uint` = "0 to 4,294,967,295 / Unsigned 32-bit
  integer / System.UInt32".
- Conclusion: type-faithful mapping is Dart `int` ⇒ C# `long`. C# `int`
  would narrow 64-bit Dart semantics; C# `uint` would change signedness
  AND overflow behaviour. This file performs NO arithmetic, NO
  bitwise/shift ops (no `>>`, `>>>`, `<<`, `&`, `|`, `^`) and has NO
  overflow path (fields are inert storage + string interpolation via the
  mnemonic getter), so signed-shift / checked-vs-unchecked / overflow
  hazards are not exercised here and are deliberately not asserted; they
  belong to consumers (the v2 interpreter / register allocator the doc
  comment mentions as a future optimisation). Codegen may down-map an
  individual provably-bounded field to `int` only with a recorded
  per-field justification; default = `long`. Authoritative both sides; no
  escalation.

### rf-dart-getter-to-csharp-property — `String get mnemonic => ...;`

- Deep analysis: every v2 opcode exposes `String get mnemonic` — a
  parameter-less getter returning either a constant string (`'unknown'`)
  or a ternary on `isReader`. No side effects, no parameters.
- Authoritative Dart: WebFetch `https://dart.dev/language/methods`
  (Dart official) — getters are property-style accessors invoked without
  parentheses; arrow-body `=> expr;` is the standard short form.
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/properties`
  (Microsoft Learn) — C# expression-bodied read-only properties (`T
  Name => expr;`) are the direct counterpart, also paren-less at call
  sites.
- Conclusion: map to a C# read-only expression-bodied property — NOT to a
  parameterless method, which would change call-site syntax. The ternary
  body translates verbatim (Dart and C# `?:` have identical semantics with
  identical operator precedence relative to `=>`). Authoritative both
  sides; no escalation.

### rf-dart-tostring-interp-to-csharp-tostring-interp — debug toString

- Deep analysis: every v2 opcode class overrides `String toString()` with
  an interpolated debug string that references the `mnemonic` getter plus
  field values. Output is used for debug logging and is treated as
  byte-identical to v1 opcode output where the same mnemonic was emitted.
- Authoritative Dart: WebFetch
  `https://api.dart.dev/stable/dart-core/Object/toString.html` (Dart
  official api.dart.dev). Verbatim relevant text: "A string representation
  of this object. Some classes have a default textual representation,
  often paired with a static `parse` function (like `int.parse`). These
  classes will provide the textual representation as their string
  representation." — confirms `toString()` is the canonical
  string-representation hook; overriding it is the idiomatic approach.
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated`
  (Microsoft Learn) — C# interpolated strings `$"...{expr}..."` are the
  direct counterpart of Dart `'...$expr...'` / `'...${expr}...'`. Numeric
  interpolation uses invariant culture by default (no `:N`/`:C` formatter
  applied to a bare `{long}` placeholder), and Dart `int.toString()` is
  likewise culture-invariant, so output text is stable across both.
- Conclusion: override `object.ToString()` and use a C# interpolated
  string; preserve literal punctuation (`X`, `A`, `(`, `)`, `,`, space)
  verbatim. Do NOT use a C# extension method (cannot override a virtual).
  Authoritative both sides; no escalation.

## Notes

- No Stream/Future/async, no isolates, no `late`/`mixin`/`extension`, no
  generics-with-bounds, no `sealed` classes, no `enum` (the v1/v2 boundary
  is type-based via `OpV2`, not an enum of opcode codes — so the "enums vs
  const int codes" nuance flagged in the task brief is ABSENT in this
  file: there are no opcode integer codes here, opcode identity is
  represented by class type), no `const` collections, no bitwise / shift /
  arithmetic, no overflow path — those well-known nuances are ABSENT and
  are correctly not asserted (asserting an absent nuance would be noise).
- v1/v2 separation is load-bearing: `IOpV2` MUST be a distinct interface
  from `IOp`. The doc comment ("v2 instructions implement this to
  distinguish them from v1 Op") makes this normative.
- The non-sealed `abstract class OpV2` is the second load-bearing semantic
  decision: the conversion must NOT introduce C# exhaustiveness/`sealed`
  semantics the Dart source never had (would mask unhandled-opcode bugs in
  the v2 interpreter).
- The `required` named parameter is the third load-bearing decision: the
  C# mapping must preserve mandatoriness (regular non-defaulted parameter),
  never a defaulted optional parameter.
- Trivial / non-construct elements: file/doc comments (`///`, `//`) map
  mechanically to C# XML-doc / `//` comments (trivial, no research). The
  large banner `// ===…===` separators are pure decoration with no semantic
  content. The `@override` annotation itself is subsumed by `override` on
  the ToString construct.
- Zero escalations: every non-trivial construct resolved from authoritative
  Dart (dart.dev / api.dart.dev) and/or .NET (learn.microsoft.com) official
  documentation; no undecidable construct, no idiom/research conflict.
