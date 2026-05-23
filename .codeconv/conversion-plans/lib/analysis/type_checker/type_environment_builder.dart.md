---
path: lib/analysis/type_checker/type_environment_builder.dart
cycle_group_id: 12
scc_siblings: []
generated_at: 2026-05-21T16:02:00Z
source_sha256: dfd2a18574bdee84c8b2875529f6401ebd0a5cb60c16c619db3a842b519793fa
schema_version: 1
---

# Conversion Plan: lib/analysis/type_checker/type_environment_builder.dart

## 1. Source Analysis

The file is the **type-environment assembler** for the GLP type-checker pipeline. It lexes/parses the prelude (`programs/self.glp`), expands parameterised types, merges user definitions, resolves simple/union type aliases transitively with cycle detection, and validates determinism of every `TypeDef`. The file declares the following surface (verified by reading the source verbatim):

**Imports.**
- `type_ast.dart` (TypeEnvironment, TypeDef, TypeExpr, TypeRef, PrimitiveModeAlt, ConstantAlt, ListNilAlt, ListConsAlt, StructAlt, DiffListAlt, ProcDecl).
- `prelude.dart` (typePrelude, isPredefinedType, isPredefinedProcedure, isBuiltinProcedure).
- `param_expansion.dart` (expandParameterizedTypes).
- `../../compiler/ast.dart as ast` (Module, Clause).
- `../../compiler/lexer.dart` (Lexer).
- `../../compiler/parser.dart` (Parser).

**Four exception types** (lines 17–62) — identical shape `class <Name> implements Exception { final String message; final int line; final int column; <Name>(this.message, this.line, this.column); @override String toString() => '$message at line $line, column $column'; }`:
- `RedefinitionError` (illegal type/procedure redefinition).
- `CircularAliasError` (cycle in alias chain).
- `NonDeterministicTypeError` (duplicate functor / constant / overlapping primitive alts).
- `AliasExpansionError` (union alias references another alias, or undefined type).

**One mutable file-private nullable string** (line 65): `String? _preludeEnvironmentSource;` — set once at engine init by `setPreludeEnvironmentSource(String source)` (line 69), read many times by `buildPreludeEnvironment` via `??` fallback to `typePrelude`.

**Top-level public functions.**
- `setPreludeEnvironmentSource(String source)` (line 69) — engine-init hook (single-writer-many-readers).
- `buildPreludeEnvironment()` (line 74) — `??`-fallback source, empty-source early return, Lex→Parse→ParseModule, extract `isParameterized` templates into a `<String, TypeDef>` map, call `expandParameterizedTypes`, call `_buildEnvironmentFromModule(checkRedefinitions: false, resolveAliasesNow: true)`, return `TypeEnvironment(env.types, env.procedures, paramProcDecls: env.paramProcDecls, typeTemplates: preludeTemplates)`.
- `buildTypeEnvironment(ast.Module module, {TypeEnvironment? ancestorScope})` (line 115) — `?? buildPreludeEnvironment()` base; `_buildEnvironmentFromModule(checkRedefinitions: ancestorScope == null, resolveAliasesNow: false)`; `baseEnv.merge(userEnv)`; **defensive shallow copy** `Map<String, TypeDef>.from(merged.types)` + `Map<String, ProcDecl>.from(merged.procedures)`; `_resolveAliases(types, procedures)` mutates the copies in place; return `TypeEnvironment(types, procedures, paramProcDecls: merged.paramProcDecls)` — `paramProcDecls` deliberately NOT copied.
- `extractClauses(ast.Module module)` (line 201) — flattens all `proc.clauses` via `clauses.addAll(...)`.

