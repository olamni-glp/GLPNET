// Canonical CDDL emitter (feature 043-xsd-schema-language, T015; research R5).
//
// Contract: specs/043-xsd-schema-language/contracts/lowering.md §Construct mapping — the full
// mapping table, emitted in canonical form: rule order = message rules in declaration order
// then named-type rules in declaration order; 2-space indent; one field per line inside
// sequence maps; trailing commas as in the shipped crdt_message artifact; choice and simple
// type rules on one line. Same document ⇒ byte-identical CDDL (FR-005 — golden tests pin it).
//
// Constructs the CDDL/functor layer cannot carry are collected (never emitted approximately):
//   - int `min` without `max` (unless min = 0 → `uint`, the table's parenthetical) and `max`
//     without `min` — the mapping table has no row for them (registration law 4);
//   - str enum members that are not CDDL barewords (the `&( name: idx )` form needs names);
//   - lowered rule-name collisions (two names lowering to the same CDDL rule).
// The "no upper" size default (contract: "defaulting an absent bound to 0 / no upper") is
// materialized as the uint-max literal 18446744073709551615, which the lifter maps back to
// an absent maxLength — deterministic and exact over the instance domain.

namespace GlpRuntime.SchemaLang;

internal static class CddlEmitter
{
    internal const string NoUpperSizeBound = "18446744073709551615";

    /// <summary>Rule name law (lowering.md): lowercased, `_`→`-`.</summary>
    internal static string RuleName(string name) => name.ToLowerInvariant().Replace('_', '-');

    /// <summary>
    /// Emits the canonical CDDL artifact for <paramref name="doc"/>. Any construct without a
    /// mapping-table row is appended to <paramref name="unlowerable"/>; the caller must treat
    /// a non-empty list as a LoweringError and discard the text (all-or-nothing).
    /// </summary>
    public static string Emit(SchemaDocument doc, List<string> unlowerable)
    {
        CheckRuleNameCollisions(doc, unlowerable);

        var sb = new System.Text.StringBuilder();
        foreach (var message in doc.Messages)
            EmitCompositionRule(sb, RuleName(message.Functor), message.Body);
        foreach (var type in doc.Types)
        {
            switch (type)
            {
                case ComplexType complex:
                    EmitCompositionRule(sb, RuleName(complex.Name), complex.Composition);
                    break;
                case SimpleType simple:
                    sb.Append(RuleName(simple.Name)).Append(" = ")
                      .Append(SimpleTypeExpr(simple, unlowerable)).Append('\n');
                    break;
            }
        }
        return sb.ToString();
    }

    private static void CheckRuleNameCollisions(SchemaDocument doc, List<string> unlowerable)
    {
        // Report order = declaration order of the first colliding name (FR-005: no
        // dictionary-enumeration ordering on any contract path — research R11).
        var declared = doc.Messages.Select(m => m.Functor).Concat(doc.Types.Select(t => t.Name)).ToList();
        var byRule = new Dictionary<string, List<string>>();
        var ruleOrder = new List<string>();
        foreach (var name in declared)
        {
            var rule = RuleName(name);
            if (!byRule.TryGetValue(rule, out var names))
            {
                byRule[rule] = names = new List<string>();
                ruleOrder.Add(rule);
            }
            names.Add(name);
        }
        foreach (var rule in ruleOrder)
        {
            var names = byRule[rule];
            if (names.Count > 1)
                unlowerable.Add(
                    $"names {string.Join(" and ", names.Select(n => $"'{n}'"))} all lower to CDDL rule '{rule}' — lowered rule names must be distinct");
        }
    }

    // ------------------------------------------------------------------
    // Compositions
    // ------------------------------------------------------------------

    private static void EmitCompositionRule(System.Text.StringBuilder sb, string rule, Composition composition)
    {
        if (composition.Kind == CompositionKind.Sequence)
        {
            sb.Append(rule).Append(" = {\n");
            foreach (var element in composition.Elements)
                sb.Append("  ").Append(FieldEntry(element)).Append(",\n");
            sb.Append("}\n");
        }
        else
        {
            // choice → branch1 // branch2 // …, each branch a one-entry map (lowering.md).
            var branches = composition.Elements.Select(e => "{ " + FieldEntry(e) + " }");
            sb.Append(rule).Append(" = ").Append(string.Join(" // ", branches)).Append('\n');
        }
    }

