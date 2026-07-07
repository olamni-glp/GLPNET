// Schema-document validator (feature 043-xsd-schema-language, T009).
//
// Contract: specs/043-xsd-schema-language/contracts/validation-api.md +
// contracts/schema-dsl.md §Well-formedness (FR-002). All six rule groups run in ONE pass and
// ALL errors are reported (not first-error-only), each with construct + line:col location:
//   1. uniqueness (type names / element names per composition / functor names)
//   2. reference resolution (every type-ref resolves to a named type or primitive)
//   3. facet consistency (legal bases, min ≤ max, 0 ≤ minLength ≤ maxLength, enum non-empty +
//      distinct + type-valid + co-facet-satisfying, pattern in the restricted subset with a
//      non-empty language — research R6)
//   4. DAG check — any cycle (incl. self-reference) rejected naming the FULL cycle path
//      (clarification 2)
//   5. occurs bounds (0 ≤ min, min ≤ max when finite)
//   6. composition arity (sequence ≥ 1 element, choice ≥ 2)

namespace GlpRuntime.SchemaLang;

public static class SchemaValidator
{
    private const string Operation = "schema-validation";

    /// <summary>Parse + all well-formedness rules (validation-api.md).</summary>
    public static SchemaValidationResult Validate(string text)
    {
        var parsed = SchemaDslParser.Parse(text);
        if (parsed.Document is null)
            return SchemaValidationResult.Invalid(parsed.Errors);
        return ValidateDocument(parsed.Document);
    }

    /// <summary>All well-formedness rules over an already-parsed document.</summary>
    public static SchemaValidationResult ValidateDocument(SchemaDocument doc)
    {
        var errors = new List<SchemaValidationError>();
        var typesByName = CheckUniqueness(doc, errors);
        CheckReferencesAndCompositions(doc, typesByName, errors);
        CheckFacets(doc, errors);
        CheckAcyclicity(doc, typesByName, errors);
        return errors.Count > 0 ? SchemaValidationResult.Invalid(errors) : SchemaValidationResult.Ok(doc);
    }

    // ------------------------------------------------------------------
    // Rule group 1: uniqueness
    // ------------------------------------------------------------------

    private static Dictionary<string, NamedType> CheckUniqueness(SchemaDocument doc, List<SchemaValidationError> errors)
    {
        var typesByName = new Dictionary<string, NamedType>();
        foreach (var type in doc.Types)
        {
            if (!typesByName.TryAdd(type.Name, type))
                errors.Add(new SchemaValidationError(Operation, type.Name, type.Location,
                    $"duplicate type name '{type.Name}' — first defined at {typesByName[type.Name].Location}"));
        }
        var functors = new Dictionary<string, MessageDecl>();
        foreach (var msg in doc.Messages)
        {
            if (!functors.TryAdd(msg.Functor, msg))
                errors.Add(new SchemaValidationError(Operation, msg.Functor, msg.Location,
                    $"duplicate message functor '{msg.Functor}' — first declared at {functors[msg.Functor].Location}"));
        }
        return typesByName;
    }

    // ------------------------------------------------------------------
    // Rule groups 2, 5, 6 (+ element-name uniqueness of group 1)
    // ------------------------------------------------------------------

    private static void CheckReferencesAndCompositions(
        SchemaDocument doc,
        Dictionary<string, NamedType> typesByName,
        List<SchemaValidationError> errors)
    {
        foreach (var type in doc.Types)
            if (type is ComplexType complex)
                CheckComposition(complex.Name, complex.Location, complex.Composition, typesByName, errors);
        foreach (var msg in doc.Messages)
            CheckComposition(msg.Functor, msg.Location, msg.Body, typesByName, errors);
    }

    private static void CheckComposition(
        string owner,
        SourceLocation ownerLocation,
        Composition composition,
        Dictionary<string, NamedType> typesByName,
        List<SchemaValidationError> errors)
    {
        // Rule 6: composition arity.
        if (composition.Kind == CompositionKind.Sequence && composition.Elements.Count < 1)
            errors.Add(new SchemaValidationError(Operation, owner, ownerLocation,
                $"sequence in '{owner}' must contain at least 1 element"));
        if (composition.Kind == CompositionKind.Choice && composition.Elements.Count < 2)
            errors.Add(new SchemaValidationError(Operation, owner, ownerLocation,
                $"choice in '{owner}' must contain at least 2 elements"));

        // Rule 1: element names unique within one composition.
        var seen = new Dictionary<string, ElementDecl>();
        foreach (var element in composition.Elements)
        {
            if (!seen.TryAdd(element.Name, element))
                errors.Add(new SchemaValidationError(Operation, element.Name, element.Location,
                    $"duplicate element name '{element.Name}' in '{owner}' — first declared at {seen[element.Name].Location}"));

            // Rule 2: every type-ref resolves.
            CheckTypeRef(element.Type, typesByName, errors);

            // Rule 5: occurs bounds.
            if (element.Occurs.Min < 0)
                errors.Add(new SchemaValidationError(Operation, element.Name, element.Location,
                    $"occurs minimum {element.Occurs.Min} on element '{element.Name}' must be ≥ 0"));
            if (element.Occurs.Max is int max && max < element.Occurs.Min)
                errors.Add(new SchemaValidationError(Operation, element.Name, element.Location,
                    $"occurs bounds {element.Occurs} on element '{element.Name}' have minimum above maximum"));
        }
    }

