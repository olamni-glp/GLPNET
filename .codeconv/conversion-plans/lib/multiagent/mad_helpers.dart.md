---
path: lib/multiagent/mad_helpers.dart
cycle_group_id: 25
scc_siblings: []
generated_at: 2026-05-21T15:05:00Z
source_sha256: 04dbbb1bfb3128349506b658d9f81f4c51a4da86e877e7755344ecd020500645
schema_version: 1
---

# Conversion Plan: lib/multiagent/mad_helpers.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/lib/multiagent/mad_helpers.dart` (392 lines, sha256 `04dbbb1bfb3128349506b658d9f81f4c51a4da86e877e7755344ecd020500645`) confirms the following constituents:

- **Library directive (line 8)**: bare `library;` — Dart 2.19+ anchor for the file-level `///` doc-comment block (lines 1–7). No `part` / `part of`.
- **Imports (lines 10–11)**: `package:glp_runtime/runtime/terms.dart` (brings `Term`, `VarRef`, `StructTerm`, `ConstTerm`) and relative `global_writers_table.dart` (brings `GlobalWritersTable`).
- **Plain enum `GlobalNameType` (lines 14–20)**: two camelCase members `writer`, `reader`, each `///`-documented with the wire shape `_w(p, i)` / `_r(p, i)`. No constructor / no methods.
- **Class `GlobalName` (lines 22–54)**: three `final` fields (`type`, `agent`, `index`). One positional ctor (line 30) + TWO named-init-list factory ctors (`.writer` line 33, `.reader` line 36). Two boolean getters (`isWriter`, `isReader`). `@override toString()` (lines 41–43) renders `_w($agent, $index)` / `_r($agent, $index)`. **LOAD-BEARING**: explicit `@override operator ==` (lines 46–50) by `(type, agent, index)` AND `@override hashCode => Object.hash(type, agent, index)` (line 53).
- **Class `GlobalSendSpawn` (lines 61–80)**: three `final` fields (`readerAddr`, `globalName`, `destAgent`). Named-required ctor (lines 71–75). `toString` override (lines 78–79). NO equality override (identity equality intended).
- **Class `TermVar` (lines 86–115)**: four `final` fields (`addr`, `isReader`, `writerAddr`, `readerAddr`). TWO named-init-list ctors (`.writer` lines 97–99, `.reader` lines 102–104) that derive `writerAddr = addr` / `readerAddr = addr` respectively. Two getters (`isWriter` line 106, `pairedReaderAddr` line 109 — a documented one-line forwarder). `toString` ternary on `isReader` (lines 112–114). NO equality override.
- **Class `GlobalizeResult` (lines 120–131)**: two `final List<T>` fields (`globalNames`, `spawns`). Named-required ctor. No `toString` / `==`.
- **Class `FreshPair` (lines 134–142)**: two `final int` fields. POSITIONAL ctor (line 138). `toString` override (line 141). No equality override.
- **Class `LocalizeResult` (lines 147–163)**: three `final List<T>` fields (`freshPairs`, `useReader`, `spawns`). Named-required ctor. No `toString` / `==`. The `useReader` list is **parallel-indexed** to `freshPairs` by construction.
- **Top-level function `globalize` (lines 178–218)**: named-required params, returns new `GlobalizeResult`, side-effects `GlobalWritersTable` via `addGlobalizeEntry` / `allocateIndex`. Branches on `v.isWriter`. **LOAD-BEARING COMMENT** at the reader branch (lines 205–208): GlobalSendSpawn.readerAddr is used as key for heap.onBind() indexed by **writer** address; passes `v.writerAddr`.
- **Top-level function `localize` (lines 236–285)**: named-required params including the function-typed `(int, int) Function() freshAddrAllocator`. Body destructures the returned record `final (writerAddr, readerAddr) = freshAddrAllocator();` (line 248). Branches on `gn.isWriter`. **LOAD-BEARING COMMENT** at the writer branch (lines 260–263): same inverted-naming intent.
- **Top-level function `globalizeTermWithResult` (lines 296–306)**: builds `Map<int, GlobalName>` via parallel zip-by-index of `variables[i].addr → result.globalNames[i]`, delegates to `_substituteGlobalNames`.
- **Private function `_substituteGlobalNames` (lines 308–321)**: `if (term is VarRef) { mapping[term.addr] → StructTerm('_w'|'_r', [ConstTerm(agent), ConstTerm(index)]) else term } else if (term is StructTerm) { rebuild args recursively } return term`.
- **Top-level function `extractGlobalNames` (lines 327–331)**: thin wrapper allocating `List<GlobalName>` and calling `_extractGlobalNamesRecursive`.
- **Private function `_extractGlobalNamesRecursive` (lines 333–351)**: void out-parameter style. On `StructTerm` with functor `_w`/`_r` and arity 2 + both args `ConstTerm`, extracts `agent as String` and `(index as num).toInt()`, appends `GlobalName.writer`/`.reader`. Else recurses into args.
- **Top-level function `localizeTermWithResult` (lines 357–371)**: builds `Map<String, int>` keyed by composite `'${gn.type.name}:${gn.agent}:${gn.index}'`, selects pair.readerAddr or pair.writerAddr per `useReader[i]`, delegates to `_substituteLocalVars`.
- **Private function `_substituteLocalVars` (lines 373–391)**: pattern-match on `StructTerm` with `_w`/`_r` functor and 2 const args → composite key lookup (literal `"writer"`/`"reader"` per functor), maps to `VarRef(localAddr)` on hit. Else recurses into args. `ConstTerm` and `VarRef` (non-matching) return as-is.

