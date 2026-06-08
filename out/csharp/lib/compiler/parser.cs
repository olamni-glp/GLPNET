// lib/compiler/parser.cs
//
// Recursive-descent parser for GLP source code.
// Converted from Dart source: lib/compiler/parser.dart
// source_sha256: d5b6f4a7c81d0dcfd0fb32be8b28f7da3d3b77dc84571a10f063188114b2e9eb

using System.Collections.Frozen;
using System.Linq;
using GlpRuntime.Analysis.TypeChecker;

namespace GlpRuntime.Compiler;

/// <summary>Parser for GLP source code</summary>
public class Parser
{
    // Token stream: immutable, read-only view (Dart `final List<Token> tokens`)
    public IReadOnlyList<Token> Tokens { get; }

    // Mutable cursor into the token stream (Dart `int _current = 0`)
    private long _current = 0;

    // Single-slot look-back: a clause parsed but belonging to a different procedure
    private Clause? _pendingClause = null;

    // Declaration keywords for SkipDeclarations / ParseModule
    private static readonly FrozenSet<string> DeclarationKeywords =
        FrozenSet.Create(StringComparer.Ordinal, "module", "stdlib", "mode");

    public Parser(IReadOnlyList<Token> tokens)
    {
        Tokens = tokens;
    }

    // ============================================================================
    // Public entry points
    // ============================================================================

    /// <summary>Parse tokens into an AST (legacy method, skips declarations)</summary>
    public Program Parse()
    {
        // Skip any module declarations at the start
        SkipDeclarations();

        var procedures = new List<Procedure>();

        while (!IsAtEnd())
        {
            procedures.Add(ParseProcedure());
        }

        // Check for non-contiguous clauses (same name/arity appearing multiple times)
        CheckContiguousClauses(procedures);

        return new Program(procedures, 1, 1);
    }

    /// <summary>Parse tokens into a Module AST (includes declarations)</summary>
    public Module ParseModule()
    {
        ModuleDeclaration? moduleDecl = null;
        var compileMode = CompileMode.User; // default: user mode

        // Parse declarations at the start of the file
        while (!IsAtEnd() && Check(TokenType.MINUS))
        {
            var startPos = _current;
            var startLine = Peek().Line;
            var startCol  = Peek().Column;
            Advance(); // consume '-'

            if (!Check(TokenType.ATOM))
            {
                _current = startPos; // Back up, not a declaration
                break;
            }

            var keyword = Advance();

            switch (keyword.Lexeme)
            {
                case "module":
                    Consume(TokenType.LPAREN, "Expected \"(\" after module");
                    var name = ParseModuleName();
                    Consume(TokenType.RPAREN, "Expected \")\" after module name");
                    Consume(TokenType.DOT, "Expected \".\" after module declaration");
                    moduleDecl = new ModuleDeclaration(name, (int)startLine, (int)startCol);
                    break;

                case "stdlib":
                    // -stdlib. is deprecated — treated as -mode(system).
                    Consume(TokenType.DOT, "Expected \".\" after stdlib declaration");
                    compileMode = CompileMode.System;
                    break;

                case "mode":
                    // -mode(user). or -mode(system). declaration
                    Consume(TokenType.LPAREN, "Expected \"(\" after mode");
                    if (!Check(TokenType.ATOM))
                    {
                        throw new CompileError(
                            "Expected \"user\" or \"system\" in mode declaration",
                            Peek().Line, Peek().Column, phase: "parser");
                    }
                    var modeToken = Advance();
                    if (modeToken.Lexeme == "user")
                    {
                        compileMode = CompileMode.User;
                    }
                    else if (modeToken.Lexeme == "system")
                    {
                        compileMode = CompileMode.System;
                    }
                    else
                    {
                        throw new CompileError(
                            $"Invalid mode \"{modeToken.Lexeme}\". Expected \"user\" or \"system\".",
                            modeToken.Line, modeToken.Column, phase: "parser");
                    }
                    Consume(TokenType.RPAREN, "Expected \")\" after mode");
                    Consume(TokenType.DOT, "Expected \".\" after mode declaration");
                    break;

                case "export":
                    throw new CompileError(
                        "The -export() declaration is no longer supported. Use 'exported procedure' instead.",
                        startLine, startCol, phase: "parser");

                case "import":
                    throw new CompileError(
                        "The -import() declaration is no longer supported. Use 'imported procedure' instead.",
                        startLine, startCol, phase: "parser");

                default:
                    // Unknown declaration, back up to the '-'
                    _current = startPos;
                    break;
            }
        }

        // Parse type definitions, procedure declarations, and clauses in order.
        var typeDefs         = new List<TypeDef>();
        var procDeclarations = new List<ProcDecl>();
        var procedures       = new List<Procedure>();

        // Track pending procedure declaration (waiting for its first clause)
        ProcDecl? pendingProcDecl = null;
        // Track which procedures we've seen clauses for (signature -> first Procedure)
        var seenProcedures = new Dictionary<string, Procedure>(StringComparer.Ordinal);

        while (!IsAtEnd())
        {
            // Check for procedure declaration: 'procedure ...' or 'exported procedure ...' or 'imported procedure ...'
            var isProcedureDecl = Check(TokenType.PROCEDURE) ||
                (Check(TokenType.ATOM) &&
                 (Peek().Lexeme == "exported" || Peek().Lexeme == "imported") &&
                 _current + 1 < Tokens.Count &&
                 Tokens[(int)(_current + 1)].Type == TokenType.PROCEDURE);

            if (isProcedureDecl)
            {
                // Procedure declaration (possibly exported or imported)
                if (pendingProcDecl != null)
                {
                    // Check if the pending declaration is for a builtin or imported (no clauses needed)
                    var pendingSig = $"{pendingProcDecl.Name}/{pendingProcDecl.ArgTypes.Count}";
                    if (!Prelude.BuiltinProcedures.Contains(pendingSig) && !pendingProcDecl.Imported)
                    {
                        throw new CompileError(
                            $"Procedure declaration for \"{pendingProcDecl.Name}\" has no clauses.\n" +
                            "  A procedure declaration must be immediately followed by its clauses.",
                            pendingProcDecl.Line, pendingProcDecl.Column, phase: "parser");
                    }
                    // Builtin or imported - clear pending without error
                    pendingProcDecl = null;
                }
                var decl = ParseProcDeclaration();
                procDeclarations.Add(decl);
                // Imported procedures are declaration-only — no clauses expected
                if (!decl.Imported)
                {
                    pendingProcDecl = decl;
                }
            }
            else if (Check(TokenType.VARIABLE) || Check(TokenType.READER))
            {
                // Might be a type definition (TypeName ::= ...) or a clause head
                var startPos = _current;
                var token    = Peek();

                // Look ahead to see if this is a type definition (has ::=)
                if (IsTypeDefinition())
                {
                    // Type definition
                    if (pendingProcDecl != null)
                    {
                        // Check if the pending declaration is for a builtin or imported (no clauses needed)
                        var pendingSig = $"{pendingProcDecl.Name}/{pendingProcDecl.ArgTypes.Count}";
                        if (!Prelude.BuiltinProcedures.Contains(pendingSig) && !pendingProcDecl.Imported)
                        {
                            throw new CompileError(
                                "Type definition cannot appear between procedure declaration and its clauses.\n" +
                                $"  Procedure \"{pendingProcDecl.Name}\" declared at line {pendingProcDecl.Line} needs clauses.",
                                token.Line, token.Column, phase: "parser");
                        }
                        // Builtin or imported - clear pending without error
                        pendingProcDecl = null;
                    }
                    typeDefs.Add(ParseTypeDef());
                }
                else
                {
                    // It's a clause - parse the procedure
                    _current = startPos;
                    var proc = ParseProcedure();
                    var sig  = $"{proc.Name}/{proc.Arity}";

                    // Check if this matches pending declaration
                    if (pendingProcDecl != null)
                    {
                        var pendingSig = $"{pendingProcDecl.Name}/{pendingProcDecl.ArgTypes.Count}";
                        if (sig == pendingSig)
                        {
                            // This clause matches the pending declaration - good
                            pendingProcDecl = null;
                        }
                        else if (Prelude.BuiltinProcedures.Contains(pendingSig))
                        {
                            // Pending was a builtin (no clauses needed) - clear it
                            pendingProcDecl = null;
                        }
                        else
                        {
                            throw new CompileError(
                                $"Clause for \"{sig}\" appears between procedure declaration and clauses for \"{pendingSig}\".\n" +
                                $"  Procedure declaration at line {pendingProcDecl.Line} must be immediately followed by its clauses.",
                                proc.Line, proc.Column, phase: "parser");
                        }
                    }

                    // Check for non-contiguous clauses
                    if (seenProcedures.TryGetValue(sig, out var firstSeen))
                    {
                        throw new CompileError(
                            $"Non-contiguous clauses for \"{sig}\".\n" +
                            $"  First group at line {firstSeen.Line}, second group at line {proc.Line}.\n" +
                            "  All clauses for a predicate must be together in the source file.",
                            proc.Line, proc.Column, phase: "parser");
                    }

                    seenProcedures[sig] = proc;
                    procedures.Add(proc);
                }
            }
            else if (Check(TokenType.ATOM))
            {
                // Clause starting with atom (procedure name)
                var proc = ParseProcedure();
                var sig  = $"{proc.Name}/{proc.Arity}";

                // Check if this matches pending declaration
                if (pendingProcDecl != null)
                {
                    var pendingSig = $"{pendingProcDecl.Name}/{pendingProcDecl.ArgTypes.Count}";
                    if (sig == pendingSig)
                    {
                        // This clause matches the pending declaration - good
                        pendingProcDecl = null;
                    }
                    else if (Prelude.BuiltinProcedures.Contains(pendingSig))
                    {
                        // Pending was a builtin (no clauses needed) - clear it
                        pendingProcDecl = null;
                    }
                    else
                    {
                        throw new CompileError(
                            $"Clause for \"{sig}\" appears between procedure declaration and clauses for \"{pendingSig}\".\n" +
                            $"  Procedure declaration at line {pendingProcDecl.Line} must be immediately followed by its clauses.",
                            proc.Line, proc.Column, phase: "parser");
                    }
                }

                // Check for non-contiguous clauses
                if (seenProcedures.TryGetValue(sig, out var firstSeen2))
                {
                    throw new CompileError(
                        $"Non-contiguous clauses for \"{sig}\".\n" +
                        $"  First group at line {firstSeen2.Line}, second group at line {proc.Line}.\n" +
                        "  All clauses for a predicate must be together in the source file.",
                        proc.Line, proc.Column, phase: "parser");
                }

                seenProcedures[sig] = proc;
                procedures.Add(proc);
            }
            else
            {
                // Unexpected token
                throw new CompileError(
                    $"Unexpected token: {Peek().Lexeme}",
                    Peek().Line, Peek().Column, phase: "parser");
            }
        }

        // Check for dangling procedure declaration at end of file
        if (pendingProcDecl != null)
        {
            var pendingSig = $"{pendingProcDecl.Name}/{pendingProcDecl.ArgTypes.Count}";
            if (!Prelude.BuiltinProcedures.Contains(pendingSig) && !pendingProcDecl.Imported)
            {
                throw new CompileError(
                    $"Procedure declaration for \"{pendingProcDecl.Name}\" has no clauses.\n" +
                    "  A procedure declaration must be immediately followed by its clauses.",
                    pendingProcDecl.Line, pendingProcDecl.Column, phase: "parser");
            }
        }

        return new Module(
            declaration:      moduleDecl,
            typeDefs:         typeDefs,
            procDeclarations: procDeclarations,
            paramProcDecls:   null,
            procedures:       procedures,
            compileMode:      compileMode,
            line:             1,
            column:           1);
    }

