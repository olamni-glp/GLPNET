# Conversion Spec — lib/runtime/runtime.dart

> Conversion-spec artifact for lib/runtime/runtime.dart (FR-011).
> Spec-only (FR-023): describes the Dart→C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> `GlpRuntime` is the **runtime facade**: a single mutable, identity-
> equal reference object that aggregates the per-runtime collaborators
> (HeapFCP, GoalQueue, SystemPredicateRegistry, BodyKernelRegistry),
> the per-goal mutable tables (budgets, env, program, module context,
> wait-readers), the cross-cutting suspension index (reader → goals),
> the OS-resource handle tables (`dart:io` `RandomAccessFile`, `dart:ffi`
> `DynamicLibrary`), and a small set of injection seams (madGLP
> context, output callback). It exposes thin pass-through methods that
> delegate writer-commit to `CommitOps.applySigmaHatFCP` (commit.dart),
> goal-suspension to `SuspendOps.suspendGoalFCP` (suspend_ops.dart),
> and tail-recursion budgeting to fairness.dart's
> `nextTailBudget`/`resetTailBudget`/`tailRecursionBudgetInit`.
>
> Heavy idiom reuse: every cross-file dependency (`HeapFCP`,
> `GoalQueue`, `GoalRef`, `GoalId`, `Pc`, `SystemPredicateRegistry`,
> `BodyKernelRegistry`, `CallEnv`, `BytecodeRunner`, `GlpChannelHandle`,
> the fairness constants/functions, the commit/suspend op-helper
> facades) inherits the convspec decisions already pinned in
> heap_fcp.dart.md, machine_state.dart.md, fairness.dart.md,
> commit.dart.md, suspend_ops.dart.md, body_kernels.dart.md,
> external_io.dart.md, and the corresponding `lib/bytecode/runner`,
> `lib/runtime/glp_activation`, `lib/runtime/system_predicates` specs.
> The threading-model decision (single-owning-context per goal /
> non-concurrent collections) is inherited from those upstream specs —
> NOT re-escalated here (FR-013: don't double-escalate a previously-
> escalated decision).
>
> Load-bearing nuances exercised by THIS file: (a) Dart `class
> GlpRuntime` with mostly-final reference fields PLUS plain-int
> counters and small mutable maps — IDENTITY equality is load-bearing
> (the runtime instance is passed by reference across the scheduler /
> runner / body kernels / system predicates / activation layers and
> mutated in place); MUST be a C# reference `class` (NEVER `record`,
> NEVER `struct`) — carry-forward of the identity-equality discipline
> from machine_state.dart.md `rf-dart-mutable-state-class-identity-
> equality-to-csharp-class`. (b) Dart `Map<int, …>` / `Map<int, Set<…>>`
> / `Map<Object?, …>` / `Map<String, …>` instance fields with
> heterogeneous key/value typing (and one `Object?`-keyed table) →
> C# `Dictionary<…>` carry-forward (idiom `rf-dart-map-to-csharp-
> dictionary` from machine_state.dart.md / suspension.dart.md /
> body_kernels.dart.md), with the `Object?`-keyed `runners` map
> requiring an explicit `Dictionary<object?, BytecodeRunner>` mapping
> AND a custom equality comparer note for the null-key case (load-
> bearing because `Dictionary<TKey, TValue>` rejects a literal `null`
> key by default, while Dart `Map<Object?, …>` accepts `null` as a
> bona-fide key). (c) Dart `Map<int, Set<GoalRef>>` with the
> `putIfAbsent(…, () => <GoalRef>{}).add(goalRef)` idiom → C# tooling
> equivalent: explicit `if (!dict.TryGetValue(k, out var set)) { set =
> new HashSet<GoalRef>(); dict[k] = set; } set.Add(goalRef);` OR the
> `CollectionsMarshal.GetValueRefOrAddDefault` shape (the spec picks
> the explicit TryGetValue form for portability and Dart-shape
> fidelity; NEW load-bearing idiom `rf-dart-putifabsent-set-add-to-
> csharp-tryget-add`). (d) Dart `int` instance counters used as
> handle/ID generators (`nextGoalId = 10000`, `_nextFileHandle = 1`,
> `_nextLibraryHandle = 1`) — pre-increment / post-increment is via
> `++` and the Dart `++` operator on a non-final integer field has
> identical semantics in C# (`_field++` value-is-pre-increment-value
> in expression context); the `nextGoalId` field is public-mutable
> (NOT a property with `private set`) because runner.dart and
> agent_runtime.dart bump it directly — carry-forward of "public
> mutable counter field" from external_io.dart.md `InputInjector
> ._currentWriterId` discipline but here EXPOSED (not encapsulated)
> at the source's request. (e) Dart `dart:io` `RandomAccessFile` →
> .NET `System.IO.FileStream` (NEW load-bearing idiom
> `rf-dart-randomaccessfile-to-csharp-filestream`) — the well-known
> `dart:io`→`System.IO` nuance IS exercised here (file-handle table
> + `closeSync` on shutdown) and MUST be addressed explicitly. (f)
> Dart `dart:ffi` `DynamicLibrary` → .NET `System.Reflection
> .NativeLibrary` (NEW load-bearing idiom `rf-dart-ffi-dynamiclibrary-
> to-csharp-nativelibrary`) — the well-known `dart:ffi`→.NET-FFI
> nuance IS exercised here (load-by-path + handle table + no-op
> close because `DynamicLibrary` exposes no close method in Dart;
> `NativeLibrary.Free(IntPtr)` is the .NET counterpart but
> "no-op close" is preserved verbatim per the source). (g) Dart
> `try { … } catch (e) { /* Ignore close errors */ }` swallow-all
> exception handling → C# `try { … } catch (Exception) { /* ignore */ }`
> with NRT-aware catch-all clause (NEW carry-forward idiom
> `rf-dart-catch-bare-to-csharp-catch-exception`). (h) Dart
> `void Function(String)? outputCallback;` → C# `Action<string>?
> OutputCallback { get; set; }` — a NULLABLE delegate property with
> set-from-caller semantics (carry-forward from repl_play_runner.dart
> .md `rf-dart-void-function-question-to-csharp-action-nullable`),
> distinct from the NON-nullable callbacks in external_io.dart.md.
> (i) Dart named-optional constructor with **field-typed defaults
> via `??`** (`heap: heap ?? HeapFCP()`, etc.) → C# constructor
> with nullable parameters and `??` null-coalesce body assignments
> (carry-forward from machine_state.dart.md `GoalState` ctor pattern
> AND external_io.dart.md `AgentIOContext` default-empty-map idiom).
> (j) Dart `static BodyKernelRegistry _createDefaultBodyKernels()`
> private static factory invoked from the constructor's
> initialiser-list default → C# `private static
> BodyKernelRegistry CreateDefaultBodyKernels()` invoked from
> constructor body; preserves the seed-with-standard-kernels
> contract. (k) Dart `throw UnimplementedError('…')` on the two
> deprecated legacy methods `commitWriters` / `abandonWriter` → C#
> `throw new NotImplementedException("…")` (NEW carry-forward
> idiom `rf-dart-unimplementederror-to-csharp-notimplementedexception`).
> (l) Dart `Object?` typed instance field (`madContext`) → C#
> `object? MadContext { get; set; }` — nullable reference type
> property with caller-driven init (the multiagent layer assigns
> it post-construction; same shape as the deferred-assignment
> seam but WITHOUT the `late final` write-once discipline because
> the source declares `Object? madContext;` plainly). (m) Dart
> nullable-return predicates (`bool? checkWaitState(int goalId)`,
> `int? getWaitReader(int goalId)`, `CallEnv? getGoalEnv`,
> `RandomAccessFile? getFile`, `ffi.DynamicLibrary? getLibrary`)
> → C# `bool?` / `int?` / `CallEnv?` / `FileStream?` /
> `NativeLibraryHandle?` (idiom carry-forward from goal_queue.dart
> .md `rf-dart-nullable-return-to-csharp-nullable-return`). (n)
> Triple-slash doc comments (`///`) → C# XML-doc comments (`///`)
> mechanically; `library;` directive (absent here) is N/A; the
> file uses ten relative imports + two `package:` imports with
> `show` clauses — the LATTER load-bearing because Dart `show`
> has no per-symbol C# counterpart (carry-forward escalation-free
> decision from heap_fcp.dart.md `rf-dart-import-relative-to-
> csharp-using-namespace` plus a NEW nuance note for the two
> `package:glp_runtime/bytecode/runner.dart show CallEnv,
> BytecodeRunner` and `package:glp_runtime/runtime/
> glp_activation.dart show GlpChannelHandle` imports — both
> become a single `using <root>.Bytecode;` / `using <root>.
> Runtime;` without the per-symbol filter).

