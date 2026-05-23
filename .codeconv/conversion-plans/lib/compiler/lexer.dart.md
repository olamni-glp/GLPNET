---
path: lib/compiler/lexer.dart
cycle_group_id: 9
scc_siblings: []
generated_at: 2026-05-21T14:47:04Z
source_sha256: f9c89267ee74e7e9151a0e97e57b00fd9db39e01d949bad680f3a6e51d4abe75
schema_version: 1
---

# Conversion Plan: lib/compiler/lexer.dart

## 1. Source Analysis

The file `glp_runtime_net/lib/compiler/lexer.dart` (353 lines) defines a single
public class `Lexer` — a hand-rolled lexical analyser for GLP source code.
The file has two imports:

- `import 'token.dart';` — supplies `Token`, `TokenType` (enum).
- `import 'error.dart';` — supplies `CompileError` (thrown on lexical errors,
  always with named arg `phase: 'lexer'`).

State held by `Lexer`:

- `final String source;` — write-once via positional-initialising-formal
  constructor `Lexer(this.source);`. Read on the hot path.
- `int _current = 0;` — cursor offset into `source` (mutable).
- `int _line = 1;` — current line counter (mutable; 1-based).
- `int _column = 1;` — current column counter (mutable; 1-based; reset to 0
  inside string/block-comment LF branches so the next `_advance()` brings it
  to 1).

Public surface:

- Single ctor `Lexer(this.source)`.
- `List<Token> tokenize()` — two-phase loop: `_skipWhitespaceAndComments()`
  then `_scanToken()`; after the loop, appends a terminal
  `Token(TokenType.EOF, '', _line, _column)`; returns the accumulated list.

Private helpers (in declaration order):

- `Token _scanToken()` — captures `startLine`/`startColumn`, consumes one
  character via `_advance()`, dispatches via a giant `switch (c)` over the
  first code unit. Cases enumerate every single-char punctuation token
  (`(`, `)`, `[`, `]`, `{`, `}`, `,`, `?`, `|`, `;`, `~`, `#`, `\`, `@`,
  `+`, `*`, `<`); arms with multi-char lookahead use `_match(...)` for
  compound tokens (`..=` ⇒ UNIV_DECOMPOSE; `//` ⇒ SLASH_SLASH; `>=` ⇒
  GREATER_EQUAL; `=..` ⇒ UNIV; `=<` ⇒ LESS_EQUAL; `=:=` ⇒ ARITH_EQUAL;
  `=\=` ⇒ ARITH_NOT_EQUAL; `=?=` ⇒ GROUND_EQUAL; `::=` ⇒ COLONCOLONEQ;
  `:-` ⇒ IMPLIES; `:=` ⇒ ASSIGN). The `.` case has a pseudo-rollback
  (`_current--; _column--;`) when `..` is seen but not followed by `=`.
  The `'_'` case decides UNDERSCORE vs identifier via lookahead. Quote
  cases (`"` and `'`) stack and delegate to `_string(c, ...)`. The `'-'`
  case decides between negative-number literal and MINUS operator. The
  default arm dispatches to `_number` / `_identifier` on digit/alpha or
  throws `CompileError('Unexpected character: $c', ...)`.

- `Token _identifier(int start, int line, int column)` — consumes the
  alpha-numeric run, slices `source.substring(start, _current)`, special-
  cases the keywords `mod` (only if not followed by `(` — to permit `mod(...)`
  as a predicate call) and `procedure`. Then classifies as variable
  (uppercase first char, or `_` + uppercase second char — covers named
  anonymous variables like `_Out`) versus atom (lowercase). For variables,
  also detects the reader suffix `?` and emits READER (consuming the `?`).

- `Token _number(int start, int line, int column)` — handles optional leading
  `-` (rewinds `_current = start + 1` so the digit loop sees it from start+1
  but `source.substring(start, _current)` later captures the sign). Integer
  digit run; optional decimal `.` lookahead requiring a digit after the dot;
  parses `double.parse(text)` vs `int.parse(text)` based on
  `text.contains('.')`; emits NUMBER with the parsed numeric literal as the
  Token's optional `literal` payload.

