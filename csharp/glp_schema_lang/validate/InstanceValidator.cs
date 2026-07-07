// Instance validator (feature 043-xsd-schema-language, T021; research R7).
//
// Contract: specs/043-xsd-schema-language/contracts/validation-api.md. Check order — which
// makes FR-007's "narrow, never widen" hold by construction:
//   1. kind resolution via the overlay registry (seed ∪ overlay); an unknown kind, or a kind
//      with no XSD-level source, throws NoSchemaRegisteredError (FR-008 — an error, distinct
//      from a Fail verdict, never a silent pass);
//   2. structure — element presence, closed-world (undeclared element = violation), sequence
//      order, exactly-one choice branch, occurs bounds;
//   3. facets — narrowing only.
// Every Violation names the violated element/facet, its schema location, and its path in the
// instance (US2 AS-2). Traversal is schema-directed: recursion depth is bounded by the schema
// DAG (documents are validated, hence acyclic), NOT by the instance — adversarially deep
// instances terminate with a located violation (spec edge case).

namespace GlpRuntime.SchemaLang;

public static class InstanceValidator
{
    /// <summary>Validate against the registered XSD-level schema for <paramref name="functor"/>.</summary>
    public static ValidationVerdict Validate(SchemaLangRegistry registry, string functor, InstanceValue instance)
    {
        var record = registry.LookupByFunctor(functor); // throws NoSchemaRegisteredError on unknown kind
        if (record.XsdSource is null)
            throw new NoSchemaRegisteredError(functor); // registered below, but no XSD-level schema (FR-008)
        var validated = SchemaValidator.Validate(record.XsdSource);
        if (!validated.IsValid)
            throw new InvalidOperationException(
                $"registry invariant broken: stored XSD source for '{functor}' no longer validates — " +
                string.Join("; ", validated.Errors.Select(e => e.ToString())));
        return Validate(validated.Document!, functor, instance);
    }

    /// <summary>Validate against an explicit (already validated) schema document.</summary>
    public static ValidationVerdict Validate(SchemaDocument document, string functor, InstanceValue instance)
    {
        var message = document.Messages.FirstOrDefault(m => m.Functor == functor)
            ?? throw new NoSchemaRegisteredError(functor);
        var types = document.Types.ToDictionary(t => t.Name);
        var violations = new List<Violation>();
        CheckComposition(types, functor, message.Location, message.Body, instance, string.Empty, violations);
        return violations.Count == 0 ? ValidationVerdict.Pass : ValidationVerdict.Fail(violations);
    }

    // ------------------------------------------------------------------
    // Structure
    // ------------------------------------------------------------------

    private static void CheckComposition(
        Dictionary<string, NamedType> types,
        string owner,
        SourceLocation ownerLocation,
        Composition composition,
        InstanceValue value,
        string path,
        List<Violation> violations)
    {
        if (value is not InstanceValue.Struct instance)
        {
            violations.Add(new Violation(ConstructKind.Composition, owner, ownerLocation.ToString(),
                PathOrRoot(path), $"expected a structure for '{owner}', found {Describe(value)}"));
            return;
        }

        if (composition.Kind == CompositionKind.Sequence)
            CheckSequence(types, owner, ownerLocation, composition, instance, path, violations);
        else
            CheckChoice(types, owner, ownerLocation, composition, instance, path, violations);
    }

