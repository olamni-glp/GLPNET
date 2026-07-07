// DSL lexer + recursive-descent parser (feature 043-xsd-schema-language, T008).
//
// Contract: specs/043-xsd-schema-language/contracts/schema-dsl.md — the full grammar:
// `schema <name> version <n>` header, simple types `type T: prim { facets }`, complex types
// `type T { sequence|choice { elements } }`, `message f { composition }`, `?` optional sugar
// (suffix on the element name), `[T]` list refs, `occurs a..b|*`, `//` line comments,
// line:col tracking on every construct (FR-002/FR-014 localization).
//
// Parse errors are SchemaValidationError records (operation "parse"). The parser recovers at
// the next top-level `type`/`message` keyword so multiple parse errors in one document are
// all reported. Well-formedness (uniqueness, resolution, facets, DAG, occurs bounds,
// composition arity) is SchemaValidator's job (T009), not the parser's.

namespace GlpRuntime.SchemaLang;

public sealed record SchemaParseResult(SchemaDocument? Document, IReadOnlyList<SchemaValidationError> Errors);

public static class SchemaDslParser
{
    public static SchemaParseResult Parse(string text)
    {
        var errors = new List<SchemaValidationError>();
        var tokens = Lexer.Lex(text, errors);
        var doc = new Parser(text, tokens, errors).ParseDocument();
        return errors.Count > 0
            ? new SchemaParseResult(null, errors)
            : new SchemaParseResult(doc, errors);
    }

    private enum TokenKind
    {
        Ident,
        Int,
        Str,
        LBrace,
        RBrace,
        LBracket,
        RBracket,
        LParen,
        RParen,
        Colon,
        Comma,
        Question,
        DotDot,
        Star,
        Eof,
    }

    private readonly record struct Token(TokenKind Kind, string Text, long IntValue, string StrValue, int Line, int Col)
    {
        public SourceLocation Location => new(Line, Col);
    }

    // ------------------------------------------------------------------
    // Lexer
    // ------------------------------------------------------------------

    private static class Lexer
    {
        public static List<Token> Lex(string text, List<SchemaValidationError> errors)
        {
            var tokens = new List<Token>();
            int pos = 0, line = 1, col = 1;

            void Advance()
            {
                if (text[pos] == '\n')
                {
                    line++;
                    col = 1;
                }
                else
                {
                    col++;
                }
                pos++;
            }

            while (pos < text.Length)
            {
                var c = text[pos];
                var startLine = line;
                var startCol = col;

                if (c is ' ' or '\t' or '\r' or '\n')
                {
                    Advance();
                    continue;
                }
                if (c == '/' && pos + 1 < text.Length && text[pos + 1] == '/')
                {
                    while (pos < text.Length && text[pos] != '\n') Advance();
                    continue;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    var start = pos;
                    while (pos < text.Length && (char.IsLetterOrDigit(text[pos]) || text[pos] == '_')) Advance();
                    tokens.Add(new Token(TokenKind.Ident, text[start..pos], 0, string.Empty, startLine, startCol));
                    continue;
                }
                if (char.IsDigit(c) || (c == '-' && pos + 1 < text.Length && char.IsDigit(text[pos + 1])))
                {
                    var start = pos;
                    if (c == '-') Advance();
                    while (pos < text.Length && char.IsDigit(text[pos])) Advance();
                    var lexeme = text[start..pos];
                    if (!long.TryParse(lexeme, out var value))
                    {
                        errors.Add(new SchemaValidationError("parse", lexeme, new SourceLocation(startLine, startCol),
                            $"integer literal '{lexeme}' out of range"));
                        value = 0;
                    }
                    tokens.Add(new Token(TokenKind.Int, lexeme, value, string.Empty, startLine, startCol));
                    continue;
                }
                if (c == '"')
                {
                    // String literal: `\"` escapes a quote; every other character (including
                    // backslashes) is verbatim, so regex pattern text passes through unmangled.
                    Advance();
                    var sb = new System.Text.StringBuilder();
                    var closed = false;
                    while (pos < text.Length && text[pos] != '\n')
                    {
                        if (text[pos] == '\\' && pos + 1 < text.Length && text[pos + 1] == '"')
                        {
                            sb.Append('"');
                            Advance();
                            Advance();
                            continue;
                        }
                        if (text[pos] == '"')
                        {
                            Advance();
                            closed = true;
                            break;
                        }
                        sb.Append(text[pos]);
                        Advance();
                    }
                    if (!closed)
                        errors.Add(new SchemaValidationError("parse", "\"", new SourceLocation(startLine, startCol),
                            "unterminated string literal"));
                    tokens.Add(new Token(TokenKind.Str, sb.ToString(), 0, sb.ToString(), startLine, startCol));
                    continue;
                }

                TokenKind? kind = c switch
                {
                    '{' => TokenKind.LBrace,
                    '}' => TokenKind.RBrace,
                    '[' => TokenKind.LBracket,
                    ']' => TokenKind.RBracket,
                    '(' => TokenKind.LParen,
                    ')' => TokenKind.RParen,
                    ':' => TokenKind.Colon,
                    ',' => TokenKind.Comma,
                    '?' => TokenKind.Question,
                    '*' => TokenKind.Star,
                    _ => null,
                };
                if (kind is not null)
                {
                    tokens.Add(new Token(kind.Value, c.ToString(), 0, string.Empty, startLine, startCol));
                    Advance();
                    continue;
                }
                if (c == '.' && pos + 1 < text.Length && text[pos + 1] == '.')
                {
                    tokens.Add(new Token(TokenKind.DotDot, "..", 0, string.Empty, startLine, startCol));
                    Advance();
                    Advance();
                    continue;
                }

                errors.Add(new SchemaValidationError("parse", c.ToString(), new SourceLocation(startLine, startCol),
                    $"unexpected character '{c}'"));
                Advance();
            }

            tokens.Add(new Token(TokenKind.Eof, string.Empty, 0, string.Empty, line, col));
            return tokens;
        }
    }

