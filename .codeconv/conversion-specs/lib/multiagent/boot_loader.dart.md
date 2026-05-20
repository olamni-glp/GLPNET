# Conversion Spec — lib/multiagent/boot_loader.dart

> Conversion-spec artifact for lib/multiagent/boot_loader.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/multiagent/boot_loader.dart
source_sha256: 90d586b75da68e31051e94da4ad8577f0e93aeddcbc7fa2f0ac2ac2629a43a22
target_code_unit: lib/multiagent/boot_loader.cs
constructs:
  - construct_key: dart.data_class.final_fields_named_required_ctor_with_default_const_list
    source_form: >-
      "class SpawnDirective { final String agentId; final String goalFunctor;
      final int goalArity; final List<String> constantArgs; SpawnDirective({
      required this.agentId, required this.goalFunctor, required this.goalArity,
      this.constantArgs = const [], }); @override String toString() => ...; }"
    target_decision: >-
      Emit a C# reference `class SpawnDirective` (NOT `record`, NOT `struct`)
      with four get-only auto-properties initialised from a single constructor
      that takes the same four logical parameters; the Dart named-with-required
      style maps to ordinary C# constructor parameters (Dart `required` on
      named params is a compile-time obligation that the call site must supply
      the argument — the C# equivalent is simply a non-default constructor
      parameter with no default value, leaving the compile-site obligation
      identical in effect). The Dart default `this.constantArgs = const []` (a
      canonical empty const list) maps to a C# default `IReadOnlyList<string>?
      constantArgs = null` with the constructor body coalescing `?? Array.Empty<
      string>()` — `const []` is a SHARED INTERNED EMPTY LIST in Dart, so the
      C# spec MUST also use a single shared empty collection (Array.Empty<
      string>()) rather than `new List<string>()` per call, preserving the
      "no allocation per default" semantic. The property type is
      `IReadOnlyList<string>` (read-only view; the field is never mutated
      after construction in any caller of this file). A record is REJECTED
      because (a) downstream callers in isolate_manager.dart hold these
      instances by reference across the spawn pipeline and (b) the synthesised
      record `ToString` would emit a different shape than the explicit Dart
      `toString()` override below, which is load-bearing for diagnostic logs.
    idiom_id: null
    research_finding_id: rf-dart-final-field-class-to-csharp-getonly-class
    nuance: >-
      Immutability nuance (explicitly addressed): Dart `final` instance fields
      => C# get-only auto-properties (NOT `readonly` fields — property surface
      preserves Dart's field-access shape). Reference-vs-value nuance: must
      remain a reference `class` (heap object with identity) — the isolate
      spawn pipeline aliases the same SpawnDirective instances across the
      caller (BootLoader) and the consumer (isolate_manager.dart); a struct
      would force per-pass defensive copies. Null-safety nuance: all four
      fields are NON-nullable in Dart (String, int, List<String> — no `?`);
      they map to non-nullable C# types under enabled NRT. Default-empty-list
      nuance (load-bearing): Dart `const []` is a single interned empty list
      shared across every default-arg call site; the faithful C# render is a
      single shared `Array.Empty<string>()` (also a single interned instance
      per Microsoft Learn), NOT `new List<string>()` per call (which would
      change allocation behaviour).
  - construct_key: dart.tostring_override.string_interpolation_no_branch_with_slash_arity
    source_form: >-
      "@override String toString() => 'SpawnDirective($goalFunctor/$goalArity
      ($agentId, ...)@$agentId)';"
    target_decision: >-
      Emit `public override string ToString()` overriding
      `System.Object.ToString` with `$\"SpawnDirective({GoalFunctor}/{GoalArity}
      ({AgentId}, ...)@{AgentId})\"`. Dart `$id` interpolation maps to C#
      `{Id}`. The literal punctuation `SpawnDirective(`, `/`, `(`, `, ...)@`,
      `)` is preserved byte-identically because this string surfaces in
      diagnostic logs and possibly test assertions. Use expression-bodied form
      `=> ...;` to mirror the Dart single-expression arrow body.
    idiom_id: null
    research_finding_id: rf-dart-tostring-interp-to-csharp-tostring-interp
    nuance: >-
      toString nuance: Dart `toString()` override on a class is faithfully an
      override of `object.ToString()` (extension methods cannot override a
      virtual — that alternative is REJECTED). Interpolation nuance:
      Dart `$x` produces the unqualified textual form of `x` via `x.toString()`;
      C# `{X}` produces the same via `X.ToString()` in invariant default
      culture. For `goalArity` (Dart `int`, C# `long`), the invariant `ToString`
      of an integer is the same decimal text on both sides. Single-branch nuance:
      no null check is needed (none of the four fields are nullable), so the
      two-branch shape seen in token.dart's toString does NOT apply here.
  - construct_key: dart.data_class.final_and_mutable_fields_named_required_ctor_with_defaults
    source_form: >-
      "class BootConfig { final List<SpawnDirective> directives; final String
      fullSource; final String source; List<String>? sharedSources; String?
      projectDir; String rootSelfGlpPath; BootConfig({ required this.directives,
      required this.fullSource, required this.source, this.sharedSources,
      this.projectDir, this.rootSelfGlpPath = '', }); }"
    target_decision: >-
      Emit a C# reference `class BootConfig` (NOT record, NOT struct) with
      SIX properties: three GET-ONLY auto-properties for the Dart `final`
      fields (`Directives`, `FullSource`, `Source`) and three GET-SET
      auto-properties for the Dart NON-final fields (`SharedSources`,
      `ProjectDir`, `RootSelfGlpPath`). A single constructor mirrors the Dart
      named-with-required style: required params have no default; optional
      named params have C# defaults `IReadOnlyList<string>? sharedSources =
      null`, `string? projectDir = null`, `string rootSelfGlpPath = \"\"`.
      `Directives` is `IReadOnlyList<SpawnDirective>` (Dart `final` reference
      to a mutable list whose contents are not modified after construction
      anywhere in this file) — but the property type is read-only-view rather
      than `List<...>` to record the immutability invariant a reader sees.
      The mutable string-list `sharedSources` (Dart non-final + nullable)
      maps to a get/SET `List<string>?` property — preserving the fact that
      callers REASSIGN this field after construction (the property is the
      authoritative mutable surface; no record). A record is REJECTED for
      three reasons: (a) record properties default to `init` (settable only
      during construction-time `with`-expressions or object-initialiser), but
      Dart non-final fields are mutated by arbitrary reassignment at any
      time AFTER construction — `init` would silently forbid that; (b)
      record value equality would compare two BootConfigs by content, but
      Dart class equality is reference equality (no `==` override is defined),
      so a record would CHANGE semantic behaviour; (c) the spawn-pipeline
      caller (isolate_manager.dart) aliases the same BootConfig instance
      across multiple isolate spawns and reads back mutated fields — record-
      with-init would prevent the mutation.
    idiom_id: null
    research_finding_id: rf-dart-mutable-class-fields-to-csharp-getset-properties
    nuance: >-
      Immutability nuance (explicitly addressed and load-bearing): Dart
      `final` fields => C# `{ get; }` (no setter). Dart NON-final fields
      (no `final` keyword) => C# `{ get; set; }` (full read-write). The
      distinction matters: `sharedSources`, `projectDir`, `rootSelfGlpPath`
      are reassigned by callers AFTER construction (the empty-string default
      for `rootSelfGlpPath` is a placeholder the caller overwrites). Using
      `init` would silently change semantics from "mutable post-construction"
      to "settable only at construction". Null-safety nuance: `sharedSources`
      (`List<String>?`) and `projectDir` (`String?`) are the ONLY nullable
      fields — both map to nullable C# types under NRT. `rootSelfGlpPath`
      is NON-nullable with default `''` and maps to `string` with default
      `\"\"`. Reference-vs-value nuance: must remain a reference `class` —
      callers alias the same instance across spawns. Default-value nuance:
      `this.rootSelfGlpPath = ''` (an interned empty string literal) maps
      to C# `string rootSelfGlpPath = \"\"` (also interned per .NET
      string-pooling), preserving the no-allocation default.
  - construct_key: dart.exception_class.implements_Exception_with_message_field
    source_form: >-
      "class BootLoaderException implements Exception { final String message;
      BootLoaderException(this.message); @override String toString() =>
      'BootLoaderException: $message'; }"
    target_decision: >-
      Emit a C# class `BootLoaderException : Exception` (inheriting from
      `System.Exception`, per Microsoft Learn "create your own exception
      class by deriving from the Exception class"). Do NOT define an
      `IException` interface (no .NET equivalent of Dart's `Exception`
      *interface* exists — every C# throwable derives from `System.Exception`).
      The Dart instance field `message` is routed to the base
      `Exception.Message` via `: base(message)` constructor chaining,
      avoiding a duplicate field; the C# class exposes `Message` (inherited)
      as the public surface that Dart's `e.message` consumers will read in
      the converted code. The Dart `toString()` override is rendered as a
      C# `public override string ToString() => $\"BootLoaderException:
      {Message}\"` (NOT `{this.Message}`, which is identical but noisier),
      preserving the exact diagnostic-text prefix used by Dart isolate-boot
      tests and isolate_manager.dart error paths.
    idiom_id: null
    research_finding_id: rf-dart-implements-exception-to-csharp-exception-base
    nuance: >-
      Exception base-class nuance (explicitly addressed): Dart `Exception`
      is an *interface* — `implements Exception` is conformance, not
      inheritance — but .NET has no equivalent interface (`System.Exception`
      is a concrete base CLASS and every throwable must derive from it).
      The faithful Dart-`implements Exception` → C#-`: Exception`
      (inheritance) mapping is well-established and authoritative-grounded.
      Message-field nuance: Dart's `Exception` interface itself declares
      no `message` member (it is just a marker); the field `message`
      defined here happens to coincide with .NET `Exception.Message`, so
      we route through the base via `: base(message)` rather than
      duplicating the field. Naming nuance: Microsoft's naming
      recommendation is "end the class name of the user-defined exception
      with the word 'Exception'" (so `BootLoaderException`) — and the Dart
      source already follows this convention verbatim, so no rename is
      required (CompileError naming-suffix tension does not arise here).
      toString nuance: the override REPLACES (not extends) the default
      `Exception.ToString()` which would otherwise emit
      `BootLoaderException: <message>\\n<stacktrace>` — the Dart override
      emits only the first line; the C# override matches that exact shape.
  - construct_key: dart.regexp.regexp_with_multiline_dotall_options_and_named_apis
    source_form: >-
      "RegExp(r'procedure\\s+boot\\s*\\.', multiLine: true)"
      ", RegExp(r'boot\\s*:-\\s*(.*?)\\.\\s*(?=\\n|procedure|$)', multiLine:
      true, dotAll: true)"
      ", RegExp(r'@\\s*(\\w+)')"
      ", RegExp(r'(\\w+)$')"
      ", RegExp(r'^\\w+$')"
      ", and `.hasMatch(s)`, `.firstMatch(s)`, `.allMatches(s)`."
    target_decision: >-
      Map Dart `RegExp` to C# `System.Text.RegularExpressions.Regex`. Each
      Dart raw-string pattern (`r'...'`) maps to a C# verbatim string `@\"...\"`
      preserving every backslash and metacharacter byte-identically — the
      patterns themselves use only PCRE-compatible features that .NET Regex
      supports verbatim (`\\s`, `\\w`, `\\.`, anchors `^`/`$`, non-greedy
      `*?`, lookahead `(?=...)`, capture groups `(...)`). Option-flag mapping:
      Dart `multiLine: true` => `RegexOptions.Multiline` (both make `^`/`$`
      match at line breaks, not just string ends); Dart `dotAll: true` =>
      `RegexOptions.Singleline` (both make `.` match `\\n`). Method mapping:
      Dart `.hasMatch(s)` => `regex.IsMatch(s)`; `.firstMatch(s)` =>
      `regex.Match(s)` returning a `Match` (use `.Success`/`.Groups[1].Value`
      where Dart uses `match?.group(1)`); `.allMatches(s)` =>
      `regex.Matches(s)` returning a `MatchCollection`. Each Regex is
      constructed inline (matches Dart's inline `RegExp(...)`); a
      production codegen pass may later promote frequently-used patterns to
      `static readonly Regex` or source-generated `[GeneratedRegex]` for
      perf, but the SPEC default keeps the inline construction one-to-one
      with the Dart source for review-fidelity.
    idiom_id: null
    research_finding_id: rf-dart-regexp-to-csharp-regex
    nuance: >-
      Regex-flavour nuance (explicitly addressed): Dart `RegExp` uses ECMA-
      262 regex syntax; .NET `Regex` uses a near-superset (ECMA-262 mode is
      available via `RegexOptions.ECMAScript`, but is NOT needed here — the
      features used by this file are common to both flavours). Option-flag
      naming nuance (load-bearing): Dart `dotAll` is named OPPOSITE to .NET
      `RegexOptions.Singleline` (same semantic — `.` matches `\\n`); the
      conversion MUST flip the name correctly or the regex will silently
      fail to match multi-line boot clauses. Dart `multiLine` and .NET
      `RegexOptions.Multiline` agree by name and semantics. Capture-group
      nuance: Dart `match.group(1)` is 1-indexed; C# `Match.Groups[1]` is
      also 1-indexed (group 0 is the whole match in both); `.group(1)!` in
      Dart asserts non-null, which in C# corresponds to `Groups[1].Success`
      being true — `.Value` on an unsuccessful group returns empty string,
      so the spec MUST check `Success` before forcing the value where the
      Dart code uses `!`. Raw-string nuance: Dart `r'...'` and C# `@\"...\"`
      both disable escape processing — every `\\s`/`\\w`/`\\.` in the Dart
      raw pattern maps to the same byte sequence in the C# verbatim pattern.
  - construct_key: dart.iterable.split_where_join_for_line_filter
    source_form: >-
      "source.split('\\n').where((line) => !line.trimLeft().startsWith('%'))
      .join('\\n')"
    target_decision: >-
      Map to LINQ-equivalent: `string.Join(\"\\n\", source.Split('\\n').Where(
      line => !line.TrimStart().StartsWith(\"%\")))`. Dart `String.split('\\n')`
      => `string.Split('\\n')` (returns string[]). Dart `.where(pred)` =>
      LINQ `Where(pred)` (deferred). Dart `.join('\\n')` (terminal) =>
      `string.Join(\"\\n\", ...)` (forces the LINQ enumeration). Dart
      `trimLeft()` (Dart-core method) => .NET `string.TrimStart()` — the
      .NET name is `TrimStart`, not `TrimLeft`, but the semantics are
      identical (removes leading whitespace by default). Dart
      `startsWith('%')` => `StartsWith(\"%\")` (default ordinal comparison
      on both sides for single-char prefix tests).
    idiom_id: null
    research_finding_id: rf-dart-iterable-where-to-linq
    nuance: >-
      Eager-vs-lazy nuance (explicitly addressed): Dart `.where` is lazy,
      terminated here by `.join('\\n')` which forces materialisation; C#
      LINQ `Where` is likewise deferred and `string.Join` enumerates eagerly
      — equivalence holds because the terminal call materialises in both
      languages. Newline-literal nuance: Dart string `'\\n'` is a single
      LF character (U+000A); C# `\"\\n\"` is identical (Environment.NewLine
      is NOT used here because the source file uses raw `\\n` as a hard-coded
      line separator for the GLP textual format — must be preserved
      byte-identically, not platform-flexed). Method-name nuance: Dart
      `trimLeft()`/`trimRight()` are spelled differently from .NET
      `TrimStart()`/`TrimEnd()` — the rename is mechanical but easy to miss;
      record it here so codegen does not emit a missing-method error.
  - construct_key: dart.collection.set_literal_typed_contains_add
    source_form: >-
      "final agentIds = <String>{}; if (agentIds.contains(d.agentId)) { throw
      ... } agentIds.add(d.agentId);"
    target_decision: >-
      Map Dart `<String>{}` (typed empty set literal) to C# `new HashSet<
      string>()`. Dart `Set<E>` is interface-typed but the default literal
      construction is a `LinkedHashSet` (insertion-ordered) per the dart-core
      docs; the call-site here uses the set only for membership testing
      (`contains`/`add`), so iteration order is NOT consumed and the C#
      `HashSet<string>` (unordered, hash-based) is a faithful mapping for
      the observable semantics. `.contains(x)` => `Contains(x)`;
      `.add(x)` => `Add(x)`. The duplicate-detection idiom (check
      `Contains` then `Add`) preserves the early `throw` semantics — using
      the slightly more idiomatic C# `if (!Add(x)) throw ...` would change
      the order of operations (add-then-check vs check-then-add), so the
      SPEC preserves the Dart shape for review-fidelity.
    idiom_id: null
    research_finding_id: rf-dart-set-literal-to-csharp-hashset
    nuance: >-
      Set-implementation nuance (explicitly addressed): Dart's default set
      literal `{}` (or typed `<E>{}`) is a `LinkedHashSet<E>` per dart-core
      — INSERTION-ORDERED. If any caller iterated the set, the .NET
      `HashSet<E>` would be a semantic drift (HashSet is unordered) and we
      would have to map to `OrderedDictionary` or a custom collection. In
      this file the set is read ONLY via `.contains` (no iteration), so the
      drift is unobservable and `HashSet<string>` is faithful. This nuance
      is recorded explicitly so a future change introducing iteration would
      trigger a re-spec. Type-parameter nuance: Dart `<String>{}` is a typed
      empty set literal; C# `new HashSet<string>()` carries the same
      explicit type parameter. The Dart `final` reference variable maps to
      C# `var` (local variable; immutability of the reference is enforced
      by no reassignment, which is a coding convention rather than a
      compile-time guarantee — Dart `final` IS compile-time-enforced, but
      C# `var` for a local is conventionally not reassigned in idiomatic
      code; the conversion records the Dart final intent and leaves
      enforcement to code review).
  - construct_key: dart.null_aware.optional_member_chain_with_force_unwrap
    source_form: >-
      "final match = pattern.firstMatch(source); return match?.group(1)?.trim();"
      " ... final targetAgentId = atMatch.group(1)!; ... final functor =
      functorMatch.group(1)!;"
    target_decision: >-
      Dart null-aware member chain `match?.group(1)?.trim()` maps to a C#
      conditional-access chain `match?.Groups[1]?.Value?.Trim()` — BUT the
      semantics differ in one important detail: C# `Match.Groups[1].Value`
      is non-null for unsuccessful groups (returns empty string), not null.
      The faithful rendering is: `if (match.Success) return
      match.Groups[1].Value.Trim(); else return null;` — preserving Dart's
      "no match => null" return semantics explicitly. The Dart force-unwrap
      `match.group(1)!` (after a successful `allMatches` iteration where
      the regex has a capture group) maps to a defensive `match.Groups[1]
      .Value` access after `match.Success` is implicit (the
      `regex.Matches(s)` iterator only yields successful matches in both
      .NET and Dart); we MAY emit the unchecked `.Value` access without an
      explicit assertion, mirroring Dart's `!`.
    idiom_id: null
    research_finding_id: rf-dart-null-aware-chain-to-csharp-conditional-access
    nuance: >-
      Null-aware semantics nuance (explicitly addressed and load-bearing):
      Dart `?.` short-circuits the WHOLE chain to `null` on the first null
      receiver; C# `?.` does the same — semantics agree. The cliff is
      `Match.Groups[1].Value`: Dart `match.group(1)` returns `String?`
      (null on no match); C# `Match.Groups[1].Value` returns `string` and
      defaults to empty when the group did not match. The SPEC therefore
      requires an explicit `if (match.Success) ... else return null;`
      branch, NOT a transliterated `match?.Groups[1]?.Value?.Trim()` (which
      would coerce no-match into empty-string-then-trimmed-empty rather
      than null). This is a non-mechanical semantic correction. Force-
      unwrap nuance: Dart `!` asserts non-null at runtime; the closest C#
      equivalent is the null-forgiving operator `!` (compile-time hint only,
      no runtime check). Where Dart `!` follows a successful `firstMatch`
      whose capture group is REQUIRED by the regex (no `?` quantifier on
      the group), the C# code can drop both `?` and `!` and access
      `.Groups[1].Value` directly.
  - construct_key: dart.string.replaceFirst_with_regex_and_replacement
    source_form: >-
      "var result = source.replaceFirst(RegExp(r'procedure\\s+boot\\s*\\.\\s*\\n?',
      multiLine: true), '');"
      "result = result.replaceFirst(RegExp(r'boot\\s*:-\\s*.*?\\.\\s*\\n?',
      multiLine: true, dotAll: true), '');"
    target_decision: >-
      Dart `String.replaceFirst(Pattern, String)` (where the Pattern is a
      RegExp) maps to .NET `Regex.Replace(input, replacement, count: 1)` —
      the explicit `count: 1` argument is the load-bearing translation
      because the default `Regex.Replace(input, repl)` replaces ALL
      occurrences (Dart's `replaceFirst` replaces only the FIRST). Use
      either `new Regex(pattern, options).Replace(input, replacement, 1)` or
      the static `Regex.Replace(input, pattern, replacement, options)` with
      the count-overload form. Spec preference: the instance method form
      `new Regex(...).Replace(input, repl, 1)` to keep the regex
      construction inline mirroring the Dart source.
    idiom_id: null
    research_finding_id: rf-dart-string-replaceFirst-to-csharp-regex-replace-count1
    nuance: >-
      Replace-count nuance (explicitly addressed and load-bearing): Dart
      `String.replaceFirst` replaces ONLY THE FIRST occurrence; .NET
      `Regex.Replace(input, repl)` (no count arg) replaces ALL occurrences.
      A transliteration that omits the count argument would silently change
      semantics and could strip multiple boot-clauses from a file. The
      explicit `count: 1` argument is mandatory. Newline-handling nuance:
      both patterns trail with `\\s*\\n?` (consume the trailing newline if
      present); .NET Regex handles the same byte-pattern identically. The
      `multiLine` and `dotAll` options translate to `RegexOptions.Multiline
      | RegexOptions.Singleline` for the second call; the first uses only
      `RegexOptions.Multiline`. Final `.trim() + '\\n'` => `.Trim() +
      \"\\n\"` (Dart `String.trim` trims both ends, like .NET `Trim()` —
      same semantics).
  - construct_key: dart.unimplemented.platform_stub_throws_unimplemented_error
    source_form: >-
      "String _readFile(String filePath) { throw UnimplementedError('Use
      load(source) directly or implement file reading'); }"
    target_decision: >-
      Map Dart `UnimplementedError` to C# `System.NotImplementedException`
      (the .NET counterpart that signals "feature not yet implemented" per
      Microsoft Learn). Emit `private string ReadFile(string filePath) {
      throw new NotImplementedException(\"Use Load(source) directly or
      implement file reading\"); }`. The platform-stub intent is preserved:
      the method exists to satisfy the API surface (callable from
      `LoadFile`) but signals to callers that file I/O is not provided by
      this layer and must be implemented platform-specifically. The Dart
      package-private `_readFile` (leading underscore) maps to C# `private`
      (same semantic — accessible only within the declaring class; Dart's
      library-private and C#'s class-private differ slightly in scope, but
      this method is referenced only from `loadFile` inside the same class,
      so `private` is faithful here).
    idiom_id: null
    research_finding_id: rf-dart-unimplemented-error-to-csharp-notimplemented
    nuance: >-
      Exception-class nuance (explicitly addressed): Dart `UnimplementedError`
      extends `Error` (a programming-defect signal, not a recoverable
      `Exception`); .NET `NotImplementedException` derives from
      `SystemException` (recoverable in theory but semantically a defect
      indicator). The faithful mapping is by INTENT (both signal "this
      method is intentionally not implemented in this layer"), not by
      class-hierarchy. .NET does NOT have a distinct `Error` vs `Exception`
      split — every throwable is `Exception` or derived. Visibility nuance
      (explicitly addressed): Dart's `_name` leading-underscore convention
      is LIBRARY-private (visible to other files in the same Dart library),
      not class-private. C# `private` is class-private (strictly tighter).
      Where the Dart underscore method is called only from within the same
      class (as is the case for `_readFile`, called only from `loadFile`),
      `private` is faithful. For underscore methods called from OTHER files
      in the same Dart library, `internal` would be the correct mapping —
      but that case does NOT arise in this file.
  - construct_key: dart.string.index_access_character_codeunits
    source_form: >-
      "if (beforeAt[beforeAt.length - 1] != ')') ... ; for (var i =
      beforeAt.length - 1; i >= 0; i--) { if (beforeAt[i] == ')') depth++;
      if (beforeAt[i] == '(') depth--; ... } ... if (argsStr[i] == '('
      || argsStr[i] == '[') depth++; ..."
    target_decision: >-
      Dart `String[i]` returns a one-character String (a substring of length
      1); C# `string[i]` returns a `char`. Compare-to-literal pattern
      `beforeAt[i] == ')'` (Dart) maps to `beforeAt[i] == ')'` (C#) — BUT
      the Dart literal `')'` is a String of length 1, while the C# literal
      `')'` is a `char`. The render is byte-identical at the source level
      (same character literal in both languages), but the underlying types
      differ. .NET `string` indexing returning `char` is the faithful and
      idiomatic counterpart; comparing `string[i]` to a char literal is
      direct and avoids the Dart-specific substring-of-length-1 indirection.
      String-length `beforeAt.length` => `beforeAt.Length` (Dart lower-case
      length getter vs C# upper-case Length property). The Dart `[` and
      `]` literal char comparisons map identically.
    idiom_id: null
    research_finding_id: rf-dart-string-index-to-csharp-char-index
    nuance: >-
      String-indexing nuance (explicitly addressed and load-bearing): Dart
      `String[i]` returns a `String` (length 1); C# `string[i]` returns a
      `char` (UTF-16 code unit). For ASCII characters used in this file
      (parentheses, brackets, comma, percent), the comparison semantics
      agree: a Dart `String` of length 1 with content `')'` equals the
      Dart literal `')'` iff a C# `char` `')'` equals the C# literal `')'`.
      For supplementary-plane characters (surrogate pairs), the two
      languages would diverge — but no such characters appear in this
      file's character set (parens, brackets, ASCII letters/digits, `_`,
      `%`). The conversion is faithful at this code site; future code
      handling arbitrary Unicode would need explicit normalisation. Index
      bound nuance: Dart `String.length` and C# `string.Length` both return
      the UTF-16 code-unit count; loop bounds carry across verbatim.
  - construct_key: dart.string.substring_pair_start_end_argument
    source_form: >-
      "final beforeAt = clauseBody.substring(0, atMatch.start).trimRight();
      ... final beforeParen = beforeAt.substring(0, parenStart).trimRight();
      ... final argsStr = beforeAt.substring(parenStart + 1, beforeAt.length
      - 1); ... args.add(argsStr.substring(start, i)); ... args.add(argsStr
      .substring(start));"
    target_decision: >-
      Map Dart `String.substring(start, end)` (end-EXCLUSIVE) to C# `string
      .Substring(startIndex, length)` (LENGTH-based, NOT end-based) — the
      semantic mismatch is load-bearing and MUST be addressed at every
      call site: `s.substring(a, b)` in Dart => `s.Substring(a, b - a)` in
      C#. Single-argument `s.substring(start)` (Dart, to end of string) =>
      `s.Substring(start)` (C#) — single-argument forms agree (both
      "from index to end"). The Dart `trimRight()` chained to the result
      maps to .NET `TrimEnd()` (note name change — `trimRight` is Dart-only;
      .NET names by end position, not direction).
    idiom_id: null
    research_finding_id: rf-dart-substring-end-vs-csharp-substring-length
    nuance: >-
      Substring-argument-convention nuance (explicitly addressed and
      load-bearing — this is one of the easiest-to-miss Dart→C# bugs):
      Dart's two-arg overload `substring(start, end)` takes the EXCLUSIVE
      END index; .NET's two-arg overload `Substring(startIndex, length)`
      takes a LENGTH. A transliteration that copies both arguments
      verbatim would extract a substring `[start..start+end)` instead of
      `[start..end)` — completely wrong for any `end > start*2`. Every
      call site MUST be rewritten as `s.Substring(a, b - a)`. The
      single-arg `substring(start)` overload IS faithful verbatim (both
      languages: from start to end). TrimRight/TrimEnd naming nuance:
      mechanical rename, easy to miss but easy to fix.
conversion_units:
  - "class SpawnDirective (reference type, NOT record, NOT struct)"
  - "  property: string AgentId { get; }"
  - "  property: string GoalFunctor { get; }"
  - "  property: long GoalArity { get; }"
  - "  property: IReadOnlyList<string> ConstantArgs { get; }"
  - "  ctor: SpawnDirective(string agentId, string goalFunctor, long goalArity, IReadOnlyList<string>? constantArgs = null) — ConstantArgs assigned from `constantArgs ?? Array.Empty<string>()`"
  - "  override ToString() — expression-bodied interpolated string, byte-identical to Dart output"
  - "class BootConfig (reference type, NOT record, NOT struct)"
  - "  property: IReadOnlyList<SpawnDirective> Directives { get; }"
  - "  property: string FullSource { get; }"
  - "  property: string Source { get; }"
  - "  property: List<string>? SharedSources { get; set; }"
  - "  property: string? ProjectDir { get; set; }"
  - "  property: string RootSelfGlpPath { get; set; }   // default \"\""
  - "  ctor: BootConfig(IReadOnlyList<SpawnDirective> directives, string fullSource, string source, List<string>? sharedSources = null, string? projectDir = null, string rootSelfGlpPath = \"\")"
  - "class BootLoader (reference type; methods only, no fields)"
  - "  public BootConfig Load(string source) — orchestrates _parseBootClause + _stripBootClause"
  - "  public BootConfig LoadFile(string filePath) — convenience wrapper over _readFile + Load"
  - "  private IReadOnlyList<SpawnDirective> _parseBootClause(string source) — uses _removeComments, _hasProcedureBoot, _extractBootClause, _parseSpawnDirectives; emits BootLoaderException on malformed input; duplicate-agent-id check via HashSet<string>"
  - "  private string _removeComments(string source) — Split('\\n') + Where(!TrimStart().StartsWith(\"%\")) + Join(\"\\n\")"
  - "  private bool _hasProcedureBoot(string source) — inline Regex with RegexOptions.Multiline + IsMatch"
  - "  private string? _extractBootClause(string source) — inline Regex with RegexOptions.Multiline | RegexOptions.Singleline + Match; explicit Success branch returning null on no-match"
  - "  private IReadOnlyList<SpawnDirective> _parseSpawnDirectives(string clauseBody) — inline Regex.Matches enumeration; balanced-paren backwards scan; arg splitting via _splitArgs; functor regex; agent-id atom regex; throws BootLoaderException on invariant violations"
  - "  private IReadOnlyList<string> _splitArgs(string argsStr) — char-by-char depth tracking with [(],[[],[)],[]] brackets at depth-0 commas; returns List<string>"
  - "  private string _stripBootClause(string source) — two new Regex(pattern, options).Replace(source, \"\", 1) calls (count: 1 is mandatory) + Trim() + \"\\n\""
  - "  private string _readFile(string filePath) — throws new NotImplementedException(\"Use Load(source) directly or implement file reading\") (platform-stub)"
  - "class BootLoaderException : Exception"
  - "  ctor: BootLoaderException(string message) : base(message)"
  - "  override ToString() — expression-bodied `$\"BootLoaderException: {Message}\"` (replaces Exception.ToString default)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-final-field-class-to-csharp-getonly-class — immutable data class (reuse, token/analysis_phase)

- Deep analysis: `SpawnDirective` has four `final` fields and a single named-
  with-required constructor with one default-empty-list parameter. Callers
  (BootLoader._parseSpawnDirectives + isolate_manager.dart) hold instances
  by reference across the spawn pipeline; no mutation after construction;
  `toString()` is overridden for diagnostic logging. Reference identity is
  incidental; structural equality is NOT defined (no `==` / `hashCode`
  overrides in the Dart source), so the consumers depend on reference
  identity OR they do not compare these instances at all.
- Authoritative Dart (cached): WebFetch `https://dart.dev/language/class-
  modifiers` and `https://dart.dev/language/constructors` — `final`
  instance fields are write-once; named-required parameters are
  compile-site obligations; default values for optional named params are
  evaluated once and re-used.
- Authoritative .NET (cached): WebFetch `https://learn.microsoft.com/en-
  us/dotnet/csharp/programming-guide/classes-and-structs/auto-implemented-
  properties` — get-only auto-properties (`{ get; }` only) are
  write-once-via-constructor; the shape and access surface match Dart
  `final` fields. WebFetch `https://learn.microsoft.com/en-us/dotnet/api/
  system.array.empty` — `Array.Empty<T>()` returns a shared, interned,
  zero-length array, mirroring Dart `const []`'s shared-empty-instance
  semantic.
- Conclusion: emit reference `class SpawnDirective` with four get-only
  properties initialised from a single constructor; default-empty-list
  param uses `IReadOnlyList<string>? = null` with constructor-body coalesce
  to `Array.Empty<string>()` to preserve the no-allocation default.
  Authoritative both sides; no escalation.

### rf-dart-tostring-interp-to-csharp-tostring-interp — debug toString (reuse, token)

- Deep analysis: `SpawnDirective.toString()` is a single-expression arrow
  body with no null check (all four fields non-nullable). The string
  shape `SpawnDirective($goalFunctor/$goalArity($agentId, ...)@$agentId)`
  surfaces in diagnostic logs and very likely in test assertions.
- Authoritative Dart (cached): dart.dev `toString()` is a virtual method
  on `Object`. String interpolation `$id` produces `id.toString()`.
- Authoritative .NET (cached): Microsoft Learn `Object.ToString` — virtual,
  overridable; C# `$"{X}"` interpolation calls `X.ToString()` in invariant
  default culture.
- Conclusion: override `object.ToString()` (NOT an extension method —
  extensions cannot override a virtual). Render the interpolated string
  byte-identically. Single-branch (no null-check) because the source has
  no null-check. Authoritative; no escalation.

### rf-dart-mutable-class-fields-to-csharp-getset-properties — mixed-mutability data class

- Deep analysis: `BootConfig` has THREE `final` fields (`directives`,
  `fullSource`, `source`) and THREE NON-`final` fields (`sharedSources`,
  `projectDir`, `rootSelfGlpPath`). Callers REASSIGN the non-final fields
  after construction (the default empty-string `rootSelfGlpPath = ''` is a
  placeholder for the caller to overwrite). The Dart source defines no
  `==` / `hashCode` overrides, so equality is reference-identity, and
  callers (isolate_manager.dart) alias the same BootConfig across multiple
  isolate spawns.
- Authoritative Dart (cached): WebFetch `https://dart.dev/language/classes`
  — Dart non-`final` instance fields are read-write (assignable any time
  after construction); `final` fields are write-once. Optional named
  parameters with default values are evaluated once and assigned in the
  initializer.
- Authoritative .NET (cached + new): WebFetch `https://learn.microsoft
  .com/en-us/dotnet/csharp/properties` — `{ get; set; }` is read-write;
  `{ get; init; }` (init-only setter, C# 9+) restricts mutation to
  object-initialiser / `with`-expression time; `{ get; }` is get-only
  (write-once via constructor). The `init` accessor does NOT permit
  post-construction reassignment, which is the load-bearing distinction
  here: Dart non-final fields ARE post-construction-reassigned, so
  `{ get; set; }` (NOT `init`) is the faithful render.
- Conclusion: three get-only properties for the Dart final fields, three
  get/set properties for the non-final fields, single constructor mirroring
  the Dart named-required-and-defaults shape, reference `class` (not
  record — record `init` would forbid post-construction mutation; record
  value equality would change semantics from Dart's reference equality).
  Authoritative; no escalation.

### rf-dart-implements-exception-to-csharp-exception-base — exception class (reuse, error.dart pattern)

- Deep analysis: `BootLoaderException implements Exception` has a `final
  String message` field, a positional constructor, and a `toString()`
  override that REPLACES the default `Exception.toString()` shape with
  `'BootLoaderException: $message'`. The class is thrown from
  BootLoader._parseBootClause on malformed input and caught by the spawn
  pipeline.
- Authoritative Dart (cached): WebFetch `https://api.dart.dev/dart-core/
  Exception-class.html` — Dart `Exception` is an *interface* with no
  declared members (a marker for "intended to be caught"); `implements
  Exception` is conformance, NOT inheritance.
- Authoritative .NET (cached): WebFetch `https://learn.microsoft.com/en-
  us/dotnet/standard/exceptions/how-to-create-user-defined-exceptions` —
  "create your own exception class by deriving from the Exception class."
  Every throwable in .NET derives from `System.Exception`; there is no
  exception INTERFACE. WebFetch `https://learn.microsoft.com/en-us/dotnet/
  api/system.exception.message` — `Exception.Message` is a virtual
  read-only property set via `: base(message)` from a derived ctor.
- Conclusion: emit `class BootLoaderException : Exception`; route the
  Dart `message` field to the base `Exception.Message` via `: base(message)`
  ctor chaining; override `ToString()` to emit the Dart-exact diagnostic
  prefix `BootLoaderException: <message>` (replacing the .NET default
  shape which would include the stack trace). Naming-suffix policy is
  not in tension (the Dart source already uses the `...Exception` suffix
  matching Microsoft's recommendation). Authoritative both sides; no
  escalation.

### rf-dart-regexp-to-csharp-regex — inline regex with flag mapping

- Deep analysis: five inline RegExp constructions: `procedure\s+boot\s*\.`
  (multiLine), `boot\s*:-\s*(.*?)\.\s*(?=\n|procedure|$)` (multiLine +
  dotAll), `@\s*(\w+)` (no flags), `(\w+)$` (no flags, anchored end),
  `^\w+$` (no flags, anchored both). Calls: `.hasMatch`, `.firstMatch`,
  `.allMatches`. Patterns use only PCRE-common features (`\s`, `\w`,
  anchors, lookahead, non-greedy quantifier, capture groups).
- Authoritative Dart: WebFetch `https://api.dart.dev/dart-core/RegExp-
  class.html` — "Regular expressions use the same syntax and semantics as
  JavaScript." Constructor named parameters `multiLine` (matches `^`/`$`
  at line breaks), `dotAll` (`.` matches `\n`), `caseSensitive`, `unicode`.
- Authoritative .NET: WebFetch `https://learn.microsoft.com/en-us/dotnet/
  api/system.text.regularexpressions.regexoptions` —
  `RegexOptions.Multiline`: "changes the meaning of `^` and `$` so they
  match at the beginning and end, respectively, of any line, and not just
  the beginning and end of the entire string." `RegexOptions.Singleline`:
  "changes the meaning of the dot (`.`) so it matches every character
  (instead of every character except `\n`)." The flag-name OPPOSITION
  (Dart `dotAll` ⇔ .NET `Singleline`) is the load-bearing translation.
  WebFetch `https://learn.microsoft.com/en-us/dotnet/standard/base-types/
  regular-expression-language-quick-reference` — `\s`/`\w`/anchors/
  lookahead/`*?` quantifier all supported, byte-identical to Dart usage.
- Conclusion: inline `new Regex(@"...", RegexOptions.Multiline [|
  RegexOptions.Singleline])` constructions matching each Dart RegExp
  one-to-one. The `dotAll` ⇔ `Singleline` rename is recorded explicitly
  in the nuance to prevent silent semantic drift. Authoritative both
  sides; no escalation.

### rf-dart-iterable-where-to-linq — split/where/join line filter (reuse)

- Deep analysis: `_removeComments` does `split('\n').where(pred).join('\n')`.
  Lazy Dart `where` terminated by eager `.join` materialises a string.
- Authoritative Dart (cached, reused): dart.dev `Iterable.where` is lazy;
  `Iterable.join` is terminal.
- Authoritative .NET (cached): Microsoft Learn `string.Split`,
  `Enumerable.Where`, `string.Join` — `Split` returns `string[]`;
  `Where` is deferred; `string.Join` enumerates eagerly. Method-name
  renames `trimLeft` ⇔ `TrimStart`, `trimRight` ⇔ `TrimEnd` per
  https://learn.microsoft.com/en-us/dotnet/api/system.string.trimstart.
- Conclusion: faithful LINQ rendering; renames recorded; equivalence
  holds because the terminal `string.Join` forces enumeration in both
  languages. Authoritative; no escalation.

### rf-dart-set-literal-to-csharp-hashset — typed empty set for membership

- Deep analysis: `final agentIds = <String>{};` then `contains` / `add`
  in a duplicate-detection loop. No iteration of the set — order is not
  consumed.
- Authoritative Dart: WebFetch `https://api.dart.dev/dart-core/Set-
  class.html` — Dart `Set` literals default to `LinkedHashSet`
  (insertion-ordered); `contains` and `add` are O(1) average; iteration
  order is insertion order.
- Authoritative .NET: WebFetch `https://learn.microsoft.com/en-us/dotnet/
  api/system.collections.generic.hashset-1` — `HashSet<T>` is
  "high-performance set operations" with `Contains` and `Add`; "the order
  in which the items are returned [from enumeration] is undefined."
- Conclusion: `new HashSet<string>()` is the faithful counterpart for
  THIS use site (no iteration). Order-divergence is unobservable here.
  The nuance is recorded so a future code change that iterates the set
  would trigger a re-spec (Dart's `LinkedHashSet` semantic would then
  require .NET `OrderedDictionary` or equivalent). Authoritative both
  sides; no escalation.

### rf-dart-null-aware-chain-to-csharp-conditional-access — group-1 extraction

- Deep analysis: `match?.group(1)?.trim()` — Dart null-aware chain
  returns `String?`, propagates `null` on first null receiver.
  `atMatch.group(1)!` / `functorMatch.group(1)!` — force-unwrap after a
  guaranteed-successful match with a required capture group.
- Authoritative Dart: WebFetch `https://dart.dev/null-safety/understanding-
  null-safety` — `?.` short-circuits the chain to null; `!` asserts
  non-null at runtime, throws on violation. `RegExpMatch.group(int)`
  returns `String?` (nullable per dart-core API).
- Authoritative .NET: WebFetch `https://learn.microsoft.com/en-us/dotnet/
  csharp/language-reference/operators/member-access-operators#null-
  conditional-operators--and-` — `?.` short-circuits to `null` on null
  receiver, same semantic as Dart. WebFetch `https://learn.microsoft.com/
  en-us/dotnet/api/system.text.regularexpressions.match.groups` and
  `https://learn.microsoft.com/en-us/dotnet/api/system.text.
  regularexpressions.group.success` — `Match.Groups[i].Value` returns
  `string` (non-null, EMPTY on unsuccessful group); `Group.Success`
  indicates whether the group matched. Microsoft Learn explicitly notes:
  "If a group did not match, the value of the corresponding `Group`
  object's `Value` property is `String.Empty`."
- Conclusion: the LOAD-BEARING semantic correction — Dart `match.group(1)`
  returns `null` on no-group-match; C# `Match.Groups[1].Value` returns
  empty string. The spec REQUIRES an explicit `if (match.Success) ...
  else return null;` branch where the Dart code returns `String?`,
  rather than a transliterated `match?.Groups[1]?.Value?.Trim()` (which
  would coerce no-match into empty-trimmed-empty). For the `!` force-
  unwrap sites (inside `regex.Matches(...)` iteration where matches are
  guaranteed successful AND the capture group is required by the
  pattern), C# may access `.Groups[1].Value` directly without check.
  Authoritative both sides; no escalation.

### rf-dart-string-replaceFirst-to-csharp-regex-replace-count1 — single-occurrence regex replace

- Deep analysis: `_stripBootClause` issues two `source.replaceFirst(RegExp(
  ...), '')` calls — Dart's `replaceFirst` is single-occurrence.
- Authoritative Dart: WebFetch `https://api.dart.dev/dart-core/String/
  replaceFirst.html` — "Replaces the FIRST occurrence of `from` with
  `replace`" (emphasis: first only).
- Authoritative .NET: WebFetch `https://learn.microsoft.com/en-us/dotnet/
  api/system.text.regularexpressions.regex.replace` — multiple overloads;
  the no-count overload `Regex.Replace(input, replacement)` replaces
  ALL occurrences; the count overload `Regex.Replace(input, replacement,
  count)` (instance method) "replaces a SPECIFIED MAXIMUM NUMBER of
  occurrences." A `count: 1` argument gives single-occurrence semantics.
- Conclusion: the explicit `count: 1` argument is mandatory — omitting
  it would silently change single-replacement to all-replacement and
  could strip multiple boot-clause-like patterns from any file containing
  them. The spec records this as a load-bearing translation. Authoritative
  both sides; no escalation.

### rf-dart-unimplemented-error-to-csharp-notimplemented — platform-stub

- Deep analysis: `_readFile` throws `UnimplementedError` with a message
  pointing callers to use `load(source)` directly. The method exists to
  satisfy the API surface (callable from `loadFile`) without dragging in
  `dart:io` here.
- Authoritative Dart: WebFetch `https://api.dart.dev/dart-core/
  UnimplementedError-class.html` — "Thrown by operations that have not
  been implemented yet." Extends `Error` (programming-defect signal).
- Authoritative .NET: WebFetch `https://learn.microsoft.com/en-us/dotnet/
  api/system.notimplementedexception` — "The exception that is thrown
  when a requested method or operation is not implemented." Microsoft
  Learn's "What's it for?" guidance directly maps this to the
  "intentionally-not-implemented-in-this-layer" intent.
- Conclusion: `throw new NotImplementedException(message)` is the
  faithful counterpart by intent. The .NET Error-vs-Exception hierarchy
  has no analogue of Dart's `Error` superclass split, so the mapping is
  by intent, not by hierarchy — recorded in the nuance. Authoritative
  both sides; no escalation.

### rf-dart-string-index-to-csharp-char-index — char-by-char scanning

- Deep analysis: balanced-paren backwards scan in `_parseSpawnDirectives`
  and `_splitArgs` use single-character indexing and char-literal
  comparison: `beforeAt[i] == ')'`, `argsStr[i] == '('`, `argsStr[i] ==
  '['`, etc. All characters used are ASCII parentheses, brackets,
  comma, and percent — single UTF-16 code units, no surrogate pairs.
- Authoritative Dart: WebFetch `https://api.dart.dev/dart-core/String-
  class.html` — `String[int]` returns a String of length 1 (a substring,
  not a code unit). Single-character literals are also single-element
  Strings.
- Authoritative .NET: WebFetch `https://learn.microsoft.com/en-us/dotnet/
  api/system.string.chars` — `string[int]` returns a `char` (UTF-16 code
  unit). Single-character literals in C# are `char` when written with
  single quotes (`')'`), `string` when written with double quotes
  (`")"`).
- Conclusion: the byte-level source rendering looks identical
  (`beforeAt[i] == ')'`) but the types differ — Dart compares String to
  String, C# compares `char` to `char`. For ASCII characters the
  semantics agree; for supplementary-plane characters the two would
  diverge (Dart `String[i]` returns a half of a surrogate pair as a
  length-1 String; C# `string[i]` returns one half as a `char`). The
  characters used in this file are all ASCII parens/brackets — no
  divergence at THIS code site. Authoritative both sides; no escalation.

### rf-dart-substring-end-vs-csharp-substring-length — substring argument convention

- Deep analysis: five `substring` calls — `clauseBody.substring(0,
  atMatch.start)`, `beforeAt.substring(0, parenStart)`, `beforeAt
  .substring(parenStart + 1, beforeAt.length - 1)`, `argsStr.substring(
  start, i)`, `argsStr.substring(start)`. Four are two-arg (start, end),
  one is single-arg.
- Authoritative Dart: WebFetch `https://api.dart.dev/dart-core/String/
  substring.html` — "The substring of this string from `start`, inclusive,
  to `end`, exclusive." Two-arg = `[start, end)`. Single-arg = `[start,
  length)`.
- Authoritative .NET: WebFetch `https://learn.microsoft.com/en-us/dotnet/
  api/system.string.substring` — `Substring(int startIndex, int length)`
  — "Retrieves a substring from this instance. The substring starts at
  a specified character position and has a specified LENGTH." Two-arg =
  `[startIndex, startIndex + length)`. Single-arg `Substring(int
  startIndex)` = `[startIndex, length)` (rest of string).
- Conclusion: the LOAD-BEARING semantic correction — the second argument
  meaning differs between Dart and .NET. Every two-arg `s.substring(a,
  b)` in Dart MUST be rewritten as `s.Substring(a, b - a)` in C#. The
  single-arg form is faithful verbatim. Documented in the nuance as one
  of the easiest Dart→C# bugs to miss. Authoritative both sides; no
  escalation.

## Notes

- No async / `Future` / `Stream` / `Isolate` / `late` / `sealed` / `mixin` /
  `extension` / channel / IAsyncEnumerable construct in this file — every
  "isolate" mention is a DOC-COMMENT reference to the multi-agent
  boot-spec (this file extracts boot-config data; isolate spawning lives
  in isolate_manager.dart, which is a separate file with its own convspec).
  Those well-known nuances are correctly NOT asserted here because the
  CODE itself does not exercise them. Asserting an absent nuance would be
  noise.
- The load-bearing semantic decisions are: (a) Dart `substring(start, end)`
  vs C# `Substring(startIndex, length)` — every two-arg call must be
  rewritten with `b - a` length; (b) Dart `replaceFirst` vs C# `Regex
  .Replace` — explicit `count: 1` is mandatory; (c) Dart `dotAll` ⇔ .NET
  `Singleline` — flag-name OPPOSITION must be flipped; (d) Dart
  `match.group(1)` returns nullable String, .NET `Match.Groups[1].Value`
  returns empty string on no-match — explicit `match.Success` check
  required where the Dart code returns `String?`; (e) Dart `const []`
  default => single shared `Array.Empty<string>()` via constructor
  coalesce, NOT `new List<string>()` per call (allocation parity); (f)
  BootConfig non-final fields => `{ get; set; }` (NOT `init`) because
  callers reassign post-construction; (g) `trimLeft`/`trimRight` ⇒
  `TrimStart`/`TrimEnd` (mechanical rename, easy to miss).
- Trivial / non-construct elements: file-level and member-level
  triple-slash doc comments (`///`) map mechanically to C# XML-doc
  comments (`///`); `@override` annotations are subsumed by the C#
  `override` keyword on each overriding member; the Dart `var` keyword
  for local variables maps to C# `var` (same type-inference role).
- Zero escalations: every non-trivial construct resolved from
  authoritative Dart (dart.dev / api.dart.dev) and/or .NET (learn.microsoft
  .com) official documentation. No undecidable construct (Dart Isolate
  is NOT exercised here so its lack of a faithful C# counterpart does
  NOT arise — that semantic decision belongs to isolate_manager.dart's
  convspec, not this file).
