> Conversion-spec artifact for lib/compiler/error.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/compiler/error.dart
source_sha256: 48c26f84e7f527b0ac9d6ecebc266bfea0cb49964bf50a7f1dbcfe2f424070a4
target_code_unit: lib/compiler/error.cs
constructs:
  - construct_key: dart.enum.plain_named_constants
    source_form: >-
      "enum ErrorCategory { lexical, syntax, semantic, codegen }" — plain
      (non-enhanced) Dart enum with four named constants, no fields, no
      constructor, no methods. Referenced as `ErrorCategory.lexical` etc.
      and stringified via `category.toString().split('.').last` to recover
      the bare member name.
    target_decision: >-
      Emit a C# `enum ErrorCategory { Lexical, Syntax, Semantic, Codegen }`
      with the default `int` underlying type and default ordinal values
      (0..3). Do NOT apply `[Flags]` (these categories are mutually
      exclusive — exactly one phase per error — and no bitwise combination
      is used in this file). Member names are PascalCased per .NET naming
      conventions (Lexical/Syntax/Semantic/Codegen). The Dart idiom
      `category.toString().split('.').last` (which yields "lexical" /
      "syntax" / ...) is replaced inside `ToString` by a direct
      `category.ToString()` call on the C# enum, which by default yields
      the member-name token ("Lexical", etc.); the spec deliberately
      records that the rendered casing differs from the Dart output (see
      nuance).
    idiom_id: null
    research_finding_id: rf-dart-plain-enum-to-csharp-enum
    nuance: >-
      Value-vs-reference (load-bearing): Dart enums are reference-typed
      heap objects; C# enums are *value types* (per Microsoft Learn
      "An enumeration type (or enum type) is a value type defined by a set
      of named constants"). Boxing therefore occurs when an `ErrorCategory`
      is stored in `object?` / used non-generically, but for the field
      `final ErrorCategory? category` the value-type↔reference-type
      distinction is captured by the nullable annotation (see next
      construct). No `.name` extension call exists in this source — only
      `toString().split('.').last`, which the conversion replaces with the
      enum's native `ToString()` (member-name token). C# `enum.ToString()`
      returns the member identifier as declared (e.g. "Lexical"); Dart
      `toString()` returns "ErrorCategory.lexical" and the code strips the
      prefix. So the *post-strip* Dart output is "lexical" (lowercase)
      while the C# default is "Lexical" (PascalCase). The conversion
      preserves bracketed-prefix shape "[Lexical] " but accepts the case
      delta as an intentional .NET-idiomatic rendering; an explicit
      `[Description("lexical")]` + reflection lookup is rejected as
      over-engineering for a debug string, and the codegen MAY (recorded
      future option) emit a small `ToLowerInvariant()` if byte-identical
      lowercase output is required — but the spec default is the
      PascalCase member name. Implicit-zero conversion hazard (Microsoft
      Learn): `(ErrorCategory)0` is valid and equals `Lexical`; this is
      acceptable because `lexical` is genuinely the first/default category
      in source order, matching Dart enum index 0.
  - construct_key: dart.nullable_enum_field.QuestionMark
    source_form: "final ErrorCategory? category;"
    target_decision: >-
      Map to C# `ErrorCategory?` (i.e. `Nullable<ErrorCategory>`) — a
      `System.Nullable<T>` over the value-typed enum. Get-only property
      initialised once in the constructor. The Dart source assigns
      `category = phase != null ? _categoryFromPhase(phase) : null`, which
      becomes a C# `category = phase != null ? CategoryFromPhase(phase) :
      null` in the constructor body assigning the get-only auto-property
      via init-only or constructor-set semantics (codegen's call;
      get-only-from-ctor is sufficient).
    idiom_id: null
    research_finding_id: rf-dart-nullable-enum-to-csharp-nullable-of-enum
    nuance: >-
      Null-safety mapping (critical because of value-type semantics): Dart
      `ErrorCategory?` is the nullable union of the enum reference type
      with `null`. C# enums are value types (cannot hold `null` directly);
      the faithful mapping is `Nullable<ErrorCategory>` (`ErrorCategory?`),
      NOT the enum bare. Using bare `ErrorCategory` with a sentinel "None"
      member is REJECTED because (a) Dart source has no sentinel and (b)
      it would conflate "no category" with a real category (and trigger
      Microsoft Learn's implicit-zero hazard). `Nullable<T>` is a struct
      wrapper; `.HasValue`/`.Value` access pattern is what the `if
      (category != null)` branch in `ToString()` becomes. No boxing under
      ordinary nullable use; boxing only occurs if the nullable enum is
      stored in `object?`, which does not happen in this file.
  - construct_key: dart.exception_class.implements_Exception_with_message_and_location
    source_form: >-
      "class CompileError implements Exception { final String message;
      final int line; final int column; final String? source; final
      ErrorCategory? category; CompileError(this.message, this.line,
      this.column, {this.source, String? phase}) : category = phase != null
      ? _categoryFromPhase(phase) : null; ... @override String toString()
      => ... }"
    target_decision: >-
      Emit a C# class `CompileError : Exception` (deriving from
      `System.Exception`, per Microsoft Learn "you can create your own
      exception class by deriving from the Exception class"). Do NOT model
      it as a plain class (loses `throw`/`catch` interop) and do NOT
      implement an `IException` interface (no such .NET idiom exists —
      .NET has no equivalent of Dart's `Exception` *interface*; the .NET
      contract for throwables is concrete inheritance from
      `System.Exception`). The Dart instance field `message` is mapped to
      base `Exception.Message` (set via the `: base(message)` constructor
      chain) so that `Message`, `ToString()`, and serialization behave
      idiomatically; `line`, `column`, `source`, `category` become extra
      get-only properties on the derived class. Name retained as
      `CompileError`: Microsoft Learn's "end the class name with the word
      'Exception'" rule is intentionally NOT applied here, because the
      Dart source explicitly named the type `CompileError` and the .NET
      "Exception" suffix is a *recommendation*, not a contract; renaming
      it (e.g. to `CompileException`) would diverge call-site identifiers
      and lose the source's chosen semantic ("compile-time error" as a
      domain term, separate from the runtime-exception colour). Reference
      semantics preserved (exceptions are reference types in both
      languages). See escalation below for the suffix decision: the
      spec's chosen default is "keep name", with a downstream-codegen
      option to rename only if Gabi later prefers the .NET-idiomatic
      suffix.
    idiom_id: null
    research_finding_id: rf-dart-implements-exception-to-csharp-derive-system-exception
    nuance: >-
      Exception base class choice (explicitly addressed): Dart's
      `Exception` is an *interface* ("an abstract interface … can only be
      implemented (not extended or mixed in)", api.dart.dev). .NET's
      `System.Exception` is a concrete base *class* (Microsoft Learn).
      There is no .NET interface-based throwable contract — every C#
      `throw`able is or derives from `System.Exception`. So Dart
      `implements Exception` → C# `: Exception` (inheritance). The Dart
      `message` field is *not* a base-interface member (Dart `Exception`
      has no `message` field in its contract), but Dart code conventionally
      exposes one; we route it to base `Exception.Message` (via
      `: base(message)`) so `Message` is set on the base for catch-site
      consumers and so the default `Exception.ToString()` would have
      something to print if our override were ever bypassed. Immutability:
      every Dart field is `final` → every C# property is get-only set
      from the constructor (Dart final field semantics preserved). Three
      common constructors (Microsoft Learn pattern: `()`, `(string)`,
      `(string, Exception)`) are NOT all emitted — only the single
      semantic ctor the source declares, because adding extras would
      manufacture an instantiation surface the Dart source does not have
      (FR-013 / spec-faithfulness). Null-safety: `String? source` →
      `string? source` under enabled NRT; `String? phase` parameter →
      `string? phase`. Reference semantics: `CompileError` is a reference
      class (Exception always is in .NET). No `Stream`/`Future`/async/
      isolate concerns (synchronous error-info container). Inner exception
      pattern (`Exception(message, Exception inner)`) is NOT applicable
      here — the Dart source has no wrapped-cause chaining.
  - construct_key: dart.named_optional_param_initialising_formal_plus_extra_named
    source_form: >-
      "CompileError(this.message, this.line, this.column, {this.source,
      String? phase}) : category = phase != null ? _categoryFromPhase(phase)
      : null;"  — positional initialising formals + named optionals where
      one named param (`phase`) is NOT a field but feeds an initializer-
      list expression for the `category` field.
    target_decision: >-
      Emit a single constructor `CompileError(string message, long line,
      long column, string? source = null, string? phase = null)` chaining
      `: base(message)`. The constructor body assigns `Line`, `Column`,
      `Source` from positional/optional args, and computes `Category` from
      `phase` exactly as Dart's initializer-list expression does:
      `Category = phase != null ? CategoryFromPhase(phase) : null;`.
      Optional named arguments in Dart become C# optional parameters with
      `= null` defaults; C# call-site `new CompileError("msg", 1, 2,
      phase: "lexer")` mirrors Dart's `CompileError("msg", 1, 2,
      phase: "lexer")` via C#'s named-argument call syntax. `phase` is
      DELIBERATELY not stored as a field (Dart source doesn't store it —
      only its derived `category` is kept); the C# constructor preserves
      that by computing-and-discarding.
    idiom_id: null
    research_finding_id: rf-dart-named-default-param-to-csharp-optional-arg
    nuance: >-
      Two-shape nuance: this is a *hybrid* constructor — three positional
      initialising-formals (`this.message`, `this.line`, `this.column`)
      AND two named optionals where one (`this.source`) is also an
      initialising-formal and the other (`phase`) is a plain named
      parameter consumed by the initializer list. C# has no
      "initialising-formal" sugar; the body must perform the assignments
      explicitly. Named-vs-optional: C# optional params are positional-
      with-defaults plus named-argument call syntax, faithfully covering
      both. Default `null` is a compile-time constant in both languages
      (no drift). `line` and `column` map to `long` per rf-dart-int-to-
      csharp-long-width (recurring idiom from opcodes.dart spec). Note
      the Dart source allows `line: 0` and even uses 0 as a "no source
      line to render" sentinel in the `toString()` branch (`if (line > 0
      && line <= lines.length)`); C# preserves this branch unchanged
      (Dart `int` 0 → C# `long` 0).
  - construct_key: dart.static_private_helper.switch_expression_to_nullable_enum
    source_form: >-
      "static ErrorCategory? _categoryFromPhase(String phase) { switch
      (phase) { case 'lexer': return ErrorCategory.lexical; case 'parser':
      return ErrorCategory.syntax; case 'analyzer': return
      ErrorCategory.semantic; case 'codegen': return
      ErrorCategory.codegen; default: return null; } }"
    target_decision: >-
      Emit a C# `private static ErrorCategory? CategoryFromPhase(string
      phase)` containing the same case dispatch. Codegen MAY choose either
      a classic `switch` statement (literal Dart shape) OR a C# switch
      expression `phase switch { "lexer" => ErrorCategory.Lexical, "parser"
      => ErrorCategory.Syntax, "analyzer" => ErrorCategory.Semantic,
      "codegen" => ErrorCategory.Codegen, _ => null }`. Spec records the
      switch-expression form as the .NET-idiomatic default (concise,
      expression-bodied, no fallthrough hazard) while permitting the
      statement form if the codegen stage prefers literal source shape.
      Privacy: Dart `_` (library-private) → C# `private` (class-private —
      .NET has no library-private equivalent inside a single class,
      adequate for a single-use helper).
    idiom_id: null
    research_finding_id: rf-dart-leading-underscore-privacy-to-csharp-private
    nuance: >-
      Privacy nuance: Dart's `_`-prefix is *library-private* (the
      compilation unit / Dart library), not class-private; C# `private` is
      class-scoped (tighter). Since `_categoryFromPhase` has no
      cross-file or cross-class consumer in this source (only used inside
      `CompileError`'s constructor initializer list), C# `private static`
      is a strictly-correct narrowing (no surface is exposed that was
      hidden in Dart). If a future cross-class call site is added during
      conversion of other compiler files, escalate at that point —
      narrowing must not break a then-existing caller. String matching:
      Dart and C# both compare string literals by value (`==` on strings
      is content equality in both — see opcodes.dart provenance for the
      Dart `String`/.NET `string` equivalence). No locale concern — these
      are ASCII tokens compared literally. Default `null` return preserves
      the `ErrorCategory?` type and the fall-through semantic.
  - construct_key: dart.override_tostring_with_branching_interpolation_and_string_repetition
    source_form: >-
      "@override String toString() { final categoryName = category != null
      ? '[${category.toString().split('.').last}] ' : ''; final loc =
      'Line $line, Column $column'; if (source != null) { final lines =
      source!.split('\\n'); if (line > 0 && line <= lines.length) { final
      sourceLine = lines[line - 1]; final pointer = ' ' * (column - 1) +
      '^'; return '$categoryName$message\\n$loc:\\n$sourceLine\\n$pointer';
      } } return '$categoryName$message at $loc'; }"
    target_decision: >-
      Emit `public override string ToString()` overriding
      `System.Object.ToString` (which `System.Exception` also overrides;
      our override further specialises it). Local variables `categoryName`,
      `loc`, `lines`, `sourceLine`, `pointer` become C# locals (`var` or
      explicit types — codegen's call). Body shape preserved branch-for-
      branch:
        1. `categoryName` ternary: `var categoryName = Category != null ?
           $"[{Category}] " : "";` — using C#'s default enum-to-string
           which yields the bare member name (no need for split-on-dot —
           C# enums stringify as the member identifier).
        2. `loc`: `var loc = $"Line {Line}, Column {Column}";`
        3. If `Source != null`: `var lines = Source.Split('\\n');` —
           Dart `String.split('\\n')` returns `List<String>` matching at
           every occurrence (api.dart.dev); C# `string.Split('\\n')` (or
           `Split(new[]{'\\n'})`) returns `string[]`. Codegen records
           `string[]` as the .NET-idiomatic counterpart; consumers index
           identically (`lines[line - 1]`). NRT: `Source!` (Dart bang) is
           replaced by C#'s null-flow narrowing inside the `if (Source !=
           null)` branch — `Source.Split(...)` is safe with the compiler
           seeing the prior null check; no `!` operator needed.
        4. `pointer` = Dart `' ' * (column - 1) + '^'`. Dart String
           supports `operator*` for repetition (api.dart.dev:
           "Creates a new string by concatenating this string with itself
           a number of times"). C# `string` does NOT overload `*`; the
           faithful counterpart is `new string(' ', (int)(Column - 1)) +
           "^"` (Microsoft Learn `String(Char, Int32)` ctor:
           "Initializes a new instance of the String class to the value
           indicated by a specified Unicode character repeated a specified
           number of times"). The narrowing `long → int` is necessary for
           the `String(char, int)` ctor signature; `column` is bounded by
           a real text column index so a checked cast is safe (no
           overflow path in this file).
        5. Return interpolated strings via C# `$"..."`. Newline literal
           `\\n` is identical in both languages (a single LF code unit).
      Override keyword preserves polymorphism over `Exception.ToString`.
    idiom_id: null
    research_finding_id: rf-dart-tostring-interp-to-csharp-tostring-interp
    nuance: >-
      toString nuance (explicitly addressed): the override hooks
      `System.Object.ToString` (the same virtual `Exception` itself
      overrides — Microsoft Learn: "This method overrides
      Object.ToString"). Our override REPLACES (not extends) the default
      `Exception.ToString` output (which would otherwise emit
      "CompileError: <message>\\n   at ..." with the stack trace per
      Microsoft Learn). This is INTENTIONAL: the Dart source's
      `toString()` produces a developer-facing diagnostic string with a
      source-line caret, NOT a stack-trace dump. The C# override
      preserves that semantic exactly; we deliberately do NOT call
      `base.ToString()` because that would prepend type-name + tail
      stack-trace and diverge from the Dart output byte-shape. Recorded
      consequence: a caller who wants both the diagnostic AND the stack
      trace must inspect `StackTrace` separately — same posture as Dart
      (where the developer prints the exception and the stack trace
      separately). Numeric interpolation: Dart `$line`/`$column` and C#
      `{Line}`/`{Column}` both use invariant integer ToString — output
      text is stable across locales. The `category.toString().split('.').
      last` Dart idiom is replaced by direct `Category` interpolation
      because C# enums don't include the type-name prefix in
      `ToString()`; the bracketed output `[Lexical]` is the C# result,
      vs Dart `[lexical]` — see rf-dart-plain-enum-to-csharp-enum nuance
      for the case-delta acknowledgement. Reference semantics: `lines`
      array is a reference; `sourceLine` is a string (value-equality
      reference type). String repetition: explicitly NOT reachable via
      C# operator overloading — `new string(char, int)` is the
      authoritative repeat-constructor (Microsoft Learn). Null-safety:
      every reference parameter the override touches (`Source`, `Category`)
      is explicitly null-checked before deref, mirroring Dart.
  - construct_key: dart.docblock_triple_slash
    source_form: >-
      "/// Error categories for compiler diagnostics" and "/// Compilation
      error with source location" — Dart triple-slash doc comments on the
      enum and the class.
    target_decision: >-
      Map to C# XML-doc comments `/// <summary>Error categories for
      compiler diagnostics</summary>` (and the class equivalent). Trivial
      mechanical mapping.
    idiom_id: null
    research_finding_id: null
    nuance: trivial
    trivial: true
  - construct_key: dart.line_comment.inline_after_enum_member
    source_form: >-
      "lexical,    // Invalid characters, unterminated strings" etc. — line
      comments documenting each enum member.
    target_decision: >-
      Map to C# `//` line comments (or, optionally, XML-doc per member);
      spec default = preserve as `//` line comments adjacent to the enum
      member declarations for byte-identical documentation shape. Trivial.
    idiom_id: null
    research_finding_id: null
    nuance: trivial
    trivial: true