    // ------------------------------------------------------------------
    // Parser (with top-level sync recovery so several errors surface at once)
    // ------------------------------------------------------------------

    private sealed class Parser
    {
        private static readonly string[] Primitives = { "int", "str", "bytes", "bool" };

        private readonly string _source;
        private readonly List<Token> _tokens;
        private readonly List<SchemaValidationError> _errors;
        private int _index;

        public Parser(string source, List<Token> tokens, List<SchemaValidationError> errors)
        {
            _source = source;
            _tokens = tokens;
            _errors = errors;
        }

        private Token Current => _tokens[_index];

        private Token Next()
        {
            var t = Current;
            if (t.Kind != TokenKind.Eof) _index++;
            return t;
        }

        private void Error(string construct, SourceLocation location, string message) =>
            _errors.Add(new SchemaValidationError("parse", construct, location, message));

        private sealed class ParseAbort : Exception { }

        private Token Expect(TokenKind kind, string what)
        {
            if (Current.Kind == kind) return Next();
            Error(Current.Text, Current.Location, $"expected {what}, found '{DescribeCurrent()}'");
            throw new ParseAbort();
        }

        private Token ExpectKeyword(string keyword)
        {
            if (Current.Kind == TokenKind.Ident && Current.Text == keyword) return Next();
            Error(Current.Text, Current.Location, $"expected '{keyword}', found '{DescribeCurrent()}'");
            throw new ParseAbort();
        }

        private string DescribeCurrent() => Current.Kind == TokenKind.Eof ? "end of document" : Current.Text;

        public SchemaDocument? ParseDocument()
        {
            string name;
            int version;
            try
            {
                ExpectKeyword("schema");
                name = Expect(TokenKind.Ident, "a schema name").Text;
                ExpectKeyword("version");
                version = (int)Expect(TokenKind.Int, "a version number").IntValue;
            }
            catch (ParseAbort)
            {
                return null;
            }

            var types = new List<NamedType>();
            var messages = new List<MessageDecl>();
            while (Current.Kind != TokenKind.Eof)
            {
                try
                {
                    if (Current.Kind == TokenKind.Ident && Current.Text == "type")
                    {
                        types.Add(ParseNamedType());
                    }
                    else if (Current.Kind == TokenKind.Ident && Current.Text == "message")
                    {
                        messages.Add(ParseMessage());
                    }
                    else
                    {
                        Error(Current.Text, Current.Location,
                            $"expected 'type' or 'message', found '{DescribeCurrent()}'");
                        throw new ParseAbort();
                    }
                }
                catch (ParseAbort)
                {
                    SyncToTopLevel();
                }
            }
            return new SchemaDocument(name, version, types, messages, _source);
        }

