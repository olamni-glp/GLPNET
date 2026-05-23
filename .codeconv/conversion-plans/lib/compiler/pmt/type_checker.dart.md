---
path: lib/compiler/pmt/type_checker.dart
cycle_group_id: 59
scc_siblings: []
generated_at: 2026-05-21T15:25:36Z
source_sha256: ec1d9f359ba877391bcc2acf4b8a4b99f7a37b8ae05d778864d89fb9940d5c95
schema_version: 1
---

# Conversion Plan: lib/compiler/pmt/type_checker.dart

## 1. Source Analysis

Inspected `glp_runtime_net/lib/compiler/pmt/type_checker.dart` (317 lines). The
file declares two reference types:

- **`TypeError`** (lines 11-27): a value-bearing error record with three
  non-nullable `final` fields (`message: String`, `line: int`, `column: int`)
  plus one nullable `String? suggestion`, a single positional+named-optional
  constructor, and an override `toString()` that builds `"[type] $message at
  Line $line, Column $column"` and conditionally appends `"\n  Suggestion:
  $suggestion"` if `suggestion != null`. **No `operator ==` or `hashCode`
  override** — Dart default reference equality is intentional.
- **`TypeChecker`** (lines 30-316): coordinator class holding two `final`
  dependency references `typeTable: TypeTable` and `modeTable: ModeTable`,
  populated by a positional constructor (`this.typeTable, this.modeTable`).
  No own mutable state.

Public method surface:

- `List<TypeError> checkModule(Module module)` (lines 37-50): outer foreach
  over `module.procedures`, lookup `modeTable.getDeclaration(proc.name,
  proc.arity)`, silent-skip on null, inner foreach over `proc.clauses`
  delegating to `checkClause` and `errors.addAll`.
- `List<TypeError> checkClause(Clause clause, ModeDeclaration modeDecl)`
  (lines 53-80): two indexed-for-with-min-length-guard loops — one over
  `clause.head.args` ∥ `modeDecl.args`, the other (guarded by `if (clause.body
  != null)`) iterating goals in `clause.body!` and for each goal another
  indexed-for over `goal.args` ∥ `goalDecl.args`. Uses Dart's `!` bang inside
  the null-guard.
- `List<TypeError> checkTerm(Term term, String expectedType, List<String>
  typeParams)` (lines 83-182): five-way `is`-dispatch — `VarTerm` trivial,
  `UnderscoreTerm` trivial, `ConstTerm` (delegates to `isValidConstant` then
  emits a formatted error with `getValidConstructors` suggestion), `ListTerm`
  (five sub-paths: nil early-return; type-resolved branch with constructor-
  set check, `<String, String>{}` typeParam-substitution map, `whereType
  <ListConstructor>().where((c) => !c.isNil).firstOrNull` chain, head recurse
  with substituted element type, `'_'` wildcard skip; else-branch type-params
  fallback; tail recurse), `StructTerm` (delegates to
  `isValidStructConstructor`, same error-with-suggestion shape).
- `bool isValidConstant(ConstTerm term, String typeName)` (lines 185-223):
  four-phase predicate — built-in numeric (`typeName in {Num, Number, Int,
  Integer}` ⇒ `term.value is num`), built-in atom/string (`typeName in
  {Atom, String}` ⇒ `term.value is String`), unknown-type permissive
  `return true`, then AtomConstructor direct-match foreach and AtomConstructor-
  capitalised-name type-reference chase foreach.
- `bool isValidStructConstructor(StructTerm term, String typeName,
  [List<String> typeParams = const []])` (lines 226-270): **optional-
  positional** third param with const-empty-list default; permissive
  `return true` on unknown type; build `<String, String>{}` substitution
  map; pass-1 direct StructConstructor.functor match; pass-2 capitalised-
  name chase with `typeParamSubst[ctorName]!` recursive substitute and
  `modeTable.getDeclarationByTypeName` lookup.
