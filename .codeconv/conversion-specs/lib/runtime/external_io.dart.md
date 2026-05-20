# Conversion Spec — lib/runtime/external_io.dart

> Conversion-spec artifact for lib/runtime/external_io.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> External I/O bridge between Dart and the GLP heap. Despite the file's
> name and the leading doc-comment phrase "External I/O", this file
> contains **no `dart:io` API surface** — no `stdin` / `stdout` / `File`
> / `Platform` / `Process` / `Directory` / `Encoding` references appear
> in the source. "External" here means "external to the GLP runtime"
> (i.e. Dart-side observers/injectors over heap variable cells), not
> "external to the Dart process". The conversion therefore does NOT
> involve `System.IO` / `System.Console` mappings; the well-known
> dart:io→System.IO nuance is correctly NOT asserted here because the
> CODE does not exercise it (per the same discipline applied in
> boot_loader.dart.md: "asserting an absent nuance would be noise").
>
> The load-bearing nuances exercised by THIS file are: (a) Dart record
> destructuring `final (a, b) = heap.allocateVariable();` (no .NET
> tuple-pattern syntactic counterpart pre-C# 7 — but C# 7+ deconstruction
> `var (a, b) = ...;` is the faithful match); (b) `late final` non-
> nullable fields initialised in a factory body, NOT in the private
> constructor (load-bearing because Dart `late final` is a deferred-
> initialisation invariant that does NOT exist verbatim in C# — the
> spec maps it to `{ get; private set; }` with a documented init
> discipline, NOT to `init`-only or readonly); (c) callback-style
> observer registration (`heap.onBind(addr, (Term value) { ... })`) +
> teardown (`heap.removeBindCallback(addr)`) — a single-subscriber
> delegate pattern lifted from the heap layer, faithful via
> `Action<Term>` rather than `event` (idiom reuse from
> repl_play_runner.dart.md `rf-dart-void-function-question-to-csharp-
> action-nullable`); (d) Dart `is`-test type narrowing on the sum-type
> `Term` hierarchy (`value is StructTerm && value.functor == '.'`) maps
> to C# type-pattern `value is StructTerm st && st.Functor == "."`,
> with the captured pattern variable load-bearing because the Dart
> `is`-test narrows the original variable's static type within the
> branch but C# requires the explicit capture (idiom-carry from
> terms.dart.md's closed-sum-type rationale).

