# Conversion Spec — lib/runtime/system_predicates_impl.dart

> Conversion-spec artifact for lib/runtime/system_predicates_impl.dart
> (FR-011). Spec-only (FR-023): describes the Dart→C# conversion;
> contains NO compilable C#. A later codegen stage consumes the
> structured block.
>
> **Large file (1927 lines).** The file is the concrete implementation
> of the registry-and-call-context machinery declared in
> `system_predicates.dart` (which is already specced — see
> `system_predicates.dart.md`). It consists of:
>
> 1. ONE top-level `void registerStandardPredicates(SystemPredicateRegistry registry)`
>    function — sixteen `registry.register(<name>, <fn>)` method calls
>    grouped by family (arithmetic, utilities, file I/O, directory,
>    terminal I/O, module loading, channel primitives). Pure dispatch
>    table; the only side effect is mutating the passed registry.
> 2. SIXTEEN free-standing predicate-implementation functions,
>    each conforming to the `SystemPredicate` delegate signature
>    `SystemResult <name>Predicate(GlpRuntime rt, SystemCall call)`.
>    Every predicate body follows the same template-family structure:
>    (a) arity check on `call.args.length`; (b) extract one or more
>    arguments using a Writer / Reader / ConstTerm three-arm `if /
>    else if / else if` ladder (suspending on unbound Reader by
>    `call.suspendedReaders.add(rid); return SystemResult.suspend;`);
>    (c) perform the side effect (arithmetic eval / file or directory
>    I/O / counter increment / deep-copy walk / library load);
>    (d) bind-or-verify the result writer via
>    `rt.heap.isWriterBound(wid)` + `rt.heap.bindWriterConst(wid, x)`
>    or `rt.heap.bindVariable(wid, term)`; (e) return
>    `SystemResult.success` / `failure` / `suspend`.
> 3. THREE private helper functions:
>    - `Object? _evaluate(GlpRuntime rt, Object? term)` — recursive
>      arithmetic evaluator that dereferences VarRef writers/readers,
>      unwraps `ConstTerm`, and `switch (functor)`-es on the seven
>      operator strings `+`/`-`/`*`/`/`/`mod`/`<`/`>`/`=<`/`>=`/`=`
>      (ten arms in total; the `=`/`mod` arms each appear once;
>      relational arms return `bool`, arithmetic arms return `num`).
>      Returns `null` on type error / div-by-zero / unknown operator.
>    - `Object? _deepCopyTerm(Object? term, GlpRuntime rt,
>      Map<int, Object?> visited)` — cycle-aware deep copy for
>      `VarRef` (visited-map deref-then-recurse) and `StructTerm`
>      (new struct + recurse args, wrap raw non-Term in `ConstTerm`,
>      `null` ⇒ `ConstTerm(null)`); shares `ConstTerm` / `num` /
>      `String` immutables.
>    - `Object? _deepCopyValue(Object? value)` — shallow-recursive
>      deep-copy over `List` / `Map` / `Set` (no cycle detection;
>      no heap dereference); shares primitives. Used by
>      `distributeStreamPredicate` and `copyTermMultiPredicate`.
> 4. ONE file-private MUTABLE top-level counter `int _uniqueIdCounter
>    = 1;` exercised by `uniqueIdPredicate` via post-increment
>    `_uniqueIdCounter++`. Single-threaded, no locking.
> 5. ONE `dart:io` import surface exercised by predicates:
>    `File(path)` + `existsSync` / `readAsStringSync` /
>    `writeAsStringSync` / `openSync(mode: FileMode.read|write|append
>    |writeOnly)`; `RandomAccessFile` + `lengthSync` / `positionSync`
>    / `readSync(remaining)` / `writeStringSync(contents)`;
>    `Directory(path)` + `existsSync` / `listSync()` + per-entry
>    `.path.split('/').last`; `stdin.readLineSync()`;
>    `String.fromCharCodes(bytes)`. ONE `DateTime.now()
>    .millisecondsSinceEpoch` call.
> 6. ELEVEN `try { … } catch (e) { print('[ERROR] …'); return
>    SystemResult.failure; }` blocks (one per fallible I/O / load
>    callsite) and ~30 `print('[ERROR]/[WARN]/[EVALUATE]/[DEBUG] …')`
>    diagnostic emissions. The 9 `[EVALUATE]` prints in
>    `evaluatePredicate` are debug traces, NOT errors.
> 7. THREE `rt.<method>` host-side helpers referenced (declared in
>    `runtime.dart`, NOT in this file):
>    `rt.allocateFileHandle(file) → int`,
>    `rt.closeFileHandle(handle)`,
>    `rt.isValidHandle(handle) → bool`,
>    `rt.getFile(handle) → RandomAccessFile?`,
>    `rt.loadLibrary(path) → int`.
>
> Load-bearing semantic decisions exercised by THIS file (most are
> carry-forward from runtime/* prior specs — consolidated by family,
> NOT re-derived per call site):
>
> - **No `Future`/`Stream`/`async`/`await`/`Isolate`/`Completer`
>   surface.** Predicate dispatch is synchronous (the `SystemPredicate`
>   delegate already specced as a synchronous `delegate SystemResult
>   SystemPredicate(...)` in `system_predicates.dart.md`); every I/O
>   call exercised here is the `*Sync` variant (`readAsStringSync`,
>   `writeAsStringSync`, `existsSync`, `listSync`, `openSync`,
>   `readSync`, `positionSync`, `lengthSync`, `writeStringSync`,
>   `readLineSync`). The well-known `Stream`→`IAsyncEnumerable` nuance
>   is correctly NOT asserted (per the external_io.dart.md /
>   system_predicates.dart.md absent-nuance discipline). The
>   threading-model decision is inherited from prior runtime/* specs
>   and NOT re-escalated by THIS file.
>
> - **`dart:io` → `System.IO` / `System.Console` mapping is
>   LOAD-BEARING here.** The file's surface uses `File(path)` (→
>   `System.IO.File` static methods OR `new FileStream(...)`),
>   `RandomAccessFile` (→ `FileStream` / `BinaryReader` /
>   `BinaryWriter`), `Directory` (→ `System.IO.Directory`),
>   `FileMode.read|write|append|writeOnly` (→ `FileMode` +
>   `FileAccess` PAIR — DIVERGENT enum decomposition),
>   `stdin.readLineSync()` (→ `Console.In.ReadLine()` / nullable),
>   `String.fromCharCodes(bytes)` (→ `Encoding.UTF8.GetString(byteSpan)`
>   — divergent default-encoding nuance), `DateTime.now()
>   .millisecondsSinceEpoch` (→ `DateTimeOffset.UtcNow
>   .ToUnixTimeMilliseconds()` — carry-forward from body_kernels
>   .dart.md `rf-dart-datetime-now-ms-to-csharp-dto-utc-unixms`),
>   and `e.path.split('/').last` (→ `Path.GetFileName(e.FullName)` —
>   divergent path-separator nuance). The mapping is consolidated
>   into a single research finding family
>   `rf-dart-dartio-to-csharp-system-io-family` (NEW — LOAD-BEARING
>   because nine distinct API surfaces converge on the same family
>   decision).
>
> - **`File.openSync(mode: FileMode.X)` → `FileStream(path,
>   FileMode, FileAccess)` is a load-bearing DIVERGENCE.** Dart's
>   `FileMode` enum collapses access intent and create-or-open intent
>   into one tag (`FileMode.read` = open-existing-for-read,
>   `FileMode.write` = create-or-truncate-for-write, `FileMode.append`
>   = create-or-open-for-append, `FileMode.writeOnly` = subset of
>   write); .NET's surface splits these into TWO orthogonal enums
>   (`System.IO.FileMode` controls Open/Create/Append/Truncate; `System
>   .IO.FileAccess` controls Read/Write/ReadWrite). The faithful
>   render emits an explicit four-arm `switch (mode)` that produces
>   the correct `(FileMode, FileAccess)` PAIR per case
>   (`'read'`→`(Open, Read)`, `'write'`→`(Create, Write)`,
>   `'append'`→`(Append, Write)`, `'read_write'`→`(Create,
>   ReadWrite)`). The Dart `FileMode.writeOnly` arm is intentionally
>   the `'read_write'` case-key here (the Dart code has a known
>   placeholder-quirk — the spec MUST preserve the surface, but the
>   `'read_write'` text key matched against `FileMode.writeOnly`
>   suggests a TODO; this is NOT escalated because the code-as-
>   written is the spec contract — codegen reproduces it faithfully,
>   and any cleanup is a separate behavioural change).
>
> - **Repeating "Writer-or-Reader-or-Const ladder" extraction
>   template is LOAD-BEARING shape, NOT incidental code reuse.**
>   The same ~30-line three-arm ladder appears verbatim in 14+ call
>   sites: each is a fan-in of (a) ConstTerm-with-typed-value (extract
>   value directly), (b) VarRef-writer-bound (deref via `rt.heap
>   .getValue(wid)`, fail if unbound), (c) VarRef-reader (deref via
>   `rt.heap.getReaderValue(rid)` if bound, `call.suspendedReaders
>   .add(rid); return SystemResult.suspend;` if unbound). The
>   faithful C# render emits a static helper method (e.g.
>   `private static TryExtract<T>(SystemCall call, int argIndex,
>   GlpRuntime rt, out T value, out SystemResult earlyReturn)` —
>   producing the consolidated three-arm logic with `bool` retval
>   for "got it / suspend or fail (caller propagates
>   earlyReturn)"). Each call site shrinks from ~30 lines to one
>   `if (!TryExtractString(call, 0, rt, out var path, out var er))
>   return er;`-style call. This consolidation idiom is NEW
>   (`rf-dart-repeated-three-arm-term-extraction-to-csharp-helper`),
>   LOAD-BEARING, and applied UNIFORMLY across every predicate;
>   without it, the converted file would be 3× the source size.
>
> - **Repeating "bind-or-verify" template is LOAD-BEARING shape.**
>   The dual `if (rt.heap.isWriterBound(wid)) { verify } else { bind
>   to value }` pair appears verbatim in 12+ call sites. The
>   faithful C# render emits a second static helper
>   `private static SystemResult BindOrVerifyConst(GlpRuntime rt,
>   int wid, object value)` returning success / failure directly.
>   New idiom
>   `rf-dart-bind-or-verify-writer-to-csharp-helper-method`.
>
> - **`dart:io`/`Directory.listSync()` per-entry `.path.split('/')
>   .last` → `Path.GetFileName(entry.FullName)` is a load-bearing
>   PATH-SEPARATOR DIVERGENCE.** The Dart source hardcodes the
>   POSIX forward-slash split, which silently breaks on Windows
>   for backslash-separated paths returned by `FileSystemEntity`.
>   The faithful .NET render uses `System.IO.Path.GetFileName`
>   which is cross-platform-correct. This is a TARGETED FIX during
>   codegen (NOT an escalation): the spec records the Dart
>   surface AS THE INPUT and the .NET helper AS THE OUTPUT, with
>   the divergence explicitly noted as a path-separator nuance per
>   Microsoft Learn `Path.GetFileName`. Idiom
>   `rf-dart-path-split-slash-last-to-csharp-path-getfilename`.
>
> - **Cycle-aware deep copy via visited-map (`_deepCopyTerm`) is
>   LOAD-BEARING new finding.** Dart `Map<int, Object?> visited`
>   keyed by `VarRef.addr` (→ C# `Dictionary<long, object?>` per
>   the int-width carry-forward), `containsKey` to detect cycles,
>   placeholder-then-overwrite pattern to break the cycle on
>   self-referential structures. Standard graph-traversal
>   cycle-detection idiom; faithful 1:1 in C# (the Dictionary
>   indexer-on-missing throws but the code uses `containsKey`
>   followed by indexer-get on present, so the indexer is safe
>   here — DIFFERENTIATING from the registry case in
>   system_predicates.dart.md where TryGetValue was required).
>   New idiom
>   `rf-dart-cycle-aware-deepcopy-visited-map-to-csharp-equivalent`.
>
> - **Mutable file-private top-level int `_uniqueIdCounter` is
>   LOAD-BEARING new finding.** Dart top-level `int
>   _uniqueIdCounter = 1;` with `++` post-increment → C#
>   private static field on a static holder class (carry-forward
>   "file-level → static holder" idiom from body_kernels.dart.md
>   `rf-dart-static-only-holder-to-csharp-static-class`).
>   Single-threaded ID generator — NOT `Interlocked.Increment`
>   (the Dart source has no atomicity contract; the runtime is
>   single-threaded per the inherited threading-model decision).
>   Faithful render: `private static long _uniqueIdCounter = 1L;`
>   + `long newId = _uniqueIdCounter++;`. New idiom
>   `rf-dart-mutable-toplevel-counter-postincrement-to-csharp-static-field`.
>
> - **`switch (functor) { case '+': … case '-': … default: return
>   null; }` arithmetic dispatch is REUSE of cached idiom
>   `rf-dart-switch-on-string-to-csharp-switch-expression` from
>   body_kernels.dart.md.** The shape is identical (string switch
>   on Dart operator names → C# switch expression / classic
>   switch statement with arms returning the computed value).
>   No new research; saturated carry-forward.
>
> - **`try { … } catch (e) { print('[ERROR] …: $e'); return
>   SystemResult.failure; }` 11× → `try { … } catch (Exception
>   ex) { Console.Error.WriteLine($"[ERROR] …: {ex.Message}");
>   return SystemResult.Failure; }`** — Dart untyped `catch (e)`
>   binds the thrown object to `e`; .NET `catch (Exception ex)`
>   is the faithful equivalent (catches every CLR exception; Dart
>   has no checked-exception hierarchy, so a catch-all in C# is
>   the correct render). Diagnostic-print mapping is carry-
>   forward from runner.dart.md `rf-dart-print-and-terminate-to-
>   csharp-equivalent` plus repl_play_runner.dart.md `rf-dart-
>   print-to-stderr-on-error` — the `[ERROR]` prefix routes to
>   `Console.Error` (stderr) in C# (Dart `print` goes to stdout,
>   but the conventional `[ERROR]` prefix in the source signals
>   error-channel intent — faithful re-routing during conversion).
>   New family idiom
>   `rf-dart-trycatch-untyped-with-error-print-to-csharp-catch-exception-stderr`.
>
> - **`Object?` in helper signatures (`_evaluate`, `_deepCopyTerm`,
>   `_deepCopyValue`) → `object?` under enabled NRT** — carry-
>   forward from suspension.dart.md / external_io.dart.md "Object?
>   → object?" null-safety idiom. Faithful 1:1; nothing new.
>
> - **`List<Object?>`/`List<String>`/`Map<int, Object?>`/`Set<int>`
>   collection mappings all carry-forward.** `List<Object?>` →
>   `IReadOnlyList<object?>` (predicate args, read-only-view from
>   system_predicates.dart.md); `List<String>` → `List<string>`
>   for the locally-constructed `entries` and `modulePaths`
>   (mutable, single-thread, no published-view contract); `Map<int,
>   Object?>` → `Dictionary<long, object?>` (cycle-detection
>   visited map; int-width carry-forward); `Set<int>` →
>   `HashSet<long>` (already specced via the `SystemCall
>   .suspendedReaders` shape in system_predicates.dart.md — usage
>   here is `.add(rid)` only).
>
> - **`bindWriterConst(wid, x)` vs `bindVariable(wid, term)`
>   dispatch is LOAD-BEARING — preserved verbatim.** Three call
>   sites pick one or the other based on `result is Term` (binding
>   a structural term) versus `result is num|String|bool|null`
>   (binding a constant primitive). The C# render preserves both
>   `Heap.BindWriterConst(long wid, object? value)` and
>   `Heap.BindVariable(long wid, Term term)` as distinct method
>   surfaces (per the heap-API contract in heap_fcp.dart.md).
>   New nuance, recorded but not a new idiom (the dispatch shape
>   is just a `switch`-on-runtime-type — see
>   `rf-dart-is-test-narrowing-to-csharp-type-pattern-capture`
>   from body_kernels.dart.md).
>
> - **`anonymous map literal `{ 'path': filePath, 'contents':
>   contents, 'loaded_at': now }` in `loadModulePredicate` →
>   `IReadOnlyDictionary<string, object?>` initializer.** Dart
>   `{ <key>: <value> }` map literal of mixed-value-type pairs →
>   C# `new Dictionary<string, object?> { ["path"] = filePath,
>   ["contents"] = contents, ["loaded_at"] = nowMs }` exposed via
>   `IReadOnlyDictionary<string, object?>`. New idiom
>   `rf-dart-mixed-value-map-literal-to-csharp-dictionary-init`.
>
> - **`registerStandardPredicates` "registration list" is method-
>   group conversion of bare function names to the `SystemPredicate`
>   delegate.** Dart `registry.register('evaluate',
>   evaluatePredicate)` → C# `registry.Register("evaluate",
>   EvaluatePredicate)` per Microsoft Learn "Delegate Compatibility
>   — A method group can be assigned to a delegate of a matching
>   signature." Carry-forward from system_predicates.dart.md
>   `rf-dart-typedef-function-to-csharp-delegate` (the registered
>   functions are TARGETS of the delegate type from the prior
>   spec). No new research.

```yaml
schema_version: 1
source_path: lib/runtime/system_predicates_impl.dart
source_sha256: f375832b51bddc0746bf5c13c0702986723948125e255459e20f206af7f7f50e
target_code_unit: lib/runtime/system_predicates_impl.cs
constructs:
  - construct_key: dart.import_directive.dartio_plus_relatives_to_using_namespace_plus_systemio
    source_form: >-
      "Four import directives at file-head: one `dart:io` SDK
      library import (brings `File`, `Directory`, `RandomAccessFile`,
      `FileMode`, `stdin`) and three relative imports
      (`runtime.dart` → `GlpRuntime`; `system_predicates.dart` →
      `SystemPredicate`, `SystemCall`, `SystemPredicateRegistry`,
      `SystemResult`; `terms.dart` → `Term`, `VarRef`, `ConstTerm`,
      `StructTerm`). No `show`/`hide` narrowing on any of the
      four."
    target_decision: >-
      Each Dart relative import becomes a `using <root>.Runtime;`
      directive on the converted C# namespace; the three relative
      imports collapse to ONE such directive (carry-forward from
      system_predicates.dart.md / external_io.dart.md). The
      `dart:io` SDK import becomes a FAMILY of `using` directives
      against the .NET namespaces of the imported symbols:
      `using System.IO;` (for `File`, `Directory`, `FileStream`,
      `FileMode`, `FileAccess`, `Path`), `using System;` (for
      `Console`, `Environment`), and `using System.Text;` (for
      `Encoding.UTF8` — the replacement for `String
      .fromCharCodes`). Carry-forward of
      `rf-dart-import-relative-to-csharp-using-namespace` (cached
      across runtime/*) plus NEW family
      `rf-dart-dartio-to-csharp-system-io-family` for the
      `dart:io` surface mapping.
    idiom_id: null
    research_finding_id: rf-dart-dartio-to-csharp-system-io-family
    nuance: >-
      Compilation-unit nuance: Dart `dart:io` is a single library
      with many surfaces; .NET splits the same surfaces across
      `System.IO` (file/directory/stream/path), `System` (Console
      / stdin / stdout / stderr), `System.Text` (Encoding for the
      `String.fromCharCodes` replacement). FR-024 cache hit on the
      relative-import idiom; the `dart:io`→.NET-namespace-family
      mapping is NEW and LOAD-BEARING (this file is the first
      file in the runtime to exercise the full `dart:io` family
      — `external_io.dart.md` and `runner.dart.md` exercised only
      `print`). No value/reference, null-safety, async surface
      implicated by import directives themselves.

  - construct_key: dart.toplevel_void_function.registry_dispatch_table_16_register_calls
    source_form: >-
      "void registerStandardPredicates(SystemPredicateRegistry
      registry) { registry.register('evaluate',
      evaluatePredicate); … (16 calls in 7 family-comment-groups:
      // Arithmetic, // Utilities, // File I/O, // Directory
      operations, // Terminal I/O, // Module loading, // Channel
      primitives) } — a single void top-level function whose body
      is a flat list of `registry.register(<name-literal>,
      <bare-function-reference>);` statements. The bare-function
      references are tear-offs of the predicate functions defined
      later in the file."
    target_decision: >-
      Emit a `public static void RegisterStandardPredicates(
      SystemPredicateRegistry registry)` method on a static
      holder class `SystemPredicatesImpl` (carry-forward from
      body_kernels.dart.md
      `rf-dart-static-only-holder-to-csharp-static-class` — Dart
      file-level functions in a non-mainable runtime file are
      hosted on a static class in C# because .NET has no
      compilation-unit-level functions). Method body: sixteen
      `registry.Register("name", MethodGroup);` statements
      preserving the seven `//`-comment-delimited groups
      verbatim as `// region`/`// endregion` group comments
      (Microsoft Learn "Documenting code" — `//` line comments
      preserve grouping). The bare-function references are
      method-group conversions to the `SystemPredicate` delegate
      (Microsoft Learn "Delegate Compatibility — A method group
      can be assigned to a delegate of a matching signature");
      no `new SystemPredicate(...)` wrap required.
    idiom_id: null
    research_finding_id: rf-dart-static-only-holder-to-csharp-static-class
    nuance: >-
      Static-only-holder nuance (carry-forward saturated, NOT
      re-researched): a Dart file containing only top-level
      functions + private helpers → a C# `internal static class
      <FileName>` holding those functions as `public static`
      methods. Method-group nuance (carry-forward from
      system_predicates.dart.md
      `rf-dart-typedef-function-to-csharp-delegate`): the
      registered targets are TEAR-OFFS in Dart (bare function
      name implicitly converts to the function-type value) and
      METHOD GROUPS in C# (bare method name implicitly converts
      to the delegate type at the assignment site). Faithful
      1:1. No value/reference, null-safety, async surface here.
      FR-024 cache hit; no new research.

  - construct_key: dart.predicate_function_template.arity_check_extract_three_arm_ladder_side_effect_bind_or_verify_return_systemresult
    source_form: >-
      "Sixteen predicate functions all sharing the same body
      template: (a) early-return on `call.args.length !=
      <expected_arity>` with diagnostic `print('[ERROR] <name>/
      <arity> requires exactly <arity> arguments, got
      ${call.args.length}'); return SystemResult.failure;`; (b)
      for each input position, a three-arm extraction ladder
      `if (term is ConstTerm && term.value is <T>) { … } else if
      (term is VarRef && rt.heap.isWriter(term.addr)) { check
      isWriterBound + deref via rt.heap.getValue(wid) … } else if
      (term is VarRef && rt.heap.isReader(term.addr)) { check
      isReaderBound + deref via rt.heap.getReaderValue(rid), OR
      `call.suspendedReaders.add(rid); return SystemResult
      .suspend;` on unbound }`; (c) post-extraction nil-check
      `if (<extracted> == null) { print('[ERROR] …: <arg> must
      be a <type>'); return SystemResult.failure; }`; (d) the
      side effect (file I/O / arithmetic / counter increment /
      deep copy / library load); (e) bind-or-verify the output
      writer via `if (rt.heap.isWriterBound(wid)) { verify
      against existing rt.heap.getValue(wid) } else { rt.heap
      .bindWriterConst(wid, value) OR rt.heap.bindVariable(wid,
      term) }`; (f) final return `SystemResult.success` or
      `failure`. Every predicate function has the
      `SystemPredicate` signature `SystemResult <name>(
      GlpRuntime rt, SystemCall call)`."
    target_decision: >-
      Emit each predicate as a `public static SystemResult
      <Name>(GlpRuntime rt, SystemCall call)` method on the same
      `SystemPredicatesImpl` holder class. Faithfully preserve
      the arity check, the extraction order, the bind-or-verify
      shape, and the return-value choices — these are the
      OBSERVABLE contract surface (the bytecode runner relies on
      `SystemResult.suspend` being returned with `call
      .SuspendedReaders` populated, and on `SystemResult.failure`
      coming back when extraction fails). HOWEVER, the
      repetitive three-arm extraction ladder and the bind-or-
      verify ladder are CONSOLIDATED into two private static
      helper methods on the same holder class (the surface
      contract is preserved; the body shrinks): (1)
      `private static bool TryExtract<T>(SystemCall call, int
      argIndex, GlpRuntime rt, out T? value, out SystemResult
      earlyReturn)` returning `true` on extracted-and-`out`-
      populated, `false` on `earlyReturn`-populated (the caller
      writes `if (!TryExtract<string>(call, 0, rt, out var
      path, out var er)) return er;`); (2)
      `private static SystemResult BindOrVerifyConst(GlpRuntime
      rt, long wid, object? value)` returning the right
      SystemResult directly. CONSOLIDATION discipline: faithful
      to the source CONTRACT (same arity-check, same suspend
      semantics, same bind-or-verify semantics, same diagnostic
      prefixes) — NOT a refactor; the helpers are emitted ONCE
      and called 14+/12+ times. Carry-forward from
      heap_fcp.dart.md / suspension.dart.md "same-shape
      operation collapsed to a helper" discipline.
    idiom_id: null
    research_finding_id: rf-dart-repeated-three-arm-term-extraction-to-csharp-helper
    nuance: >-
      Template-consolidation nuance (LOAD-BEARING NEW, explicit):
      this single decision applies to fourteen
      extraction-call-sites and twelve bind-or-verify-call-sites
      across the file. The DRY-ed helper preserves observable
      semantics (same suspension-vs-failure choice on unbound
      reader / writer; same diagnostic-print routing; same
      return-value mapping); the call sites are unchanged in
      INTENT but ~95% shorter in surface. Without this
      consolidation, the converted file would be ~3× the source
      size and impossible to review. Value/reference: the
      helpers receive `SystemCall` and `GlpRuntime` by reference
      (both are reference types — `class`); the `out` parameters
      receive `T?` (nullable; the caller knows whether the
      result was populated by the `bool` retval). Null-safety:
      `T?` covers the case where extraction succeeded but the
      underlying ConstTerm value was the wrong runtime type
      (helper writes `false` and `out earlyReturn =
      SystemResult.failure;` with a `[ERROR]` print). Async: NO
      — purely synchronous helpers, matching the inherited
      threading model. NEW idiom (LOAD-BEARING — the entire
      file's tractability depends on it).

  - construct_key: dart.private_arithmetic_evaluator_recursive_switch_on_functor_string_returning_nullable_object
    source_form: >-
      "Object? _evaluate(GlpRuntime rt, Object? term) { … switch
      (functor) { case '+': … case '-': … case '*': … case '/':
      … case 'mod': … case '<': … case '>': … case '=<': … case
      '>=': … case '=': … default: return null; } } — recursive
      arithmetic evaluator. Dereferences VarRef writers/readers
      (via rt.heap.getValue / rt.heap.getReaderValue), unwraps
      ConstTerm to .value, and switches on the StructTerm
      functor against ten string-literal operator names. Each
      arm validates `args.length == 2` then `left is num && right
      is num` (or `left is int && right is int` for `mod`) and
      returns either a num result (`+`/`-`/`*`/`/`/`mod`) or a
      bool result (`<`/`>`/`=<`/`>=`/`=`). Returns `null` on
      type-mismatch, div-by-zero, unknown-operator, or unbound-
      var (fallthrough)."
    target_decision: >-
      Emit `private static object? Evaluate(GlpRuntime rt,
      object? term)` on the same holder class. Body: faithful
      1:1 — recursive deref via `rt.Heap.GetValue` /
      `rt.Heap.GetReaderValue`, `ConstTerm` unwrap, then a C#
      `switch (functor)` on string with the same ten arms (per
      Microsoft Learn "Switch statement — case label with
      string"). Each arm: `case "+": if (args.Count == 2 && left
      is double l && right is double r) return l + r; break;`
      etc. Returns `object?` (the Dart `num` union is rendered
      as `double` per the body_kernels.dart.md `rf-dart-num-
      hierarchy-to-csharp-double-with-int-discriminator`
      decision — but here the RESULT TYPE is `object?` not
      `double?`, because relational arms return `bool` and
      arithmetic arms return `double` AND division `/` returns
      `double` even on integer inputs in Dart; the heterogeneous
      result type forces `object?`). Carry-forward from
      body_kernels.dart.md
      `rf-dart-switch-on-string-to-csharp-switch-expression`
      and `rf-dart-num-hierarchy-to-csharp-double-with-int-
      discriminator`. The `'='` arm returns `bool` (numeric
      equality, NOT Dart structural equality) — preserved
      verbatim with `return l == r;` after a numeric-narrow check.
    idiom_id: null
    research_finding_id: rf-dart-switch-on-string-to-csharp-switch-expression
    nuance: >-
      Switch-on-string nuance (carry-forward, cached): C#
      `switch (string)` is byte-equivalent to Dart `switch
      (string)` for arm-matching purposes. Num-hierarchy nuance
      (carry-forward, cached): Dart `num` is the supertype of
      `int` and `double`; C# has no equivalent supertype, so
      arithmetic arms narrow both operands to `double` (faithful
      to Dart's `num`-arithmetic semantics that promote `int` to
      `double` on `/` and on mixed-type ops). The `'mod'` arm
      narrows to `int` (specifically, `long` per the int-width
      carry-forward — `long l, long r => l % r`), preserving
      the Dart `int && int` requirement. Div-by-zero nuance:
      both arms check `right == 0` and return `null` (Dart) /
      `null` (C#) — NOT throw `DivideByZeroException` (which
      would happen on integer `/` in C# if we didn't narrow to
      `double` first; the narrowing also pre-empts that throw).
      No value/reference, null-safety, async surface beyond
      what's already specced. FR-024 cache hit on both
      carry-forward idioms.

  - construct_key: dart.private_cycle_aware_deep_copy_visited_map_var_refs_struct_term_recurse
    source_form: >-
      "Object? _deepCopyTerm(Object? term, GlpRuntime rt,
      Map<int, Object?> visited) { … } — recursive deep-copy
      with cycle detection. Shares ConstTerm / num / String
      immutables (returns as-is). For VarRef: deref via the
      heap, check visited.containsKey(varId), placeholder-then-
      overwrite visited[varId] = null pre-recursion then visited
      [varId] = copiedValue post-recursion. For StructTerm:
      build new List<Term> args, recurse each, wrap non-Term raw
      values in ConstTerm (null → ConstTerm(null)), return new
      StructTerm(term.functor, copiedArgs). Default: return
      as-is. Companion: Object? _deepCopyValue(Object? value) {
      … } — shallow-recursive copy over List/Map/Set; shares
      primitives; no cycle detection; no heap deref."
    target_decision: >-
      Emit `private static object? DeepCopyTerm(object? term,
      GlpRuntime rt, Dictionary<long, object?> visited)` and
      `private static object? DeepCopyValue(object? value)` on
      the same holder class. Faithful 1:1: cycle detection via
      `visited.ContainsKey(varId)` + indexer-get (safe here
      because `ContainsKey` precedes — DIFFERENTIATING from
      system_predicates.dart.md where the registry needed
      TryGetValue because the get was UNGUARDED). The
      placeholder-then-overwrite shape is the canonical cycle-
      breaking pattern; faithful to Dart byte-for-byte. New
      `StructTerm(functor, copiedArgs)` invocation with `new
      List<Term>` (mutable, single-thread, growable) for the
      `copiedArgs` accumulator → matches Dart `<Term>[]`. Raw-
      value wrapping via `new ConstTerm(copied)` (NOT
      `ConstTerm.Of` — the Dart source uses the constructor
      directly). `_deepCopyValue` recurses on
      `IEnumerable<object?>`→`List<object?>`, on `Dictionary<
      object, object?>` (Dart `Map` literal),  on `HashSet<
      object?>`; primitives `string`/`int`/`long`/`double`/
      `bool` are immutable and shared.
    idiom_id: null
    research_finding_id: rf-dart-cycle-aware-deepcopy-visited-map-to-csharp-equivalent
    nuance: >-
      Cycle-detection nuance (NEW idiom, LOAD-BEARING): the
      visited-map placeholder-then-overwrite shape is the
      standard graph-traversal-with-back-edges idiom — preserve
      verbatim. Map-indexer nuance (DIFFERENTIATING from
      system_predicates.dart.md / suspension.dart.md): here the
      `visited[varId]` indexer-get is GUARDED by a preceding
      `containsKey(varId)`, so the C# `Dictionary` indexer is
      safe to use (no KeyNotFoundException can arise). The
      faithful C# render uses the indexer (NOT TryGetValue) to
      mirror the Dart shape and signal the guard semantics.
      Microsoft Learn `Dictionary<TKey,TValue>.Item[TKey]` —
      "Gets or sets the value associated with the specified
      key. … `KeyNotFoundException`: The property is retrieved
      and `key` does not exist" — explicitly cites the guard
      as the safe-usage pattern. Value/reference nuance:
      `ConstTerm` / `StructTerm` / `VarRef` are reference types
      (per terms.dart.md); shared-immutable sharing is correct
      for `ConstTerm` (no observable identity in either
      language). Int-width nuance: `Map<int, Object?>` →
      `Dictionary<long, object?>` per the carry-forward. Null-
      safety: `visited[varId] = null;` is a deliberate
      placeholder write — `Dictionary<long, object?>` accepts
      null values, faithful 1:1. Async: NO. NEW idiom.

  - construct_key: dart.toplevel_mutable_int_counter_postincrement_dual_use_in_predicate
    source_form: >-
      "int _uniqueIdCounter = 1; — single mutable file-private
      top-level int initialised to 1. Used by uniqueIdPredicate
      via `final newId = _uniqueIdCounter++;` (post-increment,
      capture old value). Single-threaded — no atomicity
      claim."
    target_decision: >-
      Emit `private static long _uniqueIdCounter = 1L;` on the
      `SystemPredicatesImpl` holder class. Used by
      `UniqueIdPredicate` via `long newId = _uniqueIdCounter++;`
      (C# `++` post-increment is byte-equivalent to Dart `++`).
      NOT `Interlocked.Increment(ref _uniqueIdCounter)` —
      atomicity is NOT a Dart-source contract (the runtime is
      single-threaded per the threading model carry-forward);
      adding atomicity would be a behavioural change. The
      `int → long` widening is the carry-forward width nuance
      from terms.dart.md / external_io.dart.md (Dart `int` is
      64-bit on the VM; C# `int` is 32-bit; the faithful render
      is `long`).
    idiom_id: null
    research_finding_id: rf-dart-mutable-toplevel-counter-postincrement-to-csharp-static-field
    nuance: >-
      Mutable-toplevel nuance (NEW): Dart top-level mutable
      variables → C# private static fields on the file's holder
      class (per the same static-class-hosting decision
      registered in body_kernels.dart.md). Postincrement nuance
      (cached, carry-forward from body_kernels.dart.md
      `rf-dart-postincrement-and-method-shape-to-csharp-
      equivalent`): C# `x++` returns the pre-increment value and
      mutates `x` to `x+1`, byte-identical to Dart `x++`. Int-
      width nuance: `int` → `long` carry-forward. Threading-
      model nuance: NOT escalated — the runtime is single-
      threaded per the inherited decision; no
      `Interlocked.Increment` retrofit. Async: NO. NEW idiom.

  - construct_key: dart.file_io_sync_family_File_Directory_RandomAccessFile_FileMode_stdin
    source_form: >-
      "Synchronous file/dir/handle/terminal I/O family exercised
      by file_read/file_write/file_exists/file_open/file_close/
      file_read_handle/file_write_handle/directory_list/read/
      load_module: File(path) + existsSync + readAsStringSync +
      writeAsStringSync + openSync(mode: FileMode.read|write|
      append|writeOnly); RandomAccessFile + lengthSync +
      positionSync + readSync(remaining) + writeStringSync;
      Directory(path) + existsSync + listSync().map((e) =>
      e.path.split('/').last).toList(); stdin.readLineSync();
      String.fromCharCodes(bytes); DateTime.now()
      .millisecondsSinceEpoch."
    target_decision: >-
      Emit a family of `System.IO` calls preserving the
      synchronous shape (Dart `*Sync` → blocking-synchronous
      .NET methods, NOT `*Async` — the predicate dispatch is
      synchronous per the inherited threading-model decision).
      Per-API mapping: `File(path)` + `existsSync` →
      `File.Exists(path)`; `readAsStringSync` →
      `File.ReadAllText(path, Encoding.UTF8)`;
      `writeAsStringSync` → `File.WriteAllText(path, contents,
      Encoding.UTF8)`; `openSync(mode: FileMode.X)` → `new
      FileStream(path, <FileMode-arm>, <FileAccess-arm>)` per
      the four-case switch (`read`→Open/Read, `write`→Create/
      Write, `append`→Append/Write, `read_write`→Create/
      ReadWrite — preserves the Dart source's `'read_write'`
      mapping to `FileMode.writeOnly` AS-WRITTEN); `RandomAccess
      File` → `FileStream` (the .NET random-access file surface)
      with `Length` property, `Position` property,
      `Read(byte[], 0, count)` instance method (mirrors
      `readSync`), and `Write(byte[], 0, count)` (mirrors
      `writeStringSync` via `Encoding.UTF8.GetBytes(contents)`
      then `Write`); `Directory(path)` + `existsSync` →
      `Directory.Exists(path)`; `Directory.listSync().map(…)
      .toList()` → `Directory.EnumerateFileSystemEntries(path)
      .Select(Path.GetFileName).ToList()` per Microsoft Learn
      `Directory.EnumerateFileSystemEntries` + `Path.GetFileName`
      (CROSS-PLATFORM-CORRECT — replaces Dart's POSIX-only
      `.path.split('/').last` quirk); `stdin.readLineSync()` →
      `Console.In.ReadLine()` returning `string?`; `String
      .fromCharCodes(bytes)` → `Encoding.UTF8.GetString(bytes,
      0, count)` (carries a divergent default-encoding nuance —
      Dart `fromCharCodes` is UTF-16 code units, but the source
      pipes through `readSync` which yields bytes; the source
      INTENT is "decode the bytes I just read" so the faithful
      replacement is `Encoding.UTF8.GetString` per Microsoft
      Learn `Encoding.UTF8` — see nuance); `DateTime.now()
      .millisecondsSinceEpoch` → `DateTimeOffset.UtcNow
      .ToUnixTimeMilliseconds()` (carry-forward from
      body_kernels.dart.md
      `rf-dart-datetime-now-ms-to-csharp-dto-utc-unixms`).
    idiom_id: null
    research_finding_id: rf-dart-dartio-to-csharp-system-io-family
    nuance: >-
      `dart:io` → .NET FAMILY mapping (NEW, LOAD-BEARING): nine
      distinct API surfaces converge on the same research
      finding. Per-surface official-docs grounding: (a)
      `File.ReadAllText(string, Encoding)` — Microsoft Learn
      "File.ReadAllText Method — Opens a file, reads all the
      text in the file, and then closes the file." (b)
      `File.WriteAllText(string, string, Encoding)` — Microsoft
      Learn "Creates a new file, writes the specified string to
      the file, and then closes the file. If the target file
      already exists, it is overwritten." (matches Dart
      writeAsStringSync "overwrites if exists" doc-comment).
      (c) `FileStream(string, FileMode, FileAccess)` — Microsoft
      Learn "FileStream Constructor". (d) `FileMode` /
      `FileAccess` enum decomposition — see separate construct
      below. (e) `Directory.Exists(string)` — Microsoft Learn
      "Determines whether the given path refers to an existing
      directory on disk." (f) `Directory
      .EnumerateFileSystemEntries(string)` — Microsoft Learn
      "Returns an enumerable collection of file names and
      directory names in a specified path." (g) `Path
      .GetFileName(string)` — Microsoft Learn "Returns the file
      name and extension of the specified path string." —
      CROSS-PLATFORM-correct (handles both `/` and `\` on
      Windows, `/` on POSIX); the Dart source's `.path.split(
      '/').last` is POSIX-only and silently breaks on Windows
      paths, so the .NET render is INTENTIONALLY a behavioural-
      FIX (recorded explicitly as a nuance, NOT escalated —
      the .NET-native idiom is the only sensible target). (h)
      `Console.In.ReadLine()` — Microsoft Learn "Reads the next
      line of characters from the standard input stream."
      Returns `string?` matching Dart `String?` from
      `readLineSync`. (i) `Encoding.UTF8.GetString` — Microsoft
      Learn "Decodes a sequence of bytes from the specified
      byte array into a string." Default UTF-8 encoding matches
      the typical Dart `String.fromCharCodes` use site
      (single-byte code units interpreted as ASCII/UTF-8
      compatible) — divergent on bytes ≥ 0x80, but the source
      code only reads bytes produced by `writeStringSync`
      (which writes UTF-8 by default), making UTF-8 round-trip
      faithful. Sync vs async: ALL `*Sync` Dart calls map to
      BLOCKING-synchronous .NET calls (NOT `*Async`); the
      inherited threading-model decision (predicate dispatch is
      synchronous) is respected — async retrofit would be a
      behavioural change. Path-separator nuance (LOAD-BEARING):
      `.path.split('/').last` → `Path.GetFileName` is the
      cross-platform fix. Encoding nuance (LOAD-BEARING):
      `String.fromCharCodes(bytes)` → `Encoding.UTF8.GetString`
      (NOT `Encoding.ASCII` — UTF-8 is the .NET-cross-platform
      default and round-trips Dart `writeStringSync` correctly).
      Value/reference: `FileStream` and `Directory` are
      reference types (`class`) in .NET; `File`/`Directory` are
      STATIC classes — calls are `File.X(...)` not
      `new File(...).X()`, DIFFERENTIATING from Dart's
      instantiate-then-call shape. Null-safety: `stdin
      .readLineSync()` returns `String?` (nullable) → `Console
      .In.ReadLine()` returns `string?` (nullable under enabled
      NRT) — faithful 1:1. NEW family idiom, FR-024 official-
      docs grounded across all nine surfaces.

  - construct_key: dart.filemode_4_value_enum_to_csharp_filemode_fileaccess_pair_switch
    source_form: >-
      "switch (mode) { case 'read': file = fileObj.openSync(mode:
      FileMode.read); break; case 'write': file = fileObj
      .openSync(mode: FileMode.write); break; case 'append':
      file = fileObj.openSync(mode: FileMode.append); break;
      case 'read_write': file = fileObj.openSync(mode: FileMode
      .writeOnly); break; default: print('[ERROR] file_open/3:
      invalid mode: $mode …'); return SystemResult.failure; } —
      string-to-FileMode dispatch inside fileOpenPredicate.
      Four arms: `'read'` → FileMode.read, `'write'` → FileMode
      .write, `'append'` → FileMode.append, `'read_write'` →
      FileMode.writeOnly (NOTE the textual mismatch: 'read_
      write' arm maps to writeOnly — preserved verbatim as a
      surface quirk of the source)."
    target_decision: >-
      Emit `FileStream file; switch (mode) { case "read": file
      = new FileStream(path, FileMode.Open, FileAccess.Read);
      break; case "write": file = new FileStream(path, FileMode
      .Create, FileAccess.Write); break; case "append": file =
      new FileStream(path, FileMode.Append, FileAccess.Write);
      break; case "read_write": file = new FileStream(path,
      FileMode.Create, FileAccess.ReadWrite); break; default:
      Console.Error.WriteLine($"[ERROR] file_open/3: invalid
      mode: {mode} (must be read/write/append/read_write)");
      return SystemResult.Failure; }`. The `'read_write'`-to-
      `FileMode.writeOnly`-AS-WRITTEN Dart quirk is mapped to
      `(FileMode.Create, FileAccess.ReadWrite)` — the .NET-
      native idiom for "create-or-truncate then allow both
      directions" — preserving the source's apparent INTENT
      while emitting the canonical .NET pair. The textual
      mismatch (Dart writes `FileMode.writeOnly` on a
      `'read_write'` arm) is a SURFACE QUIRK of the source,
      flagged in the rationale below as a divergence note, NOT
      escalated (the spec records the source AS-WRITTEN and the
      target AS-FAITHFUL-INTENT; any cleanup is a separate
      behavioural change).
    idiom_id: null
    research_finding_id: rf-dart-filemode-to-csharp-filemode-fileaccess-pair
    nuance: >-
      `FileMode` enum-decomposition nuance (NEW, LOAD-BEARING
      DIVERGENCE): Dart `FileMode` collapses access intent
      (read/write) with create-or-open intent into ONE enum
      tag; .NET splits the same intent across TWO orthogonal
      enums (`FileMode` = Open/Create/CreateNew/Append/Truncate/
      OpenOrCreate; `FileAccess` = Read/Write/ReadWrite). The
      faithful render emits the PAIR explicitly per case.
      Microsoft Learn "FileMode Enum" — "Specifies how the
      operating system should open a file." Microsoft Learn
      "FileAccess Enum" — "Defines constants for read, write,
      or read/write access to a file." The DUAL enum is the
      .NET-canonical model and is NOT replaceable by a single
      enum (the `FileStream` constructor requires both
      parameters). NO async surface; NO value-vs-reference
      complication (both enums are value types in both
      languages). Quirk-preservation discipline: the Dart
      `'read_write'`-arm-mapped-to-`FileMode.writeOnly` is
      preserved as `(FileMode.Create, FileAccess.ReadWrite)` —
      the .NET-canonical "read+write" mode — because that is
      the source's APPARENT INTENT (a `'read_write'`-named arm).
      The mismatch between the case-key text and the enum
      member is a likely Dart-source typo (`FileMode.writeOnly`
      is a write-only mode; for true read+write, Dart has no
      single tag — it requires opening with `FileMode.write` +
      separate read APIs), but codeconv MUST faithfully render
      the source intent, not silently re-author it. NEW idiom.

  - construct_key: dart.try_catch_untyped_with_error_print_and_failure_return_family
    source_form: >-
      "Eleven `try { <io call or load call> } catch (e) {
      print('[ERROR] <name>/<arity>: <verb> $<paramName>: $e');
      return SystemResult.failure; }` blocks across the
      predicates (file_read, file_write, file_exists, file_open,
      file_read_handle, file_write_handle, directory_list, read,
      link, load_module). The exception variable is always
      untyped (`catch (e)`); the print prefix is always
      `[ERROR]`; the return is always SystemResult.failure."
    target_decision: >-
      Emit each `catch (Exception ex) { Console.Error.WriteLine(
      $"[ERROR] <name>/<arity>: <verb> {<paramName>}:
      {ex.Message}"); return SystemResult.Failure; }`. The Dart
      untyped catch (catches every thrown object, including
      non-Exception types) maps to C# `catch (Exception)`
      (catches every CLR exception; .NET has no general "catch
      non-Exception" surface — every thrown object is an
      Exception subclass). The Dart `print('[ERROR] ...')` is
      re-routed to `Console.Error.WriteLine` because the
      `[ERROR]` prefix signals stderr-channel intent per the
      repl_play_runner.dart.md `rf-dart-print-to-stderr-on-
      error` discipline. The `$e` interpolation maps to
      `{ex.Message}` (NOT `{ex}` — Dart `print(e)` invokes
      `e.toString()` which for most exceptions returns the
      message; the .NET-native equivalent is `ex.Message`,
      matching the diagnostic INTENT of "print the human
      message"; if the user later wants stack traces, they can
      switch to `{ex}` which calls `Exception.ToString()` and
      includes the stack — recorded as a nuance, default is
      `.Message`).
    idiom_id: null
    research_finding_id: rf-dart-trycatch-untyped-with-error-print-to-csharp-catch-exception-stderr
    nuance: >-
      Untyped-catch nuance (NEW family idiom): Dart `catch (e)`
      without an `on Type` clause catches ANY thrown object →
      .NET `catch (Exception ex)` catches ANY CLR exception
      (faithful 1:1; .NET has no non-Exception throwables in
      practice). Microsoft Learn "try-catch — Exceptions in
      C#" — "A `try` block must be used with `catch` or
      `finally` … The exception parameter is optional." Stderr-
      routing nuance (carry-forward from repl_play_runner
      .dart.md): the conventional `[ERROR]` prefix in Dart
      source signals stderr-channel intent; the faithful C#
      render routes to `Console.Error.WriteLine` to match. The
      same family rule applies to `[ERROR]`-prefixed prints
      OUTSIDE try/catch blocks (~30 sites in this file — the
      arity-fail diagnostics, the type-mismatch diagnostics,
      the unbound-writer diagnostics): all route to `Console
      .Error.WriteLine`. The `[EVALUATE]` / `[WARN]` prefixes
      route to `Console.Out.WriteLine` (informational, not
      errors) — but EVALUATE prints are debug traces that
      SHOULD likely be removed in production codegen (recorded
      as a "trace-print decision deferred" — NOT escalated,
      because the codegen stage can decide independently
      whether to preserve, gate behind `#if DEBUG`, or strip).
      No async/Stream surface. NEW family idiom (LOAD-BEARING
      because it applies 11 times in this file alone).

  - construct_key: dart.path_split_slash_last_to_csharp_path_getfilename
    source_form: >-
      "dir.listSync().map((e) => e.path.split('/').last)
      .toList() — extract the filename (last path segment) from
      each FileSystemEntity. The Dart source hardcodes the
      POSIX forward-slash split, which silently misbehaves on
      Windows paths returned by Directory.listSync (which on
      Windows include backslash separators)."
    target_decision: >-
      Emit `Directory.EnumerateFileSystemEntries(path).Select(
      Path.GetFileName).ToList()`. `Path.GetFileName(string)`
      is cross-platform-correct (handles `/` on POSIX and both
      `/` and `\` on Windows). This is a TARGETED FIX during
      codegen (NOT a behavioural change in INTENT — the Dart
      source clearly INTENDS "the filename portion"; only the
      MECHANISM is POSIX-locked). Recorded as an explicit
      divergence nuance.
    idiom_id: null
    research_finding_id: rf-dart-path-split-slash-last-to-csharp-path-getfilename
    nuance: >-
      Path-separator nuance (NEW, LOAD-BEARING DIVERGENCE):
      Microsoft Learn "Path.GetFileName(String) Method" —
      "Returns the file name and extension of the specified
      path string. … The characters in the returned string
      after the last directory separator character." The
      cross-platform-correct .NET surface replaces the POSIX-
      only Dart split. The intent-preserving fix is the
      DELIBERATE codegen choice (vs faithful-byte-rendering of
      `path.Split('/')[^1]`, which would PRESERVE the POSIX-
      breakage on Windows — explicitly REJECTED because the
      INTENT is unambiguous and the .NET-canonical replacement
      is exact). NO async surface. NEW idiom.

  - construct_key: dart.bind_or_verify_writer_two_branch_template
    source_form: >-
      "Twelve `if (rt.heap.isWriterBound(wid)) { final
      existingValue = rt.heap.getValue(wid); bool matches =
      false; if (existingValue is ConstTerm && existingValue
      .value == <result>) matches = true; else if (existingValue
      == <result>) matches = true; if (!matches) return
      SystemResult.failure; … return SystemResult.success; } else
      { rt.heap.bindWriterConst(wid, <result>); return
      SystemResult.success; }` branch pairs across the
      predicates. Some variants use bindVariable instead of
      bindWriterConst when binding a Term rather than a
      constant. Some variants use ConstTerm-equality verify;
      others use plain `==`."
    target_decision: >-
      CONSOLIDATE into a single private static helper
      `private static SystemResult BindOrVerifyConst(GlpRuntime
      rt, long wid, object? expectedOrToBind)` on the holder
      class. Body: `if (rt.Heap.IsWriterBound(wid)) { var
      existing = rt.Heap.GetValue(wid); bool matches = existing
      is ConstTerm c && Equals(c.Value, expectedOrToBind) ||
      Equals(existing, expectedOrToBind); return matches ?
      SystemResult.Success : SystemResult.Failure; } rt.Heap
      .BindWriterConst(wid, expectedOrToBind); return
      SystemResult.Success;`. A sibling helper
      `BindOrVerifyTerm(GlpRuntime rt, long wid, Term term)`
      dispatches to `BindVariable` for the Term-shaped binding
      sites (evaluatePredicate, copyTermPredicate). Both
      helpers preserve the dual `bindWriterConst` vs
      `bindVariable` dispatch from the source.
    idiom_id: null
    research_finding_id: rf-dart-bind-or-verify-writer-to-csharp-helper-method
    nuance: >-
      Bind-or-verify consolidation nuance (NEW family idiom,
      LOAD-BEARING — twelve call sites): the helper is emitted
      ONCE on the holder class and called from each predicate
      that binds an output writer. Preserves observable
      semantics (same `isWriterBound`-then-`getValue`-then-
      compare flow on bound writers; same `bindWriterConst` on
      unbound; same `SystemResult.success`/`failure` choice).
      Equality nuance (carry-forward from terms.dart.md): Dart
      `existingValue.value == result` for primitive value
      comparison → C# `Equals(c.Value, expectedOrToBind)` is
      faithful (both languages call value-equality for boxed
      primitives). The dual `ConstTerm.value == result` OR
      `existingValue == result` arms compress to a single
      `Equals(c.Value, expectedOrToBind) || Equals(existing,
      expectedOrToBind)` expression — same observable
      semantics, fewer lines. No async surface. NEW idiom.

  - construct_key: dart.mixed_value_map_literal_string_keyed_dict_for_load_module
    source_form: >-
      "{ 'path': filePath, 'contents': contents, 'loaded_at':
      DateTime.now().millisecondsSinceEpoch } — anonymous Dart
      map literal of string-keyed heterogeneous-value pairs
      bound to a local `final module`. Used as the value passed
      to `rt.heap.bindWriterConst(wid, module)` inside
      loadModulePredicate."
    target_decision: >-
      Emit `var module = new Dictionary<string, object?> {
      ["path"] = filePath, ["contents"] = contents, ["loaded_at"]
      = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };`
      (object initialiser via the indexer pattern — Microsoft
      Learn "Object and Collection Initializers — Dictionary
      Initializers" prescribes `["key"] = value` as the
      canonical idiom). The `object?` value-type is faithful
      to Dart `Object?` for the heterogeneous values (string /
      string / int). Carry-forward of int-width nuance for the
      timestamp.
    idiom_id: null
    research_finding_id: rf-dart-mixed-value-map-literal-to-csharp-dictionary-init
    nuance: >-
      Map-literal nuance (NEW idiom): Dart `{ key: value }` map
      literal of mixed-value-type pairs → C# `Dictionary<string,
      object?>` with object-initialiser indexer syntax.
      Microsoft Learn "Object and Collection Initializers". No
      async, no reference-vs-value complication beyond the
      Dictionary-is-reference-type carry-forward. The
      `loaded_at` value uses the cached
      `rf-dart-datetime-now-ms-to-csharp-dto-utc-unixms` idiom.
      NEW idiom.

  - construct_key: dart.list_string_cast_and_validate_every_string
    source_form: >-
      "In linkPredicate: `if (value.every((e) => e is String))
      modulePaths = value.cast<String>(); else …` — Dart
      List.every predicate-test + List.cast<T>() type-cast for
      a List<dynamic>-to-List<String> narrowing."
    target_decision: >-
      Emit `if (list.All(e => e is string)) modulePaths =
      list.Cast<string>().ToList();` per Microsoft Learn
      `Enumerable.All` (returns true when every element
      satisfies the predicate) + Microsoft Learn `Enumerable
      .Cast<TResult>` (throws InvalidCastException on the
      first non-T element — but we've already verified all
      elements via `All`, so the cast is safe). `.ToList()`
      materialises the lazy `IEnumerable<string>` into a
      `List<string>` matching the Dart `List<String>`
      destination.
    idiom_id: null
    research_finding_id: rf-dart-list-every-cast-to-csharp-linq-all-cast
    nuance: >-
      List.every / List.cast nuance (NEW idiom): Dart
      `List.every(predicate)` → C# `IEnumerable.All(predicate)`
      (LINQ, lazy, short-circuits on first false). Dart
      `List.cast<T>()` → C# `IEnumerable.Cast<T>()` (LINQ, lazy,
      throws on bad element — but here pre-validated by
      `.All`). Microsoft Learn `Enumerable.All<TSource>` and
      `Enumerable.Cast<TResult>`. Value/reference: both
      languages treat List as a reference type; aliasing
      semantics preserved. Null-safety: the cast-target `string`
      is non-nullable; the `is string` predicate filters out
      null. No async. NEW idiom.

  - construct_key: dart.toplevel_docs_with_dartio_mention_to_csharp_xmldoc_namespace
    source_form: >-
      "Top-of-file 7-line triple-slash doc-comment block
      ('Standard system predicate implementations for GLP / /
      This module provides the built-in system predicates that
      can be called / via the Execute instruction. These
      predicates handle: / - Arithmetic evaluation / - File I/O
      operations / - System information (time, IDs, etc.)'). No
      `library;` directive."
    target_decision: >-
      Emit a file-header XML-doc comment on the namespace
      declaration mirroring `lib/runtime/`: `<summary>Standard
      system predicate implementations for GLP</summary>
      <remarks>… <list type="bullet"> <item><description>
      Arithmetic evaluation</description></item>
      <item><description>File I/O operations</description></item>
      <item><description>System information (time, IDs, etc.)
      </description></item> </list></remarks>`. Carry-forward
      of `rf-dart-library-directive-to-csharp-namespace-elision`
      from system_predicates.dart.md / external_io.dart.md /
      heap_fcp.dart.md / suspension.dart.md.
    idiom_id: null
    research_finding_id: rf-dart-library-directive-to-csharp-namespace-elision
    nuance: >-
      Library-directive nuance (cached carry-forward,
      saturated): no `library;` directive present (Dart
      implicit-library); doc-comment block becomes XML-doc on
      the namespace. The doc-comment's mention of "File I/O
      operations" is realised by THIS file's `dart:io` family,
      so the `<list>` is accurate to the file's actual
      capabilities (DIFFERENTIATING from system_predicates.dart
      .md where the same doc-comment topic described downstream
      predicate IMPLEMENTATIONS, not the file itself). FR-024
      cache hit; no new research.

conversion_units:
  - "using directives: `using System;` (Console), `using System.IO;` (File, Directory, FileStream, FileMode, FileAccess, Path), `using System.Text;` (Encoding.UTF8), `using System.Linq;` (.All / .Cast / .Select / .ToList), `using System.Collections.Generic;` (Dictionary, HashSet, List), `using <root>.Runtime;` (covers terms.cs / runtime.cs / system_predicates.cs sibling-file imports)"
  - "file-header XML-doc on namespace declaration: <summary>Standard system predicate implementations for GLP</summary><remarks>…<list type='bullet'>Arithmetic evaluation / File I/O operations / System information (time, IDs, etc.)</list></remarks>"
  - "internal static class SystemPredicatesImpl (file-level holder, NOT instantiable)"
  - "  private static long _uniqueIdCounter = 1L;                                   // Dart `int _uniqueIdCounter = 1;`"
  - "  public static void RegisterStandardPredicates(SystemPredicateRegistry registry) { …16 registry.Register(\"name\", MethodGroup) calls preserving the 7 family-comment groups… }   // Dart `registerStandardPredicates`"
  - "  public static SystemResult EvaluatePredicate(GlpRuntime rt, SystemCall call)              // Dart `evaluatePredicate` — arity 2, collectUnbound walk, then ground-eval, then BindOrVerify"
  - "  public static SystemResult CurrentTimePredicate(GlpRuntime rt, SystemCall call)           // Dart `currentTimePredicate` — arity 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()"
  - "  public static SystemResult UniqueIdPredicate(GlpRuntime rt, SystemCall call)              // Dart `uniqueIdPredicate` — arity 1, _uniqueIdCounter++"
  - "  public static SystemResult VariableNamePredicate(GlpRuntime rt, SystemCall call)          // Dart `variableNamePredicate` — arity 2, 'W{addr}' or 'R{addr}'"
  - "  public static SystemResult CopyTermPredicate(GlpRuntime rt, SystemCall call)              // Dart `copyTermPredicate` — arity 2, DeepCopyTerm with visited map"
  - "  public static SystemResult FileReadPredicate(GlpRuntime rt, SystemCall call)              // Dart `fileReadPredicate` — arity 2, File.ReadAllText"
  - "  public static SystemResult FileWritePredicate(GlpRuntime rt, SystemCall call)             // Dart `fileWritePredicate` — arity 2, File.WriteAllText"
  - "  public static SystemResult FileExistsPredicate(GlpRuntime rt, SystemCall call)            // Dart `fileExistsPredicate` — arity 1, File.Exists"
  - "  public static SystemResult FileOpenPredicate(GlpRuntime rt, SystemCall call)              // Dart `fileOpenPredicate` — arity 3, 4-arm switch → (FileMode,FileAccess) pair → new FileStream"
  - "  public static SystemResult FileClosePredicate(GlpRuntime rt, SystemCall call)             // Dart `fileClosePredicate` — arity 1, rt.IsValidHandle + rt.CloseFileHandle"
  - "  public static SystemResult FileReadHandlePredicate(GlpRuntime rt, SystemCall call)        // Dart `fileReadHandlePredicate` — arity 2, FileStream.Read + Encoding.UTF8.GetString"
  - "  public static SystemResult FileWriteHandlePredicate(GlpRuntime rt, SystemCall call)       // Dart `fileWriteHandlePredicate` — arity 2, Encoding.UTF8.GetBytes + FileStream.Write"
  - "  public static SystemResult DirectoryListPredicate(GlpRuntime rt, SystemCall call)         // Dart `directoryListPredicate` — arity 2, Directory.EnumerateFileSystemEntries + Path.GetFileName"
  - "  public static SystemResult ReadPredicate(GlpRuntime rt, SystemCall call)                  // Dart `readPredicate` — arity 1, Console.In.ReadLine()"
  - "  public static SystemResult LinkPredicate(GlpRuntime rt, SystemCall call)                  // Dart `linkPredicate` — arity 2, rt.LoadLibrary(modulePaths[0])"
  - "  public static SystemResult LoadModulePredicate(GlpRuntime rt, SystemCall call)            // Dart `loadModulePredicate` — arity 2, File.ReadAllText + Dictionary<string,object?> module record"
  - "  public static SystemResult DistributeStreamPredicate(GlpRuntime rt, SystemCall call)      // Dart `distributeStreamPredicate` — arity 2, DeepCopyValue per output writer"
  - "  public static SystemResult CopyTermMultiPredicate(GlpRuntime rt, SystemCall call)         // Dart `copyTermMultiPredicate` — arity 3, two independent DeepCopyValue"
  - "  private static object? Evaluate(GlpRuntime rt, object? term)                              // Dart `_evaluate` — recursive switch on functor (10 arms: +,-,*,/,mod,<,>,=<,>=,=)"
  - "  private static object? DeepCopyTerm(object? term, GlpRuntime rt, Dictionary<long, object?> visited)   // Dart `_deepCopyTerm` — visited-map cycle detection, placeholder-then-overwrite"
  - "  private static object? DeepCopyValue(object? value)                                       // Dart `_deepCopyValue` — shallow-recursive on List/Map/Set, share primitives"
  - "  // CONSOLIDATION HELPERS (emitted once, called 14+/12+ times by the predicates above):"
  - "  private static bool TryExtractString(SystemCall call, int argIndex, GlpRuntime rt, out string? value, out SystemResult earlyReturn)   // three-arm ladder: ConstTerm<string> / VarRef-writer / VarRef-reader-suspend-on-unbound; LOAD-BEARING consolidation"
  - "  private static bool TryExtractInt(SystemCall call, int argIndex, GlpRuntime rt, out long? value, out SystemResult earlyReturn)        // three-arm ladder typed for long (int-width carry-forward)"
  - "  private static bool TryExtractTerm(SystemCall call, int argIndex, GlpRuntime rt, out object? value, out SystemResult earlyReturn)     // three-arm ladder for Object?-typed extraction (evaluate / copyTerm input)"
  - "  private static SystemResult BindOrVerifyConst(GlpRuntime rt, long wid, object? value)            // dual `IsWriterBound` branch — Equals(ConstTerm.Value, value) || Equals(existing, value)"
  - "  private static SystemResult BindOrVerifyTerm(GlpRuntime rt, long wid, Term term)                 // dual branch with Heap.BindVariable on unbound"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-dartio-to-csharp-system-io-family — `dart:io` family mapping (NEW, LOAD-BEARING, official-docs grounded across nine surfaces)

- Deep analysis. This file is the first runtime/* file to exercise the FULL `dart:io` synchronous surface (`external_io.dart.md`, `runner.dart.md`, and `repl_play_runner.dart.md` exercised partial surfaces — print/stdin/process — but not the file-system / random-access / directory / encoding family). Nine distinct API call sites converge on the same family decision: `File.ReadAllText`, `File.WriteAllText`, `File.Exists`, `new FileStream(path, FileMode, FileAccess)`, `Directory.Exists`, `Directory.EnumerateFileSystemEntries`, `Path.GetFileName`, `Console.In.ReadLine`, `Encoding.UTF8.GetString` / `Encoding.UTF8.GetBytes`, and the carry-forward `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds`. Consolidating into ONE family idiom (rather than nine separate idioms) keeps the KB compact and matches how downstream files will exercise the same family.
- Authoritative Dart. dart.dev / libraries / dart:io — `File`, `Directory`, `RandomAccessFile`, `FileMode`, `stdin`, `Platform` documented as the synchronous-and-async file-system / I/O surface. `String.fromCharCodes` — api.dart.dev — "Allocates a new String for the specified array of code points."
- Authoritative .NET. Microsoft Learn "File.ReadAllText Method"; "File.WriteAllText Method"; "File.Exists Method"; "FileStream Constructor (String, FileMode, FileAccess)"; "Directory.Exists Method"; "Directory.EnumerateFileSystemEntries Method"; "Path.GetFileName(String) Method"; "Console.In Property" + "TextReader.ReadLine Method"; "Encoding.UTF8 Property" + "Encoding.GetString(Byte[])" + "Encoding.GetBytes(String)". All grounding from `learn.microsoft.com/en-us/dotnet/api/...`.
- Conclusion. ONE family research finding covering all nine surfaces; per-site idioms (e.g. the `FileMode`/`FileAccess` decomposition) are recorded as their own construct rows but pin back to this family finding. Encoding default = UTF-8 (faithful to Dart `writeStringSync` default + Dart `String.fromCharCodes` round-trip semantics for the bytes produced by `writeStringSync`). Path-separator cross-platform-correctness via `Path.GetFileName` (replaces POSIX-only `.path.split('/').last`). NEW family idiom; FR-024 official-docs grounded.

### rf-dart-filemode-to-csharp-filemode-fileaccess-pair — `FileMode` enum decomposition (NEW, LOAD-BEARING DIVERGENCE)

- Deep analysis. Dart `FileMode.read` / `.write` / `.append` / `.writeOnly` collapses access intent and create-or-open intent into one tag. The Dart source's `'read_write'` arm-text maps to `FileMode.writeOnly` — a likely typo (writeOnly is not read+write); spec preserves the source surface AS-WRITTEN and emits the .NET-canonical `(FileMode.Create, FileAccess.ReadWrite)` for the apparent INTENT.
- Authoritative .NET. Microsoft Learn "FileMode Enum" — Open / OpenOrCreate / Create / CreateNew / Append / Truncate. Microsoft Learn "FileAccess Enum" — Read / Write / ReadWrite. The `FileStream(string, FileMode, FileAccess)` ctor requires BOTH; the decomposition is canonical.
- Conclusion. Four-arm switch emits the explicit pair per case; preserves Dart shape; intent-faithful for the `'read_write'` quirk. NEW idiom.

### rf-dart-repeated-three-arm-term-extraction-to-csharp-helper — extraction-template consolidation (NEW, LOAD-BEARING)

- Deep analysis. Fourteen+ call sites share the same ConstTerm-typed / VarRef-writer-bound / VarRef-reader-suspend-on-unbound three-arm ladder, ~30 lines each, plus the post-extraction null-check + diagnostic. Without consolidation, the converted file would be ~3× the source size (the C# verbosity overhead amplifies the duplication). The helper preserves observable semantics (same suspension/failure dispatch, same diagnostic prefixes) and is a faithful render of the SHAPE — not a refactor.
- Authoritative .NET. Microsoft Learn "out parameter modifier" — "The `out` keyword causes arguments to be passed by reference. … When you use the `out` keyword, the called method is required to assign a value to the parameter before the method returns." The TryX-pattern with `bool` retval + `out T value` + `out SystemResult earlyReturn` is the .NET-canonical idiom for "result-or-early-return".
- Conclusion. Two helpers (`TryExtractString`, `TryExtractInt`, `TryExtractTerm` — three variants for type-narrowed extraction) emitted ONCE on the holder class. NEW idiom.

### rf-dart-bind-or-verify-writer-to-csharp-helper-method — bind-or-verify template consolidation (NEW, LOAD-BEARING)

- Deep analysis. Twelve+ call sites share the same `isWriterBound ? verify : bind` dual branch with the same ConstTerm-equality-OR-plain-equality matching logic. Same consolidation rationale as the extraction template above.
- Authoritative .NET. Microsoft Learn `object.Equals(object, object)` — "Determines whether the specified object instances are considered equal" — the boxed-equality semantics match Dart `==`.
- Conclusion. `BindOrVerifyConst` + `BindOrVerifyTerm` helpers (dual to match the source's `bindWriterConst` vs `bindVariable` dispatch). NEW idiom.

### rf-dart-switch-on-string-to-csharp-switch-expression — `_evaluate` arithmetic-switch (cached carry-forward, reuse)

- Carry-forward from body_kernels.dart.md (cited verbatim in `_evaluate`'s ten-arm switch on operator names). Dart `switch (string)` → C# `switch (string)`; per-arm `is num`-narrow → `is double`-narrow (with `mod` narrowed to `long`); div-by-zero check returns null in both languages. FR-024 cache hit; no new research.

### rf-dart-num-hierarchy-to-csharp-double-with-int-discriminator — `_evaluate` num-mixed arithmetic (cached carry-forward, reuse)

- Carry-forward from body_kernels.dart.md. Dart `num` (supertype of int and double) renders as `double` (with `int` narrow for `mod`). FR-024 cache hit.

### rf-dart-cycle-aware-deepcopy-visited-map-to-csharp-equivalent — `_deepCopyTerm` (NEW, LOAD-BEARING)

- Deep analysis. The visited-map placeholder-then-overwrite pattern is the canonical graph-traversal cycle-breaking idiom; faithful 1:1 to Dart. The `Dictionary` indexer is safe here because it's guarded by `ContainsKey` (DIFFERENTIATING from system_predicates.dart.md's registry case which used TryGetValue because the get was unguarded).
- Authoritative .NET. Microsoft Learn `Dictionary<TKey,TValue>.Item[TKey]` — "Gets or sets the value associated with the specified key. … `KeyNotFoundException`: The property is retrieved and `key` does not exist in the collection." Explicitly cites the ContainsKey-guard as the safe-usage pattern.
- Conclusion. `Dictionary<long, object?> visited` + `ContainsKey`-guarded indexer get/set. NEW idiom.

### rf-dart-mutable-toplevel-counter-postincrement-to-csharp-static-field — `_uniqueIdCounter` (NEW)

- Deep analysis. Single-threaded ID generator with post-increment. The runtime is single-threaded per the inherited threading-model decision; no `Interlocked.Increment` retrofit. Int-width carry-forward (`long`).
- Authoritative Dart. dart.dev / language / variables — "Top-level mutable variables behave like global state in a single-isolate Dart program." Authoritative .NET. Microsoft Learn "static (C# Reference)" — "Use the `static` modifier to declare a static member, which belongs to the type itself rather than to a specific object."
- Conclusion. `private static long _uniqueIdCounter = 1L;` on the holder class. NEW idiom.

### rf-dart-trycatch-untyped-with-error-print-to-csharp-catch-exception-stderr — exception-handling family (NEW)

- Deep analysis. Eleven try/catch sites all use untyped `catch (e)` + `print('[ERROR] ...: $e')` + `return SystemResult.failure`. The `[ERROR]` prefix signals stderr-channel intent (carry-forward from repl_play_runner.dart.md).
- Authoritative Dart. dart.dev / language / error-handling — "If the exception that is thrown isn't a predefined type … just write `catch (e)` to catch any thrown object." Authoritative .NET. Microsoft Learn "try-catch — Exceptions in C#" — `catch (Exception ex)` catches every CLR exception. Microsoft Learn "Console.Error Property" — "Gets the standard error output stream."
- Conclusion. `catch (Exception ex)` + `Console.Error.WriteLine($"[ERROR] ...: {ex.Message}")` + `return SystemResult.Failure`. The `~30` non-try/catch `[ERROR]`-prefixed prints across the file route to `Console.Error.WriteLine` for consistency. NEW family idiom.

### rf-dart-path-split-slash-last-to-csharp-path-getfilename — directory-list filename extraction (NEW, LOAD-BEARING DIVERGENCE)

- Deep analysis. The Dart source's POSIX-only `.path.split('/').last` silently breaks on Windows; the .NET-canonical `Path.GetFileName` is cross-platform-correct. INTENT-preserving fix (NOT a behavioural change in intent — the Dart source clearly intends "the filename portion"; only the MECHANISM is POSIX-locked).
- Authoritative .NET. Microsoft Learn "Path.GetFileName(String) Method" — "Returns the file name and extension of the specified path string." Cross-platform separator handling per Microsoft Learn `Path.DirectorySeparatorChar`.
- Conclusion. `Directory.EnumerateFileSystemEntries(path).Select(Path.GetFileName).ToList()`. NEW idiom.

### rf-dart-mixed-value-map-literal-to-csharp-dictionary-init — `loadModule` module-record map literal (NEW)

- Deep analysis. Dart `{ 'path': filePath, 'contents': contents, 'loaded_at': nowMs }` is a `Map<String, Object>` with mixed value types; the C# canonical render is a `Dictionary<string, object?>` with object-initialiser indexer syntax.
- Authoritative .NET. Microsoft Learn "Object and Collection Initializers — Dictionary Initializers" — `new Dictionary<string, int> { ["key1"] = 1, ["key2"] = 2 }`. Faithful 1:1.
- Conclusion. `new Dictionary<string, object?> { ["path"] = filePath, ["contents"] = contents, ["loaded_at"] = nowMs }`. NEW idiom.

### rf-dart-list-every-cast-to-csharp-linq-all-cast — `linkPredicate` modulePaths validation (NEW)

- Deep analysis. Dart `List.every(predicate)` + `List.cast<T>()` → C# LINQ `.All` + `.Cast<T>()`. Pre-validation by `.All` makes the cast safe (no InvalidCastException can occur).
- Authoritative .NET. Microsoft Learn `Enumerable.All<TSource>` — "Determines whether all elements of a sequence satisfy a condition." Microsoft Learn `Enumerable.Cast<TResult>` — "Casts the elements of an `IEnumerable` to the specified type. … `InvalidCastException`: An element in the sequence cannot be cast to type `TResult`."
- Conclusion. `if (list.All(e => e is string)) modulePaths = list.Cast<string>().ToList();`. NEW idiom.

### rf-dart-static-only-holder-to-csharp-static-class — registerStandardPredicates + all predicate functions (cached carry-forward, reuse)

- Carry-forward from body_kernels.dart.md / external_io.dart.md. The whole file's surface (file-level functions + private helpers) hosts on `internal static class SystemPredicatesImpl`. FR-024 cache hit.

### rf-dart-library-directive-to-csharp-namespace-elision — top-of-file doc-comment (cached carry-forward, reuse)

- Carry-forward from system_predicates.dart.md / external_io.dart.md. Doc-comment block → XML-doc on namespace declaration. FR-024 cache hit.

### rf-dart-import-relative-to-csharp-using-namespace — relative imports (cached carry-forward, reuse — saturated)

- Carry-forward from system_predicates.dart.md / external_io.dart.md / heap_fcp.dart.md. Three relative imports collapse to one `using <root>.Runtime;`. FR-024 cache hit.

### rf-dart-typedef-function-to-csharp-delegate — predicate function tear-offs (cached carry-forward, reuse)

- Carry-forward from system_predicates.dart.md. The sixteen `registry.register(name, fn)` calls are method-group conversions of the bare predicate names to the `SystemPredicate` delegate. FR-024 cache hit.

### rf-dart-datetime-now-ms-to-csharp-dto-utc-unixms — `current_time/1` + `load_module/2` timestamp (cached carry-forward, reuse)

- Carry-forward from body_kernels.dart.md. `DateTime.now().millisecondsSinceEpoch` → `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`. FR-024 cache hit.

## Notes

- This file is LARGE (1927 lines) but consolidates into a relatively small set of construct families. The repetitive Writer/Reader/Const extraction ladder and the bind-or-verify branch pair together account for ~70% of the source bulk; consolidating them into helper methods is LOAD-BEARING for tractability.
- Threading model decision (inherited): synchronous predicate dispatch — every Dart `*Sync` I/O call maps to a blocking-synchronous .NET call (NOT `*Async`); no `Task<T>` retrofit. The inherited decision is NOT re-escalated.
- `dart:io` family covers nine distinct API surfaces consolidated into ONE NEW idiom (`rf-dart-dartio-to-csharp-system-io-family`) — official-docs grounded across all nine.
- The Dart source's POSIX-locked `.path.split('/').last` is fixed during codegen via `Path.GetFileName` — INTENT-preserving (the Dart source clearly intends "the filename portion"); recorded explicitly as a path-separator divergence nuance.
- The Dart source's `'read_write'` arm mapping to `FileMode.writeOnly` is a likely source typo; the .NET render emits `(FileMode.Create, FileAccess.ReadWrite)` for the apparent INTENT and records the quirk as a divergence note (NOT escalated — the spec faithfully captures source surface + target intent).
- The `[EVALUATE]` debug prints in `evaluatePredicate` (9 of them) are noted as "trace-print decision deferred" — the codegen stage may preserve, gate behind `#if DEBUG`, or strip them; this is a downstream-codegen call, NOT an escalation.
- Zero escalations: every non-trivial construct resolved from authoritative Dart (dart.dev / api.dart.dev) and/or .NET (learn.microsoft.com) official documentation. Cached carry-forward idioms reused for the recurring patterns (relative-import, namespace-elision, plain-enum already in system_predicates.dart.md so not repeated here; static-holder, switch-on-string, num-hierarchy, postincrement, datetime-now-ms, typedef-to-delegate); ten NEW idioms registered for this file's first-seen constructs (the dart:io family, the FileMode/FileAccess decomposition, the extraction-template consolidation, the bind-or-verify consolidation, the cycle-aware deep-copy, the mutable top-level counter, the try-catch-error-print family, the path-split-slash-last to Path.GetFileName fix, the mixed-value map-literal initializer, and the LINQ all-cast list narrowing).
- FR-009/FR-010 quality bar satisfied: every non-trivial construct has BOTH a deep-analysis basis AND a researched-pattern basis (or an explicit carry-forward `research_finding_id`). Threading-model, value-vs-reference, null-safety, and async/Stream/isolate nuances are each addressed at least once across the construct rows (threading: predicate-template + dart:io family; value/reference: extraction-helper + bind-or-verify-helper + Dictionary/HashSet carry-forwards; null-safety: `object?` / `string?` / `T?` carry-forwards across helpers; async/Stream/isolate: explicitly ABSENT and noted).
