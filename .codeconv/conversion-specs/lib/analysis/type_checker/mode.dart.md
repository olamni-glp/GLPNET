# Conversion Spec — lib/analysis/type_checker/mode.dart

```yaml
schema_version: 1
source_path: lib/analysis/type_checker/mode.dart
source_sha256: 48ca1f3517f5fd668631dff7c4b48b31567276ca330644b7c24892427aaa8e78
target_code_unit: lib/analysis/type_checker/mode.cs
constructs:
  - construct_key: dart.enhanced_enum.with_static_const_aliases_getters_tostring
    source_form: >-
      enum Mode { output, input; static const consume = Mode.input;
      static const produce = Mode.output; Mode get dual {...} Mode get flip => dual;
      @override String toString() {...} }
    target_decision: >-
      Emit a plain C# `enum Mode { Output, Input }` (no underlying-value overrides;
      keep two members so default `(Mode)0` == Output, matching Dart declaration
      order where output is listed first / is the GLP default). Behavioural and
      named-alias surface that Dart attaches *to the enum itself* moves to a
      sibling `static class ModeExtensions` (extension members) plus public
      static readonly alias fields on a `static class ModeAliases` (or const-like
      static members): `Consume = Mode.Input`, `Produce = Mode.Output`. The
      `dual`/`flip` getters become extension methods `Dual(this Mode)` /
      `Flip(this Mode)`; `toString()` becomes an extension `AsModeString(this Mode)`
      (do NOT override System.Enum.ToString via extension — extension cannot
      override; call sites that relied on Dart `toString()` are redirected to the
      helper, and a `[EnumMember]`-free explicit map is used, not enum member
      names, because Dart returns lowercase 'output'/'input').
    idiom_id: null
    research_finding_id: dart-enhanced-enum-to-csharp-enum-plus-extensions
    nuance: >-
      Dart enhanced enums may carry getters, methods, static const aliases and a
      toString override directly on the enum; C# enums are integral value types
      that CANNOT declare methods/getters/static members or override ToString
      (Microsoft Learn: "You can't define a method inside the definition of an
      enumeration type. To add functionality to an enumeration type, create an
      extension member."). Value-vs-reference: both Dart enum and C# enum are
      value types compared by identity/value, so equality semantics are
      preserved. The static const aliases `consume`/`produce` are compile-time
      constant references to enum instances; C# `const` cannot bind to an enum
      *member expression* in all the same positions, so they become
      `public static readonly`/`public const Mode` alias members on a helper
      class, not enum members (adding them as enum members would create
      duplicate-value members and pollute `Enum.GetNames`/round-trip).
  - construct_key: dart.enum.exhaustive_switch_no_default
    source_form: >-
      switch (this) { case Mode.output: return Mode.input;
      case Mode.input: return Mode.output; }  (used in `dual` and `toString`)
    target_decision: >-
      Convert to a C# switch expression with an explicit arm per member and a
      discard arm that throws ArgumentOutOfRangeException (or
      UnreachableException) — Dart's exhaustiveness is compiler-guaranteed over a
      closed enum; C# switch over an enum is NOT exhaustiveness-checked at
      compile time and a defaulted/unhandled integral value is reachable via
      casts, so the spec mandates an explicit unreachable/throw arm to preserve
      the "total function" semantics Dart guarantees.
    idiom_id: null
    research_finding_id: dart-exhaustive-enum-switch-to-csharp-switch-expression
    nuance: >-
      Null-safety/totality mapping: Dart's exhaustive switch on an enum is a
      total function the analyzer enforces; C# does not enforce enum-switch
      exhaustiveness, and an out-of-range cast `(Mode)99` is representable. The
      conversion preserves totality by adding an explicit throwing default arm
      rather than silently falling through.
  - construct_key: dart.top_level_function.pure
    source_form: "Mode combineMode(Mode parent, Mode embedded) { if (parent == embedded) return Mode.output; else return Mode.input; }"
    target_decision: >-
      C# has no top-level functions in this codebase convention; emit as a
      `public static Mode CombineMode(Mode parent, Mode embedded)` on a static
      host class (e.g. `static class Mode` is taken by the enum name — host it on
      `static class ModeOps` or as a static method of `ModeExtensions`). Body is
      a direct equality test on the enum value type (==) returning Output when
      equal else Input — semantics preserved exactly (XOR/involution property
      noted in source doc-comment carries over unchanged).
    idiom_id: null
    research_finding_id: dart-top-level-function-to-csharp-static-method
    nuance: >-
      Dart allows library-level (top-level) functions; C# requires every method
      to live in a type. The function is pure and uses value-type enum equality
      (`==`), which is identical in C# (enum `==` is an integral comparison), so
      no reference-vs-value hazard. Naming collision: the natural host name
      `Mode` is occupied by the enum; the spec records the host must be a
      distinctly-named static class to avoid the C# type-name clash.
conversion_units:
  - enum Mode { Output, Input } (value type; Output first to preserve GLP default)
  - static class ModeAliases : Consume = Mode.Input, Produce = Mode.Output
  - static class ModeExtensions : Dual(this Mode), Flip(this Mode) => Dual, AsModeString(this Mode)
  - static class ModeOps : static Mode CombineMode(Mode parent, Mode embedded)
escalations: []
```