    // ============================================================================
    // Private parsing methods
    // ============================================================================

    /// <summary>Skip module declarations at start of file (for legacy Parse())</summary>
    private void SkipDeclarations()
    {
        while (!IsAtEnd() && Check(TokenType.MINUS))
        {
            var startPos = _current;
            Advance(); // consume '-'

            if (!Check(TokenType.ATOM))
            {
                _current = startPos; // Back up, not a declaration
                break;
            }

            var keyword = Peek().Lexeme;

            if (DeclarationKeywords.Contains(keyword))
            {
                // Skip to the next DOT
                while (!IsAtEnd() && !Check(TokenType.DOT))
                {
                    Advance();
                }
                if (Check(TokenType.DOT))
                {
                    Advance(); // consume '.'
                }
            }
            else
            {
                // Not a declaration keyword, back up
                _current = startPos;
                break;
            }
        }
    }

    /// <summary>Parse hierarchical module name (e.g., utils.list)</summary>
    private string ParseModuleName()
    {
        var parts = new List<string>();
        parts.Add(Consume(TokenType.ATOM, "Expected module name").Lexeme);

        while (Match(TokenType.DOT) && Check(TokenType.ATOM))
        {
            parts.Add(Consume(TokenType.ATOM, "Expected module name part").Lexeme);
        }

        // Back up if we consumed a DOT but the next token wasn't ATOM
        // (This handles -module(foo). where DOT ends the declaration)
        if (Previous().Type == TokenType.DOT && !Check(TokenType.ATOM))
        {
            _current--;
        }

        return string.Join(".", parts);
    }

    // _parseProcRefList, _parseProcRef, _parseAtomList removed in Phase 1.
    // These were only used for -export([...]) and -import([...]) syntax.

    // Procedure: one or more clauses with same head functor/arity
    private Procedure ParseProcedure()
    {
        var clauses = new List<Clause>();

        // Use pending clause if available, otherwise parse first clause
        Clause firstClause;
        if (_pendingClause != null)
        {
            firstClause    = _pendingClause;
            _pendingClause = null;
        }
        else
        {
            firstClause = ParseClause();
        }
        clauses.Add(firstClause);

        var name  = firstClause.Head.Functor;
        var arity = firstClause.Head.Arity;

        // Parse additional clauses with same functor/arity
        // Special case: := clauses start with VARIABLE, not ATOM
        while (!IsAtEnd())
        {
            // Check if next clause could be part of this procedure
            bool couldBeSameProcedure = false;

            if (Peek().Type == TokenType.ATOM && Peek().Lexeme == name)
            {
                // Same predicate name
                couldBeSameProcedure = true;
            }
            else if (name == ":=" && (Peek().Type == TokenType.VARIABLE || Peek().Type == TokenType.READER || Peek().Type == TokenType.UNDERSCORE))
            {
                // := clauses start with variable or underscore (e.g., "Result := X + Y" or "_ := X / 0")
                // Look ahead to see if it's followed by :=
                if (_current + 1 < Tokens.Count && Tokens[(int)(_current + 1)].Type == TokenType.ASSIGN)
                {
                    couldBeSameProcedure = true;
                }
            }
            else if (name == "=.." && (Peek().Type == TokenType.VARIABLE || Peek().Type == TokenType.READER || Peek().Type == TokenType.UNDERSCORE))
            {
                // =.. clauses start with variable or underscore (e.g., "X? =.. Y")
                // Look ahead to see if it's followed by =..
                if (_current + 1 < Tokens.Count && Tokens[(int)(_current + 1)].Type == TokenType.UNIV)
                {
                    couldBeSameProcedure = true;
                }
            }
            else if (name == "..=" && (Peek().Type == TokenType.VARIABLE || Peek().Type == TokenType.READER || Peek().Type == TokenType.UNDERSCORE))
            {
                // ..= clauses start with variable or underscore (e.g., "List ..= X?")
                // Look ahead to see if it's followed by ..=
                if (_current + 1 < Tokens.Count && Tokens[(int)(_current + 1)].Type == TokenType.UNIV_DECOMPOSE)
                {
                    couldBeSameProcedure = true;
                }
            }
            else if (name == "=" && (Peek().Type == TokenType.VARIABLE || Peek().Type == TokenType.READER || Peek().Type == TokenType.UNDERSCORE))
            {
                // = clauses start with variable or underscore (e.g., "X? = Y")
                // Look ahead to see if it's followed by =
                if (_current + 1 < Tokens.Count && Tokens[(int)(_current + 1)].Type == TokenType.EQUALS)
                {
                    couldBeSameProcedure = true;
                }
            }

            if (!couldBeSameProcedure) break;

            var clause = ParseClause();

            // If functor matches but arity differs, this is a different procedure
            // Store it as pending and break
            if (clause.Head.Functor == name && clause.Head.Arity != arity)
            {
                _pendingClause = clause;
                break;
            }

            // Verify same functor (arity already checked above for same-name case)
            if (clause.Head.Functor != name)
            {
                throw new CompileError(
                    $"Clause for {clause.Head.Functor}/{clause.Head.Arity} found, expected {name}/{arity}",
                    clause.Line, clause.Column, phase: "parser");
            }

            clauses.Add(clause);
        }

        return new Procedure(name, arity, clauses, firstClause.Line, firstClause.Column);
    }

