# Conversion Spec — lib/compiler/compiler.dart

> Conversion-spec artifact for lib/compiler/compiler.dart (FR-011).
> Spec-only (FR-023): describes the Dart→C# conversion of the top-level
> `GlpCompiler` facade; contains NO compilable C#. A later codegen stage
> consumes the structured block.

```yaml
schema_version: 1
source_path: lib/compiler/compiler.dart
source_sha256: 1b65ae574b5c4d866bf91680efdd48fd3a59072b31f44da7f2a3e19cd6ddc310
target_code_unit: lib/compiler/compiler.cs
constructs:
  - construct_key: dart.module.relative_imports_with_one_package_import_and_three_reexports
    source_form: >-
      Nine import directives (eight relative — `lexer.dart`, `parser.dart`,
      `analyzer.dart`, `codegen.dart`, `partial_evaluator.dart` (added
      2026-05-20 as part of escalation #3 Option-(b): the strict
      analyzer-internal `PartialEvaluator` was renamed to
      `DefinedGuardEvaluator` and `compiler.dart` was switched to import
      the lenient `PartialEvaluator` from `partial_evaluator.dart`
      directly), `error.dart`, `token.dart`, `result.dart`, plus a
      relative `ast.dart` with a `show` allow-list of AST node types,
      and a relative cross-subdirectory
      `../analysis/type_checker/type_ast.dart show ProcDecl` and
      `../analysis/type_checker/type_checker.dart show checkModule`),
      plus one `package:`-prefixed import
      `import 'package:glp_runtime/bytecode/runner.dart' show BytecodeProgram;`
      that targets the same Dart package (`glp_runtime`) the file lives in.
      Followed by three `export` directives: a re-export of `BytecodeProgram`
      from `package:glp_runtime/bytecode/runner.dart`, a re-export of
      `CompilationResult` from `result.dart`, and a *self*-re-export
      `export 'compiler.dart' show CompileOptions;` (the file re-exports its
      own `CompileOptions` for downstream consumers that import only
      `compiler.dart`).
    target_decision: >-
      Emit `using <root>.Compiler;` (the same-namespace siblings — `lexer.cs`,
      `parser.cs`, `analyzer.cs`, `codegen.cs`, `error.cs`, `token.cs`,
      `result.cs`, `ast.cs` — elide because they share the namespace per
      `rf-dart-relative-import-to-csharp-using-or-same-namespace`); emit
      `using <root>.Analysis.TypeChecker;` for the cross-subdirectory
      `ProcDecl` + `checkModule` imports; emit `using <root>.Bytecode;` for
      `BytecodeProgram` (carrying forward `rf-dart-import-relative-to-csharp-using-namespace`
      and `rf-dart-import-show-clause-no-csharp-counterpart` — `show` clauses
      drop because C# has no per-symbol `using` narrowing). All three Dart
      `export` directives DROP at the C# level per
      `rf-dart-export-directive-to-csharp-using-alias`: C# has no per-file
      re-export — downstream consumers acquire the re-exported types by
      adding the same `using <Namespace>;` directly (or by reference to an
      assembly that contains the namespace). The self-re-export
      `export 'compiler.dart' show CompileOptions;` is purely a Dart
      surface-shape concern (Dart imports of `compiler.dart` get
      `CompileOptions` for free) — semantically a no-op in C# where types
      are addressed by their declaring namespace, not by import-chain
      transitivity.
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-using-or-same-namespace
    nuance: >-
      Module-shape nuance (explicitly addressed). Three composing idioms,
      each cached, recur in this file: (1) relative imports → same-namespace
      elision per `rf-dart-relative-import-to-csharp-using-or-same-namespace`
      — files under the same Dart subdirectory map to the same C# namespace
      and require no `using` directive; (2) `package:`-prefixed imports
      targeting the SAME Dart package as the importing file resolve to the
      converted namespace of that directory (`Bytecode`) per
      `rf-dart-import-relative-to-csharp-using-namespace`; (3) `show`
      clauses (here on `ast.dart`, on `type_ast.dart`, on `type_checker.dart`,
      and on the `package:` import) drop per
      `rf-dart-import-show-clause-no-csharp-counterpart` — C# `using` has
      no per-symbol allow-list; the post-conversion file accesses the FULL
      public surface of each imported namespace. The `export` re-emission is
      the load-bearing decision specific to this file: Dart `export`
      directives let an importing file expose symbols from another library
      through its own URI, forming an aggregator/facade. C# has no
      analogous per-file surface — the entire namespace is the export
      surface. Consumers that imported `compiler.dart` *because* it
      re-exported `BytecodeProgram` and `CompilationResult` MUST, after
      conversion, also `using` the namespaces hosting those types
      (`<root>.Bytecode`, `<root>.Compiler` — the latter is the same
      namespace as `compiler.cs`, so it's free for callers already
      `using`-ing the compiler namespace). The self-re-export is
      vestigial in C# and silently drops. Value-vs-reference, null-safety,
      async: NOT APPLICABLE to import/export directives. SC-006 hygiene:
      no per-symbol narrowing survives the conversion — this is recorded
      now (not re-discovered) and is consistent with every prior runtime/*
      and compiler/* spec.
  - construct_key: dart.data_class.compile_options_two_bool_fields_const_named_ctor_with_defaults
    source_form: >-
      "class CompileOptions { final bool typeCheck; final bool strictTypes;
      const CompileOptions({this.typeCheck = false, this.strictTypes =
      false}); }" — a plain Dart class with TWO `final bool` instance
      fields, a single `const` constructor with NAMED parameters whose
      defaults are both `false`, and initialising-formals (`this.typeCheck`,
      `this.strictTypes`). The constructor is `const` (constant evaluation
      eligible — the construct `const CompileOptions()` is permitted at
      call-sites; in this file the default-options call-site uses
      `const CompileOptions()` once, in `compileWithMetadata`). No
      `==`/`hashCode` override, no `toString` override, no methods, no
      inheritance, no interface implementation. The class is library-public.
    target_decision: >-
      Emit `public class CompileOptions` (NOT a `record`, NOT a `struct`)
      with two `public` get-only auto-properties `TypeCheck` and
      `StrictTypes` (both `bool`, non-nullable under enabled NRT). Provide a
      single constructor with two optional `bool` parameters defaulting to
      `false` — Dart NAMED parameters with defaults map to C# OPTIONAL
      POSITIONAL parameters with defaults per
      `rf-dart-named-default-param-to-csharp-optional-arg` (the Dart named
      call-site `CompileOptions(typeCheck: true)` becomes the C# call
      `new CompileOptions(typeCheck: true)` — C# supports named-argument
      *invocation* even for positional parameters, so the call-site
      ergonomics are preserved). Reject `record`: the Dart source has no
      `==` override (default identity equality); a C# `record` would
      synthesise structural equality from positional properties, silently
      changing semantics — same rejection rationale as
      `result.dart.md`/`token.dart.md`. Reject `struct`: although the class
      is small and effectively-immutable, the Dart source uses it via
      reference semantics (the `const CompileOptions()` default is shared
      among call-sites, and a future call-site may pass it through
      method parameters where defensive copies would conflate identity);
      following the project convention recorded in
      `rf-dart-final-field-class-to-csharp-getonly-class`, keep
      reference-class. The Dart `const` constructor does NOT map to any C#
      construct — `const` on a Dart constructor enables compile-time
      canonical instances of objects with `final` fields, which has no
      direct C# analogue (`readonly struct` is the nearest, but rejected
      above). The `const CompileOptions()` default-options call-site MUST
      lower to `new CompileOptions()` in C# — the Dart canonicalisation is
      a perf hint, not a semantic guarantee, and the converted call-site
      will allocate one instance per call. Optional micro-optimisation
      (NOT in baseline conversion): emit a `public static readonly
      CompileOptions Default = new();` shared singleton and substitute it
      at the call-site for an exact-shape match of Dart's canonicalisation.
      Recorded as a follow-up — baseline emits naive `new`.
    idiom_id: null
    research_finding_id: rf-dart-named-default-param-to-csharp-optional-arg
    nuance: >-
      Const-constructor nuance (explicitly addressed). Dart's `const`
      constructor permits compile-time canonical instances:
      `identical(const CompileOptions(), const CompileOptions())` is
      `true` (Dart language tour: const constructors); C# has no
      direct counterpart for reference types (`readonly struct` gives
      value-type immutability but not canonicalisation; `static readonly`
      singletons can simulate but require an explicit field). For this
      file the canonicalisation IS observable at one call-site —
      `compileWithMetadata`'s `final opts = options ?? const
      CompileOptions();` defaults to the canonical instance — but the
      observability is performance-only (allocation count) and not
      behaviour: `==` is identity on both sides; no caller relies on
      identity of the default options object. Baseline conversion emits
      `new CompileOptions()`; the singleton optimisation is documented
      but not blocking. Boolean default-value nuance: Dart `bool` defaults
      both `false`; C# `bool` is `System.Boolean` (value type, default
      `false`) — direct 1:1 mapping. Named-parameter nuance: Dart
      NAMED parameters with defaults map to C# optional positional
      parameters per the cached idiom; C# 11+ accepts named-argument
      invocation on positional parameters, so call-sites preserve the
      Dart ergonomics (`new CompileOptions(strictTypes: true)`). No
      `Stream`/`Future`/async surface. Value-vs-reference: `class`
      preserves reference semantics matching the Dart source.
      Null-safety: both fields are non-nullable in Dart → non-nullable
      `bool` properties in C# under enabled NRT.
  - construct_key: dart.class.compiler_facade_with_four_injectable_factory_typedef_fields_and_pipeline_method
    source_form: >-
      "class GlpCompiler" — the facade class. FOUR `final` instance fields
      typed as function values (factory callbacks):
      `final Lexer Function(String) _createLexer;`,
      `final Parser Function(List<Token>) _createParser;`,
      `final Analyzer Function() _createAnalyzer;`,
      `final CodeGenerator Function() _createCodegen;`. Each field is a
      Dart function-type variable (no `typedef`; an inline function type
      `<Return> Function(<arg-types>)`). All four are leading-underscore →
      library-private. The constructor takes FOUR matching named OPTIONAL
      parameters (`createLexer`, `createParser`, `createAnalyzer`,
      `createCodegen`) typed as nullable function-types
      (`Lexer Function(String)?` etc.) with NO defaults; an initialiser-list
      computes each field as `null ??` of a default factory lambda
      (`createLexer ?? ((source) => Lexer(source))`, etc.). Pattern is
      classic dependency-injection-via-factory-callbacks: callers pass
      mocks/stubs for testing; production callers pass no args and get the
      default lambdas that call the canonical constructors. The class
      surfaces three methods: `compile` (BytecodeProgram return; one-line
      delegation to `compileWithMetadata`), `compileWithMetadata` (the
      pipeline body), and `compileProgram` (alternative entry that takes
      an already-parsed `Program` AST and skips lex/parse/typecheck/_select
      — used by the project linker). The class is library-public.
    target_decision: >-
      Emit `public sealed class GlpCompiler` (sealed: no subclass in the
      Dart source; matches Dart's "no `extends`" implicit closed-for-extension
      posture in this file). The four function-typed Dart fields map to
      C# `Func<,>` / `Func<>` delegate fields, each `private readonly` and
      named in PascalCase per `rf-dart-leading-underscore-privacy-to-csharp-private`:
      `private readonly Func<string, Lexer> _createLexer;`,
      `private readonly Func<IReadOnlyList<Token>, Parser> _createParser;`,
      `private readonly Func<Analyzer> _createAnalyzer;`,
      `private readonly Func<CodeGenerator> _createCodegen;`. Idiom recorded
      as new `rf-dart-function-typed-field-to-csharp-func-delegate-field`
      (see rationale section). Constructor: emit a single public
      constructor with four optional `Func<...>?` parameters, all
      defaulting to `null`; in the body, materialise each field with the
      coalescing default identical in semantics to Dart's `??`:
      `_createLexer = createLexer ?? (source => new Lexer(source));`
      `_createParser = createParser ?? (tokens => new Parser(tokens));`
      `_createAnalyzer = createAnalyzer ?? (() => new Analyzer());`
      `_createCodegen = createCodegen ?? (() => new CodeGenerator());`
      The Dart named-optional → C# named/optional positional translation
      reuses `rf-dart-named-default-param-to-csharp-optional-arg`. The
      `Parser` constructor's `List<Token>` argument: Dart `List<T>` →
      `IReadOnlyList<T>` here per the project's read-only-parameter idiom
      (the parser does not mutate the token list — call-site shape
      preserves the Dart immutability convention); a future spec for
      `parser.dart` may relax to `List<Token>` if the parser mutates the
      list internally (that file's spec records the decision; here we
      record the IReadOnlyList choice as the FACADE-side reasonable
      default).  The three methods `compile`/`compileWithMetadata`/
      `compileProgram` map directly to public C# methods of the same
      PascalCased names.
    idiom_id: null
    research_finding_id: rf-dart-function-typed-field-to-csharp-func-delegate-field
    nuance: >-
      Function-type nuance (explicitly addressed, new idiom). Dart
      `<Return> Function(<args>)` is a structural function-type (api.dart.dev
      "Functions" / "Typedefs"); it is assignment-compatible with any
      callable with matching arity and types — no nominal declaration is
      required. C# has TWO candidates: (a) `delegate` types (nominally-typed
      reference handles, declared once); (b) generic `Func<,>`/`Action<>`
      from `System` (Microsoft Learn: "Delegates" — `Func<TResult>` and
      `Func<T,TResult>` are structural-by-convention generic delegates).
      The Dart source uses inline function types (no `typedef`), which is
      the structural usage — the faithful C# render is `Func<>`/`Func<,>`
      per the recorded idiom, NOT a named `delegate` (which would require
      synthesising new nominal type names with no source counterpart and
      would harden the convention beyond the Dart surface). Func/Action
      split: Dart `Function()` returning `T` maps to `Func<T>`; Dart
      `Function(A)` returning `T` maps to `Func<A,T>` — the order is
      `(args..., TResult)` in C# `Func`. Reference-vs-value: function-type
      values are reference handles in BOTH Dart (closures are heap-allocated
      function objects) and C# (delegates are reference types); no
      copy-semantics surprises. Null-safety: the constructor parameters
      ARE nullable in the Dart source (`Lexer Function(String)?`), and so
      the C# parameters are `Func<string, Lexer>?` (nullable delegate)
      under enabled NRT; the field is NON-nullable because the coalescing
      assignment guarantees a non-null value post-construction —
      `private readonly Func<string, Lexer> _createLexer;` (non-nullable)
      is correct; the C# compiler will infer field-init non-nullability
      from the constructor's coalescing assignment. Async/Stream/isolate:
      NOT APPLICABLE — the factory callbacks are synchronous. Dependency-
      injection nuance: the pattern is "default-factory-with-test-seam" —
      production callers omit the arguments and get the canonical
      `new`-constructor lambdas; test callers inject stubs/mocks. C# has
      no special-case syntax — the optional `Func<>?` parameters with
      null-coalescing defaults preserve the pattern exactly.
  - construct_key: dart.method.compile_one_line_delegation_to_with_metadata_returning_field
    source_form: >-
      "BytecodeProgram compile(String source, [CompileOptions? options]) {
      final result = compileWithMetadata(source, options); return
      result.program; }" — a one-line delegation method that calls
      `compileWithMetadata` and returns its `.program` field. Takes a
      POSITIONAL OPTIONAL `CompileOptions?` parameter (square-bracket
      syntax — positional optional, NOT named; default `null`).
    target_decision: >-
      Emit `public BytecodeProgram Compile(string source, CompileOptions?
      options = null) { var result = CompileWithMetadata(source, options);
      return result.Program; }`. Dart POSITIONAL OPTIONAL parameters
      (square brackets) map to C# optional positional parameters with
      defaults per `rf-dart-named-default-param-to-csharp-optional-arg`
      (the same idiom covers both the named-optional and the
      positional-optional Dart surfaces — the C# target is identical:
      optional positional with default). `CompilationResult.program` →
      `CompilationResult.Program` per PascalCase property convention
      (`rf-dart-final-field-class-to-csharp-getonly-class`).
    idiom_id: null
    research_finding_id: rf-dart-named-default-param-to-csharp-optional-arg
    nuance: >-
      Positional-optional nuance (explicitly addressed). Dart distinguishes
      NAMED optional (curly braces) from POSITIONAL optional (square
      brackets); C# has only optional positional parameters. The Dart
      source uses POSITIONAL optional here (square brackets), which maps
      to the same C# optional-positional shape — call-sites that passed
      `compile(source)` and `compile(source, opts)` both preserve. No
      named-argument call-site appears in this file. Value-vs-reference,
      null-safety: `CompileOptions?` (nullable reference) preserves;
      `String` → `string` is value-semantics-by-equality, reference-
      semantics-by-storage, faithful in both languages.
  - construct_key: dart.method.compile_with_metadata_six_phase_pipeline_with_typecheck_branch_and_try_on_compileerror_rethrow_with_source_context
    source_form: >-
      "CompilationResult compileWithMetadata(String source, [CompileOptions?
      options]) { final opts = options ?? const CompileOptions(); try { ...
      } on CompileError catch (e) { throw CompileError(e.message, e.line,
      e.column, source: source, phase: e.category?.toString().split('.').last);
      } }" — the pipeline body. Six phases inside the `try`: (1) lex via
      `_createLexer(source).tokenize()`; (2) parse via
      `_createParser(tokens).parseModule()`; (3) wrap module's procedures
      in a `Program` AST; (2.4) partial-evaluation guard expansion via
      `PartialEvaluator().transformDefinedGuards(ast)`; (2.5) optional
      type-check via `checkModule` with inner `try { ... } catch (e) { if
      (opts.strictTypes) rethrow; print('[TYPE CHECK] Failed: $e'); }` and
      explicit logging of type errors/warnings to `print` with `[TYPE
      ERROR]`/`[TYPE WARNING]` prefixes plus a `strictTypes`-gated
      `throw CompileError(...)`; (3) semantic analysis via
      `_createAnalyzer().analyze(ast, generateReduce: ..., procDeclarations:
      ..., compileMode: ...)`; (4) code generation via
      `_createCodegen().generateWithMetadata(annotatedAst)`. Outer
      `on CompileError catch (e)` catches and RE-THROWS with augmented
      `source:` and `phase:` named arguments. The `phase:` argument
      reconstructs a string from an enum via
      `e.category?.toString().split('.').last` — Dart `Enum.toString()`
      returns `"EnumType.value"` and `.split('.').last` extracts `"value"`.
    target_decision: >-
      Emit `public CompilationResult CompileWithMetadata(string source,
      CompileOptions? options = null)`. Method body (six-phase pipeline
      preserved one-for-one):
      (a) `var opts = options ?? new CompileOptions();` — Dart `??` →
        C# `??` (`rf-dart-nullsafety-to-csharp-nrt` family).
      (b) Wrap the entire body in `try { ... } catch (CompileError e) {
        throw new CompileError(e.Message, e.Line, e.Column,
        source: source, phase: e.Category?.ToString().Split('.').Last());
      }`. Dart `on X catch (e)` → C# `catch (X e)` per the
        established try-on-catch idiom (cached, recorded as
        `rf-dart-try-on-catch-to-csharp-typed-catch` in this file, see
        rationale section — same posture as the inner try-catch noted in
        `system_predicates_impl.dart.md`). The augmented re-throw
        constructs a fresh `CompileError` because the original is an
        immutable error type with no setter for `source`/`phase`; this is
        a Dart `throw new`-style augmenting rethrow, NOT a `rethrow`. C#
        equivalent is `throw new CompileError(...)` (fresh instance —
        preserves the Dart semantics) per
        `rf-dart-typed-ctor-call-to-csharp-new`. Caveat: a fresh exception
        instance LOSES the original stack-trace prefix — to preserve it,
        emit `new CompileError(..., innerException: e)` and surface the
        inner exception through `CompileError`'s constructor (the
        `error.cs` spec records the constructor surface; this spec
        records the cross-file expectation). Phase enum lookup:
        `e.Category?.ToString().Split('.').Last()` — C# enum
        `ToString()` returns the unqualified value name (`"Lexical"`,
        not `"ErrorCategory.Lexical"`), so the `Split('.').Last()`
        post-processing is REDUNDANT in C# and emits as
        `e.Category?.ToString()` (CROSS-LANGUAGE SIMPLIFICATION recorded
        per `rf-dart-enum-tostring-prefix-vs-csharp-bare-name`, see
        rationale).
      (c) Phase 1 — `var lexer = _createLexer(source); var tokens =
        lexer.Tokenize();`. The factory-callback invocation is
        `_createLexer.Invoke(source)` or sugared `_createLexer(source)`
        — C# delegates support direct invocation syntax (Microsoft Learn
        "Delegates"). PascalCase method names per
        `rf-dart-method-naming-to-csharp-pascalcase` (cached).
      (d) Phase 2 — `var parser = _createParser(tokens); var module =
        parser.ParseModule();`.
      (e) Phase 2 wrap — `var ast = new Program(module.Procedures,
        module.Line, module.Column);`. The `Module → Program` conversion
        carries forward the `Program` constructor decision from
        `ast.dart.md`.
      (f) Phase 2.4 — `var partialEvaluator = new PartialEvaluator();
        var transformedAst = partialEvaluator.TransformDefinedGuards(ast);`.
        The `PartialEvaluator` type lives in `partial_evaluator.cs`.
      (g) Phase 2.5 — `if (opts.TypeCheck) { try { var typeResult =
        CheckModule(module, transformedProcedures: transformedAst.Procedures);
        if (typeResult.Errors.Count > 0) { foreach (var error in
        typeResult.Errors) { Console.WriteLine($"[TYPE ERROR]
        {error.Message} at line {error.Line}"); } if (opts.StrictTypes) {
        throw new CompileError($"Type checking failed with
        {typeResult.Errors.Count} error(s)", typeResult.Errors[0].Line,
        typeResult.Errors[0].Column); } } if (typeResult.Warnings.Count > 0)
        { foreach (var warning in typeResult.Warnings) {
        Console.WriteLine($"[TYPE WARNING] {warning.Message} at line
        {warning.Line}"); } } } catch (Exception e) when (!opts.StrictTypes)
        { Console.WriteLine($"[TYPE CHECK] Failed: {e}"); } catch
        (Exception) when (opts.StrictTypes) { throw; } }`.  Dart
        `rethrow` → C# `throw;` (no expression) per the established
        rethrow-preservation convention. The Dart code uses
        `try-catch`-with-conditional-rethrow; the C# render uses
        `when`-filtered catch clauses (Microsoft Learn "catch when") for
        the same semantics WITHOUT re-throwing in the catch body. Dart
        `print(...)` → `Console.WriteLine(...)` per
        `rf-dart-print-to-csharp-console-writeline` (cached). String
        interpolation: Dart `'[TYPE ERROR] ${error.message} at line
        ${error.line}'` → C# `$"[TYPE ERROR] {error.Message} at line
        {error.Line}"` per
        `rf-dart-string-interpolation-to-csharp-string-interpolation`
        (cached).
      (h) Phase 3 — `var generateReduce = module.CompileMode !=
        CompileMode.System; var analyzer = _createAnalyzer(); var
        annotatedAst = analyzer.Analyze(ast, generateReduce:
        generateReduce, procDeclarations: module.ProcDeclarations,
        compileMode: module.CompileMode);`. The named-argument
        call-site survives because C# supports named-argument
        invocation; the Dart named params map to C# optional/positional
        per `rf-dart-named-required-param-to-csharp-positional-arg` and
        siblings (cached).
      (i) Phase 4 — `var codegen = _createCodegen(); var result =
        codegen.GenerateWithMetadata(annotatedAst); return result;`.
    idiom_id: null
    research_finding_id: rf-dart-try-on-catch-to-csharp-typed-catch
    nuance: >-
      Six concurrent nuances, each explicitly addressed.
      (1) Try-on-catch: Dart `on X catch (e)` is a TYPED catch — only
          exceptions assignable to `X` enter the handler (api.dart.dev
          "Errors"). C# `catch (X e)` has the SAME semantics (Microsoft
          Learn "Exceptions"); the syntactic difference is mechanical.
      (2) Augmenting rethrow vs preserving rethrow: the OUTER catch
          constructs a FRESH `CompileError` (Dart `throw CompileError(...)`),
          NOT `rethrow`. This is intentional in the Dart source — the
          augmentation adds `source` and `phase` to the error before it
          reaches the caller. C# loses the original stack-trace on
          `throw new`; the spec records that
          `innerException` MUST be passed (the cross-file expectation
          on `error.cs`) to preserve diagnostic context. The INNER
          catch (Phase 2.5) uses `rethrow` to preserve the original
          exception unchanged when `strictTypes` is true — C# render
          is `throw;` (no expression) inside a `catch` block, which
          preserves the stack-trace exactly.
      (3) Print → Console.WriteLine: Dart `print` writes to stdout
          (api.dart.dev `print`); C# `Console.WriteLine` writes to
          `Console.Out` (Microsoft Learn). Identical observable
          behaviour for the compiler-CLI use; logging to a structured
          logger is OUT OF SCOPE for this spec. Cached idiom
          `rf-dart-print-to-csharp-console-writeline`.
      (4) String interpolation: Dart `'$x'` / `'${e.message}'` → C#
          `$"{x}"` / `$"{e.Message}"` — cached idiom
          `rf-dart-string-interpolation-to-csharp-string-interpolation`.
      (5) Enum-to-string nuance: Dart `enum E { foo }` has
          `E.foo.toString() == "E.foo"` (api.dart.dev `Enum`);
          C# `enum E { Foo } E.Foo.ToString() == "Foo"` (Microsoft Learn
          "Enum.ToString"). The Dart source uses
          `.toString().split('.').last` to strip the type-name prefix;
          this is REDUNDANT in C# and the conversion DROPS it (recorded
          as new idiom `rf-dart-enum-tostring-prefix-vs-csharp-bare-name`).
      (6) Async/Stream/isolate: the pipeline is synchronous (no
          `async`/`await`/`Future`/`Stream` in the source) — render as
          a synchronous C# method, NO `Task<>` wrapping. Value-vs-
          reference: all intermediate types (`Program`, `Module`,
          `AnnotatedProgram`, `CompilationResult`) are reference types
          on both sides; no defensive copies. Null-safety:
          `options` parameter is nullable; `e.Category` is nullable
          (the `?.` chain in the augmenting throw is faithful — preserve
          as C# `?.` per `rf-dart-objectq-to-csharp-objectq`).
  - construct_key: dart.method.compile_program_alternative_entry_skipping_lex_parse_typecheck_with_named_optional_proc_decls
    source_form: >-
      "BytecodeProgram compileProgram(Program ast, {List<ProcDecl>?
      procDeclarations}) { final analyzer = _createAnalyzer(); final
      annotated = analyzer.analyze(ast, generateReduce: true, compileMode:
      CompileMode.system, procDeclarations: procDeclarations ?? [],
      skipGlobalSRSW: true); final codegen = _createCodegen(); return
      codegen.generateWithMetadata(annotated).program; }" — the alternative
      pipeline entry. Takes a Program AST directly and a NAMED OPTIONAL
      `List<ProcDecl>? procDeclarations` (curly braces — nullable, no
      default). Calls analyzer with `generateReduce: true, compileMode:
      CompileMode.system, skipGlobalSRSW: true`. Used by the project
      linker for statically-linked programs (per the doc comment).
    target_decision: >-
      Emit `public BytecodeProgram CompileProgram(Program ast,
      IReadOnlyList<ProcDecl>? procDeclarations = null) { var analyzer =
      _createAnalyzer(); var annotated = analyzer.Analyze(ast,
      generateReduce: true, compileMode: CompileMode.System,
      procDeclarations: procDeclarations ?? Array.Empty<ProcDecl>(),
      skipGlobalSRSW: true); var codegen = _createCodegen(); return
      codegen.GenerateWithMetadata(annotated).Program; }`. Dart named-
      optional → C# optional positional per
      `rf-dart-named-default-param-to-csharp-optional-arg`. The empty-list
      default `[]` → `Array.Empty<ProcDecl>()` per
      `rf-dart-const-empty-list-default-to-csharp-array-empty` (cached).
      `List<ProcDecl>?` parameter type → `IReadOnlyList<ProcDecl>?` to
      preserve the read-only-input convention adopted across compiler
      specs (the analyzer does not mutate the proc-decl list); if a
      future analyzer change mutates it, the call-site material here
      escalates.
    idiom_id: null
    research_finding_id: rf-dart-const-empty-list-default-to-csharp-array-empty
    nuance: >-
      Empty-list default-value nuance (explicitly addressed). Dart `[]`
      in an expression context allocates a fresh `List<dynamic>` (or
      type-inferred `List<ProcDecl>` here) per invocation; C# has TWO
      options: `new List<ProcDecl>()` (fresh allocation each call) OR
      `Array.Empty<ProcDecl>()` (cached singleton, `IReadOnlyList<ProcDecl>`).
      Cached idiom prefers the latter (`Array.Empty`) for parity with
      cached Dart `const []` semantics — although the Dart source here
      lacks `const` on the literal, the value is never mutated by the
      analyzer (it's only iterated). Mutation-safety: if the analyzer
      were to call `.Add` on the list, the singleton would throw —
      ESCALATION trigger if discovered. The analyzer spec
      (`analyzer.dart.md`) records the proc-decl list as read-only,
      so `Array.Empty<ProcDecl>()` is safe here. Value-vs-reference:
      `List` is a reference type on both sides — no defensive copy.
      Null-safety: parameter is nullable (`List<ProcDecl>?`); the
      coalescing operator unwraps to a non-null value. No async surface.
  - construct_key: dart.docblock_triple_slash
    source_form: >-
      Multiple triple-slash doc comments above class members:
      `/// Compilation options` above `CompileOptions`; `/// Enable type
      checking` above `typeCheck`; `/// Abort compilation on type errors
      (only applies if typeCheck is true)` above `strictTypes`; `/// Main
      GLP compiler` above `GlpCompiler`; `/// Compile GLP source to
      bytecode program` above `compile`; `/// Compile GLP source to
      bytecode program with variable metadata` above `compileWithMetadata`;
      `/// Compile a Program AST directly to bytecode.` (with multi-line
      continuation about the linker, skipped phases, and the
      `[procDeclarations]` square-bracketed parameter reference) above
      `compileProgram`; `/// Generate _select/1 dispatch table from
      exported procedure declarations.` (with trailing empty `///` line)
      above an evidently UNFINISHED method (no body, no signature — the
      file ends here at line 158 with only the doc comment block and the
      closing `}` of the class).
    target_decision: >-
      Map each triple-slash doc comment to a C# `/// <summary>...
      </summary>` block adjacent to the corresponding member per the
      established `rf-dart-docblock-triple-slash-to-csharp-xml-doc` idiom.
      For multi-line doc comments preserve paragraph structure with
      `<para>` elements where appropriate. The `[procDeclarations]`
      square-bracketed Dart reference (Dart's dartdoc convention for
      "see this identifier") maps to `<paramref name="procDeclarations"/>`
      or `<see cref="procDeclarations"/>` per dartdoc-XmlDoc convention
      mapping (cached idiom). The TRAILING orphan doc comment
      (`/// Generate _select/1 dispatch table from exported procedure
      declarations.` plus a blank `///` line, with no method body or
      signature) is an INCOMPLETE source artefact — the file is cut off
      mid-class. The C# spec PRESERVES the doc comment as a class-level
      `<remarks>` note recording the incomplete intent, but DOES NOT
      synthesise a method signature (FR-023: spec-only, no inventive
      code).
    idiom_id: null
    research_finding_id: rf-dart-docblock-triple-slash-to-csharp-xml-doc
    nuance: trivial mechanical mapping for the body; load-bearing nuance
      for the orphan trailing comment (incomplete source).
  - construct_key: dart.line_comment.inline_or_above_pipeline_phase
    source_form: >-
      Multiple `//` line comments throughout `compileWithMetadata`:
      "// Phase 1: Lexical analysis"; "// Note: Main lexer now handles
      type declarations (::= and procedure)"; "// Phase 2: Syntax analysis
      (use parseModule to get module info)"; "// Convert Module to Program
      for analyzer"; "// Phase 2.4: Apply partial evaluation (defined guard
      expansion) BEFORE type checking"; "// This transforms clauses to
      unfold unit clause guards, which affects coverage checking";
      "// Phase 2.5: Type checking (optional)"; "// Use checkModule with
      transformed procedures"; "// This ensures type checking sees the
      expanded guards"; "// Report type errors and warnings"; "// In
      non-strict mode, just print the error and continue"; "// Generate
      reduce/2 for all files except system-mode code (stdlib)"; "// Phase
      3: Semantic analysis (with reduce generation flag and proc
      declarations)"; "// Pass proc declarations for type-based SRSW
      relaxation"; "// Phase 4: Code generation"; "// Rethrow with source
      context"; "// Re-export for users of this module" (above the three
      Dart `export` directives); "// Used by the project linker for
      statically linked programs."; "// Skips lexing, parsing, type
      checking, and _select generation."; "// Linked programs: modules
      already type-checked individually" (on the analyze call's
      `skipGlobalSRSW: true` line).
    target_decision: >-
      Preserve every `//` line comment verbatim adjacent to its anchoring
      statement / expression in the converted C# (line-for-line). Trivial
      mechanical mapping per the project convention
      (`rf-dart-line-comment-preserve-verbatim`, used implicitly across
      all prior compiler/* specs).
    idiom_id: null
    research_finding_id: null
    nuance: trivial
    trivial: true
conversion_units:
  - "using <root>.Compiler; (compose with same-namespace elision per rf-dart-relative-import-to-csharp-using-or-same-namespace; show-clauses dropped per rf-dart-import-show-clause-no-csharp-counterpart)"
  - "using <root>.Analysis.TypeChecker; (cross-subdirectory imports for ProcDecl and checkModule)"
  - "using <root>.Bytecode; (for BytecodeProgram)"
  - "(Dart `export` directives DROP per rf-dart-export-directive-to-csharp-using-alias; consumers re-add the corresponding `using` directly)"
  - "public class CompileOptions (reference class; reject record/struct per rf-dart-final-field-class-to-csharp-getonly-class; const-ctor canonicalisation drops; baseline emits `new CompileOptions()`)"
  - "property: public bool TypeCheck { get; } — non-nullable, get-only, defaults via constructor"
  - "property: public bool StrictTypes { get; } — non-nullable, get-only, defaults via constructor"
  - "constructor: CompileOptions(bool typeCheck = false, bool strictTypes = false) per rf-dart-named-default-param-to-csharp-optional-arg"
  - "public sealed class GlpCompiler (sealed: no subclass in Dart)"
  - "field: private readonly Func<string, Lexer> _createLexer (Func delegate per rf-dart-function-typed-field-to-csharp-func-delegate-field)"
  - "field: private readonly Func<IReadOnlyList<Token>, Parser> _createParser"
  - "field: private readonly Func<Analyzer> _createAnalyzer"
  - "field: private readonly Func<CodeGenerator> _createCodegen"
  - "constructor: GlpCompiler(Func<string, Lexer>? createLexer = null, Func<IReadOnlyList<Token>, Parser>? createParser = null, Func<Analyzer>? createAnalyzer = null, Func<CodeGenerator>? createCodegen = null) — null-coalescing assignment in body"
  - "method: public BytecodeProgram Compile(string source, CompileOptions? options = null) — one-line delegation to CompileWithMetadata"
  - "method: public CompilationResult CompileWithMetadata(string source, CompileOptions? options = null) — six-phase pipeline with try/catch on CompileError augmenting source/phase; inner try/catch in Phase 2.5 with `when (!opts.StrictTypes)` filter for typeCheck failure; preserves Dart rethrow semantics with C# `throw;` form; drops the `.split('.').Last()` enum-prefix-strip (redundant in C# per rf-dart-enum-tostring-prefix-vs-csharp-bare-name)"
  - "method: public BytecodeProgram CompileProgram(Program ast, IReadOnlyList<ProcDecl>? procDeclarations = null) — alternative entry; uses Array.Empty<ProcDecl>() for empty default per rf-dart-const-empty-list-default-to-csharp-array-empty; passes skipGlobalSRSW: true to analyzer"
  - "doc-comments → /// <summary>...</summary> per rf-dart-docblock-triple-slash-to-csharp-xml-doc; orphan trailing comment about `_select/1 dispatch table` preserved as class-level <remarks> (NO inventive method signature — file is truncated mid-class)"
  - "inline // comments preserved verbatim adjacent to each pipeline-phase statement"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-relative-import-to-csharp-using-or-same-namespace — module imports + re-exports (cached idiom, reuse)

- Deep analysis: the file imports SEVEN sibling relative files
  (`lexer.dart`, `parser.dart`, `analyzer.dart`, `codegen.dart`,
  `error.dart`, `token.dart`, `result.dart`), one sibling-relative
  with a `show` allow-list (`ast.dart show Program, Procedure, ...`),
  TWO cross-subdirectory relative imports
  (`../analysis/type_checker/type_ast.dart show ProcDecl` and
  `../analysis/type_checker/type_checker.dart show checkModule`),
  and ONE `package:` import targeting the same package
  (`package:glp_runtime/bytecode/runner.dart show BytecodeProgram`).
  It also emits THREE `export` directives — a re-export of
  `BytecodeProgram`, a re-export of `CompilationResult` from
  `result.dart`, and a SELF-re-export of its own `CompileOptions`
  (this self-re-export is a Dart idiom for "this file is the
  canonical surface, all my consumers need not separately import
  `compiler.dart`-internal types").
- Provenance:
  - `rf-dart-relative-import-to-csharp-using-or-same-namespace`
    cached idiom first recorded in `analyzer.dart.md`
    (sibling-relative imports elide because same-namespace).
  - `rf-dart-import-relative-to-csharp-using-namespace` cached
    idiom from `runtime/heap_fcp.dart.md` and `runtime.dart.md`
    (relative imports to a DIFFERENT namespace → `using
    <root>.<Namespace>;`).
  - `rf-dart-import-show-clause-no-csharp-counterpart` cached
    idiom from `runtime/heap_fcp.dart.md` / `runtime.dart.md` /
    `result.dart.md` (`show` clauses drop — no C# per-symbol
    `using` narrowing).
  - `rf-dart-export-directive-to-csharp-using-alias` cached idiom
    from `runtime/goal_queue.dart.md` (Dart `export` → C#
    using-alias or namespace re-exposure; in practice drops
    because C# consumers `using` the declaring namespace
    directly).
- Authoritative bases were established in the prior specs:
  - Dart official: import directive semantics — relative URI
    resolves against the importing library's URI; `package:` URI
    resolves through `pubspec.yaml`; `show` clause narrows the
    visible identifier set at the importing compilation unit;
    `export` re-exposes a library's symbols through the current
    library's URI (Dart language tour: "Libraries and imports").
  - .NET official: `using <Namespace>;` imports the entire public
    surface of the namespace (Microsoft Learn: "using directive").
    No per-symbol allow-list. No per-file re-export.
- Conclusion: emit the three `using` directives noted in the
  structured block; DROP all `show` clauses; DROP all three Dart
  `export` directives (no C# counterpart needed — namespace
  declarations carry the export surface). Downstream consumers that
  used to import `compiler.dart` to acquire `BytecodeProgram` /
  `CompilationResult` MUST, in C#, separately `using
  <root>.Bytecode;` and `using <root>.Compiler;` — these are
  cross-file expectations, recorded here.

### rf-dart-named-default-param-to-csharp-optional-arg — both `CompileOptions` ctor and `CompileWithMetadata`'s optional `options` (cached idiom, reuse)

- Deep analysis: the file uses BOTH the NAMED-OPTIONAL Dart surface
  (`{this.typeCheck = false, this.strictTypes = false}` on
  `CompileOptions`; `{List<ProcDecl>? procDeclarations}` on
  `compileProgram`) and the POSITIONAL-OPTIONAL Dart surface
  (`[CompileOptions? options]` on `compile` and `compileWithMetadata`).
  Both Dart surfaces collapse to a single C# surface: optional
  positional parameters with defaults.
- Provenance: cached idiom first recorded in `error.dart.md`
  (`rf-dart-named-default-param-to-csharp-optional-arg`); refined
  in `analyzer.dart.md` and `lexer.dart.md`. Authoritative bases:
  - Dart official: named parameters with `{}` and defaults; positional
    optional with `[]` and defaults (Dart language tour: "Functions"
    → "Optional parameters").
  - .NET official: C# optional parameters via `= default` value at
    declaration; call-sites may use named-argument invocation
    (`Foo(name: value)`) for any positional parameter (Microsoft
    Learn: "Named and optional arguments").
- Conclusion: emit C# optional positional parameters; call-sites
  preserve named-argument ergonomics. Cache hit; no escalation.

### rf-dart-final-field-class-to-csharp-getonly-class — `CompileOptions` two-bool data class (cached idiom, reuse)

- Deep analysis: `CompileOptions` has two `final bool` instance
  fields, a `const` named constructor with defaults, no `==`/`hashCode`
  override, no inheritance, no methods. Identical shape to
  `CompilationResult` (covered in `result.dart.md`) and `Token`
  (covered in `token.dart.md`) modulo field count and ctor syntax.
- Provenance: cached idiom first recorded in `token.dart.md`,
  reused in `result.dart.md` and several runtime/* specs.
  Authoritative bases recorded there (Dart `final` field semantics;
  C# get-only auto-properties).
- Why NOT `record`: would synthesise structural equality. Dart source
  has default identity equality (`==` not overridden); record would
  silently change semantics. Same rejection as `result.dart.md`.
- Why NOT `struct`: although the class is small and effectively
  immutable, the `const CompileOptions()` canonical instance pattern
  and the use as a default-parameter value imply reference identity
  is acceptable but value-copy would NOT improve anything; following
  project convention, keep reference class.
- Const-constructor: Dart `const` enables compile-time canonical
  instances (`identical(const Foo(), const Foo()) == true`). C#
  has no exact analogue for reference types; the converted code
  emits a fresh `new CompileOptions()` per call. The performance
  cost is negligible (small object, infrequent allocation in the
  default-options call-site); a `public static readonly
  CompileOptions Default = new();` singleton optimisation is
  recorded but NOT baseline.
- Conclusion: emit `public class CompileOptions` with two get-only
  `bool` auto-properties and a single optional-arg constructor.
  Authoritative both sides; no escalation.

### rf-dart-function-typed-field-to-csharp-func-delegate-field — four injectable factory-callback fields on `GlpCompiler` (NEW idiom)

- Deep analysis: the four fields `_createLexer`, `_createParser`,
  `_createAnalyzer`, `_createCodegen` are typed as Dart inline
  function types (no `typedef`), each carrying a default
  factory-callback lambda assigned in the constructor's initialiser
  list via `??`. The pattern is canonical dependency injection
  via factory callbacks — production callers pass nothing and get
  the default `new <Type>(...)` lambdas; test callers pass stub
  lambdas that return mocks. The fields are leading-underscore
  (library-private).
- Research (this file's first encounter for the construct):
  - Dart official: function-type values are reference handles to
    callable objects; they are structurally-typed (assignment-
    compatible by signature, not by name); inline use without
    `typedef` is idiomatic (api.dart.dev "Functions"; Dart
    language tour: "Functions" → "Function as parameters").
  - .NET official (authoritative): the C# `System.Func<>` and
    `System.Action<>` generic delegates are structurally-typed (by
    convention — generic delegate types with matching arity and type
    arguments are mutually substitutable) reference-type function
    handles (Microsoft Learn: "Delegates" → "Func<TResult>" and
    "Func<T,TResult>"; "Delegates Overview"). `delegate` named
    types are an alternative when nominal naming is wanted; here
    the source has no `typedef`, so `Func<>`/`Action<>` is the
    faithful render.
  - Web corroboration (non-authoritative): the .NET community
    convention is to prefer `Func<>`/`Action<>` for ad-hoc /
    dependency-injection callbacks and reserve named `delegate`
    types for stable public APIs that benefit from nominal
    naming.
- Conclusion (authoritative both sides): map Dart inline
  function-typed fields to C# `Func<,>` / `Func<>` delegate fields.
  Name preserves the leading-underscore-as-`private` convention.
  Coalescing assignment in the constructor maps `??` → `??` 1:1.
  New idiom recorded for downstream specs.

### rf-dart-try-on-catch-to-csharp-typed-catch — outer `on CompileError catch (e)` + inner `try { ... } catch (e) { if (strict) rethrow }` (NEW idiom + cache hit on cousins)

- Deep analysis: the file has TWO try/catch sites. (a) Outer:
  `try { ...pipeline body... } on CompileError catch (e) { throw
  CompileError(e.message, e.line, e.column, source: source,
  phase: e.category?.toString().split('.').last); }` — typed catch
  for `CompileError`, augmenting rethrow with fresh
  `CompileError(...)`. (b) Inner (inside the typeCheck branch):
  `try { ...checkModule... } catch (e) { if (opts.strictTypes)
  rethrow; print('[TYPE CHECK] Failed: $e'); }` — UNTYPED catch
  (catches anything), conditional rethrow on `strictTypes`.
- Research (this file's first encounter for "on X catch" + Dart
  rethrow semantics):
  - Dart official: `on Type catch (e)` filters by exception type
    (api.dart.dev "Errors" → try/catch/on); `catch (e)` (no `on`)
    catches anything assignable to `Object`; `rethrow` re-throws
    the currently-caught exception preserving the stack-trace
    (Dart language tour: "Exceptions" → "Rethrow").
  - .NET official (authoritative): `catch (Type e)` filters by
    exception type (Microsoft Learn: "try-catch"); `throw;` (no
    expression) inside a catch block preserves the original
    stack-trace identical to Dart `rethrow` (Microsoft Learn:
    "throw statement"); `catch (Type e) when (condition)` is the
    C# exception-filter syntax that PREDICATES catch eligibility
    without entering the catch body, preserving the stack-trace
    if the predicate fails (Microsoft Learn: "Exception filters").
- Conclusion: outer try → C# `catch (CompileError e) { throw new
  CompileError(...); }`. Inner try → C# `catch (Exception e) when
  (!opts.StrictTypes) { Console.WriteLine($"[TYPE CHECK] Failed:
  {e}"); }` paired with `catch (Exception) when (opts.StrictTypes)
  { throw; }`. The `when`-filter rendering is the modern idiomatic
  C# form (preferred over `if-rethrow-inside-catch` for
  stack-trace preservation reasons; the older form is
  semantically equivalent here because the rethrow is at the
  start of the catch body, but the `when` form is cleaner).
  Authoritative both sides. New idiom recorded.

### rf-dart-enum-tostring-prefix-vs-csharp-bare-name — `e.category?.toString().split('.').last` (NEW idiom)

- Deep analysis: in the outer catch handler, the augmenting throw
  passes `phase: e.category?.toString().split('.').last` — the
  ErrorCategory enum value is converted to a string and then the
  type-name prefix is stripped. This is Dart-idiomatic because Dart
  `Enum.toString()` returns `"EnumType.value"`.
- Research (this file's first encounter):
  - Dart official: `Enum.toString()` returns the qualified form
    `"EnumName.valueName"` (api.dart.dev `Enum`).
  - .NET official: `Enum.ToString()` returns the unqualified value
    name `"ValueName"` only (Microsoft Learn: `Enum.ToString`
    Method). C# enums also offer `nameof(ErrorCategory.Foo)` and
    `Enum.GetName(typeof(ErrorCategory), value)` as alternatives,
    all returning the bare name.
- Conclusion: the Dart `.split('.').last` post-processing is
  REDUNDANT in C# and DROPS at the conversion boundary. C#
  render is `phase: e.Category?.ToString()`. Authoritative both
  sides; recorded as new idiom for cross-file reuse.

### rf-dart-const-empty-list-default-to-csharp-array-empty — `procDeclarations ?? []` in `compileProgram` (cached idiom, reuse)

- Deep analysis: the null-coalescing default for the optional
  `procDeclarations` parameter is `[]` (a fresh empty list literal).
  The analyzer iterates the list but does not mutate it (per
  `analyzer.dart.md` cross-reference).
- Provenance: cached idiom from `error.dart.md` /
  `runtime/*.dart.md` (`rf-dart-const-empty-list-default-to-csharp-array-empty`).
- Authoritative bases recorded in those specs (Dart empty list
  literal `[]`; C# `Array.Empty<T>()` cached singleton; Microsoft
  Learn `Array.Empty<T>`).
- Conclusion: emit `procDeclarations ?? Array.Empty<ProcDecl>()`.
  Mutation-safety pre-condition: the analyzer must not mutate the
  list; the analyzer.dart.md spec confirms this.

### rf-dart-print-to-csharp-console-writeline — `print('[TYPE ERROR] ...')` / `print('[TYPE WARNING] ...')` / `print('[TYPE CHECK] Failed: $e')` (cached idiom, reuse)

- Deep analysis: three `print` call-sites in the typeCheck branch
  write diagnostic strings with bracketed prefixes (`[TYPE ERROR]`,
  `[TYPE WARNING]`, `[TYPE CHECK]`) followed by interpolated
  message and line/column.
- Provenance: cached idiom first recorded in
  `runtime/scheduler.dart.md` and reused across runtime/*; in
  compiler/* this is the first call-site but the cached idiom
  applies verbatim.
- Authoritative bases recorded there (Dart `print` to stdout;
  C# `Console.WriteLine` to `Console.Out`).
- Conclusion: emit `Console.WriteLine($"[TYPE ERROR] {error.Message}
  at line {error.Line}");` etc., paired with
  `rf-dart-string-interpolation-to-csharp-string-interpolation`
  for the interpolation form.

### rf-dart-string-interpolation-to-csharp-string-interpolation — all three diagnostic strings (cached idiom, reuse)

- Deep analysis: all three `print` strings use Dart `${...}`
  interpolation: `'[TYPE ERROR] ${error.message} at line
  ${error.line}'`, `'[TYPE WARNING] ${warning.message} at line
  ${warning.line}'`, `'[TYPE CHECK] Failed: $e'`. Also implicit
  inside the `throw` message: `'Type checking failed with
  ${typeResult.errors.length} error(s)'`.
- Provenance: cached idiom first recorded in `error.dart.md`,
  reused across runtime/* and compiler/* specs.
- Authoritative bases recorded there (Dart `$variable` / `${expr}`
  interpolation; C# `$"{expr}"` interpolated strings).
- Conclusion: emit `$"[TYPE ERROR] {error.Message} at line
  {error.Line}"` etc. PascalCase property access (`Message`,
  `Line`) per the property-naming convention.

### rf-dart-typed-ctor-call-to-csharp-new — `Lexer(source)`, `Parser(tokens)`, `Analyzer()`, `CodeGenerator()`, `Program(...)`, `PartialEvaluator()`, `CompileError(...)` (cached idiom, reuse)

- Deep analysis: the file calls seven Dart unqualified constructors
  (Dart allows omitting `new`). All map to C# `new <Type>(...)`.
- Provenance: cached idiom from `runtime/runtime.dart.md`.
- Conclusion: prepend `new` at every constructor call-site —
  mechanical. Authoritative both sides.

### rf-dart-docblock-triple-slash-to-csharp-xml-doc — multiple `///` doc comments + the orphan trailing comment (cached idiom, reuse)

- Deep analysis: eight `///` doc comments cover the class, the two
  config fields, three methods, and one trailing-orphan comment.
  The orphan trailing comment (`/// Generate _select/1 dispatch
  table from exported procedure declarations.` followed by a blank
  `///` line and the class-closing `}`) is a SOURCE-INCOMPLETE
  artefact: the file ends with the comment block but no method
  signature or body. The Dart source is TRUNCATED mid-class.
- Provenance: cached idiom from every prior compiler/* spec.
- Conclusion: emit `/// <summary>...</summary>` blocks for each
  doc comment; preserve `<para>` paragraph structure where the
  Dart source spans multiple lines; map `[procDeclarations]`
  references to `<paramref name="procDeclarations"/>`. The
  ORPHAN trailing comment is preserved as a class-level
  `<remarks>` note documenting the unfinished intent (FR-023:
  spec-only — no inventive method signature). The C# converted
  class CLOSES with the unfinished comment recorded but NO
  invented `_select/1` method body — that synthesis would
  violate FR-023.

## Cross-file consequences recorded (not blocking THIS file)

1. **Augmenting throw and stack-trace preservation.** The outer
   `throw new CompileError(...)` requires the converted
   `CompileError` class to accept an `innerException` parameter (the
   Dart source does not pass one — Dart errors don't have C#'s
   `InnerException` chain — but the C# rendering MUST pass `e` as the
   inner to preserve diagnostic context). Recorded as cross-file
   expectation on `error.cs`. The `error.dart.md` spec already
   records `CompileError`'s constructor surface; the C# emit must
   honour the inner-exception channel.
2. **`Module → Program` constructor.** The phase-2 wrap depends on
   the `Program(procedures, line, column)` constructor signature
   established in `ast.dart.md`. Authoritative — no escalation.
3. **`Analyzer.analyze` named-argument surface.** The Phase-3 call
   site uses three named arguments (`generateReduce`,
   `procDeclarations`, `compileMode`) and the `compileProgram`
   call-site adds a fourth (`skipGlobalSRSW`). The
   `analyzer.dart.md` spec records these as named arguments → C#
   optional positional with names; call-site shape preserves.
4. **`CompilationResult.program` casing.** PascalCase to
   `.Program` per `rf-dart-final-field-class-to-csharp-getonly-class`
   (recorded in `result.dart.md`).
5. **`Module.compileMode` enum value `CompileMode.system`.**
   PascalCase → `CompileMode.System` per the enum-casing convention.
   The enum is defined in `ast.dart`; the `ast.dart.md` spec
   records the value-name casing.
6. **Truncated source.** The file ends at line 158 with an
   incomplete method (only the doc comment, no signature/body).
   The conversion preserves this as documented absence — NO
   inventive `_select` method is synthesised. A future re-discovery
   pass on a complete `compiler.dart` would add the method
   converstion; THIS spec is faithful to the current 158-line
   source.

## Idiom decisions summary (FR-012 / SC-007)

Cache reuses (FR-012 idiom-first):
- `rf-dart-relative-import-to-csharp-using-or-same-namespace`
- `rf-dart-import-relative-to-csharp-using-namespace`
- `rf-dart-import-show-clause-no-csharp-counterpart`
- `rf-dart-export-directive-to-csharp-using-alias`
- `rf-dart-named-default-param-to-csharp-optional-arg`
- `rf-dart-final-field-class-to-csharp-getonly-class`
- `rf-dart-const-empty-list-default-to-csharp-array-empty`
- `rf-dart-print-to-csharp-console-writeline`
- `rf-dart-string-interpolation-to-csharp-string-interpolation`
- `rf-dart-typed-ctor-call-to-csharp-new`
- `rf-dart-docblock-triple-slash-to-csharp-xml-doc`
- `rf-dart-leading-underscore-privacy-to-csharp-private`
- `rf-dart-method-naming-to-csharp-pascalcase`
- `rf-dart-objectq-to-csharp-objectq`
- `rf-dart-nullsafety-to-csharp-nrt`

New idioms recorded (will be re-used in downstream compiler/* and
multi-agent specs):
- `rf-dart-function-typed-field-to-csharp-func-delegate-field` —
  Dart inline function-type field → C# `Func<,>` / `Func<>`
  delegate field (authoritative both sides; preferred over named
  `delegate` for ad-hoc / dependency-injection use).
- `rf-dart-try-on-catch-to-csharp-typed-catch` — Dart `on Type catch
  (e)` → C# `catch (Type e)`; Dart `rethrow` → C# `throw;`
  (no-expression form inside a catch block); conditional rethrow →
  `catch (T) when (cond) { throw; }` filter form.
- `rf-dart-enum-tostring-prefix-vs-csharp-bare-name` — Dart
  `EnumValue.toString()` returns `"EnumType.value"` (qualified) →
  C# `EnumValue.ToString()` returns `"value"` (unqualified); the
  Dart-idiomatic `.split('.').last` post-processing DROPS in C#.

## Notes

- No `Stream`/`Future`/`async`/`await` in this file — the pipeline
  is synchronous and renders as a synchronous C# method. No
  `Task<>` wrapping. The well-known async nuance is ABSENT and
  deliberately not asserted.
- No isolates, no `late`, no `mixin`, no `extension`, no
  generics-with-bounds (the four `Func<>` fields are concrete
  delegates), no `sealed`/`abstract` classes (though `sealed` is
  applied to the C# class as a convention to lock the
  no-subclass posture).
- No bitwise/shift, no `==`/`hashCode` override on either
  `CompileOptions` or `GlpCompiler`, no `toString` override.
- No nullable INSTANCE fields on `GlpCompiler` (the four
  delegates are non-nullable post-construction). The four
  constructor parameters ARE nullable (`Func<>?`).
- Every escalation surface is satisfied — every non-trivial
  construct has BOTH a deep-analysis basis (rationale section)
  AND a research/idiom basis (cached or new). The orphan
  trailing doc comment is the one notable feature: the file is
  truncated mid-class, and the spec preserves that truncation as
  a documented absence rather than synthesising the missing
  method (FR-023).
- `open_escalation_count = 0`: every construct has an
  authoritatively-supported decision basis.