conversion_units:
  - "enum ErrorCategory { Lexical, Syntax, Semantic, Codegen } (default int underlying, no [Flags], default ordinal values 0..3)"
  - "class CompileError : Exception (reference type, get-only Message via base, get-only Line/Column/Source/Category)"
  - "property: get-only string Message (chained to base Exception.Message via : base(message))"
  - "property: get-only long Line"
  - "property: get-only long Column"
  - "property: get-only string? Source"
  - "property: get-only ErrorCategory? Category"
  - "constructor: CompileError(string message, long line, long column, string? source = null, string? phase = null) : base(message) — assigns Line/Column/Source; sets Category = phase != null ? CategoryFromPhase(phase) : null"
  - "private static ErrorCategory? CategoryFromPhase(string phase) — switch expression mapping 'lexer'/'parser'/'analyzer'/'codegen' → Lexical/Syntax/Semantic/Codegen; default null"
  - "public override string ToString() — preserves Dart's diagnostic-with-caret shape; replaces (not extends) Exception.ToString default; uses new string(' ', (int)(Column - 1)) + \"^\" for the pointer; uses Source.Split('\\n') for line slicing"
  - "doc-comments → /// <summary>...</summary> on enum and class"
  - "inline // comments on enum members preserved"
escalations:
  - kind: undecidable
    construct_key: dart.exception_class.naming_suffix_convention
    detail: >-
      Microsoft Learn says "end the class name of the user-defined
      exception with the word 'Exception'" (so `CompileException`), but
      the Dart source explicitly chose the name `CompileError` (semantic:
      "compile-time error" as a domain term distinct from runtime
      exceptions). Spec default in this artifact = preserve source name
      `CompileError`; downstream codegen MAY rename to `CompileException`
      ONLY if Gabi explicitly prefers .NET-idiomatic suffix conformance
      over source-name fidelity. This is recorded as an escalation
      because the choice is a project-policy decision (source fidelity
      vs .NET convention), not a technical determination from the docs
      alone — both options are authoritative-supported and the file-
      local docs do not adjudicate.
    needs: >-
      Project policy decision from Gabi: keep `CompileError` (current
      spec default, source-faithful) or rename to `CompileException`
      (.NET naming-convention idiomatic). Decision should be recorded as
      a project-wide idiom because every Dart-named "*Error" exception
      class faces the same question.
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-plain-enum-to-csharp-enum — plain enum mapping

