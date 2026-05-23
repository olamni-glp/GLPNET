---
path: lib/analysis/type_checker/type_conversion.dart
cycle_group_id: 10
scc_siblings: []
generated_at: 2026-05-21T14:59:50Z
source_sha256: 9c7136df1616ffa51b44939772b8a8e0d7b2d6da7d562caa07fbaa7868a1081c
schema_version: 1
---

# Conversion Plan: lib/analysis/type_checker/type_conversion.dart

## 1. Source Analysis

The Dart source `lib/analysis/type_checker/type_conversion.dart` is a single-file, stateless, pure structural converter from the parser's `Term` AST to the type-checker's `TypeExpr` AST. Verified inspection of the 89-line file shows:

- **Imports**: `'../../compiler/ast.dart'` (the `Term` hierarchy: `VarTerm`, `UnderscoreTerm`, `ConstTerm`, `ListTerm`, `StructTerm`) and `'type_ast.dart'` (the `TypeExpr` hierarchy: `TypeRef`, `PrimitiveModeAlt`, `ConstantAlt`, `ListNilAlt`, `ListConsAlt`, `DiffListAlt`, `StructAlt`).
- **One public top-level function**: `TypeExpr termToTypeExpr(Term term)` (lines 13–83). Body is a sequential five-way `if (term is X) { ... }` chain with an unreachable trailing `throw ArgumentError(...)` (line 82).
- **One library-private helper**: `bool _isUppercaseLetter(String ch)` (lines 87–89) — ASCII-range code-unit check `65..90`.
- **Doc-comments**: triple-slash `///` summary on `termToTypeExpr` documenting "pure structural conversion with no semantic validation" (lines 9–12); triple-slash `///` on `_isUppercaseLetter` documenting *why NOT* a generic uppercase check — "Excludes operators (+, -, etc.) and underscore which satisfy toUpperCase() == self" (lines 85–86).
- **Branch logic inside `StructTerm`** (lines 45–78): (a) functor `'\\'` with arity 2 → `DiffListAlt`; (b) trim trailing `'?'` from functor (set `isInput=true`), if remaining first char is uppercase ASCII → `TypeRef` with mapped `typeArgs`; (c) fall-through → `StructAlt`.
- **Branch logic inside `ListTerm`** (lines 31–43): both `head` and `tail` null → `ListNilAlt`; otherwise → `ListConsAlt(termToTypeExpr(head!), termToTypeExpr(tail!), …)` relying on the AST invariant that `ListTerm` is built either both-null (nil) or both-non-null (cons).
- **Recursion**: at four sites (`ListConsAlt` head/tail, `DiffListAlt` args 0/1, `TypeRef` `typeArgs.map`, `StructAlt` args `.map`).
- **`ConstTerm.value`** is `Object?`; adapted to `ConstantAlt`'s `Object` parameter via `term.value ?? ''`.
- **Zero state, zero I/O.** No fields, no globals, no async, no `Future`/`Stream`, no `dart:io`, no mutation of inputs.

Per the ratified convspec, this file has 9 constructs (8 substantive + the trailing throw) and the conversion is a straight transliteration into one C# static host class with two private sub-dispatch helpers and one private predicate.

## 2. Dart → C#/.NET Conversion Plan