    // Clause: Head :- Guards | Body.
    //     or: Head :- Body.
    //     or: Head.
    private Clause ParseClause()
    {
        var head = ParseAtom();

        List<Guard>? guards = null;
        List<Goal>?  body   = null;

        // Check for :- (clause with guards/body)
        if (Match(TokenType.IMPLIES))
        {
            // Parse everything before | as guards (or body if no |)
            var predicates = new List<object>();

            predicates.Add(ParseGoalOrGuard());

            while (Match(TokenType.COMMA))
            {
                predicates.Add(ParseGoalOrGuard());
            }

            // Check for | separator
            if (Match(TokenType.PIPE))
            {
                // Everything before | were guards - convert Goal to Guard
                guards = predicates.Select(g =>
                {
                    var goal      = (Goal)g;
                    // Detect negated guards (functor starts with ~)
                    bool isNegated = goal.Functor.StartsWith("~", StringComparison.Ordinal);
                    var actualFunctor = isNegated ? goal.Functor.Substring(1) : goal.Functor;
                    return new Guard(actualFunctor, goal.Args, goal.Line, goal.Column, negated: isNegated);
                }).ToList();

                // Parse body after |
                body = new List<Goal>();
                body.Add(ParseGoal());

                while (Match(TokenType.COMMA))
                {
                    body.Add(ParseGoal());
                }
            }
            else
            {
                // No | separator, so everything was body goals
                body = predicates.Cast<Goal>().ToList();
            }
        }

        Consume(TokenType.DOT, "Expected \".\" at end of clause");

        return new Clause(head, guards: guards, body: body, line: head.Line, column: head.Column);
    }

    // Parse a predicate that could be either a guard or a goal
    private object ParseGoalOrGuard()
    {
        // Check for guard negation: ~G
        bool negated  = false;
        long negLine   = Peek().Line;
        long negColumn = Peek().Column;
        if (Match(TokenType.TILDE))
        {
            negated   = true;
            negLine   = Previous().Line;
            negColumn = Previous().Column;

            // Check for double negation ~~G (syntactically forbidden)
            if (Check(TokenType.TILDE))
            {
                throw new CompileError(
                    "Double negation ~~G is not allowed",
                    Peek().Line, Peek().Column, phase: "parser");
            }
        }

        // Check for parenthesized expression: (Goal) or (Goal1 ; Goal2)
        if (Check(TokenType.LPAREN))
        {
            var startToken = Advance(); // consume '('
            var firstGoal  = ParseGoalOrGuard();

            if (Match(TokenType.SEMICOLON))
            {
                // This is a disjunction - negation not allowed
                if (negated)
                {
                    throw new CompileError(
                        "Guard negation (~) cannot be applied to disjunction",
                        negLine, negColumn, phase: "parser");
                }
                var secondGoal = ParseGoalOrGuard();
                Consume(TokenType.RPAREN, "Expected \")\" after disjunction");
                // Return as ';'(Goal1, Goal2) - need to convert goals to terms
                var firstTerm  = GoalToTerm(firstGoal);
                var secondTerm = GoalToTerm(secondGoal);
                return new Goal(";", new Term[] { firstTerm, secondTerm }, (int)startToken.Line, (int)startToken.Column);
            }
            else
            {
                // Parenthesized single goal - apply negation if present
                Consume(TokenType.RPAREN, "Expected \")\" after guard");
                if (negated)
                {
                    // Apply negation to the parsed goal
                    var gCast  = (Goal)firstGoal;
                    var functor = "~" + gCast.Functor;
                    return new Goal(functor, gCast.Args, (int)negLine, (int)negColumn);
                }
                return firstGoal;
            }
        }

        // Check for assignment (Var := Expr) or univ (Var =.. Expr)
        if (Check(TokenType.VARIABLE) || Check(TokenType.READER))
        {
            var varToken = Peek();
            bool isReader = varToken.Type == TokenType.READER;
            // Look ahead for := or =..
            if (Tokens.Count > _current + 1 && Tokens[(int)(_current + 1)].Type == TokenType.ASSIGN)
            {
                Advance(); // consume variable
                Advance(); // consume :=
                var varTerm = new VarTerm(varToken.Lexeme, isReader, (int)varToken.Line, (int)varToken.Column);
                var expr    = ParseExpression();
                return new Goal(":=", new Term[] { varTerm, expr }, (int)varToken.Line, (int)varToken.Column);
            }
            else if (Tokens.Count > _current + 1 && Tokens[(int)(_current + 1)].Type == TokenType.UNIV)
            {
                Advance(); // consume variable
                Advance(); // consume =..
                var varTerm = new VarTerm(varToken.Lexeme, isReader, (int)varToken.Line, (int)varToken.Column);
                var expr    = ParseTerm();
                return new Goal("=..", new Term[] { varTerm, expr }, (int)varToken.Line, (int)varToken.Column);
            }
            else if (Tokens.Count > _current + 1 && Tokens[(int)(_current + 1)].Type == TokenType.UNIV_DECOMPOSE)
            {
                Advance(); // consume variable
                Advance(); // consume ..=
                var varTerm = new VarTerm(varToken.Lexeme, isReader, (int)varToken.Line, (int)varToken.Column);
                var expr    = ParseTerm();
                return new Goal("..=", new Term[] { varTerm, expr }, (int)varToken.Line, (int)varToken.Column);
            }
            else if (Tokens.Count > _current + 1 && Tokens[(int)(_current + 1)].Type == TokenType.EQUALS)
            {
                Advance(); // consume variable
                Advance(); // consume =
                var varTerm = new VarTerm(varToken.Lexeme, isReader, (int)varToken.Line, (int)varToken.Column);
                var term    = ParseTerm();
                return new Goal("=", new Term[] { varTerm, term }, (int)varToken.Line, (int)varToken.Column);
            }
            else if (Tokens.Count > _current + 1 && Tokens[(int)(_current + 1)].Type == TokenType.HASH)
            {
                // Dynamic remote goal: Var # Goal (e.g., M? # factorial(5, R))
                Advance(); // consume variable
                Advance(); // consume #
                // Negation not allowed on remote goals
                if (negated)
                {
                    throw new CompileError(
                        "Guard negation (~) cannot be applied to remote goal",
                        negLine, negColumn, phase: "parser");
                }
                var moduleTerm = new VarTerm(varToken.Lexeme, isReader, (int)varToken.Line, (int)varToken.Column);
                var innerGoal  = ParseGoal();
                return new RemoteGoal(moduleTerm, innerGoal, (int)varToken.Line, (int)varToken.Column);
            }
        }

        // Try to parse as regular predicate first
        if (Check(TokenType.ATOM))
        {
            var functorToken = Consume(TokenType.ATOM, "Expected predicate name");
            var args         = new List<Term>();

            if (Match(TokenType.LPAREN))
            {
                if (!Check(TokenType.RPAREN))
                {
                    args.Add(ParseTerm());

                    while (Match(TokenType.COMMA))
                    {
                        args.Add(ParseTerm());
                    }
                }

                Consume(TokenType.RPAREN, "Expected \")\" after arguments");
            }

            // Check for static remote goal: atom # goal (e.g., math # factorial(5, R))
            if (Match(TokenType.HASH))
            {
                // Module name cannot have arguments
                if (args.Count > 0)
                {
                    throw new CompileError(
                        $"Module name cannot have arguments: {functorToken.Lexeme}",
                        functorToken.Line, functorToken.Column, phase: "parser");
                }
                // Negation not allowed on remote goals
                if (negated)
                {
                    throw new CompileError(
                        "Guard negation (~) cannot be applied to remote goal",
                        negLine, negColumn, phase: "parser");
                }
                var moduleTerm = new ConstTerm(functorToken.Lexeme, (int)functorToken.Line, (int)functorToken.Column);
                var innerGoal  = ParseGoal();
                return new RemoteGoal(moduleTerm, innerGoal, (int)functorToken.Line, (int)functorToken.Column);
            }

            // Check if followed by = (e.g., foo = bar, or foo(a) = X)
            if (Match(TokenType.EQUALS))
            {
                var leftTerm = args.Count == 0
                    ? (Term)new ConstTerm(functorToken.Lexeme, (int)functorToken.Line, (int)functorToken.Column)
                    : (Term)new StructTerm(functorToken.Lexeme, args, (int)functorToken.Line, (int)functorToken.Column);
                var rightTerm = ParseTerm();
                // Negation not allowed on unification goals
                if (negated)
                {
                    throw new CompileError(
                        "Guard negation (~) cannot be applied to unification",
                        negLine, negColumn, phase: "parser");
                }
                return new Goal("=", new Term[] { leftTerm, rightTerm }, (int)functorToken.Line, (int)functorToken.Column);
            }

            // Return as Goal for now (will be cast to Guard if before |)
            // Use ~functor convention if negated (will be detected during Guard conversion)
            var gFunctor = negated ? "~" + functorToken.Lexeme : functorToken.Lexeme;
            var goal     = new Goal(gFunctor, args,
                               negated ? (int)negLine   : (int)functorToken.Line,
                               negated ? (int)negColumn : (int)functorToken.Column);

            // Check for spawn annotation: Goal@AgentId
            if (Match(TokenType.AT))
            {
                var agentToken = Consume(TokenType.ATOM, "Expected agent identifier after @");
                return new SpawnGoal(goal, agentToken.Lexeme, (int)functorToken.Line, (int)functorToken.Column);
            }

            return goal;
        }

