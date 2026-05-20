# Conversion Spec — lib/multiagent/mad_helpers.dart

> Conversion-spec artifact for lib/multiagent/mad_helpers.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/multiagent/mad_helpers.dart
source_sha256: 04dbbb1bfb3128349506b658d9f81f4c51a4da86e877e7755344ecd020500645
target_code_unit: lib/multiagent/mad_helpers.cs
constructs:
  - construct_key: "dart.library_directive.bare_library_no_name"
    source_form: "`library;` — Dart 2.19+ bare library directive (no library name) declaring that this file is the entry point of an implicit single-file library. Required by Dart to permit the file-level doc-comment block above to attach to the library rather than the first declaration."
    target_decision: "No direct .NET counterpart. C# files do not carry per-file library directives — assemblies are the unit of code organisation and the file's `namespace` declaration carries the logical scope. The conversion drops the `library;` line entirely (no replacement emitted) and renders the file-level `///` doc comments above it as a `<remarks>` block on the namespace-level container/static class, OR (preferred for this spec) as plain `//` file-header comments above the namespace declaration. The Dart file's logical role — a free-function module with several supporting reference-type holders — maps to a single namespace `lib.multiagent` containing the holder classes plus a `static class MadHelpers` that hosts the four top-level functions (`Globalize`, `Localize`, `GlobalizeTermWithResult`, `ExtractGlobalNames`, `LocalizeTermWithResult`) and the two private helpers (`_SubstituteGlobalNames`, `_ExtractGlobalNamesRecursive`, `_SubstituteLocalVars`)."
    idiom_id: null
    research_finding_id: rf-dart-library-directive-to-csharp-namespace-no-counterpart
    nuance: "Library-vs-assembly nuance (explicitly addressed): Dart's `library` directive declares the unit of name privacy (leading-underscore privacy is library-scoped, NOT file-scoped). C# privacy is type-scoped (`private`) or assembly-scoped (`internal`), NOT file-scoped. The Dart `_substituteGlobalNames`, `_extractGlobalNamesRecursive`, and `_substituteLocalVars` private top-level functions are visible to any other Dart file in the SAME library; in this codebase each `.dart` file is its own implicit library (the bare `library;` directive plus no `part` files), so the underscore-prefix is effectively file-private. The faithful C# mapping is `private static` methods on the same `static class MadHelpers` host — class-private, strictly tighter than Dart library-private, but here equivalent because no other file consumes these helpers. No async/Future/Stream/Isolate/mixin/sealed/extension implications; the library directive itself is a Dart-only structural marker. Value-vs-reference: not applicable to a directive."
  - construct_key: "dart.import.package_relative_blend"
    source_form: "`import 'package:glp_runtime/runtime/terms.dart';` (package import — resolves via pubspec) and `import 'global_writers_table.dart';` (relative import — same directory)."
    target_decision: "Map both Dart imports to C# `using` directives referencing the matching target namespaces. `package:glp_runtime/runtime/terms.dart` → `using GlpRuntime.Runtime;` (the .NET port's namespace for the terms file, established by `lib/runtime/terms.dart`'s convspec — the file declares `abstract class Term`, `class ConstTerm`, `class StructTerm`, `class VarRef`). The relative import `global_writers_table.dart` resolves to the same multiagent namespace as this file (`lib.multiagent`), so NO `using` is required IF the C# file declares the same namespace — the namespace declaration replaces the relative-import line. Codegen MUST NOT emit a placeholder `using` if the target namespace is already the current one."
    idiom_id: null
    research_finding_id: rf-dart-import-to-csharp-using
    nuance: "Import-resolution nuance: Dart's package imports go through `pubspec.yaml` package map; relative imports are filesystem-relative. C# does not have either — `using` only opens a namespace declared somewhere in the referenced assemblies/projects. The conversion REQUIRES the target project to have a project reference / assembly reference to whatever assembly hosts `GlpRuntime.Runtime` (Term hierarchy); that wiring is a build-system concern outside the per-file convspec. Package-private vs assembly-internal: Dart `package:` URIs do not imply visibility, only resolution; .NET assembly references imply load-time linkage. No semantic divergence here, just structural plumbing."
  - construct_key: "dart.enum.plain_two_cases_writer_reader_with_doc_comments"
    source_form: "`enum GlobalNameType { writer, reader }` — a plain (non-enhanced) Dart enum with two camelCase members, each documented with a `///` doc-comment describing the global-name shape (`_w(p, i)` writer, `_r(p, i)` reader). No constructor, no instance members, no methods. Used as a discriminator tag on the `GlobalName` class."
    target_decision: "C# `enum GlobalNameType { Writer, Reader }` with two PascalCase members per .NET naming convention. Default underlying type `int`; no explicit numeric values needed (no caller observes ordinal). The Dart `///` doc-comments map to C# `///` XML-doc comments verbatim. Lives in the same `lib.multiagent` target namespace as `GlobalName`."
    idiom_id: null
    research_finding_id: rf-dart-enum-plain-to-csharp-enum
    nuance: "Same plain-enum nuance as `MessageType` in `message_queue.dart`'s convspec — Dart plain enum → C# enum is direct. Naming nuance: Dart camelCase members (`writer`, `reader`) → PascalCase (`Writer`, `Reader`). Value-vs-reference: enums are value types in both languages, identical semantics. The discriminator's identity must be PRESERVED across the conversion: `GlobalNameType.writer ↔ GlobalNameType.Writer`; downstream consumers (notably `_w` / `_r` functor selection in `_SubstituteGlobalNames`) read this tag. Null-safety/Stream/Future/Isolate: ABSENT — correctly not asserted."
  - construct_key: "dart.class.tagged_union_via_enum_three_final_fields_two_named_factory_ctors_value_equality_override"
    source_form: "`class GlobalName` with three `final` fields (`GlobalNameType type`, `String agent`, `int index`), a primary positional ctor `GlobalName(this.type, this.agent, this.index)`, two NAMED factory-style ctors (`GlobalName.writer(this.agent, this.index) : type = GlobalNameType.writer;` and `GlobalName.reader(this.agent, this.index) : type = GlobalNameType.reader;`), two boolean getters (`isWriter`, `isReader`), an `@override String toString()` that branches on type to render `_w(agent, index)` or `_r(agent, index)`, AND BOTH `@override bool operator ==(Object other) => other is GlobalName && type == other.type && agent == other.agent && index == other.index;` AND `@override int get hashCode => Object.hash(type, agent, index);` — i.e. VALUE EQUALITY by all three fields."
    target_decision: "A C# `sealed class GlobalName` (reference type) with three get-only auto-properties (`Type`, `Agent`, `Index`), a primary constructor taking three non-optional parameters, AND two `public static GlobalName Writer(string agent, int index) => new GlobalName(GlobalNameType.Writer, agent, index);` / `public static GlobalName Reader(string agent, int index) => new GlobalName(GlobalNameType.Reader, agent, index);` factory methods replacing the Dart named constructors (C# has no per-name constructor variants; static factory methods on the type are the documented .NET counterpart). The two boolean getters `IsWriter`/`IsReader` are expression-bodied get-only properties. `ToString()` override uses a ternary or `switch` expression on `Type` to render `_w({Agent}, {Index})` / `_r({Agent}, {Index})` byte-identically. EQUALITY: override `public override bool Equals(object? other) => other is GlobalName g && Type == g.Type && Agent == g.Agent && Index == g.Index;` AND `public override int GetHashCode() => HashCode.Combine(Type, Agent, Index);` — this is the LOAD-BEARING decision: the Dart source EXPLICITLY overrides `==` and `hashCode` for value equality, so the C# counterpart MUST also use value equality. NOT a `record` (despite the value-equality match) — rejected because (a) `GlobalName` is INSTANTIATED via per-tag named constructors mirroring two case-shapes that don't map cleanly to record positional/parameterised syntax without losing the readable `.Writer(agent, index)` call site, and (b) the explicit equality override in the Dart source documents intent that the conversion preserves byte-identically rather than delegating to a record's synthesised equality. NOT a `record struct` — reference identity is incidental but the type is held by reference inside `GlobalizeResult.globalNames`, `LocalizeResult` mapping, and the `varToGlobalName` / `globalNameToLocal` dictionaries; copying value semantics would change dictionary key behaviour."
    idiom_id: null
    research_finding_id: rf-dart-class-with-equals-and-hashcode-to-csharp-equals-gethashcode
    nuance: "Equality nuance (explicitly addressed and LOAD-BEARING): the Dart source overrides BOTH `==` and `hashCode` for structural equality across (type, agent, index). The conversion MUST preserve this — `GlobalName` instances are used as dictionary keys (via the `${gn.type.name}:${gn.agent}:${gn.index}` string key in `_substituteLocalVars`, which is itself a stringification of the equality fields). If the C# port silently used reference equality (no `Equals` override), the `globalNameToLocal` map would key-collision in a way that disagrees with Dart. The C# override REPLICATES Dart's value-equality contract exactly. Hash-code nuance: Dart `Object.hash(type, agent, index)` and .NET `HashCode.Combine(Type, Agent, Index)` are the official combine helpers in each language; both produce a stable hash over the same field tuple. Named-constructor nuance: Dart `Class.namedCtor(...)` has NO direct C# equivalent (C# constructors are name-less; only parameter signatures distinguish them). The canonical .NET counterpart is static factory methods on the same class (Microsoft Learn 'Choosing Between Class and Struct' / 'Static methods'). Value-vs-reference: stays a reference `class` because instances are stored by reference in `List<GlobalName>` and as dictionary keys; copying via `record struct` would change identity semantics for the keying use site. Null-safety: all three fields non-nullable (Dart `GlobalNameType`/`String`/`int` with no `?`) → non-nullable C# under NRT. Sealed-vs-open: marking `sealed` documents that no subclass exists in the Dart source (no `extends`/`implements` in any consumer) — recorded explicitly per FR-009."
  - construct_key: "dart.class.three_final_fields_named_required_ctor_tostring_no_equality_override"
    source_form: "`class GlobalSendSpawn` with three `final` fields (`int readerAddr`, `GlobalName globalName`, `String destAgent`), a single named-required constructor (`GlobalSendSpawn({required this.readerAddr, required this.globalName, required this.destAgent})`), and an `@override String toString()` that renders `GlobalSendSpawn(reader=$readerAddr, name=$globalName, dest=$destAgent)`. NO `==` / `hashCode` overrides — Dart default identity equality applies."
    target_decision: "C# reference `class GlobalSendSpawn` (NOT record, NOT struct) with three get-only auto-properties (`ReaderAddr`, `GlobalName`, `DestAgent`) and a single non-optional-parameter constructor. `ToString()` override renders the same interpolated form byte-identically. NO equality override emitted — Dart default identity equality is preserved by C# default identity equality (NOT a `record` whose synthesised value-equality would silently CHANGE semantics from the Dart side). The class is reference-shared across `GlobalizeResult.spawns` and `LocalizeResult.spawns`; identity matters for downstream consumers that may match a spawn back to its originating reader-address registration."
    idiom_id: null
    research_finding_id: rf-dart-final-named-required-ctor-to-csharp-getonly-properties
    nuance: "Reuse of idiom from `global_writers_table.dart` (rf-dart-final-named-required-ctor-to-csharp-getonly-properties — `GlobalizeEntry` / `LocalizeEntry` are the same shape: immutable holder with named-required ctor and `toString` override, NO equality override). Identity vs value nuance (explicitly addressed): the Dart source DELIBERATELY does NOT override `==` here, so identity equality is the intended contract. A `record` would inject value equality and change behaviour. Null-safety: all three fields non-nullable. Reference-vs-value: reference `class` because spawn objects are aliased across `GlobalizeResult` / `LocalizeResult` collections. The `GlobalName` property carries the value-equality nuance from the parent construct above; THIS class does not inherit that equality."
  - construct_key: "dart.class.named_factory_ctors_with_initialiser_lists_and_paired_addrs"
    source_form: "`class TermVar` with four `final` fields (`int addr`, `bool isReader`, `int writerAddr`, `int readerAddr`), TWO named factory-style constructors with INITIALISER LISTS that derive `isReader`/`writerAddr`/`readerAddr` from the call-site shape: `TermVar.writer(this.addr, {required this.readerAddr}) : isReader = false, writerAddr = addr;` and `TermVar.reader(this.addr, {required this.writerAddr}) : isReader = true, readerAddr = addr;`. A computed getter `bool get isWriter => !isReader;` and a documented-as-redundant getter `int get pairedReaderAddr => readerAddr;`. `@override String toString()` branches on `isReader` to render either `TermVar.reader($addr, writer=$writerAddr)` or `TermVar.writer($addr, reader=$readerAddr)`. NO `==` / `hashCode` overrides (identity equality)."
    target_decision: "C# reference `class TermVar` with four get-only auto-properties (`Addr`, `IsReader`, `WriterAddr`, `ReaderAddr`). The Dart named constructors with initialiser-list field derivations map to TWO `public static TermVar Writer(int addr, int readerAddr) => new TermVar(addr: addr, isReader: false, writerAddr: addr, readerAddr: readerAddr);` and `public static TermVar Reader(int addr, int writerAddr) => new TermVar(addr: addr, isReader: true, writerAddr: writerAddr, readerAddr: addr);` static factory methods, with a SINGLE private four-arg constructor populating all four backing fields. The initialiser-list derivation `writerAddr = addr` / `readerAddr = addr` (in the writer / reader factory respectively) carries over: the static factory computes the derived value and passes it to the unified ctor. Computed getters: `public bool IsWriter => !IsReader;` (expression-bodied). The `pairedReaderAddr` getter is preserved verbatim as `public int PairedReaderAddr => ReaderAddr;` even though it is a one-line forwarder — the Dart doc-comment ('Get the paired reader address') documents API intent the conversion preserves. `ToString()` ternary on `IsReader`. NO equality override — identity equality preserved (NOT a record)."
    idiom_id: null
    research_finding_id: rf-dart-named-factory-ctors-with-initialiser-list-to-csharp-static-factories
    nuance: "Initialiser-list nuance (explicitly addressed): Dart constructor initialiser lists run BEFORE the constructor body and can assign multiple final fields using values DERIVED from constructor parameters (here, `writerAddr = addr`). C# has no per-constructor initialiser-list syntax; the documented .NET counterpart is to centralise field initialisation in a single private constructor and have public static factory methods pass derived values. Named-constructor nuance: same as for `GlobalName` — Dart per-name constructors → C# static factory methods. The two Dart constructors share the same parameter count (one positional + one named), distinguished ONLY by name; in C# they would be ambiguous as overloads, so static factories named `Writer` / `Reader` are mandatory. Reference-vs-value: reference `class` (the address-pair is part of variable-bookkeeping state; identity matters as `TermVar` instances are stored in a `List<TermVar>` and consulted by index in `globalize`). Null-safety: all four fields non-nullable. The `addr` field is logically the SAME as one of `writerAddr` / `readerAddr` (depending on the factory chosen) — the duplication is intentional, reflecting Dart-source intent ('always carries both writer and reader addresses of the pair'), and the C# render preserves the redundancy."
  - construct_key: "dart.class.two_list_typed_fields_named_required_ctor_no_tostring"
    source_form: "`class GlobalizeResult` with two `final List` fields (`final List<GlobalName> globalNames; final List<GlobalSendSpawn> spawns;`), single named-required ctor (`GlobalizeResult({required this.globalNames, required this.spawns})`). NO `toString` override (Dart default `Instance of 'GlobalizeResult'` would apply). NO `==` / `hashCode` overrides."
    target_decision: "C# reference `class GlobalizeResult` with two get-only auto-properties typed as `IReadOnlyList<GlobalName> GlobalNames { get; }` and `IReadOnlyList<GlobalSendSpawn> Spawns { get; }` (read-only view; the lists are POPULATED by `Globalize` and CONSUMED by callers without further mutation in this file). Single non-optional-parameter constructor assigning both properties. NO `ToString()` override (matches Dart default). NO equality override (Dart default identity equality preserved). NOT a record (same reasoning as `GlobalSendSpawn` — no Dart value equality, no record value equality)."
    idiom_id: null
    research_finding_id: rf-dart-final-list-fields-to-csharp-ireadonlylist-properties
    nuance: "Read-only-view nuance (explicitly addressed): the Dart `final List<X>` fields hold REFERENCES that are write-once at construction; the LIST CONTENTS are not mutated after construction by any consumer in this file. The faithful C# property type is `IReadOnlyList<T>` (records the immutability-after-construction invariant a consumer sees). Alternative `List<T>` would expose `Add`/`Remove` to consumers, leaking mutation capability that the Dart `final` declaration does NOT block (Dart `final` only freezes the reference, not the contents — but the consumer convention is read-only, and the C# property type makes that explicit). Reference-vs-value: reference `class` aliasing — the Dart `globalize` function RETURNS `GlobalizeResult` and the caller (test code, mad runtime) holds it by reference. Async/Stream/Future: ABSENT. Null-safety: both fields non-nullable (the list itself; elements are non-nullable `GlobalName` / `GlobalSendSpawn`)."
  - construct_key: "dart.class.two_final_int_fields_positional_ctor_tostring_override"
    source_form: "`class FreshPair` with two `final int` fields (`writerAddr`, `readerAddr`), POSITIONAL constructor (`FreshPair(this.writerAddr, this.readerAddr)`), and `@override String toString() => 'FreshPair(writer=$writerAddr, reader=$readerAddr)';`. NO `==` / `hashCode` overrides."
    target_decision: "C# reference `class FreshPair` with two get-only auto-properties `WriterAddr` and `ReaderAddr`, positional (non-named) constructor `public FreshPair(int writerAddr, int readerAddr)`, expression-bodied `public override string ToString() => $\"FreshPair(writer={WriterAddr}, reader={ReaderAddr})\";`. NO equality override. NOT a record (identity equality is the intended Dart contract; `FreshPair` instances are aliased across `LocalizeResult.freshPairs` and the variable allocator's bookkeeping)."
    idiom_id: null
    research_finding_id: rf-dart-final-positional-ctor-to-csharp-positional-ctor
    nuance: "Positional-vs-named-ctor nuance: the Dart source uses positional `(this.writerAddr, this.readerAddr)` (NOT named-required). This is a different shape from `GlobalSendSpawn` / `OutboundMessage` / `SpawnDirective` and the conversion preserves the positional shape verbatim — the call site `FreshPair(writerAddr, readerAddr)` becomes `new FreshPair(writerAddr, readerAddr)` with the same positional order. Reference-vs-value: stays reference `class` (NOT struct / record struct) — same reasoning as siblings; identity matters for downstream consumers. Null-safety: both int fields non-nullable."
  - construct_key: "dart.class.three_typed_list_fields_named_required_ctor_no_tostring"
    source_form: "`class LocalizeResult` with three `final List` fields (`final List<FreshPair> freshPairs; final List<bool> useReader; final List<GlobalSendSpawn> spawns;`), single named-required ctor. NO `toString` / `==` / `hashCode` overrides. The `useReader` list is parallel-indexed to `freshPairs` (per documented intent: position i of `useReader` says whether position i of `freshPairs` should expose its reader-address)."
    target_decision: "C# reference `class LocalizeResult` with three get-only `IReadOnlyList<T>` properties (`FreshPairs`, `UseReader`, `Spawns`), single non-optional-parameter constructor. NO ToString / Equals / GetHashCode overrides. Parallel-indexed list invariant (positional correlation between `freshPairs[i]` and `useReader[i]`) is documented in the construct's nuance below but is NOT enforced at the type level — neither Dart nor C# can express that constraint structurally."
    idiom_id: null
    research_finding_id: rf-dart-final-list-fields-to-csharp-ireadonlylist-properties
    nuance: "Parallel-array nuance (explicitly addressed): `freshPairs` and `useReader` are positionally correlated by construction. This is a Dart-source convention that the C# port preserves byte-identically; consumers iterate both lists in lock-step. A more structured target (e.g. `IReadOnlyList<(FreshPair pair, bool useReader)>`) would be MORE idiomatic .NET but a SHAPE change that this spec deliberately does NOT make (FR-023 spec-only; codegen preserves the Dart record-of-arrays vs array-of-records shape). Read-only-view nuance: same as `GlobalizeResult` — `IReadOnlyList<T>` records the consumer-facing immutability. Null-safety: all three fields non-nullable; `List<bool>` elements are non-nullable value types."
  - construct_key: "dart.top_level_function.named_required_params_returning_result_holder_with_side_effects_on_table"
    source_form: "`GlobalizeResult globalize({required List<TermVar> variables, required String localAgent, required String remoteAgent, required GlobalWritersTable table})` — a TOP-LEVEL Dart function with named-required parameters, returning `GlobalizeResult` and SIDE-EFFECTING the `GlobalWritersTable` (calling `table.addGlobalizeEntry(...)` and `table.allocateIndex()`). The body iterates `variables`, branches on `v.isWriter`, appends to local `globalNames` / `spawns` lists, and returns the result holder."
    target_decision: "C# `public static GlobalizeResult Globalize(IReadOnlyList<TermVar> variables, string localAgent, string remoteAgent, GlobalWritersTable table)` on the `MadHelpers` static class. Dart named-required parameters map to C# positional parameters in declaration order — named-argument syntax at the call site (C# supports `Globalize(variables: x, localAgent: \"p\", ...)`) preserves call-site readability without requiring the Dart `required` flag. The method body materialises two `var globalNames = new List<GlobalName>();` and `var spawns = new List<GlobalSendSpawn>();` locals, iterates `variables`, calls the matching C# methods on `table` (`AddGlobalizeEntry(v.WriterAddr, remoteAgent)` / `AllocateIndex()`), constructs `GlobalName.Writer(localAgent, index)` / `GlobalName.Reader(localAgent, index)` via the static factory methods declared on `GlobalName`, appends a new `GlobalSendSpawn` with named-arg construction `new GlobalSendSpawn(readerAddr: v.WriterAddr, globalName: globalName, destAgent: remoteAgent)`, and returns `new GlobalizeResult(globalNames: globalNames, spawns: spawns)`. NOT async — no `Task` / `async` introduced. Return type `GlobalizeResult` is non-nullable. CRITICAL: the load-bearing comment in the Dart source — 'GlobalSendSpawn.readerAddr is used as the key for heap.onBind(), which is indexed by *writer* address' — is preserved VERBATIM as a `///` XML-doc remark on the method AND as an inline `// ` comment at the spawn-construction site. The semantic invariant (passing `writerAddr` to a field named `readerAddr`) is intentional and load-bearing for the heap.onBind callback wiring; codegen MUST preserve the comment text byte-identically because it documents the inverted naming."
    idiom_id: null
    research_finding_id: rf-dart-toplevel-function-with-named-required-to-csharp-static-method
    nuance: "Top-level-function nuance (explicitly addressed and LOAD-BEARING): Dart top-level functions are namespace-visible without a class. C# has no equivalent — every method must live on a type. The faithful counterpart is a `public static class MadHelpers { public static GlobalizeResult Globalize(...) ... }` host (Microsoft Learn's 'Static classes and static class members' is the documented basis for hosting free functions). Named-required-param nuance: Dart `required` named params are a compile-site obligation; C# call sites can use named arguments without any declaration-side keyword. The conversion drops `required` and preserves call-site clarity via named args at the consumer. Async/Future/Stream/Isolate/Completer: ABSENT — this method is synchronous and MUST stay synchronous (introducing `Task<GlobalizeResult>` would force every caller into `await`, leaking concurrency semantics the Dart code does not have). Side-effect nuance: the function mutates the `GlobalWritersTable` AND returns a new `GlobalizeResult` — the mutation order is observable (entries are appended before the return-value list is finalised, but the lists are populated in the SAME iteration). The C# port preserves the per-iteration order. Variable-shadowing nuance: the Dart `final` locals (`globalNames`, `spawns`, `index`, `globalName`) become C# `var` locals; Dart `final` is compile-enforced single-assignment, while C# `var` is type-inference only — the convention of not reassigning is preserved as a coding norm but NOT a compile-time guarantee (acceptable here — the method body is short and the no-reassignment intent is obvious). Documentation nuance: the inline doc comment block citing 'Spec Section 5.1' is preserved as a `///` XML-doc `<remarks>` on the C# method, byte-identical."
  - construct_key: "dart.top_level_function.callback_function_param_returning_record_tuple"
    source_form: "`LocalizeResult localize({required List<GlobalName> globalNames, required String localAgent, required GlobalWritersTable table, required (int, int) Function() freshAddrAllocator})` — like `globalize` but with an additional NAMED-REQUIRED FUNCTION PARAMETER `freshAddrAllocator` whose type is a zero-argument callable returning a Dart RECORD `(int, int)` (a positional 2-tuple of integers — Dart 3+ record syntax). The body destructures the record via pattern syntax `final (writerAddr, readerAddr) = freshAddrAllocator();`."
    target_decision: "C# `public static LocalizeResult Localize(IReadOnlyList<GlobalName> globalNames, string localAgent, GlobalWritersTable table, Func<(int writerAddr, int readerAddr)> freshAddrAllocator)` on `MadHelpers`. The Dart record `(int, int)` maps to a C# ValueTuple `(int writerAddr, int readerAddr)` — same positional shape, additionally NAMED at the type level for caller clarity (the Dart record is positional-only without field names at the type level; the C# port adds names for readability without changing the structural shape). The Dart pattern-destructure `final (writerAddr, readerAddr) = freshAddrAllocator();` maps to C# `var (writerAddr, readerAddr) = freshAddrAllocator();` — both languages support positional tuple deconstruction in local declarations. The function-typed parameter `() => (int, int)` maps to `Func<(int, int)>` — the documented .NET delegate type for a zero-argument callable returning a tuple. NOT `Action` (which returns void) and NOT a custom delegate type (`Func<T>` is the canonical .NET wrap for zero-argument value-returning callables). Body otherwise mirrors `Globalize` shape, branching on `gn.IsWriter` to populate `freshPairs` / `useReader` / `spawns` and (for readers) calling `table.AddLocalizeEntry(writerAddr, gn.Agent, gn.Index)`. The load-bearing inverted-naming comment ('GlobalSendSpawn.readerAddr is used as the key for heap.onBind() ... we pass writerAddr') is preserved verbatim at the writer branch."
    idiom_id: null
    research_finding_id: rf-dart-record-and-function-typed-param-to-csharp-valuetuple-and-func
    nuance: "Record-tuple nuance (explicitly addressed and load-bearing): Dart 3+ introduces records (`(int, int)`) as positional/named lightweight aggregates; C# has ValueTuple (`(int, int)`) since C# 7. Both are VALUE TYPES; semantics agree. Names are optional in both; we ADD names in the C# target type for readability (`(int writerAddr, int readerAddr)` — purely a documentation aid, identical structural type to anonymous `(int, int)`). Function-typed-parameter nuance: Dart `(int, int) Function()` is a typed function-value parameter; the documented C# counterpart is `Func<TReturn>` for value-returning callables and `Action` for void-returning callables (Microsoft Learn 'Func<TResult> Delegate'). Capture-and-closure semantics agree between Dart closures and C# lambdas at the call site — both capture by reference for mutable variables (Dart) / closure-over-locals (C#); for this read-only allocator callback no capture semantics matter. Async/Future/Stream/Isolate: ABSENT — `freshAddrAllocator` is synchronous; no `Func<Task<...>>` introduced. Side-effect nuance: same as `Globalize` — `table` is mutated and `LocalizeResult` is returned; ordering preserved."
  - construct_key: "dart.runtime_type_check.is_pattern_dispatch_on_sealed_term_hierarchy"
    source_form: "`if (term is VarRef) { ... } else if (term is StructTerm) { ... } return term; // ConstTerm unchanged` — Dart `is` runtime type-check with promotion (after `if (term is VarRef)` the variable `term` is statically a `VarRef` inside the branch, so `term.addr` resolves without a cast). The `terms.dart` file defines `Term` as an abstract base with three implementing classes (`ConstTerm`, `StructTerm`, `VarRef`) — an open hierarchy on the Dart side (no `sealed` keyword)."
    target_decision: "Map Dart `is` + promotion to C# pattern-matching: `if (term is VarRef varRef) { ... varRef.Addr ... }` and `else if (term is StructTerm structTerm) { ... structTerm.Functor ... }`. The C# `is` pattern binds a typed variable in the same statement, matching Dart's automatic promotion. The fall-through `return term;` preserves the 'ConstTerm unchanged' semantic — any unrecognised `Term` subclass is returned as-is. NO `switch` expression conversion (the Dart source uses if/else-if rather than `switch(term)`; preserving the shape aids reviewer fidelity). The `Term` hierarchy in C# MUST be reachable from this file — it lives in the `GlpRuntime.Runtime` namespace per the import mapping above."
    idiom_id: null
    research_finding_id: rf-dart-is-with-promotion-to-csharp-is-pattern
    nuance: "Type-test nuance (explicitly addressed): Dart `is T` is a runtime type-test that ALSO PROMOTES the variable's static type inside the success branch (only when the variable is local / final / unmodified between test and use). C# `is T name` is the documented pattern-matching counterpart (Microsoft Learn 'Patterns - Pattern matching using the is and switch expressions') — same runtime behaviour (vtable type check), AND introduces a bound name that is type-narrowed inside the branch. Sealed-vs-open nuance: Dart `Term` is NOT declared `sealed` (no `sealed` keyword in `terms.dart`); the C# `Term` counterpart should likewise NOT be `sealed` unless the runtime port adopts a closed hierarchy. The 'ConstTerm unchanged' fallback handles any future Term subclass that this file does not know about — preserving the open-hierarchy assumption. Exhaustiveness nuance: NEITHER Dart NOR the C# render enforces exhaustiveness here; both fall through to `return term;`. Adding `sealed` on the .NET side would enable compile-time exhaustiveness checking via `switch` expressions but is a separate, hierarchy-wide decision deferred to the `terms.dart` convspec. Null-safety: `term` is non-nullable (`Term` parameter without `?`); promotion preserves non-nullability."
  - construct_key: "dart.map.dictionary_int_to_globalname_with_per_index_lookup_and_struct_substitution"
    source_form: "`final varToGlobalName = <int, GlobalName>{}; for (var i = 0; i < variables.length; i++) { varToGlobalName[variables[i].addr] = result.globalNames[i]; } return _substituteGlobalNames(term, varToGlobalName);` — a build-once map from variable address to global name, populated by parallel iteration over `variables` and `result.globalNames` (length-matched by construction). Looked up inside `_substituteGlobalNames` via `final gn = mapping[term.addr];` (returns `GlobalName?` — null on miss). The substitution rebuilds the term tree: `VarRef` with a mapping hit becomes `StructTerm('_w' | '_r', [ConstTerm(agent), ConstTerm(index)])`; `StructTerm` recurses into args; `ConstTerm` returns unchanged."
    target_decision: "C# `var varToGlobalName = new Dictionary<int, GlobalName>(variables.Count);` (capacity-hint optional, micro-optimisation only). Population loop: `for (int i = 0; i < variables.Count; i++) varToGlobalName[variables[i].Addr] = result.GlobalNames[i];`. Lookup inside `_SubstituteGlobalNames`: use `TryGetValue` for nullable-clean access: `if (mapping.TryGetValue(varRef.Addr, out var gn)) { ... } return term;` — semantic equivalence with Dart's `Map[int]` returning `GlobalName?`. NOT the C# indexer `mapping[term.Addr]` (which throws `KeyNotFoundException` on miss for value-types — and even for reference types behaves differently from Dart). The functor selection `gn.IsWriter ? \"_w\" : \"_r\"` and rebuild `new StructTerm(functor, new List<Term> { new ConstTerm(gn.Agent), new ConstTerm(gn.Index) })` mirror Dart's `[ConstTerm(gn.agent), ConstTerm(gn.index)]` list-literal. The StructTerm recursive case maps `term.args.map((a) => _substituteGlobalNames(a, mapping)).toList()` to `term.Args.Select(a => _SubstituteGlobalNames(a, mapping)).ToList()` — LINQ Select-then-ToList is the eager equivalent of Dart `Iterable.map.toList`."
    idiom_id: null
    research_finding_id: rf-dart-map-lookup-to-csharp-trygetvalue
    nuance: "Map-lookup nuance (explicitly addressed and LOAD-BEARING): Dart `Map<K, V>[k]` returns `V?` (nullable on miss); C# `Dictionary<K, V>[k]` THROWS `KeyNotFoundException` on miss. A naive transliteration `mapping[term.Addr]` would FAIL at runtime where the Dart code returns null. The documented .NET counterpart is `TryGetValue(key, out value)` returning `bool` plus a nullable-out parameter (Microsoft Learn 'Dictionary<TKey,TValue>.TryGetValue Method'). Alternative `GetValueOrDefault` (C# 7+) returns `default(V)` on miss — for reference-type `V` this is `null`, which IS equivalent here, but `TryGetValue` is more idiomatic AND avoids a second hashed lookup. Specify `TryGetValue`. Recursive-rebuild nuance: Dart `Iterable.map(...).toList()` is eager-after-terminator; C# `Select(...).ToList()` is identical timing (deferred until `ToList` materialises). Identity-vs-value nuance for term rebuild: both Dart `Term` and the C# port use REFERENCE-CONSTRUCTED new instances per call — the substitution is a pure functional rewrite, not in-place mutation; conversion preserves that. Null-safety: `mapping[term.addr]` Dart returns `GlobalName?`; C# `TryGetValue` out is `GlobalName?` under NRT, matching."
  - construct_key: "dart.private_top_level_function.recursive_term_walker_with_args_iteration"
    source_form: "Three private top-level functions (`_substituteGlobalNames`, `_extractGlobalNamesRecursive`, `_substituteLocalVars`) — each starts with `if (term is StructTerm)` and recurses into `term.args` (and for `_substituteLocalVars`, also rebuilds the StructTerm with substituted args via `term.args.map(...).toList()`). All return early on `VarRef` / `ConstTerm` branches. `_extractGlobalNamesRecursive` is VOID-returning and APPENDS to a passed-in `List<GlobalName> result` (out-parameter style)."
    target_decision: "C# `private static` methods on the same `MadHelpers` static class. The void-returning out-parameter pattern of `_ExtractGlobalNamesRecursive(Term term, List<GlobalName> result)` is preserved as a `private static void _ExtractGlobalNamesRecursive(Term term, List<GlobalName> result)` with mutation-via-passed-list semantics — IDIOMATIC across both languages, and avoids any allocation-per-frame that an alternative `IEnumerable<GlobalName>` yield-return rewrite would change. NO conversion to `IEnumerable<GlobalName>` + `yield return` (which would change eager-vs-lazy semantics and allocate enumerator state). The recursion pattern is preserved verbatim. The leading-underscore Dart convention maps to `_camelCase` private methods on the static class (Microsoft Learn private-naming convention is PascalCase, but the Dart underscore tradition is preserved in the underscore-prefix to maintain visual parity with the source; codegen MAY rename to `PascalCase` if a project-wide convention is enforced — neutral choice deferred to project style)."
    idiom_id: null
    research_finding_id: rf-dart-private-recursive-walker-to-csharp-private-static-method
    nuance: "Eager-vs-lazy nuance (explicitly addressed): `_extractGlobalNamesRecursive` uses out-parameter mutation (eager, in-place); a LINQ `yield return` rewrite would change to lazy enumeration. The conversion DELIBERATELY preserves eager out-param mutation because (a) it matches the Dart shape byte-for-byte and (b) the caller (`extractGlobalNames`) immediately returns the list — laziness would offer no observable benefit and would introduce a different allocation profile. Recursion-depth nuance: the recursion is unbounded by static analysis (depends on term depth); both Dart and C# use the standard call-stack; neither language tail-call-optimises this pattern by default. Stack overflow on pathologically deep terms is a SHARED risk; the conversion does not introduce iterative-rewriting (out of scope for this spec). Naming nuance: underscore-prefix Dart privates map to `private` C# methods (visibility-tightening — Dart library-private to C# class-private); the leading-underscore convention is preserved at the identifier level for visual parity with the source per the project's spec-fidelity bias."
  - construct_key: "dart.runtime_cast.dynamic_typed_constant_value_extraction_with_as_string_as_num_toInt"
    source_form: "`final agent = agentArg.value as String; final index = (indexArg.value as num).toInt();` — extracts `value` field from `ConstTerm` (typed as `dynamic` per the `terms.dart` source), casts to `String` and `num` respectively, and calls `.toInt()` to coerce from Dart's `num` (the supertype of `int` and `double`) to `int`. The cast `as num` is permissive — accepts either `int` or `double` payload and narrows to `int` via truncation/conversion."
    target_decision: "C# pattern-match-or-cast: `string agent = (string)agentArg.Value;` AND `int index = Convert.ToInt32(indexArg.Value);` — the .NET `Convert.ToInt32(object)` handles both `int` and `double` (and other numeric types) source values, mirroring Dart's `num` permissiveness. Alternative `(int)(double)indexArg.Value` would assume a specific concrete type and FAIL the other; `Convert.ToInt32` is the documented permissive-numeric-to-int counterpart of Dart `num.toInt()`. For `agentArg.Value` the type is documented as `string` in the Dart source (via spec-side knowledge that global-name agents are strings); a `(string)` cast is appropriate, with a `null`-check or `as`-with-null implicit fallback handled by the surrounding `if (agentArg is ConstTerm && indexArg is ConstTerm)` guard. The Dart code does NOT null-check before the cast; the C# render preserves that — preconditions are guarded by the prior `is ConstTerm` test."
    idiom_id: null
    research_finding_id: rf-dart-num-toint-to-csharp-convert-toint32
    nuance: "Numeric-supertype nuance (explicitly addressed and LOAD-BEARING): Dart's `num` is a STATIC supertype of `int` and `double`; `(value as num).toInt()` works whether the underlying value is `int` (identity coercion) or `double` (truncation). C# has NO equivalent supertype — `int`, `long`, `double`, `decimal` are distinct value types with no common numeric base class. The documented permissive coercion in .NET is `System.Convert.ToInt32(object)` which dispatches on the runtime type and applies the appropriate conversion (Microsoft Learn 'Convert.ToInt32(Object) Method' — supports IConvertible implementations including all built-in numeric types). Alternative: pattern-match `indexArg.Value` with `switch { int i => i, double d => (int)d, _ => throw ... }` — equivalent semantics but more verbose; `Convert.ToInt32` is more concise and culturally idiomatic. The Dart source does not handle the 'neither int nor double' case (would throw a CastException); C# `Convert.ToInt32` throws `InvalidCastException` or `FormatException` on incompatible inputs — semantically equivalent failure mode. Null-safety: the `as` cast in Dart on a non-null `value` succeeds or throws; the `is ConstTerm` guard upstream ensures `agentArg.value` / `indexArg.value` are accessible. Reference-vs-value: `string` is reference-but-immutable in both languages; `int` is value type."
  - construct_key: "dart.string.interpolation_with_three_part_colon_separated_key"
    source_form: "`globalNameToLocal['${gn.type.name}:${gn.agent}:${gn.index}'] = useReader ? pair.readerAddr : pair.writerAddr;` — composite-key string interpolation using the enum's `.name` field (Dart 2.15+ adds `.name` to enums automatically, returning the source identifier string) followed by `:agent:index`. Key is then looked up inside `_substituteLocalVars` via `final key = '$type:${agentArg.value}:${indexArg.value}';` where `type` is the literal string `'writer'` or `'reader'` (matching the Dart-enum `.name` output)."
    target_decision: "C# composite key: `globalNameToLocal[$\"{gn.Type.ToString().ToLowerInvariant()}:{gn.Agent}:{gn.Index}\"] = useReader ? pair.ReaderAddr : pair.WriterAddr;`. The Dart enum `.name` returns the source identifier verbatim (`writer` / `reader`, lower-case as declared). C# `Enum.ToString()` returns the PascalCase declared name (`Writer` / `Reader` per the .NET naming convention applied above). To preserve the Dart-output key shape byte-identically, the C# port lower-cases the enum name via `.ToString().ToLowerInvariant()`. The lookup-side key construction inside `_SubstituteLocalVars` uses the LITERAL strings `\"writer\"` / `\"reader\"` (matching what the writer side produces): `string key = $\"{type}:{agentArg.Value}:{indexArg.Value}\";` where `type` is selected by ternary `term.Functor == \"_w\" ? \"writer\" : \"reader\"`. Both sides MUST agree on the casing — recording explicitly in the nuance below."
    idiom_id: null
    research_finding_id: rf-dart-enum-name-to-csharp-enum-tostring-casing
    nuance: "Enum-name-string nuance (explicitly addressed and LOAD-BEARING): Dart 2.15+ enum `.name` returns the SOURCE identifier exactly as declared (`writer` lower-case for `enum GlobalNameType { writer, reader }`). C# `Enum.ToString()` returns the .NET-conventional PascalCase declared name (`Writer` / `Reader` after our naming-convention conversion). The composite-key string `${gn.type.name}:${gn.agent}:${gn.index}` is a CROSS-METHOD CONTRACT — `globalizeTermWithResult` writes the key and `_substituteLocalVars` reads it; if the two sides disagree on casing the lookup fails silently and the term substitution returns the original StructTerm without substitution (a SILENT BUG). The C# port MUST therefore either (a) lower-case both sides via `.ToLowerInvariant()` and `\"writer\"`/`\"reader\"` literals, matching Dart byte-for-byte, OR (b) PascalCase both sides consistently. Option (a) is preferred because it preserves the wire-shape of the key for any consumer that round-trips it (no such consumer exists in this file, but conservative). The decision is recorded as a load-bearing nuance because it is exactly the kind of cross-method invariant that a naive port silently breaks. Stringification of `int.toString()` in interpolation: both Dart `$int` and C# `{int}` produce the same culture-invariant decimal form for `agentArg.Value` / `indexArg.Value` extracted from `ConstTerm`."
  - construct_key: "dart.collection.list_literal_of_struct_args_two_consts"
    source_form: "`return StructTerm(gn.isWriter ? '_w' : '_r', [ConstTerm(gn.agent), ConstTerm(gn.index)]);` and `final newArgs = term.args.map((a) => _substituteGlobalNames(a, mapping)).toList();`"
    target_decision: "Map Dart inline list literal `[ConstTerm(x), ConstTerm(y)]` to C# collection-expression / list-initialiser `new List<Term> { new ConstTerm(gn.Agent), new ConstTerm(gn.Index) }` (collection-initialiser syntax). Alternative C# 12 collection-expression `[new ConstTerm(gn.Agent), new ConstTerm(gn.Index)]` is also acceptable; spec preference is the explicit `new List<Term> { ... }` form for review-fidelity. The `term.args.map(...).toList()` recursive rebuild maps to `term.Args.Select(a => _SubstituteGlobalNames(a, mapping)).ToList()`. The `StructTerm` constructor takes `(string functor, List<Term> args)` per `terms.dart`'s C# port; preserve positional arg order."
    idiom_id: null
    research_finding_id: rf-dart-list-literal-to-csharp-list-initialiser
    nuance: "Allocation nuance: Dart list literal `[...]` allocates a new growable `List<dynamic>` (or type-annotated list if the context demands); C# `new List<Term> { ... }` allocates a new `List<Term>`. Both are heap-allocated each call; semantics agree. Collection-expression `[...]` in C# 12+ is syntactic sugar over the same allocation. Type-inference nuance: the Dart literal `[ConstTerm(...), ConstTerm(...)]` is inferred as `List<ConstTerm>` (subtype-of `List<Term>` via Dart's covariant generics); the C# render uses `List<Term>` explicitly because (a) C# generics are INVARIANT (`List<ConstTerm>` is NOT a `List<Term>`), and (b) the `StructTerm` ctor expects `List<Term>`. A `List<ConstTerm>` literal would compile-error in C# at the StructTerm call site. This invariance vs. covariance is the load-bearing nuance — naïve transliteration would compile in Dart and fail in C#."
  - construct_key: "dart.string.const_literal_comparisons_underscore_w_underscore_r_functor_match"
    source_form: "`if ((term.functor == '_w' || term.functor == '_r') && term.args.length == 2) { ... }` — equality test of `StructTerm.functor` (typed as `String`) against two literal strings, plus arity check. The functor encodes which type of global-name struct the term represents."
    target_decision: "Map Dart string-literal equality directly to C# `if ((term.Functor == \"_w\" || term.Functor == \"_r\") && term.Args.Count == 2)`. .NET `string` equality via `==` is value-equality by default (NOT reference equality); semantics agree with Dart. The C# `Args.Count` mirrors Dart's `args.length` (Dart `List.length` → C# `List<T>.Count`). The literal `\"_w\"` / `\"_r\"` strings are interned in both languages by the compiler — same allocation behaviour. PRESERVE the load-bearing constants byte-identically: a typo or rename here would silently break the round-trip through the GLP wire format."
    idiom_id: null
    research_finding_id: rf-dart-string-equality-to-csharp-string-equality
    nuance: "String-equality semantic nuance (explicitly addressed): C# `==` on `string` is overloaded to call `String.Equals(string, string)` — ordinal value equality. This MATCHES Dart `String ==` (value equality on UTF-16 code units). No `StringComparison` parameterisation is required for these ASCII literals. Functor-name nuance (LOAD-BEARING): `_w` and `_r` are WIRE-LEVEL CONSTANTS of the GLP global-name encoding (per madGLP-spec). They MUST be preserved verbatim across the conversion — any rename (e.g. to `\"writer\"` / `\"reader\"`) would break interop with the rest of the multiagent runtime that produces/consumes these StructTerms. The constants are declared inline (no central constant declaration); the C# port preserves the inlined literals (an opportunity to centralise into `const string GlobalWriterFunctor = \"_w\";` is a refactoring outside this spec's scope per FR-023)."