- `bool _isCapitalized(String name)` (lines 272-276): private; tests
  `first == first.toUpperCase() && first != first.toLowerCase()` — the
  canonical Dart Unicode-uppercase-LETTER (not just caseless) idiom.
- `bool _typeContainsAtom(TypeDefinition typeDef, String atomName)` (lines
  278-294): private recursive — foreach over `AtomConstructor`s, direct-
  match return, capitalised-name recurse via `typeTable.getType`.
- `List<String> getValidConstructors(String typeName)` (lines 297-315):
  if/else-if chain over four `TypeConstructor` subtypes
  (`AtomConstructor` → `name`, `StructConstructor` → `'${functor}(...)'`,
  `ListConstructor` → `'[]'` or `'[...|...]'`, `TupleConstructor` →
  `'(...)'`); else-less fall-through to `return result;`.

Cross-file dependencies (verbatim from `import` statements):

- `package:glp_runtime/compiler/ast.dart` — `Term`, `VarTerm`,
  `UnderscoreTerm`, `ConstTerm`, `ListTerm`, `StructTerm`, `TypeConstructor`
  subtypes (`AtomConstructor`, `StructConstructor`, `ListConstructor`,
  `TupleConstructor`), `TypeDefinition`, `Module`, `Clause`, `Procedure`,
  `Goal`.
- `type_table.dart` — `TypeTable`, `TypeDefinition`.
- `mode_table.dart` — `ModeTable`, `ModeDeclaration`.

No `async`/`Future`/`Stream`/`isolate`/`late`/`Completer` — purely
synchronous tree-walk. Three load-bearing nuances mandated by the convspec:
(a) `\n` literal preserved (NOT `Environment.NewLine`); (b)
`StringComparer.Ordinal` mandatory at every `Dictionary<string, V>` site;
(c) permissive-default `return true` on unknown type preserved verbatim
(observable behaviour for external-type references).

## 2. Dart → C#/.NET Conversion Plan

Mirroring the ratified convspec construct-by-construct (sha256 matches).

### 2.1 `class TypeError` (convspec construct `dart.value_class.error_record_final_fields_message_line_column_nullable_suggestion_named_optional_tostring_override`)

