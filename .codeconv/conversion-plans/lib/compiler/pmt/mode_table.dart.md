---
path: lib/compiler/pmt/mode_table.dart
cycle_group_id: 55
scc_siblings: []
generated_at: 2026-05-21T15:00:16Z
source_sha256: 64b7072930ab5bb1260bf1507e236ce16dff7494d456d04154c142fdd4618b21
schema_version: 1
---

# Conversion Plan: lib/compiler/pmt/mode_table.dart

## 1. Source Analysis

The source file (`glp_runtime_net/lib/compiler/pmt/mode_table.dart`, 119 lines) implements the PMT (Predicate Mode Table) used by the GLP type-checking pipeline. Direct inspection of the `.dart` reveals:

- **Imports (line 9):** `import '../ast.dart';` — the only dependency, supplying `ModeDeclaration` (and its `signature` / `args` / `typeName` accessors and the `ModedArg.isReader` flag).
- **Doc-comments (lines 1-7, 11, 17-22, 30-33, 45-48, 54-57, 62, 67, 73-75, 81-83, 88, 102, 105, 108, 111):** all `///` triple-slash form. Load-bearing semantic on the `length` getter ("Get number of unique predicates (not counting alternatives)"); load-bearing union semantics on `addDeclaration` ("If a declaration for the same predicate/arity already exists, adds this as an alternative mode (for union declarations)").
- **`enum Mode` (lines 11-15):** two members in declaration order — `reader` (Input), `writer` (Output). No constructor, no methods, no associated values.
- **`class ModeTable` (lines 23-119):** mutable container, no inheritance.
  - **Fields (lines 25, 28):** `final Map<String, List<List<Mode>>> _modes = {};` and `final Map<String, List<ModeDeclaration>> _declarations = {};` — two parallel dictionaries keyed by `"$predicate/$arity"` strings, kept structurally synchronised by `addDeclaration`.
  - **`addDeclaration(ModeDeclaration decl)` (lines 34-43):** extracts `signature = decl.signature`; builds `modes` via `decl.args.map((arg) => arg.isReader ? Mode.reader : Mode.writer).toList()`; performs two parallel `putIfAbsent(signature, () => []).add(...)` chains, `_modes` first then `_declarations`.
  - **`getModes(String, int)` (lines 49-52):** `_modes['$predicate/$arity']` indexer lookup → `allModes?.isNotEmpty == true ? allModes!.first : null` triple-state collapse (missing → null, empty → null, non-empty → first).
  - **`getAllModes(String, int)` (lines 58-60):** direct indexer return, nullable.
  - **`hasDeclaration(String, int)` (lines 63-65):** `_modes.containsKey('$predicate/$arity')`.
  - **`hasMultipleModes(String, int)` (lines 68-71):** indexer lookup + `length > 1` guard.
  - **`getDeclaration(String, int)` (lines 76-79):** mirror of `getModes` over `_declarations`.
  - **`getAllDeclarations(String, int)` (lines 84-86):** mirror of `getAllModes` over `_declarations`.
  - **`getDeclarationByTypeName(String typeName)` (lines 91-100):** nested `for ... in` over `_declarations.values` then inner list; early-return on first `decl.typeName == typeName`; `null` fallthrough.
  - **`signatures` getter (line 103):** `Iterable<String> get signatures => _modes.keys;` — live view.
  - **`length` getter (line 106):** `int get length => _modes.length;` — counts UNIQUE predicates (not alternatives) per doc-comment.
  - **`isEmpty` getter (line 109):** `bool get isEmpty => _modes.isEmpty;`.
  - **`fromDeclarations` static factory (lines 112-118):** constructs `ModeTable()`, iterates input, calls `addDeclaration` on each, returns the populated table.

No async/Future/Stream, no isolates, no `late`, no inheritance/mixins/sealed types, no extension methods. Leaf class.

## 2. Dart → C#/.NET Conversion Plan

The convspec lists 10 constructs at `constructs[*].construct_key`. Each is mirrored verbatim below (target_decision summarised; full target wording lives in the spec).

1. **`dart.enum.plain_two_members_reader_writer`** → C# `enum Mode { Reader, Writer }` placed in namespace `<root>.Compiler.Pmt` (mirroring directory). Two members in source order so `(Mode)0 == Reader`, `(Mode)1 == Writer`. PascalCase per .NET naming guidelines. No methods, no static aliases, no ToString override. No `Mode?` use anywhere in this file's surface (enum members are non-nullable values). The pmt-namespace `Mode` does NOT collide with the unrelated `analysis/type_checker/mode.dart` `Mode` — different directory → different namespace.

