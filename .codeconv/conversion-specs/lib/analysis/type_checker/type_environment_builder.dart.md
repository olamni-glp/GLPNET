# Conversion Spec — lib/analysis/type_checker/type_environment_builder.dart

> Conversion-spec artifact (FR-011). Spec-only (FR-023) — no compilable C#.
> Per-construct deep analysis + authoritative-doc research (FR-024); cached
> idioms reused verbatim per FR-012/SC-007.

```yaml
schema_version: 1
source_path: lib/analysis/type_checker/type_environment_builder.dart
source_sha256: dfd2a18574bdee84c8b2875529f6401ebd0a5cb60c16c619db3a842b519793fa
target_code_unit: lib/analysis/type_checker/type_environment_builder.cs
constructs:
  - construct_key: dart.exception_class.implements_exception_with_message_line_column
    source_form: >-
      class RedefinitionError implements Exception {
        final String message; final int line; final int column;
        RedefinitionError(this.message, this.line, this.column);
        @override String toString() => '$message at line $line, column $column';
      }
      // identical shape repeated for CircularAliasError, NonDeterministicTypeError,
      // AliasExpansionError — four named exception types sharing one structure.
    target_decision: >-
      Emit four `public sealed class <Name> : Exception` classes
      (`RedefinitionError`, `CircularAliasError`, `NonDeterministicTypeError`,
      `AliasExpansionError`) in namespace `Glp.Analysis.TypeChecker`. Each
      derives from `System.Exception` (NOT a custom interface — .NET has no
      throwable interface; cached idiom). The Dart `message` field routes to
      base via `: base(message)`; `Line`/`Column` become get-only properties
      set from the constructor (Dart `final` → C# get-only auto-property
      assigned in ctor). `ToString()` overrides the base, REPLACING (not
      extending) `Exception.ToString` so the diagnostic byte-shape
      ("<message> at line <L>, column <C>") matches Dart exactly — same
      semantic decision as `lib/compiler/error.dart`'s `CompileError`. NO
      parameterless ctor, NO `(string, Exception inner)` chaining ctor: the
      Dart source declares only one ctor and we preserve that surface
      exactly (FR-013 / spec-faithfulness — do not manufacture an
      instantiation surface the Dart code lacks). NO `[Serializable]` —
      none of the four are crossed over AppDomain / remoting boundaries in
      this codebase, and adding it would manufacture a contract Dart does
      not have. The four classes are emitted as `sealed` because they have
      no Dart subtypes (single-level domain exception leaves).
    idiom_id: dart-implements-exception-to-csharp-derive-system-exception
    research_finding_id: rf-dart-implements-exception-to-csharp-derive-system-exception
    nuance: >-
      Cached idiom reuse (FR-012/FR-024) from `lib/compiler/error.dart`. The
      load-bearing nuance is the Dart `Exception` interface vs .NET
      `System.Exception` base-class divergence: api.dart.dev documents
      Dart `Exception` as an "abstract interface … can only be implemented
      (not extended or mixed in)"; Microsoft Learn's
      `how-to-create-user-defined-exceptions` mandates derivation from
      `System.Exception` because .NET has no throwable interface. `message`
      MUST route through `: base(message)` so `Exception.Message`,
      catch-site consumers, and the default `Exception.ToString` (used if
      our override is ever bypassed) all work. Naming-suffix divergence
      (Microsoft Learn recommends `<Name>Exception` suffix, Dart source
      uses `<Name>Error`) is preserved verbatim — source-name fidelity
      over .NET naming-convention conformance, matching the precedent
      already escalated in `error.dart.md` (project policy, NOT this
      file's call to make). No reference-vs-value concern (exceptions are
      reference types in both languages). No async, no Stream, no
      isolates — synchronous error-info containers. `line`/`column` are
      Dart `int` → C# `long` per the recurring `rf-dart-int-to-csharp-long-width`
      precedent (opcodes.dart / error.dart) — preserved here for
      cross-file uniformity, no width hazard for line/column values.

  - construct_key: dart.override_tostring_with_string_interpolation_two_locations
    source_form: >-
      @override String toString() => '$message at line $line, column $column';
    target_decision: >-
      Emit `public override string ToString() => $"{Message} at line {Line},
      column {Column}";` on each of the four exception classes. C#
      interpolation `$"{x}"` is the documented twin of Dart `'$x'` /
      `'${expr}'` (cached idiom). Expression-bodied override (`=>`) maps
      1:1 to Dart's `=>`-arrow-bodied override. Override REPLACES (does
      not extend) the base `Exception.ToString` shape ("ClassName:
      <message>\n   at <stack>") — identical decision and rationale to
      `error.dart`'s diagnostic override; do NOT call `base.ToString()`,
      that would prepend type-name + stack trace and diverge from the
      Dart byte-shape.
    idiom_id: dart-tostring-interpolation-to-csharp-interpolated-string
    research_finding_id: rf-dart-tostring-interp-to-csharp-tostring-interp
    nuance: >-
      Cached idiom reuse (FR-012). Numeric interpolation in both Dart
      (`$line`, `$column`) and C# (`{Line}`, `{Column}`) uses invariant
      integer ToString — output text is locale-stable; no
      culture-sensitive drift hazard. `Message` is read from the base
      `Exception.Message` property (set via `: base(message)` in the
      ctor) — the override therefore agrees byte-for-byte with what
      `catch (Exception ex) { ex.ToString(); }` would observe, preserving
      the Dart contract where the bare field `message` and the
      `toString()` output are consistent. No `Stream`/`Future`/async
      concerns; pure pure formatting.

  - construct_key: dart.toplevel.private_nullable_string_mutable_state
    source_form: >-
      String? _preludeEnvironmentSource;
      void setPreludeEnvironmentSource(String source) {
        _preludeEnvironmentSource = source;
      }
    target_decision: >-
      Map the file-private mutable nullable string + its setter to a
      `static string?` field plus a `public static void` setter on the
      host static class `TypeEnvironmentBuilder`:
      `private static string? _preludeEnvironmentSource;`
      `public static void SetPreludeEnvironmentSource(string source) =>
      _preludeEnvironmentSource = source;`. Dart `_`-prefix library-
      private → C# `private` on the static field (tighter, correct since
      only `BuildPreludeEnvironment` reads it within the same class).
      `String?` (NRT-enabled) → `string?`. The setter is the canonical
      "engine initialisation hook" — the doc-comment "Call this once
      during engine initialization with the content of programs/self.glp"
      describes a single-writer-then-many-readers contract.
    idiom_id: dart-private-nullable-mutable-string-field-to-csharp-private-static-nullable
    research_finding_id: rf-csharp-static-nullable-field-thread-safety-considerations
    nuance: >-
      NEW idiom (file-first — sibling specs covered immutable `static
      const String` (prelude.dart) and `static readonly FrozenSet` —
      this is the FIRST mutable single-writer-many-readers static
      string field in the type_checker family). The load-bearing
      nuance is thread-safety / publication semantics. Dart isolates
      are single-threaded (no concurrent reads/writes possible per
      isolate), so the Dart source has NO synchronisation. .NET writes
      and reads of `string?` (a reference type) are atomic per ECMA-335
      / CLI memory model (Microsoft Learn: "Reads and writes of the
      following data types are atomic: bool, char, byte, sbyte, short,
      ushort, uint, int, float, and reference types"). Therefore a plain
      `static string?` field replicates the Dart semantics with zero
      tearing risk. We deliberately do NOT introduce `volatile`,
      `Interlocked.Exchange`, or a `Lazy<string>` wrapper — Dart source
      has none of those, and synthesising them would manufacture
      stronger ordering than the source provides (FR-013). If a future
      caller publishes the source from one thread and reads it from
      another, the .NET atomic-reference guarantee covers value
      visibility; if happens-before ordering relative to other writes
      is required, that becomes a future caller's responsibility (same
      posture as Dart). Null-safety: nullable annotation preserved
      verbatim (`String?` → `string?`).

  - construct_key: dart.toplevel_pub_fn.lazy_init_with_module_pipeline_and_template_extraction
    source_form: >-
      TypeEnvironment buildPreludeEnvironment() {
        final source = _preludeEnvironmentSource ?? typePrelude;
        if (source.isEmpty) { return TypeEnvironment({}, {}); }
        final lexer = Lexer(source);
        final tokens = lexer.tokenize();
        final parser = Parser(tokens);
        final module = parser.parseModule();
        final preludeTemplates = <String, TypeDef>{};
        for (final td in module.typeDefs) {
          if (td.isParameterized) { preludeTemplates[td.name] = td; }
        }
        final expandedModule = expandParameterizedTypes(module);
        final env = _buildEnvironmentFromModule(expandedModule,
            checkRedefinitions: false, resolveAliasesNow: true);
        return TypeEnvironment(env.types, env.procedures,
            paramProcDecls: env.paramProcDecls,
            typeTemplates: preludeTemplates);
      }
    target_decision: >-
      Emit `public static TypeEnvironment BuildPreludeEnvironment()` on
      a `public static class TypeEnvironmentBuilder` (namespace
      `Glp.Analysis.TypeChecker`). Body preserved step-for-step:
      (1) `var source = _preludeEnvironmentSource ?? Prelude.TypePrelude;`
      — Dart `??` (if-null) maps 1:1 to C# `??` (null-coalescing
      operator) with identical short-circuit semantics.
      (2) `if (source.Length == 0) return new TypeEnvironment(new(),
      new());` — Dart `String.isEmpty` → C# `string.Length == 0` (or
      `string.IsNullOrEmpty(source)` — but the null branch is already
      handled by the `??` above, so `Length == 0` matches Dart's
      `isEmpty` exactly without the null check). Empty `Map<>` literal
      `{}` → fresh `Dictionary<>` per call (NOT a shared static empty
      — the `TypeEnvironment` is mutable, so a shared mutable empty
      would alias mutable state across calls — same nuance as
      type_ast.dart's `TypeEnvironment.empty()`).
      (3) Lex / parse / parseModule chain: `var lexer = new Lexer(source);
      var tokens = lexer.Tokenize(); var parser = new Parser(tokens);
      var module = parser.ParseModule();` — direct `new` + method-call
      transliteration; cross-file constraint that `Lexer`, `Parser` ctor
      and method signatures match (anchored in their own convspecs).
      (4) `var preludeTemplates = new Dictionary<string, TypeDef>(
      StringComparer.Ordinal); foreach (var td in module.TypeDefs) {
      if (td.IsParameterized) preludeTemplates[td.Name] = td; }` —
      Dart `for (final ... in ...)` → C# `foreach`; the indexer
      assignment `dict[k] = v` is LAST-writer-wins (correct here:
      Dart's `[]=` has identical semantics, and the inner loop iterates
      the source list once with unique-by-construction names).
      (5) `var expandedModule = ParamExpansion.ExpandParameterizedTypes(
      module);` — top-level Dart fn → static method on host class
      `ParamExpansion` (cached idiom, anchored in param_expansion.dart.md).
      (6) Call private helper:
      `var env = BuildEnvironmentFromModule(expandedModule,
      checkRedefinitions: false, resolveAliasesNow: true);` — C# named
      arguments preserve the Dart named-arg call shape verbatim.
      (7) Final constructor: `return new TypeEnvironment(env.Types,
      env.Procedures, paramProcDecls: env.ParamProcDecls,
      typeTemplates: preludeTemplates);` — Dart named-arg call → C#
      named-arg call (cached idiom from param_expansion.dart). The
      `TypeEnvironment` ctor signature MUST emit parameter names
      matching the Dart source (`paramProcDecls`, `typeTemplates`) —
      cross-file constraint anchored in type_ast.dart.md.
    idiom_id: dart-toplevel-driver-fn-to-csharp-static-builder-method
    research_finding_id: rf-csharp-mutable-local-accumulator-pure-function
    nuance: >-
      Cached idiom reuse (FR-012; type_ast.dart / program_dfa.dart /
      param_expansion.dart). Load-bearing nuances: (1) The
      `??`-with-static-fallback (`_preludeEnvironmentSource ?? typePrelude`)
      is the "engine has initialised us" sentinel — C# `??` has
      identical short-circuit semantics (Microsoft Learn:
      "?? operator — null-coalescing"), so direct transliteration.
      `Prelude.TypePrelude` is the cross-file reference to the C#
      `const string` field anchored in prelude.dart.md. (2) The
      empty-source early return MUST allocate a *fresh* empty
      `TypeEnvironment`, never share a static singleton, because
      `TypeEnvironment` is a mutable accumulator (cached nuance from
      type_ast.dart — `addType`/`addProcedure` mutate in place). (3)
      Template extraction iterates `module.TypeDefs` reading the
      `IsParameterized` boolean — cross-file constraint that
      `TypeDef.IsParameterized` is a get-only property (anchored in
      type_ast.dart.md). Insertion via indexer (`dict[k] = v`,
      LAST-wins) matches Dart `Map[]=` semantics — distinct from the
      file's other dictionary-write site (the `procedures` map below,
      which uses indexer assignment via `qualifiedKey` per the Dart
      source). (4) Named-arg call `TypeEnvironment(..., paramProcDecls:
      ..., typeTemplates: ...)` — C# named-argument call site requires
      that the target ctor's parameter names match. Reference
      semantics: `TypeDef` and `ProcDecl` are reference types in both
      languages; the dictionary stores aliases (NOT clones), preserving
      shared-node identity exactly as Dart does.

  - construct_key: dart.toplevel_pub_fn.nullable_named_param_default_to_prelude_fallback_then_merge
    source_form: >-
      TypeEnvironment buildTypeEnvironment(ast.Module module,
          {TypeEnvironment? ancestorScope}) {
        final baseEnv = ancestorScope ?? buildPreludeEnvironment();
        final userEnv = _buildEnvironmentFromModule(module,
            checkRedefinitions: ancestorScope == null,
            resolveAliasesNow: false);
        final merged = baseEnv.merge(userEnv);
        final types = Map<String, TypeDef>.from(merged.types);
        final procedures = Map<String, ProcDecl>.from(merged.procedures);
        _resolveAliases(types, procedures);
        return TypeEnvironment(types, procedures,
            paramProcDecls: merged.paramProcDecls);
      }
    target_decision: >-
      Emit `public static TypeEnvironment BuildTypeEnvironment(Module
      module, TypeEnvironment? ancestorScope = null)`. Optional named
      Dart param → C# optional parameter with `= null` default + named-
      argument call syntax at call sites — cached idiom from
      error.dart.md / param_expansion.dart.md.
      Body: (1) `var baseEnv = ancestorScope ?? BuildPreludeEnvironment();`
      — `??` direct transliteration. (2) `var userEnv =
      BuildEnvironmentFromModule(module, checkRedefinitions: ancestorScope
      == null, resolveAliasesNow: false);` — `bool` ⇒ `bool`; the
      truth-value computation `ancestorScope == null` is identical in both
      languages (reference null-equality). (3) `var merged =
      baseEnv.Merge(userEnv);` — cross-file: `TypeEnvironment.Merge` is
      anchored in type_ast.dart.md as returning a NEW environment with
      LAST-wins map merge (Dart `{...a, ...b}` semantics preserved via C#
      indexer-upsert per that spec). (4) Defensive copy:
      `var types = new Dictionary<string, TypeDef>(merged.Types,
      StringComparer.Ordinal); var procedures = new Dictionary<string,
      ProcDecl>(merged.Procedures, StringComparer.Ordinal);` — Dart
      `Map<K,V>.from(other)` creates a SHALLOW COPY (new map, aliased
      values); the C# `Dictionary<K,V>(IDictionary<K,V>)` ctor is
      documented as the equivalent (Microsoft Learn: "Initializes a new
      instance of the Dictionary<TKey,TValue> class that contains
      elements copied from the specified IDictionary<TKey,TValue>"). The
      defensive copy is LOAD-BEARING: `_resolveAliases` then mutates
      `types`/`procedures` in place, and the source comment
      "Now resolve aliases on the merged environment" makes clear the
      mutation must NOT propagate back to the inputs.
      (5) `ResolveAliases(types, procedures);` — call to the private
      static helper that mutates by ref (see below).
      (6) `return new TypeEnvironment(types, procedures, paramProcDecls:
      merged.ParamProcDecls);` — note: only two of the three
      collections are deep-copied; `paramProcDecls` is passed through by
      reference (deliberate alias — proc-decl templates are not aliased
      and `_resolveAliases` only writes `types` and `procedures`).
    idiom_id: dart-toplevel-driver-fn-to-csharp-static-builder-method
    research_finding_id: rf-csharp-dictionary-copy-constructor-shallow
    nuance: >-
      NEW research_finding (Dictionary copy-ctor shallow semantics) +
      cached idiom (top-level driver pattern). Load-bearing nuances:
      (1) `Map<K,V>.from(other)` is documented (api.dart.dev:
      `Map.of` / `Map.from`) as constructing a new map with the same
      key/value pairs — SHALLOW, references preserved. The C#
      `Dictionary<TKey,TValue>(IDictionary<TKey,TValue>)` ctor
      (Microsoft Learn) has IDENTICAL shallow-copy semantics: "Every
      key in the dictionary must be unique … The new Dictionary
      contains references to the same values" — verbatim authoritative
      match. Ordinal comparer specified for the defensive-copy
      dictionaries (string keys, same cross-file ordinal-discipline
      thread as the rest of the type_checker family). (2) The defensive
      copy exists specifically because `_resolveAliases` mutates its
      arguments (`types[k] = ...`, `types.Remove(k)`, `procedures[k] =
      ...`). Without the copy, the user environment's maps would be
      observed-mutated by callers (silent footgun). MUST be preserved.
      (3) `paramProcDecls` is intentionally NOT copied — a future
      maintainer who adds alias resolution over `paramProcDecls` MUST
      add a defensive copy at that point. Recorded here as a
      maintenance invariant. (4) `?? BuildPreludeEnvironment()` allocates
      a fresh prelude env per call — a future optimisation could cache
      it, but the Dart source does not, so we preserve the per-call
      allocation (FR-013 / spec faithfulness).

  - construct_key: dart.private_static_module_to_env_assembler_with_two_boolean_flags
    source_form: >-
      TypeEnvironment _buildEnvironmentFromModule(ast.Module module, {
          required bool checkRedefinitions,
          required bool resolveAliasesNow,
      }) {
        final types = <String, TypeDef>{};
        final procedures = <String, ProcDecl>{};
        final paramProcDecls = <String, ProcDecl>{};
        for (final typeDef in module.typeDefs) {
          if (checkRedefinitions && isPredefinedType(typeDef.name)) {
            throw RedefinitionError(...); }
          if (!_isTypeAlias(typeDef)) { _checkDeterminism(typeDef); }
          types[typeDef.name] = typeDef;
        }
        for (final procDecl in module.procDeclarations) {
          if (checkRedefinitions && isPredefinedProcedure(procDecl.name)) {
            throw RedefinitionError(...); }
          final isBuiltin = isBuiltinProcedure(procDecl.key);
          if (isBuiltin && !procDecl.isBuiltin) {
            procedures[procDecl.qualifiedKey] = ProcDecl(
              procDecl.name, procDecl.argTypes, procDecl.line, procDecl.column,
              isBuiltin: true, exported: procDecl.exported,
              imported: procDecl.imported, modulePath: procDecl.modulePath);
          } else {
            procedures[procDecl.qualifiedKey] = procDecl;
          }
        }
        for (final paramDecl in module.paramProcDecls) {
          paramProcDecls[paramDecl.qualifiedKey] = paramDecl;
        }
        if (resolveAliasesNow) { _resolveAliases(types, procedures); }
        return TypeEnvironment(types, procedures, paramProcDecls: paramProcDecls);
      }
    target_decision: >-
      Emit `private static TypeEnvironment BuildEnvironmentFromModule(
      Module module, bool checkRedefinitions, bool resolveAliasesNow)`.
      Dart `required` named bool parameters → C# positional bool
      parameters (no `= null` default — both are mandatory). Body
      preserved step-for-step:
      (1) Three accumulators: `var types = new Dictionary<string,
      TypeDef>(StringComparer.Ordinal); var procedures = new
      Dictionary<string, ProcDecl>(StringComparer.Ordinal); var
      paramProcDecls = new Dictionary<string, ProcDecl>(
      StringComparer.Ordinal);` — Ordinal mandated (cached
      ordinal-discipline thread from param_expansion.dart).
      (2) Type-defs loop: `foreach (var typeDef in module.TypeDefs) {
      if (checkRedefinitions && Prelude.IsPredefinedType(typeDef.Name))
      throw new RedefinitionError($"Cannot redefine predefined type:
      {typeDef.Name}", typeDef.Line, typeDef.Column); if
      (!IsTypeAlias(typeDef)) CheckDeterminism(typeDef);
      types[typeDef.Name] = typeDef; }` — `throw new` (C# requires
      `new` for exceptions; cached idiom). Indexer LAST-wins assignment
      matches Dart `Map[]=`.
      (3) Proc-decls loop: `foreach (var procDecl in
      module.ProcDeclarations) { if (checkRedefinitions &&
      Prelude.IsPredefinedProcedure(procDecl.Name)) throw new
      RedefinitionError($"Cannot redefine predefined procedure:
      {procDecl.Name}/{procDecl.Arity}", procDecl.Line,
      procDecl.Column); var isBuiltin =
      Prelude.IsBuiltinProcedure(procDecl.Key); procedures[
      procDecl.QualifiedKey] = (isBuiltin && !procDecl.IsBuiltin) ? new
      ProcDecl(procDecl.Name, procDecl.ArgTypes, procDecl.Line,
      procDecl.Column, isBuiltin: true, exported: procDecl.Exported,
      imported: procDecl.Imported, modulePath: procDecl.ModulePath) :
      procDecl; }` — the if/else collapse into a ternary preserves the
      Dart conditional-creation shape (a fresh `ProcDecl` only when the
      builtin flag needs to be SET; otherwise the original is aliased).
      Named-arg call (`isBuiltin: true, ...`) requires `ProcDecl` ctor
      parameter names match Dart (cross-file constraint anchored in
      type_ast.dart.md). The shadowing pattern — keying `procedures`
      by `procDecl.QualifiedKey` (which combines name/arity AND
      module-qualifier) — is preserved verbatim from Dart; do NOT
      normalise to `Name`+`Arity`.
      (4) Parameterised-proc-decls loop: identical shape, keying by
      `paramDecl.QualifiedKey`. Indexer LAST-wins.
      (5) Conditional alias resolution: `if (resolveAliasesNow)
      ResolveAliases(types, procedures);` — pass dictionaries by-reference
      (reference type — mutation observable to caller).
      (6) Return: `return new TypeEnvironment(types, procedures,
      paramProcDecls: paramProcDecls);` — note `paramProcDecls` named-arg
      call preserves the Dart shape.
    idiom_id: dart-private-static-module-assembler-with-flag-params
    research_finding_id: rf-csharp-required-named-bool-to-positional-bool-or-namedarg
    nuance: >-
      NEW idiom + NEW research_finding. Three load-bearing nuances:
      (1) Dart `required` named parameters (which the source uses
      explicitly: `required bool checkRedefinitions, required bool
      resolveAliasesNow`) have NO direct C# equivalent. Two faithful
      mappings exist: (a) positional bool parameters (caller passes
      `BuildEnvironmentFromModule(m, true, false)` — terse but loses
      the Dart self-documenting call-site labels); (b) optional
      parameters with NO default + named-argument call discipline
      (caller passes `BuildEnvironmentFromModule(m, checkRedefinitions:
      true, resolveAliasesNow: false)` — verbose but preserves the
      Dart call-site readability). Microsoft Learn: "Named arguments
      enable you to specify an argument for a parameter by matching
      the argument with its name rather than with its position …" —
      authoritative. THIS SPEC CHOOSES (b) for the call sites *because*
      the file's two callers (`BuildPreludeEnvironment` calling with
      `checkRedefinitions: false, resolveAliasesNow: true`, and
      `BuildTypeEnvironment` calling with `checkRedefinitions:
      ancestorScope == null, resolveAliasesNow: false`) rely on the
      labels for non-trivial readability — the alternative (raw `true,
      false` and `(scope == null), false`) buries the semantic and is
      a known footgun for boolean parameters (see Microsoft "boolean
      blindness" anti-pattern). The method SIGNATURE in C# keeps the
      parameters positional (no `= null` defaults, since both bools
      are conceptually required) but the CALL SITES use named-argument
      syntax — preserving Dart's `required`-named-call discipline
      without inventing a C# language feature. (2) The two boolean
      flags MUST NOT be elided into separate methods (e.g.
      `BuildEnvironmentForPrelude` / `BuildEnvironmentForUser`) — the
      Dart source uses one entry point with flag dispatch, and
      splitting would manufacture a different API surface (FR-013).
      (3) The conditional `ProcDecl`-with-`isBuiltin`-set construction
      (lines 170-181) creates a FRESH `ProcDecl` only when the flag
      needs to be promoted. Reference semantics preserved: when no
      promotion needed, the dictionary aliases the original
      `procDecl`. The carry-forward of `exported`, `imported`,
      `modulePath`, `argTypes`, `line`, `column` MUST be byte-exact —
      these fields participate in downstream identity / module-
      resolution. Cross-file constraint: `ProcDecl` ctor signature in
      type_ast.dart.md.

  - construct_key: dart.toplevel_public_fn.extract_clauses_via_addall_accumulator
    source_form: >-
      List<ast.Clause> extractClauses(ast.Module module) {
        final clauses = <ast.Clause>[];
        for (final proc in module.procedures) { clauses.addAll(proc.clauses); }
        return clauses;
      }
    target_decision: >-
      Emit `public static List<Clause> ExtractClauses(Module module)` on
      `TypeEnvironmentBuilder`. Body: `var clauses = new List<Clause>();
      foreach (var proc in module.Procedures) clauses.AddRange(
      proc.Clauses); return clauses;`. Dart `List.addAll(iterable)` →
      C# `List<T>.AddRange(IEnumerable<T>)` — Microsoft Learn:
      "Adds the elements of the specified collection to the end of the
      `List<T>`." Faithful counterpart with O(n) amortised append in
      both languages.
      Equivalent LINQ formulation `module.Procedures.SelectMany(p =>
      p.Clauses).ToList()` is recorded as an optional codegen
      micro-optimisation but the imperative-accumulator form is
      preferred for review parity with the Dart source.
      `module.Procedures` is a `List<Procedure>` (or `IEnumerable`) in
      Dart; the C# counterpart's type is anchored in ast.dart.md /
      compiler convspecs.
    idiom_id: dart-list-addall-to-csharp-list-addrange
    research_finding_id: rf-csharp-list-addrange-vs-linq-selectmany
    nuance: >-
      NEW idiom + NEW research finding. Dart `List<T>.addAll(other)`
      mutates the receiver in place — `AddRange` does the same. Both
      are O(n) amortised and both share the same reference-aliasing
      semantic (the added elements are aliased into the receiver, not
      cloned; `Clause` is a reference type, so the list-of-references
      shape is preserved exactly across the conversion). The reverse
      transform (`.SelectMany(...).ToList()`) is a deferred-execution
      LINQ pipeline — semantically identical with `.ToList()`
      materialisation, but allocates a `SelectIterator` then walks it
      once; for review parity with the simple Dart loop, the
      imperative form is the spec default. The `clauses.AddRange(...)`
      site is the SINGLE place this idiom appears in the file; recorded
      as a KB entry because the pattern recurs across the type_checker
      family.

  - construct_key: dart.private_predicate_fn.alternatives_length_and_alt_subtype_dispatch
    source_form: >-
      bool _isSimpleAlias(TypeDef def) {
        if (def.alternatives.length != 1) return false;
        final alt = def.alternatives.first;
        if (alt is PrimitiveModeAlt) return true;
        if (alt is TypeRef) return true;
        return false;
      }
      bool _isUnionAlias(TypeDef def) {
        if (def.alternatives.length < 2) return false;
        for (final alt in def.alternatives) {
          if (alt is! TypeRef) return false;
          final typeName = (alt as TypeRef).name;
          if (isPredefinedType(typeName)) { return false; }
        }
        return true;
      }
      bool _isTypeAlias(TypeDef def) => _isSimpleAlias(def) || _isUnionAlias(def);
    target_decision: >-
      Emit three `private static bool` helpers on `TypeEnvironmentBuilder`.
      `IsSimpleAlias(TypeDef def)`: classic body form preserved —
      `if (def.Alternatives.Count != 1) return false; var alt =
      def.Alternatives[0]; return alt is PrimitiveModeAlt || alt is
      TypeRef;` — collapses the two `if (alt is X) return true;` arms
      into a short-circuit OR over type-pattern matches (semantically
      identical because the sub-types are disjoint; cached
      `dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch`
      idiom). Equivalent C# switch-expression form:
      `def.Alternatives switch { [PrimitiveModeAlt _] or [TypeRef _]
      => true, _ => false };` (using list patterns with one-element
      shape) — recorded as a more declarative alternative but the
      classic body is the default for readability.
      `IsUnionAlias(TypeDef def)`: `if (def.Alternatives.Count < 2)
      return false; foreach (var alt in def.Alternatives) { if (alt
      is not TypeRef r) return false; if
      (Prelude.IsPredefinedType(r.Name)) return false; } return true;`
      — Dart `is!` → C# `is not`; the `(alt as TypeRef).name` cast +
      access is fused into a declaration pattern `alt is not TypeRef
      r` (test+cast in one). Equivalent LINQ formulation
      `def.Alternatives.Count >= 2 && def.Alternatives.All(alt => alt
      is TypeRef r && !Prelude.IsPredefinedType(r.Name))` is recorded
      but the imperative form is preferred (early-exit on the first
      non-TypeRef preserves Dart's loop-body shape and the explicit
      sub-clause `if (isPredefinedType(typeName)) return false;`).
      `IsTypeAlias(TypeDef def) => IsSimpleAlias(def) ||
      IsUnionAlias(def);` — expression-bodied member, direct
      transliteration.
    idiom_id: dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch
    research_finding_id: rf-dart-extension-is-as-to-csharp-type-pattern-switch
    nuance: >-
      Cached idiom reuse (FR-012; type_ast.dart, program_dfa.dart,
      param_expansion.dart). The `is` + `as` pair (`alt is TypeRef`
      then `(alt as TypeRef).name`) fuses into a declaration pattern
      `alt is TypeRef r` — Microsoft Learn pattern-matching doc:
      "test the type of the variable, and assign it to a new
      variable", removing the double type-check and the `InvalidCast`
      hazard. Reference semantics preserved: `TypeExpr` is a
      reference type (per type_ast.dart.md, `class` not `struct`);
      the `alt` variable holds a reference, `r` holds the same
      reference after pattern narrowing. Empty-list case for
      `IsUnionAlias`: Dart `alternatives.length < 2` excludes
      0-element and 1-element lists; C# `.Count < 2` is identical
      (`Count` is documented as the number of elements; matches Dart
      `length`). Predefined-type guard is the load-bearing semantic
      distinction between a union alias and a primitive-union type
      (e.g. `Constant ::= Number ; String` is NOT a union alias
      because it references predefined primitives — preserved
      verbatim).

  - construct_key: dart.toplevel_private_alias_resolution_pipeline_6step_imperative_with_closure_and_cycle_detect
    source_form: >-
      void _resolveAliases(Map<String, TypeDef> types,
          Map<String, ProcDecl> procedures) {
        // Step 1: classify simple vs union aliases
        final simpleAliases = <String, TypeDef>{};
        final unionAliases = <String, TypeDef>{};
        for (final entry in types.entries) { ... }
        if (simpleAliases.isEmpty && unionAliases.isEmpty) return;
        // Step 2: transitive resolve with cycle detection
        final resolved = <String, TypeExpr>{};
        final visiting = <String>{};
        TypeExpr resolveSimpleAlias(String name) { ... recursive closure ... }
        for (final name in simpleAliases.keys) { resolveSimpleAlias(name); }
        // Step 3: expand union aliases (collect alts, complement, determinism check)
        // Step 4: replace simple alias references in all non-simple-alias type defs
        // Step 5: replace alias references in procedure declarations
        // Step 6: remove simple alias definitions from types map
      }
    target_decision: >-
      Emit `private static void ResolveAliases(IDictionary<string,
      TypeDef> types, IDictionary<string, ProcDecl> procedures)` — pass
      by reference (reference type — mutation observable to caller, as
      designed; this is a mutate-in-place pipeline, not a builder).
      Body preserved step-for-step:
      (1) Two classification dictionaries via `foreach (var entry in
      types) { if (IsSimpleAlias(entry.Value)) simpleAliases[entry.Key]
      = entry.Value; else if (IsUnionAlias(entry.Value))
      unionAliases[entry.Key] = entry.Value; }` — Dart `Map.entries`
      → C# `foreach` over `KeyValuePair<TKey,TValue>` directly (or
      `.Keys.Zip(.Values)` — but the natural C# iteration over a
      `Dictionary` is by KVP, equivalent shape). All accumulator dicts
      use `StringComparer.Ordinal`.
      (2) Early exit: `if (simpleAliases.Count == 0 && unionAliases.Count
      == 0) return;` — Dart `isEmpty` → C# `Count == 0`.
      (3) The local recursive closure `resolveSimpleAlias` MUST be
      ported as a `private static` HELPER method
      `ResolveSimpleAlias` taking explicit parameters (the Dart closure
      captures `resolved`, `visiting`, `simpleAliases`): C# has lambdas
      but they cannot be recursive without an explicit `Func<...>` self-
      reference (Roslyn assigns a name like `<>g__ResolveSimpleAlias|0`
      for local functions — recursive local functions ARE supported
      via C# 7.0 local functions). The cleanest mapping uses a `static
      TypeExpr ResolveSimpleAliasLocal(string name, IDictionary<string,
      TypeExpr> resolved, ISet<string> visiting, IDictionary<string,
      TypeDef> simpleAliases)` LOCAL function inside `ResolveAliases`
      (Microsoft Learn: "Local functions are private methods of a type
      that are nested in another member. They can only be called from
      their containing member. Local functions can be declared in and
      called from … Methods …"). Cycle detection: `if
      (visiting.Contains(name)) throw new CircularAliasError(
      $"Circular alias chain detected: {name}", aliasDef.Line,
      aliasDef.Column);` — the `visiting` set add/remove pair brackets
      the recursive call, identically to Dart.
      (4) Step 3 — union expansion — preserves the per-alt foreach +
      complement-apply + determinism-check shape. The
      `simpleAliases.containsKey(ref.name) || unionAliases.containsKey(
      ref.name)` check → `simpleAliases.ContainsKey(r.Name) ||
      unionAliases.ContainsKey(r.Name)`; on hit, `throw new
      AliasExpansionError($"Union alias cannot reference another alias:
      {r.Name}", def.Line, def.Column);`. The `_applyComplementToAlt`
      call recurses across the AST hierarchy — see dedicated construct
      below.
      (5) Steps 4 and 5 — alias-reference replacement — use `.toList()`
      materialisation on the entry view (`types.entries.where(...)
      .toList()` Dart, `types.Where(e => !simpleAliases.ContainsKey(
      e.Key)).ToList()` C#) — the `.ToList()` materialisation is
      CORRECTNESS-CRITICAL: the loop body mutates `types` (`types[k] =
      new TypeDef(...)`), and iterating a live `Dictionary` while
      mutating throws `InvalidOperationException` ("Collection was
      modified; enumeration operation may not execute" — Microsoft Learn
      on `Dictionary<TKey,TValue>.Enumerator`). Same nuance as
      param_expansion.dart's snapshot-iteration; cached idiom.
      (6) Step 6 — `foreach (var name in simpleAliases.Keys.ToList())
      types.Remove(name);` — note `.ToList()` snapshot of the KEYS
      collection because we cannot mutate `types` while iterating
      `simpleAliases.Keys` if it is a view onto `types` (not the case
      here — `simpleAliases` is a separate dictionary — but the
      defensive snapshot preserves the conservative-iteration discipline
      already established in param_expansion.dart).
    idiom_id: dart-multistep-pipeline-with-recursive-closure-and-cycle-detection-to-csharp-local-function
    research_finding_id: rf-csharp-local-function-vs-lambda-recursive
    nuance: >-
      NEW idiom + NEW research finding. Three load-bearing nuances:
      (1) **Recursive local closure** — Dart nested functions
      (`TypeExpr resolveSimpleAlias(String name) { ... }`) are
      first-class closures that capture enclosing scope and can be
      recursive without ceremony. C# has THREE candidates: (a) lambda
      assigned to `Func<...>` (requires self-reference workaround:
      assign `null!` then reassign — clunky and error-prone), (b)
      private instance/static method on the enclosing class (loses
      Dart's lexical-scope encapsulation), or (c) C# 7.0+ LOCAL
      FUNCTION (Microsoft Learn: "Local functions are private methods
      of a type that are nested in another member … Local functions
      can be declared in and called from … Methods, including
      extension methods … Local functions explicitly indicate that the
      code in the local function is part of the containing member's
      logic"). Local functions support natural recursion AND closure
      over enclosing locals (`resolved`, `visiting`, `simpleAliases`
      — but per Microsoft Learn, captured variables in `static local
      function` require explicit parameter passing; choosing
      non-`static` local function permits free capture and is the
      faithful mapping for the Dart closure). Verbatim query: "C#
      local functions recursion capture enclosing locals". Authoritative.
      Choice: NON-STATIC local function inside `ResolveAliases` to
      mirror Dart's closure capture exactly. This idiom is NEW to the
      type_checker family (no prior file used a nested recursive
      closure — sibling drivers used flat recursive private static
      methods).
      (2) **Cycle detection via visiting set** — the `visiting.add(name)`
      / `visiting.remove(name)` bracket around the recursive call is
      the standard DFS-coloring trick (white/gray/black via presence
      in `resolved`/`visiting`/neither). C# `HashSet<string>` with
      `.Add` (returns false if already present — Microsoft Learn,
      cached from param_expansion.dart) and `.Remove` is the exact
      counterpart. The cycle throws `CircularAliasError` with line/
      column from the alias DEFINITION (not the call site) — preserved.
      (3) **(T?)? = T involution** — the helper `_applyComplement`
      implements the type-theoretic involution: applying complement
      twice yields the original. This is a domain-spec invariant
      (cited in the Dart source's doc-comment "Implements the
      involution (T?)? = T") — preserved verbatim in C# as
      `private static TypeExpr ApplyComplement(TypeExpr expr, bool
      applyComplement, long line, long column)` returning either the
      expression unchanged (no complement requested) or a fresh
      `TypeRef`/`PrimitiveModeAlt` with `IsInput: !expr.IsInput`. The
      involution is dispatched via type-pattern switch on `expr`. NO
      research required beyond the cached `is-typecheck → switch` idiom
      — but the semantic invariant is recorded here as a maintenance
      checkpoint.

  - construct_key: dart.private_recursive_walker.apply_complement_to_alt_full_ast_hierarchy
    source_form: >-
      TypeExpr _applyComplementToAlt(TypeExpr alt, bool applyComplement,
          int line, int column) {
        if (!applyComplement) return alt;
        if (alt is TypeRef) { return TypeRef(alt.name, line, column,
          isInput: !alt.isInput); }
        else if (alt is PrimitiveModeAlt) { return PrimitiveModeAlt(
          !alt.isInput, line, column); }
        else if (alt is ConstantAlt) { return alt; }
        else if (alt is ListNilAlt) { return alt; }
        else if (alt is ListConsAlt) { return ListConsAlt(
          _applyComplementToAlt(alt.head, true, line, column),
          _applyComplementToAlt(alt.tail, true, line, column),
          line, column); }
        else if (alt is StructAlt) { return StructAlt(alt.functor,
          alt.args.map((a) => _applyComplementToAlt(a, true, line, column))
          .toList(), line, column); }
        else if (alt is DiffListAlt) { return DiffListAlt(
          _applyComplementToAlt(alt.content, true, line, column),
          _applyComplementToAlt(alt.hole, true, line, column),
          line, column); }
        return alt;
      }
      // companion: _replaceAliasReferences (same shape, different per-arm body)
    target_decision: >-
      Each of the two AST walkers becomes a `private static TypeExpr`
      method on `TypeEnvironmentBuilder`. Body: classic C# type-pattern
      switch with arms for each `TypeExpr` leaf. Early-exit
      `if (!applyComplement) return alt;` preserved verbatim (still
      compiles to the same machine instruction shape in C#). Per-arm
      logic:
      - `TypeRef`: `case TypeRef r => new TypeRef(r.Name, line, column,
        isInput: !r.IsInput);` — fresh node with complement-flipped
        `isInput`.
      - `PrimitiveModeAlt`: `case PrimitiveModeAlt p => new
        PrimitiveModeAlt(!p.IsInput, line, column);`.
      - `ConstantAlt`, `ListNilAlt`: pass-through (`alt` returned
        unchanged — these have no mode).
      - `ListConsAlt`: recursive descent into `Head`/`Tail`, both
        complemented.
      - `StructAlt`: `case StructAlt s => new StructAlt(s.Functor,
        s.Args.Select(a => ApplyComplementToAlt(a, true, line,
        column)).ToList(), line, column);` — LINQ
        `.Select(...).ToList()` materialisation required (cached
        deferred-execution nuance from param_expansion.dart).
      - `DiffListAlt`: recursive descent into `Content`/`Hole`, both
        complemented.
      - Default: pass-through (`alt` returned unchanged) — Dart
        fallthrough `return alt;` semantic.
      Equivalent expression form:
      `return (alt, applyComplement) switch { (_, false) => alt,
      (TypeRef r, true) => new TypeRef(r.Name, line, column,
      isInput: !r.IsInput), ... };` — recorded as alternative; classic
      switch is default for review parity.
      `_replaceAliasReferences` has identical structural shape but
      per-arm body replaces by lookup in the `resolved` map
      (`if (resolved.TryGetValue(r.Name, out var resolvedTarget))
      return ApplyComplement(resolvedTarget, r.IsInput, r.Line,
      r.Column); return r;`).
    idiom_id: dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch
    research_finding_id: rf-dart-extension-is-as-to-csharp-type-pattern-switch
    nuance: >-
      Cached idiom reuse (FR-012). Three load-bearing nuances:
      (1) **Closed-sum exhaustiveness** — `TypeExpr` is treated as a
      closed algebraic sum (per type_ast.dart.md, abstract base +
      sealed leaves). C# does NOT compile-time verify subtype
      exhaustiveness over a non-sealed-by-the-language base, so the
      `default: return alt;` arm (pass-through for unknown types) is
      load-bearing — matches Dart's fall-through `return alt;` and
      keeps the function total. (2) **AST node identity preservation
      vs fresh allocation** — the `ConstantAlt` and `ListNilAlt` arms
      RETURN THE INPUT REFERENCE UNCHANGED (no new allocation); the
      other arms allocate fresh nodes. This asymmetry is intentional —
      complement-of-constant is the constant itself (it has no mode to
      flip), so reusing the reference saves allocation and preserves
      identity for downstream `==` checks where it matters. Preserved
      verbatim in C# (reference-type semantics — returning `alt`
      returns the same reference). (3) **`.Select(...).ToList()`
      materialisation** — the `StructAlt` arm uses LINQ `Select` over
      `s.Args`; `.ToList()` is MANDATORY (cached deferred-execution
      nuance: without it, the projection re-runs on each enumeration,
      producing distinct-but-equal nodes — silent correctness regression
      for downstream alias-replacement passes). Microsoft Learn:
      "Enumerable.Select … is implemented by using deferred execution."

  - construct_key: dart.private_walker.replace_alias_references_typeexpr_recursive_with_map_lookup
    source_form: >-
      TypeExpr _replaceAliasReferences(TypeExpr expr,
          Map<String, TypeExpr> resolved) {
        if (expr is TypeRef) {
          final resolvedTarget = resolved[expr.name];
          if (resolvedTarget != null) {
            return _applyComplement(resolvedTarget, expr.isInput,
                expr.line, expr.column); }
          return expr;
        }
        if (expr is PrimitiveModeAlt) { return expr; }
        if (expr is ConstantAlt) { return expr; }
        if (expr is ListNilAlt) { return expr; }
        if (expr is ListConsAlt) { return ListConsAlt(...); }
        if (expr is StructAlt) { return StructAlt(...); }
        if (expr is DiffListAlt) { return DiffListAlt(...); }
        return expr;
      }
    target_decision: >-
      Emit `private static TypeExpr ReplaceAliasReferences(TypeExpr
      expr, IDictionary<string, TypeExpr> resolved)`. Body uses a C#
      type-pattern switch on `expr` with arms identical to
      `ApplyComplementToAlt`, EXCEPT the `TypeRef` arm:
      `case TypeRef r: { if (resolved.TryGetValue(r.Name, out var
      resolvedTarget)) return ApplyComplement(resolvedTarget,
      r.IsInput, r.Line, r.Column); return r; }`. The Dart `map[key]`
      indexer returns `V?` on miss; C# `IDictionary<K,V>[K]` THROWS
      `KeyNotFoundException` on miss — direct transliteration is
      WRONG. The faithful mapping is `TryGetValue(key, out value)`
      (Microsoft Learn: "Gets the value associated with the specified
      key. … When this method returns, contains the value associated
      with the specified key, if the key is found; otherwise, the
      default value … Returns true if the dictionary contains an
      element with the specified key; otherwise, false") — semantically
      identical to Dart's `map[key]` + null-check.
      Recursive arms (`ListConsAlt`, `StructAlt`, `DiffListAlt`)
      descend identically to `ApplyComplementToAlt` but call
      `ReplaceAliasReferences` (NOT `ApplyComplementToAlt`) on
      children. Pass-through arms (`PrimitiveModeAlt`, `ConstantAlt`,
      `ListNilAlt`) return `expr` unchanged (reference preserved).
      Default arm: `return expr;` (Dart fall-through preserved).
    idiom_id: dart-map-indexer-null-on-miss-to-csharp-trygetvalue
    research_finding_id: rf-csharp-dictionary-trygetvalue-vs-indexer-null-vs-keynotfound
    nuance: >-
      NEW idiom + NEW research finding. The critical Dart→C# divergence
      is the dictionary-miss semantic: Dart `Map<K,V>[k]` returns
      `null` (`V?`) on miss; C# `Dictionary<K,V>[k]` THROWS
      `KeyNotFoundException` on miss. This is a CLASSIC silent-bug
      conversion pitfall — a naive `dict[r.Name]` translation would
      change behaviour from "fall through to default" to "throw an
      exception", failing on any TypeRef that isn't an alias (which
      is the common case). Microsoft Learn `Dictionary<TKey,TValue>
      .Item[TKey]`: "Gets or sets the value associated with the
      specified key … KeyNotFoundException — The property is
      retrieved and key does not exist in the collection."
      Authoritative. The `TryGetValue(key, out V value)` pattern is
      the documented faithful counterpart and the spec mandates it.
      This idiom is recorded as a NEW KB entry because it recurs:
      any Dart `Map[]`-with-null-check site MUST become `TryGetValue`
      in C#. Reference semantics preserved (the dictionary holds
      `TypeExpr` references; `out` parameter receives the same
      reference). Ordinal comparer carried forward from the parent
      dictionary (the `resolved` map is constructed in
      `ResolveAliases` with Ordinal — consistent with the
      type_checker family ordinal-discipline thread).

  - construct_key: dart.private_void_validator.duplicate_alt_signature_detection_with_4_sub_dicts_and_primitive_overlap
    source_form: >-
      void _checkDeterminism(TypeDef def) {
        final functors = <String>{};
        final constants = <String>{};
        final primitives = <String>{};
        bool hasWildcard = false;
        for (final alt in def.alternatives) {
          if (alt is ConstantAlt) { ... throw NonDeterministicTypeError ... }
          else if (alt is ListNilAlt) { ... }
          else if (alt is ListConsAlt) { ... }
          else if (alt is StructAlt) { ... }
          else if (alt is DiffListAlt) { ... }
          else if (alt is PrimitiveModeAlt) { ... }
          else if (alt is TypeRef) { ... _checkPrimitiveOverlap ... }
        }
      }
      void _checkPrimitiveOverlap(String newPrimitive, Set<String>
          existing, bool hasWildcard, TypeDef def) {
        if (hasWildcard) throw ...;
        // Number overlaps with Integer/Real (both directions)
        // direct duplicate
      }
    target_decision: >-
      Emit `private static void CheckDeterminism(TypeDef def)` and
      `private static void CheckPrimitiveOverlap(string newPrimitive,
      ISet<string> existing, bool hasWildcard, TypeDef def)` on
      `TypeEnvironmentBuilder`. Body of `CheckDeterminism`:
      (1) Three `HashSet<string>(StringComparer.Ordinal)` accumulators
      + one `bool hasWildcard = false;` — Dart `<String>{}` set
      literal → C# `new HashSet<string>(StringComparer.Ordinal)`
      (cached ordinal-discipline thread; cached idiom
      `dart-empty-set-literal-to-csharp-hashset-ctor`).
      (2) Single C# type-pattern `switch` on `alt`:
      - `case ConstantAlt c: var key = c.Value.ToString() ?? "null";
        if (!constants.Add(key)) throw new NonDeterministicTypeError(
        $"Duplicate constant alternative: {key} in {def.Name}",
        def.Line, def.Column); break;` — `HashSet<T>.Add` returns
        `false` if the element already exists (Microsoft Learn,
        cached); fuses Dart's "contains + add" two-step into a single
        atomic check. `c.Value.ToString()` may return null on Dart's
        `Object?` — C# `object?.ToString()` may also return null,
        guarded by `?? "null"` sentinel (or keep nullability and use
        `string?` key — but `HashSet<string>` rejects null keys per
        Microsoft Learn; mapping to a sentinel matches Dart's
        behaviour where `.toString()` on null would itself throw,
        which the Dart source's `alt.value.toString()` is already
        guarded against by `ConstantAlt` ensuring non-null value).
      - `case ListNilAlt _: if (!functors.Add("[]/0")) throw new
        NonDeterministicTypeError($"Duplicate [] alternative in
        {def.Name}", def.Line, def.Column); break;`.
      - `case ListConsAlt _: if (!functors.Add("[|]/2")) throw ...;
        break;`.
      - `case StructAlt s: var key = $"{s.Functor}/{s.Args.Count}";
        if (!functors.Add(key)) throw new NonDeterministicTypeError(
        $"Duplicate functor alternative: {key} in {def.Name}",
        def.Line, def.Column); break;`.
      - `case DiffListAlt _: if (!functors.Add("\\/2")) throw ...;
        break;` — Dart string literal `'\\/2'` is the two-char string
        `\/2`; C# verbatim string `@"\/2"` or escaped `"\\/2"`
        produces the SAME two-char string. Spec mandates byte-exact
        key (`\/2`) — choose whichever C# string form yields it
        identically.
      - `case PrimitiveModeAlt _: if (hasWildcard ||
        primitives.Count > 0) throw new NonDeterministicTypeError(
        $"Wildcard _ overlaps with other alternatives in {def.Name}",
        def.Line, def.Column); hasWildcard = true; break;`.
      - `case TypeRef r: if (new[] {"Integer", "Real", "Number",
        "String"}.Contains(r.Name)) { CheckPrimitiveOverlap(r.Name,
        primitives, hasWildcard, def); primitives.Add(r.Name); }
        break;` — Dart set-literal-in-expression
        `{'Integer','Real','Number','String'}.contains(name)` → C#
        `new[] {...}.Contains(...)` (LINQ Contains over an inline
        array — Microsoft Learn `Enumerable.Contains`). On hot paths
        promote to a static `FrozenSet<string>` (cached idiom from
        prelude.dart) — recorded as optimisation; default keeps the
        inline form because it is called once per type def alt,
        non-hot.
      `CheckPrimitiveOverlap`: direct transliteration of the four
      throw arms. Reference semantics preserved (mutable `existing`
      set passed by reference — C# pass-by-reference-type-default
      matches Dart). Note `def` parameter passed by reference (used
      only to read `Name`/`Line`/`Column` — no mutation).
    idiom_id: dart-set-contains-then-add-to-csharp-hashset-add-bool-return
    research_finding_id: rf-csharp-hashset-add-returns-false-on-duplicate
    nuance: >-
      Cached idiom from param_expansion.dart (HashSet.Add returns
      false on duplicate) — reuse verbatim. Three load-bearing
      nuances: (1) **Atomic "check and add"** — the Dart pattern
      `if (set.contains(x)) throw; set.add(x);` is TWO operations;
      the C# pattern `if (!set.Add(x)) throw;` is ONE — strictly
      tighter (eliminates the impossible-but-real race where the
      set could theoretically change between contains and add; not a
      real concern here because we're single-threaded, but the C#
      idiom is cleaner). Authoritative: Microsoft Learn
      `HashSet<T>.Add` — "Returns true if the element is added to the
      `HashSet<T>` object; false if the element is already present."
      (2) **Backslash-in-key string** — the Dart literal `'\\/2'` is
      the two-character string `\/2` (Dart `\\` = single backslash);
      C# `"\\/2"` is also `\/2` (C# `\\` = single backslash). The
      bytes MUST match for the functor table to interoperate across
      the conversion. Either escaped or verbatim string form works;
      spec records both as acceptable. (3) **Primitive-overlap
      semantic** — `Number` overlaps with `Integer` and `Real` in
      both directions (the type-environment spec v0.5 mandates this
      determinism check). The C# port preserves the four-way check
      verbatim: Number-vs-existing-Integer-or-Real,
      Integer-or-Real-vs-existing-Number, plus the direct-duplicate
      check. Domain invariant; preserved with no refactor.

conversion_units:
  - "namespace Glp.Analysis.TypeChecker { public static class TypeEnvironmentBuilder { ... } public sealed class RedefinitionError : Exception { ... } public sealed class CircularAliasError : Exception { ... } public sealed class NonDeterministicTypeError : Exception { ... } public sealed class AliasExpansionError : Exception { ... } }"
  - "exception: sealed class RedefinitionError : Exception (Line/Column get-only long; ctor(string message, long line, long column) : base(message); override ToString() => $\"{Message} at line {Line}, column {Column}\")"
  - "exception: sealed class CircularAliasError : Exception (same shape)"
  - "exception: sealed class NonDeterministicTypeError : Exception (same shape)"
  - "exception: sealed class AliasExpansionError : Exception (same shape)"
  - "field: private static string? _preludeEnvironmentSource (single-writer-many-readers; ECMA-335 atomic reference write)"
  - "method: public static void SetPreludeEnvironmentSource(string source) => _preludeEnvironmentSource = source; (engine init hook, single-call contract per doc-comment)"
  - "method: public static TypeEnvironment BuildPreludeEnvironment() — `??`-fallback to Prelude.TypePrelude; empty-source returns fresh new TypeEnvironment(new(), new()); Lexer/Parser pipeline; template-extraction foreach over module.TypeDefs; ParamExpansion.ExpandParameterizedTypes; private BuildEnvironmentFromModule(checkRedefinitions: false, resolveAliasesNow: true); returns new TypeEnvironment(...,typeTemplates: preludeTemplates)"
  - "method: public static TypeEnvironment BuildTypeEnvironment(Module module, TypeEnvironment? ancestorScope = null) — `??`-fallback to BuildPreludeEnvironment; BuildEnvironmentFromModule(checkRedefinitions: ancestorScope == null, resolveAliasesNow: false); merged via baseEnv.Merge(userEnv); defensive Dictionary copy of types/procedures (Ordinal); ResolveAliases mutates copies; return new TypeEnvironment(types, procedures, paramProcDecls: merged.ParamProcDecls) — note paramProcDecls NOT defensively copied (intentional, recorded)"
  - "method: private static TypeEnvironment BuildEnvironmentFromModule(Module module, bool checkRedefinitions, bool resolveAliasesNow) — three Dictionary accumulators (Ordinal); foreach over module.TypeDefs (RedefinitionError on predefined hit when flag set; CheckDeterminism on non-alias; indexer set); foreach over module.ProcDeclarations (RedefinitionError on predefined hit when flag set; conditional ProcDecl(...) creation when isBuiltin promotion needed, otherwise alias original; indexer by procDecl.QualifiedKey); foreach over module.ParamProcDecls (indexer by paramDecl.QualifiedKey); conditional ResolveAliases call; return new TypeEnvironment(..., paramProcDecls: paramProcDecls). Call sites use named-argument syntax to preserve Dart `required`-named-call semantics."
  - "method: public static List<Clause> ExtractClauses(Module module) — accumulator List<Clause>; foreach proc in module.Procedures: clauses.AddRange(proc.Clauses); return clauses; (alternative LINQ SelectMany recorded but imperative default for review parity)"
  - "method: private static bool IsSimpleAlias(TypeDef def) — alternatives.Count != 1 ? false : alt is PrimitiveModeAlt or alt is TypeRef"
  - "method: private static bool IsUnionAlias(TypeDef def) — alternatives.Count < 2 ? false; foreach alt: alt is not TypeRef r ? false; Prelude.IsPredefinedType(r.Name) ? false; return true"
  - "method: private static bool IsTypeAlias(TypeDef def) => IsSimpleAlias(def) || IsUnionAlias(def);"
  - "method: private static void ResolveAliases(IDictionary<string, TypeDef> types, IDictionary<string, ProcDecl> procedures) — Step 1 classify; early-exit if both empty; Step 2 transitive resolve via non-static local function ResolveSimpleAliasLocal (captures resolved/visiting/simpleAliases) with cycle detection via visiting HashSet; Step 3 union expansion (per-alt foreach, AliasExpansionError on alias-references-alias, AliasExpansionError on undefined type, ApplyComplementToAlt per target alt, CheckDeterminism on expanded def, indexer set); Step 4 replace simple alias refs in non-simple-alias types (.Where(!simple).ToList() snapshot; per-entry .Select(_replaceAliasReferences).ToList(); indexer set); Step 5 replace alias refs in procedures (foreach over .ToList() snapshot; .Select(_replaceAliasReferences).ToList() on argTypes; new ProcDecl with full field carry-forward); Step 6 remove simple alias defs from types (foreach over .Keys.ToList() snapshot)"
  - "method: private static TypeExpr ApplyComplement(TypeExpr expr, bool applyComplement, long line, long column) — early-exit (!applyComplement); type-pattern switch arms for TypeRef (fresh new TypeRef with !isInput) / PrimitiveModeAlt (fresh new with !isInput) / default (return expr)"
  - "method: private static TypeExpr ApplyComplementToAlt(TypeExpr alt, bool applyComplement, long line, long column) — early-exit (!applyComplement); type-pattern switch with arms: TypeRef (fresh), PrimitiveModeAlt (fresh), ConstantAlt/ListNilAlt (pass-through identity), ListConsAlt (recurse Head/Tail), StructAlt (recurse Args via Select(...).ToList()), DiffListAlt (recurse Content/Hole), default (return alt)"
  - "method: private static TypeExpr ReplaceAliasReferences(TypeExpr expr, IDictionary<string, TypeExpr> resolved) — type-pattern switch: TypeRef arm uses resolved.TryGetValue(out var t) + ApplyComplement (NOT the indexer — Dart map[]-returns-null vs C# indexer-throws-KeyNotFoundException); PrimitiveModeAlt/ConstantAlt/ListNilAlt pass-through; ListConsAlt/StructAlt/DiffListAlt recursive descent with .Select(...).ToList() materialisation on StructAlt.Args; default pass-through"
  - "method: private static void CheckDeterminism(TypeDef def) — three HashSet<string>(Ordinal) accumulators (functors/constants/primitives) + bool hasWildcard; type-pattern switch on each alt: ConstantAlt (HashSet.Add returns false ⇒ NonDeterministicTypeError with .ToString() value key); ListNilAlt ([]/0 key); ListConsAlt ([|]/2 key); StructAlt ($\"{s.Functor}/{s.Args.Count}\" key); DiffListAlt (\\/2 key — byte-exact two-char \\ + / + 2); PrimitiveModeAlt (hasWildcard || primitives.Count > 0 ⇒ throw; set hasWildcard); TypeRef (if name in {Integer,Real,Number,String} ⇒ CheckPrimitiveOverlap + primitives.Add)"
  - "method: private static void CheckPrimitiveOverlap(string newPrimitive, ISet<string> existing, bool hasWildcard, TypeDef def) — direct transliteration of four-way overlap check (wildcard, Number-vs-Integer/Real both directions, direct duplicate); each path throws NonDeterministicTypeError with byte-exact message"
  - "XML-doc /// summary blocks ported verbatim from each Dart /// doc-comment (class/function purpose; spec citation 'Specification: docs/modules/type-environment.md v0.8' as a file-level <remarks>; per-helper rationale)"

escalations: []
```

## Rationale & Research Provenance

This file is the type-environment assembler for the GLP type-checker pipeline:
it lexes/parses the prelude, expands parameterised types (delegating to
`expandParameterizedTypes`), merges user definitions, resolves simple/union
type aliases transitively with cycle detection, and validates determinism of
each `TypeDef`. It exposes two public entry points (`buildPreludeEnvironment`,
`buildTypeEnvironment`) plus four domain exception types and one mutable
engine-init hook (`setPreludeEnvironmentSource`). Every non-trivial decision
below carries an authoritative Dart/.NET citation; cached idioms reused
verbatim per FR-012/FR-024 (no re-research for the dozen-plus cached patterns
this file shares with `type_ast.dart`, `param_expansion.dart`, `prelude.dart`,
`error.dart`, `clause_validation.dart`).

### dart-implements-exception-to-csharp-derive-system-exception  (cached idiom)

**Deep analysis.** Four exception classes (`RedefinitionError`,
`CircularAliasError`, `NonDeterministicTypeError`, `AliasExpansionError`)
share one structure: `final String message; final int line; final int
column;` + ctor + `@override String toString() => '$message at line $line,
column $column'`. They are thrown from `_buildEnvironmentFromModule`
(predefined-type/proc redefinition), `_resolveAliases`'s recursive closure
(circular alias chain), the union-alias expansion step (alias-references-
alias, undefined type), and `_checkDeterminism` / `_checkPrimitiveOverlap`
(duplicate functor/constant, primitive overlap, wildcard overlap).

**Research (cached, FR-024 — no fresh call).** Reuses
`rf-dart-implements-exception-to-csharp-derive-system-exception` (anchored
in `error.dart.md` / `CompileError`). Dart `Exception` is an interface
(api.dart.dev: "abstract interface … can only be implemented"); .NET
`System.Exception` is a concrete base class (Microsoft Learn
`how-to-create-user-defined-exceptions`). No .NET throwable interface
exists. Idiom `dart-implements-exception-to-csharp-derive-system-exception`
is `active`; reuse verbatim.

**Conclusion.** Four `public sealed class <Name> : Exception` types. `message`
routed via `: base(message)` so `Exception.Message` is set; `Line`/`Column`
become get-only `long` auto-properties (cached `rf-dart-int-to-csharp-long-
width` from opcodes.dart/error.dart). `ToString()` overrides REPLACE base
shape (no `base.ToString()` call) — same decision as `CompileError` in
`error.dart.md`. NO `[Serializable]`, NO multi-ctor "common pattern" — Dart
source declares one ctor and we preserve that surface (FR-013). Suffix-
naming-policy escalation already recorded against `CompileError` in
`error.dart.md`; THIS file inherits that policy (preserve source `<Name>Error`
suffix) — no fresh escalation here.

### dart-tostring-interpolation-to-csharp-interpolated-string  (cached idiom)

**Deep analysis.** Each exception's `toString` uses Dart `'$message at line
$line, column $column'`. Pure formatting — no source-line slicing, no caret,
no branching (contrast `CompileError`'s diagnostic with caret).

**Research (cached, FR-024).** Reuses
`rf-dart-tostring-interp-to-csharp-tostring-interp` (error.dart.md);
`rf-csharp-interpolated-string-equivalent-to-dart-interpolation`
(program_dfa.dart, clause_validation.dart). Idiom
`dart-tostring-interpolation-to-csharp-interpolated-string` is `active`;
reuse verbatim per SC-007.

**Conclusion.** `=>` arrow body, `$"{Message} at line {Line}, column
{Column}"`. Override REPLACES base — intentional, documented.

### dart-private-nullable-mutable-string-field-to-csharp-private-static-nullable  (NEW idiom)

**Deep analysis.** `String? _preludeEnvironmentSource;` is the only mutable
top-level state in the file — set once by `setPreludeEnvironmentSource`
(documented contract: "Call this once during engine initialization"), read
many times by `buildPreludeEnvironment`. Dart isolates are single-threaded
within an isolate, so the source has no synchronisation.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/atomic-references` —
Microsoft Learn / ECMA-335 atomic-reads-writes guarantee. Verbatim
authoritative: "Reads and writes of the following data types are atomic:
bool, char, byte, sbyte, short, ushort, uint, int, float, and reference
types." `string?` is a reference type. Therefore a plain `static string?`
field replicates the Dart single-writer-many-readers contract with zero
tearing risk and no `volatile`/`Interlocked` ceremony. Verbatim query: "C#
ECMA-335 atomic reads writes reference types static field".
Authoritative.

**Conclusion.** `private static string? _preludeEnvironmentSource;` + a
`public static void SetPreludeEnvironmentSource(string source)` setter on
the host static class. No `volatile`, no `Interlocked.Exchange`, no
`Lazy<string>` wrapper — these would manufacture stronger ordering than
Dart provides (FR-013). Idiom recorded as new active KB entry: file-first
in the type_checker family.

### dart-toplevel-driver-fn-to-csharp-static-builder-method  (cached idiom)

**Deep analysis.** Both `buildPreludeEnvironment` and `buildTypeEnvironment`
are multi-step pipelines with local mutable accumulators returning a fresh
`TypeEnvironment`. Identical shape to `expandParameterizedTypes`
(param_expansion.dart) and `buildProgramDFA` (program_dfa.dart).

**Research (cached, FR-024).** Reuses
`rf-csharp-mutable-local-accumulator-pure-function` (program_dfa.dart,
param_expansion.dart). Idiom is `active`; reuse verbatim.

**Conclusion.** Host on `public static class TypeEnvironmentBuilder` in
namespace `Glp.Analysis.TypeChecker`. Preserve step-by-step structure
verbatim. Two extracted sub-nuances both load-bearing:

(1) **`??` (null-coalesce) preservation** — both pipelines use Dart `??`
to fall back: `_preludeEnvironmentSource ?? typePrelude` and `ancestorScope
?? buildPreludeEnvironment()`. Microsoft Learn `??` operator — identical
short-circuit semantics; the right operand is evaluated only when the left
is null. Direct transliteration: C# `??`.

(2) **Fresh-allocation discipline** — the empty-source branch and the
`{}` literal default for missing inputs MUST allocate fresh `Dictionary`
instances per call (never share a static singleton): `TypeEnvironment` is
a mutable accumulator (cached nuance from type_ast.dart — `addType` /
`addProcedure` mutate in place), so sharing would alias mutable state
across instances. Same nuance recorded in `type_ast.dart.md` for
`TypeEnvironment.empty()`.

### rf-csharp-dictionary-copy-constructor-shallow  (NEW research finding)

**Deep analysis.** `buildTypeEnvironment` calls `Map<String, TypeDef>.from(
merged.types)` and `Map<String, ProcDecl>.from(merged.procedures)` to
create defensive copies, because the subsequent `_resolveAliases` call
mutates those maps in place (`types[k] = ...`, `types.remove(k)`).

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.-ctor#system-collections-generic-dictionary-2-ctor(system-collections-generic-idictionary((-0-1))-system-collections-generic-iequalitycomparer((-0)))` —
Microsoft Learn, decisive: "Initializes a new instance of the
`Dictionary<TKey,TValue>` class that contains elements copied from the
specified `IDictionary<TKey,TValue>` … Every key in the dictionary must
be unique … The new Dictionary contains references to the same values."
This is the C# counterpart of Dart `Map<K,V>.from(other)`. Both perform
shallow copies (new container, aliased values). Verbatim query: "C#
Dictionary copy constructor shallow IDictionary parameter equality
comparer". Authoritative.

**Conclusion.** `Map<K,V>.from(other)` → `new Dictionary<K,V>(other,
StringComparer.Ordinal)`. The defensive copy is correctness-critical:
without it, `_resolveAliases`'s mutations would propagate back to the
caller's environment (silent footgun). The `paramProcDecls` field is
deliberately NOT defensively copied (the source does not copy it,
because alias resolution does not touch it) — recorded as a maintenance
invariant; if alias resolution ever extends to `paramProcDecls`, a
defensive copy must be added.

### dart-private-static-module-assembler-with-flag-params + rf-csharp-required-named-bool-to-positional-bool-or-namedarg  (NEW idiom + NEW research)

**Deep analysis.** `_buildEnvironmentFromModule` takes two `required bool`
named parameters (`checkRedefinitions`, `resolveAliasesNow`) and
dispatches behaviour on them. The Dart `required` keyword forces the
caller to supply both at the call site by name. C# has no `required`
named parameter (the C# `required` keyword applies to properties /
record members for object-initialiser enforcement, NOT to method
parameter call-site labeling).

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments` —
Microsoft Learn, decisive: "Named arguments enable you to specify an
argument for a parameter by matching the argument with its name rather
than with its position in the parameter list. … You can mix named and
positional arguments." Verbatim query: "C# named arguments required
keyword parameter boolean". Authoritative confirms: C# has named-
argument *call syntax* (free at the call site) but the *signature*
side has no `required`-named-only modifier (the C# `required` keyword
is for member-initialisation enforcement, a different concept).

**Conclusion.** Method signature uses positional `bool checkRedefinitions,
bool resolveAliasesNow` (no defaults — both conceptually mandatory).
Call sites within this file (`BuildPreludeEnvironment` and
`BuildTypeEnvironment`) MUST use named-argument syntax
(`BuildEnvironmentFromModule(module, checkRedefinitions: false,
resolveAliasesNow: true)`) to preserve Dart's self-documenting call-site
labels — a CONVENTION enforced by code review, not by the C# compiler.
Spec records this as the type_checker family convention. Idiom recorded
as new active KB entry: file-first in the family. Pure boolean-blindness
mitigation; Microsoft Learn lists named arguments as the documented
alternative to overloaded boolean APIs.

### dart-list-addall-to-csharp-list-addrange + rf-csharp-list-addrange-vs-linq-selectmany  (NEW idiom + NEW research)

**Deep analysis.** `extractClauses` builds a flat list of all clauses
across all procedures in the module via `clauses.addAll(proc.clauses)`.
This is a simple O(n) accumulator pattern.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1.addrange` —
Microsoft Learn, decisive: "Adds the elements of the specified
collection to the end of the `List<T>`. … The collection itself cannot
be null, but it can contain elements that are null, if type T is a
reference type." Plus `System.Linq.Enumerable.SelectMany` — Microsoft
Learn: "Projects each element of a sequence to an `IEnumerable<T>` and
flattens the resulting sequences into one sequence. … This method is
implemented by using deferred execution." Verbatim queries: "C# List
AddRange documented IEnumerable parameter"; "C# LINQ SelectMany flatten
deferred execution". Authoritative.

**Conclusion.** `List.addAll(other)` → `List<T>.AddRange(other)`. Faithful
imperative-accumulator transliteration. Alternative LINQ form
`module.Procedures.SelectMany(p => p.Clauses).ToList()` is
semantically identical (with `.ToList()` materialisation) and recorded
as an optional micro-optimisation; the imperative form is the spec
default for review parity with the Dart source. Idiom recorded as new
active KB entry: file-first in the family.

### dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch  (cached idiom — three call sites)

**Deep analysis.** The file uses `is X` dispatch in four locations:
`_isSimpleAlias` (two-way OR), `_isUnionAlias` (loop with `is not`),
`_applyComplementToAlt` (seven-way recursive AST walker),
`_replaceAliasReferences` (seven-way recursive AST walker),
`_checkDeterminism` (seven-way alt-classification dispatch).

**Research (cached, FR-024).** Reuses
`rf-dart-extension-is-as-to-csharp-type-pattern-switch` (type_ast.dart,
program_dfa.dart, clause_validation.dart, param_expansion.dart). Idiom
`dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch` is `active`;
reuse verbatim per SC-007.

**Conclusion.** Each `is X` chain becomes a C# type-pattern `switch`
statement (or expression) with declaration patterns (`case TypeRef r:
... break;`) — fuses `is` test + `as` cast into one arm. `is not` →
C# `is not`. Recursion depth bounded by AST depth (small, single-digit
for realistic type definitions) — no stack-overflow risk, no work-stack
transform required. Reference semantics preserved.

### dart-multistep-pipeline-with-recursive-closure-and-cycle-detection-to-csharp-local-function + rf-csharp-local-function-vs-lambda-recursive  (NEW idiom + NEW research)

**Deep analysis.** `_resolveAliases` is a six-step pipeline. Step 2
contains a nested recursive function `resolveSimpleAlias` that captures
`resolved`, `visiting`, and `simpleAliases` from the enclosing scope.
Dart nested functions are first-class closures and trivially recursive.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/local-functions` —
Microsoft Learn, decisive: "Local functions are private methods of a
type that are nested in another member. They can only be called from
their containing member. Local functions can be declared in and called
from: Methods, especially iterator methods and async methods …
Constructors … Property accessors … Event accessors … Anonymous methods
… Lambda expressions … Finalizers … Other local functions." And:
"Local functions can use local variables and method parameters defined
in their enclosing scope." Recursion: local functions support natural
recursion via name reference (unlike lambdas, which require the
`Func<...>`-self-reference workaround). Verbatim query: "C# local
functions recursion capture enclosing scope variables".
Authoritative.

**Conclusion.** Port the Dart nested recursive function as a **non-static
local function** inside the enclosing C# method, so it captures the
enclosing `resolved` / `visiting` / `simpleAliases` locals (Microsoft
Learn: "Local functions that aren't declared static can capture
variables from the enclosing scope"). The `visiting.Add(name)` /
`visiting.Remove(name)` bracket around the recursive call implements
DFS cycle detection identically; `CircularAliasError` is thrown with
the alias definition's `Line`/`Column` (not the call site's). Idiom
recorded as new active KB entry: file-first use of a nested-recursive-
closure pattern in the type_checker family.

The five-step shape after Step 2 follows the established
`dart-toplevel-driver-fn-to-csharp-static-builder-method` cached idiom
(snapshot-iteration via `.ToList()`, fresh-`new`-allocations for
TypeDef/ProcDecl, ordinal-keyed `Dictionary`s). Each step's
authoritative basis is in param_expansion.dart.md (cached).

### dart-map-indexer-null-on-miss-to-csharp-trygetvalue + rf-csharp-dictionary-trygetvalue-vs-indexer-null-vs-keynotfound  (NEW idiom + NEW research)

**Deep analysis.** `_replaceAliasReferences` uses `final resolvedTarget
= resolved[expr.name]; if (resolvedTarget != null) { ... }` — Dart's
`Map[]` indexer returns `null` on miss. This is THE classic Dart→C#
silent-bug conversion pitfall.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.item` —
Microsoft Learn, decisive: "Gets or sets the value associated with the
specified key. … `System.Collections.Generic.KeyNotFoundException`:
The property is retrieved and key does not exist in the collection."
Plus `Dictionary<TKey,TValue>.TryGetValue(TKey, out TValue)` — Microsoft
Learn: "Gets the value associated with the specified key. … When this
method returns, contains the value associated with the specified key,
if the key is found; otherwise, the default value … Returns true if
the dictionary contains an element with the specified key; otherwise,
false." Verbatim query: "C# Dictionary indexer throws KeyNotFoundException
miss vs Dart map null". Authoritative.

**Conclusion.** Dart `dict[k]` (returns null on miss) → C#
`dict.TryGetValue(k, out var v)` (returns false + null `v` on miss).
Naive `dict[k]` transliteration would CHANGE BEHAVIOUR from "fall
through to default" to "throw exception" — a silent semantic regression.
Idiom recorded as new active KB entry: this conversion pattern recurs
across the codebase and any Dart `Map[]`-with-null-check MUST become
`TryGetValue` in C#.

### dart-set-contains-then-add-to-csharp-hashset-add-bool-return  (cached/refined idiom)

**Deep analysis.** `_checkDeterminism` uses the pattern `if
(set.contains(key)) throw; set.add(key);` repeatedly. The C# equivalent
fuses into `if (!set.Add(key)) throw;`.

**Research (cached, FR-024).** Reuses
`rf-csharp-hashset-add-returns-false-on-duplicate` (param_expansion.dart
— `HashSet<T>.Add` returns true if added, false if already present:
Microsoft Learn). The idiom is `active` from
`dart-collection-spread-union-to-csharp-hashset-unionwith`'s side notes;
the `contains-then-add` two-step → `Add`-with-return-check refinement
is recorded explicitly here as a sibling-to-the-union variant. Verbatim
query: "C# HashSet Add return value duplicate". Authoritative.

**Conclusion.** Every `set.contains(k) throw; set.add(k);` site → `if
(!set.Add(k)) throw new NonDeterministicTypeError(...);`. Strictly
tighter (single atomic operation; no race window between `contains`
and `add` — not a real concern here because we are single-threaded,
but the idiom is cleaner). Recorded as active sibling idiom in the KB.

### Cross-file constraints (anchored elsewhere; this file depends but does not redefine)

The following dependencies anchor in OTHER conversion specs and must
emit consistent target shapes:

- `Module`, `Procedure`, `Clause`, `TypeDef`, `ProcDecl`, `TypeExpr`,
  `TypeRef`, `PrimitiveModeAlt`, `ConstantAlt`, `ListNilAlt`,
  `ListConsAlt`, `StructAlt`, `DiffListAlt`, `TypeEnvironment` —
  anchored in `type_ast.dart.md` and `ast.dart.md`. Constructor
  signatures (positional + named parameter labels matching Dart source
  lowerCamel) MUST be honoured for the named-argument call sites in
  this file (e.g. `new ProcDecl(name, argTypes, line, column, isBuiltin:
  true, exported: ..., imported: ..., modulePath: ...)`).
- `Lexer.Tokenize`, `Parser.ParseModule` — anchored in `lexer.dart.md`
  and `parser.dart.md`.
- `Prelude.IsPredefinedType`, `Prelude.IsPredefinedProcedure`,
  `Prelude.IsBuiltinProcedure`, `Prelude.TypePrelude` — anchored in
  `prelude.dart.md`.
- `ParamExpansion.ExpandParameterizedTypes` — anchored in
  `param_expansion.dart.md`.

This file's convspec records dependencies on those names but does not
redefine them; consistency is the responsibility of the codegen stage
when it assembles cross-file output.

### Trivial / non-construct elements

- `// lib/analysis/type_checker/type_environment_builder.dart` file header
  + `// Specification: docs/modules/type-environment.md v0.8` spec citation
  map mechanically to C# `//` comments — no research required.
- `/// XML doc-comments` on classes, methods, and the private helpers
  port 1-for-1 to C# `/// <summary>...</summary>` blocks — Dart triple-
  slash and C# triple-slash semantics are identical.
- `import 'type_ast.dart';` etc. are subsumed by `using Glp.Analysis
  .TypeChecker;`, `using Glp.Compiler;` `using` directives the codegen
  emits per the project's namespace layout (cross-file concern; not
  specced per construct).
- `final` field declarations (Dart) → get-only auto-properties (C#) —
  cached mechanical mapping.

### No escalations

All non-trivial constructs resolved against official Dart/.NET
documentation with consistent conclusions; no undecidable points; no
idiom/research conflicts. The suffix-naming-policy question (`<Name>Error`
vs `<Name>Exception`) is INHERITED from the project-wide escalation
already recorded against `CompileError` in `error.dart.md` — this file
does not re-escalate; it follows whatever Gabi decides project-wide.
`open_escalation_count` = 0.
