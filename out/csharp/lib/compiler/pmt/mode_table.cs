// lib/compiler/pmt/mode_table.cs
//
// PMT Mode Table: Stores mode declarations for type checking
//
// Maps predicate signatures (e.g., "merge/3") to their argument modes
// (reader/writer for each position).
//
// Supports multiple mode alternatives for the same predicate/arity
// (union of mode declarations).
//
// Converted from Dart source: lib/compiler/pmt/mode_table.dart
// source_sha256: 64b7072930ab5bb1260bf1507e236ce16dff7494d456d04154c142fdd4618b21

using System.Collections.Generic;
using System.Linq;

namespace GlpRuntime.Compiler.Pmt;

/// <summary>Argument mode in a PMT declaration.</summary>
public enum Mode
{
    /// <summary>Input: value flows from caller to callee.</summary>
    Reader,
    /// <summary>Output: value flows from callee to caller.</summary>
    Writer,
}

/// <summary>
/// Mode table: stores and retrieves mode declarations by predicate signature.
/// <para>
/// Supports multiple mode alternatives per predicate/arity for union declarations:
/// <code>Observe := observe(Any?, Any, Any?) | observe(Any, Any?, Any?).</code>
/// </para>
/// </summary>
public class ModeTable
{
    /// <summary>Maps predicate/arity to list of mode alternatives.</summary>
    private readonly Dictionary<string, List<List<Mode>>> _modes =
        new(StringComparer.Ordinal);

    /// <summary>Maps predicate/arity to list of declarations (one per alternative).</summary>
    private readonly Dictionary<string, List<ModeDeclaration>> _declarations =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Add a mode declaration to the table.
    /// <para>
    /// If a declaration for the same predicate/arity already exists,
    /// adds this as an alternative mode (for union declarations).
    /// </para>
    /// </summary>
    public void AddDeclaration(ModeDeclaration decl)
    {
        var signature = decl.Signature;

        // Extract modes from moded arguments
        var modes = decl.Args.Select(arg => arg.IsReader ? Mode.Reader : Mode.Writer).ToList();

        // Add to existing alternatives or create new list — _modes first, then _declarations
        if (!_modes.TryGetValue(signature, out var modesBucket))
        {
            modesBucket = new List<List<Mode>>();
            _modes[signature] = modesBucket;
        }
        modesBucket.Add(modes);

        if (!_declarations.TryGetValue(signature, out var declsBucket))
        {
            declsBucket = new List<ModeDeclaration>();
            _declarations[signature] = declsBucket;
        }
        declsBucket.Add(decl);
    }

    /// <summary>
    /// Get the first/primary mode for a predicate with given arity.
    /// <para>Returns null if no declaration exists.
    /// For predicates with multiple modes, use <see cref="GetAllModes"/>.</para>
    /// </summary>
    public List<Mode>? GetModes(string predicate, int arity)
    {
        if (!_modes.TryGetValue($"{predicate}/{arity}", out var allModes) || allModes.Count == 0)
            return null;
        return allModes[0];
    }

    /// <summary>
    /// Get all mode alternatives for a predicate with given arity.
    /// <para>Returns null if no declaration exists.
    /// Returns list of mode signatures (each is a List&lt;Mode&gt;).</para>
    /// </summary>
    public List<List<Mode>>? GetAllModes(string predicate, int arity)
    {
        if (!_modes.TryGetValue($"{predicate}/{arity}", out var allModes))
            return null;
        return allModes;
    }

    /// <summary>Check if a declaration exists for predicate/arity.</summary>
    public bool HasDeclaration(string predicate, int arity) =>
        _modes.ContainsKey($"{predicate}/{arity}");

    /// <summary>Check if predicate has multiple mode alternatives.</summary>
    public bool HasMultipleModes(string predicate, int arity)
    {
        if (!_modes.TryGetValue($"{predicate}/{arity}", out var allModes))
            return false;
        return allModes.Count > 1;
    }

    /// <summary>
    /// Get the first/primary declaration for a predicate/arity.
    /// <para>Returns null if no declaration exists.</para>
    /// </summary>
    public ModeDeclaration? GetDeclaration(string predicate, int arity)
    {
        if (!_declarations.TryGetValue($"{predicate}/{arity}", out var allDecls) || allDecls.Count == 0)
            return null;
        return allDecls[0];
    }

    /// <summary>
    /// Get all declarations for a predicate/arity.
    /// <para>Returns null if no declaration exists.</para>
    /// </summary>
    public List<ModeDeclaration>? GetAllDeclarations(string predicate, int arity)
    {
        if (!_declarations.TryGetValue($"{predicate}/{arity}", out var allDecls))
            return null;
        return allDecls;
    }

    /// <summary>
    /// Get a declaration by its type name (e.g., "And" for "And := and(...)").
    /// <para>Returns null if no declaration with that type name exists.</para>
    /// <para>
    /// Note: FIRST-match order depends on insertion order — both Dart Map.values
    /// and C# Dictionary.Values yield insertion order in practice.
    /// </para>
    /// </summary>
    public ModeDeclaration? GetDeclarationByTypeName(string typeName)
    {
        foreach (var decls in _declarations.Values)
        {
            foreach (var decl in decls)
            {
                if (decl.TypeName.Equals(typeName, StringComparison.Ordinal))
                    return decl;
            }
        }
        return null;
    }

    /// <summary>Get all declared signatures (live view; reflects subsequent AddDeclaration calls).</summary>
    public IEnumerable<string> Signatures => _modes.Keys;

    /// <summary>Get number of unique predicates (not counting alternatives).</summary>
    public int Count => _modes.Count;

    /// <summary>Check if table is empty.</summary>
    public bool IsEmpty => _modes.Count == 0;

    /// <summary>Build a mode table from a list of mode declarations.</summary>
    public static ModeTable FromDeclarations(IReadOnlyList<ModeDeclaration> declarations)
    {
        var table = new ModeTable();
        foreach (var decl in declarations)
        {
            table.AddDeclaration(decl);
        }
        return table;
    }
}
