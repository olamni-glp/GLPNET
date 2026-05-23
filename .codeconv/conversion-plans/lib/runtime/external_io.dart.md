---
path: lib/runtime/external_io.dart
cycle_group_id: 64
scc_siblings: []
generated_at: 2026-05-21T15:25:39Z
source_sha256: 7295d8789cac898386fecbab28013d922a922c8fe718a59c8c19c9fc979a4c14
schema_version: 1
---

# Conversion Plan: lib/runtime/external_io.dart

## 1. Source Analysis

Inspected `glp_runtime_net/lib/runtime/external_io.dart` (332 lines, sha256 `7295d8789c...4c14`). The file is the Dart→GLP heap-variable bridge layer (Phase 0 of the external-I/O spec, per the leading doc-comment citation of `docs/glp-io-spec.md`). Despite the "I/O" name, the file uses **no** `dart:io` API surface — no `stdin` / `stdout` / `File` / `Platform` / `Process` / `Directory` / `Encoding` references appear. "External" here means "external to the GLP runtime" (Dart-side observers/injectors over heap variable cells), not "external to the Dart process".

Top-of-file directives:

- `library;` — unnamed library directive (line 5), no semantic load beyond marking the compilation unit.
- Three relative imports (lines 7–9): `terms.dart` (Term, VarRef, ConstTerm, StructTerm), `heap_fcp.dart` (HeapFCP with `allocateVariable`, `bindVariable`, `tryWriterForReader`, `onBind`, `removeBindCallback`), `machine_state.dart` (GoalRef — verbatim preserved doc comment "For GoalRef").

Top-level declarations (in source order):

1. **`class ExternalChannel`** (lines 22–45) — five `final` fields (`String name`, four `int` for input/output writer/reader addresses), single named-required constructor, `@override toString()` with a fixed diagnostic interpolation shape `ExternalChannel(<name>, in=(<i_w>,<i_r>), out=(<o_w>,<o_r>))`. No `==` / `hashCode` overrides.

2. **`ExternalChannel createExternalChannel(HeapFCP heap, String name)`** (lines 48–63) — top-level factory function. Uses Dart 3.0+ record-destructuring twice: `final (inputWriterAddr, inputReaderAddr) = heap.allocateVariable();` and likewise for the output pair. Constructs the result via named arguments.

3. **`Term buildChannelTerm(ExternalChannel channel)`** (lines 80–88) — top-level function returning `StructTerm('ch', [VarRef(channel.inputReaderAddr), VarRef(channel.outputWriterAddr)])`. Note the deliberate (inputReader, outputWriter) ordering per the doc comment citing CGLP paper Definition 5.5 and the bytecode spec sections 8.1–8.2.

4. **`class InputInjector`** (lines 94–133) — two `final` references (heap, channelName), one mutable `int _currentWriterId`, get-only accessor `currentWriterId`, and two methods returning `List<GoalRef>`:
   - `inject(Term term)` — allocates a fresh tail variable via record-destructuring with discard `final (tailWriterAddr, _) = heap.allocateVariable();`, builds a `StructTerm('.', [term, VarRef(tailWriterAddr)])` cons cell, binds it to the current writer (capturing the activation list), and advances `_currentWriterId` to the tail.
   - `close()` — binds the current writer to `ConstTerm('nil')` (empty-list sentinel).

5. **`class OutputObserver`** (lines 139–242) — three `final` references (heap, channelName, two callbacks `void Function(Term) onTerm` and `void Function() onClose`, both NON-nullable), two mutable fields (`int _currentReaderId`, `bool _closed = false`), get-only accessors `currentReaderId` / `isClosed`, constructor that initialises `_currentReaderId` and then calls `_observeNext()`. The `_observeNext()` body and `_processNestedCons()` body dispatch on the sum-type `Term` hierarchy via Dart `is`-tests with type-promotion: `value is StructTerm && value.functor == '.'` (cons cell), `tail is VarRef` (continuation), `tail is ConstTerm && tail.value == 'nil'` (end-of-list sentinel), `tail is StructTerm && tail.functor == '.'` (nested cons). Six total `is`-narrowing sites. `dispose()` sets `_closed = true` and removes the bind callback.

