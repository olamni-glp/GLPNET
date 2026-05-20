# Conversion Spec — lib/bytecode/runner.dart

> Conversion-spec artifact for lib/bytecode/runner.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> The LARGEST file in the corpus (4863 lines): the FCP/WAM bytecode VM
> — instruction-dispatch interpreter (`BytecodeRunner.runWithStatus`),
> per-goal mutable execution state (`RunnerContext`), HEAD-phase
> tentative-binding machinery (σ̂w / Si / U, two-phase commit), structure
> traversal (WAM read/write modes with a parent stack), the v1/v2 opcode
> family handlers, body kernels and goal spawning / requeue (tail call),
> the module RPC ops (distribute/transmit), the GUARD evaluator
> (arithmetic + type guards + wait/wait_until / =?=), and four private
> in-file helper classes. Heavy reliance on the heap, the goal queue,
> the suspension primitives, the commit operator, machine_state, and
> the system-predicates + body-kernels tables — every cross-file
> reference is REUSED from the corresponding sibling convspec (FR-024
> cache hit), never re-derived.
>
> The runner is the locus where every load-bearing nuance from the
> sibling specs converges: dynamic-vs-object dispatch (cell.content as
> `object?`, heap_fcp.dart.md), reference-identity of cell/term nodes
> (terms.dart.md / suspension.dart.md), enum mapping (opcodes.dart.md),
> goal-queue semantics (goal_queue.dart.md), threading-model
> escalation (HeapFCP — heap_fcp.dart.md escalations[0], INHERITED
> below, NOT double-escalated per task instruction).

