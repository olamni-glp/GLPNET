// lib/compiler/pmt/validator.cs
//
// PMT Validator: High-level API for validating GLP programs against mode declarations.
// Provides static methods to validate source code or parsed ASTs.
//
// Converted from Dart source: lib/compiler/pmt/validator.dart
// source_sha256: 448fc123513f79f1a1f9a0444d1c4525a10d38163a5471cdae5b41e0c9eba4e6

using GlpRuntime.Compiler;

namespace GlpRuntime.Compiler.Pmt;

/// <summary>
/// PMT Validator: static facade for PMT validation entry-points.
/// Cannot be instantiated — all members are static.
/// </summary>
public static class PmtValidator
{
    /// <summary>
    /// Validate source code against mode declarations.
    /// Returns a list of PMT errors. Empty list means the program is valid.
    /// Allocates a fresh Lexer and Parser per call — both carry mutable cursor
    /// state and must NOT be hoisted or reused across calls.
    /// </summary>
    public static List<PmtError> ValidateSource(string source)
    {
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var ast = parser.ParseModule();
        return ValidateAst(ast);
    }

    /// <summary>
    /// Validate a parsed AST against its mode declarations.
    /// Returns a list of PMT errors. Empty list means the program is valid.
    /// Silently returns an empty list when no mode declarations are present
    /// ("No mode declarations = nothing to check").
    /// Performs SRSW checking (phase 1) followed by optional type checking
    /// (phase 2, only when type definitions exist).
    /// SRSW errors are accumulated first, then type errors — textual order preserved.
    /// </summary>
    public static List<PmtError> ValidateAst(Module ast)
    {
        // No mode declarations = nothing to check (silent early-return, NOT an error)
        var modeDeclarations = ast.ModeDeclarations();
        if (modeDeclarations.Count == 0)
            return new List<PmtError>();

        // Build mode table from declarations
        var modeTable = ModeTable.FromDeclarations(modeDeclarations);

        // Build type table from type definitions
        var typeTable = TypeTable.FromModule(ast);

        // Create SRSW checker with mode table — fresh instance per call
        var srswChecker = new PmtChecker(modeTable);

        // Single shared accumulator — SRSW errors appended first
        var errors = new List<PmtError>();
        foreach (var proc in ast.Procedures)
        {
            errors.AddRange(srswChecker.CheckProcedure(proc));
        }

        // Conditional second phase: type checking only when type definitions exist
        if (ast.TypeDefs.Count != 0)
        {
            var typeChecker = new TypeChecker(typeTable, modeTable);
            var typeErrors = typeChecker.CheckModule(ast);

            // Convert TypeError → PmtError (lossy: keep only Message/Line/Column)
            // in-place Add on shared accumulator preserves single-accumulator design
            foreach (var te in typeErrors)
            {
                errors.Add(new PmtError(te.Message, te.Line, te.Column));
            }
        }

        return errors;
    }

    /// <summary>
    /// Check if source code is valid according to mode declarations.
    /// Returns true if no PMT errors are found.
    /// Allocates and discards a List&lt;PmtError&gt; per call — observable cost preserved verbatim.
    /// </summary>
    public static bool IsValid(string source) => ValidateSource(source).Count == 0;

    /// <summary>
    /// Validate source and throw <see cref="PmtErrors"/> if invalid.
    /// The errors list reference is aliased into <see cref="PmtErrors.Errors"/>
    /// with no defensive copy, per the aliasing contract in errors.dart.md.
    /// </summary>
    public static void AssertValid(string source)
    {
        var errors = ValidateSource(source);
        if (errors.Count != 0)
            throw new PmtErrors(errors);
    }
}