6. **`class AgentIOContext`** (lines 247–331) — top-level orchestrator. Six `final` fields (agentId, heap, userChannel, userInput, netChannel, netInput), TWO `late final` fields (`userOutput`, `netOutput` — LOAD-BEARING deferred-init pattern), two `final List<Term>` collected-terms lists initialised inline to `[]`, two `bool` mutable flags. Private named constructor `AgentIOContext._({ required ... })` (Dart library-private factory-only convention). Factory constructor `factory AgentIOContext.create(HeapFCP heap, String agentId)` that constructs the two channels, the two InputInjectors, calls the private constructor, then assigns `context.userOutput` and `context.netOutput` via OutputObserver constructions whose callbacks close over `context.userOutputTerms.add(...)` / `context.userOutputClosed = true` — this circular dependency is what FORCES the `late final` pattern (the OutputObserver cannot be built before `context` exists). Two computed getters `userChannelTerm` / `netChannelTerm` delegate to `buildChannelTerm`. `dispose()` chains to the two observers. `@override toString() => 'AgentIOContext($agentId)'`.

No `async` / `await` / `Future` / `Stream` / `Isolate` / `Completer` constructs. No `dart:io`. The observer pattern is SYNCHRONOUS callback registration (`heap.onBind(addr, lambda)`), not a `StreamSubscription` or async-iterator surface.

## 2. Dart → C#/.NET Conversion Plan

The conversion mirrors the ratified convspec at `.codeconv/conversion-specs/lib/runtime/external_io.dart.md` (10 constructs, 0 escalations). Each construct → C#/.NET as follows:

### C1. `library;` directive → namespace elision

- **Source**: `library;` at line 5 (no library name).
- **Target**: ELIDE the directive. Preserve the leading file-level doc comments ("External I/O for GLP - Phase 0 Implementation", `docs/glp-io-spec.md` citation) as a C# XML-doc comment block at the top of the namespace declaration mirroring `lib/runtime/`.
- **Idiom**: `rf-dart-library-directive-to-csharp-namespace-elision` (cached carry-forward from heap_fcp.dart.md / suspension.dart.md / variable_table.dart.md).

### C2. Relative imports → `using` namespace

- **Source**: `import 'terms.dart';` / `import 'heap_fcp.dart';` / `import 'machine_state.dart';`.
- **Target**: A single `using <root>.Runtime;` covers all three sibling files (terms.cs, heap_fcp.cs, machine_state.cs all target the `lib/runtime/` namespace). No `show`-style narrowing arises (the Dart imports are bare).
- **Idiom**: `rf-dart-import-relative-to-csharp-using-namespace` (cached carry-forward).

### C3. `class ExternalChannel` → reference class with get-only properties

- **Source**: Five `final` fields + named-required constructor + `toString()` override.
- **Target**: Reference `class ExternalChannel` (NOT `record`, NOT `struct`) with:
  - `public string Name { get; }`
  - `public long InputWriterAddr { get; }`
  - `public long InputReaderAddr { get; }`
  - `public long OutputWriterAddr { get; }`
  - `public long OutputReaderAddr { get; }`
  - Single constructor `public ExternalChannel(string name, long inputWriterAddr, long inputReaderAddr, long outputWriterAddr, long outputReaderAddr)` with no defaults (faithful to Dart `required`).
  - `public override string ToString() => $"ExternalChannel({Name}, in=({InputWriterAddr},{InputReaderAddr}), out=({OutputWriterAddr},{OutputReaderAddr}))";` — punctuation byte-identical.
- **Rejected alternatives**: `record` (would synthesise structural equality the Dart source lacks; would synthesise a different `ToString`). `struct` (instances escape via factory return and are aliased across InputInjector + OutputObserver — boxing regression).
- **Int-width**: Dart `int` → C# `long` per terms.dart.md width-fidelity policy.
- **Idioms**: `rf-dart-final-field-class-to-csharp-getonly-class`, `rf-dart-tostring-interp-to-csharp-tostring-interp` (both cached carry-forwards).

### C4. `createExternalChannel` → static method with tuple-deconstruction

