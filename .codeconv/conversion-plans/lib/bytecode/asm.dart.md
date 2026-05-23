---
path: lib/bytecode/asm.dart
cycle_group_id: 40
scc_siblings: []
generated_at: 2026-05-21T16:14:28Z
source_sha256: 8b1bffcb06af0db1fc0b8228d34209e8e47ccda5d74732ca5c7d6c86ad083839
schema_version: 1
---

# Conversion Plan: lib/bytecode/asm.dart

## 1. Source Analysis

Verbatim inspection of `glp_runtime_net/lib/bytecode/asm.dart` (93 lines,
single Dart compilation unit):

- **Line 1** — file-level Dart analyzer lint directive:
  `// ignore_for_file: non_constant_identifier_names`. Silences the
  Dart analyzer's `non_constant_identifier_names` rule that would
  otherwise flag the SCREAMING_CASE / SCREAMING_SNAKE_CASE alias
  methods. No semantic effect; the Dart compiler ignores it.

- **Lines 2–4** — three relative imports:
  - `import 'opcodes.dart';` (v1 opcode types: `Label`, `ClauseTry`,
    `GuardNeedReader`, `HeadBindWriter`, `Commit`, `ClauseNext`,
    `TryNextClause`, `NoMoreClauses`, `UnionSiAndGoto`,
    `ResetAndGoto`, `SuspendEnd`, `Proceed`, `BodySetConst`,
    `BodySetStructConstArgs`, `HeadStructure`, `UnifyConstant`,
    `UnifyVoid`, `HeadConstant`, `GetVariable`, `GetValue`,
    `PutConstant`, `PutStructure`, `SetConstant`, `Otherwise`,
    `Spawn`, `Requeue`, `Guard`, `Ground`, `Known`, `HeadNil`,
    `HeadList`, `PutNil`, `PutList`, `Allocate`, `Deallocate`,
    `Nop`, `Halt`, marker interface `Op`).
  - `import 'opcodes_v2.dart' as opv2;` — PREFIX-ALIASED import;
    only `opv2.HeadVariable` and `opv2.Unknown` are referenced.
  - `import 'runner.dart';` — `BytecodeProgram` (return type of
    `prog(...)`).

- **Lines 6–93** — `class BC { ... }`: pure namespace-of-helpers.
  No instance fields. No instance constructor. No `extends` /
  `implements` / `with` clauses. Every member is `static`.

- **~50 static methods**, all one-line arrow-bodied factories of the
  shape `static <T> <name>(<params>) => <T>(<args>);`. Examples
  verified verbatim:
  - `static Label l(String name) => Label(name);` (line 8)
  - `static ClauseTry try_() => ClauseTry();` (line 9) — trailing
    underscore escapes the Dart keyword `try`.
  - `static GuardNeedReader r(int readerId) => GuardNeedReader(readerId);`
  - `static HeadBindWriter w(int writerId) => HeadBindWriter(writerId);`
  - `static Commit commit() => Commit();`
  - `static ClauseNext clauseNext(String label) => ClauseNext(label);`
  - `static TryNextClause tryNextClause() => TryNextClause();`
  - `static NoMoreClauses noMoreClauses() => NoMoreClauses();`
  - `@deprecated static UnionSiAndGoto u(String label) => UnionSiAndGoto(label);`
  - `@deprecated static ResetAndGoto next(String label) => ResetAndGoto(label);`
  - `static SuspendEnd susp() => SuspendEnd();`
  - `static Proceed proceed() => Proceed();`
  - `static BodySetConst bconst(int writerId, Object? v) => BodySetConst(writerId, v);`
  - `static BodySetStructConstArgs bstructC(int writerId, String f, List<Object?> constArgs) => BodySetStructConstArgs(writerId, f, constArgs);`
  - `static HeadStructure headStruct(String functor, int arity, int argSlot) => HeadStructure(functor, arity, argSlot);`
  - `static opv2.HeadVariable headWriter(int varIndex) => opv2.HeadVariable(varIndex, isReader: false);`
  - `static opv2.HeadVariable headReader(int varIndex) => opv2.HeadVariable(varIndex, isReader: true);`
  - `static UnifyConstant unifyConst(Object? value) => UnifyConstant(value);`
  - `static UnifyVoid unifyVoid({int count = 1}) => UnifyVoid(count: count);` — named-optional param, default 1.
  - `static HeadConstant headConst(Object? value, int argSlot) => HeadConstant(value, argSlot);`
  - `static GetVariable getVar(int varIndex, int argSlot) => GetVariable(varIndex, argSlot);`
  - `static GetValue getVal(int varIndex, int argSlot) => GetValue(varIndex, argSlot);`
  - `static PutConstant putConst(Object? value, int argSlot) => PutConstant(value, argSlot);`
  - `static PutStructure putStructure(String functor, int arity, int argSlot) => PutStructure(functor, arity, argSlot);`
  - `static SetConstant setConst(Object? value) => SetConstant(value);`
  - `static Otherwise otherwise() => Otherwise();`
  - `static opv2.Unknown unknown(int varIndex) => opv2.Unknown(varIndex);`
  - `static Spawn spawn(String label, int arity) => Spawn(label, arity);`
  - `static Requeue requeue(String label, int arity) => Requeue(label, arity);`
  - `static Guard guard(String label, int arity) => Guard(label, arity);`
  - `static Ground ground(int varIndex) => Ground(varIndex);`
  - `static Known known(int varIndex) => Known(varIndex);`
  - `static HeadNil headNil(int argSlot) => HeadNil(argSlot);`
  - `static HeadList headList(int argSlot) => HeadList(argSlot);`
  - `static PutNil putNil(int argSlot) => PutNil(argSlot);`
  - `static PutList putList(int argSlot) => PutList(argSlot);`
  - `static Allocate allocate(int slots) => Allocate(slots);`
  - `static Deallocate deallocate() => Deallocate();`
  - `static Nop nop() => Nop();`
  - `static Halt halt() => Halt();`

