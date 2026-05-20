# Conversion Spec — lib/analysis/type_checker/param_expansion.dart

```yaml
schema_version: 1
source_path: lib/analysis/type_checker/param_expansion.dart
source_sha256: c716e6969f9947cf137f59e5a597ce359d062829a4a3c6f810b76d263c83a64c
target_code_unit: lib/analysis/type_checker/param_expansion.cs
constructs:
  - construct_key: dart.toplevel_public_driver_fn_with_named_const_default_collections
    source_form: >-
      ast.Module expandParameterizedTypes(ast.Module module, {
        Set<String> knownTypeNames = const {},
        Map<String, TypeDef> externalTemplates = const {},
      }) { ... returns a NEW ast.Module ... }
    target_decision: >-
      Emit as the SOLE public entry point on a `public static class
      ParamExpansion` in namespace `Glp.Analysis.TypeChecker`:
      `public static Module ExpandParameterizedTypes(Module module,
      IReadOnlySet<string>? knownTypeNames = null,
      IReadOnlyDictionary<string, TypeDef>? externalTemplates = null)`.
      On entry, normalise `knownTypeNames ??= ImmutableHashSet<string>.Empty`
      and `externalTemplates ??= ImmutableDictionary<string, TypeDef>.Empty`
      so the public surface preserves Dart's "two callable shapes: with or
      without the optional knobs" exactly. The function MUST remain a pure
      transformation: input `module` is never mutated (mirrors Dart's "The
      original Module is not modified" doc-comment); output is a freshly
      constructed `Module`. `Set<String>` (Dart) → `IReadOnlySet<string>` /
      `HashSet<string>` ordinal at call sites (see ordinal-discipline nuance).
      The Dart `Map<String, TypeDef>` parameter → `IReadOnlyDictionary<string,
      TypeDef>` (read-only at the API boundary — the body never writes to
      `externalTemplates`; it only iterates and `putIfAbsent`s into a LOCAL
      `templates` dictionary).
    idiom_id: dart-toplevel-fn-to-csharp-static-method
    research_finding_id: rf-csharp-static-class-no-toplevel-members
    nuance: >-
      Reuse cached idiom (prelude.dart, clause_validation.dart): Dart allows
      library-level functions; C# requires every method to be a member of a
      type, so a `public static class ParamExpansion` is the host. The
      load-bearing nuance is the `const {}` / `const {}` default values: Dart
      `const {}` is a compile-time canonicalised immutable empty set/map
      (single shared instance across calls — safe because it cannot be
      mutated). C# has NO `const` collection, so the cached idiom (per
      type_ast.dart's `rf-dart-factory-ctor-const-default-to-csharp-static-
      factory`) mandates one of (a) `ImmutableHashSet/Dictionary.Empty`
      sentinel (no allocation), or (b) `null` default + `??=` nullary
      normalisation. (b) is chosen here because the body never mutates
      `knownTypeNames`/`externalTemplates`; aliasing a mutable singleton
      would be a latent footgun, while the immutable empty is allocation-
      free. Naming: Dart `lowerCamel` free function → C# `PascalCase` static
      method (.NET conventions). NO `async` — the function is synchronous;
      do not introduce `Task<Module>` (no I/O, no awaits in source).
  - construct_key: dart.algorithmic_driver.five_step_imperative_pipeline_local_mutation
    source_form: >-
      // Step 1: Separate templates from monomorphic types  (Map+List build)
      // Step 2: Collect all instantiations from type defs and proc decls
      // Step 3: Expand each instantiation using a worklist  (while-loop)
      // Step 4: Replace references in monomorphic type defs
      // Step 5: Replace references in procedure declarations + post-worklist
      return ast.Module(declaration:..., typeDefs:[...replaced, ...expanded],
                       procDeclarations:..., paramProcDecls:..., ...);
    target_decision: >-
      Preserve the five-step imperative structure verbatim in the body of
      `ExpandParameterizedTypes`. Each step becomes its own local block
      (optionally factored into a `private static` step helper if the body
      crosses ~80 lines after porting). Local accumulators map directly:
      `final templates = <String, TypeDef>{};` → `var templates = new
      Dictionary<string, TypeDef>(StringComparer.Ordinal);` (ordinal — see
      string-keyed-map nuance); `final monoTypeDefs = <TypeDef>[];` → `var
      monoTypeDefs = new List<TypeDef>();`; `final instantiations =
      <String, List<TypeExpr>>{};` → `var instantiations = new
      Dictionary<string, IReadOnlyList<TypeExpr>>(StringComparer.Ordinal);`;
      `final expandedDefs = <TypeDef>[];` → `var expandedDefs = new
      List<TypeDef>();`; `final expanded = <String>{};` → `var expanded =
      new HashSet<string>(StringComparer.Ordinal);`. The `while
      (instantiations.length > expanded.length)` worklist (steps 3 and the
      post-Step-5 loop) is reproduced as `while (instantiations.Count >
      expanded.Count)` — note the LOOP IS RUN TWICE in source (lines 89-117
      and 167-192) and BOTH RUNS MUST BE PRESERVED: the second pass exists
      specifically to expand instantiations newly created by the wildcard-
      substitution step (5). Eliding the second loop would silently regress
      coverage of `Stream(_)`-style wildcard-instantiated proc decls.
    idiom_id: dart-toplevel-driver-fn-to-csharp-static-builder-method
    research_finding_id: rf-csharp-mutable-local-accumulator-pure-function
    nuance: >-
      Reuse cached idiom (program_dfa.dart): Dart and C# both express
      imperative drivers with local mutable accumulators returning an
      immutable result; semantics are identical. Two non-trivial nuances:
      (1) The `Map.of(instantiations).entries` defensive copy inside the
      worklist (Dart line 90, line 168) is REQUIRED because the loop body
      may extend `instantiations` via `_substituteTypeExpr` /
      `_collectInstantiations*` — iterating the live dictionary would throw
      `ConcurrentModificationError`. The C# counterpart MUST take the same
      snapshot: `foreach (var entry in instantiations.ToList())` (or
      `.ToArray()`), and MUST iterate over the snapshot — iterating the
      live `Dictionary<TKey,TValue>` while modifying it throws
      `InvalidOperationException` ("Collection was modified; enumeration
      operation may not execute", documented on `Dictionary<TKey,
      TValue>.Enumerator`). (2) The structure-preserving copy of
      `template.alternatives.map(...).toList()` (Dart) → `template
      .Alternatives.Select(...).ToList()` (C#) — `Select` is lazy in C#, so
      `.ToList()` is REQUIRED (without it, the deferred enumeration would
      observe partial state and break Step 3's invariants). Skipping
      `.ToList()` is the classic LINQ deferred-execution pitfall.
  - construct_key: dart.dictionary.putifabsent_lambda_first_writer_wins
    source_form: >-
      for (final entry in externalTemplates.entries) {
        templates.putIfAbsent(entry.key, () => entry.value);
      }
      // and: instantiations.putIfAbsent(name, () => expr.typeArgs);
    target_decision: >-
      Map `Map.putIfAbsent(k, () => v)` to the C# pair
      `if (!templates.ContainsKey(entry.Key)) templates[entry.Key] =
      entry.Value;` (or equivalently `templates.TryAdd(entry.Key,
      entry.Value)` on .NET Core+). `TryAdd` is the canonical analog
      (Microsoft Learn: "Attempts to add the specified key and value …
      Returns true if the key/value pair was added … false if the key
      already exists"). Either form preserves Dart's "first writer wins"
      semantics. Do NOT use `Dictionary.Add` (throws on duplicate — that
      semantics is the OPPOSITE of `putIfAbsent`). Do NOT use the indexer
      `dict[k] = v` (last-writer-wins — wrong direction). The doc-comment
      "Local templates take precedence over external ones" is the
      observable contract this idiom preserves.
    idiom_id: dart-map-putifabsent-to-csharp-tryadd
    research_finding_id: rf-csharp-dictionary-tryadd-first-writer-wins
    nuance: >-
      Cached as a new idiom (file-first use in the type_checker family —
      previous specs covered `addType[k]=v` (last-wins) and spread-merge
      (last-wins); putIfAbsent is the dual semantics and earns its own KB
      entry). The lambda `() => entry.value` in Dart is evaluated lazily —
      only when the key is absent. `TryAdd` evaluates `entry.Value`
      eagerly, which is identical-behaviour here because `entry.Value` is
      a TypeDef *reference* already in hand (no side effect, no
      computation). If a future call site passes a *lazy* value
      (computed-on-miss), the conversion MUST switch to
      `CollectionsMarshal.GetValueRefOrAddDefault` or an explicit
      `if (!d.ContainsKey(k)) d[k] = factory();` to preserve laziness.
      Ordinal string keys mandated (StringComparer.Ordinal) — same nuance
      thread as moded_term/type_ast/program_dfa.
  - construct_key: dart.recursive_ast_walker.is_typecheck_dispatch_with_template_param_awareness
    source_form: >-
      void _collectInstantiations(TypeExpr expr, Map<String, TypeDef>
        templates, Map<String, List<TypeExpr>> instantiations) {
        if (expr is TypeRef) { if (_isTemplateRef(expr, templates)) { ... }
          for (final arg in expr.typeArgs) _collectInstantiations(arg, ...);
          return; }
        if (expr is StructAlt) { for (final arg in expr.args)
          _collectInstantiations(arg, ...); }
        if (expr is ListConsAlt) { _collectInstantiations(expr.head, ...);
          _collectInstantiations(expr.tail, ...); }
        if (expr is DiffListAlt) { ... } }
      // Mirror: _collectInstantiationsInTemplate, _substituteTypeExpr,
      //         _replaceParamRefs, _collectInnerTypeParamCandidates
    target_decision: >-
      Each of the five private recursive AST walkers
      (`_collectInstantiations`, `_collectInstantiationsInTemplate`,
      `_substituteTypeExpr`, `_replaceParamRefs`,
      `_collectInnerTypeParamCandidates`) becomes a `private static`
      method on `ParamExpansion`. Convert each chain of independent
      `if (expr is X)` blocks to a single C# `switch` statement on `expr`
      with pattern arms: `case TypeRef r: ... break; case StructAlt s:
      ... break; case ListConsAlt c: ... break; case DiffListAlt d:
      ... break; default: break;` — the sub-types are disjoint
      (`TypeExpr` is treated as a closed sum per the type_ast.dart spec),
      so the Dart sequential-`if` and a `switch` are semantically
      equivalent. The early `return` after the `TypeRef` arm in Dart is
      load-bearing (it skips falling into StructAlt/ListConsAlt/
      DiffListAlt arms even though those tests would be false — the
      `return` documents intent and shaves the four follow-on `is`-tests);
      in C# the `switch` arms are naturally mutually exclusive, so the
      `return` collapses to a `break;` in the `case TypeRef` arm — the
      observable behaviour is identical (no fall-through in C# anyway).
    idiom_id: dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch
    research_finding_id: rf-dart-extension-is-as-to-csharp-type-pattern-switch
    nuance: >-
      Reuse cached idiom (type_ast.dart, program_dfa.dart,
      clause_validation.dart). Dart `is X` with smart-cast (the `expr`
      inside the block is narrowed to `X`) → C# `case X x:` pattern
      variable (test+cast fused, no `InvalidCast` risk). Recursion depth
      is bounded by AST depth (small, single-digit in realistic programs)
      — no stack overflow risk, no need for an explicit work-stack
      transform. Reference semantics for the `TypeExpr` parameter are
      preserved in both languages (AST nodes are reference types; C#
      `class`, never `struct`). The two specialised walkers
      (`_collectInstantiationsInTemplate`, `_collectInnerTypeParamCandidates`)
      thread an extra `List<string> templateParams` / `Set<string>
      candidates` accumulator — both map mechanically to additional `IList
      <string>` / `ISet<string>` parameters with identical semantics. The
      `templateParams.contains(arg.name)` membership test is small-N
      linear in Dart on a `List<String>`; in C# preserve `templateParams
      .Contains(r.Name)` on `IList<string>` (also linear) — do NOT silently
      promote to `HashSet` (would change asymptotic but not observable
      semantics; preserve verbatim, leave optimisation to a later pass).
  - construct_key: dart.canonical_name_construction.string_interpolation_with_join_brackets
    source_form: >-
      String _expandedName(String templateName, List<TypeExpr> typeArgs) =>
        '$templateName<${typeArgs.map(_typeExprToCanonical).join(',')}>';
      String _typeExprToCanonical(TypeExpr expr) { if (expr is TypeRef)
        { if (expr.typeArgs.isNotEmpty) return
          '${expr.name}<${expr.typeArgs.map(_typeExprToCanonical).join(',')}>${expr.isInput ? '?' : ''}';
          return expr.toString(); } return expr.toString(); }
      String _templateNameFromExpanded(String expandedName) { final idx =
        expandedName.indexOf('<'); if (idx < 0) return expandedName;
        return expandedName.substring(0, idx); }
    target_decision: >-
      Three `private static string` methods on `ParamExpansion`.
      `_expandedName` → `private static string ExpandedName(string
      templateName, IReadOnlyList<TypeExpr> typeArgs) => $"{templateName}<
      {string.Join(",", typeArgs.Select(TypeExprToCanonical))}>";`. The
      Dart `Iterable.join(',')` maps to .NET `string.Join(",", source)`
      (Microsoft Learn — `string.Join(string? separator, IEnumerable<T>
      values)`). `_typeExprToCanonical` → recursive `private static string
      TypeExprToCanonical(TypeExpr expr) => expr switch { TypeRef
      { TypeArgs.Count: > 0 } r => $"{r.Name}<{string.Join(",",
      r.TypeArgs.Select(TypeExprToCanonical))}>{(r.IsInput ? "?" : "")}",
      _ => expr.ToString() };` — note the property pattern `TypeArgs.Count:
      > 0` collapses the Dart `isNotEmpty` check into the type pattern
      itself. `_templateNameFromExpanded` → `private static string
      TemplateNameFromExpanded(string expandedName) { var idx =
      expandedName.IndexOf('<'); return idx < 0 ? expandedName :
      expandedName.Substring(0, idx); }`. CRITICAL nuance: pass `'<'`
      to `IndexOf(char)` (the char overload, NOT the string overload) —
      the char overload is documented to use ORDINAL semantics, while
      `IndexOf(string)` defaults to current culture and would mis-match
      under e.g. Turkish locale. This is the same ordinal-discipline
      thread the clause_validation/StartsWith spec already records.
    idiom_id: dart-tostring-interpolation-to-csharp-interpolated-string
    research_finding_id: rf-csharp-interpolated-string-equivalent-to-dart-interpolation
    nuance: >-
      Reuse cached idiom (program_dfa.dart, clause_validation.dart):
      Dart `'$x'` / `'${...}'` → C# `$"{x}"` / `$"{...}"` are syntactic
      twins. The `string.Join(",", ...)` mapping (Microsoft Learn) is the
      standard .NET equivalent of Dart's `Iterable.join`. The
      construction is round-trip-paired with `TemplateNameFromExpanded`:
      `ExpandedName(t, args)` followed by `TemplateNameFromExpanded(name)`
      must return `t` for every well-formed pair, and the test is the
      `'<'` char position. The ordinal `IndexOf(char)` mandate is the
      load-bearing nuance: a culture-sensitive default would silently
      break wildcard-instantiated round-trip for non-ASCII template
      names (not currently used but the discipline is preserved as a
      codebase-wide invariant per the clause_validation precedent).
      Conditional Dart `expr.isInput ? '?' : ''` → C# ternary inside
      interpolation `(r.IsInput ? "?" : "")` — direct transliteration.
  - construct_key: dart.collection.list_select_to_list_two_step_pipeline
    source_form: >-
      final newAlts = template.alternatives
          .map((alt) => _substituteTypeExpr(alt, substitution, ..., monoNames: monoNames))
          .toList();
      final processedAlts = newAlts
          .map((alt) => _replaceParamRefs(alt, templates, monoNames: monoNames))
          .toList();
      // and: final wildcardArgTypes = pd.argTypes.map((arg) {...}).toList();
    target_decision: >-
      Map each `.map(...).toList()` chain to LINQ
      `.Select(...).ToList()`. The mandatory `.ToList()` materialises the
      Dart eager-iterable contract: Dart's `Iterable.map().toList()` runs
      the projection eagerly when `.toList()` is called and produces a
      fresh `List<T>`. The C# pair `.Select(...).ToList()` matches that
      shape exactly. Two-step pipelines (`newAlts` → `processedAlts`)
      MUST keep BOTH `.ToList()` calls — fusing into `.Select(a =>
      replaceParamRefs(substituteTypeExpr(a, ...), ...)).ToList()` is a
      semantically-equivalent micro-optimisation but loses the
      intermediate variable's debuggability; PRESERVE the two-step shape
      for review parity.
    idiom_id: dart-iterable-map-tolist-to-csharp-linq-select-tolist
    research_finding_id: rf-csharp-linq-deferred-execution-materialisation
    nuance: >-
      Cached as a new idiom (file-first use in type_checker family). The
      load-bearing nuance is LINQ deferred execution: `Select` returns
      `IEnumerable<T>` which is enumerated ONLY when the result is
      iterated. Without `.ToList()`, two passes over the result would
      re-execute the projection (re-allocating new TypeExpr nodes both
      times — bug-grade behaviour in this driver because subsequent
      `_replaceParamRefs` walks would see fresh-but-distinct nodes each
      iteration). Microsoft Learn explicitly documents:
      "`Enumerable.Select<TSource,TResult>` … is implemented by using
      deferred execution." The `.ToList()` call site is therefore a
      correctness requirement, not a stylistic choice. Reference-vs-value
      preserved: `TypeExpr` is a reference type in both languages; the
      lists store node references, sharing identity through the pipeline.
  - construct_key: dart.dictionary.fromIterables_zip_two_lists_into_map
    source_form: >-
      final substitution = Map<String, TypeExpr>.fromIterables(
          template.typeParams, typeArgs);
    target_decision: >-
      Map `Map<K,V>.fromIterables(keys, values)` to a C# `Zip`+`ToDictionary`
      chain: `var substitution = template.TypeParams.Zip(typeArgs, (k, v)
      => new KeyValuePair<string, TypeExpr>(k, v)).ToDictionary(kv =>
      kv.Key, kv => kv.Value, StringComparer.Ordinal);`. Equivalently
      `template.TypeParams.Zip(typeArgs).ToDictionary(t => t.First, t =>
      t.Second, StringComparer.Ordinal);` on .NET 6+ where `Zip` has a
      tuple-returning overload (Microsoft Learn:
      `Enumerable.Zip<TFirst,TSecond>(IEnumerable<TFirst>,
      IEnumerable<TSecond>)`). Critical preservation: Dart
      `Map.fromIterables` throws `StateError` if lengths differ — but
      THIS CALL SITE GUARDS WITH `template.typeParams.length ==
      typeArgs.length` immediately above (Dart lines 102-103, 178-179), so
      the post-condition is "lengths equal". C# `Zip` SILENTLY TRUNCATES
      to the shorter sequence (does NOT throw) — but because the arity
      guard runs first, the silent-truncation path is unreachable.
      Preserve the explicit arity check verbatim so the invariant remains
      visible.
    idiom_id: dart-map-fromiterables-to-csharp-zip-todictionary
    research_finding_id: rf-csharp-linq-zip-shorter-sequence-truncation
    nuance: >-
      Cached as a new idiom (file-first). The behavioural nuance is the
      length-mismatch divergence: Dart throws; C# Zip silently truncates.
      The conversion MUST keep the arity guard in the surrounding code so
      that the truncation behaviour is dead code, preserving the Dart
      invariant. If a future refactor removes the arity guard, the C#
      site MUST switch to a manual zip with an explicit length check
      (e.g. `if (template.TypeParams.Count != typeArgs.Count) throw new
      InvalidOperationException(...)`). Ordinal key comparer mandated
      (string-keyed dictionary, same thread as the rest of the file).
  - construct_key: dart.collection_literal.set_with_spread_union_of_iterables
    source_form: >-
      final monoNames = <String>{
        ...monoTypeDefs.map((td) => td.name),
        ...knownTypeNames,
      };
      // and: final knownTypes = <String>{ ...templates.keys,
      //   ...monoTypeDefs.map((td) => td.name), ...TypeRef.builtins,
      //   ...externalKnownTypes, 'Constant', };
    target_decision: >-
      Dart collection-literal spread `{...a, ...b}` (set union, last-wins
      on duplicates — but for `Set<T>` duplicates are de-duplicated, so
      order of spread is observationally irrelevant) → C# explicit set
      construction: `var monoNames = new HashSet<string>(
      StringComparer.Ordinal); foreach (var td in monoTypeDefs)
      monoNames.Add(td.Name); monoNames.UnionWith(knownTypeNames);`. OR
      equivalently `var monoNames = monoTypeDefs.Select(td => td.Name)
      .Concat(knownTypeNames).ToHashSet(StringComparer.Ordinal);`. Both
      preserve ordinal comparison + de-duplication. The four-source union
      in `_detectProcTypeParams` becomes the same shape extended with
      additional `UnionWith` calls (`templates.Keys`, `monoTypeDefs
      .Select(...)`, `TypeRef.Builtins`, `externalKnownTypes`) plus a
      single `.Add("Constant")`.
    idiom_id: dart-collection-spread-union-to-csharp-hashset-unionwith
    research_finding_id: rf-csharp-hashset-unionwith-vs-linq-concat-tohashset
    nuance: >-
      Cached as a new idiom (file-first; type_ast/program_dfa covered
      Dart map spread for last-wins-key-merge; this is the SET variant
      and the dual case "union of disjoint-but-occasionally-overlapping
      iterables"). Dart set-literal spread is sugar for repeated `.add()`
      which silently no-ops on duplicates — exactly `HashSet<T>.Add`'s
      contract (Microsoft Learn: "Returns true if the element is added
      … false if the element is already present"). Ordinal comparer is
      load-bearing — without it, two strings differing only by Turkish
      `I` could collide in some locales. The `.ToHashSet(comparer)` LINQ
      extension was added in .NET Core 2.0; on older targets fall back
      to the explicit `new HashSet<string>(source, comparer)` ctor.
  - construct_key: dart.collection.iterable_every_universal_quantifier_short_circuit
    source_form: >-
      final allParamRefs = expr.typeArgs.every((arg) =>
          arg is TypeRef && arg.typeArgs.isEmpty &&
          templateParams.contains(arg.name));
      // and: final allWildcards = substArgs.every((a) =>
      //     a is PrimitiveModeAlt);
    target_decision: >-
      Map Dart `Iterable.every(predicate)` to LINQ `.All(predicate)`.
      First site: `var allParamRefs = expr.TypeArgs.All(arg => arg is
      TypeRef r && r.TypeArgs.Count == 0 && templateParams.Contains(r.Name));`
      Second site: `var allWildcards = substArgs.All(a => a is
      PrimitiveModeAlt);`. The Dart `every` is documented to
      short-circuit on the first `false`; LINQ `All` has identical
      short-circuit semantics (Microsoft Learn: "Returns true if every
      element of the source sequence passes the test in the specified
      predicate, or if the sequence is empty; otherwise, false. … The
      enumeration of source is stopped as soon as the result can be
      determined."). Preserve the pattern variable `arg is TypeRef r &&
      r.TypeArgs.Count == 0` → fuses the Dart `is TypeRef` + cast-via-
      property-access in a single declaration-pattern arm.
    idiom_id: dart-iterable-every-to-csharp-linq-all
    research_finding_id: rf-csharp-linq-all-short-circuit
    nuance: >-
      Cached as a new idiom (moded_term.dart covered the existential
      `dart.list_every.short_circuit_universal_quantifier` shape; this
      file uses a *different* nesting — universal quantifier with
      compound type-pattern + nested membership test — earning a paired
      sibling idiom for the type_checker family). Empty-list case:
      Dart `[].every(...)` returns true (vacuous truth); LINQ `All` on
      an empty `IEnumerable` also returns true (Microsoft Learn,
      verbatim). Behaviour preserved exactly.
  - construct_key: dart.ast_node.construct_typeref_with_named_args_preserving_isinput
    source_form: >-
      return TypeRef(replacement.name, replacement.line, replacement.column,
          isInput: true, typeArgs: replacement.typeArgs);
      // and many other TypeRef(...) / StructAlt(...) / ListConsAlt(...) /
      // DiffListAlt(...) / PrimitiveModeAlt(...) / TypeDef(...) /
      // ProcDecl(...) / ast.Module(...) construction sites
    target_decision: >-
      Each Dart constructor invocation maps to a `new ClassName(...)`
      with `new` prepended (C# requires it, Dart doesn't — same nuance
      as clause_validation.dart's `throw new CompileError(...)`). Dart
      named arguments (`isInput:`, `typeArgs:`, `exported:`, `imported:`,
      `modulePath:`, `typeParams:`, `declaration:`, `typeDefs:`,
      `procDeclarations:`, `paramProcDecls:`, `procedures:`,
      `compileMode:`, `line:`, `column:`) → C# named arguments with the
      identical `name: value` syntax (Microsoft Learn: "you can supply
      arguments by name"). The C# `TypeRef` / `StructAlt` / `ListConsAlt`
      / `DiffListAlt` / `PrimitiveModeAlt` / `TypeDef` / `ProcDecl` /
      `Module` constructors MUST expose constructor signatures matching
      the Dart positional+named layout exactly — these signature
      requirements are recorded as *cross-file constraints* (the
      consuming convspecs `type_ast.dart.md` / `ast.dart.md` / etc.
      anchor them); param_expansion.dart's convspec only DEPENDS on
      those signatures, it doesn't redefine them. Spread-list arguments
      (`typeDefs: [...replacedTypeDefs, ...expandedDefs]`) become C#
      collection construction: `typeDefs: replacedTypeDefs.Concat(
      expandedDefs).ToList()`.
    idiom_id: dart-constructor-named-args-to-csharp-new-with-named-args
    research_finding_id: rf-csharp-named-arguments-on-positional-ctor
    nuance: >-
      Cached as a new idiom (file-first — sibling specs invoked named-
      arg semantics only inside throw-CompileError; this file uses them
      pervasively on AST constructors). C# named arguments require the
      target ctor's parameter NAMES to match the call-site labels — the
      cross-file constraint must propagate to type_ast.dart.md /
      ast.dart.md so the constructor signatures emit identical parameter
      names (lowerCamel → camelCase or PascalCase per .NET conventions —
      pick one and apply consistently across the entire convspec corpus;
      the precedent in clause_validation.dart's `phase: "validation"`
      shows the family has settled on lowerCamel parameter names at C#
      call sites, matching the Dart source verbatim). The `isInput:
      expr.isInput` pattern is a *property carry-forward* — load-bearing
      because TypeRef equality depends on `isInput` (per type_ast.dart),
      so dropping it would silently break equality.
  - construct_key: dart.return_record_via_module_constructor_with_spread_lists
    source_form: >-
      return ast.Module(
        declaration: module.declaration,
        typeDefs: [...replacedTypeDefs, ...expandedDefs],
        procDeclarations: replacedProcDecls,
        paramProcDecls: paramProcDeclTemplates,
        procedures: module.procedures,
        compileMode: module.compileMode,
        line: module.line,
        column: module.column,
      );
    target_decision: >-
      Single return statement constructing a new `Module`:
      `return new Module(declaration: module.Declaration,
      typeDefs: replacedTypeDefs.Concat(expandedDefs).ToList(),
      procDeclarations: replacedProcDecls,
      paramProcDecls: paramProcDeclTemplates,
      procedures: module.Procedures,
      compileMode: module.CompileMode,
      line: module.Line,
      column: module.Column);`. The `module.procedures` field is passed
      THROUGH unchanged — this is a deliberate alias (the procedure
      bodies are not re-checked here; type-checker concerns only headers,
      proc-decls, and type-defs at this stage). Document the alias so a
      future caller doesn't expect a deep copy. The `[...a, ...b]` list
      spread → `a.Concat(b).ToList()` (LINQ `Concat` returns a deferred
      `IEnumerable`; `.ToList()` materialises — same deferred-execution
      nuance as the earlier `.Select().ToList()` pipeline).
    idiom_id: dart-list-spread-concat-to-csharp-linq-concat-tolist
    research_finding_id: rf-csharp-linq-concat-deferred-execution
    nuance: >-
      Cached as a new idiom (file-first — type_ast/program_dfa covered
      map-spread; this is the list/array variant). Dart `[...a, ...b]`
      is an eager fresh-list allocation; LINQ `Concat` is deferred —
      `.ToList()` is mandatory to materialise (without it, downstream
      iterations would re-walk both source lists each time). The
      `module.Procedures` reference-pass-through nuance is load-bearing:
      `Procedures` is documented to be a mutable map elsewhere
      (program_dfa.dart records the mutable-accumulator pattern); a
      future codegen that emits this idiom MUST NOT clone here, or it
      will silently change behaviour for callers that mutate the returned
      module's procedures field downstream.
conversion_units:
  - "namespace Glp.Analysis.TypeChecker { public static class ParamExpansion { ... } }"
  - "public static Module ExpandParameterizedTypes(Module module, IReadOnlySet<string>? knownTypeNames = null, IReadOnlyDictionary<string, TypeDef>? externalTemplates = null): null-coalesce defaults to ImmutableHashSet/Dictionary.Empty; preserves Dart 'two callable shapes' contract"
  - "Step 1 block: separate templates vs monomorphic via foreach on module.TypeDefs; templates uses StringComparer.Ordinal; merge externalTemplates with TryAdd (first-writer-wins)"
  - "Step 1.5: build monoNames HashSet<string>(Ordinal) via foreach Add + UnionWith(knownTypeNames)"
  - "Step 2: collect instantiations via foreach over monoTypeDefs.Alternatives and module.ProcDeclarations; per-proc-decl branch on procTypeParams emptiness; template-body scan via foreach on templates.Values"
  - "Step 3: while (instantiations.Count > expanded.Count) loop with foreach over instantiations.ToList() snapshot; substitute via Dictionary fromIterables (Zip+ToDictionary); two-step Select(...).ToList() pipeline (substitute → replaceParamRefs)"
  - "Step 4: replacedTypeDefs via monoTypeDefs.Select(td => new TypeDef(td.Name, td.Alternatives.Select(...).ToList(), td.Line, td.Column)).ToList()"
  - "Step 5: replacedProcDecls + paramProcDeclTemplates accumulation; per-pd branch on procTypeParams; wildcard substitution via Dictionary<string, TypeExpr> with PrimitiveModeAlt(false, 0, 0) values; argTypes via Select(arg => ReplaceParamRefs(SubstituteTypeExpr(arg, ...), ...)).ToList()"
  - "Step 5b: REPEAT the while-worklist (lines 167-192) to expand instantiations newly created by wildcard substitution"
  - "Return: new Module(declaration:..., typeDefs: replacedTypeDefs.Concat(expandedDefs).ToList(), procDeclarations: replacedProcDecls, paramProcDecls: paramProcDeclTemplates, procedures: module.Procedures, compileMode: module.CompileMode, line: module.Line, column: module.Column);"
  - "private static List<string> DetectProcTypeParams(ProcDecl pd, IDictionary<string, TypeDef> templates, IList<TypeDef> monoTypeDefs, IReadOnlySet<string> externalKnownTypes): build knownTypes HashSet(Ordinal) union of templates.Keys/monoTypeDefs.Name/TypeRef.Builtins/externalKnownTypes/{\"Constant\"}; collect candidates via foreach + CollectInnerTypeParamCandidates; return candidates.ToList()"
  - "private static void CollectInnerTypeParamCandidates(TypeExpr expr, ISet<string> knownTypes, ISet<string> candidates): switch (expr) { case TypeRef r when r.TypeArgs.Count > 0: foreach (var arg in r.TypeArgs) { if (arg is TypeRef inner && inner.TypeArgs.Count == 0 && !knownTypes.Contains(inner.Name)) candidates.Add(inner.Name); CollectInnerTypeParamCandidates(arg, knownTypes, candidates); } break; case TypeRef: break; case StructAlt s: foreach (var arg in s.Args) Recurse; break; case ListConsAlt c: Recurse(c.Head); Recurse(c.Tail); break; case DiffListAlt d: Recurse(d.Content); Recurse(d.Hole); break; default: break; }"
  - "private static string ExpandedName(string templateName, IReadOnlyList<TypeExpr> typeArgs) => $\"{templateName}<{string.Join(\\\",\\\", typeArgs.Select(TypeExprToCanonical))}>\";"
  - "private static string TypeExprToCanonical(TypeExpr expr) => expr switch { TypeRef { TypeArgs.Count: > 0 } r => $\"{r.Name}<{string.Join(\\\",\\\", r.TypeArgs.Select(TypeExprToCanonical))}>{(r.IsInput ? \\\"?\\\" : \\\"\\\")}\", _ => expr.ToString() };"
  - "private static string TemplateNameFromExpanded(string expandedName) { var idx = expandedName.IndexOf('<'); return idx < 0 ? expandedName : expandedName.Substring(0, idx); }  // ORDINAL via char-overload"
  - "private static bool IsTemplateRef(TypeRef expr, IDictionary<string, TypeDef> templates): TypeArgs.Count > 0 AND templates.TryGetValue(expr.Name, out var t) AND expr.TypeArgs.Count == t.TypeParams.Count"
  - "private static void CollectInstantiations(TypeExpr expr, IDictionary<string, TypeDef> templates, IDictionary<string, IList<TypeExpr>> instantiations): switch-on-TypeExpr with TypeRef branch handling _isTemplateRef + putIfAbsent (TryAdd) + recurse into TypeArgs; StructAlt/ListConsAlt/DiffListAlt arms recurse into structural children"
  - "private static void CollectInstantiationsInTemplate(TypeExpr expr, IDictionary<string, TypeDef> templates, IDictionary<string, IList<TypeExpr>> instantiations, IList<string> templateParams): same shape with the All-bare-param-refs short-circuit check (LINQ All)"
  - "private static TypeExpr SubstituteTypeExpr(TypeExpr expr, IDictionary<string, TypeExpr> substitution, IDictionary<string, TypeDef> templates, IDictionary<string, IList<TypeExpr>> instantiations, IReadOnlySet<string>? monoNames = null): switch with TypeRef arm (substitution.TryGetValue for type-param replacement; isInput carry-forward via new TypeRef(...) or PrimitiveModeAlt(...)), template-ref arm (recursive substitution of args; wildcard-collapse via .All(a => a is PrimitiveModeAlt) + monoNames.Contains; putIfAbsent into instantiations), and structural arms (StructAlt/ListConsAlt/DiffListAlt) recursing"
  - "private static TypeExpr ReplaceParamRefs(TypeExpr expr, IDictionary<string, TypeDef> templates, IReadOnlySet<string>? monoNames = null): switch with TypeRef arm (replace template refs with expanded names; wildcard-collapse via All+monoNames.Contains; recurse into non-template TypeArgs), and structural arms recursing"
  - "XML-doc /// summary blocks ported verbatim from each Dart /// doc-comment (function purpose, spec citation 'docs/type system/typed-program.md', paper Section 8 Def 8.1, per-helper recursion rationale)"
escalations: []
```

## Rationale & Research Provenance

This file is a pure functional driver: one public AST→AST transform
(`expandParameterizedTypes`) that mono-morphises parameterised type templates,
implemented as a five-step imperative pipeline over local mutable collections
returning a freshly-constructed `Module`. Surrounding it are eight private
helpers — five recursive AST walkers (`_collectInstantiations`,
`_collectInstantiationsInTemplate`, `_substituteTypeExpr`,
`_replaceParamRefs`, `_collectInnerTypeParamCandidates`) and three string
utilities (`_expandedName`, `_typeExprToCanonical`,
`_templateNameFromExpanded`) plus a small predicate `_isTemplateRef` and a
type-parameter detector `_detectProcTypeParams`. Every non-trivial decision
below carries an authoritative Dart/.NET citation; cached idioms are reused
verbatim per FR-012/FR-024 (no re-research).

### dart-toplevel-fn-to-csharp-static-method  (cached idiom)

**Deep analysis.** `expandParameterizedTypes` is a library-level (top-level)
public function. Its two optional named parameters use `const {}` defaults —
an immutable compile-time-empty `Set<String>` and `Map<String, TypeDef>`.
The function body never mutates either parameter.

**Research (cached, FR-024 — no fresh call).** Reuses
`rf-csharp-static-class-no-toplevel-members` (prelude.dart,
clause_validation.dart): Microsoft Learn — "A class declared at namespace
scope is a top-level type; methods can only be declared inside a type."
Idiom `dart-toplevel-fn-to-csharp-static-method` is `active`; reuse verbatim.

**Conclusion.** Host class `public static class ParamExpansion` in namespace
`Glp.Analysis.TypeChecker`; `public static Module ExpandParameterizedTypes
(Module, IReadOnlySet<string>?, IReadOnlyDictionary<string, TypeDef>?)`. The
`const {}` defaults map to `null` + `??=` normalisation (no shared mutable
singleton; matches Dart's immutable-empty contract).

### dart-toplevel-driver-fn-to-csharp-static-builder-method  (cached idiom)

**Deep analysis.** The body is a five-step imperative pipeline with five
local mutable accumulators (`templates`, `monoTypeDefs`, `instantiations`,
`expandedDefs`, `expanded`) and two while-worklist loops. The driver returns
a freshly-constructed `Module` and never mutates its inputs. The pipeline
shape is identical to the driver pattern recorded in `program_dfa.dart`
(`buildProgramDFA`).

**Research (cached, FR-024).** Reuses
`rf-csharp-mutable-local-accumulator-pure-function` (program_dfa.dart);
idiom `dart-toplevel-driver-fn-to-csharp-static-builder-method` is `active`.

**Conclusion.** Preserve the five-step structure verbatim. Two extracted
behavioural sub-nuances are load-bearing:

(1) **Snapshot-iteration to avoid concurrent-modification.** The worklist
iterates `Map.of(instantiations).entries` (Dart line 90, 168) explicitly to
take a defensive copy — because the loop body may extend `instantiations`
via `_substituteTypeExpr`. The C# counterpart MUST snapshot via
`instantiations.ToList()` (or `.ToArray()`) — iterating the live
`Dictionary<TKey, TValue>` while inserting throws `InvalidOperationException`
("Collection was modified; enumeration operation may not execute" — documented
on `Dictionary<TKey, TValue>.Enumerator`).

(2) **LINQ deferred-execution materialisation.** Every `.Select(...)` MUST be
followed by `.ToList()` (or `.ToArray()`) so the projection runs once and
allocates fresh AST nodes once. Without `.ToList()`, downstream passes
re-execute the projection and produce *distinct-but-equal* nodes on each
walk — a silent regression of Step 3's invariant that template expansions
share node identity. Microsoft Learn: "`Enumerable.Select<TSource, TResult>`
… is implemented by using deferred execution." This is a correctness
requirement, not a style choice.

### dart-map-putifabsent-to-csharp-tryadd  (NEW idiom, this file)

**Deep analysis.** Two `Map.putIfAbsent` sites: (a) merging
`externalTemplates` into local `templates` with "Local templates take
precedence over external ones" (line 35-37); (b) recording new instantiations
without overwriting existing ones (lines 305, 405). Dart `putIfAbsent(k, ()
=> v)` is first-writer-wins.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.tryadd` —
Microsoft Learn, decisive: "Attempts to add the specified key and value to
the dictionary. Returns true if the key/value pair was added … false if the
key already exists." This is exactly Dart `putIfAbsent`'s contract. Verbatim
query: "C# Dictionary TryAdd first-writer-wins return value". The `Add`
overload is rejected (throws `ArgumentException` on duplicate — opposite
semantics); the indexer `dict[k] = v` is rejected (last-writer-wins — also
opposite). Authoritative.

**Conclusion.** `Map.putIfAbsent(k, () => v)` → `dict.TryAdd(k, v)`. Idiom
recorded as new active KB entry `dart-map-putifabsent-to-csharp-tryadd`.
**Nuance preserved**: the Dart lambda `() => v` is lazy; `TryAdd` is eager.
For this file every call site passes a value already in hand
(`entry.value`, `expr.typeArgs`, `substArgs`), so the eager/lazy distinction
is observationally invisible — but flagged in the idiom YAML for future
sites that pass computed-on-miss factories.

### dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch  (cached idiom)

**Deep analysis.** Five recursive AST walkers (lines 233-262, 300-330,
336-370, 375-429, 433-476) each branch on `TypeExpr` runtime sub-type using
sequential `if (expr is X)` blocks with smart-cast member access on the
narrowed variable. Sub-types are disjoint (`TypeExpr` is treated as a closed
sum per `type_ast.dart`), so sequential-if and `switch` are observationally
equivalent.

**Research (cached, FR-024).** Reuses
`rf-dart-extension-is-as-to-csharp-type-pattern-switch` (type_ast.dart,
program_dfa.dart, clause_validation.dart). Idiom
`dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch` is `active`;
reuse verbatim per SC-007.

**Conclusion.** Each walker becomes a `switch (expr) { case TypeRef r: ...;
break; case StructAlt s: ...; break; case ListConsAlt c: ...; break; case
DiffListAlt d: ...; break; default: break; }` — preserves both the
disjointness intent and the no-fall-through invariant of the Dart source.
The early `return` after the Dart `TypeRef` arm becomes a `break` (the
switch is already mutually-exclusive). Recursion depth is bounded by AST
depth — no stack-overflow risk, no work-stack transform required.

### dart-tostring-interpolation-to-csharp-interpolated-string  (cached idiom)

**Deep analysis.** Three string-construction helpers build canonical
template-name strings: `_expandedName` (`$templateName<...>`),
`_typeExprToCanonical` (`${expr.name}<...>${expr.isInput ? '?' : ''}`), and
`_templateNameFromExpanded` (substring before `'<'`). Round-trip-paired:
`TemplateNameFromExpanded(ExpandedName(t, args)) == t`.

**Research (cached, FR-024).** Reuses
`rf-csharp-interpolated-string-equivalent-to-dart-interpolation`
(program_dfa.dart, clause_validation.dart). Idiom
`dart-tostring-interpolation-to-csharp-interpolated-string` is `active`;
reuse verbatim.

**Conclusion.** Dart `'$x'` / `'${expr}'` → C# `$"{x}"` / `$"{expr}"`. Dart
`Iterable.join(',')` → `string.Join(",", source)` (Microsoft Learn standard
mapping). **Ordinal nuance** preserved: `expandedName.indexOf('<')` MUST
become `expandedName.IndexOf('<')` using the `char` overload (ordinal,
documented), NOT `IndexOf(string)` (current-culture default, can mis-match
under Turkish locale and similar) — same ordinal-discipline thread as
clause_validation's `StartsWith` and type_ast's `FrozenSet` ordinal comparer.

### dart-iterable-map-tolist-to-csharp-linq-select-tolist  (NEW idiom)

**Deep analysis.** The file uses `Iterable.map(...).toList()` pervasively
(lines 108-113, 121-123, 149-152, 158-160, 183-188) — typically as
two-step pipelines (substitute then replace-refs) that allocate fresh
intermediate `List<TypeExpr>`.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.select` —
Microsoft Learn, decisive: "`Enumerable.Select<TSource, TResult>(IEnumerable
<TSource>, Func<TSource, TResult>)` … This method is implemented by using
deferred execution. The immediate return value is an object that stores all
the information that is required to perform the action. The query
represented by this method is not executed until the object is enumerated
either by calling its GetEnumerator method directly or by using foreach in
Visual C#." Verbatim query: "Enumerable.Select deferred execution
materialisation ToList". Authoritative.

**Conclusion.** `.map(...).toList()` → `.Select(...).ToList()`. `.ToList()`
is a CORRECTNESS REQUIREMENT (materialises the projection so subsequent
passes do not re-execute and produce distinct-but-equal nodes). Two-step
pipelines keep BOTH `.ToList()` calls — preserve review parity.

### dart-map-fromiterables-to-csharp-zip-todictionary  (NEW idiom)

**Deep analysis.** Two sites zip a `List<String>` of template type-params
with a `List<TypeExpr>` of type-args into a substitution map
(`Map<String, TypeExpr>.fromIterables(template.typeParams, typeArgs)` —
lines 107, 182). Both sites are arity-guarded immediately above.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.zip` —
Microsoft Learn, decisive: "If the sequences are of unequal length, this
method merges sequences until it reaches the end of one of them." (silent
truncation, NO exception). Contrasts with Dart's `Map.fromIterables` which
throws `StateError` on length mismatch — but the surrounding arity guard
makes the divergent path dead code. Verbatim query: "C# Enumerable.Zip
length mismatch silent truncation vs Dart Map.fromIterables".
Authoritative.

**Conclusion.** `Map.fromIterables(keys, values)` → `keys.Zip(values, (k, v)
=> new KeyValuePair<string, TypeExpr>(k, v)).ToDictionary(kv => kv.Key, kv
=> kv.Value, StringComparer.Ordinal)`. The explicit arity check above each
call site (Dart lines 102-103, 178-179) MUST be preserved so the silent-
truncation case remains unreachable. **If the guard is ever removed**, the
C# site MUST add an explicit length-mismatch check that throws — recorded
in the idiom YAML as a maintenance invariant.

### dart-collection-spread-union-to-csharp-hashset-unionwith  (NEW idiom)

**Deep analysis.** Two `Set<String>` literals built from multi-source spread:
`monoNames` from `monoTypeDefs.map(td => td.name)` ∪ `knownTypeNames` (lines
44-47); `knownTypes` from `templates.keys` ∪ `monoTypeDefs.map(td => td.name)`
∪ `TypeRef.builtins` ∪ `externalKnownTypes` ∪ `{'Constant'}` (lines 214-220).
Set semantics ⇒ duplicates de-duplicated, order observationally irrelevant.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1.unionwith` —
Microsoft Learn: "Modifies the current `HashSet<T>` object to contain all
elements that are present in itself, the specified collection, or both."
Plus WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1.add` —
"Returns true if the element is added to the `HashSet<T>` object; false if
the element is already present." Verbatim queries: "C# HashSet UnionWith
multi-source union semantics", "HashSet Add returns false duplicate".
Authoritative.

**Conclusion.** `{...a, ...b, ...c, 'x'}` → `new HashSet<string>
(StringComparer.Ordinal) { ... foreach Add / UnionWith ... }` (or the
fluent `a.Concat(b).Concat(c).Append("x").ToHashSet(StringComparer
.Ordinal)`). **Ordinal nuance** mandated — same thread as the rest of the
file. The `.ToHashSet(comparer)` LINQ extension was added in .NET Core 2.0;
on older targets the explicit `new HashSet<string>(source, comparer)` ctor
is the fallback.

### dart-iterable-every-to-csharp-linq-all  (NEW idiom)

**Deep analysis.** Two sites use `Iterable.every(...)` for short-circuit
universal quantification: `allParamRefs` (line 342-343) tests "every typeArg
is a bare TypeRef whose name is a template parameter"; `allWildcards` (line
400, 442) tests "every substituted arg is a `PrimitiveModeAlt`". Both are
correctness-critical: `allParamRefs` distinguishes recursive self-reference
(no instantiation to collect) from concrete instantiation;
`allWildcards` collapses `Stream(_)` to `Stream` when a monomorphic
`Stream` type exists.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.all` —
Microsoft Learn, decisive: "Returns true if every element of the source
sequence passes the test in the specified predicate, or if the sequence is
empty; otherwise, false. … The enumeration of source is stopped as soon as
the result can be determined." Identical to Dart `Iterable.every`
(short-circuit + vacuously-true on empty). Verbatim query: "C# LINQ All
short-circuit empty sequence true". Authoritative.

**Conclusion.** `iter.every(pred)` → `iter.All(pred)`. Compound type-pattern
predicates fuse `is` test + member access into one declaration pattern:
`arg is TypeRef r && r.TypeArgs.Count == 0 && templateParams.Contains(r.Name)`.
The empty-list case (vacuous truth) preserved exactly. This is a SIBLING of
`moded_term.dart`'s `dart.list_every.short_circuit_universal_quantifier`
(existential there, universal here with nested type-pattern + membership) —
recorded as a separate active KB entry so future audits can reuse it
verbatim.

### dart-constructor-named-args-to-csharp-new-with-named-args  (NEW idiom)

**Deep analysis.** Pervasive AST-node construction with mixed
positional+named args (`TypeRef(name, line, col, isInput: ..., typeArgs:
...)`, `ProcDecl(name, argTypes, line, col, typeParams: ..., exported: ...,
imported: ..., modulePath: ...)`, `ast.Module(declaration: ..., typeDefs:
..., procDeclarations: ..., paramProcDecls: ..., procedures: ...,
compileMode: ..., line: ..., column: ...)` — 12+ sites). Property
carry-forward (`isInput: expr.isInput`, `exported: pd.exported`) is
load-bearing for equality semantics (per type_ast.dart's TypeRef equality
override).

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments` —
Microsoft Learn, decisive: "Named arguments enable you to specify an
argument for a parameter by matching the argument with its name rather
than with its position in the parameter list. … You can mix named and
positional arguments." Verbatim query: "C# named arguments positional
constructor matching parameter names". Authoritative.

**Conclusion.** Dart `Class(pos1, pos2, named1: v1, named2: v2)` → C# `new
Class(pos1, pos2, named1: v1, named2: v2)` — direct transliteration with
two nuances: (1) C# requires `new` (Dart doesn't); (2) the target class's
constructor parameter NAMES must match the call-site labels exactly. The
latter is a **cross-file constraint**: type_ast.dart's convspec (and the
ast.dart convspec when written) MUST emit constructor parameter names that
match Dart source verbatim (lowerCamel preserved on the C# parameter side
even though .NET convention is PascalCase for properties — the family has
already settled on this in clause_validation.dart's `phase: "validation"`
call). Idiom recorded as new active KB entry. Spread-list arguments
(`typeDefs: [...replacedTypeDefs, ...expandedDefs]`) handled via the
sibling idiom `dart-list-spread-concat-to-csharp-linq-concat-tolist`.

### dart-list-spread-concat-to-csharp-linq-concat-tolist  (NEW idiom)

**Deep analysis.** One site (line 196: `typeDefs: [...replacedTypeDefs,
...expandedDefs]`) concatenates two `List<TypeDef>` into a fresh list as a
named-argument value.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.concat` —
Microsoft Learn: "Concatenates two sequences. … This method is implemented
by using deferred execution. The immediate return value is an object that
stores all the information that is required to perform the action."
Verbatim query: "C# LINQ Concat deferred execution two sequences". The
deferred-execution nuance is identical to `Select` — `.ToList()` is
required to materialise. Authoritative.

**Conclusion.** `[...a, ...b]` → `a.Concat(b).ToList()`. Mandatory
`.ToList()` materialisation — same correctness rationale as the `Select`
+ `.ToList()` pipeline. Idiom recorded as new active KB entry. **Note**:
`module.procedures` is passed through unchanged in the final `Module`
constructor — a deliberate reference alias (procedures are not re-checked
at this stage); the codegen MUST NOT silently clone here, or it changes
behaviour for downstream callers that mutate the returned module's
procedures map.

### Trivial / non-construct elements

- File header `// lib/analysis/...` and the spec-citation comments
  (`// Spec: docs/type system/typed-program.md, section "Parameterized
  Types"`, `// Paper: Section 8, Definition 8.1`) map to C# `//` comments
  mechanically — no research.
- `/// XML doc-comments` map 1-for-1 to C# `///` summary blocks — no
  research; Dart triple-slash and C# triple-slash semantics are
  identical.
- `import '../../compiler/ast.dart' as ast;` + `import 'type_ast.dart';`
  are subsumed by `using Glp.Compiler;` and `using Glp.Analysis.TypeChecker;`
  directives that the codegen emits per the project's namespace layout
  (cross-file concern; not specced per construct).
- The doc-comment "If the module has no parameterized types, returns it
  unchanged" is descriptive, not normative — the Step-1 comment
  ("don't return early if templates is empty — proc decls may reference
  prelude templates") clarifies this. Both port verbatim as XML-doc.