- Deep analysis: `ErrorCategory` is a plain Dart enum with four mutually
  exclusive named constants, no fields, no methods. Source-side stringification
  is `category.toString().split('.').last` which strips the `EnumName.` prefix
  to yield just the member name.
- Authoritative Dart: WebFetch `https://dart.dev/language/enums` (Dart
  official). Verbatim: "If you need to access the name of an enumerated value,
  such as `'blue'` from `Color.blue`, use the `.name` property"; the page
  documents plain-vs-enhanced enum shapes (the source uses plain).
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/enum`
  (Microsoft Learn). Verbatim: "An *enumeration type* (or *enum type*) is a
  *value type* defined by a set of named constants of the underlying integral
  numeric type." Also verbatim: "By default, the associated constant values of
  enum members are of type `int`. They start with zero and increase by one
  following the definition text order." And the `[Flags]` distinction:
  "[Flags] … apply the Flags attribute" — only when the enum represents bit
  combinations. The Dart enum here is NOT a flags-style choice (each error has
  exactly one category), so `[Flags]` is not applicable.
- Conclusion: Dart plain enum ⇔ C# plain enum with default `int` underlying,
  default ordinal values, no `[Flags]`. The case-delta (Dart member token
  `lexical` lowercase vs C# member token `Lexical` PascalCase) is recorded as
  an intentional .NET-idiomatic rendering of the bracketed prefix; if
  byte-identical lowercase output is required, codegen may add
  `.ToString().ToLowerInvariant()`, but the default preserves member-name
  casing per .NET conventions. Authoritative both sides; no escalation
  (separate escalation exists for the exception-suffix naming question, which
  is project-policy, not enum-mapping).

### rf-dart-nullable-enum-to-csharp-nullable-of-enum — `ErrorCategory?` field

- Deep analysis: the field `final ErrorCategory? category` holds either an
  enum member or `null`. The constructor's initializer-list computes it from
  `phase`.
- Authoritative .NET: same Microsoft Learn enum page used above: C# enums are
  value types; therefore `null` cannot be stored in the bare enum and the
  faithful nullable mapping is `System.Nullable<ErrorCategory>` (sugar
  `ErrorCategory?`). The implicit-zero hazard documented on the same page is
  recorded but does not apply here — the Dart source's first member
  (`lexical`) is genuinely the right semantic default for a freshly-cast
  `(ErrorCategory)0` if it ever occurred, and the code never casts integers
  to the enum.
- Conclusion: `ErrorCategory?` (`Nullable<ErrorCategory>`), not a sentinel
  member, not a bare enum. Authoritative; no escalation.

### rf-dart-implements-exception-to-csharp-derive-system-exception — exception base class choice (load-bearing)

- Deep analysis: Dart `class X implements Exception` produces an immutable
  data-bearing error type that participates in `throw`/`catch`. The conversion
  must preserve `throw`/`catch` interop on the .NET side.
- Authoritative Dart: WebFetch `https://api.dart.dev/dart-core/Exception-class.html`
  (Dart official). Verbatim: "abstract interface" and "can only be implemented
  (not extended or mixed in)" — so Dart `Exception` is an *interface contract*,
  and the canonical way to define a custom Dart exception is `implements
  Exception`. The page also notes "Creating instances of Exception directly
  with `Exception("message")` is discouraged in library code since it doesn't
  give users a precise type they can catch" — i.e. user-defined exception
  types ARE the recommended Dart practice, matching exactly what the source
  does.
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/standard/exceptions/how-to-create-user-defined-exceptions`
  (Microsoft Learn). Verbatim: ".NET provides a hierarchy of exception
  classes ultimately derived from the [Exception](/en-us/dotnet/api/system.exception)
  base class. However, if none of the predefined exceptions meet your needs,
  you can create your own exception class by deriving from the
  [Exception](/en-us/dotnet/api/system.exception) class." And: "end the class
  name of the user-defined exception with the word 'Exception', and implement
  the three common constructors". The "three common constructors" pattern is
  documented but recorded by this spec as NOT mechanically applied — only the
  Dart source's single semantic constructor is emitted, because adding
  parameterless or inner-exception ctors would manufacture an instantiation
  surface the Dart code does not have (spec-faithfulness, FR-013).
- Conclusion: Dart `implements Exception` ⇔ C# `: Exception` (class
  inheritance, since .NET has no throwable interface). Message routed to
  base via `: base(message)`. Naming-suffix convention is recorded as a
  SEPARATE escalation (project policy: preserve `CompileError` or rename to
  `CompileException`) — both technical options are authoritative-supported,
  so the decision is policy, not facts. Authoritative; one escalation as
  noted.

### rf-dart-named-default-param-to-csharp-optional-arg — `{this.source, String? phase}`

- Deep analysis: hybrid constructor — three positional initialising-formals
  plus two named optionals; one named (`source`) is itself an
  initialising-formal, the other (`phase`) is a plain named parameter fed
  to the `category` field's initializer-list expression.
- Authoritative Dart: covered by the existing idiom basis in the opcodes.dart
  spec (`https://dart.dev/language/functions`) — named parameters are
  optional unless `required`; defaults must be compile-time constants. Dart
  initialising-formals (`this.x`) are syntactic sugar for "assign argument to
  field".
