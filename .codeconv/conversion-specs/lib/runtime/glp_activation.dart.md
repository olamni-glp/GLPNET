# Conversion Spec — lib/runtime/glp_activation.dart

> Conversion-spec artifact for lib/runtime/glp_activation.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> Module-activation glue: creates a GLP channel (writer/reader pair),
> constructs a `ModuleTerm` from compiled bytecode, spawns a
> `serve(Module, ChannelReader?)` goal, registers a serve runner, tags
> the goal as infrastructure, and returns a writer-side
> `GlpChannelHandle`. Every type touched here is already convspec'd
> elsewhere (`HeapFCP`, `Term` hierarchy, `GoalRef`, `BytecodeProgram`,
> `BytecodeRunner`, `CallEnv`, `GlpRuntime`); this file's only novel
> surface is the `GlpChannelHandle` reference class and the
> `activateModule` top-level function. All rf-IDs below are REUSED
> verbatim from prior runtime/* convspecs (FR-024 cache hits; no new
> research).

```yaml
schema_version: 1
source_path: lib/runtime/glp_activation.dart
source_sha256: ffba37a1c2ae6161898532e842040e38b1aaab8a818fe9c60bd4a001952688c4
target_code_unit: lib/runtime/glp_activation.cs
constructs:
  - construct_key: "dart.library_directive.top_of_file_no_name"
    source_form: >-
      "`library;` -- top-of-file directive with no library name, sitting
      below the leading triple-slash doc-comment block ('GLP-level module
      activation. ... Phase 4 of dynamic module dispatch ...'). Carries
      the file's header docs to the library-symbol the analyzer attaches
      them to."
    target_decision: >-
      No direct .NET counterpart -- elide the `library;` directive. The
      leading triple-slash doc-comment block becomes a file-header
      XML-doc / `//`-comment block placed above the `namespace`
      declaration that mirrors `lib/runtime/`. Carry-forward of the same
      mapping decided in lib/runtime/suspension.dart.md and
      lib/runtime/heap_fcp.dart.md (FR-024 cache hit; do not re-research).
    idiom_id: null
    research_finding_id: rf-dart-library-directive-to-csharp-namespace-elision
    nuance: >-
      Compilation-unit nuance only; no value-vs-reference, null-safety,
      async/Stream/Future, or isolate surface implicated by a top-of-file
      `library` directive. Reused verbatim from
      .codeconv/conversion-specs/lib/runtime/suspension.dart.md and
      heap_fcp.dart.md.

  - construct_key: "dart.import_directive.package_internal_to_using_namespace.multi"
    source_form: >-
      "Five `import 'package:glp_runtime/...';` directives:
      `runtime/runtime.dart` (re-exporting GlpRuntime / CallEnv / etc.),
      `runtime/terms.dart` (Term / VarRef / ConstTerm / StructTerm /
      ModuleTerm), `runtime/heap_fcp.dart` (HeapFCP), `runtime/machine_state
      .dart` (GoalRef / GoalId / Pc), `bytecode/runner.dart` (BytecodeProgram
      / BytecodeRunner). All package-internal; no `show`/`hide` clauses."
    target_decision: >-
      Each Dart package-internal import becomes a single .NET `using`
      directive resolving the namespace of the corresponding converted
      file. All five sibling files target the same `lib/runtime/` /
      `lib/bytecode/` namespaces (decided by the depgraph/namespace
      stage); the converted `glp_activation.cs` emits a `using
      <root>.Runtime;` plus a `using <root>.Bytecode;`. No `show`/`hide`
      narrowing exists in C# -- imports bring the full public surface;
      codegen MUST NOT synthesise per-symbol `using X = ...;` aliases
      (over-translation). Carry-forward from heap_fcp.dart.md /
      body_kernels.dart.md / external_io.dart.md.
    idiom_id: null
    research_finding_id: rf-dart-import-relative-to-csharp-using-namespace
    nuance: >-
      Import-unit nuance: Dart imports a *library/file*; C# imports a
      *namespace*. Show/hide nuance: ABSENT here (no `show`/`hide`
      clauses) -- aligns with the goal_queue.dart.md / fairness.dart.md
      precedent that per-symbol narrowing has no faithful C#
      counterpart. Value-vs-reference / null-safety / async / Stream /
      isolate: NOT APPLICABLE -- directives produce no runtime form.

  - construct_key: "dart.docblock_triple_slash.module_header_and_member_docs"
    source_form: >-
      "Triple-slash doc-comment blocks above the library directive, above
      `class GlpChannelHandle`, above `writerAddr` getter, `send`, `close`,
      and `activateModule`; in-body `// 1. Create GLP channel ...` .. `// 7.
      Return channel handle ...` step-numbered `//`-line comments inside
      `activateModule`. Triple-slash docs explain the public API;
      `//`-lines explain implementation steps."
    target_decision: >-
      Triple-slash blocks become C# XML-doc `/// <summary>...</summary>`
      blocks on the converted namespace, class, property, method, and
      function. In-body `//` step-numbered comments stay as in-body
      `//`-line comments verbatim (NOT promoted to XML-doc -- they are
      implementation notes, not API documentation, and the source
      explicitly chose `//` over `///`). Trivial mechanical mapping per
      the project doc-comment idiom (same treatment as hanger.dart.md /
      machine_state.dart.md).
    idiom_id: null
    research_finding_id: null
    nuance: trivial
    trivial: true

  - construct_key: "dart.class.mutable-state-container-identity-equality-two-fields-positional-ctor-this-shorthand-with-getter-and-two-mutating-methods-returning-list"
    source_form: >-
      "`class GlpChannelHandle { final HeapFCP _heap; int _writerAddr;
      GlpChannelHandle(this._heap, this._writerAddr); int get writerAddr
      => _writerAddr; List<GoalRef> send(Term goal) { final
      (tailWriterAddr, _) = _heap.allocateVariable(); final consCell =
      StructTerm('.', [goal, VarRef(tailWriterAddr)]); final activations =
      _heap.bindVariable(_writerAddr, consCell); _writerAddr =
      tailWriterAddr; return activations; } List<GoalRef> close() { return
      _heap.bindVariable(_writerAddr, ConstTerm('nil')); } }` -- a Dart
      class with: (a) one `final` private `HeapFCP` reference field
      `_heap` (Dart leading-underscore = library-private); (b) one mutable
      private `int` field `_writerAddr`; (c) a single positional
      constructor with `this.field` shorthand for both args; (d) one
      expression-bodied public getter `writerAddr` returning the current
      writer address; (e) two methods `send(Term goal) -> List<GoalRef>`
      and `close() -> List<GoalRef>` that mutate (`send` advances
      `_writerAddr`) and call into `_heap` to produce activation lists.
      No `==`/`hashCode` override -- default identity equality. No
      `toString` override. No mutator setter -- mutation happens via
      `send()` only (`close()` is terminal; the runtime is expected to
      drop the handle after close per the docstring contract)."
    target_decision: >-
      Map to a reference-type .NET `class` `GlpChannelHandle` (NOT
      `record`, NOT `struct`, NOT `record class`, NOT `record struct`).
      The class has: a `private readonly HeapFCP _heap;` field (Dart
      `final` private -> C# `private readonly`); a `private int
      _writerAddr;` field (Dart non-final private -> C# `private`); a
      public get-only expression-bodied property `public int WriterAddr
      => _writerAddr;` (mirrors Dart `int get writerAddr =>
      _writerAddr;` shape; Dart leading-underscore on the field, not on
      the getter, becomes `_writerAddr` (private field, kept
      `_camelCase` per the .NET capitalisation conventions for
      private-instance fields) vs `WriterAddr` (public property,
      PascalCased)). Single positional constructor `public
      GlpChannelHandle(HeapFCP heap, int writerAddr) { _heap = heap;
      _writerAddr = writerAddr; }` -- the Dart `this.field` shorthand
      has no C# counterpart, so codegen emits explicit body assignments
      (carry-forward of rf-dart-positional-ctor-with-this-shorthand-to-
      csharp-positional-ctor-with-explicit-assignment). NOT a `record`
      / `record class`: would inject value-equality on every field --
      a correctness bug because the runtime stores `GlpChannelHandle`
      references in `rt.glpChannels` (a Dictionary keyed by module
      name) and the test code holds the *specific* returned handle and
      mutates it across `send()` calls; reference identity is the
      observed contract. NOT a `struct` / `record struct`: would copy
      on assignment -- the `_writerAddr` field is mutated in place by
      `send()`; a struct copy would split the writer state into
      independent copies (the caller's handle would diverge from the
      `rt.glpChannels`-stored handle after the first `send()`),
      breaking the channel-extension invariant the class exists to
      enforce. The identity-equality + mutable-field combination is
      the same load-bearing reason `machine_state.dart`'s `GoalState`
      and `external_io.dart`'s `InputInjector` are reference-type
      classes -- reused verbatim here.
    idiom_id: null
    research_finding_id: rf-dart-mutable-state-class-identity-equality-to-csharp-class
    nuance: >-
      VALUE-VS-REFERENCE / IDENTITY is the load-bearing nuance.
      Reference identity is the observed contract: `rt.glpChannels[
      moduleName] = channel` stores the handle keyed by module name,
      and the caller's local `channel` reference must alias the
      stored one so subsequent `send()` mutations are observed
      everywhere -- a struct would diverge silently. Mutability:
      `_writerAddr` is a single-writer non-shared state advanced by
      `send()`; `private int` (NOT `readonly`, NOT `{ get; private
      set; }`) is the faithful render -- the field is fully private
      and the public surface exposes only a read-only `WriterAddr`
      getter, matching Dart's surface exactly. Null-safety:
      `HeapFCP` is a non-nullable reference type in both languages;
      `int` is a non-nullable value type in both. No async surface,
      no Stream/Future, no isolate. Constancy/canonicalisation: not
      applicable -- handles are stateful and instance-distinct.
      Reference-sharing of `_heap`: the constructor stores the
      caller's `HeapFCP` reference (no defensive copy); the converted
      C# class assigns `_heap = heap;` -- both languages share the
      same `HeapFCP` instance with the runtime, which is the
      load-bearing contract (mutations to the heap performed via
      `_heap.bindVariable` must be observed by every holder of the
      same heap reference).

  - construct_key: "dart.positional_ctor.this_dot_field_shorthand.two_args.no_initialiser_list"
    source_form: >-
      "`GlpChannelHandle(this._heap, this._writerAddr);` -- a single
      positional constructor with two Dart `this.field` initialising
      formal parameters and an empty body. No initialiser list, no
      `super` call (default Object superclass), no default values, no
      named parameters, no `required` keyword. Both parameters bind
      to private leading-underscore fields."
    target_decision: >-
      Map to a single C# positional constructor with explicit body
      assignment per parameter: `public GlpChannelHandle(HeapFCP heap,
      int writerAddr) { _heap = heap; _writerAddr = writerAddr; }`. C#
      has no `this.field`-in-parameter-list shorthand; the Dart
      shortcut MUST be expanded into the constructor body. Parameter
      names are decapitalised-Pascal of the Dart field names
      stripping the leading underscore (`_heap` -> `heap`; `_writerAddr`
      -> `writerAddr`) -- the public parameter name MUST NOT carry a
      leading underscore (C# convention reserves `_camelCase` for
      private fields, not parameters). Carry-forward from
      lib/runtime/suspend.dart.md and lib/multiagent/mad_helpers.dart
      .md.
    idiom_id: null
    research_finding_id: rf-dart-positional-ctor-with-this-shorthand-to-csharp-positional-ctor-with-explicit-assignment
    nuance: >-
      Constructor-shape nuance: Dart `this.field` formal parameters
      do double duty as parameter declarations AND field-init
      directives; C# requires the two responsibilities to be
      separated (parameter list declares the parameter; body
      assigns it to the field). Parameter-name nuance: leading
      underscore on the field does not carry to the parameter --
      strips off in the translation. Reference-sharing nuance:
      `_heap = heap;` aliases the caller's `HeapFCP` instance (no
      defensive copy) -- preserves Dart's identical reference-
      sharing semantics. Null-safety: both parameters are non-
      nullable (no `?` annotations); C# under enabled NRT preserves
      this. Async/Stream/isolate: ABSENT.

  - construct_key: "dart.method.tuple_destructure_with_discard.builds_struct_term_cons.mutates_field.returns_list_of_record_struct"
    source_form: >-
      "`List<GoalRef> send(Term goal) { final (tailWriterAddr, _) =
      _heap.allocateVariable(); final consCell = StructTerm('.',
      [goal, VarRef(tailWriterAddr)]); final activations = _heap
      .bindVariable(_writerAddr, consCell); _writerAddr =
      tailWriterAddr; return activations; }` -- four statements: (1)
      record-destructure the `(writer, reader)` pair returned by
      `_heap.allocateVariable()`, binding only the writer half and
      discarding the reader with `_`; (2) construct a cons-cell
      `StructTerm('.', [goal, VarRef(tailWriterAddr)])` (Dart-FCP
      list-construction idiom: `.` = list-cons functor, two args =
      head + tail-VarRef); (3) call `_heap.bindVariable(...)`
      returning `List<GoalRef>` of woken activations; (4) advance
      the writer field to the new tail and return the activations."
    target_decision: >-
      Map to a public instance method `public IReadOnlyList<GoalRef>
      Send(Term goal) { var (tailWriterAddr, _) = _heap
      .AllocateVariable(); var consCell = new StructTerm(".", new
      List<Term> { goal, new VarRef(tailWriterAddr) }); var
      activations = _heap.BindVariable(_writerAddr, consCell);
      _writerAddr = tailWriterAddr; return activations; }`. The
      record-destructure `final (writer, _) = ...` maps to C# 7+
      tuple-deconstruction `var (writer, _) = ...` -- C# has the
      `_` discard pattern in tuple deconstruction with identical
      semantics (Microsoft Learn: "Discards"). The Dart `[a, b]`
      list literal of `Term` becomes a C# collection-initialiser
      `new List<Term> { a, b }` (NOT a `Term[]` array -- the
      `StructTerm` constructor takes `IReadOnlyList<Term>` per
      terms.dart.md). The `.` cons-functor string literal is
      preserved byte-identically as `"."`. Return type
      `IReadOnlyList<GoalRef>` matches the Dart `List<GoalRef>`
      shape per the boot_loader.dart.md / external_io.dart.md
      `List`->`IReadOnlyList` convention on return values (callers
      do not mutate). `_writerAddr` mutation is a plain
      field-assignment in both languages.
    idiom_id: null
    research_finding_id: rf-dart-record-destructure-to-csharp-valuetuple-deconstruction
    nuance: >-
      Record-destructure-with-discard nuance (LOAD-BEARING): the
      `_` placeholder discards the reader half of
      `allocateVariable()`'s return tuple -- `send()` is the
      writer-side surface only. C# `var (x, _) = expr;` is
      byte-equivalent (no binding, no allocation, no warning).
      Cons-cell construction nuance: `StructTerm('.', [head,
      tailVarRef])` is the canonical Dart-FCP list-cons encoding
      (carry-forward from body_kernels.dart.md
      rf-dart-cons-cell-encoding-to-csharp-structterm-cons); the
      C# render preserves the literal `.` functor and the
      head+tail-VarRef shape. List-literal nuance: Dart `[a, b]`
      is a growable `List<Term>` (NOT `const`); the C# render is
      also growable; the reference is passed BY REFERENCE to the
      `StructTerm` constructor and aliased (no defensive copy --
      mirroring Dart's reference-sharing semantics from
      rf-dart-sumleaf-with-list-no-eq-to-csharp-class-
      ireadonlylist in terms.dart.md). Mutability nuance: the
      `_writerAddr` field is reassigned on every successful
      `send()` -- a mutable single-writer state; the assignment
      `_writerAddr = tailWriterAddr;` is the canonical advancing-
      writer step (matches the `InputInjector._currentWriterId`
      rendering in external_io.dart.md
      rf-dart-mutable-int-field-callback-list-return). Activation
      list nuance: `_heap.bindVariable` returns a list of woken
      goals that the *caller* MUST enqueue (per docstring); the
      method does NOT enqueue them itself -- semantics preserved
      verbatim. Value-vs-reference: `Term`, `StructTerm`, `VarRef`,
      `GoalRef` per their respective convspecs (Term hierarchy is
      reference-type classes per terms.dart.md; `GoalRef` is a
      readonly record struct per machine_state.dart.md so
      `IReadOnlyList<GoalRef>` stores value-type bit-patterns).
      Async/Stream/isolate: ABSENT -- send is synchronous.

  - construct_key: "dart.method.bind_variable_to_const_nil_returns_list"
    source_form: >-
      "`List<GoalRef> close() { return _heap.bindVariable(_writerAddr,
      ConstTerm('nil')); }` -- single-expression method body that
      binds the current writer to a `ConstTerm('nil')` sentinel
      (Dart-FCP empty-list encoding) and returns the resulting
      activation list. Terminal operation -- no field mutation, no
      writer-advance."
    target_decision: >-
      Map to a public expression-bodied method `public
      IReadOnlyList<GoalRef> Close() => _heap.BindVariable(
      _writerAddr, new ConstTerm("nil"));`. The Dart string literal
      `'nil'` becomes the C# `"nil"` literal byte-identically (the
      sentinel functor name is load-bearing -- preserved verbatim
      per the same rationale as the `'.'` cons functor; the
      runtime, the bytecode compiler, and the trace logs all
      compare on the string identity `nil`). Return type
      `IReadOnlyList<GoalRef>` per the same convention as `Send`.
      The method is terminal but does NOT mark the handle as
      closed (no `_closed` boolean; the docstring is silent on
      idempotence; the Dart source does not guard against double-
      close, so the C# port MUST NOT silently introduce one --
      that would be over-translation per FR-024/FR-013).
    idiom_id: null
    research_finding_id: rf-dart-top-level-fn-builds-sum-type-leaf
    nuance: >-
      Sentinel-functor-string nuance (LOAD-BEARING): the literal
      `nil` is the Dart-FCP empty-list encoding; codegen MUST NOT
      replace it with a typed sentinel (e.g. `ConstTerm.Nil`) --
      doing so would silently fork the source-to-spec
      correspondence (the bytecode compiler and trace logs key
      on the string `nil`). Idempotence nuance (explicitly
      addressed): the Dart source does NOT guard against double-
      close -- a second `close()` would re-bind a now-bound writer
      via `_heap.bindVariable`, whose behaviour on already-bound
      writers is owned by HeapFCP (out of scope for THIS file).
      The C# port preserves this behaviour faithfully; codegen
      MUST NOT introduce a `_closed` flag or an
      `InvalidOperationException` guard (would invent semantics
      the source does not have). Value-vs-reference: `ConstTerm`
      is a reference-type leaf per terms.dart.md; allocation per
      call is the Dart semantics (no canonicalisation), preserved
      in C# as `new ConstTerm("nil")`. Null-safety: return type
      is non-nullable; the `nil` literal is non-null. Async /
      Stream / isolate: ABSENT.

  - construct_key: "dart.top_level_function.named_required_args_with_no_defaults.tuple_destructure.dict_index_assign.postincrement.set_add.contains_key_guarded_insert.returns_class_instance"
    source_form: >-
      "`GlpChannelHandle activateModule({required GlpRuntime rt,
      required BytecodeProgram serveBytecode, required BytecodeProgram
      moduleBytecode, required String moduleName}) { ... }` -- a Dart
      top-level function with four `required` named parameters (no
      defaulted-args), returning a freshly-constructed
      `GlpChannelHandle`. Body (seven numbered steps): (1) `final
      (writerAddr, readerAddr) = rt.heap.allocateVariable();` --
      record-destructure of the heap's variable-pair allocator (both
      halves bound, neither discarded); (2) `final moduleTerm =
      ModuleTerm(moduleBytecode, name: moduleName);` followed by
      `final moduleAddr = rt.heap.storeTermOnHeap(moduleTerm);` --
      ModuleTerm constructed with a positional + named argument and
      stored on the heap; (3) post-increment `rt.nextGoalId++`
      reads-then-increments the runtime's goal-id counter, with the
      pre-increment value used as the new goal's id; CallEnv built
      via a Map<int, Term> literal `{0: VarRef(moduleAddr), 1:
      VarRef(readerAddr)}` passed as the `args:` named argument;
      `rt.setGoalEnv(goalId, env)` and `rt.setGoalProgram(goalId,
      serveBytecode)` invoked; (4) `serveBytecode.labels['serve/2']!`
      -- Map lookup with the `!` null-bang post-fix asserting non-null;
      `rt.gq.enqueue(GoalRef(goalId, servePc))` enqueues a freshly
      constructed GoalRef; (5) `rt.infrastructureGoalIds.add(goalId)`
      -- Set add of the new goal id; (6) `if (!rt.runners
      .containsKey(serveBytecode)) { rt.runners[serveBytecode] =
      BytecodeRunner(serveBytecode); }` -- Map contains-key guard +
      conditional insert; (7) `final channel = GlpChannelHandle(
      rt.heap, writerAddr); rt.glpChannels[moduleName] = channel;
      return channel;` -- construct handle, store it in the
      channel-name-keyed Map, return."
    target_decision: >-
      Map to a public static method `public static GlpChannelHandle
      ActivateModule(GlpRuntime rt, BytecodeProgram serveBytecode,
      BytecodeProgram moduleBytecode, string moduleName)` on a
      hosting `public static class GlpActivation` (top-level
      functions have no direct C# counterpart -- per the
      external_io.dart.md / fairness.dart.md / body_kernels.dart.md
      precedent, host them on a per-file static class named after
      the source library). All four parameters are required
      non-nullable positional parameters -- Dart `required` named
      args have no `required` keyword in C# method signatures, so
      the faithful counterpart is non-defaulted positional
      parameters (callers may use C# named-argument syntax `new
      ActivateModule(rt: ..., serveBytecode: ..., ...)` for
      call-site readability; identical to Dart). Body, statement by
      statement: (1) `var (writerAddr, readerAddr) = rt.Heap
      .AllocateVariable();` -- C# tuple deconstruction, both
      halves bound, no discard (rf-dart-record-destructure-to-
      csharp-valuetuple-deconstruction); (2) `var moduleTerm = new
      ModuleTerm(moduleBytecode, name: moduleName); var moduleAddr
      = rt.Heap.StoreTermOnHeap(moduleTerm);` -- named-argument
      `name:` preserved byte-identically in C# (named-arg syntax
      is the same); (3) `var goalId = rt.NextGoalId++; var env =
      new CallEnv(args: new Dictionary<int, Term> { { 0, new
      VarRef(moduleAddr) }, { 1, new VarRef(readerAddr) } }); rt
      .SetGoalEnv(goalId, env); rt.SetGoalProgram(goalId,
      serveBytecode);` -- the Dart map literal `{0: x, 1: y}` maps
      to a C# collection-initialiser `new Dictionary<int, Term>
      { { 0, x }, { 1, y } }` (rf-dart-map-to-csharp-dictionary
      from machine_state.dart.md / body_kernels.dart.md; the
      double-brace `{ key, val }` is the .NET dictionary-
      initialiser syntax, NOT a nested object initialiser);
      post-increment `++` has identical read-then-increment
      semantics in both languages (rf-dart-postincrement-and-
      method-shape-to-csharp-equivalent from heap_fcp.dart.md);
      (4) `var servePc = serveBytecode.Labels["serve/2"]!; rt.Gq
      .Enqueue(new GoalRef(goalId, servePc));` -- the Dart `!`
      null-bang post-fix maps to the C# null-forgiving operator
      `!` with identical "asserts non-null; runtime undefined if
      null" semantics (rf-dart-null-bang-to-csharp-null-
      forgiving from body_kernels.dart.md); Dart Map index
      `m['key']` maps to C# Dictionary index `m["key"]` (which
      throws KeyNotFoundException if absent -- same behaviour as
      Dart's Map index returning null + the `!` then crashing on
      `null.toX()`; semantic match up to the exception type, which
      is faithful to the spirit of the source's "must be present;
      crash if not" contract); `new GoalRef(goalId, servePc)`
      constructs the readonly record struct (machine_state.dart.md);
      (5) `rt.InfrastructureGoalIds.Add(goalId);` -- Dart `Set.add`
      maps to .NET `HashSet<int>.Add` (rt.InfrastructureGoalIds is
      a Set<int> per the runtime.dart convspec, not yet emitted but
      named here for cross-file consistency); return-value of
      `Add` is discarded in both languages; (6) `if (!rt.Runners
      .ContainsKey(serveBytecode)) { rt.Runners[serveBytecode] =
      new BytecodeRunner(serveBytecode); }` -- the Dart Map
      `containsKey` + indexed-insert idiom maps to C# Dictionary
      `ContainsKey` + indexer-set with identical semantics;
      codegen MUST NOT replace this with `TryAdd` (.NET 5+):
      `TryAdd` returns a `bool` discarded result and has slightly
      different "if-not-present" semantics that ARE equivalent
      here but the explicit two-step form preserves the source's
      reviewable shape (no semantic loss; choose the literal
      rendering); (7) `var channel = new GlpChannelHandle(rt
      .Heap, writerAddr); rt.GlpChannels[moduleName] = channel;
      return channel;` -- straight construction + Dictionary
      indexed-insert + return.
    idiom_id: null
    research_finding_id: rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults
    nuance: >-
      Named-required-args-without-defaults nuance: Dart `{required
      A a, required B b, ...}` with no defaulted parameters maps
      to C# non-defaulted positional parameters -- there is no C#
      method-parameter `required` keyword (the C# 11 `required`
      modifier is property-only, for object-initialiser
      enforcement, NOT a method-parameter feature). Callers may
      adopt C# named-argument syntax for call-site readability;
      semantically identical to Dart. Map-literal nuance (LOAD-
      BEARING): Dart `{0: x, 1: y}` is the canonical small-int-
      keyed CallEnv-args shape; the C# Dictionary-initialiser
      `new Dictionary<int, Term> { { 0, x }, { 1, y } }` is the
      faithful counterpart -- NOT a `Dictionary<int, Term>.From(
      ...)` factory (C# has no such factory) and NOT a
      `ImmutableDictionary` (the CallEnv is mutable per its own
      convspec). Tuple-destructure-no-discard nuance: BOTH halves
      of `allocateVariable()`'s return are used here (writer +
      reader); the wildcard `_` pattern from `send()` is NOT
      applicable. Postincrement-on-field nuance: `rt.nextGoalId++`
      uses the *current* value as the new goal id and stores the
      incremented value back on the runtime; C# `rt.NextGoalId++`
      has identical read-then-store-incremented semantics (the
      `NextGoalId` property MUST have `{ get; set; }` for `++`
      to compile; the runtime.dart convspec records this). Null-
      bang/null-forgiving nuance: `serveBytecode.labels['serve/2']!`
      asserts the label is present; C# `!` is the byte-for-byte
      counterpart and behaviour on null differs (Dart throws
      `TypeError` on the `!`; C# `!` is compile-time only and the
      indexer throws `KeyNotFoundException` if absent) -- both are
      "crash if absent" semantics, faithful at the spec level even
      though the thrown type differs (the source contract is
      "label must be present", not "throws TypeError"; codegen MUST
      NOT introduce a `TryGetValue` + throw to mimic the Dart
      exception type -- that would be over-translation). Set-add
      nuance: discarded return value is faithful in both
      languages; the Set type is determined by the runtime.dart
      convspec, not this file. Map-containsKey-then-indexed-insert
      nuance: explicit two-step form preserved (NOT TryAdd) to
      keep reviewable shape parity with the Dart source. Value-vs-
      reference: `GoalRef` is a value type per
      machine_state.dart.md (record struct -- stack-allocated, no
      boxing on `gq.Enqueue` of a struct into a non-generic
      collection because `Queue<GoalRef>` is generic per
      machine_state.dart.md's queue rendering). Null-safety: all
      four parameters are non-nullable reference/value types;
      under enabled NRT C# enforces this; the source has no `?`
      annotations on the parameters. Async/Stream/Future:
      ABSENT -- the function is synchronous; the spawned
      `serve(Module, ChannelReader?)` goal is enqueued, NOT
      awaited (the caller is responsible for draining the
      scheduler per the docstring). Isolate: ABSENT --
      single-isolate runtime. Side-effect ordering nuance: the
      seven steps MUST be emitted in the source order (channel
      create -> ModuleTerm store -> goal spawn -> label lookup
      and enqueue -> infrastructure tag -> runner register ->
      handle register and return); reordering would risk subtle
      observability bugs (e.g. registering the handle BEFORE the
      goal is enqueued could let another thread observe a
      handle whose goal is not yet scheduled -- though the
      runtime is single-threaded, the spec-preserving render
      keeps the source order).
conversion_units:
  - "namespace mirroring lib/runtime/ (depgraph stage decides root namespace name) -- library; directive elided; leading triple-slash module doc-block emitted as XML-doc on the namespace declaration"
  - "using directives: using <root>.Runtime; (covers GlpRuntime, CallEnv, Term, VarRef, ConstTerm, StructTerm, ModuleTerm, HeapFCP, GoalRef, GoalId, Pc) and using <root>.Bytecode; (covers BytecodeProgram, BytecodeRunner)"
  - "public class GlpChannelHandle (reference type; identity equality; private readonly HeapFCP _heap; private int _writerAddr; public expression-bodied int WriterAddr => _writerAddr;)"
  - "  - GlpChannelHandle ctor: public GlpChannelHandle(HeapFCP heap, int writerAddr) { _heap = heap; _writerAddr = writerAddr; } -- explicit body assignments (Dart this.field shorthand expanded)"
  - "  - Send(Term goal) -> IReadOnlyList<GoalRef>: var (tailWriterAddr, _) = _heap.AllocateVariable(); var consCell = new StructTerm(\".\", new List<Term> { goal, new VarRef(tailWriterAddr) }); var activations = _heap.BindVariable(_writerAddr, consCell); _writerAddr = tailWriterAddr; return activations; -- tuple deconstruction with `_` discard; cons-cell '.' functor preserved verbatim; mutates _writerAddr"
  - "  - Close() -> IReadOnlyList<GoalRef>: expression-bodied; returns _heap.BindVariable(_writerAddr, new ConstTerm(\"nil\")) -- 'nil' sentinel string preserved verbatim; no idempotence guard introduced"
  - "public static class GlpActivation (hosting class for the file's top-level function)"
  - "  - ActivateModule(GlpRuntime rt, BytecodeProgram serveBytecode, BytecodeProgram moduleBytecode, string moduleName) -> GlpChannelHandle: seven steps emitted in source order -- (1) tuple-deconstruct heap.AllocateVariable into (writerAddr, readerAddr); (2) build ModuleTerm with named arg `name:` and store on heap; (3) post-increment NextGoalId; build CallEnv with Dictionary<int, Term>{{0, new VarRef(moduleAddr)}, {1, new VarRef(readerAddr)}}; SetGoalEnv + SetGoalProgram; (4) label lookup serveBytecode.Labels[\"serve/2\"]! with C# null-forgiving operator; Gq.Enqueue(new GoalRef(goalId, servePc)); (5) InfrastructureGoalIds.Add(goalId); (6) containsKey-guarded Runners[serveBytecode] = new BytecodeRunner(serveBytecode) -- explicit two-step form, NOT TryAdd; (7) construct GlpChannelHandle, store in GlpChannels[moduleName], return"
  - "in-body `// 1. Create GLP channel ... // 7. Return channel handle ...` step-numbered comments retained as `//`-line comments verbatim (NOT promoted to XML-doc -- implementation notes, not API documentation)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-library-directive-to-csharp-namespace-elision — `library;` mapping (carry-forward)

- Deep analysis. The file opens with a triple-slash doc block ("GLP-level module activation. ... Phase 4 of dynamic module dispatch") followed by `library;`. The directive carries no library name; its sole role is to anchor the leading doc-comments to the library symbol the analyzer attaches them to. No code references the library by name.
- Authoritative Dart. https://dart.dev/language/libraries documents the `library;` directive as a compilation-unit declaration whose only structural effect is to host library-level annotations and doc-comments; the unnamed form has no observable surface beyond doc-anchoring.
- Authoritative .NET. .NET has no compilation-unit / library directive. The closest concept is the file-scoped `namespace` declaration (C# 10+ — https://learn.microsoft.com/dotnet/csharp/language-reference/proposals/csharp-10.0/file-scoped-namespaces), which anchors top-of-file XML-doc / `//`-comments to the namespace. The Dart library docs migrate to a namespace-scoped header.
- Decision. Elide `library;`; emit the leading doc-block as XML-doc on the namespace. Authoritative; no escalation. Carry-forward from suspension.dart.md / heap_fcp.dart.md (FR-024 cache hit; no re-research).

### rf-dart-import-relative-to-csharp-using-namespace — package-internal import mapping (carry-forward)

- Deep analysis. Five `import 'package:glp_runtime/...';` directives bring in the full public surface of five sibling libraries (runtime.dart, terms.dart, heap_fcp.dart, machine_state.dart, runner.dart). No `show`/`hide` clauses — full surface imported; the file actually uses only `GlpRuntime`/`CallEnv` (from runtime.dart), `Term`/`VarRef`/`ConstTerm`/`StructTerm`/`ModuleTerm` (terms.dart), `HeapFCP` (heap_fcp.dart), `GoalRef`/`GoalId`/`Pc` (machine_state.dart), and `BytecodeProgram`/`BytecodeRunner` (runner.dart).
- Authoritative Dart. https://dart.dev/language/libraries#using-libraries documents `import` as bringing the imported library's public surface into the current library's namespace; `show`/`hide` clauses narrow per-symbol.
- Authoritative .NET. https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/using-directive documents `using <namespace>;` as bringing all public types of a namespace into the current compilation unit; there is no per-symbol narrowing (the per-type alias form `using X = Ns.X;` introduces a single alias, not a narrowing of the surface). The .NET unit of import is the namespace, not the file.
- Decision. One `using <converted-namespace>;` per Dart import-line, deduplicated by target namespace (the five Dart imports collapse to two `using` directives because the five sibling files live in two namespaces). Authoritative; no escalation. Carry-forward from heap_fcp.dart.md / body_kernels.dart.md / external_io.dart.md (FR-024 cache hit).

### rf-dart-mutable-state-class-identity-equality-to-csharp-class — `GlpChannelHandle` reference-class mapping (carry-forward)

- Deep analysis. `GlpChannelHandle` is a mutable single-writer state container: `_heap` (final, reference to the shared HeapFCP), `_writerAddr` (mutable int, advanced by `send()`). No `==`/`hashCode` override → default identity equality. The runtime stores the handle keyed by module name in `rt.glpChannels` and the caller holds the same reference (returned from `activateModule`); both must alias the same instance so mutations propagate.
- Authoritative Dart. https://dart.dev/language/classes documents `class` declarations as reference-type entities with default identity equality unless `==` is overridden; mutable instance fields are mutated in place on the heap-allocated instance.
- Authoritative .NET. https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/class documents `class` as a reference-type with default identity equality; struct/record/record-struct alternatives are documented at https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/struct and https://learn.microsoft.com/dotnet/csharp/fundamentals/types/records and explicitly inject value-equality (records) or copy-on-assignment (structs). Both are rejected for this class because (a) identity is the observed contract (the caller's local handle must alias the dictionary-stored one), and (b) `_writerAddr` mutation must propagate across all holders.
- Decision. Reference-type `class` with private fields, public get-only property for the writer-address surface, two methods. Authoritative; no escalation. Carry-forward from machine_state.dart.md (`GoalState`), external_io.dart.md (`InputInjector`), hanger.dart.md (`Hanger`).

### rf-dart-positional-ctor-with-this-shorthand-to-csharp-positional-ctor-with-explicit-assignment — constructor shorthand expansion (carry-forward)

- Deep analysis. `GlpChannelHandle(this._heap, this._writerAddr);` uses Dart's `this.field` formal-parameter shorthand to declare two positional parameters that bind directly to the same-named instance fields. No body, no initialiser list, no `super` call.
- Authoritative Dart. https://dart.dev/language/constructors#initializing-formal-parameters documents `this.field` formal parameters as syntactic sugar for "declare a parameter of the field's type and assign it to the field before the constructor body runs". The expanded form is `Class(T x, U y) { this.f1 = x; this.f2 = y; }`.
- Authoritative .NET. https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/constructors documents C# constructors as positional or named-via-call-site, with all field/property assignments performed in the body or initialiser list. C# has no `this.field`-in-parameter-list shorthand; the explicit-assignment form is the faithful counterpart.
- Decision. Emit `public GlpChannelHandle(HeapFCP heap, int writerAddr) { _heap = heap; _writerAddr = writerAddr; }` — parameter names strip the leading underscore (C# convention reserves `_camelCase` for private fields, not parameters). Authoritative; no escalation. Carry-forward from suspend.dart.md (FR-024 cache hit).

### rf-dart-record-destructure-to-csharp-valuetuple-deconstruction — tuple deconstruction (carry-forward)

- Deep analysis. Two sites: (a) `GlpChannelHandle.send()` uses `final (tailWriterAddr, _) = _heap.allocateVariable();` with a `_` discard on the reader half (writer-only consumer); (b) `activateModule` uses `final (writerAddr, readerAddr) = rt.heap.allocateVariable();` binding both halves. Both are Dart 3.0+ record-pattern destructuring over a `(int, int)` record return.
- Authoritative Dart. https://dart.dev/language/patterns#patterns-in-variable-declarations documents `final (a, b) = expr;` as the destructuring form for positional records and tuples; `_` is the discard pattern.
- Authoritative .NET. https://learn.microsoft.com/dotnet/csharp/fundamentals/functional/deconstruct documents `var (a, b) = expr;` as the C# 7+ tuple-deconstruction form, and https://learn.microsoft.com/dotnet/csharp/fundamentals/functional/discards documents `_` as the discard pattern in deconstruction. Semantic match: no binding, no allocation, no warning.
- Decision. `var (writer, _) = ...;` for `send()`, `var (writer, reader) = ...;` for `activateModule`. Authoritative; no escalation. Carry-forward from external_io.dart.md / body_kernels.dart.md (FR-024 cache hit).

### rf-dart-top-level-fn-builds-sum-type-leaf — `Close()` ConstTerm('nil') construction (carry-forward)

- Deep analysis. `close()` constructs a `ConstTerm('nil')` sentinel — the Dart-FCP empty-list encoding — and passes it to `_heap.bindVariable`. The string literal `nil` is load-bearing: the bytecode compiler, the runtime list-walker, and the trace-log formatters all key on the exact string `nil`.
- Authoritative Dart. ConstTerm semantics are documented in this repo's terms.dart.md convspec (which is the authoritative spec for `ConstTerm`, a sealed leaf of the `Term` hierarchy). The `'nil'` functor name is a project-internal convention; the convspec at terms.dart.md governs the C# rendering of `ConstTerm`.
- Authoritative .NET. https://learn.microsoft.com/dotnet/csharp/language-reference/operators/new-operator documents `new` for reference-type construction; no canonicalisation occurs (each `new ConstTerm("nil")` is a fresh heap allocation, matching Dart `ConstTerm('nil')`).
- Decision. `new ConstTerm("nil")`, literal preserved byte-identically; no idempotence guard introduced (Dart source has none; codegen MUST NOT add one — would be over-translation, FR-013/FR-024). Authoritative; no escalation. Carry-forward from external_io.dart.md.

### rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults — `activateModule` named-required-arg mapping (carry-forward, no defaults)

- Deep analysis. `activateModule({required GlpRuntime rt, required BytecodeProgram serveBytecode, required BytecodeProgram moduleBytecode, required String moduleName})` — four Dart `required` named parameters, none defaulted. The call-site `activateModule(rt: r, serveBytecode: s, moduleBytecode: m, moduleName: "x")` is named-argument-only.
- Authoritative Dart. https://dart.dev/language/functions#named-parameters documents `{required T x}` as a named parameter that callers MUST supply; named-args are not positional (order-independent at call site).
- Authoritative .NET. https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments documents C# named-argument syntax `methodName(x: value, y: value)` as identical to Dart at the call site. The C# method declaration uses positional parameters; the `required` keyword on method parameters does NOT exist (the C# 11 `required` modifier is property-only — https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/required — and applies to object initializers, NOT method calls). Non-defaulted positional parameters are the faithful counterpart of `required` named parameters because both require the caller to supply a value.
- Decision. Positional parameters with no defaults: `public static GlpChannelHandle ActivateModule(GlpRuntime rt, BytecodeProgram serveBytecode, BytecodeProgram moduleBytecode, string moduleName)`. Callers MAY use named-arg syntax `ActivateModule(rt: ..., serveBytecode: ..., ...)` for parity with Dart call sites. Authoritative; no escalation. Carry-forward from machine_state.dart.md / commit.dart.md / heap_fcp.dart.md / suspend_ops.dart.md (FR-024 cache hit).

## Idiom KB note

Every `research_finding_id` above is a prior-spec cache hit (the rf-IDs appear in the rf-ID grep across .codeconv/conversion-specs/lib/runtime/). No new research was conducted for this file (FR-024 — never re-research a cached construct_key); no idiom-vs-research or idiom-vs-idiom conflict was detected. No construct was undecidable. `escalations: []`.