2. **`dart.collection_class.dual_map_string_to_list_of_t_signature_keyed_by_predicate_slash_arity`** → public reference-type `class ModeTable` with two `private readonly Dictionary<string, List<...>>` fields, BOTH constructed with `StringComparer.Ordinal`: `private readonly Dictionary<string, List<List<Mode>>> _modes = new(StringComparer.Ordinal);` and `private readonly Dictionary<string, List<ModeDeclaration>> _declarations = new(StringComparer.Ordinal);`. The two-dictionary structure MUST NOT collapse into a single dictionary-of-pair — the Dart source separately exposes `getAllModes` (List<List<Mode>>) and `getAllDeclarations` (List<ModeDeclaration>).

3. **`dart.map.putifabsent_list_factory_then_append`** → cached `TryGetValue`-then-`Add`-then-append idiom. Per call site, four-statement block: `if (!_modes.TryGetValue(signature, out var modesBucket)) { modesBucket = new List<List<Mode>>(); _modes[signature] = modesBucket; } modesBucket.Add(modes);` and the parallel form for `_declarations`. Lazy default-construction preserved (empty list constructed only on miss arm). Order: `_modes` first, `_declarations` second (Dart source order). Reject `CollectionsMarshal.GetValueRefOrAddDefault` (project precedent: well_typed_term.dart and message_queue.dart reject it as outside scope).

4. **`dart.string_interpolation.predicate_slash_arity_signature`** → C# `$"{predicate}/{arity}"` at all six call sites. No `string.Format`, no `StringBuilder` rewrite, no hoisting to a single computed local. `arity` is `int`; default integer formatting is invariant decimal-digit, identical to Dart `int.toString()`.

5. **`dart.nullable_first_or_default_via_isnotempty_then_first_bang`** → two-statement TryGetValue + Count guard. For `getModes`: `if (!_modes.TryGetValue($"{predicate}/{arity}", out var allModes) || allModes.Count == 0) return null; return allModes[0];`. Parallel form for `getDeclaration` over `_declarations`. The three-state Dart collapse (missing key → null; key present + empty list → null; key present + non-empty → first element) is preserved across the `||` guard. Use indexer `[0]` (allocation-free), NOT LINQ `.First()`.

6. **`dart.map.containsKey_signature_lookup`** → one-liner expression-bodied C# method: `public bool HasDeclaration(string predicate, int arity) => _modes.ContainsKey($"{predicate}/{arity}");`. Both APIs total; both O(1).

