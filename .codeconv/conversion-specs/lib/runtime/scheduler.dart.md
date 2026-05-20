# Conversion Spec — lib/runtime/scheduler.dart

> Conversion-spec artifact for lib/runtime/scheduler.dart (FR-011).
> Spec-only (FR-023): describes the Dart→C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> The `Scheduler` is the **drain loop** that pulls activations from
> `GlpRuntime.gq` (the `GoalQueue` decided in `goal_queue.dart.md` /
> `machine_state.dart.md`), looks up the appropriate `BytecodeRunner`
> (per-program; see `lib/bytecode/runner.dart.md`), constructs a
> `RunnerContext` (`runner.dart.md`), and dispatches one reduction
> step per loop iteration; on `RunResult.suspended`/`.terminated` it
> updates per-goal tracking, formats trace lines for stdout (or a
> user-supplied sink), classifies the final status
> (`succeeded`/`failed`/`suspended`), and (for `drainAsyncWithStatus`)
> polls for `dart:async` timers via `Future.delayed`. Output and term-
> formatting helpers (`_formatTerm`, `_formatGoal`, `formatBinding`)
> implement the Dart-side pretty-printer for terms, lists, conjunctions,
> and bindings with cycle detection on the heap.
>
> Heavy idiom reuse — every cross-file dependency inherits the prior
> convspec decisions: `GlpRuntime` (runtime.dart.md), `GoalQueue` /
> `GoalRef` (goal_queue.dart.md / machine_state.dart.md),
> `BytecodeRunner` / `RunnerContext` / `RunResult` /
> `CallEnv` (lib/bytecode/runner.dart.md), `Term` / `VarRef` /
> `ConstTerm` / `StructTerm` (terms.dart.md), `HeapFCP.derefAddr` /
> `isReader` / `tryWriterForReader` (heap_fcp.dart.md), the per-goal
> infrastructure-goal-ids set (runtime.dart.md), `rt.suspended` reader-
> index (suspension.dart.md). The threading-model decision (single-
> owning-context per goal / non-concurrent collections /
> `System.Threading.Timer`-or-`Task.Delay` marshalled to the owning
> scheduler) is INHERITED from heap_fcp.dart.md escalations[0] and the
> bytecode/runner.dart.md `rf-dart-timer-to-csharp-system-threading-
> timer` carry-forward — NOT re-escalated here (FR-013: don't double-
> escalate a previously-escalated decision).
>
> Load-bearing nuances exercised by THIS file:
> (a) Dart `class Scheduler` is a reference-identity mutable container
>     of the runtime, the per-program runner map, the trace sink, and
>     three small per-query name/display maps; identity equality is
>     load-bearing (the scheduler instance is held by the REPL / play
>     driver / test harness across drain calls and mutated in place);
>     MUST be a C# reference `class` (NEVER `record`, NEVER `struct`).
> (b) Dart `enum ExecutionStatus { succeeded, failed, suspended }` — a
>     plain three-member tag-only enum returned by `drainWithStatus` /
>     `drainAsyncWithStatus` (NOT spec-named identifiers, so .NET
>     PascalCase per the standard naming guideline — unlike `WrtTag` /
>     `RoTag` / `ValueTag` whose verbatim preservation IS load-bearing
>     per cells.dart.md / heap_fcp.dart.md).
> (c) Dart `class DrainResult { final List<int> goalsRan; final
>     ExecutionStatus status; final List<String> suspendedGoals; final
>     Set<int> blockingReaders; }` — an IMMUTABLE result bundle with
>     value-equality intent (all fields `final`, no `==`/`hashCode`
>     override, but the source's INTENT is "a value bundle of one
>     drain"). Decision: REFERENCE-TYPE `class` (NOT a record) —
>     justified below in nuance (sequence-of-int identity in test
>     harnesses, large `List<int>` value-equality would be O(N) on every
>     comparison, no source `==` override).
> (d) Dart `Map<int, String> _queryVarNames` / `Map<int, int>
>     _varDisplayMap` — small per-query mutable maps used by the
>     pretty-printer; carry-forward `rf-dart-map-to-csharp-dictionary`
>     (machine_state.dart.md / runtime.dart.md). Single-owner usage
>     (one Scheduler instance per REPL session); NON-concurrent
>     `Dictionary<int, string>` / `Dictionary<int, int>` — same
>     single-owning-context invariant from heap_fcp.dart.md inherited.
> (e) Dart `void Function(String)? traceSink` — a NULLABLE single-
>     subscriber delegate field; carry-forward
>     `rf-dart-void-function-question-to-csharp-action-nullable`
>     (body_kernels.dart.md / repl_play_runner.dart.md /
>     mad_context.dart.md). Mirrored as `Action<string>? TraceSink
>     { get; set; }`.
> (f) Dart top-level `print` → `System.Console.WriteLine` carry-forward
>     `rf-dart-print-to-csharp-console-writeline` from
>     body_kernels.dart.md — applied at the `_trace` else-branch and
>     at the `[DEBUG] Waiting for ... pending timer(s)...` line in
>     `drainAsyncWithStatus`.
> (g) Dart `Map.putIfAbsent(addr, () => _nextDisplayId++)` — the
>     "lazy-allocate-and-bump-counter" idiom used to assign fresh
>     display IDs. Carry-forward the explicit
>     `if (!dict.TryGetValue(k, out var v)) { v = _next++; dict[k] = v; }`
>     shape from runtime.dart.md (`rf-dart-putifabsent-set-add-to-
>     csharp-tryget-add` adapted to a counter-value).
> (h) Dart `RegExp(r'(\w+)/\d+\(') ` + `replaceAllMapped((m) =>
>     '${m.group(1)}(')` — strip `/arity` suffixes from procedure-name
>     prefixes in trace output. New load-bearing idiom
>     `rf-dart-regexp-replaceallmapped-to-csharp-regex-replace-match-
>     evaluator` (Dart `RegExp.replaceAllMapped` → .NET `Regex.Replace`
>     with `MatchEvaluator` delegate; `\w` / `\d` / capture-group `$1`
>     semantics identical between RE2-ish Dart and .NET ECMAScript
>     defaults for this pattern).
> (i) Dart `Future<DrainResult> drainAsyncWithStatus(...) async { ...
>     await Future.delayed(Duration(milliseconds: 10)); ... }` — the
>     ONE async surface in this file: a poll-with-delay loop waiting
>     for pending timers to fire. Carry-forward of
>     `rf-dart-timer-to-csharp-system-threading-timer` nuance
>     (bytecode/runner.dart.md) AND new specific idiom
>     `rf-dart-future-delayed-to-csharp-task-delay` for the `await
>     Future.delayed(Duration(ms))` → `await Task.Delay(ms)` mapping.
>     Crucially the surrounding sync drain calls are NOT changed to
>     async — `drainWithStatus` remains synchronous; only the polling
>     wrapper carries the `async`/`await`. NO `Stream<T>` /
>     `IAsyncEnumerable<T>` introduced — the source returns a single
>     `Future<DrainResult>`, not a stream of results.
> (j) Dart `dynamic value` parameter in `formatBinding(int varId,
>     dynamic value)` — Dart `dynamic` defers all type-tests to runtime;
>     mirrored as `object?` per the load-bearing `dynamic`→`object?`
>     decision in heap_fcp.dart.md (`rf-dart-dynamic-to-csharp-object`
>     idiom: NOT C# `dynamic` which uses the DLR and pays per-access
>     dispatch cost out of character for a hot-path runtime).
> (k) Dart `Set<int>?` optional positional parameter with `path ??=
>     <int>{}` body-side default — the cycle-detection accumulator;
>     mirrored as `HashSet<int>? path = null` parameter with `path ??=
>     new HashSet<int>();` body assignment. Default-empty-set idiom
>     carry-forward.
> (l) Dart `Map<int, String>.from(suspendedGoals)..removeWhere(...)`
>     cascade — copy-and-filter idiom. Mirrored as `var filtered =
>     suspendedGoals.Where(kv => !rt.InfrastructureGoalIds.Contains(
>     kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);` — the .NET
>     LINQ-canonical "copy + filter" form (carry-forward from
>     body_kernels.dart.md / runtime.dart.md LINQ idiom; alternative
>     in-place `foreach` + `Remove` is rejected because the Dart source
>     intent is to NOT mutate the inner `suspendedGoals` map, which the
>     cascade `Map.from(...)` makes explicit).

```yaml
schema_version: 1
source_path: lib/runtime/scheduler.dart
source_sha256: 6e6b012c5e9b5262847879644915ac2b9e75fd509e8cc5be8f6882a8515f2b03
target_code_unit: lib/runtime/scheduler.cs
constructs:
  - construct_key: dart.import_directive.dart_async_full_and_package_internal_and_relative
    source_form: >-
      Four `import` directives: `import 'dart:async';` (full
      `dart:async` surface, used for `Future`, `Future.delayed`,
      `Duration` in `drainAsyncWithStatus`); `import 'runtime.dart';`
      (relative same-directory sibling, brings `GlpRuntime`,
      `CallEnv`, `GlpChannelHandle` types into scope per
      runtime.dart.md); `import 'package:glp_runtime/bytecode/runner.dart';`
      (package-internal absolute path, brings `BytecodeRunner` /
      `RunnerContext` / `RunResult` per bytecode/runner.dart.md);
      `import 'terms.dart';` (relative same-directory sibling, brings
      `Term` / `VarRef` / `ConstTerm` / `StructTerm` /
      `MutualRefTerm` / `ModuleTerm` per terms.dart.md). No `show` /
      `hide` clauses; no prefix-aliased imports.
    target_decision: >-
      Each Dart package-internal / relative import becomes a .NET
      `using` directive naming the namespace of the converted file:
      `using <root>.Runtime;` covers `GlpRuntime` / `CallEnv` /
      `GlpChannelHandle` / `Term` / `VarRef` / `ConstTerm` /
      `StructTerm` (all sibling `lib/runtime/` files target the same
      namespace) and `using <root>.Bytecode;` for `BytecodeRunner` /
      `RunnerContext` / `RunResult`. The `dart:async` import is split
      across TWO .NET namespaces: `using System.Threading.Tasks;`
      (for `Task` / `Task.Delay` — the `Future` / `Future.delayed`
      counterpart per the carry-forward idiom in
      bytecode/runner.dart.md) and (implicitly) `using System;`
      (for `TimeSpan` — the `Duration` counterpart, see nuance).
      Codegen MUST NOT introduce `System.Threading.Timer` here — this
      file uses `Future.delayed`, NOT `Timer`; the `Timer` mapping
      decision lives in bytecode/runner.dart.md and is NOT exercised
      by `scheduler.dart`. Namespace name decided by the downstream
      depgraph/namespace step. No `show` clause to translate (full
      surface imported).
    idiom_id: null
    research_finding_id: rf-dart-import-relative-to-csharp-using-namespace
    nuance: >-
      Import-unit nuance: Dart imports a *library/file*; C# imports a
      *namespace* — carry-forward from goal_queue.dart.md and
      fairness.dart.md. The `dart:async` import has no single
      namespace counterpart in .NET — it splits across
      `System.Threading.Tasks` (Task / Task.Delay) and `System`
      (TimeSpan); both are BCL-provided, no NuGet package needed.
      Value-vs-reference / null-safety / async / Stream / isolate:
      NOT APPLICABLE — a directive declares no values/types and has
      no runtime form. Reference-identity: NOT APPLICABLE — imports
      do not produce instances. FR-024 cache hit on
      rf-dart-import-relative-to-csharp-using-namespace (NOT
      re-researched).

  - construct_key: dart.enum.plain_marker_three_member_execution_status
    source_form: >-
      `enum ExecutionStatus { succeeded, failed, suspended }` — a
      closed, tag-only Dart enum (no enhanced-enum constructor /
      fields / methods); used as the `DrainResult.status` discriminator
      and the final-status classifier returned by `drainWithStatus`.
      Compared by `==` (e.g. `lastStatus == ExecutionStatus.failed`,
      `status == ExecutionStatus.suspended`). Ordinal positions are
      never observed by code. Doc comments on each member describe the
      drain outcome.
    target_decision: >-
      C# enum `ExecutionStatus` with three members in declaration
      order so underlying integral values are stable (`Succeeded == 0`,
      `Failed == 1`, `Suspended == 2`). Default underlying type
      `int`; no explicit member values needed (no code observes
      ordinals). PascalCase per the .NET enum-naming guideline
      (https://learn.microsoft.com/dotnet/standard/design-guidelines/
      capitalization-conventions) — `succeeded` → `Succeeded`,
      `failed` → `Failed`, `suspended` → `Suspended`. NOT spec-named
      identifiers (unlike `WrtTag` / `RoTag` / `ValueTag` in
      cells.dart.md whose verbatim preservation is load-bearing) —
      the names are user-facing English words, NOT corpus-wide
      trace-string contracts. Lives in the namespace mirroring
      `lib/runtime/`. NOT `[Flags]` — the three values are mutually
      exclusive classifier outputs, not combinable bits. XML doc
      comments preserved per the .NET XML-doc convention.
    idiom_id: null
    research_finding_id: rf-dart-enum-plain-to-csharp-enum
    nuance: >-
      Discriminator nuance (explicitly addressed): `ExecutionStatus`
      is the load-bearing classifier returned by every drain — every
      caller does `if (status == .failed)` / `if (status ==
      .suspended)` style switches. C# enum value-equality `==`
      matches Dart enum `==` exactly. Casing nuance: PascalCase per
      standard .NET naming — NOT preserved verbatim (the source's
      camelCase `succeeded` / `failed` / `suspended` are not
      corpus-wide trace strings; .NET-idiomatic PascalCase is the
      faithful target). Open-vs-closed nuance: this enum has no
      documented intent to grow; no `sealed`/exhaustive-switch
      semantics in Dart (Dart enums are closed by language); any
      `switch (status)` in the .NET target should still include a
      `default` arm that throws `InvalidOperationException` for
      defensiveness (carry-forward from heap_fcp.dart.md `CellTag`
      switch discipline). Null-safety: enum is a value type, never
      null; non-nullable on both sides. Value-vs-reference: value
      type in both languages, identical. Async / Stream / Mixin /
      Sealed: ABSENT — correctly not asserted. Carry-forward of the
      same `rf-dart-enum-plain-to-csharp-enum` idiom used in
      machine_state.dart.md `GoalStatus` and bytecode/runner.dart.md
      `RunResult` (FR-012 cache hit).

  - construct_key: dart.class.drainresult_immutable_value_bundle_no_eq_override_with_optional_positional_default_const_set
    source_form: >-
      `class DrainResult { final List<int> goalsRan; final
      ExecutionStatus status; final List<String> suspendedGoals;
      final Set<int> blockingReaders; DrainResult(this.goalsRan,
      this.status, this.suspendedGoals, [this.blockingReaders = const
      {}]); }` — an IMMUTABLE result bundle. Four `final` fields: a
      `List<int>` of goal IDs that ran in dispatch order; the
      `ExecutionStatus`; a `List<String>` of formatted suspended-goal
      strings; and a `Set<int>` of reader addresses blocking the
      suspended goals (per heap-pointer-architecture-spec.md §8.4).
      Positional constructor with THREE required parameters and ONE
      optional positional parameter `[this.blockingReaders = const
      {}]` — the `const {}` literal is the Dart compile-time-constant
      empty set, used as the default when no blockingReaders is
      passed. NO `==`/`hashCode` override (default identity
      equality). NO `toString` override.
    target_decision: >-
      Map to a reference-type .NET `sealed class DrainResult` (NOT a
      record / record class / record struct / struct). Four
      get-only auto-properties: `public IReadOnlyList<int> GoalsRan
      { get; }`, `public ExecutionStatus Status { get; }`, `public
      IReadOnlyList<string> SuspendedGoals { get; }`, `public
      IReadOnlySet<int> BlockingReaders { get; }`. Single constructor
      with three required parameters and one default-valued
      parameter: `public DrainResult(IReadOnlyList<int> goalsRan,
      ExecutionStatus status, IReadOnlyList<string> suspendedGoals,
      IReadOnlySet<int>? blockingReaders = null)` — the constructor
      body assigns `BlockingReaders = blockingReaders ?? new
      HashSet<int>();` (preserving the "default to empty" semantics
      that the Dart `const {}` provides; .NET has no `const`-set
      literal — the closest faithful counterpart is to lazily allocate
      a fresh empty set per construction, OR to expose a single
      module-level `static readonly IReadOnlySet<int> EmptySet =
      new HashSet<int>();` and default to it, but the per-call fresh
      empty-set allocation is preferred for parity with the Dart
      `const {}` value-equality intent — `const Set<int>{}` in Dart
      is canonicalised to a single shared immutable empty set at
      compile time; the .NET counterpart that EXACTLY matches that
      intent is `System.Collections.Frozen.FrozenSet<int>.Empty` on
      .NET 8+, but since this is a hot path the lazy-fresh-empty
      allocation is acceptable and matches the value-equality intent
      with no shared-mutability hazard). NOT a `record class` (would
      synthesise `Equals`/`GetHashCode` that compare `List<int>` /
      `List<string>` / `Set<int>` BY REFERENCE — not value-equality
      on the contents — which is the SAME equality the source has
      today but is misleading: a `record` advertises "value
      equality" that two distinct `DrainResult` instances with
      identical-CONTENT lists would NOT satisfy without overriding
      list-equality, and the test harnesses around this type compare
      counts / contents via `.GoalsRan.Count` / `.GoalsRan[i]`, not
      via `==`). NOT a `record struct` (would box on every store
      into a `Map<int, DrainResult>`-like collection — none used
      here, but the immutable-bundle-with-collection-members shape
      is more naturally a reference type). NOT a `class` with
      mutable setters (the source's `final` discipline is preserved).
      `sealed` because no Dart subclass and no documented extension.
    idiom_id: null
    research_finding_id: rf-dart-immutable-value-bundle-no-eq-to-csharp-sealed-class-getonly
    nuance: >-
      VALUE-VS-REFERENCE (LOAD-BEARING, explicitly addressed): the
      Dart source declares all-`final` fields BUT NO `==`/`hashCode`
      override — i.e. the equality semantics of `DrainResult` is
      DEFAULT REFERENCE IDENTITY (two distinct `DrainResult`
      instances with identical contents are NOT `==`). A `record
      class` would silently inject value-equality, breaking the
      reference-identity equality the Dart side carries today. A
      `record struct` would copy on every assignment AND box when
      stored as `object` — extra allocation on the return path of
      `drainAsyncWithStatus` (because the local `result.goalsRan`
      etc. are read after the result is returned, by ref in the
      Dart sense). The `sealed class` with get-only auto-properties
      preserves the Dart shape exactly. IMMUTABILITY NUANCE: Dart
      `final List<int>` is "the FIELD is final; the LIST is mutable"
      — the Dart source does not freeze the list contents (`goalsRan`
      is built via `<int>[]` and `add()` then assigned in the
      constructor). The .NET counterpart uses `IReadOnlyList<int>` /
      `IReadOnlySet<int>` for the PUBLIC SURFACE (callers cannot
      mutate the returned list) while letting the constructor accept
      `List<int>` / `HashSet<int>` (the builder code in
      `drainWithStatus` constructs the mutable concrete types and
      passes them in); this preserves the Dart-shape "publicly-
      immutable, internally-built-by-mutation" contract that
      `IReadOnlyList` precisely expresses (Microsoft Learn:
      `IReadOnlyList<T>`). NULL-SAFETY: all four fields non-nullable
      in Dart (no `?` annotations) — same in C# under enabled NRT;
      the constructor parameter `blockingReaders` is nullable
      (default-null) and null-coalesces to a fresh empty set.
      OPTIONAL-POSITIONAL-PARAMETER nuance: Dart `[this.blockingReaders
      = const {}]` is optional-positional with a compile-time-const
      default; the .NET counterpart is a default-valued positional
      parameter `IReadOnlySet<int>? blockingReaders = null` with a
      body-side `?? new HashSet<int>()` (the Dart `const {}` cannot
      be expressed as a C# parameter default — only `null` /
      `default(T)` / literal primitives / `nameof(...)` are valid
      parameter defaults in C#; the null-coalesce form is the
      documented faithful translation, also used in
      machine_state.dart.md `GoalState` ctor and runtime.dart.md
      `GlpRuntime` ctor). CONST-EMPTY-SET SHARING NUANCE (explicitly
      addressed, NOT glossed): Dart's `const {}` is a SHARED
      compile-time-canonicalised empty set — every default-arg call
      site sees the SAME instance. .NET has no compile-time-const
      collection literals; the lazy-fresh per-call allocation is
      semantically acceptable here ONLY because the empty set is
      never mutated (no caller does `result.BlockingReaders.Add(...)`
      — the type is `IReadOnlySet<int>`). If a future code path
      mutates a returned `BlockingReaders`, this MUST be revisited
      (escalate). Alternative: `FrozenSet<int>.Empty` (.NET 8+) is a
      shared immutable empty set — exact semantic match for `const
      Set<int>{}` — but adds a .NET-8 target-framework dependency
      not present elsewhere in the corpus; deferred. Async / Stream
      / Mixin: ABSENT.

  - construct_key: dart.class.scheduler_reference_identity_mutable_per_query_state_with_nullable_trace_sink
    source_form: >-
      `class Scheduler { final GlpRuntime rt; final Map<Object?,
      BytecodeRunner> runners; void Function(String)? traceSink;
      Scheduler({required this.rt, BytecodeRunner? runner,
      Map<Object?, BytecodeRunner>? runners, this.traceSink}) :
      runners = runners ?? (runner != null ? {null: runner} : {});
      Map<int, String> _queryVarNames = {}; Map<int, int>
      _varDisplayMap = {}; int _nextDisplayId = 1; ... }` — the
      Scheduler reference object. Fields: TWO `final` reference
      fields (`rt` — the `GlpRuntime` facade; `runners` — per-program
      `BytecodeRunner` table keyed by `Object?` program identifier);
      ONE mutable nullable single-subscriber delegate (`traceSink`);
      THREE mutable internal pretty-printer state fields
      (`_queryVarNames` mapping writer-addr→original name from query;
      `_varDisplayMap` mapping fresh-var-addr→display number;
      `_nextDisplayId` counter). Single named-only constructor with
      `required this.rt`, optional `runner` (single-program
      convenience param), optional `runners` (multi-program map), and
      optional `traceSink`; initialiser-list builds the runners map
      via ternary on `runner != null` defaulting to `{}` (empty map).
      NO `==`/`hashCode` override (default identity equality).
    target_decision: >-
      Map to a reference-type .NET `class Scheduler` (NOT `sealed`
      necessarily — the Dart class is plain and not declared `final`;
      consistency with `GlpRuntime` from runtime.dart.md which is
      `class` not `sealed class`). Members: `public GlpRuntime Rt
      { get; }` (init-only via ctor, mirroring Dart `final`); `public
      Dictionary<object?, BytecodeRunner> Runners { get; }`
      (init-only — the source has `final` on the field, NOT on the
      map contents; the source mutates the map only through the
      ctor's initialiser-list, never reassigns the reference, so
      `{ get; }` with constructor-assignment is correct; NOT
      `IReadOnlyDictionary` — runtime.dart.md / heap_fcp.dart.md
      surfaces add runners post-construction via
      `rt.runners[program] = runner` which delegates here ONLY if
      this scheduler's `runners` is the canonical table — see nuance
      below); `public Action<string>? TraceSink { get; set; }`
      (mutable nullable delegate property, carry-forward of
      rf-dart-void-function-question-to-csharp-action-nullable from
      runtime.dart.md / repl_play_runner.dart.md). Private state:
      `private Dictionary<int, string> _queryVarNames = new();`
      (mutable; auto-clears via `Clear()` in `SetQueryVarNames` and
      `ResetDisplayNumbering`); `private Dictionary<int, int>
      _varDisplayMap = new();` (mutable); `private int
      _nextDisplayId = 1;` (mutable plain-int counter — carry-forward
      from runtime.dart.md `nextGoalId` public-mutable counter
      idiom, but `private` here because the Dart source has it
      private). Constructor: a SINGLE constructor matching the
      Dart shape but with positional parameters defaulted (the Dart
      named-only constructor maps to a C# positional ctor with
      named-argument call-site convention — carry-forward from
      machine_state.dart.md `GoalState` ctor). Signature: `public
      Scheduler(GlpRuntime rt, BytecodeRunner? runner = null,
      Dictionary<object?, BytecodeRunner>? runners = null,
      Action<string>? traceSink = null) { Rt = rt; Runners = runners
      ?? (runner != null ? new Dictionary<object?, BytecodeRunner>
      { { null!, runner } } : new Dictionary<object?, BytecodeRunner>());
      TraceSink = traceSink; }`. CRITICAL: the `{ null: runner }`
      Dart map literal uses the literal `null` as the Map KEY —
      Dart `Map<Object?, V>` accepts `null` as a bona-fide key, but
      .NET `Dictionary<TKey, TValue>` REJECTS a literal `null` key
      WITH `ArgumentNullException` for reference-type keys (Microsoft
      Learn: `Dictionary<TKey,TValue>.Add(TKey,TValue)`). Same
      load-bearing nuance as runtime.dart.md `rf-dart-null-key-map-
      to-csharp-dictionary-with-comparer-or-sentinel` (NOT a
      previously-named idiom — see escalation nuance below). For
      THIS file's restricted use (the only null-key insertion site
      is the ctor convenience-for-single-program case and the only
      null-key lookup site is `runners[program]` where `program`
      might be `null`), the spec MUST address this AND defer to the
      shared idiom decided in runtime.dart.md construct
      "Map<Object?, BytecodeRunner> runners" (see nuance for
      forwarding). NOT a `record` (would inject value equality
      across `rt` / `runners` / `traceSink` — breaking REPL/test
      identity tests like `scheduler == otherScheduler`); NOT a
      `struct` (mutable fields with reference-aggregation — copy
      semantics would split state). `traceSink` is a public mutable
      property because the REPL / play harness sets it AFTER
      construction (e.g. `scheduler.traceSink = (s) => log.add(s);`).
    idiom_id: null
    research_finding_id: rf-dart-mutable-state-class-identity-equality-to-csharp-class
    nuance: >-
      VALUE-VS-REFERENCE / IDENTITY (LOAD-BEARING, explicitly
      addressed): same identity-equality discipline as `GoalState` /
      `GoalQueue` / `GlpRuntime` / `HeapFCP`. The Scheduler is held
      across drain calls by the caller; mutating its `_queryVarNames`
      / `_varDisplayMap` / `_nextDisplayId` MUST propagate via the
      shared reference. Reference-type `class` is the only correct
      mapping; `record` / `record class` / `struct` / `record struct`
      are ALL categorically rejected — same authoritative rationale
      as `GoalState` (machine_state.dart.md rf-dart-mutable-state-
      class-identity-equality-to-csharp-class — FR-012 cache hit; not
      re-researched). NULL-KEY MAP NUANCE (LOAD-BEARING, explicitly
      addressed): `Map<Object?, BytecodeRunner> runners` uses `null`
      as a bona-fide key in `{null: runner}` (the "default
      single-program" entry). .NET `Dictionary<object?, V>` does NOT
      accept `null` as a key for reference-type `TKey` — `Add(null!,
      v)` throws `ArgumentNullException` per Microsoft Learn
      `Dictionary<TKey,TValue>.Add`. THREE options (escalation-vs-
      decision tree): (i) sentinel-key — replace `null` with a
      private `static readonly object DefaultProgramKey = new();` and
      translate every `runners[null]` / `runners[program]` lookup to
      `runners[program ?? DefaultProgramKey]` (semantically
      faithful, no thrown exception, requires every consumer to use
      the same sentinel — load-bearing across runtime.dart.md too);
      (ii) custom IEqualityComparer<object?> that accepts null —
      .NET allows a custom comparer to define `Equals(null, null)`
      and `GetHashCode(null)` but the dictionary still throws on
      `null` insertion at the Add layer; this option DOES NOT WORK
      (verified per Microsoft Learn: the null-check is in `Add`
      itself, not in the comparer); (iii) parallel field — a
      separate `_defaultRunner: BytecodeRunner?` field PLUS the
      non-null dictionary; lookup becomes `program == null ?
      _defaultRunner : runners[program]`. Decision: OPTION (i)
      sentinel-key is the documented faithful translation, AND it
      must be SHARED across this file AND runtime.dart.md (which
      also has a `Map<Object?, BytecodeRunner> runners` field — same
      type, same null-key usage). The codegen MUST emit a SINGLE
      `internal static readonly object DefaultProgramKey = new
      object();` in the namespace (e.g. as a constant on a
      `RuntimeKeys` static class) and BOTH this file's `Runners` and
      runtime.dart.md's `Runners` MUST agree on it — this is the
      load-bearing cross-file invariant. FR-024 official-docs
      authoritative (Microsoft Learn Dictionary<TKey,TValue>.Add); no
      escalation required because the sentinel pattern is a
      well-known .NET workaround documented in many Microsoft Learn
      articles on "null-tolerant collections". NULL-SAFETY (other):
      `traceSink` nullable delegate matches Dart nullable
      `void Function(String)?` → C# `Action<string>?`. PER-INSTANCE-
      DEFAULT-EMPTY-MAP NUANCE: the source mutates `_queryVarNames`
      and `_varDisplayMap` in place; the field-initialiser literal
      `{}` is per-instance fresh (Dart class field initialisers run
      per constructor invocation, NOT shared across instances — same
      pitfall-avoidance as the `sigmaHat ?? <>{}` idiom in
      machine_state.dart.md). The C# counterpart `= new()` in the
      auto-property/field initialiser is per-instance fresh — exact
      match. Async / Stream / Mixin / Sealed: ABSENT — the class
      has neither `async` methods (drainAsync is a method-level
      `async` but the class itself is not async-typed), nor stream
      surfaces, nor mixins, nor sealed-base inheritance.

  - construct_key: dart.method.set_query_var_names_named_inverse_map_build
    source_form: >-
      `void setQueryVarNames(Map<String, int> varWriters) {
      _queryVarNames.clear(); for (final entry in
      varWriters.entries) { _queryVarNames[entry.value] = entry.key;
      } }` — accepts a forward map (varName → writerAddr) and builds
      the inverse map (writerAddr → varName) into the private
      `_queryVarNames` field. Mutates the field in place (no
      reassignment of the reference).
    target_decision: >-
      Map to `public void SetQueryVarNames(IReadOnlyDictionary<string,
      int> varWriters) { _queryVarNames.Clear(); foreach (var kv in
      varWriters) _queryVarNames[kv.Value] = kv.Key; }` — same
      shape; `Dictionary<int, string>.Clear` matches `Map<int,
      String>.clear`; the `foreach` over `IReadOnlyDictionary<K,V>`
      enumerates `KeyValuePair<K,V>`, same as Dart `varWriters.entries`
      enumerating `MapEntry<K,V>`. PascalCase per .NET naming.
      Parameter type `IReadOnlyDictionary<string, int>` (NOT
      `Dictionary<string, int>`) because the method does not mutate
      the input; .NET-idiomatic input-immutability marker (Microsoft
      Learn: `IReadOnlyDictionary<TKey,TValue>`). NOT a LINQ
      `.ToDictionary(kv => kv.Value, kv => kv.Key)` form (would
      ALLOCATE a fresh dictionary and reassign the field; the source
      explicitly clears-and-mutates the existing instance so other
      observers see the mutation — see runtime.dart.md identity-
      preservation discipline).
    idiom_id: null
    research_finding_id: rf-dart-map-clear-and-fill-inverse-to-csharp-dictionary-clear-foreach
    nuance: >-
      Mutability nuance (LOAD-BEARING): the Dart source explicitly
      mutates `_queryVarNames` in place via `.clear()` + index
      assignment in a loop — does NOT reassign the field reference.
      The .NET port MUST preserve in-place mutation (`Clear()` +
      `_queryVarNames[k] = v` in foreach) so any future observer
      that holds the same dictionary reference (none today, but
      defensively preserved) sees the update. A LINQ `.ToDictionary`
      reassignment would silently break this contract. Value-vs-
      reference: `Dictionary<TKey,TValue>` is a reference type;
      identity preserved. Null-safety: `string` keys non-nullable
      (Dart `String` is non-nullable here — no `?`); `int` values
      non-nullable; no nullable annotations in the signature. Input-
      immutability marker: `IReadOnlyDictionary<K,V>` is the .NET
      idiom for "this method reads the input but does not mutate
      it" — load-bearing because callers (the REPL query setup,
      test harnesses) commonly pass their OWN forward maps that
      they continue to own. Async / Stream: ABSENT.

  - construct_key: dart.method.get_var_display_name_putifabsent_counter_lazy_with_reader_writer_lookup
    source_form: >-
      `String _getVarDisplayName(int addr) { if (rt.heap.isReader(addr))
      { final writerAddr = rt.heap.tryWriterForReader(addr); if
      (writerAddr != null && _queryVarNames.containsKey(writerAddr))
      { return _queryVarNames[writerAddr]!; } } if
      (_queryVarNames.containsKey(addr)) { return
      _queryVarNames[addr]!; } final displayId =
      _varDisplayMap.putIfAbsent(addr, () => _nextDisplayId++);
      return 'X$displayId'; }` — assigns a display name to an
      address: first try writer-name-for-reader, then query-name,
      then lazy-allocate-and-bump-counter for an `Xn` fresh name.
      Uses the `Map.putIfAbsent(key, () => valueFactory)` idiom
      (Dart `Map.putIfAbsent` calls the supplier ONLY if the key is
      absent and returns the resulting value).
    target_decision: >-
      Map to `private string GetVarDisplayName(int addr) { if
      (Rt.Heap.IsReader(addr)) { var writerAddr =
      Rt.Heap.TryWriterForReader(addr); if (writerAddr is int w &&
      _queryVarNames.TryGetValue(w, out var wname)) return wname; }
      if (_queryVarNames.TryGetValue(addr, out var qname)) return
      qname; if (!_varDisplayMap.TryGetValue(addr, out var
      displayId)) { displayId = _nextDisplayId++; _varDisplayMap[addr]
      = displayId; } return $"X{displayId}"; }`. The Dart
      `putIfAbsent` idiom maps to the explicit
      `TryGetValue`+`++`+index-assign shape, carry-forward of
      runtime.dart.md `rf-dart-putifabsent-set-add-to-csharp-tryget-
      add` adapted from `putIfAbsent(..., () => <GoalRef>{})` to the
      counter case `putIfAbsent(..., () => _next++)`. Alternative
      .NET 6+ `CollectionsMarshal.GetValueRefOrAddDefault` is
      rejected — same rationale as runtime.dart.md (portability and
      Dart-shape fidelity over micro-optimisation). The
      `writerAddr is int w` pattern-match extracts the non-null
      `int?` return into a non-nullable `int` — see nuance.
    idiom_id: null
    research_finding_id: rf-dart-putifabsent-counter-to-csharp-tryget-add-and-bump
    nuance: >-
      PUT-IF-ABSENT-COUNTER (LOAD-BEARING, explicitly addressed): the
      Dart `_varDisplayMap.putIfAbsent(addr, () => _nextDisplayId++)`
      idiom does TWO things atomically: (a) check if `addr` is a key;
      (b) if not, call the supplier (which bumps the counter and
      returns its OLD value via `++`), insert that value, and return
      it. The .NET port must preserve both the lazy-call semantics
      (the supplier MUST NOT run if the key is already present —
      otherwise the counter would over-bump for every lookup) AND
      the post-increment semantics (the displayed `X1` corresponds
      to `_nextDisplayId == 1` at the time of allocation; the
      counter ends at `2` after). The explicit `TryGetValue`+`++` is
      the canonical .NET shape (Microsoft Learn:
      `Dictionary<TKey,TValue>.TryGetValue`) — NOT `Dictionary.
      GetOrAdd` (that's `ConcurrentDictionary`, wrong concurrency
      model per the inherited single-owning-context invariant) and
      NOT `GetValueOrDefault` (returns the default `0` without
      inserting — wrong semantics). NULL-RESULT pattern: Dart
      `tryWriterForReader(addr)` returns `int?`; mirror per
      heap_fcp.dart.md `rf-dart-nullable-int-return-with-type-test-
      to-csharp-pattern-match` — the C# `writerAddr is int w` is the
      idiomatic "extract non-null int from Nullable<int>" pattern.
      Value-vs-reference: `int` is a value type in both languages;
      `string` is a reference type but immutable in both — semantics
      identical. Null-safety: `_queryVarNames[k]!` Dart force-unwrap
      after a `containsKey` check maps to `TryGetValue(k, out var v)
      && (use v)` — the .NET pattern that avoids the bang operator
      and is NRT-aware. Interpolation: Dart `'X$displayId'` → C#
      `$"X{displayId}"`. Async / Stream: ABSENT.

  - construct_key: dart.method.reset_display_numbering_clear_three_fields
    source_form: >-
      `void resetDisplayNumbering() { _queryVarNames.clear();
      _varDisplayMap.clear(); _nextDisplayId = 1; }` — clears both
      maps and resets the counter to 1 for a fresh query.
    target_decision: >-
      `public void ResetDisplayNumbering() { _queryVarNames.Clear();
      _varDisplayMap.Clear(); _nextDisplayId = 1; }` — direct
      mapping. `Dictionary<K,V>.Clear()` matches `Map<K,V>.clear()`;
      reassignment of `_nextDisplayId = 1` is plain. PascalCase per
      .NET naming.
    idiom_id: null
    research_finding_id: rf-dart-map-clear-and-counter-reset-to-csharp-dictionary-clear-assign
    nuance: >-
      In-place mutation preserved (same rationale as
      `SetQueryVarNames`). Value-vs-reference / null-safety / async:
      no new nuances. The integer literal `1` is preserved verbatim
      (same as Dart). Trivial idiom but recorded so future
      "reset-multiple-collection-fields" methods reuse it
      (FR-012 reuse over re-derive).

  - construct_key: dart.method.format_term_recursive_with_cycle_detection_named_optional_default_set_and_dereference_loop_and_list_special_case_and_conj_special_case
    source_form: >-
      `String _formatTerm(Term term, {bool markReaders = true,
      Set<int>? path}) { path ??= <int>{}; var current = term; while
      (current is VarRef) { final addr = current.addr; if
      (path.contains(addr)) return '<circular>'; final derefResult =
      rt.heap.derefAddr(addr); if (derefResult is VarRef) break;
      else if (derefResult is Term) { path.add(addr); current =
      derefResult; } else break; } if (current is ConstTerm) { if
      (current.value == 'nil') return '[]'; if (current.value ==
      null) return '<null>'; return current.value.toString(); } else
      if (current is VarRef) { final addr = current.addr; final
      name = _getVarDisplayName(addr); final isReader =
      rt.heap.isReader(addr); return (markReaders && isReader) ?
      '$name?' : name; } else if (current is StructTerm) { if
      (current.functor == '.' && current.args.length == 2) { /* list
      formatting with cycle detection */ } if (current.functor ==
      ',' && current.args.length == 2) { /* conjunction formatting */
      } final args = current.args.map((a) => _formatTerm(a,
      markReaders: markReaders, path: path)).join(', '); return
      '${current.functor}($args)'; } return current.toString(); }` —
      ~90-line recursive term formatter: dereferences VarRef chains
      with cycle detection, then dispatches by type
      (`ConstTerm`/`VarRef`/`StructTerm`) with special-cases for list
      (functor `.` arity 2) and conjunction (functor `,` arity 2).
      Uses a `Set<int>? path` optional named parameter that defaults
      to `<int>{}` via the body's `path ??= <int>{}` null-coalesce
      assignment. Recurses through `current.args.map(...).join(',
      ')`.
    target_decision: >-
      Map to `private string FormatTerm(Term term, bool markReaders =
      true, HashSet<int>? path = null) { path ??= new HashSet<int>();
      var current = term; while (current is VarRef vr) { var addr =
      vr.Addr; if (path.Contains(addr)) return "<circular>"; var
      derefResult = Rt.Heap.DerefAddr(addr); if (derefResult is
      VarRef) break; else if (derefResult is Term t) { path.Add(addr);
      current = t; } else break; } if (current is ConstTerm ct) { ... }
      else if (current is VarRef vr2) { ... } else if (current is
      StructTerm st) { ... } return current.ToString() ?? string.Empty;
      }` — preserves the dereference-loop, the cycle-detection set,
      and the dispatch shape verbatim. The Dart `is`/`as` type-test
      pattern maps to C# `is T name` pattern-match (introduces a
      typed local in the same expression — semantically identical to
      Dart's `current is VarRef` + subsequent `(current as VarRef)`).
      The optional-NAMED parameter `{bool markReaders = true,
      Set<int>? path}` becomes a positional-with-default parameter
      `bool markReaders = true, HashSet<int>? path = null` (Dart
      named optional → C# positional optional; carry-forward from
      machine_state.dart.md `GoalState` ctor). The `path ??= <int>{}`
      body-side default maps to `path ??= new HashSet<int>();`
      (default-empty-set idiom — FRESH per call to avoid sharing).
      Recursion in the list-formatter MUST pass the SAME `path` set
      reference so cycle detection works across nested terms;
      preserved verbatim. The `current.args.map(...).join(', ')`
      maps to `string.Join(", ", current.Args.Select(a =>
      FormatTerm(a, markReaders: markReaders, path: path)))` —
      LINQ-canonical projection + join. The `else return
      current.toString();` final fallback maps to `current.ToString()
      ?? string.Empty` — C# `Object.ToString` is documented to be
      nullable under NRT (Microsoft Learn: `Object.ToString` —
      "It can be null"), so an explicit null-coalesce is required
      to preserve the Dart non-nullable string return contract.
      LINQ `string.Join` is the .NET-canonical counterpart to Dart
      `Iterable.join` (Microsoft Learn: `String.Join(string, IEnumerable
      <string>)`).
    idiom_id: null
    research_finding_id: rf-dart-recursive-term-formatter-with-cycle-detection-to-csharp-recursive-pattern-match
    nuance: >-
      RECURSIVE-FORMATTER-WITH-CYCLE-DETECTION (LOAD-BEARING,
      explicitly addressed): the cycle-detection accumulator `Set<int>
      path` is THREADED THROUGH RECURSIVE CALLS by reference (Dart
      `Set` is a reference type; mutations in one recursion level
      are seen by the caller and by sibling sub-formatters). C# port
      uses `HashSet<int>` (reference type, same threading semantics)
      with the EXACT same parameter-forwarding pattern (`path: path`
      on every recursive call). A common porting mistake would be to
      use `IReadOnlySet<int>` here — that would forbid the
      `path.Add(addr)` mutation; or to use `ImmutableHashSet<int>` —
      that would allocate a new set on every Add, breaking the
      shared-cycle-detection invariant. Both rejected. DEREFERENCE-
      LOOP (LOAD-BEARING): the Dart while-loop dereferences VarRef
      chains UNTIL a fixed point (still-unbound VarRef OR bound to a
      non-VarRef Term), with cycle detection BEFORE the deref call
      (so a cycle is caught BEFORE incrementing the path set). The
      C# port preserves this ordering EXACTLY — `if
      (path.Contains(addr)) return "<circular>";` MUST run BEFORE
      `var derefResult = Rt.Heap.DerefAddr(addr);` and BEFORE
      `path.Add(addr);`. Subtle but load-bearing for correctness.
      LIST SPECIAL-CASE (functor `.` arity 2): the Dart inner loop
      walks the list (functor `.`, two args = head/tail) accumulating
      head-format-strings, with tail being either `nil` ConstTerm
      (end of proper list), another `.` StructTerm (cons-cell
      continuation), a VarRef (partial-list with possibly-circular
      tail — cycle-detected via path), or anything else (improper
      list, formatted with `| <tail>`). The C# port preserves this
      walk structure verbatim — see escalation discussion below on
      whether to inline or factor. CONJUNCTION SPECIAL-CASE (functor
      `,` arity 2): Dart `(left, right)` parentheses-comma form.
      Preserved verbatim. STRING INTERPOLATION: Dart
      `'${current.functor}($args)'` → C# $"{st.Functor}({args})";
      Dart `'[$elements.join(\', \') | $tailStr]'` → C#
      $"[{string.Join(\", \", elements)} | {tailStr}]" — both
      semantically identical (Microsoft Learn: interpolated string
      reference). The `'<circular>'` / `'<null>'` / `'[]'` literal
      strings are preserved verbatim. NULLABLE-OBJECT-TOSTRING: Dart
      `current.value.toString()` on a `ConstTerm.value` that is
      Object? returns a String (Dart `Object.toString` is
      non-nullable); .NET `object.ToString()` under NRT returns
      `string?` per Microsoft Learn — the C# port MUST guard with
      `?? string.Empty` to preserve the non-nullable string return.
      Value-vs-reference for Term hierarchy: all `Term` subtypes are
      reference types per terms.dart.md (cache hit on the term-
      class identity-equality decision). Null-safety: `Set<int>?
      path` (nullable) coalesced to fresh-empty-set; ALL other
      parameters non-nullable. Async / Stream: ABSENT — recursive
      synchronous formatter, no awaits, no streams.

  - construct_key: dart.method.format_goal_args_loop_with_call_env_arg_lookup_and_break_on_null
    source_form: >-
      `String _formatGoal(int goalId, String procName, CallEnv? env)
      { if (env == null) return procName; final args = <String>[];
      for (int i = 0; i < 10; i++) { final arg = env.arg(i); if (arg
      != null) { args.add(_formatTerm(arg)); } else { break; } } if
      (args.isEmpty) return procName; return '$procName(${args.join(',
      ')})'; }` — extracts up to 10 argument terms from a `CallEnv`
      (the bytecode-runner's per-goal argument register file, per
      bytecode/runner.dart.md), stops at the first null, and formats
      `procName(arg1, arg2, ...)`. The hard-coded `10` reflects the
      max-arity register-file scan; nulls indicate "no more args".
    target_decision: >-
      `private string FormatGoal(int goalId, string procName, CallEnv?
      env) { if (env == null) return procName; var args = new
      List<string>(); for (int i = 0; i < 10; i++) { var arg =
      env.Arg(i); if (arg != null) args.Add(FormatTerm(arg)); else
      break; } if (args.Count == 0) return procName; return
      $"{procName}({string.Join(", ", args)})"; }` — direct shape
      mapping. `CallEnv?` (nullable reference) preserves Dart
      nullable. `env.arg(i)` becomes `env.Arg(i)` (PascalCase per .NET
      method naming). The hard-coded `10` is preserved verbatim
      (it's an implementation detail tied to bytecode/runner.dart.md's
      `CallEnv` register-file size; codegen must not refactor to a
      named constant unless the upstream `CallEnv` spec exposes one).
      `args.isEmpty` → `args.Count == 0` per the `Queue<T>` /
      `List<T>` `IsEmpty`-shortcut absence (.NET `List<T>` exposes
      `Count`, not an `IsEmpty` getter — same nuance as
      machine_state.dart.md `GoalQueue.IsEmpty` mapping).
      `args.join(', ')` → `string.Join(", ", args)`.
    idiom_id: null
    research_finding_id: rf-dart-args-scan-with-nullable-and-join-to-csharp-list-loop-and-stringjoin
    nuance: >-
      Mutable-list-builder pattern (LOAD-BEARING, explicitly
      addressed): the Dart `args = <String>[]` then `.add(...)` then
      `.join(...)` is the canonical "accumulate-then-join" idiom; C#
      counterpart `new List<string>()` + `.Add(...)` + `string.Join`
      is the canonical .NET form. Alternative: a `StringBuilder`
      directly accumulating — REJECTED here for source-shape
      fidelity AND because `string.Join` is more efficient for the
      small-N (≤10) case and avoids manual separator-handling. The
      hard-coded `10` is a CallEnv register-file artefact; preserving
      it verbatim avoids inventing a named constant on the .NET side
      that the upstream `CallEnv` does not expose. Nullable-return-
      and-break pattern: `arg.arg(i)` returns `Term?` (the register
      may be unset); the loop stops at the first null — load-bearing
      because the register-file is contiguously filled and a null
      indicates end-of-args. The C# port preserves the `if (arg !=
      null) ... else break;` shape; alternative `while (arg != null)`
      with separate iteration index is rejected for source-shape
      fidelity. Value-vs-reference: `Term?` nullable reference; the
      list stores non-null formatted strings. Null-safety: ENV
      nullable `CallEnv?`, ARG nullable `Term?` — both preserved.
      Async / Stream: ABSENT.

  - construct_key: dart.method.format_binding_dynamic_value_dispatch_with_const_wrapper_strip
    source_form: >-
      `String formatBinding(int varId, dynamic value) { final name =
      _getVarDisplayName(varId); String valueStr; if (value is Term)
      { valueStr = _formatTerm(value, markReaders: false); } else if
      (value is String) { valueStr = value; } else if (value == null
      || value == 'nil') { valueStr = '[]'; } else { valueStr =
      value.toString(); } if (valueStr.startsWith('Const(') &&
      valueStr.endsWith(')')) { valueStr = valueStr.substring(6,
      valueStr.length - 1); } return '$name = $valueStr'; }` —
      formats `X = value` lines; takes a `dynamic value` and
      dispatches via type-test to format. Strips `Const(...)`
      wrappers (legacy ConstTerm string form) via prefix-suffix-
      check-and-substring.
    target_decision: >-
      `public string FormatBinding(int varId, object? value) { var
      name = GetVarDisplayName(varId); string valueStr; if (value is
      Term t) valueStr = FormatTerm(t, markReaders: false); else if
      (value is string s) valueStr = s; else if (value == null ||
      object.Equals(value, "nil")) valueStr = "[]"; else valueStr =
      value.ToString() ?? string.Empty; if
      (valueStr.StartsWith("Const(") && valueStr.EndsWith(")"))
      valueStr = valueStr[6..^1]; return $"{name} = {valueStr}"; }`.
      The `dynamic value` parameter maps to `object?` per the
      load-bearing `dynamic`→`object?` decision in heap_fcp.dart.md
      (`rf-dart-dynamic-to-csharp-object` — FR-012 cache hit; NOT
      C# `dynamic` which uses the DLR). The `value is Term` / `value
      is String` type-tests map to C# `is T name` pattern-match
      (same as `FormatTerm`). The `value == 'nil'` Dart string
      equality on an `Object?` carries through to .NET as
      `object.Equals(value, "nil")` — required because under NRT the
      `==` operator on `object?` performs reference identity, not
      structural string equality (Microsoft Learn:
      `Object.Equals(Object, Object)` — "the canonical way to
      compare two arbitrary object references for value equality").
      Alternative `value is string ns && ns == "nil"` is
      semantically equivalent but more verbose; `object.Equals` is
      the documented faithful idiom. The `valueStr.substring(6,
      valueStr.length - 1)` Dart call (end-exclusive 1-arg-less
      variant: `substring(start, end)` excludes end) maps to C#
      RANGE-INDEXER `valueStr[6..^1]` (.NET 8+/C# 8+, half-open
      range, ^1 = "one from the end" exclusive of the last char) —
      Microsoft Learn: "Indices and ranges". The range-indexer is
      semantically EXACT for the Dart shape (`length - 1` exclusive
      end = `^1`). Alternative `Substring(6, valueStr.Length - 7)`
      arithmetic form is correct but less idiomatic.
    idiom_id: null
    research_finding_id: rf-dart-dynamic-typed-format-with-prefix-suffix-strip-to-csharp-object-pattern-and-range-index
    nuance: >-
      DYNAMIC-VS-OBJECT NUANCE (LOAD-BEARING, explicitly addressed):
      Dart `dynamic value` defers type-checking to runtime; the C#
      counterpart is `object?` with pattern-match dispatch (NOT C#
      `dynamic` which uses the DLR — wrong cost model, wrong error-
      surface). This is the cache hit on heap_fcp.dart.md
      rf-dart-dynamic-to-csharp-object (FR-012; not re-researched).
      OBJECT-EQUALITY ON STRING LITERAL: Dart `value == 'nil'` works
      because Dart `Object.==` calls the receiver's `==` operator
      which for `String` is structural; C# `value == "nil"` on an
      `object?` performs REFERENCE identity (since `value` is
      typed `object?`, the compiler picks `object.operator ==` which
      is reference-equal). The .NET port MUST use
      `object.Equals(value, "nil")` to preserve the Dart STRUCTURAL
      equality. Alternative `value is string s && s == "nil"`
      preserves the structural equality but adds a redundant
      pattern-match — both are correct; `object.Equals` is more
      direct. SUBSTRING SLICING (LOAD-BEARING NUANCE): Dart
      `String.substring(start, end)` is "characters from start
      (inclusive) to end (exclusive)" matching C# range-indexer
      `[start..end]` — Microsoft Learn: "String.Substring(Int32,
      Int32)" documents the 2-arg form as `(startIndex, length)` —
      DIFFERENT FROM Dart. The 2-arg `Substring(6, valueStr.Length -
      7)` arithmetic is needed if NOT using the range indexer. The
      RANGE-INDEXER form `valueStr[6..^1]` is the cleaner faithful
      translation AND avoids the start/end-vs-start/length pitfall.
      Trivia: the `^1` means "1 from the end, exclusive" — exactly
      matching Dart's `valueStr.length - 1`. NULL-SAFETY: `value`
      nullable (`object?`); `value.ToString()` nullable return under
      NRT — guarded by `?? string.Empty`. String literals are
      non-nullable in both languages. `StartsWith` / `EndsWith` are
      .NET-BCL methods on `string` (Microsoft Learn:
      `String.StartsWith(String)`) — direct counterparts of Dart
      `String.startsWith` / `String.endsWith`. Async / Stream:
      ABSENT.

  - construct_key: dart.method.trace_sink_dispatch_with_print_fallback
    source_form: >-
      `void _trace(String line) { if (traceSink != null) {
      traceSink!(line); } else { print(line); } }` — single-line
      trace dispatch: if a sink is set, call it; else use top-level
      `print`. The `traceSink!(line)` uses the bang operator to
      assert non-null after the explicit null check.
    target_decision: >-
      `private void Trace(string line) { var cb = TraceSink; if (cb !=
      null) cb(line); else System.Console.WriteLine(line); }`.
      Carry-forward of two idioms: (1) snapshot-then-invoke nullable-
      delegate (load-bearing for NRT under multi-thread settings —
      see mad_context.dart.md TraceSink discipline: `var cb =
      OnMessageReady; if (cb == null) return; cb.Invoke(...)`),
      faithfully preserving the Dart `traceSink!(line)` no-race
      semantics (Dart is single-isolate; .NET under the inherited
      single-owning-context invariant is also race-free — but the
      snapshot pattern is the .NET-idiomatic NRT shape). (2) Dart
      `print` → `System.Console.WriteLine` carry-forward
      `rf-dart-print-to-csharp-console-writeline` (body_kernels.dart
      .md). The `!` Dart force-unwrap is REMOVED in C# — the local
      `cb` is already non-null after the `!= null` check.
    idiom_id: null
    research_finding_id: rf-dart-void-function-question-to-csharp-action-nullable
    nuance: >-
      NULLABLE-DELEGATE INVOKE NUANCE (LOAD-BEARING, explicitly
      addressed): Dart `traceSink!(line)` works only because Dart
      flow analysis narrows the type after the null check; the bang
      operator asserts non-null at runtime (would throw on null,
      which the null-check guarantees doesn't happen). C# under NRT
      uses the snapshot-then-invoke pattern to (a) prevent races
      between the null-check and the invoke if a sibling thread
      nullifies the field, AND (b) provide cleaner NRT inference
      (the local `cb` is unambiguously non-null after the check).
      Same exact discipline as mad_context.dart.md TraceSink (cache
      hit on rf-dart-void-function-question-to-csharp-action-
      nullable — FR-012). PRINT TO STDOUT: Dart top-level `print` is
      a `dart:core` function writing to stdout with a trailing
      newline; `System.Console.WriteLine` is the documented .NET
      counterpart (Microsoft Learn: `Console.WriteLine(String)`)
      with identical stdout + newline semantics. Cache hit on
      rf-dart-print-to-csharp-console-writeline (body_kernels.dart
      .md — FR-012; not re-researched). Async / Stream / Mixin:
      ABSENT.

  - construct_key: dart.method.drain_with_status_sync_dispatch_loop_with_max_cycles_and_reduction_callback_and_status_classification_and_blocking_readers
    source_form: >-
      `DrainResult drainWithStatus({int maxCycles = 1000, bool debug
      = false, bool showBindings = true, bool debugOutput = false}) {
      final ran = <int>[]; final suspendedGoals = <int, String>{}; var
      cycles = 0; var hasFailed = false; while (rt.gq.length > 0 &&
      cycles < maxCycles) { final act = rt.gq.dequeue(); if (act ==
      null) break; ran.add(act.id); ... var runner = runners[program];
      runner ??= rt.runners[program]; if (runner == null) throw
      StateError(...); ... final cx = RunnerContext(rt: rt, goalId:
      act.id, kappa: act.pc, env: env, goalHead: goalStr,
      goalProcName: procName, showBindings: showBindings, debugOutput:
      debugOutput, moduleContext: moduleContext, termFormatter:
      (term, {bool markReaders = true}) => _formatTerm(term,
      markReaders: markReaders), onReduction: (goalId, head, body) {
      ... if (debug) { ... _trace('$cleanHead :- $cleanBody'); }
      hadReduction = true; suspendedGoals.remove(goalId); }, ); final
      result = runner.runWithStatus(cx); if (result ==
      RunResult.suspended) { suspendedGoals[act.id] = goalStr; ... }
      else if (result == RunResult.terminated) { ... if (!hadReduction
      && !isQueryWrapper) { ... hasFailed = true; break; } ... }
      cycles++; } final userSuspendedGoals = Map<int, String>.from(
      suspendedGoals)..removeWhere((goalId, _) =>
      rt.infrastructureGoalIds.contains(goalId)); final ExecutionStatus
      status; if (hasFailed) status = .failed; else if
      (userSuspendedGoals.isNotEmpty) status = .suspended; else status
      = .succeeded; ... final blockingReaders = status ==
      ExecutionStatus.suspended ? rt.suspended.keys.toSet() : <int>{};
      return DrainResult(ran, status, suspendedList, blockingReaders);
      }` — ~130-line CENTRAL drain loop: dequeues activations from
      `rt.gq`, looks up the per-program runner, builds a
      `RunnerContext` (with lambdas for `termFormatter` and
      `onReduction`), dispatches via `runner.runWithStatus(cx)`,
      classifies the run result, tracks suspended-goal map,
      classifies final status, and computes blocking-reader set from
      `rt.suspended.keys`. Uses cascade `..removeWhere(...)` to
      filter infrastructure goals from the suspended map.
    target_decision: >-
      Map to `public DrainResult DrainWithStatus(int maxCycles =
      1000, bool debug = false, bool showBindings = true, bool
      debugOutput = false) { var ran = new List<int>(); var
      suspendedGoals = new Dictionary<int, string>(); var cycles = 0;
      var hasFailed = false; while (Rt.Gq.Length > 0 && cycles <
      maxCycles) { var act = Rt.Gq.Dequeue(); if (act == null) break;
      ran.Add(act.Value.Id); var env = Rt.GetGoalEnv(act.Value.Id);
      var program = Rt.GetGoalProgram(act.Value.Id); var runner =
      Runners.TryGetValue(program ?? DefaultProgramKey, out var r1) ?
      r1 : null; runner ??= Rt.Runners.TryGetValue(program ??
      DefaultProgramKey, out var r2) ? r2 : null; if (runner == null)
      throw new InvalidOperationException($"No runner found for
      program {program} for goal {act.Value.Id}"); ... var goalStr =
      FormatGoal(act.Value.Id, procName, env); ... var hadReduction =
      false; var moduleContext = Rt.GetGoalModuleContext(act.Value
      .Id); var cx = new RunnerContext(rt: Rt, goalId: act.Value.Id,
      kappa: act.Value.Pc, env: env, goalHead: goalStr, goalProcName:
      procName, showBindings: showBindings, debugOutput: debugOutput,
      moduleContext: moduleContext, termFormatter: (term,
      markReaders) => FormatTerm(term, markReaders), onReduction:
      (goalId, head, body) => { ... if (debug) { var cleanHead =
      StripArityRegex.Replace(head, m => $"{m.Groups[1].Value}(");
      var cleanBody = StripArityRegex.Replace(body, m =>
      $"{m.Groups[1].Value}("); Trace($"{cleanHead} :- {cleanBody}");
      } hadReduction = true; suspendedGoals.Remove(goalId); }); var
      result = runner.RunWithStatus(cx); if (result ==
      RunResult.Suspended) { suspendedGoals[act.Value.Id] = goalStr;
      ... } else if (result == RunResult.Terminated) { ... if
      (!hadReduction && !isQueryWrapper) { ... hasFailed = true;
      break; } ... } cycles++; } var userSuspendedGoals =
      suspendedGoals.Where(kv =>
      !Rt.InfrastructureGoalIds.Contains(kv.Key))
      .ToDictionary(kv => kv.Key, kv => kv.Value); ExecutionStatus
      status; if (hasFailed) status = ExecutionStatus.Failed; else
      if (userSuspendedGoals.Count > 0) status =
      ExecutionStatus.Suspended; else status = ExecutionStatus
      .Succeeded; var suspendedList = userSuspendedGoals.Values
      .Select(g => StripArityRegex.Replace(g, m =>
      $"{m.Groups[1].Value}(")).ToList(); var blockingReaders = status
      == ExecutionStatus.Suspended ? new HashSet<int>(Rt.Suspended
      .Keys) : new HashSet<int>(); return new DrainResult(ran, status,
      suspendedList, blockingReaders); }`. CRITICAL load-bearing
      pieces preserved: (a) `Rt.Gq.Dequeue()` returns `GoalRef?` per
      machine_state.dart.md (the `Dequeue() => Count==0 ? null : ...`
      contract); `act.Value.Id` is the `Nullable<GoalRef>.Value`
      unwrap (since `GoalRef` is a `readonly record struct` per
      machine_state.dart.md, accessing fields requires `.Value`);
      (b) the program-key lookup uses the `DefaultProgramKey`
      sentinel from the Scheduler-ctor nuance to handle `program ==
      null` (the source's `runners[null]` for the single-program
      case); (c) the `RunnerContext` constructor takes named-or-
      positional arguments per bytecode/runner.dart.md
      `RunnerContext` ctor decisions (using C# named-argument
      call-site convention); (d) the `termFormatter` lambda
      preserves the `bool markReaders = true` optional parameter as
      a positional default; (e) the `onReduction` lambda captures
      `debug`, `hadReduction` (via the surrounding closure — `bool
      hadReduction` MUST be a class-level local-state variable
      reachable from the lambda; in C# this is achieved by
      declaring `var hadReduction = false;` in the enclosing scope
      AND the lambda mutates it via capture — note that capturing
      a value-type local from a lambda in C# requires the local to
      be reachable as a closure-captured variable, which the
      compiler handles via a synthetic display class — semantically
      identical to Dart's lexical capture); (f) the
      `StripArityRegex` is a `private static readonly Regex
      StripArityRegex = new(@"(\w+)/\d+\(");` declared at class
      level (Microsoft Learn: prefer pre-compiled static Regex
      instances for repeated use). (g) the cascade `Map.from(
      suspendedGoals)..removeWhere(...)` maps to the LINQ
      `.Where(...).ToDictionary(...)` form which ALSO yields a
      FRESH dictionary that does NOT alias `suspendedGoals` — load-
      bearing because the Dart cascade copies AND mutates the COPY,
      not the original; the LINQ form is equivalent because LINQ
      `.ToDictionary` allocates a fresh dictionary. (h)
      `rt.suspended.keys.toSet()` maps to `new HashSet<int>(
      Rt.Suspended.Keys)` — fresh `HashSet<int>` constructed from
      an `IEnumerable<int>` (Microsoft Learn:
      `HashSet<T>.HashSet(IEnumerable<T>)`).
    idiom_id: null
    research_finding_id: rf-dart-central-drain-loop-with-closure-capture-and-status-classification-to-csharp-while-loop-with-display-class
    nuance: >-
      DEQUEUE NULLABLE RETURN (LOAD-BEARING, explicitly addressed):
      `Rt.Gq.Dequeue()` returns `GoalRef?` per machine_state.dart
      .md (the explicit `Count==0 ? null : _q.Dequeue()` to preserve
      Dart's nullable-on-empty contract — NOT the .NET throw-on-
      empty `Queue<T>.Dequeue`). Since `GoalRef` is a `readonly
      record struct` (a value type), `GoalRef?` is `Nullable<GoalRef>`
      and field access goes via `.Value.Id` / `.Value.Pc`. The
      C# port MUST guard with `if (act == null) break;` BEFORE
      accessing `.Value` (same as the Dart `if (act == null)
      break;`). NULL-KEY DICTIONARY LOOKUP (LOAD-BEARING, explicitly
      addressed — cache hit on Scheduler-ctor nuance): every
      `runners[program]` and `Rt.Runners[program]` lookup uses the
      `DefaultProgramKey` sentinel substitution `program ??
      DefaultProgramKey` because .NET `Dictionary<object?, V>` does
      not accept `null` as a key. CLOSURE-CAPTURE MUTABLE-LOCAL
      NUANCE (explicitly addressed, NOT glossed): the `hadReduction
      = true; suspendedGoals.Remove(goalId);` inside the
      `onReduction` lambda CAPTURES THE ENCLOSING LOCAL `hadReduction`
      AND THE ENCLOSING `suspendedGoals` Dictionary. Dart closures
      capture by reference (the lambda sees the outer variable's
      current value AND mutations propagate back to the outer scope
      after the lambda returns). C# closures also capture by
      reference for local variables (Microsoft Learn: "Captured
      variables" — "The compiler captures the variable's storage
      location"). Verified: `hadReduction = true` inside the lambda
      DOES update the outer `hadReduction` local — semantically
      identical to Dart. CASCADE COPY-AND-FILTER NUANCE (LOAD-
      BEARING): Dart `Map<int, String>.from(suspendedGoals)..
      removeWhere((goalId, _) => rt.infrastructureGoalIds.contains(
      goalId))` is "make a COPY, then mutate the copy via
      `removeWhere`, returning the mutated copy" — the original
      `suspendedGoals` is NOT mutated (load-bearing because the
      outer scope may still iterate the original). The LINQ
      `.Where(predicate).ToDictionary(kv => kv.Key, kv => kv.Value)`
      form yields a FRESH dictionary that excludes the filtered
      entries — semantically equivalent AND idiomatic .NET. An
      in-place `foreach (var key in toRemove) suspendedGoals
      .Remove(key);` is REJECTED because it would mutate the
      ORIGINAL `suspendedGoals`, silently changing the Dart contract.
      MAX-CYCLES BUDGET: the `int maxCycles = 1000` default is
      preserved verbatim (carry-forward of the .NET-preserves-
      magic-numbers discipline from machine_state.dart.md
      TailRecursionBudgetInit). REGEX (LOAD-BEARING, explicitly
      addressed): the `RegExp(r'(\w+)/\d+\(')` Dart pattern is a
      "raw" regex literal — `r'...'` disables backslash escape
      processing inside the string, so `\w` and `\d` reach the
      regex engine unescaped. The .NET counterpart MUST use either
      a verbatim string `@"(\w+)/\d+\("` OR a regular string with
      doubled backslashes `"(\\w+)/\\d+\\("` — Microsoft Learn:
      "Regular expression language - quick reference" confirms `\w`
      = word char, `\d` = digit — same semantics as Dart's RE2-ish
      `\w` / `\d` for this pattern. The `replaceAllMapped((m) =>
      '${m.group(1)}(')` Dart match-evaluator maps to
      `Regex.Replace(input, m => $"{m.Groups[1].Value}(")` (Microsoft
      Learn: `Regex.Replace(String, MatchEvaluator)`). `m.Groups[1]
      .Value` is the .NET counterpart of Dart `m.group(1)` — the
      first capture group's matched substring. STATIC PRE-COMPILED
      REGEX: declared once as `private static readonly Regex
      StripArityRegex = new(@"(\w+)/\d+\(");` to avoid recompilation
      on every drain (Microsoft Learn: "Best practices for regular
      expressions" — "If you're going to use the same regular
      expression repeatedly, create a Regex object once"). RUNRESULT
      ENUM COMPARE: `RunResult.suspended` / `RunResult.terminated`
      → `RunResult.Suspended` / `RunResult.Terminated` (PascalCase
      per bytecode/runner.dart.md enum mapping). STATEERROR →
      INVALIDOPERATIONEXCEPTION: carry-forward of the standard
      Dart→.NET exception mapping from heap_fcp.dart.md
      `rf-dart-stateerror-to-csharp-invalidoperationexception`
      (FR-012; not re-researched). Async / Stream: ABSENT — this
      method is SYNCHRONOUS. Identity-preserving outputs: the
      returned `DrainResult` aggregates the freshly-allocated `ran`,
      `suspendedList`, and `blockingReaders` collections; callers
      receive them via `IReadOnly...` surfaces per the DrainResult
      ctor signature.

  - construct_key: dart.method.drain_legacy_delegating_to_drain_with_status_and_returning_goals_ran_only
    source_form: >-
      `List<int> drain({int maxCycles = 1000, bool debug = false,
      bool showBindings = true, bool debugOutput = false}) { return
      drainWithStatus(maxCycles: maxCycles, debug: debug,
      showBindings: showBindings, debugOutput: debugOutput).goalsRan;
      }` — legacy back-compat method that drops everything except
      the goals-ran list. Documented as "Legacy drain for backward
      compatibility".
    target_decision: >-
      `public IReadOnlyList<int> Drain(int maxCycles = 1000, bool
      debug = false, bool showBindings = true, bool debugOutput =
      false) => DrainWithStatus(maxCycles, debug, showBindings,
      debugOutput).GoalsRan;` — expression-bodied delegate to
      `DrainWithStatus` returning `.GoalsRan` (the `IReadOnlyList<int>`
      surface decided on `DrainResult`). Same defaults preserved
      verbatim. PascalCase per .NET naming. NO surface change other
      than the `IReadOnlyList<int>` vs `List<int>` return type
      (carry-forward of the DrainResult immutability decision —
      consumers were already supposed to treat the returned list as
      read-only since Dart `final List<int>` exposes a mutable list
      reference but the source convention is read-only).
    idiom_id: null
    research_finding_id: rf-dart-named-arg-delegating-method-to-csharp-positional-expression-bodied-delegate
    nuance: >-
      Named-vs-positional argument forwarding nuance: Dart
      `drainWithStatus(maxCycles: maxCycles, ...)` named-argument
      forwarding maps to C# positional argument forwarding (the
      defaults are aligned by parameter order). Both languages
      preserve the default values at the forwarding call site. The
      expression-bodied form `=>` mirrors the Dart `return ...;`
      single-expression body. Mutability-vs-immutability return
      type: see DrainResult nuance above. Async / Stream: ABSENT.

  - construct_key: dart.method.drain_async_with_status_async_await_future_delayed_poll_loop_with_pending_timers
    source_form: >-
      `Future<DrainResult> drainAsyncWithStatus({int maxCycles =
      1000, bool debug = false, bool showBindings = true, bool
      debugOutput = false}) async { final ran = <int>[]; var
      totalCycles = 0; ExecutionStatus lastStatus =
      ExecutionStatus.succeeded; List<String> lastSuspended = [];
      Set<int> lastBlockingReaders = {}; while (totalCycles <
      maxCycles) { final result = drainWithStatus(maxCycles:
      maxCycles - totalCycles, debug: debug, showBindings:
      showBindings, debugOutput: debugOutput); ran.addAll(result
      .goalsRan); totalCycles += result.goalsRan.length; lastStatus =
      result.status; lastSuspended = result.suspendedGoals;
      lastBlockingReaders = result.blockingReaders; if (lastStatus ==
      ExecutionStatus.failed) break; if (rt.pendingTimers <= 0)
      break; if (debugOutput) print('[DEBUG] Waiting for ${rt
      .pendingTimers} pending timer(s)...'); while (rt.gq.length ==
      0 && rt.pendingTimers > 0 && totalCycles < maxCycles) { await
      Future.delayed(Duration(milliseconds: 10)); } } return
      DrainResult(ran, lastStatus, lastSuspended,
      lastBlockingReaders); }` — async polling wrapper around
      synchronous `drainWithStatus`: runs sync drain, checks pending
      timers, waits 10 ms if queue empty and timers pending, repeats.
      Uses `Future.delayed(Duration(milliseconds: 10))` as the
      polling tick.
    target_decision: >-
      Map to `public async Task<DrainResult> DrainAsyncWithStatus(int
      maxCycles = 1000, bool debug = false, bool showBindings =
      true, bool debugOutput = false) { var ran = new List<int>(); var
      totalCycles = 0; ExecutionStatus lastStatus = ExecutionStatus
      .Succeeded; IReadOnlyList<string> lastSuspended = Array.Empty<
      string>(); IReadOnlySet<int> lastBlockingReaders = new
      HashSet<int>(); while (totalCycles < maxCycles) { var result =
      DrainWithStatus(maxCycles - totalCycles, debug, showBindings,
      debugOutput); ran.AddRange(result.GoalsRan); totalCycles +=
      result.GoalsRan.Count; lastStatus = result.Status; lastSuspended
      = result.SuspendedGoals; lastBlockingReaders = result
      .BlockingReaders; if (lastStatus == ExecutionStatus.Failed)
      break; if (Rt.PendingTimers <= 0) break; if (debugOutput)
      System.Console.WriteLine($"[DEBUG] Waiting for
      {Rt.PendingTimers} pending timer(s)..."); while (Rt.Gq.Length
      == 0 && Rt.PendingTimers > 0 && totalCycles < maxCycles) {
      await Task.Delay(10); } } return new DrainResult(ran,
      lastStatus, lastSuspended, lastBlockingReaders); }`.
      `Future<T>` → `Task<T>` (Microsoft Learn: "Asynchronous
      programming with async and await" — `Task` is the standard
      .NET counterpart of Dart's `Future`). `Future.delayed(Duration(
      milliseconds: 10))` → `Task.Delay(10)` (Microsoft Learn:
      `Task.Delay(Int32)` — "Creates a task that completes after a
      specified number of milliseconds" — the int-overload accepts
      milliseconds directly, no need for `TimeSpan.FromMilliseconds`
      construction; semantically identical to Dart `Future.delayed(
      Duration(milliseconds: 10))`). The TimeSpan-overload
      `Task.Delay(TimeSpan.FromMilliseconds(10))` is also correct
      but the int-overload is more concise; both compile to the
      same IL. `addAll` → `AddRange`. `result.goalsRan.length` →
      `result.GoalsRan.Count`. The `print('[DEBUG] ...')` →
      `System.Console.WriteLine($"[DEBUG] ...")` carry-forward.
      Initialise `lastSuspended` to `Array.Empty<string>()` instead
      of `new List<string>()` (Microsoft Learn: `Array.Empty<T>` —
      shared zero-allocation empty array) AND `lastBlockingReaders`
      to `new HashSet<int>()` (no shared empty set in .NET pre-8 —
      see DrainResult nuance). Both compatible with the
      `IReadOnly...` surfaces. Cancellation: NO `CancellationToken`
      parameter introduced — the Dart source has no cancellation
      surface, and adding one would expand the API contract; if
      future callers need cancellation, ESCALATE then (FR-013), do
      not silently add. ConfigureAwait: NOT used here — the
      surrounding test/REPL harness does not require library-
      style ConfigureAwait(false), and the inherited single-owning-
      context invariant means continuations should resume on the
      owning context (the default), NOT on the ThreadPool. This is
      the same threading-model decision INHERITED from
      heap_fcp.dart.md escalations[0] / bytecode/runner.dart.md
      Timer nuance.
    idiom_id: null
    research_finding_id: rf-dart-future-delayed-to-csharp-task-delay
    nuance: >-
      ASYNC SURFACE (LOAD-BEARING, explicitly addressed): the
      `Future<DrainResult>` return type + `async` modifier + `await
      Future.delayed(...)` is the ONLY async surface in this entire
      file. The conversion MUST preserve the async surface
      (`Task<DrainResult>` + `async` + `await Task.Delay(...)`) so
      callers of `DrainAsync` (await the result) continue to work.
      Adding `async`/`await` here is NOT inventing async semantics —
      the source already has them. Microsoft Learn: "Asynchronous
      programming with async and await" documents `Task<T>` as the
      direct counterpart of Dart `Future<T>`. FUTURE.DELAYED →
      TASK.DELAY (LOAD-BEARING, explicitly addressed): Dart
      `Future.delayed(Duration(milliseconds: 10))` returns a
      `Future<void>` that completes after 10 ms; .NET
      `Task.Delay(10)` returns a `Task` that completes after 10 ms
      — exact semantic match (Microsoft Learn: `Task.Delay(Int32)`).
      DURATION (LOAD-BEARING NUANCE): Dart `Duration(milliseconds:
      10)` is a `Duration` value (an immutable interval); .NET
      counterpart for "milliseconds interval" passed to
      `Task.Delay` can be EITHER `int` (the int-overload of
      `Task.Delay(Int32)`) OR `TimeSpan.FromMilliseconds(10)`. The
      int-overload is preferred for conciseness AND because the
      Dart source uses an integer literal for the milliseconds
      value; the TimeSpan-overload would invent a typed-Duration
      wrapper not present in the source. POLL-WITH-DELAY LOOP
      (LOAD-BEARING): the inner `while (rt.gq.length == 0 && rt
      .pendingTimers > 0 && ...) { await Future.delayed(...); }`
      polls for pending timers to fire; the .NET counterpart
      preserves the EXACT shape — `while (Rt.Gq.Length == 0 &&
      Rt.PendingTimers > 0 && ...) { await Task.Delay(10); }`. No
      `CancellationToken`; no `ConfigureAwait(false)` (see decision
      above for the threading-model inherited rationale). CAVEAT
      ON 10 MS POLL: the 10 ms is preserved verbatim from the
      source (the literal `10`); under heavy timer-driven workloads
      this is a busy-poll with 10 ms granularity — same as Dart.
      Refactoring to a real timer-completion source would change
      semantics; preserved as-is. NO STREAM SURFACE: the method
      returns a single `Task<DrainResult>`, NOT an `IAsyncEnumerable
      <DrainResult>` — there is no per-step yielding in the Dart
      source; preserved. THREADING-MODEL INHERITANCE: the
      `Task.Delay` continuation resumes on the captured
      `SynchronizationContext` by default — under the inherited
      single-owning-context invariant from heap_fcp.dart.md
      escalations[0], the owning context must be set up by the
      surrounding scheduler init (the codegen stage's threading-
      model implementation owns this). NOT re-escalated here.

  - construct_key: dart.method.drain_async_legacy_delegating_to_drain_async_with_status_and_returning_goals_ran_only
    source_form: >-
      `Future<List<int>> drainAsync({int maxCycles = 1000, bool
      debug = false, bool showBindings = true, bool debugOutput =
      false}) async { final result = await drainAsyncWithStatus(
      maxCycles: maxCycles, debug: debug, showBindings: showBindings,
      debugOutput: debugOutput); return result.goalsRan; }` —
      legacy async back-compat wrapper. Documented as "Legacy async
      drain for backward compatibility".
    target_decision: >-
      `public async Task<IReadOnlyList<int>> DrainAsync(int maxCycles
      = 1000, bool debug = false, bool showBindings = true, bool
      debugOutput = false) { var result = await
      DrainAsyncWithStatus(maxCycles, debug, showBindings,
      debugOutput); return result.GoalsRan; }`. Direct delegation;
      `async`/`await` preserved. Could be simplified to a non-`async`
      method returning `Task<IReadOnlyList<int>>` via
      `DrainAsyncWithStatus(...).ContinueWith(t => (IReadOnlyList<
      int>)t.Result.GoalsRan)` but the `async`/`await` form is
      simpler, mirrors the Dart shape, and the `async`/`await`
      overhead is negligible relative to the drain itself.
      Alternative: `=> DrainAsyncWithStatus(...).GoalsRan` — INVALID
      (cannot await with `=>` and `.GoalsRan` on a `Task`). The
      explicit `async`/`await` is required.
    idiom_id: null
    research_finding_id: rf-dart-async-await-delegating-method-to-csharp-async-await
    nuance: >-
      Async-delegation pattern: a method whose body is a single
      `await F(...).field` — the Dart shape is `Future<X> f() async
      { return (await g()).field; }`; C# counterpart `async Task<X>
      F() { var r = await G(); return r.Field; }`. Equivalent;
      preserved verbatim. NO CancellationToken / ConfigureAwait —
      same rationale as `DrainAsyncWithStatus`. Return-type
      narrowing (`IReadOnlyList<int>` instead of `List<int>`) —
      carry-forward of the DrainResult immutability surface
      decision. Async / Stream: PRESENT on the outer surface (Task,
      async/await); NO IAsyncEnumerable.

conversion_units:
  - "using directives at top of lib/runtime/scheduler.cs: using System; using System.Collections.Generic; using System.Linq; using System.Text.RegularExpressions; using System.Threading.Tasks; using <root>.Runtime; using <root>.Bytecode; (and any namespace-static-import for DefaultProgramKey shared sentinel — see Scheduler ctor nuance)"
  - "public enum ExecutionStatus { Succeeded, Failed, Suspended } in the namespace mirroring lib/runtime/"
  - "public sealed class DrainResult — get-only auto-properties GoalsRan: IReadOnlyList<int>, Status: ExecutionStatus, SuspendedGoals: IReadOnlyList<string>, BlockingReaders: IReadOnlySet<int>; single ctor with three required + one default-null parameter (blockingReaders null-coalesce to new HashSet<int>())"
  - "internal static readonly object DefaultProgramKey = new object(); — namespace-shared sentinel used as Dictionary<object?, V> null-key replacement; SHARED with runtime.dart.md Runners table"
  - "public class Scheduler in the namespace mirroring lib/runtime/ — reference identity equality, mutable internal pretty-printer state, single ctor matching the Dart shape with positional-with-defaults parameters"
  - "  - public GlpRuntime Rt { get; } (init-only via ctor)"
  - "  - public Dictionary<object?, BytecodeRunner> Runners { get; } (init-only; convenience-from-single-runner ctor path uses DefaultProgramKey)"
  - "  - public Action<string>? TraceSink { get; set; } (mutable nullable single-subscriber delegate; carry-forward rf-dart-void-function-question-to-csharp-action-nullable)"
  - "  - private Dictionary<int, string> _queryVarNames = new();"
  - "  - private Dictionary<int, int> _varDisplayMap = new();"
  - "  - private int _nextDisplayId = 1;"
  - "  - private static readonly Regex StripArityRegex = new(@\"(\\w+)/\\d+\\(\"); — pre-compiled regex for trace-output procedure-name cleanup (Microsoft Learn best practice for repeated-use Regex)"
  - "  - public Scheduler(GlpRuntime rt, BytecodeRunner? runner = null, Dictionary<object?, BytecodeRunner>? runners = null, Action<string>? traceSink = null) — body: Rt = rt; Runners = runners ?? (runner != null ? new Dictionary<object?, BytecodeRunner> { { DefaultProgramKey, runner } } : new Dictionary<object?, BytecodeRunner>()); TraceSink = traceSink;"
  - "  - public void SetQueryVarNames(IReadOnlyDictionary<string, int> varWriters) — Clear + foreach inverse fill, in-place mutation"
  - "  - private string GetVarDisplayName(int addr) — reader-to-writer lookup (heap.TryWriterForReader) + query-name lookup + putIfAbsent-bump for fresh X{n}; uses `writerAddr is int w` pattern-match and TryGetValue+manual-increment+index-assign for the putIfAbsent-counter idiom"
  - "  - public void ResetDisplayNumbering() — Clear both maps + _nextDisplayId = 1"
  - "  - private string FormatTerm(Term term, bool markReaders = true, HashSet<int>? path = null) — recursive formatter with cycle detection; path null-coalesces to new HashSet<int>(); dereference-loop with cycle-check-before-deref; special-case list (functor '.', arity 2) with circular-tail detection; special-case conjunction (functor ',', arity 2); general StructTerm string.Join recursion"
  - "  - private string FormatGoal(int goalId, string procName, CallEnv? env) — env-null short-circuit; up-to-10-arg loop with break-on-null; List<string>+string.Join"
  - "  - public string FormatBinding(int varId, object? value) — pattern-match dispatch (Term/string/null/nil/fallback); object.Equals(value, \"nil\") for cross-type structural equality; range-indexer [6..^1] for Const(...) wrapper strip"
  - "  - private void Trace(string line) — snapshot-then-invoke TraceSink else System.Console.WriteLine"
  - "  - public DrainResult DrainWithStatus(int maxCycles = 1000, bool debug = false, bool showBindings = true, bool debugOutput = false) — CENTRAL drain loop: dequeue Rt.Gq, lookup runner via DefaultProgramKey sentinel, construct RunnerContext with termFormatter and onReduction lambdas (closure-capture of hadReduction and suspendedGoals), dispatch via runner.RunWithStatus, classify result (Suspended/Terminated+hadReduction/Terminated+!hadReduction=Failed), filter infrastructure goals via LINQ Where+ToDictionary, compute blocking-readers via new HashSet<int>(Rt.Suspended.Keys), return new DrainResult(...)"
  - "  - public IReadOnlyList<int> Drain(int maxCycles = 1000, bool debug = false, bool showBindings = true, bool debugOutput = false) => DrainWithStatus(maxCycles, debug, showBindings, debugOutput).GoalsRan; — expression-bodied legacy delegate"
  - "  - public async Task<DrainResult> DrainAsyncWithStatus(int maxCycles = 1000, bool debug = false, bool showBindings = true, bool debugOutput = false) — async polling wrapper: outer while drains synchronously then awaits Task.Delay(10) until pending timers fire; preserves the 10 ms polling tick; no CancellationToken (FR-013 — escalate if future API needs it); no ConfigureAwait (single-owning-context invariant inherited)"
  - "  - public async Task<IReadOnlyList<int>> DrainAsync(int maxCycles = 1000, bool debug = false, bool showBindings = true, bool debugOutput = false) — legacy async delegate that returns .GoalsRan"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-immutable-value-bundle-no-eq-to-csharp-sealed-class-getonly — DrainResult

- **Deep analysis.** `DrainResult` is a four-field immutable bundle
  with `final` fields, no `==`/`hashCode` override, and an optional-
  positional defaulted-to-const-empty-set parameter. The fields are
  collection types (`List<int>`, `List<String>`, `Set<int>`) plus
  one enum; the source's intent is "a one-shot result of one drain
  call". The bundle is built once in `drainWithStatus` /
  `drainAsyncWithStatus` and consumed by test harnesses / the REPL.
  Lack of `==` override is INTENTIONAL — two distinct
  `DrainResult` instances with coincidentally identical contents
  are NOT `==`; callers compare via `.goalsRan.length` /
  `.status == .failed` etc., not via `==`.
- **Authoritative .NET.** The C# language reference on records vs
  classes (https://learn.microsoft.com/dotnet/csharp/fundamentals/types/records
  — Microsoft Learn) is explicit: "Use a class when you want
  reference semantics and identity-based equality. Use a record
  when you want value-based equality across all of an object's
  data." This file's `DrainResult` is reference-identity-equal in
  Dart (no `==` override) AND aggregates two `List<T>` and one
  `Set<T>` whose value equality would either be reference-equal
  (cheap, misleading) or contents-equal (expensive, requires
  custom `EqualityComparer`). The reference-class shape is the
  faithful counterpart.
- **Authoritative .NET (IReadOnlyList<T>).** The `IReadOnlyList<T>`
  interface (https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1
  — Microsoft Learn) is the documented .NET-idiomatic surface for
  "publicly read-only, internally built by mutation" — exactly the
  Dart `final List<int>` semantic (the FIELD is final; the LIST
  contents are mutable internally, but callers should treat them
  as read-only). The `IReadOnlySet<T>` (https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlyset-1)
  is the symmetric surface for sets.
- **`sealed` rationale.** No Dart subclass; no documented intent to
  extend; sealing is a defensive narrowing that costs nothing and
  matches the source-shape immutability intent. Carry-forward of
  the `sealed` discipline from terms.dart.md (`ConstTerm` /
  `StructTerm` are NOT sealed because terms are an open hierarchy;
  `DrainResult` IS a closed bundle, so sealing applies).
- **Const-empty-set semantics.** The Dart `const Set<int>{}` default
  is a compile-time-canonicalised SHARED empty set; .NET has no
  per-method-default `const` collection literal. The C# port uses
  a body-side `?? new HashSet<int>()` null-coalesce per the same
  default-empty-collection idiom in machine_state.dart.md and
  runtime.dart.md. Microsoft Learn `Method parameters` documents
  that valid parameter defaults are limited to `null`, `default(T)`,
  literal primitives, and `nameof(...)` — collection literals are
  not valid; the null-coalesce is the documented workaround.
- **No escalation.** Authoritative both sides; no idiom-vs-research
  conflict (the prior `GoalState` decision is for a MUTABLE
  reference class, distinct from `DrainResult` which is immutable
  but identity-equal; this is a NEW idiom for "immutable-no-eq
  bundle" that is recorded for future similar files).

### rf-dart-mutable-state-class-identity-equality-to-csharp-class — Scheduler (cache hit)

- FR-012 cache hit on `machine_state.dart.md`
  rf-dart-mutable-state-class-identity-equality-to-csharp-class
  (`GoalState`). Same identity-equality discipline: the Scheduler
  instance is held by callers across drain calls and mutated in
  place; reference-type `class` is the only correct mapping;
  `record` / `record class` / `struct` / `record struct` are
  categorically rejected. Authoritative.

### rf-dart-null-key-map (forwarded to runtime.dart.md) — runners

The `Map<Object?, BytecodeRunner> runners` null-key usage is a
load-bearing cross-file invariant shared with runtime.dart.md (which
has the same type for the same purpose). Microsoft Learn
`Dictionary<TKey,TValue>.Add(TKey,TValue)`
(https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2.add)
documents that the method throws `ArgumentNullException` when `key`
is `null` for reference-type `TKey`. Three options analysed
(sentinel-key, custom comparer, parallel-field); the sentinel-key
option is the faithful translation AND is the .NET-canonical
workaround. SHARED across this file and runtime.dart.md via a
namespace-level `internal static readonly object DefaultProgramKey
= new object();`. No escalation (FR-013) because the decision is
authoritative and the workaround is documented.

### rf-dart-print-to-csharp-console-writeline — _trace fallback and [DEBUG] message (cache hit)

FR-012 cache hit on `body_kernels.dart.md`
rf-dart-print-to-csharp-console-writeline. Dart top-level `print` →
`System.Console.WriteLine` per Microsoft Learn
(https://learn.microsoft.com/dotnet/api/system.console.writeline) —
identical stdout+newline semantics. Reused verbatim.

### rf-dart-void-function-question-to-csharp-action-nullable — traceSink (cache hit)

FR-012 cache hit on `body_kernels.dart.md` / `repl_play_runner.dart.md`
/ `mad_context.dart.md`
rf-dart-void-function-question-to-csharp-action-nullable. Dart `void
Function(String)?` → C# `Action<string>?`. Snapshot-then-invoke
pattern preserved per the mad_context.dart.md TraceSink discipline.

### rf-dart-putifabsent-counter-to-csharp-tryget-add-and-bump — _getVarDisplayName

- **Deep analysis.** Dart `Map.putIfAbsent(key, valueFactory)`
  atomically checks `key`; if absent, calls `valueFactory()`,
  inserts the result, and returns it; if present, returns the
  existing value WITHOUT calling `valueFactory()`. The
  lazy-call semantics is load-bearing because `valueFactory = () =>
  _nextDisplayId++` HAS A SIDE EFFECT (it bumps the counter);
  calling it on every lookup would over-bump the counter and break
  the display-numbering contract.
- **Authoritative Dart.** Dart `Map.putIfAbsent`
  (https://api.dart.dev/stable/dart-core/Map/putIfAbsent.html) is
  documented as "Returns the value associated with `key`. If `key`
  is not yet in the map, the `ifAbsent` callback is invoked, its
  result associated with `key`, and the result is returned."
  Lazy-call semantics confirmed.
- **Authoritative .NET.** Microsoft Learn
  `Dictionary<TKey,TValue>.TryGetValue`
  (https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2.trygetvalue)
  documents "Gets the value associated with the specified key. ...
  When this method returns, contains the value associated with the
  specified key, if the key is found; otherwise, the default value
  for the type of the value parameter." Combined with an explicit
  `if (!TryGetValue(...)) { _next++; dict[key] = ...; }`, this
  reproduces the lazy-bump semantics EXACTLY.
- **Rejected alternatives.** `Dictionary.GetOrAdd` is a
  `ConcurrentDictionary` method, not `Dictionary` — using it would
  silently change the concurrency model (rejected per the inherited
  single-owning-context invariant from heap_fcp.dart.md).
  `GetValueOrDefault` returns the default `0` without inserting —
  wrong semantics. `CollectionsMarshal.GetValueRefOrAddDefault`
  (.NET 6+) would work but is rejected for portability and shape
  fidelity (carry-forward from runtime.dart.md decision).
- **Idiom.** New idiom `rf-dart-putifabsent-counter-to-csharp-
  tryget-add-and-bump` — refinement of runtime.dart.md
  `rf-dart-putifabsent-set-add-to-csharp-tryget-add` for the
  counter-value case (vs the set-element-add case in
  runtime.dart.md).

### rf-dart-recursive-term-formatter-with-cycle-detection-to-csharp-recursive-pattern-match — _formatTerm

- **Deep analysis.** The 90-line recursive formatter dereferences
  VarRef chains with cycle detection BEFORE the deref call (so a
  cycle is caught at the next encounter of the same address),
  dispatches by Term subtype, special-cases list (functor `.`
  arity 2) with circular-tail detection, special-cases conjunction
  (functor `,` arity 2), and recurses for general structures with
  `args.map(...).join(', ')`. The cycle-detection set is THREADED
  THROUGH RECURSIVE CALLS BY REFERENCE — mutations in one level
  are visible to siblings and parents.
- **Authoritative .NET.** Microsoft Learn "Pattern matching - C#
  language reference"
  (https://learn.microsoft.com/dotnet/csharp/language-reference/operators/patterns)
  documents the `is T name` pattern that extracts a typed local in
  the same expression — the .NET-canonical counterpart of Dart's
  `current is VarRef` followed by `(current as VarRef).addr`.
  Microsoft Learn `HashSet<T>` 
  (https://learn.microsoft.com/dotnet/api/system.collections.generic.hashset-1)
  documents `Contains` / `Add` with O(1) average-case complexity —
  the .NET-canonical set for cycle detection.
- **Authoritative .NET (string.Join, Select).** Microsoft Learn
  `String.Join(String, IEnumerable<String>)`
  (https://learn.microsoft.com/dotnet/api/system.string.join) and
  `Enumerable.Select<TSource,TResult>`
  (https://learn.microsoft.com/dotnet/api/system.linq.enumerable.select)
  — the .NET-canonical projection + join for `args.map(...).join(...)`.
- **Authoritative .NET (Object.ToString nullability).** Microsoft
  Learn `Object.ToString`
  (https://learn.microsoft.com/dotnet/api/system.object.tostring)
  documents the return type as `string?` under NRT — "Returns: A
  string that represents the current object. ... It can be null."
  Hence the explicit `?? string.Empty` guard at the fallback.
- **No escalation.** Authoritative both sides. New idiom records
  the load-bearing combination "recursive formatter + ref-passed
  cycle set + pattern-match dispatch + LINQ projection" for reuse.

### rf-dart-args-scan-with-nullable-and-join-to-csharp-list-loop-and-stringjoin — _formatGoal

- Trivial idiom: build a `List<string>` via loop with break-on-null,
  then `string.Join`. Microsoft Learn `String.Join(String,
  IEnumerable<String>)` direct counterpart; List<string>+Add+
  Count==0+Join — all standard BCL methods. Authoritative; idiom
  recorded for "scan-fixed-width-args-with-null-sentinel" reuse.

### rf-dart-dynamic-typed-format-with-prefix-suffix-strip-to-csharp-object-pattern-and-range-index — formatBinding

- **Deep analysis.** Three load-bearing nuances combined: (1)
  `dynamic value` → `object?` per heap_fcp.dart.md (cache hit);
  (2) `value == 'nil'` Dart structural equality on `Object?` →
  `object.Equals(value, "nil")` because C# `==` on `object?` is
  reference-identity; (3) `substring(6, length - 1)` Dart half-open
  range → C# `[6..^1]` range-indexer (`^1` = "one from the end,
  exclusive").
- **Authoritative .NET (Object.Equals).** Microsoft Learn
  `Object.Equals(Object, Object)`
  (https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals(system-object-system-object))
  documents "Determines whether the specified object instances
  are considered equal. ... if both objects are null, true is
  returned. Otherwise, if only one of the objects is null, false
  is returned. Otherwise, the method's effective behavior is
  determined by the `Equals(Object)` instance method of the first
  parameter." For `value` of compile-time type `object?` and the
  literal `"nil"`, `object.Equals` dispatches to `String.Equals`
  → structural string equality. Direct counterpart of Dart's
  `Object.==` for `String`.
- **Authoritative .NET (range indexers).** Microsoft Learn "Indices
  and ranges"
  (https://learn.microsoft.com/dotnet/csharp/language-reference/proposals/csharp-8.0/ranges)
  documents `s[start..end]` as the half-open range substring; `^N`
  as "N from the end, exclusive". Direct counterpart of Dart
  `String.substring(start, end)` (which is also half-open). Avoids
  the `Substring(start, length)` arithmetic pitfall.
- **No escalation.** Authoritative both sides.

### rf-dart-central-drain-loop-with-closure-capture-and-status-classification-to-csharp-while-loop-with-display-class — drainWithStatus

- **Deep analysis.** The 130-line drain loop is THE central
  scheduler logic. Load-bearing pieces: (a) nullable-on-empty
  dequeue (GoalRef? + `.Value` unwrap); (b) null-key dictionary
  lookup via `DefaultProgramKey` sentinel; (c) RunnerContext ctor
  with closure-captured `hadReduction` and `suspendedGoals`; (d)
  cascade `Map.from(...)..removeWhere(...)` copy-and-filter →
  LINQ `Where+ToDictionary`; (e) `RunResult` enum compare; (f)
  pre-compiled static `Regex` for `/arity` cleanup; (g) blocking-
  reader set construction from `rt.suspended.keys`.
- **Authoritative .NET (closure capture).** Microsoft Learn
  "Lambda expressions and anonymous functions"
  (https://learn.microsoft.com/dotnet/csharp/language-reference/operators/lambda-expressions#capture-of-outer-variables-and-variable-scope-in-lambda-expressions)
  documents that local variables captured by a lambda are stored
  in a compiler-generated display class; mutations inside the
  lambda are visible to the outer scope after the lambda returns.
  Exact semantic match for Dart closure-capture-by-reference.
- **Authoritative .NET (LINQ ToDictionary).** Microsoft Learn
  `Enumerable.ToDictionary`
  (https://learn.microsoft.com/dotnet/api/system.linq.enumerable.todictionary)
  documents "Creates a `Dictionary<TKey,TValue>` from an
  `IEnumerable<T>`" — yields a FRESH dictionary, not aliasing the
  source. Direct counterpart of the Dart cascade `Map.from(...)..
  removeWhere(...)` which also yields a fresh non-aliased
  dictionary.
- **Authoritative .NET (Regex)**. Microsoft Learn "Regular
  expression language - quick reference"
  (https://learn.microsoft.com/dotnet/standard/base-types/regular-expression-language-quick-reference)
  confirms `\w` / `\d` / capture groups have semantics identical to
  the Dart pattern. Microsoft Learn "Best practices for regular
  expressions in .NET"
  (https://learn.microsoft.com/dotnet/standard/base-types/best-practices)
  recommends pre-compiled static `Regex` for repeated use — applied
  here via `private static readonly Regex StripArityRegex`.
  `Regex.Replace(String, MatchEvaluator)`
  (https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex.replace#system-text-regularexpressions-regex-replace(system-string-system-text-regularexpressions-matchevaluator))
  is the direct counterpart of Dart `RegExp.replaceAllMapped`.
- **Authoritative .NET (HashSet from IEnumerable).** Microsoft
  Learn `HashSet<T>.HashSet(IEnumerable<T>)`
  (https://learn.microsoft.com/dotnet/api/system.collections.generic.hashset-1.-ctor#system-collections-generic-hashset-1-ctor(system-collections-generic-ienumerable((-0))))
  documents construction from an IEnumerable — direct counterpart
  of Dart `Iterable.toSet()`.
- **No escalation.** Every nuance resolved from official .NET docs.

### rf-dart-named-arg-delegating-method-to-csharp-positional-expression-bodied-delegate — drain (legacy)

- Trivial idiom: a method that forwards all parameters to a
  more-detailed sibling. Named-vs-positional argument forwarding
  noted as a casing difference, not a semantic difference. The
  `IReadOnlyList<int>` return narrowing carries the immutability-
  surface decision from DrainResult.

### rf-dart-future-delayed-to-csharp-task-delay — drainAsyncWithStatus

- **Deep analysis.** The ONLY async surface in the file: a
  poll-with-delay loop waiting for pending timers to fire. The
  10 ms poll tick is preserved verbatim; the surrounding
  synchronous `drainWithStatus` is NOT made async (the inner
  reduction step is synchronous).
- **Authoritative .NET (Task<T> vs Future<T>).** Microsoft Learn
  "Asynchronous programming with async and await"
  (https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/)
  documents `Task<T>` as the awaitable representing a future
  result — direct counterpart of Dart `Future<T>` (Dart
  language tour https://dart.dev/codelabs/async-await).
- **Authoritative .NET (Task.Delay).** Microsoft Learn
  `Task.Delay(Int32)`
  (https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.delay#system-threading-tasks-task-delay(system-int32))
  documents "Creates a task that completes after a specified number
  of milliseconds" — exact semantic match of Dart `Future.delayed(
  Duration(milliseconds: 10))`. The int-overload is preferred over
  `Task.Delay(TimeSpan.FromMilliseconds(10))` for conciseness
  AND because the Dart source uses an integer literal.
- **Threading-model carry-forward.** The async continuation's
  resumption context is governed by the inherited single-owning-
  context invariant from heap_fcp.dart.md escalations[0] — NOT
  re-escalated here per the task instruction. No
  `ConfigureAwait(false)` (the surrounding scheduler should resume
  on the owning context).
- **No CancellationToken.** The Dart source has no cancellation
  surface; introducing a `CancellationToken` parameter would expand
  the API contract. Per FR-013, escalate at the future call site
  if needed, do not silently add. Idiom records this discipline.
- **No escalation.** Authoritative both sides.

### rf-dart-async-await-delegating-method-to-csharp-async-await — drainAsync (legacy)

- Trivial idiom: a method whose body is a single `await F(...).field`.
  Preserved verbatim; carries the same return-type-narrowing
  (`IReadOnlyList<int>`) decision from DrainResult.

### rf-dart-map-clear-and-fill-inverse-to-csharp-dictionary-clear-foreach — SetQueryVarNames

- Trivial idiom: clear-then-foreach-fill, in-place mutation
  preservation. Microsoft Learn
  `Dictionary<TKey,TValue>.Clear`
  (https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2.clear)
  direct counterpart of Dart `Map.clear`. New idiom recorded for
  reuse.

### rf-dart-map-clear-and-counter-reset-to-csharp-dictionary-clear-assign — ResetDisplayNumbering

- Trivial subsumed idiom: clear-multiple-fields-and-reset-counter.
  Authoritative; recorded.

## Notes — well-known nuances explicitly addressed (FR-009 / US2 AS4)

- **VALUE-VS-REFERENCE**: LOAD-BEARING throughout. `Scheduler` is
  reference-type `class` (identity equality, mutable internal state);
  `DrainResult` is reference-type `sealed class` (all-final fields,
  no `==` override → reference identity preserved); `ExecutionStatus`
  is a value-type enum. NEVER `record` / `record class` for
  `Scheduler` (would inject value equality) or `DrainResult` (would
  inject value equality + collection-reference-equality misleading);
  NEVER `struct` / `record struct` for either (mutable fields with
  reference-aggregation; copy semantics break mutation propagation).
- **NULL-SAFETY**: every Dart `?` annotation preserved verbatim in
  the C# port under NRT. `TraceSink: Action<string>?` (nullable
  delegate); `BlockingReaders: IReadOnlySet<int>` non-nullable but
  ctor parameter `blockingReaders: IReadOnlySet<int>?` nullable +
  null-coalesce-to-empty; `HashSet<int>? path` (nullable parameter,
  null-coalesce-to-empty); `CallEnv? env` (nullable param +
  short-circuit); `Term? arg` (nullable register-file slot + break);
  `object? value` (nullable dynamic-equivalent in `FormatBinding`);
  `GoalRef? Dequeue()` (Nullable<GoalRef> on the value-type record-
  struct return). The bang operator `!` from Dart force-unwraps
  becomes pattern-match (`is int w`) or `TryGetValue(out var v)` in
  the C# port — NRT-clean.
- **ASYNC / Stream / Future / Task / async / await**: ONE async
  surface — `DrainAsyncWithStatus` and `DrainAsync`. `Future<T>` →
  `Task<T>`; `Future.delayed(Duration(ms))` → `Task.Delay(ms)`;
  `async`/`await` preserved verbatim. NO `IAsyncEnumerable<T>` /
  `Channel<T>` / `Stream<T>` introduced — the source returns a
  single result, not a stream. NO `CancellationToken` introduced
  (would expand API contract; escalate later if needed). NO
  `ConfigureAwait(false)` (single-owning-context invariant).
  Synchronous sibling `DrainWithStatus` is NOT made async.
- **DYNAMIC vs OBJECT**: Dart `dynamic value` parameter in
  `formatBinding` → `object?` (NOT C# `dynamic`); pattern-match
  dispatch. Carry-forward of heap_fcp.dart.md
  `rf-dart-dynamic-to-csharp-object`.
- **THREADING MODEL**: inherited from heap_fcp.dart.md escalations[0]
  (single-owning-context per goal / non-concurrent collections);
  NOT re-escalated. The `Dictionary<int, string>` / `Dictionary<int,
  int>` / `Dictionary<object?, BytecodeRunner>` are NON-concurrent
  per the inherited decision; `HashSet<int>` is non-concurrent;
  `Task.Delay` continuation resumes on the owning context (default
  `ConfigureAwait(true)`).
- **NULL-KEY DICTIONARY**: `Map<Object?, BytecodeRunner>` with `null`
  key (Dart) → `Dictionary<object?, BytecodeRunner>` with
  `DefaultProgramKey` sentinel (.NET) — SHARED across this file and
  runtime.dart.md via namespace-level `internal static readonly
  object DefaultProgramKey`. Microsoft Learn `Dictionary<TKey,TValue>
  .Add` documents the `ArgumentNullException` on `null` key.
- **CONST-EMPTY-COLLECTION DEFAULT**: Dart `const Set<int>{}` /
  `const Map<int, V>{}` default-parameter values have no .NET
  counterpart; the body-side `?? new HashSet<int>()` / `?? new
  Dictionary<K,V>()` null-coalesce is the documented faithful
  workaround. Carry-forward from machine_state.dart.md (`sigmaHat`
  default) and runtime.dart.md.
- **MUTABLE-LIST-BUILDER + JOIN**: `<String>[]` + `.add` + `.join` →
  `new List<string>()` + `.Add` + `string.Join` — .NET-canonical.
- **REGEX**: Dart raw `RegExp(r'...')` → C# verbatim `@"..."` Regex;
  `replaceAllMapped((m) => '${m.group(1)}(')` →
  `Regex.Replace(..., m => $"{m.Groups[1].Value}(")`; pre-compiled
  `private static readonly Regex` per Microsoft Learn best practices.
- **CASCADE COPY-AND-FILTER**: `Map.from(x)..removeWhere(...)` →
  `x.Where(...).ToDictionary(kv => kv.Key, kv => kv.Value)` — LINQ-
  canonical, yields fresh non-aliased dictionary; NOT in-place
  mutation of the source.
- **PRINT STDOUT**: Dart top-level `print` → `System.Console
  .WriteLine` (carry-forward from body_kernels.dart.md).
- **NULLABLE DELEGATE INVOKE**: `traceSink!(line)` Dart bang →
  `var cb = TraceSink; if (cb != null) cb(line);` snapshot-then-
  invoke (carry-forward from mad_context.dart.md TraceSink).
- **DEREFERENCE LOOP + CYCLE DETECTION**: load-bearing in
  `_formatTerm`; preserved verbatim with `HashSet<int>` ref-threaded
  through recursion; `is T name` pattern-match dispatch.
- **SUBSTRING SLICING**: Dart `substring(start, end)` (half-open)
  → C# range-indexer `[start..end]` (also half-open) — exact
  semantic match; AVOIDS the C# `Substring(start, length)`
  arithmetic pitfall. `^1` = "one from the end, exclusive".
- **OBJECT.EQUALS for cross-type structural equality**: `value ==
  'nil'` Dart structural string equality on `Object?` →
  `object.Equals(value, "nil")` (NOT `==` which would be reference
  identity on `object?`).
- **OBJECT.TOSTRING NULLABILITY**: Dart `Object.toString` non-nullable
  → .NET `object.ToString()` nullable under NRT → explicit `??
  string.Empty` guard. Microsoft Learn `Object.ToString`.
- **EXCEPTION MAPPING**: `StateError(...)` → `InvalidOperationException
  (...)` (carry-forward from heap_fcp.dart.md).
- **PASCALCASE NAMING**: every public Dart camelCase identifier
  (`drainWithStatus`, `setQueryVarNames`, `formatBinding`, etc.)
  becomes PascalCase per the .NET capitalisation guideline; private
  underscore-prefixed Dart names (`_queryVarNames`, `_varDisplayMap`,
  `_nextDisplayId`, `_formatTerm`, `_formatGoal`, `_trace`,
  `_getVarDisplayName`) stay underscore-prefixed camelCase per the
  same convention.
- **TRIVIAL SUBSUMED**: doc comments → XML doc comments uniformly;
  integer literals (`1000`, `10`, `1`, `6`, `0`) preserved verbatim;
  string literals (`'<circular>'`, `'<null>'`, `'[]'`, `'X'`,
  `'Const('`, `' :- '`, `' → suspended'`, `' → failed'`, `'[DEBUG]
  Waiting for ${...} pending timer(s)...'`) preserved verbatim
  (English semantic content unchanged; not corpus-wide spec
  strings).
- **MIXIN / SEALED-BASE / EXTENSIONS / GENERICS / late / abstract**:
  ABSENT. The class has no `mixin`, no `sealed` Dart modifier (the
  C# port marks `DrainResult` as `sealed` but not the `Scheduler`
  class), no extension methods, no generics, no `late` fields, no
  `abstract` members; correctly not asserted.
- **TRAIL / CHOICE-POINTS / WAM-STYLE BACKTRACKING**: ABSENT from
  this file. The scheduler does NOT carry a trail; the runtime spec
  keeps those structures in the runner/heap-FCP layer; correctly
  not asserted (carry-forward from machine_state.dart.md scope
  discipline).
- **ZERO ESCALATIONS**: every non-trivial construct resolved from
  authoritative Dart and .NET official documentation, with the
  threading-model decision INHERITED from heap_fcp.dart.md
  escalations[0] (NOT re-escalated per FR-013). Cache hits on
  prior idioms from machine_state.dart.md, runtime.dart.md,
  heap_fcp.dart.md, body_kernels.dart.md, mad_context.dart.md,
  bytecode/runner.dart.md. No idiom-vs-research conflicts; no
  idiom-vs-idiom conflicts (FR-014).
