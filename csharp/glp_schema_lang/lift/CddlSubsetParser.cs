// CDDL-subset parser for lift (feature 043-xsd-schema-language, T025; research R10).
//
// Contract: specs/043-xsd-schema-language/contracts/lift-fidelity.md law 1 — the parser
// covers exactly the lowering emitter subset (contracts/lowering.md) PLUS the shipped
// crdt_message idioms (`&(…)` enums, `? key`, `[* t]`, `[a* t]`, `[a*b t]`, ranges, `.size`,
// `.regexp`, named rule refs, single-line and multi-line maps, optional trailing commas).
// Constructs outside the subset are captured VERBATIM with their location and become
// UnexpressibleConstruct entries (never approximation, FR-009): an out-of-subset entry value
// omits that entry; an out-of-subset rule omits that rule — every omission is a report entry.

namespace GlpRuntime.SchemaLang;

internal abstract record CddlType;

internal sealed record CddlEntry(string Key, CddlType Value, bool Optional);

internal sealed record CddlMap(IReadOnlyList<CddlEntry> Entries) : CddlType;

internal sealed record CddlChoice(IReadOnlyList<CddlMap> Branches) : CddlType;

internal sealed record CddlEnum(IReadOnlyList<(string Name, long Index)> Members) : CddlType;

internal sealed record CddlIntAlternates(IReadOnlyList<long> Values) : CddlType;

internal sealed record CddlRange(long Lo, long Hi) : CddlType;

/// <summary>int / uint / tstr / bstr / bool with optional .size and .regexp controls.</summary>
internal sealed record CddlPrim(string Base, (long Lo, ulong Hi)? Size, string? Regexp) : CddlType;

/// <summary><c>[* t]</c> (Min null), <c>[a* t]</c> (Max null), <c>[a*b t]</c>.</summary>
internal sealed record CddlArray(long? Min, long? Max, CddlType Element) : CddlType;

internal sealed record CddlRef(string Name) : CddlType;

internal sealed record CddlRule(string Name, CddlType Type, int Line);

internal sealed record CddlParseOutput(
    IReadOnlyList<CddlRule> Rules,
    IReadOnlyList<UnexpressibleConstruct> Unexpressible);

internal static class CddlSubsetParser
{
    private sealed class SubsetException : Exception
    {
        public SubsetException(string construct, string reason) : base(reason) => Construct = construct;

        public string Construct { get; }
    }

    public static CddlParseOutput Parse(string cddl)
    {
        var rules = new List<CddlRule>();
        var unexpressible = new List<UnexpressibleConstruct>();
        foreach (var (name, rhs, line) in SplitRules(cddl))
        {
            var scanner = new Scanner(rhs, line, unexpressible);
            try
            {
                var type = scanner.ParseValue();
                scanner.ExpectEnd();
                rules.Add(new CddlRule(name, type, line));
            }
            catch (SubsetException ex)
            {
                unexpressible.Add(new UnexpressibleConstruct(
                    $"{name} = {rhs.Trim()}", $"line {line}",
                    $"rule '{name}' is outside the expressible CDDL subset: {ex.Message} ('{ex.Construct}')"));
            }
        }
        return new CddlParseOutput(rules, unexpressible);
    }

    /// <summary>Split into rules: a rule starts at a line-leading `ident =` at bracket depth 0.
    /// Depth counting is STRING-AWARE: brackets inside `"…"` literals (e.g. a `.regexp "[(]"`
    /// pattern with unbalanced literal brackets) are text, not structure.</summary>
    private static IEnumerable<(string Name, string Rhs, int Line)> SplitRules(string cddl)
    {
        var lines = cddl.Split('\n');
        string? name = null;
        var rhs = new System.Text.StringBuilder();
        var startLine = 0;
        var depth = 0;
        var inString = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (depth == 0 && !inString && TryMatchRuleStart(line, out var newName, out var afterEq))
            {
                if (name is not null) yield return (name, rhs.ToString(), startLine);
                name = newName;
                rhs.Clear();
                rhs.Append(afterEq).Append('\n');
                startLine = i + 1;
            }
            else if (name is not null)
            {
                rhs.Append(line).Append('\n');
            }
            for (var j = 0; j < line.Length; j++)
            {
                var c = line[j];
                if (inString)
                {
                    if (c == '\\' && j + 1 < line.Length && line[j + 1] == '"') j++; // \" escape
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') inString = true;
                else depth += c is '{' or '[' or '(' ? 1 : c is '}' or ']' or ')' ? -1 : 0;
            }
        }
        if (name is not null) yield return (name, rhs.ToString(), startLine);
    }

    private static bool TryMatchRuleStart(string line, out string name, out string afterEq)
    {
        name = string.Empty;
        afterEq = string.Empty;
        var i = 0;
        if (line.Length == 0 || !(char.IsAsciiLetter(line[0]))) return false;
        while (i < line.Length && (char.IsAsciiLetterOrDigit(line[i]) || line[i] is '_' or '-' or '.' or '@' or '$')) i++;
        var end = i;
        while (i < line.Length && line[i] is ' ' or '\t') i++;
        if (i >= line.Length || line[i] != '=') return false;
        name = line[..end];
        afterEq = line[(i + 1)..];
        return true;
    }