The plan mirrors the ratified convspec verbatim. Each construct is reproduced here in plan form (Dart fragment → C# target decision); rationale lives in convspec §`nuance` and §`Rationale & Research Provenance` and is NOT duplicated.

### 2.1 Host class + namespace  (construct `dart.toplevel_public_pure_dispatch_fn_term_to_typeexpr`)

Dart top-level function `TypeExpr termToTypeExpr(Term term)` → C# `public static TypeExpr TermToTypeExpr(Term term)` on a new `public static class TypeConversion` in namespace `Glp.Analysis.TypeChecker`. PascalCase per .NET conventions. `public` visibility (sibling-assembly callers — type environment, declaration builders). Idiom `dart-toplevel-fn-to-csharp-static-method` (cached, FR-024).

### 2.2 Sequential `is`-chain dispatch  (construct `dart.is_typecheck_cast_chain_returns_disjoint_term_subtypes`)

The five sequential `if (term is X) { … return …; }` arms (Var/Underscore/Const/List/Struct) → single C# switch expression on `term`:

```
return term switch {
  VarTerm v        => new TypeRef(v.Name, v.Line, v.Column, isInput: v.IsReader),
  UnderscoreTerm u => new PrimitiveModeAlt(u.IsReader, u.Line, u.Column),
  ConstTerm c      => new ConstantAlt(c.Value ?? "", c.Line, c.Column),
  ListTerm l       => ConvertList(l),
  StructTerm s     => ConvertStruct(s),
  _                => throw new ArgumentException($"Cannot convert term to type expression: {term}")
};
```

Idiom `dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch` (cached, FR-024).

### 2.3 `Object? → Object` null-coalescing on `ConstTerm`  (construct `dart.nullable_object_default_via_null_coalescing_to_empty_string`)

`final value = term.value ?? ''; return ConstantAlt(value, …);` → inline at arm: `ConstTerm c => new ConstantAlt(c.Value ?? "", c.Line, c.Column)`. Fallback literal MUST be `""` (NOT `0`/`null`/sentinel) per convspec nuance — diagnostic-toString stability. Idiom `dart-nullable-object-coalesce-default-to-csharp-null-coalescing` (first-seen, recorded).

### 2.4 `ListTerm` nil-vs-cons sub-dispatch  (construct `dart.listterm_nullable_head_tail_empty_vs_cons_branching`)

Factor to private helper `private static TypeExpr ConvertList(ListTerm l)`:

```
if (l.Head is null && l.Tail is null)
    return new ListNilAlt(l.Line, l.Column);
return new ListConsAlt(TermToTypeExpr(l.Head!), TermToTypeExpr(l.Tail!), l.Line, l.Column);
```

Dart `head!`/`tail!` → C# null-forgiving `!` (intentional; convspec nuance documents the runtime-vs-compile-time semantic gap and the recommendation to add `Debug.Assert(l.Head is not null && l.Tail is not null)` on the cons branch). Idiom `dart-nullable-pair-invariant-bang-to-csharp-null-forgiving` (first-seen, recorded).

### 2.5 `StructTerm` sub-dispatch — `\` arity-2 → `DiffListAlt`  (construct `dart.structterm_diff_list_special_case_two_arg_backslash_functor`)

Factor to private helper `private static TypeExpr ConvertStruct(StructTerm s)`. First arm:

```
if (s.Functor == "\\" && s.Args.Count == 2)
    return new DiffListAlt(TermToTypeExpr(s.Args[0]), TermToTypeExpr(s.Args[1]), s.Line, s.Column);
```

C# `==` on `string` is ordinal value equality by default — NO `StringComparison.Ordinal` argument required (convspec nuance vs §2.6 `EndsWith`). Backslash literal `"\\"` (single-char `\`) verbatim from Dart `'\\'`. Idiom `dart-string-literal-equality-to-csharp-ordinal-default` (first-seen, recorded).

### 2.6 Functor `?` trim + uppercase-first-char → `TypeRef`  (construct `dart.string_suffix_trim_and_uppercase_first_letter_check`)

Continue in `ConvertStruct`:

```
var functor = s.Functor;
bool isInput = false;
if (functor.EndsWith("?", StringComparison.Ordinal)) {
    functor = functor.Substring(0, functor.Length - 1);
    isInput = true;
}
if (functor.Length > 0 && IsUppercaseLetter(functor[0]))
    return new TypeRef(functor, s.Line, s.Column,
        isInput: isInput,
        typeArgs: s.Args.Select(TermToTypeExpr).ToList());
```

Explicit `StringComparison.Ordinal` on `EndsWith` (C# default overload is culture-sensitive). `functor.isNotEmpty` → `functor.Length > 0`. `functor[0]` returns `char` in C# (not 1-char `string`), passed to `IsUppercaseLetter(char)` (signature flip from Dart `String ch`). `.Select(...).ToList()` materialises (convspec nuance: deferred-execution hazard if `.ToList()` omitted). Idiom `dart-string-keyed-map-to-csharp-ordinal-dictionary` (cached, FR-024 — reused for the broader ordinal-discipline principle).

### 2.7 ASCII uppercase-letter private predicate  (construct `dart.private_codeunit_range_uppercase_ascii_predicate`)

Dart `bool _isUppercaseLetter(String ch) { return ch.codeUnitAt(0) >= 65 && ch.codeUnitAt(0) <= 90; }` → C# `private static bool IsUppercaseLetter(char ch) => ch >= 'A' && ch <= 'Z';` on the host static class. Visibility `private` (single co-located caller). Char literals `'A'`/`'Z'` replace numeric `65`/`90` for readability — UTF-16 code units 65/90 exactly. Doc-comment rationale (*why NOT `char.IsUpper(ch)`* — over-accepts Unicode `Lu`) ported verbatim to `/// <summary>` XML-doc. `char.IsAsciiLetterUpper` (.NET 7+) mentioned and REJECTED to avoid SDK-version coupling. Idiom `dart-private-toplevel-helper-to-csharp-private-static-method` (cached, FR-024).

### 2.8 Default `StructTerm` arm → `StructAlt`  (construct `dart.structterm_default_arm_emits_struct_alt_with_mapped_args`)

Final `return` in `ConvertStruct`:

```
return new StructAlt(s.Functor, s.Args.Select(TermToTypeExpr).ToList(), s.Line, s.Column);
```

`s.Args.Select(TermToTypeExpr).ToList()` — `.ToList()` MANDATORY (convspec nuance: deferred-execution + side-effect hazard). Sub-dispatch within `ConvertStruct` written as imperative `if/return` ladder (NOT nested switch) per convspec — early-return chain reads more naturally. Idiom `dart-iterable-map-tolist-to-csharp-linq-select-tolist` (first-seen, recorded).

### 2.9 Unreachable `throw ArgumentError` default  (construct `dart.throw_argumenterror_with_term_interpolation_unreachable_default`)

Dart `throw ArgumentError('Cannot convert term to type expression: $term');` → C# `_ => throw new ArgumentException($"Cannot convert term to type expression: {term}")` as the discard arm of the top-level switch expression in §2.2. `ArgumentError` (`dart:core`) → `ArgumentException` (`System`). Dart `$term` interpolation → C# `{term}` interpolated-string (identical `toString()`/`ToString()` semantics on reference-typed `Term`). C# `throw new` (vs Dart `throw`). Satisfies CS8509 (non-exhaustive switch) and preserves runtime totality if `Term` hierarchy widens (Dart `abstract class` is open; C# does not infer sealed). Idiom `dart-error-class-recoverable-signal-to-csharp-exception` (cached, FR-024, applied to the `ArgumentError → ArgumentException` sub-case via research finding `rf-csharp-argumentexception-maps-to-dart-argumenterror`).

### 2.10 File-level concerns

- **Namespace declaration**: `namespace Glp.Analysis.TypeChecker { … }` (file-scoped or block-scoped — codegen choice; convspec is agnostic; block-scoped recommended for clarity with the single host class).
- **`using` directives**: `using System;` (for `ArgumentException`), `using System.Linq;` (for `Select`/`ToList`), `using Glp.Compiler;` (for the `Term`/`VarTerm`/`UnderscoreTerm`/`ConstTerm`/`ListTerm`/`StructTerm` types from the converted `ast.dart`). The `type_ast.dart` types are in the same `Glp.Analysis.TypeChecker` namespace and require no explicit `using`.
- **File header comments**: Dart `//`-line file-path header and the `// Per spec: /Users/udi/GLP/docs/type system/type-conversion.md` citation port to C# `//` comments verbatim (1-for-1; trivial).
- **XML doc-comments**: Dart `///`-summary blocks on `termToTypeExpr` and `_isUppercaseLetter` port to C# `/// <summary> … </summary>` blocks verbatim — both rationale comments load-bearing per convspec (`conversion_units` 6th bullet).
- **Target file path**: `lib/analysis/type_checker/type_conversion.cs` (mirroring the source subtree, per the langpair convention; tombstone `target_path` confirms).

## 3. Decomposed Task Units

- T1: Create file `lib/analysis/type_checker/type_conversion.cs` with `namespace Glp.Analysis.TypeChecker` and the three required `using` directives (`System`, `System.Linq`, `Glp.Compiler`).
- T2: Declare `public static class TypeConversion` host (§2.1) — file header comments + spec-citation comment ported verbatim.
- T3: Emit `public static TypeExpr TermToTypeExpr(Term term) => term switch { … };` (§2.2) with the five typed arms (Var/Underscore/Const/List/Struct) and the discard `_ => throw new ArgumentException(...)` (§2.9).
- T4: In the `ConstTerm c => …` arm, emit `c.Value ?? ""` for the `Object?→Object` adapter (§2.3).
- T5: Emit `private static TypeExpr ConvertList(ListTerm l)` with nil-detect + cons recursive-build (§2.4) including `Debug.Assert(l.Head is not null && l.Tail is not null)` on the cons branch.
- T6: Emit `private static TypeExpr ConvertStruct(StructTerm s)` opening with the `s.Functor == "\\" && s.Args.Count == 2 → DiffListAlt` arm (§2.5).
- T7: Continue `ConvertStruct` with the functor `EndsWith("?", StringComparison.Ordinal)` trim + uppercase-first-char check producing `TypeRef` with `s.Args.Select(TermToTypeExpr).ToList()` (§2.6).
- T8: Close `ConvertStruct` with the default `return new StructAlt(s.Functor, s.Args.Select(TermToTypeExpr).ToList(), s.Line, s.Column);` (§2.8).
- T9: Emit `private static bool IsUppercaseLetter(char ch) => ch >= 'A' && ch <= 'Z';` with the load-bearing XML-doc rationale (§2.7).
- T10: Port both Dart `///` doc-comments to C# `/// <summary>` XML-doc blocks verbatim (`termToTypeExpr` purity contract + `IsUppercaseLetter` rationale).
- T11: Mark tombstone `status: complete`, `target_path: lib/analysis/type_checker/type_conversion.cs`.

## 4. Research Findings

None required. All decisions are verbatim-derivable from the ratified convspec, which itself cites the cached FR-024 idiom KB (7 of 9 constructs reuse `prelude.dart` / `clause_validation.dart` / `type_ast.dart` / `program_dfa.dart` research; 2 constructs — `dart-nullable-pair-invariant-bang-to-csharp-null-forgiving` and `dart-string-literal-equality-to-csharp-ordinal-default` — were first-seen and authoritatively researched against Microsoft Learn + api.dart.dev as documented in the convspec's "Rationale & Research Provenance" section). No fresh WebSearch / WebFetch / Agent calls are necessary at the plan stage.

## 5. Consistency Pass

Fixed — derived from the ratified convspec `.codeconv/conversion-specs/lib/analysis/type_checker/type_conversion.dart.md` (source_sha256 match: 9c7136df1616ffa51b44939772b8a8e0d7b2d6da7d562caa07fbaa7868a1081c). All nine constructs map 1-for-1 to plan sections §2.1–§2.9; all eleven decomposed tasks T1–T11 are covered by convspec `conversion_units` (six bullets enumerating namespace+host-class, top-level switch, `ConvertList`, `ConvertStruct`, `IsUppercaseLetter`, and the XML-doc-port mandate). No cross-file decision required for this file — `TypeEnvironment.getType(String)` is NOT referenced (the convspec's reference-to-`type_ast.dart` E1 is the parsing-side helper note and does not apply here). All identifier-rename, null-safety, ordinal-string-discipline, deferred-execution, and exception-mapping choices are derived from convspec `target_decision` + `nuance` verbatim.

## 6. Escalations

None.