**Top-level private helpers.**
- `_buildEnvironmentFromModule(ast.Module, {required bool checkRedefinitions, required bool resolveAliasesNow})` (line 134) — three `<String, TypeDef|ProcDecl>{}` accumulators; foreach `module.typeDefs` (Redefinition guard + determinism check on non-aliases + indexer assignment by `typeDef.name`); foreach `module.procDeclarations` (Redefinition guard + conditional `ProcDecl(..., isBuiltin: true, ...)` promotion + indexer assignment by `procDecl.qualifiedKey`); foreach `module.paramProcDecls` (indexer by `qualifiedKey`); conditional `_resolveAliases` call; return `TypeEnvironment(types, procedures, paramProcDecls: paramProcDecls)`.
- `_isSimpleAlias(TypeDef def)` (line 217) — `def.alternatives.length != 1 → false`; first alt is `PrimitiveModeAlt` or `TypeRef` → true; else false.
- `_isUnionAlias(TypeDef def)` (line 240) — `def.alternatives.length < 2 → false`; every alt must be a `TypeRef` to a non-predefined type; else false.
- `_isTypeAlias(TypeDef def)` (line 258) — `_isSimpleAlias(def) || _isUnionAlias(def)`.
- `_resolveAliases(Map<String, TypeDef> types, Map<String, ProcDecl> procedures)` (line 268) — **6-step pipeline**: (1) classify simple/union aliases into two local maps; (2) transitive resolve with a nested recursive closure `resolveSimpleAlias(String name)` capturing `resolved`/`visiting`/`simpleAliases` (DFS cycle detection via `visiting.add`/`visiting.remove` bracket; `CircularAliasError` on revisit); (3) expand union aliases (per-alt: forbid alias-references-alias, retain predefined `TypeRef`, lookup target, copy alts applying complement via `_applyComplementToAlt`; then `_checkDeterminism` on the expanded `TypeDef`); (4) replace simple-alias references in all non-simple-alias type defs (snapshot via `.toList()`); (5) replace alias references in procedure declarations (snapshot via `.toList()`, rebuild `ProcDecl` with full field carry-forward); (6) `types.remove(name)` for every simple alias.
- `_applyComplement(TypeExpr expr, bool applyComplement, int line, int column)` (line 434) — implements the type-theoretic involution `(T?)? = T`: `TypeRef` → fresh `TypeRef` with `isInput: !expr.isInput`; `PrimitiveModeAlt` → fresh with `!expr.isInput`; otherwise unchanged.
- `_applyComplementToAlt(TypeExpr alt, bool applyComplement, int line, int column)` (line 447) — recursive walker that complements ALL `TypeRef`/`PrimitiveModeAlt` leaves inside compound `ListConsAlt`/`StructAlt`/`DiffListAlt`; `ConstantAlt` and `ListNilAlt` pass through unchanged (no modes).
- `_replaceAliasReferences(TypeExpr expr, Map<String, TypeExpr> resolved)` (line 484) — recursive walker that looks up `TypeRef.name` in `resolved` (Dart `map[key]` returns `null` on miss); if found, returns `_applyComplement(resolvedTarget, expr.isInput, ...)`; else keeps the `TypeRef` as-is; recurses through compound alts.
- `_checkDeterminism(TypeDef def)` (line 546) — three `<String>{}` accumulators (`functors`, `constants`, `primitives`) + `bool hasWildcard`; switch on each alt by `is`-type and add to the right accumulator, throwing `NonDeterministicTypeError` on duplicate keys (`<functor>/<arity>`, `[]/0`, `[|]/2`, `\/2`, constant `.toString()`); `PrimitiveModeAlt` rejects co-existence with any other; `TypeRef` to `{Integer, Real, Number, String}` triggers `_checkPrimitiveOverlap`.
- `_checkPrimitiveOverlap(String newPrimitive, Set<String> existing, bool hasWildcard, TypeDef def)` (line 615) — four-way overlap check: wildcard-vs-anything; `Number` overlaps with `Integer`/`Real` (both directions); direct duplicate.

**Cross-file dependencies** (verbatim names that must resolve from peer convspecs): `Module.typeDefs`, `Module.procDeclarations`, `Module.paramProcDecls`, `Module.procedures`, `Procedure.clauses`, `Clause`, `TypeDef(name, alternatives, line, column)`, `TypeDef.isParameterized`, `TypeDef.alternatives`, `TypeDef.name`, `TypeDef.line`, `TypeDef.column`, `ProcDecl(name, argTypes, line, column, {isBuiltin, exported, imported, modulePath})`, `ProcDecl.key`, `ProcDecl.qualifiedKey`, `ProcDecl.isBuiltin`, `ProcDecl.exported`, `ProcDecl.imported`, `ProcDecl.modulePath`, `ProcDecl.arity`, `ProcDecl.argTypes`, `TypeEnvironment(types, procedures, {paramProcDecls, typeTemplates})`, `TypeEnvironment.types`, `TypeEnvironment.procedures`, `TypeEnvironment.paramProcDecls`, `TypeEnvironment.merge`, `TypeRef(name, line, column, {isInput})`, `TypeRef.name`, `TypeRef.isInput`, `TypeRef.line`, `TypeRef.column`, `PrimitiveModeAlt(isInput, line, column)`, `PrimitiveModeAlt.isInput`, `ConstantAlt.value`, `ListConsAlt(head, tail, line, column)`, `ListConsAlt.head`, `ListConsAlt.tail`, `StructAlt(functor, args, line, column)`, `StructAlt.functor`, `StructAlt.args`, `DiffListAlt(content, hole, line, column)`, `DiffListAlt.content`, `DiffListAlt.hole`, `Lexer.tokenize`, `Parser.parseModule`, `typePrelude`, `isPredefinedType`, `isPredefinedProcedure`, `isBuiltinProcedure`, `expandParameterizedTypes`.

No async, no isolates, no streams, no FFI, no I/O — pure synchronous, in-memory.

## 2. Dart → C#/.NET Conversion Plan

Single output unit `lib/analysis/type_checker/type_environment_builder.cs` in namespace `Glp.Analysis.TypeChecker`. The unit declares one `public static class TypeEnvironmentBuilder` (the function host) plus four `public sealed class <Name> : Exception` classes (the four domain exception types). The construct→target decisions below mirror the ratified convspec verbatim.

### C-1. Four exception classes (`RedefinitionError`, `CircularAliasError`, `NonDeterministicTypeError`, `AliasExpansionError`)

Emit four `public sealed class <Name> : Exception` types in `Glp.Analysis.TypeChecker`. Each derives from `System.Exception` (cached idiom `dart-implements-exception-to-csharp-derive-system-exception`, anchored in `error.dart.md`: Dart `Exception` is an interface, .NET has no throwable interface, derive from the concrete base). Surface (each class identical except for the type name):