        /// <summary>Skip to the next top-level `type`/`message` (at brace depth 0) or EOF.</summary>
        private void SyncToTopLevel()
        {
            var depth = 0;
            while (Current.Kind != TokenKind.Eof)
            {
                if (Current.Kind == TokenKind.LBrace) depth++;
                else if (Current.Kind == TokenKind.RBrace && depth > 0) depth--;
                else if (depth == 0 && Current.Kind == TokenKind.Ident && Current.Text is "type" or "message")
                    return;
                Next();
            }
        }

        private NamedType ParseNamedType()
        {
            ExpectKeyword("type");
            var nameTok = Expect(TokenKind.Ident, "a type name");
            if (!char.IsUpper(nameTok.Text[0]))
                Error(nameTok.Text, nameTok.Location, $"type name '{nameTok.Text}' must be UpperCamel");

            if (Current.Kind == TokenKind.Colon)
            {
                Next();
                var primTok = Expect(TokenKind.Ident, "a primitive type (int, str, bytes, bool)");
                var prim = ParsePrimitiveKind(primTok);
                Expect(TokenKind.LBrace, "'{'");
                var facets = ParseFacets();
                Expect(TokenKind.RBrace, "'}'");
                return new SimpleType(nameTok.Text, nameTok.Location, prim, facets);
            }

            Expect(TokenKind.LBrace, "':' or '{'");
            var composition = ParseComposition();
            Expect(TokenKind.RBrace, "'}'");
            return new ComplexType(nameTok.Text, nameTok.Location, composition);
        }

        private MessageDecl ParseMessage()
        {
            ExpectKeyword("message");
            var nameTok = Expect(TokenKind.Ident, "a functor name");
            if (!IsLowerSnake(nameTok.Text))
                Error(nameTok.Text, nameTok.Location, $"functor name '{nameTok.Text}' must be lower_snake");
            Expect(TokenKind.LBrace, "'{'");
            var composition = ParseComposition();
            Expect(TokenKind.RBrace, "'}'");
            return new MessageDecl(nameTok.Text, composition, nameTok.Location);
        }

        private Composition ParseComposition()
        {
            var kindTok = Expect(TokenKind.Ident, "'sequence' or 'choice'");
            CompositionKind kind;
            switch (kindTok.Text)
            {
                case "sequence":
                    kind = CompositionKind.Sequence;
                    break;
                case "choice":
                    kind = CompositionKind.Choice;
                    break;
                default:
                    Error(kindTok.Text, kindTok.Location,
                        $"expected 'sequence' or 'choice', found '{kindTok.Text}'");
                    throw new ParseAbort();
            }
            Expect(TokenKind.LBrace, "'{'");
            var elements = new List<ElementDecl>();
            while (Current.Kind != TokenKind.RBrace && Current.Kind != TokenKind.Eof)
                elements.Add(ParseElement());
            Expect(TokenKind.RBrace, "'}'");
            return new Composition(kind, elements);
        }

        private ElementDecl ParseElement()
        {
            var nameTok = Expect(TokenKind.Ident, "an element name");
            if (!IsLowerSnake(nameTok.Text))
                Error(nameTok.Text, nameTok.Location, $"element name '{nameTok.Text}' must be lower_snake");

            var optionalSugar = false;
            if (Current.Kind == TokenKind.Question)
            {
                Next();
                optionalSugar = true;
            }
            Expect(TokenKind.Colon, "':'");
            var typeRef = ParseTypeRef();

            Occurs occurs;
            if (Current.Kind == TokenKind.Ident && Current.Text == "occurs")
            {
                var occursTok = Next();
                var min = (int)Expect(TokenKind.Int, "the occurs minimum").IntValue;
                Expect(TokenKind.DotDot, "'..'");
                int? max;
                if (Current.Kind == TokenKind.Star)
                {
                    Next();
                    max = null;
                }
                else
                {
                    max = (int)Expect(TokenKind.Int, "the occurs maximum or '*'").IntValue;
                }
                if (optionalSugar)
                {
                    Error(nameTok.Text, occursTok.Location,
                        $"element '{nameTok.Text}' has both the '?' optional sugar and an explicit occurs clause");
                    throw new ParseAbort();
                }
                occurs = new Occurs(min, max);
            }
            else
            {
                occurs = optionalSugar ? Occurs.Optional : Occurs.One;
            }
            return new ElementDecl(nameTok.Text, typeRef, occurs, nameTok.Location);
        }

