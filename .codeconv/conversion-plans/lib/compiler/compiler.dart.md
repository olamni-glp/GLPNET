---
path: lib/compiler/compiler.dart
cycle_group_id: 43
scc_siblings: []
generated_at: 2026-05-21T16:35:07Z
source_sha256: 1b65ae574b5c4d866bf91680efdd48fd3a59072b31f44da7f2a3e19cd6ddc310
schema_version: 1
---

# Conversion Plan: lib/compiler/compiler.dart

## 1. Source Analysis

The file is the GLP **compiler facade** — a 158-line top-level entry that
threads source text through the six-phase pipeline (lex → parse → wrap →
partial-evaluate defined guards → optionally type-check → analyse →
codegen). The file is intentionally thin: every phase is delegated to a
factory-callback-injected collaborator, and the facade itself owns only
the pipeline shape, the typecheck-branch error/warning surface, and the
augmenting outer catch.

Top-level structure:

- **Nine imports** — seven sibling-relative (`lexer.dart`, `parser.dart`,
  `analyzer.dart`, `codegen.dart`, `partial_evaluator.dart`, `error.dart`,
  `token.dart`, `result.dart`), one sibling-relative with `show` allow-list
  (`ast.dart show Program, Procedure, Clause, Atom, Goal, Guard, Term,
  VarTerm, StructTerm, UnderscoreTerm, CompileMode`), two
  cross-subdirectory relative (`../analysis/type_checker/type_ast.dart show
  ProcDecl`, `../analysis/type_checker/type_checker.dart show checkModule`),
  and one `package:`-prefixed (`package:glp_runtime/bytecode/runner.dart
  show BytecodeProgram`) — the latter targets the SAME Dart package the
  file lives in. Note: `partial_evaluator.dart` was added in commit
  `213e5601` (escalation #3 close, Option-b) so the lenient `PartialEvaluator`
  resolves to `partial_evaluator.dart`, NOT the analyzer-internal strict
  `DefinedGuardEvaluator` (the name was renamed in the same commit).
- **Three exports** — `BytecodeProgram` (from `package:glp_runtime/bytecode/
  runner.dart`), `CompilationResult` (from `result.dart`), and a
  SELF-re-export `export 'compiler.dart' show CompileOptions;`.
- **`class CompileOptions`** — two-bool data class with `const` named
  constructor, defaults both `false`, library-public, no `==`/`hashCode`,
  no methods, no inheritance.
- **`class GlpCompiler`** — the facade. Four library-private factory-
  callback fields (typed as inline Dart function types — no `typedef`),
  initialised in the constructor's initialiser list via `??` against
  default lambdas that call the canonical `new`-constructors. Three
  public methods:
  - `compile(source, [options])` — one-line delegation to
    `compileWithMetadata`, returns `.program`.
  - `compileWithMetadata(source, [options])` — the pipeline body, wrapped
    in a single outer `try { ... } on CompileError catch (e) { throw
    CompileError(...augmented with source/phase...); }`. Inside, six
    phases run in order (Phase 1 lex, Phase 2 parse, Module→Program
    wrap, Phase 2.4 partial evaluation, Phase 2.5 OPTIONAL type-check
    inside a nested try/catch with conditional rethrow, Phase 3 analyse
    with `generateReduce: module.compileMode != CompileMode.system`,
    Phase 4 codegen).
  - `compileProgram(ast, {procDeclarations})` — alternative entry that
    takes a pre-parsed `Program`, runs only Phase 3 + Phase 4 with
    `generateReduce: true, compileMode: CompileMode.system,
    skipGlobalSRSW: true`. Used by the project linker.
- **Orphan trailing doc comment** — `/// Generate _select/1 dispatch table
  from exported procedure declarations.\n  ///\n}` — the file is
  TRUNCATED mid-class. No method signature or body follows the doc
  comment; the class-closing `}` is on the next line. Source-faithful
  conversion preserves this absence (FR-023: no inventive synthesis).

Concurrency: PURELY synchronous; no `async`/`await`/`Future`/`Stream`/
`Completer`/`Isolate`; no `dart:io`/`dart:ffi`/`dart:isolate`. No mixins,
extensions, records, operator overloading, extension types, or FFI.

## 2. Dart → C#/.NET Conversion Plan

Each construct below mirrors the convspec one-for-one (FR-024 cache
discipline — no re-derivation).

### 2.1 Module / imports / exports

- Place every type emitted from this file in namespace
  `Glp.Runtime.Compiler` (same namespace as `lexer.cs`, `parser.cs`,
  `analyzer.cs`, `codegen.cs`, `partial_evaluator.cs`, `error.cs`,
  `token.cs`, `result.cs`, `ast.cs` siblings) → all eight sibling-relative
  Dart imports COLLAPSE to ZERO C# `using` directives (`rf-dart-relative-
  import-to-csharp-using-or-same-namespace`).
- Cross-subdirectory imports → `using Glp.Runtime.Analysis.TypeChecker;`
  for both `ProcDecl` (from `type_ast.cs`) and `CheckModule` (from
  `type_checker.cs`) — one `using` covers both per
  `rf-dart-import-relative-to-csharp-using-namespace`.
- `package:`-prefixed import → `using Glp.Runtime.Bytecode;` for
  `BytecodeProgram` (same package, different namespace) per
  `rf-dart-import-relative-to-csharp-using-namespace`.
- All `show` allow-list clauses DROP per
  `rf-dart-import-show-clause-no-csharp-counterpart` — C# `using` has no
  per-symbol narrowing; the post-conversion file accesses the FULL
  public surface of each imported namespace.
- All three Dart `export` directives DROP per
  `rf-dart-export-directive-to-csharp-using-alias`. C# has no per-file
  re-export — downstream consumers that imported `compiler.dart` *because*
  it re-exported `BytecodeProgram` / `CompilationResult` MUST, in C#,
  separately `using Glp.Runtime.Bytecode;` and `using Glp.Runtime.
  Compiler;` (cross-file expectation, recorded here, not blocking).
- Self-re-export `export 'compiler.dart' show CompileOptions;` is
  vestigial in C# and silently drops.

### 2.2 `public class CompileOptions` — two-bool data class

```
public class CompileOptions
{
    public bool TypeCheck { get; }
    public bool StrictTypes { get; }

    public CompileOptions(bool typeCheck = false, bool strictTypes = false)
    {
        TypeCheck = typeCheck;
        StrictTypes = strictTypes;
    }
}
```

- `public class` (NOT `record`, NOT `struct`) per
  `rf-dart-final-field-class-to-csharp-getonly-class`. Rejection rationale:
  - `record` would synthesise structural `==`; Dart source has default
    identity `==` (not overridden) — silent semantic change rejected.
  - `struct` would change reference/value semantics — Dart source uses
    the class via reference semantics throughout; project convention
    keeps reference class for consistency with `CompilationResult` /
    `Token`.
- Two get-only auto-properties `TypeCheck` / `StrictTypes`, both non-
  nullable `bool` under enabled NRT.
- Single constructor with two OPTIONAL POSITIONAL `bool` parameters
  defaulting to `false` per `rf-dart-named-default-param-to-csharp-
  optional-arg` (Dart NAMED-optional → C# optional-positional; call-sites
  may use named-argument invocation: `new CompileOptions(typeCheck: true)`).
- Dart `const` constructor canonicalisation: NO C# analogue for reference
  types. Baseline emits a fresh `new CompileOptions()` per call. The
  single Dart-source `const CompileOptions()` call-site (in
  `compileWithMetadata`'s `options ?? const CompileOptions()`) lowers to
  `options ?? new CompileOptions()` (`rf-dart-typed-ctor-call-to-csharp-new`).
  Performance is negligible (small object, infrequent allocation);
  optional `public static readonly CompileOptions Default = new();` is
  documented but NOT baseline.

### 2.3 `public sealed class GlpCompiler` — facade skeleton

```
public sealed class GlpCompiler
{
    private readonly Func<string, Lexer> _createLexer;
    private readonly Func<IReadOnlyList<Token>, Parser> _createParser;
    private readonly Func<Analyzer> _createAnalyzer;
    private readonly Func<CodeGenerator> _createCodegen;

    public GlpCompiler(
        Func<string, Lexer>? createLexer = null,
        Func<IReadOnlyList<Token>, Parser>? createParser = null,
        Func<Analyzer>? createAnalyzer = null,
        Func<CodeGenerator>? createCodegen = null)
    {
        _createLexer    = createLexer    ?? (source => new Lexer(source));
        _createParser   = createParser   ?? (tokens => new Parser(tokens));
        _createAnalyzer = createAnalyzer ?? (() => new Analyzer());
        _createCodegen  = createCodegen  ?? (() => new CodeGenerator());
    }

    // public methods: Compile, CompileWithMetadata, CompileProgram (§§2.4–2.6)
}
```

- `sealed` per the convspec: no Dart subclass in source; lock the
  no-subclass posture at the C# level.
- Four `private readonly Func<>` delegate fields per the new idiom
  `rf-dart-function-typed-field-to-csharp-func-delegate-field`. Authoritative
  both sides: Dart inline function-type values are structural reference
  handles (api.dart.dev "Functions"); C# `System.Func<>` is the
  structurally-typed-by-convention generic delegate (Microsoft Learn:
  "Delegates" → "Func<TResult>" / "Func<T,TResult>"). Reject named C#
  `delegate` — Dart source has no `typedef`; `Func<>` is the faithful
  render.
- Fields are NON-nullable post-construction (the coalescing assignment
  guarantees non-null); the C# compiler infers field-init non-nullability
  from the constructor body. Constructor parameters ARE nullable per
  Dart source (`Lexer Function(String)?`).
- Field naming preserves Dart's `_camelCase` (leading underscore) per
  `rf-dart-leading-underscore-privacy-to-csharp-private` (private member
  with leading-underscore convention preserved verbatim for tight Dart
  mapping; alternative project-wide PascalCase strip is recorded in the
  convspec rationale but NOT applied here per the convspec's literal
  `_createLexer` field naming).
- `Parser` constructor argument: Dart `List<Token>` → C#
  `IReadOnlyList<Token>` per the read-only-parameter convention adopted
  across compiler specs (the parser does not mutate the token list at
  the facade boundary; a future `parser.dart` spec may relax if internal
  mutation is discovered — escalation trigger).
- Default factory lambdas: `?? (arg => new T(arg))` faithfully maps
  Dart's `?? ((arg) => T(arg))` (`rf-dart-typed-ctor-call-to-csharp-new`
  prepends `new`).

### 2.4 `public BytecodeProgram Compile(string source, CompileOptions? options = null)`

```
public BytecodeProgram Compile(string source, CompileOptions? options = null)
{
    var result = CompileWithMetadata(source, options);
    return result.Program;
}
```

- One-line delegation to `CompileWithMetadata`, returns `.Program`.
- Dart POSITIONAL-OPTIONAL `[CompileOptions? options]` → C# optional-
  positional `CompileOptions? options = null` per `rf-dart-named-default-
  param-to-csharp-optional-arg` (same idiom covers both Dart surfaces).
- `CompilationResult.program` → `CompilationResult.Program` PascalCase
  property access per `rf-dart-final-field-class-to-csharp-getonly-class`.

### 2.5 `public CompilationResult CompileWithMetadata(string source, CompileOptions? options = null)` — six-phase pipeline

```
public CompilationResult CompileWithMetadata(string source, CompileOptions? options = null)
{
    var opts = options ?? new CompileOptions();
    try
    {
        // Phase 1: Lexical analysis
        // Note: Main lexer now handles type declarations (::= and procedure)
        var lexer = _createLexer(source);
        var tokens = lexer.Tokenize();

        // Phase 2: Syntax analysis (use parseModule to get module info)
        var parser = _createParser(tokens);
        var module = parser.ParseModule();

        // Convert Module to Program for analyzer
        var ast = new Program(module.Procedures, module.Line, module.Column);

        // Phase 2.4: Apply partial evaluation (defined guard expansion) BEFORE type checking
        // This transforms clauses to unfold unit clause guards, which affects coverage checking
        var partialEvaluator = new PartialEvaluator();
        var transformedAst = partialEvaluator.TransformDefinedGuards(ast);

        // Phase 2.5: Type checking (optional)
        if (opts.TypeCheck)
        {
            try
            {
                // Use checkModule with transformed procedures
                // This ensures type checking sees the expanded guards
                var typeResult = CheckModule(module, transformedProcedures: transformedAst.Procedures);

                // Report type errors and warnings
                if (typeResult.Errors.Count > 0)
                {
                    foreach (var error in typeResult.Errors)
                        Console.WriteLine($"[TYPE ERROR] {error.Message} at line {error.Line}");
                    if (opts.StrictTypes)
                        throw new CompileError(
                            $"Type checking failed with {typeResult.Errors.Count} error(s)",
                            typeResult.Errors[0].Line,
                            typeResult.Errors[0].Column);
                }

                if (typeResult.Warnings.Count > 0)
                {
                    foreach (var warning in typeResult.Warnings)
                        Console.WriteLine($"[TYPE WARNING] {warning.Message} at line {warning.Line}");
                }
            }
            catch (Exception) when (opts.StrictTypes)
            {
                throw;
            }
            catch (Exception e) when (!opts.StrictTypes)
            {
                // In non-strict mode, just print the error and continue
                Console.WriteLine($"[TYPE CHECK] Failed: {e}");
            }
        }

        // Generate reduce/2 for all files except system-mode code (stdlib)
        var generateReduce = module.CompileMode != CompileMode.System;

        // Phase 3: Semantic analysis (with reduce generation flag and proc declarations)
        // Pass proc declarations for type-based SRSW relaxation
        var analyzer = _createAnalyzer();
        var annotatedAst = analyzer.Analyze(
            ast,
            generateReduce: generateReduce,
            procDeclarations: module.ProcDeclarations,
            compileMode: module.CompileMode);

        // Phase 4: Code generation
        var codegen = _createCodegen();
        var result = codegen.GenerateWithMetadata(annotatedAst);

        return result;
    }
    catch (CompileError e)
    {
        // Rethrow with source context
        throw new CompileError(
            e.Message, e.Line, e.Column,
            source: source,
            phase: e.Category?.ToString(),
            innerException: e);
    }
}
```

- **Outer try/on-catch → C# `catch (CompileError e)`** per the new idiom
  `rf-dart-try-on-catch-to-csharp-typed-catch` (authoritative both sides;
  Microsoft Learn "try-catch").
- **Augmenting rethrow** — Dart `throw CompileError(...)` (fresh
  instance, NOT `rethrow`) maps to `throw new CompileError(...)`. C#
  loses the original stack-trace on `throw new`; we PASS `e` as
  `innerException` to preserve diagnostic context (cross-file expectation
  on `error.cs`; the convspec rationale records this as the load-bearing
  cross-file commitment).
- **Enum-prefix-strip dropped** — Dart `e.category?.toString().split('.').
  last` strips the `"EnumType."` prefix because Dart `Enum.toString()`
  returns the qualified form `"EnumType.value"`. C# `Enum.ToString()`
  returns the bare unqualified value name (Microsoft Learn:
  `Enum.ToString`); the `.split('.').last` post-processing is REDUNDANT
  and DROPS per the new idiom `rf-dart-enum-tostring-prefix-vs-csharp-
  bare-name`. C# render: `phase: e.Category?.ToString()`.
- **Inner try/catch with conditional rethrow** — Dart's
  `try { ... } catch (e) { if (opts.strictTypes) rethrow; print('...'); }`
  is rendered with C# `when`-filtered catch clauses (Microsoft Learn:
  "Exception filters"). The strict-rethrow case uses `throw;` (no
  expression) which preserves the original stack-trace identically to
  Dart `rethrow` (Microsoft Learn: "throw statement"). The order is
  `catch (Exception) when (opts.StrictTypes) { throw; }` FIRST, then
  `catch (Exception e) when (!opts.StrictTypes) { Console.WriteLine(...); }`
  — catch ordering matters in C#; the predicate-filtered clauses are
  evaluated top-down. (Note: the original Dart had the `if` test FIRST
  with rethrow on true; the `when`-filter rendering preserves observable
  semantics — exactly one branch fires per exception.)
- **Phase 1 lex** — factory invocation `_createLexer(source)` uses
  C# delegate direct-invocation sugar (Microsoft Learn: "Delegates").
  `lexer.tokenize()` → `lexer.Tokenize()` per `rf-dart-method-naming-to-
  csharp-pascalcase`.
- **Phase 2 parse** — `_createParser(tokens).ParseModule()`. Token list
  flows through as `IReadOnlyList<Token>` (matches the field type in §2.3).
- **Module→Program wrap** — `new Program(module.Procedures, module.Line,
  module.Column)` per `rf-dart-typed-ctor-call-to-csharp-new`. The
  three-arg constructor signature is established in `ast.dart.md`
  (cross-file dependency; authoritative).
- **Phase 2.4 partial-eval** — `new PartialEvaluator().TransformDefinedGuards(
  ast)`. The `PartialEvaluator` type lives in `partial_evaluator.cs`
  (lenient variant; same namespace; no `using` needed).
- **Phase 2.5 typecheck** — `CheckModule(module, transformedProcedures:
  transformedAst.Procedures)`. The `transformedProcedures:` named-argument
  invocation preserves Dart call-site ergonomics (C# supports named-
  argument invocation on positional parameters; Microsoft Learn "Named
  and optional arguments"). Static `CheckModule` call resolves through
  `using Glp.Runtime.Analysis.TypeChecker;` (an alternative is `using
  static Glp.Runtime.Analysis.TypeChecker.TypeChecker;` if `CheckModule`
  is declared as a static member of a `TypeChecker` class — the
  `type_checker.dart.md` spec records the declaring shape; either
  resolution preserves the call-site syntax `CheckModule(...)`).
- **Diagnostic logging** — three `print` call-sites + the implicit
  message in `throw CompileError(...)`:
  - `print('[TYPE ERROR] ${error.message} at line ${error.line}')` →
    `Console.WriteLine($"[TYPE ERROR] {error.Message} at line {error.Line}")`.
  - `print('[TYPE WARNING] ${warning.message} at line ${warning.line}')`
    → `Console.WriteLine($"[TYPE WARNING] {warning.Message} at line
    {warning.Line}")`.
  - `print('[TYPE CHECK] Failed: $e')` →
    `Console.WriteLine($"[TYPE CHECK] Failed: {e}")`.
  - `'Type checking failed with ${typeResult.errors.length} error(s)'`
    → `$"Type checking failed with {typeResult.Errors.Count} error(s)"`.
  All four use `rf-dart-print-to-csharp-console-writeline` (cached) and
  `rf-dart-string-interpolation-to-csharp-string-interpolation` (cached).
  `.length` → `.Count` is part of the `rf-dart-list-to-csharp-list-of-T`
  family (cached); `typeResult.Errors` is a `List<>`/`IReadOnlyList<>`.
- **Phase 3 analyse** — `analyzer.Analyze(ast, generateReduce:
  generateReduce, procDeclarations: module.ProcDeclarations, compileMode:
  module.CompileMode)`. Three named-argument call-sites preserved per
  C# named-argument-invocation support.
- **`CompileMode.system` → `CompileMode.System`** — PascalCase enum-value
  rename per the enum-casing convention (recorded in `ast.dart.md`).
- **Phase 4 codegen** — `codegen.GenerateWithMetadata(annotatedAst)`.
- **Null-conditional `?.` preserved** — the augmenting throw's
  `e.Category?.ToString()` preserves the Dart `?.` per `rf-dart-objectq-
  to-csharp-objectq` (cached).
- **String type** — Dart `String` → C# `string` (`rf-dart-nullsafety-to-
  csharp-nrt` covers the nullability surface; value-semantics-by-
  equality, reference-semantics-by-storage on both sides).

### 2.6 `public BytecodeProgram CompileProgram(Program ast, IReadOnlyList<ProcDecl>? procDeclarations = null)` — alternative entry

```
public BytecodeProgram CompileProgram(Program ast, IReadOnlyList<ProcDecl>? procDeclarations = null)
{
    var analyzer = _createAnalyzer();
    var annotated = analyzer.Analyze(
        ast,
        generateReduce: true,
        compileMode: CompileMode.System,
        procDeclarations: procDeclarations ?? Array.Empty<ProcDecl>(),
        skipGlobalSRSW: true);

    var codegen = _createCodegen();
    return codegen.GenerateWithMetadata(annotated).Program;
}
```

- Dart NAMED-OPTIONAL `{List<ProcDecl>? procDeclarations}` → C# optional-
  positional `IReadOnlyList<ProcDecl>? procDeclarations = null` per
  `rf-dart-named-default-param-to-csharp-optional-arg`.
- Empty-list default `procDeclarations ?? []` → `procDeclarations ??
  Array.Empty<ProcDecl>()` per `rf-dart-const-empty-list-default-to-
  csharp-array-empty` (cached idiom). Mutation-safety pre-condition: the
  analyzer must not mutate the list — `analyzer.dart.md` confirms the
  proc-decl list is read-only at the analyzer boundary; safe.
- Four named-argument invocations preserved (`generateReduce`,
  `compileMode`, `procDeclarations`, `skipGlobalSRSW`).
- `CompileMode.system` → `CompileMode.System` (enum-casing).
- Chain access `codegen.GenerateWithMetadata(annotated).Program` is
  identical to the Dart `.program` access (member-access chaining is
  load-bearing-shape-equivalent in both languages).

### 2.7 Doc comments — eight `///` blocks plus the truncated orphan

Map each Dart `///` doc comment to a C# `/// <summary>...</summary>`
block adjacent to the corresponding member per
`rf-dart-docblock-triple-slash-to-csharp-xml-doc` (cached). Specifically:

- `/// Compilation options` → `/// <summary>Compilation options.</summary>`
  above `CompileOptions`.
- `/// Enable type checking` → above `TypeCheck` property.
- `/// Abort compilation on type errors (only applies if typeCheck is
  true)` → above `StrictTypes` property; `typeCheck` reference rendered
  as `<see cref="TypeCheck"/>` where the dartdoc convention permits.
- `/// Main GLP compiler` → above `GlpCompiler`.
- `/// Compile GLP source to bytecode program` → above `Compile`.
- `/// Compile GLP source to bytecode program with variable metadata` →
  above `CompileWithMetadata`.
- Multi-line block above `CompileProgram` (paragraph 1 "Compile a
  Program AST directly to bytecode", paragraph 2 "Used by the project
  linker for statically linked programs", paragraph 3 "Skips lexing,
  parsing, type checking, and _select generation", paragraph 4 with the
  `[procDeclarations]` reference) → multi-line `<summary>` with `<para>`
  paragraphs; `[procDeclarations]` → `<paramref name="procDeclarations"/>`
  per the dartdoc-XmlDoc convention.
- **Orphan trailing comment** — `/// Generate _select/1 dispatch table
  from exported procedure declarations.\n  ///\n` followed immediately by
  the class-closing `}` at line 159 — the file is TRUNCATED mid-class.
  Convert to a class-level `<remarks>` note (or trailing `<summary>` on
  an explicit `// TODO: incomplete in source — _select/1 dispatch table`
  comment) documenting the unfinished intent. **DO NOT synthesise a
  method signature or body** (FR-023: spec-only, no inventive code).

### 2.8 Line comments — preserved verbatim

All `//` line comments inside `compileWithMetadata` and `compileProgram`
preserve verbatim adjacent to their anchoring statements per the project
convention `rf-dart-line-comment-preserve-verbatim` (trivial, implicit
across all prior compiler/* specs). Specifically (from the source):
"// Phase 1: Lexical analysis"; "// Note: Main lexer now handles type
declarations (::= and procedure)"; "// Phase 2: Syntax analysis (use
parseModule to get module info)"; "// Convert Module to Program for
analyzer"; "// Phase 2.4: Apply partial evaluation (defined guard
expansion) BEFORE type checking"; "// This transforms clauses to unfold
unit clause guards, which affects coverage checking"; "// Phase 2.5:
Type checking (optional)"; "// Use checkModule with transformed
procedures"; "// This ensures type checking sees the expanded guards";
"// Report type errors and warnings"; "// In non-strict mode, just
print the error and continue"; "// Generate reduce/2 for all files
except system-mode code (stdlib)"; "// Phase 3: Semantic analysis (with
reduce generation flag and proc declarations)"; "// Pass proc
declarations for type-based SRSW relaxation"; "// Phase 4: Code
generation"; "// Rethrow with source context"; "// Re-export for users
of this module" (above the three Dart `export` directives — DROPS because
the exports drop); "// Used by the project linker for statically linked
programs."; "// Skips lexing, parsing, type checking, and _select
generation."; "// Linked programs: modules already type-checked
individually" (on the analyze call's `skipGlobalSRSW: true` line).

### 2.9 Cross-cutting policies

- **Naming**: Dart `_camelCase` private fields preserve the underscore
  prefix verbatim (`_createLexer`, etc.) per the convspec's literal
  field-naming decision. Dart `camelCase` public methods → C# PascalCase
  (`Compile`, `CompileWithMetadata`, `CompileProgram`) per
  `rf-dart-method-naming-to-csharp-pascalcase`. Property access uses
  PascalCase (`.Program`, `.Procedures`, `.Line`, `.Column`,
  `.CompileMode`, `.ProcDeclarations`, `.Errors`, `.Warnings`, `.Message`,
  `.Category`, `.Count`).
- **Nullability**: `?` suffix on `CompileOptions?`, `Func<...>?`,
  `IReadOnlyList<ProcDecl>?` under `<Nullable>enable</Nullable>` per
  `rf-dart-nullsafety-to-csharp-nrt`.
- **Exception hierarchy**: `CompileError` derives from `System.Exception`
  per `rf-dart-implements-exception-to-csharp-derive-system-exception`
  (cached, `error.dart.md`). The augmenting-throw constructor receives
  `source:` and `phase:` as named optional parameters plus
  `innerException:` (the latter is the C# rendering's stack-trace-
  preserving extension; the `error.dart.md` spec records the constructor
  surface).
- **Synchronous pipeline**: NO `Task<>` wrapping. All methods render as
  synchronous C# methods. No `async`/`await` introduced.
- **Value/reference**: all intermediate types (`Program`, `Module`,
  `AnnotatedProgram`, `CompilationResult`) are reference types on both
  sides; no defensive copies.

## 3. Decomposed Task Units

- T1 — emit `namespace Glp.Runtime.Compiler` declaration + `using
  Glp.Runtime.Analysis.TypeChecker;` + `using Glp.Runtime.Bytecode;` +
  `using System;` (for `Func<>`, `Array.Empty`, `Console.WriteLine`,
  `Exception`) + `using System.Collections.Generic;` (for
  `IReadOnlyList<>`).
- T2 — emit `public class CompileOptions` with two get-only `bool`
  auto-properties and a single optional-arg constructor.
- T3 — emit `public sealed class GlpCompiler` skeleton with four
  `private readonly Func<>` delegate fields.
- T4 — emit the `GlpCompiler` constructor with four nullable `Func<>?`
  optional parameters and null-coalescing default-lambda assignments in
  the body.
- T5 — emit `public BytecodeProgram Compile(string source,
  CompileOptions? options = null)` one-line delegation method.
- T6 — emit `public CompilationResult CompileWithMetadata(string source,
  CompileOptions? options = null)` six-phase pipeline body.
- T7 — emit the outer `try { ... } catch (CompileError e) { throw new
  CompileError(..., source: source, phase: e.Category?.ToString(),
  innerException: e); }` augmenting-throw wrapper inside T6.
- T8 — emit the inner Phase-2.5 type-check `try { ... } catch (Exception)
  when (opts.StrictTypes) { throw; } catch (Exception e) when (!opts.
  StrictTypes) { Console.WriteLine($"[TYPE CHECK] Failed: {e}"); }`
  conditional-rethrow block inside T6.
- T9 — emit `public BytecodeProgram CompileProgram(Program ast,
  IReadOnlyList<ProcDecl>? procDeclarations = null)` alternative entry
  with `Array.Empty<ProcDecl>()` default + `skipGlobalSRSW: true` named
  argument.
- T10 — emit `/// <summary>...</summary>` doc comments adjacent to
  `CompileOptions`, `TypeCheck`, `StrictTypes`, `GlpCompiler`, `Compile`,
  `CompileWithMetadata`, and `CompileProgram` per
  `rf-dart-docblock-triple-slash-to-csharp-xml-doc`.
- T11 — preserve the orphan trailing doc comment about `_select/1` as a
  class-level `<remarks>` block; emit NO method signature or body
  (FR-023).
- T12 — preserve all 20 `//` line comments verbatim adjacent to their
  anchoring statements inside `CompileWithMetadata` and `CompileProgram`.

## 4. Research Findings

None required — every construct maps to a research finding ALREADY
cached from sibling specs (per FR-024 cache discipline). The convspec
section "Cache reuses (FR-012 idiom-first)" + "New idioms recorded"
enumerates every research finding reused or newly-introduced here:

Cached idioms reused:
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
- `rf-dart-list-to-csharp-list-of-T`
- `rf-dart-implements-exception-to-csharp-derive-system-exception`
- `rf-dart-line-comment-preserve-verbatim`

New idioms recorded in this file's convspec (will be reused downstream):
- `rf-dart-function-typed-field-to-csharp-func-delegate-field` — Dart
  inline function-type field → C# `Func<,>` / `Func<>` delegate field;
  authoritative both sides (Dart language tour "Functions"; Microsoft
  Learn "Delegates" → `Func<TResult>` / `Func<T,TResult>`).
- `rf-dart-try-on-catch-to-csharp-typed-catch` — Dart `on Type catch (e)`
  → C# `catch (Type e)`; Dart `rethrow` → C# `throw;` (no-expression
  form); conditional rethrow → `catch (T) when (cond) { throw; }` filter
  form (Microsoft Learn "Exception filters").
- `rf-dart-enum-tostring-prefix-vs-csharp-bare-name` — Dart `Enum.
  toString()` returns `"EnumType.value"` (qualified) → C# `Enum.
  ToString()` returns `"value"` (unqualified); the Dart-idiomatic
  `.split('.').last` post-processing DROPS in C# (Microsoft Learn
  `Enum.ToString`).

All authoritatively grounded — Dart official docs + Microsoft Learn —
per the FR-024 contract. No WebSearch / WebFetch / Agent was invoked;
no new research was needed for this file.

## 5. Consistency Pass

All target decisions in §2 are derivable VERBATIM from the convspec
(file `.codeconv/conversion-specs/lib/compiler/compiler.dart.md`, sha
matches source `1b65ae574b5c4d866bf91680efdd48fd3a59072b31f44da7f2a3e19cd6ddc310`,
0 convspec escalations). Every construct decision is grounded in:

- the convspec construct block's `target_decision` + `nuance` (mirrored
  one-for-one in §2.1 through §2.8);
- the convspec `conversion_units` enumeration (T1–T12 in §3 cover every
  listed unit);
- the cached cross-spec research findings + three new idioms enumerated
  in §4 (FR-024).

Per-section provenance:

- §2.1 imports/exports — convspec construct
  `dart.module.relative_imports_with_one_package_import_and_three_reexports`.
- §2.2 CompileOptions — convspec construct
  `dart.data_class.compile_options_two_bool_fields_const_named_ctor_with_defaults`.
- §2.3 GlpCompiler skeleton — convspec construct
  `dart.class.compiler_facade_with_four_injectable_factory_typedef_fields_and_pipeline_method`.
- §2.4 Compile — convspec construct
  `dart.method.compile_one_line_delegation_to_with_metadata_returning_field`.
- §2.5 CompileWithMetadata — convspec construct
  `dart.method.compile_with_metadata_six_phase_pipeline_with_typecheck_branch_and_try_on_compileerror_rethrow_with_source_context`.
- §2.6 CompileProgram — convspec construct
  `dart.method.compile_program_alternative_entry_skipping_lex_parse_typecheck_with_named_optional_proc_decls`.
- §2.7 doc comments — convspec construct `dart.docblock_triple_slash`.
- §2.8 line comments — convspec construct
  `dart.line_comment.inline_or_above_pipeline_phase`.

Cross-file consequences recorded by the convspec (noted but not blocking
THIS file's plan):

1. **Augmenting throw and stack-trace preservation** — `error.cs`
   `CompileError` constructor must accept an `innerException` parameter.
   Recorded by convspec; `error.dart.md` records the constructor surface.
2. **`Module → Program` constructor** — `Program(procedures, line, column)`
   signature established in `ast.dart.md`. Authoritative.
3. **`Analyzer.Analyze` named-argument surface** — three named arguments
   in `CompileWithMetadata`, four in `CompileProgram`. `analyzer.dart.md`
   records the named-argument surface (preserved via C# named-argument
   invocation).
4. **`CompilationResult.Program` casing** — PascalCase per
   `result.dart.md`.
5. **`CompileMode.System` enum-value casing** — PascalCase per
   `ast.dart.md`.
6. **Truncated source** — file ends at line 158 mid-class. Preserved as
   documented absence; no inventive `_select` method synthesis (FR-023).

Fixed — derived from `.codeconv/conversion-specs/lib/compiler/compiler.dart.md`
(escalation-cleared convspec, 0 escalations) and source file at the
matching sha `1b65ae574b5c4d866bf91680efdd48fd3a59072b31f44da7f2a3e19cd6ddc310`.

## 6. Escalations

None.
