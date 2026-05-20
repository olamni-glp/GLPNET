# Conversion Spec — lib/compiler/lexer.dart

> Conversion-spec artifact for lib/compiler/lexer.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/compiler/lexer.dart
source_sha256: f9c89267ee74e7e9151a0e97e57b00fd9db39e01d949bad680f3a6e51d4abe75
target_code_unit: lib/compiler/lexer.cs
constructs:
  - construct_key: dart.class.mutable_lexer_with_final_source_and_int_cursors
    source_form: >-
      "class Lexer { final String source; int _current = 0; int _line = 1;
      int _column = 1; Lexer(this.source); ... }"
    target_decision: >-
      Emit a C# reference `class Lexer` (NOT a `record`, NOT a `struct`) with
      one get-only auto-property `Source` initialised from a single ctor
      `Lexer(string source)` and three private mutable instance fields
      `_current` / `_line` / `_column` of type `long` initialised to 0 / 1 / 1
      respectively. Reference semantics preserved: the lexer is constructed
      once and its `Tokenize()` method mutates the cursor as it scans —
      defensive copies (struct semantics) would silently break the scan loop
      because each `_advance()` increments `_current` and the caller observes
      the post-increment state. A `record` is REJECTED because records imply
      synthesised value-equality on declared members and a lexer is a
      stateful scanner whose identity matters (not its instantaneous cursor
      values). The Dart positional-initialising-formal `this.source` expands
      to an explicit constructor assignment `Source = source;` in C# (C# has
      no `this.x` ctor-parameter sugar). The three private cursors stay as
      mutable `long` fields (NOT properties) because they are not part of
      the public surface and are mutated on the hot path (~once per source
      character) — fields are direct memory loads, properties go through an
      accessor (negligible JIT difference in practice but the field shape is
      faithful to Dart and reviewer-clearer).
    idiom_id: null
    research_finding_id: rf-dart-final-field-class-to-csharp-getonly-class
    nuance: >-
      Mutability nuance (load-bearing): the source mixes ONE final field
      (`source` — write-once via ctor) with THREE mutable fields (`_current`,
      `_line`, `_column`) — only `Source` becomes a get-only auto-property;
      the three cursors become mutable private fields. Reference-vs-value
      (load-bearing): the lexer MUST remain a reference class so the
      `Tokenize()` scan loop and the `_scanToken()` switch observe the same
      mutating cursors; a struct would either box (defeating the point) or
      defensive-copy at every method-call boundary. Integer-width: cursors
      are Dart `int` ⇒ C# `long` per rf-dart-int-to-csharp-long-width
      (recurring idiom from token.dart). Privacy: Dart `_`-prefix
      library-private becomes C# `private` (class-scoped) per
      rf-dart-leading-underscore-privacy-to-csharp-private (recurring idiom
      from error.dart) — strictly tighter, correct here because the cursors
      are only touched by methods of this class.
  - construct_key: dart.string_indexing.code_unit_as_one_char_string
    source_form: >-
      "String _advance() { _column++; return source[_current++]; }" plus
      "String _peek() => _isAtEnd() ? '\\x00' : source[_current];" and
      "String _peekNext() => _current + 1 >= source.length ? '\\x00' :
      source[_current + 1];" — `String[int]` returns a one-character String
      (a single UTF-16 code unit wrapped as a String).
    target_decision: >-
      DO NOT mirror Dart `String[int]→String` literally with C# substring
      slicing. Map Dart `String` (= C# `string`) and Dart `String[i]`
      (one-character String) ⇒ C# `string[i]` which returns a `char` (UTF-16
      code unit). Change the return type of `_advance`/`_peek`/`_peekNext`
      from `string` to `char`. Replace the sentinel-empty value Dart uses
      ('\\x00' — a one-character String) with C# `char` literal `'\0'` (also
      U+0000) — both languages encode it as a single UTF-16 code unit, so
      `_isDigit`/`_isAlpha` callers comparing against `'\\x00'` ⇒ `'\0'`
      preserve the same fast-path test. The String→char shift cascades into
      every comparison site (`c == '('` becomes `c == '('` in C# but the
      RHS is now a `char` literal not a `String` literal — IDENTICAL textual
      form, IDENTICAL semantics, no extra allocations). The few sites that
      currently use a one-character Dart String to compare against a
      multi-character Dart String literal (none exist in this file —
      `_match` already takes a one-character expected) are unaffected.
    idiom_id: null
    research_finding_id: rf-dart-string-indexing-to-csharp-char-indexing
    nuance: >-
      Unicode / code-unit-vs-rune nuance (explicitly addressed —
      load-bearing): Dart `String` is a sequence of UTF-16 code units; Dart
      `String[i]` returns a NEW single-code-unit `String` of length 1 (NOT a
      `Rune`/`int`; Dart documents `String.runes` and `String.codeUnits` as
      separate getters for that purpose). C# `string` is also a sequence of
      UTF-16 code units (System.String): `string[i]` returns a `char`
      (System.Char, 16-bit unsigned) at index `i` — also a code unit, NOT a
      `Rune` (C# `System.Text.Rune` is the equivalent of Dart's `Rune` for
      code-point iteration). The two languages therefore agree EXACTLY at
      the code-unit level — the only mechanical difference is the wrapper
      type (one-char `String` vs `char`). FOR THIS LEXER specifically, every
      lexeme it recognises (`(`, `)`, `[`, `+`, `*`, `:`, `=`, `.`, …, ASCII
      letters/digits/underscore, plus `'`/`"` for string literals) lives in
      the basic Latin block (U+0000..U+007F), so every recognition path is
      naturally one-code-unit-per-character with NO surrogate-pair concern.
      However: arbitrary text inside `'…'` / `"…"` strings or after `%`
      line-comments MAY contain supplementary-plane characters (emoji,
      historic scripts) encoded as a UTF-16 surrogate pair (high surrogate
      D800..DBFF + low surrogate DC00..DFFF). The Dart source copies these
      verbatim via `buffer.write(_advance())` — which writes the
      one-code-unit `String` to the `StringBuffer`, naturally appending the
      surrogate code units one at a time and re-pairing them in the output;
      C# `StringBuilder.Append(char)` likewise appends a single code unit
      and re-pairs them on `ToString()`. Net: surrogate pairs are PRESERVED
      bit-for-bit in both languages because both walk code-units and both
      string builders are code-unit append-only. We do NOT switch to `Rune`
      iteration on the C# side because (a) Dart does not iterate runes here
      either and (b) `Rune` iteration would change the cursor advancement
      from 1-per-code-unit to 1-per-code-point, diverging from the Dart
      semantics. Column counting nuance (recorded, not modified): `_advance`
      increments `_column` once per code unit — surrogate pairs therefore
      count as 2 columns in both languages; this is a known characteristic
      of code-unit-based scanners and is preserved unchanged (a future
      change to "Unicode column = code-point column" would be a separate
      project-policy decision and would need to apply to both Dart and C#).
  - construct_key: dart.match_advance_helper_one_char_String_expected
    source_form: >-
      "bool _match(String expected) { if (_isAtEnd()) return false; if
      (source[_current] != expected) return false; _advance(); return true; }"
    target_decision: >-
      Convert to `private bool Match(char expected)` (signature parameter
      changes from `string` to `char` to mirror the `string[i]→char` shift).
      Body becomes `if (IsAtEnd()) return false; if (_source[_current] !=
      expected) return false; Advance(); return true;`. Call sites currently
      pass one-character Dart String literals (`_match('.')`, `_match('=')`,
      …) which translate to one-character C# char literals — Dart `'.'` (a
      one-code-unit String) ⇒ C# `'.'` (a char), textually IDENTICAL in
      source code. NO performance hazard: `string == string` in Dart is
      value-equality; `char == char` in C# is value-equality on a 16-bit
      integral — both are O(1). Behaviour preserved: post-advance the
      caller's `_current - N` substring offsets continue to point at the
      same code units (because `Advance()` increments `_current` by exactly
      1, the same as Dart).
    idiom_id: null
    research_finding_id: rf-dart-string-indexing-to-csharp-char-indexing
    nuance: >-
      One-character-String-parameter nuance (explicitly addressed): Dart has
      no `char` type — the canonical way to express "the character X" is a
      one-code-unit `String`, and the value-equality comparison
      `source[_current] != expected` works because `String.==` is structural
      on both sides. C# DOES have a `char` type and `string[i]` returns it
      natively; mirroring the Dart shape with `string` parameters would
      force per-call `string` allocation for what is in C# a `char` value —
      a regression with no upside. The signature change `string→char` is
      the correct .NET-idiomatic faithful mapping of the Dart intent, not
      a divergence.
  - construct_key: dart.utf16_codeunit_predicates_via_codeUnitAt
    source_form: >-
      "bool _isDigit(String c) { if (c == '\\x00') return false; final code
      = c.codeUnitAt(0); return code >= '0'.codeUnitAt(0) && code <=
      '9'.codeUnitAt(0); }" — and the analogous `_isAlpha`, `_isAlphaNumeric`,
      `_isUpper` helpers using `c.codeUnitAt(0)` plus literal-range
      comparisons.
    target_decision: >-
      DO NOT use `char.IsDigit(c)` / `char.IsLetter(c)` / `char.IsLetterOrDigit(c)`
      from the BCL — those .NET helpers accept the full Unicode definition
      of "digit"/"letter" (per Microsoft Learn: `Char.IsDigit` "Indicates
      whether the specified Unicode character is categorized as a decimal
      digit" — i.e. ANY decimal-digit code point, e.g. Arabic-Indic digits
      U+0660..U+0669, fullwidth digits U+FF10..U+FF19, etc.), which would
      ADMIT identifiers and number literals that the Dart lexer REJECTS.
      The Dart helpers use a hard ASCII-range comparison (`code >=
      '0'.codeUnitAt(0) && code <= '9'.codeUnitAt(0)`, i.e. U+0030..U+0039
      ONLY). Preserve that exactly in C#: emit `private static bool
      IsDigit(char c) { if (c == '\0') return false; int code = (int)c;
      return code >= (int)'0' && code <= (int)'9'; }` and analogues for
      `IsAlpha` (ASCII letter or '_'), `IsAlphaNumeric` (delegates),
      `IsUpper` (ASCII A..Z). `c.codeUnitAt(0)` ⇒ `(int)c` (or implicit
      char→int conversion in the comparison — Microsoft Learn: "Char
      implicitly converts to ushort/int" and "comparison operators on Char
      compare the underlying numeric value"). The `'\x00'` sentinel guard
      is preserved as `c == '\0'` (an "absent character" sentinel returned
      by `Peek`/`PeekNext` past end-of-source). Methods marked `private
      static` because they do not read instance state.
    idiom_id: null
    research_finding_id: rf-dart-ascii-range-predicate-to-csharp-ascii-range-predicate
    nuance: >-
      Unicode-vs-ASCII predicate nuance (explicitly addressed —
      load-bearing): `char.IsDigit` / `char.IsLetter` in C# follow
      Unicode-category semantics (Microsoft Learn). The Dart source
      INTENTIONALLY constrains the lexer to ASCII letters/digits via the
      `>= '0' && <= '9'` / `>= 'a' && <= 'z'` / `>= 'A' && <= 'Z'` range
      comparisons (the only acceptance of '_' is special-cased in
      `_isAlpha`). If we silently swapped in `char.IsDigit`, the lexer
      would start accepting Arabic-Indic digits in number literals, Greek
      letters in identifiers, etc. — a behaviour divergence the Dart code
      DOES NOT have. Faithful mapping is therefore the literal ASCII-range
      predicate, NOT the BCL Unicode predicate. Codeunit-extraction nuance:
      Dart `String.codeUnitAt(int)` returns a UTF-16 code unit as `int`
      (api.dart.dev: "Returns the 16-bit UTF-16 code unit at the given
      index"); C# has no `codeUnitAt` because `string[i]` already returns
      that as `char` — the (int)cast or implicit promotion to ushort/int
      is the direct equivalent. Sentinel guard nuance: Dart `'\x00'`
      (one-code-unit String containing U+0000) ⇒ C# `'\0'` (char U+0000)
      — same code unit, different wrapper.
  - construct_key: dart.scanner_state_machine.scantoken_giant_switch_on_first_char
    source_form: >-
      "Token _scanToken() { final startLine = _line; final startColumn =
      _column; final c = _advance(); switch (c) { case '(': return
      _makeToken(...); ... case '.': if (_match('.')) { if (_match('=')) {
      ...UNIV_DECOMPOSE...} _current--; _column--; return _makeToken(DOT,
      ...);} return _makeToken(DOT, ...); ... case ':': if (_match(':')) {
      if (_match('=')) ...COLONCOLONEQ... throw CompileError('Expected
      \"=\" after \"::\"', ...);} ... case '_': if (!_isAlphaNumeric(_peek()))
      return _makeToken(UNDERSCORE, ...); return _identifier(_current - 1,
      ...); ... default: if (_isDigit(c)) return _number(_current - 1, ...);
      if (_isAlpha(c)) return _identifier(_current - 1, ...); throw
      CompileError('Unexpected character: \$c', ...); } }"
    target_decision: >-
      Emit `private Token ScanToken()` returning a `Token`. Capture
      `startLine` / `startColumn` (as `long`) at entry, then `var c =
      Advance();` (now `char`). The body is a C# `switch (c)` whose cases
      are `char` literals (NOT string literals — Microsoft Learn `switch`
      statement on a `char` expression matches against `char` constants).
      The Dart-style fall-through-via-explicit-return shape is preserved
      verbatim — every Dart `case` ends in a `return`, so C# `case 'x':
      return …;` is a 1:1 mapping with NO implicit fallthrough hazard
      (C# requires explicit `break`/`return`/`throw`/`goto case` per arm,
      which the Dart shape already provides). The `default:` arm is
      preserved with the `IsDigit(c)` / `IsAlpha(c)` dispatch followed by
      the `throw new CompileError(...)`. Pseudo-rollback in the `.` arm
      (Dart `_current--; _column--;` then `return _makeToken(DOT, ...);`)
      maps DIRECTLY to `_current--; _column--; return MakeToken(DOT, ...);`
      — `long` decrement is well-defined and matches Dart's `int` semantics
      (no overflow path; cursors are >= 0 by invariant). String literal
      contexts (the inner `case '"': case "'":` fall-through case) and
      negative-number lookahead (`case '-': if (IsDigit(Peek())) return
      Number(_current - 1, …); else return MakeToken(MINUS, …);`) are
      preserved branch-for-branch. The compound-token recognition (e.g.
      `..=` ⇒ UNIV_DECOMPOSE via `_match('.')` + `_match('=')`) becomes a
      nested `if (Match('.')) { if (Match('=')) { … } }` — line-for-line
      shape.
    idiom_id: null
    research_finding_id: rf-dart-switch-on-char-to-csharp-switch-on-char
    nuance: >-
      Switch-statement nuance (explicitly addressed): Dart `switch` allows
      arbitrary type-pattern cases in Dart 3, but here every case is a
      one-code-unit String constant — equivalent to a constant-pattern
      switch. C# `switch` on `char` is the natural counterpart (Microsoft
      Learn `switch` statement); the JIT may compile a dense
      constant-pattern switch on `char` into a jump table. Implicit
      fall-through nuance: C# DISALLOWS implicit fall-through between
      non-empty case labels (a compile-time error) — Dart does too in
      practice (but tolerates it more), and the source uses explicit
      `return` for every arm, so the C# requirement is already satisfied.
      Case-stacking nuance: the Dart source stacks `case '"':` and `case
      "'":` (two cases falling into the same body) — C# supports identical
      syntax for empty case labels followed by a single body (`case '"':
      case '\'': return String(c, startLine, startColumn);`). Note the C#
      char literal for single-quote requires escape `'\''`. Throw vs return
      nuance: every error path is `throw new CompileError(...)` — Dart
      `throw` ⇒ C# `throw new`, preserved verbatim. The two-character
      lookahead in `.` (back up by decrementing) is recorded as a faithful
      mutation; both languages permit post/pre-increment/decrement on
      mutable integer fields.
  - construct_key: dart.identifier_scanner.append_via_substring_keyword_filter
    source_form: >-
      "Token _identifier(int start, int line, int column) { while
      (_isAlphaNumeric(_peek())) { _advance(); } final text =
      source.substring(start, _current); if (text == 'mod' && _peek() !=
      '(') return Token(TokenType.MOD, text, line, column); if (text ==
      'procedure') return Token(TokenType.PROCEDURE, text, line, column);
      final isVariable = _isUpper(text[0]) || (text[0] == '_' && text.length
      > 1 && _isUpper(text[1])); if (_peek() == '?' && isVariable) {
      _advance(); return Token(TokenType.READER, text, line, column); }
      final type = isVariable ? TokenType.VARIABLE : TokenType.ATOM; return
      Token(type, text, line, column); }"
    target_decision: >-
      Emit `private Token Identifier(long start, long line, long column)`.
      Loop body: `while (IsAlphaNumeric(Peek())) { Advance(); }` (Peek
      returns `char` now). Slice extraction Dart `source.substring(start,
      _current)` ⇒ C# `_source.Substring((int)start, (int)(_current -
      start))`. Note the .NET signature is `Substring(int startIndex, int
      length)` — Dart's `String.substring(int start, [int? end])` uses
      half-open [start, end). The two narrowing casts long→int are required
      because `string.Substring`'s parameters are `int`; the cursors are
      always >= 0 and bounded by `_source.Length` (which is itself `int` —
      a Dart→C# String-length nuance, see below). Keyword filter for "mod"
      / "procedure": preserved literally as `if (text == "mod" && Peek() !=
      '(') return new Token(TokenType.MOD, text, line, column); if (text ==
      "procedure") return new Token(TokenType.PROCEDURE, text, line,
      column);` — note `text == "mod"` in C# is `string.Equals` value
      equality (Microsoft Learn `string`: "Two strings are considered equal
      if they have the same length and contain the same characters in the
      same order"), MATCHING Dart's `==` on String. `text[0]` / `text[1]` ⇒
      `text[0]` / `text[1]` returning `char` (was: a one-code-unit `String`
      in Dart). The IsUpper helper signature shifts string→char alongside
      the codeunit-predicate refactor. Reader-form check `_peek() == '?' &&
      isVariable` preserved verbatim. The variable-vs-atom enum branch maps
      `TokenType.VARIABLE` / `TokenType.ATOM` per token.dart's enum
      mapping. The literal strings "mod" / "procedure" stay as C# string
      literals (not chars) because they are multi-character keywords.
    idiom_id: null
    research_finding_id: rf-dart-string-substring-to-csharp-string-substring
    nuance: >-
      Substring signature nuance (explicitly addressed): Dart
      `String.substring(int start, [int? end])` is HALF-OPEN [start, end) —
      api.dart.dev: "Returns the substring of this string that extends from
      startIndex, inclusive, to endIndex, exclusive". C# `string.Substring(int
      startIndex, int length)` takes LENGTH not END — Microsoft Learn:
      "Retrieves a substring from this instance. The substring starts at a
      specified character position and has a specified length". The faithful
      rewrite is `_source.Substring((int)start, (int)(_current - start))`
      (start position + length = _current - start). NOT just blind
      mechanical renaming — getting this wrong silently produces strings of
      the wrong length. Width nuance: cursors are `long` in our mapping but
      `Substring` takes `int`; the cast is required and safe because the
      cursor is always 0 <= _ <= _source.Length and `string.Length` returns
      `int` in C# (Microsoft Learn `String.Length` "Gets the number of
      characters in the current String"). String length nuance:
      `_source.Length` returns the COUNT OF UTF-16 CODE UNITS (Microsoft
      Learn: "The Length property returns the number of Char objects in
      this instance, not the number of Unicode characters") — IDENTICAL to
      Dart `String.length` (api.dart.dev: "The number of UTF-16 code units
      in this string"). So `IsAtEnd()` returning `_current >= _source.Length`
      compares like-for-like. Char-as-index-into-string nuance: `text[0]`
      in C# returns `char` (was Dart one-code-unit-String); IsUpper consumes
      `char`, perfect. Keyword-equality nuance: C# `string == string` is
      value-equality (Microsoft Learn). String interning concerns do not
      apply (we never rely on reference identity).
  - construct_key: dart.number_scanner.optional_negative_then_digits_dot_lookahead
    source_form: >-
      "Token _number(int start, int line, int column) { if (source[start]
      == '-') { _current = start + 1; } while (_isDigit(_peek())) {
      _advance(); } if (_peek() == '.' && _isDigit(_peekNext())) { _advance();
      while (_isDigit(_peek())) { _advance(); } } final text =
      source.substring(start, _current); final value = text.contains('.') ?
      double.parse(text) : int.parse(text); return Token(TokenType.NUMBER,
      text, line, column, value); }"
    target_decision: >-
      Emit `private Token Number(long start, long line, long column)`. The
      negative-sign rewind: `if (_source[(int)start] == '-') { _current =
      start + 1; }` — `string[int]` returns `char`; comparison `char ==
      char` literal `'-'` is value-equality. Integer scan loop and
      decimal-lookahead branch preserved verbatim (using `IsDigit(Peek())`,
      `Peek() == '.'`, `IsDigit(PeekNext())`). Slice via `_source.Substring((int)
      start, (int)(_current - start))`. Numeric parse: Dart `text.contains('.')
      ? double.parse(text) : int.parse(text)` ⇒ C# `text.Contains('.') ?
      (object)double.Parse(text, CultureInfo.InvariantCulture) : (object)
      long.Parse(text, CultureInfo.InvariantCulture)`. CultureInfo.InvariantCulture
      is REQUIRED to keep '.' as the decimal separator across locales —
      Microsoft Learn `Double.Parse`: "If `provider` is `null`, the
      `NumberFormatInfo` for the current culture is used." Dart `double.parse`
      always uses '.', never the user locale (api.dart.dev: "The String
      representation of the number can be in any format that the language
      accepts as a numeric literal" — locale-independent); preserving that
      across locales mandates passing the invariant culture in C#. `int.parse`
      ⇒ `long.Parse` (NOT `int.Parse` / `System.Int32.Parse`) because Dart
      `int` is 64-bit-on-native (rf-dart-int-to-csharp-long-width). The boxed
      payload then matches `Token.Literal`'s `object?` declared shape (see
      token.dart spec's `dart.nullable_object_field.Object_question_literal_payload`).
    idiom_id: null
    research_finding_id: rf-dart-number-parse-to-csharp-invariant-parse
    nuance: >-
      Locale-sensitivity nuance (explicitly addressed — load-bearing):
      `double.Parse` in C# WITHOUT an explicit `CultureInfo` reads the
      decimal point from the AMBIENT THREAD CULTURE (e.g. `,` in de-DE,
      fr-FR, es-ES, …) — Microsoft Learn explicitly warns. Dart `double.parse`
      DOES NOT do this; it accepts only the `.`-decimal form. Faithful
      mapping therefore requires `CultureInfo.InvariantCulture` on every
      numeric parse in this file. Silent omission of the invariant culture
      would cause the C# lexer to fail on `3.14` when running under, e.g.,
      a German-locale build server while the Dart lexer succeeds — a
      classic Dart→C# parity bug. Width nuance: Dart `int.parse("1234")`
      returns 64-bit native ⇒ C# `long.Parse(...)`. Boxing nuance: the
      ternary result is the same boxed `object?` payload the Token literal
      slot expects (token.dart spec construct
      `dart.nullable_object_field.Object_question_literal_payload`).
      Contains-char nuance: Dart `text.contains('.')` accepts a `Pattern`
      (api.dart.dev) which a one-character String satisfies; C# `string.Contains(char)`
      is an overload (Microsoft Learn — added in .NET Core 2.1) returning
      `bool` — the direct equivalent. ToString round-trip nuance:
      `double.ToString` in C# is also locale-sensitive; consumers of the
      boxed literal that round-trip it back to string (none here) MUST
      similarly use `CultureInfo.InvariantCulture`. Negative-sign branch
      nuance: the rewind sets `_current = start + 1` skipping the `-` —
      preserved as-is, both languages.
  - construct_key: dart.string_scanner.stringbuffer_with_escape_switch
    source_form: >-
      "Token _string(String quote, int line, int column) { final buffer =
      StringBuffer(); while (!_isAtEnd() && _peek() != quote) { if (_peek()
      == '\\\\') { _advance(); if (_isAtEnd()) break; switch (_peek()) {
      case 'n': buffer.write('\\n'); break; case 't': buffer.write('\\t');
      break; case 'r': buffer.write('\\r'); break; case '\\\\':
      buffer.write('\\\\'); break; case '\"': buffer.write('\"'); break;
      case \"'\": buffer.write(\"'\"); break; default: buffer.write(_peek());
      } _advance(); } else { if (_peek() == '\\n') { _line++; _column = 0;
      } buffer.write(_advance()); } } if (_isAtEnd()) throw CompileError(
      'Unterminated string', line, column, phase: 'lexer'); _advance(); //
      closing quote if (quote == \"'\") return Token(TokenType.ATOM,
      buffer.toString(), line, column); else return Token(TokenType.STRING,
      buffer.toString(), line, column, buffer.toString()); }"
    target_decision: >-
      Emit `private Token String(char quote, long line, long column)` —
      parameter type shifts string→char. Buffer: Dart `StringBuffer()` ⇒
      C# `var buffer = new System.Text.StringBuilder();`. Append: Dart
      `buffer.write(c)` (which for a one-code-unit String invokes
      `StringBuffer.write(Object?)` which calls `Object.toString()`) ⇒ C#
      `buffer.Append(c)` (the `StringBuilder.Append(char)` overload,
      O(1)-amortised). The escape-switch becomes a C# `switch (Peek())`
      with `case 'n': buffer.Append('\n'); break;` etc. — branch-for-branch
      preservation. The `default: buffer.Append(Peek()); break;` arm is
      preserved (unknown escape ⇒ append the literal character — same
      forgiving semantic). The newline-tracking branch outside the
      escape-handler increments `_line` and resets `_column = 0` (Dart
      behaviour preserved; note `_column = 0` not 1 because the immediately
      following `_advance()` brings it to 1 on the new line — a faithful
      detail to preserve). Closing-quote consumption `_advance(); //
      closing quote` ⇒ `Advance(); // closing quote`. Final dispatch on
      `quote == "'" ? ATOM : STRING` ⇒ C# `if (quote == '\'') return new
      Token(TokenType.ATOM, buffer.ToString(), line, column); return new
      Token(TokenType.STRING, buffer.ToString(), line, column,
      buffer.ToString());` — preserves the Dart asymmetry that ATOM does
      NOT pass a literal payload (Token ctor optional `literal` defaults
      to `null`) while STRING passes the same string twice (text AND
      literal). The unterminated-string error becomes `throw new
      CompileError("Unterminated string", line, column, phase: "lexer");`
      — matches the error.dart spec's named-parameter / optional-arg
      handling.
    idiom_id: null
    research_finding_id: rf-dart-stringbuffer-to-csharp-stringbuilder
    nuance: >-
      StringBuilder vs StringBuffer nuance (explicitly addressed): Dart
      `StringBuffer` (api.dart.dev) is a mutable accumulator backed by a
      growable buffer; calling `toString()` materialises the accumulated
      string. C# `System.Text.StringBuilder` (Microsoft Learn) is the
      canonical counterpart: "Represents a mutable string of characters …
      StringBuilder will not necessarily allocate a new String object
      until you invoke the ToString method." API mapping: Dart
      `buffer.write(x)` ⇒ C# `buffer.Append(x)`; Dart `buffer.writeAll`
      (not used here) ⇒ C# `buffer.AppendJoin`; Dart `buffer.toString()`
      ⇒ C# `buffer.ToString()`. Code-unit append nuance (referencing
      rf-dart-string-indexing-to-csharp-char-indexing): each
      `buffer.Append(Peek())` appends a SINGLE UTF-16 code unit; if the
      source string contains a surrogate pair, BOTH halves are appended
      individually in sequence (Peek sees the high, Advance consumes it
      then a second iteration sees the low), preserving the pair on
      `ToString()`. No surrogate-splitting hazard exists because the
      Dart loop also walks one code unit per iteration. Newline counting
      nuance: only `\n` (U+000A) is treated as a line break; `\r` is
      consumed by whitespace skipping (see next construct); CRLF on
      Windows produces one line-break per newline as in Dart. Escape-table
      nuance: the source supports `\n \t \r \\ \" \'` and falls through
      to "append the raw character" for unrecognised escapes; this is
      Dart-specific lenient behaviour and is preserved verbatim — we DO
      NOT add additional escapes (e.g. `\0`, `\b`, `\f`, `\xNN`, `\uNNNN`)
      because the Dart source does not, and that would be a semantic
      extension (FR-013). ATOM-vs-STRING dispatch nuance: single-quoted
      strings produce ATOM tokens (functor-callable), double-quoted
      produce STRING tokens — Token literal slot is filled only for
      STRING (token.dart spec records `object?` literal field). Buffer-
      identity nuance: in Dart, `buffer.toString()` called twice returns
      two equal-but-possibly-distinct String instances; same in C# (`ToString()`
      returns a fresh `string` per call). The double-call (text and literal)
      yields two `string` references that compare value-equal — no
      observable difference.
  - construct_key: dart.whitespace_and_comment_skipper_two_styles
    source_form: >-
      "void _skipWhitespaceAndComments() { while (!_isAtEnd()) { final c =
      _peek(); if (c == ' ' || c == '\\t' || c == '\\r') { _advance(); }
      else if (c == '\\n') { _advance(); _line++; _column = 1; } else if
      (c == '%') { while (!_isAtEnd() && _peek() != '\\n') { _advance(); } }
      else if (c == '/' && _peekNext() == '*') { _advance(); _advance();
      while (!_isAtEnd()) { if (_peek() == '*' && _peekNext() == '/') {
      _advance(); _advance(); break; } if (_peek() == '\\n') { _line++;
      _column = 0; } _advance(); } } else { break; } } }"
    target_decision: >-
      Emit `private void SkipWhitespaceAndComments()` mirroring the Dart
      shape. Whitespace classification uses literal `char` comparisons:
      `c == ' ' || c == '\t' || c == '\r'` ⇒ identical in C#. Newline
      branch `c == '\n'` ⇒ identical. Line-comment loop `while (!IsAtEnd()
      && Peek() != '\n') { Advance(); }` is preserved verbatim. Block
      comment uses two-character lookahead via `c == '/' && PeekNext() ==
      '*'`; once entered, consumes the opening `/*` (two `Advance()`
      calls), then loops until `Peek() == '*' && PeekNext() == '/'`,
      consuming the closing `*/` (two `Advance()` calls) and `break`-ing
      out. The line-counter increment inside the block-comment loop
      (`_line++; _column = 0;`) is preserved exactly — note `_column = 0`
      not 1, paralleling the string-scanner nuance. NO support for nested
      block comments (Dart source doesn't, neither does C# version).
      Unterminated block comment: the Dart loop falls out at IsAtEnd and
      the next `_scanToken` call sees EOF — same behaviour in C#. Trailing
      else `break;` (exit the skipper when no whitespace/comment matched)
      preserved.
    idiom_id: null
    research_finding_id: rf-dart-string-indexing-to-csharp-char-indexing
    nuance: >-
      State-machine nuance (explicitly addressed): the skipper is a tiny
      hand-rolled DFA with four states-by-first-character — space/tab/CR,
      LF, `%` (line comment), `/*` (block comment). Preserved branch-for-
      branch in C#; no `switch` rewrite is attempted because the LF
      branch increments `_line` (a side effect) which would clutter a
      `switch`-expression rewrite. CRLF nuance: `\r` is silently consumed
      as whitespace (not a line break); `\n` is the SOLE line-break
      indicator. CRLF source files therefore produce one line-break per
      newline, identical to LF-only files — preserved by mirroring the
      Dart shape. Column-reset asymmetry: line-counter increments inside
      string and block-comment loops set `_column = 0` (anticipating the
      next Advance to bring it to 1), whereas the bare-newline branch in
      `_skipWhitespaceAndComments` sets `_column = 1` directly (because
      it `Advance()`s BEFORE the increment). This Dart asymmetry is
      PRESERVED in C# verbatim — it is internally consistent (both paths
      arrive at column=1 for the first character of the new line) and
      changing it would be a behaviour delta.
  - construct_key: dart.error_position.compile_error_throw_with_named_phase
    source_form: >-
      "throw CompileError('Expected \"..\" after \"=.\"', startLine,
      startColumn, phase: 'lexer');" — multiple occurrences in `_scanToken`
      / `_string` etc., all passing `phase: 'lexer'` named arg.
    target_decision: >-
      Emit `throw new CompileError("...", startLine, startColumn, phase:
      "lexer");` per the error.dart spec's named-parameter mapping
      (rf-dart-named-default-param-to-csharp-optional-arg). C# supports
      named-argument call syntax (`phase: "lexer"`) identically to Dart.
      All error messages preserved byte-for-byte to keep test/log
      assertions on diagnostic strings stable. The error position is the
      `startLine`/`startColumn` captured at the entry of the offending
      token, NOT the current cursor — preserved exactly.
    idiom_id: null
    research_finding_id: rf-dart-named-default-param-to-csharp-optional-arg
    nuance: >-
      Error-position nuance (explicitly addressed): a lexer error site is
      always reported at the START of the offending token (`startLine` /
      `startColumn` captured at the top of `_scanToken`), NOT the cursor
      at fault. Both languages preserve this by capturing the start
      coordinates into locals. Named-argument nuance: C# named-argument
      call syntax `phase: "lexer"` is grammatically identical to the Dart
      named-arg form, no translation needed at call sites. Phase string
      nuance: `'lexer'` lowercase is consumed by error.dart's
      `CategoryFromPhase` switch which maps it to
      `ErrorCategory.Lexical`/`Lexical` (depending on case-preservation
      decision recorded in error.dart spec) — the lowercase string is the
      keying mechanism, preserved verbatim.
  - construct_key: dart.tokenize_loop.list_accumulation_with_eof_sentinel
    source_form: >-
      "List<Token> tokenize() { final tokens = <Token>[]; while
      (!_isAtEnd()) { _skipWhitespaceAndComments(); if (_isAtEnd()) break;
      final token = _scanToken(); tokens.add(token); }
      tokens.add(Token(TokenType.EOF, '', _line, _column)); return tokens; }"
    target_decision: >-
      Emit `public List<Token> Tokenize()` returning `System.Collections.
      Generic.List<Token>`. Body: `var tokens = new List<Token>(); while
      (!IsAtEnd()) { SkipWhitespaceAndComments(); if (IsAtEnd()) break;
      var tok = ScanToken(); tokens.Add(tok); } tokens.Add(new Token(TokenType.
      EOF, "", _line, _column)); return tokens;`. The empty-lexeme EOF
      sentinel uses C# `""` (a length-0 string literal) — matches Dart `''`.
      The two-phase loop (whitespace-then-scan with re-check of `IsAtEnd`)
      preserved verbatim — required because whitespace skipping can
      itself reach EOF and a naive single-loop would call `ScanToken` on
      an empty cursor and read past the end.
    idiom_id: null
    research_finding_id: rf-dart-list-to-csharp-list-of-T
    nuance: >-
      Collection nuance (explicitly addressed): Dart `List<T>` is the
      growable-list interface (default implementation `_GrowableList`); C#
      counterpart is `System.Collections.Generic.List<T>` (Microsoft Learn:
      "Represents a strongly typed list of objects that can be accessed by
      index"). Both grow amortised-O(1); both expose `.Add` / `.add`.
      `<Token>[]` literal ⇒ `new List<Token>()`. EOF-sentinel nuance:
      adding an EOF token with empty lexeme is a recognised lexer
      convention; the consumer (parser) can rely on `tokens[tokens.Count -
      1].Type == TokenType.EOF`. Two-phase loop nuance: re-checking
      IsAtEnd after SkipWhitespaceAndComments is REQUIRED because
      whitespace/comments can consume to EOF; preserved exactly.
  - construct_key: dart.doc_comment_triple_slash
    source_form: >-
      "/// Lexical analyzer for GLP source code" and "/// Tokenize the
      entire source into a list of tokens" etc. — Dart triple-slash doc
      comments on the class and selected methods.
    target_decision: >-
      Map each `///` Dart doc comment to a C# XML-doc `/// <summary>...
      </summary>` placed on the corresponding declaration. Multi-line
      doc comments wrap the body in `<summary>...</summary>`. Trivial
      mechanical mapping.
    idiom_id: null
    research_finding_id: null
    nuance: trivial
    trivial: true
  - construct_key: dart.line_comment_inline
    source_form: >-
      "// Whitespace", "// Line comment: % to end of line", "// Block
      comment: /* ... */", "// closing quote", "// Otherwise fall
      through to identifier", "// Helper methods", "// Two dots not
      followed by = - back up and return single DOT", "// consume '?'",
      "// consume '.'", "// /", "// *", "// Check for 'mod' keyword - but
      only if not followed by '(' (predicate call)", "// Handle escape
      sequences", "// Handle decimal point", "// Handle negative sign",
      "// Check if it's a negative number literal or minus operator",
      "// Single-quoted: produce ATOM (quoted atom, usable as functor)",
      "// Double-quoted: produce STRING (string literal)",
      "// Check if this is a variable: starts with uppercase OR starts
      with _ followed by uppercase", "// Named anonymous variables like
      _Out, _Result are variables, not atoms", "// Check for reader
      syntax (Variable?)", "// Variable (uppercase or _Uppercase) or
      Atom (lowercase)", "// Arithmetic operators", "// Comparison
      operators" — inline `//` comments throughout the file.
    target_decision: >-
      Preserve as C# `//` line comments at the same source positions for
      byte-identical documentation shape. Trivial mechanical mapping.
    idiom_id: null
    research_finding_id: null
    nuance: trivial
    trivial: true
conversion_units:
  - "class Lexer (reference type, NOT record, NOT struct)"
  - "  property: string Source { get; } (initialised from ctor; Dart `final String source` → C# get-only auto-property)"
  - "  private long _current = 0 (mutable cursor)"
  - "  private long _line = 1 (mutable line counter)"
  - "  private long _column = 1 (mutable column counter)"
  - "  ctor: Lexer(string source) — assigns Source = source"
  - "  public List<Token> Tokenize() — two-phase loop (skip whitespace/comments, scan token), terminal EOF sentinel"
  - "  private Token ScanToken() — char-keyed switch dispatch over the first code unit; nested Match() lookahead for compound tokens; identifier / number / string sub-scanners; throws CompileError on unexpected char"
  - "  private Token Identifier(long start, long line, long column) — alpha-numeric run; keyword filter 'mod'/'procedure'; variable-vs-atom classification via IsUpper(text[0]); reader-form lookahead on '?'"
  - "  private Token Number(long start, long line, long column) — optional negative-sign rewind; integer digit run; optional decimal '.' lookahead + fractional digit run; double.Parse / long.Parse with CultureInfo.InvariantCulture; NUMBER token with boxed numeric Literal"
  - "  private Token String(char quote, long line, long column) — StringBuilder accumulation; escape switch with default-pass-through; line-counter increment on embedded LF; ATOM vs STRING dispatch on quote"
  - "  private void SkipWhitespaceAndComments() — hand-rolled DFA over space/tab/CR/LF/`%`/`/*`; line-counter increment on LF in both whitespace and block-comment branches"
  - "  private char Advance() — _column++; return _source[_current++]"
  - "  private bool Match(char expected) — at-end guard, equality check, conditional Advance"
  - "  private char Peek() — IsAtEnd() ? '\\0' : _source[_current]"
  - "  private char PeekNext() — _current + 1 >= _source.Length ? '\\0' : _source[(int)(_current + 1)]"
  - "  private bool IsAtEnd() — _current >= _source.Length"
  - "  private static bool IsDigit(char c) — ASCII-range '0'..'9' (NOT char.IsDigit)"
  - "  private static bool IsAlpha(char c) — ASCII-range 'a'..'z' / 'A'..'Z' / '_' (NOT char.IsLetter)"
  - "  private static bool IsAlphaNumeric(char c) — IsAlpha(c) || IsDigit(c)"
  - "  private static bool IsUpper(char c) — ASCII-range 'A'..'Z'"
  - "  private Token MakeToken(TokenType type, long line, long column) — slices a one-character lexeme via _source.Substring((int)(_current - 1), 1) and constructs Token"
  - "doc comments → /// <summary>...</summary> on class and selected methods"
  - "// line comments preserved at the same positions"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-final-field-class-to-csharp-getonly-class — Lexer class shape (reuse, token.dart family)

- Deep analysis: the Lexer is a stateful scanner — one `final` field
  (`source`) plus three mutable cursors (`_current`, `_line`, `_column`).
  `Tokenize()` mutates those cursors and yields tokens. Identity matters
  (the parser holds a single Lexer instance); mutation is intentional;
  defensive copies would break the loop.
- Authoritative Dart (cached, reused from token.dart):
  `https://dart.dev/language/class-modifiers` — Dart class instances are
  reference-typed; `final` is write-once per-instance.
- Authoritative .NET (cached): Microsoft Learn auto-properties — get-only
  auto-properties (`{ get; }` only) are write-once-via-constructor and
  match the public read-only surface Dart `final` exposes; mutable
  private fields remain as fields.
- Conclusion: C# reference `class Lexer` with one get-only auto-property
  `Source` and three mutable private `long` fields. Record/struct
  rejected (mutation, identity, reference semantics). Authoritative both
  sides; no escalation.

### rf-dart-string-indexing-to-csharp-char-indexing — String[i] → char[i] (NEW idiom)

- Deep analysis: this lexer is character-by-character. Every
  `source[_current]`, `_peek()`, `_peekNext()`, `_advance()` site reads a
  single position from `source` and compares it against ASCII literals.
  Dart's `String[i]` returns a one-code-unit String (NOT a `Rune`/`int`);
  C# `string[i]` returns a `char`. The conversion changes
  `_peek`/`_peekNext`/`_advance` return types from `string` to `char` and
  the `_match` parameter from `string` to `char` — driven by the C#
  indexer's native type.
- Authoritative Dart: WebFetch
  `https://api.dart.dev/dart-core/String/operator_get.html` (Dart
  official). Verbatim signature: `String operator [](int index)` —
  "Returns the substring of length 1 starting at the given index." Also
  on the same `String` class page: `int codeUnitAt(int index)` —
  "Returns the 16-bit UTF-16 code unit at the given index in this
  string." This documents that `String[i]` and `codeUnitAt(i)` return
  DIFFERENT types in Dart — the lexer uses both, picking `[i]` for
  character-equality comparisons and `codeUnitAt(0)` for range checks.
- Authoritative Dart (Rune semantics): WebFetch
  `https://api.dart.dev/dart-core/Runes-class.html` (Dart official) —
  "An [Iterable] of Unicode code-points of a string." Confirms that
  Rune iteration is a SEPARATE API not used by this lexer (the lexer is
  code-unit-based by design).
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.string.chars`
  (Microsoft Learn). Verbatim: "Gets the [Char](/en-us/dotnet/api/system.char)
  object at a specified position in the current String object." And the
  `String` class page: "A String object is a sequential collection of
  System.Char objects … The .NET String object is an in-memory array of
  Char objects" — UTF-16 code units. Confirms C# `string[i]` returns a
  16-bit code unit, same encoding as Dart `String.codeUnitAt(i)`.
- Authoritative .NET (Rune): WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.text.rune`
  (Microsoft Learn) — "Represents a Unicode scalar value … " confirms
  that `Rune` is the C# code-point counterpart NOT used in this
  conversion (we stay on `char` to match Dart's `String[i]` semantics).
- Conclusion: the new idiom rf-dart-string-indexing-to-csharp-char-indexing
  is: Dart `String[i]` (one-char String) ⇒ C# `string[i]` (char); the
  return types of `_peek`/`_peekNext`/`_advance` shift accordingly; the
  Match parameter shifts; the `'\x00'` sentinel becomes `'\0'`; all
  ASCII-literal comparisons stay textually identical (`c == '('` in both
  languages). FOR THIS SOURCE — every recognised lexeme is in the basic
  Latin block (U+0000..U+007F), so no surrogate concern at the recognition
  path; arbitrary text in string literals and line/block comments can
  contain surrogate pairs, which both languages PRESERVE bit-for-bit
  because both `StringBuffer.write` and `StringBuilder.Append(char)`
  append single code units. Column counting follows code units in both
  languages (a surrogate-pair character counts as 2 columns) — preserved
  unchanged. Authoritative both sides; no escalation.

### rf-dart-ascii-range-predicate-to-csharp-ascii-range-predicate — IsDigit/IsAlpha/IsUpper (NEW idiom)

- Deep analysis: the Dart helpers do NOT use any standard library
  classifier — they hard-code ASCII range comparisons via
  `code >= '0'.codeUnitAt(0) && code <= '9'.codeUnitAt(0)` etc. This is
  an INTENTIONAL ASCII-only design: the GLP source language is itself
  ASCII (the alphabet of identifiers, numbers, operators, and the four
  recognised quote/comment delimiters all live in U+0000..U+007F).
- Authoritative .NET (the helper we DO NOT use): WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.char.isdigit`
  (Microsoft Learn). Verbatim: `Char.IsDigit(Char)` — "Indicates whether
  the specified Unicode character is categorized as a decimal digit."
  Remarks: "This method determines whether a Char is a radix-10 digit.
  This contrasts with `IsNumber`, which determines whether a Char is of
  any numeric Unicode category." Decimal-digit codepoints include
  U+0660..U+0669 (Arabic-Indic), U+06F0..U+06F9 (Extended Arabic-Indic),
  U+0966..U+096F (Devanagari), U+FF10..U+FF19 (fullwidth), and many
  more. USING `Char.IsDigit` would broaden the lexer's accepted alphabet
  and silently diverge from Dart.
- Authoritative .NET (Char.IsLetter analogous):
  `https://learn.microsoft.com/en-us/dotnet/api/system.char.isletter` —
  "Indicates whether the specified Unicode character is categorized as a
  Unicode letter." Same hazard: includes all Unicode letter categories.
- Authoritative .NET (Char numeric conversion):
  `https://learn.microsoft.com/en-us/dotnet/api/system.char` —
  documents implicit conversion to ushort/int (`Char` is a 16-bit
  unsigned integer at the value-type level); range comparisons against
  `(int)'0'` / `(int)'9'` etc. work identically to Dart's
  `c.codeUnitAt(0)` comparisons.
- Authoritative Dart: WebFetch
  `https://api.dart.dev/dart-core/String/codeUnitAt.html` — already
  cited above; confirms `codeUnitAt(int)` returns the 16-bit UTF-16
  code unit as `int`.
- Conclusion: the new idiom is "preserve ASCII-range predicates
  verbatim — do NOT replace with `char.IsDigit`/`char.IsLetter`." Emit
  `private static bool IsDigit(char c) { if (c == '\0') return false;
  int code = (int)c; return code >= (int)'0' && code <= (int)'9'; }` and
  analogues. The implicit `char→int` promotion (or explicit `(int)c`)
  is the direct counterpart of Dart's `c.codeUnitAt(0)`. The '\x00'
  sentinel guard preserves "absent character" semantics returned by
  Peek/PeekNext past end-of-source. Authoritative both sides; no
  escalation.

### rf-dart-switch-on-char-to-csharp-switch-on-char — ScanToken dispatch (NEW idiom)

- Deep analysis: ScanToken is a giant first-character `switch (c)`
  whose cases are all one-character String literals in Dart and become
  `char` literals in C#. Every arm ends in `return` (so no implicit
  fall-through hazard) except the two stacked cases (`case '"': case
  "'":`) which share a body. The default arm uses `IsDigit(c)` /
  `IsAlpha(c)` dispatch and finally throws.
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/selection-statements#the-switch-statement`
  (Microsoft Learn). Verbatim: "The switch statement … selects a
  statement list to execute based on a pattern match with an expression"
  and "A case label specifies a pattern to compare with the match
  expression. If they match, control is transferred to the corresponding
  statement list." Also: "C# requires the end point of each switch
  section, including the last one, to be unreachable. To satisfy this
  requirement, you usually use the `break`, `return`, `throw`, or `goto`
  statements" — so the Dart shape (every arm ends in `return` or
  `throw`) directly satisfies the C# fall-through requirement.