conversion_units:
  - "namespace declaration mirroring lib/multiagent/ per the workspace's pair-specific namespace convention (drops Dart 'library;' directive)"
  - "file-level XML doc-comment block (// or <remarks>) — 'Helper types and operations for madGLP ...' preserved byte-identically"
  - "enum GlobalNameType (Writer, Reader) — PascalCase per .NET; underlying int; no explicit values"
  - "sealed class GlobalName (reference type, VALUE-EQUALITY explicit override — NOT a record)"
  - "  property: GlobalNameType Type { get; }"
  - "  property: string Agent { get; }"
  - "  property: int Index { get; }"
  - "  private ctor: GlobalName(GlobalNameType type, string agent, int index)"
  - "  static factory: GlobalName Writer(string agent, int index)"
  - "  static factory: GlobalName Reader(string agent, int index)"
  - "  expression-bodied: bool IsWriter / bool IsReader"
  - "  override ToString() — ternary on Type, byte-identical to Dart output ('_w(agent, index)' / '_r(agent, index)')"
  - "  override Equals(object?) — by (Type, Agent, Index)"
  - "  override GetHashCode() — HashCode.Combine(Type, Agent, Index)"
  - "class GlobalSendSpawn (reference type, IDENTITY EQUALITY — NOT a record)"
  - "  property: int ReaderAddr { get; }"
  - "  property: GlobalName GlobalName { get; }"
  - "  property: string DestAgent { get; }"
  - "  ctor: GlobalSendSpawn(int readerAddr, GlobalName globalName, string destAgent)"
  - "  override ToString() — interpolated 'GlobalSendSpawn(reader=..., name=..., dest=...)'"
  - "class TermVar (reference type, IDENTITY EQUALITY)"
  - "  property: int Addr { get; }"
  - "  property: bool IsReader { get; }"
  - "  property: int WriterAddr { get; }"
  - "  property: int ReaderAddr { get; }"
  - "  private ctor: TermVar(int addr, bool isReader, int writerAddr, int readerAddr)"
  - "  static factory: TermVar Writer(int addr, int readerAddr) — derives writerAddr = addr"
  - "  static factory: TermVar Reader(int addr, int writerAddr) — derives readerAddr = addr"
  - "  expression-bodied: bool IsWriter / int PairedReaderAddr"
  - "  override ToString() — ternary on IsReader"
  - "class GlobalizeResult (reference type, IDENTITY EQUALITY, no ToString override)"
  - "  property: IReadOnlyList<GlobalName> GlobalNames { get; }"
  - "  property: IReadOnlyList<GlobalSendSpawn> Spawns { get; }"
  - "  ctor: GlobalizeResult(IReadOnlyList<GlobalName> globalNames, IReadOnlyList<GlobalSendSpawn> spawns)"
  - "class FreshPair (reference type, IDENTITY EQUALITY, POSITIONAL ctor — distinguished from named-required siblings)"
  - "  property: int WriterAddr { get; }"
  - "  property: int ReaderAddr { get; }"
  - "  ctor: FreshPair(int writerAddr, int readerAddr)"
  - "  override ToString() — 'FreshPair(writer=..., reader=...)'"
  - "class LocalizeResult (reference type, IDENTITY EQUALITY, no ToString override)"
  - "  property: IReadOnlyList<FreshPair> FreshPairs { get; }"
  - "  property: IReadOnlyList<bool> UseReader { get; }   // PARALLEL-INDEXED with FreshPairs"
  - "  property: IReadOnlyList<GlobalSendSpawn> Spawns { get; }"
  - "  ctor: LocalizeResult(IReadOnlyList<FreshPair> freshPairs, IReadOnlyList<bool> useReader, IReadOnlyList<GlobalSendSpawn> spawns)"
  - "static class MadHelpers — hosts the top-level free functions"
  - "  public static GlobalizeResult Globalize(IReadOnlyList<TermVar> variables, string localAgent, string remoteAgent, GlobalWritersTable table) — synchronous, mutates table, returns new GlobalizeResult; PRESERVES inverted-naming comment ('GlobalSendSpawn.readerAddr is used as the key for heap.onBind() ... we pass writerAddr')"
  - "  public static LocalizeResult Localize(IReadOnlyList<GlobalName> globalNames, string localAgent, GlobalWritersTable table, Func<(int writerAddr, int readerAddr)> freshAddrAllocator) — synchronous, callable-arg for fresh-pair allocation; PRESERVES inverted-naming comment at writer branch"
  - "  public static Term GlobalizeTermWithResult(Term term, IReadOnlyList<TermVar> variables, GlobalizeResult result) — builds varToGlobalName dictionary, delegates to _SubstituteGlobalNames"
  - "  public static IReadOnlyList<GlobalName> ExtractGlobalNames(Term term) — entry-point that allocates List<GlobalName>, calls _ExtractGlobalNamesRecursive, returns the list (eager)"
  - "  public static Term LocalizeTermWithResult(Term term, IReadOnlyList<GlobalName> globalNames, LocalizeResult result) — builds composite-key Dictionary<string, int>, delegates to _SubstituteLocalVars; KEY CASING must match _SubstituteLocalVars (lower-case 'writer'/'reader')"
  - "  private static Term _SubstituteGlobalNames(Term term, Dictionary<int, GlobalName> mapping) — pattern-match on Term, rebuilds StructTerm recursively; TryGetValue on dictionary lookup (NOT indexer, which throws on miss)"
  - "  private static void _ExtractGlobalNamesRecursive(Term term, List<GlobalName> result) — void with out-parameter style; recurses into StructTerm args; appends GlobalName.Writer / GlobalName.Reader on _w/_r matches; uses Convert.ToInt32 for permissive num-to-int extraction"
  - "  private static Term _SubstituteLocalVars(Term term, Dictionary<string, int> mapping) — pattern-match on Term, composite-key lookup, rebuilds StructTerm recursively"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-library-directive-to-csharp-namespace-no-counterpart — library directive