```yaml
schema_version: 1
source_path: lib/runtime/external_io.dart
source_sha256: 7295d8789cac898386fecbab28013d922a922c8fe718a59c8c19c9fc979a4c14
target_code_unit: lib/runtime/external_io.cs
constructs:
  - construct_key: dart.library_directive.top_of_file_no_name
    source_form: >-
      "Top-of-file `library;` directive (no library name) following the
      leading triple-slash doc-comments ('External I/O for GLP - Phase 0
      Implementation' and the per-class doc references to heap-pointer-
      architecture-spec.md and docs/glp-io-spec.md)."
    target_decision: >-
      No direct .NET counterpart — .NET's compilation-unit / namespace
      model has no `library` concept. The library-level doc comments
      (External I/O for GLP, the spec citations to heap-pointer-
      architecture-spec.md Section 1.1, CGLP paper Definition 5.5, and
      bytecode spec Section 8.1-8.2) become a file-header XML doc on
      the namespace declaration mirroring `lib/runtime/`. The `library;`
      directive itself is elided. Carry-forward of the idiom recorded in
      heap_fcp.dart.md / suspension.dart.md / variable_table.dart.md.
    idiom_id: null
    research_finding_id: rf-dart-library-directive-to-csharp-namespace-elision
    nuance: >-
      Compilation-unit nuance only; no value/reference, null-safety, or
      async surface implicated. FR-024 cache hit on the carry-forward
      idiom — do NOT re-research.

  - construct_key: dart.import_directive.package_internal_to_using_namespace
    source_form: >-
      Three `import` directives: `import 'terms.dart';` (Term, VarRef,
      ConstTerm, StructTerm), `import 'heap_fcp.dart';` (HeapFCP with
      `allocateVariable`, `bindVariable`, `tryWriterForReader`, `onBind`,
      `removeBindCallback`), `import 'machine_state.dart';` (GoalRef —
      the doc comment "For GoalRef" is preserved verbatim).
    target_decision: >-
      Each Dart relative import becomes a .NET `using` directive naming
      the namespace of the corresponding converted file: `using
      <root>.Runtime;` covers all three sibling files (`terms.cs`,
      `heap_fcp.cs`, `machine_state.cs` all target the same
      `lib/runtime/` namespace). No `show`-style allow-list applies (the
      Dart source uses bare imports without `show`-narrowing). Carry-
      forward of the idiom recorded in heap_fcp.dart.md.
    idiom_id: null
    research_finding_id: rf-dart-import-relative-to-csharp-using-namespace
    nuance: >-
      Compilation-unit nuance: Dart resolves package imports by URI; .NET
      resolves type references by assembly + namespace. The `show`
      allow-list has no parallel — does not arise here. No value-vs-
      reference, null-safety, or async surface implicated by import
      directives.

  - construct_key: dart.data_class.final_int_fields_named_required_ctor_tostring_override
    source_form: >-
      "class ExternalChannel { final String name; final int inputWriterAddr;
      final int inputReaderAddr; final int outputWriterAddr; final int
      outputReaderAddr; ExternalChannel({ required this.name, required
      this.inputWriterAddr, required this.inputReaderAddr, required
      this.outputWriterAddr, required this.outputReaderAddr, }); @override
      String toString() => 'ExternalChannel($name, in=($inputWriterAddr,$inputReaderAddr), out=($outputWriterAddr,$outputReaderAddr))'; }"
    target_decision: >-
      Emit a C# reference `class ExternalChannel` (NOT `record`, NOT
      `struct`) with five get-only auto-properties (`Name` string, plus
      four `long` for the writer/reader addresses) initialised by a
      single constructor taking the same five parameters. Dart `int` →
      C# `long` per the Dart-int width-fidelity policy (Dart `int` is
      64-bit on the native runtime, see terms.dart.md
      rf-dart-int-to-csharp-long); heap addresses fit comfortably in
      Int32 but the spec preserves Dart-int → long for cross-file
      consistency and to avoid silent narrowing when the heap grows.
      Named-required parameters → ordinary C# constructor parameters
      with no defaults (same compile-site obligation). `toString()`
      override → expression-bodied `public override string ToString()
      => $"ExternalChannel({Name}, in=({InputWriterAddr},{InputReaderAddr}), out=({OutputWriterAddr},{OutputReaderAddr}))";`
      byte-identical punctuation (load-bearing for diagnostic logs and
      test assertions). A `record` is REJECTED because (a) callers
      (createExternalChannel + AgentIOContext factory) hold these
      instances by reference and pass them through factory and
      observer wiring — synthesised structural equality would be an
      unused behavioural addition; (b) the explicit toString shape
      MUST be preserved verbatim, which a record's synthesised
      ToString would override differently. A `struct` is REJECTED
      because the instance escapes through factory return and is
      shared across InputInjector + OutputObserver (boxing on every
      use would be a silent allocation regression).
    idiom_id: null
    research_finding_id: rf-dart-final-field-class-to-csharp-getonly-class
    nuance: >-
      Immutability nuance (explicitly addressed): all five Dart fields
      are `final` → C# get-only auto-properties (`{ get; }`), NOT
      `readonly` fields. Reference-vs-value nuance: must remain a
      reference `class` — instances are aliased across the spawn-and-
      observe pipeline (one ExternalChannel passed to both an
      InputInjector and an OutputObserver, then re-read by
      buildChannelTerm). Null-safety nuance: all five fields are
      NON-nullable in Dart (String, int — no `?`); map to non-nullable
      C# types under enabled NRT. Int-width nuance (explicitly
      addressed and load-bearing for cross-file consistency): Dart
      `int` → C# `long` per the carry-forward policy from terms.dart.md
      / machine_state.dart.md — heap addresses, writer IDs, and reader
      IDs all share the same width discipline. Default-value nuance:
      no field has a default; all five are required at construction.

  - construct_key: dart.tostring_override.string_interpolation_no_branch_multifield
    source_form: >-
      "@override String toString() => 'ExternalChannel($name, in=($inputWriterAddr,$inputReaderAddr), out=($outputWriterAddr,$outputReaderAddr))';"
    target_decision: >-
      Emit `public override string ToString() => $"ExternalChannel({Name}, in=({InputWriterAddr},{InputReaderAddr}), out=({OutputWriterAddr},{OutputReaderAddr}))";`
      overriding `System.Object.ToString` with a C# interpolated
      verbatim of the Dart shape. Punctuation `ExternalChannel(`, `, in=(`,
      `,`, `), out=(`, `))` preserved byte-identically. Expression-
      bodied form mirrors the Dart `=>` arrow body. Carry-forward of
      rf-dart-tostring-interp-to-csharp-tostring-interp from
      boot_loader.dart.md (FR-012/SC-007 reuse).
    idiom_id: null
    research_finding_id: rf-dart-tostring-interp-to-csharp-tostring-interp
    nuance: >-
      toString nuance: override `object.ToString()` (no extension-
      method alternative). Interpolation nuance: Dart `$x` → C# `{X}`;
      for integer fields the invariant decimal text matches on both
      sides (no culture-sensitivity surface). Single-branch (no null
      check) because all five fields are non-nullable. No null
      coalescence required.

  - construct_key: dart.top_level_factory_function.constructs_class_via_named_args_from_two_tuple_returns
    source_form: >-
      "ExternalChannel createExternalChannel(HeapFCP heap, String name) {
      final (inputWriterAddr, inputReaderAddr) = heap.allocateVariable();
      final (outputWriterAddr, outputReaderAddr) = heap.allocateVariable();
      return ExternalChannel(name: name, inputWriterAddr: inputWriterAddr,
      inputReaderAddr: inputReaderAddr, outputWriterAddr: outputWriterAddr,
      outputReaderAddr: outputReaderAddr); }"
    target_decision: >-
      A Dart top-level (file-level, non-method) function maps to a C#
      `public static` method on a hosting static class (the conventional
      C# stand-in for file-level functions, since C# requires every
      method to belong to a type). The spec emits a `public static
      class ExternalIO` in the same `lib/runtime/` namespace, with a
      `public static ExternalChannel CreateExternalChannel(HeapFCP heap,
      string name)` method body. Inside the body, the Dart RECORD
      DESTRUCTURING `final (inputWriterAddr, inputReaderAddr) = heap
      .allocateVariable();` (where `allocateVariable()` returns a
      two-element record of `(int, int)`) maps to C# 7+ TUPLE
      DECONSTRUCTION `var (inputWriterAddr, inputReaderAddr) = heap
      .AllocateVariable();` — the .NET API surface returns
      `(long inputWriterAddr, long inputReaderAddr)` (a
      `System.ValueTuple<long, long>`, the spec-preferred lightweight
      multi-return shape; the Dart record `(int, int)` and the .NET
      `(long, long)` value-tuple match shape-for-shape). Constructing
      the result via named arguments `ExternalChannel(name: name, ...)`
      maps to C# `new ExternalChannel(name: name, ...)` — C# named-
      argument syntax is identical to Dart's at the call site.
    idiom_id: null
    research_finding_id: rf-dart-record-destructure-to-csharp-valuetuple-deconstruction
    nuance: >-
      File-level-function nuance (explicitly addressed): Dart permits
      top-level functions outside any class; C# requires a hosting
      type. The faithful translation is `public static` methods on a
      `public static class ExternalIO` — NOT an instance method on
      `ExternalChannel` (would force callers to construct a temporary)
      and NOT a free C# 12 file-scoped statement (does not apply in
      library code). Record-destructuring nuance (LOAD-BEARING):
      Dart's `final (a, b) = expr;` is RECORD destructuring (positional
      pattern over a Dart record type), a Dart 3.0+ feature; .NET
      `ValueTuple` deconstruction `var (a, b) = expr;` is its
      faithful counterpart (Microsoft Learn: "Deconstructing tuples
      and other types"). The two languages diverge in surface vocab
      ("record" vs "tuple") but agree on the semantic — a multi-
      element positional value carrier. Int-width nuance: Dart `int`
      tuple elements → C# `long` tuple elements per width policy.
      Named-argument nuance: faithful one-to-one because C# named
      args use identical `name: value` syntax.

  - construct_key: dart.top_level_function.builds_struct_term_with_varref_args_for_cons_functor
    source_form: >-
      "Term buildChannelTerm(ExternalChannel channel) { return StructTerm(
      'ch', [ VarRef(channel.inputReaderAddr), VarRef(channel
      .outputWriterAddr), ]); }"
    target_decision: >-
      `public static Term BuildChannelTerm(ExternalChannel channel)` on
      the same `ExternalIO` hosting static class. Body: `return new
      StructTerm("ch", new List<Term> { new VarRef(channel
      .InputReaderAddr), new VarRef(channel.OutputWriterAddr) });`. The
      Dart string literal `'ch'` maps to a C# `"ch"` literal byte-
      identically. The Dart `[ ... ]` list literal of `Term` becomes a
      C# `new List<Term> { ... }` collection-initialiser; the
      StructTerm constructor accepts an `IReadOnlyList<Term>` per
      terms.dart.md, so the inline `List<Term>` is the load-bearing
      backing reference that the caller shares (no defensive copy —
      mirroring Dart's `this.args = args` reference-sharing semantics
      from rf-dart-sumleaf-with-list-no-eq-to-csharp-class-
      ireadonlylist). The `VarRef` constructor takes a `long` address
      per terms.dart.md's int-width policy.
    idiom_id: null
    research_finding_id: rf-dart-top-level-fn-builds-sum-type-leaf
    nuance: >-
      List-literal nuance (explicitly addressed): Dart `[a, b]` is a
      growable `List<Term>` (NOT `const`); the C# render `new List<Term>
      { ... }` is also growable. Reference-sharing nuance (carry-
      forward from terms.dart.md): the `List<Term>` reference is
      passed BY REFERENCE to the `StructTerm` constructor and aliased
      — no defensive copy — preserving Dart's `final List<Term> args`
      "rebind-final, body-mutable" semantics. Sum-type-leaf nuance:
      `VarRef` is a sealed leaf of the `Term` hierarchy (per
      terms.dart.md); construction `new VarRef(addr)` matches the
      Dart `VarRef(addr)` byte-for-byte.

  - construct_key: dart.mutable_class.single_mutable_int_field_callback_returning_list
    source_form: >-
      "class InputInjector { final HeapFCP heap; final String channelName;
      int _currentWriterId; InputInjector(this.heap, this.channelName, int
      initialWriterId) : _currentWriterId = initialWriterId; int get
      currentWriterId => _currentWriterId; List<GoalRef> inject(Term term)
      { final (tailWriterAddr, _) = heap.allocateVariable(); final listCell
      = StructTerm('.', [term, VarRef(tailWriterAddr)]); final activations
      = heap.bindVariable(_currentWriterId, listCell); _currentWriterId =
      tailWriterAddr; return activations; } List<GoalRef> close() { return
      heap.bindVariable(_currentWriterId, ConstTerm('nil')); } }"
    target_decision: >-
      Emit a reference `class InputInjector` with: two get-only
      properties (`HeapFCP Heap { get; }`, `string ChannelName { get; }`
      — Dart `final` fields), one private mutable field (`private long
      _currentWriterId;` — Dart non-final integer field, reassigned in
      `inject()`), a public get-only accessor (`public long
      CurrentWriterId => _currentWriterId;` — expression-bodied,
      mirroring the Dart `int get currentWriterId => _currentWriterId;`
      shape). Constructor: `public InputInjector(HeapFCP heap, string
      channelName, long initialWriterId) { Heap = heap; ChannelName =
      channelName; _currentWriterId = initialWriterId; }` — positional
      parameters mirroring the Dart positional constructor with the
      `_currentWriterId` initialised in the initialiser list (`: _x =
      y` Dart → C# constructor body assignment, since C# has no
      initialiser-list-only field assignment). `Inject(Term term)`
      returns `IReadOnlyList<GoalRef>` (matches Dart `List<GoalRef>`
      return shape — see boot_loader.dart.md for the
      `List`→`IReadOnlyList` convention on return values where
      callers do not mutate). The wildcard `_` in `final
      (tailWriterAddr, _) = ...` maps to C# `var (tailWriterAddr, _)
      = ...` — C# 7+ supports the `_` discard pattern in tuple
      deconstruction with identical semantics (Microsoft Learn:
      "Discards"). `Close()` returns `IReadOnlyList<GoalRef>`
      analogously and uses the ConstTerm("nil") sentinel
      construction.
    idiom_id: null
    research_finding_id: rf-dart-mutable-int-field-callback-list-return
    nuance: >-
      Mutability nuance (explicitly addressed and load-bearing): the
      `_currentWriterId` field is reassigned every call to `inject()`
      — a mutable single-writer state. C# `private long` (NOT
      `readonly long`, NOT a `{ get; private set; }` property) is
      the faithful render — the mutation is encapsulated within the
      class and is not part of the public surface. Discard-pattern
      nuance (LOAD-BEARING): the Dart `_` placeholder in record
      destructuring `final (tailWriterAddr, _) = ...` discards the
      second tuple element (the unused reader address); C# `var
      (tailWriterAddr, _) = ...` does the same with identical
      semantics — no binding, no allocation, no warning. Return-
      collection nuance: Dart `List<GoalRef>` returned from
      `heap.bindVariable` IS a `List<GoalRef>` (mutable) but the
      caller treats it as read-only (immediately iterated to enqueue
      goals); the spec exposes the surface as `IReadOnlyList<GoalRef>`
      to record the immutability invariant the consumer relies on,
      matching the convention in boot_loader.dart.md
      `_parseBootClause` returning `IReadOnlyList<SpawnDirective>`.
      Initialiser-list nuance: Dart `: _currentWriterId =
      initialWriterId` runs BEFORE the constructor body; C# has no
      initialiser-list syntax outside the special chained-ctor form
      `: this(...)` / `: base(...)`, so the assignment moves into
      the constructor body — semantically equivalent because there
      is no subclass field-initialiser ordering hazard here (no
      base class).

  - construct_key: dart.bind_callback_registration.heap_observer_with_is_test_sum_type_narrowing
    source_form: >-
      "class OutputObserver { final HeapFCP heap; final String channelName;
      final void Function(Term) onTerm; final void Function() onClose; int
      _currentReaderId; bool _closed = false; OutputObserver(this.heap,
      this.channelName, int initialReaderId, this.onTerm, this.onClose,) :
      _currentReaderId = initialReaderId { _observeNext(); } int get
      currentReaderId => _currentReaderId; bool get isClosed => _closed;
      void _observeNext() { if (_closed) return; final writerAddr = heap
      .tryWriterForReader(_currentReaderId); if (writerAddr == null) {
      return; } heap.onBind(writerAddr, (Term value) { if (_closed) return;
      if (value is StructTerm && value.functor == '.') { final head = value
      .args[0]; final tail = value.args[1]; onTerm(head); if (tail is
      VarRef) { _currentReaderId = tail.addr; _observeNext(); } else if
      (tail is ConstTerm && tail.value == 'nil') { _closed = true; onClose();
      } else if (tail is StructTerm && tail.functor == '.') {
      _processNestedCons(tail); } } else if (value is ConstTerm && value
      .value == 'nil') { _closed = true; onClose(); } }); } void
      _processNestedCons(StructTerm cons) { var current = cons; while
      (true) { final head = current.args[0]; final tail = current.args[1];
      onTerm(head); if (tail is VarRef) { _currentReaderId = tail.addr;
      _observeNext(); break; } else if (tail is ConstTerm && tail.value ==
      'nil') { _closed = true; onClose(); break; } else if (tail is
      StructTerm && tail.functor == '.') { current = tail; } else { break; }
      } } void dispose() { _closed = true; final writerAddr = heap
      .tryWriterForReader(_currentReaderId); if (writerAddr != null) { heap
      .removeBindCallback(writerAddr); } } }"
    target_decision: >-
      Emit a reference `class OutputObserver` mirroring the Dart class
      one-to-one. Fields: two get-only properties for the `final`
      references (`HeapFCP Heap { get; }`, `string ChannelName { get; }`),
      two get-only callback fields (`Action<Term> OnTerm { get; }`,
      `Action OnClose { get; }`) — Dart `void Function(Term)` →
      C# `Action<Term>`; Dart `void Function()` → C# `Action`. CRITICAL
      DIFFERENCE FROM repl_play_runner.dart.md: those callbacks are
      Dart-NULLABLE (`void Function(...)?`) — these are NON-NULLABLE
      (`void Function(Term)` with NO `?`), so the C# render is
      `Action<Term>` (no `?`) under enabled NRT, AND the call sites
      use direct invocation `OnTerm(head)` / `OnClose()` (NOT
      `OnTerm?.Invoke(head)` — null-conditional would be over-
      translation here). Private mutable fields: `private long
      _currentReaderId;` and `private bool _closed = false;` (mirror
      Dart's mutable fields). Constructor: positional parameters
      including the two callbacks, body initialises `_currentReaderId`
      then calls `_ObserveNext()` (Dart's initialiser-list
      `_currentReaderId = initialReaderId` THEN the constructor body
      `{ _observeNext(); }` collapses to a single constructor body
      in C# where the order is explicit). The `is` TYPE-PATTERN
      narrowing on the sum-type `Term` hierarchy maps to C# type-
      patterns with CAPTURE variables: Dart `if (value is StructTerm
      && value.functor == '.')` → C# `if (value is StructTerm st &&
      st.Functor == ".")` — the C# capture `st` is LOAD-BEARING
      because C# `is`-test (pre-pattern-matching) does NOT narrow
      the original variable's static type in the branch; the pattern
      variable `st` provides the narrowed handle. Same for Dart
      `tail is VarRef` → C# `tail is VarRef vr` (then `vr.Addr`),
      `tail is ConstTerm && tail.value == 'nil'` → C# `tail is
      ConstTerm ct && ct.Value is "nil"` (and the inner equality is
      a value comparison on a nullable `object?` field — see nuance
      on the `ConstTerm.value` field type). The `onBind` registration
      `heap.onBind(writerAddr, (Term value) { ... })` maps to a C#
      lambda passed to `Heap.OnBind(writerAddr, (Term value) => {
      ... });` — Dart `(Term value) { ... }` block lambda → C#
      `(Term value) => { ... }` statement-bodied lambda. The inner
      while-loop in `_ProcessNestedCons` maps to a C# `while (true)
      { ... }` with `break` statements — identical control-flow
      semantics. `Dispose()` mirrors the Dart `dispose()` method
      (rename to PascalCase) but is NOT named `Dispose` for
      IDisposable conformance — the spec deliberately keeps the
      method name `Dispose()` (matching PascalCased Dart) WITHOUT
      adding `: IDisposable` to the class, because the Dart source
      makes NO disposal-interface declaration and the cleanup
      contract is caller-driven (AgentIOContext.dispose() chains
      to `userOutput.dispose()` and `netOutput.dispose()`). Adding
      `IDisposable` would be a behaviour addition (e.g.
      `using`-statement participation, finaliser obligations) and
      is intentionally NOT specified — keeping line-for-line
      fidelity (same discipline as repl_play_runner.dart.md's
      Process-handle-no-IDisposable nuance).
    idiom_id: null
    research_finding_id: rf-dart-is-test-narrowing-to-csharp-type-pattern-capture
    nuance: >-
      Sum-type type-narrowing nuance (LOAD-BEARING, explicitly
      addressed): Dart `is`-tests on a sum-type variable narrow the
      static type WITHIN the matched branch (`if (value is StructTerm)
      { value.functor }` — `value` has static type `StructTerm` in
      the body). C# `is`-tests WITHOUT a pattern variable DO NOT
      narrow the original variable's static type (the Dart-style
      `if (value is StructTerm) { value.Functor }` would not compile
      in C# — `value` retains static type `Term`). The faithful C#
      render REQUIRES the type-pattern with capture variable
      (Microsoft Learn: "Pattern matching with the is and switch
      expressions" — "If the pattern matches, the result of the
      expression is assigned to the variable declared on the left
      of the operator"). Every `is`-test in this file must be
      rewritten with an explicit capture variable. Callback
      nullability nuance (explicitly addressed and DIFFERENTIATING
      from repl_play_runner): these callbacks are Dart-NON-nullable
      (the source declares `void Function(Term) onTerm`, no `?`) —
      caller MUST supply non-null; the C# render is `Action<Term>`
      (no `?`) and call-sites use `OnTerm(head)` directly without
      null-conditional. Closure-capture nuance: the lambda passed to
      `heap.onBind` captures `this._closed` / `this._currentReaderId`
      / `this.onTerm` / `this.onClose` by reference (Dart closure
      semantics); C# lambdas capture instance fields via `this`
      identically — no behaviour change. Recursion nuance: the
      lambda invokes `_observeNext()` recursively on tail
      continuation; both Dart and C# permit lambda → instance-method
      → lambda recursion through the enclosing `this`. Sum-type
      pair-with-equality nuance: `value is ConstTerm && value.value
      == 'nil'` — the inner `value.value == 'nil'` compares a
      nullable `object?` field (per terms.dart.md `ConstTerm.value`
      being `object?`) to a Dart string literal `'nil'`. In Dart
      `Object?.==` performs structural equality on String (Dart's
      `==` on String is value equality). In C# `object?.Equals` /
      `==` on `object` is REFERENCE equality unless overridden —
      but C# `string` overrides `==` for value equality, AND C#
      pattern matching `ct.Value is "nil"` (constant pattern)
      performs the same value-equality check semantically. The
      faithful render uses the constant pattern `ct.Value is "nil"`
      to preserve value-equality semantics (Microsoft Learn:
      "Constant pattern" — "if the expression is equal to the
      constant"). Initialiser-list-plus-body nuance: Dart `:
      _currentReaderId = initialReaderId { _observeNext(); }` runs
      the field assignment THEN the constructor body; C# has no
      initialiser-list outside chained-ctor calls, so the order
      collapses to constructor body `{ _currentReaderId =
      initialReaderId; _ObserveNext(); }` — semantically
      equivalent (no base-class field-initialiser ordering hazard).

  - construct_key: dart.late_final_field_assigned_in_factory_with_private_named_ctor
    source_form: >-
      "class AgentIOContext { final String agentId; final HeapFCP heap;
      final ExternalChannel userChannel; final InputInjector userInput; late
      final OutputObserver userOutput; final ExternalChannel netChannel;
      final InputInjector netInput; late final OutputObserver netOutput;
      final List<Term> userOutputTerms = []; final List<Term> netOutputTerms
      = []; bool userOutputClosed = false; bool netOutputClosed = false;
      AgentIOContext._({ required this.agentId, required this.heap, required
      this.userChannel, required this.userInput, required this.netChannel,
      required this.netInput, }); factory AgentIOContext.create(HeapFCP heap,
      String agentId) { ... context.userOutput = OutputObserver(...);
      context.netOutput = OutputObserver(...); return context; } }"
    target_decision: >-
      Emit a reference `class AgentIOContext` with: six get-only
      properties for the `final` fields (`AgentId`, `Heap`,
      `UserChannel`, `UserInput`, `NetChannel`, `NetInput`), TWO
      `{ get; private set; }` properties for the two Dart `late
      final` fields (`UserOutput`, `NetOutput`), TWO get-only
      properties exposing the collected-terms lists
      (`IReadOnlyList<Term> UserOutputTerms` / `NetOutputTerms`,
      backed by `private readonly List<Term>` fields initialised
      inline to `new List<Term>()`), TWO read-write `{ get; set; }`
      properties (`UserOutputClosed`, `NetOutputClosed`, both
      `bool` defaulting to `false`). Constructor: the Dart PRIVATE
      NAMED constructor `AgentIOContext._({ required ... })` maps
      to a C# `private` constructor with named-argument syntax at
      the call site (the underscore in Dart's `._` is a library-
      private factory-only constructor convention; C# `private`
      enforces class-private access which is strictly tighter but
      faithful because the only caller is the in-class `Create`
      factory method). The `factory AgentIOContext.create(...)` Dart
      factory constructor maps to a C# `public static AgentIOContext
      Create(HeapFCP heap, string agentId)` — Dart factory
      constructors have no direct C# counterpart (factory pattern
      = static method returning an instance, per Microsoft Learn
      C# factory-pattern guidance). The `late final` field
      assignment `context.userOutput = OutputObserver(...);` inside
      the factory body is the LOAD-BEARING construct: Dart `late
      final` is a deferred-initialisation invariant (write-once,
      but the assignment may be deferred to AFTER the constructor
      returns); C# has NO direct counterpart — `init`-only setters
      (`{ get; init; }`) allow assignment ONLY inside object-
      initialiser / constructor / `with`-expression, NOT after the
      object is fully constructed; `readonly` fields can only be
      assigned in the constructor body. The faithful render is
      `public OutputObserver UserOutput { get; private set; } =
      null!;` — a `{ get; private set; }` property with a
      null-forgiving default, assigned exactly once by the
      `Create` factory before returning. The `null!` initialiser
      is a documented C# pattern for late-initialised fields
      under NRT (Microsoft Learn: "The null-forgiving operator —
      typical uses include … assigning to a non-nullable field
      that's initialized later"). Code-review discipline (NOT
      compile-time enforcement) ensures the field is written
      exactly once. Constants list-init: Dart `final List<Term>
      userOutputTerms = []` → C# `private readonly List<Term>
      _userOutputTerms = new List<Term>();` with the public
      surface `public IReadOnlyList<Term> UserOutputTerms =>
      _userOutputTerms;` — preserves Dart's "rebind-final, body-
      mutable" semantics (the list reference is fixed, but the
      OutputObserver callbacks `.Add(term)` to it). Computed
      getters: `Term get userChannelTerm => buildChannelTerm(
      userChannel);` → C# `public Term UserChannelTerm =>
      ExternalIO.BuildChannelTerm(UserChannel);` — expression-
      bodied computed property, faithful one-to-one. `Dispose()`
      chains to `UserOutput.Dispose()` and `NetOutput.Dispose()`.
      `toString()` override mirrors the simpler shape
      `$"AgentIOContext({AgentId})"`.
    idiom_id: null
    research_finding_id: rf-dart-late-final-to-csharp-getprivateset-with-null-forgiving
    nuance: >-
      `late final` nuance (LOAD-BEARING, explicitly addressed):
      Dart `late final` is a deferred-initialisation contract — the
      field is non-nullable, must be written EXACTLY ONCE, and the
      write may happen at ANY POINT after construction (typically
      from within a factory closure or from another method called
      from a factory). C# has THREE non-faithful candidates and
      ONE faithful render: (a) `readonly` field — REJECTED because
      `readonly` only allows assignment in the same-class
      constructor body, not from a factory `static` method after
      construction; (b) `{ get; init; }` — REJECTED because `init`
      only allows assignment within object-initialiser / `with` /
      ctor, NOT after the object is returned to a factory caller
      (Microsoft Learn: "init accessor … restricts the property's
      assignment to object construction"); (c) `Lazy<T>` — REJECTED
      because the Dart `late final` here is NOT lazily computed
      on first access — it is eagerly assigned in the factory
      before return. The FAITHFUL render is (d) `{ get; private
      set; }` with `= null!;` and code-review discipline; the
      private setter is exercised exactly once by the in-class
      `Create` factory before the instance escapes the factory.
      The null-forgiving initialiser `= null!;` is documented by
      Microsoft Learn as the canonical pattern for "non-nullable
      field initialised later" — pragma-quality but it is the
      .NET-idiomatic counterpart of Dart's `late` keyword in this
      shape. A future hardening pass MAY introduce a Roslyn
      analyser to enforce the exactly-once invariant; the SPEC
      records the invariant as a code-review obligation. Factory-
      constructor nuance (explicitly addressed): Dart `factory
      X.create(...)` has no direct C# counterpart — the .NET
      idiom is a `public static X Create(...)` method on the same
      class. Private-named-ctor nuance: Dart `X._({ ... })` is a
      library-private named constructor; C# `private X(...)` is
      class-private (strictly tighter but faithful because only
      the in-class `Create` factory invokes it). Field-init nuance:
      Dart `final List<Term> userOutputTerms = []` maps to C# `=
      new List<Term>()` for the backing field; the public surface
      `IReadOnlyList<Term>` records the read-only-view invariant
      (carry-forward from boot_loader.dart.md and terms.dart.md
      conventions). Mutability nuance for closed-flags: Dart
      `bool userOutputClosed = false` is NON-final (callers /
      callbacks set it via `context.userOutputClosed = true`); C#
      `public bool UserOutputClosed { get; set; } = false;` —
      `{ get; set; }` (NOT `init`) because the callback re-assigns
      after construction.
conversion_units:
  - "class ExternalChannel (reference type, NOT record, NOT struct)"
  - "  property: string Name { get; }"
  - "  property: long InputWriterAddr { get; }"
  - "  property: long InputReaderAddr { get; }"
  - "  property: long OutputWriterAddr { get; }"
  - "  property: long OutputReaderAddr { get; }"
  - "  ctor: ExternalChannel(string name, long inputWriterAddr, long inputReaderAddr, long outputWriterAddr, long outputReaderAddr) — all required, named-style preserved at call sites via C# named arguments"
  - "  override ToString() — expression-bodied interpolated string preserving Dart shape byte-identically"
  - "static class ExternalIO (hosting type for file-level functions)"
  - "  public static ExternalChannel CreateExternalChannel(HeapFCP heap, string name) — uses var (a, b) = heap.AllocateVariable() tuple deconstruction twice; new ExternalChannel(name: ..., inputWriterAddr: ..., ...) with C# named arguments"
  - "  public static Term BuildChannelTerm(ExternalChannel channel) — returns new StructTerm(\"ch\", new List<Term> { new VarRef(channel.InputReaderAddr), new VarRef(channel.OutputWriterAddr) })"
  - "class InputInjector (reference type)"
  - "  property: HeapFCP Heap { get; }"
  - "  property: string ChannelName { get; }"
  - "  field: private long _currentWriterId"
  - "  property: long CurrentWriterId => _currentWriterId  // expression-bodied getter"
  - "  ctor: InputInjector(HeapFCP heap, string channelName, long initialWriterId)"
  - "  public IReadOnlyList<GoalRef> Inject(Term term) — var (tailWriterAddr, _) = Heap.AllocateVariable() with discard `_`; constructs StructTerm(\".\", new List<Term> { term, new VarRef(tailWriterAddr) }); calls Heap.BindVariable; reassigns _currentWriterId; returns activations"
  - "  public IReadOnlyList<GoalRef> Close() — return Heap.BindVariable(_currentWriterId, new ConstTerm(\"nil\"))"
  - "class OutputObserver (reference type; no IDisposable conformance — intentionally absent)"
  - "  property: HeapFCP Heap { get; }"
  - "  property: string ChannelName { get; }"
  - "  property: Action<Term> OnTerm { get; }   // Dart non-nullable callback → C# Action<Term> (no ?)"
  - "  property: Action OnClose { get; }        // Dart non-nullable callback → C# Action (no ?)"
  - "  field: private long _currentReaderId"
  - "  field: private bool _closed = false"
  - "  property: long CurrentReaderId => _currentReaderId"
  - "  property: bool IsClosed => _closed"
  - "  ctor: OutputObserver(HeapFCP heap, string channelName, long initialReaderId, Action<Term> onTerm, Action onClose) — body assigns then calls _ObserveNext()"
  - "  private void _ObserveNext() — early return on _closed; Heap.TryWriterForReader(_currentReaderId) → long? writerAddr; if null return; Heap.OnBind(writerAddr.Value, (Term value) => { ... type-pattern dispatch ... }); inner branches use type-pattern capture `value is StructTerm st && st.Functor == \".\"` / `tail is VarRef vr` / `tail is ConstTerm ct && ct.Value is \"nil\"` / `tail is StructTerm sn && sn.Functor == \".\"`"
  - "  private void _ProcessNestedCons(StructTerm cons) — while(true) loop with break statements, identical control flow"
  - "  public void Dispose() — sets _closed = true; null-conditional Heap.TryWriterForReader(_currentReaderId) result → if not-null, Heap.RemoveBindCallback(writerAddr.Value)"
  - "class AgentIOContext (reference type)"
  - "  property: string AgentId { get; }"
  - "  property: HeapFCP Heap { get; }"
  - "  property: ExternalChannel UserChannel { get; }"
  - "  property: InputInjector UserInput { get; }"
  - "  property: OutputObserver UserOutput { get; private set; } = null!   // late final analogue"
  - "  property: ExternalChannel NetChannel { get; }"
  - "  property: InputInjector NetInput { get; }"
  - "  property: OutputObserver NetOutput { get; private set; } = null!   // late final analogue"
  - "  field: private readonly List<Term> _userOutputTerms = new List<Term>()"
  - "  field: private readonly List<Term> _netOutputTerms = new List<Term>()"
  - "  property: IReadOnlyList<Term> UserOutputTerms => _userOutputTerms"
  - "  property: IReadOnlyList<Term> NetOutputTerms => _netOutputTerms"
  - "  property: bool UserOutputClosed { get; set; } = false"
  - "  property: bool NetOutputClosed { get; set; } = false"
  - "  private ctor: AgentIOContext(string agentId, HeapFCP heap, ExternalChannel userChannel, InputInjector userInput, ExternalChannel netChannel, InputInjector netInput) — corresponds to Dart `._({ required ... })` private named ctor"
  - "  public static AgentIOContext Create(HeapFCP heap, string agentId) — creates channels via ExternalIO.CreateExternalChannel; constructs both InputInjectors; calls the private ctor; assigns context.UserOutput / context.NetOutput via the private setters (the `late final` write); returns context"
  - "  computed property: Term UserChannelTerm => ExternalIO.BuildChannelTerm(UserChannel)"
  - "  computed property: Term NetChannelTerm => ExternalIO.BuildChannelTerm(NetChannel)"
  - "  public void Dispose() — UserOutput.Dispose(); NetOutput.Dispose();"
  - "  override ToString() — expression-bodied `$\"AgentIOContext({AgentId})\"`"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-library-directive-to-csharp-namespace-elision — `library;` directive (cached idiom, reuse)

