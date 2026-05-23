---
path: lib/analysis/type_checker/param_expansion.dart
cycle_group_id: 2
scc_siblings: []
generated_at: 2026-05-21T14:59:24Z
source_sha256: c716e6969f9947cf137f59e5a597ce359d062829a4a3c6f810b76d263c83a64c
schema_version: 1
---

# Conversion Plan: lib/analysis/type_checker/param_expansion.dart

## 1. Source Analysis

The file is a single-purpose, pure functional module that mono-morphises
parameterised type templates in a GLP `Module` AST. It exports exactly one
public top-level driver, `expandParameterizedTypes`, surrounded by ten
private helpers. There is no mutable shared state; the input `Module` is
never mutated; the output is a freshly constructed `Module`.

Verbatim inspection (lines correspond to the source file as read at the
sha pinned in the front matter):

- **Header / imports (lines 1-10)**: `// lib/analysis/type_checker/param_expansion.dart` plus a 5-line doc-block citing
  `docs/type system/typed-program.md`, section "Parameterized Types" and the
  paper's "Section 8, Definition 8.1". Imports `../../compiler/ast.dart`
  prefixed `as ast` and `type_ast.dart` un-prefixed.
- **Public driver `expandParameterizedTypes` (lines 12-204)** — single
  named-argument-bearing top-level function returning a new `ast.Module`.
  Two named optional parameters: `Set<String> knownTypeNames = const {}` and
  `Map<String, TypeDef> externalTemplates = const {}`. Body is a five-step
  imperative pipeline over five local mutable accumulators (`templates`,
  `monoTypeDefs`, `instantiations`, `expandedDefs`, `expanded`):
  - **Step 1 (lines 21-37)**: partition `module.typeDefs` into `templates`
    (parameterised) vs `monoTypeDefs` (monomorphic); merge
    `externalTemplates` via `putIfAbsent` (first-writer-wins, "Local
    templates take precedence over external ones").
  - **Step 1.5 (lines 39-47)**: build `monoNames` set as the union of
    `monoTypeDefs.map(td => td.name)` and `knownTypeNames` via set-literal
    spread; note the deliberate non-early-return on empty templates.
  - **Step 2 (lines 49-83)**: walk `monoTypeDefs.alternatives`,
    `module.procDeclarations.argTypes` (with branching on
    `_detectProcTypeParams`), and `templates.values.alternatives` to collect
    instantiation requests into `instantiations`.
  - **Step 3 (lines 85-117)**: while-worklist loop expanding each
    `instantiation` entry into a new `TypeDef` via `_substituteTypeExpr`
    then `_replaceParamRefs`. Uses `Map.of(instantiations).entries` defensive
    snapshot to permit mid-loop extension.
  - **Step 4 (lines 119-125)**: rewrite `monoTypeDefs` alternatives via
    `_replaceParamRefs`, accumulating into `replacedTypeDefs`.
  - **Step 5 (lines 127-164)**: process each `ProcDecl`. Parameterised
    proc-decls produce BOTH (a) a preserved-template entry in
    `paramProcDeclTemplates` (for call-site inference) AND (b) a
    wildcard-substituted concrete entry in `replacedProcDecls`
    (substitution = each type-param → `PrimitiveModeAlt(false, 0, 0)`).
    Non-parameterised proc-decls take the simple `_replaceParamRefs` path.
  - **Step 5b worklist re-run (lines 166-192)**: identical body to Step 3,
    re-executed to expand any new instantiations created by the wildcard
    substitution in Step 5. **This second pass is load-bearing.**
  - **Return (lines 194-203)**: `ast.Module(declaration: ...,
    typeDefs: [...replacedTypeDefs, ...expandedDefs], procDeclarations:
    replacedProcDecls, paramProcDecls: paramProcDeclTemplates, procedures:
    module.procedures, compileMode: ..., line: ..., column: ...)`.
- **`_detectProcTypeParams` (lines 206-228)** — returns the list of
  proc-decl type-parameter names. Builds a `knownTypes` HashSet from five
  sources (`templates.keys`, `monoTypeDefs.name`, `TypeRef.builtins`,
  `externalKnownTypes`, `'Constant'`); then walks each `pd.argTypes` to
  collect bare-inner-unknown candidates via
  `_collectInnerTypeParamCandidates`.
- **`_collectInnerTypeParamCandidates` (lines 232-262)** — recursive walker
  with arms for `TypeRef`, `StructAlt`, `ListConsAlt`, `DiffListAlt`. Only
  collects when an `arg` is a bare TypeRef (no type-args) appearing inside
  another TypeRef's `typeArgs` and not in `knownTypes`.
- **`_expandedName` (lines 265-268)** — single-line `String` interpolation
  building `"Template<arg1,arg2,...>"` via `Iterable.map.join(',')` over
  `_typeExprToCanonical`.
- **`_typeExprToCanonical` (lines 272-281)** — recursive helper that
  produces canonical strings; emits `"?"` suffix when `expr.isInput`.
- **`_templateNameFromExpanded` (lines 284-288)** — extracts the substring
  before the first `'<'`; returns the whole name if no `<` present.
- **`_isTemplateRef` (lines 292-297)** — predicate: TypeRef has `typeArgs`,
  the name resolves in `templates`, and arity matches.
- **`_collectInstantiations` (lines 300-330)** — recursive walker;
  TypeRef arm records each `_isTemplateRef` via `putIfAbsent`; recurses
  into `typeArgs` (twice when template — once for self, once for nested);
  structural arms recurse via `StructAlt.args` / `ListConsAlt.head/tail`
  / `DiffListAlt.content/hole`.
- **`_collectInstantiationsInTemplate` (lines 336-370)** — variant of
  the above that takes a `templateParams` list and skips bare-self-recursive
  references using `Iterable.every`.
- **`_substituteTypeExpr` (lines 375-429)** — recursive substitution.
  TypeRef arm: (a) if `expr.name` is in `substitution` and `expr.typeArgs`
  is empty → replace; preserve `isInput` by re-wrapping if the replacement
  is itself a TypeRef or PrimitiveModeAlt. (b) if `_isTemplateRef` →
  recursively substitute args; if `allWildcards && monoNames.contains
  (expr.name)` collapse to the bare name; otherwise emit
  `TypeRef(expandedName, ...)` and record the new instantiation.
  Structural arms recurse mechanically.
- **`_replaceParamRefs` (lines 433-476)** — recursive replacement of
  template TypeRefs by their expanded canonical names. Same wildcard-
  collapse rule as `_substituteTypeExpr`. Structural arms recurse.

The file imports use of these external types: from `compiler/ast.dart`
(prefixed `ast`): `Module`. From `type_ast.dart` (un-prefixed): `TypeDef`,
`TypeExpr`, `TypeRef`, `StructAlt`, `ListConsAlt`, `DiffListAlt`,
`PrimitiveModeAlt`, `ProcDecl`. Each carries constructor signatures and
properties the conversion target depends on.

## 2. Dart → C#/.NET Conversion Plan

The plan below mirrors the convspec construct-by-construct. Every
`→` (U+2192) below points from the Dart source form to the chosen
C#/.NET form, with the convspec's nuance preserved verbatim.

**Host shell.** All members live inside

```
namespace Glp.Analysis.TypeChecker
{
    public static class ParamExpansion { ... }
}
```

per the cached `dart-toplevel-fn-to-csharp-static-method` idiom.

**Construct 1 — public driver signature** (`dart.toplevel_public_driver_fn_with_named_const_default_collections`):

`ast.Module expandParameterizedTypes(ast.Module module, { Set<String> knownTypeNames = const {}, Map<String, TypeDef> externalTemplates = const {} })`
→
`public static Module ExpandParameterizedTypes(Module module, IReadOnlySet<string>? knownTypeNames = null, IReadOnlyDictionary<string, TypeDef>? externalTemplates = null)`,
normalising on entry: `knownTypeNames ??= ImmutableHashSet<string>.Empty;` and
`externalTemplates ??= ImmutableDictionary<string, TypeDef>.Empty;`. Pure
function — `module` never mutated, output freshly constructed.

**Construct 2 — five-step pipeline** (`dart.algorithmic_driver.five_step_imperative_pipeline_local_mutation`):

Body preserves all five steps verbatim. Local accumulators:
- `final templates = <String, TypeDef>{};` → `var templates = new Dictionary<string, TypeDef>(StringComparer.Ordinal);`
- `final monoTypeDefs = <TypeDef>[];` → `var monoTypeDefs = new List<TypeDef>();`
- `final instantiations = <String, List<TypeExpr>>{};` → `var instantiations = new Dictionary<string, IReadOnlyList<TypeExpr>>(StringComparer.Ordinal);`
- `final expandedDefs = <TypeDef>[];` → `var expandedDefs = new List<TypeDef>();`
- `final expanded = <String>{};` → `var expanded = new HashSet<string>(StringComparer.Ordinal);`

Worklist: `while (instantiations.length > expanded.length)` →
`while (instantiations.Count > expanded.Count)`. **Both worklist
passes (lines 89-117 AND lines 167-192) are emitted verbatim** — the
second pass is required to expand wildcard-instantiated proc-decl
instantiations.

**Construct 3 — first-writer-wins map merge** (`dart.dictionary.putifabsent_lambda_first_writer_wins`):

`templates.putIfAbsent(entry.key, () => entry.value)` →
`templates.TryAdd(entry.Key, entry.Value)` (per Microsoft Learn:
`TryAdd` returns true when added, false when key already present —
exactly Dart `putIfAbsent`'s "first-writer-wins" semantics).
`instantiations.putIfAbsent(name, () => expr.typeArgs)` →
`instantiations.TryAdd(name, expr.TypeArgs)`. Do NOT use `Add` (throws
on duplicate) or `dict[k] = v` (last-writer-wins).

**Construct 4 — recursive AST walkers via type-pattern switch**
(`dart.recursive_ast_walker.is_typecheck_dispatch_with_template_param_awareness`):

Every Dart `if (expr is X) { ... }` chain becomes a single C# switch:

```csharp
switch (expr)
{
    case TypeRef r: /* ... */ break;
    case StructAlt s: /* recurse over s.Args */ break;
    case ListConsAlt c: /* recurse over c.Head/c.Tail */ break;
    case DiffListAlt d: /* recurse over d.Content/d.Hole */ break;
    default: break;
}
```

Dart's `return` after the TypeRef arm becomes `break;` (the C# switch
is naturally mutually exclusive; observable behaviour identical).
Applies to `_collectInnerTypeParamCandidates`, `_collectInstantiations`,
`_collectInstantiationsInTemplate`, `_substituteTypeExpr`,
`_replaceParamRefs`.

**Construct 5 — canonical-name string helpers**
(`dart.canonical_name_construction.string_interpolation_with_join_brackets`):

- `_expandedName` →
  `private static string ExpandedName(string templateName, IReadOnlyList<TypeExpr> typeArgs) => $"{templateName}<{string.Join(",", typeArgs.Select(TypeExprToCanonical))}>";`
- `_typeExprToCanonical` →
  `private static string TypeExprToCanonical(TypeExpr expr) => expr switch { TypeRef { TypeArgs.Count: > 0 } r => $"{r.Name}<{string.Join(",", r.TypeArgs.Select(TypeExprToCanonical))}>{(r.IsInput ? "?" : "")}", _ => expr.ToString() };`
- `_templateNameFromExpanded` →
  `private static string TemplateNameFromExpanded(string expandedName) { var idx = expandedName.IndexOf('<'); return idx < 0 ? expandedName : expandedName.Substring(0, idx); }`

**Critical**: pass `'<'` (char literal) to `IndexOf` — the `char`
overload is ordinal; `IndexOf(string)` defaults to current culture.

**Construct 6 — `.map().toList()` pipelines**
(`dart.collection.list_select_to_list_two_step_pipeline`):

`list.map(f).toList()` → `list.Select(f).ToList()`. **`.ToList()` is a
correctness requirement** — Microsoft Learn documents `Select` as
deferred-execution; without `.ToList()` subsequent walks would
re-execute the projection. Two-step pipelines (substitute → replace)
keep BOTH materialisations.

**Construct 7 — `Map.fromIterables` zip**
(`dart.dictionary.fromIterables_zip_two_lists_into_map`):

`Map<String, TypeExpr>.fromIterables(template.typeParams, typeArgs)` →
`template.TypeParams.Zip(typeArgs, (k, v) => new KeyValuePair<string, TypeExpr>(k, v)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal)`.
The Dart-throws-vs-C#-truncates divergence is dead-code thanks to the
explicit arity guard immediately above each call site (lines 102-103,
178-179). Preserve the guard verbatim.

**Construct 8 — set-literal union via spread**
(`dart.collection_literal.set_with_spread_union_of_iterables`):

`<String>{...a, ...b}` → `var monoNames = new HashSet<string>(StringComparer.Ordinal); foreach (var td in monoTypeDefs) monoNames.Add(td.Name); monoNames.UnionWith(knownTypeNames);`.
For the multi-source `knownTypes` set in `_detectProcTypeParams`,
chain `UnionWith` for `templates.Keys`, `monoTypeDefs.Select(td => td.Name)`,
`TypeRef.Builtins`, `externalKnownTypes`, then `.Add("Constant")`.

**Construct 9 — universal-quantifier short-circuit**
(`dart.collection.iterable_every_universal_quantifier_short_circuit`):

`iter.every(p)` → `iter.All(p)`. Empty-list vacuous truth preserved by
both (Microsoft Learn). Compound predicates fuse via declaration
patterns: `arg is TypeRef r && r.TypeArgs.Count == 0 && templateParams.Contains(r.Name)`.

**Construct 10 — AST-node construction with named args**
(`dart.ast_node.construct_typeref_with_named_args_preserving_isinput`):

Dart `Class(pos1, pos2, named1: v1, named2: v2)` → C# `new Class(pos1, pos2, named1: v1, named2: v2)`.
C# requires `new`; named arguments use the identical `name: value`
syntax. **Cross-file constraint**: the consuming convspecs
(`type_ast.dart`, `ast.dart`) must emit constructor parameter names
matching the Dart source labels verbatim — the family settled on
lowerCamel parameter names in C# call sites (per
clause_validation.dart's `phase:` precedent). `isInput: expr.isInput`
property-carry-forward is load-bearing for `TypeRef` equality (per
type_ast.dart spec).

**Construct 11 — Module return with list spread**
(`dart.return_record_via_module_constructor_with_spread_lists`):

`ast.Module(declaration: ..., typeDefs: [...replacedTypeDefs, ...expandedDefs], ...)` →
`new Module(declaration: module.Declaration, typeDefs: replacedTypeDefs.Concat(expandedDefs).ToList(), procDeclarations: replacedProcDecls, paramProcDecls: paramProcDeclTemplates, procedures: module.Procedures, compileMode: module.CompileMode, line: module.Line, column: module.Column);`.
Concat is deferred — `.ToList()` mandatory. `module.Procedures` passes
through as a reference alias (NOT cloned) — preserve the alias.

**Doc-comments**: `///` triple-slash blocks port 1-for-1 to C# `///`
XML-doc summary blocks — Dart and C# triple-slash semantics are
identical.

**Imports**: `import '../../compiler/ast.dart' as ast;` +
`import 'type_ast.dart';` → `using Glp.Compiler;` +
`using Glp.Analysis.TypeChecker;` (cross-file concern; the `ast.`
prefix in Dart becomes a no-op once `Module` is resolved through the
namespace). No type-name collision is anticipated because the
type_ast.dart convspec lifts `TypeDef`, `TypeRef`, etc. into a
distinct namespace from `ast.Module`.

## 3. Decomposed Task Units

- **T1** Create file `out/csharp/lib/analysis/type_checker/param_expansion.cs` with namespace `Glp.Analysis.TypeChecker`, `using` directives (`System`, `System.Collections.Generic`, `System.Collections.Immutable`, `System.Linq`, `Glp.Compiler`), and shell `public static class ParamExpansion { }`.
- **T2** Port file-header `//` block + spec/paper citation comments verbatim.
- **T3** Emit `public static Module ExpandParameterizedTypes(Module module, IReadOnlySet<string>? knownTypeNames = null, IReadOnlyDictionary<string, TypeDef>? externalTemplates = null)` with `??=` ImmutableHashSet/Dictionary.Empty normalisation; port the `///` doc-comment as `/// <summary>` XML-doc.
- **T4** Step 1 block: declare `templates` (Dictionary, StringComparer.Ordinal) and `monoTypeDefs` (List); `foreach (var td in module.TypeDefs) { if (td.IsParameterized) templates[td.Name] = td; else monoTypeDefs.Add(td); }`.
- **T5** External-template merge loop: `foreach (var entry in externalTemplates) templates.TryAdd(entry.Key, entry.Value);` (first-writer-wins via TryAdd).
- **T6** Preserve the non-early-return comment verbatim as a C# `//` comment ("don't return early if templates is empty…").
- **T7** Step 1.5 monoNames set: `var monoNames = new HashSet<string>(StringComparer.Ordinal); foreach (var td in monoTypeDefs) monoNames.Add(td.Name); monoNames.UnionWith(knownTypeNames);`.
- **T8** Step 2 instantiations dictionary declaration + scanning loops over `monoTypeDefs` alternatives, `module.ProcDeclarations` (with the per-pd branch on `procTypeParams.Count`), and `templates.Values`.
- **T9** Step 3 worklist: `var expandedDefs = new List<TypeDef>(); var expanded = new HashSet<string>(StringComparer.Ordinal); while (instantiations.Count > expanded.Count) { foreach (var entry in instantiations.ToList()) { ... } }`. Inside: arity guard, `Zip+ToDictionary` substitution build, two-step `Select(...).ToList()` pipeline (substitute → replaceParamRefs), `expandedDefs.Add(new TypeDef(...))`, `expanded.Add(expandedName)`.
- **T10** Step 4 `replacedTypeDefs`: `monoTypeDefs.Select(td => new TypeDef(td.Name, td.Alternatives.Select(alt => ReplaceParamRefs(alt, templates, monoNames: monoNames)).ToList(), td.Line, td.Column)).ToList()`.
- **T11** Step 5 proc-decl rewrite loop: declare `replacedProcDecls` + `paramProcDeclTemplates` Lists; foreach `pd` in `module.ProcDeclarations`; compute `procTypeParams`; branch: if non-empty, build `paramTemplate` `ProcDecl` + wildcard substitution dict (`{ for (var tp in procTypeParams) tp: new PrimitiveModeAlt(false, 0, 0) }`) + `wildcardArgTypes` via two-step Select+ToList; else simple `_replaceParamRefs` mapping. Both branches append to `replacedProcDecls`; parameterised branch also appends to `paramProcDeclTemplates`.
- **T12** Step 5b worklist re-run: emit a verbatim duplicate of the T9 worklist body (lines 167-192 of source). Preserve the "Expand any new instantiations generated by wildcard substitution" comment.
- **T13** Final `return new Module(declaration: module.Declaration, typeDefs: replacedTypeDefs.Concat(expandedDefs).ToList(), procDeclarations: replacedProcDecls, paramProcDecls: paramProcDeclTemplates, procedures: module.Procedures, compileMode: module.CompileMode, line: module.Line, column: module.Column);`.
- **T14** Emit `private static List<string> DetectProcTypeParams(ProcDecl pd, IDictionary<string, TypeDef> templates, IList<TypeDef> monoTypeDefs, IReadOnlySet<string> externalKnownTypes)` with five-source knownTypes union (`templates.Keys`, `monoTypeDefs.Select(td => td.Name)`, `TypeRef.Builtins`, `externalKnownTypes`, `"Constant"`), candidate-collection foreach + return `candidates.ToList()`.
- **T15** Emit `private static void CollectInnerTypeParamCandidates(TypeExpr expr, ISet<string> knownTypes, ISet<string> candidates)` as type-pattern switch over `TypeRef` (with `TypeArgs.Count > 0` guard), `StructAlt`, `ListConsAlt`, `DiffListAlt`.
- **T16** Emit `private static string ExpandedName(string templateName, IReadOnlyList<TypeExpr> typeArgs)` as one-liner interpolation `$"{templateName}<{string.Join(\",\", typeArgs.Select(TypeExprToCanonical))}>"`.
- **T17** Emit `private static string TypeExprToCanonical(TypeExpr expr)` as expression-switch with property-pattern `TypeRef { TypeArgs.Count: > 0 } r` arm + default `expr.ToString()`.
- **T18** Emit `private static string TemplateNameFromExpanded(string expandedName)` using `IndexOf('<')` char-overload (ordinal) + `Substring(0, idx)`.
- **T19** Emit `private static bool IsTemplateRef(TypeRef expr, IDictionary<string, TypeDef> templates)` checking `TypeArgs.Count > 0 && templates.TryGetValue(expr.Name, out var t) && expr.TypeArgs.Count == t.TypeParams.Count`.
- **T20** Emit `private static void CollectInstantiations(TypeExpr expr, IDictionary<string, TypeDef> templates, IDictionary<string, IReadOnlyList<TypeExpr>> instantiations)` as type-pattern switch; TypeRef arm performs `IsTemplateRef` check, `instantiations.TryAdd(name, expr.TypeArgs)`, and recurses into `expr.TypeArgs` (both inside the template branch AND unconditionally afterwards — verbatim Dart shape); structural arms recurse.
- **T21** Emit `private static void CollectInstantiationsInTemplate(TypeExpr expr, IDictionary<string, TypeDef> templates, IDictionary<string, IReadOnlyList<TypeExpr>> instantiations, IList<string> templateParams)` mirroring T20 with the `allParamRefs` LINQ-All short-circuit gate.
- **T22** Emit `private static TypeExpr SubstituteTypeExpr(TypeExpr expr, IDictionary<string, TypeExpr> substitution, IDictionary<string, TypeDef> templates, IDictionary<string, IReadOnlyList<TypeExpr>> instantiations, IReadOnlySet<string>? monoNames = null)` as type-pattern switch; TypeRef arm with (a) `substitution.TryGetValue` type-param replacement preserving `isInput` via re-wrap (TypeRef → TypeRef, PrimitiveModeAlt → PrimitiveModeAlt), (b) `IsTemplateRef` arm with recursive substitution of args, `allWildcards` collapse check, and `instantiations.TryAdd` for new instantiations; structural arms recurse mechanically. Default normalises `monoNames ??= ImmutableHashSet<string>.Empty`.
- **T23** Emit `private static TypeExpr ReplaceParamRefs(TypeExpr expr, IDictionary<string, TypeDef> templates, IReadOnlySet<string>? monoNames = null)` as type-pattern switch; TypeRef arm with `IsTemplateRef` branch (recursive arg replacement + wildcard collapse), and non-template-with-args branch (recurse into `TypeArgs`, re-wrap with new `TypeRef(name, line, column, isInput: ..., typeArgs: replaced)`); structural arms recurse.
- **T24** Port every Dart `///` doc-comment to C# `/// <summary>` XML-doc on its corresponding method, preserving spec/paper citations verbatim.

## 4. Research Findings

none required — all eleven convspec constructs reuse research already
captured in the convspec's `## Rationale & Research Provenance` section
(four cached idioms: `dart-toplevel-fn-to-csharp-static-method`,
`dart-toplevel-driver-fn-to-csharp-static-builder-method`,
`dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch`,
`dart-tostring-interpolation-to-csharp-interpolated-string`; seven new
idioms with authoritative Microsoft Learn citations recorded:
`dart-map-putifabsent-to-csharp-tryadd` (Dictionary.TryAdd doc),
`dart-iterable-map-tolist-to-csharp-linq-select-tolist` (Enumerable.Select
deferred-execution doc), `dart-map-fromiterables-to-csharp-zip-todictionary`
(Enumerable.Zip silent-truncation doc),
`dart-collection-spread-union-to-csharp-hashset-unionwith`
(HashSet.UnionWith + HashSet.Add docs),
`dart-iterable-every-to-csharp-linq-all` (Enumerable.All short-circuit
doc), `dart-constructor-named-args-to-csharp-new-with-named-args`
(named-arguments programming-guide), `dart-list-spread-concat-to-csharp-linq-concat-tolist`
(Enumerable.Concat deferred-execution doc)). Each was issued an
authoritative WebFetch query during convspec authoring; no fresh
research is required to write the conversion plan.

## 5. Consistency Pass

- **Construct 1 (public driver signature)** — fixed — derived from
  convspec construct `dart.toplevel_public_driver_fn_with_named_const_default_collections`
  + cached idiom `dart-toplevel-fn-to-csharp-static-method` (prelude.dart,
  clause_validation.dart).
- **Construct 2 (five-step pipeline + dual worklist)** — fixed —
  derived from convspec construct
  `dart.algorithmic_driver.five_step_imperative_pipeline_local_mutation`
  + cached idiom `dart-toplevel-driver-fn-to-csharp-static-builder-method`
  (program_dfa.dart). The dual-worklist nuance is explicit in the
  convspec's nuance field (lines 89-117 AND 167-192 both run).
- **Construct 3 (putIfAbsent → TryAdd)** — fixed — derived from convspec
  construct `dart.dictionary.putifabsent_lambda_first_writer_wins` +
  Microsoft Learn `Dictionary<TKey,TValue>.TryAdd` authoritative citation.
- **Construct 4 (recursive AST walker switch)** — fixed — derived from
  convspec construct
  `dart.recursive_ast_walker.is_typecheck_dispatch_with_template_param_awareness`
  + cached idiom `dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch`
  (type_ast.dart, program_dfa.dart, clause_validation.dart).
- **Construct 5 (canonical-name helpers + ordinal IndexOf char overload)** —
  fixed — derived from convspec construct
  `dart.canonical_name_construction.string_interpolation_with_join_brackets`
  + cached idiom `dart-tostring-interpolation-to-csharp-interpolated-string`
  (program_dfa.dart, clause_validation.dart). Ordinal `IndexOf(char)`
  follows the clause_validation.dart `StartsWith`-ordinal thread.
- **Construct 6 (.map.toList → .Select.ToList)** — fixed — derived from
  convspec construct `dart.collection.list_select_to_list_two_step_pipeline`
  + Microsoft Learn `Enumerable.Select` deferred-execution authoritative
  citation.
- **Construct 7 (Map.fromIterables → Zip+ToDictionary)** — fixed —
  derived from convspec construct
  `dart.dictionary.fromIterables_zip_two_lists_into_map`
  + Microsoft Learn `Enumerable.Zip` silent-truncation authoritative
  citation. Arity guard preserved as the dead-code-keeper.
- **Construct 8 (set spread → HashSet UnionWith)** — fixed — derived from
  convspec construct
  `dart.collection_literal.set_with_spread_union_of_iterables`
  + Microsoft Learn `HashSet.UnionWith` + `HashSet.Add` authoritative
  citations.
- **Construct 9 (every → All short-circuit)** — fixed — derived from
  convspec construct
  `dart.collection.iterable_every_universal_quantifier_short_circuit`
  + Microsoft Learn `Enumerable.All` short-circuit + vacuous-truth
  authoritative citation.
- **Construct 10 (AST-node named-arg construction)** — fixed — derived
  from convspec construct
  `dart.ast_node.construct_typeref_with_named_args_preserving_isinput`
  + Microsoft Learn "named-and-optional-arguments" programming-guide
  citation. Cross-file constructor-parameter-name constraint is
  documented in convspec nuance and propagates to type_ast.dart.md and
  ast.dart.md (no decision required here).
- **Construct 11 (Module return + Concat.ToList)** — fixed — derived
  from convspec construct
  `dart.return_record_via_module_constructor_with_spread_lists`
  + Microsoft Learn `Enumerable.Concat` deferred-execution
  authoritative citation. `module.Procedures` reference-alias-pass-through
  nuance preserved verbatim from convspec.

## 6. Escalations

None.