- Authoritative .NET (char comparison):
  `https://learn.microsoft.com/en-us/dotnet/api/system.char.op_equality`
  — equality on `char` is value-equality on the underlying 16-bit
  unsigned integral.
- Authoritative Dart: WebFetch `https://dart.dev/language/branches`
  (Dart official) — Dart `switch` statement / expression page; cases
  can be constant patterns matching the discriminant by `==`. The lexer
  uses constant-pattern cases throughout.
- Conclusion: the new idiom is the natural switch-on-char dispatch with
  one-character-literal cases preserved verbatim, including stacked
  cases (C# syntax `case 'a': case 'b': body;`) and the explicit
  `throw new CompileError(...)` default. Authoritative both sides; no
  escalation.

### rf-dart-string-substring-to-csharp-string-substring — Identifier slice (NEW idiom)

- Deep analysis: Dart `source.substring(start, _current)` is used twice
  in this file (Identifier and Number) to extract the lexeme of a
  multi-character token. The signatures DIFFER between languages: Dart
  takes (start, end) half-open; C# takes (startIndex, length).
- Authoritative Dart: WebFetch
  `https://api.dart.dev/dart-core/String/substring.html` (Dart
  official). Verbatim: "String substring(int startIndex, [int? endIndex])
  — Returns the substring of this string that extends from startIndex,
  inclusive, to endIndex, exclusive."
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.string.substring`
  (Microsoft Learn). Verbatim: "Substring(Int32, Int32) — Retrieves a
  substring from this instance. The substring starts at a specified
  character position and has a specified length."
- Conclusion: Dart `source.substring(start, _current)` ⇒ C#
  `_source.Substring((int)start, (int)(_current - start))` (start
  position + LENGTH = _current - start). Width casts long→int are
  required because `Substring` takes `int` and our cursors are `long`;
  safe because cursor <= source.Length and source.Length itself is
  `int` in C# (the underlying String container's documented limit).
  Authoritative both sides; no escalation.

### rf-dart-number-parse-to-csharp-invariant-parse — locale-safe numeric parse (NEW idiom)

- Deep analysis: `text.contains('.') ? double.parse(text) : int.parse(text)`
  produces the NUMBER token's literal payload. The classifier
  `text.contains('.')` selects float vs integer. Dart's parse is
  locale-independent; C#'s default-overload Parse is locale-sensitive —
  a silent divergence under any non-en-US locale.
- Authoritative Dart: WebFetch `https://api.dart.dev/dart-core/double/parse.html`
  (Dart official). Verbatim: "Parses source as a, possibly signed,
  numeric literal and returns its value … The format is described in
  the grammar at the top of this library." No locale parameter, no
  ambient culture concept.
- Authoritative Dart (int): `https://api.dart.dev/dart-core/int/parse.html`
  — same: no locale.
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.double.parse`
  (Microsoft Learn). Verbatim: "If provider is null, the
  NumberFormatInfo for the current culture is used" — the default
  overload is locale-sensitive. Recommendation: "When the s parameter
  contains a string returned by the Double.ToString method, the s
  parameter is converted to its Double equivalent successfully without
  loss of precision … " (locale-paired round-trip). To force
  locale-independence: pass `CultureInfo.InvariantCulture`.
- Authoritative .NET (long.Parse):
  `https://learn.microsoft.com/en-us/dotnet/api/system.int64.parse` —
  same provider-default-is-current-culture caveat applies to integer
  parses involving sign characters; ASCII '0'-'9' digits are
  locale-invariant, but using the invariant-culture overload for both
  removes any chance of regression.
- Authoritative .NET (Contains char overload):
  `https://learn.microsoft.com/en-us/dotnet/api/system.string.contains`
  — `string.Contains(char)` added in .NET Core 2.1; returns `bool`.
  Direct counterpart of Dart `String.contains(Pattern)` with a
  one-character String.
- Conclusion: the new idiom is "Dart locale-independent numeric parse
  ⇒ C# locale-invariant numeric parse (always pass
  CultureInfo.InvariantCulture)." Width: `int.parse` ⇒ `long.Parse`
  (rf-dart-int-to-csharp-long-width). Authoritative both sides; no
  escalation.