- Deep analysis: file-level `library;` directive (no library name) following 5 doc-comment lines that cite docs/glp-io-spec.md and three architecture spec sections. The directive carries no semantics in this file beyond marking the compilation unit.
- Authoritative Dart (cached): WebFetch `https://dart.dev/language/libraries` — `library` directive names the compilation unit; an unnamed `library;` is the default and is elidable in null-safe code (the directive itself is optional).
- Authoritative .NET (cached): Microsoft Learn `https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/namespaces` — .NET groups compilation by namespace, not by Dart-library. The Dart `library;` directive has no .NET counterpart.
- Conclusion: elide the `library;` directive; preserve the file-level doc-comment block as a C# XML-doc comment on the file or namespace. FR-024 cache hit (carry-forward from heap_fcp.dart.md / suspension.dart.md / variable_table.dart.md); no new research.

### rf-dart-import-relative-to-csharp-using-namespace — relative imports → using directives (cached idiom, reuse)

- Deep analysis: three relative imports (`terms.dart`, `heap_fcp.dart`, `machine_state.dart`) — all three target sibling files in `lib/runtime/` and use bare-import surface (no `show` / `hide` clauses).
- Authoritative Dart (cached): WebFetch `https://dart.dev/language/libraries#using-libraries` — `import 'relative.dart';` resolves by file path; the imported library's full public surface is available.
- Authoritative .NET (cached): Microsoft Learn `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive` — `using <Namespace>;` makes the namespace's full public surface available; no per-symbol filtering.
- Conclusion: a single `using <root>.Runtime;` directive covers all three sibling-file imports (terms.cs, heap_fcp.cs, machine_state.cs all target the same `lib/runtime/` namespace). FR-024 cache hit; no new research.

