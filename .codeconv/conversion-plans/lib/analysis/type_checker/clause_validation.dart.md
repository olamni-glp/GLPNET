---
path: lib/analysis/type_checker/clause_validation.dart
cycle_group_id: 4
scc_siblings: []
generated_at: 2026-05-21T14:52:37Z
source_sha256: a1d75e00b2790d353904ac1a09dc2185eddd7498f9e4d0be4257e70493813438
schema_version: 1
---

# Conversion Plan: lib/analysis/type_checker/clause_validation.dart

## 1. Source Analysis

Inspected `glp_runtime_net/lib/analysis/type_checker/clause_validation.dart` (67 lines, sha256 `a1d75e00…3438`). The file is a leaf module in the type-checker subtree (topo_level=2, cycle_group_id=4, no SCC siblings) that validates `Term` AST nodes against the project-wide invariant "no anonymous readers in program clauses" (typed-glp-manual §9). Concrete inventory:

- **Imports (2):** `../../compiler/ast.dart` (provides `Term`, `UnderscoreTerm`, `VarTerm`, `StructTerm`, `ListTerm`); `../../compiler/error.dart` (provides `CompileError`).
- **Top-level public functions (3):** `validateClauseHead(Term)`, `validateClauseBody(Term)`, `validateGuard(Term)` — each a one-line delegate to `_checkNoAnonymousReader(term)`. Three named entry points are kept distinct per the referenced spec `clause-validation.md` (anticipates future per-context divergence).
- **Top-level private helper (1):** `_checkNoAnonymousReader(Term)` — recursive AST walker with four sequential `if (term is X)` blocks over disjoint AST sub-types:
  1. `UnderscoreTerm` with `isReader` ⇒ throw `CompileError('_? (anonymous reader) …', term.line, term.column, phase: 'validation')`.
  2. `VarTerm` with `name.startsWith('_')` and `isReader` ⇒ throw `CompileError('${term.name}? (anonymous reader) …', term.line, term.column, phase: 'validation')`.
  3. `StructTerm` ⇒ recurse over `term.args`.
  4. `ListTerm` ⇒ recurse over nullable `term.head` and `term.tail` (Dart bang `!` after null-check).
- **Doc-comments:** four Dart `///` blocks (one per function) — verbatim XML-doc port.
- **External call to `TypeEnvironment.getType(String)`:** NONE in this file. The coupled-E1 note in the task prompt does not apply (verified by inspecting all 67 lines — no such call site exists).

## 2. Dart → C#/.NET Conversion Plan

Mirrors convspec §constructs verbatim. Five constructs total; all driven by cached idioms (FR-024); zero escalations.