- **UPPERCASE alias block** (lines 67–90) — parallel naming surface
  forwarding to the camelCase short form, each verified verbatim:
  - `static Label L(String name) => l(name);`
  - `static ClauseTry TRY() => try_();`
  - `static GuardNeedReader R(int readerId) => r(readerId);`
  - `static HeadBindWriter W(int writerId) => w(writerId);`
  - `static Commit COMMIT() => commit();`
  - `static ClauseNext CLAUSE_NEXT(String label) => clauseNext(label);`
  - `static TryNextClause TRY_NEXT_CLAUSE() => tryNextClause();`
  - `static NoMoreClauses NO_MORE_CLAUSES() => noMoreClauses();`
  - `@deprecated static UnionSiAndGoto U(String label) => u(label);`
  - `@deprecated static ResetAndGoto NEXT(String label) => next(label);`
  - `static SuspendEnd SUSP() => susp();`
  - `static Proceed PROCEED() => proceed();`
  - `static Otherwise OTHERWISE() => otherwise();`
  - `static opv2.Unknown UNKNOWN(int varIndex) => unknown(varIndex);`
  - `static BodySetConst BCONST(int writerId, Object? v) => bconst(writerId, v);`
  - `static BodySetStructConstArgs BSTRUCTC(int writerId, String f, List<Object?> constArgs) => bstructC(writerId, f, constArgs);`

- **Line 92** — `static BytecodeProgram prog(List<Op> ops) => BytecodeProgram(ops);`
  Program-bundle factory. Uses `List<Op>` (v1 marker interface).

- **Section comments** (no semantic content): `// lowerCamelCase
  helpers`, `// New spec-compliant control flow instructions`,
  `// Legacy (deprecated)`, `// Guard instructions`,
  `// List-specific instructions`, `// Environment frame instructions`,
  `// Utility instructions`, `// UPPERCASE aliases`,
  `// New spec-compliant control flow (UPPERCASE)`.

- Absent constructs (correctly NOT asserted): no Stream/Future/async,
  no isolates, no `late`/`mixin`/`extension`, no generics-with-bounds,
  no `sealed`, no enums, no records, no bitwise/shift/arithmetic, no
  nullable-dereference, no callable-objects.

## 2. Dart → C#/.NET Conversion Plan

The plan mirrors the convspec verbatim; each construct entry below
references its convspec `construct_key` and `research_finding_id`.

### 2.1 `// ignore_for_file: non_constant_identifier_names` → ELIDED

