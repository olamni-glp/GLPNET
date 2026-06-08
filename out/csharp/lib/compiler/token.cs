namespace GlpRuntime.Compiler;

/// <summary>Token types for GLP lexical analysis</summary>
public enum TokenType
{
    // Identifiers and literals
    ATOM,           // lowercase: p, merge, foo_bar
    VARIABLE,       // uppercase: X, Ys, Result
    READER,         // variable with ?: X?, Ys?
    NUMBER,         // 42, 3.14, -17
    STRING,         // "hello", 'world'

    // Delimiters
    LPAREN,         // (
    RPAREN,         // )
    LBRACKET,       // [
    RBRACKET,       // ]
    LBRACE,         // { (future: sets)
    RBRACE,         // } (future: sets)

    // Punctuation
    DOT,            // .
    COMMA,          // ,
    PIPE,           // |
    QUESTION,       // ? (for readers)
    SEMICOLON,      // ; (disjunction/OR in guards)

    // Operators
    IMPLIES,        // :-
    ASSIGN,         // := (arithmetic assignment)
    GUARD_SEP,      // | (in clause: Head :- Guards | Body)

    // Arithmetic operators
    PLUS,           // +
    MINUS,          // - (can be unary or binary)
    STAR,           // *
    SLASH,          // /
    SLASH_SLASH,    // // (integer division)
    MOD,            // mod (keyword)

    // Comparison operators
    LESS,           // <
    GREATER,        // >
    LESS_EQUAL,     // =< (Prolog convention, not <=)
    GREATER_EQUAL,  // >=
    EQUALS,         // = (unification in guards)
    ARITH_EQUAL,    // =:= (arithmetic equality)
    ARITH_NOT_EQUAL,// =\= (arithmetic inequality)
    GROUND_EQUAL,   // =?= (ground equality)
    UNIV,           // =.. (structure composition: list to compound)
    UNIV_DECOMPOSE, // ..= (structure decomposition: compound to list)

    // Special
    UNDERSCORE,     // _ (anonymous variable)
    TILDE,          // ~ (guard negation)
    HASH,           // # (module operator: Module # Goal)
    BACKSLASH,      // \ (difference list operator: H\T)
    AT,             // @ (isolate spawn operator: Goal@Agent)
    // Standard-order term-comparison guards (T011/FR-037): @< @> @=< @>=
    AT_LESS,          // @<
    AT_GREATER,       // @>
    AT_LESS_EQUAL,    // @=<
    AT_GREATER_EQUAL, // @>=

    // Type declarations
    COLONCOLONEQ,   // ::= (type definition)
    PROCEDURE,      // procedure (keyword)

    // End of file
    EOF
}

/// <summary>Token representing a lexical unit in GLP source code</summary>
public sealed class Token : IEquatable<Token>
{
    public TokenType Type { get; }
    public string Lexeme { get; }
    public long Line { get; }
    public long Column { get; }
    public object? Literal { get; }  // For NUMBER, STRING

    public Token(TokenType type, string lexeme, long line, long column, object? literal = null)
    {
        Type = type;
        Lexeme = lexeme;
        Line = line;
        Column = column;
        Literal = literal;
    }

    public override string ToString()
    {
        if (Literal is not null)
        {
            return $"{Type}({Lexeme}={Literal}) at {Line}:{Column}";
        }
        return $"{Type}({Lexeme}) at {Line}:{Column}";
    }

    public override bool Equals(object? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is not Token o) return false;
        return o.Type == Type
            && o.Lexeme == Lexeme
            && o.Line == Line
            && o.Column == Column
            && object.Equals(o.Literal, Literal);
    }

    public bool Equals(Token? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return other.Type == Type
            && other.Lexeme == Lexeme
            && other.Line == Line
            && other.Column == Column
            && object.Equals(other.Literal, Literal);
    }

    public override int GetHashCode() => HashCode.Combine(Type, Lexeme, Line, Column, Literal);
}
