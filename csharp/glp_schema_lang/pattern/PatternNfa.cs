// Restricted-regex NFA engine (feature 043-xsd-schema-language, T006; research R6).
//
// Contract: specs/043-xsd-schema-language/contracts/schema-dsl.md §Restricted pattern subset.
// Supported: literal chars, escapes \. \\ \-, '.', character classes [a-z0-9_] (ranges,
// negation [^…]), grouping ( ), alternation |, quantifiers * + ? {n} {n,m}. Implicitly
// anchored both ends. Anything else (backreferences, lookaround, named groups, flags, perl
// class escapes, explicit anchors, open-ended {n,}) is a parse error NAMING the construct —
// loud, never silently accepted (FR-014).
//
// Emptiness is decided by accept-state reachability over the NFA (an empty-language pattern
// is a facet-consistency error, FR-002). Instance-time matching is a linear-time NFA
// simulation — no backtracking, bounded and deterministic on adversarial input (spec edge
// case). Language inclusion (for compat checking, contracts/compat-evolution.md †) runs a
// product walk of the left NFA against the lazily-determinized right NFA over a shared
// alphabet partition; if the state space exceeds a fixed cap the result is Unknown and the
// caller treats the change as conservatively breaking.

namespace GlpRuntime.SchemaLang;

/// <summary>A pattern-subset error naming the unsupported/ill-formed construct (R6).</summary>
public sealed record PatternError(string Construct, int Position, string Message);

public sealed record PatternParseResult(PatternNfa? Nfa, PatternError? Error);

public enum PatternInclusion
{
    Subset,
    NotSubset,
    /// <summary>Inclusion could not be established within bounds — conservatively breaking.</summary>
    Unknown,
}

public sealed class PatternNfa
{
    private const int InclusionStateCap = 100_000;

    // States 0..N-1. A transition with Set == null is an epsilon transition.
    private readonly List<List<Transition>> _states;
    private readonly int _start;
    private readonly int _accept;

    private PatternNfa(List<List<Transition>> states, int start, int accept, string pattern)
    {
        _states = states;
        _start = start;
        _accept = accept;
        Pattern = pattern;
    }

    /// <summary>The source pattern text.</summary>
    public string Pattern { get; }

    private readonly record struct Transition(CharSet? Set, int Target);

    // ------------------------------------------------------------------
    // Parse
    // ------------------------------------------------------------------

    public static PatternParseResult Parse(string pattern)
    {
        try
        {
            var node = new Parser(pattern).ParsePattern();
            var builder = new Builder();
            var (start, accept) = builder.Build(node);
            return new PatternParseResult(new PatternNfa(builder.States, start, accept, pattern), null);
        }
        catch (PatternParseException ex)
        {
            return new PatternParseResult(null, ex.Error);
        }
    }

    // ------------------------------------------------------------------
    // Emptiness (accept-state reachability)
    // ------------------------------------------------------------------

    public bool IsEmptyLanguage()
    {
        var seen = new bool[_states.Count];
        var queue = new Queue<int>();
        seen[_start] = true;
        queue.Enqueue(_start);
        while (queue.Count > 0)
        {
            var s = queue.Dequeue();
            if (s == _accept) return false;
            foreach (var t in _states[s])
            {
                if (t.Set is { IsEmpty: true }) continue; // unsatisfiable transition
                if (!seen[t.Target])
                {
                    seen[t.Target] = true;
                    queue.Enqueue(t.Target);
                }
            }
        }
        return true;
    }

    // ------------------------------------------------------------------
    // Anchored match (linear-time NFA simulation)
    // ------------------------------------------------------------------

    public bool Matches(string input)
    {
        var current = EpsilonClosure(new[] { _start });
        foreach (var c in input)
        {
            current = EpsilonClosure(Move(current, c));
            if (current.Length == 0) return false;
        }
        return Array.IndexOf(current, _accept) >= 0;
    }

    private int[] Move(int[] states, char c)
    {
        var next = new SortedSet<int>();
        foreach (var s in states)
            foreach (var t in _states[s])
                if (t.Set is not null && t.Set.Contains(c))
                    next.Add(t.Target);
        return next.ToArray();
    }

    private int[] EpsilonClosure(int[] states)
    {
        var closure = new SortedSet<int>(states);
        var stack = new Stack<int>(states);
        while (stack.Count > 0)
        {
            var s = stack.Pop();
            foreach (var t in _states[s])
                if (t.Set is null && closure.Add(t.Target))
                    stack.Push(t.Target);
        }
        return closure.ToArray();
    }