ABSENT in this file: `Future`/`async`/`await`/`Completer`, `Stream`/`StreamController`, `Isolate` (this file is the support-layer for isolate-crossing data marshaling but does NOT spawn isolates), `mixin`/`extension`/`sealed`/`abstract`, `?`-typed declarations (no nullable fields), force-unwrap `!`. Every function is synchronous.

## 2. Dart → C#/.NET Conversion Plan

The plan mirrors the convspec's 17 constructs verbatim.

- **`library;` directive** → drop entirely; render the file-level `///` doc block as `//` file-header comments above the `namespace lib.multiagent` declaration. (rf-dart-library-directive-to-csharp-namespace-no-counterpart)
- **`import 'package:glp_runtime/runtime/terms.dart';`** → `using GlpRuntime.Runtime;`. **`import 'global_writers_table.dart';`** → DROP (same target namespace `lib.multiagent`). (rf-dart-import-to-csharp-using)
- **`enum GlobalNameType { writer, reader }`** → C# `enum GlobalNameType { Writer, Reader }` (PascalCase per .NET); default underlying `int`; preserves `///` XML-doc comments. (rf-dart-enum-plain-to-csharp-enum, REUSE)
- **`class GlobalName`** → `sealed class GlobalName` (reference type, **explicit value equality** — NOT a record): get-only properties `Type`, `Agent`, `Index`; **private** ctor `(GlobalNameType, string, int)`; static factories `Writer(string, int)` / `Reader(string, int)`; expression-bodied `IsWriter` / `IsReader`; `ToString()` ternary on `Type` rendering `_w({Agent}, {Index})` / `_r({Agent}, {Index})`; **`Equals(object?)` override by `(Type, Agent, Index)` + `GetHashCode() => HashCode.Combine(Type, Agent, Index)`**. (rf-dart-class-with-equals-and-hashcode-to-csharp-equals-gethashcode, rf-dart-named-factory-ctors-with-initialiser-list-to-csharp-static-factories)
- **`class GlobalSendSpawn`** → reference `class` (identity equality — NOT a record): get-only props `ReaderAddr`, `GlobalName`, `DestAgent`; non-optional-parameter ctor; `ToString()` override interpolating the same form. NO equality override. (rf-dart-final-named-required-ctor-to-csharp-getonly-properties, REUSE)
- **`class TermVar`** → reference `class` (identity equality): get-only props `Addr`, `IsReader`, `WriterAddr`, `ReaderAddr`; **private** four-arg ctor; static factories `Writer(int addr, int readerAddr)` (passes `writerAddr: addr`) / `Reader(int addr, int writerAddr)` (passes `readerAddr: addr`); expression-bodied `IsWriter` / `PairedReaderAddr`; `ToString()` ternary on `IsReader`. NO equality override. (rf-dart-named-factory-ctors-with-initialiser-list-to-csharp-static-factories)
- **`class GlobalizeResult`** → reference `class` (identity equality): `IReadOnlyList<GlobalName> GlobalNames`, `IReadOnlyList<GlobalSendSpawn> Spawns`; non-optional-parameter ctor. No `ToString` / `Equals` overrides. (rf-dart-final-list-fields-to-csharp-ireadonlylist-properties)
- **`class FreshPair`** → reference `class` (identity equality): `int WriterAddr`, `int ReaderAddr`; **positional** (NOT named-required) ctor `FreshPair(int, int)` — distinguishing shape preserved; `ToString()` `$"FreshPair(writer={WriterAddr}, reader={ReaderAddr})"`. (rf-dart-final-positional-ctor-to-csharp-positional-ctor)
- **`class LocalizeResult`** → reference `class` (identity equality): three `IReadOnlyList<T>` props (`FreshPairs`, `UseReader`, `Spawns`); non-optional-parameter ctor; parallel-array invariant between `FreshPairs[i]` and `UseReader[i]` preserved as a documented (un-typeable) contract. (rf-dart-final-list-fields-to-csharp-ireadonlylist-properties)
- **`public static class MadHelpers`** → hosts all five top-level free functions as `public static` methods + three `private static` recursive helpers. (rf-dart-toplevel-function-with-named-required-to-csharp-static-method)
- **`globalize(...)`** → `public static GlobalizeResult Globalize(IReadOnlyList<TermVar> variables, string localAgent, string remoteAgent, GlobalWritersTable table)` — synchronous; Dart `required` named params become C# positional (call sites may opt into named-argument syntax); body materialises `List<GlobalName>` + `List<GlobalSendSpawn>` locals; calls `GlobalName.Writer(localAgent, index)` / `.Reader(...)` factories; constructs `new GlobalSendSpawn(readerAddr: v.WriterAddr, globalName: globalName, destAgent: remoteAgent)`; **PRESERVES the inverted-naming `///` remark verbatim** at the spawn-construction site. (rf-dart-toplevel-function-with-named-required-to-csharp-static-method)
- **`localize(...)`** → `public static LocalizeResult Localize(IReadOnlyList<GlobalName> globalNames, string localAgent, GlobalWritersTable table, Func<(int writerAddr, int readerAddr)> freshAddrAllocator)`. Dart record `(int, int)` → C# ValueTuple `(int writerAddr, int readerAddr)`; deconstruction `var (writerAddr, readerAddr) = freshAddrAllocator();`. Function-typed param → `Func<(int, int)>`. **PRESERVES the inverted-naming comment** at the writer branch. (rf-dart-record-and-function-typed-param-to-csharp-valuetuple-and-func)
- **`is`-pattern dispatch on `Term`** → `if (term is VarRef varRef)` / `else if (term is StructTerm structTerm)` — pattern-binding replaces Dart's automatic promotion. Fall-through `return term;` preserves the open-hierarchy `ConstTerm`-unchanged path. NO `sealed` on `Term` (preserves Dart source's open hierarchy). (rf-dart-is-with-promotion-to-csharp-is-pattern)
- **Dictionary lookup** → `Dictionary<int, GlobalName>.TryGetValue(key, out var gn)` — **NOT the indexer** (which throws `KeyNotFoundException` on miss). Matches Dart `Map[k]` returning `V?`. (rf-dart-map-lookup-to-csharp-trygetvalue)
- **Private recursive walkers `_SubstituteGlobalNames` / `_ExtractGlobalNamesRecursive` / `_SubstituteLocalVars`** → `private static` methods on `MadHelpers`; out-parameter (passed-list mutation) style **preserved**; NOT rewritten to `IEnumerable<T> yield return` (would change eager-vs-lazy semantics + allocation profile). (rf-dart-private-recursive-walker-to-csharp-private-static-method)
- **`(indexArg.value as num).toInt()`** → `Convert.ToInt32(indexArg.Value)` — the documented .NET permissive-numeric-to-int counterpart (handles `int` / `double` / other IConvertible). `(agentArg.value as String)` → `(string)agentArg.Value`. (rf-dart-num-toint-to-csharp-convert-toint32)
- **Composite key `'${gn.type.name}:${gn.agent}:${gn.index}'`** → C# `$"{gn.Type.ToString().ToLowerInvariant()}:{gn.Agent}:{gn.Index}"`. The reader side uses LITERAL `"writer"` / `"reader"` per functor (`term.Functor == "_w" ? "writer" : "reader"`). **CROSS-METHOD KEY-CASING CONTRACT** — both sides agree on lower-case. (rf-dart-enum-name-to-csharp-enum-tostring-casing)
- **List literal `[ConstTerm(x), ConstTerm(y)]`** → `new List<Term> { new ConstTerm(...), new ConstTerm(...) }` with **explicit `Term` element type** (NOT `List<ConstTerm>` — C# generics are INVARIANT, the `StructTerm` ctor expects `List<Term>`, a `List<ConstTerm>` literal would fail at the call site). `term.args.map(...).toList()` → `term.Args.Select(...).ToList()`. (rf-dart-list-literal-to-csharp-list-initialiser)
- **String equality `term.functor == '_w'`** → preserved verbatim; C# `==` on `string` is ordinal value equality. **PRESERVES the wire-level constants `"_w"` / `"_r"`** byte-identically (madGLP-spec wire format). (rf-dart-string-equality-to-csharp-string-equality)

## 3. Decomposed Task Units

- T1: Emit `namespace lib.multiagent { ... }` and file-header `//` doc comments; drop `library;`. — done
- T2: Emit `using GlpRuntime.Runtime;`; drop the relative import. — done
- T3: Emit `enum GlobalNameType { Writer, Reader }` with `///` XML-doc comments. — done
- T4: Emit `sealed class GlobalName` with private ctor + static `Writer`/`Reader` factories + value-equality `Equals`/`GetHashCode` overrides + ternary `ToString`. — done
- T5: Emit `class GlobalSendSpawn` with three get-only props + non-optional-parameter ctor + `ToString` override (NO equality override). — done
- T6: Emit `class TermVar` with private four-arg ctor + static `Writer`/`Reader` factories + `IsWriter`/`PairedReaderAddr` getters + `ToString` ternary. — done
- T7: Emit `class GlobalizeResult` with `IReadOnlyList<T>` properties + non-optional-parameter ctor. — done
- T8: Emit `class FreshPair` with positional ctor + `ToString` override. — done
- T9: Emit `class LocalizeResult` with three `IReadOnlyList<T>` props + non-optional-parameter ctor. — done
- T10: Emit `public static class MadHelpers` host shell. — done
- T11: Emit `MadHelpers.Globalize(...)` with the inverted-naming comment block preserved verbatim. — done
- T12: Emit `MadHelpers.Localize(...)` with `Func<(int, int)>` callback param, ValueTuple deconstruction, and the inverted-naming comment at the writer branch. — done
- T13: Emit `MadHelpers.GlobalizeTermWithResult(...)` building `Dictionary<int, GlobalName>` and delegating to `_SubstituteGlobalNames`. — done
- T14: Emit `MadHelpers._SubstituteGlobalNames(...)` with `is`-pattern dispatch, `TryGetValue` lookup, and `new List<Term> { ... }` element-type-explicit rebuild. — done
- T15: Emit `MadHelpers.ExtractGlobalNames(...)` thin wrapper + `_ExtractGlobalNamesRecursive(...)` void out-parameter recursion using `Convert.ToInt32` for numeric coercion. — done
- T16: Emit `MadHelpers.LocalizeTermWithResult(...)` building `Dictionary<string, int>` with lower-cased enum-name key and delegating to `_SubstituteLocalVars`. — done
- T17: Emit `MadHelpers._SubstituteLocalVars(...)` with `is`-pattern dispatch, literal `"writer"`/`"reader"` key construction, `TryGetValue` lookup, and `VarRef` substitution on hit. — done

## 4. Research Findings

None required. Every non-trivial construct is resolved from authoritative Dart docs (dart.dev / api.dart.dev) and/or Microsoft Learn .NET docs as recorded inline in the ratified convspec's research-provenance section (rf-dart-library-directive-to-csharp-namespace-no-counterpart, rf-dart-import-to-csharp-using, rf-dart-enum-plain-to-csharp-enum, rf-dart-class-with-equals-and-hashcode-to-csharp-equals-gethashcode, rf-dart-final-named-required-ctor-to-csharp-getonly-properties, rf-dart-named-factory-ctors-with-initialiser-list-to-csharp-static-factories, rf-dart-final-list-fields-to-csharp-ireadonlylist-properties, rf-dart-final-positional-ctor-to-csharp-positional-ctor, rf-dart-toplevel-function-with-named-required-to-csharp-static-method, rf-dart-record-and-function-typed-param-to-csharp-valuetuple-and-func, rf-dart-is-with-promotion-to-csharp-is-pattern, rf-dart-map-lookup-to-csharp-trygetvalue, rf-dart-private-recursive-walker-to-csharp-private-static-method, rf-dart-num-toint-to-csharp-convert-toint32, rf-dart-enum-name-to-csharp-enum-tostring-casing, rf-dart-list-literal-to-csharp-list-initialiser, rf-dart-string-equality-to-csharp-string-equality). The Isolate-equivalence file-wide decision is inherited from `lib/multiagent/global_writers_table.dart`'s convspec (FR-024 cache reuse); this file's contribution is preserving per-agent single-threaded ownership of `GlobalWritersTable` mutations by NOT introducing async signatures on the surface.

## 5. Consistency Pass

Cross-checked the plan against:

- **Source SHA**: `04dbbb1bfb3128349506b658d9f81f4c51a4da86e877e7755344ecd020500645` matches both the convspec front-matter (`source_sha256`) and the tombstone (`sha256`). No drift since the convspec was ratified — fixed — derived from convspec line 10 + tombstone line 45.
- **Convspec construct coverage**: all 17 constructs in the convspec `constructs:` list have a corresponding §2 line and at least one T-task in §3 — fixed — derived from convspec lines 12–120.
- **Convspec `conversion_units:` block (54 entries)**: every emitted unit (namespace, file-header comments, enum, six classes with their internal members, static MadHelpers shell, five public methods, three private methods) is covered by the §3 task units — fixed — derived from convspec lines 121–174.
- **Zero `escalations:`** in convspec — matches §6 below (None) — fixed — derived from convspec line 175.
- **Cycle group**: prompt specifies `cycle_group_id: 25` with empty `scc_siblings: []`. Tombstone front-matter has `cycle_group_id: 26`. The prompt is the authoritative input for this artefact's front-matter per the task contract; the tombstone value is a separate workspace-state question handled by `/codeconv-planagents` stamp-back, not by this plan. No escalation required — divergence is upstream tombstone bookkeeping, not a plan-content gap. Fixed — derived from prompt header + tombstone line 47.
- **FR-024 cross-file idiom reuse**: `rf-dart-enum-plain-to-csharp-enum` reused from `message_queue.dart`, `rf-dart-final-named-required-ctor-to-csharp-getonly-properties` reused from `global_writers_table.dart`, Isolate-equivalence concurrency decision inherited from `global_writers_table.dart`. All explicit in convspec rationale block — fixed — derived from convspec lines 218–221, 240–242, 460–476.
- **Load-bearing decisions preserved**: (a) `GlobalName` value-equality override (NOT record) recorded as load-bearing nuance in convspec construct §4, surfaced in T4 + the `Equals`/`GetHashCode` plan line; (b) inverted-naming comments inside `Globalize` / `Localize` flagged as byte-preserve in T11 + T12; (c) cross-method key-casing lower-case contract recorded in T16 + T17; (d) `List<Term>` element-type-explicit rebuild flagged as covariance-vs-invariance trap in T14. All four load-bearing items are explicit in the plan — fixed — derived from convspec construct nuances + rationale.
- **Sync-only constraint**: convspec is explicit that no `Task<T>` / `async` / `Stream` / `IAsyncEnumerable` / `Channel<T>` may be introduced (lines 444–476). The plan preserves synchronous signatures throughout — fixed — derived from convspec FR-009 nuances block.

No outstanding gaps.

## 6. Escalations

None.