    private static void CheckSequence(
        Dictionary<string, NamedType> types,
        string owner,
        SourceLocation ownerLocation,
        Composition composition,
        InstanceValue.Struct instance,
        string path,
        List<Violation> violations)
    {
        var elementIndex = new Dictionary<string, int>();
        for (var i = 0; i < composition.Elements.Count; i++)
            elementIndex[composition.Elements[i].Name] = i;

        var present = new HashSet<string>();
        var lastIndex = -1;
        var orderBroken = false;
        foreach (var field in instance.Fields)
        {
            if (!elementIndex.TryGetValue(field.Key, out var index))
            {
                violations.Add(new Violation(ConstructKind.Element, field.Key, ownerLocation.ToString(),
                    Join(path, field.Key), $"undeclared element '{field.Key}' in '{owner}' (closed-world)"));
                continue;
            }
            if (!present.Add(field.Key))
            {
                violations.Add(new Violation(ConstructKind.Element, field.Key, ownerLocation.ToString(),
                    Join(path, field.Key), $"element '{field.Key}' appears more than once"));
                continue;
            }
            if (index < lastIndex && !orderBroken)
            {
                orderBroken = true; // report the sequence-order break once, at its first witness
                violations.Add(new Violation(ConstructKind.Composition, owner, ownerLocation.ToString(),
                    Join(path, field.Key),
                    $"sequence order violated in '{owner}': element '{field.Key}' appears after later-declared elements"));
            }
            lastIndex = Math.Max(lastIndex, index);
            CheckElementValue(types, composition.Elements[index], field.Value, Join(path, field.Key), violations);
        }

        foreach (var element in composition.Elements)
            if (element.Occurs.Min >= 1 && !present.Contains(element.Name))
                violations.Add(new Violation(ConstructKind.Element, element.Name, element.Location.ToString(),
                    PathOrRoot(path), $"missing mandatory element '{element.Name}' in '{owner}'"));
    }

    private static void CheckChoice(
        Dictionary<string, NamedType> types,
        string owner,
        SourceLocation ownerLocation,
        Composition composition,
        InstanceValue.Struct instance,
        string path,
        List<Violation> violations)
    {
        if (instance.Fields.Count != 1)
            violations.Add(new Violation(ConstructKind.Composition, owner, ownerLocation.ToString(),
                PathOrRoot(path),
                $"choice '{owner}' requires exactly one branch, found {instance.Fields.Count}"));

        foreach (var field in instance.Fields)
        {
            var branch = composition.Elements.FirstOrDefault(e => e.Name == field.Key);
            if (branch is null)
            {
                violations.Add(new Violation(ConstructKind.Element, field.Key, ownerLocation.ToString(),
                    Join(path, field.Key), $"'{field.Key}' is not a branch of choice '{owner}'"));
                continue;
            }
            CheckElementValue(types, branch, field.Value, Join(path, field.Key), violations);
        }
    }

    private static void CheckElementValue(
        Dictionary<string, NamedType> types,
        ElementDecl element,
        InstanceValue value,
        string path,
        List<Violation> violations)
    {
        var occurs = element.Occurs;
        if (occurs.IsDefault || occurs.IsOptional)
        {
            CheckType(types, element.Type, value, path, violations);
            return;
        }

        // Repetition bounds beyond 1..1 / 0..1: the value is a list of the element type.
        if (value is not InstanceValue.List list)
        {
            violations.Add(new Violation(ConstructKind.Element, element.Name, element.Location.ToString(),
                path, $"element '{element.Name}' with occurs {occurs} expects a list, found {Describe(value)}"));
            return;
        }
        if (list.Items.Count < occurs.Min || (occurs.Max is int max && list.Items.Count > max))
            violations.Add(new Violation(ConstructKind.Element, element.Name, element.Location.ToString(),
                path, $"element '{element.Name}' has {list.Items.Count} item(s), outside occurs {occurs}"));
        for (var i = 0; i < list.Items.Count; i++)
            CheckType(types, element.Type, list.Items[i], $"{path}[{i}]", violations);
    }

    private static void CheckType(
        Dictionary<string, NamedType> types,
        TypeRef typeRef,
        InstanceValue value,
        string path,
        List<Violation> violations)
    {
        switch (typeRef)
        {
            case PrimitiveRef prim:
                CheckPrimitiveBase(prim.Kind, "(inline)", prim.Location, value, path, violations);
                break;
            case ListRef list:
                if (value is not InstanceValue.List items)
                {
                    violations.Add(new Violation(ConstructKind.Element, "[..]", list.Location.ToString(),
                        path, $"expected a list, found {Describe(value)}"));
                    break;
                }
                for (var i = 0; i < items.Items.Count; i++)
                    CheckType(types, list.Element, items.Items[i], $"{path}[{i}]", violations);
                break;
            case NamedRef named:
                // Documents are validated, so the reference resolves and the graph is a DAG —
                // this recursion is bounded by the schema, never by the instance.
                switch (types[named.Name])
                {
                    case SimpleType simple:
                        CheckSimple(simple, value, path, violations);
                        break;
                    case ComplexType complex:
                        CheckComposition(types, complex.Name, complex.Location, complex.Composition,
                            value, path, violations);
                        break;
                }
                break;
        }
    }

