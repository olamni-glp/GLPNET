---
path: lib/compiler/error.dart
cycle_group_id: 3
scc_siblings: []
generated_at: 2026-05-21T14:25:00Z
source_sha256: 48c26f84e7f527b0ac9d6ecebc266bfea0cb49964bf50a7f1dbcfe2f424070a4
schema_version: 1
---

# Conversion Plan: lib/compiler/error.dart

## 1. Source Analysis

`lib/compiler/error.dart` is a 50-line, self-contained, dependency-free leaf module (tombstone `dependencies: []`, `topo_level: 0`, `cycle_group_id: 3`). It exposes two public types:

- **`enum ErrorCategory`** — a *plain* (non-enhanced) Dart enum with four mutually-exclusive named constants (`lexical`, `syntax`, `semantic`, `codegen`), one inline `//` comment per member describing the phase. No fields, no constructor, no methods, no `values`-list consumption in this file.
- **`class CompileError implements Exception`** — an immutable error-data container with five `final` fields:
  - `final String message`
  - `final int line`
  - `final int column`
  - `final String? source` (nullable, holds full source text for the caret-rendering branch)
  - `final ErrorCategory? category` (nullable, derived in the initializer list)
  - one hybrid constructor: three positional initialising-formals (`this.message`, `this.line`, `this.column`) + two named optionals — one is also an initialising-formal (`{this.source}`) and the other is a plain named parameter (`String? phase`) consumed by the `: category = phase != null ? _categoryFromPhase(phase) : null` initializer-list expression. Note that `phase` is never stored — only its derived `category` is kept.
  - one `static` library-private helper: `static ErrorCategory? _categoryFromPhase(String phase)` — a 4-case `switch` over phase tokens (`'lexer'` → `lexical`, `'parser'` → `syntax`, `'analyzer'` → `semantic`, `'codegen'` → `codegen`, `default` → `null`).
  - one `@override String toString()` — the load-bearing diagnostic surface: conditionally formats a bracketed-category prefix (`'[${category.toString().split('.').last}] '`), the `Line L, Column C` location, and (when `source != null` AND `line > 0` AND `line <= lines.length`) the offending source line plus a `^`-pointer-caret built via `' ' * (column - 1) + '^'`; otherwise falls back to `'$categoryName$message at $loc'`.

Public surface (per tombstone `callers:`): consumed by 10 sites across `lib/analysis/type_checker/{clause_validation,type_checker}.dart`, `lib/compiler/{analyzer,codegen,compiler,lexer,parser,partial_evaluator}.dart`, and three test files. Every consumer pattern is `throw CompileError(...)` and/or pattern-matching/`catch` on the type.

What is **absent**: no `Future`/`Stream`/`async`, no `await`, no isolate primitives, no `late`, no `mixin`, no `extension`, no generics (parameter or bound), no `sealed`/`abstract`, no codegen / build-runner annotation, no bitwise/shift, no factory constructors, no `noSuchMethod`, no operator overloads. Comment shapes are: two `///` triple-slash doc-comments (one each on the enum and the class) and four `//` inline line-comments on the enum members.

## 2. Dart → C#/.NET Conversion Plan

Conversion decisions below mirror the ratified per-construct decisions in `.codeconv/conversion-specs/lib/compiler/error.dart.md` (convspec) verbatim. Where the convspec records nuance, the plan adopts the convspec default and surfaces no new design decision.

1. **`enum ErrorCategory { lexical, syntax, semantic, codegen }` → C# `enum ErrorCategory { Lexical, Syntax, Semantic, Codegen }`**
   - Default `int` underlying type; default ordinal values `0..3`; no `[Flags]` (mutually exclusive phases, no bitwise combination). Member names PascalCased per .NET naming. Implicit-zero (`(ErrorCategory)0 == Lexical`) acceptable because `lexical` is genuinely the first/default category in source order, matching Dart's enum-index-0 convention.
   - **Rationale**: per convspec construct `dart.enum.plain_named_constants` and `rf-dart-plain-enum-to-csharp-enum`. C# enums are value types (Microsoft Learn); Dart enums are heap-reference. Boxing only on `object?` storage, which never occurs in this file.