- `public sealed class RedefinitionError : Exception { public long Line { get; } public long Column { get; } public RedefinitionError(string message, long line, long column) : base(message) { Line = line; Column = column; } public override string ToString() => $"{Message} at line {Line}, column {Column}"; }`.

`Line`/`Column` are `long` per the cross-file `rf-dart-int-to-csharp-long-width` precedent (opcodes.dart, error.dart). `message` routes via `: base(message)` so `Exception.Message` is set. `ToString()` REPLACES the base (no `base.ToString()` — preserves byte-shape of Dart `'<message> at line <L>, column <C>'`). No `[Serializable]`, no `(string, Exception inner)` chaining ctor (Dart source declares one ctor; preserve surface — FR-013). All four are `sealed` (no Dart subtypes). The `<Name>Error` suffix is preserved (project-wide policy inherited from the `error.dart.md` / `CompileError` escalation already closed by Gabi 2026-05-20).

### C-2. `static string?` field + setter

`private static string? _preludeEnvironmentSource;` on `TypeEnvironmentBuilder`. Setter:

`public static void SetPreludeEnvironmentSource(string source) => _preludeEnvironmentSource = source;`.

Dart `_`-prefix library-private → C# `private`. `String?` (NRT-enabled) → `string?`. NO `volatile`, NO `Interlocked.Exchange`, NO `Lazy<string>` — Dart isolates are single-threaded and ECMA-335 / CLI memory model documents reads/writes of reference types as atomic (cached idiom `dart-private-nullable-mutable-string-field-to-csharp-private-static-nullable`); adding stronger ordering would manufacture a contract Dart lacks (FR-013).

### C-3. `public static TypeEnvironment BuildPreludeEnvironment()`

```
public static TypeEnvironment BuildPreludeEnvironment()
{
    var source = _preludeEnvironmentSource ?? Prelude.TypePrelude;
    if (source.Length == 0)
        return new TypeEnvironment(
            new Dictionary<string, TypeDef>(StringComparer.Ordinal),
            new Dictionary<string, ProcDecl>(StringComparer.Ordinal));

    var lexer = new Lexer(source);
    var tokens = lexer.Tokenize();
    var parser = new Parser(tokens);
    var module = parser.ParseModule();

    var preludeTemplates = new Dictionary<string, TypeDef>(StringComparer.Ordinal);
    foreach (var td in module.TypeDefs)
        if (td.IsParameterized) preludeTemplates[td.Name] = td;

    var expandedModule = ParamExpansion.ExpandParameterizedTypes(module);
    var env = BuildEnvironmentFromModule(
        expandedModule,
        checkRedefinitions: false,
        resolveAliasesNow: true);
    return new TypeEnvironment(
        env.Types,
        env.Procedures,
        paramProcDecls: env.ParamProcDecls,
        typeTemplates: preludeTemplates);
}
```

Decisions: Dart `??` → C# `??` (Microsoft Learn — identical short-circuit semantics). Dart `isEmpty` → `Length == 0` (matches Dart `String.isEmpty` byte-exactly — the null branch is already handled by `??`). Empty `Map<>{}` literals → **fresh** `Dictionary<>` per call (NOT a shared static empty: `TypeEnvironment` is a mutable accumulator — cached nuance from type_ast.dart.md). `for (final ... in ...)` → `foreach`. Named-arg call (`paramProcDecls:`, `typeTemplates:`) preserves Dart shape and requires the `TypeEnvironment` ctor parameter names to match (cross-file constraint anchored in type_ast.dart.md). `Prelude.TypePrelude`, `Lexer.Tokenize`, `Parser.ParseModule`, `ParamExpansion.ExpandParameterizedTypes` are cross-file references anchored in their own convspecs.

### C-4. `public static TypeEnvironment BuildTypeEnvironment(Module module, TypeEnvironment? ancestorScope = null)`

```
public static TypeEnvironment BuildTypeEnvironment(
    Module module, TypeEnvironment? ancestorScope = null)
{
    var baseEnv = ancestorScope ?? BuildPreludeEnvironment();
    var userEnv = BuildEnvironmentFromModule(
        module,
        checkRedefinitions: ancestorScope == null,
        resolveAliasesNow: false);
    var merged = baseEnv.Merge(userEnv);
    var types = new Dictionary<string, TypeDef>(merged.Types, StringComparer.Ordinal);
    var procedures = new Dictionary<string, ProcDecl>(merged.Procedures, StringComparer.Ordinal);
    ResolveAliases(types, procedures);
    return new TypeEnvironment(
        types, procedures, paramProcDecls: merged.ParamProcDecls);
}
```

Decisions: optional Dart named param `{TypeEnvironment? ancestorScope}` → C# `TypeEnvironment? ancestorScope = null` with named-argument call syntax at call sites (cached idiom). `Map<K,V>.from(other)` → `new Dictionary<K,V>(other, StringComparer.Ordinal)` — Microsoft Learn confirms the C# copy-ctor performs the documented SHALLOW copy (`rf-csharp-dictionary-copy-constructor-shallow`). The defensive copy is **correctness-critical** because `ResolveAliases` mutates `types`/`procedures` in place (`types[k] = ...`, `types.Remove(k)`, `procedures[k] = ...`); without it those mutations would leak into the caller's `merged` environment (silent footgun). `paramProcDecls` is **deliberately not copied** — `ResolveAliases` does not touch it, and the source mirrors that (recorded as a maintenance invariant: if alias resolution is ever extended to `paramProcDecls`, add a defensive copy at that point).

