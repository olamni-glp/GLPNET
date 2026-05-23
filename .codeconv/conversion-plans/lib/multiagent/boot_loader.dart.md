---
path: lib/multiagent/boot_loader.dart
cycle_group_id: 49
scc_siblings: []
generated_at: 2026-05-21T15:10:00Z
source_sha256: 90d586b75da68e31051e94da4ad8577f0e93aeddcbc7fa2f0ac2ac2629a43a22
schema_version: 1
---

# Conversion Plan: lib/multiagent/boot_loader.dart

## 1. Source Analysis

Source file: `glp_runtime_net/lib/multiagent/boot_loader.dart` (308 lines, sha256 `90d586b7…a43a22`). A self-contained, pure-Dart text-processing module for the maGLP isolate boot loader. No imports (no `dart:io`, no `dart:async`, no `Future`, no `Isolate`, no `Stream`, no `mixin`, no `extension`, no `sealed`, no `late`). All execution is synchronous; all data is in-memory.

Top-level declarations (four):

1. **`class SpawnDirective`** — immutable data carrier for one `goal(agent, ...)@agent` spawn directive.
   - Fields (all `final`, all non-nullable): `String agentId`, `String goalFunctor`, `int goalArity`, `List<String> constantArgs`.
   - Single named-with-required constructor: `agentId`/`goalFunctor`/`goalArity` are `required`; `constantArgs` defaults to `const []` (shared interned empty list).
   - Overrides `toString()` (expression-bodied) for diagnostic logs: `'SpawnDirective($goalFunctor/$goalArity($agentId, ...)@$agentId)'`.
   - No `==` / `hashCode` overrides → reference identity.

2. **`class BootConfig`** — mixed-mutability data carrier for the extracted boot configuration.
   - Three `final` fields: `List<SpawnDirective> directives`, `String fullSource`, `String source` (source with boot clause stripped).
   - Three NON-`final` (mutable) fields: `List<String>? sharedSources`, `String? projectDir`, `String rootSelfGlpPath` (default `''`, callers overwrite).
   - Single named-with-required constructor; required: `directives`, `fullSource`, `source`. Optional with defaults: `sharedSources`/`projectDir` default `null`; `rootSelfGlpPath` defaults `''`.
   - No `toString()` / `==` / `hashCode` overrides → reference identity, default `toString`.

3. **`class BootLoader`** — stateless parser; methods only (no fields):
   - `BootConfig load(String source)` — public entry; orchestrates `_parseBootClause` + `_stripBootClause`.
   - `BootConfig loadFile(String filePath)` — public convenience wrapper; calls `_readFile` then `load`.
   - `List<SpawnDirective> _parseBootClause(String source)` — package-private; runs `_removeComments`, `_hasProcedureBoot`, `_extractBootClause`, `_parseSpawnDirectives`; throws `BootLoaderException` on malformed input; uses a `<String>{}` set to detect duplicate agent IDs.
   - `String _removeComments(String source)` — `split('\n').where(line => !line.trimLeft().startsWith('%')).join('\n')`.
   - `bool _hasProcedureBoot(String source)` — inline `RegExp(r'procedure\s+boot\s*\.', multiLine: true).hasMatch(source)`.
   - `String? _extractBootClause(String source)` — inline `RegExp(r'boot\s*:-\s*(.*?)\.\s*(?=\n|procedure|$)', multiLine: true, dotAll: true).firstMatch(source)`; returns `match?.group(1)?.trim()` (nullable; null when no match).
   - `List<SpawnDirective> _parseSpawnDirectives(String clauseBody)` — iterates `RegExp(r'@\s*(\w+)').allMatches(clauseBody)`; for each `@target`, walks backwards balancing parens to locate the goal, extracts functor via `RegExp(r'(\w+)$').firstMatch(...)`, splits arguments via `_splitArgs`, validates first arg is an atom via `RegExp(r'^\w+$').hasMatch(...)`, validates `goalAgentId == targetAgentId`; appends `SpawnDirective`. Throws `BootLoaderException` on agent-id mismatch / non-atom first arg.
   - `List<String> _splitArgs(String argsStr)` — char-by-char scan tracking paren/bracket depth, splits at depth-0 commas.
   - `String _stripBootClause(String source)` — two `source.replaceFirst(RegExp(..., ...), '')` calls (the `procedure boot.` declaration and the `boot :- … .` clause), then `result.trim() + '\n'`.
   - `String _readFile(String filePath)` — platform stub; throws `UnimplementedError('Use load(source) directly or implement file reading')`.