        // Otherwise, try to parse as infix comparison (e.g., X < Y, X? mod P? =:= 0)
        // Use ParseExpression(6) to parse arithmetic but stop at comparison operators
        var left = ParseExpression(6);

        // Check for comparison operator
        if (Check(TokenType.LESS) || Check(TokenType.GREATER) ||
            Check(TokenType.LESS_EQUAL) || Check(TokenType.GREATER_EQUAL) ||
            Check(TokenType.EQUALS) || Check(TokenType.ARITH_EQUAL) ||
            Check(TokenType.ARITH_NOT_EQUAL) || Check(TokenType.GROUND_EQUAL) ||
            // Standard-order term-comparison guards (T011/FR-037): @< @> @=< @>=.
            // The transform reuses opToken.Lexeme as the functor → @<(L,R).
            Check(TokenType.AT_LESS) || Check(TokenType.AT_GREATER) ||
            Check(TokenType.AT_LESS_EQUAL) || Check(TokenType.AT_GREATER_EQUAL))
        {
            var opToken = Advance();
            var right   = ParseExpression(6);

            // Transform infix to prefix: X < Y -> <(X, Y)
            // For negation: ~(X =?= Y) -> use ~=?= functor convention
            var functor = negated ? "~" + opToken.Lexeme : opToken.Lexeme;
            return new Goal(functor, new Term[] { left, right },
                            negated ? (int)negLine   : (int)opToken.Line,
                            negated ? (int)negColumn : (int)opToken.Column);
        }

        // Not a valid guard or goal
        throw new CompileError(
            "Expected predicate name or comparison",
            Peek().Line, Peek().Column, phase: "parser");
    }

    // Convert a Goal to a Term representation (for disjunction)
    private Term GoalToTerm(object goal)
    {
        if (goal is Goal g) return new StructTerm(g.Functor, g.Args, g.Line, g.Column);
        throw new CompileError("Expected goal", 0, 0, phase: "parser");
    }

    // Atom: functor(arg1, arg2, ...) or Var := Expr or Var =.. Expr (for clause heads)
    private Atom ParseAtom()
    {
        // Check for := or =.. pattern: Var := Expr or Var =.. Expr or _ := Expr
        if (Check(TokenType.VARIABLE) || Check(TokenType.READER) || Check(TokenType.UNDERSCORE))
        {
            var varToken      = Advance();
            bool isReader     = varToken.Type == TokenType.READER;
            bool isUnderscore = varToken.Type == TokenType.UNDERSCORE;
            if (Match(TokenType.ASSIGN))
            {
                // Parse as ':='(Var, Expr) or ':='(_, Expr)
                var lhsTerm = isUnderscore
                    ? (Term)new UnderscoreTerm((int)varToken.Line, (int)varToken.Column)
                    : (Term)new VarTerm(varToken.Lexeme, isReader, (int)varToken.Line, (int)varToken.Column);
                var expr = ParseTerm();
                return new Atom(":=", new Term[] { lhsTerm, expr }, (int)varToken.Line, (int)varToken.Column);
            }
            else if (Match(TokenType.UNIV))
            {
                // Parse as '=..'(Var, Expr)
                var varTerm = new VarTerm(varToken.Lexeme, isReader, (int)varToken.Line, (int)varToken.Column);
                var expr    = ParseTerm();
                return new Atom("=..", new Term[] { varTerm, expr }, (int)varToken.Line, (int)varToken.Column);
            }
            else if (Match(TokenType.UNIV_DECOMPOSE))
            {
                // Parse as '..='(Var, Expr)
                var varTerm = new VarTerm(varToken.Lexeme, isReader, (int)varToken.Line, (int)varToken.Column);
                var expr    = ParseTerm();
                return new Atom("..=", new Term[] { varTerm, expr }, (int)varToken.Line, (int)varToken.Column);
            }
            else if (Match(TokenType.EQUALS))
            {
                // Parse as '='(Var, Term) - unification
                var varTerm = new VarTerm(varToken.Lexeme, isReader, (int)varToken.Line, (int)varToken.Column);
                var term    = ParseTerm();
                return new Atom("=", new Term[] { varTerm, term }, (int)varToken.Line, (int)varToken.Column);
            }
            else
            {
                // Not an assignment - put variable back by rewinding
                _current--;
            }
        }

        var functorToken = Consume(TokenType.ATOM, "Expected predicate name");
        var atomArgs     = new List<Term>();

        if (Match(TokenType.LPAREN))
        {
            if (!Check(TokenType.RPAREN))
            {
                atomArgs.Add(ParseTerm());

                while (Match(TokenType.COMMA))
                {
                    atomArgs.Add(ParseTerm());
                }
            }

            Consume(TokenType.RPAREN, "Expected \")\" after arguments");
        }

        // Check if this is followed by =.. (e.g., foo(a,b) =.. L)
        if (Match(TokenType.UNIV))
        {
            // Convert the already-parsed atom to a StructTerm
            var leftTerm  = new StructTerm(functorToken.Lexeme, atomArgs, (int)functorToken.Line, (int)functorToken.Column);
            var rightTerm = ParseTerm();
            return new Atom("=..", new Term[] { leftTerm, rightTerm }, (int)functorToken.Line, (int)functorToken.Column);
        }

        // Check if this is followed by = (e.g., foo = bar, foo(a) = X)
        if (Match(TokenType.EQUALS))
        {
            var leftTerm = atomArgs.Count == 0
                ? (Term)new ConstTerm(functorToken.Lexeme, (int)functorToken.Line, (int)functorToken.Column)
                : (Term)new StructTerm(functorToken.Lexeme, atomArgs, (int)functorToken.Line, (int)functorToken.Column);
            var rightTerm = ParseTerm();
            return new Atom("=", new Term[] { leftTerm, rightTerm }, (int)functorToken.Line, (int)functorToken.Column);
        }

        return new Atom(functorToken.Lexeme, atomArgs, (int)functorToken.Line, (int)functorToken.Column);
    }

    // Goal: same as Atom, or assignment (Var := Expr) or univ (Var =.. Expr)
    // Also handles remote goals: Module # Goal
    private Goal ParseGoal()
    {
        // Check for assignment or univ: Var := Expr or Var =.. Expr
        // Also check for dynamic remote goal: Var # Goal
        if (Check(TokenType.VARIABLE) || Check(TokenType.READER))
        {
            var varToken  = Advance();
            bool isReader = varToken.Type == TokenType.READER;

            // Check for dynamic remote goal: Var # Goal (e.g., M # factorial(5, R))
            if (Match(TokenType.HASH))
            {
                var moduleTerm = new VarTerm(varToken.Lexeme, isReader, (int)varToken.Line, (int)varToken.Column);
                var innerGoal  = ParseGoal();
                return new RemoteGoal(moduleTerm, innerGoal, (int)varToken.Line, (int)varToken.Column);
            }
            else if (Match(TokenType.ASSIGN))
            {
                // Parse as ':='(Var, Expr)
                var varTerm = new VarTerm(varToken.Lexeme, isReader, (int)varToken.Line, (int)varToken.Column);
                var expr    = ParseTerm();
                return new Goal(":=", new Term[] { varTerm, expr }, (int)varToken.Line, (int)varToken.Column);
            }
            else if (Match(TokenType.UNIV))
            {
                // Parse as '=..'(Var, Expr)
                var varTerm = new VarTerm(varToken.Lexeme, isReader, (int)varToken.Line, (int)varToken.Column);
                var expr    = ParseTerm();
                return new Goal("=..", new Term[] { varTerm, expr }, (int)varToken.Line, (int)varToken.Column);
            }
            else if (Match(TokenType.UNIV_DECOMPOSE))
            {
                // Parse as '..='(Var, Expr)
                var varTerm = new VarTerm(varToken.Lexeme, isReader, (int)varToken.Line, (int)varToken.Column);
                var expr    = ParseTerm();
                return new Goal("..=", new Term[] { varTerm, expr }, (int)varToken.Line, (int)varToken.Column);
            }
            else if (Match(TokenType.EQUALS))
            {
                // Parse as '='(Var, Term) - unification
                var varTerm = new VarTerm(varToken.Lexeme, isReader, (int)varToken.Line, (int)varToken.Column);
                var term    = ParseTerm();
                return new Goal("=", new Term[] { varTerm, term }, (int)varToken.Line, (int)varToken.Column);
            }
            else
            {
                // Not an assignment or univ - this is an error in goal position
                throw new CompileError(
                    $"Expected predicate name or assignment, got variable \"{varToken.Lexeme}\"",
                    varToken.Line, varToken.Column, phase: "parser");
            }
        }

        var functorToken = Consume(TokenType.ATOM, "Expected predicate name");
        var goalArgs     = new List<Term>();

        if (Match(TokenType.LPAREN))
        {
            if (!Check(TokenType.RPAREN))
            {
                goalArgs.Add(ParseTerm());

                while (Match(TokenType.COMMA))
                {
                    goalArgs.Add(ParseTerm());
                }
            }

            Consume(TokenType.RPAREN, "Expected \")\" after arguments");
        }

        // Check for static remote goal: Module # Goal (e.g., math # factorial(5, R))
        if (Match(TokenType.HASH))
        {
            // Module name cannot have arguments
            if (goalArgs.Count > 0)
            {
                throw new CompileError(
                    $"Module name cannot have arguments: {functorToken.Lexeme}",
                    functorToken.Line, functorToken.Column, phase: "parser");
            }
            var moduleTerm = new ConstTerm(functorToken.Lexeme, (int)functorToken.Line, (int)functorToken.Column);
            var innerGoal  = ParseGoal();
            return new RemoteGoal(moduleTerm, innerGoal, (int)functorToken.Line, (int)functorToken.Column);
        }

        // Check if this is followed by =.. (e.g., foo(a,b) =.. L)
        if (Match(TokenType.UNIV))
        {
            // Convert the already-parsed atom to a StructTerm
            var leftTerm  = new StructTerm(functorToken.Lexeme, goalArgs, (int)functorToken.Line, (int)functorToken.Column);
            var rightTerm = ParseTerm();
            return new Goal("=..", new Term[] { leftTerm, rightTerm }, (int)functorToken.Line, (int)functorToken.Column);
        }

        var goalResult = new Goal(functorToken.Lexeme, goalArgs, (int)functorToken.Line, (int)functorToken.Column);

        // Check for spawn annotation: Goal@AgentId
        if (Match(TokenType.AT))
        {
            var agentToken = Consume(TokenType.ATOM, "Expected agent identifier after @");
            return new SpawnGoal(goalResult, agentToken.Lexeme, (int)functorToken.Line, (int)functorToken.Column);
        }

        return goalResult;
    }