- **Construct key**: `dart.analyzer_lint_directive.ignore_for_file`
- **Finding**: `rf-dart-ignore-for-file-elided`
- **Dart** → **C#/.NET**: Elide. The directive is a Dart-analyzer-only
  lint suppression with NO behavioural effect (the source compiles
  identically without it). The .NET counterpart style rule
  `IDE1006` ("Naming styles") is OFF by default; absent any
  `.editorconfig` opt-in there is nothing to suppress. Codegen MUST
  NOT emit `#pragma warning disable`, `[SuppressMessage]`, or
  `.editorconfig` edits. Codegen MAY emit a single `//` comment
  documenting why SCREAMING_CASE alias methods are preserved
  (optional, no semantic content).
- **Nuance**: lint-vs-compiler: Dart `// ignore_for_file:` targets
  Dart analyzer only; the Dart compiler ignores it. C# analyzer
  diagnostics reach through attributes / pragmas / `.editorconfig`,
  not file-leading comments. Faithful conversion preserves
  OBSERVED BEHAVIOUR (methods compile), not the suppression token.

### 2.2 Three relative imports (one with prefix alias) → `using` directives

- **Construct key**: `dart.import_directive.relative_to_using_namespace_with_prefix_alias`
- **Finding**: `rf-dart-import-relative-to-csharp-using-namespace`
- **Dart** → **C#/.NET**:
  - `import 'opcodes.dart';` → `using <root>.Bytecode;` (v1 opcode
    types; same namespace as `opcodes.dart`).
  - `import 'opcodes_v2.dart' as opv2;` → `using <root>.Bytecode.V2;`
    (kept DISTINCT from V1 per `opcodes_v2.dart.md`). The `as opv2`
    prefix is honoured by namespace-qualified type references at
    every use site (`V2.HeadVariable`, `V2.Unknown`), NEVER bare
    `HeadVariable` / `Unknown`.
  - `import 'runner.dart';` → `using <root>.Bytecode;` (same
    namespace; `BytecodeProgram`).
- **Nuance**: Prefix-import is LOAD-BEARING. Dart `as opv2` is a
  true symbol prefix disambiguating colliding class names; the C#
  equivalent is namespace-qualified usage (`V2.HeadVariable`). The
  v2 marker interface `IOpV2` is deliberately disjoint from v1
  `IOp` (per `opcodes_v2.dart.md`). Codegen MUST NOT collapse v1
  and v2 opcode types into one namespace.

### 2.3 `class BC` → `public static class BC`

- **Construct key**: `dart.class.namespace_of_static_helpers`
- **Finding**: `rf-dart-namespace-class-of-statics-to-csharp-static-class`
- **Dart** → **C#/.NET**: Emit as `public static class BC` in the
  `lib/bytecode/`-mirroring namespace. The `static` class modifier
  (a) forbids instantiation (`new BC()` is a compile error),
  (b) forbids inheritance, (c) mandates every member be `static` —
  exactly the Dart not-instantiable namespace-class shape. Do NOT
  emit a `private`-ctor class (pre-2.0-C# pattern). Do NOT emit
  top-level functions (C# has none outside file-scoped programs).
  Visibility is `public` (Dart `BC` is library-public; callable
  from `compiler/codegen.dart` and the test files in tombstone
  callers).
- **Nuance**: C# `static class` is single-purpose for the
  namespace-of-helpers role. Reference-vs-value: not implicated
  (static class is not instantiable). Null-safety: not implicated
  (no instance fields). Async: not implicated (all factories
  synchronous). Inheritance: a `static class` cannot be a base —
  matches Dart intent.

### 2.4 Every `static T name(...) => T(...);` → `public static T Name(...) => new T(...);`

- **Construct key**: `dart.static_method.one_line_arrow_factory_returning_new_instance`
- **Finding**: `rf-dart-static-arrow-factory-to-csharp-static-expression-bodied-method`
- **Dart** → **C#/.NET**: Each factory becomes a C# `public static`
  method on `static class BC` with an expression-bodied member
  returning a new instance: `public static Label L(string name) =>
  new Label(name);`. Use `new <Type>(...)` (NOT target-typed
  `new(...)` — the return-type annotation already names the type;
  semantically equivalent, codegen choice).

  Parameter-type cache hits (verbatim from convspec):
  - Dart `String` → C# `string` (cache hit on `opcodes.dart.md`
    rf-dart-typedef-string-to-csharp-using-alias).
  - Dart `int` → C# `long` (cache hit on `opcodes.dart.md`
    rf-dart-int-to-csharp-long-width — every `int` here is a
    register index / writer id / reader id / arity / argSlot).
  - Dart `Object?` → C# `object?` (cache hit on `opcodes.dart.md`
    rf-dart-objectq-to-csharp-objectq).
  - Dart `List<Object?>` → C# `List<object?>` (cache hit, same
    finding).
  - Dart `List<Op>` → C# `List<IOp>` (cache hit on
    `opcodes.dart.md` rf-dart-abstract-marker-to-csharp-interface
    — `Op` is the v1 marker interface).
