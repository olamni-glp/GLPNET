// lib/compiler/pmt/type_checker.cs
//
// MT Type Checker: Verifies terms match their declared types.
// Checks that constants, structs, and list elements conform to
// the type definitions in the module.
//
// Converted from Dart source: lib/compiler/pmt/type_checker.dart
// source_sha256: ec1d9f359ba877391bcc2acf4b8a4b99f7a37b8ae05d778864d89fb9940d5c95

using System;
using System.Collections.Generic;
using System.Linq;
using GlpRuntime.Analysis.TypeChecker;
using GlpRuntime.Compiler;

namespace GlpRuntime.Compiler.Pmt;

// ---------------------------------------------------------------------------
// TypeError — sealed reference class, no IEquatable (Dart source has no ==)
// ---------------------------------------------------------------------------

/// <summary>
/// Type checking error. Three non-nullable positional properties plus one
/// nullable Suggestion. No IEquatable override — Dart source did not
/// hand-write == / hashCode, so C# preserves default reference equality.
/// </summary>
public sealed class TypeError
{
    public string Message { get; }
    public int Line { get; }
    public int Column { get; }
    public string? Suggestion { get; }

    public TypeError(string message, int line, int column, string? suggestion = null)
    {
        Message    = message;
        Line       = line;
        Column     = column;
        Suggestion = suggestion;
    }

    public override string ToString()
    {
        var result = $"[type] {Message} at Line {Line}, Column {Column}";
        if (Suggestion is not null)
            result += "\n  Suggestion: " + Suggestion;
        return result;
    }
}

// ---------------------------------------------------------------------------
// TypeChecker — coordinator class, not sealed, two private readonly deps
// ---------------------------------------------------------------------------

/// <summary>Type checker for Moded Type definitions.</summary>
public class TypeChecker
{
    private readonly TypeTable _typeTable;
    private readonly ModeTable _modeTable;

    public TypeChecker(TypeTable typeTable, ModeTable modeTable)
    {
        _typeTable = typeTable;
        _modeTable = modeTable;
    }

    // -----------------------------------------------------------------------
    // CheckModule
    // -----------------------------------------------------------------------

    /// <summary>Check all clauses in a module.</summary>
    public List<TypeError> CheckModule(Module module)
    {
        var errors = new List<TypeError>();
        foreach (var proc in module.Procedures)
        {
            var modeDecl = _modeTable.GetDeclaration(proc.Name, proc.Arity);
            if (modeDecl is null) continue; // No declaration, skip type checking
            foreach (var clause in proc.Clauses)
            {
                errors.AddRange(CheckClause(clause, modeDecl));
            }
        }
        return errors;
    }

    // -----------------------------------------------------------------------
    // CheckClause
    // -----------------------------------------------------------------------

    /// <summary>Check a single clause against its mode declaration.</summary>
    public List<TypeError> CheckClause(Clause clause, ModeDeclaration modeDecl)
    {
        var errors = new List<TypeError>();

        // Check head arguments
        for (int i = 0; i < clause.Head.Args.Count && i < modeDecl.Args.Count; i++)
        {
            var arg          = clause.Head.Args[i];
            var declaredType = modeDecl.Args[i].TypeName;
            var typeParams   = modeDecl.Args[i].TypeParams;
            errors.AddRange(CheckTerm(arg, declaredType, typeParams));
        }

        // Check body goals
        if (clause.Body is not null)
        {
            foreach (var goal in clause.Body)
            {
                var goalDecl = _modeTable.GetDeclaration(goal.Functor, goal.Arity);
                if (goalDecl is null) continue;

                for (int i = 0; i < goal.Args.Count && i < goalDecl.Args.Count; i++)
                {
                    var arg          = goal.Args[i];
                    var declaredType = goalDecl.Args[i].TypeName;
                    var typeParams   = goalDecl.Args[i].TypeParams;
                    errors.AddRange(CheckTerm(arg, declaredType, typeParams));
                }
            }
        }

        return errors;
    }

    // -----------------------------------------------------------------------
    // CheckTerm
    // -----------------------------------------------------------------------

