// lib/compiler/compiler.cs
//
// GLP compiler facade — threads source through the six-phase pipeline.
// Converted from Dart source: lib/compiler/compiler.dart
// source_sha256: 1b65ae574b5c4d866bf91680efdd48fd3a59072b31f44da7f2a3e19cd6ddc310

using GlpRuntime.Analysis.TypeChecker;
using GlpRuntime.Bytecode;

namespace GlpRuntime.Compiler;

/// <summary>Compilation options</summary>
public class CompileOptions
{
    /// <summary>Enable type checking</summary>
    public bool TypeCheck { get; }

    /// <summary>Abort compilation on type errors (only applies if <see cref="TypeCheck"/> is true)</summary>
    public bool StrictTypes { get; }

    public CompileOptions(bool typeCheck = false, bool strictTypes = false)
    {
        TypeCheck   = typeCheck;
        StrictTypes = strictTypes;
    }
}

/// <summary>Main GLP compiler</summary>
public sealed class GlpCompiler
{
    private readonly Func<string, Lexer>                  _createLexer;
    private readonly Func<IReadOnlyList<Token>, Parser>   _createParser;
    private readonly Func<Analyzer>                       _createAnalyzer;
    private readonly Func<CodeGenerator>                  _createCodegen;

    public GlpCompiler(
        Func<string, Lexer>?                createLexer    = null,
        Func<IReadOnlyList<Token>, Parser>? createParser   = null,
        Func<Analyzer>?                     createAnalyzer = null,
        Func<CodeGenerator>?                createCodegen  = null)
    {
        _createLexer    = createLexer    ?? (source => new Lexer(source));
        _createParser   = createParser   ?? (tokens => new Parser(tokens));
        _createAnalyzer = createAnalyzer ?? (() => new Analyzer());
        _createCodegen  = createCodegen  ?? (() => new CodeGenerator());
    }

    /// <summary>Compile GLP source to bytecode program</summary>
    public BytecodeProgram Compile(string source, CompileOptions? options = null)
    {
        var result = CompileWithMetadata(source, options);
        return result.Program;
    }

    /// <summary>Compile GLP source to bytecode program with variable metadata</summary>
    public CompilationResult CompileWithMetadata(string source, CompileOptions? options = null)
    {
        var opts = options ?? new CompileOptions();
        try
        {
            // Phase 1: Lexical analysis
            // Note: Main lexer now handles type declarations (::= and procedure)
            var lexer  = _createLexer(source);
            var tokens = lexer.Tokenize();

            // Phase 2: Syntax analysis (use parseModule to get module info)
            var parser = _createParser(tokens);
            var module = parser.ParseModule();

            // Convert Module to Program for analyzer
            var ast = new Program(module.Procedures, module.Line, module.Column);

            // Phase 2.4: Apply partial evaluation (defined guard expansion) BEFORE type checking
            // This transforms clauses to unfold unit clause guards, which affects coverage checking
            var partialEvaluator = new PartialEvaluator();
            var transformedAst   = partialEvaluator.TransformDefinedGuards(ast);

            // Phase 2.5: Type checking (optional)
            if (opts.TypeCheck)
            {
                try
                {
                    // Use checkModule with transformed procedures
                    // This ensures type checking sees the expanded guards
                    var typeResult = TypeCheckerDriver.CheckModule(module, transformedProcedures: transformedAst.Procedures);

                    // Report type errors and warnings
                    if (typeResult.Errors.Count > 0)
                    {
                        foreach (var error in typeResult.Errors)
                            Console.WriteLine($"[TYPE ERROR] {error.Message} at line {error.Line}");
                        if (opts.StrictTypes)
                            throw new CompileError(
                                $"Type checking failed with {typeResult.Errors.Count} error(s)",
                                (long)typeResult.Errors[0].Line,
                                (long)typeResult.Errors[0].Column);
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
            var analyzer     = _createAnalyzer();
            var annotatedAst = analyzer.Analyze(
                ast,
                generateReduce:   generateReduce,
                procDeclarations: module.ProcDeclarations,
                compileMode:      module.CompileMode);

            // Phase 4: Code generation
            var codegen = _createCodegen();
            var result  = codegen.GenerateWithMetadata(annotatedAst);

            return result;
        }
        catch (CompileError e)
        {
            // Rethrow with source context
            throw new CompileError(e.Message, e.Line, e.Column, source: source, phase: e.Category?.ToString());
        }
    }

    /// <summary>
    /// Compile a Program AST directly to bytecode.
    /// <para>Used by the project linker for statically linked programs.</para>
    /// <para>Skips lexing, parsing, type checking, and _select generation.</para>
    /// <para><paramref name="procDeclarations"/> should contain renamed declarations
    /// (e.g., from linkProject) for SRSW type-based relaxation.</para>
    /// </summary>
    public BytecodeProgram CompileProgram(Program ast, IReadOnlyList<ProcDecl>? procDeclarations = null)
    {
        // Used by the project linker for statically linked programs.
        // Skips lexing, parsing, type checking, and _select generation.
        var analyzer = _createAnalyzer();
        var annotated = analyzer.Analyze(
            ast,
            generateReduce:   true,
            compileMode:      CompileMode.System,
            procDeclarations: procDeclarations ?? Array.Empty<ProcDecl>(),
            skipGlobalSRSW:   true);  // Linked programs: modules already type-checked individually

        var codegen = _createCodegen();
        return codegen.GenerateWithMetadata(annotated).Program;
    }

    // <remarks>
    // TODO: Generate _select/1 dispatch table from exported procedure declarations.
    // (Source file truncated mid-class — method not present in original Dart source.)
    // </remarks>
}