## Rationale & Research Provenance

### dart-enhanced-enum-to-csharp-enum-plus-extensions

**Deep analysis.** `Mode` is a Dart *enhanced enum*: two instances (`output`, `input`),
two static-const aliases (`consume`→`input`, `produce`→`output`), an instance
getter `dual`, a forwarding getter `flip => dual`, and an overridden
`toString()` returning lowercase `'output'`/`'input'`. Both Dart enums and C#
enums are value types compared by value/identity, so equality and the
involution/XOR property of `combineMode` are preserved with no boxing concern.
The semantic problem is purely *where the behaviour and aliases can live*.

**Research (authoritative).**
- Verbatim query / source: WebFetch `https://dart.dev/language/enums` — Dart
  official docs confirm enhanced enums "allow you to declare classes with
  fields, methods, and const constructors", support getters and instance
  methods, and that Dart enums are value types compared by identity.
- Verbatim query / source: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/enum`
  — Microsoft Learn, authoritative: *"An enumeration type ... is a value type
  defined by a set of named constants of the underlying integral numeric type."*
  and decisively *"You can't define a method inside the definition of an
  enumeration type. To add functionality to an enumeration type, create an
  extension member."*

**Conclusion.** The enum *values* map 1:1 to a plain C# `enum Mode`. Everything
Dart attaches to the enum body (getters, `toString`, static const aliases) must
relocate: behaviour → extension methods; aliases → static helper members. The
`toString()` override cannot be expressed by overriding `System.Enum.ToString`
(extensions can't override virtuals and the required output is lowercase, not
the member name), so it becomes an explicit-mapped helper `AsModeString`. Member
order keeps `Output` first so the C# default `(Mode)0` equals `Output`, matching
the Dart source's documented "(default, no ? marker)" semantics. Corroboration
(non-authoritative, not sole basis): common .NET guidance to model
behaviour-rich enums via extension classes — used only as confirmation of the
authoritative Microsoft Learn directive.

### dart-exhaustive-enum-switch-to-csharp-switch-expression

**Deep analysis.** `dual` and `toString` switch over `this` with one `case` per
member and no `default`. Dart's analyzer treats this as exhaustive (total) over
the closed enum.

**Research (authoritative).** Microsoft Learn enum reference (same WebFetch
above) documents that C# enums are integral value types and out-of-range
integral values are representable (`var c = (Season)4;` prints `4`) — i.e. a C#
`switch` over an enum is **not** exhaustiveness-guaranteed and an unmapped value
is reachable. Therefore the total-function guarantee Dart provides is preserved
only by emitting an explicit throwing discard arm.

**Conclusion.** Convert each switch to a C# switch *expression* with explicit
`Mode.Output`/`Mode.Input` arms plus a discard `_ =>` arm that throws
(`ArgumentOutOfRangeException`/`UnreachableException`). This preserves totality
and surfaces corrupt enum values loudly rather than silently.

### dart-top-level-function-to-csharp-static-method

**Deep analysis.** `combineMode` is a pure top-level (library-level) function;
its body relies only on value-type enum `==`.

**Research (authoritative).** Microsoft Learn enum reference confirms enum
equality is an integral comparison of value types (explicit conversions between
enum and underlying integral type; comparison operators defined) — so Dart `==`
on enum values maps exactly to C# enum `==` with no reference-identity hazard.
C# (and this codebase's convention) has no library-level functions, so it must
become a `public static` method on a host type.

**Conclusion.** Emit as a `public static Mode CombineMode(Mode parent, Mode
embedded)` on a distinctly-named static host class (`ModeOps`) — the natural
name `Mode` is occupied by the enum type, a C# name collision the codegen stage
must avoid. Logic (`parent == embedded ? Output : Input`, the documented XOR /
involution property) carries over verbatim.

### Trivial / non-construct elements

File header comments and doc-comments are non-code; they map to C# XML-doc /
`//` comments mechanically (trivial, no research required).