- **Source**: Top-level function using two record-destructurings (`final (inputWriterAddr, inputReaderAddr) = heap.allocateVariable();`) and named-argument construction.
- **Target**: Host on a `public static class ExternalIO` in `lib/runtime/` namespace. Method signature `public static ExternalChannel CreateExternalChannel(HeapFCP heap, string name)`. Body uses C# 7+ tuple deconstruction `var (inputWriterAddr, inputReaderAddr) = heap.AllocateVariable();` (the .NET surface returns `(long, long)` per heap_fcp.cs). Construction `new ExternalChannel(name: name, inputWriterAddr: inputWriterAddr, ...)` uses C# named arguments (identical syntax to Dart's at the call site).
- **Idiom**: `rf-dart-record-destructure-to-csharp-valuetuple-deconstruction` (NEW idiom registered in convspec; first multi-site record-destructuring in corpus).

### C5. `buildChannelTerm` → static method building sum-type leaf

- **Source**: Top-level function returning `StructTerm('ch', [VarRef(channel.inputReaderAddr), VarRef(channel.outputWriterAddr)])`.
- **Target**: `public static Term BuildChannelTerm(ExternalChannel channel)` on the `ExternalIO` static class. Body: `return new StructTerm("ch", new List<Term> { new VarRef(channel.InputReaderAddr), new VarRef(channel.OutputWriterAddr) });`. The `List<Term>` reference is shared with the `StructTerm` (no defensive copy — mirrors Dart's `this.args = args` reference-sharing per terms.dart.md).
- **Idiom**: `rf-dart-top-level-fn-builds-sum-type-leaf` (NEW idiom registered).

### C6. `class InputInjector` → reference class with mutable backing field

- **Source**: Two `final` references, one mutable `int _currentWriterId`, get-only accessor, two methods returning `List<GoalRef>`.
- **Target**: Reference `class InputInjector` with:
  - `public HeapFCP Heap { get; }` (get-only)
  - `public string ChannelName { get; }` (get-only)
  - `private long _currentWriterId;` (mutable backing field — Dart non-final integer)
  - `public long CurrentWriterId => _currentWriterId;` (expression-bodied get-only)
  - Constructor `public InputInjector(HeapFCP heap, string channelName, long initialWriterId)` with body assignments (Dart initialiser-list `: _currentWriterId = initialWriterId` collapses to constructor body — no base-class field-init ordering hazard).
  - `public IReadOnlyList<GoalRef> Inject(Term term)` — body uses C# tuple deconstruction with discard: `var (tailWriterAddr, _) = Heap.AllocateVariable();`. Constructs `new StructTerm(".", new List<Term> { term, new VarRef(tailWriterAddr) });`. Calls `Heap.BindVariable(_currentWriterId, listCell)` to obtain activations. Reassigns `_currentWriterId = tailWriterAddr;`. Returns activations.
  - `public IReadOnlyList<GoalRef> Close() => Heap.BindVariable(_currentWriterId, new ConstTerm("nil"));`
- **Return-shape nuance**: Dart `List<GoalRef>` returned from `heap.bindVariable` IS mutable, but callers treat it as read-only (immediately iterated). The C# surface exposes `IReadOnlyList<GoalRef>` to record the immutability invariant (boot_loader.dart.md convention).
- **Idiom**: `rf-dart-mutable-int-field-callback-list-return` (NEW idiom registered).

### C7. `class OutputObserver` → reference class with type-pattern-capture dispatch

- **Source**: Three `final` references (heap, channelName, two NON-nullable callbacks), two mutable fields (`_currentReaderId`, `_closed`), constructor that initialises then calls `_observeNext()`, `_observeNext()` + `_processNestedCons()` dispatch on Term sum-type via `is`-narrowing, `dispose()` cleans up.
- **Target**: Reference `class OutputObserver` (NO `: IDisposable` — Dart source declares no disposal interface; cleanup is caller-driven via AgentIOContext) with:
  - `public HeapFCP Heap { get; }`
  - `public string ChannelName { get; }`
  - `public Action<Term> OnTerm { get; }` (Dart `void Function(Term)` non-nullable → C# `Action<Term>` no `?`)
  - `public Action OnClose { get; }` (Dart `void Function()` non-nullable → C# `Action` no `?`)
  - `private long _currentReaderId;`
  - `private bool _closed = false;`
  - `public long CurrentReaderId => _currentReaderId;`
  - `public bool IsClosed => _closed;`
  - Constructor `public OutputObserver(HeapFCP heap, string channelName, long initialReaderId, Action<Term> onTerm, Action onClose)` — body assigns all five inputs then calls `_ObserveNext()` (Dart initialiser-list + body collapses to single body in C#).
  - `private void _ObserveNext()` — body: early return on `_closed`; `long? writerAddr = Heap.TryWriterForReader(_currentReaderId);`; if null, return; `Heap.OnBind(writerAddr.Value, (Term value) => { ... });`. Inside the lambda, type-pattern dispatch:
    - `if (value is StructTerm st && st.Functor == ".") { var head = st.Args[0]; var tail = st.Args[1]; OnTerm(head); if (tail is VarRef vr) { _currentReaderId = vr.Addr; _ObserveNext(); } else if (tail is ConstTerm ct && ct.Value is "nil") { _closed = true; OnClose(); } else if (tail is StructTerm sn && sn.Functor == ".") { _ProcessNestedCons(sn); } }`
    - `else if (value is ConstTerm ct2 && ct2.Value is "nil") { _closed = true; OnClose(); }`
  - `private void _ProcessNestedCons(StructTerm cons)` — `while (true) { var head = current.Args[0]; var tail = current.Args[1]; OnTerm(head); if (tail is VarRef vr) { _currentReaderId = vr.Addr; _ObserveNext(); break; } else if (tail is ConstTerm ct && ct.Value is "nil") { _closed = true; OnClose(); break; } else if (tail is StructTerm sn && sn.Functor == ".") { current = sn; } else { break; } }`. The loop variable `current` reassigns; declare as `var current = cons;` then update inside the branch.
  - `public void Dispose()` — sets `_closed = true;`; `long? writerAddr = Heap.TryWriterForReader(_currentReaderId);`; if non-null, `Heap.RemoveBindCallback(writerAddr.Value);`.
- **Callback nullability differential**: explicitly differs from repl_play_runner.dart.md — those callbacks are Dart-nullable (`void Function(...)?`), these are NON-nullable. Direct invocation `OnTerm(head)` / `OnClose()`, NOT `OnTerm?.Invoke(head)`.
- **`is`-test type-narrowing**: every Dart `is`-test in this file MUST use a C# pattern-capture variable (`is StructTerm st`, `is VarRef vr`, `is ConstTerm ct`) because C# `is` WITHOUT a capture does NOT narrow the original variable's static type — the dispatch would not compile.
- **`ct.Value is "nil"` constant-pattern**: Dart `value.value == 'nil'` compares the `object?` field to a string literal with Dart's value-equality `==`. The faithful C# render uses the constant pattern `ct.Value is "nil"` (Microsoft Learn "Constant pattern": "if the expression is equal to the constant") — preserves value-equality semantics. (An alternative `string.Equals(ct.Value, "nil")` would also work but the constant pattern is more idiomatic and matches the convspec.)
- **Idiom**: `rf-dart-is-test-narrowing-to-csharp-type-pattern-capture` (NEW idiom registered, LOAD-BEARING).

### C8. `class AgentIOContext` → reference class with `late final` analogue

- **Source**: Six `final` fields, two `late final` fields, two `final List<Term>` collected-terms lists (inline-initialised), two `bool` mutable flags, private named constructor `_({ required ... })`, factory constructor `.create(...)`, two computed getters, `dispose()`, `toString()`.
- **Target**: Reference `class AgentIOContext` with:
  - `public string AgentId { get; }`
  - `public HeapFCP Heap { get; }`
  - `public ExternalChannel UserChannel { get; }`
  - `public InputInjector UserInput { get; }`
  - `public OutputObserver UserOutput { get; private set; } = null!;` — `late final` analogue (LOAD-BEARING).
  - `public ExternalChannel NetChannel { get; }`
  - `public InputInjector NetInput { get; }`
  - `public OutputObserver NetOutput { get; private set; } = null!;` — `late final` analogue.
  - `private readonly List<Term> _userOutputTerms = new List<Term>();`
  - `private readonly List<Term> _netOutputTerms = new List<Term>();`
  - `public IReadOnlyList<Term> UserOutputTerms => _userOutputTerms;`
  - `public IReadOnlyList<Term> NetOutputTerms => _netOutputTerms;`
  - `public bool UserOutputClosed { get; set; } = false;` (`{ get; set; }` NOT `init` — callbacks set after construction).
  - `public bool NetOutputClosed { get; set; } = false;`
  - Private constructor `private AgentIOContext(string agentId, HeapFCP heap, ExternalChannel userChannel, InputInjector userInput, ExternalChannel netChannel, InputInjector netInput)` (corresponds to Dart `._({ required ... })`).
  - `public static AgentIOContext Create(HeapFCP heap, string agentId)` — body:
    1. `var userChannel = ExternalIO.CreateExternalChannel(heap, "user");`
    2. `var netChannel = ExternalIO.CreateExternalChannel(heap, "net");`
    3. `var userInput = new InputInjector(heap, "user", userChannel.InputWriterAddr);`
    4. `var netInput = new InputInjector(heap, "net", netChannel.InputWriterAddr);`
    5. `var context = new AgentIOContext(agentId, heap, userChannel, userInput, netChannel, netInput);`
    6. `context.UserOutput = new OutputObserver(heap, "user", userChannel.OutputReaderAddr, (term) => context._userOutputTerms.Add(term), () => context.UserOutputClosed = true);`
    7. `context.NetOutput = new OutputObserver(heap, "net", netChannel.OutputReaderAddr, (term) => context._netOutputTerms.Add(term), () => context.NetOutputClosed = true);`
    8. `return context;`
  - `public Term UserChannelTerm => ExternalIO.BuildChannelTerm(UserChannel);`
  - `public Term NetChannelTerm => ExternalIO.BuildChannelTerm(NetChannel);`
  - `public void Dispose() { UserOutput.Dispose(); NetOutput.Dispose(); }`
  - `public override string ToString() => $"AgentIOContext({AgentId})";`
- **`late final` rationale**: the OutputObserver callbacks close over `context.userOutputTerms.add(...)` so the OutputObserver cannot be constructed before `context` exists. Rejected alternatives: `readonly` (forbids assignment from `static` factory after constructor returns), `init` (only allows assignment in object-initialiser/with/ctor, not from static factory body after the instance escapes), `Lazy<T>` (not lazy-on-first-access — it is eagerly assigned in the factory). Faithful render is `{ get; private set; } = null!;` with code-review discipline enforcing exactly-once assignment.
- **Private-named-ctor nuance**: Dart `._({ ... })` is library-private; C# `private` is class-private (strictly tighter but faithful — only the in-class `Create` factory invokes it).
- **Field-init nuance**: Dart `final List<Term> userOutputTerms = []` → C# `private readonly List<Term> _userOutputTerms = new List<Term>();` with `IReadOnlyList<Term>` public surface (boot_loader.dart.md / terms.dart.md convention).
- **Idiom**: `rf-dart-late-final-to-csharp-getprivateset-with-null-forgiving` (NEW idiom registered, LOAD-BEARING).

## 3. Decomposed Task Units

- **T1**: Emit file header (XML-doc preserving Dart leading triple-slash block) + namespace declaration + single `using <root>.Runtime;` directive. Done.
- **T2**: Emit `public class ExternalChannel` with five get-only properties, single constructor, and `ToString()` override. Done.
- **T3**: Emit `public static class ExternalIO` skeleton (host for top-level functions). Done.
- **T4**: Emit `public static ExternalChannel CreateExternalChannel(HeapFCP heap, string name)` using `var (a, b) = Heap.AllocateVariable()` deconstruction twice; named-argument construction. Done.
- **T5**: Emit `public static Term BuildChannelTerm(ExternalChannel channel)` returning `new StructTerm("ch", new List<Term> { new VarRef(channel.InputReaderAddr), new VarRef(channel.OutputWriterAddr) })`. Done.
- **T6**: Emit `public class InputInjector` with two get-only properties, private mutable `_currentWriterId`, expression-bodied `CurrentWriterId` accessor, constructor, `Inject(Term term)` with discard-deconstruction, `Close()`. Done.
- **T7**: Emit `public class OutputObserver` with four get-only properties (two refs + two non-nullable `Action`/`Action<Term>` callbacks), two private mutable fields, two expression-bodied accessors, constructor that calls `_ObserveNext()`, `_ObserveNext()` with type-pattern-capture dispatch lambda, `_ProcessNestedCons(StructTerm cons)` while-loop with type-pattern dispatch, `Dispose()`. Done.
- **T8**: Emit `public class AgentIOContext` with six get-only properties, two `{ get; private set; } = null!;` late-final analogues, two `private readonly List<Term>` fields with `IReadOnlyList<Term>` public surface, two `{ get; set; }` bool flags, private constructor mirroring Dart `._({ required ... })`. Done.
- **T9**: Emit `public static AgentIOContext Create(HeapFCP heap, string agentId)` factory body — constructs channels, injectors, instance, then assigns `context.UserOutput` and `context.NetOutput` via OutputObserver constructors whose callbacks close over `context._userOutputTerms.Add(...)` / `context.UserOutputClosed = true` (etc.). Returns `context`. Done.
- **T10**: Emit `UserChannelTerm` / `NetChannelTerm` computed properties + `Dispose()` chaining + `ToString()` override on `AgentIOContext`. Done.

## 4. Research Findings

None required. The ratified convspec at `.codeconv/conversion-specs/lib/runtime/external_io.dart.md` already records the full deep-analysis + authoritative-citation provenance for every non-trivial construct: three cached carry-forward idioms (`rf-dart-library-directive-to-csharp-namespace-elision`, `rf-dart-import-relative-to-csharp-using-namespace`, `rf-dart-final-field-class-to-csharp-getonly-class`, plus `rf-dart-tostring-interp-to-csharp-tostring-interp`) and four NEW idioms registered (`rf-dart-record-destructure-to-csharp-valuetuple-deconstruction`, `rf-dart-top-level-fn-builds-sum-type-leaf`, `rf-dart-mutable-int-field-callback-list-return`, `rf-dart-is-test-narrowing-to-csharp-type-pattern-capture`, `rf-dart-late-final-to-csharp-getprivateset-with-null-forgiving`). Each carries a deep-analysis basis AND a researched-pattern basis from authoritative Dart docs (dart.dev / api.dart.dev) and .NET docs (learn.microsoft.com). FR-009/FR-010 quality bar satisfied. Zero escalations in the convspec; this plan inherits that resolution verbatim.

## 5. Consistency Pass

Every construct decision in §2 is verbatim-derivable from the ratified convspec at `.codeconv/conversion-specs/lib/runtime/external_io.dart.md` (the `<construct_key, target_decision, nuance>` triples in the YAML block + the "Rationale and research provenance" prose section). Specifically:

- C1 (library elision) — derived from convspec construct `dart.library_directive.top_of_file_no_name`.
- C2 (using namespace) — derived from convspec construct `dart.import_directive.package_internal_to_using_namespace`.
- C3 (ExternalChannel class) — derived from convspec construct `dart.data_class.final_int_fields_named_required_ctor_tostring_override` + `dart.tostring_override.string_interpolation_no_branch_multifield`.
- C4 (CreateExternalChannel static method) — derived from convspec construct `dart.top_level_factory_function.constructs_class_via_named_args_from_two_tuple_returns`.
- C5 (BuildChannelTerm static method) — derived from convspec construct `dart.top_level_function.builds_struct_term_with_varref_args_for_cons_functor`.
- C6 (InputInjector class) — derived from convspec construct `dart.mutable_class.single_mutable_int_field_callback_returning_list`.
- C7 (OutputObserver class) — derived from convspec construct `dart.bind_callback_registration.heap_observer_with_is_test_sum_type_narrowing`.
- C8 (AgentIOContext class) — derived from convspec construct `dart.late_final_field_assigned_in_factory_with_private_named_ctor`.

Carry-forward integrity: the int-width policy (Dart `int` → C# `long`) is consistent with terms.dart.md / machine_state.dart.md / heap_fcp.dart.md. The `List<T>` → `IReadOnlyList<T>` public-surface convention is consistent with boot_loader.dart.md. The reference-class-not-record/struct decision is consistent with terms.dart.md's closed-sum-type rationale and boot_loader.dart.md's `SpawnDirective` idiom. The non-nullable-callback differential vs repl_play_runner.dart.md is explicitly documented in the convspec.

Fixed — derived from `.codeconv/conversion-specs/lib/runtime/external_io.dart.md` (sha-matched to the same source, schema_version 1, zero escalations).

## 6. Escalations

None.