### rf-dart-final-field-class-to-csharp-getonly-class — ExternalChannel immutable data class (cached idiom, reuse)

- Deep analysis: `ExternalChannel` has five `final` fields (one `String`, four `int`) and a single named-required constructor; instances are aliased across the InputInjector / OutputObserver / buildChannelTerm pipeline; no `==` / `hashCode` override; explicit `toString()` override for diagnostic logs.
- Authoritative Dart (cached): WebFetch `https://dart.dev/language/class-modifiers` and `https://dart.dev/language/constructors` — `final` instance fields are write-once; named-required parameters are compile-site obligations.
- Authoritative .NET (cached): Microsoft Learn `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/auto-implemented-properties` — get-only auto-properties are write-once via constructor.
- Conclusion: reference `class ExternalChannel` with five get-only properties initialised from a single constructor. Reject `record` (would synthesise structural equality the Dart source lacks; would synthesise a different `ToString`). Reject `struct` (instance escapes via factory return and is aliased — boxing regression). Carry-forward of boot_loader.dart.md's `SpawnDirective` idiom. Authoritative both sides; no escalation.

### rf-dart-tostring-interp-to-csharp-tostring-interp — ExternalChannel diagnostic toString (cached idiom, reuse)

- Deep analysis: single-expression arrow body `toString()` interpolating five non-nullable fields into a fixed diagnostic shape `ExternalChannel(<name>, in=(<i_w>,<i_r>), out=(<o_w>,<o_r>))`. No null branch.
- Authoritative Dart (cached): dart.dev — `toString()` is a virtual `Object` method; `$id` interpolation calls `id.toString()`.
- Authoritative .NET (cached): Microsoft Learn `Object.ToString` — virtual, overridable; `$"{X}"` calls `X.ToString()` in invariant default culture.
- Conclusion: override `object.ToString()` with an expression-bodied interpolated string. Punctuation preserved byte-identically (load-bearing for diagnostic logs and any equality assertions in tests). No extension-method alternative (extensions cannot override virtuals). FR-024 cache hit; no new research.

