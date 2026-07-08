// Structural AST equivalence modulo canonical naming (feature 043, T023 helper; FR-010,
// contracts/lift-fidelity.md lift law 4a). Two documents are equivalent when their messages
// and named types have the same structure after every type name is canonicalized through the
// lowering rule-name law (lift renders canonical UpperCamel names, so `UserName` and
// `Username` both canonicalize to rule name `username`).

using GlpRuntime.SchemaLang;

namespace GlpRuntime.SchemaLang.Tests;

internal static class SchemaEquivalence
{
    public static void AssertEquivalent(SchemaDocument expected, SchemaDocument actual)
    {
        Assert.Equal(
            expected.Messages.Select(m => m.Functor),
            actual.Messages.Select(m => m.Functor));
        foreach (var (e, a) in expected.Messages.Zip(actual.Messages))
            AssertComposition($"message {e.Functor}", e.Body, a.Body);

        var expectedTypes = expected.Types.ToDictionary(t => CddlEmitter.RuleName(t.Name));
        var actualTypes = actual.Types.ToDictionary(t => CddlEmitter.RuleName(t.Name));
        Assert.Equal(
            expectedTypes.Keys.OrderBy(k => k, StringComparer.Ordinal),
            actualTypes.Keys.OrderBy(k => k, StringComparer.Ordinal));

        foreach (var (key, e) in expectedTypes)
        {
            var a = actualTypes[key];
            switch (e)
            {
                case SimpleType es:
                    var asimple = Assert.IsType<SimpleType>(a);
                    Assert.Equal(es.Base, asimple.Base);
                    Assert.Equal(NormalizedFacets(es), NormalizedFacets(asimple));
                    break;
                case ComplexType ec:
                    var acomplex = Assert.IsType<ComplexType>(a);
                    AssertComposition($"type {key}", ec.Composition, acomplex.Composition);
                    break;
            }
        }
    }

    private static void AssertComposition(string context, Composition expected, Composition actual)
    {
        Assert.True(expected.Kind == actual.Kind, $"{context}: composition kind differs");
        Assert.Equal(expected.Elements.Select(e => e.Name), actual.Elements.Select(e => e.Name));
        foreach (var (e, a) in expected.Elements.Zip(actual.Elements))
        {
            Assert.True(e.Occurs == a.Occurs, $"{context}.{e.Name}: occurs {e.Occurs} vs {a.Occurs}");
            Assert.True(TypeKey(e.Type) == TypeKey(a.Type),
                $"{context}.{e.Name}: type {TypeKey(e.Type)} vs {TypeKey(a.Type)}");
        }
    }

    private static string TypeKey(TypeRef typeRef) => typeRef switch
    {
        PrimitiveRef p => $"prim:{p.Kind}",
        NamedRef n => $"ref:{CddlEmitter.RuleName(n.Name)}",
        ListRef l => $"list<{TypeKey(l.Element)}>",
        _ => typeRef.GetType().Name,
    };

    private static IReadOnlyList<string> NormalizedFacets(SimpleType type) =>
        type.Facets
            .Select(f => f switch
            {
                MinValueFacet v => $"min:{v.Value}",
                MaxValueFacet v => $"max:{v.Value}",
                MinLengthFacet v => $"minLength:{v.Value}",
                MaxLengthFacet v => $"maxLength:{v.Value}",
                PatternFacet v => $"pattern:{v.Pattern}",
                EnumerationFacet v => $"enum:{string.Join(",", v.Members)}",
                _ => f.Keyword,
            })
            .Where(s => s != "minLength:0") // minLength 0 ≡ absent (the size default)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
}