| # | Construct (convspec key) | Dart form | C# target |
|---|---|---|---|
| C1 | `dart.toplevel_public_void_validator_fn_thin_dispatch` | three top-level `void validateXxx(Term)` functions | host class `public static class ClauseValidation` in `namespace Glp.Analysis.TypeChecker`; methods become `public static void ValidateClauseHead/ValidateClauseBody/ValidateGuard(Term term) => CheckNoAnonymousReader(term);` (idiom `dart-toplevel-fn-to-csharp-static-method`, research `rf-csharp-static-class-no-toplevel-members`) |
| C2 | `dart.private_toplevel_recursive_ast_walker_fn` | private helper `_checkNoAnonymousReader` | `private static void CheckNoAnonymousReader(Term term)` on the SAME `ClauseValidation` class. `private` (NOT `internal`) — helper is file-internal (single caller surface = three siblings in this file). Idiom `dart-private-toplevel-helper-to-csharp-private-static-method`, research `rf-csharp-private-vs-internal-library-helpers` |
| C3 | `dart.is_typecheck_chain_recursive_ast_dispatch` | four sequential `if (term is X)` blocks | single C# `switch (term) { case UnderscoreTerm u when u.IsReader: throw …; case VarTerm v when v.Name.StartsWith("_", StringComparison.Ordinal) && v.IsReader: throw …; case StructTerm s: foreach (var arg in s.Args) CheckNoAnonymousReader(arg); break; case ListTerm l: if (l.Head is not null) CheckNoAnonymousReader(l.Head); if (l.Tail is not null) CheckNoAnonymousReader(l.Tail); break; default: break; }`. Disjoint AST sub-types ⇒ `switch` is observationally equivalent to the if-chain. Idiom `dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch`, research `rf-dart-extension-is-as-to-csharp-type-pattern-switch`. Null-safety nuance: Dart `head!`/`tail!` bang drops in C# under NRT flow-analysis after `is not null`. |
| C4 | `dart.compile_error_throw_with_position_and_named_phase` | `throw CompileError('…', term.line, term.column, phase: 'validation')` (one literal + one interpolated message) | `throw new CompileError("…", u.Line, u.Column, phase: "validation");` for the literal site; `throw new CompileError($"{v.Name}? (anonymous reader) is not permitted in program clauses", v.Line, v.Column, phase: "validation");` for the interpolated site. C# requires `new`; Dart named-arg syntax is identical (`name: value`); `CompileError`'s ctor `(string, int, int, string? phase = null)` is specced in the sibling `compiler/error.dart` convspec. Idiom `dart-tostring-interpolation-to-csharp-interpolated-string`, research `rf-csharp-interpolated-string-equivalent-to-dart-interpolation` |
| C5 | `dart.string_startswith_underscore_ordinal_prefix_check` | `term.name.startsWith('_')` | `v.Name.StartsWith("_", StringComparison.Ordinal)` — ordinal comparer ALWAYS explicit (Dart `String.startsWith` is code-unit; C# default is current-culture ⇒ Turkish-I class of bugs). Idiom `dart-string-keyed-map-to-csharp-ordinal-dictionary` (broader ordinal-discipline rule), research `rf-csharp-string-equality-ordinal-by-default` |

**Trivial elements (no construct):** file header `//` comment, spec-citation comment `// Per spec: …` (preserve verbatim); `///` XML-doc summaries (1-for-1 from Dart triple-slash); `import` directives → `using Glp.Compiler;` emitted by codegen stage (cross-file).

## 3. Decomposed Task Units

- **T1:** Create `lib/analysis/type_checker/clause_validation.cs` with file header comment, spec-citation comment, and `namespace Glp.Analysis.TypeChecker;` (file-scoped namespace) plus `using System;` (for `StringComparison`) and `using Glp.Compiler;` (for `Term`, AST sub-types, `CompileError`).
- **T2:** Emit `public static class ClauseValidation` shell.
- **T3:** Port the three Dart `///` doc-comments for `validateClauseHead`/`Body`/`Guard` as C# `///` summary blocks verbatim; emit the three public expression-bodied methods `public static void Validate{ClauseHead,ClauseBody,Guard}(Term term) => CheckNoAnonymousReader(term);` (C1).
- **T4:** Port the Dart `///` doc-comment for `_checkNoAnonymousReader` as a C# `///` summary block verbatim (preserving the typed-glp-manual §9 citation); emit `private static void CheckNoAnonymousReader(Term term)` signature (C2).
- **T5:** Inside the helper, emit a single `switch (term) { … }` with five arms — `UnderscoreTerm u when u.IsReader`, `VarTerm v when v.Name.StartsWith("_", StringComparison.Ordinal) && v.IsReader`, `StructTerm s`, `ListTerm l`, and a `default:` no-op (C3).
- **T6:** Emit the two `throw new CompileError(…)` call sites — literal message (C4 literal arm) and interpolated `$"{v.Name}? …"` (C4 interpolation arm), both passing `phase: "validation"` as a named argument.
- **T7:** Confirm the StructTerm arm uses `foreach (var arg in s.Args) CheckNoAnonymousReader(arg); break;` and the ListTerm arm uses `if (l.Head is not null) CheckNoAnonymousReader(l.Head); if (l.Tail is not null) CheckNoAnonymousReader(l.Tail); break;` (no `!` forgiveness operator — NRT flow analysis narrows).
- **T8:** Record the four cached-idiom reuses (no fresh KB inserts) and stamp tombstone `plan_completed_at` + `plan_path`.

## 4. Research Findings

none required — all five constructs map to cached idioms already `active` in the conversion-idiom KB (FR-024 / SC-007 reuse). Research findings referenced (each previously recorded by a prior convspec; this plan cites, does not re-derive):

- `rf-csharp-static-class-no-toplevel-members` (originally `prelude.dart`) — C# has no library-level members; host in `static class`.
- `rf-csharp-private-vs-internal-library-helpers` (originally `program_dfa.dart`) — Dart leading-underscore = library-private ⇒ C# `private` (when co-located with callers) or `internal` (when cross-file).
- `rf-dart-extension-is-as-to-csharp-type-pattern-switch` (originally `type_ast.dart` / `program_dfa.dart`) — Microsoft Learn pattern-matching reference; `case T t when <guard>:` arms with captured pattern variables.
- `rf-csharp-interpolated-string-equivalent-to-dart-interpolation` (originally `program_dfa.dart`) — `$"{expr}"` is the C# equivalent of Dart `'${expr}'`; named-argument syntax `name: value` is identical in both languages.
- `rf-csharp-string-equality-ordinal-by-default` (cited under idiom `dart-string-keyed-map-to-csharp-ordinal-dictionary`) — Microsoft Learn culture-sensitive-string-comparison guidance; always pass `StringComparison.Ordinal` to `StartsWith`.

## 5. Consistency Pass

fixed — derived from convspec §constructs + cited cached idioms + tombstone metadata. Specific consistency checks:

- **C1 host-class naming `ClauseValidation`:** matches convspec line "public static class ClauseValidation" and matches the file's purpose as a cohesive set of clause-validation entry points; no conflict with sibling files in `lib/analysis/type_checker/`.
- **C2 visibility `private` vs `internal`:** convspec explicitly argues for `private` because the helper has zero cross-file callers (verified — tombstone `callers:` lists only `type_checker.dart`, which calls the three public siblings, not the private helper). Tightest correct mapping confirmed.
- **C3 `switch` semantic equivalence to four sequential `if`s:** the four AST sub-types `UnderscoreTerm`, `VarTerm`, `StructTerm`, `ListTerm` are disjoint (no diamond inheritance) — derived from `compiler/ast.dart`'s class hierarchy as recorded in its convspec; `switch` picks at most one arm, matching the de-facto Dart behavior (a `StructTerm` never matches `UnderscoreTerm` etc.). The `default:` no-op arm preserves "leaf node ⇒ nothing to recurse into" semantics.
- **C4 `CompileError` ctor `(string, int, int, string? phase = null)`:** derived from `compiler/error.dart`'s own convspec (sibling cached idiom `dart-error-class-recoverable-signal-to-csharp-exception`); convspec line 130–138 mirrors that ctor shape one-for-one.
- **C5 `StringComparison.Ordinal` discipline:** convspec line 178–181 — code currently only checks ASCII `_` so culture sensitivity is observationally harmless TODAY, but the ordinal discipline is preserved as a codebase-wide invariant. Matches the cached idiom's principle.
- **TypeEnvironment.getType(String) cross-file E1 coupling note (from task prompt):** N/A — verified by full source inspection (lines 1–67); this file does not call `TypeEnvironment.getType`. No coupled escalation; no §5 deferral needed.
- **Tombstone alignment:** `cycle_group_id: 4`, `scc_siblings: []`, `target_path: lib/analysis/type_checker/clause_validation.cs`, `source_sha256: a1d75e00…3438` — all match plan front-matter.

## 6. Escalations

None.