    /// <summary>Check a term against an expected type.</summary>
    public List<TypeError> CheckTerm(Term term, string expectedType, IReadOnlyList<string> typeParams)
    {
        var errors = new List<TypeError>();

        // Variables are not checked — their types are inferred
        if (term is VarTerm) return errors;

        // Underscore matches any type
        if (term is UnderscoreTerm) return errors;

        // Constants must be valid constructors of the type
        if (term is ConstTerm constTerm)
        {
            if (!IsValidConstant(constTerm, expectedType))
            {
                var validConstructors = GetValidConstructors(expectedType);
                errors.Add(new TypeError(
                    $"'{constTerm.Value}' is not a valid '{expectedType}'",
                    constTerm.Line,
                    constTerm.Column,
                    suggestion: validConstructors.Count > 0
                        ? $"Valid constructors: {string.Join(", ", validConstructors)}"
                        : null));
            }
            return errors;
        }

        // List terms
        if (term is ListTerm listTerm)
        {
            // Empty list is valid for any list type
            if (listTerm.IsNil) return errors;

            // Check if expectedType has list constructors
            var typeDef = _typeTable.LookupType(expectedType);
            if (typeDef is not null)
            {
                bool hasListCtor = typeDef.Alternatives.Any(c => c is ListNilAlt || c is ListConsAlt);
                if (!hasListCtor && expectedType != "List")
                {
                    errors.Add(new TypeError(
                        $"List term not valid for type '{expectedType}'",
                        listTerm.Line,
                        listTerm.Column));
                    return errors;
                }

                // Build type parameter substitution map
                // e.g., List(A) with typeParams=["BinaryDigit"] -> {A: BinaryDigit}
                var typeParamSubst = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 0; i < typeDef.TypeParams.Count && i < typeParams.Count; i++)
                {
                    typeParamSubst[typeDef.TypeParams[i]] = typeParams[i];
                }

                // Find non-nil list constructor (ListConsAlt) to get element type
                var listCons = typeDef.Alternatives
                    .OfType<ListConsAlt>()
                    .FirstOrDefault();
                if (listCons is not null && listTerm.Head is not null)
                {
                    // TypeExpr.TypeName returns the name if the head is a TypeRef, else null
                    var paramTypeName = listCons.Head.TypeName;
                    if (paramTypeName is not null)
                    {
                        var elementType = typeParamSubst.TryGetValue(paramTypeName, out var substituted)
                            ? substituted
                            : paramTypeName;
                        if (elementType != "_")
                        {
                            errors.AddRange(CheckTerm(listTerm.Head, elementType, Array.Empty<string>()));
                        }
                    }
                }
            }
            else if (typeParams.Count > 0 && listTerm.Head is not null)
            {
                // Type not defined but we have type params — use first as element type
                errors.AddRange(CheckTerm(listTerm.Head, typeParams[0], Array.Empty<string>()));
            }

            // Check tail recursively
            if (listTerm.Tail is not null)
            {
                errors.AddRange(CheckTerm(listTerm.Tail, expectedType, typeParams));
            }

            return errors;
        }

        // Struct terms
        if (term is StructTerm structTerm)
        {
            if (!IsValidStructConstructor(structTerm, expectedType, typeParams))
            {
                var validConstructors = GetValidConstructors(expectedType);
                errors.Add(new TypeError(
                    $"'{structTerm.Functor}(...)' is not a valid '{expectedType}'",
                    structTerm.Line,
                    structTerm.Column,
                    suggestion: validConstructors.Count > 0
                        ? $"Valid constructors: {string.Join(", ", validConstructors)}"
                        : null));
            }
            return errors;
        }