### C-5. `private static TypeEnvironment BuildEnvironmentFromModule(Module module, bool checkRedefinitions, bool resolveAliasesNow)`

Dart `required` named bool params have no direct C# equivalent (`rf-csharp-required-named-bool-to-positional-bool-or-namedarg`: Microsoft Learn — the C# `required` keyword applies to *properties*, not method parameters; the documented faithful mapping is positional bool parameters in the signature combined with named-argument syntax at call sites). Signature: `private static TypeEnvironment BuildEnvironmentFromModule(Module module, bool checkRedefinitions, bool resolveAliasesNow)`. Body preserved step-for-step:

```
var types = new Dictionary<string, TypeDef>(StringComparer.Ordinal);
var procedures = new Dictionary<string, ProcDecl>(StringComparer.Ordinal);
var paramProcDecls = new Dictionary<string, ProcDecl>(StringComparer.Ordinal);

foreach (var typeDef in module.TypeDefs)
{
    if (checkRedefinitions && Prelude.IsPredefinedType(typeDef.Name))
        throw new RedefinitionError(
            $"Cannot redefine predefined type: {typeDef.Name}",
            typeDef.Line, typeDef.Column);
    if (!IsTypeAlias(typeDef)) CheckDeterminism(typeDef);
    types[typeDef.Name] = typeDef;
}

foreach (var procDecl in module.ProcDeclarations)
{
    if (checkRedefinitions && Prelude.IsPredefinedProcedure(procDecl.Name))
        throw new RedefinitionError(
            $"Cannot redefine predefined procedure: {procDecl.Name}/{procDecl.Arity}",
            procDecl.Line, procDecl.Column);
    var isBuiltin = Prelude.IsBuiltinProcedure(procDecl.Key);
    procedures[procDecl.QualifiedKey] = (isBuiltin && !procDecl.IsBuiltin)
        ? new ProcDecl(
            procDecl.Name, procDecl.ArgTypes, procDecl.Line, procDecl.Column,
            isBuiltin: true,
            exported: procDecl.Exported,
            imported: procDecl.Imported,
            modulePath: procDecl.ModulePath)
        : procDecl;
}

foreach (var paramDecl in module.ParamProcDecls)
    paramProcDecls[paramDecl.QualifiedKey] = paramDecl;

if (resolveAliasesNow) ResolveAliases(types, procedures);

return new TypeEnvironment(
    types, procedures, paramProcDecls: paramProcDecls);
```

Decisions: `<String, V>{}` set/map literal → `new Dictionary<string, V>(StringComparer.Ordinal)` (cached ordinal-discipline thread). Indexer assignment matches Dart `Map[]=` (LAST-wins). The conditional `ProcDecl`-with-`isBuiltin:true` construction creates a fresh node ONLY when the flag needs to be promoted; otherwise the dictionary aliases the original — preserving Dart's reference-aliasing semantic exactly. All field carry-forward (`Exported`, `Imported`, `ModulePath`, `ArgTypes`, `Line`, `Column`) is byte-exact. `procedures` keyed by `QualifiedKey` (module-qualified) — not by `Name`+`Arity`. Call sites within this file (C-3, C-4) use named-argument syntax (`checkRedefinitions: ..., resolveAliasesNow: ...`) to preserve the Dart self-documenting call-site labels (boolean-blindness mitigation; cached idiom `dart-private-static-module-assembler-with-flag-params`).

### C-6. `public static List<Clause> ExtractClauses(Module module)`

```
public static List<Clause> ExtractClauses(Module module)
{
    var clauses = new List<Clause>();
    foreach (var proc in module.Procedures) clauses.AddRange(proc.Clauses);
    return clauses;
}
```

Dart `List.addAll(other)` → C# `List<T>.AddRange(IEnumerable<T>)` — Microsoft Learn confirms shape (`rf-csharp-list-addrange-vs-linq-selectmany`); same in-place O(n) append + reference-aliasing semantic. The LINQ alternative `module.Procedures.SelectMany(p => p.Clauses).ToList()` is recorded in the convspec as an optional codegen micro-optimisation; the imperative form is the spec default for review parity with the Dart source.

### C-7. Predicate helpers `IsSimpleAlias`, `IsUnionAlias`, `IsTypeAlias`

```
private static bool IsSimpleAlias(TypeDef def)
{
    if (def.Alternatives.Count != 1) return false;
    var alt = def.Alternatives[0];
    return alt is PrimitiveModeAlt || alt is TypeRef;
}

private static bool IsUnionAlias(TypeDef def)
{
    if (def.Alternatives.Count < 2) return false;
    foreach (var alt in def.Alternatives)
    {
        if (alt is not TypeRef r) return false;
        if (Prelude.IsPredefinedType(r.Name)) return false;
    }
    return true;
}

private static bool IsTypeAlias(TypeDef def) =>
    IsSimpleAlias(def) || IsUnionAlias(def);
```

Decisions: Dart `is`/`as` fused into C# declaration pattern `alt is not TypeRef r` (Microsoft Learn — `rf-dart-extension-is-as-to-csharp-type-pattern-switch`), eliminating the double-test + `InvalidCast` hazard. Dart `length` → C# `Count` (List/IList semantics identical). The predefined-type guard is the load-bearing distinction between a union alias and a primitive-union type (e.g. `Constant ::= Number ; String` is NOT a union alias) — preserved verbatim.

