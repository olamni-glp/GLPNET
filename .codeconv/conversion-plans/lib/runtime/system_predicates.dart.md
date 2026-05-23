---
path: lib/runtime/system_predicates.dart
cycle_group_id: 36
scc_siblings: [lib/bytecode/runner.dart, lib/multiagent/mad_context.dart, lib/runtime/body_kernels.dart, lib/runtime/glp_activation.dart, lib/runtime/runtime.dart]
generated_at: 2026-05-21T16:30:00Z
source_sha256: ec6e1f4d6555f57c8b7450418b64282524e86e8b2ba6d06323047da3c7a64b05
schema_version: 1
---

# Conversion Plan: lib/runtime/system_predicates.dart

## 1. Source Analysis

Inspected `glp_runtime_net/lib/runtime/system_predicates.dart` (80 lines) directly. The file declares the system-predicate execution infrastructure that backs the `execute` bytecode opcode. Surface enumerated:

1. **Library-level doc-comment block** (lines 1–10): multi-paragraph triple-slash doc comment describing the file's role (registry + call-context plumbing for external functions invoked from GLP), the four kinds of predicates expected (I/O, arithmetic, system information, side-effecting host interaction), and the FCP-inspiration provenance. No explicit `library;` directive — implicit-library form.
2. **Two relative imports** (lines 12–13): `import 'runtime.dart';` (brings `GlpRuntime` — the runtime façade type used by the `SystemPredicate` delegate's first parameter) and `import 'terms.dart';` (sibling-file dependency completeness; predicate IMPLEMENTATIONS use `Term`/`VarRef`/`ConstTerm`/`StructTerm` types, but THIS file does not directly reference them). No `show`/`hide` narrowing.
3. **Plain enum `SystemResult`** (lines 15–25): three tag-only members (`success`, `failure`, `suspend`), each with a triple-slash doc comment. No constructor, no fields, no methods, no associated values. Used as the return type of every `SystemPredicate` and compared by `==`; ordinal values are never observed.
4. **Mutable call-context class `SystemCall`** (lines 27–40): two `final` fields (`String name`, `List<Object?> args`) bound by positional initialising-formal constructor `SystemCall(this.name, this.args)`, plus a THIRD `final` field `Set<int> suspendedReaders = {}` initialised inline to an empty growable set. The class's leading doc comment explicitly documents the mutability contract: "Contains arguments and collects suspended readers if predicate blocks". No `==`/`hashCode`/`toString()` overrides — default reference identity.
5. **Function-type typedef `SystemPredicate`** (lines 42–57): `typedef SystemPredicate = SystemResult Function(GlpRuntime rt, SystemCall call);` with multi-paragraph doc comment documenting the parameters, the return, and the side effects ("Can modify: Writer bindings (via rt.bindWriter), call.suspendedReaders (if suspending)"). The typedef name appears in the registry signature `Map<String, SystemPredicate>`.
6. **Registry class `SystemPredicateRegistry`** (lines 59–79): one inline-initialised `final Map<String, SystemPredicate> _predicates = {}` backing field, four members — `register(String, SystemPredicate)` void (indexer-set), `lookup(String) → SystemPredicate?` arrow-body via Map indexer, `has(String) → bool` arrow-body via `containsKey`, and `Iterable<String> get names => _predicates.keys`. Key is the BARE name (NOT name/arity — system predicates dispatch on name alone). NO `==`/`hashCode`/`toString()` overrides — default reference identity.

Zero `async`/`await`/`Future`/`Stream`/`Isolate`/`Completer` surface. Zero `dart:io` surface (the doc-comment mention of "I/O operations (file, terminal, network)" describes downstream predicate IMPLEMENTATIONS, NOT this file). The file is purely synchronous registry + call-context plumbing; the "suspend" tag is NOT async — it is a synchronous return value telling the scheduler to park the goal until the readers in `call.suspendedReaders` bind.

## 2. Dart → C#/.NET Conversion Plan

Construct-by-construct mirror of the ratified convspec (each row faithfully renders the Dart source into the canonical .NET shape). The "→" arrows below are U+2192.

- **Library-level doc-comment block → file-header XML-doc on namespace declaration.** Multi-paragraph `<summary>` followed by `<list type="bullet">` rendering of the four bullet items (`<item><description>I/O operations (file, terminal, network)</description></item>` etc.) and a `<remarks>Inspired by FCP's execute mechanism but adapted for Dart.</remarks>` paragraph. No `library;` directive to elide (none present). Per Microsoft Learn "Recommended XML tags for C# documentation comments".

- **`import 'runtime.dart'; import 'terms.dart';` → `using <root>.Runtime;`.** Both sibling-file imports collapse to a single `using` directive (both target the same `lib/runtime/` namespace). Carry-forward from external_io.dart.md / heap_fcp.dart.md.

- **`enum SystemResult { success, failure, suspend }` → `public enum SystemResult { Success, Failure, Suspend }`.** Plain payload-free enum; default `int` underlying type; declaration order preserves implicit ordinals (`Success = 0`, `Failure = 1`, `Suspend = 2`). Three triple-slash doc comments preserved as `/// <summary>...</summary>` XML-doc with hyphens preserved byte-identically (prose, not en-dashes). NO `record`/sealed-class discriminated-union — the enum is genuinely payload-free.

- **`class SystemCall { ... }` → `public sealed class SystemCall` (reference type; NOT `record`, NOT `struct`).** Three properties:
  - `public string Name { get; }` — Dart `final String name` → get-only auto-property, non-nullable under enabled NRT.
  - `public IReadOnlyList<object?> Args { get; }` — Dart `final List<Object?> args` → read-only-view surface, alias-not-copy. Non-nullable list of nullable elements.
  - `public ISet<long> SuspendedReaders { get; } = new HashSet<long>();` — Dart `final Set<int> suspendedReaders = {}` → get-only auto-property with inline initialiser. The property is GET-ONLY at the public surface (reference write-once, matching Dart `final`) but the SET ITSELF is mutable (`ISet<long>` exposes `Add`/`Remove`/`Clear`; callees `.Add(id)` to record suspended-reader addresses). NOT `IReadOnlySet<long>` — the contract REQUIRES callees to mutate the set. Int width: Dart `int` → C# `long` (heap reader IDs, carry-forward from terms.dart.md / external_io.dart.md).

  Constructor: `public SystemCall(string name, IReadOnlyList<object?> args) { Name = name; Args = args; }` — Dart `this.name`/`this.args` initialising-formal shorthand expands to two explicit assignments (C# has no initialising-formal shorthand per Microsoft Learn "Instance constructors").

  Reference-type rationale (load-bearing): (i) the predicate callee MUTATES `call.suspendedReaders` via `.Add(addr)`; this mutation MUST be visible to the caller — reference semantics REQUIRED. (ii) Two coincidentally-equal `SystemCall` instances are NOT the same call — reference identity is correct. (iii) The leading doc comment's "collects" verb is the load-bearing mutability contract. Record/struct REJECTED for the reasons enumerated in the convspec.

- **`typedef SystemPredicate = SystemResult Function(GlpRuntime rt, SystemCall call);` → `public delegate SystemResult SystemPredicate(GlpRuntime rt, SystemCall call);`.** NAMED `delegate` declaration (NOT `using SystemPredicate = System.Func<GlpRuntime, SystemCall, SystemResult>;` structural alias — the named typedef has its own type identity used as the value type of the registry `Map<String, SystemPredicate>` and as the parameter type of `Register`/`Lookup`; a named delegate preserves diagnostic identity per Microsoft Learn "delegate (C# Reference)"). Parameters preserved verbatim: `GlpRuntime rt`, `SystemCall call`, both non-nullable reference types under enabled NRT. NO `ref` qualifier on `SystemCall call` — the type is already a reference; `ref` would be over-translation. Multi-paragraph doc comment preserved verbatim as XML-doc (`<summary>`, `<param>`, `<returns>`, `<remarks>` per Microsoft Learn).

- **`class SystemPredicateRegistry { ... }` → `public sealed class SystemPredicateRegistry` (reference type; NOT `record`, NOT `struct`).** Backing field and four members:
  - `private readonly Dictionary<string, SystemPredicate> _predicates = new Dictionary<string, SystemPredicate>();` — Dart `final Map<String, SystemPredicate> _predicates = {}` → `readonly` Dictionary with inline initialiser. `readonly` preserves "rebind-final, contents-mutable" semantics (blocks field reassignment, permits `Add`/`Remove`/indexer-set). Key is the BARE name (NOT `$"{name}/{arity}"` — system predicates dispatch on name alone; arity validation is internal to each predicate against `call.Args.Count`).
  - `public void Register(string name, SystemPredicate predicate) { _predicates[name] = predicate; }` — block body; indexer-set (add-or-overwrite, identical contract both sides).
  - `public SystemPredicate? Lookup(string name) => _predicates.TryGetValue(name, out var p) ? p : null;` — expression-bodied. **Load-bearing DIVERGENCE**: Dart `Map<K,V>` indexer returns `null` on missing key; C# `Dictionary<K,V>` indexer THROWS `KeyNotFoundException`. Faithful render uses `TryGetValue` (NOT the indexer) per Microsoft Learn `Dictionary<TKey,TValue>.TryGetValue`. Return type `SystemPredicate?` is nullable (delegates are reference types).
  - `public bool Has(string name) => _predicates.ContainsKey(name);` — expression-bodied; Dart `containsKey` → C# `ContainsKey` byte-identical semantics.
  - `public IEnumerable<string> Names => _predicates.Keys;` — expression-bodied getter; Dart `Iterable<String>` → C# `IEnumerable<string>`; `Dictionary<TKey,TValue>.Keys` returns a `KeyCollection` assignable to `IEnumerable<TKey>` per Microsoft Learn.

  Record REJECTED — the registry has mutable internal state; record-synthesised structural equality and `with`-expression baggage are not in the Dart source.

## 3. Decomposed Task Units

- **T1.** Emit file-header XML-doc on namespace declaration (multi-paragraph `<summary>` + `<list type="bullet">` for the four bullet items + `<remarks>` for the FCP-inspiration line); preserve hyphens byte-identically.
- **T2.** Emit `using <root>.Runtime;` directive (collapses both Dart relative imports — sibling files `runtime.cs` and `terms.cs` share the namespace).
- **T3.** Emit `public enum SystemResult { Success, Failure, Suspend }` with three triple-slash `<summary>` XML-docs preserved verbatim from the Dart source.
- **T4.** Emit `public sealed class SystemCall` reference class with three get-only auto-properties (`string Name`, `IReadOnlyList<object?> Args`, `ISet<long> SuspendedReaders = new HashSet<long>()`) and one positional constructor expanding Dart `this.name`/`this.args` into explicit assignments.
- **T5.** Emit `public delegate SystemResult SystemPredicate(GlpRuntime rt, SystemCall call);` named delegate with multi-paragraph XML-doc (`<summary>`/`<param>`/`<returns>`/`<remarks>`) preserved verbatim.
- **T6.** Emit `public sealed class SystemPredicateRegistry` reference class with one `private readonly Dictionary<string, SystemPredicate>` field initialised inline, plus four members: `Register` (block body, indexer-set), `Lookup` (expression-bodied, `TryGetValue` — NOT indexer), `Has` (expression-bodied `ContainsKey`), `Names` (expression-bodied getter returning `IEnumerable<string>`).
- **T7.** Verify five carry-forward idioms reused verbatim (`rf-dart-import-relative-to-csharp-using-namespace`, `rf-dart-enum-plain-to-csharp-enum`, `rf-dart-typedef-function-to-csharp-delegate`, `rf-dart-map-to-csharp-dictionary`, `rf-dart-library-directive-to-csharp-namespace-elision`) and ONE new idiom registered (`rf-dart-mutable-callcontext-class-final-fields-with-inline-set`).

## 4. Research Findings

None required. The ratified convspec resolved every non-trivial construct against authoritative dart.dev / api.dart.dev / learn.microsoft.com documentation (FR-024 cache hits on five carry-forward idioms + one new finding). No `research unavailable` escalation needed.

## 5. Consistency Pass

- **Mirror to convspec.** The plan reproduces the convspec's six constructs and 15 conversion units one-for-one. No additions, no omissions, no re-decisions.
- **Mirror to tombstone.** The tombstone's `target_path: lib/runtime/system_predicates.cs` and `open_escalation_count: 0` are preserved; the plan adds nothing the tombstone disclaims.
- **GLP / CLAUDE.md authority.** This file is .NET-side infrastructure (predicate dispatch glue) — it has no GLP-language surface. The CLAUDE.md "language authority" guardrail and SRSW invariants are not exercised by THIS file; they are exercised by the GLP programs that CALL system predicates via the `execute` opcode at the bytecode level (which is `lib/bytecode/runner.dart`'s concern — see §7 sibling cross-reference).
- **Threading model coherence with SCC siblings (escalations #4 / #5 already ratified).** This file declares NO concurrency primitives (no `lock`/`Interlocked`/`ConcurrentDictionary`/`SemaphoreSlim`/`Channel<T>`/`Task`/`async`/`await`). `SystemPredicateRegistry._predicates` is a plain `Dictionary<string, SystemPredicate>`, `SystemCall.SuspendedReaders` is a plain `HashSet<long>`. This is faithful to the Dart source AND consistent with the ratified threading model: predicate dispatch runs INSIDE the single-owning-context of the calling agent (heap_fcp single-owning-context, escalation #4, commit `497428c8`), serialised one-message-at-a-time through the agent's `Channel<IsolateMessage>` mailbox (escalation #5, commit `12a468f5`). Atomicity of the indexer-set / `TryGetValue` / `ContainsKey` operations is guaranteed by mailbox serialisation at the agent boundary, NOT by per-collection locks — the same invariant heap_fcp / mad_context rely on. Re-introducing `ConcurrentDictionary` or `lock` here would over-translate (Dart has no such primitive) AND violate the SCC-wide single-owning-context discipline.
- **SCC coherence.** All 5 sibling files (`runner.dart`, `mad_context.dart`, `body_kernels.dart`, `glp_activation.dart`, `runtime.dart`) reference types declared HERE (`SystemPredicate` delegate, `SystemCall` class, `SystemResult` enum, `SystemPredicateRegistry` class) or are referenced FROM here (`GlpRuntime` parameter type comes from `runtime.dart`). The plan's named-delegate decision, `IReadOnlyList<object?>` aliasing, `ISet<long>` mutability surface, and `TryGetValue`-on-miss-returns-null decisions are co-dependent with the sibling plans — see §7.

## 6. Escalations

None.

## 7. Cycle Siblings

This file is a member of SCC cycle group 36 (6 files total). The conversion decisions in §2 are co-dependent with the 5 sibling files. Cross-references:

### lib/bytecode/runner.dart
**Co-dependence: load-bearing.** `runner.dart` implements the `Execute` opcode that constructs `SystemCall` instances, looks them up in `SystemPredicateRegistry`, invokes the `SystemPredicate` delegate, and dispatches on the `SystemResult` return value (`Success` → continue; `Failure` → try next clause; `Suspend` → park goal on `call.SuspendedReaders`). Co-dependent decisions:
- The `SystemPredicate` delegate signature `(GlpRuntime rt, SystemCall call) → SystemResult` MUST match `runner.dart`'s invocation site (`predicate(_runtime, call)`).
- `SystemCall.SuspendedReaders` MUST be `ISet<long>` (mutable surface) — `runner.dart` reads the post-invocation contents to enqueue the goal on the per-reader suspension lists; if exposed as `IReadOnlySet<long>`, the runner could not introspect the mutated set without a cast.
- `SystemPredicateRegistry.Lookup` MUST return `SystemPredicate?` nullable — `runner.dart` checks for null before invoking (treats missing predicate as goal failure, not exception).
- Atomicity: `runner.dart` invokes the predicate on the SAME thread that owns the heap (single-owning-context invariant from escalation #4); no cross-thread predicate dispatch.

### lib/multiagent/mad_context.dart
**Co-dependence: indirect.** `mad_context.dart` owns the per-agent execution context and the `Channel<IsolateMessage>` mailbox (escalation #5). System-predicate dispatch happens INSIDE the mad_context's owning Task — one predicate call per dequeued message, fully serialised. Co-dependent decisions:
- `SystemCall`/`SystemPredicateRegistry` field types remain plain `HashSet<long>`/`Dictionary<string, SystemPredicate>` (NOT `ConcurrentDictionary`/`ConcurrentBag`) — atomicity at the agent boundary, not at the collection level. This faithfully mirrors mad_context's discipline of plain Dart collections inside the single-owning-context.
- `SystemPredicate` delegate invocations are synchronous (`SystemResult` returned immediately); no `Task<SystemResult>` / `ValueTask<SystemResult>` — the mad_context's await-foreach loop processes one message-and-its-predicates fully before pulling the next.

### lib/runtime/body_kernels.dart
**Co-dependence: shape parallel.** body_kernels.dart already ratified the parallel structure: `BodyKernel` delegate (parallels `SystemPredicate`), `BodyKernelResult` enum (parallels `SystemResult`), `BodyKernelRegistry` class (parallels `SystemPredicateRegistry`). Co-dependent decisions:
- Both registries use `readonly Dictionary<string, TDelegate>` with inline initialiser, `TryGetValue` for nullable lookup, `ContainsKey` for `Has`, and `IEnumerable<string> Names => _predicates.Keys`. **Differentiation**: body_kernels' registry key is `$"{name}/{arity}"` (composite); system_predicates' registry key is the bare name (arity validated inside each predicate against `call.Args.Count`).
- Both delegates are NAMED `public delegate` declarations (NOT `Func<,,>` structural aliases) — preserves diagnostic identity at lookup-failure sites.
- Both `*Result` enums are plain payload-free `public enum` with PascalCased tags.

### lib/runtime/glp_activation.dart
**Co-dependence: type reference.** `glp_activation.dart` represents the activation record (current goal, current clause, current PC) that the runner uses to thread `GlpRuntime` state through Execute-opcode dispatch. Co-dependent decisions:
- `SystemPredicate`'s first parameter `GlpRuntime rt` is the runtime façade — `glp_activation.dart`'s activation record holds a reference to the same `GlpRuntime`; whatever `GlpRuntime` exposes (heap accessors, `BindWriter`, suspension enqueue) is the surface system predicates may call. Plan §2's "`ref GlpRuntime`" rejection (over-translation) is consistent with glp_activation's reference-typing of the same instance.
- `SystemCall` reference-identity (NOT value-equal) is consistent with `glp_activation`'s goal/clause reference-identity discipline.

### lib/runtime/runtime.dart
**Co-dependence: import target.** `runtime.dart` declares `GlpRuntime` — the runtime façade type referenced by the `SystemPredicate` delegate's first parameter and (transitively) by every predicate implementation. Co-dependent decisions:
- The Dart `import 'runtime.dart';` collapses (with `terms.dart`) into a single C# `using <root>.Runtime;` — `runtime.cs` and `system_predicates.cs` both target the same namespace `<root>.Runtime`. No cross-namespace using directive is needed.
- `runtime.dart` owns `bindWriter` (the side-effect surface system predicates use to bind writers on suspend); this plan's delegate signature MUST not constrain that surface (`SystemPredicate` returns `SystemResult` synchronously; bindWriter is invoked from inside the predicate body against the captured `rt` reference, NOT via the return value).
- `SystemPredicateRegistry` is held by ONE `GlpRuntime` instance (per the convspec's reference-identity discipline); two `GlpRuntime` instances each own their own registry. Reference-type `class` (NOT record/struct) for both `SystemPredicateRegistry` and `SystemCall` is the only render consistent with `runtime.dart`'s single-runtime-per-VM convention.