    // Guard: same as Goal but marked as guard
    private Guard ParseGuard()
    {
        var functorToken = Consume(TokenType.ATOM, "Expected guard predicate name");
        var args         = new List<Term>();

        if (Match(TokenType.LPAREN))
        {
            if (!Check(TokenType.RPAREN))
            {
                args.Add(ParseTerm());

                while (Match(TokenType.COMMA))
                {
                    args.Add(ParseTerm());
                }
            }

            Consume(TokenType.RPAREN, "Expected \")\" after arguments");
        }

        return new Guard(functorToken.Lexeme, args, (int)functorToken.Line, (int)functorToken.Column);
    }

    // Term: variable, structure, list, constant, underscore, tuple, or expression
    private Term ParseTerm() => ParseExpression();

    // Expression parsing with precedence (Pratt parsing)
    // This handles arithmetic operators with proper precedence
    private Term ParseExpression(int minPrecedence = 0)
    {
        var left = ParsePrimary();

        while (IsOperator(Peek()) && Precedence(Peek()) >= minPrecedence)
        {
            var op    = Advance();
            var right = ParseExpression(Precedence(op) + 1);
            left = new StructTerm(OperatorFunctor(op), new List<Term> { left, right }, (int)op.Line, (int)op.Column);
        }

        return left;
    }

    // Primary expression: variable, number, string, list, structure, parenthesized, unary minus
    private Term ParsePrimary()
    {
        // Operator as functor (for type definitions like Exp ::= +(Exp?, Exp?))
        // Must check BEFORE unary minus so -(X,Y) is parsed as struct, not neg((X,Y))
        if (Check(TokenType.PLUS) || Check(TokenType.MINUS) || Check(TokenType.STAR) ||
            Check(TokenType.SLASH) || Check(TokenType.SLASH_SLASH) || Check(TokenType.MOD))
        {
            // Look ahead: if followed by (, treat as functor
            if (_current + 1 < Tokens.Count && Tokens[(int)(_current + 1)].Type == TokenType.LPAREN)
            {
                var functorToken = Advance();
                Advance(); // consume (
                var funcArgs = new List<Term>();
                if (!Check(TokenType.RPAREN))
                {
                    funcArgs.Add(ParseExpression());
                    while (Match(TokenType.COMMA))
                    {
                        funcArgs.Add(ParseExpression());
                    }
                }
                Consume(TokenType.RPAREN, "Expected \")\" after operator struct arguments");
                return new StructTerm(functorToken.Lexeme, funcArgs, (int)functorToken.Line, (int)functorToken.Column);
            }
            // Otherwise fall through - will be handled as unary minus or infix operator
        }

        // Unary minus: -X becomes neg(X)
        if (Match(TokenType.MINUS))
        {
            var minusToken = Previous();
            var operand    = ParsePrimary();
            return new StructTerm("neg", new List<Term> { operand }, (int)minusToken.Line, (int)minusToken.Column);
        }

        // Variable or Reader - check for := assignment
        if (Check(TokenType.VARIABLE) || Check(TokenType.READER))
        {
            var token     = Advance();
            bool isReader = token.Type == TokenType.READER;

            // Check for := assignment (Var := Expr)
            if (Match(TokenType.ASSIGN))
            {
                var varTerm = new VarTerm(token.Lexeme, isReader, (int)token.Line, (int)token.Column);
                var expr    = ParseExpression();
                return new StructTerm(":=", new List<Term> { varTerm, expr }, (int)token.Line, (int)token.Column);
            }

            return new VarTerm(token.Lexeme, isReader, (int)token.Line, (int)token.Column);
        }

        // Underscore (anonymous variable) - can have reader mark: _ or _?
        if (Match(TokenType.UNDERSCORE))
        {
            var token    = Previous();
            bool isReader = Match(TokenType.QUESTION);
            return new UnderscoreTerm((int)token.Line, (int)token.Column, isReader: isReader);
        }

        // Number
        if (Check(TokenType.NUMBER))
        {
            var token = Advance();
            // Check for invalid reader mark on number
            if (Check(TokenType.QUESTION))
            {
                throw new CompileError(
                    "Reader mark \"?\" can only be applied to variables, not numbers",
                    Peek().Line, Peek().Column, phase: "parser");
            }
            return new ConstTerm(token.Literal, (int)token.Line, (int)token.Column);
        }

        // String - preserve quotes for type checking string detection
        if (Check(TokenType.STRING))
        {
            var token = Advance();
            // Check for invalid reader mark on string
            if (Check(TokenType.QUESTION))
            {
                throw new CompileError(
                    "Reader mark \"?\" can only be applied to variables, not strings",
                    Peek().Line, Peek().Column, phase: "parser");
            }
            // Wrap in quotes so type checker can distinguish strings from atoms
            return new ConstTerm($"\"{token.Literal}\"", (int)token.Line, (int)token.Column);
        }

        // List
        if (Check(TokenType.LBRACKET))
        {
            return ParseList();
        }

        // Parenthesized expression - could be tuple (A, B) or single term (A) or arithmetic (A + B)
        if (Match(TokenType.LPAREN))
        {
            var startToken = Previous();
            var terms      = new List<Term>();

            // Parse first term (which may be an expression)
            terms.Add(ParseExpression());

            // Check for comma - indicates tuple/conjunction
            if (Match(TokenType.COMMA))
            {
                // Build right-associative tuple: (A, B, C) = ','(A, ','(B, C))
                terms.Add(ParseExpression());

                while (Match(TokenType.COMMA))
                {
                    terms.Add(ParseExpression());
                }

                Consume(TokenType.RPAREN, "Expected \")\" after tuple");

                // Build right-associative structure
                Term result = terms[terms.Count - 1];
                for (int i = terms.Count - 2; i >= 0; i--)
                {
                    result = new StructTerm(",", new List<Term> { terms[i], result }, (int)startToken.Line, (int)startToken.Column);
                }

                return result;
            }
            else
            {
                // Single parenthesized expression - return it
                Consume(TokenType.RPAREN, "Expected \")\" after expression");
                return terms[0];
            }
        }

        // Structure or Constant Atom
        if (Check(TokenType.ATOM))
        {
            var functorToken = Advance();

            // Structure with arguments
            if (Match(TokenType.LPAREN))
            {
                var structArgs = new List<Term>();

                if (!Check(TokenType.RPAREN))
                {
                    structArgs.Add(ParseExpression());

                    while (Match(TokenType.COMMA))
                    {
                        structArgs.Add(ParseExpression());
                    }
                }

                Consume(TokenType.RPAREN, "Expected \")\" after structure arguments");

                // Check for invalid reader mark on structure
                if (Check(TokenType.QUESTION))
                {
                    throw new CompileError(
                        $"Reader mark \"?\" can only be applied to variables, not structures like {functorToken.Lexeme}(...)",
                        Peek().Line, Peek().Column, phase: "parser");
                }

                return new StructTerm(functorToken.Lexeme, structArgs, (int)functorToken.Line, (int)functorToken.Column);
            }
            else
            {
                // Constant atom - check for invalid reader mark
                if (Check(TokenType.QUESTION))
                {
                    throw new CompileError(
                        $"Reader mark \"?\" can only be applied to variables, not constants like \"{functorToken.Lexeme}\"",
                        Peek().Line, Peek().Column, phase: "parser");
                }
                return new ConstTerm(functorToken.Lexeme, (int)functorToken.Line, (int)functorToken.Column);
            }
        }

        throw new CompileError(
            $"Expected term, got {Peek().Type}",
            Peek().Line, Peek().Column, phase: "parser");
    }