4. **`class BootLoaderException implements Exception`** — exception class.
   - Field `final String message`; positional constructor `BootLoaderException(this.message)`.
   - Overrides `toString()`: `'BootLoaderException: $message'`.

Cross-cutting Dart constructs exercised (every one is addressed in §2):
- Five `RegExp` constructions; flags used: `multiLine: true`, `dotAll: true`; methods called: `.hasMatch`, `.firstMatch`, `.allMatches`; group access: `.group(1)`, `.group(1)!`, `.group(1)?.trim()`.
- Five `String.substring(...)` calls (four two-arg, one single-arg).
- Two `String.replaceFirst(RegExp, String)` calls.
- One `Iterable.where(...)` pipeline (`split` → `where` → `join`).
- One typed empty set literal (`<String>{}`) used only for `contains` + `add`.
- Null-aware chains (`?.`), force-unwrap (`!`), null-aware return.
- Single-character indexing (`s[i]`) and char-literal comparison (ASCII parens/brackets only).
- `UnimplementedError` from a platform-stub method.

Singleton in cycle_group 49 (no SCC siblings → no §7).

## 2. Dart → C#/.NET Conversion Plan

This section mirrors the ratified convspec verbatim. Every construct decision below is sourced from the convspec rationale block (load-bearing decisions and all "nuance" entries are preserved).

### 2.1 `class SpawnDirective` → C# reference `class SpawnDirective` (NOT record, NOT struct)
- Construct: `dart.data_class.final_fields_named_required_ctor_with_default_const_list`
- Emit a reference `class SpawnDirective` with four get-only auto-properties initialised from a single constructor. The four `final` fields → four `{ get; }` auto-properties:
  - `String agentId` → `string AgentId { get; }`
  - `String goalFunctor` → `string GoalFunctor { get; }`
  - `int goalArity` → `long GoalArity { get; }`
  - `List<String> constantArgs` → `IReadOnlyList<string> ConstantArgs { get; }` (read-only view; never mutated post-construction).
- Constructor: `SpawnDirective(string agentId, string goalFunctor, long goalArity, IReadOnlyList<string>? constantArgs = null)` with body assigning `ConstantArgs = constantArgs ?? Array.Empty<string>()` → preserves Dart `const []`'s shared-interned-empty-list semantic (single shared zero-length array per `System.Array.Empty<T>()`).
- `record` REJECTED: (a) downstream callers (isolate_manager.dart) alias instances by reference across the spawn pipeline; (b) synthesised record `ToString` would deviate from the explicit Dart override below, which is load-bearing for diagnostics.
- `struct` REJECTED: aliasing would force per-pass defensive copies.

### 2.2 `SpawnDirective.toString()` override → `public override string ToString()`
- Construct: `dart.tostring_override.string_interpolation_no_branch_with_slash_arity`
- Emit `public override string ToString() => $"SpawnDirective({GoalFunctor}/{GoalArity}({AgentId}, ...)@{AgentId})";`
- Expression-bodied (mirrors Dart `=>`). All four fields non-nullable → no null check (single-branch). Punctuation preserved byte-identically (load-bearing for log/test assertions). Dart `$id` → C# `{Id}`; `int.ToString()` invariant culture matches Dart `int.toString()` decimal text.
- Extension-method alternative REJECTED — extensions cannot override a virtual.

### 2.3 `class BootConfig` → C# reference `class BootConfig` (NOT record, NOT struct)
- Construct: `dart.data_class.final_and_mutable_fields_named_required_ctor_with_defaults`
- Three `final` fields → three `{ get; }` get-only properties:
  - `List<SpawnDirective> directives` → `IReadOnlyList<SpawnDirective> Directives { get; }`
  - `String fullSource` → `string FullSource { get; }`
  - `String source` → `string Source { get; }`