### rf-dart-record-destructure-to-csharp-valuetuple-deconstruction — tuple-return destructuring (NEW finding)

- Deep analysis: two call sites use Dart 3.0+ record-destructuring syntax: `final (inputWriterAddr, inputReaderAddr) = heap.allocateVariable();` and `final (tailWriterAddr, _) = heap.allocateVariable();` — the second uses the wildcard `_` discard. `heap.allocateVariable()` returns a Dart record `(int writerAddr, int readerAddr)` per heap_fcp.dart.md.
- Authoritative Dart: WebFetch `https://dart.dev/language/records` — "Records are an anonymous, immutable, aggregate type. … You can use a pattern to destructure a record value, binding the fields to new variables." Wildcards: "Use the `_` (underscore) in patterns to match without binding." The `final (a, b) = expr;` form is a record-pattern variable declaration.
- Authoritative .NET: WebFetch `https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/deconstruct` — "You deconstruct a tuple by declaring … fields of the tuple to a set of variables." Tuple deconstruction syntax: `var (a, b) = expr;`. WebFetch `https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/discards` — "Discards … local variables that you can use intentionally to ignore certain values in your code. Discards are equivalent to unassigned variables; they don't have a value." Form: `var (a, _) = expr;`.
- Conclusion: Dart record `(int, int)` ↔ .NET `ValueTuple<long, long>` (`(long, long)`). Destructuring `final (a, b) = …` ↔ `var (a, b) = …`. Discard `_` ↔ C# `_` (identical syntax, identical semantics). The .NET surface API for `HeapFCP.AllocateVariable()` returns `(long, long)` per the corresponding heap_fcp.cs spec. Authoritative both sides; no escalation. NEW idiom registered (no carry-forward; this is the first file in the convspec corpus to exercise record-destructuring at multiple sites with wildcard).