    // Check if token is an arithmetic operator, # (module operator), or \ (difference list)
    private static bool IsOperator(Token token) =>
        token.Type == TokenType.PLUS ||
        token.Type == TokenType.MINUS ||
        token.Type == TokenType.STAR ||
        token.Type == TokenType.SLASH ||
        token.Type == TokenType.SLASH_SLASH ||
        token.Type == TokenType.MOD ||
        token.Type == TokenType.LESS ||
        token.Type == TokenType.GREATER ||
        token.Type == TokenType.LESS_EQUAL ||
        token.Type == TokenType.GREATER_EQUAL ||
        token.Type == TokenType.EQUALS ||
        token.Type == TokenType.ARITH_EQUAL ||
        token.Type == TokenType.ARITH_NOT_EQUAL ||
        token.Type == TokenType.HASH ||
        token.Type == TokenType.BACKSLASH;

    // Get operator precedence
    private static int Precedence(Token op) => op.Type switch
    {
        TokenType.STAR or TokenType.SLASH or TokenType.SLASH_SLASH or TokenType.MOD => 20, // Multiplicative
        TokenType.PLUS or TokenType.MINUS                                             => 10, // Additive
        TokenType.HASH                                                                => 2,  // Module operator (very low, so M # foo(X,Y) parses correctly)
        TokenType.BACKSLASH                                                           => 1,  // Difference list operator (lowest, so [H|T]\T parses correctly)
        TokenType.LESS or TokenType.GREATER or TokenType.LESS_EQUAL or
            TokenType.GREATER_EQUAL or TokenType.EQUALS or
            TokenType.ARITH_EQUAL or TokenType.ARITH_NOT_EQUAL                       => 5,  // Comparison (lower than arithmetic)
        _                                                                             => 0,
    };

    // Get operator functor name for AST
    private static string OperatorFunctor(Token op) => op.Type switch
    {
        TokenType.PLUS            => "+",
        TokenType.MINUS           => "-",
        TokenType.STAR            => "*",
        TokenType.SLASH           => "/",
        TokenType.SLASH_SLASH     => "//",
        TokenType.MOD             => "mod",
        TokenType.LESS            => "<",
        TokenType.GREATER         => ">",
        TokenType.LESS_EQUAL      => "=<",
        TokenType.GREATER_EQUAL   => ">=",
        TokenType.EQUALS          => "=",
        TokenType.ARITH_EQUAL     => "=:=",
        TokenType.ARITH_NOT_EQUAL => "=\\=",
        TokenType.HASH            => "#",
        TokenType.BACKSLASH       => "\\",
        _ => throw new CompileError(
                 $"Unknown operator: {op.Type}",
                 op.Line, op.Column, phase: "parser"),
    };

    // List: [], [H|T], [X], [X,Y,Z], [X,Y,Z|T]
    private Term ParseList()
    {
        var bracketToken = Consume(TokenType.LBRACKET, "Expected \"[\"");

        // Empty list []
        if (Match(TokenType.RBRACKET))
        {
            // Check for invalid reader mark on list
            if (Check(TokenType.QUESTION))
            {
                throw new CompileError(
                    "Reader mark \"?\" can only be applied to variables, not lists",
                    Peek().Line, Peek().Column, phase: "parser");
            }
            return new ListTerm(null, null, (int)bracketToken.Line, (int)bracketToken.Column);
        }

        // Parse elements
        var elements = new List<Term>();
        Term? tail   = null;

        elements.Add(ParseTerm());

        // Parse remaining elements and check for tail
        while (Match(TokenType.COMMA))
        {
            elements.Add(ParseTerm());
        }

        // Check for tail syntax [H|T] or [X,Y|T]
        if (Match(TokenType.PIPE))
        {
            tail = ParseTerm();
            Consume(TokenType.RBRACKET, "Expected \"]\" after list tail");

            // Check for invalid reader mark on list
            if (Check(TokenType.QUESTION))
            {
                throw new CompileError(
                    "Reader mark \"?\" can only be applied to variables, not lists",
                    Peek().Line, Peek().Column, phase: "parser");
            }

            // Build right-associative list: [X,Y,Z|T] -> [X|[Y|[Z|T]]]
            Term result = tail;
            for (int i = elements.Count - 1; i >= 0; i--)
            {
                result = new ListTerm(elements[i], result, (int)bracketToken.Line, (int)bracketToken.Column);
            }
            return result;
        }

        Consume(TokenType.RBRACKET, "Expected \"]\" after list elements");

        // Check for invalid reader mark on list
        if (Check(TokenType.QUESTION))
        {
            throw new CompileError(
                "Reader mark \"?\" can only be applied to variables, not lists",
                Peek().Line, Peek().Column, phase: "parser");
        }

        // Build right-associative list: [X, Y, Z] -> [X|[Y|[Z|[]]]]
        Term listResult = new ListTerm(null, null, (int)bracketToken.Line, (int)bracketToken.Column); // []
        for (int i = elements.Count - 1; i >= 0; i--)
        {
            listResult = new ListTerm(elements[i], listResult, (int)bracketToken.Line, (int)bracketToken.Column);
        }

        return listResult;
    }

    // ============================================================================
    // Helper methods
    // ============================================================================

    private bool Match(TokenType type)
    {
        if (Check(type))
        {
            Advance();
            return true;
        }
        return false;
    }

    private bool Check(TokenType type)
    {
        if (IsAtEnd()) return false;
        return Peek().Type == type;
    }

    private Token Advance()
    {
        if (!IsAtEnd()) _current++;
        return Previous();
    }

    private Token Peek()     => Tokens[(int)_current];
    private Token Previous() => Tokens[(int)(_current - 1)];
    private bool  IsAtEnd()  => Peek().Type == TokenType.EOF;

    private Token Consume(TokenType type, string message)
    {
        if (Check(type)) return Advance();

        throw new CompileError(message, Peek().Line, Peek().Column, phase: "parser");
    }

    // ============================================================================
    // === Yardeni-Shapiro Type Declaration Parser Methods ===
    // ============================================================================

    /// <summary>Check if we're at a type definition or procedure declaration</summary>
    private bool IsTypeOrProcDeclaration()
    {
        // procedure keyword
        if (Check(TokenType.PROCEDURE)) return true;

        // TypeName ::= ... (type names are capitalized, tokenized as VARIABLE)
        if (Check(TokenType.VARIABLE) || Check(TokenType.READER))
        {
            // Look ahead for ::=
            var saved = _current;
            Advance(); // consume type name

            var isTypeDef = Check(TokenType.COLONCOLONEQ);

            _current = saved; // restore position
            return isTypeDef;
        }

        return false;
    }

    /// <summary>Check if we're at a type definition (TypeName ::= ... or TypeName(X) ::= ...)</summary>
    /// <remarks>Used to distinguish type definitions from clause heads starting with capitalized variable.</remarks>
    private bool IsTypeDefinition()
    {
        // TypeName ::= ... (type names are capitalized, tokenized as VARIABLE)
        if (Check(TokenType.VARIABLE) || Check(TokenType.READER))
        {
            // Look ahead for ::=, skipping optional type parameters (X, Y, ...)
            var saved = _current;
            Advance(); // consume type name

            // Skip optional type parameters: (X, Y, ...)
            if (Check(TokenType.LPAREN))
            {
                Advance(); // consume (
                int depth = 1;
                while (!IsAtEnd() && depth > 0)
                {
                    if (Check(TokenType.LPAREN)) depth++;
                    if (Check(TokenType.RPAREN)) depth--;
                    Advance();
                }
            }

            var isTypeDef = Check(TokenType.COLONCOLONEQ);

            _current = saved; // restore position
            return isTypeDef;
        }

        return false;
    }