- Authoritative .NET: covered by Microsoft Learn language reference for
  optional parameters and named arguments (same family fetched in opcodes.dart
  provenance) — C# supports default values for parameters and named-argument
  call syntax at call sites.
- Conclusion: single C# constructor with optional `= null` defaults;
  initialising-formal sugar expanded to explicit assignments in the body;
  `: base(message)` chains the message to `Exception`. Authoritative; no
  escalation.

### rf-dart-leading-underscore-privacy-to-csharp-private — `_categoryFromPhase`

- Deep analysis: the helper is library-private in Dart (`_` prefix on a
  top-level static class member). In this file the helper is used only by
  the class's own constructor initializer list — class-private narrowing is
  strictly correct.
- Basis: Dart official language tour documents `_`-prefix as
  library-private. C# has no library-private modifier; the strictly tighter
  `private` (class-scoped) is the faithful narrowing in this single-class
  context. (No external fetch needed beyond the Dart language tour, which is
  the broadly-cited official guidance for privacy.) Authoritative; no
  escalation.

### rf-dart-tostring-interp-to-csharp-tostring-interp — diagnostic toString with caret + repetition

- Deep analysis: this override is the load-bearing diagnostic surface for
  compile errors. It conditionally formats: bracketed-category prefix,
  message, "Line L, Column C", and (when source is provided and the line
  number is in range) the offending source line plus a `^`-pointer-caret at
  the column.
