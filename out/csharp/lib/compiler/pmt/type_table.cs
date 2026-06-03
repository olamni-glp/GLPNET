// lib/compiler/pmt/type_table.cs
//
// Type table for Moded Type definitions.
// Stores user-defined type definitions parsed from GLP source.
// Converted from Dart source: lib/compiler/pmt/type_table.dart
// source_sha256: fecf3a38722602da8b1ac6e1a3459b739c37c0c02c91588b335e30ba0d6ce74a

using System.Collections.Generic;
using System.Linq;
using System.Text;
using GlpRuntime.Analysis.TypeChecker;
using GlpRuntime.Compiler;

namespace GlpRuntime.Compiler.Pmt;

/// <summary>Table mapping type names to their definitions.</summary>
public class TypeTable
{
    // Dart `final Map<String, TypeDef> _types = {}` — reference-final, value-mutable.
    // StringComparer.Ordinal: byte-exact matching, no culture-sensitive drift.
    private readonly Dictionary<string, TypeDef> _types =
        new(StringComparer.Ordinal);

    /// <summary>Initialises an empty TypeTable.</summary>
    public TypeTable() { }

    // -------------------------------------------------------------------------
    // Mutation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Add a type definition to the table.
    /// If the type already exists, merges alternatives from <paramref name="def"/>
    /// into the existing entry, preserving the first definition's TypeParams,
    /// Line, and Column.
    /// </summary>
    public void AddDefinition(TypeDef def)
    {
        if (_types.TryGetValue(def.Name, out var existing))
        {
            // Merge: existing alternatives first, then def's alternatives.
            var mergedAlternatives = existing.Alternatives.Concat(def.Alternatives).ToList();
            _types[def.Name] = new TypeDef(
                def.Name,
                mergedAlternatives,
                existing.Line,
                existing.Column,
                existing.TypeParams);   // Keep params from first definition
        }
        else
        {
            _types[def.Name] = def;
        }
    }

    /// <summary>
    /// Add a single alternative to an existing or new type entry.
    /// For an existing entry the alternative is appended, preserving the first
    /// definition's TypeParams, Line, and Column.
    /// For a new entry a fresh TypeDef is created with the supplied
    /// <paramref name="typeParams"/>, <paramref name="line"/>, and
    /// <paramref name="col"/>.
    /// </summary>
    public void AddConstructor(
        string typeName,
        TypeExpr ctor,
        IReadOnlyList<string>? typeParams = null,
        int line = 0,
        int col = 0)
    {
        if (_types.TryGetValue(typeName, out var existing))
        {
            _types[typeName] = new TypeDef(
                typeName,
                existing.Alternatives.Append(ctor).ToList(),
                existing.Line,
                existing.Column,
                existing.TypeParams);
        }
        else
        {
            _types[typeName] = new TypeDef(
                typeName,
                new List<TypeExpr> { ctor },
                line,
                col,
                typeParams ?? new List<string>());
        }
    }

    // -------------------------------------------------------------------------
    // Queries
    // -------------------------------------------------------------------------

    /// <summary>
    /// Look up a type definition by name.
    /// Returns <see langword="null"/> if not found (mirrors Dart <c>Map[]</c>
    /// null-on-miss semantics; avoids <c>KeyNotFoundException</c>).
    /// Renamed from Dart <c>getType</c> to avoid shadowing
    /// <c>object.GetType()</c>.
    /// </summary>
    public TypeDef? LookupType(string name) =>
        _types.GetValueOrDefault(name);

    /// <summary>Returns <see langword="true"/> if a type with <paramref name="name"/> exists.</summary>
    public bool HasType(string name) => _types.ContainsKey(name);

    // -------------------------------------------------------------------------
    // Views (live — not snapshots)
    // -------------------------------------------------------------------------

    /// <summary>Live view of all type names in the table.</summary>
    public IEnumerable<string> TypeNames => _types.Keys;

    /// <summary>Live view of all type definitions in the table.</summary>
    public IEnumerable<TypeDef> Definitions => _types.Values;

    /// <summary>Number of type entries (O(1)).</summary>
    public int Length => _types.Count;

    /// <summary>Whether the table contains no entries.</summary>
    public bool IsEmpty => _types.Count == 0;

    // -------------------------------------------------------------------------
    // Static factories
    // -------------------------------------------------------------------------

    /// <summary>
    /// Build a <see cref="TypeTable"/> by calling
    /// <see cref="AddDefinition"/> for each element of <paramref name="defs"/>
    /// in order.  Iteration order is preserved: first-occurrence wins for
    /// TypeParams / Line / Column on duplicate type names.
    /// </summary>
    public static TypeTable FromDefinitions(IReadOnlyList<TypeDef> defs)
    {
        var table = new TypeTable();
        foreach (var def in defs)
            table.AddDefinition(def);
        return table;
    }

    /// <summary>
    /// Build a <see cref="TypeTable"/> from a module's type definitions.
    /// Delegates to <see cref="FromDefinitions"/>.
    /// </summary>
    public static TypeTable FromModule(Module module) =>
        FromDefinitions(module.TypeDefs);

    // -------------------------------------------------------------------------
    // Diagnostics
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a multi-line string listing all type definitions.
    /// Uses explicit <c>\n</c> (LF) to match Dart <c>StringBuffer.writeln</c>
    /// behaviour across all host operating systems.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder("TypeTable(\n");
        foreach (var def in _types.Values)
            sb.Append($"  {def}\n");
        sb.Append(')');
        return sb.ToString();
    }
}