- Three NON-`final` fields → three `{ get; set; }` read-write properties (NOT `init` — load-bearing; callers reassign post-construction):
  - `List<String>? sharedSources` → `List<string>? SharedSources { get; set; }`
  - `String? projectDir` → `string? ProjectDir { get; set; }`
  - `String rootSelfGlpPath` (non-nullable, default `''`) → `string RootSelfGlpPath { get; set; }` (default `""`)
- Constructor: `BootConfig(IReadOnlyList<SpawnDirective> directives, string fullSource, string source, List<string>? sharedSources = null, string? projectDir = null, string rootSelfGlpPath = "")` mirroring the Dart named-required-and-defaults shape.
- `record` REJECTED for three reasons (load-bearing): (a) record auto-properties default to `init` (settable only at construction-time / `with`-expression), but Dart NON-final fields ARE reassigned post-construction by callers (the `''` default for `rootSelfGlpPath` is a placeholder the caller overwrites); (b) record value-equality would deviate from Dart class equality (reference identity — no `==`/`hashCode` override); (c) isolate_manager.dart aliases the same BootConfig across spawns and reads back mutated fields.
- Default-value renderings: `''` → `""` (both interned/pooled empty strings — no allocation per default).

### 2.4 `class BootLoaderException implements Exception` → `class BootLoaderException : Exception`
- Construct: `dart.exception_class.implements_Exception_with_message_field`
- Emit `public class BootLoaderException : Exception { public BootLoaderException(string message) : base(message) {} public override string ToString() => $"BootLoaderException: {Message}"; }`.
- The Dart `message` field is routed to `Exception.Message` via `: base(message)` ctor chaining — NO duplicate `Message` property declaration. Public read access is via inherited `Message`.
- `IException` interface NOT introduced — .NET has no exception interface; every throwable derives from `System.Exception`. Dart `implements Exception` (conformance) → C# `: Exception` (inheritance) is the faithful and authoritative mapping.
- `ToString()` override REPLACES the default `Exception.ToString()` (which would include the stack trace) with the exact Dart-source single-line prefix `BootLoaderException: <message>`.
- Naming-suffix policy (`...Exception`) — source already complies with Microsoft naming guidelines; no rename.

### 2.5 RegExp constructions (five, inline) → `System.Text.RegularExpressions.Regex`
- Construct: `dart.regexp.regexp_with_multiline_dotall_options_and_named_apis`
- Raw-string patterns `r'...'` → verbatim strings `@"..."` (byte-identical preservation of `\s`, `\w`, `\.`, anchors, `(?=...)`, `*?`, `(...)`).
- Flag mapping (LOAD-BEARING — `dotAll` ⇔ `Singleline` is the opposite name):
  - Dart `multiLine: true` → `RegexOptions.Multiline`
  - Dart `dotAll: true` → `RegexOptions.Singleline`
- Inline constructions one-to-one with the Dart source (review fidelity); promotion to `static readonly Regex` or `[GeneratedRegex]` source-generated regex is a later optimisation, NOT spec-default.
- Method mapping:
  - Dart `.hasMatch(s)` → `regex.IsMatch(s)`
  - Dart `.firstMatch(s)` → `regex.Match(s)` (returns `Match`)
  - Dart `.allMatches(s)` → `regex.Matches(s)` (returns `MatchCollection`)
- Specific renderings:
  - `RegExp(r'procedure\s+boot\s*\.', multiLine: true)` → `new Regex(@"procedure\s+boot\s*\.", RegexOptions.Multiline)`
  - `RegExp(r'boot\s*:-\s*(.*?)\.\s*(?=\n|procedure|$)', multiLine: true, dotAll: true)` → `new Regex(@"boot\s*:-\s*(.*?)\.\s*(?=\n|procedure|$)", RegexOptions.Multiline | RegexOptions.Singleline)`
  - `RegExp(r'@\s*(\w+)')` → `new Regex(@"@\s*(\w+)")`
  - `RegExp(r'(\w+)$')` → `new Regex(@"(\w+)$")`
  - `RegExp(r'^\w+$')` → `new Regex(@"^\w+$")`