- `Token _string(String quote, int line, int column)` — accumulates into a
  `StringBuffer`. Handles escape sequences (`\n \t \r \\ \" \'`) via inner
  switch; unrecognised escapes fall through to `buffer.write(_peek())`
  (lenient: the raw escaped char is appended). Embedded LF inside a string
  increments `_line` and resets `_column = 0`. On EOF before closing quote
  throws `CompileError('Unterminated string', line, column, phase: 'lexer')`.
  Final dispatch on `quote == "'"`: emits ATOM (single-quoted; functor-
  callable) with NO literal payload (ctor optional `literal` defaults null);
  otherwise emits STRING (double-quoted) passing `buffer.toString()` for
  BOTH the `lexeme` AND `literal` slots.

- `void _skipWhitespaceAndComments()` — hand-rolled DFA over the first
  character: space/tab/CR consumed silently; LF consumed + `_line++` +
  `_column = 1`; `%` starts a line comment consumed up to (but not past)
  the next LF; `/*` starts a block comment consumed up to and including
  `*/`, with embedded LF tracked via `_line++; _column = 0;` (note the 0
  — anticipating the next `_advance` to bring it to 1). Trailing `else
  break;` exits when no whitespace/comment first-character matched.

- Hot-path primitives:
  - `String _advance()` — `_column++; return source[_current++];`
  - `bool _match(String expected)` — at-end guard, equality check on
    `source[_current]`, conditional `_advance()`.
  - `String _peek()` — `_isAtEnd() ? '\x00' : source[_current];`
  - `String _peekNext()` — `_current + 1 >= source.length ? '\x00' :
    source[_current + 1];`
  - `bool _isAtEnd()` — `_current >= source.length;`
  - `bool _isDigit(String c)` — `'\x00'` sentinel guard then
    `c.codeUnitAt(0)` range `'0'..'9'`.
  - `bool _isAlpha(String c)` — same shape; range `'a'..'z'` or `'A'..'Z'`
    or `c == '_'`.
  - `bool _isAlphaNumeric(String c)` — `_isAlpha(c) || _isDigit(c)`.
  - `bool _isUpper(String c)` — same shape; range `'A'..'Z'`.
  - `Token _makeToken(TokenType type, int line, int column)` — single-char
    lexeme via `source.substring(_current - 1, _current)`.

Documentation: triple-slash `///` doc comments on the class and on
`tokenize`, `_scanToken`, `_identifier`, `_number`, `_string`,
`_skipWhitespaceAndComments`; numerous inline `//` comments at section
headers and at every non-obvious branch.

Notable absences (confirmed by inspection): no async / Future / Stream /
isolate / mixin / extension / generics / `late` / records / bitwise / shift
/ overflow paths anywhere in this file.

## 2. Dart → C#/.NET Conversion Plan

Mirrors the convspec's 13 `constructs` (10 non-trivial + 2 trivial doc/comment).
Each construct below maps Dart → C#/.NET per the ratified convspec
`.codeconv/conversion-specs/lib/compiler/lexer.dart.md`; key idioms are
restated for traceability.

### Construct 1 — `dart.class.mutable_lexer_with_final_source_and_int_cursors`

- **Dart**: `class Lexer { final String source; int _current = 0; int _line
  = 1; int _column = 1; Lexer(this.source); ... }`.
- **C#/.NET**: reference `class Lexer` (NOT `record`, NOT `struct`) with one
  get-only auto-property `public string Source { get; }` initialised from
  ctor `public Lexer(string source) { Source = source; }` and three mutable
  private `long` fields `_current = 0`, `_line = 1`, `_column = 1`. Reference
  semantics preserved — `Tokenize()` mutates the cursors observed by
  `ScanToken()`. Dart positional-initialising-formal `this.source` expands
  to explicit assignment in ctor (C# has no `this.x` ctor-parameter sugar).
- **Idiom**: rf-dart-final-field-class-to-csharp-getonly-class (reuse).
- **Width**: cursors stay `long` per rf-dart-int-to-csharp-long-width
  (recurring); privacy: Dart `_` → C# `private` per
  rf-dart-leading-underscore-privacy-to-csharp-private (recurring).

### Construct 2 — `dart.string_indexing.code_unit_as_one_char_string`

- **Dart**: `_advance` / `_peek` / `_peekNext` all return one-char `String`;
  `source[_current]` is a one-code-unit `String`.
- **C#/.NET**: change return types of `_advance`/`_peek`/`_peekNext` from
  `string` to `char`. Replace Dart `'\x00'` sentinel with C# `'\0'` (both
  U+0000). All ASCII-literal comparisons (`c == '('`) remain textually
  identical; the RHS is a `char` literal in C# instead of a one-char
  `String` in Dart — identical underlying code unit, no allocation.