Emit `sealed class TypeError` (NOT record, NOT exception). Three get-only
non-nullable auto-properties `Message: string`, `Line: int`, `Column: int`
plus nullable get-only `Suggestion: string?`. Single positional+optional-
named constructor `TypeError(string message, int line, int column, string?
suggestion = null)`. Override `ToString()` with two-branch shape: build
`var result = $"[type] {Message} at Line {Line}, Column {Column}";`, then
`if (Suggestion is not null) result += $"\n  Suggestion: {Suggestion}";`,
then return `result`. **`\n` literal stays `"\n"` (LF) — NEVER
`Environment.NewLine`.** **NO `IEquatable<TypeError>`** — Dart source did
not hand-write `==`/`hashCode`, so C# preserves default reference equality
(deliberate divergence from `errors.dart.md`'s `PmtError`).

### 2.2 `class TypeChecker` (convspec construct `dart.coordinator_class.two_constructor_fields_module_traversal_returning_error_list`)

Emit non-sealed `class TypeChecker`. Two `private readonly` fields populated
by a single positional constructor:

```
private readonly TypeTable _typeTable;
private readonly ModeTable _modeTable;
public TypeChecker(TypeTable typeTable, ModeTable modeTable)
{
    _typeTable = typeTable;
    _modeTable = modeTable;
}
```

Constructor parameter aliasing (NOT cloning) preserved. NOT `static`, NOT
`sealed`, NO `IEquatable<TypeChecker>`. Three public methods +
five internal helpers carry the per-method translations below.

### 2.3 `CheckModule(Module module): List<TypeError>` (convspec construct `dart.list_accumulator.errors_addAll_per_iteration_skip_when_null_lookup`)

```
public List<TypeError> CheckModule(Module module)
{
    var errors = new List<TypeError>();
    foreach (var proc in module.Procedures)
    {
        var modeDecl = _modeTable.GetDeclaration(proc.Name, proc.Arity);
        if (modeDecl is null) continue;
        foreach (var clause in proc.Clauses)
        {
            errors.AddRange(CheckClause(clause, modeDecl));
        }
    }
    return errors;
}
```

`<TypeError>[]` → `new List<TypeError>()` (fresh allocation per call, NOT
hoisted). `errors.addAll(other)` → `errors.AddRange(other)`. `modeDecl ==
null` → `modeDecl is null` (pattern-matching idiom per project corpus).
`continue` verbatim. Silent-skip on null is load-bearing — preserved.

### 2.4 `CheckClause(Clause clause, ModeDeclaration modeDecl): List<TypeError>` (convspec construct `dart.indexed_dual_list_for_loop.parallel_arg_iteration_with_min_length_guard` + `dart.nullable_collection_iteration.body_questionmark_bang_pattern`)

```
public List<TypeError> CheckClause(Clause clause, ModeDeclaration modeDecl)
{
    var errors = new List<TypeError>();
    for (int i = 0; i < clause.Head.Args.Count && i < modeDecl.Args.Count; i++)
    {
        var arg = clause.Head.Args[i];
        var declaredType = modeDecl.Args[i].TypeName;
        var typeParams = modeDecl.Args[i].TypeParams;
        errors.AddRange(CheckTerm(arg, declaredType, typeParams));
    }
    if (clause.Body is not null)
    {
        foreach (var goal in clause.Body)
        {
            var goalDecl = _modeTable.GetDeclaration(goal.Functor, goal.Arity);
            if (goalDecl is null) continue;
            for (int i = 0; i < goal.Args.Count && i < goalDecl.Args.Count; i++)
            {
                var arg = goal.Args[i];
                var declaredType = goalDecl.Args[i].TypeName;
                var typeParams = goalDecl.Args[i].TypeParams;
                errors.AddRange(CheckTerm(arg, declaredType, typeParams));
            }
        }
    }
    return errors;
}
```

`.length` → `.Count` (Dart `List<T>.length` → C# `List<T>.Count`; `.Length`
is reserved for arrays/strings/StringBuilder). Min-length-guard
`a.Count && b.Count` preserved verbatim (silent length-mismatch tolerance is
load-bearing). NO `Zip`/LINQ rewrite. **`clause.body!` Dart bang OMITTED**
under the C# `if (clause.Body is not null)` flow narrowing — C# NRT flow
analysis narrows `clause.Body` to `List<Goal>` inside the truthy branch;
emitting `clause.Body!` would be anti-idiomatic.

### 2.5 `CheckTerm(Term term, string expectedType, IReadOnlyList<string> typeParams): List<TypeError>` (convspec constructs `dart.is_type_test_chain.checkterm_dispatch_over_term_subtypes` + `dart.error_construction_with_optional_named_argument.suggestion_via_ternary_or_null` + `dart.checkterm_listterm_branch.element_type_resolution_via_substitution_then_recurse` + `dart.checkterm_structterm_branch.delegating_to_isvalidstructconstructor_with_error_emission`)

```
public List<TypeError> CheckTerm(Term term, string expectedType, IReadOnlyList<string> typeParams)
{
    var errors = new List<TypeError>();
    if (term is VarTerm) return errors;
    if (term is UnderscoreTerm) return errors;
    if (term is ConstTerm constTerm)
    {
        if (!IsValidConstant(constTerm, expectedType))
        {
            var validConstructors = GetValidConstructors(expectedType);
            errors.Add(new TypeError(
                $"'{constTerm.Value}' is not a valid '{expectedType}'",
                constTerm.Line,
                constTerm.Column,
                suggestion: validConstructors.Count > 0
                    ? $"Valid constructors: {string.Join(", ", validConstructors)}"
                    : null));
        }
        return errors;
    }
    if (term is ListTerm listTerm)
    {
        if (listTerm.IsNil) return errors;
        var typeDef = _typeTable.LookupType(expectedType);
        if (typeDef is not null)
        {
            bool hasListCtor = typeDef.Constructors.Any(c => c is ListConstructor);
            if (!hasListCtor && expectedType != "List")
            {
                errors.Add(new TypeError(
                    $"List term not valid for type '{expectedType}'",
                    listTerm.Line,
                    listTerm.Column));
                return errors;
            }
            var typeParamSubst = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < typeDef.TypeParams.Count && i < typeParams.Count; i++)
            {
                typeParamSubst[typeDef.TypeParams[i]] = typeParams[i];
            }
            var listCtor = typeDef.Constructors
                .OfType<ListConstructor>()
                .FirstOrDefault(c => !c.IsNil);
            if (listCtor is not null && listCtor.Head is not null && listTerm.Head is not null)
            {
                var paramTypeName = listCtor.Head.TypeName;
                var elementType = typeParamSubst.TryGetValue(paramTypeName, out var substituted)
                    ? substituted
                    : paramTypeName;
                if (elementType != "_")
                {
                    errors.AddRange(CheckTerm(listTerm.Head, elementType, Array.Empty<string>()));
                }
            }
        }
        else if (typeParams.Count > 0 && listTerm.Head is not null)
        {
            errors.AddRange(CheckTerm(listTerm.Head, typeParams[0], Array.Empty<string>()));
        }
        if (listTerm.Tail is not null)
        {
            errors.AddRange(CheckTerm(listTerm.Tail, expectedType, typeParams));
        }
        return errors;
    }
    if (term is StructTerm structTerm)
    {
        if (!IsValidStructConstructor(structTerm, expectedType, typeParams))
        {
            var validConstructors = GetValidConstructors(expectedType);
            errors.Add(new TypeError(
                $"'{structTerm.Functor}(...)' is not a valid '{expectedType}'",
                structTerm.Line,
                structTerm.Column,
                suggestion: validConstructors.Count > 0
                    ? $"Valid constructors: {string.Join(", ", validConstructors)}"
                    : null));
        }
        return errors;
    }
    return errors;
}
```

Key idiom translations (all from convspec, all backed by recorded rf-ids):

- `if (term is X) { ... use term as X ...}` Dart smart-cast → C#
  `if (term is X xBinding) { ... use xBinding ... }` declaration pattern
  (rf-id `rf-dart-is-test-smart-cast-to-csharp-declaration-pattern`).
- `term.head!`, `term.tail!` Dart bangs OMITTED — flow-narrowed under
  `is not null` guards.
- `list.join(', ')` → `string.Join(", ", list)` — **separator-position
  swap** (rf-id `rf-dart-list-join-to-csharp-string-join-separator-first`).
- `validConstructors.isNotEmpty` → `validConstructors.Count > 0`.
- `<String, String>{}` map literal → `new Dictionary<string, string>(
  StringComparer.Ordinal)` — explicit ordinal comparer mandatory.
- `whereType<ListConstructor>().where(p).firstOrNull` → `.OfType<
  ListConstructor>().FirstOrDefault(p)` — two-step Dart chain collapses to
  one-step C# chain (rf-id `rf-dart-wheretype-firstornull-chain-to-csharp-
  oftype-firstordefault`). `OfType` (skips mismatches) NOT `Cast` (throws).
- `map[k] ?? k` (Dart read-with-key-fallback) → `map.TryGetValue(k, out
  var v) ? v : k` — direct `map[k] ?? k` in C# would THROW
  `KeyNotFoundException` before `??` evaluated.
- `[]` empty list of strings in argument position → `Array.Empty<string>()`
  (cached zero-allocation singleton; callee only iterates).
- The trailing `return errors;` (line 181) is preserved as the catch-all for
  any unforeseen `Term` subtype — DO NOT mark `Term` C# class `sealed` here.

### 2.6 `IsValidConstant(ConstTerm term, string typeName): bool` (convspec construct `dart.predicate_method.isvalidconstant_builtin_then_userdefined_then_type_reference`)

```
public bool IsValidConstant(ConstTerm term, string typeName)
{
    if (typeName is "Num" or "Number" or "Int" or "Integer")
        return term.Value is double or float or int or long or short or byte or decimal;
    if (typeName is "Atom" or "String")
        return term.Value is string;
    var typeDef = _typeTable.LookupType(typeName);
    if (typeDef is null) return true;
    var termValue = term.Value?.ToString() ?? "";
    foreach (var ctor in typeDef.Constructors)
    {
        if (ctor is AtomConstructor atomCtor && atomCtor.Name == termValue)
            return true;
    }
    foreach (var ctor in typeDef.Constructors)
    {
        if (ctor is AtomConstructor atomCtor && _IsCapitalized(atomCtor.Name))
        {
            var refTypeDef = _typeTable.LookupType(atomCtor.Name);
            if (refTypeDef is not null && _TypeContainsAtom(refTypeDef, termValue))
                return true;
        }
    }
    return false;
}
```

- Dart `term.value is num` (Dart `num` = closed `int | double`) → C#
  disjunctive type pattern `term.Value is double or float or int or long or
  short or byte or decimal` (broader, defensive; per convspec rf-id
  `rf-dart-numeric-supertype-is-num-to-csharp-disjunctive-type-pattern`).
- `term.value.toString()` → `term.Value?.ToString() ?? ""` (Dart `Object
  .toString()` defined on null returns `"null"`; C# `obj?.ToString() ?? ""`
  returns `""`. Convspec records this as a **benign acknowledged divergence
  — NOT an escalation**; parser invariant makes a null `ConstTerm.value`
  unreachable in this code path).
- `if (typeDef == null) return true;` permissive-default preserved verbatim
  (load-bearing — "unknown type ⇒ allow"; doc-comment at source line
  197-198 is canonical).
- `foreach` + declaration-pattern + early-exit-on-first-match.

### 2.7 `IsValidStructConstructor(StructTerm term, string typeName, IReadOnlyList<string>? typeParams = null): bool` (convspec construct `dart.recursive_predicate_method.isvalidstructconstructor_with_typeparam_substitution`)

```
public bool IsValidStructConstructor(StructTerm term, string typeName, IReadOnlyList<string>? typeParams = null)
{
    var effectiveParams = typeParams ?? Array.Empty<string>();
    var typeDef = _typeTable.LookupType(typeName);
    if (typeDef is null) return true;
    var typeParamSubst = new Dictionary<string, string>(StringComparer.Ordinal);
    for (int i = 0; i < typeDef.TypeParams.Count && i < effectiveParams.Count; i++)
    {
        typeParamSubst[typeDef.TypeParams[i]] = effectiveParams[i];
    }
    foreach (var ctor in typeDef.Constructors)
    {
        if (ctor is StructConstructor structCtor && structCtor.Functor == term.Functor)
            return true;
    }
    foreach (var ctor in typeDef.Constructors)
    {
        if (ctor is AtomConstructor atomCtor && _IsCapitalized(atomCtor.Name))
        {
            var ctorName = atomCtor.Name;
            if (typeParamSubst.TryGetValue(ctorName, out var substitutedType))
            {
                if (IsValidStructConstructor(term, substitutedType))
                    return true;
            }
            var modeDecl = _modeTable.GetDeclarationByTypeName(ctorName);
            if (modeDecl is not null && modeDecl.Predicate == term.Functor)
                return true;
        }
    }
    return false;
}
```

- Dart `[List<String> typeParams = const []]` optional-positional →
  C# `IReadOnlyList<string>? typeParams = null` with `typeParams ?? Array
  .Empty<string>()` runtime coalesce (per convspec rf-id `rf-dart-optional-
  positional-default-const-empty-list-to-csharp-nullable-with-coalesce`).
  `new List<string>()` is NOT a compile-time constant; `null` IS.
- `typeParamSubst[ctorName]!` Dart force-unwrap → C# `TryGetValue(out var
  substitutedType)` with recursive call inside the truthy branch — bang
  eliminated entirely.
- Recursive `IsValidStructConstructor(term, substitutedType)` omits third
  arg, relying on the `typeParams = null` default — preserves Dart's
  reliance on `const []` default at the corresponding Dart site.
- `_modeTable.GetDeclarationByTypeName(ctorName)` returns
  `ModeDeclaration?` per mode_table.dart convspec; C# return-type
  matches under NRT.

### 2.8 `_IsCapitalized(string name): bool` (convspec construct `dart.private_helper_predicate.iscapitalized_via_first_char_case_test`)

```
private bool _IsCapitalized(string name)
{
    if (name.Length == 0) return false;
    var first = name[0];
    return char.IsUpper(first);
}
```

Dart's two-condition `first == first.toUpperCase() && first != first
.toLowerCase()` → C# `char.IsUpper(first)` (direct semantic match: Unicode-
category `UppercaseLetter`/`Lu`-aware; returns false for digits / caseless
symbols, matching Dart's idiom exactly per convspec rf-id
`rf-dart-uppercase-letter-test-to-csharp-char-isupper`). Leading underscore
retained per `errors.dart` / `mode_table.dart` private-helper convention.

### 2.9 `_TypeContainsAtom(TypeDefinition typeDef, string atomName): bool` (convspec construct `dart.recursive_search_predicate.typecontainsatom_visiting_capitalized_atomctors`)

```
private bool _TypeContainsAtom(TypeDefinition typeDef, string atomName)
{
    foreach (var ctor in typeDef.Constructors)
    {
        if (ctor is AtomConstructor atomCtor)
        {
            if (atomCtor.Name == atomName) return true;
            if (_IsCapitalized(atomCtor.Name))
            {
                var refType = _typeTable.LookupType(atomCtor.Name);
                if (refType is not null && _TypeContainsAtom(refType, atomName))
                    return true;
            }
        }
    }
    return false;
}
```

Direct foreach + declaration-pattern + recursive descent. **Recursion
termination depends on `TypeTable` being acyclic** (no cycle detection;
Dart source has none; C# port preserves the parser-side invariant per
convspec Notes "Recursion termination caveat"). Codegen may annotate with
`// acyclic-TypeTable invariant` at the recursion call site.

### 2.10 `GetValidConstructors(string typeName): List<string>` (convspec construct `dart.formatter_method.getvalidconstructors_walks_typedef_emitting_displays`)

```
public List<string> GetValidConstructors(string typeName)
{
    var result = new List<string>();
    var typeDef = _typeTable.LookupType(typeName);
    if (typeDef is null) return result;
    foreach (var ctor in typeDef.Constructors)
    {
        switch (ctor)
        {
            case AtomConstructor atomCtor:
                result.Add(atomCtor.Name);
                break;
            case StructConstructor structCtor:
                result.Add($"{structCtor.Functor}(...)");
                break;
            case ListConstructor listCtor:
                result.Add(listCtor.IsNil ? "[]" : "[...|...]");
                break;
            case TupleConstructor:
                result.Add("(...)");
                break;
        }
    }
    return result;
}
```

Dart if/else-if chain over four `TypeConstructor` subtypes → C# `switch
(ctor)` with type-pattern cases (idiomatic for 4+ cases per convspec rf-id
`rf-dart-is-else-chain-to-csharp-switch-with-type-patterns`). **NO
`default:` arm** — preserves Dart's silent-skip-on-unknown-subtype
semantic. `TupleConstructor` case has no binding (body doesn't reference
members). Observable error-formatting tokens (`""`, `"[]"`, `"[...|...]"`,
`"(...)"`) preserved verbatim.

### 2.11 Cross-cutting C# project conventions (all derived from convspec)

- **Newline literal**: `\n` (U+000A LF) — NEVER `Environment.NewLine`. One
  site (`TypeError.ToString`).
- **String equality discipline**: bare `==` on string is ordinal-by-convention
  (project-wide); explicit `StringComparer.Ordinal` mandatory at every
  `Dictionary<string, V>` construction (two sites — `CheckTerm.ListTerm`
  branch and `IsValidStructConstructor`).
- **Null-pattern idiom**: `obj is null` / `obj is not null` everywhere
  (NOT `== null` / `!= null`). One exception: bare `==` on string is
  acceptable per the established corpus convention.
- **NRT annotations**: enabled. `Module`, `Clause.Body`, `ModeDeclaration?`,
  `TypeDefinition?`, etc., all annotated per source nullable contracts.
- **`using System.Linq;`** required for `Any`, `OfType`, `FirstOrDefault`.
  **`using System.Collections.Generic;`** for `List<T>`, `Dictionary<K, V>`,
  `IReadOnlyList<T>`. **`using System;`** for `Array.Empty<T>`,
  `StringComparer.Ordinal`, `char.IsUpper`.
- **No defensive copies**: tables (`_typeTable`, `_modeTable`) are shared
  references with the caller — preserved verbatim.
- **No async/Stream/Future/isolate concerns** — synchronous tree-walk.

## 3. Decomposed Task Units

- T1. Define `sealed class TypeError` with three non-nullable + one nullable
  get-only properties, positional+optional-named constructor, `ToString()`
  override with literal `"\n"`, no `IEquatable<>`.
- T2. Define `class TypeChecker` with `private readonly` `_typeTable` /
  `_modeTable` fields and positional constructor.
- T3. Implement `CheckModule` with outer foreach over `Procedures`, silent-
  skip on null `modeDecl`, inner foreach over `Clauses`, `errors.AddRange`.
- T4. Implement `CheckClause` with two indexed-for-with-min-length-guards
  and flow-narrowed `if (clause.Body is not null)` foreach over `Body`.
- T5. Implement `CheckTerm` five-arm dispatch with declaration-pattern
  bindings (`VarTerm`/`UnderscoreTerm` trivial-return; `ConstTerm` /
  `StructTerm` formatted-error branches; `ListTerm` five-sub-path branch).
- T6. Implement `ListTerm` sub-path: `OfType<ListConstructor>().FirstOrDefault
  (c => !c.IsNil)`, `Dictionary<string, string>(StringComparer.Ordinal)`,
  `TryGetValue(...) ? substituted : paramTypeName`, `Array.Empty<string>()`
  for recursive `typeParams` arg.
- T7. Implement `IsValidConstant` four-phase with disjunctive type pattern
  for numerics, null-safe `term.Value?.ToString() ?? ""`.
- T8. Implement `IsValidStructConstructor` with `IReadOnlyList<string>?
  typeParams = null` + `?? Array.Empty<string>()`, two-pass foreach,
  `TryGetValue` recursive substitute, mode-table lookup.
- T9. Implement `_IsCapitalized` via `char.IsUpper`.
- T10. Implement `_TypeContainsAtom` recursive foreach with acyclic-
  TypeTable invariant comment.
- T11. Implement `GetValidConstructors` with `switch (ctor)` over four
  type-pattern cases, no `default:` arm, verbatim formatting tokens.

## 4. Research Findings

None required. The convspec is fully ratified with twelve new rf-ids
backed by official .NET (Microsoft Learn) + Dart (dart.dev/api.dart.dev)
documentation, plus six cached rf-ids reused from `mode_table.dart.md`,
`type_table.dart.md`, `errors.dart.md`. Every construct in §2 derives
verbatim from a convspec construct with matching source_form and
target_decision. No WebSearch / WebFetch / Agent invocations needed; no
gaps requiring escalation.

## 5. Consistency Pass

- §2.1 (`TypeError`): fixed — derived from convspec construct
  `dart.value_class.error_record_final_fields_message_line_column_nullable_suggestion_named_optional_tostring_override`
  + rf-id `rf-dart-error-class-no-equality-override-to-csharp-sealed-class`.
- §2.2 (`TypeChecker`): fixed — derived from convspec construct
  `dart.coordinator_class.two_constructor_fields_module_traversal_returning_error_list`
  + rf-id `rf-dart-coordinator-class-with-final-deps-to-csharp-class-with-readonly-fields`.
- §2.3 (`CheckModule`): fixed — derived from convspec construct
  `dart.list_accumulator.errors_addAll_per_iteration_skip_when_null_lookup`
  + rf-id `rf-dart-list-typed-literal-and-addall-to-csharp-list-and-addrange`.
- §2.4 (`CheckClause`): fixed — derived from convspec constructs
  `dart.indexed_dual_list_for_loop.parallel_arg_iteration_with_min_length_guard`
  + `dart.nullable_collection_iteration.body_questionmark_bang_pattern`
  + rf-ids `rf-dart-indexed-min-length-for-loop-to-csharp-imperative-for`
  + `rf-dart-nullable-bang-inside-null-check-to-csharp-flow-narrowed-access`.
- §2.5 (`CheckTerm`): fixed — derived from convspec constructs
  `dart.is_type_test_chain.checkterm_dispatch_over_term_subtypes`
  + `dart.error_construction_with_optional_named_argument.suggestion_via_ternary_or_null`
  + `dart.checkterm_listterm_branch.element_type_resolution_via_substitution_then_recurse`
  + `dart.checkterm_structterm_branch.delegating_to_isvalidstructconstructor_with_error_emission`
  + rf-ids `rf-dart-is-test-smart-cast-to-csharp-declaration-pattern`,
  `rf-dart-list-join-to-csharp-string-join-separator-first`,
  `rf-dart-wheretype-firstornull-chain-to-csharp-oftype-firstordefault`.
- §2.6 (`IsValidConstant`): fixed — derived from convspec construct
  `dart.predicate_method.isvalidconstant_builtin_then_userdefined_then_type_reference`
  + rf-id `rf-dart-numeric-supertype-is-num-to-csharp-disjunctive-type-pattern`.
- §2.7 (`IsValidStructConstructor`): fixed — derived from convspec construct
  `dart.recursive_predicate_method.isvalidstructconstructor_with_typeparam_substitution`
  + rf-id `rf-dart-optional-positional-default-const-empty-list-to-csharp-nullable-with-coalesce`.
- §2.8 (`_IsCapitalized`): fixed — derived from convspec construct
  `dart.private_helper_predicate.iscapitalized_via_first_char_case_test`
  + rf-id `rf-dart-uppercase-letter-test-to-csharp-char-isupper`.
- §2.9 (`_TypeContainsAtom`): fixed — derived from convspec construct
  `dart.recursive_search_predicate.typecontainsatom_visiting_capitalized_atomctors`
  (reuses rf-id `rf-dart-uppercase-letter-test-to-csharp-char-isupper`).
- §2.10 (`GetValidConstructors`): fixed — derived from convspec construct
  `dart.formatter_method.getvalidconstructors_walks_typedef_emitting_displays`
  + rf-id `rf-dart-is-else-chain-to-csharp-switch-with-type-patterns`.
- §2.11 (cross-cutting conventions): fixed — derived from convspec Notes
  section (Newline portability; String-equality discipline; Permissive-
  default semantics preserved; Silent-tolerance semantics preserved;
  Recursion termination caveat) + cached rf-ids `rf-csharp-string-equality-
  ordinal-by-default`, `rf-csharp-interpolated-string-equivalent-to-dart-
  interpolation`, `rf-csharp-dictionary-trygetvalue-then-fallback-null`,
  `rf-dart-length-isempty-to-csharp-count`, `rf-dart-named-default-param-
  to-csharp-optional-arg`, `rf-dart-stringbuffer-to-csharp-stringbuilder`.

All eleven §2 sub-sections derive verbatim from the ratified convspec — no
inference, no addition, no escalation.

## 6. Escalations

None.