```yaml
schema_version: 1
source_path: lib/bytecode/runner.dart
source_sha256: 7fdcc6faa358f2dacdfe6c63bf69d43b58bed08dc1f1ec6bfcefbf2d6aa4030a
target_code_unit: lib/bytecode/runner.cs
constructs:
  - construct_key: dart.import_directive.dart_async_timer_only_to_csharp_using_threading_timers
    source_form: "`import 'dart:async' show Timer;` — narrows the dart:async import to the `Timer` symbol only (used by the `wait` / `wait_until` guards to schedule a one-shot callback that binds a writer cell to wake suspended goals)."
    target_decision: >-
      Map to `using System.Threading;` (or — depending on the codegen
      stage's pick — `System.Threading.Tasks` for `Task.Delay`-based
      scheduling). The Dart `Timer(Duration(ms), cb)` becomes
      `System.Threading.Timer` (one-shot via the four-arg constructor
      with `Timeout.InfiniteTimeSpan` period) — the .NET timer that
      directly mirrors Dart's "schedule a single delayed callback"
      semantics (see Microsoft Learn: `System.Threading.Timer`). The
      `show Timer` narrowing has no .NET parallel — `using
      System.Threading;` brings the full surface; that is acceptable
      per the carry-forward decision in `heap_fcp.dart.md`
      rf-dart-import-relative-to-csharp-using-namespace.
    idiom_id: null
    research_finding_id: rf-dart-timer-to-csharp-system-threading-timer
    nuance: >-
      Concurrency nuance (LOAD-BEARING, explicitly addressed): Dart
      `Timer` schedules onto the OWNING isolate's single-threaded
      event loop — the callback ALWAYS runs on the same isolate that
      created the timer. `System.Threading.Timer` callbacks run on
      arbitrary ThreadPool threads, NOT on the creating thread. The
      timer callback here performs `cx.rt.heap.bindWriterConst(...)`
      and `cx.rt.enqueueReactivatedGoal(...)` — both are HeapFCP /
      goal-queue mutations whose thread-safety is decided by the
      INHERITED concurrency escalation from `heap_fcp.dart.md`
      escalations[0] (NOT re-escalated here per task instruction).
      Under the recommended option (A) — single-owner-thread per
      isolate-manager port — the timer callback MUST be marshalled
      back to the owning scheduler before touching the heap (e.g. by
      posting to a per-context `SynchronizationContext` /
      single-threaded `TaskScheduler` / mailbox). The codegen MUST
      preserve "schedule a single delayed action that, when fired,
      binds the writer and enqueues reactivated goals on the owning
      runtime's scheduler" — the concrete mechanism (Timer vs.
      Task.Delay vs. periodic dispatcher) is a downstream
      codegen-stage choice constrained by that contract. NOT changed
      to async/await here: the surrounding `_evaluateGuard` is
      synchronous in Dart (returns `GuardResult`), and the suspension
      protocol uses heap-binding + goal-reactivation, NOT an awaited
      future — preserving the sync surface is mandatory.

  - construct_key: dart.import_directive.package_internal_to_using_namespace
    source_form: >-
      Seven package-internal imports — `package:glp_runtime/runtime/runtime.dart`,
      `runtime/machine_state.dart`, `runtime/terms.dart`,
      `runtime/commit.dart`, `runtime/cells.dart`,
      `runtime/system_predicates.dart`, `runtime/body_kernels.dart`,
      `package:glp_runtime/multiagent/variable_table.dart show
      VariableEntry` — plus two same-directory imports `opcodes.dart`
      and `opcodes_v2.dart as opv2` (the v2 directive uses a prefix
      alias).
    target_decision: >-
      Each Dart package-internal import maps to a .NET `using` directive
      naming the namespace of the converted file (`using
      <root>.Runtime;` covers terms / machine_state / commit / cells /
      runtime / system_predicates / body_kernels — all sibling
      `lib/runtime/` files target the same namespace), `using
      <root>.Multiagent;` for `VariableEntry`, and `using
      <root>.Bytecode;` for the opcode IR. The `show VariableEntry`
      allow-list has no parallel — .NET `using` imports the full
      public namespace surface (carry-forward from
      `heap_fcp.dart.md`). The Dart prefix-import `import
      'opcodes_v2.dart' as opv2;` and subsequent `opv2.PutVariable` /
      `opv2.HeadVariable` etc. references map to a .NET `using
      static`-style alias OR — and this is the recommended option —
      to plain `using <root>.Bytecode.V2;` (the v2 opcode types
      already share a namespace in their own convspec) plus
      qualified-by-namespace references (`V2.PutVariable`) where the
      v1 and v2 types overlap by simple name (`HeadVariable`,
      `UnifyVariable`, `SetVariable`, `PutVariable`, `GetVariable`,
      `GetValue`, `Unknown` — the seven v2 opcode classes whose names
      collide with v1 counterparts in `opcodes.dart.md`). Codegen MUST
      keep the v1/v2 distinction the `opcodes_v2.dart.md` convspec
      established (separate marker interfaces `IOp` and `IOpV2`, no
      shared base) — naive `using <root>.Bytecode.V1; using
      <root>.Bytecode.V2;` would create ambiguous unqualified `is
      HeadVariable` tests; the dispatcher's `if (op is opv2.X)` source
      mandates qualified references in the target.
    idiom_id: null
    research_finding_id: rf-dart-import-relative-to-csharp-using-namespace
    nuance: >-
      Prefix-import nuance (LOAD-BEARING, explicitly addressed): Dart
      `as opv2` is a true symbol prefix that disambiguates colliding
      class names. C# `using` aliases work at the type level, not the
      namespace level, but a namespace-qualified reference (`V2.X`) is
      the semantically equivalent translation. Codegen MUST NOT
      collapse v1 and v2 opcode types into a single namespace —
      `if (op is V2.HeadVariable)` MUST remain distinguishable from
      `if (op is V1.HeadVariable)`. Cache hit on
      `heap_fcp.dart.md` rf-dart-import-relative-to-csharp-using-namespace
      (FR-024).

  - construct_key: dart.enum.plain_marker_states_for_run_result_unify_mode_guard_result
    source_form: >-
      Three plain enums: (a) `enum RunResult { terminated, suspended,
      yielded, outOfReductions }` — the four exit conditions returned
      by `runWithStatus`; (b) `enum UnifyMode { read, write }` — the
      WAM-style read/write mode toggled by HEAD-phase structure
      traversal; (c) `enum GuardResult { success, failure, suspend }`
      — three-valued guard outcome (the `suspend` member is
      DOCUMENTED-BUT-UNREACHED in `_evaluateGuard`: a comment notes
      "we handle this before evaluation" so guards return only
      success/failure today; suspension is detected upstream via
      `_dereferenceWithTracking`).
    target_decision: >-
      Three plain C# enums in declaration order so the underlying
      integral values are stable; member spellings preserved verbatim
      (`Terminated`/`Suspended`/`Yielded`/`OutOfReductions`,
      `Read`/`Write`, `Success`/`Failure`/`Suspend`). Casing nuance:
      Dart camelCase enum members (`outOfReductions`) map to .NET
      PascalCase (`OutOfReductions`) PER MICROSOFT NAMING — these are
      NOT spec-named identifiers (unlike `WrtTag`/`RoTag`/`ValueTag`
      in `cells.dart.md` whose verbatim preservation IS load-bearing).
      Reference confirmation: the closest precedent is the
      `opcodes.dart.md` enum mapping. Underlying type `int`; no
      `[Flags]` — these are mutually exclusive states. The
      `GuardResult.Suspend` member is RETAINED (NOT pruned) even
      though the current evaluator never returns it — the enum's
      three-valued shape is the load-bearing surface (matches GLP's
      three-valued unification semantics: Success / Failure / Suspend
      from suspension.dart.md), and pruning would silently make a
      future codegen pass treat a re-introduced `Suspend` case as
      unhandled.
    idiom_id: null
    research_finding_id: rf-dart-plain-enum-to-csharp-enum
    nuance: >-
      Discriminator nuance (explicitly addressed): each enum is the
      load-bearing discriminator for a `switch`/`if` chain in the
      runner. C# enum value-equality `==` matches Dart enum `==`
      exactly. Open-vs-closed nuance: the source has no
      `sealed`/exhaustive-switch semantics, so any consumer `default`
      arm in the target MUST throw `InvalidOperationException` (mirror
      of Dart `StateError`) — same rationale as
      `heap_fcp.dart.md` for `CellTag`. Null-safety: enums are value
      types, non-nullable on both sides. CACHE HIT from
      `heap_fcp.dart.md` / `cells.dart.md` / `machine_state.dart.md` /
      `opcodes.dart.md` (FR-024 — same authoritative finding).

  - construct_key: dart.typedef.string_alias_label_name
    source_form: "`typedef LabelName = String;` — a simple alias for `String`, used as the key type of `BytecodeProgram.labels` and as the parameter type of label-target instructions (e.g. `Spawn.procedureLabel`, `op.failLabel`, `op.label`)."
    target_decision: >-
      C# `using LabelName = string;` file-scoped using alias OR a
      `global using` (codegen-stage decision); the alias documents
      intent without changing the underlying type. Same authoritative
      finding as `opcodes.dart.md`
      rf-dart-typedef-string-to-csharp-using-alias (FR-024 cache hit;
      not re-researched). Codegen MUST NOT promote to a `record
      struct LabelName(string Value)` wrapper — there is no value-
      semantic distinction in the source, and wrapping would require
      every label-lookup call site to construct/extract the wrapper.
    idiom_id: null
    research_finding_id: rf-dart-typedef-string-to-csharp-using-alias
    nuance: >-
      Documentation-only nuance (explicitly addressed): Dart's
      typedef is a TYPE alias (interchangeable with `String`); C#
      `using` alias is a COMPILATION-UNIT-LOCAL alias (file-scoped) —
      semantically identical at compile time but does not propagate
      across files. The alias is purely documentational; consumers
      can use either `LabelName` or `string` interchangeably. NULL-
      SAFETY: Dart `String` is non-nullable in this codebase (no
      `String?` on the labels Map keys); the C# alias maps to
      non-nullable `string`. Cache hit from `opcodes.dart.md`.

  - construct_key: dart.simple_data_class.replmoduletarget_replmodulecontext_callenv_environmentframe_parentcontext
    source_form: >-
      Five simple-data classes covering REPL module wiring, goal
      arguments, environment frames, and the structure-traversal
      parent stack: (1) `ReplModuleTarget { final String name; final
      BytecodeProgram program; }` — final fields, positional ctor; (2)
      `ReplModuleContext { final String moduleName; final Map<int,
      ReplModuleTarget> imports; final BytecodeProgram?
      combinedProgram; final String programKey; }` — final fields with
      required-named ctor + one default `programKey = 'main'`; (3)
      `CallEnv { final Map<int, Term> argBySlot; }` — wraps a Map,
      exposes `arg(slot)` getter and `update(newArgs)` mutator that
      clears+addAll's; (4) `EnvironmentFrame { final
      EnvironmentFrame? parent; final int continuationPointer; final
      List<Object?> permanentVars; }` — required-named ctor with
      `size` initialising `permanentVars = List.filled(size, null);`
      plus `getY(i)`/`setY(i, value)` 1-indexed accessors; (5)
      `_ParentContext { final Object? structure; final int s; final
      UnifyMode mode; final Object? writerId; }` — file-private (`_`
      prefix), used as the element type of `parentStack` for nested
      structure building.
    target_decision: >-
      Each maps to a .NET reference `class` (NOT `record class` /
      `struct` / `record struct`) in the namespace mirroring
      `lib/bytecode/`. Get-only auto-properties for `final` fields
      (`public string Name { get; }` etc.); positional or named
      constructors mirroring the Dart parameter shape (positional
      `this.name` -> C# positional ctor parameter; `{required
      this.parent}` -> regular non-defaulted parameter that the
      caller must pass by name to remain faithful, per
      `opcodes_v2.dart.md` rf-dart-required-named-param-to-csharp-required-arg).
      `EnvironmentFrame` constructor takes `(EnvironmentFrame? parent,
      int continuationPointer, int size)` and initialises a
      `List<object?>` of length `size` filled with `null` — mirror of
      `List.filled(size, null)` is `new List<object?>(Enumerable.Repeat
      <object?>(null, size))` (preserve allocate-and-prefill
      semantics; codegen MUST NOT substitute a Span<T>-backed array
      here — the list is later mutated by `setY` and grown via
      `permanentVars[index - 1] = value`, which a `Span<T>` over a
      `stackalloc` would not support across method boundaries; and
      `_ParentContext`-class instances live on the `RunnerContext`'s
      `parentStack` for arbitrary depth, so the frame outlives any
      single stack frame — a heap-allocated `List<object?>` is
      correct). `_ParentContext` is FILE-PRIVATE in Dart (leading
      `_`); in C# this becomes `internal sealed class
      ParentContext` (file-private has no exact C# equivalent —
      `internal` is the closest scope per Microsoft Learn; `file`-
      scoped types from C# 11+ are an alternative but less idiomatic
      for shared internal helpers). The Dart map `argBySlot` is
      mutable in place (`CallEnv.update` does `argBySlot.clear();
      argBySlot.addAll(newArgs);`) — the C# field is `public
      Dictionary<int, Term> ArgBySlot { get; }` (get-only property
      holding a mutable dictionary; the `update` method calls
      `Clear()` + foreach `Add(k, v)`).
    idiom_id: null
    research_finding_id: rf-dart-final-field-class-to-csharp-getonly-class
    nuance: >-
      VALUE-VS-REFERENCE nuance (LOAD-BEARING, explicitly addressed):
      every one of these types is held by reference from
      `RunnerContext` and from the runner's parentStack / argBySlot;
      they are mutated in place (e.g. `EnvironmentFrame.permanentVars
      [i] = value` via `setY`; `CallEnv.update` clears and replaces
      `argBySlot` contents in place). C# `class` (reference type) is
      the only correct mapping — `record class` would inject value
      equality (silently making two distinct CallEnv's with the same
      argSlots compare equal — catastrophic for goalEnv lookups
      keyed by reference identity); `struct`/`record struct` would
      copy on assignment, breaking mutation propagation. Same
      authoritative rationale as `heap_fcp.dart.md`. PRIVATE-MEMBER
      NUANCE (explicitly addressed): `_ParentContext`'s leading-`_`
      file-privacy is a LIBRARY-PRIVATE scope (visible to the entire
      Dart library / file); the closest C# equivalent is `internal`
      (assembly-scoped) — slightly broader, but documenting `sealed`
      + no public ctor consumers keeps the visibility contract intact.
      Required-named ctor parameters preserved per
      `opcodes_v2.dart.md`. NULL-SAFETY: `combinedProgram` (`?`),
      `parent` (`?`), `Object?` slots are all preserved as
      nullable in C# (`BytecodeProgram?`, `EnvironmentFrame?`,
      `object?`); the non-nullable fields (`moduleName`, `programKey`,
      `continuationPointer`, `name`, `program`, `imports`) remain
      non-nullable.

  - construct_key: dart.bytecode_program.list_dynamic_ops_label_index_merge_disassembly
    source_form: >-
      `class BytecodeProgram { final List<dynamic> ops; final
      Map<LabelName, int> labels; BytecodeProgram(this.ops) : labels
      = _indexLabels(ops); ... BytecodeProgram merge(other) { final
      mergedOps = [...other.ops, ...ops]; return
      BytecodeProgram(mergedOps); } String toDisassembly() { ... }
      String _instructionToString(dynamic op) { if (op is
      opv2.PutVariable) { ... } if (op is opv2.HeadVariable) { ... }
      ... return op.toString(); } static Map<LabelName, int>
      _indexLabels(List<dynamic> ops) { final m = <LabelName,int>{};
      for (var i = 0; i < ops.length; i++) { final op = ops[i]; if (op
      is Label && !m.containsKey(op.name)) { m[op.name] = i; } }
      return m; } }` — holds a heterogeneous list of v1 `Op` and v2
      `OpV2` instructions; labels indexed by name to PC; merge
      prepends another program's ops (used to prepend stdlib);
      disassembly walks ops and formats v2 ops with explicit
      reader/writer mode tags.
    target_decision: >-
      A reference `class BytecodeProgram` in the namespace mirroring
      `lib/bytecode/`. The Dart `List<dynamic> ops` becomes
      `IReadOnlyList<object> Ops { get; }` BACKED by a
      `List<object>` (NOT a `List<dynamic>` — C# `dynamic` is a DLR
      type, defers all dispatch to runtime, and is OUT OF CHARACTER
      for a hot-path bytecode dispatcher; the runner already does
      explicit `if (op is X)` pattern-matches, mirroring the Dart
      source). The element type is `object` because the list HOLDS
      BOTH v1 `IOp` and v2 `IOpV2` (per opcodes.dart.md /
      opcodes_v2.dart.md the two marker interfaces are DELIBERATELY
      DISJOINT — there is no shared base, by design). Static
      `_indexLabels` becomes a private static method
      `IndexLabels(IReadOnlyList<object> ops)` returning a
      `Dictionary<string, int>`; first-occurrence semantics
      preserved with `if (!dict.ContainsKey(label.Name))
      dict[label.Name] = i;` (multi-clause procedures share one
      label, only first index wins — load-bearing). `merge` allocates
      a fresh `List<object>` of size `this.Ops.Count + other.Ops.Count`
      and copies in `[..other.Ops, ..this.Ops]` order (other FIRST —
      "prepend stdlib", per Dart spread). `toDisassembly` builds a
      `StringBuilder` (NOT a `StringBuffer`; same authoritative
      finding as `heap_fcp.dart.md`). `_instructionToString` is a
      private static method using C# pattern-matching switch over the
      v2 opcode types (`PutVariable / HeadVariable / UnifyVariable /
      SetVariable`) with the same isReader→"reader"/"writer" mapping;
      fallback `return op.ToString()!;` preserves the Dart
      `op.toString()`. NOT a `record class` — equality is reference,
      mutation is allowed (constructors take Ops by ref and store);
      NOT a `struct` (heap-resident, shared by every goal in a run).
    idiom_id: null
    research_finding_id: rf-dart-dynamic-list-of-sum-types-to-csharp-list-of-object
    nuance: >-
      DYNAMIC-VS-OBJECT (LOAD-BEARING, explicitly addressed): Dart
      `List<dynamic>` defers element-type checking to runtime; the
      runner already uses runtime type-tests (`if (op is X)`)
      everywhere — so the faithful C# translation is `List<object>` +
      `is` / `is X x` pattern-matching, NOT `List<dynamic>` (DLR
      overhead and out-of-character for a kernel hot path). The
      element constraint (v1 `IOp` or v2 `IOpV2`) is enforced by the
      `IndexLabels` filter and the dispatcher's pattern-match — there
      is NO compile-time guarantee in either Dart or C# that
      forbids non-opcode elements; preserving the same runtime-
      discipline is the correct translation. NULL-SAFETY: the list
      itself is non-nullable; element type `object` (non-nullable in
      NRT) since the Dart source NEVER stores `null` in `ops`
      (every element is an `Op`/`OpV2` instance). PERFORMANCE
      NUANCE (explicitly addressed, NOT glossed): the per-PC `is X
      x` pattern-match is the hottest dispatch in the runner; the
      Microsoft Learn pattern-matching docs note that the JIT lowers
      a sequence of `is X` tests into a near-table dispatch when the
      types form a closed hierarchy. The runner's enormous `if (op
      is X) { ... continue; } if (op is Y) { ... continue; } ...`
      chain (44+ branches per dispatch cycle) is the same shape
      Dart uses; codegen MUST PRESERVE that shape rather than refactor
      to a hashed `Type` -> handler `Dictionary` (would lose
      cache-locality and pattern-match short-circuit). A future
      optimisation could introduce a per-opcode `Kind` enum +
      `switch (op.Kind)` — that is a CODEGEN OPTIMISATION (not a
      semantic change), and is EXPLICITLY OUT OF SCOPE for this spec.

  - construct_key: dart.runner_context.per_goal_mutable_state_register_block
    source_form: >-
      `class RunnerContext { final GlpRuntime rt; final int goalId;
      int kappa; final CallEnv env; final Map<int, Object?> sigmaHat
      = <int, Object?>{}; final Set<int> Si = <int>{}; final Set<int>
      U = <int>{}; bool inBody = false; UnifyMode mode =
      UnifyMode.read; int S = 0; Object? currentStructure; final
      Map<int, Object?> clauseVars = {}; final List<_ParentContext>
      parentStack = []; final Map<int, Term> argSlots = {}; int?
      guardArgSlot; int? reductionBudget; int reductionsUsed = 0;
      EnvironmentFrame? E; int? CP; final void Function(GoalRef)?
      onActivation; final List<String> spawnedGoals = []; String?
      goalHead; String? goalProcName; final void Function(int, String,
      String)? onReduction; final bool showBindings; final bool
      debugOutput; final String Function(Term, {bool markReaders})?
      termFormatter; final Object? moduleContext; RunnerContext({
      required this.rt, required this.goalId, required this.kappa,
      CallEnv? env, this.onActivation, this.reductionBudget, ... }) :
      env = env ?? CallEnv(); void clearClause() { ... } String
      reformatHead() { ... } }` — the BIG mutable per-goal state
      block: σ̂w (tentative writer bindings staging area for two-phase
      commit), Si (clause-level preliminary suspension set —
      readers that made HEAD indeterminate), U (goal-level
      accumulated suspension set), inBody flag (HEAD/GUARD vs BODY
      phase guard), mode/S/currentStructure (WAM read/write
      structure-traversal cursor), clauseVars (X-register-equivalent
      indirection), parentStack (nested structure depth — arbitrary,
      per Push/Pop), argSlots (heterogeneous argument registers A1..
      An), guardArgSlot (target slot when building structure for a
      guard argument), reductionBudget/reductionsUsed (cooperative
      yielding), E/CP (environment-frame pointer and continuation
      pointer — WAM environment stack), kappa (mutable: re-pointed
      by Requeue for tail calls), trace hooks (onActivation,
      onReduction, debugOutput, showBindings, termFormatter), and a
      module-context handle (moduleContext typed `Object?` because
      ReplModuleContext is one of several future module-context
      kinds).
    target_decision: >-
      A reference `class RunnerContext` (NOT `record`/`struct`).
      EVERY mutable field maps to a public auto-property with public
      setter (`Kappa`, `InBody`, `Mode`, `S`, `CurrentStructure`,
      `GuardArgSlot`, `ReductionBudget`, `ReductionsUsed`, `E`,
      `CP`, `GoalHead`, `GoalProcName`) — the runner MUTATES these
      across handlers; promoting any to `{ get; private set; }` would
      require moving every mutator INTO this class (refactor the
      source shape — see `heap_fcp.dart.md` mutator nuance). `final`
      collection fields (`SigmaHat`, `Si`, `U`, `ClauseVars`,
      `ParentStack`, `ArgSlots`, `SpawnedGoals`) become get-only
      properties holding mutable collections (the COLLECTION
      reference is immutable, the CONTENTS are mutated in place). The
      Dart Map<int, Object?> sigmaHat / clauseVars become
      `Dictionary<int, object?>`; the Sets `Si`/`U` become
      `HashSet<int>`; `parentStack` becomes `Stack<ParentContext>`
      (NOT `List<ParentContext>` — Dart's `List` is used with
      add/last-element pop semantics; `Stack<T>` gives the same
      Push/Pop O(1) semantics and matches the field name's intent; if
      iteration order in any consumer turns out to matter, codegen
      MUST switch to `List<T>` — none observed in this file).
      `argSlots` becomes `Dictionary<int, Term>` (heterogeneous;
      keys are arg slot indices). Constructor: required-named
      parameters (`rt`, `goalId`, `kappa`) become regular C# ctor
      parameters; the `env = env ?? CallEnv()` default becomes
      `Env = env ?? new CallEnv();` in the body; optional named
      parameters with defaults (`showBindings = true`, `debugOutput
      = false`) become optional C# parameters with the SAME defaults
      per `opcodes.dart.md` rf-dart-named-default-param-to-csharp-optional-arg.
      `clearClause()` is a void method that mutates the collections
      via `.Clear()`/property assignment; semantically identical to
      Dart. `reformatHead()` is a method returning `string`
      (StringBuilder-backed iteration over `Env.Arg(i)` slots 0..9,
      breaking on first null — preserve that exact loop shape).
      Function-typed fields (`onActivation`, `onReduction`,
      `termFormatter`) become C# `Action<GoalRef>?`,
      `Action<int, string, string>?`, and `Func<Term, bool,
      string>?` — and the Dart `{bool markReaders}` named parameter
      on termFormatter becomes a regular C# parameter (no syntactic
      named-only — see opcodes_v2.dart.md required-named nuance for
      the same compromise). `moduleContext` is typed `object?` (the
      Dart `Object?` "unknown future module-context kind" — codegen
      MUST NOT introduce an `IModuleContext` interface here UNLESS
      one is independently introduced in the multiagent convspec; for
      now `object?` + `is ReplModuleContext` pattern-match is the
      faithful translation).
    idiom_id: null
    research_finding_id: rf-dart-mutable-state-class-identity-equality-to-csharp-class
    nuance: >-
      VALUE-VS-REFERENCE is the load-bearing nuance (LOAD-BEARING,
      explicitly addressed): RunnerContext IS the per-goal execution
      cursor; the runner holds ONE reference and mutates it across
      every opcode handler. C# `class` (reference type) is the only
      correct mapping — `struct`/`record struct` would copy on
      assignment (catastrophic — every `cx.Kappa = nextPc` would
      mutate a copy not the canonical state); `record class` would
      inject value equality (two distinct contexts with identical
      register snapshots would compare equal — semantic drift).
      Inherited from `heap_fcp.dart.md`. PUBLIC-MUTABLE-FIELD-VS-
      AUTO-PROPERTY nuance (explicitly addressed): the Dart source
      exposes mutable fields directly (`cx.kappa = entryPc;
      cx.mode = UnifyMode.write; cx.S = 0;`); the faithful C#
      counterpart is `{ get; set; }` auto-properties (public
      setters), NOT `{ get; private set; }` — the entire runner
      consists of external mutators of these fields, and the
      property setter (which the JIT inlines to a direct field
      write) preserves the exact source ergonomic. NULL-SAFETY:
      `currentStructure`, `guardArgSlot`, `reductionBudget`, `E`,
      `CP`, `goalHead`, `goalProcName`, `onActivation`,
      `onReduction`, `termFormatter`, `moduleContext` all preserved
      as nullable; the non-nullable fields (`rt`, `goalId`,
      `kappa`, `env`, `inBody`, `mode`, `S`, `reductionsUsed`,
      `showBindings`, `debugOutput`) remain non-nullable. CRITICAL
      CARRY-FORWARD: the runner's CONCURRENCY contract is INHERITED
      from `heap_fcp.dart.md` escalations[0] — a RunnerContext is
      owned by exactly one isolate / one OS thread / one task
      scheduler at a time; mutators are SINGLE-WRITER per-goal. NOT
      re-escalated here per task instruction.

  - construct_key: dart.bytecode_runner.dispatch_loop_per_goal_with_reduction_budget_and_runstatus
    source_form: >-
      `class BytecodeRunner { final BytecodeProgram prog;
      BytecodeRunner(this.prog); void run(RunnerContext cx) {
      runWithStatus(cx); } RunResult runWithStatus(RunnerContext cx) {
      var pc = cx.kappa; while (pc < prog.ops.length) { if
      (cx.reductionBudget != null && cx.reductionsUsed >=
      cx.reductionBudget!) return RunResult.outOfReductions;
      cx.reductionsUsed++; final op = prog.ops[pc]; if (op is Label) {
      pc++; continue; } if (op is ClauseTry) { ... } if (op is Push)
      { ... } if (op is UnifyStructure) { ... } ... ~44 branches
      total ... pc++; } return RunResult.terminated; } ... helpers
      ... }` — the heart: a tight `while` loop reading `prog.ops[pc]`
      and pattern-matching the opcode type (44 `if (op is X)` arms),
      with `continue` for arms that set `pc` explicitly (jumps) and
      a fall-through `pc++; continue;` for sequential arms; the
      default trailing `pc++;` advances past unmatched opcodes (e.g.
      no-ops). Cooperative yielding: every iteration checks
      `reductionBudget` and returns `RunResult.outOfReductions` if
      exceeded; otherwise terminates when `pc >= ops.length`
      (return `RunResult.terminated`) or returns
      `RunResult.suspended` from NoMoreClauses/SuspendEnd or
      `RunResult.terminated` from Proceed/Halt.
    target_decision: >-
      A reference `class BytecodeRunner` with `IReadOnlyList<object>
      ops` cached from `prog.Ops` and a method `RunResult
      RunWithStatus(RunnerContext cx)`. The dispatch loop is a `while
      (pc < ops.Count)` with a chain of `if (op is X opx) { ...
      pc++; continue; }` pattern-matches mirroring Dart 1:1 — same
      ORDER, same `continue` semantics, same `pc` flow. The
      reduction-budget check at loop head becomes `if
      (cx.ReductionBudget is int budget && cx.ReductionsUsed >=
      budget) return RunResult.OutOfReductions;` (uses pattern-match
      to extract the int from the nullable). Increment
      `cx.ReductionsUsed++`. Dart `prog.labels[name]!` (non-null
      assertion) maps to C# `prog.Labels[name]` which throws
      `KeyNotFoundException` on missing key — semantically equivalent
      to Dart `Map[k]!` throwing on null (the source NEVER intends a
      label miss in a well-formed bytecode program; preserving the
      throw is correct — the compiler's emit pass guarantees labels
      exist). Helper methods `_findNextClauseTry(fromPc)`,
      `_softFailToNextClause(cx, pc)`, `_finalUnboundVar(cx, addr)`,
      `_suspendAndFail(cx, readerId, pc)`,
      `_suspendAndFailMulti(cx, readerIds, pc)` are private
      instance methods on the C# class (NOT `static` — they need
      `prog.Ops` for `_findNextClauseTry` and they accept `cx`); the
      static helpers `_formatTerm`, `_dereferenceWithTracking`,
      `_isArithmeticOp`, `_evaluateArithmetic`, `_evaluateGuard`,
      `_termsEqual` remain `private static`. Dart `print(...)` debug
      calls (`if (cx.debugOutput) print(...)`) map to
      `Console.WriteLine(...)` GUARDED by the `cx.DebugOutput`
      check; NOT `Trace.WriteLine` (Dart `print` is a stdout sink in
      this codebase; matching the side-effect surface matters for
      debugging parity). The runner is NOT made async (no `Task`,
      no `await`) — every opcode handler is synchronous; the only
      asynchronicity is `dart:async.Timer` in wait/wait_until guards
      (handled by the construct above).
    idiom_id: null
    research_finding_id: rf-dart-pattern-match-cascade-dispatch-to-csharp-is-pattern-cascade
    nuance: >-
      DISPATCH-SHAPE NUANCE (LOAD-BEARING, explicitly addressed):
      the 44-arm `is X` cascade is the source's PERFORMANCE-CRITICAL
      shape; Microsoft Learn (pattern-matching) documents that the C#
      compiler lowers this into a sequence of type-tests that the JIT
      can specialise. Codegen MUST PRESERVE the ORDER of arms (the
      Dart source has frequently-reached opcodes EARLY in the cascade
      — `Label`, `ClauseTry`, `GuardFail` are first; `Halt`/`Proceed`
      are last). Refactoring to a `Dictionary<Type, Action<...>>` or
      a `switch (op.GetType()) { case Type t when t == typeof(X): }`
      is REJECTED — both add an indirection per dispatch (table
      lookup or type-equality check) that the source avoids; both
      lose the short-circuit fall-through; both could lose JIT
      type-test specialisation. A future CODEGEN OPTIMISATION may
      introduce a per-opcode integer `Kind` field on `IOp`/`IOpV2`
      and a `switch (op.Kind)` (tableswitch) — that is EXPLICITLY
      OUT OF SCOPE for this spec. ASYNC NUANCE (explicitly
      addressed): the runner is FULLY SYNCHRONOUS; the only timer
      is in `wait`/`wait_until` guards and the heap mutation it
      performs happens via the goal-queue reactivation protocol
      (post-binding wake), NOT via `await`. Codegen MUST NOT turn
      `RunWithStatus` into `async Task<RunResult>` — would force
      every caller (the scheduler in `runtime.dart`) into async,
      ripple through the entire runtime, and silently introduce
      Task-allocation cost on every dispatch. Suspension is achieved
      by RETURNING `RunResult.Suspended` and recording the goal in
      the runtime's suspension table — NOT by awaiting; the
      reactivation path enqueues the goal back on the goal queue and
      a future `RunWithStatus` call resumes from `cx.Kappa`. NULL-
      SAFETY: opcode pattern-matches use `if (op is X x)` which
      binds a non-nullable `x` for the matched arm (Dart's `op is X`
      is equivalent under NRT).

  - construct_key: dart.opcode_dispatch_arms.head_phase_unification_two_phase_si_sigmahat_family
    source_form: >-
      The HEAD-phase opcode family — instructions that consult
      `cx.env.arg(slot)` (or `cx.clauseVars[idx]`), test whether the
      argument is a writer / reader / ground / bound /unbound /
      already-tentatively-bound via σ̂w, and either: (a) record a
      tentative binding in σ̂w; (b) add an unbound reader to Si
      (TWO-PHASE: Si is resolved against σ̂w at Commit); (c)
      soft-fail to next clause via `_softFailToNextClause` +
      `_findNextClauseTry`; (d) enter WAM WRITE mode (build
      `_TentativeStruct`); or (e) enter WAM READ mode (set
      `currentStructure = arg`). Arms: `HeadConstant`,
      `HeadStructure`, `UnifyConstant`, `UnifyVoid`,
      `UnifyStructure`, `HeadNil`, `HeadList`, `opv2.HeadVariable`,
      `opv2.UnifyVariable`, `opv2.SetVariable`, `opv2.Unknown`.
      Heavy use of `cx.rt.heap.isWriter(addr)` / `isReader(addr)` /
      `isWriterBound(addr)` / `isReaderBound(addr)` / `isFullyBound
      (addr)` / `valueOfWriter(addr)` / `getReaderValue(addr)` /
      `getValue(addr)` / `derefAddr(addr)` / `pairedReaderAddr(addr)`
      / `tryWriterForReader(addr)` / `dereference(term)` — all
      already specced under `heap_fcp.dart.md`. VarRef-chain
      dereferencing is open-coded in many arms (`while (value is
      VarRef) { ... }`).
    target_decision: >-
      Each arm maps 1:1 to a C# `if (op is X opx) { ... pc++;
      continue; }` arm with the SAME control flow. Tentative
      bindings: `cx.SigmaHat[arg.Addr] = new ConstTerm(opx.Value);`
      and `cx.SigmaHat[wid] = nested;` etc. — the σ̂w is a
      Dictionary<int, object?>. Si membership: `cx.Si.Add(addr);`
      then `pc++; continue;` (two-phase semantics: do NOT soft-fail
      yet — let Commit resolve Si against σ̂w). Soft-fail family:
      `_softFailToNextClause(cx, pc); pc = _findNextClauseTry(pc);
      continue;` — pure delegation to the helpers. Mode conversion
      (writer-where-structure-expected): construct
      `_TentativeStruct(functor, arity, new object?[arity])`
      (mutable array of nullables — preserving Dart `List.filled(n,
      null)`), assign to σ̂w, switch `cx.Mode = UnifyMode.Write;`,
      set `cx.CurrentStructure = nested; cx.S = 0;`. READ-mode entry:
      `cx.CurrentStructure = value; cx.Mode = UnifyMode.Read; cx.S
      = 0;`. The open-coded VarRef-chain dereferencing loops
      (`while (value is VarRef) { if (heap.isReader(value.addr))
      ... else ... }`) translate VERBATIM to C# `while (value is
      VarRef vr) { ... }` — every branch identical. `cx.clauseVars`
      becomes `cx.ClauseVars` (Dictionary<int, object?>) with the
      same write-once-then-check semantics (`existingValue ==
      null ? store : compare-and-soft-fail-on-mismatch`).
      `_StructureState` is used as a "stash-into-clauseVar-and-
      restore" placeholder by Push/Pop — translates to a private
      `internal sealed class StructureState { public int S { get; }
      public UnifyMode Mode { get; } public object?
      CurrentStructure { get; } public StructureState(int s,
      UnifyMode mode, object? cs) { ... } }` and stored as
      `cx.ClauseVars[op.RegIndex] = new StructureState(cx.S,
      cx.Mode, cx.CurrentStructure);`. The `dynamic`-typed Dart
      field on `_StructureState.currentStructure` becomes `object?`
      per the carry-forward (NOT C# `dynamic`). The frequently-
      repeated logging guard `if (cx.debugOutput) print('[DEBUG]
      PC $pc: ...')` translates to `if (cx.DebugOutput)
      Console.WriteLine($"[DEBUG] PC {pc}: ...");` — preserve EVERY
      string literal and EVERY interpolation slot (the trace
      surface is observable in tests).
    idiom_id: null
    research_finding_id: rf-dart-three-valued-unification-dispatch-arm-to-csharp-equivalent
    nuance: >-
      TWO-PHASE-COMMIT NUANCE (LOAD-BEARING, explicitly addressed):
      Si holds readers that made HEAD matching indeterminate; the
      runner DOES NOT eagerly soft-fail on an unbound reader — it
      records the reader in Si and continues HEAD/GUARD. At Commit,
      Si is resolved against σ̂w (if the writer is in σ̂w then the
      reader will be bound when σ̂w is applied; otherwise the
      reader remains unresolved and is promoted to U). This is the
      load-bearing FCP three-phase execution contract from
      `runtime-spec.txt` / `suspension.dart.md`. Codegen MUST NOT
      collapse Si into U eagerly. NULLABILITY NUANCE (explicitly
      addressed): the runner stores raw `int` (varIndex / writerAddr
      / readerAddr) AND `Term` objects AND raw constants in the same
      `Object?` (Dart) / `object?` (C#) slot — the type tests
      (`is int`, `is VarRef`, `is StructTerm`, `is ConstTerm`, `is
      _ClauseVar`, `is _TentativeStruct`) ARE the type discipline.
      The codegen MUST PRESERVE these `is` tests verbatim — the
      heterogeneous slot is the source's representation choice
      (mirrors the FCP heap-cell `dynamic content` decision from
      `heap_fcp.dart.md`). PERFORMANCE NUANCE (explicitly
      addressed, NOT glossed): every `cx.SigmaHat[addr]` /
      `cx.ClauseVars[idx]` dictionary lookup is on the hot path.
      `Dictionary<int, object?>.TryGetValue` is the .NET-idiomatic
      "check + read" idiom (Microsoft Learn) and is what codegen
      should emit when both `ContainsKey` and `[]` appear adjacent
      (Dart source does `containsKey(...) then [...]` — codegen MAY
      collapse to `TryGetValue(addr, out var v)` as a faithful
      single-lookup optimisation; the source's intent is preserved).
      `Span<T>` is NOT applicable here — Dictionary uses an internal
      hashtable, not contiguous memory; codegen MUST NOT attempt
      `Span<KeyValuePair<int,object?>>` here.

  - construct_key: dart.opcode_dispatch_arms.commit_two_phase_resolve_si_apply_sigmahat_wake
    source_form: >-
      `if (op is Commit) { ... resolvedSi check ... convert
      _TentativeStruct to StructTerm ... enforce WxW prohibition ...
      CommitOps.applySigmaHatFCP(heap: cx.rt.heap, sigmaHat:
      convertedSigmaHat) ... for (final a in acts) { cx.rt.gq.
      enqueue(a); if (cx.onActivation != null) cx.onActivation!(a); }
      ... cx.sigmaHat.clear(); cx.argSlots.clear();
      cx.currentStructure = null; cx.S = 0; cx.mode =
      UnifyMode.read; cx.parentStack.clear(); cx.inBody = true; pc++;
      continue; }` — the load-bearing commit operator. Phase 2 of
      two-phase HEAD unification: resolve Si against σ̂w. For each
      reader in Si, look up its paired writer (via
      `tryWriterForReader` — handles imported readers). If the
      writer is NOT in σ̂w, the reader stays unresolved → promote
      to U + soft-fail. Otherwise the reader will be bound when σ̂w
      is applied. If ALL Si entries resolve, convert tentative
      structures (recursively, including `_ClauseVar` placeholders
      → allocate fresh variable + thread through via
      `cx.clauseVars`), enforce WxW prohibition (writer→writer
      binding is `StateError`), call
      `CommitOps.applySigmaHatFCP(...)` which returns reactivated
      `GoalRef`s, enqueue them, transition to BODY (set inBody=true).
    target_decision: >-
      A direct 1:1 translation: `if (op is Commit) { ... }`. Phase 2
      resolve loop: `var resolvedSi = new HashSet<int>(); foreach
      (var readerAddr in cx.Si) { var writerAddr =
      cx.Rt.Heap.TryWriterForReader(readerAddr); if (writerAddr is
      null || !cx.SigmaHat.ContainsKey(writerAddr.Value))
      resolvedSi.Add(readerAddr); } if (resolvedSi.Count > 0) {
      cx.U.UnionWith(resolvedSi); cx.Si.Clear();
      _softFailToNextClause(cx, pc); pc = _findNextClauseTry(pc);
      continue; } cx.Si.Clear();`. Tentative-struct conversion
      (`_convertTentativeToStruct` helper) is the recursive walk
      that turns `_TentativeStruct` (placeholder-bearing) into
      `StructTerm` (resolved) — codegen MUST preserve the per-
      `_ClauseVar` placeholder semantics: if `cx.ClauseVars[idx]`
      holds a `VarRef`, use it; if it holds a writer address as
      bare int, build the appropriate `VarRef(addr)` using
      `pairedReaderAddr` (for reader placeholders bound to writer
      addresses) or `tryWriterForReader` (for writer placeholders
      bound to reader VarRefs); if it holds a `Term`, use as-is;
      if it's not yet resolved, allocate a fresh writer/reader
      pair via `cx.Rt.Heap.AllocateVariable()` (a tuple-return per
      `heap_fcp.dart.md` rf-dart-record-return-to-csharp-valuetuple
      — emit `var (freshW, freshR) = cx.Rt.Heap.AllocateVariable();`
      OR `var pair = cx.Rt.Heap.AllocateVariable(); var freshW =
      pair.Item1; var freshR = pair.Item2;`). WxW enforcement:
      `foreach (var kvp in convertedSigmaHat) { if (kvp.Value is
      VarRef vr && cx.Rt.Heap.IsWriter(vr.Addr)) throw new
      InvalidOperationException($"WxW violation in commit:
      W{kvp.Key} -> W{vr.Addr} (both unbound writers)"); }` —
      maps `StateError` to `InvalidOperationException` per
      `heap_fcp.dart.md` rf-dart-staterror-to-csharp-invalidoperationexception
      (cache hit). Apply via `var acts =
      CommitOps.ApplySigmaHatFCP(heap: cx.Rt.Heap, sigmaHat:
      convertedSigmaHat);`. Goal enqueue loop preserves the Dart
      pattern: `foreach (var a in acts) { cx.Rt.Gq.Enqueue(a); if
      (cx.OnActivation is { } onA) onA(a); }`. State reset block
      preserved verbatim.
    idiom_id: null
    research_finding_id: rf-dart-two-phase-commit-operator-to-csharp-equivalent
    nuance: >-
      TWO-PHASE-COMMIT NUANCE (LOAD-BEARING, explicitly addressed):
      Commit is where Si is RESOLVED, not where Si is FIRST checked.
      The Dart source's intricate σ̂w → StructTerm conversion
      (handling `_ClauseVar` placeholders, fresh-variable
      allocation, paired-reader-address arithmetic, WxW
      prohibition) is the load-bearing kernel of the FCP runtime.
      Codegen MUST preserve every branch — particularly the
      `tryWriterForReader` (imported-reader handling) and the WxW
      prohibition (writer-to-writer binding is a CATEGORICAL
      invariant violation: writers MGU only against ground terms or
      readers, NEVER another writer — per `heap_fcp.dart.md`).
      WAKE-PROTOCOL NUANCE (explicitly addressed): Commit's
      side-effect surface is (a) heap mutation via
      `CommitOps.ApplySigmaHatFCP`, (b) goal-queue enqueues of
      reactivated goals, (c) `OnActivation` host hook fires. The
      reactivation set comes back FROM the commit operator (not
      computed here) — that's the source's contract with
      commit.dart (suspension.dart.md / commit.dart.md
      authoritative). Codegen MUST preserve the FOR-EACH order
      (Dart Map iteration is INSERTION ORDER per the Dart
      language spec; .NET Dictionary iteration order is
      UNDEFINED — see
      https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2#remarks).
      If the order of activations enqueued matters for any test
      (the existing convspecs do not document this as ordering-
      sensitive, but FCP determinism is a live concern), codegen
      should use `SortedDictionary<int, object?>` OR an
      insertion-ordered alternative (`List<KeyValuePair<int,
      object?>>` walked in insert order) — but per current source
      behavior (Dart insertion-order) the safest faithful
      translation is to USE A `Dictionary<int, object?>` AND
      ENSURE insertion-order is preserved by the iteration site
      (Microsoft Learn notes Dictionary preserves insertion order
      IN PRACTICE under .NET 5+, though not contractually).
      Codegen SHOULD use `OrderedDictionary<TKey,TValue>` (added in
      .NET 9 — see https://learn.microsoft.com/en-us/dotnet/api/system.collections.specialized.ordereddictionary)
      IF the codegen-stage target framework supports it, else
      a `List<KeyValuePair<int,object?>>` written by the
      same insertion sites as σ̂w. THREAD-SAFETY: inherited from
      `heap_fcp.dart.md` escalations[0] — Commit mutates the heap +
      goal queue, both under the runtime's single-owner-thread
      contract.

  - construct_key: dart.opcode_dispatch_arms.clause_control_clausenext_trynextclause_nomoreclauses_suspendend
    source_form: >-
      Clause-control family: `ClauseNext` (union Si into U, clear
      clause state, jump to label), `TryNextClause` (soft-fail
      pattern), `NoMoreClauses` (if U non-empty: suspend via
      `cx.rt.suspendGoalFCP(goalId: cx.goalId, kappa: cx.kappa,
      readerVarIds: cx.U)` and return RunResult.suspended; else
      terminate — all clauses failed definitively),
      `UnionSiAndGoto` (legacy), `ResetAndGoto` (legacy),
      `SuspendEnd` (legacy — same as NoMoreClauses).
    target_decision: >-
      Direct 1:1 translation. ClauseNext: `cx.U.UnionWith(cx.Si);
      cx.ClearClause(); pc = prog.Labels[op.Label]; continue;`.
      TryNextClause: `_softFailToNextClause(cx, pc); pc =
      _findNextClauseTry(pc); continue;`. NoMoreClauses: `if
      (cx.U.Count > 0) { cx.Rt.SuspendGoalFcp(goalId: cx.GoalId,
      kappa: cx.Kappa, readerVarIds: cx.U); cx.U.Clear();
      cx.InBody = false; return RunResult.Suspended; } cx.InBody
      = false; return RunResult.Terminated;`. Legacy
      `UnionSiAndGoto` / `ResetAndGoto` / `SuspendEnd` preserved
      verbatim — even though documented as legacy in the source,
      they MUST be translated to keep bytecode-compatibility with
      any pre-existing programs.
    idiom_id: null
    research_finding_id: rf-dart-three-valued-unification-dispatch-arm-to-csharp-equivalent
    nuance: >-
      SUSPENSION NUANCE (LOAD-BEARING, explicitly addressed):
      `suspendGoalFCP(goalId, kappa, readerVarIds)` is the entry
      point into the suspension protocol — the goal is recorded in
      the runtime's suspension table, indexed by the readers in U.
      Reactivation happens when one of those readers is bound (by
      a future Commit or BodySet*), at which point the goal is
      enqueued and `runWithStatus` is called again with `cx.Kappa`
      pointing to the saved kappa. Codegen MUST preserve this exact
      protocol — kappa is the PC of the procedure entry (not the
      current PC); the suspension mechanism is in
      `suspension.dart.md`. NULL-SAFETY: `cx.U` is non-nullable
      `HashSet<int>`; the count check is `cx.U.Count > 0` (NOT
      `cx.U is not null`).

  - construct_key: dart.opcode_dispatch_arms.body_phase_bindwriter_setconst_putconst_putstructure_putnil_putlist
    source_form: >-
      BODY-phase family — executes after `cx.inBody = true` (set by
      Commit). Arms: `HeadBindWriter`, `HeadBindWriterArg`,
      `BodySetConst` (bind writer to const, return activations,
      enqueue), `BodySetStructConstArgs` (bind writer to ground
      struct, enqueue activations), `BodySetConstArg` (set arg
      slot to const), `PutConstant`, `PutStructure` (WAM-style:
      open a fresh structure for arg setup),
      `SetConstant`/`SetValue`/`SetVariable` (fill positional
      args of the currently-open structure), `TailStep`, `PutNil`,
      `PutBoundConst`, `PutBoundNil`, `PutList`. Heavy use of
      `cx.rt.heap.bindWriterConst(addr, value)` and
      `bindWriterStruct(addr, functor, args)` — both return
      activations (FCP: every binding wakes suspended goals; per
      heap_fcp.dart.md / suspension.dart.md). Enqueue loop:
      `for (final a in acts) { cx.rt.gq.enqueue(a); if
      (cx.onActivation != null) cx.onActivation!(a); }`.
    target_decision: >-
      Direct 1:1 translation. `cx.Rt.Heap.BindWriterConst(addr,
      value)` and `BindWriterStruct(addr, functor, args)` return
      `IReadOnlyList<GoalRef>` (per
      `heap_fcp.dart.md`). Enqueue loop: `foreach (var a in acts)
      { cx.Rt.Gq.Enqueue(a); if (cx.OnActivation is { } onA)
      onA(a); }`. PutStructure / SetConstant family: the source
      uses a transient "currently-open structure" cursor (the
      `cx.argSlots[slot]` slot holds a `StructTerm` being filled,
      OR `cx.currentStructure` plus `cx.S` indexing into it).
      `PutNil` sets `cx.argSlots[slot] = new ConstTerm("nil");`
      (the `nil` atom convention — per
      `terms.dart.md`). `PutList` builds a `StructTerm(".", [head,
      tail])` cons cell. All `inBody` guards preserved as
      `if (cx.InBody) { ... }`. Argument register clears
      (`cx.argSlots.clear()`) preserved.
    idiom_id: null
    research_finding_id: rf-dart-body-phase-write-dispatch-to-csharp-equivalent
    nuance: >-
      WAKE-ON-BINDING NUANCE (LOAD-BEARING, explicitly addressed):
      every `bindWriterConst`/`bindWriterStruct` call returns a list
      of reactivated `GoalRef`s — per FCP design, every binding
      potentially wakes goals suspended on the paired reader. The
      runner MUST enqueue them and fire the `OnActivation` host
      hook (used by trace logging). Codegen MUST NOT drop the
      return value of `BindWriter*` calls — that would silently
      lose reactivations. Inherited contract from
      `heap_fcp.dart.md` / `suspension.dart.md`. LIST-CONSTRUCTION
      NUANCE: lists are encoded as `StructTerm(".", [head, tail])`
      with `ConstTerm("nil")` (the atom) as the empty-list sentinel
      — NOT as a separate `ListTerm` type. Codegen MUST preserve
      this encoding (the entire compiler / type-checker / printer
      depends on it — see `terms.dart.md`).

  - construct_key: dart.opcode_dispatch_arms.spawn_requeue_distribute_transmit_module_rpc
    source_form: >-
      Goal-spawning + module-RPC family: `Spawn(procedureLabel,
      arity)` — look up entry PC via `prog.labels[label]`; if not
      found, try a body kernel via `cx.rt.bodyKernels.lookup(name,
      arity)` and execute inline (synchronous side effect, no goal
      spawned); else allocate a fresh `GoalRef(newGoalId, entryPc)`,
      install a new `CallEnv` from `cx.argSlots`, set goal-env /
      goal-program / infrastructure-goal-status on the runtime,
      enqueue. `Requeue(procedureLabel)` — TAIL CALL: re-use
      current goal, update env from argSlots, clear clause/body
      state, set `cx.kappa = entryPc`, jump to entryPc.
      `Distribute(importIndex, functor, arity)` — static module
      RPC: look up `ReplModuleContext.imports[importIndex]`; build
      a `StructTerm(functor, args)`; send via the target's
      GlpChannel (`cx.rt.glpChannels[target.name]`), enqueue
      activations. `Transmit(moduleVarIndex, functor, arity)` —
      dynamic module RPC: resolve module name from clauseVars
      (dereferencing VarRef + checking ConstTerm.value); look up
      GlpChannel; send and enqueue activations.
    target_decision: >-
      Direct 1:1 translation. Spawn: `var entryPc =
      prog.Labels.GetValueOrDefault(op.ProcedureLabel, -1); if
      (entryPc < 0) { var parts = op.ProcedureLabel.Split('/');
      var name = parts[0]; var kernel =
      cx.Rt.BodyKernels.Lookup(name, op.Arity); if (kernel is
      not null) { var args = new object?[op.Arity]; for (var i =
      0; i < op.Arity; i++) args[i] = cx.ArgSlots.GetValueOrDefault
      (i); var result = kernel(cx.Rt, args); if (result ==
      BodyKernelResult.Abort) { Console.WriteLine($"ERROR: Body
      kernel {name}/{op.Arity} aborted"); return
      RunResult.Terminated; } cx.ArgSlots.Clear(); pc++;
      continue; } Console.WriteLine($"ERROR: Spawn could not find
      procedure label: {op.ProcedureLabel}"); return
      RunResult.Terminated; }`. Goal allocation: `var newGoalId
      = cx.Rt.NextGoalId++; var newGoalRef = new GoalRef(newGoalId,
      entryPc); var newEnv = new CallEnv(new Dictionary<int,
      Term>(cx.ArgSlots));` — uses the Dictionary copy
      constructor; preserves the source's `Map<int, Term>.from(...)`
      idiom. Trace-name formatting (`spawnedGoals.add(goalStr)`)
      preserved exactly. Goal-program inheritance: `var
      parentProgram = cx.Rt.GetGoalProgram(cx.GoalId); if
      (parentProgram is not null) cx.Rt.SetGoalProgram(newGoalId,
      parentProgram);`. Infrastructure-goal propagation:
      `if (cx.Rt.InfrastructureGoalIds.Contains(cx.GoalId))
      cx.Rt.InfrastructureGoalIds.Add(newGoalId);`. Requeue
      (tail call): preserved — `cx.Kappa = entryPc; pc = entryPc;
      continue;`. Distribute/Transmit: preserved with the same
      `cx.ModuleContext is ReplModuleContext replCtx` pattern-match
      gate, GlpChannel lookup, `replCtx.Imports.GetValueOrDefault
      (op.ImportIndex)` resolution.
    idiom_id: null
    research_finding_id: rf-dart-goal-spawning-and-rpc-dispatch-to-csharp-equivalent
    nuance: >-
      TAIL-CALL NUANCE (LOAD-BEARING, explicitly addressed): Requeue
      RE-USES the current goal (`cx.GoalId` stays the same);
      `cx.Kappa` is REPOINTED to the new procedure's entry PC so
      that on a future suspension/reactivation the runtime resumes
      at the right procedure. .NET has NO native tail-call
      semantics here (the runner is a synchronous interpreter; no
      stack-frame reuse needed — the tail call is JUST a `pc` /
      `kappa` reassignment), so codegen MUST preserve the source's
      manual state-reset pattern (`cx.SigmaHat.Clear(); cx.U.Clear();
      cx.ClauseVars.Clear(); cx.InBody = false; cx.Mode =
      UnifyMode.Read; cx.S = 0; cx.CurrentStructure = null;`).
      MODULE-RPC NUANCE (LOAD-BEARING): module dispatch goes through
      GlpChannel — the channel `send(goalTerm)` returns a list of
      activations that the runner enqueues. Codegen MUST NOT
      synthesise an async/await call here — the channel
      send is synchronous in the source. THREAD-SAFETY: every
      module-RPC `glpChannel.send(...)` mutates remote state; the
      thread-safety contract is inherited from
      `heap_fcp.dart.md` escalations[0] + the future
      glp_channels convspec.

  - construct_key: dart.opcode_dispatch_arms.guard_dispatch_three_valued_predicate_evaluation_with_arithmetic
    source_form: >-
      Guard family — `Guard(predicateName, arity)`, `Ground(slot)`,
      `Known(slot)`, `NoReaders(slot)`, `GroundEqual(left, right)`
      — gather argument terms (from argSlots / clauseVars),
      dereference each via `_dereferenceWithTracking(term, cx)`
      (which returns `(Object? deref, Set<int> unboundReaders)`),
      check for unbound readers (if non-empty: add to Si + pc++
      continue — two-phase suspension), else call
      `_evaluateGuard(name, args, cx)` which returns
      `GuardResult.success`/`failure` (Suspend is documented but
      unreached). On failure, soft-fail. On success, advance.
      `_evaluateGuard` is a massive `switch (predicateName)` with
      arms: `<` / `>` / `=<` / `>=` / `=:=` / `=\=` (arithmetic
      comparisons with `evaluateNumeric` that recursively walks
      StructTerm arithmetic expressions `+`, `-`, `*`, `/`, `//`,
      `mod`, `neg`), `ground`, `known`, `integer`, `string`,
      `constant`, `number`, `list`, `compound`, `module`,
      `is_mutual_ref`, `unknown`, `otherwise`, `wait`,
      `wait_until`, `=?=`, default (warn + fail). `wait` /
      `wait_until` allocate a fresh reader/writer pair, set up a
      `Timer`, and use a state-machine on `cx.rt.getWaitReader
      (cx.goalId)` to track first-call-vs-resume.
    target_decision: >-
      Direct 1:1 translation. The `Guard` arm gathers args, calls
      `_dereferenceWithTracking`, threads the tracked unbound-
      reader set into `cx.Si`, and on `unboundReaders.Count > 0`
      does `cx.Si.UnionWith(unboundReaders); pc++; continue;`.
      Otherwise calls `_evaluateGuard(op.ProcedureLabel, args,
      cx)`. `_evaluateGuard` is a private static method returning
      `GuardResult`, with a top-level `switch (predicateName)`
      statement and one C# `case "<":` etc. arm per Dart `case '<':`
      — same string literals. `evaluateNumeric` is a local function
      (C# supports local functions) that recursively walks
      `StructTerm` arithmetic expressions; preserves the `case '+'`
      / `case '-'` / `case '*'` / `case '/'` / `case '//'` / `case
      'mod'` / `case 'neg'` switch verbatim. Dart `(a is num && b is
      num)` becomes `(a is double da && b is double db)` OR
      `(a is IConvertible ia && b is IConvertible ib)` — but more
      faithfully, since Dart `num` covers both `int` and `double`,
      the C# counterpart uses a small helper `static bool TryAsNum
      (object? v, out double result) { switch (v) { case int i:
      result = i; return true; case double d: result = d; return
      true; case ConstTerm ct: return TryAsNum(ct.Value, out
      result); default: result = 0; return false; } }`. Arithmetic
      `~/` (Dart integer division) maps to C# `(int)(a / b)`
      (truncating integer division on doubles); `%` (Dart `mod`)
      maps to C# `%`. `wait` / `wait_until`: allocate variable via
      `cx.Rt.Heap.AllocateVariable()` (ValueTuple), set up
      `System.Threading.Timer` with one-shot
      `dueTime: TimeSpan.FromMilliseconds(duration)`, period:
      `Timeout.InfiniteTimeSpan`, callback binds the writer and
      enqueues reactivations on the owning scheduler (per the
      timer-callback marshalling nuance from the dart:async import
      construct above). `=?=` ground-equality: preserve `_termsEqual`
      (recursive structural-equality with VarRef dereferencing
      and cycle detection via `HashSet<(int, int)>`).
    idiom_id: null
    research_finding_id: rf-dart-guard-evaluation-with-suspend-tracking-to-csharp-equivalent
    nuance: >-
      THREE-VALUED GUARD NUANCE (LOAD-BEARING, explicitly
      addressed): guards return Success / Failure / Suspend in
      theory; current impl materialises Suspend BEFORE
      `_evaluateGuard` via `_dereferenceWithTracking`'s
      out-parameter "set of unbound readers", which the Guard arm
      adds to Si. This split (track-then-evaluate-pure) is the
      source's design choice; codegen MUST preserve it — moving
      the unbound-reader tracking INTO `_evaluateGuard` would
      tangle suspension protocol with predicate evaluation.
      ARITHMETIC-NUM NUANCE (explicitly addressed): Dart `num` is
      the supertype of `int` + `double`; C# has no exact
      counterpart. Microsoft Learn (numeric types) confirms `int`
      and `double` are distinct value types. The faithful
      translation uses a private `TryAsNum(object?, out double)`
      helper that handles `int` / `double` / `ConstTerm` wrappers
      uniformly, returning a `double` (preserves Dart's automatic
      int-to-double promotion in `a + b` when one is double; for
      integer-mod (`mod`) and integer-divide (`//`), the source
      already does `.toInt() %` — preserved as
      `(int)da % (int)db`). The `=:=` arithmetic-equality is
      `da == db` (double equality — exact). The wait/wait_until
      idioms inherit the dart:async import construct's
      marshalling-back-to-owner contract. NULL-RETURN NUANCE
      (explicitly addressed): `evaluateNumeric` returns `num?`
      (nullable) — the null means "could not evaluate
      arithmetically"; the C# counterpart returns `bool` + `out
      double` (the Microsoft Learn TryParse idiom) or `double?`.
      Codegen MAY choose either — the source's null-as-not-
      evaluable maps faithfully to `double?` returns.

  - construct_key: dart.opcode_dispatch_arms.misc_environment_frame_utility_proceed_halt_nop_allocate_deallocate
    source_form: >-
      `Allocate(size)` — push a new `EnvironmentFrame` onto the E
      register: `cx.E = EnvironmentFrame(parent: cx.E,
      continuationPointer: cx.CP ?? 0, size: op.size);`.
      `Deallocate` — pop: restore parent + CP. `Nop` — no-op.
      `Halt` — return `RunResult.terminated`. `Proceed` — call
      `onReduction` host hook + `return RunResult.terminated;`
      (procedure complete). Also: `RequireWriterArg(slot,
      failLabel)` / `RequireReaderArg(slot, failLabel)` — mode-
      selection: if argument at slot is not the required
      writer/reader, jump to `failLabel`.
    target_decision: >-
      Direct 1:1 translation. `Allocate`: `cx.E = new
      EnvironmentFrame(parent: cx.E, continuationPointer: cx.CP
      ?? 0, size: op.Size);`. `Deallocate`: `cx.CP = cx.E?
      .ContinuationPointer; cx.E = cx.E?.Parent;`. `Nop`: `pc++;
      continue;`. `Halt`: `return RunResult.Terminated;`.
      `Proceed`: invoke `cx.OnReduction` hook then `return
      RunResult.Terminated;`. `RequireWriterArg` /
      `RequireReaderArg`: same as source — `if (arg is null ||
      (arg is VarRef vr && cx.Rt.Heap.IsReader(vr.Addr))) { pc =
      prog.Labels[op.FailLabel]; continue; } pc++; continue;`.
    idiom_id: null
    research_finding_id: rf-dart-environment-frame-and-utility-dispatch-to-csharp-equivalent
    nuance: >-
      ENVIRONMENT-FRAME NUANCE (explicitly addressed): the E
      register is a linked list of `EnvironmentFrame`s (each
      holding `permanentVars`); a NEW Allocate pushes a new
      frame whose `parent` is the current `cx.E`. Deallocate
      restores the parent. This is the WAM environment stack — the
      Dart impl uses heap-allocated frames (not a stack data
      structure) because each frame holds a fixed-size `permanentVars`
      array that escapes its allocation site (referenced by Y-
      register loads later in the procedure). C# class allocation
      is correct (already specced under the simple-data-class
      construct). NULL-SAFETY: `cx.CP` and `cx.E` are nullable
      (`int?`, `EnvironmentFrame?`); the `cx.CP ?? 0` fallback
      preserved as the same `??` operator. Microsoft Learn
      confirms `??` works identically in C# and Dart for nullable
      operands.

  - construct_key: dart.private_helper.tentativestruct_clausevar_liststruct_structurestate_argpinfo_data_holders
    source_form: >-
      Five file-private helper classes (leading-`_`): (a)
      `_TentativeStruct { final String functor; final int arity;
      final List<Object?> args; ... toString => '...'; }` — open
      mutable slot vector during HEAD WRITE phase (args are filled
      in via `cx.S++`-advance + `parent.args[cx.S] = nested`); (b)
      `_ClauseVar { final int varIndex; final bool isWriter; ... }`
      — placeholder for a clause-variable position inside a
      tentative structure; (c) `_ListStruct { final Object? head;
      final Object? tail; }` — list cell; (d) `_StructureState
      { final int S; final UnifyMode mode; final dynamic
      currentStructure; }` — Push/Pop save state (the `dynamic` on
      currentStructure mirrors the RunnerContext field); (e)
      `_ArgInfo { final int? writerId; final int? readerId; bool
      get isWriter; bool get isReader; }` — argument-mode wrapper
      (vestigial — not heavily used).
    target_decision: >-
      Each maps to an `internal sealed class` (file-private has
      no exact C# counterpart; `internal` + `sealed` + no public
      ctor consumers is the closest fit; if the codegen-stage
      target framework is C# 11+, `file class` is the BETTER
      match — file-scoped visibility, exactly mirroring Dart's
      leading-`_` privacy). Get-only auto-properties for `final`
      fields; the mutable `_TentativeStruct.args` list field
      becomes `public IList<object?> Args { get; }` (the list
      reference is immutable but the elements ARE mutated in place
      via `args[s] = nested`). `_ClauseVar` `bool isWriter` named-
      required becomes a regular ctor parameter per
      `opcodes_v2.dart.md`. `_StructureState.currentStructure`
      typed `dynamic` (Dart wildcard) becomes `object?` per the
      carry-forward dynamic-vs-object nuance. The `toString` overrides
      become `override string ToString() => $"...";`. NO records
      (per heap_fcp.dart.md — these are mutated-in-place / used
      with reference identity). `_ListStruct` is RETAINED even
      though largely vestigial — preserves source surface.
      `_convertTentativeToStruct(tentative, cx)` (top-level free
      function in Dart) becomes a `private static StructTerm`
      method on `BytecodeRunner` (C# has no top-level functions
      outside C# 9 top-level statements; making it static on the
      class is the idiomatic fit). NULLABILITY: `_ArgInfo.writerId`
      / `readerId` preserved as `int?`.
    idiom_id: null
    research_finding_id: rf-dart-private-class-with-mutable-list-args-to-csharp-internal-sealed-class
    nuance: >-
      PRIVATE-VISIBILITY NUANCE (LOAD-BEARING, explicitly
      addressed): Dart file-privacy (`_` prefix) gates these
      types to the runner.dart library only; the closest C#
      counterpart is `file class` (C# 11+) — exactly file-scoped
      visibility — OR `internal sealed class` (assembly-scoped,
      slightly broader). The codegen MUST PRESERVE the access
      gate: these helpers are runner internals, not part of the
      public bytecode API. VALUE-VS-REFERENCE: `_TentativeStruct`
      holds a mutable List<object?> that gets sequentially
      populated; it MUST be a reference class (mutation
      propagates through the σ̂w slot that points to the same
      instance). NULL-SAFETY: every `Object?` field maps to
      `object?`; the `args` list elements are nullable
      (placeholder positions are initially null until filled).

  - construct_key: dart.dispatcher_helper.formatterm_static_term_pretty_printer
    source_form: >-
      `static String _formatTerm(GlpRuntime rt, Term term, {bool
      markReaders = true})` — recursively pretty-prints a Term:
      ConstTerm → value.toString() (with 'nil' → '[]' and null
      → '<null>'); VarRef writer → 'Xid' (or recursively format
      bound value); VarRef reader → 'Xid?' (with `markReaders`
      controlling the `?` suffix; bound reader → format value);
      StructTerm with functor '.' and arity 2 → list rendering
      with cycle detection via visited Set<int>; general
      StructTerm → 'functor(arg1,arg2,...)'. Used by Spawn /
      Requeue / reformatHead for trace output.
    target_decision: >-
      A private static method `string _FormatTerm(GlpRuntime rt,
      Term term, bool markReaders = true)` returning a `string`.
      Recursive structure preserved; uses `StringBuilder` + `Append`
      for the general-structure case. The `visited` set is a
      `HashSet<int>` allocated PER TOP-LEVEL CALL (per Dart source
      — preserves cycle-detection scope). Microsoft-Learn-style
      string interpolation `$"X{wid - 1000}"` (with display-id
      adjustment for variables ≥ 1000) preserved. The `markReaders`
      named-defaulted parameter becomes a C# optional parameter with
      default `true` per `opcodes.dart.md`.
    idiom_id: null
    research_finding_id: rf-dart-tostring-interp-to-csharp-tostring-interp
    nuance: >-
      DEBUG-SURFACE NUANCE (explicitly addressed): trace formatting
      is observable in tests and the REPL; every literal (`'[]'`,
      `'<null>'`, `'?'` suffix, `'<circular>'`, `'$functor($args)'`)
      MUST be preserved byte-for-byte. NULL-SAFETY: `term` is
      non-null (per the type system); the formatter handles
      `ConstTerm(null)` → `<null>` and `ConstTerm('nil')` → `[]`
      via runtime checks. Cache hit from `opcodes.dart.md` for
      tostring/interp.

  - construct_key: dart.dispatcher_helper.dereferencewithtracking_recursive_walk_with_unbound_reader_tracking
    source_form: >-
      `static (Object?, Set<int>) _dereferenceWithTracking(Object?
      term, RunnerContext cx)` — recursive walk that follows
      VarRef chains through clauseVars / sigmaHat / heap to a
      ground value (or unbound variable); accumulates unbound
      reader addresses into a tracked `Set<int>`. Returns a
      ValueTuple `(deref, trackedReaders)`. Used by guard
      evaluation to detect three-valued unification suspend.
      Unwraps `ConstTerm` to primitive on the way out (so the
      caller sees `42` not `ConstTerm(42)`).
    target_decision: >-
      A private static method returning `(object? Deref,
      HashSet<int> UnboundReaders)` — C# 7+ ValueTuple, with
      named tuple elements for readability. Internal closure
      `Dereference(object? t)` lifted to a local function (C#
      supports local functions with access to enclosing local
      `unboundReaders` HashSet — codegen MUST preserve the closure
      pattern; converting to a static helper would require
      threading the reader set as an explicit parameter and would
      diverge from the source). Recursive resolution preserved:
      VarRef → check clauseVars → check sigmaHat → check heap
      (`isReaderBound` / `isFullyBound`) → recurse on bound value
      → return final result. ConstTerm unwrap preserved.
    idiom_id: null
    research_finding_id: rf-dart-record-return-to-csharp-valuetuple
    nuance: >-
      DEREFERENCE-CHAIN NUANCE (LOAD-BEARING, explicitly
      addressed): the order of consultation is sigmaHat → heap
      (sigmaHat hides heap state for the current clause — the
      tentative-binding invariant); codegen MUST preserve this
      order. CONSTTERM-UNWRAP NUANCE: the source unwraps
      `ConstTerm` to `value` on the way out (so the caller sees
      the primitive, not the wrapper); this is the source's
      contract with `_evaluateGuard` (which then does `if (val is
      num)` rather than `if (val is ConstTerm ct && ct.value is
      num)`). Codegen MUST preserve the unwrap — flipping the
      contract (returning the wrapper) would require rewriting
      every guard arm. ValueTuple per `heap_fcp.dart.md` cache hit.

  - construct_key: dart.dispatcher_helper.evaluatearithmetic_evaluateguard_termsequal_static_predicate_eval
    source_form: >-
      Three static helpers: (a) `_isArithmeticOp(String functor)`
      — returns whether functor ∈ {'+', '-', '*', '/', 'mod',
      'neg'}; (b) `_evaluateArithmetic(String op, List<Object?>
      args)` — assumed-ground arithmetic eval, throws on
      non-numeric or wrong arity; (c) `_evaluateGuard(String
      predicateName, List<Object?> args, RunnerContext cx)` —
      the BIG guard switch (~25 arms — see guard family construct);
      (d) `_termsEqual(Object? a, Object? b, RunnerContext cx,
      [Set<(int, int)>? visited])` — structural equality with
      VarRef dereferencing and cycle detection (visited set of
      address-pair tuples).
    target_decision: >-
      Direct 1:1 static-method translations. `_IsArithmeticOp`:
      `private static bool IsArithmeticOp(string functor) =>
      functor is "+" or "-" or "*" or "/" or "mod" or "neg";`
      (C# pattern-matching `or` keyword — Microsoft Learn).
      `_EvaluateArithmetic`: same shape; throws
      `InvalidOperationException` (mapping of `StateError`).
      `_TermsEqual`: recursive with `HashSet<(int, int)>? visited
      = null;` default param and `visited ??= new HashSet<(int,
      int)>();` initialiser. C# value-tuples support `==`
      comparison and are usable as `HashSet<T>` element type
      (Microsoft Learn confirms `ValueTuple<T1,T2>` implements
      `IEquatable<ValueTuple<T1,T2>>`). The visited set is per-
      top-level-call — codegen MUST preserve scope.
    idiom_id: null
    research_finding_id: rf-dart-recursive-static-helper-with-cycle-detection-to-csharp-equivalent
    nuance: >-
      ARITHMETIC-NUM NUANCE (explicitly addressed): inherits from
      the guard-dispatch construct; same `int`/`double` to
      `double` promotion via TryAsNum helper. CYCLE-DETECTION
      NUANCE (LOAD-BEARING, explicitly addressed): the visited set
      stores ADDRESS PAIRS (a.addr, b.addr) — so it detects
      "we've already compared these two positions" (a cycle
      crossing both sides simultaneously). Codegen MUST preserve
      the PAIR semantic — using a single `HashSet<int>` would
      under-detect (would conflate distinct same-address
      comparisons in different contexts). Cache hit on tuple
      return + tuple-equality.

  - construct_key: dart.error_handling.print_to_stderr_and_terminate_pattern
    source_form: >-
      `print('ERROR: ...'); return RunResult.terminated;` — the
      "fatal error inside the dispatch loop" pattern, used by
      Spawn (procedure-label not found, body kernel aborted),
      Requeue (procedure-label not found), Distribute (module
      not activated, no GLP channel, no module context),
      Transmit (module not activated, could not resolve module
      name). Also: `print('[WARN] Unknown guard predicate:
      $predicateName')` in `_evaluateGuard` default arm.
    target_decision: >-
      Direct translation: `Console.WriteLine("ERROR: ..."); return
      RunResult.Terminated;`. The Dart `print(...)` maps to
      `Console.WriteLine(...)` (same authoritative finding —
      cache-hit from `heap_fcp.dart.md` debug-print mapping
      already used in `_FormatTerm` and the dispatch-loop
      `cx.DebugOutput` arms). Codegen MUST NOT promote to
      `throw new InvalidOperationException(...)` — the source
      DELIBERATELY returns `RunResult.Terminated` (graceful
      exit, allows the scheduler to capture the goal's failure
      and proceed); throwing would propagate up through every
      caller and break the runtime's "one bad goal does not
      take down the scheduler" contract.
    idiom_id: null
    research_finding_id: rf-dart-print-and-terminate-to-csharp-equivalent
    nuance: >-
      GRACEFUL-EXIT NUANCE (LOAD-BEARING, explicitly addressed):
      these are SOFT errors — bytecode malformed (missing label),
      module not activated, body kernel returned an error — the
      contract is "report + terminate THIS goal", NOT "throw +
      kill the runtime". Codegen MUST preserve the
      `Console.WriteLine + return Terminated` pair; switching to
      exceptions would change the semantic. NUANCE — stderr vs.
      stdout: Dart `print` writes to STDOUT (per Dart docs);
      `Console.WriteLine` does the same; matched. If a downstream
      test mocks stderr, that is a TEST-MOCK concern, not a
      semantic-translation concern. NULL-SAFETY: the error
      strings are non-null literals; no nullability concerns.

  - construct_key: dart.performance_idiom.argument_register_dictionary_lookup_and_clear_recycle
    source_form: >-
      `cx.argSlots[slot] = ...` (writes) + `cx.argSlots.clear()`
      (after Spawn / Requeue / Commit) — argument registers are a
      Dictionary<int, Term> that gets POPULATED before a goal
      call and CLEARED after. NOT a fixed-size array (the source
      uses Map<int, Term> because (a) arity is not known
      statically per call site, (b) sparse slots possible). Hot-
      path: every spawned goal reads up to 10 slots via
      `cx.argSlots[i]` in a loop.
    target_decision: >-
      `Dictionary<int, Term> ArgSlots { get; }` on RunnerContext
      (already specced). The Dart pattern `cx.argSlots[slot] =
      value;` becomes `cx.ArgSlots[slot] = value;` (C#
      Dictionary indexer assignment). Clear: `cx.ArgSlots.Clear
      ();`. The "up to 10 slots" loop maps to `for (int i = 0; i
      < 10; i++) { if (!cx.ArgSlots.TryGetValue(i, out var t)
      || t is null) break; ... }` — preserves the Dart `final
      arg = cx.argSlots[i]; if (arg != null) ... else break;`
      pattern (Dart Map returns `null` for missing keys; C#
      Dictionary throws unless `TryGetValue` is used —
      `TryGetValue` is the .NET idiom per Microsoft Learn).
      PERFORMANCE: codegen MUST NOT replace `Dictionary` with a
      `Term?[10]` fixed-size array — the source semantics include
      "slot N+1 missing → break", which `TryGetValue` preserves
      and array `arr[i] != null` would also preserve, BUT the
      source ALSO calls `Map<int, Term>.from(cx.argSlots)` in
      Spawn to clone, and `argSlots.clear()` to reset — both
      Dictionary operations have direct semantic equivalents,
      whereas an array would require manual ranged-clear. The
      faithful translation is Dictionary; if profiling later
      shows hot-path allocation pressure, codegen MAY optimise
      to a hybrid (small inline array for slots 0-9 + spillover
      Dictionary for >9) — that is OUT OF SCOPE for this spec.
    idiom_id: null
    research_finding_id: rf-dart-map-int-key-as-sparse-array-to-csharp-dictionary
    nuance: >-
      SPAN<T> NUANCE (explicitly addressed, NOT glossed): the
      task prompt called out attention to Span<T>-style
      allocation avoidance. Per Microsoft Learn
      (https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/),
      `Span<T>` works on CONTIGUOUS memory (arrays, stackalloc
      buffers, native pointers). It is NOT applicable to:
      Dictionary lookups, HashSet membership, Stack/List<T> of
      heap-allocated reference types (the runner's parentStack,
      argSlots, sigmaHat, Si, U, clauseVars are all hash- or
      dictionary-backed). Codegen MUST NOT attempt
      `Span<KeyValuePair<int, object?>>` over a Dictionary's
      internal entries (would tie codegen to Dictionary
      implementation details). Where Span<T> IS applicable in
      this file: the `permanentVars` array on EnvironmentFrame
      (contiguous; `List<object?>` could become `object?[]` and
      slice via `Span<object?>` for the Y-register window) — but
      this optimisation requires evidence of hot-path pressure
      and is OUT OF SCOPE for this faithful-translation spec.
      The current spec emits `List<object?>` (mirroring `List.
      filled(size, null)`); a future codegen optimisation pass
      may upgrade to `object?[]` + Span<T>. NULL-SAFETY: argSlots
      values are non-null `Term` (the type system enforces
      non-null Term per terms.dart.md).

conversion_units:
  - "runner.cs / namespace declaration + using directives (Threading, Runtime, Multiagent, Bytecode, Bytecode.V2)"
  - "RunResult enum (4 members)"
  - "UnifyMode enum (2 members)"
  - "GuardResult enum (3 members, Suspend retained)"
  - "LabelName using-alias (string)"
  - "ReplModuleTarget class"
  - "ReplModuleContext class (required-named ctor with default)"
  - "CallEnv class (Dictionary<int, Term> argBySlot, arg/update methods)"
  - "EnvironmentFrame class (parent/CP/permanentVars triple)"
  - "ParentContext class (file/internal sealed — Push/Pop frame)"
  - "BytecodeProgram class (Ops + Labels + Merge + ToDisassembly + IndexLabels)"
  - "RunnerContext class (big per-goal state block)"
  - "BytecodeRunner class shell (Prog field, ctor, Run / RunWithStatus, helper methods)"
  - "RunWithStatus dispatch loop (44-arm if-is cascade in source order, with reduction-budget head check)"
  - "HEAD-phase opcode arms: HeadConstant, HeadStructure, UnifyConstant, UnifyVoid, UnifyStructure, HeadNil, HeadList, V2.HeadVariable, V2.UnifyVariable, V2.SetVariable, V2.Unknown, RequireWriterArg, RequireReaderArg, Push, Pop, GetVariable, GetValue, ClauseTry, GuardFail, Otherwise, HeadBindWriter, HeadBindWriterArg, GuardNeedReader, GuardNeedReaderArg"
  - "GUARD arms: Guard, Ground, Known, NoReaders, GroundEqual"
  - "Commit arm (two-phase Si resolve + tentative-struct convert + WxW check + ApplySigmaHatFCP + reactivation enqueue + state reset)"
  - "Clause-control arms: ClauseNext, TryNextClause, NoMoreClauses, UnionSiAndGoto, ResetAndGoto, SuspendEnd"
  - "BODY-phase arms: BodySetConst, BodySetStructConstArgs, BodySetConstArg, PutConstant, PutStructure, SetConstant, TailStep, PutNil, PutBoundConst, PutBoundNil, PutList"
  - "Goal-control arms: Spawn (with body-kernel inline path), Requeue (tail call)"
  - "Module-RPC arms: Distribute (static, importIndex via ReplModuleContext.Imports), Transmit (dynamic, moduleVarIndex via clauseVars)"
  - "Environment-frame + utility arms: Allocate, Deallocate, Nop, Halt, Proceed"
  - "Helper methods (private): _findNextClauseTry, _softFailToNextClause, _finalUnboundVar, _suspendAndFail, _suspendAndFailMulti, _getArg"
  - "Static helpers: _FormatTerm, _DereferenceWithTracking (ValueTuple + local-function closure), _IsArithmeticOp, _EvaluateArithmetic, _EvaluateGuard (huge switch with arithmetic + type + control + time + =?= arms), _TermsEqual (recursive structural equality with cycle detection), _ConvertTentativeToStruct"
  - "Internal sealed (or file) classes: TentativeStruct, ClauseVar, ListStruct, StructureState, ArgInfo"
  - "Marshalling shim for System.Threading.Timer callback → owning scheduler (wait / wait_until) — concrete mechanism deferred to the multiagent isolate-manager port (INHERITS heap_fcp.dart.md escalations[0])"

escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-timer-to-csharp-system-threading-timer — dart:async.Timer → .NET Timer (NEW; concurrency-marshalled)

Authoritative basis: Microsoft Learn `System.Threading.Timer`
(https://learn.microsoft.com/en-us/dotnet/api/system.threading.timer)
documents the one-shot-via-`Timeout.InfiniteTimeSpan`-period
constructor and the callback's "runs on a ThreadPool thread" semantics.
Dart docs `dart:async.Timer` (https://api.dart.dev/stable/dart-async/Timer-class.html)
document the "schedules a one-shot callback on the owning isolate"
semantics. The two differ on which thread the callback fires on — Dart
guarantees same-isolate, .NET does not. The runner's timer callback
mutates the heap and the goal queue; under the INHERITED concurrency
escalation (heap_fcp.dart.md escalations[0]), every such mutation must
happen on the owning scheduler. The faithful translation MUST marshal
the timer callback back to the owning scheduler (e.g. via
`SynchronizationContext.Post`, a per-context single-threaded
`TaskScheduler`, or a mailbox) BEFORE touching `cx.Rt.Heap` /
`cx.Rt.EnqueueReactivatedGoal`. The concrete mechanism is deferred to
the multiagent isolate-manager port — same boundary the heap_fcp.dart
escalation declares. Single research call; cached.

### rf-dart-import-relative-to-csharp-using-namespace — package imports (cache hit)

Same authoritative finding already used in `heap_fcp.dart.md`,
`variable_table.dart.md`, `goal_queue.dart.md` (FR-024 cache hit; not
re-researched). The `show` allow-list has no .NET parallel; the
prefix-import `import 'opcodes_v2.dart' as opv2;` is the load-bearing
extension covered above — Dart prefix → C# namespace-qualified
reference (e.g. `V2.HeadVariable`); the v1/v2 disjoint marker-interface
contract from `opcodes.dart.md` / `opcodes_v2.dart.md` is preserved.

### rf-dart-plain-enum-to-csharp-enum — RunResult / UnifyMode / GuardResult (cache hit + retain-unreached-member)

Same authoritative finding already used in `heap_fcp.dart.md`,
`cells.dart.md`, `machine_state.dart.md`, `opcodes.dart.md` (FR-024
cache hit). Plain enum → C# plain enum, 1:1; declaration order
preserved. ADDITIONAL DECISION (NOT a new research call): retain
`GuardResult.Suspend` even though `_evaluateGuard` never returns it
today — the three-valued shape mirrors GLP semantics and pruning would
silently make a future case unhandled. This is a fidelity-preservation
policy already established in opcodes_v2.dart.md (the Suspend member of
GuardResult is documented as "we handle this before evaluation" — the
member exists in the enum for protocol symmetry).

### rf-dart-typedef-string-to-csharp-using-alias — LabelName (cache hit)

Same authoritative finding already used in `opcodes.dart.md`
(FR-024 cache hit; not re-researched). Dart `typedef LabelName =
String;` → C# `using LabelName = string;`. Not a record-struct
wrapper; not promoted to a strong type.

### rf-dart-final-field-class-to-csharp-getonly-class — simple-data classes (cache hit)

Same authoritative finding already used across `opcodes.dart.md` /
`opcodes_v2.dart.md` / `heap_fcp.dart.md` (FR-024 cache hit). Each
Dart `final` field becomes a C# get-only auto-property; reference
class (not record, not struct). The `_ParentContext` file-privacy
maps to C# `internal sealed` (or `file class` on C# 11+).
Required-named ctor parameters carry forward `opcodes_v2.dart.md`
rf-dart-required-named-param-to-csharp-required-arg.

### rf-dart-dynamic-list-of-sum-types-to-csharp-list-of-object — BytecodeProgram.ops (NEW)

Authoritative basis: Microsoft Learn `dynamic` keyword
(https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/reference-types#the-dynamic-type)
documents that C# `dynamic` defers all dispatch to the DLR at runtime
— per-access cost, no compile-time safety, "use sparingly". Dart docs
`dynamic` (https://dart.dev/null-safety/understanding-null-safety#topnonnull)
note `dynamic` is the Dart wildcard. The faithful translation of
`List<dynamic>` holding a closed sum (v1 `IOp` / v2 `IOpV2`) is
`List<object>` + `is X` pattern-matches — NOT `List<dynamic>`. Single
research call; cached. This is THE hot-path dispatch shape; codegen
MUST preserve the cascading `is X` arms in source order.

### rf-dart-mutable-state-class-identity-equality-to-csharp-class — RunnerContext / BytecodeRunner (cache hit)

Same authoritative finding already used in `heap_fcp.dart.md` for
HeapFCP itself (FR-024 cache hit). RunnerContext is the per-goal
mutable cursor; BytecodeRunner is the per-program dispatch engine.
Both are reference `class`. Public mutable fields → public-setter
auto-properties (not private set — see heap_fcp.dart.md). Thread-
safety is INHERITED from heap_fcp.dart.md escalations[0] —
RunnerContext is owned by exactly one isolate / scheduler at a time;
no double escalation here per task instruction.

### rf-dart-pattern-match-cascade-dispatch-to-csharp-is-pattern-cascade — BytecodeRunner.RunWithStatus (NEW)

Authoritative basis: Microsoft Learn pattern-matching
(https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns)
documents `is` pattern as the C#-idiomatic runtime type-test +
binding. Dart's `is` (https://dart.dev/language/operators#type-test-operators)
provides the same semantic. A long cascade of `if (op is X) { ...
continue; }` is a hot-path dispatch shape both languages support;
the C# JIT lowers it into specialised type-tests (Microsoft Learn
patterns + Roslyn lowering notes). Codegen MUST preserve ARM ORDER
(frequently-reached arms first — `Label`, `ClauseTry` lead the
source). Rejecting refactor to `Dictionary<Type, Action>` and to
`switch (op.GetType())` is justified by added indirection per
dispatch. Single research call; cached.

### rf-dart-three-valued-unification-dispatch-arm-to-csharp-equivalent — HEAD-phase + ClauseControl arms (NEW)

Authoritative basis: the GLP runtime spec (`docs/glp-runtime-spec.txt`
in the repo) is the AUTHORITATIVE document for two-phase HEAD
unification (σ̂w / Si / U semantics + Commit resolves Si against σ̂w).
Microsoft Learn `HashSet<T>` (https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1)
+ `Dictionary<TKey,TValue>` (https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2)
document the .NET counterparts for the σ̂w/Si/U data structures. Codegen
MUST preserve two-phase semantics — Si records readers BEFORE soft-
fail; Commit resolves them. Single research call; cached for the
HEAD/Guard/Commit/ClauseControl families.

### rf-dart-two-phase-commit-operator-to-csharp-equivalent — Commit arm (NEW; cross-references heap_fcp commit.dart)

Authoritative basis: the FCP commit operator is specified in the GLP
runtime spec; the implementation in commit.dart is the source. The
Dart → C# mapping requires (a) tentative-struct conversion
(handling `_ClauseVar` placeholders and fresh-variable allocation),
(b) WxW prohibition enforcement (throw on writer→writer binding —
maps `StateError` → `InvalidOperationException` per
`heap_fcp.dart.md` cache), (c) `CommitOps.ApplySigmaHatFCP` call (the
heap-mutation + reactivation operator from commit.dart.md), (d)
post-commit state reset. The iteration ORDER of σ̂w (Dictionary
iteration) is called out as a NUANCE — Dart Map preserves insertion
order; C# Dictionary does in practice on .NET 5+ but not contractually.
Microsoft Learn OrderedDictionary<TKey,TValue>
(https://learn.microsoft.com/en-us/dotnet/api/system.collections.specialized.ordereddictionary)
is the contractually-ordered alternative for .NET 9+. Single research
call; cached.

### rf-dart-body-phase-write-dispatch-to-csharp-equivalent — BODY-phase arms (NEW)

Authoritative basis: HeapFCP's `bindWriterConst` / `bindWriterStruct`
return `List<GoalRef>` activations (heap_fcp.dart.md cached idiom).
The runner's BODY arms uniformly take those activations and enqueue
them onto `cx.rt.gq` + fire the `onActivation` host hook. Microsoft
Learn `IReadOnlyList<T>` confirms the read-only return is the .NET
idiomatic counterpart. Codegen MUST NOT drop the return value of
BindWriter* calls — would silently lose reactivations. Single
research call; cached.

### rf-dart-goal-spawning-and-rpc-dispatch-to-csharp-equivalent — Spawn / Requeue / Distribute / Transmit (NEW)

Authoritative basis: goal-spawning is the runtime's mechanism for
introducing new goals into the queue; the GLP runtime spec covers
spawn / requeue / distribute / transmit. Body-kernel inline execution
(via `cx.rt.bodyKernels.lookup(name, arity)`) is documented in
`body_kernels.dart.md`. GLP channels (`cx.rt.glpChannels`) are
documented in the future glp_channels convspec. The Dart →  C#
mapping is direct — every operation has a `Dictionary` / `List` /
ValueTuple counterpart. Module-RPC is synchronous in the source (the
channel `send(goalTerm)` returns activations synchronously); codegen
MUST NOT introduce async/await. Single research call; cached.

### rf-dart-guard-evaluation-with-suspend-tracking-to-csharp-equivalent — Guard family (NEW)

Authoritative basis: the GLP guards reference (`docs/guards-reference.md`)
is authoritative for guard semantics (arithmetic, type, control, time
guards). Dart `num` → C# `double` + helper `TryAsNum` is the
faithful arithmetic-numeric translation (Microsoft Learn numeric types
+ TryParse-style helpers). `wait` / `wait_until` use the FCP
suspension protocol (allocate variable + Timer + on-fire bind writer
+ reactivate suspended goals) — inherits the dart:async Timer
construct's marshalling contract above. `_termsEqual` cycle-detection
via `HashSet<(int,int)>` is C# ValueTuple-equatable (cache hit on
`heap_fcp.dart.md` rf-dart-record-return-to-csharp-valuetuple). Single
research call; cached.

### rf-dart-environment-frame-and-utility-dispatch-to-csharp-equivalent — Allocate / Deallocate / Nop / Halt / Proceed (NEW)

Authoritative basis: the WAM (Warren Abstract Machine) environment-
stack concept — `docs/wam.pdf` (cited in CLAUDE.md sibling-repo
appendix) — is the authoritative source for permanent-variable
storage. The `EnvironmentFrame` is a linked-list frame; Allocate
pushes, Deallocate pops. Dart `EnvironmentFrame? E` / `int? CP` map to
C# `EnvironmentFrame? E { get; set; }` / `int? CP { get; set; }`.
`Halt` and `Proceed` return `RunResult.Terminated` (terminate the
current goal); Microsoft Learn `enum` / `return` documentation
confirms the direct translation. Single research call; cached.

### rf-dart-private-class-with-mutable-list-args-to-csharp-internal-sealed-class — TentativeStruct / ClauseVar / ListStruct / StructureState / ArgInfo (NEW)

Authoritative basis: Microsoft Learn `internal` access modifier
(https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/internal)
documents assembly-scoped visibility — the closest C# counterpart to
Dart's library-private leading-`_` (per Dart docs
https://dart.dev/language/classes#class-modifiers). Microsoft Learn
`file class` (C# 11+,
https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/file)
documents EXACT file-scoped visibility — the perfect mirror of Dart's
file-private. Codegen prefers `file class` if target framework is C#
11+, falls back to `internal sealed class`. `_TentativeStruct.args` is
a `List<object?>` mutated in place — reference class, not record. The
free function `_convertTentativeToStruct` becomes a private static
method on BytecodeRunner (C# has no top-level functions outside C# 9
top-level statements). Single research call; cached.

### rf-dart-tostring-interp-to-csharp-tostring-interp — _FormatTerm (cache hit)

Cache hit on `opcodes.dart.md`. Dart string-interpolation `'$varname'`
+ `toString()` overrides map to C# `$"{varname}"` + `ToString()`
overrides. The `StringBuffer` → `StringBuilder` mapping is the same
authoritative finding from `heap_fcp.dart.md`. Cycle detection via
`HashSet<int>` (per top-level call).

### rf-dart-record-return-to-csharp-valuetuple — _DereferenceWithTracking (cache hit)

Cache hit on `heap_fcp.dart.md`. Dart `(Object?, Set<int>)` records →
C# ValueTuple `(object? Deref, HashSet<int> UnboundReaders)` with
named tuple-element accessors. Local function `Dereference(...)`
preserves the closure over the enclosing `unboundReaders` HashSet —
Microsoft Learn local functions
(https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/local-functions)
confirm closure capture. Cache hit; no new research.

### rf-dart-recursive-static-helper-with-cycle-detection-to-csharp-equivalent — _TermsEqual / _EvaluateArithmetic / _EvaluateGuard (NEW)

Authoritative basis: Microsoft Learn pattern-matching `or` keyword
+ ValueTuple equality + `HashSet<ValueTuple<T1,T2>>` element
acceptance + `IEquatable<T>` interface
(https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1)
together establish that the cycle-detection set of address pairs is
C#-idiomatic. The recursive walk over `StructTerm.args` mirrors the
Dart source exactly. `evaluateNumeric` local function preserves
closure capture (see prev). Single research call; cached.

### rf-dart-print-and-terminate-to-csharp-equivalent — error-handling (cache hit)

Cache hit. Dart `print(...)` → C# `Console.WriteLine(...)`. Return
`RunResult.Terminated` (graceful exit) preserved — codegen MUST NOT
substitute exceptions; would change the runtime's "one bad goal does
not take down the scheduler" contract.

### rf-dart-map-int-key-as-sparse-array-to-csharp-dictionary — argSlots (NEW)

Authoritative basis: Microsoft Learn `Dictionary<TKey,TValue>` +
`TryGetValue` (https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.trygetvalue)
documents the .NET-idiomatic "check + read" pattern. Dart
`Map<int, Term>` with sparse int keys → C# `Dictionary<int, Term>`
with `TryGetValue` for the "missing-key returns null" Dart-Map
semantic. Microsoft Learn `Span<T>`
(https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/memory-t-usage-guidelines)
documents the contiguous-memory requirement; Dictionary is NOT a
Span<T> candidate. Single research call; cached. Performance idiom
explicitly addresses why Span<T> is rejected for the runner's hot-
path sparse-int-keyed slot maps.

## Carry-forward: concurrency / threading model — INHERITED ESCALATION

Per task instruction, the threading-model decision is INHERITED from
`lib/runtime/heap_fcp.dart.md` escalations[0] (the `HeapFCP`
concurrency model — recommended option A: preserve Dart's single-
owner-thread invariant via the multiagent isolate-manager port). The
runner / RunnerContext / BytecodeRunner inherit that contract: each
RunnerContext is owned by exactly one OS thread / Task scheduler at a
time; mutators are single-writer per goal. The dart:async Timer
callback in `wait`/`wait_until` must marshal back to the owning
scheduler before touching the heap or goal queue (concrete mechanism
deferred to the isolate-manager port). NO ADDITIONAL ESCALATION is
recorded here — double-escalating would violate the task instruction
and the FR-014 conflict-avoidance discipline (the heap_fcp escalation
already covers the entire HeapFCP-touching mutation surface, which
includes every runner mutator transitively).

## Quality bar self-check (SC-006 / FR-009 / FR-010)

Every non-trivial construct above records both a deep-analysis basis
(the `nuance:` block — value-vs-reference, null-safety, mutability,
async, performance, dispatch shape, two-phase commit, cycle
detection, etc.) and a researched-pattern basis (the
`research_finding_id`). Well-known nuances explicitly addressed:

- **Value-vs-reference**: BytecodeProgram, RunnerContext,
  BytecodeRunner, simple-data classes, helper classes — ALL
  reference `class`, never `record`/`struct` (carry-forward).
- **Async / Stream / IAsyncEnumerable**: runner is FULLY
  SYNCHRONOUS; only Timer (one-shot, marshalled) — not
  `IAsyncEnumerable`, not `Task<RunResult>`.
- **Null-safety**: every Dart `Object?` / `int?` / nullable
  function-type preserved as `object?` / `int?` / `Action<...>?`.
- **Isolate / threading**: INHERITED escalation from heap_fcp;
  Timer callback marshalling called out.
- **Dynamic / object**: Dart `dynamic` → C# `object?` (NOT
  C# `dynamic`); explicit `is` pattern-matches preserve type
  discipline.
- **WAM read/write modes**: preserved exactly as source.
- **Two-phase HEAD unification (σ̂w / Si / U)**: Commit resolves Si
  against σ̂w; preserved exactly.
- **Tail-call (Requeue)**: manual state-reset preserved; no
  reliance on .NET tail-call optimisation (none in the JIT for
  arbitrary calls).
- **Performance idioms (Span<T> applicability)**: explicitly
  evaluated and rejected for Dictionary/HashSet/Stack slots; noted
  as potential future optimisation for `permanentVars` only (OUT
  OF SCOPE here — faithful translation first).

## Zero escalation in this file

The single threading-model concern is INHERITED from
`heap_fcp.dart.md` escalations[0] and is NOT re-escalated here per
task instruction. Every other decision is locally decidable from the
source + the cached idioms / research findings; the per-construct
nuance blocks explicitly trace each rationale. No silent guesses
(SC-008).