    private static string FieldEntry(ElementDecl element)
    {
        var optional = element.Occurs.IsOptional ? "? " : string.Empty;
        return $"{optional}{element.Name}: {ElementTypeExpr(element)}";
    }

    private static string ElementTypeExpr(ElementDecl element)
    {
        var inner = TypeExpr(element.Type);
        var occurs = element.Occurs;
        if (occurs.IsDefault || occurs.IsOptional) return inner;
        return occurs.Max is int max
            ? $"[{occurs.Min}*{max} {inner}]"
            : $"[{occurs.Min}* {inner}]";
    }

    private static string TypeExpr(TypeRef typeRef) => typeRef switch
    {
        PrimitiveRef prim => PrimitiveExpr(prim.Kind),
        NamedRef named => RuleName(named.Name),
        ListRef list => $"[* {TypeExpr(list.Element)}]",
        _ => throw new InvalidOperationException($"unknown type-ref {typeRef.GetType().Name}"),
    };

    private static string PrimitiveExpr(PrimitiveKind kind) => kind switch
    {
        PrimitiveKind.Int => "int",
        PrimitiveKind.Str => "tstr",
        PrimitiveKind.Bytes => "bstr",
        PrimitiveKind.Bool => "bool",
        _ => throw new InvalidOperationException($"unknown primitive {kind}"),
    };

    // ------------------------------------------------------------------
    // Simple types (facets → CDDL controls)
    // ------------------------------------------------------------------

    private static string SimpleTypeExpr(SimpleType simple, List<string> unlowerable)
    {
        var min = simple.Facets.OfType<MinValueFacet>().FirstOrDefault();
        var max = simple.Facets.OfType<MaxValueFacet>().FirstOrDefault();
        var minLen = simple.Facets.OfType<MinLengthFacet>().FirstOrDefault();
        var maxLen = simple.Facets.OfType<MaxLengthFacet>().FirstOrDefault();
        var pattern = simple.Facets.OfType<PatternFacet>().FirstOrDefault();
        var enumeration = simple.Facets.OfType<EnumerationFacet>().FirstOrDefault();

        switch (simple.Base)
        {
            case PrimitiveKind.Int:
                if (enumeration is not null)
                    return string.Join(" / ", enumeration.Members); // i1 / i2 / …
                if (min is not null && max is not null)
                    return $"{min.Value}..{max.Value}";
                if (min is not null)
                {
                    if (min.Value == 0) return "uint"; // the table's parenthetical
                    unlowerable.Add(
                        $"facet 'min {min.Value}' without 'max' on type '{simple.Name}' has no CDDL mapping row");
                    return "int";
                }
                if (max is not null)
                {
                    unlowerable.Add(
                        $"facet 'max {max.Value}' without 'min' on type '{simple.Name}' has no CDDL mapping row");
                    return "int";
                }
                return "int";

            case PrimitiveKind.Str:
                if (enumeration is not null)
                {
                    foreach (var member in enumeration.Members)
                        if (!IsCddlBareword(member))
                            unlowerable.Add(
                                $"enum member \"{member}\" on type '{simple.Name}' is not a CDDL bareword — the &(name: idx) form cannot carry it");
                    // Indices = declaration order (matches the shipped crdt-model).
                    var members = enumeration.Members.Select((m, i) => $"{m}: {i}");
                    return $"&( {string.Join(", ", members)} )";
                }
                return "tstr" + SizeControl(minLen, maxLen) + PatternControl(pattern);

            case PrimitiveKind.Bytes:
                return "bstr" + SizeControl(minLen, maxLen);

            case PrimitiveKind.Bool:
                return "bool";

            default:
                throw new InvalidOperationException($"unknown primitive base {simple.Base}");
        }
    }

    private static string SizeControl(MinLengthFacet? minLen, MaxLengthFacet? maxLen)
    {
        if (minLen is null && maxLen is null) return string.Empty;
        var lo = minLen?.Value ?? 0;                                     // absent bound → 0
        var hi = maxLen is null ? NoUpperSizeBound : maxLen.Value.ToString(); // absent bound → no upper
        return $" .size ({lo}..{hi})";
    }

    private static string PatternControl(PatternFacet? pattern) =>
        pattern is null ? string.Empty : $" .regexp \"{pattern.Pattern.Replace("\"", "\\\"")}\"";

    internal static bool IsCddlBareword(string name) =>
        name.Length > 0
        && char.IsAsciiLetter(name[0])
        && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
}