    // ------------------------------------------------------------------
    // Scanner over one rule's right-hand side
    // ------------------------------------------------------------------

    private sealed class Scanner
    {
        private readonly string _text;
        private readonly int _baseLine;
        private readonly List<UnexpressibleConstruct> _unexpressible;
        private int _pos;

        public Scanner(string text, int baseLine, List<UnexpressibleConstruct> unexpressible)
        {
            _text = text;
            _baseLine = baseLine;
            _unexpressible = unexpressible;
        }

        private bool AtEnd => _pos >= _text.Length;

        private char Peek => _text[_pos];

        private int CurrentLine => _baseLine + _text.Take(_pos).Count(c => c == '\n');

        private void SkipWs()
        {
            while (!AtEnd && Peek is ' ' or '\t' or '\r' or '\n') _pos++;
        }

        public void ExpectEnd()
        {
            SkipWs();
            if (!AtEnd)
                throw new SubsetException(_text[_pos..].Trim(), "trailing content after the rule value");
        }

        public CddlType ParseValue()
        {
            SkipWs();
            if (AtEnd) throw new SubsetException(string.Empty, "missing rule value");
            switch (Peek)
            {
                case '{':
                {
                    var branches = new List<CddlMap> { ParseMap() };
                    while (true)
                    {
                        var save = _pos;
                        SkipWs();
                        if (!AtEnd && _pos + 1 < _text.Length && Peek == '/' && _text[_pos + 1] == '/')
                        {
                            _pos += 2;
                            SkipWs();
                            if (AtEnd || Peek != '{')
                                throw new SubsetException(Rest(), "choice branch must be a one-entry map");
                            branches.Add(ParseMap());
                        }
                        else
                        {
                            _pos = save;
                            break;
                        }
                    }
                    return branches.Count == 1 ? branches[0] : new CddlChoice(branches);
                }
                case '&':
                    return ParseEnum();
                case '[':
                    return ParseArray();
                default:
                    if (char.IsAsciiDigit(Peek) || Peek == '-') return ParseNumberish();
                    if (char.IsAsciiLetter(Peek)) return ParseIdentValue();
                    throw new SubsetException(Rest(), "unrecognized value form");
            }
        }

        private string Rest()
        {
            var rest = _text[_pos..].Trim();
            var newline = rest.IndexOf('\n');
            return newline >= 0 ? rest[..newline] : rest;
        }

        private CddlMap ParseMap()
        {
            Expect('{');
            var entries = new List<CddlEntry>();
            while (true)
            {
                SkipWs();
                if (AtEnd) throw new SubsetException("{", "unterminated map");
                if (Peek == '}')
                {
                    _pos++;
                    return new CddlMap(entries);
                }
                var optional = false;
                if (Peek == '?')
                {
                    _pos++;
                    optional = true;
                    SkipWs();
                }
                var key = ParseIdent("a map key");
                SkipWs();
                Expect(':');

                SkipWs();
                var valueStart = _pos;
                var valueLine = CurrentLine;
                CddlType? value;
                try
                {
                    value = ParseValue();
                }
                catch (SubsetException ex)
                {
                    // Entry-level capture: the offending value span verbatim; the entry is
                    // omitted from the map, and the omission IS the report entry (FR-009).
                    _pos = valueStart;
                    var span = CaptureValueSpan();
                    _unexpressible.Add(new UnexpressibleConstruct(
                        span.Trim(), $"line {valueLine}",
                        $"value of entry '{key}' is outside the expressible CDDL subset: {ex.Message}"));
                    value = null;
                }
                if (value is not null)
                    entries.Add(new CddlEntry(key, value, optional));

                SkipWs();
                if (!AtEnd && Peek == ',') _pos++;
            }
        }

        /// <summary>Scan the raw value span forward to the entry delimiter (',' or '}' at depth 0).
        /// String-aware: brackets and commas inside `"…"` literals are text, not structure.</summary>
        private string CaptureValueSpan()
        {
            var start = _pos;
            var depth = 0;
            var inString = false;
            while (!AtEnd)
            {
                var c = Peek;
                if (inString)
                {
                    if (c == '\\' && _pos + 1 < _text.Length && _text[_pos + 1] == '"') _pos++; // \" escape
                    else if (c == '"') inString = false;
                }
                else if (c == '"') inString = true;
                else if (c is '{' or '[' or '(') depth++;
                else if (c is ']' or ')') depth--;
                else if (c == '}')
                {
                    if (depth == 0) break;
                    depth--;
                }
                else if (c == ',' && depth == 0) break;
                _pos++;
            }
            return _text[start.._pos];
        }

        private CddlType ParseEnum()
        {
            Expect('&');
            SkipWs();
            Expect('(');
            var members = new List<(string, long)>();
            while (true)
            {
                SkipWs();
                var name = ParseIdent("an enum member name");
                SkipWs();
                Expect(':');
                SkipWs();
                var index = ParseLong();
                members.Add((name, index));
                SkipWs();
                if (!AtEnd && Peek == ',')
                {
                    _pos++;
                    continue;
                }
                Expect(')');
                return new CddlEnum(members);
            }
        }

