using System.Globalization;
using System.Text;

namespace GlpRuntime.Compiler;

/// <summary>Lexical analyzer for GLP source code</summary>
public class Lexer
{
    public string Source { get; }
    private long _current = 0;
    private long _line = 1;
    private long _column = 1;

    public Lexer(string source)
    {
        Source = source;
    }

    /// <summary>Tokenize the entire source into a list of tokens</summary>
    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (!IsAtEnd())
        {
            SkipWhitespaceAndComments();
            if (IsAtEnd()) break;

            var tok = ScanToken();
            tokens.Add(tok);
        }

        tokens.Add(new Token(TokenType.EOF, "", _line, _column));
        return tokens;
    }

    /// <summary>Scan a single token</summary>
    private Token ScanToken()
    {
        long startLine = _line;
        long startColumn = _column;
        var c = Advance();

        switch (c)
        {
            case '(': return MakeToken(TokenType.LPAREN, startLine, startColumn);
            case ')': return MakeToken(TokenType.RPAREN, startLine, startColumn);
            case '[': return MakeToken(TokenType.LBRACKET, startLine, startColumn);
            case ']': return MakeToken(TokenType.RBRACKET, startLine, startColumn);
            case '{': return MakeToken(TokenType.LBRACE, startLine, startColumn);
            case '}': return MakeToken(TokenType.RBRACE, startLine, startColumn);
            case '.':
                if (Match('.'))
                {
                    if (Match('='))
                    {
                        var lexeme = Source.Substring((int)(_current - 3), 3);
                        return new Token(TokenType.UNIV_DECOMPOSE, lexeme, startLine, startColumn);
                    }
                    // Two dots not followed by = - back up and return single DOT
                    _current--;
                    _column--;
                    return MakeToken(TokenType.DOT, startLine, startColumn);
                }
                return MakeToken(TokenType.DOT, startLine, startColumn);
            case ',': return MakeToken(TokenType.COMMA, startLine, startColumn);
            case '?': return MakeToken(TokenType.QUESTION, startLine, startColumn);
            case '|': return MakeToken(TokenType.PIPE, startLine, startColumn);
            case ';': return MakeToken(TokenType.SEMICOLON, startLine, startColumn);
            case '~': return MakeToken(TokenType.TILDE, startLine, startColumn);
            case '#': return MakeToken(TokenType.HASH, startLine, startColumn);
            case '\\': return MakeToken(TokenType.BACKSLASH, startLine, startColumn);
            case '@':
                // Standard-order term-comparison guards (T011/FR-037): @< @> @>= @=<.
                // `@` followed by anything else stays a bare AT — the Goal@Agent
                // isolate-spawn operator (Agent never starts with </>/=).
                if (Match('<'))
                {
                    var lexAtLt = Source.Substring((int)(_current - 2), 2);
                    return new Token(TokenType.AT_LESS, lexAtLt, startLine, startColumn);
                }
                if (Match('>'))
                {
                    if (Match('='))
                    {
                        var lexAtGe = Source.Substring((int)(_current - 3), 3);
                        return new Token(TokenType.AT_GREATER_EQUAL, lexAtGe, startLine, startColumn);
                    }
                    var lexAtGt = Source.Substring((int)(_current - 2), 2);
                    return new Token(TokenType.AT_GREATER, lexAtGt, startLine, startColumn);
                }
                if (Match('='))
                {
                    if (Match('<'))
                    {
                        var lexAtLe = Source.Substring((int)(_current - 3), 3);
                        return new Token(TokenType.AT_LESS_EQUAL, lexAtLe, startLine, startColumn);
                    }
                    throw new CompileError("Expected \"<\" after \"@=\" (the @=< guard)", startLine, startColumn, phase: "lexer");
                }
                return MakeToken(TokenType.AT, startLine, startColumn);

            // Arithmetic operators
            case '+': return MakeToken(TokenType.PLUS, startLine, startColumn);
            case '*': return MakeToken(TokenType.STAR, startLine, startColumn);
            case '/':
                if (Match('/'))
                {
                    var lexeme = Source.Substring((int)(_current - 2), 2);
                    return new Token(TokenType.SLASH_SLASH, lexeme, startLine, startColumn);
                }
                return MakeToken(TokenType.SLASH, startLine, startColumn);

            // Comparison operators
            case '<':
                return MakeToken(TokenType.LESS, startLine, startColumn);
            case '>':
                if (Match('='))
                {
                    var lexeme = Source.Substring((int)(_current - 2), 2);
                    return new Token(TokenType.GREATER_EQUAL, lexeme, startLine, startColumn);
                }
                return MakeToken(TokenType.GREATER, startLine, startColumn);
            case '=':
                if (Match('.'))
                {
                    if (Match('.'))
                    {
                        var lexeme = Source.Substring((int)(_current - 3), 3);
                        return new Token(TokenType.UNIV, lexeme, startLine, startColumn);
                    }
                    throw new CompileError("Expected \"..\" after \"=.\"", startLine, startColumn, phase: "lexer");
                }
                if (Match('<'))
                {
                    var lexeme = Source.Substring((int)(_current - 2), 2);
                    return new Token(TokenType.LESS_EQUAL, lexeme, startLine, startColumn);
                }
                if (Match(':'))
                {
                    if (Match('='))
                    {
                        var lexeme = Source.Substring((int)(_current - 3), 3);
                        return new Token(TokenType.ARITH_EQUAL, lexeme, startLine, startColumn);
                    }
                    throw new CompileError("Expected \"=\" after \"=:\"", startLine, startColumn, phase: "lexer");
                }
                if (Match('\\'))
                {
                    if (Match('='))
                    {
                        var lexeme = Source.Substring((int)(_current - 3), 3);
                        return new Token(TokenType.ARITH_NOT_EQUAL, lexeme, startLine, startColumn);
                    }
                    throw new CompileError("Expected \"=\" after \"=\\\"", startLine, startColumn, phase: "lexer");
                }
                if (Match('?'))
                {
                    if (Match('='))
                    {
                        var lexeme = Source.Substring((int)(_current - 3), 3);
                        return new Token(TokenType.GROUND_EQUAL, lexeme, startLine, startColumn);
                    }
                    throw new CompileError("Expected \"=\" after \"=?\"", startLine, startColumn, phase: "lexer");
                }
                return MakeToken(TokenType.EQUALS, startLine, startColumn);

            case ':':
                if (Match(':'))
                {
                    if (Match('='))
                    {
                        var lexeme = Source.Substring((int)(_current - 3), 3);
                        return new Token(TokenType.COLONCOLONEQ, lexeme, startLine, startColumn);
                    }
                    throw new CompileError("Expected \"=\" after \"::\"", startLine, startColumn, phase: "lexer");
                }
                if (Match('-'))
                {
                    var lexeme = Source.Substring((int)(_current - 2), 2);
                    return new Token(TokenType.IMPLIES, lexeme, startLine, startColumn);
                }
                if (Match('='))
                {
                    var lexeme = Source.Substring((int)(_current - 2), 2);
                    return new Token(TokenType.ASSIGN, lexeme, startLine, startColumn);
                }
                throw new CompileError("Unexpected character :", startLine, startColumn, phase: "lexer");

            case '_':
                if (!IsAlphaNumeric(Peek()))
                {
                    return MakeToken(TokenType.UNDERSCORE, startLine, startColumn);
                }
                // Otherwise fall through to identifier
                return Identifier(_current - 1, startLine, startColumn);

            case '"':
            case '\'':
                return String(c, startLine, startColumn);

            case '-':
                // Check if it's a negative number literal or minus operator
                if (IsDigit(Peek()))
                {
                    return Number(_current - 1, startLine, startColumn);
                }
                else
                {
                    return MakeToken(TokenType.MINUS, startLine, startColumn);
                }

            default:
                if (IsDigit(c))
                {
                    return Number(_current - 1, startLine, startColumn);
                }
                if (IsAlpha(c))
                {
                    return Identifier(_current - 1, startLine, startColumn);
                }
                throw new CompileError($"Unexpected character: {c}", startLine, startColumn, phase: "lexer");
        }
    }

    /// <summary>Scan identifier or variable</summary>
    private Token Identifier(long start, long line, long column)
    {
        while (IsAlphaNumeric(Peek()))
        {
            Advance();
        }

        var text = Source.Substring((int)start, (int)(_current - start));

        // Check for 'mod' keyword - but only if not followed by '(' (predicate call)
        if (text == "mod" && Peek() != '(')
        {
            return new Token(TokenType.MOD, text, line, column);
        }

        // Check for 'procedure' keyword
        if (text == "procedure")
        {
            return new Token(TokenType.PROCEDURE, text, line, column);
        }

        // Check if this is a variable: starts with uppercase OR starts with _ followed by uppercase
        // Named anonymous variables like _Out, _Result are variables, not atoms
        bool isVariable = IsUpper(text[0]) ||
            (text[0] == '_' && text.Length > 1 && IsUpper(text[1]));

        // Check for reader syntax (Variable?)
        if (Peek() == '?' && isVariable)
        {
            Advance(); // consume '?'
            return new Token(TokenType.READER, text, line, column);
        }

        // Variable (uppercase or _Uppercase) or Atom (lowercase)
        var type = isVariable ? TokenType.VARIABLE : TokenType.ATOM;
        return new Token(type, text, line, column);
    }

    /// <summary>Scan number (integer or float)</summary>
    private Token Number(long start, long line, long column)
    {
        // Handle negative sign
        if (Source[(int)start] == '-')
        {
            _current = start + 1;
        }

        while (IsDigit(Peek()))
        {
            Advance();
        }

        // Handle decimal point
        if (Peek() == '.' && IsDigit(PeekNext()))
        {
            Advance(); // consume '.'
            while (IsDigit(Peek()))
            {
                Advance();
            }
        }

        var text = Source.Substring((int)start, (int)(_current - start));
        var value = text.Contains('.') ?
            (object)double.Parse(text, CultureInfo.InvariantCulture) :
            (object)long.Parse(text, CultureInfo.InvariantCulture);
        return new Token(TokenType.NUMBER, text, line, column, value);
    }

    /// <summary>Scan string literal or quoted atom.
    /// Single quotes produce ATOM (quoted atom, can be used as functor).
    /// Double quotes produce STRING (string literal).</summary>
    private Token String(char quote, long line, long column)
    {
        var buffer = new StringBuilder();

        while (!IsAtEnd() && Peek() != quote)
        {
            if (Peek() == '\\')
            {
                Advance();
                if (IsAtEnd()) break;

                // Handle escape sequences
                switch (Peek())
                {
                    case 'n': buffer.Append('\n'); break;
                    case 't': buffer.Append('\t'); break;
                    case 'r': buffer.Append('\r'); break;
                    case '\\': buffer.Append('\\'); break;
                    case '"': buffer.Append('"'); break;
                    case '\'': buffer.Append('\''); break;
                    default: buffer.Append(Peek()); break;
                }
                Advance();
            }
            else
            {
                if (Peek() == '\n')
                {
                    _line++;
                    _column = 0;
                }
                buffer.Append(Advance());
            }
        }

        if (IsAtEnd())
        {
            throw new CompileError("Unterminated string", line, column, phase: "lexer");
        }

        Advance(); // closing quote

        // Single-quoted: produce ATOM (quoted atom, usable as functor)
        // Double-quoted: produce STRING (string literal)
        if (quote == '\'')
        {
            return new Token(TokenType.ATOM, buffer.ToString(), line, column);
        }
        else
        {
            return new Token(TokenType.STRING, buffer.ToString(), line, column, buffer.ToString());
        }
    }

    /// <summary>Skip whitespace and comments</summary>
    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd())
        {
            var c = Peek();

            // Whitespace
            if (c == ' ' || c == '\t' || c == '\r')
            {
                Advance();
            }
            else if (c == '\n')
            {
                Advance();
                _line++;
                _column = 1;
            }
            // Line comment: % to end of line
            else if (c == '%')
            {
                while (!IsAtEnd() && Peek() != '\n')
                {
                    Advance();
                }
            }
            // Block comment: /* ... */
            else if (c == '/' && PeekNext() == '*')
            {
                Advance(); // /
                Advance(); // *
                while (!IsAtEnd())
                {
                    if (Peek() == '*' && PeekNext() == '/')
                    {
                        Advance(); // *
                        Advance(); // /
                        break;
                    }
                    if (Peek() == '\n')
                    {
                        _line++;
                        _column = 0;
                    }
                    Advance();
                }
            }
            else
            {
                break;
            }
        }
    }

    // Helper methods
    private char Advance()
    {
        _column++;
        return Source[(int)_current++];
    }

    private bool Match(char expected)
    {
        if (IsAtEnd()) return false;
        if (Source[(int)_current] != expected) return false;
        Advance();
        return true;
    }

    private char Peek() => IsAtEnd() ? '\0' : Source[(int)_current];
    private char PeekNext() => _current + 1 >= Source.Length ? '\0' : Source[(int)(_current + 1)];
    private bool IsAtEnd() => _current >= Source.Length;

    private static bool IsDigit(char c)
    {
        if (c == '\0') return false;
        int code = (int)c;
        return code >= (int)'0' && code <= (int)'9';
    }

    private static bool IsAlpha(char c)
    {
        if (c == '\0') return false;
        int code = (int)c;
        return (code >= (int)'a' && code <= (int)'z') ||
               (code >= (int)'A' && code <= (int)'Z') ||
               c == '_';
    }

    private static bool IsAlphaNumeric(char c) => IsAlpha(c) || IsDigit(c);

    private static bool IsUpper(char c)
    {
        if (c == '\0') return false;
        int code = (int)c;
        return code >= (int)'A' && code <= (int)'Z';
    }

    private Token MakeToken(TokenType type, long line, long column)
    {
        var lexeme = Source.Substring((int)(_current - 1), 1);
        return new Token(type, lexeme, line, column);
    }
}