7. **`dart.collection.nested_for_in_search_returning_first_match_or_null`** → direct nested `foreach` translation preserving early-return: `public ModeDeclaration? GetDeclarationByTypeName(string typeName) { foreach (var decls in _declarations.Values) { foreach (var decl in decls) { if (decl.TypeName == typeName) return decl; } } return null; }`. Do NOT rewrite to LINQ `SelectMany + FirstOrDefault`. String equality on `decl.TypeName == typeName` is ordinal under project convention (well_typed_term.dart establishes explicit-comparer convention; codegen MAY use `Equals(typeName, StringComparison.Ordinal)` or rely on bare `==` which is ordinal for `string` in C#).

8. **`dart.getter.iterable_keys_property`** → C# get-only expression-bodied property returning `IEnumerable<string>`: `public IEnumerable<string> Signatures => _modes.Keys;`. Live `KeyCollection` view preserved. Do NOT materialise to `List<string>` or `IReadOnlyList<string>` — callers that need a snapshot materialise themselves.

9. **`dart.getter.length_proxy_to_inner_collection_length`** → two get-only expression-bodied properties: `public int Count => _modes.Count;` (renamed `length`→`Count` per .NET Framework Design Guidelines; `Length` reserved for arrays / `string` / `StringBuilder`) and `public bool IsEmpty => _modes.Count == 0;` (no single-token C# equivalent on `Dictionary`). XML-doc on `Count` carries the Dart doc-comment verbatim: "Get number of unique predicates (not counting alternatives)".

10. **`dart.static_factory.classmethod_builds_instance_from_iterable`** → `public static ModeTable FromDeclarations(IReadOnlyList<ModeDeclaration> declarations) { var table = new ModeTable(); foreach (var decl in declarations) { table.AddDeclaration(decl); } return table; }`. Parameter typed `IReadOnlyList<ModeDeclaration>` (NOT `List<...>`, NOT `IEnumerable<...>`). Verbatim imperative loop, NOT a LINQ chain (`AddDeclaration` has side-effects).

## 3. Decomposed Task Units

- T1: Emit `enum Mode { Reader, Writer }` in pmt namespace (per construct 1).
- T2: Emit `public class ModeTable` declaration with two `Dictionary<string, List<...>>` private readonly fields, both `new(StringComparer.Ordinal)` (per construct 2).
- T3: Emit `public void AddDeclaration(ModeDeclaration decl)` with two parallel TryGetValue-Add-append blocks in order `_modes` then `_declarations`; build `modes` via `decl.Args.Select(arg => arg.IsReader ? Mode.Reader : Mode.Writer).ToList()` (per constructs 1 + 3 + 4).
- T4: Emit `public List<Mode>? GetModes(string predicate, int arity)` with TryGetValue + Count==0 guard returning `[0]` or `null` (per constructs 4 + 5).
- T5: Emit `public List<List<Mode>>? GetAllModes(string predicate, int arity)` with TryGetValue + nullable return (per constructs 4 + 5).
- T6: Emit `public bool HasDeclaration(string predicate, int arity) => _modes.ContainsKey($"{predicate}/{arity}");` (per constructs 4 + 6).
- T7: Emit `public bool HasMultipleModes(string predicate, int arity)` with TryGetValue + Count > 1 guard (per constructs 4 + 5).
- T8: Emit `public ModeDeclaration? GetDeclaration(string predicate, int arity)` mirroring T4 over `_declarations` (per constructs 4 + 5).
- T9: Emit `public List<ModeDeclaration>? GetAllDeclarations(string predicate, int arity)` mirroring T5 over `_declarations` (per constructs 4 + 5).
- T10: Emit `public ModeDeclaration? GetDeclarationByTypeName(string typeName)` with nested foreach + early return + null fallthrough (per construct 7).
- T11: Emit `public IEnumerable<string> Signatures => _modes.Keys;` expression-bodied property (per construct 8).
- T12: Emit `public int Count => _modes.Count;` with XML-doc preserving "unique predicates, not counting alternatives" semantic (per construct 9).
- T13: Emit `public bool IsEmpty => _modes.Count == 0;` expression-bodied property (per construct 9).
- T14: Emit `public static ModeTable FromDeclarations(IReadOnlyList<ModeDeclaration> declarations)` static factory with foreach + AddDeclaration loop (per construct 10).
- T15: Carry forward `///` Dart doc-comments to C# `///` XML-doc on each public surface (enum, class, methods, properties, static factory) verbatim — load-bearing on `Count` (per construct 9) and `AddDeclaration` union semantics (per construct 3).
- T16: Place `enum Mode` and `class ModeTable` in namespace `<root>.Compiler.Pmt` mirroring the Dart directory; rely on the cross-file invariant that the C# `ModeDeclaration` type (from `ast.cs`) exposes PascalCase `Signature` / `Args` / `TypeName` properties and `ModedArg.IsReader` (fixed by the ast.dart conversion spec) (per constructs 1 + 2 + 3 + 10).

## 4. Research Findings

none required — every construct in this file resolves against either a cached rf-* finding from the prior 018 convspec corpus (rf-dart-enum-plain-to-csharp-enum, rf-csharp-string-equality-ordinal-by-default, rf-dart-map-putifabsent-to-csharp-trygetvalue-or-add, rf-csharp-interpolated-string-equivalent-to-dart-interpolation) or one of the FIVE fresh rf-* findings already recorded in the convspec with authoritative Microsoft Learn + dart.dev WebFetches: rf-csharp-dictionary-trygetvalue-then-fallback-null, rf-dart-map-containskey-to-csharp-dictionary-containskey, rf-csharp-dictionary-keys-values-live-views, rf-dart-length-isempty-to-csharp-count, rf-csharp-static-factory-method-from-iterable. No new research needed at planning time.

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/lib/compiler/pmt/mode_table.dart.md` (the RATIFIED convspec). All ten construct decisions in §2 are byte-faithful summaries of the convspec's `constructs[*].target_decision` fields; task units in §3 are mechanical decompositions of the convspec's `conversion_units` list with construct cross-references; §4 mirrors the convspec's `escalations: []` and its provenance section (zero escalations, all findings cached or freshly recorded with authoritative both-side citations). The cross-file invariant on `ModeDeclaration`'s C# property surface (PascalCase `Signature`/`Args`/`TypeName`, `ModedArg.IsReader`) is carried forward from the convspec's Notes section, fixed by the ast.dart conversion spec.

## 6. Escalations

None.
