// 043 DSL pretty-printer (feature 043-xsd-schema-language, T026 support).
//
// Renders a SchemaDocument back to the authoring DSL (contracts/schema-dsl.md) so a lifted
// entry can be VIEWED in the XSD-style representation (US3). The printed text re-parses and
// re-validates to a document structurally equivalent to the input (asserted by T023).

namespace GlpRuntime.SchemaLang;

public static class SchemaDslPrinter
{
    public static string Print(SchemaDocument doc)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("schema ").Append(doc.Name).Append(" version ").Append(doc.Version).Append('\n');
        foreach (var type in doc.Types)
        {
            sb.Append('\n');
            switch (type)
            {
                case SimpleType simple:
                    sb.Append("type ").Append(simple.Name).Append(": ")
                      .Append(Primitive(simple.Base)).Append(" {");
                    foreach (var facet in simple.Facets)
                        sb.Append(' ').Append(FacetText(facet));
                    sb.Append(" }\n");
                    break;
                case ComplexType complex:
                    sb.Append("type ").Append(complex.Name).Append(" {\n");
                    PrintComposition(sb, complex.Composition);
                    sb.Append("}\n");
                    break;
            }
        }
        foreach (var message in doc.Messages)
        {
            sb.Append('\n');
            sb.Append("message ").Append(message.Functor).Append(" {\n");
            PrintComposition(sb, message.Body);
            sb.Append("}\n");
        }
        return sb.ToString();
    }

    private static void PrintComposition(System.Text.StringBuilder sb, Composition composition)
    {
        sb.Append("  ").Append(composition.Kind == CompositionKind.Sequence ? "sequence" : "choice").Append(" {\n");
        foreach (var element in composition.Elements)
        {
            sb.Append("    ").Append(element.Name);
            if (element.Occurs.IsOptional) sb.Append('?');
            sb.Append(": ").Append(TypeRefText(element.Type));
            if (!element.Occurs.IsDefault && !element.Occurs.IsOptional)
                sb.Append(" occurs ").Append(element.Occurs.Min).Append("..")
                  .Append(element.Occurs.Max is int max ? max.ToString() : "*");
            sb.Append('\n');
        }
        sb.Append("  }\n");
    }

    private static string TypeRefText(TypeRef typeRef) => typeRef switch
    {
        PrimitiveRef p => Primitive(p.Kind),
        NamedRef n => n.Name,
        ListRef l => $"[{TypeRefText(l.Element)}]",
        _ => throw new InvalidOperationException($"unknown type-ref {typeRef.GetType().Name}"),
    };

    private static string Primitive(PrimitiveKind kind) => kind switch
    {
        PrimitiveKind.Int => "int",
        PrimitiveKind.Str => "str",
        PrimitiveKind.Bytes => "bytes",
        PrimitiveKind.Bool => "bool",
        _ => throw new InvalidOperationException($"unknown primitive {kind}"),
    };

    private static string FacetText(Facet facet) => facet switch
    {
        MinValueFacet f => $"min {f.Value}",
        MaxValueFacet f => $"max {f.Value}",
        MinLengthFacet f => $"minLength {f.Value}",
        MaxLengthFacet f => $"maxLength {f.Value}",
        PatternFacet f => $"pattern \"{f.Pattern.Replace("\"", "\\\"")}\"",
        EnumerationFacet f => $"enum({string.Join(", ", f.Members.Select(MemberText))})",
        _ => throw new InvalidOperationException($"unknown facet {facet.Keyword}"),
    };

    /// <summary>An enum member prints unquoted ONLY when the DSL lexer tokenizes it as one
    /// identifier (`[A-Za-z_][A-Za-z0-9_]*` — parser/SchemaDslParser.cs); anything else (a
    /// `-`, a digit-leading name, …) is printed as a DSL string literal so the printed Source
    /// always re-parses to the same member text.</summary>
    private static string MemberText(string member) =>
        member.Length > 0
        && (char.IsAsciiLetter(member[0]) || member[0] == '_')
        && member.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
            ? member
            : $"\"{member.Replace("\"", "\\\"")}\"";
}