- Deep analysis. `library;` at the top of the file is Dart 2.19+ syntax that
  declares the file as the root of an implicit single-file library, which
  permits the preceding `///` doc-comment block to attach to the library
  itself rather than the first declaration. No other library directives
  (`part`, `part of`, named libraries) are used.
- Authoritative Dart. The Dart language tour page on libraries
  (https://dart.dev/language/libraries) documents the `library` directive
  as 'optional in Dart 2.19 and later when the library has no name'; its
  ONLY semantic role here is to host a documentation comment block.
- Authoritative .NET. Microsoft Learn 'Namespaces in C#'
  (https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/namespace)
  documents C# namespace declarations as the unit of logical name scoping;
  there is no per-file scoping directive. File-level documentation in C#
  is rendered as XML doc-comments on the namespace or its containing
  types.
- Conclusion. Drop the `library;` directive; preserve the doc-comment as
  file-header `//` comments or a `<remarks>` block on the static class.
  Authoritative both sides; no escalation.

### rf-dart-import-to-csharp-using — import-to-using mapping

- Deep analysis. Two imports: one `package:` (resolved via pubspec) and
  one relative (same directory). Both bring types into scope.
- Authoritative Dart. dart.dev `https://dart.dev/language/libraries#using-libraries`
  documents `import` as scope-introducing with no semantic difference
  between `package:` and relative URLs (URL is resolved by the package
  manager / filesystem).
- Authoritative .NET. Microsoft Learn 'using directive'
  (https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/using-directive)
  documents `using` as namespace-scope-introducing — exactly the role of
  Dart import.
- Conclusion. `import 'package:glp_runtime/runtime/terms.dart';` →
  `using GlpRuntime.Runtime;`; relative import to a same-namespace file is
  redundant in C# (the current namespace is implicitly in scope), so the
  line is dropped. Authoritative; no escalation.

### rf-dart-enum-plain-to-csharp-enum — plain enum (REUSE, message_queue.dart)

- Reused verbatim from `lib/multiagent/message_queue.dart.md` (rf-dart-enum-plain-to-csharp-enum). Same shape — plain Dart enum with two camelCase members, no associated state, no methods — maps to a C# enum with two PascalCase members. Authoritative; no escalation.

### rf-dart-class-with-equals-and-hashcode-to-csharp-equals-gethashcode — value-equality class

- Deep analysis. `GlobalName` explicitly overrides `==` and `hashCode` for
  structural equality across `(type, agent, index)`. The class is used
  AS A DICTIONARY KEY (via stringified composite-key
  `${gn.type.name}:${gn.agent}:${gn.index}` in `_substituteLocalVars`),
  which makes the structural-equality contract load-bearing.
- Authoritative Dart. dart.dev 'Equality'
  (https://dart.dev/language/operators#equality-and-relational-operators)
  documents `==` as an Object-defined virtual method that subclasses
  override; `hashCode` must be overridden consistently. `Object.hash(...)`
  (https://api.dart.dev/dart-core/Object/hash.html) is the documented
  combine helper.
- Authoritative .NET. Microsoft Learn 'Equals and the equality operator =='
  (https://learn.microsoft.com/dotnet/csharp/programming-guide/statements-expressions-operators/equality-comparisons)
  documents the canonical override pattern: `public override bool Equals(object? obj)` plus `public override int GetHashCode()`. `HashCode.Combine(...)` (https://learn.microsoft.com/dotnet/api/system.hashcode.combine) is the documented .NET combine helper, structurally equivalent to Dart `Object.hash`. Microsoft Learn 'Records' explicitly notes that records provide SYNTHESISED value equality — opposite of the explicit-override Dart shape we are translating.
- Decision. Override `Equals(object?)` + `GetHashCode()` explicitly on `GlobalName` (NOT `record`). Use `HashCode.Combine` for the hash. Authoritative both sides; no escalation.

### rf-dart-final-named-required-ctor-to-csharp-getonly-properties — immutable holder (REUSE, global_writers_table)

- Reused verbatim from `lib/multiagent/global_writers_table.dart.md`. Same shape — Dart class with `final` fields and named-required ctor, no equality override — maps to a C# reference class with get-only auto-properties and a non-optional-parameter ctor. Applied here to `GlobalSendSpawn`. Authoritative; no escalation.

### rf-dart-named-factory-ctors-with-initialiser-list-to-csharp-static-factories — multi-shape construction

- Deep analysis. `TermVar` (and `GlobalName`) declare named factory-style
  constructors with initialiser lists that compute multiple `final` fields
  from a smaller set of caller-supplied parameters (`TermVar.writer(addr,
  {readerAddr}) : isReader = false, writerAddr = addr;`). This is a
  multi-shape construction idiom: per-shape entry points to a single
  underlying invariant.
- Authoritative Dart. dart.dev 'Constructors'
  (https://dart.dev/language/constructors#named-constructors) and
  'Initializer lists' (same page) document named constructors as
  per-name entry points and initialiser lists as pre-body field
  assignment with derived values.
- Authoritative .NET. Microsoft Learn 'Constructors'
  (https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/constructors)
  documents C# constructors as nameless and distinguished only by
  parameter signature. C# has NO equivalent of named-per-shape
  constructors. The documented .NET counterpart is STATIC FACTORY METHODS
  on the class itself (Microsoft Learn 'Factory pattern' and Framework
  Design Guidelines — 'Constructors' and 'Static methods' sections).
- Decision. `TermVar.Writer(...)` / `TermVar.Reader(...)` and
  `GlobalName.Writer(...)` / `GlobalName.Reader(...)` as static factory
  methods, all delegating to a single private constructor that accepts
  every field. Authoritative both sides; no escalation.

### rf-dart-final-list-fields-to-csharp-ireadonlylist-properties — result-holder lists

- Deep analysis. `GlobalizeResult` and `LocalizeResult` hold `final
  List<T>` fields whose contents are not mutated after construction by
  any consumer in this file. Dart `final` freezes the reference but not
  the contents; the consumer-side contract is read-only.
- Authoritative .NET. Microsoft Learn 'IReadOnlyList<T> Interface'
  (https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
  documents `IReadOnlyList<T>` as a read-only-view interface that exposes
  `Count` and indexer but not `Add`/`Remove`/`Insert`/`Clear`. It is the
  canonical .NET counterpart for a read-only-list property surface.
- Conclusion. Properties typed `IReadOnlyList<T>` (NOT `List<T>` — which
  would leak mutation capability). Constructors accept `IReadOnlyList<T>`
  parameters; if a caller passes a `List<T>` the implicit covariance
  matches. Authoritative; no escalation.

### rf-dart-final-positional-ctor-to-csharp-positional-ctor — positional immutable holder

- Deep analysis. `FreshPair` uses a positional (not named-required)
  constructor. Two `final int` fields, `toString` override, no equality
  override.
- Authoritative .NET. Microsoft Learn 'Constructors' documents positional
  constructors as the default C# form. Identity equality is the default.
- Decision. C# class with positional constructor preserving the Dart
  shape. Authoritative; no escalation.

### rf-dart-toplevel-function-with-named-required-to-csharp-static-method — host class for free functions

- Deep analysis. `globalize`, `localize`, `globalizeTermWithResult`,
  `extractGlobalNames`, `localizeTermWithResult` are top-level Dart
  functions. C# has no top-level functions outside of a `Program` class
  with `top-level statements` (which is a single-entry-point feature,
  not a general free-function host).
- Authoritative .NET. Microsoft Learn 'Static classes and static class
  members' (https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/static-classes-and-static-class-members)
  documents the canonical pattern: a static class hosting public static
  methods is the .NET counterpart for a free-function module.
- Decision. `public static class MadHelpers` hosting all top-level
  functions as `public static` methods. The Dart `required` named
  parameters become C# positional parameters (call sites can opt into
  named-argument syntax for readability — Microsoft Learn 'Named and
  optional arguments'). Authoritative; no escalation.

### rf-dart-record-and-function-typed-param-to-csharp-valuetuple-and-func — callback returning tuple

- Deep analysis. `localize` takes `(int, int) Function() freshAddrAllocator`
  — a zero-arg callable returning a Dart record (positional 2-tuple).
- Authoritative Dart. dart.dev 'Records'
  (https://dart.dev/language/records) documents records as positional
  and/or named lightweight immutable aggregates; value types in spirit
  (compared structurally) since Dart 3.
- Authoritative .NET. Microsoft Learn 'ValueTuple Struct'
  (https://learn.microsoft.com/dotnet/api/system.valuetuple) documents
  ValueTuple as a value-type aggregate with positional and/or named
  fields and structural equality — direct counterpart of Dart records.
  Microsoft Learn 'Func<TResult> Delegate'
  (https://learn.microsoft.com/dotnet/api/system.func-1) documents
  `Func<T>` as the parameterless value-returning delegate type — direct
  counterpart of Dart `T Function()`.
- Decision. `Func<(int writerAddr, int readerAddr)>` for the callback
  type. Authoritative both sides; no escalation.

### rf-dart-is-with-promotion-to-csharp-is-pattern — runtime type-test with binding

- Deep analysis. The Dart `is T` test + automatic type promotion is used
  to dispatch on the `Term` hierarchy. The promotion makes the variable
  statically typed `T` inside the success branch.
- Authoritative Dart. dart.dev 'Types' / 'Type promotion'
  (https://dart.dev/language/type-system#type-promotion) documents
  the promotion behaviour for `is` tests on local/final variables.
- Authoritative .NET. Microsoft Learn 'Patterns - Pattern matching using
  the is and switch expressions'
  (https://learn.microsoft.com/dotnet/csharp/language-reference/operators/patterns)
  documents `is T name` as a pattern that performs a runtime type-test
  AND binds a typed variable in the success scope — exactly the Dart
  promotion behaviour, more explicit.
- Decision. Direct mapping `term is VarRef varRef` / `term is StructTerm
  structTerm`. Authoritative both sides; no escalation.

### rf-dart-map-lookup-to-csharp-trygetvalue — nullable-clean dictionary access

- Deep analysis. `Map<K, V>[k]` returns `V?` in Dart (null on miss);
  `Dictionary<K, V>[k]` throws `KeyNotFoundException` in .NET. A naive
  transliteration would change a graceful-miss into a runtime exception.
- Authoritative .NET. Microsoft Learn
  'Dictionary<TKey,TValue>.TryGetValue Method'
  (https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2.trygetvalue)
  documents `TryGetValue(key, out value)` as the documented miss-tolerant
  lookup — returns `bool`, sets `out value` to the matched value or
  `default(V)`.
- Decision. Use `TryGetValue(key, out var v)` for every lookup that the
  Dart side treats as nullable. Authoritative; no escalation.

### rf-dart-private-recursive-walker-to-csharp-private-static-method — out-parameter recursion

- Deep analysis. `_extractGlobalNamesRecursive(Term, List<GlobalName>)`
  uses out-parameter mutation style (passes the list down, appends in
  place). The conversion preserves the eager out-parameter shape rather
  than rewriting to `IEnumerable<T>` with `yield return`.
- Authoritative .NET. Microsoft Learn 'Method parameters'
  (https://learn.microsoft.com/dotnet/csharp/methods) documents
  reference-type parameter passing — a passed `List<T>` is appended in
  place by the callee, identical to Dart. `yield return` would change
  evaluation timing (deferred) and allocate enumerator state, neither
  matching the Dart behaviour.
- Decision. Preserve out-parameter recursion. Authoritative; no
  escalation.

### rf-dart-num-toint-to-csharp-convert-toint32 — permissive numeric coercion

- Deep analysis. `(indexArg.value as num).toInt()` accepts either `int`
  or `double` and narrows to `int`. .NET has no `num` supertype.
- Authoritative .NET. Microsoft Learn
  'Convert.ToInt32(Object) Method'
  (https://learn.microsoft.com/dotnet/api/system.convert.toint32)
  documents the method as 'Converts the value of the specified object to
  a 32-bit signed integer' — dispatches on the runtime type via
  IConvertible, supporting all built-in numeric types and string. Direct
  counterpart of Dart's `num.toInt()`.
- Decision. `Convert.ToInt32(indexArg.Value)` for the permissive
  coercion. Authoritative; no escalation.

### rf-dart-enum-name-to-csharp-enum-tostring-casing — cross-method key casing contract

- Deep analysis. Dart `enum.name` returns the source-identifier verbatim
  (lower-case `writer`/`reader`). C# `Enum.ToString()` returns the
  .NET-conventional declared name (PascalCase `Writer`/`Reader` after
  our naming-convention conversion). The composite-key string is a
  CROSS-METHOD CONTRACT between `LocalizeTermWithResult` (writer) and
  `_SubstituteLocalVars` (reader).
- Authoritative Dart. dart.dev 'Enumerated types'
  (https://dart.dev/language/enums#using-enums) documents `.name` as
  'a string version of the enum's value, similar to what toString
  returned in Dart 2.14 and earlier' — returns the SOURCE identifier
  exactly as declared.
- Authoritative .NET. Microsoft Learn 'Enum.ToString Method'
  (https://learn.microsoft.com/dotnet/api/system.enum.tostring) and
  'Enum.GetName' document the default ToString as 'the textual
  representation of the value of this instance' — for a named constant,
  the declared name (PascalCase per our conversion).
- Decision. To preserve the Dart-output composite-key bytes identically,
  lower-case the enum name on the writer side: `gn.Type.ToString().ToLowerInvariant()`. The reader side already uses literal `"writer"`/`"reader"` strings, matching. Authoritative; no escalation.

### rf-dart-list-literal-to-csharp-list-initialiser — list construction and covariance trap

- Deep analysis. `[ConstTerm(x), ConstTerm(y)]` is a Dart list literal
  containing two `ConstTerm` instances. Inferred type is
  `List<ConstTerm>`, which is a `List<Term>` under Dart's covariant
  generics. C# generics are INVARIANT — `List<ConstTerm>` is NOT a
  `List<Term>`.
- Authoritative .NET. Microsoft Learn 'Covariance and contravariance in
  generics'
  (https://learn.microsoft.com/dotnet/csharp/programming-guide/concepts/covariance-contravariance/)
  documents that .NET generic CLASSES (including `List<T>`) are
  invariant; only some generic INTERFACES (`IEnumerable<T>`,
  `IReadOnlyList<T>`) are covariant. Therefore the list passed to the
  `StructTerm(string, List<Term>)` constructor MUST be typed as
  `List<Term>`, not `List<ConstTerm>`.
- Decision. Emit `new List<Term> { new ConstTerm(...), new ConstTerm(...) }`
  with the explicit `Term` element type. Authoritative; no escalation.

### rf-dart-string-equality-to-csharp-string-equality — value-equality of literal constants

- Deep analysis. `term.functor == '_w'` is value-equality on string
  contents. Both languages overload `==` on strings to ordinal value
  equality.
- Authoritative .NET. Microsoft Learn 'String equality'
  (https://learn.microsoft.com/dotnet/csharp/programming-guide/strings/how-to-compare-strings)
  documents `==` on `string` as `String.Equals(string, string)` ordinal
  comparison. Direct match with Dart.
- Decision. Direct mapping; preserve the literal constants `"_w"` /
  `"_r"` verbatim. Authoritative; no escalation.

## Notes — well-known nuances explicitly addressed (FR-009 / US2 AS4)

- **Future / async / await / Completer / Task / TaskCompletionSource**: ABSENT
  from this file. Every function is synchronous; the multi-agent runtime's
  asynchrony lives in `lib/multiagent/isolate_manager.dart` and
  `lib/multiagent/message_queue.dart`'s consumers, not here. The conversion
  MUST NOT silently introduce `Task<T>` return types or `async` modifiers —
  doing so would force every caller into `await`, changing the
  surrounding execution semantics. This is exactly the "robustness as
  workaround" trap the project's bug protocol forbids: an absent shape
  must not be invented.
- **Stream / StreamController / IAsyncEnumerable**: ABSENT. There is no
  publish/subscribe surface. Codegen MUST NOT introduce
  `IAsyncEnumerable<T>` or `System.Threading.Channels.Channel<T>`.
- **Isolate (Dart share-nothing) → C# (no direct equivalent)**: this file
  DOES NOT exercise Dart `Isolate` directly. It is the SUPPORT-LAYER for
  isolate-crossing data marshaling (`Globalize` / `Localize` transform
  terms between local and global representations FOR inter-agent
  communication — the comments make this explicit), but the actual
  isolate-spawn machinery lives in `lib/multiagent/isolate_manager.dart`.
  The isolate-equivalence decision (pinned `System.Threading.Thread`,
  single-threaded `TaskScheduler`, actor mailbox via
  `System.Threading.Channels.Channel<T>`, or
  `ConcurrentExclusiveSchedulerPair.ExclusiveScheduler`) is RECORDED ONCE
  in `lib/multiagent/global_writers_table.dart.md`'s
  rf-dart-isolate-singlethread-to-csharp-actor-or-pinned-thread and
  inherited file-wide for the multiagent subsystem (FR-024 reuse). This
  file's contribution to the contract is preserving the per-agent
  single-threaded ownership of `GlobalWritersTable` mutations — every
  `table.AddGlobalizeEntry`, `table.AllocateIndex`, `table.AddLocalizeEntry`
  call happens on the agent's owning execution context, NOT on a
  background thread. No async signature is introduced on this file's
  surface to ensure that invariant is preserved at the call site. NO
  ESCALATION: the load-bearing concurrency decision is authoritative and
  recorded; this file inherits it correctly.
- **Mixin / extension / sealed / abstract**: ABSENT from this file's
  declarations. The `Term` hierarchy lives in `lib/runtime/terms.dart` and
  is NOT `sealed` in the Dart source — the C# port likewise should not be
  `sealed` unless a deliberate decision is taken at the `terms.dart`
  convspec level. This file consumes `Term` openly via `is`-pattern
  dispatch with a fall-through default — open-hierarchy assumption
  preserved.
- **`null`-safety**: every field non-nullable except where the source
  explicitly uses `?` — and this file has NO `?`-typed declarations. All
  Dart `String`/`int`/`bool`/`GlobalName`/`GlobalWritersTable`/`Term`
  field/parameter types map to non-nullable C# under an enabled nullable
  context. The Dart `Map<int, GlobalName>` lookup `mapping[key]` returns
  `GlobalName?` implicitly — the conversion uses `TryGetValue` to make the
  nullable case explicit. Force-unwrap (`!`) is NOT used in this file.
- **Generic variance trap**: the `List<ConstTerm>` literal that is assigned
  to a `List<Term>` parameter is a covariance leak that Dart accepts via
  its covariant generics and C# rejects via invariance. The
  rf-dart-list-literal-to-csharp-list-initialiser idiom records this
  load-bearing translation; codegen MUST emit `new List<Term> { ... }`,
  not `new List<ConstTerm> { ... }`, at every term-rebuild site.
- **Cross-method key-casing contract**: the composite-key
  `${type.name}:${agent}:${index}` shared between
  `LocalizeTermWithResult` and `_SubstituteLocalVars` is a load-bearing
  byte-level contract — both sides must agree on lower-case
  `"writer"` / `"reader"`. The C# port uses `.ToLowerInvariant()` on the
  enum side. Recorded in rf-dart-enum-name-to-csharp-enum-tostring-casing.
- **Inverted-naming load-bearing comment**: the inline doc-comment block
  inside `globalize` / `localize` that explains 'GlobalSendSpawn.readerAddr
  is used as the key for heap.onBind(), which is indexed by *writer*
  address. We pass writerAddr so the callback fires when bindVariable is
  called on Y' is LOAD-BEARING — without it, a maintainer reading the
  spawn-construction site would file a bug ('field misnamed'). The C#
  render MUST preserve this comment byte-identically as a
  triple-slash XML-doc remark at the same code site.
- **Zero escalations**: every non-trivial construct in this file is
  resolved from authoritative Dart (dart.dev / api.dart.dev) and/or .NET
  (learn.microsoft.com) documentation. The hard Isolate-equivalence
  decision is INHERITED from
  `lib/multiagent/global_writers_table.dart.md`'s authoritative
  resolution (FR-024 cache reuse); the inheritance is what FR-024 is
  for. No `kind: undecidable` here.