### C-8. `private static void ResolveAliases(IDictionary<string, TypeDef> types, IDictionary<string, ProcDecl> procedures)`

The six-step pipeline is preserved verbatim. The nested recursive closure `resolveSimpleAlias` is ported as a **non-static C# local function** inside `ResolveAliases` so it captures the enclosing `resolved` / `visiting` / `simpleAliases` locals naturally (cached idiom `dart-multistep-pipeline-with-recursive-closure-and-cycle-detection-to-csharp-local-function`; `rf-csharp-local-function-vs-lambda-recursive` — Microsoft Learn confirms local functions support natural recursion *and* enclosing-scope capture when non-static; lambdas would require the `Func<...>` self-reference workaround).

```
private static void ResolveAliases(
    IDictionary<string, TypeDef> types,
    IDictionary<string, ProcDecl> procedures)
{
    // Step 1: classify simple vs union aliases.
    var simpleAliases = new Dictionary<string, TypeDef>(StringComparer.Ordinal);
    var unionAliases = new Dictionary<string, TypeDef>(StringComparer.Ordinal);
    foreach (var entry in types)
    {
        if (IsSimpleAlias(entry.Value)) simpleAliases[entry.Key] = entry.Value;
        else if (IsUnionAlias(entry.Value)) unionAliases[entry.Key] = entry.Value;
    }
    if (simpleAliases.Count == 0 && unionAliases.Count == 0) return;

    // Step 2: transitive resolve with cycle detection.
    var resolved = new Dictionary<string, TypeExpr>(StringComparer.Ordinal);
    var visiting = new HashSet<string>(StringComparer.Ordinal);

    TypeExpr ResolveSimpleAliasLocal(string name)
    {
        if (resolved.TryGetValue(name, out var prior)) return prior;
        if (!simpleAliases.TryGetValue(name, out var aliasDef))
            return new TypeRef(name, 0, 0);
        if (visiting.Contains(name))
            throw new CircularAliasError(
                $"Circular alias chain detected: {name}",
                aliasDef.Line, aliasDef.Column);
        visiting.Add(name);
        var target = aliasDef.Alternatives[0];
        TypeExpr result;
        if (target is TypeRef tr)
        {
            if (simpleAliases.ContainsKey(tr.Name))
            {
                var resolvedTarget = ResolveSimpleAliasLocal(tr.Name);
                result = ApplyComplement(resolvedTarget, tr.IsInput, tr.Line, tr.Column);
            }
            else result = tr;
        }
        else if (target is PrimitiveModeAlt) result = target;
        else result = target;
        visiting.Remove(name);
        resolved[name] = result;
        return result;
    }

    foreach (var name in simpleAliases.Keys) ResolveSimpleAliasLocal(name);

    // Step 3: expand union aliases (per-alt foreach + complement + determinism check).
    foreach (var entry in unionAliases)
    {
        var name = entry.Key;
        var def = entry.Value;
        var expandedAlts = new List<TypeExpr>();
        foreach (var alt in def.Alternatives)
        {
            var r = (TypeRef)alt;  // verified by IsUnionAlias
            if (simpleAliases.ContainsKey(r.Name) || unionAliases.ContainsKey(r.Name))
                throw new AliasExpansionError(
                    $"Union alias cannot reference another alias: {r.Name}",
                    def.Line, def.Column);
            if (Prelude.IsPredefinedType(r.Name)) { expandedAlts.Add(r); continue; }
            if (!types.TryGetValue(r.Name, out var targetDef))
                throw new AliasExpansionError(
                    $"Union alias references undefined type: {r.Name}",
                    def.Line, def.Column);
            foreach (var targetAlt in targetDef.Alternatives)
                expandedAlts.Add(ApplyComplementToAlt(targetAlt, r.IsInput, def.Line, def.Column));
        }
        var expandedDef = new TypeDef(name, expandedAlts, def.Line, def.Column);
        CheckDeterminism(expandedDef);
        types[name] = expandedDef;
    }

    // Step 4: replace simple alias refs in non-simple-alias type defs (snapshot via ToList).
    var nonSimpleAliasTypes = types
        .Where(e => !simpleAliases.ContainsKey(e.Key))
        .ToList();
    foreach (var entry in nonSimpleAliasTypes)
    {
        var newAlternatives = new List<TypeExpr>();
        foreach (var alt in entry.Value.Alternatives)
            newAlternatives.Add(ReplaceAliasReferences(alt, resolved));
        types[entry.Key] = new TypeDef(
            entry.Value.Name, newAlternatives,
            entry.Value.Line, entry.Value.Column);
    }

    // Step 5: replace alias refs in procedure declarations (snapshot via ToList).
    foreach (var entry in procedures.ToList())
    {
        var newArgTypes = new List<TypeExpr>();
        foreach (var argType in entry.Value.ArgTypes)
            newArgTypes.Add(ReplaceAliasReferences(argType, resolved));
        procedures[entry.Key] = new ProcDecl(
            entry.Value.Name, newArgTypes,
            entry.Value.Line, entry.Value.Column,
            isBuiltin: entry.Value.IsBuiltin,
            exported: entry.Value.Exported,
            imported: entry.Value.Imported,
            modulePath: entry.Value.ModulePath);
    }

    // Step 6: remove simple alias definitions from types map.
    foreach (var name in simpleAliases.Keys.ToList()) types.Remove(name);
}
```