        private CddlType ParseArray()
        {
            Expect('[');
            SkipWs();
            long? min = null;
            long? max = null;
            if (Peek == '*')
            {
                _pos++; // bare [* t]
            }
            else if (char.IsAsciiDigit(Peek))
            {
                min = ParseLong();
                Expect('*');
                if (!AtEnd && char.IsAsciiDigit(Peek)) max = ParseLong(); // [a*b t] — no space
            }
            else
            {
                throw new SubsetException(Rest(), "array must be [* t], [a* t], or [a*b t]");
            }
            var element = ParseValue();
            SkipWs();
            Expect(']');
            return new CddlArray(min, max, element);
        }

        private CddlType ParseNumberish()
        {
            var first = ParseLong();
            if (!AtEnd && _pos + 1 < _text.Length && Peek == '.' && _text[_pos + 1] == '.')
            {
                _pos += 2;
                var hi = ParseLong();
                return new CddlRange(first, hi);
            }
            var values = new List<long> { first };
            while (true)
            {
                var save = _pos;
                SkipWs();
                if (!AtEnd && Peek == '/' && (_pos + 1 >= _text.Length || _text[_pos + 1] != '/'))
                {
                    _pos++;
                    SkipWs();
                    values.Add(ParseLong());
                }
                else
                {
                    _pos = save;
                    break;
                }
            }
            return new CddlIntAlternates(values);
        }

        private CddlType ParseIdentValue()
        {
            var word = ParseIdent("a type name");
            if (word is not ("int" or "uint" or "tstr" or "bstr" or "bool"))
            {
                // Named rule ref; a control on a ref is outside the subset.
                var save = _pos;
                SkipWs();
                if (!AtEnd && Peek == '.' && _pos + 1 < _text.Length && char.IsAsciiLetter(_text[_pos + 1]))
                    throw new SubsetException(word + Rest(), "controls on a rule reference are outside the subset");
                _pos = save;
                return new CddlRef(word);
            }

            (long, ulong)? size = null;
            string? regexp = null;
            while (true)
            {
                var save = _pos;
                SkipWs();
                if (TryMatchWord(".size"))
                {
                    SkipWs();
                    Expect('(');
                    SkipWs();
                    var lo = ParseLong();
                    Expect('.');
                    Expect('.');
                    var hi = ParseULong();
                    SkipWs();
                    Expect(')');
                    size = (lo, hi);
                }
                else if (TryMatchWord(".regexp"))
                {
                    SkipWs();
                    regexp = ParseString();
                }
                else if (!AtEnd && Peek == '.' && _pos + 1 < _text.Length && char.IsAsciiLetter(_text[_pos + 1]))
                {
                    throw new SubsetException(Rest(), "only the .size and .regexp controls are in the subset");
                }
                else
                {
                    _pos = save;
                    break;
                }
            }
            return new CddlPrim(word, size, regexp);
        }

        private bool TryMatchWord(string word)
        {
            if (_pos + word.Length > _text.Length) return false;
            if (_text.AsSpan(_pos, word.Length).SequenceEqual(word))
            {
                _pos += word.Length;
                return true;
            }
            return false;
        }

        private string ParseIdent(string what)
        {
            if (AtEnd || !char.IsAsciiLetter(Peek))
                throw new SubsetException(Rest(), $"expected {what}");
            var start = _pos;
            while (!AtEnd && (char.IsAsciiLetterOrDigit(Peek) || Peek is '_' or '-')) _pos++;
            return _text[start.._pos];
        }

        private long ParseLong()
        {
            var start = _pos;
            if (!AtEnd && Peek == '-') _pos++;
            while (!AtEnd && char.IsAsciiDigit(Peek)) _pos++;
            if (_pos == start || !long.TryParse(_text.AsSpan(start, _pos - start), out var value))
                throw new SubsetException(Rest(), "expected an integer");
            return value;
        }

        private ulong ParseULong()
        {
            var start = _pos;
            while (!AtEnd && char.IsAsciiDigit(Peek)) _pos++;
            if (_pos == start || !ulong.TryParse(_text.AsSpan(start, _pos - start), out var value))
                throw new SubsetException(Rest(), "expected an unsigned integer");
            return value;
        }

        private string ParseString()
        {
            Expect('"');
            var sb = new System.Text.StringBuilder();
            while (!AtEnd && Peek != '"')
            {
                if (Peek == '\\' && _pos + 1 < _text.Length && _text[_pos + 1] == '"')
                {
                    sb.Append('"');
                    _pos += 2;
                    continue;
                }
                sb.Append(Peek);
                _pos++;
            }
            Expect('"');
            return sb.ToString();
        }

        private void Expect(char c)
        {
            if (AtEnd || Peek != c)
                throw new SubsetException(Rest(), $"expected '{c}'");
            _pos++;
        }
    }
}