### rf-dart-top-level-fn-builds-sum-type-leaf — file-level factory function (NEW finding, hosted on static class)

- Deep analysis: `createExternalChannel` and `buildChannelTerm` are FILE-LEVEL (top-level, non-class) Dart functions. Dart permits this; C# requires every method to belong to a type.
- Authoritative Dart: WebFetch `https://dart.dev/language/functions` — "You can use functions like top-level functions, class members, … or anonymous functions. … A Dart program can have many top-level functions."
- Authoritative .NET: Microsoft Learn `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/static-classes-and-static-class-members` — "A static class is basically the same as a non-static class, but … A static class can be used as a convenient container for sets of methods that just operate on input parameters and don't have to get or set any internal instance fields." The recommended C# idiom for grouping file-level helpers is a `public static class` (e.g. `System.Math`, `System.IO.Path`).
- Conclusion: emit `public static class ExternalIO` in `lib/runtime/` and host `CreateExternalChannel` and `BuildChannelTerm` as `public static` methods. Callers reference `ExternalIO.CreateExternalChannel(...)`. Authoritative both sides; no escalation. NEW idiom registered (first time a top-level Dart function pair has been spec'd in this file — the previous corpus spec'd top-level functions on `BootLoader` instances, not as static-class helpers).

### rf-dart-mutable-int-field-callback-list-return — InputInjector (NEW finding)