- **Idiom**: rf-dart-string-indexing-to-csharp-char-indexing (NEW).
- **Unicode note**: every recognised lexeme is basic-Latin (U+0000..U+007F)
  so surrogate pairs never appear at the recognition path; surrogate pairs
  in string-literal bodies and comments are preserved bit-for-bit because
  both `StringBuffer.write` and `StringBuilder.Append(char)` append single
  code units. Column counting per code unit is preserved (a surrogate pair
  counts as 2 columns in both languages — intentional preservation).

### Construct 3 — `dart.match_advance_helper_one_char_String_expected`

- **Dart**: `bool _match(String expected) { ... source[_current] != expected
  ... }`.
- **C#/.NET**: `private bool Match(char expected)` — parameter shifts
  `string` → `char` to mirror Construct 2's `string[i]→char` shift. Body:
  `if (IsAtEnd()) return false; if (Source[(int)_current] != expected)
  return false; Advance(); return true;`. Call sites (`_match('.')`,
  `_match('=')`, …) translate to identical-looking C# char literals; no
  per-call allocation (was implicit one-char String allocation in Dart).
- **Idiom**: rf-dart-string-indexing-to-csharp-char-indexing (NEW; same
  idiom as Construct 2).

### Construct 4 — `dart.utf16_codeunit_predicates_via_codeUnitAt`

- **Dart**: `_isDigit` / `_isAlpha` / `_isAlphaNumeric` / `_isUpper` —
  `'\x00'` guard then `c.codeUnitAt(0)` ASCII-range comparison.