    /// <summary>Parse a type definition: TypeName ::= alt ; alt ; alt.</summary>
    /// <remarks>Also supports parameterized: TypeName(X, Y) ::= alt ; alt.</remarks>
    /// <remarks>Also supports explicit dual definitions: TypeName? ::= alt.</remarks>
    private TypeDef ParseTypeDef()
    {
        var typeNameToken = Check(TokenType.READER)
            ? Advance()
            : Consume(TokenType.VARIABLE, "Expected type name");

        // For READER tokens (e.g., Channel?), append '?' to the name
        // This supports explicit dual type definitions
        var typeName = typeNameToken.Type == TokenType.READER
            ? $"{typeNameToken.Lexeme}?"
            : typeNameToken.Lexeme;
        var line   = (int)typeNameToken.Line;
        var column = (int)typeNameToken.Column;

        // Parse optional type parameters: (X, Y, ...)
        var typeParams = new List<string>();
        if (Match(TokenType.LPAREN))
        {
            var firstParam = Consume(TokenType.VARIABLE, "Expected type parameter name");
            typeParams.Add(firstParam.Lexeme);
            while (Match(TokenType.COMMA))
            {
                var param = Consume(TokenType.VARIABLE, "Expected type parameter name");
                typeParams.Add(param.Lexeme);
            }
            Consume(TokenType.RPAREN, "Expected \")\" after type parameters");
        }

        Consume(TokenType.COLONCOLONEQ, "Expected \"::=\" in type definition");

        // Parse alternatives separated by ;
        var alternatives = new List<TypeExpr>();
        alternatives.Add(ParseTypeAlt());

        while (Match(TokenType.SEMICOLON))
        {
            alternatives.Add(ParseTypeAlt());
        }

        Consume(TokenType.DOT, "Expected \".\" after type definition");

        return new TypeDef(typeName, alternatives, line, column, typeParams: typeParams);
    }

    /// <summary>
    /// Parse a single type alternative using unified term parsing.
    /// For explicit dual definitions like `Channel? ::= ch(Stream?, Stream)?.`,
    /// the trailing `?` on the structure is allowed and consumed. The duality
    /// is captured in the type name (Channel?), so the trailing `?` is
    /// documentation that confirms the definition is for the dual form.
    /// </summary>
    private TypeExpr ParseTypeAlt() => TypeConversion.TermToTypeExpr(ParseTypeAltTerm());

    /// <summary>
    /// Parse a term in type alternative context.
    /// Similar to ParseTerm() but allows trailing `?` on structures.
    /// </summary>
    private Term ParseTypeAltTerm() => ParseTypeAltExpression();

    /// <summary>
    /// Parse expression in type alternative context.
    /// Handles operators like \ for difference lists.
    /// </summary>
    private Term ParseTypeAltExpression(int minPrecedence = 0)
    {
        var left = ParseTypeAltPrimary();

        while (IsOperator(Peek()) && Precedence(Peek()) >= minPrecedence)
        {
            var op    = Advance();
            var right = ParseTypeAltExpression(Precedence(op) + 1);
            left = new StructTerm(OperatorFunctor(op), new List<Term> { left, right }, (int)op.Line, (int)op.Column);
        }

        // Check for trailing ? on the whole expression (for explicit duals)
        // This is allowed in type definitions and simply consumed
        Match(TokenType.QUESTION);

        return left;
    }

    /// <summary>
    /// Parse primary term in type alternative context.
    /// Allows trailing `?` on structures (for explicit dual definitions).
    /// </summary>
    private Term ParseTypeAltPrimary()
    {
        // Operator as functor (for type definitions like Exp ::= +(Exp?, Exp?))
        if (Check(TokenType.PLUS) || Check(TokenType.MINUS) || Check(TokenType.STAR) ||
            Check(TokenType.SLASH) || Check(TokenType.SLASH_SLASH) || Check(TokenType.MOD))
        {
            if (_current + 1 < Tokens.Count && Tokens[(int)(_current + 1)].Type == TokenType.LPAREN)
            {
                var functorToken = Advance();
                Advance(); // consume (
                var typeArgs = new List<Term>();
                if (!Check(TokenType.RPAREN))
                {
                    typeArgs.Add(ParseTypeAltExpression());
                    while (Match(TokenType.COMMA))
                    {
                        typeArgs.Add(ParseTypeAltExpression());
                    }
                }
                Consume(TokenType.RPAREN, "Expected \")\" after operator struct arguments");
                // Allow trailing ? on structure in type definitions
                Match(TokenType.QUESTION);
                return new StructTerm(functorToken.Lexeme, typeArgs, (int)functorToken.Line, (int)functorToken.Column);
            }
        }

        // Parameterized type reference in type body: TypeName(Arg1, Arg2, ...)
        // Uppercase names followed by ( are parameterized type refs, not structs.
        // Encode reader mode in functor name for type_conversion to decode.
        if ((Check(TokenType.VARIABLE) || Check(TokenType.READER)) &&
            _current + 1 < Tokens.Count && Tokens[(int)(_current + 1)].Type == TokenType.LPAREN)
        {
            var token    = Advance();
            bool isReader = token.Type == TokenType.READER;
            Advance(); // consume (
            var typeArgs = new List<Term>();
            if (!Check(TokenType.RPAREN))
            {
                typeArgs.Add(ParseTypeAltExpression());
                while (Match(TokenType.COMMA))
                {
                    typeArgs.Add(ParseTypeAltExpression());
                }
            }
            Consume(TokenType.RPAREN, "Expected \")\" after type arguments");
            bool trailingQ    = Match(TokenType.QUESTION);
            var effectiveName = (isReader || trailingQ) ? $"{token.Lexeme}?" : token.Lexeme;
            return new StructTerm(effectiveName, typeArgs, (int)token.Line, (int)token.Column);
        }

        // Variable or Reader (simple, non-parameterized)
        if (Check(TokenType.VARIABLE) || Check(TokenType.READER))
        {
            var token    = Advance();
            bool isReader = token.Type == TokenType.READER;
            return new VarTerm(token.Lexeme, isReader, (int)token.Line, (int)token.Column);
        }

        // Underscore (anonymous variable) - can have reader mark: _ or _?
        if (Match(TokenType.UNDERSCORE))
        {
            var token    = Previous();
            bool isReader = Match(TokenType.QUESTION);
            return new UnderscoreTerm((int)token.Line, (int)token.Column, isReader: isReader);
        }

        // Number
        if (Check(TokenType.NUMBER))
        {
            var token = Advance();
            return new ConstTerm(token.Literal, (int)token.Line, (int)token.Column);
        }

        // String
        if (Check(TokenType.STRING))
        {
            var token = Advance();
            return new ConstTerm($"\"{token.Literal}\"", (int)token.Line, (int)token.Column);
        }

        // List
        if (Check(TokenType.LBRACKET))
        {
            return ParseTypeAltList();
        }

        // Parenthesized expression or tuple
        if (Match(TokenType.LPAREN))
        {
            var startToken = Previous();
            var terms      = new List<Term>();
            terms.Add(ParseTypeAltExpression());

            if (Match(TokenType.COMMA))
            {
                terms.Add(ParseTypeAltExpression());
                while (Match(TokenType.COMMA))
                {
                    terms.Add(ParseTypeAltExpression());
                }
                Consume(TokenType.RPAREN, "Expected \")\" after tuple");
                Term result = terms[terms.Count - 1];
                for (int i = terms.Count - 2; i >= 0; i--)
                {
                    result = new StructTerm(",", new List<Term> { terms[i], result }, (int)startToken.Line, (int)startToken.Column);
                }
                // Allow trailing ? on parenthesized expression
                Match(TokenType.QUESTION);
                return result;
            }
            else
            {
                Consume(TokenType.RPAREN, "Expected \")\" after expression");
                // Allow trailing ? on parenthesized expression
                Match(TokenType.QUESTION);
                return terms[0];
            }
        }

        // Structure or Constant Atom
        if (Check(TokenType.ATOM))
        {
            var functorToken = Advance();

            if (Match(TokenType.LPAREN))
            {
                var structArgs = new List<Term>();
                if (!Check(TokenType.RPAREN))
                {
                    structArgs.Add(ParseTypeAltExpression());
                    while (Match(TokenType.COMMA))
                    {
                        structArgs.Add(ParseTypeAltExpression());
                    }
                }
                Consume(TokenType.RPAREN, "Expected \")\" after structure arguments");
                // Allow trailing ? on structure in type definitions (for explicit duals)
                Match(TokenType.QUESTION);
                return new StructTerm(functorToken.Lexeme, structArgs, (int)functorToken.Line, (int)functorToken.Column);
            }
            else
            {
                return new ConstTerm(functorToken.Lexeme, (int)functorToken.Line, (int)functorToken.Column);
            }
        }

        throw new CompileError(
            $"Expected type alternative term, got {Peek().Type}",
            Peek().Line, Peek().Column, phase: "parser");
    }