        private TypeRef ParseTypeRef()
        {
            if (Current.Kind == TokenKind.LBracket)
            {
                var open = Next();
                var inner = ParseTypeRef();
                Expect(TokenKind.RBracket, "']'");
                return new ListRef(inner, open.Location);
            }
            var tok = Expect(TokenKind.Ident, "a type reference");
            if (char.IsUpper(tok.Text[0]))
                return new NamedRef(tok.Text, tok.Location);
            if (Primitives.Contains(tok.Text))
                return new PrimitiveRef(ParsePrimitiveKind(tok), tok.Location);
            Error(tok.Text, tok.Location,
                $"'{tok.Text}' is not a primitive (int, str, bytes, bool) or an UpperCamel type name");
            throw new ParseAbort();
        }

        private PrimitiveKind ParsePrimitiveKind(Token tok)
        {
            switch (tok.Text)
            {
                case "int": return PrimitiveKind.Int;
                case "str": return PrimitiveKind.Str;
                case "bytes": return PrimitiveKind.Bytes;
                case "bool": return PrimitiveKind.Bool;
                default:
                    Error(tok.Text, tok.Location,
                        $"'{tok.Text}' is not a primitive type (int, str, bytes, bool)");
                    throw new ParseAbort();
            }
        }

        private IReadOnlyList<Facet> ParseFacets()
        {
            var facets = new List<Facet>();
            while (Current.Kind != TokenKind.RBrace && Current.Kind != TokenKind.Eof)
            {
                var tok = Expect(TokenKind.Ident, "a facet keyword (min, max, minLength, maxLength, pattern, enum)");
                switch (tok.Text)
                {
                    case "min":
                        facets.Add(new MinValueFacet(Expect(TokenKind.Int, "the min value").IntValue, tok.Location));
                        break;
                    case "max":
                        facets.Add(new MaxValueFacet(Expect(TokenKind.Int, "the max value").IntValue, tok.Location));
                        break;
                    case "minLength":
                        facets.Add(new MinLengthFacet((int)Expect(TokenKind.Int, "the minLength value").IntValue, tok.Location));
                        break;
                    case "maxLength":
                        facets.Add(new MaxLengthFacet((int)Expect(TokenKind.Int, "the maxLength value").IntValue, tok.Location));
                        break;
                    case "pattern":
                        facets.Add(new PatternFacet(Expect(TokenKind.Str, "the pattern string").StrValue, tok.Location));
                        break;
                    case "enum":
                    {
                        Expect(TokenKind.LParen, "'('");
                        var members = new List<string> { ParseEnumLiteral() };
                        while (Current.Kind == TokenKind.Comma)
                        {
                            Next();
                            members.Add(ParseEnumLiteral());
                        }
                        Expect(TokenKind.RParen, "')'");
                        facets.Add(new EnumerationFacet(members, tok.Location));
                        break;
                    }
                    default:
                        Error(tok.Text, tok.Location,
                            $"'{tok.Text}' is not a facet keyword (min, max, minLength, maxLength, pattern, enum)");
                        throw new ParseAbort();
                }
            }
            return facets;
        }

        private string ParseEnumLiteral()
        {
            if (Current.Kind is TokenKind.Ident or TokenKind.Int)
                return Next().Text;
            if (Current.Kind == TokenKind.Str)
                return Next().StrValue;
            Error(Current.Text, Current.Location,
                $"expected an enum member (identifier, integer, or string), found '{DescribeCurrent()}'");
            throw new ParseAbort();
        }

        private static bool IsLowerSnake(string name) =>
            char.IsLower(name[0]) && name.All(c => char.IsLower(c) || char.IsDigit(c) || c == '_');
    }
}
