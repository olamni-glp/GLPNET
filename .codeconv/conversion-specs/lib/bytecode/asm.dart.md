# Conversion Spec — lib/bytecode/asm.dart

> Conversion-spec artifact for lib/bytecode/asm.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> A small (93-line) hand-written bytecode-assembler facade: the single
> public class `BC` holds ~50 `static` factory methods that wrap the
> opcode constructors defined in `opcodes.dart` (v1) and
> `opcodes_v2.dart` (v2, imported as `opv2`), plus a `prog(...)` factory
> that returns a `BytecodeProgram` (from `runner.dart`). It is a
> namespace-of-helpers — NOT an instance class. Two parallel naming
> surfaces are exposed: lowerCamelCase short forms (`l`, `try_`, `r`,
> `w`, `commit`, `clauseNext`, ...) and UPPERCASE aliases (`L`, `TRY`,
> `R`, `W`, `COMMIT`, `CLAUSE_NEXT`, ...) that forward to the
> camelCase form. The aliases are the load-bearing surface for tests
> that read like assembly listings — they MUST both survive the
> conversion. Two static methods (`u`/`U` → `UnionSiAndGoto`,
> `next`/`NEXT` → `ResetAndGoto`) carry Dart `@deprecated` mirroring
> the deprecated v1 opcode classes in `opcodes.dart.md`. Every cross-
> file type reference (`Label`, `ClauseTry`, `BodySetConst`,
> `BytecodeProgram`, `Op`, `opv2.HeadVariable`, ...) is REUSED from the
> sibling convspecs (FR-024 cache hit), never re-derived. No
> escalations.