### rf-dart-stringbuffer-to-csharp-stringbuilder — String scanner accumulation (NEW idiom)

- Deep analysis: the string scanner uses `StringBuffer` to accumulate
  characters from the source, processing escape sequences as it goes,
  and finally calls `toString()` to materialise the unescaped payload.
- Authoritative Dart: WebFetch `https://api.dart.dev/dart-core/StringBuffer-class.html`
  (Dart official). Verbatim: "A class for concatenating strings
  efficiently. Allows for the incremental building of a string using
  write*() methods."
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.text.stringbuilder`
  (Microsoft Learn). Verbatim: "Represents a mutable string of
  characters. This class cannot be inherited." And: "The
  System.Text.StringBuilder class can be used when you want to modify a
  string without creating a new object."
- Authoritative .NET (Append overloads):
  `https://learn.microsoft.com/en-us/dotnet/api/system.text.stringbuilder.append`
  — documents `Append(char)`, `Append(string)`, `Append(object)`. We
  use `Append(char)` for code-unit appends and `Append(char)` again
  for escape-expanded characters; `ToString()` materialises the
  string.
- Conclusion: Dart `StringBuffer()` ⇒ C# `new StringBuilder()`; `write(c)`
  ⇒ `Append(c)`; `toString()` ⇒ `ToString()`. Default-fall-through
  escape behaviour preserved verbatim. Authoritative both sides; no
  escalation.