    /// <summary>
    /// Parse list in type alternative context.
    /// Allows trailing ? on lists (for explicit duals).
    /// </summary>
    private Term ParseTypeAltList()
    {
        var bracketToken = Consume(TokenType.LBRACKET, "Expected \"[\"");

        if (Match(TokenType.RBRACKET))
        {
            // Allow trailing ? on empty list in type definitions
            Match(TokenType.QUESTION);
            return new ListTerm(null, null, (int)bracketToken.Line, (int)bracketToken.Column);
        }

        var elements = new List<Term>();
        Term? tail   = null;

        elements.Add(ParseTypeAltTerm());

        while (Match(TokenType.COMMA))
        {
            elements.Add(ParseTypeAltTerm());
        }

        if (Match(TokenType.PIPE))
        {
            tail = ParseTypeAltTerm();
            Consume(TokenType.RBRACKET, "Expected \"]\" after list tail");
            // Allow trailing ? on list in type definitions
            Match(TokenType.QUESTION);
            Term result = tail;
            for (int i = elements.Count - 1; i >= 0; i--)
            {
                result = new ListTerm(elements[i], result, (int)bracketToken.Line, (int)bracketToken.Column);
            }
            return result;
        }

        Consume(TokenType.RBRACKET, "Expected \"]\" after list elements");
        // Allow trailing ? on list in type definitions
        Match(TokenType.QUESTION);

        Term listResult = new ListTerm(null, null, (int)bracketToken.Line, (int)bracketToken.Column);
        for (int i = elements.Count - 1; i >= 0; i--)
        {
            listResult = new ListTerm(elements[i], listResult, (int)bracketToken.Line, (int)bracketToken.Column);
        }
        return listResult;
    }

    /// <summary>
    /// Parse a procedure declaration: procedure name(Type?, Type).
    /// or: exported procedure name(Type?, Type).
    /// or: imported procedure [path#]name(Type?, Type).
    /// </summary>
    private ProcDecl ParseProcDeclaration()
    {
        // Check for 'exported' or 'imported' keyword before 'procedure'
        bool exported    = false;
        bool imported    = false;
        var  startLine   = Peek().Line;
        var  startColumn = Peek().Column;
        if (Check(TokenType.ATOM) && Peek().Lexeme == "exported")
        {
            Advance(); // consume 'exported'
            exported = true;
        }
        else if (Check(TokenType.ATOM) && Peek().Lexeme == "imported")
        {
            Advance(); // consume 'imported'
            imported = true;
        }
        Consume(TokenType.PROCEDURE, "Expected \"procedure\" keyword");
        var line   = (int)startLine;
        var column = (int)startColumn;

        // Parse procedure name, possibly with module path for imported procedures.
        // For imported: 'social#agent' -> modulePath='social', name='agent'
        //              'ui#actors#render' -> modulePath='ui#actors', name='render'
        //              'merge' -> modulePath=null, name='merge'
        string? modulePath = null;

        // Procedure name can be atom or operator (<, >, =<, >=, =:=, =\=, =?=, =)
        Token nameToken;
        switch (Peek().Type)
        {
            case TokenType.ATOM:
            case TokenType.LESS:
            case TokenType.GREATER:
            case TokenType.LESS_EQUAL:
            case TokenType.GREATER_EQUAL:
            case TokenType.ARITH_EQUAL:
            case TokenType.ARITH_NOT_EQUAL:
            case TokenType.GROUND_EQUAL:
            // Standard-order term-comparison guards (T011/FR-037): @< @> @=< @>=
            case TokenType.AT_LESS:
            case TokenType.AT_GREATER:
            case TokenType.AT_LESS_EQUAL:
            case TokenType.AT_GREATER_EQUAL:
            case TokenType.EQUALS:
            case TokenType.UNIV:
            case TokenType.UNIV_DECOMPOSE:
            case TokenType.ASSIGN:
                nameToken = Advance();
                break;
            default:
                throw new CompileError(
                    "Expected procedure name",
                    Peek().Line, Peek().Column, phase: "parser");
        }

        // For imported procedures, parse #-separated path: social#agent, ui#actors#render
        // The last component is the procedure name, everything before is the module path.
        var procName = nameToken.Lexeme;
        if (imported)
        {
            var parts = new List<string> { procName };
            while (Match(TokenType.HASH))
            {
                // Next token should be an atom (next path component or procedure name)
                if (!Check(TokenType.ATOM))
                {
                    throw new CompileError(
                        "Expected module path component or procedure name after \"#\"",
                        Peek().Line, Peek().Column, phase: "parser");
                }
                parts.Add(Advance().Lexeme);
            }
            // Last part is the procedure name, rest is the module path
            procName = parts[parts.Count - 1];
            if (parts.Count > 1)
            {
                modulePath = string.Join("#", parts.GetRange(0, parts.Count - 1));
            }
        }

        // Parentheses are optional for nullary procedures:
        // procedure play_introduction.    (valid - nullary)
        // procedure play_introduction().  (valid - nullary with explicit parens)
        // procedure double(Number?, Number). (valid - with args)
        var argTypes = new List<TypeExpr>();
        if (Match(TokenType.LPAREN))
        {
            // Parse argument types if not empty
            if (!Check(TokenType.RPAREN))
            {
                argTypes.Add(ParseProcArgType());
                while (Match(TokenType.COMMA))
                {
                    argTypes.Add(ParseProcArgType());
                }
            }
            Consume(TokenType.RPAREN, "Expected \")\" after procedure arguments");
        }
        // If no LPAREN, argTypes remains empty (nullary procedure)

        Consume(TokenType.DOT, "Expected \".\" after procedure declaration");

        return new ProcDecl(procName, argTypes, line, column, exported: exported, imported: imported, modulePath: modulePath);
    }

    /// <summary>
    /// Parse a procedure argument type: TypeName, TypeName?, _, _?,
    /// or qualified: mod#TypeName, mod#TypeName?
    /// </summary>
    private TypeExpr ParseProcArgType()
    {
        var line   = (int)Peek().Line;
        var column = (int)Peek().Column;

        // Primitive: _ or _?
        if (Match(TokenType.UNDERSCORE))
        {
            bool isInput = Match(TokenType.QUESTION);
            return new PrimitiveModeAlt(isInput, line, column);
        }

        // Qualified type reference: atom # TypeName or atom # TypeName?
        // e.g., social#AgentChannel, social#AgentChannel?
        if (Check(TokenType.ATOM) && _current + 1 < Tokens.Count && Tokens[(int)(_current + 1)].Type == TokenType.HASH)
        {
            // Collect path: atom # atom # ... # TypeName
            var pathParts = new List<string>();
            while (Check(TokenType.ATOM) && _current + 1 < Tokens.Count && Tokens[(int)(_current + 1)].Type == TokenType.HASH)
            {
                pathParts.Add(Advance().Lexeme); // consume atom
                Advance(); // consume #
            }
            // Now parse the final type name (must be VARIABLE or READER)
            if (Check(TokenType.VARIABLE) || Check(TokenType.READER))
            {
                var typeToken = Advance();
                bool isInput  = typeToken.Type == TokenType.READER || Match(TokenType.QUESTION);
                var qualifiedName = $"{string.Join("#", pathParts)}#{typeToken.Lexeme}";
                return new TypeRef(qualifiedName, line, column, isInput: isInput);
            }
            throw new CompileError(
                "Expected type name after module path in qualified type reference",
                Peek().Line, Peek().Column, phase: "parser");
        }

        // Type reference with optional type arguments and optional mode
        if (Check(TokenType.VARIABLE) || Check(TokenType.READER))
        {
            var token    = Advance();
            var baseName = token.Lexeme;

            // Parse optional type arguments: (Type1, Type2, ...)
            var typeArgs = new List<TypeExpr>();
            if (Match(TokenType.LPAREN))
            {
                typeArgs.Add(ParseProcArgType()); // recursive — supports nested parameterized types
                while (Match(TokenType.COMMA))
                {
                    typeArgs.Add(ParseProcArgType());
                }
                Consume(TokenType.RPAREN, "Expected \")\" after type arguments");
            }

            bool isInput = token.Type == TokenType.READER || Match(TokenType.QUESTION);
            return new TypeRef(baseName, line, column, isInput: isInput, typeArgs: typeArgs);
        }

        throw new CompileError(
            "Expected type in procedure argument",
            Peek().Line, Peek().Column, phase: "parser");
    }

    // ============================================================================
    // Check that all clauses for each procedure are contiguous in the source.
    // GLP requires clauses to be grouped together - non-contiguous clauses
    // cause the compiler to generate incorrect bytecode.
    // ============================================================================

    /// <summary>Check that all clauses for each procedure are contiguous in the source.</summary>
    private void CheckContiguousClauses(IList<Procedure> procedures)
    {
        var seen = new Dictionary<string, Procedure>(StringComparer.Ordinal); // signature -> first occurrence

        foreach (var proc in procedures)
        {
            var sig = $"{proc.Name}/{proc.Arity}";

            if (seen.TryGetValue(sig, out var first))
            {
                throw new CompileError(
                    $"Non-contiguous clauses for \"{sig}\".\n" +
                    $"  First group at line {first.Line}, second group at line {proc.Line}.\n" +
                    "  All clauses for a predicate must be together in the source file.",
                    proc.Line, proc.Column, phase: "parser");
            }

            seen[sig] = proc;
        }
    }
}