```yaml
schema_version: 1
source_path: lib/runtime/runtime.dart
source_sha256: cb25bc0bcb2f6d07603fb9cba8c8b81802e016af0d8558cff28df8f8c3a470c3
target_code_unit: lib/runtime/runtime.cs
constructs:
  - construct_key: dart.import_directive.dart_core_library.dart_io
    source_form: >-
      "import 'dart:io';" — imports the dart:io core library; this file
      references `RandomAccessFile` (the file-handle map value type and the
      parameter type of `allocateFileHandle` + `closeSync()` call sites).
      No `show` clause — the full public surface of `dart:io` is in scope
      but only `RandomAccessFile` is used.
    target_decision: >-
      The Dart-core `dart:io` import maps to a C# `using System.IO;`
      directive (the .NET counterpart for OS-file APIs). The actual port
      of `RandomAccessFile` is decided in a separate construct below
      (`rf-dart-randomaccessfile-to-csharp-filestream`); the import-line
      decision here is purely: emit `using System.IO;` at the top of
      `runtime.cs`. Do NOT emit a per-symbol `using static System.IO
      .RandomAccessFile`-style filter — the Dart `import` here has no
      `show`-list and the .NET counterpart is the bare `using` of the
      whole namespace.
    idiom_id: null
    research_finding_id: rf-dart-import-dartio-to-csharp-using-systemio
    nuance: >-
      Compilation-unit nuance: Dart imports a *library*; C# imports a
      *namespace*. The 1:1 mapping is "the dart:io library → `using
      System.IO;`". No `show`/`hide` clause is present (full library
      surface available). The .NET counterpart of `dart:io` is split
      across multiple System.* namespaces (System.IO for file APIs,
      System.Console for stdin/stdout, System.Diagnostics for Process,
      System.Net for sockets) — for THIS file the only used dart:io
      type is `RandomAccessFile`, so a single `using System.IO;` is the
      faithful minimum. Value-vs-reference / null-safety / async /
      Stream / isolate: NOT APPLICABLE — a directive declares no
      values/types and has no runtime form.

  - construct_key: dart.import_directive.dart_ffi_library_aliased_as_ffi
    source_form: >-
      "import 'dart:ffi' as ffi;" — imports the dart:ffi core library
      with the prefix alias `ffi`; the file references `ffi.DynamicLibrary`
      (the type of the values stored in `_libraries` and returned from
      `getLibrary`) and `ffi.DynamicLibrary.open(path)` (a static factory
      invoked from `loadLibrary`). The `as ffi` prefix is LOAD-BEARING
      because it disambiguates `ffi.DynamicLibrary` from any local
      `DynamicLibrary` symbol.
    target_decision: >-
      The Dart-core `dart:ffi` import maps to a C# `using` directive (or
      `using <Alias> = …` alias) targeting the .NET namespace hosting
      the chosen `DynamicLibrary` counterpart. The actual port of
      `DynamicLibrary` is decided in a separate construct below
      (`rf-dart-ffi-dynamiclibrary-to-csharp-nativelibrary`) — the .NET
      counterpart of `dart:ffi.DynamicLibrary` is `System.Runtime
      .InteropServices.NativeLibrary` (a static helper, not a type)
      plus an `IntPtr` handle value; the .NET .NET-canonical
      counterpart of "a typed library handle" is `IntPtr` (or `nint`
      under modern C#) returned from `NativeLibrary.Load(…)`. The
      import-line decision here is therefore: emit `using System
      .Runtime.InteropServices;`. The Dart `as ffi` prefix has no
      direct C# counterpart at the import line (C# `using <Alias> =
      <Namespace>;` works for namespace aliases but the prefix is
      typically dropped because the .NET API surface uses unambiguous
      type names). Codegen MAY emit `using NativeLib = System.Runtime
      .InteropServices.NativeLibrary;` as a stylistic alias if it
      helps round-trip review; the SPEC records the canonical
      `using System.Runtime.InteropServices;` as the minimum.
    idiom_id: null
    research_finding_id: rf-dart-import-dartffi-to-csharp-using-interopservices
    nuance: >-
      Import-prefix nuance: Dart `import '…' as prefix;` introduces a
      qualified scope; C# `using <Alias> = <Namespace-or-Type>;`
      introduces a similar alias but it is OPTIONAL — the more
      idiomatic C# form is unqualified `using <Namespace>;`. The Dart
      prefix `ffi.` thus collapses to bare `NativeLibrary` /
      `IntPtr` references in C#. Compilation-unit nuance: the
      `dart:ffi` library has no single .NET-namespace 1:1 mapping;
      `System.Runtime.InteropServices` is the closest semantic
      counterpart for the load/lookup/free surface used here.
      Value-vs-reference / null-safety / async: NOT APPLICABLE at
      the import line.

  - construct_key: dart.import_directive.relative_same_package_runtime_siblings
    source_form: >-
      Eight relative imports of sibling `lib/runtime/*.dart` files:
      `'machine_state.dart'` (HeapFCP-adjacent types: GoalId, Pc,
      GoalRef, GoalQueue, GoalStatus, SigmaHat), `'heap_fcp.dart'`
      (HeapFCP itself + isFullyBound), `'suspend_ops.dart'`
      (SuspendOps facade), `'commit.dart'` (CommitOps facade),
      `'abandon.dart'` (abandon helpers — referenced via import-
      side-effect only in this file; no direct symbol), `'fairness.dart'`
      (`tailRecursionBudgetInit`, `nextTailBudget`, `resetTailBudget`),
      `'system_predicates.dart'` (SystemPredicateRegistry),
      `'body_kernels.dart'` (BodyKernelRegistry,
      registerStandardBodyKernels). All imports are bare (no
      `show`/`hide` clause).
    target_decision: >-
      Each Dart relative import becomes a .NET `using` directive
      targeting the namespace of the corresponding converted file:
      `using <root>.Runtime;` covers ALL EIGHT sibling-file imports
      because every one of them targets `lib/runtime/` in the source
      and therefore the same `<root>.Runtime` namespace in the
      target. No `show`-style per-symbol filter applies (Dart source
      uses bare imports). Carry-forward of the idiom recorded in
      heap_fcp.dart.md / suspension.dart.md / external_io.dart.md /
      machine_state.dart.md / fairness.dart.md.
    idiom_id: null
    research_finding_id: rf-dart-import-relative-to-csharp-using-namespace
    nuance: >-
      Compilation-unit nuance: Dart resolves relative imports by file
      path; .NET resolves type references by namespace. Eight Dart
      `import` lines collapse to a SINGLE `using <root>.Runtime;`
      directive in the target. No `show`/`hide` per-symbol narrowing
      (none in source). Side-effect-only import (`'abandon.dart'`):
      Dart treats every import as both a name-introducer and a
      module-effect; .NET `using` is name-only — but since the C#
      port pulls abandon.cs into the same `<root>.Runtime` assembly,
      the side-effect (if any — none observable here) is preserved
      automatically by inclusion in the same assembly. FR-024
      cache hit; no new research.

  - construct_key: dart.import_directive.package_with_show_clause_bytecode_runner_and_glpchannelhandle
    source_form: >-
      Two `package:`-prefixed imports with `show` clauses:
      `import 'package:glp_runtime/bytecode/runner.dart' show CallEnv,
      BytecodeRunner;` (used as: `Map<Object?, BytecodeRunner>
      runners`, `Map<GoalId, CallEnv> _goalEnvs`, and the `CallEnv`
      parameter / return type on `setGoalEnv`/`getGoalEnv`),
      `import 'package:glp_runtime/runtime/glp_activation.dart' show
      GlpChannelHandle;` (used as: `Map<String, GlpChannelHandle>
      glpChannels`). Both imports use Dart `show` to narrow the
      imported surface to the named symbols.
    target_decision: >-
      Each Dart `package:` import becomes a C# `using` directive
      targeting the namespace of the corresponding converted package
      file. `package:glp_runtime/bytecode/runner.dart` → `using
      <root>.Bytecode;` (the same `<root>.Bytecode` namespace that
      hosts the converted `runner.cs`). `package:glp_runtime/runtime/
      glp_activation.dart` → already covered by `using <root>.Runtime;`
      (same namespace as the eight relative imports above). The Dart
      `show` per-symbol narrowing has NO faithful C# counterpart —
      `using <Namespace>;` imports the full public surface of the
      namespace, and C# has no per-symbol allow-list at the import
      level. The spec records "show clauses are dropped" as the
      established convention (carry-forward from heap_fcp.dart.md
      `rf-dart-import-show-clause-no-csharp-counterpart`). Codegen
      MUST NOT attempt to emit a `using static <Type>` pseudo-
      counterpart — that imports type *members*, not the type
      itself, and the Dart `show` here narrows TYPE imports.
    idiom_id: null
    research_finding_id: rf-dart-import-show-clause-no-csharp-counterpart
    nuance: >-
      Show-clause nuance (explicitly addressed): Dart `import '…'
      show A, B;` narrows the imported library's exposed surface
      to A and B at THIS compilation unit; C# has no per-symbol
      `using` narrowing. The faithful render is a bare `using
      <Namespace>;` — the per-symbol filter is dropped, which is
      a one-way coarsening (any *other* symbol from the same
      namespace becomes accessible in the target too). The Dart-
      side `show` was a code-hygiene affordance, not a load-bearing
      semantic; dropping it preserves observable behaviour and is
      the established convention. Compilation-unit nuance: two
      different `package:` imports here target two different
      namespaces (`<root>.Bytecode` and `<root>.Runtime`); the
      latter coincides with the relative-imports' namespace, so
      the deduplicated `using` set is `{ <root>.Bytecode, <root>
      .Runtime, System.IO, System.Runtime.InteropServices }`.
      Value-vs-reference / null-safety / async: NOT APPLICABLE.

  - construct_key: dart.mutable_state_class.identity_equality.runtime_facade_aggregate
    source_form: >-
      `class GlpRuntime { final HeapFCP heap; final GoalQueue gq; final
      SystemPredicateRegistry systemPredicates; final BodyKernelRegistry
      bodyKernels; final Map<Object?, BytecodeRunner> runners = {};
      final Map<String, GlpChannelHandle> glpChannels = {}; final
      Map<GoalId, int> _budgets = <GoalId, int>{}; final Map<GoalId,
      CallEnv> _goalEnvs = <GoalId, CallEnv>{}; final Map<GoalId,
      Object?> _goalPrograms = <GoalId, Object?>{}; final Map<GoalId,
      Object?> _goalModuleContexts = <GoalId, Object?>{}; final
      Map<int, RandomAccessFile> _fileHandles = <int, RandomAccessFile>{};
      int _nextFileHandle = 1; final Map<int, ffi.DynamicLibrary>
      _libraries = <int, ffi.DynamicLibrary>{}; int _nextLibraryHandle =
      1; int nextGoalId = 10000; int _pendingTimers = 0; int get
      pendingTimers => _pendingTimers; void incrementPendingTimers()
      => _pendingTimers++; void decrementPendingTimers() =>
      _pendingTimers--; final Map<int, int> _waitReaders = <int, int>{};
      final Map<int, Set<GoalRef>> suspended = <int, Set<GoalRef>>{};
      final Set<int> infrastructureGoalIds = {}; Object? madContext;
      void Function(String)? outputCallback; GlpRuntime({HeapFCP?
      heap, GoalQueue? gq, SystemPredicateRegistry? systemPredicates,
      BodyKernelRegistry? bodyKernels}) : heap = heap ?? HeapFCP(),
      gq = gq ?? GoalQueue(), systemPredicates = systemPredicates ??
      SystemPredicateRegistry(), bodyKernels = bodyKernels ??
      _createDefaultBodyKernels(); /* ... methods ... */ }`. No
      `==`/`hashCode` override (identity equality). No `toString`
      override. Mutation happens via direct field assignment AND
      via the helper methods (`commitSigmaHat`, `suspendGoalFCP`,
      `tailReduce`, `setGoalEnv`/`setGoalProgram`/`setGoalModuleContext`,
      `enqueueReactivatedGoal`, `allocateFileHandle`/`closeFileHandle`/
      `closeAllFiles`, `loadLibrary`/`closeLibrary`/`closeAllLibraries`).
    target_decision: >-
      Map to a reference-type .NET `class GlpRuntime` (NOT `record`,
      NOT `struct`, NOT `record class`). Justification mirrors the
      machine_state.dart.md `GoalState` analysis: (i) the runtime
      instance is stored by reference across the scheduler / runner
      / body-kernel / system-predicate / activation / multiagent
      layers; the runtime asks "is this the same runtime I started?"
      which is reference identity; value-equality would silently
      make two distinct runtimes compare equal if their fields
      coincide — a correctness bug at the runtime level. (ii) every
      field except the four constructor-injected references is
      mutated in place; `record`-synthesised equality on a mutable
      reference type is well-known to lead to hash-stability bugs
      (a `GlpRuntime` stored as a dictionary key would have its
      hash drift on every counter bump). (iii) the runtime
      aggregates many other reference types (HeapFCP, GoalQueue,
      registries, the maps); the outer being a reference type
      ensures the same nested instances are shared across all
      holders of the `GlpRuntime` reference — exactly the Dart
      semantics. Fields map as follows: the four constructor-
      injected references (`heap`, `gq`, `systemPredicates`,
      `bodyKernels`) → `public HeapFCP Heap { get; }` /
      `public GoalQueue Gq { get; }` / `public
      SystemPredicateRegistry SystemPredicates { get; }` /
      `public BodyKernelRegistry BodyKernels { get; }` — init-
      only via constructor (mirroring Dart `final`). The runners
      and glpChannels maps → `public Dictionary<object?,
      BytecodeRunner> Runners { get; } = new();` /
      `public Dictionary<string, GlpChannelHandle> GlpChannels
      { get; } = new();` (Dart `final` field of a mutable
      reference = "rebind-final, body-mutable"). The private
      maps `_budgets`, `_goalEnvs`, `_goalPrograms`,
      `_goalModuleContexts`, `_fileHandles`, `_libraries`,
      `_waitReaders` → `private readonly Dictionary<…,…> _x = new();`
      with the appropriate generic-argument port (`GoalId` → `int`
      per machine_state.dart.md aliases; `Object?` → `object?`).
      The public `suspended` field → `public Dictionary<int,
      HashSet<GoalRef>> Suspended { get; } = new();` (PUBLIC
      because the source declares it without `_` prefix and
      external code reads it; mutation goes through the
      methods). `infrastructureGoalIds` → `public HashSet<int>
      InfrastructureGoalIds { get; } = new();`. The plain-int
      counters: `_nextFileHandle` and `_nextLibraryHandle` →
      `private int _nextFileHandle = 1;` /
      `private int _nextLibraryHandle = 1;` (NO public accessor
      — the source has none). `nextGoalId = 10000` → `public
      int NextGoalId = 10000;` (public mutable INSTANCE FIELD,
      NOT a property — see nuance below; the Dart source
      exposes it as a raw field and external code performs
      `runtime.nextGoalId++` and `runtime.nextGoalId = …`). The
      `_pendingTimers` field + read-only getter + increment /
      decrement methods → `private int _pendingTimers = 0;
      public int PendingTimers => _pendingTimers; public void
      IncrementPendingTimers() => _pendingTimers++; public void
      DecrementPendingTimers() => _pendingTimers--;` (preserving
      the encapsulation discipline the Dart source exhibits for
      this counter). `madContext` (`Object? madContext;`) →
      `public object? MadContext { get; set; }` — a NULLABLE
      reference type property with caller-driven post-
      construction assignment. `outputCallback` (`void
      Function(String)? outputCallback;`) → `public Action<string>?
      OutputCallback { get; set; }` — a NULLABLE delegate
      property (idiom carry-forward from repl_play_runner
      .dart.md `rf-dart-void-function-question-to-csharp-action-
      nullable`). Constructor: a single C# constructor with
      four nullable parameters and `??`-bodied defaults:
      `public GlpRuntime(HeapFCP? heap = null, GoalQueue? gq =
      null, SystemPredicateRegistry? systemPredicates = null,
      BodyKernelRegistry? bodyKernels = null) { Heap = heap ??
      new HeapFCP(); Gq = gq ?? new GoalQueue(); SystemPredicates
      = systemPredicates ?? new SystemPredicateRegistry();
      BodyKernels = bodyKernels ?? CreateDefaultBodyKernels(); }`
      — mirroring the Dart named-optional ctor with `??` null-
      coalesce defaults (carry-forward from machine_state.dart.md
      `GoalState` ctor pattern).
    idiom_id: null
    research_finding_id: rf-dart-mutable-state-class-identity-equality-to-csharp-class
    nuance: >-
      VALUE-VS-REFERENCE / IDENTITY — the dominant load-bearing
      nuance for this construct (carry-forward from
      machine_state.dart.md). Dart `class GlpRuntime` uses default
      identity equality (no `==` override). The .NET counterpart
      MUST be `class` (reference type), NEVER `record`/`record
      class`/`record struct`/`struct`. Init-only-vs-set nuance:
      Dart `final` fields become C# `{ get; }` (init-only via
      constructor) — the conservative form, NOT `{ get; init; }`
      (`with`-expressions on a mutable identity-equal aggregate
      would be misleading). PUBLIC-FIELD-vs-PROPERTY nuance
      (LOAD-BEARING and DIFFERENTIATING): `nextGoalId` is a
      PUBLIC MUTABLE INSTANCE FIELD in the Dart source
      (declared `int nextGoalId = 10000;` without `_` prefix);
      external code performs `runtime.nextGoalId++` directly.
      The faithful C# render is `public int NextGoalId = 10000;`
      (a public mutable field, NOT a `{ get; set; }` property);
      `++` on a public field has identical semantics in both
      languages. A property with `{ get; set; }` would also
      compile but would change the LValue surface in subtle
      ways (`runtime.NextGoalId++` on a property invokes
      `get_NextGoalId` + add + `set_NextGoalId`, three calls
      instead of a single field-load+store). For a hot-path
      goal-id allocator the field form is the .NET-canonical
      counterpart of "raw mutable integer counter" (see
      Microsoft Learn: "Fields" — "Generally, you should use
      fields only for variables that have private or protected
      accessibility. … However, fields are sometimes necessary
      for public access for performance reasons" — and the
      surrounding scheduler hot path qualifies). Default-field-
      init nuance: Dart `final Map<…,…> _x = <…,…>{}` (instance
      field initialiser) → C# `private readonly Dictionary<…,…>
      _x = new();` (target-typed `new()`, .NET 9+; on older
      targets `= new Dictionary<…,…>();`). The "rebind-final,
      body-mutable" semantics are preserved verbatim. Map-with-
      nullable-object-key nuance (LOAD-BEARING and SPECIFIC TO
      `runners`): the Dart `Map<Object?, BytecodeRunner> runners`
      has key type `Object?` — Dart `Map` accepts `null` as a
      bona-fide key; .NET `Dictionary<TKey, TValue>` REJECTS a
      literal `null` key with `ArgumentNullException` even when
      `TKey` is a reference type. The faithful render is
      `Dictionary<object?, BytecodeRunner>` (the type system
      under enabled NRT allows `object?` as a generic type
      argument), BUT consumers MUST NOT pass `null` as a key
      — a code-review obligation, NOT a compile-time guarantee.
      If the source ever stores a runner under the null key
      (current evidence from runner.dart and body_kernels.dart
      is that the key is always non-null — typically a
      `BytecodeProgram` reference), the faithful render would
      need a custom `IEqualityComparer<object?>` that maps
      `null` to a sentinel — but this is NOT specified here
      because the source's load-bearing invariant matches the
      .NET default. ESCALATION CANDIDATE: if a downstream
      survey of `runners[…]` keys finds a `null` key write
      site, this construct MUST be re-spec'd with the
      sentinel-comparer; the SPEC records this as a code-
      review obligation. Null-safety nuance: every Dart `?`-
      typed field (`madContext`, `outputCallback`) and every
      `Object?`-typed map value (`_goalPrograms`,
      `_goalModuleContexts`, `runners` key) preserves
      nullability under enabled NRT. `final Map<int,
      Set<GoalRef>> suspended` non-nullable value type →
      C# `Dictionary<int, HashSet<GoalRef>>` non-nullable
      (the inner `HashSet<GoalRef>` is always created
      eagerly by the `TryGetValue + putIfAbsent` idiom). Set-
      vs-HashSet nuance: Dart `Set<T>` literal `<GoalRef>{}`
      defaults to a `LinkedHashSet<T>` (insertion-ordered);
      `HashSet<T>` in .NET is unordered. For THIS file the
      iteration order over `suspended[reader]` does not
      affect observable runtime behaviour (the consumers
      enqueue every member to `gq`, and the queue itself
      defines ordering via FIFO insertion order at the
      `gq` level). The faithful render is `HashSet<GoalRef>`
      — unordered, O(1) add/remove/contains. If a future
      consumer ever requires insertion-ordered iteration
      over the suspended-set, this construct MUST be re-
      spec'd with `LinkedHashSet`-like wrapper; the SPEC
      records this as a code-review obligation. Async /
      Stream / Mixin / Sealed: ABSENT — no async surface,
      no streams, no isolates, no mixin/sealed declarations
      on this class.

  - construct_key: dart.constructor.named_optional_with_null_coalesce_defaults_static_factory_seed
    source_form: >-
      "GlpRuntime({HeapFCP? heap, GoalQueue? gq, SystemPredicateRegistry?
      systemPredicates, BodyKernelRegistry? bodyKernels}) : heap = heap
      ?? HeapFCP(), gq = gq ?? GoalQueue(), systemPredicates =
      systemPredicates ?? SystemPredicateRegistry(), bodyKernels =
      bodyKernels ?? _createDefaultBodyKernels();" — a Dart named-only
      constructor with FOUR optional parameters (no `required`), each
      defaulting to a freshly-allocated instance via `??` in the
      initialiser list. The `bodyKernels` slot uses the private static
      factory `_createDefaultBodyKernels()` (NOT `BodyKernelRegistry()`
      directly) because the factory seeds the registry with
      `registerStandardBodyKernels(registry)` (see body_kernels.dart
      for the standard kernel set).
    target_decision: >-
      Map to a single C# positional-parameter constructor with FOUR
      nullable parameters and `??`-bodied defaults (NOT initialiser-
      list — C# has no Dart-style initialiser list outside the
      chained `: base(…)` / `: this(…)` form). Signature:
      `public GlpRuntime(HeapFCP? heap = null, GoalQueue? gq = null,
      SystemPredicateRegistry? systemPredicates = null,
      BodyKernelRegistry? bodyKernels = null)`. Body: explicit
      property assignment with `??` null-coalesce: `Heap = heap ?? new
      HeapFCP(); Gq = gq ?? new GoalQueue(); SystemPredicates =
      systemPredicates ?? new SystemPredicateRegistry(); BodyKernels =
      bodyKernels ?? CreateDefaultBodyKernels();`. Carry-forward of
      machine_state.dart.md `rf-dart-named-required-ctor-with-defaults-
      to-csharp-positional-ctor-with-defaults` (the only difference is
      that here NONE of the parameters are `required` in the Dart
      source, so ALL get `= null` defaults in the C# signature).
    idiom_id: null
    research_finding_id: rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults
    nuance: >-
      Named-vs-positional nuance: Dart named-only constructor maps
      to C# positional parameters with named-argument syntax at the
      call site (`new GlpRuntime(heap: customHeap)` works in C#
      identically to Dart). Default-fresh-instance idiom: Dart
      `?? HeapFCP()` allocates a FRESH `HeapFCP` per call when the
      caller omits the parameter — `??` is short-circuiting, so no
      allocation when a non-null is supplied. C# `?? new HeapFCP()`
      preserves the same short-circuiting allocation semantics.
      Default-collection-aliasing pitfall: Dart's `?? <K,V>{}`
      default-empty-map idiom (avoids the textbook pitfall of
      sharing a single mutable default across calls) is mirrored
      here by `?? new HeapFCP()` etc.; same fix applies. Static-
      factory-seeded nuance: `_createDefaultBodyKernels()` (private
      static helper) maps to `CreateDefaultBodyKernels()` (private
      static helper on the same `GlpRuntime` class). Codegen MUST
      NOT inline the factory body at the call site — the named
      factory documents the "seeded with standard kernels"
      contract. Value-vs-reference / null-safety / async: addressed
      by the field-level constructs above (constructor only
      assigns).

  - construct_key: dart.private_static_factory_method_returns_seeded_registry
    source_form: >-
      "static BodyKernelRegistry _createDefaultBodyKernels() { final
      registry = BodyKernelRegistry(); registerStandardBodyKernels(
      registry); return registry; }" — a private (`_`-prefixed) static
      method that constructs an empty `BodyKernelRegistry`, seeds it
      with the standard kernels via the imported
      `registerStandardBodyKernels` top-level function (from
      `body_kernels.dart`), and returns the seeded registry.
    target_decision: >-
      Map to a `private static BodyKernelRegistry
      CreateDefaultBodyKernels()` method on the `GlpRuntime` class.
      Body: `var registry = new BodyKernelRegistry();
      BodyKernels.RegisterStandardBodyKernels(registry); return
      registry;` — the static helper `registerStandardBodyKernels`
      from `body_kernels.dart` is, in the converted target, a
      `public static` method on the `BodyKernels` static class (per
      body_kernels.dart.md `rf-dart-top-level-fn-builds-sum-type-leaf`
      / `rf-dart-static-only-holder-to-csharp-static-class`). The
      qualifier `BodyKernels.RegisterStandardBodyKernels(registry)`
      reaches it via the hosting static class. NOT an instance
      method, NOT a constructor — the Dart source uses `static`
      explicitly because the helper is invoked from the constructor's
      initialiser-list default and there is no `this` available at
      that point.
    idiom_id: null
    research_finding_id: rf-dart-private-static-factory-seed-to-csharp-private-static
    nuance: >-
      Static-method nuance: Dart `static` on a class member is the
      same concept as C# `static` — a class-level (NOT instance-
      level) method. Naming nuance: Dart `_`-prefixed methods map
      to C# `private` access (carry-forward from convspec corpus).
      Method-naming-case nuance: Dart `_createDefaultBodyKernels`
      → C# `CreateDefaultBodyKernels` (PascalCase per .NET
      capitalisation guidelines; the leading `_` is dropped because
      C# encodes access via `private` keyword, not via name prefix).
      Value-vs-reference: the returned `BodyKernelRegistry` is a
      reference type — the factory returns the fresh instance by
      reference (same as Dart). Side-effect-on-construction
      nuance: `registerStandardBodyKernels(registry)` mutates the
      registry in place (a side-effecting call). The C# port
      preserves the mutation-then-return pattern verbatim. Null-
      safety: non-nullable return type, non-null fresh instance.
      Async: ABSENT.

  - construct_key: dart.method.thin_pass_through_delegate_to_static_op_helper.commit_sigmahat
    source_form: >-
      "List<GoalRef> commitSigmaHat(Map<int, Object?> sigmaHat) { final
      acts = CommitOps.applySigmaHatFCP(heap: heap, sigmaHat: sigmaHat);
      _enqueueAll(acts); return acts; }" — a pass-through method that
      delegates writer-commit to the `CommitOps.applySigmaHatFCP` static
      facade (commit.dart), then enqueues every reactivated goal-ref
      into the goal queue via the private `_enqueueAll` helper, then
      returns the list to the caller. The parameter type `Map<int,
      Object?>` matches the `SigmaHat` typedef from machine_state.dart
      (`Map<WriterId, Object?>` after typedef-resolution).
    target_decision: >-
      Map to `public IReadOnlyList<GoalRef> CommitSigmaHat(SigmaHat
      sigmaHat) { var acts = CommitOps.ApplySigmaHatFCP(heap: Heap,
      sigmaHat: sigmaHat); EnqueueAll(acts); return acts; }`. The
      parameter type uses the converted `SigmaHat` alias (per
      machine_state.dart.md `rf-dart-map-to-csharp-dictionary` plus
      the typedef-alias decision `global using SigmaHat = Dictionary
      <int, object?>;`). The return type follows the
      `boot_loader.dart.md` / `external_io.dart.md` convention of
      `IReadOnlyList<GoalRef>` for activation lists (records the
      immutability invariant the caller relies on — the caller
      iterates and enqueues without mutating). `CommitOps
      .ApplySigmaHatFCP` is the converted commit.cs facade; the
      named-argument form `heap: Heap, sigmaHat: sigmaHat` is
      faithful one-to-one (C# named args use identical syntax).
    idiom_id: null
    research_finding_id: rf-dart-method-passthrough-to-csharp-method-passthrough
    nuance: >-
      Pass-through-method nuance: Dart and C# both permit thin
      facades that delegate to a static helper. The conversion
      preserves the structure verbatim. Side-effect nuance:
      `_enqueueAll` mutates the goal queue in place — the
      `IReadOnlyList<GoalRef>` return type records that the
      caller treats the activation list as read-only AFTER the
      enqueue side-effect has already happened. Null-safety:
      the parameter `SigmaHat` is non-nullable (every call site
      supplies a non-null map); the return is non-nullable
      (every call returns a list — possibly empty). Async: ABSENT
      (the operation is synchronous, mirroring the Dart shape).

  - construct_key: dart.method.deprecated_legacy_throws_unimplementederror
    source_form: >-
      Two pass-through methods that throw `UnimplementedError`:
      `List<GoalRef> commitWriters(Iterable<int> writerIds) { throw
      UnimplementedError('Legacy commitWriters deprecated - use
      commitSigmaHat'); }` and `List<GoalRef> abandonWriter(int
      writerId) { throw UnimplementedError('Legacy abandonWriter
      deprecated - FCP has no abandon'); }`. Both carry doc comments
      tagging them deprecated with TODO removal directives.
    target_decision: >-
      Map each to a public method with `[Obsolete(…, true)]`
      attribute and `throw new NotImplementedException(…)` body. The
      `[Obsolete(message, error: true)]` attribute is the .NET
      canonical counterpart of Dart's `@deprecated` / TODO-removal
      pattern; the `error: true` flag emits a compile-time error
      on use (matching the Dart intent that no caller should
      remain). Signature: `[Obsolete("Legacy CommitWriters
      deprecated - use CommitSigmaHat", error: true)] public
      IReadOnlyList<GoalRef> CommitWriters(IEnumerable<int>
      writerIds) => throw new NotImplementedException("Legacy
      CommitWriters deprecated - use CommitSigmaHat");`. Same shape
      for `AbandonWriter(int writerId)`. The .NET counterpart of
      Dart `UnimplementedError` is `System.NotImplementedException`
      (Microsoft Learn: "NotImplementedException — The exception
      that is thrown when a requested method or operation is not
      implemented"). The exception message is preserved byte-
      identically (load-bearing for any test assertion that reads
      `e.Message`).
    idiom_id: null
    research_finding_id: rf-dart-unimplementederror-to-csharp-notimplementedexception
    nuance: >-
      Exception-type nuance (LOAD-BEARING, NEW idiom): Dart
      `UnimplementedError` (from `dart:core`) and .NET
      `System.NotImplementedException` are the canonical "this
      stub is intentionally unimplemented" exception types in
      their respective ecosystems. Both extend the standard
      exception hierarchy (Dart: `Error` → `UnimplementedError`;
      .NET: `Exception` → `SystemException` →
      `NotImplementedException`). Message preservation:
      byte-identical string preserved. Deprecation-marker nuance:
      Dart's `// TODO: Remove after …` doc-comment convention
      maps to `[Obsolete(message, error: true)]` in .NET — the
      attribute form gives compile-time enforcement that
      mirrors the Dart intent (a doc comment alone has no
      compile-time effect, but the `throw` at runtime does;
      `[Obsolete(..., error: true)]` adds the compile-time
      layer for stronger fidelity). Iterable-vs-IEnumerable
      nuance: Dart `Iterable<int>` → C# `IEnumerable<int>`
      (carry-forward from goal_queue.dart.md
      `rf-dart-iterable-to-csharp-ienumerable`). Async: ABSENT.

  - construct_key: dart.method.suspend_goal_fcp.putifabsent_set_add_then_delegate
    source_form: >-
      "void suspendGoalFCP({required int goalId, required int kappa,
      required Set<int> readerVarIds}) { final goalRef = GoalRef(goalId,
      kappa); for (final readerId in readerVarIds) { suspended
      .putIfAbsent(readerId, () => <GoalRef>{}).add(goalRef); }
      SuspendOps.suspendGoalFCP(heap: heap, goalId: goalId, kappa:
      kappa, readerVarIds: readerVarIds); }" — combines (a) per-reader
      bookkeeping into the cross-cutting `suspended` index (Map<int,
      Set<GoalRef>>) using the Dart `putIfAbsent(key, () =>
      defaultEmpty).add(value)` idiom, then (b) delegates the
      suspension-record write to the `SuspendOps.suspendGoalFCP`
      static facade (suspend_ops.dart).
    target_decision: >-
      Map to `public void SuspendGoalFCP(int goalId, int kappa,
      ISet<int> readerVarIds) { var goalRef = new GoalRef(goalId,
      kappa); foreach (var readerId in readerVarIds) { if (!Suspended
      .TryGetValue(readerId, out var set)) { set = new
      HashSet<GoalRef>(); Suspended[readerId] = set; } set
      .Add(goalRef); } SuspendOps.SuspendGoalFCP(heap: Heap, goalId:
      goalId, kappa: kappa, readerVarIds: readerVarIds); }`. The
      Dart `Map.putIfAbsent(k, () => defaultEmpty).add(v)` idiom
      maps to the C# `TryGetValue + new + Add` 3-line shape (the
      .NET-canonical "lazily create-empty-collection on first
      access" pattern, Microsoft Learn: "Dictionary<TKey,TValue>
      .TryGetValue(TKey, out TValue) — Gets the value associated
      with the specified key … Use TryGetValue method if your code
      frequently attempts to access keys that aren't in the
      dictionary"). Alternative `CollectionsMarshal
      .GetValueRefOrAddDefault` (.NET 6+) is rejected because it
      requires a struct-ref dance that obscures the Dart-shape
      fidelity. `GoalRef` is a `readonly record struct` per
      machine_state.dart.md (value-equal); `HashSet<GoalRef>`
      uses the record's synthesised `GetHashCode` / `Equals` —
      identical semantics to Dart's `Set<GoalRef>` with the
      Dart `==`/`hashCode` overrides.
    idiom_id: null
    research_finding_id: rf-dart-putifabsent-set-add-to-csharp-tryget-add
    nuance: >-
      PutIfAbsent-add idiom nuance (LOAD-BEARING, NEW idiom):
      Dart `Map.putIfAbsent(k, () => default).add(v)` is a
      well-known idiom for "lazily create a default-empty
      collection on first access, then add a value to it". .NET
      `Dictionary<TKey, TValue>` has NO single-call equivalent
      pre-.NET 6 (and the .NET 6+ `CollectionsMarshal
      .GetValueRefOrAddDefault` is a low-level escape hatch that
      requires explicit-ref bookkeeping). The faithful render is
      the explicit `TryGetValue + new + indexer-set + Add`
      sequence. Set-element-equality nuance (LOAD-BEARING):
      Dart `Set<GoalRef>` uses `==`/`hashCode` (Dart overrides
      → value equality on the two int fields); .NET
      `HashSet<GoalRef>` uses `EqualityComparer<GoalRef>
      .Default` which delegates to `GoalRef.Equals` / `GoalRef
      .GetHashCode` (synthesised by the `readonly record
      struct` per machine_state.dart.md). Both deliver
      identical "same id+pc → same set element" semantics — no
      escalation. Named-parameter nuance: Dart `required Set<int>
      readerVarIds` → C# `ISet<int> readerVarIds` (positional,
      no `required` keyword needed because all-required
      positional parameters in C# are the default).
      Set-vs-ISet parameter nuance: the source's `Set<int>`
      parameter is read-only at the call site (no mutation —
      only iteration); `ISet<int>` is the most general .NET
      counterpart that admits both `HashSet<int>` and
      `SortedSet<int>` callers (carry-forward from
      hanger.dart.md / suspend.dart.md set-parameter
      conventions). Side-effect-on-`Suspended`: the indexer-
      set `Suspended[readerId] = set;` writes back when the
      entry is freshly created (TryGetValue returned false);
      no write needed when found (the set was created on a
      previous call and the new `goalRef.Add` mutates it in
      place — same as Dart). Async: ABSENT.

  - construct_key: dart.method.tail_reduce.budget_state_with_fairness_helpers
    source_form: >-
      "bool tailReduce(GoalId g) { final current = _budgets[g] ??
      tailRecursionBudgetInit; final next = nextTailBudget(current); if
      (next == 0) { _budgets[g] = resetTailBudget(); return true; }
      else { _budgets[g] = next; return false; } }" — a per-goal
      tail-recursion budget step: read the budget (defaulting to
      `tailRecursionBudgetInit` if unseen), compute the next budget
      via fairness's `nextTailBudget`, then if exhausted reset and
      return `true` (yield), else store the decremented budget and
      return `false` (continue).
    target_decision: >-
      Map to `public bool TailReduce(int g) { var current = _budgets
      .TryGetValue(g, out var b) ? b : MachineStateConstants
      .TailRecursionBudgetInit; var next = Fairness
      .NextTailBudget(current); if (next == 0) { _budgets[g] =
      Fairness.ResetTailBudget(); return true; } else { _budgets[g]
      = next; return false; } }`. The Dart `Map[key] ?? default`
      pattern (read with default-on-miss) maps to .NET `TryGetValue
      + ternary` — the canonical .NET counterpart (Microsoft Learn
      pattern for "read with fallback"). The fairness constants and
      functions live on the converted `Fairness` static class per
      fairness.dart.md (`TailRecursionBudgetInit` lives on
      `MachineStateConstants` per machine_state.dart.md; the source
      `tailRecursionBudgetInit` import resolves to that constant).
      Carry-forward.
    idiom_id: null
    research_finding_id: rf-dart-map-index-null-coalesce-default-to-csharp-tryget-ternary
    nuance: >-
      Map-read-with-default nuance: Dart `m[k] ?? default` returns
      the value if present, else the default; .NET
      `Dictionary<TKey, TValue>` indexer THROWS
      `KeyNotFoundException` on miss, so the faithful render
      MUST use `TryGetValue + ternary` (NOT `_budgets[g] ??
      default` — that pattern is invalid C# for `Dictionary`
      because `Dictionary[]` returns a non-nullable value type
      that is never null). Carry-forward of the `TryGetValue +
      ternary` discipline from goal_queue.dart.md / suspend.dart
      .md. Value-vs-reference: `_budgets` is `Dictionary<int,
      int>` (value-type values); the indexer-set `_budgets[g] =
      next;` writes the integer by value. Null-safety: both
      `current` and `next` are non-nullable `int`. Async: ABSENT.

  - construct_key: dart.method.simple_getter_indexer_pair.budget_of
    source_form: >-
      "int budgetOf(GoalId g) => _budgets[g] ?? tailRecursionBudgetInit;"
      — a single-expression arrow-bodied method that reads the per-
      goal budget with a default fallback (identical pattern to
      `tailReduce` but read-only).
    target_decision: >-
      Map to `public int BudgetOf(int g) => _budgets.TryGetValue(g,
      out var b) ? b : MachineStateConstants.TailRecursionBudgetInit;`
      — expression-bodied method, identical Dart shape. Carry-
      forward.
    idiom_id: null
    research_finding_id: rf-dart-map-index-null-coalesce-default-to-csharp-tryget-ternary
    nuance: >-
      Same as `tailReduce`. Note expression-bodied form is
      preserved (C# expression-bodied member supports a single
      ternary expression in the body).

  - construct_key: dart.method_pair.setter_getter_for_goal_keyed_map.goal_env_program_module_context
    source_form: >-
      Three setter+getter method pairs over the per-goal maps:
      `void setGoalEnv(GoalId g, CallEnv env) { _goalEnvs[g] = env; }
      CallEnv? getGoalEnv(GoalId g) => _goalEnvs[g];` — setter writes,
      getter returns nullable (Dart `Map[k]` returns `V?`); similarly
      `setGoalProgram`/`getGoalProgram` over `_goalPrograms` and
      `setGoalModuleContext`/`getGoalModuleContext` over
      `_goalModuleContexts`.
    target_decision: >-
      Map each pair to a C# setter+getter method pair on the same
      class. Setter: `public void SetGoalEnv(int g, CallEnv env) =>
      _goalEnvs[g] = env;` (expression-bodied because the body is a
      single statement). Getter: `public CallEnv? GetGoalEnv(int g)
      => _goalEnvs.TryGetValue(g, out var env) ? env : null;` —
      faithful to the Dart `m[k]` returning `V?`. Same shape for
      `SetGoalProgram(int g, object? program)` / `GetGoalProgram(int
      g)` returning `object?`, and `SetGoalModuleContext(int g,
      object? ctx)` / `GetGoalModuleContext(int g)` returning
      `object?`. NOT a property — the source uses methods (not
      getters/setters) because the indexer key is a parameter; a
      property would need an indexer surface (`this[int g] { get;
      set; }`) which would conflate three distinct maps under a
      single indexer. The Dart shape preserves the three-method-
      pair surface verbatim.
    idiom_id: null
    research_finding_id: rf-dart-map-index-to-csharp-tryget-or-null-for-nullable-return
    nuance: >-
      Map-read-returning-nullable nuance: Dart `Map<K, V>[k]`
      returns `V?` (nullable, null on miss); the .NET
      `Dictionary<TKey, TValue>` indexer throws on miss. The
      faithful render of "Dart `m[k]` returns nullable on miss"
      is `TryGetValue + ternary or null`. For value-type V
      (e.g. `CallEnv` if it is a reference type, or
      `Dictionary<int, object?>` if V is `object?`), the
      return type is `V?` (nullable reference for ref types
      or `Nullable<V>` for value types). `CallEnv` is decided
      in the bytecode/runner spec to be a reference type;
      `Object?` → `object?`. Value-vs-reference: per-map.
      Null-safety: every getter returns nullable; every setter
      takes non-nullable (Dart `CallEnv env` parameter is non-
      nullable — no `?`). Async: ABSENT.

  - construct_key: dart.method.private_helper_iterates_and_enqueues
    source_form: >-
      "void _enqueueAll(List<GoalRef> acts) { for (final a in acts) {
      enqueueReactivatedGoal(a); } }" — iterates a list of `GoalRef`
      activations and enqueues each via the public
      `enqueueReactivatedGoal` (which both enqueues and cleans up the
      suspended index).
    target_decision: >-
      Map to `private void EnqueueAll(IReadOnlyList<GoalRef> acts) {
      foreach (var a in acts) { EnqueueReactivatedGoal(a); } }`. NOT
      expression-bodied (multi-statement-body `foreach`). The
      parameter type uses `IReadOnlyList<GoalRef>` to record the
      read-only-iteration invariant (same convention as
      `CommitSigmaHat` and other callers).
    idiom_id: null
    research_finding_id: rf-dart-method-passthrough-to-csharp-method-passthrough
    nuance: >-
      Private-helper nuance: Dart `_`-prefixed methods → C#
      `private` access. `foreach` loop semantics identical between
      Dart and C# (both lazy-iterate the source).

  - construct_key: dart.method.enqueue_then_remove_from_suspended_cross_cutting
    source_form: >-
      "void enqueueReactivatedGoal(GoalRef goal) { gq.enqueue(goal);
      _removeFromSuspended(goal); }" — a thin facade that enqueues a
      reactivated goal AND removes it from EVERY entry of the
      `suspended` index (the goal may have been suspended on multiple
      readers; reactivating drops it from all of them).
    target_decision: >-
      Map to `public void EnqueueReactivatedGoal(GoalRef goal) { Gq
      .Enqueue(goal); _RemoveFromSuspended(goal); }`. The doc comment
      "Use this instead of gq.enqueue() when reactivating suspended
      goals" is preserved as an XML-doc comment on the method.
    idiom_id: null
    research_finding_id: rf-dart-method-passthrough-to-csharp-method-passthrough
    nuance: >-
      Pass-through method. Same as `CommitSigmaHat`.

  - construct_key: dart.method.private_iterate_and_mutate_with_collected_keys_pattern
    source_form: >-
      "void _removeFromSuspended(GoalRef goal) { final toRemove =
      <int>[]; for (final entry in suspended.entries) { entry.value
      .remove(goal); if (entry.value.isEmpty) { toRemove.add(entry
      .key); } } for (final key in toRemove) { suspended.remove(key); }
      }" — uses the well-known Dart idiom "collect-keys-then-remove"
      to avoid concurrent-modification during iteration: first pass
      collects keys whose value-set is now empty, second pass
      deletes those entries from the outer map.
    target_decision: >-
      Map to `private void _RemoveFromSuspended(GoalRef goal) { var
      toRemove = new List<int>(); foreach (var entry in Suspended) {
      entry.Value.Remove(goal); if (entry.Value.Count == 0) { toRemove
      .Add(entry.Key); } } foreach (var key in toRemove) { Suspended
      .Remove(key); } }`. The Dart `Map.entries` → C#
      `Dictionary<TKey, TValue>` direct iteration (yields
      `KeyValuePair<TKey, TValue>`). The Dart `set.isEmpty` → C#
      `set.Count == 0` (`HashSet<T>` exposes `Count`, NOT an
      `IsEmpty` shortcut). Carry-forward from goal_queue.dart.md
      `rf-dart-isempty-to-csharp-count-eq-zero` (`Queue<T>` /
      `HashSet<T>` / `Dictionary<,>` all use `.Count == 0`).
    idiom_id: null
    research_finding_id: rf-dart-collect-keys-then-remove-to-csharp-collect-keys-then-remove
    nuance: >-
      Concurrent-modification-during-iteration nuance (LOAD-
      BEARING, NEW idiom): Dart's `Map`/`Set` raise
      `ConcurrentModificationError` if mutated during iteration;
      .NET `Dictionary<,>`/`HashSet<>` raise
      `InvalidOperationException` ("Collection was modified;
      enumeration operation may not execute") in the same
      scenario. The "collect-keys-then-remove" idiom is the
      faithful counterpart in both languages — preserving the
      shape verbatim is the correct render. Set-empty nuance:
      Dart `set.isEmpty` → C# `set.Count == 0` (carry-forward).
      Value-vs-reference: `Suspended` is the public reference-
      type map; `toRemove` is a fresh local `List<int>` (no
      aliasing). Null-safety: every type non-nullable
      (`HashSet<GoalRef>` value is always present when the key
      is). Async: ABSENT.

  - construct_key: dart.method.simple_state_check_returning_nullable_bool.check_wait_state
    source_form: >-
      "bool? checkWaitState(int goalId) { final readerId =
      _waitReaders[goalId]; if (readerId == null) return null; return
      heap.isFullyBound(readerId); }" — reads the wait-reader for a
      goal, returning null if no wait state, else returning whether
      the reader has been bound (timer fired).
    target_decision: >-
      Map to `public bool? CheckWaitState(int goalId) { if
      (!_waitReaders.TryGetValue(goalId, out var readerId)) return
      null; return Heap.IsFullyBound(readerId); }`. The Dart
      `Map<int, int>[k] == null` check → C# `TryGetValue` (carry-
      forward — the Dart-side `Map<int, int>[k]` returns `int?`
      and is null on miss; C# `Dictionary<int, int>` throws on
      miss). The .NET `HeapFCP.IsFullyBound` method returns `bool`
      per heap_fcp.dart.md; the wrapping `bool?` records the
      "null on no-wait-state, bool on wait-state-present"
      contract.
    idiom_id: null
    research_finding_id: rf-dart-map-index-null-coalesce-default-to-csharp-tryget-ternary
    nuance: >-
      Same as above. Nullable-return nuance: Dart `bool?` → C#
      `bool?` (`Nullable<bool>`, a value-type nullable).

  - construct_key: dart.method.trivial_wait_reader_setter_getter_clearer
    source_form: >-
      Three single-line helpers over `_waitReaders`: `void
      clearWaitState(int goalId) { _waitReaders.remove(goalId); }`,
      `void setWaitReader(int goalId, int readerId) {
      _waitReaders[goalId] = readerId; }`, `int? getWaitReader(int
      goalId) => _waitReaders[goalId];` — straightforward map-
      keyed-by-int CRUD with the standard Dart-`Map[k]` returning
      nullable on miss.
    target_decision: >-
      Map each to a C# method: `public void ClearWaitState(int
      goalId) => _waitReaders.Remove(goalId);` (expression-bodied;
      `Dictionary<TKey, TValue>.Remove` returns `bool` but the
      Dart caller ignores the return — the C# version discards it
      via the void-return wrapper, preserving the Dart shape).
      `public void SetWaitReader(int goalId, int readerId) =>
      _waitReaders[goalId] = readerId;`. `public int?
      GetWaitReader(int goalId) => _waitReaders.TryGetValue(
      goalId, out var v) ? v : null;` (where `v` is `int` and
      the ternary returns `int?` — the boxed nullable form is
      automatic).
    idiom_id: null
    research_finding_id: rf-dart-map-index-null-coalesce-default-to-csharp-tryget-ternary
    nuance: >-
      `Dictionary.Remove` returns `bool` (true if key was
      present), but the Dart source ignores the return — the
      C# port preserves the "discard the return" pattern with
      a void wrapper. Expression-bodied form preserved.

  - construct_key: dart.handle_table_with_counter.allocate_get_isvalid_close_closeall.dart_io_randomaccessfile
    source_form: >-
      "int allocateFileHandle(RandomAccessFile file) { final handle =
      _nextFileHandle++; _fileHandles[handle] = file; return handle; }
      RandomAccessFile? getFile(int handle) => _fileHandles[handle];
      bool isValidHandle(int handle) => _fileHandles.containsKey(
      handle); void closeFileHandle(int handle) { final file =
      _fileHandles.remove(handle); if (file != null) { try { file
      .closeSync(); } catch (e) { /* Ignore close errors */ } } }
      void closeAllFiles() { for (final file in _fileHandles.values)
      { try { file.closeSync(); } catch (e) { /* Ignore close errors
      */ } } _fileHandles.clear(); }" — the file-handle table
      surface: allocate a new monotonically-increasing integer
      handle, look up by handle, close one, close all.
      `RandomAccessFile.closeSync()` is the blocking close API from
      `dart:io`.
    target_decision: >-
      Map `RandomAccessFile` to `System.IO.FileStream` (the .NET
      counterpart of "a random-access file handle" per Microsoft
      Learn: "FileStream provides a Stream for a file, supporting
      both synchronous and asynchronous read and write operations").
      The handle-table type becomes `private readonly
      Dictionary<int, FileStream> _fileHandles = new();`. Map the
      surface methods: `public int AllocateFileHandle(FileStream
      file) { var handle = _nextFileHandle++; _fileHandles[handle]
      = file; return handle; }` — `++` post-increment semantics
      identical in both languages (returns pre-increment value
      and stores incremented). `public FileStream? GetFile(int
      handle) => _fileHandles.TryGetValue(handle, out var f) ? f
      : null;`. `public bool IsValidHandle(int handle) =>
      _fileHandles.ContainsKey(handle);` (Dart
      `Map.containsKey` → C# `Dictionary.ContainsKey`, identical
      shape). `public void CloseFileHandle(int handle) { if
      (_fileHandles.Remove(handle, out var file)) { try { file
      .Close(); } catch (Exception) { /* Ignore close errors */
      } } }` — Dart `Map.remove(key)` returns the removed value
      or null; the .NET 5+ `Dictionary.Remove(key, out value)`
      overload returns `bool` AND yields the value, which is
      the cleanest faithful render. `RandomAccessFile.closeSync()`
      maps to `FileStream.Close()` (a synchronous, blocking
      close — Microsoft Learn: "Close — Closes the current
      stream and releases any resources … associated with the
      current stream"). `public void CloseAllFiles() { foreach
      (var file in _fileHandles.Values) { try { file.Close(); }
      catch (Exception) { /* Ignore close errors */ } }
      _fileHandles.Clear(); }`.
    idiom_id: null
    research_finding_id: rf-dart-randomaccessfile-to-csharp-filestream
    nuance: >-
      dart:io → System.IO nuance (LOAD-BEARING, NEW idiom,
      explicitly addressed): the well-known Dart-`dart:io` →
      .NET-`System.IO` mapping IS exercised here. The Dart
      `RandomAccessFile` (a class from `dart:io` representing
      an open file with positional read/write) maps to .NET
      `FileStream` (the .NET counterpart with the same
      capabilities — read/write at a positional offset). NOT
      `System.IO.File` (a static helper, not a handle type);
      NOT `BinaryReader`/`BinaryWriter` (those wrap a
      `FileStream` and add typed-read/write overloads — the
      Dart source uses raw byte-level read/write so the
      wrapper layer would be over-translation). Sync-close
      nuance: Dart `closeSync()` (the synchronous variant of
      `close()` — Dart APIs offer both Sync and async variants)
      maps to `FileStream.Close()` (synchronous; .NET also
      offers `CloseAsync` via `DisposeAsync` since .NET Core
      3.0, but the source's synchronous shape is preserved).
      Exception-swallow nuance (NEW carry-forward idiom):
      Dart's bare `catch (e) { /* Ignore … */ }` matches any
      thrown object; the .NET counterpart is `catch
      (Exception) { … }` (catch the universal supertype). NRT
      under enabled NRT permits unbound catch clauses; the
      `catch (Exception)` form is the .NET-canonical "swallow
      everything" pattern (Microsoft Learn: "catch — A catch
      block without an exception type catches all exceptions").
      Map-remove-returning-removed-value nuance (LOAD-BEARING):
      Dart `Map.remove(key)` returns `V?` (the removed value
      or null); .NET 5+ `Dictionary.Remove(key, out value)`
      overload returns `bool` AND yields the value via the
      `out` parameter — the faithful render. Pre-.NET 5 the
      idiom requires `TryGetValue + Remove(key)` separately —
      but the target framework is .NET 8+ per the project
      configuration so the single-call overload is available.
      Async: ABSENT in this file (the Dart source uses
      `closeSync`, not `close` async); .NET port preserves
      the sync surface. Counter post-increment nuance:
      Dart `_nextFileHandle++` and C# `_nextFileHandle++`
      both return the pre-increment value as the expression
      value and store the incremented value back — identical
      semantics.

  - construct_key: dart.handle_table_with_counter.allocate_get_isvalid_close_closeall.dart_ffi_dynamiclibrary
    source_form: >-
      "int loadLibrary(String path) { try { final lib = ffi
      .DynamicLibrary.open(path); final handle = _nextLibraryHandle++;
      _libraries[handle] = lib; return handle; } catch (e) { throw
      Exception('Failed to load library $path: $e'); } } ffi
      .DynamicLibrary? getLibrary(int handle) => _libraries[handle];
      bool isValidLibrary(int handle) => _libraries.containsKey(
      handle); void closeLibrary(int handle) { _libraries.remove(
      handle); } void closeAllLibraries() { _libraries.clear(); }" —
      the dynamic-library handle table: load by path (throws on
      load failure with a wrapped exception), look up by handle,
      no-op close (Dart's `DynamicLibrary` exposes no close
      method, so the table simply forgets the handle).
    target_decision: >-
      Map `ffi.DynamicLibrary` to `System.IntPtr` (the .NET
      counterpart of "a native-library handle" per Microsoft
      Learn: "NativeLibrary.Load — Loads a native library by name
      and returns its handle, an IntPtr"). The handle-table type
      becomes `private readonly Dictionary<int, IntPtr>
      _libraries = new();`. Map the surface methods: `public int
      LoadLibrary(string path) { try { var lib = NativeLibrary
      .Load(path); var handle = _nextLibraryHandle++; _libraries[
      handle] = lib; return handle; } catch (Exception e) { throw
      new Exception($"Failed to load library {path}: {e}"); } }`
      — `ffi.DynamicLibrary.open(path)` maps to `System.Runtime
      .InteropServices.NativeLibrary.Load(path)` (Microsoft Learn:
      "Provides APIs for managing native libraries. … Load(string)
      — Provides a simple API for loading a native library and
      returns a value that can be used in a call to GetExport").
      The wrapped-exception `Exception('Failed to load library
      $path: $e')` maps to `new Exception($"Failed to load
      library {path}: {e}")` — the message format is preserved
      byte-identically with C# interpolation `{path}` /
      `{e}` (the .NET `Exception` class is `System.Exception`,
      reachable via `using System;` implicitly). `public IntPtr?
      GetLibrary(int handle) => _libraries.TryGetValue(handle,
      out var lib) ? lib : (IntPtr?)null;` — `IntPtr` is a
      value type so the nullable form is `IntPtr?`; the explicit
      `(IntPtr?)null` cast resolves the ternary's type. `public
      bool IsValidLibrary(int handle) => _libraries.ContainsKey(
      handle);`. `public void CloseLibrary(int handle) =>
      _libraries.Remove(handle);` — Dart `Map.remove` returns
      `V?` (the removed value), the source ignores it. NOT
      `NativeLibrary.Free(handle)` — the source's `closeLibrary`
      is INTENTIONALLY a no-op-on-the-handle (Dart
      `DynamicLibrary` has no close method, and the source's
      doc comment makes this explicit: "note: DynamicLibrary
      doesn't have close method"). The faithful render preserves
      "remove from the handle table, do NOT free the native
      library" — same observable behaviour. Code-review
      discipline: a future hardening pass MAY add `NativeLibrary
      .Free(libHandle)` to match the .NET resource-management
      conventions, but doing so would CHANGE observable
      behaviour (subsequent `GetExport` on a freed handle would
      crash, where the Dart source leaves the library loaded
      indefinitely) — explicitly NOT done here. `public void
      CloseAllLibraries() => _libraries.Clear();` — same no-op-
      on-the-handle pattern.
    idiom_id: null
    research_finding_id: rf-dart-ffi-dynamiclibrary-to-csharp-nativelibrary
    nuance: >-
      dart:ffi → System.Runtime.InteropServices nuance (LOAD-
      BEARING, NEW idiom, explicitly addressed): Dart's `dart
      :ffi` library exposes `DynamicLibrary` (a class wrapping a
      native-library handle) and methods like `lookup<T>(symbol)`
      and `lookupFunction<T1, T2>(symbol)` for typed FFI calls.
      .NET's counterpart is `System.Runtime.InteropServices
      .NativeLibrary` (a STATIC helper class providing
      `Load(string)`, `GetExport(IntPtr, string)`, and
      `Free(IntPtr)`) plus `IntPtr` for the handle value —
      Microsoft Learn: "Provides APIs for managing native
      libraries". The .NET model uses static helpers + an
      `IntPtr` handle; the Dart model uses an instance class.
      The faithful render uses `IntPtr` as the table value
      type. Type-vs-static-helper nuance: a `ffi.DynamicLibrary
      lib = …; lib.lookup<T>(…);` call sequence in Dart
      becomes `IntPtr lib = …; NativeLibrary.GetExport(lib, …);`
      in .NET — TWO surface differences: (i) the instance type
      becomes a value-type handle; (ii) the lookup is a static
      call. THIS file does NOT exercise the lookup surface (it
      only loads and stores handles), so the lookup-call
      nuance is documented but not exercised. No-op-close
      nuance (LOAD-BEARING): Dart `DynamicLibrary` exposes no
      close method (a deliberate design choice — dynamic
      libraries in Dart are loaded for the lifetime of the
      isolate); the .NET counterpart DOES expose
      `NativeLibrary.Free(IntPtr)` but the source's
      `closeLibrary` is INTENTIONALLY a no-op-on-the-handle
      (only the table entry is removed). The faithful render
      preserves the no-op semantics — DO NOT inject
      `NativeLibrary.Free`. Wrapped-exception nuance: Dart
      `try { … } catch (e) { throw Exception('… $e'); }` wraps
      the inner exception into a new top-level `Exception`
      with an interpolated message; the .NET counterpart is
      `catch (Exception e) { throw new Exception($"… {e}"); }`.
      Microsoft Learn ("Best practices for exceptions —
      Preserve information using inner exceptions") recommends
      `throw new Exception(msg, innerException: e)` to
      preserve the inner stack — but the Dart source does
      NOT preserve the inner exception (it interpolates `$e`
      into the message string only), so the faithful render
      matches that one-way coarsening. Async / Stream /
      isolate: ABSENT (no async FFI surface here).

  - construct_key: dart.field.nullable_object_for_madglp_injection_seam
    source_form: >-
      "// madGLP context (set when running in multiagent mode) //
      Used by '_cold_send' kernel to access globalization
      infrastructure Object? madContext;" — a plain nullable
      `Object?` instance field, public, set post-construction by
      the multiagent layer (mad_context.dart) and read by the
      `_cold_send` body kernel (body_kernels.dart).
    target_decision: >-
      Map to `public object? MadContext { get; set; }` — a
      NULLABLE reference type auto-property with public getter
      and public setter. NOT a field (the source declares it
      without `_` prefix, so external code reads AND writes it;
      a property gives encapsulation without changing the
      observable behaviour). NOT `late final` (the source does
      NOT use `late final` — `Object? madContext;` is a
      plain-nullable field, the caller may set it OR leave it
      null; the multiagent layer is responsible for setting it
      when activating multiagent mode). NRT under enabled
      conditions records the nullable property type.
    idiom_id: null
    research_finding_id: rf-dart-nullable-object-injection-seam-to-csharp-object-question-property
    nuance: >-
      Nullable-`Object?` field nuance: Dart `Object?` is the
      least-precise reference type (top of the reference
      hierarchy plus null); .NET `object?` is the direct
      counterpart. Injection-seam pattern nuance: the source
      uses this field as an OPTIONAL dependency injection
      point — "if multiagent mode, set this; else leave null".
      The faithful C# render preserves the same opt-in
      semantics. Read-write nuance: caller code does
      `runtime.madContext = …` then later body-kernel code
      does `final ctx = rt.madContext`. The C# property with
      `{ get; set; }` preserves both directions. Late-final
      RUL-OUT: NOT `late final` (would require write-exactly-
      once); the source allows multiple writes (or zero
      writes). Value-vs-reference: `object` is a reference
      type; the field stores a reference (or null). Async:
      ABSENT.

  - construct_key: dart.field.nullable_function_typed_for_output_callback_injection_seam
    source_form: >-
      "// Output callback for '_output'/1 kernel. // If set, called
      instead of print(). Used by tests and Flutter UI. void
      Function(String)? outputCallback;" — a plain nullable
      function-typed instance field, public, set post-construction
      by tests or the Flutter UI, read by the `_output` body
      kernel.
    target_decision: >-
      Map to `public Action<string>? OutputCallback { get; set; }`
      — a NULLABLE delegate auto-property. The Dart function-
      typed signature `void Function(String)` (a void-returning,
      one-string-parameter function) maps to the .NET
      `Action<string>` delegate (per Microsoft Learn: "Action<T>
      — Encapsulates a method that has a single parameter and
      does not return a value"). The trailing `?` makes it
      nullable. Caller invokes with `OutputCallback?.Invoke(s)`
      (null-conditional) or with a null-guard `if
      (OutputCallback != null) OutputCallback(s);`. Carry-
      forward of repl_play_runner.dart.md
      `rf-dart-void-function-question-to-csharp-action-nullable`.
    idiom_id: null
    research_finding_id: rf-dart-void-function-question-to-csharp-action-nullable
    nuance: >-
      Function-typed-field nuance: Dart `void Function(T)` →
      .NET `Action<T>` (carry-forward). Nullable nuance: the
      `?` is preserved; the field MUST be `Action<string>?`
      and call sites MUST use `?.Invoke` or a null-guard.
      Differentiation-from-external_io.dart.md note:
      `OutputObserver.onTerm` is Dart-NON-nullable in
      external_io.dart.md and maps to `Action<Term>` (no `?`);
      THIS field IS Dart-nullable and maps to `Action<string>?`
      — the differentiation is load-bearing. Value-vs-
      reference: delegates in .NET are reference types
      (instances of `System.MulticastDelegate`); identical
      to Dart's closures. Async: ABSENT (no async-callback
      signature here).

conversion_units:
  - "class GlpRuntime (reference type, NOT record, NOT struct — identity equality load-bearing)"
  - "  property: HeapFCP Heap { get; }   // ctor-injected, init-only (Dart `final`)"
  - "  property: GoalQueue Gq { get; }   // ctor-injected, init-only"
  - "  property: SystemPredicateRegistry SystemPredicates { get; }   // ctor-injected, init-only"
  - "  property: BodyKernelRegistry BodyKernels { get; }   // ctor-injected, init-only, seeded via CreateDefaultBodyKernels()"
  - "  property: Dictionary<object?, BytecodeRunner> Runners { get; } = new()   // public; null-key NOT supported by Dictionary default — code-review obligation that no caller stores under null"
  - "  property: Dictionary<string, GlpChannelHandle> GlpChannels { get; } = new()"
  - "  field: private readonly Dictionary<int, int> _budgets = new()   // per-goal tail-budget table"
  - "  field: private readonly Dictionary<int, CallEnv> _goalEnvs = new()"
  - "  field: private readonly Dictionary<int, object?> _goalPrograms = new()"
  - "  field: private readonly Dictionary<int, object?> _goalModuleContexts = new()"
  - "  field: private readonly Dictionary<int, FileStream> _fileHandles = new()   // RandomAccessFile → FileStream"
  - "  field: private int _nextFileHandle = 1   // monotonic counter; ++ used"
  - "  field: private readonly Dictionary<int, IntPtr> _libraries = new()   // DynamicLibrary handle → IntPtr"
  - "  field: private int _nextLibraryHandle = 1   // monotonic counter; ++ used"
  - "  field: public int NextGoalId = 10000   // PUBLIC MUTABLE FIELD (not property) — external code does runtime.NextGoalId++"
  - "  field: private int _pendingTimers = 0"
  - "  property: int PendingTimers => _pendingTimers   // expression-bodied read-only getter"
  - "  method: public void IncrementPendingTimers() => _pendingTimers++"
  - "  method: public void DecrementPendingTimers() => _pendingTimers--"
  - "  field: private readonly Dictionary<int, int> _waitReaders = new()"
  - "  property: Dictionary<int, HashSet<GoalRef>> Suspended { get; } = new()   // PUBLIC (external readers); mutation via methods"
  - "  property: HashSet<int> InfrastructureGoalIds { get; } = new()"
  - "  property: object? MadContext { get; set; }   // nullable injection seam — multiagent context"
  - "  property: Action<string>? OutputCallback { get; set; }   // nullable injection seam — output redirection"
  - "  ctor: public GlpRuntime(HeapFCP? heap = null, GoalQueue? gq = null, SystemPredicateRegistry? systemPredicates = null, BodyKernelRegistry? bodyKernels = null) — body: Heap = heap ?? new HeapFCP(); Gq = gq ?? new GoalQueue(); SystemPredicates = systemPredicates ?? new SystemPredicateRegistry(); BodyKernels = bodyKernels ?? CreateDefaultBodyKernels();"
  - "  private static BodyKernelRegistry CreateDefaultBodyKernels() — body: var registry = new BodyKernelRegistry(); BodyKernels.RegisterStandardBodyKernels(registry); return registry;"
  - "  public bool? CheckWaitState(int goalId) — TryGetValue + Heap.IsFullyBound(readerId)"
  - "  public void ClearWaitState(int goalId) => _waitReaders.Remove(goalId)"
  - "  public void SetWaitReader(int goalId, int readerId) => _waitReaders[goalId] = readerId"
  - "  public int? GetWaitReader(int goalId) => _waitReaders.TryGetValue(goalId, out var v) ? v : null"
  - "  public IReadOnlyList<GoalRef> CommitSigmaHat(SigmaHat sigmaHat) — delegates to CommitOps.ApplySigmaHatFCP, calls EnqueueAll(acts), returns acts"
  - "  [Obsolete(error: true)] public IReadOnlyList<GoalRef> CommitWriters(IEnumerable<int> writerIds) => throw new NotImplementedException(\"Legacy CommitWriters deprecated - use CommitSigmaHat\")"
  - "  [Obsolete(error: true)] public IReadOnlyList<GoalRef> AbandonWriter(int writerId) => throw new NotImplementedException(\"Legacy AbandonWriter deprecated - FCP has no abandon\")"
  - "  public void SuspendGoalFCP(int goalId, int kappa, ISet<int> readerVarIds) — TryGetValue + new HashSet + Add idiom on Suspended; then delegates to SuspendOps.SuspendGoalFCP(heap: Heap, ...)"
  - "  public bool TailReduce(int g) — TryGetValue on _budgets default to MachineStateConstants.TailRecursionBudgetInit; Fairness.NextTailBudget; if 0 reset and return true; else store decremented, return false"
  - "  public int BudgetOf(int g) => _budgets.TryGetValue(g, out var b) ? b : MachineStateConstants.TailRecursionBudgetInit"
  - "  public void SetGoalEnv(int g, CallEnv env) => _goalEnvs[g] = env"
  - "  public CallEnv? GetGoalEnv(int g) => _goalEnvs.TryGetValue(g, out var env) ? env : null"
  - "  public void SetGoalProgram(int g, object? program) => _goalPrograms[g] = program"
  - "  public object? GetGoalProgram(int g) => _goalPrograms.TryGetValue(g, out var p) ? p : null"
  - "  public void SetGoalModuleContext(int g, object? ctx) => _goalModuleContexts[g] = ctx"
  - "  public object? GetGoalModuleContext(int g) => _goalModuleContexts.TryGetValue(g, out var c) ? c : null"
  - "  private void EnqueueAll(IReadOnlyList<GoalRef> acts) — foreach (var a in acts) EnqueueReactivatedGoal(a)"
  - "  public void EnqueueReactivatedGoal(GoalRef goal) — Gq.Enqueue(goal); _RemoveFromSuspended(goal)"
  - "  private void _RemoveFromSuspended(GoalRef goal) — collect-keys-then-remove idiom: first foreach removes goal from every value-set, collects empty-set keys into toRemove; second foreach removes those keys from Suspended"
  - "  public int AllocateFileHandle(FileStream file) — var handle = _nextFileHandle++; _fileHandles[handle] = file; return handle"
  - "  public FileStream? GetFile(int handle) — _fileHandles.TryGetValue(handle, out var f) ? f : null"
  - "  public bool IsValidHandle(int handle) => _fileHandles.ContainsKey(handle)"
  - "  public void CloseFileHandle(int handle) — Dictionary.Remove(handle, out var file); if removed, try { file.Close(); } catch (Exception) { /* ignore */ }"
  - "  public void CloseAllFiles() — foreach values try Close catch swallow; then _fileHandles.Clear()"
  - "  public int LoadLibrary(string path) — try { var lib = NativeLibrary.Load(path); ... } catch (Exception e) { throw new Exception($\"Failed to load library {path}: {e}\"); }"
  - "  public IntPtr? GetLibrary(int handle) — _libraries.TryGetValue(handle, out var lib) ? lib : (IntPtr?)null"
  - "  public bool IsValidLibrary(int handle) => _libraries.ContainsKey(handle)"
  - "  public void CloseLibrary(int handle) => _libraries.Remove(handle)   // INTENTIONAL no-op on the native handle (matches Dart DynamicLibrary having no close)"
  - "  public void CloseAllLibraries() => _libraries.Clear()   // INTENTIONAL no-op on native handles"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-import-dartio-to-csharp-using-systemio — `dart:io` import → `using System.IO;` (NEW finding)

- Deep analysis: the file imports `dart:io` at the top and references one symbol only: `RandomAccessFile`. The `dart:io` library is part of the Dart-core SDK and groups file/socket/process/stdio APIs; its .NET counterpart is split across several `System.*` namespaces. THIS file uses only the file-handle subset.
- Authoritative Dart (cached): https://api.dart.dev/stable/dart-io/dart-io-library.html — "Built-in types and core primitives for a Dart program. Includes File, Directory, Process, Platform, RandomAccessFile, Socket, …".
- Authoritative .NET (cached): https://learn.microsoft.com/en-us/dotnet/api/system.io — "The System.IO namespace contains types that allow reading and writing to files and data streams". `FileStream` and related types live here.
- Conclusion: emit `using System.IO;`. NEW idiom registered: "dart:io top-level import → `using System.IO;`" (the .NET counterpart for the file-API subset). Authoritative both sides; no escalation.

### rf-dart-import-dartffi-to-csharp-using-interopservices — `dart:ffi` import → `using System.Runtime.InteropServices;` (NEW finding)

- Deep analysis: the file imports `dart:ffi as ffi` and uses two symbols: `ffi.DynamicLibrary` (type) and `ffi.DynamicLibrary.open(path)` (static factory). The `as ffi` prefix is for disambiguation only — the call sites use the qualified form `ffi.DynamicLibrary.open(...)`.
- Authoritative Dart (cached): https://api.dart.dev/stable/dart-ffi/dart-ffi-library.html — "Foreign Function Interface for interoperability with the C programming language. Provides Pointer, DynamicLibrary, NativeFunction, …".
- Authoritative .NET (cached): https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.nativelibrary — "Provides APIs for managing native libraries". `NativeLibrary` is a static helper class with `Load(string)`, `GetExport(IntPtr, string)`, `Free(IntPtr)`.
- Conclusion: emit `using System.Runtime.InteropServices;` (the namespace hosting `NativeLibrary` and the related interop types). The Dart `as ffi` prefix collapses to bare references in the target (per the convspec corpus's import-prefix convention). NEW idiom registered. Authoritative both sides; no escalation.

### rf-dart-import-relative-to-csharp-using-namespace — eight relative imports → single `using <root>.Runtime;` (cached idiom, reuse)

- Deep analysis: eight relative imports (`machine_state.dart`, `heap_fcp.dart`, `suspend_ops.dart`, `commit.dart`, `abandon.dart`, `fairness.dart`, `system_predicates.dart`, `body_kernels.dart`) — all target sibling files in `lib/runtime/` and use bare-import surface.
- Authoritative Dart (cached): https://dart.dev/language/libraries#using-libraries — relative imports resolve by file path.
- Authoritative .NET (cached): https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive — `using <Namespace>;` imports the full public surface.
- Conclusion: a single `using <root>.Runtime;` covers all eight. Carry-forward of the idiom recorded in heap_fcp.dart.md / external_io.dart.md / suspension.dart.md / machine_state.dart.md / fairness.dart.md. FR-024 cache hit; no new research.

### rf-dart-import-show-clause-no-csharp-counterpart — `package:`-prefixed imports with `show` clauses (cached idiom, reuse)

- Deep analysis: two `package:`-prefixed imports with `show` clauses narrow the imported surface to specific symbols (`CallEnv`, `BytecodeRunner` from `package:glp_runtime/bytecode/runner.dart`; `GlpChannelHandle` from `package:glp_runtime/runtime/glp_activation.dart`).
- Authoritative Dart (cached): https://dart.dev/language/libraries#importing-only-part-of-a-library — `show A, B` and `hide A, B` narrow the imported surface.
- Authoritative .NET (cached): https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive — no per-symbol narrowing for `using <Namespace>;`. `using static <Type>` imports a TYPE's static members but is not a per-symbol filter for TYPE imports.
- Conclusion: emit `using <root>.Bytecode;` and `using <root>.Runtime;` (the latter coincides with the relative-imports' target). The `show` per-symbol narrowing is dropped (a one-way coarsening that does not affect observable behaviour). Carry-forward of the convspec-corpus convention. FR-024 cache hit.

### rf-dart-mutable-state-class-identity-equality-to-csharp-class — `GlpRuntime` reference-equal runtime facade (cached idiom, reuse)

- Deep analysis: `GlpRuntime` aggregates many reference types and mutable counters; no `==`/`hashCode` override; mutated in place across the runtime layers. Identity equality is the only correct semantics.
- Authoritative Dart (cached): https://dart.dev/language/class-modifiers — default classes use identity equality unless `==` is overridden.
- Authoritative .NET (cached): https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/classes — reference types compare by identity unless `Equals`/`==` is overridden; records synthesise value equality.
- Conclusion: reference `class GlpRuntime` (NOT record, NOT struct). Carry-forward of machine_state.dart.md `GoalState` analysis. Authoritative both sides; no escalation.

### rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults — runtime ctor with `??`-defaulted collaborators (cached idiom, reuse)

- Deep analysis: four optional named parameters with `??`-bodied defaults to fresh-instance allocations; one default uses a private-static factory to seed the `BodyKernelRegistry`.
- Authoritative Dart (cached): https://dart.dev/language/constructors — initialiser-list syntax, named-optional parameters, default expressions.
- Authoritative .NET (cached): https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/constructors — constructor parameters with default values, body assignment, null-coalesce.
- Conclusion: single C# constructor with four nullable parameters and `??`-bodied defaults; static factory invoked from constructor body. Carry-forward. Authoritative both sides; no escalation.

### rf-dart-private-static-factory-seed-to-csharp-private-static — `_createDefaultBodyKernels` (NEW finding)

- Deep analysis: a `static` Dart method returning a freshly-allocated, side-effect-seeded `BodyKernelRegistry`. Invoked from the constructor's initialiser-list default expression.
- Authoritative Dart (cached): https://dart.dev/language/methods — static methods are class-level, no `this`.
- Authoritative .NET (cached): https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/static-classes-and-static-class-members — `static` members on a non-static class are class-level helpers, no `this`.
- Conclusion: `private static BodyKernelRegistry CreateDefaultBodyKernels()` on the `GlpRuntime` class. NEW idiom registered. Authoritative both sides; no escalation.

### rf-dart-method-passthrough-to-csharp-method-passthrough — thin facade methods (NEW finding)

- Deep analysis: `commitSigmaHat`, `enqueueReactivatedGoal`, `_enqueueAll`, the three goal-keyed setters/getters, the wait-state helpers, and several other methods are thin pass-throughs that delegate to a sibling static helper or perform a single map operation.
- Authoritative Dart: dart.dev — methods may be expression-bodied (`=>` arrow) or block-bodied.
- Authoritative .NET: learn.microsoft.com — methods may be expression-bodied (C# 6+ `=>`) or block-bodied.
- Conclusion: preserve the Dart shape verbatim — expression-bodied for single-statement bodies; block-bodied otherwise. NEW idiom registered (the convention is widely used but had not been pinned as a named idiom).

### rf-dart-unimplementederror-to-csharp-notimplementedexception — deprecated legacy methods (NEW finding)

- Deep analysis: two deprecated legacy methods (`commitWriters`, `abandonWriter`) throw `UnimplementedError` with explanatory messages and TODO removal directives.
- Authoritative Dart (cached): https://api.dart.dev/stable/dart-core/UnimplementedError-class.html — "The operation was not implemented".
- Authoritative .NET (cached): https://learn.microsoft.com/en-us/dotnet/api/system.notimplementedexception — "The exception that is thrown when a requested method or operation is not implemented".
- Conclusion: `throw new NotImplementedException(message)` with `[Obsolete(message, error: true)]` attribute for compile-time enforcement of the deprecation. NEW idiom registered. Authoritative both sides; no escalation.

### rf-dart-putifabsent-set-add-to-csharp-tryget-add — `suspended.putIfAbsent(k, () => <GoalRef>{}).add(v)` (NEW finding, LOAD-BEARING)

- Deep analysis: the cross-cutting `suspended` map is updated via the Dart `putIfAbsent(key, () => defaultEmpty).add(value)` idiom — lazy-create the inner set on first access, then add the value.
- Authoritative Dart: https://api.dart.dev/stable/dart-core/Map/putIfAbsent.html — "Look up the value of key, or add a new entry if it isn't there. Returns the value associated with key, if there is one. Otherwise calls ifAbsent to get a new value, associates key with that value, and then returns the new value".
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.trygetvalue — "Gets the value associated with the specified key. Returns true if the dictionary contains an element with the specified key; otherwise, false".
- Conclusion: explicit `TryGetValue + new HashSet + indexer-set + Add` (the .NET-canonical "lazily create-empty-collection on first access" pattern). Rejected: `CollectionsMarshal.GetValueRefOrAddDefault` (.NET 6+ — low-level, obscures shape fidelity). NEW idiom registered. Authoritative both sides; no escalation.

### rf-dart-map-index-null-coalesce-default-to-csharp-tryget-ternary — `m[k] ?? default` (NEW finding)

- Deep analysis: four sites use the Dart `Map[k] ?? default` pattern for "read with default-on-miss" (`tailReduce`, `budgetOf`, `checkWaitState` via `if (readerId == null)`, the three goal-keyed getters).
- Authoritative Dart: https://api.dart.dev/stable/dart-core/Map/operator_get.html — "Returns the value for the given key or null if key is not in the map".
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.item — "Gets or sets the value associated with the specified key. Exceptions: KeyNotFoundException — The property is retrieved and key does not exist in the collection". https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.trygetvalue — "Gets the value associated with the specified key".
- Conclusion: `Dictionary<K,V>` indexer THROWS on miss; the faithful render of "Dart `m[k]` returns nullable on miss" MUST use `TryGetValue + ternary`. NEW idiom registered. Authoritative both sides; no escalation.

### rf-dart-map-index-to-csharp-tryget-or-null-for-nullable-return — getter methods returning nullable (NEW finding)

- Deep analysis: three pairs of setter/getter methods over goal-keyed maps; getters return nullable (`V?`); setters take non-nullable.
- Authoritative: same as `rf-dart-map-index-null-coalesce-default-to-csharp-tryget-ternary` above.
- Conclusion: getter methods use `TryGetValue + ternary` returning `V?`. NEW idiom registered.

### rf-dart-collect-keys-then-remove-to-csharp-collect-keys-then-remove — `_removeFromSuspended` (NEW finding, LOAD-BEARING)

- Deep analysis: the helper uses the "collect-keys-then-remove" two-pass idiom to avoid concurrent-modification during iteration.
- Authoritative Dart: https://api.dart.dev/stable/dart-core/ConcurrentModificationError-class.html — "Error thrown when modifying a collection while iterating over it".
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception — "Collection was modified; enumeration operation may not execute" (the standard message when a collection is modified during enumeration).
- Conclusion: both languages raise an error on concurrent modification during iteration; the two-pass idiom is the faithful counterpart in both. NEW idiom registered. Authoritative both sides; no escalation.

### rf-dart-randomaccessfile-to-csharp-filestream — file-handle table (NEW finding, LOAD-BEARING)

- Deep analysis: the file-handle table stores `RandomAccessFile` instances keyed by integer handles; `closeSync()` is called on shutdown; exceptions during close are swallowed.
- Authoritative Dart (cached): https://api.dart.dev/stable/dart-io/RandomAccessFile-class.html — "Random access to the data in a file. RandomAccessFile objects are obtained by calling open on a File object". Synchronous methods: `closeSync()`, `readSync()`, `writeFromSync()`.
- Authoritative .NET (cached): https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream — "Provides a Stream for a file, supporting both synchronous and asynchronous read and write operations. … Close() — Closes the current stream and releases any resources … associated with the current stream".
- Conclusion: `RandomAccessFile` → `FileStream`; `closeSync()` → `Close()`. The exception-swallow `catch (e) { /* Ignore close errors */ }` → `catch (Exception) { /* Ignore close errors */ }`. NEW idiom registered. Authoritative both sides; no escalation.

### rf-dart-ffi-dynamiclibrary-to-csharp-nativelibrary — native-library handle table (NEW finding, LOAD-BEARING)

- Deep analysis: the library-handle table stores `ffi.DynamicLibrary` instances keyed by integer handles; `ffi.DynamicLibrary.open(path)` is the load API; close is a no-op-on-the-handle (Dart `DynamicLibrary` has no close method).
- Authoritative Dart (cached): https://api.dart.dev/stable/dart-ffi/DynamicLibrary-class.html — "A dynamically loaded native library. … DynamicLibrary.open(String path) — Loads a dynamic library file with local visibility". No `close` method on the class.
- Authoritative .NET (cached): https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.nativelibrary — "Provides APIs for managing native libraries. Load(String) — Provides a simple API for loading a native library and returns a value that can be used in a call to GetExport". `Free(IntPtr)` — "Provides a simple API for freeing a native library represented by an IntPtr".
- Conclusion: `ffi.DynamicLibrary` → `IntPtr` (the .NET counterpart of "a native-library handle"); `ffi.DynamicLibrary.open(path)` → `NativeLibrary.Load(path)`. The source's no-op-on-the-handle close is preserved — DO NOT inject `NativeLibrary.Free` (would change observable behaviour). NEW idiom registered. Authoritative both sides; no escalation.

### rf-dart-nullable-object-injection-seam-to-csharp-object-question-property — `madContext` field (NEW finding)

- Deep analysis: a plain nullable `Object?` instance field used as an opt-in dependency-injection seam by the multiagent layer.
- Authoritative Dart: https://api.dart.dev/stable/dart-core/Object-class.html — "The base class for all Dart objects".
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/api/system.object — "Supports all classes in the .NET class hierarchy".
- Conclusion: `public object? MadContext { get; set; }` — auto-property with public getter/setter, nullable under enabled NRT. NOT `late final` (the source's plain-nullable field is the canonical "opt-in injection seam" pattern). NEW idiom registered.

### rf-dart-void-function-question-to-csharp-action-nullable — `outputCallback` field (cached idiom, reuse)

- Deep analysis: a plain nullable `void Function(String)?` instance field used as an opt-in output-redirection seam by tests and the Flutter UI.
- Authoritative Dart: https://dart.dev/language/functions#function-types — "void Function(String) is the type of a function that takes a String and returns void".
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/api/system.action-1 — "Encapsulates a method that has a single parameter and does not return a value".
- Conclusion: `public Action<string>? OutputCallback { get; set; }` — auto-property, nullable delegate. Callers use `OutputCallback?.Invoke(s)` (null-conditional) or a null-guard. Carry-forward of repl_play_runner.dart.md `rf-dart-void-function-question-to-csharp-action-nullable`. FR-024 cache hit; no new research.

## Notes

- This file DOES exercise the well-known `dart:io` → `System.IO` nuance (file-handle table — `RandomAccessFile` → `FileStream`; `closeSync` → `Close`; bare `catch (e)` → `catch (Exception)`).
- This file DOES exercise the well-known `dart:ffi` → .NET-FFI nuance (library-handle table — `ffi.DynamicLibrary` → `IntPtr` + `NativeLibrary.Load`/`.Free`; the source's no-op-close is preserved verbatim).
- This file does NOT exercise `async` / `await` / `Future` / `Stream` / `Isolate` / `Completer`. All operations are synchronous (the file-handle close uses `closeSync`, NOT `close` async). The well-known `Stream` → `IAsyncEnumerable` nuance is correctly NOT asserted here because the CODE does not exercise it.
- Load-bearing semantic decisions for THIS file: (a) `GlpRuntime` MUST be a C# reference `class`, NEVER `record`/`record class`/`record struct`/`struct` — identity equality + mutation propagation are both required (carry-forward from machine_state.dart.md). (b) `Map<Object?, BytecodeRunner> runners` → `Dictionary<object?, BytecodeRunner>` with a code-review obligation that no caller stores a null key — `Dictionary<,>` rejects literal-null keys by default; an `IEqualityComparer<object?>` with a null-sentinel would be the upgrade path if needed. (c) `Map<int, Set<GoalRef>> suspended` with the `putIfAbsent + add` idiom → explicit `TryGetValue + new HashSet + indexer-set + Add` (NEW idiom). (d) `nextGoalId` MUST be a PUBLIC MUTABLE FIELD (not a property) — external code does `runtime.NextGoalId++`; the field form is the .NET-canonical counterpart of "raw mutable integer counter exposed for direct mutation". (e) `RandomAccessFile` → `FileStream` (NEW idiom); `closeSync()` → `Close()`; bare `catch (e)` → `catch (Exception)`. (f) `ffi.DynamicLibrary` → `IntPtr` + `NativeLibrary.Load`/`.Free` (NEW idiom); the source's no-op-on-the-handle close is preserved verbatim (DO NOT inject `Free`). (g) `UnimplementedError` → `NotImplementedException` + `[Obsolete(error: true)]` (NEW idiom). (h) `madContext` (`Object? madContext;`) → `object? MadContext { get; set; }` — a plain nullable property, NOT `late final` (the source uses a plain-nullable injection seam, not a write-once contract). (i) `outputCallback` (`void Function(String)? outputCallback;`) → `Action<string>? OutputCallback { get; set; }` — nullable delegate property (carry-forward from repl_play_runner.dart.md).
- Trivial / non-construct elements: triple-slash doc comments (`///`) map mechanically to C# XML-doc comments (`///`); `@override` annotations are subsumed by the C# `override` keyword on each overriding member (none in this file — `GlpRuntime` does not override any `Object` methods); `var`-style locals (none in source — all locals are explicitly typed) map to C# `var`; `for` and `foreach` loops are control-flow-identical across languages; `++` and `--` post-increment/decrement have identical semantics in both languages.
- Zero escalations: every non-trivial construct resolved from authoritative Dart (dart.dev / api.dart.dev) and/or .NET (learn.microsoft.com) official documentation. Three carry-forward idioms reused verbatim (relative-import→using, package-show→drop, mutable-state-class→reference-class) plus carry-forward of the named-optional-ctor and void-Function-nullable patterns. Ten NEW idioms registered (`dart:io`→`System.IO` import, `dart:ffi`→`System.Runtime.InteropServices` import, private-static-factory-seed, method-passthrough, `UnimplementedError`→`NotImplementedException`, `putIfAbsent + add`→`TryGetValue + new + Add`, `m[k] ?? default`→`TryGetValue + ternary`, `m[k]` nullable getter→`TryGetValue + ternary or null`, collect-keys-then-remove, `RandomAccessFile`→`FileStream`, `ffi.DynamicLibrary`→`IntPtr` + `NativeLibrary`, nullable-`Object?` injection seam). FR-009/FR-010 quality bar satisfied: every non-trivial construct has BOTH a deep-analysis basis AND a researched-pattern basis (or an explicit carry-forward idiom_id surrogate via the named research_finding_id).
- Code-review obligations recorded (NOT compile-time enforced): (i) `runners` map MUST NOT receive a null key; (ii) `suspended` HashSet iteration order is unordered — consumers MUST NOT depend on insertion order; (iii) `closeLibrary` is INTENTIONALLY a no-op-on-the-native-handle — DO NOT add `NativeLibrary.Free(handle)` without an explicit Gabi-approved re-spec.