### 2.6 `_removeComments` `split/where/join` → LINQ pipeline
- Construct: `dart.iterable.split_where_join_for_line_filter`
- Emit `string.Join("\n", source.Split('\n').Where(line => !line.TrimStart().StartsWith("%")))`.
- Renames (LOAD-BEARING — mechanical but easy to miss): `trimLeft` → `TrimStart`; `trimRight` → `TrimEnd`.
- Lazy-vs-eager equivalence holds — `Where` deferred in both; `string.Join` forces enumeration (matches Dart `.join` terminal).
- Newline `'\n'` byte-identical (single LF, U+000A) — DO NOT substitute `Environment.NewLine`; the GLP textual format hard-codes LF.

### 2.7 Duplicate-agent-ID set → `HashSet<string>`
- Construct: `dart.collection.set_literal_typed_contains_add`
- Emit `var agentIds = new HashSet<string>();` then `if (agentIds.Contains(d.AgentId)) throw new BootLoaderException(...); agentIds.Add(d.AgentId);`.
- Order divergence (Dart `LinkedHashSet` insertion-ordered vs C# `HashSet` unordered) is UNOBSERVABLE here — set is read only via `Contains` (no iteration). Recorded so a future change introducing iteration triggers re-spec.
- The "check-then-add" sequence is preserved (NOT collapsed to `if (!Add(x)) throw`) — preserves Dart shape and the exact order of operations for review fidelity.

### 2.8 Null-aware chain `match?.group(1)?.trim()` → explicit `Success` branch
- Construct: `dart.null_aware.optional_member_chain_with_force_unwrap`
- LOAD-BEARING semantic correction: Dart `match.group(1)` returns `String?` (null on no-group-match); C# `Match.Groups[1].Value` returns `string` (empty on no-group-match, NEVER null). A transliterated `match?.Groups[1]?.Value?.Trim()` would silently coerce no-match to empty-trimmed-empty.
- Emit explicit branch:
  ```
  var match = pattern.Match(source);
  if (!match.Success) return null;
  return match.Groups[1].Value.Trim();
  ```
  (Inside `_extractBootClause` which returns `string?`.)
- Force-unwrap sites (`atMatch.group(1)!`, `functorMatch.group(1)!`) — inside `regex.Matches(...)` enumeration (only successful matches yielded) AND the capture group is required by the pattern (no `?` quantifier). Emit direct `.Groups[1].Value` access without `Success` check or `!`.
- `?.` semantics agree between Dart and C# at the operator level — short-circuit chain to null on first null receiver.

### 2.9 `_stripBootClause` `replaceFirst` → `Regex.Replace(input, repl, count: 1)`
- Construct: `dart.string.replaceFirst_with_regex_and_replacement`
- LOAD-BEARING — `count: 1` is mandatory; the no-count overload `Regex.Replace(input, repl)` replaces ALL occurrences and would silently strip multiple boot-clause-like patterns.
- Emit instance-method form keeping the regex construction inline (mirrors Dart):
  ```
  var result = new Regex(@"procedure\s+boot\s*\.\s*\n?", RegexOptions.Multiline).Replace(source, "", 1);
  result = new Regex(@"boot\s*:-\s*.*?\.\s*\n?", RegexOptions.Multiline | RegexOptions.Singleline).Replace(result, "", 1);
  return result.Trim() + "\n";
  ```
- Final `.trim() + '\n'` → `.Trim() + "\n"` (Dart `String.trim` and .NET `Trim()` both trim both ends with default whitespace — equivalent).

### 2.10 `UnimplementedError` from platform stub → `NotImplementedException`
- Construct: `dart.unimplemented.platform_stub_throws_unimplemented_error`
- Emit `private string ReadFile(string filePath) { throw new NotImplementedException("Use Load(source) directly or implement file reading"); }`.
- Mapping is by INTENT (both signal "intentionally not implemented in this layer"), not by hierarchy — .NET has no `Error` vs `Exception` split.
- Dart `_name` library-private → C# `private` is faithful here because `_readFile` is called only from `loadFile` inside the same class. (Cross-file underscore methods would map to `internal`; not applicable here.)

### 2.11 String indexing `s[i]` and char-literal comparison
- Construct: `dart.string.index_access_character_codeunits`
- Source rendering is byte-identical (`beforeAt[i] == ')'`) but underlying types differ: Dart `String[i]` returns a length-1 `String`; C# `string[i]` returns a `char` (UTF-16 code unit).
- For the ASCII parens / brackets / comma / percent used in this file, comparison semantics agree exactly. Future code touching supplementary-plane characters would need explicit handling — recorded but does NOT arise here.
- Length getter: Dart `.length` (lower-case) → C# `.Length` (upper-case Property).

### 2.12 `String.substring(start, end)` → `string.Substring(startIndex, length)`
- Construct: `dart.string.substring_pair_start_end_argument`
- LOAD-BEARING semantic correction (one of the easiest Dart→C# bugs to miss): Dart two-arg `substring(start, end)` is END-exclusive; C# two-arg `Substring(startIndex, length)` is LENGTH-based. EVERY two-arg call MUST be rewritten as `s.Substring(a, b - a)`.
- Specific renderings (four two-arg + one single-arg):
  - `clauseBody.substring(0, atMatch.start)` → `clauseBody.Substring(0, atMatch.Index - 0)` (i.e. `clauseBody.Substring(0, atMatch.Index)`)
  - `beforeAt.substring(0, parenStart)` → `beforeAt.Substring(0, parenStart)` (a == 0, so b - 0 == b — same value, but the rewrite rule still applied)
  - `beforeAt.substring(parenStart + 1, beforeAt.length - 1)` → `beforeAt.Substring(parenStart + 1, (beforeAt.Length - 1) - (parenStart + 1))`
  - `argsStr.substring(start, i)` → `argsStr.Substring(start, i - start)`
  - `argsStr.substring(start)` → `argsStr.Substring(start)` (single-arg form is faithful verbatim)
- `RegExpMatch.start` → `Match.Index` (the matched-position-from-start property in .NET).
- `.trimRight()` chained → `.TrimEnd()` (mechanical rename — already covered in §2.6 nuance).

### 2.13 Trivial / non-construct items
- Triple-slash doc comments (`/// ...`) → C# XML-doc comments (`/// ...`) — mechanical, byte-identical comment preservation.
- `@override` annotations are subsumed by the C# `override` keyword on each overriding member; no separate render.
- `var` local-variable inference → C# `var` (same role).
- Dart `final` locals (e.g. `final agentIds`, `final beforeAt`, etc.) → C# `var` (the `final` reference-immutability is enforced by code-review convention in C#, not compile-time; recorded in convspec §2.7 nuance).
- File-level doc comment (lines 1-6) → C# XML-doc on the first emitted top-level class (or file-scoped block comment if the codegen pass prefers).

### 2.14 Constructs explicitly NOT exercised in this file (recorded so they raise no nuance)
- No `async` / `await` / `Future` / `Stream` / `Isolate` / `Completer` / channel / `IAsyncEnumerable` / `late` / `sealed` / `mixin` / `extension`. Every "isolate" word in this file is a DOC-COMMENT reference to the boot spec; isolate spawning lives in `isolate_manager.dart`, a separate convspec.

## 3. Decomposed Task Units

- **T1**: Emit C# reference `class SpawnDirective` with four get-only properties (`AgentId`, `GoalFunctor`, `GoalArity` as `long`, `ConstantArgs` as `IReadOnlyList<string>`) and single constructor with `constantArgs ?? Array.Empty<string>()` coalesce. **Done when**: class compiles with NRT enabled, all four properties are `{ get; }`, constructor signature matches the convspec render, and `ConstantArgs` is `Array.Empty<string>()` when caller passes `null`.
- **T2**: Emit `SpawnDirective.ToString()` override as expression-bodied `$"SpawnDirective({GoalFunctor}/{GoalArity}({AgentId}, ...)@{AgentId})"`. **Done when**: invoking `ToString()` on a representative instance returns the byte-identical Dart-source format.
- **T3**: Emit C# reference `class BootConfig` with three get-only properties (`Directives` as `IReadOnlyList<SpawnDirective>`, `FullSource`, `Source`) and three get/set properties (`SharedSources` as `List<string>?`, `ProjectDir` as `string?`, `RootSelfGlpPath` defaulting to `""`), plus single constructor. **Done when**: class compiles, the three non-final properties are `{ get; set; }` (NOT `init`), default values match convspec, and post-construction reassignment of `SharedSources`/`ProjectDir`/`RootSelfGlpPath` compiles and works.
- **T4**: Emit `class BootLoaderException : Exception` with `BootLoaderException(string message) : base(message)` ctor and `ToString()` override `$"BootLoaderException: {Message}"`. **Done when**: throwing the exception and calling `ToString()` returns the exact `BootLoaderException: <message>` shape (no stack trace), and `Message` is inherited (NOT redeclared).
- **T5**: Emit `class BootLoader` shell with `public BootConfig Load(string source)` and `public BootConfig LoadFile(string filePath)` orchestrating the private helpers. **Done when**: `Load` calls `_parseBootClause` + `_stripBootClause`, builds `BootConfig(directives, source, compilableSource)`, and `LoadFile` calls `_readFile` then `Load`.
- **T6**: Emit `private string _removeComments(string source)` using `string.Join("\n", source.Split('\n').Where(line => !line.TrimStart().StartsWith("%")))`. **Done when**: representative input with `%`-prefixed lines returns the file with those lines removed.
- **T7**: Emit `private bool _hasProcedureBoot(string source)` using `new Regex(@"procedure\s+boot\s*\.", RegexOptions.Multiline).IsMatch(source)`. **Done when**: returns `true` for `procedure boot.` (any whitespace), `false` otherwise.
- **T8**: Emit `private string? _extractBootClause(string source)` using `new Regex(@"boot\s*:-\s*(.*?)\.\s*(?=\n|procedure|$)", RegexOptions.Multiline | RegexOptions.Singleline).Match(source)`, returning `null` if `!match.Success` else `match.Groups[1].Value.Trim()`. **Done when**: missing clause → `null`; present clause → trimmed body string.
- **T9**: Emit `private IReadOnlyList<SpawnDirective> _parseSpawnDirectives(string clauseBody)` — iterates `new Regex(@"@\s*(\w+)").Matches(clauseBody)`, walks backwards balancing parens, extracts functor via `new Regex(@"(\w+)$").Match(...)`, calls `_splitArgs`, validates first arg via `new Regex(@"^\w+$").IsMatch(...)`, validates `goalAgentId == targetAgentId`, appends `SpawnDirective`. Uses `string.Substring(a, b - a)` for every two-arg substring (LOAD-BEARING). Throws `BootLoaderException` on mismatch / non-atom. **Done when**: representative `parent_init(alice, carol, 4, _)@alice` yields `SpawnDirective` with `agentId="alice"`, `goalFunctor="parent_init"`, `goalArity=4`, `constantArgs=["carol", "4"]`; mismatched goal/target throws.
- **T10**: Emit `private IReadOnlyList<string> _splitArgs(string argsStr)` — char-by-char scan, depth tracked at `(`/`)` and `[`/`]`, splits at depth-0 commas. **Done when**: `"a, b(c, d), [e, f], g"` returns `["a", " b(c, d)", " [e, f]", " g"]`.
- **T11**: Emit `private string _stripBootClause(string source)` — two `new Regex(pattern, options).Replace(input, "", 1)` calls (count argument is LOAD-BEARING) + `.Trim() + "\n"`. **Done when**: `procedure boot.` declaration and `boot :- … .` clause both removed from output, single trailing `\n` preserved.
- **T12**: Emit `private string _readFile(string filePath)` throwing `new NotImplementedException("Use Load(source) directly or implement file reading")`. **Done when**: invocation throws `NotImplementedException` with the exact message.
- **T13**: Emit `private IReadOnlyList<SpawnDirective> _parseBootClause(string source)` — orchestrates `_removeComments`, `_hasProcedureBoot` (throws `BootLoaderException` on miss), `_extractBootClause` (throws on null), `_parseSpawnDirectives` (throws on empty), then duplicate-ID check via `var agentIds = new HashSet<string>(); if (agentIds.Contains(d.AgentId)) throw ...; agentIds.Add(d.AgentId);`. **Done when**: malformed input throws `BootLoaderException` with the right message; duplicate agent IDs throw.
- **T14**: Preserve all `///` doc comments verbatim on the corresponding C# members. **Done when**: every Dart `///` block has a mechanical XML-doc counterpart on the C# member.

(Concurrency / scheduling / heap-FCP / channel constructs do NOT appear in this file — no tasks for them.)

## 4. Research Findings

None required.

All ten constructs in the convspec are resolved against authoritative Dart docs (dart.dev, api.dart.dev) and authoritative .NET docs (Microsoft Learn `learn.microsoft.com`) per the convspec's "Rationale and research provenance" section. The convspec records zero open research findings; every research_finding_id in the constructs block is grounded by cached/verified WebFetch results documented in the convspec rationale block. This file's constructs are entirely within the scope of those findings — no new research needed for the plan.

## 5. Consistency Pass

Cross-checks performed between §2 (construct decisions), §3 (decomposed tasks), §4 (research), the ratified convspec, and CLAUDE.md / project conventions:

- **§2.1/§2.3 vs §3 T1/T3** — both target reference `class` (not record/struct); both list the exact property accessors (get-only for Dart `final` fields, get/set for Dart non-final). Consistent — derived from convspec `dart.data_class.*` constructs.
- **§2.2 vs §3 T2** — `ToString` is expression-bodied, byte-identical interpolation string. Consistent — derived from convspec `dart.tostring_override.*` construct.
- **§2.4 vs §3 T4** — `: Exception` (NOT `IException`), `Message` inherited via `: base(message)` (NOT redeclared), `ToString` override replaces base format. Consistent — derived from convspec `dart.exception_class.*` construct + nuance.
- **§2.5 vs §3 T7/T8/T9/T11** — every `RegExp` rendered as inline `new Regex(@"...", RegexOptions.*)` with the `dotAll`⇔`Singleline` flag flip (load-bearing) and the count-1 form for `replaceFirst` (also load-bearing). Consistent — derived from convspec `dart.regexp.*` and `dart.string.replaceFirst_*` constructs.
- **§2.6 vs §3 T6** — `Split('\n')` + LINQ `Where` + `string.Join("\n", ...)` with `TrimStart`/`StartsWith("%")`. Consistent — derived from convspec `dart.iterable.split_where_join_*` construct.
- **§2.7 vs §3 T13** — `HashSet<string>` with `Contains`-then-`Add` order preserved (NOT collapsed to `!Add(x)`). Consistent — derived from convspec `dart.collection.set_literal_*` construct + nuance.
- **§2.8 vs §3 T8/T9** — explicit `Success` branch in `_extractBootClause` returning `string?`; direct `.Groups[1].Value` (no `?` / no `!`) at `Matches`-loop sites where the group is required by the pattern. Consistent — derived from convspec `dart.null_aware.*` construct (LOAD-BEARING semantic correction).
- **§2.9 vs §3 T11** — `count: 1` argument explicit at both `Replace` call sites. Consistent — derived from convspec `dart.string.replaceFirst_*` construct (LOAD-BEARING).
- **§2.10 vs §3 T12** — `NotImplementedException` with the exact message string. Consistent — derived from convspec `dart.unimplemented.*` construct.
- **§2.11 vs §3 T9/T10** — char-literal comparison renders identically at the source level; ASCII-only at these sites; convspec nuance about supplementary-plane chars recorded but not actionable here. Consistent — derived from convspec `dart.string.index_access_*` construct.
- **§2.12 vs §3 T9/T10** — every two-arg `substring` rewritten as `Substring(a, b - a)`; single-arg form kept verbatim; `Match.start` → `Match.Index`. Consistent — derived from convspec `dart.string.substring_*` construct (LOAD-BEARING — listed at every call site).
- **§4 vs §2 + convspec** — `None.` is consistent with the convspec's `escalations: []` and zero open research findings; every construct's `research_finding_id` is grounded by cached/verified authoritative-doc citations in the convspec rationale block. No external research deferred.
- **§2/§3 vs CLAUDE.md DISCIPLINE.md spec-first rule** — every emitted decision is verbatim-derivable from the ratified convspec (which itself cites authoritative docs); no inferred behaviour added.
- **§2/§3 vs project convention on `Error` naming-suffix** — convspec records that the `BootLoaderException` suffix already matches Microsoft naming guidelines; consistent with the project-wide policy (memorialised in prior CompileError discussion 2026-05-20) that Dart `*Error`/`*Exception` types retain their source names. No tension at this file.
- **§2/§3 vs cycle_group_id 49 singleton** — singleton (no SCC siblings); no §7 emitted; consistent with the artefact-structure rule.

No gaps found. No items escalated.

## 6. Escalations

None.
