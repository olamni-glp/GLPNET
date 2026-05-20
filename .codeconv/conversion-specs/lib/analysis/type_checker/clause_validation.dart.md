# Conversion Spec — lib/analysis/type_checker/clause_validation.dart

```yaml
schema_version: 1
source_path: lib/analysis/type_checker/clause_validation.dart
source_sha256: a1d75e00b2790d353904ac1a09dc2185eddd7498f9e4d0be4257e70493813438
target_code_unit: lib/analysis/type_checker/clause_validation.cs
constructs:
  - construct_key: dart.toplevel_public_void_validator_fn_thin_dispatch
    source_form: >-
      void validateClauseHead(Term term) { _checkNoAnonymousReader(term); }
      void validateClauseBody(Term term) { _checkNoAnonymousReader(term); }
      void validateGuard(Term term)      { _checkNoAnonymousReader(term); }
    target_decision: >-
      Emit each as `public static void` on a single host static class
      `public static class ClauseValidation` in namespace mirroring the
      file path (`Glp.Analysis.TypeChecker`). The three methods become
      `ValidateClauseHead(Term term)`, `ValidateClauseBody(Term term)`,
      `ValidateGuard(Term term)` and each simply calls the private static
      helper `CheckNoAnonymousReader(term)`. Three named entry points are
      preserved (not collapsed into one with an enum parameter) because
      the public spec/manual carves them as three distinct
      validation-context APIs (head / body / guard) and the doc-comments
      describe per-context allowed forms; later guard-vs-head divergence
      is anticipated by `clause-validation.md`.
    idiom_id: dart-toplevel-fn-to-csharp-static-method
    research_finding_id: rf-csharp-static-class-no-toplevel-members
    nuance: >-
      Dart allows library-level (top-level) functions; C# has no
      library-level members — every method must be a member of a type.
      Reusing the cached idiom (FR-024 cache from
      `prelude.dart` / `program_dfa.dart`): host in a `static class`
      named after the file (`ClauseValidation`) so call sites read
      `ClauseValidation.ValidateClauseHead(term)`. No reference-vs-value
      hazard (void return; reference-type `Term` parameter passes by
      reference identity in both languages). Naming: Dart `lowerCamel`
      free functions become C# `PascalCase` static methods (.NET
      conventions); the host class is `internal`/`public` per the
      consumer surface — `public` is chosen because the methods are
      currently called from outside the file.
  - construct_key: dart.private_toplevel_recursive_ast_walker_fn
    source_form: >-
      void _checkNoAnonymousReader(Term term) { ... recursive descent
      over UnderscoreTerm / VarTerm / StructTerm / ListTerm ... }
    target_decision: >-
      Emit as `private static void CheckNoAnonymousReader(Term term)` on
      the SAME host static class `ClauseValidation` (not a separate
      file-private type) — `private` visibility on a static class member
      is the .NET equivalent of Dart's library-private leading
      underscore for a helper that is genuinely file-internal. Do NOT
      use `internal` here: Dart `_name` is **library-private**, but in
      this file the helper is also file-internal (single caller surface
      = the three siblings in this file), so `private` on the host
      static class is the tighter, correct mapping. If a later
      consolidation reveals the helper is needed across files within
      the same assembly, narrow→widen to `internal` is mechanical.
    idiom_id: dart-private-toplevel-helper-to-csharp-private-static-method
    research_finding_id: rf-csharp-private-vs-internal-library-helpers
    nuance: >-
      Reusing the cached idiom (program_dfa.dart). Dart visibility model
      is two-level (public / library-private via `_`); C# has five
      access levels (public/protected/internal/protected
      internal/private/file). The leading-underscore Dart convention
      maps to `private` when the helper is co-located with its callers
      in a single C# type, which matches this file's structure
      precisely. Reference semantics for the `Term` parameter and
      recursion depth are unchanged (C# stack frames cost is
      comparable; Dart isolates / C# default thread share equivalent
      stack semantics for a single synchronous call). No tail-call
      transformation needed; recursion depth is bounded by AST depth.
  - construct_key: dart.is_typecheck_chain_recursive_ast_dispatch
    source_form: >-
      if (term is UnderscoreTerm && term.isReader) { throw ... }
      if (term is VarTerm && term.name.startsWith('_') && term.isReader)
      { throw ... }
      if (term is StructTerm) { for (final arg in term.args)
        _checkNoAnonymousReader(arg); }
      if (term is ListTerm) { if (term.head != null)
        _checkNoAnonymousReader(term.head!);
        if (term.tail != null) _checkNoAnonymousReader(term.tail!); }
    target_decision: >-
      Convert to a single C# `switch` statement with type patterns on
      `term`, with arms for each AST subtype, plus a discard `_ =>`
      arm that does nothing (a non-matching node is a leaf with no
      readers — same as Dart falling through all four ifs). Pattern
      arms: `case UnderscoreTerm u when u.IsReader: throw ...;`
      `case VarTerm v when v.Name.StartsWith("_",
      StringComparison.Ordinal) && v.IsReader: throw ...;`
      `case StructTerm s: foreach (var arg in s.Args)
      CheckNoAnonymousReader(arg); break;`
      `case ListTerm l: if (l.Head is not null)
      CheckNoAnonymousReader(l.Head); if (l.Tail is not null)
      CheckNoAnonymousReader(l.Tail); break;`
      `default: break;`.
      Preserves the Dart sequential-if semantics exactly: in Dart the
      four `if` blocks are independent (a `StructTerm` does NOT also
      match `UnderscoreTerm`, so order is observationally irrelevant —
      the AST sub-types are disjoint), so a `switch` (which picks one
      arm) is semantically equivalent. The switch additionally
      *documents* the disjointness to readers and to any future C#
      analyzer pass.
    idiom_id: dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch
    research_finding_id: rf-dart-extension-is-as-to-csharp-type-pattern-switch
    nuance: >-
      Reusing the cached idiom (type_ast.dart, program_dfa.dart). Dart
      `is X` with implicit smart-cast (the `term` inside the block is
      narrowed to `X`) maps to C# `case X x` (pattern variable bound).
      Combined boolean guard (`&& term.isReader`) maps to a `when`
      clause on the pattern arm — Microsoft Learn explicitly documents
      pattern arms with `when` guards. Null-safety mapping for the
      ListTerm branch: Dart's `term.head != null` then `term.head!`
      (bang asserts non-null after the check) maps to C#
      `l.Head is not null` followed by direct use of `l.Head` — under
      C# nullable-reference-types flow analysis the compiler narrows
      `l.Head` to non-null after the `is not null` test, so NO `!`
      forgiveness operator is needed. Reference-vs-value: all four AST
      types are reference types (classes) in both languages, so
      pattern matching dispatches on the runtime type tag identically;
      no boxing.
  - construct_key: dart.compile_error_throw_with_position_and_named_phase
    source_form: >-
      throw CompileError(
        '_? (anonymous reader) is not permitted in program clauses',
        term.line, term.column, phase: 'validation');
    target_decision: >-
      Emit as `throw new CompileError("_? (anonymous reader) is not
      permitted in program clauses", term.Line, term.Column,
      phase: "validation");` — the `CompileError` C# type (specced
      elsewhere in `compiler/error.cs`) MUST expose a constructor with
      `(string message, int line, int column, string? phase = null)`
      so the call site is a direct transliteration. Dart's named
      argument `phase:` becomes a C# *named argument* on a defaulted
      parameter (Microsoft Learn: "you can supply arguments by name");
      this preserves the original call-site readability one-for-one
      and avoids inventing a separate ctor overload. Interpolation in
      the second message (Dart `'${term.name}? ...'`) becomes a C#
      interpolated string `$"{v.Name}? (anonymous reader) is not
      permitted in program clauses"`.
    idiom_id: dart-tostring-interpolation-to-csharp-interpolated-string
    research_finding_id: rf-csharp-interpolated-string-equivalent-to-dart-interpolation
    nuance: >-
      Reusing cached interpolation idiom (program_dfa.dart). Two Dart→C#
      nuances are explicit: (1) Dart `throw E(...)` does not require
      `new`; C# requires `throw new E(...)`. (2) Dart named-arg syntax
      `phase: 'validation'` IS valid C# named-argument syntax (same
      `name: value` form), so no rewrite to a builder/withers pattern
      is required. `CompileError` is a recoverable, raised-by-this-
      module signal — it MUST inherit from `System.Exception` in the
      target (NOT `System.SystemException` and NOT `System.Error`
      which doesn't exist in .NET); the type's own spec records this
      using `dart-error-class-recoverable-signal-to-csharp-exception`
      (cached idiom from program_dfa.dart). Position fields
      `term.line` / `term.column` are non-null `int` in both languages
      — no nullability mapping concern at this call site.
  - construct_key: dart.string_startswith_underscore_ordinal_prefix_check
    source_form: term.name.startsWith('_')
    target_decision: >-
      Emit as `v.Name.StartsWith("_", StringComparison.Ordinal)` —
      ALWAYS pass `StringComparison.Ordinal` explicitly. Dart's
      `String.startsWith(Pattern)` performs a UTF-16 code-unit prefix
      test (ordinal byte-wise for the BMP character `_`); C#'s
      `string.StartsWith(string)` overload uses the **current culture**
      by default, which (per Microsoft Learn's well-known
      culture-sensitive-string-comparison guidance) can yield surprising
      results for some Unicode characters and is locale-dependent.
      Passing `StringComparison.Ordinal` matches Dart's code-unit
      semantics deterministically.
    idiom_id: dart-string-keyed-map-to-csharp-ordinal-dictionary
    research_finding_id: rf-csharp-string-equality-ordinal-by-default
    nuance: >-
      The KB idiom `dart-string-keyed-map-to-csharp-ordinal-dictionary`
      records the same ordinal-discipline principle for dictionary
      keys; the same principle (use ordinal comparisons whenever a Dart
      string operation is semantically code-unit-based) applies to
      `StartsWith`. This is one of the most-cited Dart→.NET porting
      pitfalls (culture-sensitive default ⇒ Turkish-`I` class of bugs)
      and the spec MUST address it rather than gloss. The check looks
      only at the ASCII `_` (U+005F), so culture sensitivity could not
      *actually* mis-classify here today, but the ordinal discipline is
      preserved as a code-base-wide invariant so a future maintainer
      adding a non-ASCII prefix check does not get bitten.
conversion_units:
  - "namespace Glp.Analysis.TypeChecker { public static class ClauseValidation { ... } }"
  - "public static void ValidateClauseHead(Term term) => CheckNoAnonymousReader(term);"
  - "public static void ValidateClauseBody(Term term) => CheckNoAnonymousReader(term);"
  - "public static void ValidateGuard(Term term)      => CheckNoAnonymousReader(term);"
  - "private static void CheckNoAnonymousReader(Term term): switch (term) { case UnderscoreTerm u when u.IsReader: throw new CompileError(\"_? (anonymous reader) is not permitted in program clauses\", u.Line, u.Column, phase: \"validation\"); case VarTerm v when v.Name.StartsWith(\"_\", StringComparison.Ordinal) && v.IsReader: throw new CompileError($\"{v.Name}? (anonymous reader) is not permitted in program clauses\", v.Line, v.Column, phase: \"validation\"); case StructTerm s: foreach (var arg in s.Args) CheckNoAnonymousReader(arg); break; case ListTerm l: if (l.Head is not null) CheckNoAnonymousReader(l.Head); if (l.Tail is not null) CheckNoAnonymousReader(l.Tail); break; default: break; }"
  - "XML-doc /// summary blocks ported from each Dart /// doc-comment verbatim (head, body, guard rationale + spec citation /Users/udi/GLP/docs/type system/clause-validation.md preserved as the source link)."
escalations: []
```

## Rationale & Research Provenance

### dart-toplevel-fn-to-csharp-static-method  (cached idiom)

**Deep analysis.** Three thin public `void` validators (`validateClauseHead`,
`validateClauseBody`, `validateGuard`) and one private helper
(`_checkNoAnonymousReader`) sit at the library top level. The three publics
are 1-statement delegates to the helper. The carve-up is intentional: the
referenced spec `clause-validation.md` documents three *contexts* even
though the current implementation collapses them, so the three named entry
points MUST survive into C# to keep API parity and to anchor future per-
context divergence.

**Research (cached, FR-024 — no fresh call).** Reuses
`rf-csharp-static-class-no-toplevel-members` first recorded by the
`prelude.dart` spec: Microsoft Learn — "A class declared at namespace
scope is a top-level type; methods can only be declared inside a type."
Idiom `dart-toplevel-fn-to-csharp-static-method` is already `active` in
the KB; per FR-012 / SC-007, REUSE verbatim, do not re-research.

**Conclusion.** Host class `public static class ClauseValidation`; the
three publics become `public static void` methods with PascalCase
identifiers; the helper becomes `private static void
CheckNoAnonymousReader`.

### dart-private-toplevel-helper-to-csharp-private-static-method  (cached idiom)

**Deep analysis.** `_checkNoAnonymousReader` is library-private (leading
underscore). Its only callers are the three siblings in this file.
Therefore the tightest correct visibility in C# is `private` on the host
type — not `internal` (which leaks to the whole assembly).

**Research (cached, FR-024).** Reuses
`rf-csharp-private-vs-internal-library-helpers` (program_dfa.dart). The
idiom is `active`; reuse verbatim.

**Conclusion.** `private static void CheckNoAnonymousReader(Term term)`
on the same `ClauseValidation` host class.

### dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch  (cached idiom)

**Deep analysis.** The helper's body is four sequential `if (term is X)`
blocks: `UnderscoreTerm`, `VarTerm`, `StructTerm`, `ListTerm`. The four
target sub-types are disjoint in the AST hierarchy (no diamond), so the
ifs run in any order — `switch` is observationally equivalent. The
recursion descends into `StructTerm.args` and `ListTerm.head/tail`.

**Research (cached, FR-024).** Reuses
`rf-dart-extension-is-as-to-csharp-type-pattern-switch` from
`type_ast.dart` / `program_dfa.dart`. Microsoft Learn pattern-matching
reference is authoritative on `case T t when <guard>:` arms with
captured pattern variables. The idiom
`dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch` is `active`
in the KB; reuse verbatim per SC-007.

**Conclusion.** Single `switch (term) { case UnderscoreTerm u when ...
case VarTerm v when ... case StructTerm s ... case ListTerm l ... default ... }`
preserves all semantics. **Null-safety nuance** explicitly addressed
in the YAML `nuance:` field: Dart's `term.head!` non-null-bang assertion
disappears in C# because the `is not null` test narrows the variable
under nullable-reference-types flow analysis — a strict improvement
(static guarantee replaces a runtime check).

### dart-tostring-interpolation-to-csharp-interpolated-string  (cached idiom) + CompileError throw shape

**Deep analysis.** Two `throw CompileError(...)` sites: one literal string,
one with a `${term.name}?` interpolation. The constructor takes
`(message, line, column)` positional + `phase:` named.

**Research (cached, FR-024).** Reuses
`rf-csharp-interpolated-string-equivalent-to-dart-interpolation` (idiom
`dart-tostring-interpolation-to-csharp-interpolated-string`,
program_dfa.dart) for the interpolation half, and Microsoft Learn's
named-argument reference for the `phase:` argument — *"Named arguments,
when used with positional arguments, are valid as long as ... they're
used in the correct position"*. The `CompileError` C# type's existence
and ctor shape are specced elsewhere (`compiler/error.dart`'s own
convspec, where the idiom
`dart-error-class-recoverable-signal-to-csharp-exception` records the
`: Exception` base; reused here without re-research).

**Conclusion.** `throw new CompileError("...", u.Line, u.Column,
phase: "validation");` for the literal message; the variable case is
`throw new CompileError($"{v.Name}? (anonymous reader) is not permitted
in program clauses", v.Line, v.Column, phase: "validation");`. Note `new`
(C# requires it, Dart does not).

### dart-string-keyed-map-to-csharp-ordinal-dictionary  (cached idiom, applied to StartsWith)

**Deep analysis.** `term.name.startsWith('_')` is a deterministic
code-unit prefix test in Dart.

**Research (cached, FR-024).** Reuses
`rf-csharp-string-equality-ordinal-by-default`: Microsoft Learn — *"By
default, string operations that depend on culture sensitivity (such as
... StartsWith without a StringComparison parameter) use the current
culture, which can yield surprising results."* The idiom
`dart-string-keyed-map-to-csharp-ordinal-dictionary` is the cached
ordinal-discipline KB entry; the SAME ordinal principle applies to
`StartsWith` (the idiom is named for dictionary keys but its KB
description carries the broader rule), so it is reused rather than
re-derived. If a future audit prefers a separate idiom
`dart-string-startswith-to-csharp-startswith-ordinal`, splitting is
mechanical.

**Conclusion.** `v.Name.StartsWith("_", StringComparison.Ordinal)` —
ordinal comparer explicit. This is the **non-trivial nuance the spec
must not gloss**: a culture-sensitive default is one of the most
well-known Dart→.NET porting pitfalls (per AS4 in US2).

### Trivial / non-construct elements

- File header `// lib/analysis/...` and the spec-citation comment
  `// Per spec: /Users/udi/GLP/docs/type system/clause-validation.md`
  map to C# `//` comments mechanically — no research.
- `/// XML doc-comments` map 1-for-1 to C# `///` summary blocks — no
  research; Dart triple-slash and C# triple-slash semantics are
  identical.
- `import '../../compiler/ast.dart';` and
  `import '../../compiler/error.dart';` are subsumed by `using`
  directives that the codegen stage emits per the project's namespace
  layout (`using Glp.Compiler;`); not specced per construct (trivial,
  cross-file concern).