### rf-dart-list-to-csharp-list-of-T — token accumulator (NEW idiom)

- Deep analysis: `final tokens = <Token>[];` + `tokens.add(token)` is
  the classic growable-list accumulator. Returning a `List<Token>` is
  the Tokenize() contract.
- Authoritative Dart: WebFetch `https://api.dart.dev/dart-core/List-class.html`
  (Dart official). Verbatim: "An indexable collection of objects with
  a length."
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1`
  (Microsoft Learn). Verbatim: "Represents a strongly typed list of
  objects that can be accessed by index. Provides methods to search,
  sort, and manipulate lists."
- Conclusion: `<Token>[]` ⇒ `new List<Token>()`; `.add` ⇒ `.Add`; the
  return type changes to `System.Collections.Generic.List<Token>`. A
  later signature-design pass MAY widen the return type to
  `IReadOnlyList<Token>` or `IList<Token>` (.NET-idiomatic for
  encapsulation), but the spec default is the concrete `List<Token>`
  for line-for-line faithfulness with the Dart signature.
  Authoritative both sides; no escalation.

### rf-dart-named-default-param-to-csharp-optional-arg — CompileError construction (reuse, error.dart)

- Deep analysis: every `throw CompileError(...)` site in this file
  uses the named optional `phase: 'lexer'`. Mapping is identical to
  the recorded idiom in error.dart.
- Authoritative basis: identical to the cached
  rf-dart-named-default-param-to-csharp-optional-arg finding from the
  error.dart spec; no new research required (FR-024 cache reuse —
  research_findings.is_authoritative=true is preserved).
- Conclusion: `throw new CompileError("msg", startLine, startColumn,
  phase: "lexer")` — C# named-argument call syntax mirrors Dart.
  Authoritative; no escalation.

## Notes

- Code-unit-vs-rune nuance is the single load-bearing Unicode decision
  in this file: BOTH languages walk UTF-16 code units in this lexer
  (no `Rune` iteration on either side), and the recognised lexeme
  alphabet is ASCII-only — so the conversion is trivially safe at the
  recognition path. Surrogate pairs in string-literal bodies and
  comment bodies are preserved bit-for-bit because both
  `StringBuffer.write`/`StringBuilder.Append(char)` append single code
  units. Column counting is per-code-unit on both sides (a surrogate
  pair counts as 2 columns) — preserved unchanged.
- ASCII-range predicates (`IsDigit`/`IsAlpha`/`IsUpper`) are
  DELIBERATELY preserved rather than swapped for `char.IsDigit` /
  `char.IsLetter` from the BCL: doing so would silently broaden the
  accepted alphabet to Unicode-categorised letters/digits, a behaviour
  delta from Dart.
- Numeric-parse locale-invariance: every `double.Parse` and
  `long.Parse` site MUST pass `CultureInfo.InvariantCulture` —
  default-overload usage is a latent Dart→C# parity bug under non-en-US
  locales.
- Substring half-open-vs-length signature: every Dart
  `source.substring(start, end)` ⇒ C# `_source.Substring((int)start,
  (int)(end - start))`. Mechanical but easy to get wrong.
- No async / Stream / Future / isolate / mixin / extension / generics-
  with-bounds / `late` / records / bitwise / shift / overflow path
  appears in this file — those well-known nuances are ABSENT and are
  correctly not asserted.
- The Dart `_match`/`_peek`/`_peekNext`/`_advance` signatures shift
  from `String`/one-char-String to `char` in C# because the C#
  indexer is `char`-typed natively — preserving the Dart `String`
  return type would force per-call substring allocation for what C#
  expresses zero-cost as a `char` value.
- Zero escalations: every non-trivial construct is resolved either by
  reuse of a recorded idiom (rf-dart-final-field-class-to-csharp-
  getonly-class, rf-dart-named-default-param-to-csharp-optional-arg)
  or by a new authoritative finding citing Dart api.dart.dev /
  dart.dev and .NET learn.microsoft.com documentation
  (rf-dart-string-indexing-to-csharp-char-indexing, rf-dart-ascii-
  range-predicate-to-csharp-ascii-range-predicate, rf-dart-switch-on-
  char-to-csharp-switch-on-char, rf-dart-string-substring-to-csharp-
  string-substring, rf-dart-number-parse-to-csharp-invariant-parse,
  rf-dart-stringbuffer-to-csharp-stringbuilder, rf-dart-list-to-
  csharp-list-of-T). No undecidable construct, no idiom-vs-research
  conflict, no idiom-vs-idiom conflict.
</content>
</invoke>