- Deep analysis: InputInjector has two `final` references (heap, channelName), one mutable `int _currentWriterId`, and two methods returning `List<GoalRef>` (the heap layer's bind-callback activation list). The mutable field is reassigned every `inject()` call to track the stream's tail. The `inject()` method exercises tuple destructuring with a discard for the unused reader address of the freshly-allocated tail variable.
- Authoritative Dart: WebFetch `https://dart.dev/language/variables#default-value` — non-`final` instance fields are read-write. WebFetch `https://dart.dev/language/methods` — getter `int get currentWriterId => _currentWriterId;` is a synthesised property.
- Authoritative .NET: Microsoft Learn `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/fields` — instance fields default to read-write; `readonly` modifier required for write-once. Expression-bodied properties (`public T X => _x;`) are the C# 6+ counterpart of Dart's synthesised-getter shape.
- Conclusion: emit `private long _currentWriterId;` (mutable backing field) plus `public long CurrentWriterId => _currentWriterId;` (expression-bodied get-only accessor). Return shape `IReadOnlyList<GoalRef>` matches the boot_loader.dart.md convention (record the immutability invariant the caller relies on). Authoritative both sides; no escalation.

### rf-dart-is-test-narrowing-to-csharp-type-pattern-capture — sum-type type-narrowing (NEW finding, LOAD-BEARING)

- Deep analysis: OutputObserver dispatches on the sum-type `Term` hierarchy (the cons-cell shape `[Head | Tail]` is encoded as `StructTerm('.', [head, tail])`, end-of-list as `ConstTerm('nil')`, and a tail-continuation as `VarRef(addr)`). Six type-narrowing branches in `_observeNext` + `_processNestedCons`: `value is StructTerm && value.functor == '.'`, `tail is VarRef`, `tail is ConstTerm && tail.value == 'nil'`, `tail is StructTerm && tail.functor == '.'`, `value is ConstTerm && value.value == 'nil'`, and the same patterns inside the nested-cons while-loop. Each branch accesses fields specific to the narrowed type (`.functor`, `.args`, `.addr`, `.value`).
- Authoritative Dart: WebFetch `https://dart.dev/null-safety/understanding-null-safety#type-promotion-on-null-checks` and `https://dart.dev/language/branches#type-promotion` — "When you check the type of a variable using the `is` operator, the type of the variable is promoted to the more specific type in the branch where the check is true." So `if (value is StructTerm) { value.functor }` is well-typed in Dart because `value`'s static type narrows to `StructTerm` inside the branch.
- Authoritative .NET: WebFetch `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns#declaration-and-type-patterns` — "Beginning with C# 7.0, the `is` operator also tests an expression against a pattern. … A declaration pattern with type T matches an expression when an expression result is non-null and any of the following conditions are true: … The runtime type of an expression result is T. … If a match succeeds, the corresponding variable is assigned the converted expression result." The pattern-variable form `if (value is StructTerm st)` introduces `st` of type `StructTerm` bound to the converted value. Without the capture (`if (value is StructTerm)`), the original `value` is NOT narrowed — accessing `value.Functor` in the body would NOT compile.
- Conclusion: every Dart `is`-test in this file MUST be rewritten with a C# pattern-variable capture: `if (value is StructTerm st && st.Functor == ".")`, `if (tail is VarRef vr)`, `if (tail is ConstTerm ct && ct.Value is "nil")` (constant pattern for value-equality), `if (tail is StructTerm sn && sn.Functor == ".")`, etc. Authoritative both sides; no escalation. NEW idiom registered (first OutputObserver-style sum-type dispatch in the convspec corpus).

### rf-dart-late-final-to-csharp-getprivateset-with-null-forgiving — `late final` deferred initialisation (NEW finding, LOAD-BEARING)

- Deep analysis: AgentIOContext has TWO `late final OutputObserver` fields (`userOutput`, `netOutput`) that are NOT initialised by the private named constructor `AgentIOContext._(...)` — they are assigned exclusively in the `AgentIOContext.create` factory body AFTER the private constructor returns: `final context = AgentIOContext._( ... ); context.userOutput = OutputObserver( ... ); context.netOutput = OutputObserver( ... );`. The OutputObserver constructor closes over `context.userOutputTerms` / `context.netOutputTerms` via the callback `(term) => context.userOutputTerms.add(term)`, so the OutputObserver cannot be constructed BEFORE the `context` instance exists — hence the `late final` deferred-init pattern.
- Authoritative Dart: WebFetch `https://dart.dev/null-safety/understanding-null-safety#late-variables` — "The `late` keyword lets you declare a non-nullable variable that is initialized after its declaration. … If you mark a variable `late final`, it can only be set once. Setting the variable a second time throws a runtime error." So `late final` = non-nullable + write-exactly-once + deferred-write-allowed.
- Authoritative .NET: WebFetch `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/readonly` — "A `readonly` field can be assigned multiple times, but only as part of the declaration or in a constructor in the same class." So `readonly` does NOT allow assignment from a `static` factory method on the same class — the factory writes after the constructor returns, which `readonly` forbids. WebFetch `https://learn.microsoft.com/en-us/dotnet/csharp/properties#init-only-setters` — "Init-only setters provide consistent syntax to initialize members of an object. … An init-only setter introduces an init accessor that's a variant of the set accessor. … This new form of accessor is only callable in the following circumstances: In an object initializer. In a with expression. Inside an instance constructor." So `init` does NOT permit assignment from a `static` factory method either. WebFetch `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/null-forgiving` — "The unary postfix `!` operator is the null-forgiving operator … typical uses … assigning to a non-nullable field that's initialized later: `class Person { string _name = null!; }`." This is the documented C# pattern for `late`-style fields.
- Conclusion: the FAITHFUL render is `public OutputObserver UserOutput { get; private set; } = null!;` — a `{ get; private set; }` property defaulted to `null!` (suppressing the NRT initialisation warning) and written exactly once by the `Create` factory before the instance escapes. Code-review (not compile-time) enforces the exactly-once invariant. The three rejected alternatives (`readonly`, `init`, `Lazy<T>`) each fail for documented reasons. Authoritative both sides; no escalation. NEW idiom registered (first `late final` site in the convspec corpus).

## Notes

- This file does NOT exercise the well-known dart:io → System.IO / System.Console nuance. Asserting an absent nuance would be noise (same discipline as boot_loader.dart.md): "External I/O" in the file name refers to "external to the GLP runtime" (Dart-side observers/injectors over heap variable cells), not "external to the Dart process". No `stdin` / `stdout` / `File` / `Platform` / `Process` / `Directory` / `Encoding` references appear.
- This file does NOT exercise `async` / `await` / `Future` / `Stream` / `Isolate` / `Completer`. The "observer" pattern here is SYNCHRONOUS callback registration (`heap.onBind(addr, lambda)`), not a `StreamSubscription` or async-iterator surface. The well-known `Stream` → `IAsyncEnumerable` nuance is correctly NOT asserted here because the CODE does not exercise it.
- Load-bearing semantic decisions for THIS file: (a) Dart `is`-test type narrowing → C# type-pattern WITH explicit capture variable (`value is StructTerm st`) — without the capture, C# does not narrow the original variable's static type and the dispatch will not compile; (b) Dart `late final` → C# `{ get; private set; } = null!;` with code-review discipline — `readonly`, `init`, and `Lazy<T>` all fail; (c) Dart record-destructuring `final (a, b) = …` → C# tuple-deconstruction `var (a, b) = …`; the discard `_` is identical in both; (d) Dart top-level functions → C# static methods on a hosting `public static class ExternalIO`; (e) callback nullability differential vs repl_play_runner: these callbacks (`OutputObserver.onTerm` / `onClose`) are Dart-NON-nullable, mapping to `Action<Term>` / `Action` (no `?`) with direct invocation `OnTerm(head)` — explicitly NOT the `OnOutput?.Invoke(x)` shape that applies to repl_play_runner's nullable callbacks.
- Trivial / non-construct elements: triple-slash doc comments (`///`) map mechanically to C# XML-doc comments (`///`); `@override` annotations are subsumed by the C# `override` keyword on each overriding member; `var` for locals maps to C# `var` (same type-inference role); `for`-loops and `while (true) { ... break; }` shapes are control-flow-identical across languages.
- Zero escalations: every non-trivial construct resolved from authoritative Dart (dart.dev / api.dart.dev) and/or .NET (learn.microsoft.com) official documentation. Three carry-forward idioms reused verbatim (library-elision, import→using, final-field-class) and four NEW idioms registered (record-destructuring → tuple-deconstruction; top-level-fn → static-class; is-test → type-pattern-capture; late-final → get/private-set/null-forgiving). FR-009/FR-010 quality bar satisfied: every non-trivial construct has BOTH a deep-analysis basis AND a researched-pattern basis (or an explicit carry-forward idiom_id surrogate via the named research_finding_id).