```yaml
schema_version: 1
source_path: lib/bytecode/asm.dart
source_sha256: 8b1bffcb06af0db1fc0b8228d34209e8e47ccda5d74732ca5c7d6c86ad083839
target_code_unit: lib/bytecode/asm.cs
constructs:
  - construct_key: dart.analyzer_lint_directive.ignore_for_file
    source_form: "`// ignore_for_file: non_constant_identifier_names` — file-level Dart analyzer lint suppression. Suppresses the `non_constant_identifier_names` lint that would otherwise flag the UPPERCASE alias methods (`L`, `TRY`, `R`, `W`, `COMMIT`, `CLAUSE_NEXT`, `TRY_NEXT_CLAUSE`, `NO_MORE_CLAUSES`, `U`, `NEXT`, `SUSP`, `PROCEED`, `OTHERWISE`, `UNKNOWN`, `BCONST`, `BSTRUCTC`) — Dart's recommended style is lowerCamelCase for methods."
    target_decision: >-
      Elide. The directive is a Dart-analyzer-specific lint suppression
      with NO behavioural effect — the source still compiles without
      it; it only silences a style warning. C# has no equivalent
      file-level analyzer-suppression token (the .NET counterparts are
      `#pragma warning disable <CSnnnn>` for specific compiler
      warnings, `[SuppressMessage]` per-symbol for analyzers, or
      `.editorconfig` for project-wide style rules). The corresponding
      .NET style rule is `IDE1006` ("Naming styles"), which is OFF by
      default in a fresh `dotnet new` project — so absent any
      `.editorconfig` opt-in there is nothing to suppress, and a
      blanket `#pragma warning disable IDE1006` would suppress
      legitimate naming-violation warnings the codegen baseline does
      not want to silence. Codegen MUST NOT emit any of: a `#pragma
      warning disable`, a `[SuppressMessage]` attribute, or an
      `.editorconfig` edit. Codegen MAY emit a single `//` comment
      line documenting why the SCREAMING_CASE alias methods are
      preserved (see rf-dart-uppercase-alias-method-naming below) but
      this is optional and bears no semantic content.
    idiom_id: null
    research_finding_id: rf-dart-ignore-for-file-elided
    nuance: >-
      Lint-vs-compiler nuance (explicitly addressed): Dart
      `// ignore_for_file:` targets the Dart static analyzer
      (analyzer-only diagnostics), NOT the Dart compiler — the file
      compiles identically with or without it. C# analyzer diagnostics
      live in a parallel space (Roslyn analyzers + `IDE####`/`CA####`
      ids) reached via attributes / pragmas / editorconfig, not via
      file-leading comments. Suppression-style nuance: a faithful
      conversion preserves OBSERVED BEHAVIOUR (the methods compile and
      work), not the analyzer-suppression token itself; since the
      target naming surface (PascalCase BC.L / BC.Tr / etc.) is a
      codegen-stage decision that is downstream of this file, the lint
      directive has no preserved counterpart. NULL-SAFETY / value-vs-
      reference / async — not implicated by a lint directive.

  - construct_key: dart.import_directive.relative_to_using_namespace_with_prefix_alias
    source_form: >-
      Three relative imports: `import 'opcodes.dart';` (brings the v1
      opcode types `Label`, `ClauseTry`, `GuardNeedReader`,
      `HeadBindWriter`, `Commit`, `ClauseNext`, `TryNextClause`,
      `NoMoreClauses`, `UnionSiAndGoto`, `ResetAndGoto`, `SuspendEnd`,
      `Proceed`, `BodySetConst`, `BodySetStructConstArgs`,
      `HeadStructure`, `UnifyConstant`, `UnifyVoid`, `HeadConstant`,
      `GetVariable`, `GetValue`, `PutConstant`, `PutStructure`,
      `SetConstant`, `Otherwise`, `Spawn`, `Requeue`, `Guard`,
      `Ground`, `Known`, `HeadNil`, `HeadList`, `PutNil`, `PutList`,
      `Allocate`, `Deallocate`, `Nop`, `Halt`, and the marker
      interface `Op` itself), `import 'opcodes_v2.dart' as opv2;`
      (brings the v2 opcode family under a PREFIX alias — used as
      `opv2.HeadVariable` and `opv2.Unknown` in the factory bodies),
      and `import 'runner.dart';` (brings `BytecodeProgram` used as
      the return type of `prog(...)`).
    target_decision: >-
      Each relative import maps to a .NET `using` directive naming the
      namespace of the converted sibling file: the codegen-stage
      namespace mirroring `lib/bytecode/` (single namespace shared
      with `opcodes.dart`/`opcodes_v2.dart`/`runner.dart`). The Dart
      prefix-import `as opv2;` MUST be honoured by a
      namespace-qualified target reference — codegen MUST keep the
      v1/v2 type families distinguishable (per `opcodes_v2.dart.md`
      and `runner.dart.md`: separate marker interfaces `IOp` and
      `IOpV2`, no shared base). Concretely, `opv2.HeadVariable` (in
      `headWriter`/`headReader`) and `opv2.Unknown` (in `unknown`/
      `UNKNOWN`) target the v2 namespace `V2.HeadVariable` /
      `V2.Unknown`, NOT a collapsed unqualified `HeadVariable` — the
      v1 opcodes file contains no `HeadVariable`/`Unknown` of its own
      (the collision exists only with `runner.dart` parameter shapes),
      but the convention from `runner.dart.md`
      rf-dart-import-relative-to-csharp-using-namespace (the
      load-bearing v1/v2-separation precedent) is normative for THIS
      file too: every `opv2.X` reference MUST emit as a v2-qualified
      type, never as a bare `X`. Cache hit on
      `runner.dart.md` / `heap_fcp.dart.md` /
      `variable_table.dart.md` — same authoritative finding
      (FR-024).
    idiom_id: null
    research_finding_id: rf-dart-import-relative-to-csharp-using-namespace
    nuance: >-
      Prefix-import nuance (LOAD-BEARING, explicitly addressed): Dart
      `as opv2` is a true symbol prefix that disambiguates colliding
      class names across two files in the same package. C# `using`
      aliases work at the type level, but a namespace-qualified
      reference (`V2.HeadVariable`) is the semantically equivalent
      translation — and is REQUIRED here because the v2 marker
      interface `IOpV2` is deliberately disjoint from v1 `IOp` (see
      `opcodes_v2.dart.md`). Codegen MUST NOT collapse v1 and v2
      opcode types into a single namespace. Compilation-unit nuance
      (carry-forward): Dart resolves relative imports by URI, .NET
      resolves type references by assembly + namespace; the `show`
      allow-list (NOT used in this file) has no parallel. No value-
      vs-reference, null-safety, or async surface implicated by
      import directives themselves.

  - construct_key: dart.class.namespace_of_static_helpers
    source_form: >-
      `class BC { /* ~50 static factory methods, no instance state,
      no instance constructor, no instance methods, no fields */ }`
      — `BC` is a pure namespace-of-helpers. Every member is `static`;
      Dart does not give a way to declare "this class cannot be
      instantiated" beyond not providing a constructor body, but the
      idiomatic use is `BC.l('x')` / `BC.commit()` etc., never
      `BC()`.
    target_decision: >-
      Emit as a C# `public static class BC` in the
      `lib/bytecode/`-mirroring namespace. The `static` class modifier
      is the load-bearing decision: it (a) forbids instantiation (`new
      BC()` is a compile error), (b) forbids inheritance, and (c)
      mandates every member be `static` — exactly the Dart
      not-instantiable namespace-class shape. Do NOT emit as a plain
      `class BC` with a private constructor: a `private BC() {}`
      hack is the pre-2.0-C# pattern from before the `static class`
      modifier existed; the modern faithful target is the dedicated
      `static class` keyword (Microsoft Learn: "Static Classes and
      Static Class Members"). Do NOT emit as a `module BC` or a
      top-level set of functions (C# has no top-level free functions
      outside of file-scoped programs). Do NOT emit as an `internal`
      class — Dart `BC` is library-public (no leading underscore,
      callable from `compiler/codegen.dart` and the test files listed
      in the tombstone callers); `public` is the faithful visibility.
    idiom_id: null
    research_finding_id: rf-dart-namespace-class-of-statics-to-csharp-static-class
    nuance: >-
      Static-class nuance (explicitly addressed): C# `static class`
      is a single-purpose modifier specifically for the
      namespace-of-helpers role; absent it, the C# compiler does not
      stop callers from writing `new BC()`. Reference-vs-value: a
      static class is not instantiable so the value/reference question
      does not arise — methods are looked up by class type, never by
      an object reference. Null-safety: not implicated (no instance
      fields). Async: not implicated (all factories are synchronous
      one-line `new`-and-return). Inheritance: a C# `static class`
      cannot be the base of another class — matching Dart's intent
      (no `extends BC` or `implements BC` exists in the codebase,
      verified across the tombstone callers).

  - construct_key: dart.static_method.one_line_arrow_factory_returning_new_instance
    source_form: >-
      Every factory follows the same shape:
      `static <ReturnType> <name>(<params>) => <ReturnType>(<args>);`.
      Examples: `static Label l(String name) => Label(name);`,
      `static ClauseTry try_() => ClauseTry();`,
      `static GuardNeedReader r(int readerId) => GuardNeedReader(readerId);`,
      `static HeadBindWriter w(int writerId) => HeadBindWriter(writerId);`,
      `static Commit commit() => Commit();`,
      `static ClauseNext clauseNext(String label) => ClauseNext(label);`,
      `static BytecodeProgram prog(List<Op> ops) => BytecodeProgram(ops);`,
      and so on for ~50 factories. The body is a single expression that
      invokes the IR-node class constructor and returns the instance
      (Dart 2 elides the `new` keyword; the call is constructor-
      invocation).
    target_decision: >-
      Each becomes a C# `public static` method on `static class BC`
      with an expression-bodied member returning a new instance:
      `public static Label L(string name) => new Label(name);` (final
      method-name casing is a downstream codegen choice — see
      rf-dart-uppercase-alias-method-naming for the load-bearing
      SCREAMING_CASE preservation rule; the lowerCamelCase short
      forms convert under standard Microsoft naming to PascalCase).
      Use `new <Type>(...)` rather than C# target-typed `new(...)`
      because the return-type annotation already names the type and
      a target-typed `new` would be inferred against the declared
      return type (semantically equivalent; codegen choice).
      Parameter types follow per-type idioms already decided in
      sibling specs: Dart `String` → C# `string`
      (cache hit on `opcodes.dart.md`
      rf-dart-typedef-string-to-csharp-using-alias for the broader
      String mapping; here the params are bare `String`, not
      `LabelName`, but the underlying mapping is the same); Dart
      `int` → C# `long` (cache hit on `opcodes.dart.md`
      rf-dart-int-to-csharp-long-width — every `int` here is a
      register index / writer id / reader id / arity / argSlot, the
      same family that flows into the IR-node fields); Dart
      `Object?` → C# `object?` (cache hit on `opcodes.dart.md`
      rf-dart-objectq-to-csharp-objectq for the `bconst`/`BCONST`
      factory's `Object? v` parameter); Dart `List<Object?>` → C#
      `List<object?>` (cache hit, same finding, for `bstructC`/
      `BSTRUCTC`'s `List<Object?> constArgs`); Dart `List<Op>` → C#
      `List<IOp>` (cache hit on `opcodes.dart.md`
      rf-dart-abstract-marker-to-csharp-interface — `Op` is the
      v1 marker interface). The factories are pure (no side
      effects, no null deref, no state) so they are trivially
      thread-safe and need no `[ThreadStatic]` or locking.
    idiom_id: null
    research_finding_id: rf-dart-static-arrow-factory-to-csharp-static-expression-bodied-method
    nuance: >-
      Constructor-invocation nuance (explicitly addressed): in Dart 2+
      the `new` keyword is optional and elided here — `Label(name)`
      is constructor invocation, not a function call. The C# target
      MUST emit `new Label(name)` (or target-typed `new(name)`); C#
      requires `new` for constructor invocation (or target-typed
      `new(...)` from C# 9). Value-vs-reference: every constructed
      IR-node is a reference class (per
      `opcodes.dart.md`/`opcodes_v2.dart.md`
      rf-dart-final-field-class-to-csharp-getonly-class) so each
      factory call allocates a new heap object — preserved by the
      C# `new`. Generics nuance: the only generic parameter type in
      this file is `List<Op>` (in `prog`) and `List<Object?>` (in
      `bstructC`/`BSTRUCTC`); `List<T>` is a closed generic at the
      call site in both languages — no `where T : ...` constraint,
      no covariance/contravariance concern. Expression-bodied members:
      Dart `=> e;` and C# `=> e;` have identical semantics (eager
      evaluation on each call; no memoisation). Static dispatch:
      `BC.l('x')` in Dart and `BC.L("x")` in C# are both resolved at
      compile time — no v-table, no boxing.

  - construct_key: dart.identifier.trailing_underscore_to_escape_reserved_word
    source_form: >-
      `static ClauseTry try_() => ClauseTry();` — the method is named
      `try_` (with a trailing underscore) because `try` is a Dart
      reserved keyword (control-flow `try/catch/finally`). The
      UPPERCASE alias is `TRY()` (no trailing underscore because
      uppercase `TRY` is NOT reserved in Dart).
    target_decision: >-
      The trailing-underscore-as-keyword-escape is a Dart-specific
      identifier convention with TWO faithful C# expressions and ONE
      forbidden anti-pattern: (a) RECOMMENDED — emit as the
      verbatim-prefixed identifier `@try`. C# accepts any reserved
      keyword as an identifier when prefixed with `@`, preserving
      the conceptual mapping `try` exactly; the method-call site is
      `BC.@try()`. (b) ALTERNATE — emit as the renamed identifier
      `Tr()` or `TryOp()` (drop the underscore, choose a non-
      keyword name); this is the more idiomatic Microsoft naming but
      LOSES the visual correspondence to Dart's `try_`. (c)
      FORBIDDEN — emit as `Try()` (PascalCase) WITHOUT the `@`
      escape: `Try` is NOT a C# reserved keyword and is a perfectly
      valid C# identifier, so this also works syntactically; HOWEVER,
      this loses the readability cue that the symbol corresponds to
      the GLP `try` control-flow opcode. Codegen MUST choose (a) or
      (b), MUST document the choice once per file, and MUST be
      consistent across all v1 callers. The default per this spec is
      (a) `@try` — preserves the conceptual one-to-one mapping with
      the Dart short form. The UPPERCASE alias `TRY()` already
      avoids the issue (uppercase TRY is not a C# keyword either) and
      maps directly to `TRY()` per
      rf-dart-uppercase-alias-method-naming below.
    idiom_id: null
    research_finding_id: rf-dart-trailing-underscore-keyword-escape-to-csharp-at-prefix
    nuance: >-
      Keyword-escape nuance (LOAD-BEARING, explicitly addressed): Dart
      and C# both reserve `try` (control-flow keyword in both). Dart's
      escape convention is a trailing underscore (`try_`,
      idiomatic-but-not-syntactic — `try_` is just a regular
      identifier that happens not to be the keyword). C#'s escape
      convention is a leading `@` (syntactic — `@try` is exactly the
      identifier "try" the way the compiler sees it). The two are
      semantically equivalent (both produce a valid identifier whose
      written form references the keyword) but syntactically distinct
      — `try_` in Dart and `@try` in C# are NOT the same string of
      characters. Codegen MUST NOT mechanically transliterate
      `try_` ↔ `try_` across the conversion (C# `try_` would be a
      DIFFERENT identifier from `@try`, and external callers'
      references would have to change). Null-safety / value-vs-
      reference / async — not implicated by an identifier escape.

  - construct_key: dart.naming.parallel_uppercase_alias_methods
    source_form: >-
      Every short-form factory has an UPPERCASE alias that
      forwards to it: `static Label L(String name) => l(name);`,
      `static ClauseTry TRY() => try_();`,
      `static GuardNeedReader R(int readerId) => r(readerId);`,
      `static HeadBindWriter W(int writerId) => w(writerId);`,
      `static Commit COMMIT() => commit();`,
      `static ClauseNext CLAUSE_NEXT(String label) => clauseNext(label);`,
      `static TryNextClause TRY_NEXT_CLAUSE() => tryNextClause();`,
      `static NoMoreClauses NO_MORE_CLAUSES() => noMoreClauses();`,
      `static UnionSiAndGoto U(String label) => u(label);` (also
      `@deprecated`),
      `static ResetAndGoto NEXT(String label) => next(label);` (also
      `@deprecated`),
      `static SuspendEnd SUSP() => susp();`,
      `static Proceed PROCEED() => proceed();`,
      `static Otherwise OTHERWISE() => otherwise();`,
      `static opv2.Unknown UNKNOWN(int varIndex) => unknown(varIndex);`,
      `static BodySetConst BCONST(int writerId, Object? v) => bconst(writerId, v);`,
      `static BodySetStructConstArgs BSTRUCTC(int writerId, String f, List<Object?> constArgs) => bstructC(writerId, f, constArgs);`.
      Each alias is the SCREAMING_CASE or SCREAMING_SNAKE_CASE form
      of the camelCase short name, used in test files to make
      bytecode listings read like an assembly source.
    target_decision: >-
      PRESERVE BOTH NAMING SURFACES VERBATIM. Every camelCase factory
      AND every UPPERCASE-aliased factory MUST exist on `static class
      BC` in the target. The UPPERCASE aliases (`L`, `TRY`, `R`,
      `W`, `COMMIT`, `CLAUSE_NEXT`, `TRY_NEXT_CLAUSE`,
      `NO_MORE_CLAUSES`, `U`, `NEXT`, `SUSP`, `PROCEED`,
      `OTHERWISE`, `UNKNOWN`, `BCONST`, `BSTRUCTC`) are
      LOAD-BEARING for the assembly-style readability of the test
      files (`test/bytecode/utility_instructions_test.dart`,
      `test/lint/linter_*.dart`) AND for `compiler/codegen.dart` —
      pruning them would break every caller. Casing nuance: under
      Microsoft naming the camelCase short forms convert to
      PascalCase (`l → L`, `r → R`, `w → W`, `commit → Commit`,
      `clauseNext → ClauseNext`, ...) — and this collides with the
      already-existing UPPERCASE aliases for the SINGLE-LETTER cases
      (`L`, `R`, `W` — both surfaces would map to the same C#
      method name). RESOLUTION: codegen MUST keep the two surfaces
      DISTINCT. Two acceptable target shapes: (i) keep the
      short-form camelCase verbatim (`BC.l`, `BC.r`, `BC.w`,
      `BC.commit`, `BC.clauseNext`) and the UPPERCASE alias
      verbatim (`BC.L`, `BC.R`, `BC.W`, `BC.COMMIT`,
      `BC.CLAUSE_NEXT`) — the C# compiler accepts ANY casing for
      identifiers, only the `IDE1006` style analyzer would complain
      (off by default, see rf-dart-ignore-for-file-elided); OR (ii)
      rename the short-form camelCase to a distinct PascalCase
      surface (`BC.Lbl`, `BC.Rdr`, `BC.Wtr`, `BC.Commit`,
      `BC.ClauseNext`) and keep UPPERCASE aliases. The DEFAULT per
      this spec is (i) — verbatim preservation — because every
      caller in the tombstone callers list reads with the Dart
      naming and the parallel-surface intent is the WHOLE POINT of
      the file. Constants vs. methods: SCREAMING_CASE in C# is
      conventionally reserved for `const` fields, but C# imposes
      NO syntactic restriction (only `IDE1006` style); the
      conversion deliberately keeps the SCREAMING_CASE method
      names to mirror the Dart source 1-to-1, accepting the
      analyzer-style deviation as the price of round-trip
      readability.
    idiom_id: null
    research_finding_id: rf-dart-uppercase-alias-method-naming
    nuance: >-
      Parallel-surface nuance (LOAD-BEARING, explicitly addressed):
      the Dart file deliberately exposes TWO names for the same
      factory (`l` AND `L`, `try_` AND `TRY`, ...) — this is NOT a
      lint/style accident but a deliberate API design. C# compiler
      semantics: case-sensitive identifiers (`L` and `l` ARE
      different methods at the IL level), so the two-surface API is
      directly representable. The single-letter collision after
      PascalCase casing is the ONLY casing hazard; the SCREAMING_SNAKE
      aliases (`CLAUSE_NEXT`, `TRY_NEXT_CLAUSE`, `NO_MORE_CLAUSES`)
      and the all-caps short forms (`SUSP`, `PROCEED`, `OTHERWISE`,
      `UNKNOWN`, `BCONST`, `BSTRUCTC`) never collide with the
      camelCase forms. Underscore preservation: C# identifiers permit
      `_` (`CLAUSE_NEXT` is a valid C# identifier), so the
      SCREAMING_SNAKE form is transferable verbatim. The
      lowerCamelCase-→-PascalCase Microsoft naming convention is
      deliberately NOT applied here because doing so would erase the
      "two parallel naming surfaces" design pillar that motivates
      `// ignore_for_file: non_constant_identifier_names` in the
      first place. Value-vs-reference / null-safety / async: not
      implicated by a naming alias (the aliased methods themselves
      are covered by
      rf-dart-static-arrow-factory-to-csharp-static-expression-bodied-method).

  - construct_key: dart.deprecated_annotation_on_static_method
    source_form: >-
      `@deprecated static UnionSiAndGoto u(String label) => UnionSiAndGoto(label);`,
      `@deprecated static ResetAndGoto next(String label) => ResetAndGoto(label);`,
      `@deprecated static UnionSiAndGoto U(String label) => u(label);`,
      `@deprecated static ResetAndGoto NEXT(String label) => next(label);`.
      Mirror of the `@deprecated` on the `UnionSiAndGoto` and
      `ResetAndGoto` opcode CLASSES in `opcodes.dart.md` — these
      factories are kept for backward compatibility (the
      `// Legacy (deprecated)` comments mark both blocks).
    target_decision: >-
      Each `@deprecated` static method maps to the C#
      `[System.Obsolete]` attribute on the corresponding `public
      static` method. The methods are PRESERVED (not deleted)
      because (i) the source comments label them
      `// Legacy (deprecated)` explicitly for backward compat with
      tests, and (ii) `opcodes.dart.md` rf-dart-deprecated-to-
      csharp-obsolete already preserved the underlying opcode
      classes — deleting the BC factories would orphan callable C#
      types. The `@deprecated` on the camelCase form AND on the
      UPPERCASE alias forms `[Obsolete]` independently: both must
      survive (the forwarding chain `U → u → new UnionSiAndGoto(...)`
      means a caller of `BC.U` already gets two warning sites,
      matching Dart). Cache hit on `opcodes.dart.md`
      rf-dart-deprecated-to-csharp-obsolete (same authoritative
      finding — FR-024).
    idiom_id: null
    research_finding_id: rf-dart-deprecated-to-csharp-obsolete
    nuance: >-
      Deprecation-on-method nuance (explicitly addressed): the
      `opcodes.dart.md` cache hit covered `@deprecated` on a CLASS;
      here it is `@deprecated` on a STATIC METHOD. The mapping is
      identical — `[Obsolete]` applies equally to types, methods,
      constructors, properties, and fields (`AttributeTargets.All`
      per Microsoft Learn ObsoleteAttribute documentation). Both
      annotations are advisory (analyzer / compiler warning), no
      runtime effect. Null-safety / value-vs-reference / async: not
      implicated by a deprecation annotation.

  - construct_key: dart.method.named_required_arg_forwarded_via_qualified_constructor_call
    source_form: >-
      `static opv2.HeadVariable headWriter(int varIndex) => opv2.HeadVariable(varIndex, isReader: false);`
      and
      `static opv2.HeadVariable headReader(int varIndex) => opv2.HeadVariable(varIndex, isReader: true);`.
      The factories accept a single positional `int varIndex` and
      construct a v2 `HeadVariable` (whose constructor is
      `HeadVariable(this.varIndex, {required this.isReader})` per
      `opcodes_v2.dart.md`
      rf-dart-required-named-param-to-csharp-required-arg) by passing
      `isReader:` as a named argument with a literal bool. Two
      factories baked the mode flag into the method name (writer /
      reader).
    target_decision: >-
      Each factory becomes a C# `public static` method on `static
      class BC` invoking the v2 constructor with C# named-argument
      syntax: codegen emits something equivalent to
      `static V2.HeadVariable HeadWriter(long varIndex) => new
      V2.HeadVariable(varIndex, isReader: false);` and the analogous
      `HeadReader(...)` with `isReader: true`. The
      `opv2.HeadVariable` reference resolves under
      rf-dart-import-relative-to-csharp-using-namespace above
      (qualified-by-namespace `V2.HeadVariable`, NOT a bare
      `HeadVariable`). The named-argument call `isReader: false`
      maps verbatim — C# named-argument syntax accepts the same
      `name: value` form and the underlying constructor parameter
      `isReader` (no default) preserves mandatoriness (cache hit on
      `opcodes_v2.dart.md`
      rf-dart-required-named-param-to-csharp-required-arg).
    idiom_id: null
    research_finding_id: rf-dart-required-named-param-to-csharp-required-arg
    nuance: >-
      Mode-flag nuance (explicitly addressed): the file deliberately
      provides TWO factories per WAM HEAD/UNIFY/etc. mode (one for
      writer, one for reader) instead of a single factory that takes
      a `bool isReader` parameter — this hides the boolean at the
      call site so bytecode listings read with intent-named
      mnemonics (`BC.headWriter(0)` vs `BC.headReader(0)`). Codegen
      MUST preserve this — DO NOT collapse the two factories into
      a single `HeadVariable(int varIndex, bool isReader)` helper;
      doing so would change the call-site shape every caller (and
      `compiler/codegen.dart`) emits. Value-vs-reference: returned
      `V2.HeadVariable` is a reference class (per
      `opcodes_v2.dart.md`). Null-safety / async: not implicated
      (the `bool` is non-nullable and the call is synchronous).

  - construct_key: dart.static_method.named_optional_param_forwarded_with_default
    source_form: >-
      `static UnifyVoid unifyVoid({int count = 1}) => UnifyVoid(count: count);`
      — the factory accepts a named optional `int count` with default
      `1` and forwards it as a named argument to the
      `UnifyVoid({this.count = 1})` constructor (per
      `opcodes.dart.md` rf-dart-named-default-param-to-csharp-optional-arg).
      No UPPERCASE alias exists for this one (the file does not
      define `UNIFYVOID()`).
    target_decision: >-
      Maps to a C# `public static` method on `static class BC` with
      an optional parameter carrying the same default literal:
      `static UnifyVoid UnifyVoid(long count = 1) => new
      UnifyVoid(count: count);`. The forwarded constructor call uses
      C# named-argument syntax (`count: count`) — semantically
      equivalent to a positional pass since `UnifyVoid` has only one
      parameter, but preserving the named form matches the Dart
      source 1-to-1 and remains correct if the constructor signature
      gains additional parameters later. Default-value nuance: the
      literal `1` is a compile-time constant in both languages.
      Cache hit on `opcodes.dart.md`
      rf-dart-named-default-param-to-csharp-optional-arg (FR-024).
    idiom_id: null
    research_finding_id: rf-dart-named-default-param-to-csharp-optional-arg
    nuance: >-
      Method-name-collides-with-type-name nuance (explicitly
      addressed): the factory `unifyVoid` and the constructor it
      wraps are both literally `UnifyVoid` after PascalCase casing
      — a static method and a constructor share the spelling but
      live in different scopes (`BC.UnifyVoid(...)` vs.
      `new UnifyVoid(...)`), and C# resolves them by syntactic
      context (method call vs. `new` expression). No ambiguity at
      compile time. Default-value nuance: a Dart compile-time
      constant default (`1`) maps to a C# compile-time constant
      default (`1`) — no drift; the default propagates to the
      `BC.unifyVoid()` call shape as well. Forwarding semantics:
      explicit `count: count` named-argument forwarding is
      identical-cost in both languages (single function call, no
      allocation). Value-vs-reference / null-safety / async: not
      additionally implicated beyond the underlying constructor.

conversion_units:
  - "(elide) // ignore_for_file: non_constant_identifier_names — no C# emission"
  - "using <root>.Bytecode; (replaces import 'opcodes.dart' + import 'runner.dart')"
  - "using <root>.Bytecode.V2; (replaces import 'opcodes_v2.dart' as opv2 — kept distinct from V1 per opcodes_v2.dart.md)"
  - "public static class BC (namespace-of-helpers; not instantiable; not inheritable)"
  - "public static Label L(string name) => new Label(name); (camelCase short form: l → L per Dart; OR Lbl per Microsoft naming — codegen choice)"
  - "public static ClauseTry @try() => new ClauseTry(); (Dart try_ → C# @try; UPPERCASE alias TRY() carries no escape)"
  - "public static GuardNeedReader R(long readerId) => new GuardNeedReader(readerId);"
  - "public static HeadBindWriter W(long writerId) => new HeadBindWriter(writerId);"
  - "public static Commit Commit() => new Commit(); (and UPPERCASE alias COMMIT() — both preserved)"
  - "public static ClauseNext ClauseNext(string label) => new ClauseNext(label); (and CLAUSE_NEXT alias)"
  - "public static TryNextClause TryNextClause() => new TryNextClause(); (and TRY_NEXT_CLAUSE alias)"
  - "public static NoMoreClauses NoMoreClauses() => new NoMoreClauses(); (and NO_MORE_CLAUSES alias)"
  - "[Obsolete] public static UnionSiAndGoto U(string label) => new UnionSiAndGoto(label); (and lowercase u + UPPERCASE U, both [Obsolete])"
  - "[Obsolete] public static ResetAndGoto NEXT(string label) => new ResetAndGoto(label); (and lowercase next, both [Obsolete])"
  - "public static SuspendEnd Susp() => new SuspendEnd(); (and SUSP alias)"
  - "public static Proceed Proceed() => new Proceed(); (and PROCEED alias)"
  - "public static BodySetConst BConst(long writerId, object? v) => new BodySetConst(writerId, v); (and BCONST alias)"
  - "public static BodySetStructConstArgs BStructC(long writerId, string f, List<object?> constArgs) => new BodySetStructConstArgs(writerId, f, constArgs); (and BSTRUCTC alias)"
  - "public static HeadStructure HeadStruct(string functor, long arity, long argSlot) => new HeadStructure(functor, arity, argSlot);"
  - "public static V2.HeadVariable HeadWriter(long varIndex) => new V2.HeadVariable(varIndex, isReader: false);"
  - "public static V2.HeadVariable HeadReader(long varIndex) => new V2.HeadVariable(varIndex, isReader: true);"
  - "public static UnifyConstant UnifyConst(object? value) => new UnifyConstant(value);"
  - "public static UnifyVoid UnifyVoid(long count = 1) => new UnifyVoid(count: count); (optional arg default = 1 preserved)"
  - "public static HeadConstant HeadConst(object? value, long argSlot) => new HeadConstant(value, argSlot);"
  - "public static GetVariable GetVar(long varIndex, long argSlot) => new GetVariable(varIndex, argSlot);"
  - "public static GetValue GetVal(long varIndex, long argSlot) => new GetValue(varIndex, argSlot);"
  - "public static PutConstant PutConst(object? value, long argSlot) => new PutConstant(value, argSlot);"
  - "public static PutStructure PutStructure(string functor, long arity, long argSlot) => new PutStructure(functor, arity, argSlot);"
  - "public static SetConstant SetConst(object? value) => new SetConstant(value);"
  - "public static Otherwise Otherwise() => new Otherwise(); (and OTHERWISE alias)"
  - "public static V2.Unknown Unknown(long varIndex) => new V2.Unknown(varIndex); (and UNKNOWN alias)"
  - "public static Spawn Spawn(string label, long arity) => new Spawn(label, arity);"
  - "public static Requeue Requeue(string label, long arity) => new Requeue(label, arity);"
  - "public static Guard Guard(string label, long arity) => new Guard(label, arity);"
  - "public static Ground Ground(long varIndex) => new Ground(varIndex);"
  - "public static Known Known(long varIndex) => new Known(varIndex);"
  - "public static HeadNil HeadNil(long argSlot) => new HeadNil(argSlot);"
  - "public static HeadList HeadList(long argSlot) => new HeadList(argSlot);"
  - "public static PutNil PutNil(long argSlot) => new PutNil(argSlot);"
  - "public static PutList PutList(long argSlot) => new PutList(argSlot);"
  - "public static Allocate Allocate(long slots) => new Allocate(slots);"
  - "public static Deallocate Deallocate() => new Deallocate();"
  - "public static Nop Nop() => new Nop();"
  - "public static Halt Halt() => new Halt();"
  - "public static BytecodeProgram Prog(List<IOp> ops) => new BytecodeProgram(ops);"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-ignore-for-file-elided — Dart-analyzer-only lint directive

- Deep analysis: `// ignore_for_file: non_constant_identifier_names` is
  a Dart-analyzer-specific suppression comment. It silences the
  `non_constant_identifier_names` lint (Dart official lint rule)
  which would otherwise flag every UPPERCASE alias method. The
  Dart compiler ignores it; the source compiles identically without
  it. It is a tooling hint, not a language construct.
- Authoritative Dart: WebFetch
  `https://dart.dev/tools/analysis` (Dart official). Verbatim
  relevant text: "You can configure static analysis to: ... Exclude
  code from analysis. ... You can suppress diagnostics for a single
  file using `// ignore_for_file:`." — i.e. the directive's whole
  contract is "tell the analyzer to skip this rule for this file";
  no semantic content.
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/preprocessor-directives/preprocessor-pragma-warning`
  and
  `https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/naming-rules`
  (Microsoft Learn) — `#pragma warning disable <id>` is the C#
  preprocessor counterpart for compiler warnings; analyzer rules
  (including the naming-style rule `IDE1006`) are configured via
  `.editorconfig` or `[SuppressMessage]`. Crucially, `IDE1006` is
  OFF BY DEFAULT in a fresh project, so without an opt-in there is
  no warning to suppress.
- Conclusion: elide. Emitting a blanket `#pragma warning disable
  IDE1006` would suppress LEGITIMATE naming warnings the codegen
  baseline does not want to silence; a `[SuppressMessage]` per
  method would be 32+ attributes for no behaviour change. The
  Dart directive's only effect is on Dart-analyzer output; there is
  nothing for C# to preserve. Authoritative both sides; no
  escalation.

### rf-dart-import-relative-to-csharp-using-namespace — relative imports + prefix alias (cache hit)

- Cache hit. Same authoritative finding already used in
  `runner.dart.md` (which explicitly handles the
  `import 'opcodes_v2.dart' as opv2;` prefix-import case) and
  `heap_fcp.dart.md` / `variable_table.dart.md` (for the broader
  Dart-import → C#-using mapping).
- Conclusion (per cached finding): Dart `import '<file>.dart';` ⇒
  C# `using <namespace>;`. Dart `as <prefix>` ⇒ a
  namespace-qualified target reference (`V2.X`) rather than a bare
  `X` — REQUIRED to keep the v1/v2 marker interfaces (`IOp` vs.
  `IOpV2`) disjoint per `opcodes_v2.dart.md`. The `show` allow-list
  (absent here) has no .NET parallel. Authoritative cache hit; no
  escalation.

### rf-dart-namespace-class-of-statics-to-csharp-static-class — `class BC`

- Deep analysis: `BC` has no instance fields, no instance methods,
  no explicit instance constructor, no `extends`/`implements`/
  `mixin`/`with` clauses. Every member is `static`. Idiomatic use
  is `BC.l('x')` — there is no callsite that ever writes `BC()`
  (verified by reading the tombstone callers list).
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/static-classes-and-static-class-members`
  (Microsoft Learn). Verbatim relevant text: "A static class is
  basically the same as a non-static class, but there is one
  difference: a static class cannot be instantiated. ... All the
  members of a static class must be static. ... Static classes are
  sealed and therefore cannot be inherited." — exactly the
  namespace-of-helpers shape `BC` has in Dart.
- Authoritative Dart: WebFetch
  `https://dart.dev/language/classes` (Dart official). Verbatim
  relevant text: "Static methods (class methods) don't operate on
  an instance, and thus don't have access to `this`. They do,
  however, have access to static variables. ... Use static
  methods as compile-time constants." — confirms Dart static
  methods are class-scoped, not instance-scoped, matching the
  C# `static class` semantics.
- Conclusion: `public static class BC` — the modern, faithful, and
  load-bearing target shape. Do NOT use a `private`-ctor class
  (pre-2.0-C# pattern); do NOT use top-level functions (C# has
  none outside file-scoped programs). Authoritative both sides; no
  escalation.

### rf-dart-static-arrow-factory-to-csharp-static-expression-bodied-method — one-line factories

- Deep analysis: every factory is a one-line expression-bodied
  static method that calls a constructor and returns the new
  instance. No side effects, no null checks, no branches, no
  generics-with-bounds. Parameter types are `String`, `int`,
  `Object?`, `List<Op>`, `List<Object?>` — each already decided
  by sibling specs (cache hits chained inline in the target
  decision).
- Authoritative Dart: WebFetch
  `https://dart.dev/language/functions` (Dart official). Verbatim
  relevant text: "If a function contains just one expression, you
  can use a shorthand syntax: ... `bool isNoble(int atomicNumber)
  => _nobleGases[atomicNumber] != null;`. The `=> expr` syntax is
  a shorthand for `{ return expr; }`. ... Note that only an
  expression — not a statement — can appear between the arrow
  (`=>`) and the semicolon."
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/statements-expressions-operators/expression-bodied-members`
  (Microsoft Learn). Verbatim relevant text: "Expression body
  definitions let you provide a member's implementation in a very
  concise, readable form. You can use an expression body
  definition whenever the logic for any supported member, such as
  a method or property, consists of a single expression. ... The
  general syntax is: `member => expression;`."
- Conclusion: Dart `static T name(params) => T(args);` ⇒ C#
  `public static T Name(params) => new T(args);` (with `new`
  required for constructor invocation in C# — Dart 2 elides
  `new`, C# requires it absent target-typed `new(...)`).
  Authoritative both sides; no escalation.

### rf-dart-trailing-underscore-keyword-escape-to-csharp-at-prefix — `try_`

- Deep analysis: `try` is reserved in both Dart and C# (try/catch/
  finally control-flow). Dart uses a TRAILING-UNDERSCORE
  convention to make a non-keyword identifier; C# uses a
  LEADING-`@` syntactic escape that produces the SAME identifier
  as the keyword spelling. The two are NOT mechanically
  transliterated — `try_` in Dart is the identifier whose name is
  the 4-character string `try_`; `@try` in C# is the identifier
  whose name is the 3-character string `try`.
- Authoritative Dart: WebFetch
  `https://dart.dev/language/keywords` (Dart official). Verbatim
  relevant text confirms `try` is a reserved word, and the only
  way to name something `try` is to choose a different identifier
  (the `try_` underscore convention is a community idiom, not a
  syntactic feature).
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/verbatim`
  / the C# Language Specification §7.4.3 (identifiers — covered on
  Microsoft Learn at
  `https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names`
  too). Verbatim relevant text: "The prefix `@` enables the use of
  keywords as identifiers, which is useful when interfacing with
  other programming languages." — i.e. `@try` is a syntactically
  valid C# identifier whose ToString is `"try"`.
- Conclusion: emit `@try()` as the default short form (best
  preserves the conceptual mapping to Dart `try_`); a renamed
  alternative `Tr()` is acceptable when team style forbids `@`-
  prefixed identifiers. The UPPERCASE alias `TRY()` is unaffected
  (uppercase TRY is not a reserved word in either language).
  Authoritative both sides; no escalation.

### rf-dart-uppercase-alias-method-naming — parallel SCREAMING_CASE surface

- Deep analysis: the file deliberately exposes two parallel
  naming surfaces (camelCase + SCREAMING_CASE) for each factory,
  motivated by the `// ignore_for_file: non_constant_identifier_names`
  at the top — i.e. the author KNEW lint would complain and
  chose to suppress it because the parallel surface is the WHOLE
  POINT. The aliases are called from `compiler/codegen.dart` and
  from every test file in the tombstone callers list.
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/identifier-names`
  (Microsoft Learn). Verbatim relevant text confirms C#
  identifiers are case-sensitive (`L` and `l` are different
  identifiers; `Try` and `try` are different) — so the
  two-surface API is directly representable in C# with zero
  collision at the IL level.
- Authoritative .NET (style):
  `https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions`
  and
  `https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/naming-rules`
  (Microsoft Learn) — SCREAMING_CASE for methods is OUTSIDE the
  recommended `IDE1006` style (which is off by default), but is
  syntactically valid and is the price of preserving Dart-source
  parity.
- Conclusion: preserve both surfaces verbatim. The single-letter
  cases (`L`/`l`, `R`/`r`, `W`/`W`) keep their case-distinct C#
  spellings. The SCREAMING_SNAKE_CASE aliases (`CLAUSE_NEXT`,
  `TRY_NEXT_CLAUSE`, `NO_MORE_CLAUSES`) and the all-caps short
  forms (`SUSP`, `PROCEED`, `OTHERWISE`, `UNKNOWN`, `BCONST`,
  `BSTRUCTC`) carry through unchanged — `_` is permitted in C#
  identifiers; only the analyzer style rule (off) would object.
  Authoritative both sides; no escalation.

### rf-dart-deprecated-to-csharp-obsolete — `@deprecated` on static methods (cache hit)

- Cache hit. Same authoritative finding already used in
  `opcodes.dart.md` (covering `@deprecated` on the underlying
  `UnionSiAndGoto` / `ResetAndGoto` classes). Microsoft Learn
  `ObsoleteAttribute` documentation explicitly notes
  `AttributeTargets.All` — the same `[Obsolete]` attribute
  applies equally to types and methods.
- Conclusion (per cached finding): each `@deprecated static` →
  `[Obsolete] public static`. Advisory-only deprecation warning;
  no runtime effect. Authoritative cache hit; no escalation.

### rf-dart-required-named-param-to-csharp-required-arg — `headWriter`/`headReader` forwarding (cache hit)

- Cache hit. Same authoritative finding already used in
  `opcodes_v2.dart.md` for the underlying
  `HeadVariable(this.varIndex, {required this.isReader})`
  constructor. The factories `headWriter(int)` / `headReader(int)`
  forward a literal `false` / `true` for the `isReader:` named
  argument; the C# target uses C# named-argument call syntax
  (`isReader: false`) on the (no-default) constructor parameter,
  which forces mandatoriness exactly as Dart `required` does.
- Conclusion (per cached finding): the two mode-named factories
  survive intact; collapsing them to a single boolean-parameter
  helper would change every call site's shape. Authoritative
  cache hit; no escalation.

### rf-dart-named-default-param-to-csharp-optional-arg — `unifyVoid({int count = 1})` (cache hit)

- Cache hit. Same authoritative finding already used in
  `opcodes.dart.md` for the underlying
  `UnifyVoid({this.count = 1})` constructor. The factory simply
  forwards the default through; both languages permit
  compile-time-constant defaults on the equivalent shapes
  (Dart named-param default, C# optional-arg default).
- Conclusion (per cached finding): `public static UnifyVoid
  UnifyVoid(long count = 1) => new UnifyVoid(count: count);` —
  default preserved, forwarding shape preserved. Authoritative
  cache hit; no escalation.

## Notes

- No Stream/Future/async, no isolates, no `late`/`mixin`/
  `extension`, no generics-with-bounds, no `sealed` classes, no
  enums, no records, no bitwise/shift/arithmetic, no overflow
  path, no nullable-dereference, no callable-objects — those
  well-known nuances are ABSENT in this file and are correctly
  not asserted.
- Trivial / non-construct elements: the file/section comments
  `// lowerCamelCase helpers`, `// New spec-compliant control
  flow instructions`, `// Legacy (deprecated)`, `// Guard
  instructions`, `// List-specific instructions`, `//
  Environment frame instructions`, `// Utility instructions`,
  `// UPPERCASE aliases`, `// New spec-compliant control flow
  (UPPERCASE)` map mechanically to `//` C# comments (no research).
- The `@deprecated` annotations are subsumed by
  rf-dart-deprecated-to-csharp-obsolete on each affected
  factory; they are NOT a separate construct.
- Load-bearing decisions: (i) `public static class BC` (not a
  regular class) is the faithful target for the
  namespace-of-helpers; (ii) BOTH naming surfaces
  (camelCase + UPPERCASE) MUST survive — the Dart
  `// ignore_for_file: non_constant_identifier_names`
  directive's whole purpose is to permit the deliberate dual
  surface, and the tombstone callers (`compiler/codegen.dart`
  and four test files) read with that dual surface; (iii) the
  v1/v2 prefix-import distinction (`opv2.HeadVariable` /
  `opv2.Unknown`) MUST emit qualified (`V2.HeadVariable` /
  `V2.Unknown`), never as bare names, per
  `opcodes_v2.dart.md` and `runner.dart.md`; (iv) the Dart
  trailing-underscore keyword escape `try_` MUST map to C#
  `@try` (or be deliberately renamed), NEVER mechanically
  transliterated to a C# `try_` identifier (which would be a
  different name from `@try`).
- Zero escalations: every non-trivial construct resolved from
  authoritative Dart (dart.dev) and/or .NET
  (learn.microsoft.com) official documentation; every cross-file
  type reference is a cache hit on a sibling convspec (FR-024),
  with no idiom/research conflict and no undecidable construct.