    // ------------------------------------------------------------------
    // Language inclusion: L(a) ⊆ L(b) ?
    // ------------------------------------------------------------------

    public static PatternInclusion IsSubsetOf(PatternNfa a, PatternNfa b)
    {
        // Alphabet partition: within each interval every char set of both NFAs is constant.
        var cuts = new SortedSet<int>();
        foreach (var nfa in new[] { a, b })
            foreach (var state in nfa._states)
                foreach (var t in state)
                    if (t.Set is not null)
                        foreach (var (lo, hi) in t.Set.Ranges)
                        {
                            cuts.Add(lo);
                            cuts.Add(hi + 1);
                        }
        var cutList = cuts.ToList();
        var symbols = new List<char>();
        for (var i = 0; i + 1 < cutList.Count; i++)
            symbols.Add((char)cutList[i]);

        var startA = a.EpsilonClosure(new[] { a._start });
        var startB = b.EpsilonClosure(new[] { b._start });

        var visited = new HashSet<string>();
        var queue = new Queue<(int[] A, int[] B)>();
        visited.Add(Key(startA, startB));
        queue.Enqueue((startA, startB));

        while (queue.Count > 0)
        {
            var (sa, sb) = queue.Dequeue();
            if (Array.IndexOf(sa, a._accept) >= 0 && Array.IndexOf(sb, b._accept) < 0)
                return PatternInclusion.NotSubset;

            foreach (var c in symbols)
            {
                var na = a.EpsilonClosure(a.Move(sa, c));
                if (na.Length == 0) continue; // a is dead on this path — cannot accept beyond
                var nb = b.EpsilonClosure(b.Move(sb, c));
                var key = Key(na, nb);
                if (visited.Add(key))
                {
                    if (visited.Count > InclusionStateCap) return PatternInclusion.Unknown;
                    queue.Enqueue((na, nb));
                }
            }
        }
        return PatternInclusion.Subset;

        static string Key(int[] sa, int[] sb) => string.Join(',', sa) + "|" + string.Join(',', sb);
    }

    // ------------------------------------------------------------------
    // Character sets: sorted, disjoint, merged ranges over the BMP
    // ------------------------------------------------------------------

    private sealed class CharSet
    {
        public CharSet(IEnumerable<(char Lo, char Hi)> ranges)
        {
            Ranges = Normalize(ranges);
        }

        public (char Lo, char Hi)[] Ranges { get; }

        public bool IsEmpty => Ranges.Length == 0;

        public static CharSet Single(char c) => new(new[] { (c, c) });

        public static CharSet Any { get; } = new(new[] { ((char)0, (char)0xFFFF) });

        public bool Contains(char c)
        {
            foreach (var (lo, hi) in Ranges)
            {
                if (c < lo) return false;
                if (c <= hi) return true;
            }
            return false;
        }

        public CharSet Negate()
        {
            var result = new List<(char, char)>();
            var next = 0;
            foreach (var (lo, hi) in Ranges)
            {
                if (lo > next) result.Add(((char)next, (char)(lo - 1)));
                next = hi + 1;
            }
            if (next <= 0xFFFF) result.Add(((char)next, (char)0xFFFF));
            return new CharSet(result);
        }

        private static (char, char)[] Normalize(IEnumerable<(char Lo, char Hi)> ranges)
        {
            var sorted = ranges.Where(r => r.Lo <= r.Hi).OrderBy(r => r.Lo).ThenBy(r => r.Hi).ToList();
            var merged = new List<(char Lo, char Hi)>();
            foreach (var r in sorted)
            {
                if (merged.Count > 0 && r.Lo <= merged[^1].Hi + 1)
                {
                    if (r.Hi > merged[^1].Hi) merged[^1] = (merged[^1].Lo, r.Hi);
                }
                else
                {
                    merged.Add(r);
                }
            }
            return merged.ToArray();
        }
    }

    // ------------------------------------------------------------------
    // Regex AST (internal to the engine)
    // ------------------------------------------------------------------

    private abstract record Node;

    private sealed record EmptyNode : Node;

    private sealed record CharNode(CharSet Set) : Node;

    private sealed record ConcatNode(Node Left, Node Right) : Node;

    private sealed record AltNode(Node Left, Node Right) : Node;

    private sealed record StarNode(Node Inner) : Node;

