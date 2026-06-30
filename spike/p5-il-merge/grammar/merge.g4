// =============================================================================
// merge.g4 — minimal combined ANTLR grammar covering exactly the tokens of
// merge/3 clause 1 (programs/paper/merge.glp:8):
//
//   merge([X|Xs], Ys, [X?|Zs?]) :- merge(Ys?, Xs?, Zs).
//
// Tokens covered (P5 spike B1): lowercase ATOM (functor/constant), uppercase VAR,
// the reader marker `?`, list cons `[ H | T ]`, `:-` neck, `,`, the guard
// separator `|` (the BAR token — same lexeme as the list-cons bar; clause 1 uses
// it only as a cons bar, but a guarded clause `head :- guards | body .` reuses
// this exact token), compound/struct terms `f(...)`, and the `.` clause end.
// Styled after Csharp/qhxm/grammar/{QhxmLexer,QhxmParser}.g4.
// =============================================================================
grammar merge;

// ---- parser rules ------------------------------------------------------------
program   : clause+ EOF ;

// A clause is a head, an optional `:- (guards |)? body`, and a `.`.
clause    : head ( NECK ( guards BAR )? body )? DOT ;

head      : compound ;

guards    : goal ( COMMA goal )* ;
body      : goal ( COMMA goal )* ;
goal      : compound ;

compound  : ATOM LPAREN termList RPAREN ;

termList  : term ( COMMA term )* ;

term      : var
          | list
          | compound
          | atom
          ;

// `X` (writer) or `X?` (reader): the QUESTION marks the reader half.
var       : VAR QUESTION? ;

// lowercase constant (e.g. an atom used as a value)
atom      : ATOM ;

// [], [a, b], [H | T], [a, b | T]
list      : LBRACK ( listItems )? RBRACK ;
listItems : term ( COMMA term )* ( BAR term )? ;

// ---- lexer rules -------------------------------------------------------------
NECK      : ':-' ;
QUESTION  : '?' ;
LBRACK    : '[' ;
RBRACK    : ']' ;
BAR       : '|' ;      // list-cons bar AND guard separator (same token)
LPAREN    : '(' ;
RPAREN    : ')' ;
COMMA     : ',' ;
DOT       : '.' ;

ATOM      : [a-z] [A-Za-z0-9_]* ;
VAR       : [A-Z_] [A-Za-z0-9_]* ;

LINE_COMMENT : '%' ~[\r\n]* -> channel(HIDDEN) ;
WS           : [ \t\r\n]+    -> channel(HIDDEN) ;