Decisions: `Map.entries` → `foreach` over `KeyValuePair<TKey,TValue>` directly. `Map[key]` (returns null on miss) → `TryGetValue(out var t)` (cached idiom `dart-map-indexer-null-on-miss-to-csharp-trygetvalue`; `rf-csharp-dictionary-trygetvalue-vs-indexer-null-vs-keynotfound` — Microsoft Learn: C# indexer THROWS `KeyNotFoundException` on miss; the documented faithful counterpart is `TryGetValue`). `.ToList()` snapshots before mutating-while-iterating are **correctness-critical** (`Dictionary` enumeration throws `InvalidOperationException` if the collection is modified during iteration — Microsoft Learn). `HashSet<string>` `Add`/`Contains`/`Remove` match Dart `Set<String>` byte-shape. The `visiting.Add(name)` / `visiting.Remove(name)` bracket implements DFS cycle detection identically; `CircularAliasError` is thrown with the alias *definition*'s line/column (not the recursive call site). All accumulator dicts use `StringComparer.Ordinal`.

### C-9. `private static TypeExpr ApplyComplement(TypeExpr expr, bool applyComplement, long line, long column)`

```
private static TypeExpr ApplyComplement(
    TypeExpr expr, bool applyComplement, long line, long column)
{
    if (!applyComplement) return expr;
    return expr switch
    {
        TypeRef r => new TypeRef(r.Name, line, column, isInput: !r.IsInput),
        PrimitiveModeAlt p => new PrimitiveModeAlt(!p.IsInput, line, column),
        _ => expr,
    };
}
```

Implements the type-theoretic involution `(T?)? = T`. Early exit when no complement requested (preserves Dart byte-shape). The switch expression maps Dart `is` chains 1:1 (cached idiom).

### C-10. `private static TypeExpr ApplyComplementToAlt(TypeExpr alt, bool applyComplement, long line, long column)`

```
private static TypeExpr ApplyComplementToAlt(
    TypeExpr alt, bool applyComplement, long line, long column)
{
    if (!applyComplement) return alt;
    switch (alt)
    {
        case TypeRef r:
            return new TypeRef(r.Name, line, column, isInput: !r.IsInput);
        case PrimitiveModeAlt p:
            return new PrimitiveModeAlt(!p.IsInput, line, column);
        case ConstantAlt: return alt;       // no mode
        case ListNilAlt: return alt;        // no mode
        case ListConsAlt lc:
            return new ListConsAlt(
                ApplyComplementToAlt(lc.Head, true, line, column),
                ApplyComplementToAlt(lc.Tail, true, line, column),
                line, column);
        case StructAlt s:
            return new StructAlt(
                s.Functor,
                s.Args.Select(a => ApplyComplementToAlt(a, true, line, column)).ToList(),
                line, column);
        case DiffListAlt d:
            return new DiffListAlt(
                ApplyComplementToAlt(d.Content, true, line, column),
                ApplyComplementToAlt(d.Hole, true, line, column),
                line, column);
        default: return alt;
    }
}
```

The asymmetry between fresh-allocation arms (`TypeRef`/`PrimitiveModeAlt`/`ListConsAlt`/`StructAlt`/`DiffListAlt`) and pass-through arms (`ConstantAlt`/`ListNilAlt`/default) is **load-bearing** — preserves Dart's identical asymmetry exactly. `.Select(...).ToList()` materialisation on `StructAlt.Args` is MANDATORY (cached deferred-execution nuance from param_expansion.dart.md: without `.ToList()` the projection re-runs on every enumeration and produces distinct-but-equal nodes — silent regression for downstream alias-replacement passes). The `default:` fall-through preserves Dart's terminal `return alt;` for the closed-sum exhaustiveness gap (C# does not compile-time verify subtype exhaustiveness over a non-language-sealed base — keeps the function total).

### C-11. `private static TypeExpr ReplaceAliasReferences(TypeExpr expr, IDictionary<string, TypeExpr> resolved)`

```
private static TypeExpr ReplaceAliasReferences(
    TypeExpr expr, IDictionary<string, TypeExpr> resolved)
{
    switch (expr)
    {
        case TypeRef r:
            if (resolved.TryGetValue(r.Name, out var resolvedTarget))
                return ApplyComplement(resolvedTarget, r.IsInput, r.Line, r.Column);
            return r;
        case PrimitiveModeAlt: return expr;
        case ConstantAlt: return expr;
        case ListNilAlt: return expr;
        case ListConsAlt lc:
            return new ListConsAlt(
                ReplaceAliasReferences(lc.Head, resolved),
                ReplaceAliasReferences(lc.Tail, resolved),
                lc.Line, lc.Column);
        case StructAlt s:
            return new StructAlt(
                s.Functor,
                s.Args.Select(a => ReplaceAliasReferences(a, resolved)).ToList(),
                s.Line, s.Column);
        case DiffListAlt d:
            return new DiffListAlt(
                ReplaceAliasReferences(d.Content, resolved),
                ReplaceAliasReferences(d.Hole, resolved),
                d.Line, d.Column);
        default: return expr;
    }
}
```

The `TypeRef` arm uses `TryGetValue` — the classic Dart→C# silent-bug pitfall (Dart `Map[]` returns `null`; C# indexer THROWS `KeyNotFoundException` — `rf-csharp-dictionary-trygetvalue-vs-indexer-null-vs-keynotfound`). Pass-through arms return `expr` unchanged (reference preserved — same identity semantic as Dart).

### C-12. `private static void CheckDeterminism(TypeDef def)`

```
private static void CheckDeterminism(TypeDef def)
{
    var functors = new HashSet<string>(StringComparer.Ordinal);
    var constants = new HashSet<string>(StringComparer.Ordinal);
    var primitives = new HashSet<string>(StringComparer.Ordinal);
    var hasWildcard = false;

    foreach (var alt in def.Alternatives)
    {
        switch (alt)
        {
            case ConstantAlt c:
            {
                var key = c.Value.ToString() ?? "null";
                if (!constants.Add(key))
                    throw new NonDeterministicTypeError(
                        $"Duplicate constant alternative: {key} in {def.Name}",
                        def.Line, def.Column);
                break;
            }
            case ListNilAlt:
                if (!functors.Add("[]/0"))
                    throw new NonDeterministicTypeError(
                        $"Duplicate [] alternative in {def.Name}",
                        def.Line, def.Column);
                break;
            case ListConsAlt:
                if (!functors.Add("[|]/2"))
                    throw new NonDeterministicTypeError(
                        $"Duplicate [|] alternative in {def.Name}",
                        def.Line, def.Column);
                break;
            case StructAlt s:
            {
                var key = $"{s.Functor}/{s.Args.Count}";
                if (!functors.Add(key))
                    throw new NonDeterministicTypeError(
                        $"Duplicate functor alternative: {key} in {def.Name}",
                        def.Line, def.Column);
                break;
            }
            case DiffListAlt:
                if (!functors.Add("\\/2"))
                    throw new NonDeterministicTypeError(
                        $"Duplicate \\ alternative in {def.Name}",
                        def.Line, def.Column);
                break;
            case PrimitiveModeAlt:
                if (hasWildcard || primitives.Count > 0)
                    throw new NonDeterministicTypeError(
                        $"Wildcard _ overlaps with other alternatives in {def.Name}",
                        def.Line, def.Column);
                hasWildcard = true;
                break;
            case TypeRef r:
                if (PrimitiveTypeNames.Contains(r.Name))
                {
                    CheckPrimitiveOverlap(r.Name, primitives, hasWildcard, def);
                    primitives.Add(r.Name);
                }
                break;
        }
    }
}

private static readonly FrozenSet<string> PrimitiveTypeNames =
    new[] { "Integer", "Real", "Number", "String" }.ToFrozenSet(StringComparer.Ordinal);
```

Decisions: Dart `<String>{}` set literal → `new HashSet<string>(StringComparer.Ordinal)` (cached). `set.contains(k) → throw; set.add(k);` two-step fused into `if (!set.Add(k)) throw;` (Microsoft Learn — `HashSet<T>.Add` returns false on duplicate; cached `rf-csharp-hashset-add-returns-false-on-duplicate`). `c.Value.ToString() ?? "null"` sentinel guards potential null from `object.ToString()` (Dart's `alt.value.toString()` would itself throw on null, but Dart `ConstantAlt` already guarantees non-null value; the sentinel is defensive and never observed). The Dart literal `'\\/2'` is the two-char string `\/2`; C# `"\\/2"` is also `\/2` (both languages: `\\` = single backslash) — bytes match exactly. The inline `{Integer, Real, Number, String}` set is hoisted to a `static readonly FrozenSet<string>` (cached idiom from prelude.dart for hot-ish predicate sets) — the field-level optimisation is recorded in the convspec; the inline `new[]{...}.Contains(...)` form remains an acceptable lower-priority alternative.

### C-13. `private static void CheckPrimitiveOverlap(string newPrimitive, ISet<string> existing, bool hasWildcard, TypeDef def)`

```
private static void CheckPrimitiveOverlap(
    string newPrimitive, ISet<string> existing, bool hasWildcard, TypeDef def)
{
    if (hasWildcard)
        throw new NonDeterministicTypeError(
            $"Wildcard _ overlaps with {newPrimitive} in {def.Name}",
            def.Line, def.Column);
    if (newPrimitive == "Number"
        && (existing.Contains("Integer") || existing.Contains("Real")))
        throw new NonDeterministicTypeError(
            $"Number overlaps with Integer/Real in {def.Name}",
            def.Line, def.Column);
    if ((newPrimitive == "Integer" || newPrimitive == "Real")
        && existing.Contains("Number"))
        throw new NonDeterministicTypeError(
            $"{newPrimitive} overlaps with Number in {def.Name}",
            def.Line, def.Column);
    if (existing.Contains(newPrimitive))
        throw new NonDeterministicTypeError(
            $"Duplicate primitive type {newPrimitive} in {def.Name}",
            def.Line, def.Column);
}
```

Direct transliteration of the four-arm overlap check. Domain invariant (type-environment.md v0.5 mandate). Preserved verbatim.

### C-14. XML doc-comments

Each `///` triple-slash doc-comment from the Dart source is ported 1:1 to C# `/// <summary>...</summary>` / `/// <remarks>...</remarks>` blocks on the corresponding C# member. The file-level spec citation `// Specification: docs/modules/type-environment.md v0.8` becomes a class-level `/// <remarks>` on `TypeEnvironmentBuilder`. Dart `///` and C# `///` doc-comment semantics are identical.

### Trivial / non-construct elements

- File header `//` line-comments map mechanically to C# `//` line-comments.
- `import` directives subsumed by `using Glp.Analysis.TypeChecker;` / `using Glp.Compiler;` `using` directives at codegen time (cross-file concern; not specced per construct).
- `final` field declarations (Dart) → get-only auto-properties (C#) — cached mechanical mapping.

## 3. Decomposed Task Units

- **T1.** Emit `lib/analysis/type_checker/type_environment_builder.cs` namespace + file header + `using` directives — done.
- **T2.** Emit four `public sealed class <Name> : Exception` classes (RedefinitionError, CircularAliasError, NonDeterministicTypeError, AliasExpansionError) with `Line`/`Column` get-only `long` properties + `(string, long, long)` ctor + override `ToString()` — done.
- **T3.** Emit `public static class TypeEnvironmentBuilder` host + `private static string?` field + `SetPreludeEnvironmentSource` setter — done.
- **T4.** Emit `BuildPreludeEnvironment()` (C-3) — done.
- **T5.** Emit `BuildTypeEnvironment(Module, TypeEnvironment? = null)` (C-4) — done.
- **T6.** Emit `BuildEnvironmentFromModule(Module, bool, bool)` (C-5) — done.
- **T7.** Emit `ExtractClauses(Module)` (C-6) — done.
- **T8.** Emit predicate helpers `IsSimpleAlias` / `IsUnionAlias` / `IsTypeAlias` (C-7) — done.
- **T9.** Emit `ResolveAliases(IDictionary, IDictionary)` 6-step pipeline with non-static local function `ResolveSimpleAliasLocal` (C-8) — done.
- **T10.** Emit `ApplyComplement` involution helper (C-9) — done.
- **T11.** Emit `ApplyComplementToAlt` recursive AST walker (C-10) — done.
- **T12.** Emit `ReplaceAliasReferences` recursive AST walker with `TryGetValue` (C-11) — done.
- **T13.** Emit `CheckDeterminism(TypeDef)` (C-12) + `PrimitiveTypeNames` FrozenSet field — done.
- **T14.** Emit `CheckPrimitiveOverlap(string, ISet, bool, TypeDef)` (C-13) — done.
- **T15.** Port all `///` XML doc-comments verbatim (C-14) — done.
- **T16.** Ensure call sites within this file (T4, T5) use named-argument syntax (`checkRedefinitions: ..., resolveAliasesNow: ...`, `paramProcDecls: ...`, `typeTemplates: ...`) — done.

## 4. Research Findings

None required. Every construct in this plan is covered by either a cached idiom or a research finding already ratified in the convspec (`rf-dart-implements-exception-to-csharp-derive-system-exception`, `rf-dart-tostring-interp-to-csharp-tostring-interp`, `rf-csharp-static-nullable-field-thread-safety-considerations`, `rf-csharp-mutable-local-accumulator-pure-function`, `rf-csharp-dictionary-copy-constructor-shallow`, `rf-csharp-required-named-bool-to-positional-bool-or-namedarg`, `rf-csharp-list-addrange-vs-linq-selectmany`, `rf-dart-extension-is-as-to-csharp-type-pattern-switch`, `rf-csharp-local-function-vs-lambda-recursive`, `rf-csharp-dictionary-trygetvalue-vs-indexer-null-vs-keynotfound`, `rf-csharp-hashset-add-returns-false-on-duplicate`, `rf-dart-int-to-csharp-long-width`). All cross-file dependencies (`Module`, `TypeDef`, `ProcDecl`, `TypeEnvironment`, `TypeRef`/`PrimitiveModeAlt`/`ConstantAlt`/`ListNilAlt`/`ListConsAlt`/`StructAlt`/`DiffListAlt`/`TypeExpr`, `Lexer`/`Parser`, `Prelude.*`, `ParamExpansion.*`) are anchored in their respective convspecs; this plan records dependencies without redefining them.

## 5. Consistency Pass

Fixed — derived from convspec `.codeconv/conversion-specs/lib/analysis/type_checker/type_environment_builder.dart.md` (constructs C-1 through C-13 match the convspec's `constructs:` array entry-for-entry; `conversion_units:` block enumerates the same 17 emission units this plan emits; `escalations: []` matches §6 below). Cross-file ctor/method-signature constraints derived from the named-argument call sites in the convspec (anchored in type_ast.dart.md / ast.dart.md / lexer.dart.md / parser.dart.md / prelude.dart.md / param_expansion.dart.md). Suffix-naming-policy (`<Name>Error` over `<Name>Exception`) inherited from the project-wide decision already closed against `CompileError` in `error.dart.md` (Gabi 2026-05-20) — this file does not re-decide. Per the planning prompt, any plan touch on `TypeEnvironment.getType(String)` would defer to type_ast.dart E1; verified by Grep that this source contains **no `getType`/`GetType` references**, so the deferral is vacuously satisfied.

## 6. Escalations

None.