        return errors;
    }

    // -----------------------------------------------------------------------
    // IsValidConstant
    // -----------------------------------------------------------------------

    /// <summary>Check if a constant is a valid constructor for a type.</summary>
    public bool IsValidConstant(ConstTerm term, string typeName)
    {
        // Built-in numeric types
        if (typeName is "Num" or "Number" or "Int" or "Integer")
            return term.Value is double or float or int or long or short or byte or decimal;

        // Built-in atom/string types
        if (typeName is "Atom" or "String")
            return term.Value is string;

        // User-defined types
        var typeDef = _typeTable.LookupType(typeName);
        if (typeDef is null) return true; // Type not defined — allow (might be built-in or external)

        var termValue = term.Value?.ToString() ?? "";

        // Check if constant matches any ConstantAlt (atom constructor)
        foreach (var alt in typeDef.Alternatives)
        {
            if (alt is ConstantAlt constAlt && constAlt.Value?.ToString() == termValue)
                return true;
        }

        // Check if type references another type (ConstantAlt whose value is a capitalised name)
        // e.g., Gates ::= And ; Or ; Not  — And/Or/Not are type references
        foreach (var alt in typeDef.Alternatives)
        {
            if (alt is ConstantAlt constAlt && constAlt.Value is string refName && _IsCapitalized(refName))
            {
                var refTypeDef = _typeTable.LookupType(refName);
                if (refTypeDef is not null && _TypeContainsAtom(refTypeDef, termValue))
                    return true;
            }
        }

        return false;
    }

    // -----------------------------------------------------------------------
    // IsValidStructConstructor
    // -----------------------------------------------------------------------

    /// <summary>Check if a struct term is valid for a type.</summary>
    public bool IsValidStructConstructor(StructTerm term, string typeName, IReadOnlyList<string>? typeParams = null)
    {
        var effectiveParams = typeParams ?? Array.Empty<string>();
        var typeDef = _typeTable.LookupType(typeName);
        if (typeDef is null) return true; // Unknown type — allow

        // Build type parameter substitution map
        var typeParamSubst = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < typeDef.TypeParams.Count && i < effectiveParams.Count; i++)
        {
            typeParamSubst[typeDef.TypeParams[i]] = effectiveParams[i];
        }

        // Pass 1: direct StructAlt functor match
        foreach (var alt in typeDef.Alternatives)
        {
            if (alt is StructAlt structAlt && structAlt.Functor == term.Functor)
                return true;
        }

        // Pass 2: capitalised ConstantAlt type references
        foreach (var alt in typeDef.Alternatives)
        {
            if (alt is ConstantAlt constAlt && constAlt.Value is string ctorName && _IsCapitalized(ctorName))
            {
                // Check if this is a type parameter that should be substituted
                if (typeParamSubst.TryGetValue(ctorName, out var substitutedType))
                {
                    // acyclic-TypeTable invariant
                    if (IsValidStructConstructor(term, substitutedType))
                        return true;
                }

                // This is a type reference — check if its mode declaration has this predicate
                var modeDecl = _modeTable.GetDeclarationByTypeName(ctorName);
                if (modeDecl is not null && modeDecl.Predicate == term.Functor)
                    return true;
            }
        }

        return false;
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private bool _IsCapitalized(string name)
    {
        if (name.Length == 0) return false;
        var first = name[0];
        return char.IsUpper(first);
    }

    private bool _TypeContainsAtom(TypeDef typeDef, string atomName)
    {
        // no cycle detection; depends on TypeTable being acyclic, an invariant of the parser
        foreach (var alt in typeDef.Alternatives)
        {
            if (alt is ConstantAlt constAlt)
            {
                var name = constAlt.Value?.ToString() ?? "";
                if (name == atomName) return true;
                if (_IsCapitalized(name))
                {
                    var refType = _typeTable.LookupType(name);
                    if (refType is not null && _TypeContainsAtom(refType, atomName))
                        return true;
                }
            }
        }
        return false;
    }

    /// <summary>Get list of valid constructor display strings for error messages.</summary>
    public List<string> GetValidConstructors(string typeName)
    {
        var result  = new List<string>();
        var typeDef = _typeTable.LookupType(typeName);
        if (typeDef is null) return result;

        foreach (var alt in typeDef.Alternatives)
        {
            switch (alt)
            {
                case ConstantAlt constAlt:
                    result.Add(constAlt.Value?.ToString() ?? "");
                    break;
                case StructAlt structAlt:
                    result.Add($"{structAlt.Functor}(...)");
                    break;
                case ListNilAlt:
                    result.Add("[]");
                    break;
                case ListConsAlt:
                    result.Add("[...|...]");
                    break;
                // DiffListAlt, PrimitiveModeAlt, TypeRef: silently skipped
                // (no equivalent in Dart's TupleConstructor path either;
                //  E3 escalation resolution: treat non-PMT-checkable alts as not enumerable)
            }
        }

        return result;
    }
}