- Authoritative Dart: WebFetch `https://api.dart.dev/dart-core/Object/toString.html`
  (Dart official). Verbatim: "Such classes will typically override `toString`
  to provide useful information when inspecting the object, mainly for
  debugging or logging." Confirms that overriding `toString` for diagnostic
  output is the recommended Dart practice.
- Authoritative Dart (string repetition): WebFetch
  `https://api.dart.dev/dart-core/String/operator_multiply.html` (Dart
  official). Verbatim: "Creates a new string by concatenating this string
  with itself a number of times. The result of `str * n` is equivalent to
  `str + str + ...`(n times)`... + str`." Confirms `' ' * (column - 1)`
  produces `(column - 1)` spaces.
- Authoritative Dart (split): WebFetch
  `https://api.dart.dev/dart-core/String/split.html` (Dart official). Verbatim
  return signature: "List<String> split(Pattern pattern);" and "Finds all the
  matches of `pattern` in this string, as by using Pattern.allMatches, and
  returns the list of the substrings between the matches, before the first
  match, and after the last match." Confirms `source.split('\\n')` returns
  a `List<String>` cut at every LF.
- Authoritative .NET (Exception.ToString): WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.exception.tostring`
  (Microsoft Learn). Verbatim: "The default implementation of ToString
  obtains the name of the class that threw the current exception, the
  message, the result of calling ToString on the inner exception, and the
  result of calling Environment.StackTrace … This method overrides
  Object.ToString." This documents what we are REPLACING: our override does
  NOT call `base.ToString()` because the Dart semantic is a diagnostic
  string WITHOUT a stack-trace prefix.
- Authoritative .NET (string repetition): WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.string.-ctor`
  (Microsoft Learn). Verbatim under `String(Char, Int32)`: "Initializes a
  new instance of the String class to the value indicated by a specified
  Unicode character repeated a specified number of times." This is the
  authoritative counterpart for Dart's `String * int` — `new string(' ',
  count)`. Throws `ArgumentOutOfRangeException` if count < 0; since the
  Dart code computes `(column - 1)` without a guard, the Dart source can
  itself fault when `column == 0` (`' ' * -1` is also negative in Dart,
  which would throw RangeError). The conversion preserves that
  behaviour — fault semantics are equivalent (both throw on negative
  count), so no defensive guard is synthesised (FR-013 / "robustness is
  often a workaround in disguise" per project CLAUDE.md).
- Conclusion: the override emits a C# `public override string ToString()`
  that mirrors Dart branch-for-branch using `string.Split('\\n')` and
  `new string(' ', (int)(Column - 1))` per the authoritative APIs.
  Replaces (does not extend) `Exception.ToString` — intentional and
  documented. Reference/value-type concerns: `string` is a reference type
  in both languages (immutable, value-equality `==`); arrays are reference
  types. Authoritative both sides; no escalation.

## Notes

- No Stream/Future/async, no isolates, no `late`, no `mixin`, no
  `extension`, no generics-with-bounds, no `sealed` classes, no
  bitwise/shift, no overflow path beyond the documented `String(char,
  int)` argument-out-of-range (which Dart's `' ' * n` also throws on) —
  the well-known nuances ABSENT from this file are deliberately not
  asserted.
- The load-bearing semantic decision is the exception-base-class choice:
  Dart's `implements Exception` (interface) must become C#'s `: Exception`
  (inheritance) because .NET has no throwable-interface idiom. Message
  is routed through the base constructor so `Exception.Message`,
  `Exception.ToString` interop and serialization-by-message all work
  correctly when our override is bypassed.
- One escalation (kind: undecidable) is recorded for the exception
  naming-suffix policy (`CompileError` vs `CompileException`) — both
  options are authoritative-supported; the choice is project policy,
  not a docs determination, so per FR-013 we do not silently pick. Spec
  default until Gabi decides: preserve source name `CompileError`.
- Trivial constructs (doc comments → `///` XML-doc, line comments
  preserved as `//`) are recorded as `trivial: true` without research,
  per the contract.