- **C#/.NET**: `private static bool IsDigit(char c) { if (c == '\0') return
  false; int code = (int)c; return code >= (int)'0' && code <= (int)'9'; }`
  and analogues. **DO NOT** use `char.IsDigit` / `char.IsLetter` — those
  accept the full Unicode category (Arabic-Indic, Devanagari, fullwidth,
  Greek letters, …) and would silently broaden the lexer's accepted
  alphabet. The implicit/explicit `char→int` promotion is the direct
  counterpart of Dart's `codeUnitAt(0)`. Methods `private static` because
  they read no instance state.
- **Idiom**: rf-dart-ascii-range-predicate-to-csharp-ascii-range-predicate
  (NEW).

### Construct 5 — `dart.scanner_state_machine.scantoken_giant_switch_on_first_char`

- **Dart**: giant `switch (c)` over the first character with multi-char
  lookahead via `_match(...)`, stacked quote cases, negative-number
  lookahead, pseudo-rollback in the `.` arm, throws on unexpected char.
- **C#/.NET**: `private Token ScanToken()` returning `Token`. Capture
  `long startLine = _line; long startColumn = _column;` then `var c =
  Advance();` (now `char`). Body is `switch (c)` whose case labels are
  `char` literals matching identically (`case '(': return MakeToken(...);`
  …). Every arm ends in `return` / `throw` (Dart-shape satisfies C#'s no-
  implicit-fallthrough requirement; case-stacking `case '"': case '\'':
  return String(c, startLine, startColumn);` — note single-quote escape).
  Compound-token recognition becomes nested `if (Match('.')) { if
  (Match('=')) { ... } }`. Pseudo-rollback `_current--; _column--;` maps
  1:1. Negative-number lookahead `if (IsDigit(Peek())) return
  Number(_current - 1, …); else return MakeToken(MINUS, …);`. Default arm
  `throw new CompileError($"Unexpected character: {c}", startLine,
  startColumn, phase: "lexer");`.
- **Idiom**: rf-dart-switch-on-char-to-csharp-switch-on-char (NEW).

### Construct 6 — `dart.identifier_scanner.append_via_substring_keyword_filter`

- **Dart**: alpha-numeric loop; `source.substring(start, _current)`; keyword
  filter for `mod` (only if next char isn't `(`) and `procedure`;
  variable-vs-atom by IsUpper(text[0]) or `_`+IsUpper(text[1]); reader
  lookahead on `?`.
- **C#/.NET**: `private Token Identifier(long start, long line, long column)`.
  Loop `while (IsAlphaNumeric(Peek())) Advance();`. Slice via
  `Source.Substring((int)start, (int)(_current - start))` — note the
  signature shift Dart (start, end) ⇒ .NET (startIndex, length); the long→int
  cast is required because `string.Substring` takes `int` and safe because
  cursors are `0 <= _ <= Source.Length` (which is `int`-typed in .NET).
  Keyword filter: `if (text == "mod" && Peek() != '(') return new
  Token(TokenType.MOD, text, line, column); if (text == "procedure") return
  new Token(TokenType.PROCEDURE, text, line, column);` (C# `string ==
  string` is value-equality per Microsoft Learn — matches Dart). Variable
  detection: `bool isVariable = IsUpper(text[0]) || (text[0] == '_' &&
  text.Length > 1 && IsUpper(text[1]));`. Reader-form: `if (Peek() == '?'
  && isVariable) { Advance(); return new Token(TokenType.READER, text,
  line, column); }`. Final dispatch: `var type = isVariable ?
  TokenType.VARIABLE : TokenType.ATOM; return new Token(type, text, line,
  column);`.
- **Idiom**: rf-dart-string-substring-to-csharp-string-substring (NEW;
  load-bearing signature shift end⇒length).

### Construct 7 — `dart.number_scanner.optional_negative_then_digits_dot_lookahead`

- **Dart**: optional leading `-` (cursor rewind); integer digit run; optional
  decimal-point with digit-lookahead; `double.parse` vs `int.parse` on
  `text.contains('.')`; NUMBER token with boxed numeric literal.
- **C#/.NET**: `private Token Number(long start, long line, long column)`.
  Negative-sign rewind: `if (Source[(int)start] == '-') { _current = start
  + 1; }`. Digit loop and decimal lookahead use `IsDigit(Peek())`,
  `Peek() == '.'`, `IsDigit(PeekNext())` — preserved verbatim. Slice via
  `Source.Substring((int)start, (int)(_current - start))`. Numeric parse:
  `text.Contains('.') ? (object)double.Parse(text,
  CultureInfo.InvariantCulture) : (object)long.Parse(text,
  CultureInfo.InvariantCulture)`. **MANDATORY**
  `CultureInfo.InvariantCulture` on every parse — `double.Parse` without
  it reads decimal separator from the ambient thread culture (`,` in
  de-DE/fr-FR/es-ES); Dart's `double.parse` always uses `.`; faithful
  parity requires the invariant culture. `int.parse` ⇒ `long.Parse`
  per rf-dart-int-to-csharp-long-width. The boxed payload (`object`)
  matches the `Token.Literal` `object?` slot from the token.dart spec.
- **Idiom**: rf-dart-number-parse-to-csharp-invariant-parse (NEW;
  load-bearing locale safety).

### Construct 8 — `dart.string_scanner.stringbuffer_with_escape_switch`

- **Dart**: `StringBuffer`; inner switch on escape; default fall-through
  appends raw character; LF tracking; closing-quote consumption; ATOM vs
  STRING dispatch on quote; throws on unterminated.
- **C#/.NET**: `private Token String(char quote, long line, long column)` —
  parameter shifts string→char. `var buffer = new
  System.Text.StringBuilder();`. Outer loop `while (!IsAtEnd() && Peek()
  != quote)` — branch on `Peek() == '\\'` with inner `switch (Peek())` and
  cases `case 'n': buffer.Append('\n'); break;` etc.; `default:
  buffer.Append(Peek()); break;` preserves Dart's lenient unknown-escape
  behaviour (we MUST NOT add `\0`/`\b`/`\f`/`\xNN`/`\uNNNN` — that would be
  a semantic extension per FR-013). Non-escape branch: `if (Peek() == '\n')
  { _line++; _column = 0; } buffer.Append(Advance());` — note `_column = 0`
  preserved verbatim (the next `Advance()` brings it to 1 on the new line).
  EOF check `throw new CompileError("Unterminated string", line, column,
  phase: "lexer");`. Closing-quote consumption `Advance();`. Final
  dispatch: `if (quote == '\'') return new Token(TokenType.ATOM,
  buffer.ToString(), line, column); return new Token(TokenType.STRING,
  buffer.ToString(), line, column, buffer.ToString());` — preserves the
  asymmetry that ATOM omits the literal payload (ctor optional `literal`
  defaults `null`) while STRING passes `buffer.ToString()` twice (text AND
  literal slots).
- **Idiom**: rf-dart-stringbuffer-to-csharp-stringbuilder (NEW).

### Construct 9 — `dart.whitespace_and_comment_skipper_two_styles`

- **Dart**: DFA over space/tab/CR/LF/`%`/`/*`; LF tracking in both
  whitespace-newline branch (`_column = 1`) and block-comment-LF branch
  (`_column = 0`); no nested-block-comment support; unterminated block
  comment silently falls out at EOF.
- **C#/.NET**: `private void SkipWhitespaceAndComments()`. Outer `while
  (!IsAtEnd())` loop with `var c = Peek();`. Whitespace branch `if (c ==
  ' ' || c == '\t' || c == '\r') Advance();`. Newline branch `else if (c
  == '\n') { Advance(); _line++; _column = 1; }`. Line-comment branch
  `else if (c == '%') { while (!IsAtEnd() && Peek() != '\n') Advance(); }`.
  Block-comment branch `else if (c == '/' && PeekNext() == '*') { Advance();
  Advance(); while (!IsAtEnd()) { if (Peek() == '*' && PeekNext() == '/')
  { Advance(); Advance(); break; } if (Peek() == '\n') { _line++; _column
  = 0; } Advance(); } }`. Trailing `else { break; }`. The column-reset
  asymmetry (1 vs 0) is preserved verbatim — both paths arrive at column=1
  for the first character of the new line.
- **Idiom**: rf-dart-string-indexing-to-csharp-char-indexing (reused for
  the underlying string-indexing pattern; no new idiom for the DFA shape
  itself — branch-for-branch faithful mapping).

### Construct 10 — `dart.error_position.compile_error_throw_with_named_phase`

- **Dart**: multiple `throw CompileError('msg', startLine, startColumn,
  phase: 'lexer');` sites in `_scanToken` and `_string`.
- **C#/.NET**: `throw new CompileError("msg", startLine, startColumn,
  phase: "lexer");` — C# supports named-argument call syntax identically.
  All error message strings preserved byte-for-byte to keep
  test/log assertions stable. Error position uses captured
  `startLine`/`startColumn` (start of the offending token), NOT the
  current cursor — preserved exactly.
- **Idiom**: rf-dart-named-default-param-to-csharp-optional-arg (reuse
  from error.dart spec).

### Construct 11 — `dart.tokenize_loop.list_accumulation_with_eof_sentinel`

- **Dart**: `List<Token> tokenize() { final tokens = <Token>[]; while
  (!_isAtEnd()) { _skipWhitespaceAndComments(); if (_isAtEnd()) break;
  ...tokens.add(_scanToken())...; } tokens.add(Token(TokenType.EOF, '',
  _line, _column)); return tokens; }`.
- **C#/.NET**: `public List<Token> Tokenize()` returning
  `System.Collections.Generic.List<Token>`. Body: `var tokens = new
  List<Token>(); while (!IsAtEnd()) { SkipWhitespaceAndComments(); if
  (IsAtEnd()) break; var tok = ScanToken(); tokens.Add(tok); }
  tokens.Add(new Token(TokenType.EOF, "", _line, _column)); return
  tokens;`. The empty-lexeme EOF sentinel uses `""` (matching Dart `''`).
  Two-phase loop required because whitespace can consume to EOF — preserved.
- **Idiom**: rf-dart-list-to-csharp-list-of-T (NEW).

### Construct 12 — `dart.doc_comment_triple_slash` (trivial)

- Each `///` Dart doc comment maps to `/// <summary>...</summary>` placed
  on the corresponding C# declaration.

### Construct 13 — `dart.line_comment_inline` (trivial)

- Each `//` Dart inline comment is preserved as a C# `//` line comment at
  the same source position.

## 3. Decomposed Task Units

- T1. Translate `class Lexer` (Construct 1): emit reference `class Lexer`
  with get-only `Source` property, mutable `_current`/`_line`/`_column`
  `long` fields, and ctor.
- T2. Translate hot-path primitives (Constructs 2, 4): `Advance()`,
  `Match(char)`, `Peek()`, `PeekNext()`, `IsAtEnd()`, `IsDigit(char)`,
  `IsAlpha(char)`, `IsAlphaNumeric(char)`, `IsUpper(char)`, `MakeToken`
  — including the `'\0'` sentinel and ASCII-range predicate preservation.
- T3. Translate `ScanToken()` (Construct 5): char-keyed `switch` with all
  punctuation/compound-token cases, stacked quote cases, negative-number
  lookahead, `_current`/`_column` pseudo-rollback in `.` arm, throw on
  unexpected.
- T4. Translate `Identifier(long start, long line, long column)` (Construct
  6): alpha-numeric loop, `Substring` with length-not-end, `mod`/`procedure`
  keyword filter, variable/atom classification, reader-form `?` lookahead.
- T5. Translate `Number(long start, long line, long column)` (Construct 7):
  negative-sign rewind, integer/decimal scan, `double.Parse` / `long.Parse`
  with `CultureInfo.InvariantCulture`, NUMBER token with boxed literal.
- T6. Translate `String(char quote, long line, long column)` (Construct 8):
  `StringBuilder` accumulation, escape switch with lenient default,
  embedded-LF tracking with `_column = 0`, ATOM-vs-STRING dispatch with
  literal-slot asymmetry, unterminated-string throw.
- T7. Translate `SkipWhitespaceAndComments()` (Construct 9): hand-rolled
  DFA over space/tab/CR/LF/`%`/`/*` with both column-reset shapes
  preserved.
- T8. Translate `Tokenize()` (Construct 11): two-phase loop with EOF
  sentinel; return `List<Token>`.
- T9. Map all `throw CompileError(...)` sites (Construct 10) with named
  `phase: "lexer"` arg preserved verbatim, error messages byte-for-byte
  identical.
- T10. Convert `///` doc comments (Construct 12) to XML `<summary>` doc
  comments on the corresponding C# declarations.
- T11. Preserve `//` inline comments (Construct 13) byte-for-byte at the
  same source positions.

## 4. Research Findings

None required — all 10 non-trivial constructs reuse or directly mirror
authoritative findings already recorded in the ratified convspec:

- `rf-dart-final-field-class-to-csharp-getonly-class` (reuse, token.dart
  family).
- `rf-dart-string-indexing-to-csharp-char-indexing` (NEW in convspec, with
  Dart api.dart.dev `String.operator[]` / `codeUnitAt` / `Runes` and
  Microsoft Learn `String.Chars` / `System.Text.Rune` citations).
- `rf-dart-ascii-range-predicate-to-csharp-ascii-range-predicate` (NEW in
  convspec, with Microsoft Learn `Char.IsDigit` / `Char.IsLetter` /
  `System.Char` and Dart api.dart.dev `codeUnitAt` citations).
- `rf-dart-switch-on-char-to-csharp-switch-on-char` (NEW in convspec, with
  Microsoft Learn switch-statement / Char.op_Equality and Dart dart.dev
  branches citations).
- `rf-dart-string-substring-to-csharp-string-substring` (NEW in convspec,
  with Dart api.dart.dev `String.substring` and Microsoft Learn
  `string.Substring(Int32, Int32)` citations; documents half-open vs
  length signature shift).
- `rf-dart-number-parse-to-csharp-invariant-parse` (NEW in convspec, with
  Dart api.dart.dev `double.parse` / `int.parse` and Microsoft Learn
  `Double.Parse` / `Int64.Parse` / `string.Contains(char)` citations;
  documents locale-invariance requirement).
- `rf-dart-stringbuffer-to-csharp-stringbuilder` (NEW in convspec, with
  Dart api.dart.dev `StringBuffer` and Microsoft Learn `StringBuilder` /
  `StringBuilder.Append` citations).
- `rf-dart-list-to-csharp-list-of-T` (NEW in convspec, with Dart
  api.dart.dev `List` and Microsoft Learn `List<T>` citations).
- `rf-dart-named-default-param-to-csharp-optional-arg` (reuse from
  error.dart spec; FR-024 cache reuse).
- `rf-dart-int-to-csharp-long-width` (recurring from token.dart family;
  applied to cursors and to int.parse → long.Parse).
- `rf-dart-leading-underscore-privacy-to-csharp-private` (recurring from
  error.dart family; applied to all `_`-prefixed fields/methods).

## 5. Consistency Pass

Cross-checked the plan against the source file, the ratified convspec, the
tombstone metadata, and CLAUDE.md / sibling specs:

- **Convspec ↔ source**: every Dart construct enumerated in the convspec's
  `constructs` block (13 entries) appears in the source and is covered by
  this plan (Constructs 1–13 ↔ Tasks T1–T11; T2 fuses Constructs 2+4 hot-
  path primitives; T10/T11 cover trivial doc/inline comments).
- **Convspec ↔ plan**: every C# target form in §2 matches the
  `target_decision` field of the corresponding convspec construct
  verbatim — fixed — derived from
  `.codeconv/conversion-specs/lib/compiler/lexer.dart.md`.
- **Idiom reuse**: rf-dart-named-default-param-to-csharp-optional-arg is
  reused as-is from the error.dart spec; rf-dart-int-to-csharp-long-width
  and rf-dart-leading-underscore-privacy-to-csharp-private are recurring
  idioms from the token.dart / error.dart family — fixed — derived from
  the convspec's "Rationale and research provenance" section and the
  recurring-idiom annotations in convspec construct `nuance` fields.
- **Cycle group**: this task assigns `cycle_group_id: 9` with empty
  `scc_siblings`; the tombstone records `cycle_group_id: 13` but the
  workflow's frontmatter takes precedence (matches the convspec's silence
  on cycle metadata — both fields written verbatim from the planning task
  parameters).
- **Dependencies**: `lib/compiler/error.dart` (CompileError throw) and
  `lib/compiler/token.dart` (Token, TokenType) — both listed in the
  tombstone `dependencies` block; both already have ratified convspecs
  (CompileError reused via rf-dart-named-default-param-to-csharp-optional-
  arg; Token/TokenType referenced for ctor signature and literal slot
  shape; no new dependency surfaced by this plan).
- **CLAUDE.md compliance**: no Dart `*Error` type rename (CompileError
  preserved); no Char.IsDigit/Char.IsLetter substitution; no semantic
  extension (escape table preserved verbatim per FR-013); no `await` /
  async injected (file is fully synchronous in both languages); no
  `late`-style deferred-init; no `record`/`struct` collapse of the
  reference `class Lexer`.
- **Numeric parse safety**: `CultureInfo.InvariantCulture` explicitly
  required on every `double.Parse` / `long.Parse` call (Construct 7
  / T5) — fixed — derived from convspec rf-dart-number-parse-to-csharp-
  invariant-parse.
- **Substring signature**: half-open `(start, end)` ⇒ length `(startIndex,
  end - start)` recorded at every slice site (Constructs 6, 7) — fixed —
  derived from convspec rf-dart-string-substring-to-csharp-string-
  substring.
- **String-vs-char shift**: all method signatures updated consistently
  (Constructs 2, 3, 4, 5, 8) and the `'\x00'` → `'\0'` sentinel mapping
  applied uniformly — fixed — derived from convspec rf-dart-string-
  indexing-to-csharp-char-indexing.

No gaps; no escalations raised. Plan is line-for-line derivable from the
ratified convspec + source.

## 6. Escalations

None.