    /// <summary>Finite counted repeat {n} / {n,m} — expanded structurally at build time.</summary>
    private sealed record RepeatNode(Node Inner, int Min, int Max) : Node;

    private sealed class PatternParseException : Exception
    {
        public PatternParseException(PatternError error) : base(error.Message) => Error = error;

        public PatternError Error { get; }
    }

    // ------------------------------------------------------------------
    // Recursive-descent parser over the restricted subset
    // ------------------------------------------------------------------

    private sealed class Parser
    {
        private readonly string _text;
        private int _pos;

        public Parser(string text) => _text = text;

        private bool AtEnd => _pos >= _text.Length;

        private char Peek => _text[_pos];

        public Node ParsePattern()
        {
            var node = ParseAlternation();
            if (!AtEnd)
                throw Error(Peek.ToString(), _pos, $"unexpected '{Peek}'");
            return node;
        }

        private static PatternParseException Error(string construct, int pos, string message) =>
            new(new PatternError(construct, pos, $"unsupported or ill-formed pattern construct '{construct}': {message}"));

        private Node ParseAlternation()
        {
            var node = ParseConcat();
            while (!AtEnd && Peek == '|')
            {
                _pos++;
                node = new AltNode(node, ParseConcat());
            }
            return node;
        }

        private Node ParseConcat()
        {
            Node node = new EmptyNode();
            var first = true;
            while (!AtEnd && Peek != '|' && Peek != ')')
            {
                var atom = ParseRepeat();
                node = first ? atom : new ConcatNode(node, atom);
                first = false;
            }
            return node;
        }

        private Node ParseRepeat()
        {
            var node = ParseAtom();
            while (!AtEnd)
            {
                switch (Peek)
                {
                    case '*':
                        _pos++;
                        node = new StarNode(node);
                        break;
                    case '+':
                        _pos++;
                        node = new ConcatNode(node, new StarNode(node));
                        break;
                    case '?':
                        _pos++;
                        node = new AltNode(node, new EmptyNode());
                        break;
                    case '{':
                        node = ParseCountedRepeat(node);
                        break;
                    default:
                        return node;
                }
            }
            return node;
        }

        private Node ParseCountedRepeat(Node inner)
        {
            var open = _pos;
            _pos++; // '{'
            var min = ParseInt(open);
            var max = min;
            if (!AtEnd && Peek == ',')
            {
                _pos++;
                if (!AtEnd && Peek == '}')
                {
                    // {n,} — open-ended counted repeat is not in the subset (schema-dsl.md).
                    var construct = _text.Substring(open, _pos - open + 1);
                    throw Error(construct, open, "open-ended counted repeat {n,} is not in the restricted subset");
                }
                max = ParseInt(open);
            }
            if (AtEnd || Peek != '}')
                throw Error(_text.Substring(open, Math.Min(_text.Length, _pos + 1) - open), open, "unterminated counted repeat");
            _pos++; // '}'
            if (min > max)
                throw Error(_text.Substring(open, _pos - open), open, $"counted repeat minimum {min} exceeds maximum {max}");
            return new RepeatNode(inner, min, max);
        }

        private int ParseInt(int open)
        {
            var start = _pos;
            while (!AtEnd && Peek is >= '0' and <= '9') _pos++;
            if (_pos == start)
                throw Error(_text.Substring(open, Math.Min(_text.Length, _pos + 1) - open), open, "expected a number in counted repeat");
            return int.Parse(_text.AsSpan(start, _pos - start));
        }

        private Node ParseAtom()
        {
            if (AtEnd)
                throw Error(string.Empty, _pos, "expected an atom at end of pattern");
            var c = Peek;
            switch (c)
            {
                case '(':
                    if (_pos + 1 < _text.Length && _text[_pos + 1] == '?')
                        throw Error("(?", _pos, "lookaround / named / non-capturing groups are not in the restricted subset");
                    _pos++;
                    var inner = ParseAlternation();
                    if (AtEnd || Peek != ')')
                        throw Error("(", _pos, "unbalanced group — missing ')'");
                    _pos++;
                    return inner;
                case ')':
                    throw Error(")", _pos, "unbalanced ')'");
                case '[':
                    return ParseClass();
                case '.':
                    _pos++;
                    return new CharNode(CharSet.Any);
                case '\\':
                    return new CharNode(CharSet.Single(ParseEscape()));
                case '^':
                    throw Error("^", _pos, "explicit anchors are not in the subset — patterns are implicitly anchored");
                case '$':
                    throw Error("$", _pos, "explicit anchors are not in the subset — patterns are implicitly anchored");
                case '*' or '+' or '?':
                    throw Error(c.ToString(), _pos, "quantifier without a preceding atom");
                case '{':
                    throw Error("{", _pos, "counted repeat without a preceding atom");
                default:
                    _pos++;
                    return new CharNode(CharSet.Single(c));
            }
        }