- **Nuance**: Dart 2 elides the `new` keyword; C# requires `new`
  for constructor invocation (or target-typed `new(...)` from C# 9).
  Value-vs-reference: every constructed IR-node is a reference
  class (per `opcodes.dart.md`/`opcodes_v2.dart.md`); each factory
  call allocates a new heap object — preserved by the C# `new`.
  Generics: only `List<Op>` and `List<Object?>` — both closed
  generic at the call site, no `where T : ...`, no variance.
  Static dispatch: `BC.l('x')` / `BC.L("x")` resolved at compile
  time, no v-table, no boxing. Factories are pure (no side effects,
  no null deref, no state) → trivially thread-safe; no
  `[ThreadStatic]` / locking required.

### 2.5 `static ClauseTry try_() => ClauseTry();` → `public static ClauseTry @try() => new ClauseTry();`

- **Construct key**: `dart.identifier.trailing_underscore_to_escape_reserved_word`
- **Finding**: `rf-dart-trailing-underscore-keyword-escape-to-csharp-at-prefix`
- **Dart** → **C#/.NET**: Default (recommended) — emit as the
  verbatim-prefixed identifier `@try`. C# accepts any reserved
  keyword as an identifier when prefixed with `@`, preserving the
  conceptual mapping `try` exactly; the call site is `BC.@try()`.
  Alternate — emit as `Tr()` or `TryOp()` (drop the underscore,
  choose a non-keyword name; loses visual correspondence to Dart's
  `try_`). FORBIDDEN — `Try()` PascalCase WITHOUT the `@` escape:
  syntactically valid (`Try` is not a C# keyword) but loses the
  readability cue that the symbol corresponds to the GLP `try`
  control-flow opcode. Default per spec is **(a) `@try`**.
- **Nuance**: Dart's escape is a trailing underscore (idiomatic,
  not syntactic — `try_` is just a regular identifier). C#'s
  escape is a leading `@` (syntactic — `@try` IS the identifier
  whose name is the keyword spelling). Semantically equivalent but
  syntactically distinct. Codegen MUST NOT mechanically
  transliterate `try_` ↔ `try_` across the conversion (C# `try_`
  would be a DIFFERENT identifier from `@try`). UPPERCASE alias
  `TRY()` is unaffected (uppercase TRY is not a C# keyword) and
  maps to `TRY()` per §2.6.

### 2.6 Parallel UPPERCASE/SCREAMING_CASE alias methods → PRESERVED VERBATIM

- **Construct key**: `dart.naming.parallel_uppercase_alias_methods`
- **Finding**: `rf-dart-uppercase-alias-method-naming`
- **Dart** → **C#/.NET**: PRESERVE BOTH NAMING SURFACES VERBATIM.
  Every camelCase factory AND every UPPERCASE-aliased factory MUST
  exist on `static class BC` in the target. The UPPERCASE aliases
  (`L`, `TRY`, `R`, `W`, `COMMIT`, `CLAUSE_NEXT`,
  `TRY_NEXT_CLAUSE`, `NO_MORE_CLAUSES`, `U`, `NEXT`, `SUSP`,
  `PROCEED`, `OTHERWISE`, `UNKNOWN`, `BCONST`, `BSTRUCTC`) are
  LOAD-BEARING for the assembly-style readability of the test
  files AND for `compiler/codegen.dart` — pruning would break
  every caller.

  Casing collision: under Microsoft naming the camelCase short
  forms convert to PascalCase (`l → L`, `r → R`, `w → W`), which
  collides with the already-existing UPPERCASE aliases for the
  single-letter cases. **Resolution** — codegen MUST keep the two
  surfaces DISTINCT. Two acceptable target shapes:
  - **(i) DEFAULT** — keep the short-form camelCase verbatim
    (`BC.l`, `BC.r`, `BC.w`, `BC.commit`, `BC.clauseNext`) AND
    the UPPERCASE alias verbatim (`BC.L`, `BC.R`, `BC.W`,
    `BC.COMMIT`, `BC.CLAUSE_NEXT`). The C# compiler accepts ANY
    casing; only `IDE1006` (off by default) would complain.
  - **(ii) ALTERNATE** — rename the short-form camelCase to a
    distinct PascalCase surface (`BC.Lbl`, `BC.Rdr`, `BC.Wtr`,
    `BC.Commit`, `BC.ClauseNext`) and keep UPPERCASE aliases.

  Default per spec is **(i) — verbatim preservation** — because
  every caller reads with the Dart naming and the parallel-surface
  intent is the WHOLE POINT of the file.
- **Nuance**: SCREAMING_CASE in C# is conventionally reserved for
  `const` fields, but C# imposes NO syntactic restriction (only
  `IDE1006`); the conversion deliberately keeps SCREAMING_CASE
  method names to mirror Dart 1-to-1. C# identifiers are
  case-sensitive (`L` and `l` ARE different methods at the IL
  level). Underscore preservation: `_` is permitted in C#
  identifiers (`CLAUSE_NEXT` is a valid C# identifier).

### 2.7 `@deprecated` static methods → `[Obsolete]` static methods

- **Construct key**: `dart.deprecated_annotation_on_static_method`
- **Finding**: `rf-dart-deprecated-to-csharp-obsolete`
- **Dart** → **C#/.NET**: Each `@deprecated static` method maps to
  C# `[System.Obsolete]` attribute on the corresponding
  `public static` method. The methods are PRESERVED (not deleted)
  because the source comments label them `// Legacy (deprecated)`
  for backward compat with tests AND because `opcodes.dart.md`
  rf-dart-deprecated-to-csharp-obsolete already preserved the
  underlying opcode classes. `@deprecated` on the camelCase form
  AND on the UPPERCASE alias forms `[Obsolete]` independently:
  both must survive (the forwarding chain `U → u → new
  UnionSiAndGoto(...)` means a caller of `BC.U` already gets two
  warning sites, matching Dart).
- **Nuance**: The `opcodes.dart.md` cache hit covered `@deprecated`
  on a CLASS; here it is `@deprecated` on a STATIC METHOD. The
  mapping is identical — `[Obsolete]` applies equally to types,
  methods, constructors, properties, and fields
  (`AttributeTargets.All` per Microsoft Learn `ObsoleteAttribute`).
  Both annotations are advisory (analyzer/compiler warning); no
  runtime effect.

### 2.8 `headWriter` / `headReader` — named-required-arg forwarding via qualified ctor

- **Construct key**: `dart.method.named_required_arg_forwarded_via_qualified_constructor_call`
- **Finding**: `rf-dart-required-named-param-to-csharp-required-arg`
- **Dart** → **C#/.NET**:
  `public static V2.HeadVariable HeadWriter(long varIndex) => new V2.HeadVariable(varIndex, isReader: false);`
  and the analogous `HeadReader(...)` with `isReader: true`. The
  `opv2.HeadVariable` reference resolves under §2.2
  (namespace-qualified `V2.HeadVariable`, NOT bare). The
  named-argument call `isReader: false` maps verbatim — C#
  named-argument syntax accepts the same `name: value` form and
  the underlying constructor parameter `isReader` (no default)
  preserves mandatoriness (cache hit on `opcodes_v2.dart.md`
  rf-dart-required-named-param-to-csharp-required-arg).
- **Nuance**: The file deliberately provides TWO factories per
  WAM HEAD/UNIFY/etc. mode (writer vs reader) instead of a single
  factory taking a `bool isReader` parameter — this hides the
  boolean at the call site so bytecode listings read with
  intent-named mnemonics. Codegen MUST preserve this — DO NOT
  collapse the two factories into a single helper; doing so would
  change the call-site shape of every caller (including
  `compiler/codegen.dart`). Value-vs-reference: returned
  `V2.HeadVariable` is a reference class. Null-safety / async:
  not implicated.

### 2.9 `unifyVoid({int count = 1})` — named-optional-with-default forwarding

- **Construct key**: `dart.static_method.named_optional_param_forwarded_with_default`
- **Finding**: `rf-dart-named-default-param-to-csharp-optional-arg`
- **Dart** → **C#/.NET**:
  `public static UnifyVoid UnifyVoid(long count = 1) => new UnifyVoid(count: count);`.
  The forwarded constructor call uses C# named-argument syntax
  (`count: count`) — semantically equivalent to positional pass
  (single parameter), but preserving the named form matches Dart
  1-to-1 and remains correct if the constructor signature gains
  parameters later. Default-value: literal `1` is a compile-time
  constant in both languages.
- **Nuance**: Method-name-collides-with-type-name — the factory
  `unifyVoid` and the constructor it wraps are both literally
  `UnifyVoid` after PascalCase casing; a static method and a
  constructor share the spelling but live in different scopes
  (`BC.UnifyVoid(...)` vs. `new UnifyVoid(...)`), and C# resolves
  them by syntactic context. No ambiguity at compile time. No
  drift in defaults; forwarding semantics identical-cost.

## 3. Decomposed Task Units

- T1. Emit `using <root>.Bytecode;` and `using <root>.Bytecode.V2;` directives in the converted file header (replaces `import 'opcodes.dart'`, `import 'opcodes_v2.dart' as opv2`, `import 'runner.dart'`). — done
- T2. Elide `// ignore_for_file: non_constant_identifier_names` (NO `#pragma`, NO `[SuppressMessage]`, NO `.editorconfig` edit). — done
- T3. Emit `public static class BC` in the `lib/bytecode/`-mirroring namespace. — done
- T4. Emit factory `public static Label L(string name) => new Label(name);` plus UPPERCASE alias preserving the dual-surface (two distinct C# methods sharing the L/l casing). — done
- T5. Emit factory `public static ClauseTry @try() => new ClauseTry();` (default `@try`); UPPERCASE alias `TRY()` forwards to `@try()`. — done
- T6. Emit factory `public static GuardNeedReader R(long readerId) => new GuardNeedReader(readerId);` plus camelCase short form (R/r distinct). — done
- T7. Emit factory `public static HeadBindWriter W(long writerId) => new HeadBindWriter(writerId);` plus camelCase short form (W/w distinct). — done
- T8. Emit factory `public static Commit Commit() => new Commit();` plus UPPERCASE alias `COMMIT()`. — done
- T9. Emit factory `public static ClauseNext ClauseNext(string label) => new ClauseNext(label);` plus SCREAMING_SNAKE alias `CLAUSE_NEXT`. — done
- T10. Emit factory `public static TryNextClause TryNextClause() => new TryNextClause();` plus SCREAMING_SNAKE alias `TRY_NEXT_CLAUSE`. — done
- T11. Emit factory `public static NoMoreClauses NoMoreClauses() => new NoMoreClauses();` plus SCREAMING_SNAKE alias `NO_MORE_CLAUSES`. — done
- T12. Emit `[Obsolete] public static UnionSiAndGoto U(string label) => new UnionSiAndGoto(label);` AND `[Obsolete]` lowercase `u` AND `[Obsolete]` UPPERCASE `U` (all three flavours obsolete-tagged). — done
- T13. Emit `[Obsolete] public static ResetAndGoto NEXT(string label) => new ResetAndGoto(label);` AND `[Obsolete]` lowercase `next` (both obsolete-tagged). — done
- T14. Emit factory `public static SuspendEnd Susp() => new SuspendEnd();` plus UPPERCASE alias `SUSP()`. — done
- T15. Emit factory `public static Proceed Proceed() => new Proceed();` plus UPPERCASE alias `PROCEED()`. — done
- T16. Emit factory `public static BodySetConst BConst(long writerId, object? v) => new BodySetConst(writerId, v);` plus SCREAMING_CASE alias `BCONST`. — done
- T17. Emit factory `public static BodySetStructConstArgs BStructC(long writerId, string f, List<object?> constArgs) => new BodySetStructConstArgs(writerId, f, constArgs);` plus SCREAMING_CASE alias `BSTRUCTC`. — done
- T18. Emit factory `public static HeadStructure HeadStruct(string functor, long arity, long argSlot) => new HeadStructure(functor, arity, argSlot);`. — done
- T19. Emit `public static V2.HeadVariable HeadWriter(long varIndex) => new V2.HeadVariable(varIndex, isReader: false);` (namespace-qualified V2, NOT bare `HeadVariable`). — done
- T20. Emit `public static V2.HeadVariable HeadReader(long varIndex) => new V2.HeadVariable(varIndex, isReader: true);`. — done
- T21. Emit factory `public static UnifyConstant UnifyConst(object? value) => new UnifyConstant(value);`. — done
- T22. Emit factory `public static UnifyVoid UnifyVoid(long count = 1) => new UnifyVoid(count: count);` (optional default `1` preserved; named-arg forwarding). — done
- T23. Emit factory `public static HeadConstant HeadConst(object? value, long argSlot) => new HeadConstant(value, argSlot);`. — done
- T24. Emit factory `public static GetVariable GetVar(long varIndex, long argSlot) => new GetVariable(varIndex, argSlot);`. — done
- T25. Emit factory `public static GetValue GetVal(long varIndex, long argSlot) => new GetValue(varIndex, argSlot);`. — done
- T26. Emit factory `public static PutConstant PutConst(object? value, long argSlot) => new PutConstant(value, argSlot);`. — done
- T27. Emit factory `public static PutStructure PutStructure(string functor, long arity, long argSlot) => new PutStructure(functor, arity, argSlot);`. — done
- T28. Emit factory `public static SetConstant SetConst(object? value) => new SetConstant(value);`. — done
- T29. Emit factory `public static Otherwise Otherwise() => new Otherwise();` plus UPPERCASE alias `OTHERWISE()`. — done
- T30. Emit factory `public static V2.Unknown Unknown(long varIndex) => new V2.Unknown(varIndex);` (namespace-qualified V2) plus UPPERCASE alias `UNKNOWN()`. — done
- T31. Emit factory `public static Spawn Spawn(string label, long arity) => new Spawn(label, arity);`. — done
- T32. Emit factory `public static Requeue Requeue(string label, long arity) => new Requeue(label, arity);`. — done
- T33. Emit factory `public static Guard Guard(string label, long arity) => new Guard(label, arity);`. — done
- T34. Emit factory `public static Ground Ground(long varIndex) => new Ground(varIndex);`. — done
- T35. Emit factory `public static Known Known(long varIndex) => new Known(varIndex);`. — done
- T36. Emit factory `public static HeadNil HeadNil(long argSlot) => new HeadNil(argSlot);`. — done
- T37. Emit factory `public static HeadList HeadList(long argSlot) => new HeadList(argSlot);`. — done
- T38. Emit factory `public static PutNil PutNil(long argSlot) => new PutNil(argSlot);`. — done
- T39. Emit factory `public static PutList PutList(long argSlot) => new PutList(argSlot);`. — done
- T40. Emit factory `public static Allocate Allocate(long slots) => new Allocate(slots);`. — done
- T41. Emit factory `public static Deallocate Deallocate() => new Deallocate();`. — done
- T42. Emit factory `public static Nop Nop() => new Nop();`. — done
- T43. Emit factory `public static Halt Halt() => new Halt();`. — done
- T44. Emit factory `public static BytecodeProgram Prog(List<IOp> ops) => new BytecodeProgram(ops);` (List<Op> → List<IOp> via v1 marker-interface cache hit). — done
- T45. Section comments (`// lowerCamelCase helpers`, `// New spec-compliant control flow instructions`, `// Legacy (deprecated)`, `// Guard instructions`, `// List-specific instructions`, `// Environment frame instructions`, `// Utility instructions`, `// UPPERCASE aliases`, `// New spec-compliant control flow (UPPERCASE)`) carry through as `//` C# comments unchanged. — done

## 4. Research Findings

none required — every construct resolves from authoritative Dart
(dart.dev) and/or .NET (learn.microsoft.com) findings already
ratified in the convspec; every cross-file type reference is a
cache hit on a sibling convspec (FR-024). Specifically:

- `rf-dart-ignore-for-file-elided` — Dart-analyzer-only directive;
  elided. (Authoritative both sides, no escalation.)
- `rf-dart-import-relative-to-csharp-using-namespace` — cache hit
  from `runner.dart.md` / `heap_fcp.dart.md` / `variable_table.dart.md`.
- `rf-dart-namespace-class-of-statics-to-csharp-static-class` —
  authoritative Microsoft Learn "Static Classes and Static Class
  Members" + Dart "Classes".
- `rf-dart-static-arrow-factory-to-csharp-static-expression-bodied-method`
  — authoritative Dart "Functions" + Microsoft Learn
  "Expression-bodied members".
- `rf-dart-trailing-underscore-keyword-escape-to-csharp-at-prefix`
  — authoritative Dart "Keywords" + Microsoft Learn
  "Identifier names" (verbatim `@` prefix rule).
- `rf-dart-uppercase-alias-method-naming` — authoritative Microsoft
  Learn "Identifier names" (case-sensitivity) + "Naming rules"
  (`IDE1006` off by default).
- `rf-dart-deprecated-to-csharp-obsolete` — cache hit from
  `opcodes.dart.md`.
- `rf-dart-required-named-param-to-csharp-required-arg` — cache hit
  from `opcodes_v2.dart.md`.
- `rf-dart-named-default-param-to-csharp-optional-arg` — cache hit
  from `opcodes.dart.md`.

Parameter-type cache hits chained from `opcodes.dart.md`:
- `rf-dart-typedef-string-to-csharp-using-alias` (String → string).
- `rf-dart-int-to-csharp-long-width` (int → long).
- `rf-dart-objectq-to-csharp-objectq` (Object? → object?).
- `rf-dart-abstract-marker-to-csharp-interface` (Op → IOp).

## 5. Consistency Pass

fixed — derived from convspec
`.codeconv/conversion-specs/lib/bytecode/asm.dart.md` (ratified),
which itself derives from authoritative Dart docs (dart.dev), .NET
docs (learn.microsoft.com), and cache hits on sibling convspecs
(`opcodes.dart.md`, `opcodes_v2.dart.md`, `runner.dart.md`,
`heap_fcp.dart.md`, `variable_table.dart.md`) per FR-024. The plan
mirrors the convspec verbatim:

- §2.1 (lint directive elision) mirrors construct
  `dart.analyzer_lint_directive.ignore_for_file` / finding
  `rf-dart-ignore-for-file-elided`.
- §2.2 (three imports incl. prefix alias) mirrors
  `dart.import_directive.relative_to_using_namespace_with_prefix_alias`
  / `rf-dart-import-relative-to-csharp-using-namespace`.
- §2.3 (`public static class BC`) mirrors
  `dart.class.namespace_of_static_helpers` /
  `rf-dart-namespace-class-of-statics-to-csharp-static-class`.
- §2.4 (one-line arrow factories) mirrors
  `dart.static_method.one_line_arrow_factory_returning_new_instance`
  / `rf-dart-static-arrow-factory-to-csharp-static-expression-bodied-method`,
  including all parameter-type cache hits (String/int/Object?/
  List<Op>/List<Object?>).
- §2.5 (`@try` keyword escape) mirrors
  `dart.identifier.trailing_underscore_to_escape_reserved_word` /
  `rf-dart-trailing-underscore-keyword-escape-to-csharp-at-prefix`.
- §2.6 (parallel UPPERCASE aliases) mirrors
  `dart.naming.parallel_uppercase_alias_methods` /
  `rf-dart-uppercase-alias-method-naming`, including the
  single-letter casing-collision resolution (option (i) verbatim
  preservation as default).
- §2.7 (`[Obsolete]`) mirrors
  `dart.deprecated_annotation_on_static_method` /
  `rf-dart-deprecated-to-csharp-obsolete`.
- §2.8 (`headWriter`/`headReader`) mirrors
  `dart.method.named_required_arg_forwarded_via_qualified_constructor_call`
  / `rf-dart-required-named-param-to-csharp-required-arg`.
- §2.9 (`unifyVoid` default 1) mirrors
  `dart.static_method.named_optional_param_forwarded_with_default`
  / `rf-dart-named-default-param-to-csharp-optional-arg`.

§3 task units T1–T45 mirror the convspec's `conversion_units` list
verbatim. §4 enumerates the same finding ids as the convspec
`research_finding_id` fields. No drift, no conflict, no
undecidable construct.

## 6. Escalations

None.
