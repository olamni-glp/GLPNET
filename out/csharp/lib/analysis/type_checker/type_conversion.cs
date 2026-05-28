// lib/analysis/type_checker/type_conversion.cs
//
// Converts Term AST (from unified parser) to TypeExpr AST for type definitions.
// Per spec: /Users/udi/GLP/docs/type system/type-conversion.md
// Converted from Dart source: lib/analysis/type_checker/type_conversion.dart
// source_sha256: 9c7136df1616ffa51b44939772b8a8e0d7b2d6da7d562caa07fbaa7868a1081c

using System;
using System.Diagnostics;
using System.Linq;
using GlpRuntime.Compiler;

namespace GlpRuntime.Analysis.TypeChecker;

/// <summary>
/// Pure structural converter from parser <see cref="Term"/> AST to type-checker
/// <see cref="TypeExpr"/> AST. Zero state, zero I/O, recursive.
/// Semantic checks (determinism, alias prohibition) happen in TypeEnvironment.
/// </summary>
public static class TypeConversion
{
    /// <summary>
    /// Converts a Term AST node to a TypeExpr AST node.
    ///
    /// This is a pure structural conversion with no semantic validation.
    /// Semantic checks (determinism, alias prohibition) happen in type_environment.
    /// </summary>
    public static TypeExpr TermToTypeExpr(Term term) => term switch
    {
        VarTerm v        => new TypeRef(v.Name, v.Line, v.Column, isInput: v.IsReader),
        UnderscoreTerm u => new PrimitiveModeAlt(u.IsReader, u.Line, u.Column),
        ConstTerm c      => new ConstantAlt(c.Value ?? "", c.Line, c.Column),
        ListTerm l       => ConvertList(l),
        StructTerm s     => ConvertStruct(s),
        _                => throw new ArgumentException($"Cannot convert term to type expression: {term}")
    };

    // -------------------------------------------------------------------------
    // Private sub-dispatch helpers
    // -------------------------------------------------------------------------

    private static TypeExpr ConvertList(ListTerm l)
    {
        if (l.Head is null && l.Tail is null)
            return new ListNilAlt(l.Line, l.Column);

        // The ListTerm invariant guarantees that if either Head or Tail is non-null
        // then both are non-null (cons cells are built with exactly both-null or
        // both-non-null). The null-forgiving operator ! below is intentional: C#
        // flow analysis cannot prove individual non-nullness from the conjunction
        // `Head is null && Tail is null` being false (it only proves at least one is
        // non-null, not both). The Dart source uses `head!`/`tail!` for the same
        // reason. On debug builds, the assert below fires if the invariant is violated,
        // preserving Dart's tight-failure semantics (Dart `!` is runtime-checked;
        // C# `!` is compile-time-only).
        Debug.Assert(l.Head is not null && l.Tail is not null,
            "ListTerm cons invariant violated: Head and Tail must both be non-null.");
        return new ListConsAlt(TermToTypeExpr(l.Head!), TermToTypeExpr(l.Tail!), l.Line, l.Column);
    }

    private static TypeExpr ConvertStruct(StructTerm s)
    {
        // Difference list: A \ B
        if (s.Functor == "\\" && s.Args.Count == 2)
            return new DiffListAlt(
                TermToTypeExpr(s.Args[0]),
                TermToTypeExpr(s.Args[1]),
                s.Line,
                s.Column);

        // Parameterized type reference — uppercase-initial functor (possibly with trailing '?')
        // e.g., Stream(X), Channel(In, Out), Stream(X)? (encoded as "Stream?")
        // In type definition bodies, uppercase functor with arguments is always a parameterized
        // type reference, never a struct alternative (struct functors are lowercase atoms).
        var functor = s.Functor;
        bool isInput = false;
        if (functor.EndsWith("?", StringComparison.Ordinal))
        {
            functor = functor.Substring(0, functor.Length - 1);
            isInput = true;
        }
        if (functor.Length > 0 && IsUppercaseLetter(functor[0]))
            return new TypeRef(functor, s.Line, s.Column,
                isInput: isInput,
                typeArgs: s.Args.Select(TermToTypeExpr).ToList());

        // Regular structure (lowercase functor, including conjunction ',')
        return new StructAlt(s.Functor, s.Args.Select(TermToTypeExpr).ToList(), s.Line, s.Column);
    }

    /// <summary>
    /// Check if a character is an uppercase letter (A-Z).
    /// Excludes operators (+, -, etc.) and underscore which satisfy toUpperCase() == self.
    ///
    /// Note: <see cref="char.IsUpper(char)"/> is intentionally NOT used here — it
    /// would over-accept non-ASCII uppercase letters in Unicode category Lu (Á, Ω, Я, etc.)
    /// which the GLP functor lexer rejects. The explicit ASCII range check is preserved
    /// verbatim from the Dart source. <c>char.IsAsciiLetterUpper(ch)</c> (.NET 7+) would
    /// be a safe equivalent but is avoided to prevent an SDK-version dependency.
    /// </summary>
    private static bool IsUppercaseLetter(char ch) => ch >= 'A' && ch <= 'Z';
}