        private char ParseEscape()
        {
            var start = _pos;
            _pos++; // '\'
            if (AtEnd)
                throw Error("\\", start, "dangling escape at end of pattern");
            var c = Peek;
            if (c is '.' or '\\' or '-')
            {
                _pos++;
                return c;
            }
            var construct = "\\" + c;
            var reason = c is >= '0' and <= '9'
                ? "backreferences are not in the restricted subset"
                : "only the escapes \\. \\\\ \\- are in the restricted subset";
            throw Error(construct, start, reason);
        }

        private Node ParseClass()
        {
            var open = _pos;
            _pos++; // '['
            var negated = false;
            if (!AtEnd && Peek == '^')
            {
                negated = true;
                _pos++;
            }
            var ranges = new List<(char Lo, char Hi)>();
            while (!AtEnd && Peek != ']')
            {
                var lo = ParseClassChar();
                if (!AtEnd && Peek == '-' && _pos + 1 < _text.Length && _text[_pos + 1] != ']')
                {
                    var dashPos = _pos;
                    _pos++; // '-'
                    var hi = ParseClassChar();
                    if (lo > hi)
                        throw Error($"{lo}-{hi}", dashPos, "reversed character-class range");
                    ranges.Add((lo, hi));
                }
                else
                {
                    ranges.Add((lo, lo));
                }
            }
            if (AtEnd)
                throw Error("[", open, "unterminated character class");
            _pos++; // ']'
            var set = new CharSet(ranges);
            return new CharNode(negated ? set.Negate() : set);
        }

        private char ParseClassChar()
        {
            if (Peek == '\\') return ParseEscape();
            var c = Peek;
            _pos++;
            return c;
        }
    }

    // ------------------------------------------------------------------
    // Thompson construction
    // ------------------------------------------------------------------

    private sealed class Builder
    {
        public List<List<Transition>> States { get; } = new();

        private int NewState()
        {
            States.Add(new List<Transition>());
            return States.Count - 1;
        }

        private void Eps(int from, int to) => States[from].Add(new Transition(null, to));

        private void On(int from, CharSet set, int to) => States[from].Add(new Transition(set, to));

        public (int Start, int Accept) Build(Node node)
        {
            switch (node)
            {
                case EmptyNode:
                {
                    var s = NewState();
                    var a = NewState();
                    Eps(s, a);
                    return (s, a);
                }
                case CharNode c:
                {
                    var s = NewState();
                    var a = NewState();
                    On(s, c.Set, a);
                    return (s, a);
                }
                case ConcatNode c:
                {
                    var (s1, a1) = Build(c.Left);
                    var (s2, a2) = Build(c.Right);
                    Eps(a1, s2);
                    return (s1, a2);
                }
                case AltNode alt:
                {
                    var s = NewState();
                    var a = NewState();
                    var (s1, a1) = Build(alt.Left);
                    var (s2, a2) = Build(alt.Right);
                    Eps(s, s1);
                    Eps(s, s2);
                    Eps(a1, a);
                    Eps(a2, a);
                    return (s, a);
                }
                case StarNode star:
                {
                    var s = NewState();
                    var a = NewState();
                    var (s1, a1) = Build(star.Inner);
                    Eps(s, s1);
                    Eps(s, a);
                    Eps(a1, s1);
                    Eps(a1, a);
                    return (s, a);
                }
                case RepeatNode rep:
                {
                    // {min,max}: min mandatory copies followed by (max-min) optional copies.
                    Node expanded = new EmptyNode();
                    var first = true;
                    for (var i = 0; i < rep.Min; i++)
                    {
                        expanded = first ? rep.Inner : new ConcatNode(expanded, rep.Inner);
                        first = false;
                    }
                    for (var i = rep.Min; i < rep.Max; i++)
                    {
                        Node optional = new AltNode(rep.Inner, new EmptyNode());
                        expanded = first ? optional : new ConcatNode(expanded, optional);
                        first = false;
                    }
                    return Build(expanded);
                }
                default:
                    throw new InvalidOperationException($"unknown regex node {node.GetType().Name}");
            }
        }
    }
}