    // ------------------------------------------------------------------
    // Facets (narrowing only — checked after structure accepted the value)
    // ------------------------------------------------------------------

    private static void CheckSimple(
        SimpleType simple,
        InstanceValue value,
        string path,
        List<Violation> violations)
    {
        if (!CheckPrimitiveBase(simple.Base, simple.Name, simple.Location, value, path, violations))
            return;

        foreach (var facet in simple.Facets)
        {
            switch (facet)
            {
                case MinValueFacet min when value is InstanceValue.Int i && i.Value < min.Value:
                    Facet(facet, $"value {i.Value} is below min {min.Value} of type '{simple.Name}'");
                    break;
                case MaxValueFacet max when value is InstanceValue.Int i && i.Value > max.Value:
                    Facet(facet, $"value {i.Value} is above max {max.Value} of type '{simple.Name}'");
                    break;
                case MinLengthFacet minLen when Length(value) is int len && len < minLen.Value:
                    Facet(facet, $"length {len} is below minLength {minLen.Value} of type '{simple.Name}'");
                    break;
                case MaxLengthFacet maxLen when Length(value) is int len && len > maxLen.Value:
                    Facet(facet, $"length {len} is above maxLength {maxLen.Value} of type '{simple.Name}'");
                    break;
                case PatternFacet pattern when value is InstanceValue.Str s:
                {
                    var parsed = PatternNfa.Parse(pattern.Pattern);
                    if (parsed.Nfa is not null && !parsed.Nfa.Matches(s.Value))
                        Facet(facet, $"value \"{s.Value}\" does not match pattern \"{pattern.Pattern}\" of type '{simple.Name}'");
                    break;
                }
                case EnumerationFacet enumeration:
                {
                    var text = value switch
                    {
                        InstanceValue.Str s => s.Value,
                        InstanceValue.Int i => i.Value.ToString(),
                        _ => null,
                    };
                    if (text is not null && !enumeration.Members.Contains(text))
                        Facet(facet, $"value '{text}' is not an enumeration member of type '{simple.Name}'");
                    break;
                }
            }
        }

        void Facet(Facet facet, string message) =>
            violations.Add(new Violation(ConstructKind.Facet, facet.Keyword, facet.Location.ToString(),
                path, message));
    }

    private static bool CheckPrimitiveBase(
        PrimitiveKind kind,
        string typeName,
        SourceLocation location,
        InstanceValue value,
        string path,
        List<Violation> violations)
    {
        var ok = kind switch
        {
            PrimitiveKind.Int => value is InstanceValue.Int,
            PrimitiveKind.Str => value is InstanceValue.Str,
            PrimitiveKind.Bytes => value is InstanceValue.Bytes,
            PrimitiveKind.Bool => value is InstanceValue.Bool,
            _ => false,
        };
        if (!ok)
            violations.Add(new Violation(ConstructKind.Element, typeName, location.ToString(),
                path, $"expected {kind.ToString().ToLowerInvariant()}, found {Describe(value)}"));
        return ok;
    }

    private static int? Length(InstanceValue value) => value switch
    {
        InstanceValue.Str s => s.Value.Length,
        InstanceValue.Bytes b => b.Value.Count,
        _ => null,
    };

    private static string Describe(InstanceValue value) => value switch
    {
        InstanceValue.Int => "int",
        InstanceValue.Str => "str",
        InstanceValue.Bytes => "bytes",
        InstanceValue.Bool => "bool",
        InstanceValue.List => "list",
        InstanceValue.Struct s => $"struct '{s.Name}'",
        _ => value.GetType().Name,
    };

    private static string Join(string path, string name) =>
        path.Length == 0 ? name : $"{path}.{name}";

    private static string PathOrRoot(string path) => path.Length == 0 ? "(root)" : path;
}