    private static void CheckTypeRef(TypeRef typeRef, Dictionary<string, NamedType> typesByName, List<SchemaValidationError> errors)
    {
        switch (typeRef)
        {
            case NamedRef named when !typesByName.ContainsKey(named.Name):
                errors.Add(new SchemaValidationError(Operation, named.Name, named.Location,
                    $"unresolved type reference '{named.Name}' — no such named type in this document"));
                break;
            case ListRef list:
                CheckTypeRef(list.Element, typesByName, errors);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Rule group 3: facet consistency
    // ------------------------------------------------------------------

    private static void CheckFacets(SchemaDocument doc, List<SchemaValidationError> errors)
    {
        foreach (var type in doc.Types)
        {
            if (type is not SimpleType simple) continue;

            // Duplicate facet kinds are contradictions, not silent last-wins (FR-014).
            var byKeyword = new Dictionary<string, Facet>();
            foreach (var facet in simple.Facets)
            {
                if (!byKeyword.TryAdd(facet.Keyword, facet))
                    errors.Add(new SchemaValidationError(Operation, facet.Keyword, facet.Location,
                        $"duplicate facet '{facet.Keyword}' on type '{simple.Name}' — first given at {byKeyword[facet.Keyword].Location}"));
            }

            foreach (var facet in simple.Facets)
                CheckFacetBase(simple, facet, errors);

            var min = FacetOf<MinValueFacet>(simple);
            var max = FacetOf<MaxValueFacet>(simple);
            if (min is not null && max is not null && min.Value > max.Value)
                errors.Add(new SchemaValidationError(Operation, "min", min.Location,
                    $"facet contradiction on type '{simple.Name}': min {min.Value} exceeds max {max.Value}"));

            var minLen = FacetOf<MinLengthFacet>(simple);
            var maxLen = FacetOf<MaxLengthFacet>(simple);
            if (minLen is not null && minLen.Value < 0)
                errors.Add(new SchemaValidationError(Operation, "minLength", minLen.Location,
                    $"minLength {minLen.Value} on type '{simple.Name}' must be ≥ 0"));
            if (maxLen is not null && maxLen.Value < 0)
                errors.Add(new SchemaValidationError(Operation, "maxLength", maxLen.Location,
                    $"maxLength {maxLen.Value} on type '{simple.Name}' must be ≥ 0"));
            if (minLen is not null && maxLen is not null && minLen.Value > maxLen.Value)
                errors.Add(new SchemaValidationError(Operation, "minLength", minLen.Location,
                    $"facet contradiction on type '{simple.Name}': minLength {minLen.Value} exceeds maxLength {maxLen.Value}"));

            var pattern = FacetOf<PatternFacet>(simple);
            PatternNfa? nfa = null;
            if (pattern is not null && simple.Base == PrimitiveKind.Str)
            {
                var parsed = PatternNfa.Parse(pattern.Pattern);
                if (parsed.Error is not null)
                {
                    errors.Add(new SchemaValidationError(Operation, parsed.Error.Construct, pattern.Location,
                        $"pattern on type '{simple.Name}': {parsed.Error.Message}"));
                }
                else
                {
                    nfa = parsed.Nfa;
                    if (nfa!.IsEmptyLanguage())
                        errors.Add(new SchemaValidationError(Operation, "pattern", pattern.Location,
                            $"facet contradiction on type '{simple.Name}': pattern \"{pattern.Pattern}\" matches the empty language"));
                }
            }

            var enumeration = FacetOf<EnumerationFacet>(simple);
            if (enumeration is not null && (simple.Base is PrimitiveKind.Str or PrimitiveKind.Int))
                CheckEnumeration(simple, enumeration, min, max, minLen, maxLen, nfa, errors);
        }
    }

    private static void CheckFacetBase(SimpleType simple, Facet facet, List<SchemaValidationError> errors)
    {
        var legal = facet switch
        {
            MinValueFacet or MaxValueFacet => simple.Base == PrimitiveKind.Int,
            MinLengthFacet or MaxLengthFacet => simple.Base is PrimitiveKind.Str or PrimitiveKind.Bytes,
            PatternFacet => simple.Base == PrimitiveKind.Str,
            EnumerationFacet => simple.Base is PrimitiveKind.Str or PrimitiveKind.Int,
            _ => false,
        };
        if (!legal)
            errors.Add(new SchemaValidationError(Operation, facet.Keyword, facet.Location,
                $"facet '{facet.Keyword}' is not applicable to base '{simple.Base.ToString().ToLowerInvariant()}' on type '{simple.Name}'"));
    }

    private static void CheckEnumeration(
        SimpleType simple,
        EnumerationFacet enumeration,
        MinValueFacet? min,
        MaxValueFacet? max,
        MinLengthFacet? minLen,
        MaxLengthFacet? maxLen,
        PatternNfa? nfa,
        List<SchemaValidationError> errors)
    {
        if (enumeration.Members.Count == 0)
        {
            errors.Add(new SchemaValidationError(Operation, "enum", enumeration.Location,
                $"facet contradiction on type '{simple.Name}': enumeration is empty"));
            return;
        }

        var distinct = new HashSet<string>();
        foreach (var member in enumeration.Members)
        {
            if (!distinct.Add(member))
                errors.Add(new SchemaValidationError(Operation, "enum", enumeration.Location,
                    $"enumeration on type '{simple.Name}' repeats member '{member}'"));

            if (simple.Base == PrimitiveKind.Int)
            {
                if (!long.TryParse(member, out var value))
                {
                    errors.Add(new SchemaValidationError(Operation, "enum", enumeration.Location,
                        $"enumeration member '{member}' on int type '{simple.Name}' is not an integer"));
                    continue;
                }
                if (min is not null && value < min.Value)
                    errors.Add(new SchemaValidationError(Operation, "enum", enumeration.Location,
                        $"enumeration member {value} on type '{simple.Name}' violates co-facet min {min.Value}"));
                if (max is not null && value > max.Value)
                    errors.Add(new SchemaValidationError(Operation, "enum", enumeration.Location,
                        $"enumeration member {value} on type '{simple.Name}' violates co-facet max {max.Value}"));
            }
            else
            {
                if (minLen is not null && member.Length < minLen.Value)
                    errors.Add(new SchemaValidationError(Operation, "enum", enumeration.Location,
                        $"enumeration member '{member}' on type '{simple.Name}' violates co-facet minLength {minLen.Value}"));
                if (maxLen is not null && member.Length > maxLen.Value)
                    errors.Add(new SchemaValidationError(Operation, "enum", enumeration.Location,
                        $"enumeration member '{member}' on type '{simple.Name}' violates co-facet maxLength {maxLen.Value}"));
                if (nfa is not null && !nfa.Matches(member))
                    errors.Add(new SchemaValidationError(Operation, "enum", enumeration.Location,
                        $"enumeration member '{member}' on type '{simple.Name}' does not match the co-facet pattern"));
            }
        }
    }

    private static TFacet? FacetOf<TFacet>(SimpleType simple) where TFacet : Facet =>
        simple.Facets.OfType<TFacet>().FirstOrDefault();

    // ------------------------------------------------------------------
    // Rule group 4: the named-type reference graph is a DAG (clarification 2)
    // ------------------------------------------------------------------

    private static void CheckAcyclicity(
        SchemaDocument doc,
        Dictionary<string, NamedType> typesByName,
        List<SchemaValidationError> errors)
    {
        // Edges: complex type → every resolving named type it references.
        var adjacency = new Dictionary<string, List<string>>();
        foreach (var type in doc.Types)
        {
            if (adjacency.ContainsKey(type.Name)) continue; // duplicates already reported
            var targets = new List<string>();
            if (type is ComplexType complex)
                foreach (var element in complex.Composition.Elements)
                    CollectNamedRefs(element.Type, typesByName, targets);
            adjacency[type.Name] = targets;
        }

        // DFS with colors; a GRAY hit names the full cycle path (A → B → A). Roots are
        // visited in DOCUMENT order (FR-005/R11: no dictionary-enumeration ordering on any
        // contract path), so error order is deterministic.
        var color = adjacency.Keys.ToDictionary(n => n, _ => 0); // 0 white, 1 gray, 2 black
        var reported = new HashSet<string>();
        foreach (var type in doc.Types)
        {
            if (!color.TryGetValue(type.Name, out var c) || c != 0) continue;
            var path = new List<string>();
            Dfs(type.Name, path);
        }

        void Dfs(string node, List<string> path)
        {
            color[node] = 1;
            path.Add(node);
            foreach (var target in adjacency[node])
            {
                if (!color.ContainsKey(target)) continue;
                if (color[target] == 1)
                {
                    var start = path.IndexOf(target);
                    var cycleNodes = path.Skip(start).Append(target).ToList();
                    var cyclePath = string.Join(" → ", cycleNodes);
                    // Report each distinct cycle once, canonicalized by its member set.
                    var key = string.Join(",", cycleNodes.SkipLast(1).OrderBy(n => n, StringComparer.Ordinal));
                    if (reported.Add(key))
                        errors.Add(new SchemaValidationError(Operation, cyclePath, typesByName[target].Location,
                            $"cyclic type reference {cyclePath} — recursion is not supported; type references must form a DAG"));
                }
                else if (color[target] == 0)
                {
                    Dfs(target, path);
                }
            }
            path.RemoveAt(path.Count - 1);
            color[node] = 2;
        }
    }

    private static void CollectNamedRefs(TypeRef typeRef, Dictionary<string, NamedType> typesByName, List<string> targets)
    {
        switch (typeRef)
        {
            case NamedRef named when typesByName.ContainsKey(named.Name):
                targets.Add(named.Name);
                break;
            case ListRef list:
                CollectNamedRefs(list.Element, typesByName, targets);
                break;
        }
    }
}