2. **`final ErrorCategory? category` field → C# `public ErrorCategory? Category { get; }` (i.e. `Nullable<ErrorCategory>`)**
   - Get-only auto-property; assigned once in constructor body. Faithful nullable mapping over the value-type enum — NOT a sentinel `None` member (rejected: would conflate "no category" with a real category and would trigger the implicit-zero hazard).
   - **Rationale**: per convspec construct `dart.nullable_enum_field.QuestionMark` and `rf-dart-nullable-enum-to-csharp-nullable-of-enum`.

3. **`class CompileError implements Exception` → C# `public class CompileError : Exception`**
   - Deriving from `System.Exception` (not an "IException" interface — no such .NET idiom; throwable contract is concrete inheritance per Microsoft Learn). **Name retained verbatim as `CompileError`** — see §5 for the project-wide policy (escalation #1 closed in commit `e3abe921`).
   - Dart `message` field routed to base `Exception.Message` via `: base(message)` so `Message`, default `ToString()`, and serialization behave idiomatically when the override is bypassed.
   - `line`, `column`, `source`, `category` become get-only properties on the derived class (Dart `final` semantics preserved). Inner-exception ctor pattern NOT emitted (Dart source has no cause-chaining; spec-faithfulness, FR-013).
   - **Rationale**: per convspec construct `dart.exception_class.implements_Exception_with_message_and_location` and `rf-dart-implements-exception-to-csharp-derive-system-exception`.

4. **Hybrid constructor `CompileError(this.message, this.line, this.column, {this.source, String? phase})` → C# single constructor with optional defaults**
   - Signature: `public CompileError(string message, long line, long column, string? source = null, string? phase = null) : base(message)`.
   - Body assigns `Line = line; Column = column; Source = source; Category = phase != null ? CategoryFromPhase(phase) : null;` — initialising-formal sugar expanded explicitly (C# has no `this.x` parameter sugar).
   - `phase` deliberately not stored (matches Dart source: only its derived `category` is kept).
   - C# call-site `new CompileError("msg", 1, 2, phase: "lexer")` mirrors Dart's named-argument call via C# named-argument syntax.
   - `line` and `column` map to **`long`** (per recurring `rf-dart-int-to-csharp-long-width` idiom, ratified in opcodes.dart spec). Dart's `line == 0` "no-render" sentinel branch preserved unchanged under `long`.
   - **Rationale**: per convspec construct `dart.named_optional_param_initialising_formal_plus_extra_named` and `rf-dart-named-default-param-to-csharp-optional-arg`.

5. **`static ErrorCategory? _categoryFromPhase(String phase)` → C# `private static ErrorCategory? CategoryFromPhase(string phase)`**
   - Body: switch-expression form is the .NET-idiomatic default:
     `phase switch { "lexer" => ErrorCategory.Lexical, "parser" => ErrorCategory.Syntax, "analyzer" => ErrorCategory.Semantic, "codegen" => ErrorCategory.Codegen, _ => null }`. Classic switch statement permitted as alternative for literal source-shape.
   - Privacy narrowing Dart library-private → C# class-private is strictly correct in this single-class context (helper has no cross-class consumers in this file). If a future converted file adds a cross-class call, escalate at that point.
   - **Rationale**: per convspec construct `dart.static_private_helper.switch_expression_to_nullable_enum` and `rf-dart-leading-underscore-privacy-to-csharp-private`.

6. **`@override String toString()` → C# `public override string ToString()`**
   - Overrides `System.Object.ToString` (the same virtual `Exception` itself overrides). Body preserved branch-for-branch:
     1. `var categoryName = Category != null ? $"[{Category}] " : "";` — uses C# enum default `ToString()` (member identifier as declared). Recorded case-delta: Dart prints `[lexical]`; C# prints `[Lexical]` — intentional .NET-idiomatic rendering. Optional `.ToString().ToLowerInvariant()` if byte-identical lowercase is later required (codegen's call; spec default = PascalCase).
     2. `var loc = $"Line {Line}, Column {Column}";`
     3. If `Source != null`: `var lines = Source.Split('\n');` (yielding `string[]`; consumer indexes identically: `lines[line - 1]`). NRT null-flow narrowing replaces Dart's `Source!` bang — no `!` operator needed inside the `if (Source != null)` branch.
     4. `var pointer = new string(' ', (int)(Column - 1)) + "^";` — Dart's `String * int` repetition (api.dart.dev `String.operator_multiply`) maps to .NET's `String(Char, Int32)` constructor (Microsoft Learn). Narrowing cast `long → int` for the ctor signature is checked-safe (column indices are bounded by real text columns; no overflow path). Fault semantics preserved: both throw on negative count (Dart `RangeError` ⇔ .NET `ArgumentOutOfRangeException`); no defensive guard synthesised (FR-013 / CLAUDE.md "robustness is often a workaround in disguise").
     5. Return interpolated strings via C# `$"..."`; `\n` is identical (single LF code unit) in both languages.
   - **Does NOT call `base.ToString()`** — intentional, mirrors Dart. `Exception.ToString` default would prepend type name + tail stack-trace (Microsoft Learn), which would diverge from the Dart developer-facing diagnostic shape. A caller wanting the stack trace must inspect `StackTrace` separately — same posture as Dart.
   - **Rationale**: per convspec construct `dart.override_tostring_with_branching_interpolation_and_string_repetition` and `rf-dart-tostring-interp-to-csharp-tostring-interp`.

7. **Doc-comments and inline comments** (trivial)
   - Two `///` triple-slash doc-comments → C# XML-doc `/// <summary>...</summary>` on the enum and the class.
   - Four `//` inline comments on enum members → preserved verbatim as C# `//` line comments adjacent to each enum member declaration (byte-identical documentation shape; XML-doc per-member is permitted as alternative).
   - **Rationale**: per convspec constructs `dart.docblock_triple_slash` and `dart.line_comment.inline_after_enum_member` (both `trivial: true`).

8. **Namespace and file layout**
   - Target file `lib/compiler/error.cs` (per tombstone `target_path`). Namespace: the project-wide compiler namespace (`Glp.Compiler` or whatever the scaffold step established for `lib/compiler/`) — codegen's call, no design decision here.

9. **CA1710 suppression (project policy from commit `e3abe921`)**
   - CA1710 ("Exception identifiers should end in 'Exception'") is OFF by default in .NET 10. Codegen MUST emit `.editorconfig` line `dotnet_code_quality.CA1710.severity = none` (project-wide, not per-file) to suppress the opt-in lint and keep `CompileError` (plus every sibling `*Error` type) at its source name.

## 3. Decomposed Task Units

- **T1**: Create `lib/compiler/error.cs` with namespace scaffold + XML-doc and the `enum ErrorCategory { Lexical, Syntax, Semantic, Codegen }` declaration including the four `//` per-member comments. **DoD**: file compiles standalone (no other refs), `dotnet build` passes for this single file in isolation.

- **T2**: Add the `public class CompileError : Exception` skeleton with the five get-only properties (`Line: long`, `Column: long`, `Source: string?`, `Category: ErrorCategory?`) and XML-doc on the class. **DoD**: properties resolve under NRT-enabled compile; `Message` is inherited from `Exception` (no shadow declaration).

- **T3**: Add the single constructor `CompileError(string message, long line, long column, string? source = null, string? phase = null) : base(message)` with explicit body assignments for `Line`, `Column`, `Source`, and the `Category = phase != null ? CategoryFromPhase(phase) : null` computation. **DoD**: `new CompileError("m", 1, 2)`, `new CompileError("m", 1, 2, source: "src")`, and `new CompileError("m", 1, 2, phase: "lexer")` all compile and a smoke test confirms `Category == ErrorCategory.Lexical` for the third form.

- **T4**: Add `private static ErrorCategory? CategoryFromPhase(string phase)` using the switch-expression form mapping `"lexer"`/`"parser"`/`"analyzer"`/`"codegen"` → `Lexical`/`Syntax`/`Semantic`/`Codegen`, `_` → `null`. **DoD**: a smoke test confirms all four hit cases plus a fall-through `null` for an unknown phase string.

- **T5**: Override `public override string ToString()` reproducing the Dart branch shape: bracketed-category prefix, `Line L, Column C`, and the conditional caret-rendering branch (`Source != null` + line-bounds check) using `Source.Split('\n')`, `new string(' ', (int)(Column - 1)) + "^"`, and `$"..."` interpolations. Do NOT call `base.ToString()`. **DoD**: smoke tests assert the four output shapes — (a) no category + no source, (b) category + no source, (c) source + in-range line, (d) source + out-of-range line — match the Dart reference byte-for-byte modulo the documented `[Lexical]`/`[lexical]` case-delta.

- **T6**: Emit / update `.editorconfig` line `dotnet_code_quality.CA1710.severity = none` (idempotent — only add if absent). **DoD**: `dotnet build` does not raise CA1710 against `CompileError`.

## 4. Research Findings

none required

## 5. Consistency Pass

Cross-checking §2 vs §3 vs the convspec, the tombstone, and project conventions (CLAUDE.md, commit `e3abe921`):

- **Exception naming suffix**: §2.3 keeps the name `CompileError` (no `*Exception` suffix). Convspec records this as the spec default and notes a previously-open escalation. Project-wide policy locked in by commit `e3abe921` (2026-05-20, Gabi): "all Dart `*Error` exception types retain their source names; CA1710 is OFF by default in .NET 10; codegen MUST emit `.editorconfig dotnet_code_quality.CA1710.severity = none`". **Fixed (pre-specified, incremental) — derived from commit `e3abe921` policy + CLAUDE.md convention.** No new design decision. §2.9 + §3 T6 carry the `.editorconfig` action.

- **`int` → `long` for `line` and `column`**: §2.4 maps Dart `int` to C# `long` via the recurring `rf-dart-int-to-csharp-long-width` idiom (ratified in the opcodes.dart spec). Convspec construct `dart.named_optional_param_initialising_formal_plus_extra_named` explicitly cites this provenance. **Fixed (pre-specified, incremental) — derived from convspec + opcodes.dart idiom-KB entry.**

- **Case-delta in `[Category]` bracketed prefix**: §2.6 acknowledges Dart prints `[lexical]` (lowercase post-split) while C# enum `ToString()` prints `[Lexical]` (PascalCase member identifier). Convspec records this as an intentional .NET-idiomatic rendering, with `ToLowerInvariant()` available as a downstream-codegen option if byte-identical output is later required. **Fixed (pre-specified, incremental) — derived from convspec `dart.enum.plain_named_constants.nuance`.** No silent design decision.

- **`base.ToString()` not invoked**: §2.6 deliberately replaces (does not extend) `Exception.ToString` to preserve Dart's diagnostic-with-caret shape (without stack-trace tail). Convspec records this explicitly. **Fixed (pre-specified, incremental) — derived from convspec `dart.override_tostring_...nuance`.**

- **Privacy narrowing (library-private → class-private)** for `_categoryFromPhase` → `CategoryFromPhase`: §2.5 narrows strictly because the helper has zero cross-class consumers in this file. Convspec records the narrowing as strictly correct in single-class context and flags the conditional-escalation rule ("if a future converted file adds a cross-class call, escalate at that point") — does not apply now. **Fixed (pre-specified, incremental) — derived from convspec `rf-dart-leading-underscore-privacy-to-csharp-private`.**

- **No `[Flags]` on `ErrorCategory`**: §2.1 explicitly omits `[Flags]` because phases are mutually exclusive and no bitwise combination occurs. Convspec records this. **Fixed (pre-specified, incremental) — derived from convspec.**

- **Hybrid constructor — single ctor only, no three-common-constructors pattern**: §2.4 emits only the single semantic ctor the Dart source declares. Microsoft Learn's "three common constructors" recommendation is intentionally NOT mechanically applied (would manufacture an instantiation surface absent from Dart, violating FR-013 spec-faithfulness). Convspec records this. **Fixed (pre-specified, incremental) — derived from convspec + FR-013.**

- **Fault semantics on negative `Column`**: §2.6 step 4 preserves Dart's throw-on-negative-count semantics (Dart `RangeError` ⇔ .NET `ArgumentOutOfRangeException`) without synthesising a defensive guard. Convspec records this with reference to CLAUDE.md ("robustness is often a workaround in disguise") and FR-013. **Fixed (pre-specified, incremental) — derived from convspec + CLAUDE.md.**

- **Tombstone metadata** (`cycle_group_id: 3`, `scc_siblings: []`, `topo_level: 0`, dependency-free): consistent with the front-matter and with §1's "self-contained leaf module" statement. No `## 7. Cycle Siblings` section is emitted because `scc_siblings` is empty (correct structural choice — adding it would be an error).

No residual gaps. Open escalation count: **0**.

## 6. Escalations

None.
