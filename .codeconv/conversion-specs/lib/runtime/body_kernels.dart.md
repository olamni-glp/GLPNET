# Conversion Spec — lib/runtime/body_kernels.dart

> Conversion-spec artifact for lib/runtime/body_kernels.dart (FR-011).
> Spec-only (FR-023): describes the Dart→C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> Body kernels are runtime-implemented predicates that execute inline
> (not spawned as separate goals), have two-valued semantics (success
> or abort), and are only accessible to system predicates (assign.glp).
> The file declares: (i) a two-value enum `BodyKernelResult`, (ii) a
> Dart `typedef` for a delegate-style kernel-function signature, (iii)
> a `BodyKernelRegistry` keyed by `name/arity`, (iv) one registration
> entry-point `registerStandardBodyKernels`, (v) a tier of arithmetic
> + math + type-conversion + structure + stream-append + madGLP +
> output + module-dispatch kernels, plus private helpers `_getNum`,
> `_evaluateArithmetic`, `_bindResult`, `_deref`, `_deepDeref`,
> `_dartListToGlpList`, `_glpListToDartList`, and the public helper
> `formatGroundTerm`.
>
> Heavy idiom reuse: every cross-file dependency (`HeapFCP`, the
> `Term` sum hierarchy `ConstTerm`/`StructTerm`/`VarRef`/`MutualRefTerm`
> /`ModuleTerm`, `GoalRef`, `BytecodeRunner`/`BytecodeProgram`/`CallEnv`,
> `MadContext`) inherits the convspec decisions already pinned in
> heap_fcp.dart.md, terms.dart.md, machine_state.dart.md,
> external_io.dart.md, suspension.dart.md, commit.dart.md,
> suspend_ops.dart.md (and the multiagent specs for MadContext). The
> threading-model decision is inherited from those upstream specs —
> NOT re-escalated here (FR-013 discipline: don't double-escalate a
> previously-escalated decision).
>
> Load-bearing nuances exercised by THIS file: (a) Dart `typedef`
> for a function signature → C# `delegate` declaration (functional
> signature, the conversion's preferred "first-class kernel" shape);
> (b) Dart `enum BodyKernelResult { success, abort }` → C# `enum` —
> a plain two-valued enum (NOT a discriminated-union: no payload);
> (c) Dart `is`-test type-narrowing on the `Term` sum hierarchy (16+
> sites: `arg is num`, `arg is ConstTerm`, `arg is VarRef`, `arg is
> StructTerm`, `term is VarRef`, `current is VarRef`, `current is
> StructTerm`, `refArg is MutualRefTerm`, `moduleArg is ModuleTerm`,
> `goalArg is StructTerm`, etc.) → C# type-pattern with explicit
> capture variable (idiom carry-forward from external_io.dart.md
> `rf-dart-is-test-narrowing-to-csharp-type-pattern-capture`); (d)
> Dart Map<String, BodyKernel> with string-key `'$name/$arity'`
> composition → C# `Dictionary<string, BodyKernel>` (carry-forward
> idiom from suspension.dart.md `rf-dart-map-to-csharp-dictionary`);
> (e) Dart switch-on-string returning nullable num → C# switch-
> expression returning `double?` (no fall-through, expression-form
> faithful to the Dart arrow-bodied switch); (f) Dart record
> destructuring `final (newTailWriter, newTailReader) = rt.heap
> .allocateVariable();` → C# tuple deconstruction `var (a, b) =
> Rt.Heap.AllocateVariable();` (carry-forward from
> external_io.dart.md `rf-dart-record-destructure-to-csharp-
> valuetuple-deconstruction`); (g) numeric-discipline nuance — Dart
> `num`/`int`/`double` hierarchy collapses to a single dynamic
> numeric supertype in Dart, but .NET has NO common runtime-
> overloadable numeric supertype before .NET 7's static-virtual
> `INumber<T>` — the faithful render uses `double` for arithmetic
> results AND an `is int`-vs-`is double` discriminator where
> integer-only kernels (idiv, mod) demand exact-integer operands
> (NEW load-bearing idiom `rf-dart-num-hierarchy-to-csharp-
> double-with-int-discriminator`); (h) Dart top-level functions
> hosted on a static class (idiom carry-forward from
> external_io.dart.md `rf-dart-top-level-fn-builds-sum-type-leaf`
> / boot_loader.dart.md `rf-dart-static-only-holder-to-csharp-
> static-class`); (i) Dart `'$name/$arity'` string interpolation
> → C# `$"{name}/{arity}"` (idiom carry-forward from
> external_io.dart.md `rf-dart-tostring-interp-to-csharp-tostring-
> interp`); (j) `print('[ABORT] ...')` → C# `Console.WriteLine`
> (the file uses `dart:io` via Dart's top-level `print` builtin —
> a documented `dart:core` re-export of `dart:io.stdout.writeln` —
> faithfully maps to `System.Console.WriteLine`); (k) `DateTime
> .now().millisecondsSinceEpoch` → `DateTimeOffset.UtcNow
> .ToUnixTimeMilliseconds()` (NEW load-bearing idiom
> `rf-dart-datetime-now-ms-to-csharp-dto-utc-unixms`); (l)
> `math.sin`/`cos`/`tan`/`exp`/`log`/`pow`/`sqrt`/`asin`/`acos`/
> `atan` (the imported `dart:math` library) → `System.Math.*`
> faithful one-to-one (NEW carry-forward idiom
> `rf-dart-math-library-to-csharp-system-math`); (m) Dart `x ~/ y`
> (integer-division operator on `int`) → C# integer division for
> `int`/`long` operands (`(long)(x / y)` for `int`, since C# `/`
> on integers IS integer division — the Dart `~/` operator does
> NOT have a direct C# spelling but has identical semantics on
> integers); (n) Dart `x % y` on integers → C# `%` (same
> truncated-modulo semantics for non-negative operands, BUT Dart
> and C# diverge on negative-operand modulo: Dart `%` is Euclidean
> (always non-negative); C# `%` follows sign of dividend — the
> spec records this divergence and pins C# `%` because: the
> kernel's preconditions are guarded by `assign.glp` callers per
> the file's leading doc comment ("Expect all preconditions met
> (guards should verify before calling)"); the existing GLP
> semantics on negative operands are not specified by the file
> itself and would require a separate spec amendment to pin —
> the spec EXPLICITLY records this as a NUANCE-without-
> escalation, deferring to a callsite-level spec amendment if
> divergence becomes load-bearing); (o) Dart `(GlpRuntime, List<
> Object?>)`-taking delegate signature, with `Object?` element
> type → C# `Func<GlpRuntime, IReadOnlyList<object?>,
> BodyKernelResult>` delegate (`object?` matches Dart `Object?`
> faithfully under enabled NRT); (p) Dart pattern `args[i] as
> Term` → C# `(Term)args[i]` (idiom carry-forward from suspension
> .dart.md / heap_fcp.dart.md `rf-dart-as-cast-to-csharp-
> explicit-cast`); (q) Dart `value is ConstTerm && value.value is
> num` (nested-`is` on the sum-type's payload field) → C# `value
> is ConstTerm ct && ct.Value is double n` (constant pattern's
> sibling: type pattern with capture on the inner field — and
> the `num` → `double` mapping is load-bearing per the (g)
> nuance above); (r) Dart `var (a, b) = rt.heap.allocateVariable
> ()` is the only multi-element record-destructure callsite
> (`streamAppendKernel`); (s) Dart `for-in` over `List<GoalRef>`
> activations enqueuing into `rt.gq` → C# `foreach (var act in
> activations) Rt.Gq.Enqueue(act);` faithful one-to-one (idiom
> carry-forward from suspension.dart.md / external_io.dart.md
> for-in iteration).

```yaml
schema_version: 1
source_path: lib/runtime/body_kernels.dart
source_sha256: 9d360613abdb60c46d883ad215633020e879fefa7f3d422f319dac02fb7063ba
target_code_unit: lib/runtime/body_kernels.cs
constructs:
  - construct_key: dart.import_directive.dart_math_as_math
    source_form: >-
      `import 'dart:math' as math;` -- a core-library import of
      `dart:math` with a prefix alias `math`. Used as `math.sqrt`,
      `math.sin`, `math.cos`, `math.tan`, `math.exp`, `math.log`,
      `math.ln10`, `math.pow`, `math.asin`, `math.acos`, `math.atan`
      across the math-function kernel block (12 callsites).
    target_decision: >-
      Emit `using System;` (covers `System.Math`) at the top of the
      converted `body_kernels.cs`. Every `math.X` callsite maps to
      `Math.X`: `math.sqrt(x)` → `Math.Sqrt(x)`, `math.sin(x)` →
      `Math.Sin(x)`, `math.cos(x)` → `Math.Cos(x)`, `math.tan(x)` →
      `Math.Tan(x)`, `math.exp(x)` → `Math.Exp(x)`, `math.log(x)` →
      `Math.Log(x)` (the Dart `math.log` is natural log per
      api.dart.dev; matches `Math.Log(x)`'s default natural-log
      semantics), `math.ln10` → `Math.Log(10.0)` (NO direct `LN10`
      constant in `System.Math` — `Math.Log(10.0)` evaluates the
      same IEEE-754 constant; equivalent at runtime), `math.pow(x,
      y)` → `Math.Pow(x, y)`, `math.asin(x)` → `Math.Asin(x)`,
      `math.acos(x)` → `Math.Acos(x)`, `math.atan(x)` →
      `Math.Atan(x)`. The Dart prefix alias `math` has no .NET
      equivalent at the using-directive level (no per-namespace
      alias rename is needed because `System.Math` is the only
      math import); the conversion drops the alias and reaches
      static members on `Math` directly.
    idiom_id: null
    research_finding_id: rf-dart-math-library-to-csharp-system-math
    nuance: >-
      Numeric-result nuance: every `dart:math` function returns
      `double`; `System.Math.*` likewise returns `double` — exact
      shape match. Domain-error nuance: Dart `math.log(0)` returns
      `-infinity` (no throw); `Math.Log(0)` likewise returns
      `double.NegativeInfinity` — semantically identical. Dart
      `math.sqrt(-1)` returns `NaN`; `Math.Sqrt(-1)` likewise
      returns `double.NaN` — semantically identical. The kernels
      pre-guard negative `sqrt`, `<=0` `ln`/`log10`, and out-of-
      range `asin`/`acos` before calling `math.X`, so domain-
      sensitivity is callsite-defensive — preserved verbatim in
      the C# render. `math.ln10` constant nuance: Dart exposes
      `ln10` as a top-level constant; .NET has no `Math.LN10`
      (only `Math.E` and `Math.PI`); the faithful render is
      `Math.Log(10.0)` which produces the same IEEE-754 value
      (corroborating note: `dart:math` source defines
      `const ln10 = 2.302585092994046;` — exactly the value of
      `Math.Log(10.0)`). No value-vs-reference / null-safety /
      async / isolate surface implicated.

  - construct_key: dart.import_directive.relative-same-package.runtime
    source_form: >-
      `import 'runtime.dart';` -- relative same-package import.
      Brings the `GlpRuntime` reference-type class (with public
      surface `heap`, `gq`, `madContext`, `outputCallback`,
      `runners`, `nextGoalId`, `setGoalEnv`, `setGoalProgram`)
      into scope. Consumed by every kernel as the first parameter
      `GlpRuntime rt`.
    target_decision: >-
      Emit a `using <root>.Runtime;` directive naming the namespace
      hosting the converted `runtime.cs`. The actual namespace
      name is decided by the downstream depgraph/namespace step.
      The public surface methods on `GlpRuntime` are pinned by
      glp_runtime.dart.md (carry-forward; this spec consumes
      those decisions, does NOT re-derive them).
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-namespace-using
    nuance: >-
      Same import-unit / show-hide-absent profile as suspend_ops
      .dart.md / external_io.dart.md. Cross-file dependency
      nuance: the `GlpRuntime` surface (`Heap`, `Gq`, `MadContext`,
      `OutputCallback`, `Runners`, `NextGoalId`, `SetGoalEnv`,
      `SetGoalProgram`) is load-bearing here and is pinned by
      glp_runtime.dart.md.

  - construct_key: dart.import_directive.relative-same-package.terms
    source_form: >-
      `import 'terms.dart';` -- relative same-package import.
      Brings the `Term` sum-hierarchy root, the leaves `ConstTerm`,
      `StructTerm`, `VarRef`, plus `MutualRefTerm` and `ModuleTerm`
      (referenced via `is`-tests and constructor calls).
    target_decision: >-
      Emit a `using <root>.Runtime;` directive naming the namespace
      hosting the converted `terms.cs`. The `Term` sum-hierarchy
      and the five referenced leaves are pinned by terms.dart.md
      (sealed abstract base `Term` with sealed leaf classes per
      `rf-dart-abstract-marker-base-to-csharp-abstract-sealed-
      leaves`). Constructor calls `ConstTerm('nil')`,
      `StructTerm('.', [head, tail])`, `VarRef(addr)`,
      `MutualRefTerm(addr)` map to `new ConstTerm("nil")`,
      `new StructTerm(".", new List<Term> { head, tail })`,
      `new VarRef(addr)`, `new MutualRefTerm(addr)` — all faithful
      to the construction shapes pinned by terms.dart.md.
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-namespace-using
    nuance: >-
      Reference-identity load-bearing nuance (carry-forward from
      terms.dart.md): `Term` and every leaf MUST be reference
      types (sealed `class`, NOT `record`, NOT `struct`) so that
      heap-stored term references retain identity. This spec does
      NOT re-decide that mapping.

  - construct_key: dart.import_directive.relative-same-package-with-show.machine_state
    source_form: >-
      `import 'machine_state.dart' show GoalRef;` -- relative
      same-package import with a `show` allow-list restricting
      the imported surface to `GoalRef`. `GoalRef` is consumed in
      `_bindResult` (`final List<GoalRef> activations`),
      `streamAppendKernel` (same), `mutualRefCloseKernel` (same),
      and `activateKernel` (`rt.gq.enqueue(GoalRef(newGoalId,
      entryPc))`).
    target_decision: >-
      Emit a `using <root>.Runtime;` directive naming the namespace
      hosting the converted `machine_state.cs`. The Dart `show
      GoalRef` allow-list narrows the symbol surface — C# has NO
      per-symbol `using` allow-list (only namespace-level
      imports); the `show` clause is ELIDED at the `using`-
      directive level. Faithful: the wider namespace surface is
      imported, but the file references only `GoalRef`. Idiom
      carry-forward from suspend_ops.dart.md / suspension.dart.md
      (the spec records this as no-data-loss because Dart `show`
      is a programmer hint for compilation-unit hygiene, not a
      semantic restriction). `GoalRef` constructor calls
      `GoalRef(newGoalId, entryPc)` map to `new GoalRef(newGoalId,
      entryPc)` per machine_state.dart.md.
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-namespace-using
    nuance: >-
      Show-clause nuance: Dart `show X` restricts the imported
      surface; C# has no equivalent. Faithful to elide. No
      value/reference/async/null-safety surface.

  - construct_key: dart.import_directive.package_uri.bytecode_runner_with_show
    source_form: >-
      `import 'package:glp_runtime/bytecode/runner.dart' show
      BytecodeRunner, BytecodeProgram, CallEnv;` -- package-URI
      import with a three-symbol `show` allow-list. Brings
      `BytecodeRunner` (constructed in `activateKernel`),
      `BytecodeProgram` (used in an `is`-test and via `.labels`
      lookup), and `CallEnv` (constructed via `CallEnv(args:
      argSlots)`).
    target_decision: >-
      Emit a `using <root>.Bytecode;` directive naming the
      namespace hosting the converted `runner.cs`. The three
      referenced symbols `BytecodeRunner`, `BytecodeProgram`,
      `CallEnv` are pinned by bytecode/runner.dart.md (carry-
      forward; this spec consumes those decisions). Constructor
      calls `BytecodeRunner(bytecode)` → `new BytecodeRunner(
      bytecode)`; `CallEnv(args: argSlots)` → `new CallEnv(args:
      argSlots)` (C# named-argument syntax identical to Dart).
      `bytecode.labels[label]` → `bytecode.Labels[label]` (the
      `labels` getter on `BytecodeProgram` is a `Map<String, int>`
      per the upstream spec; C# `Dictionary<string, long>` with
      indexer access).
    idiom_id: null
    research_finding_id: rf-dart-import-relative-to-csharp-using-namespace
    nuance: >-
      Package-URI nuance: Dart `package:NAME/path.dart` resolves
      via the package map; C# `using` resolves via assembly +
      namespace. Faithful 1:1 mapping at the namespace level.
      Show-clause nuance: identical to the machine_state import
      — Dart `show` ELIDED in C#. No value/reference/async/null-
      safety surface implicated by the import directive itself.

  - construct_key: dart.import_directive.package_uri.mad_context
    source_form: >-
      `import 'package:glp_runtime/multiagent/mad_context.dart';`
      -- package-URI import without `show`. Brings the
      `MadContext` reference-type class into scope (consumed in
      `sendKernel` via an `is MadContext`-test on
      `rt.madContext`, then via `ctx.send(termArg, isWriter,
      gnAgent, gnIndex, destAgent)`).
    target_decision: >-
      Emit a `using <root>.MultiAgent;` directive naming the
      namespace hosting the converted `mad_context.cs`. The
      `MadContext` reference-type class and its `send(...)`
      method surface are pinned by multiagent/mad_context.dart.md
      (carry-forward; this spec consumes those decisions, does
      NOT re-derive them — and notably the threading-model
      decision for `send` is inherited from upstream, NOT re-
      escalated here per the FR-013 "don't double-escalate"
      discipline).
    idiom_id: null
    research_finding_id: rf-dart-import-relative-to-csharp-using-namespace
    nuance: >-
      Threading-model inheritance nuance (LOAD-BEARING for this
      file's discipline): the convspec for mad_context.dart.md
      has already decided the threading-model treatment for
      `MadContext.send(...)` (or has escalated it). This spec
      MUST NOT re-decide that — it CONSUMES the upstream
      decision verbatim. Per the user's task statement:
      "Threading-model decision inherited (don't double-
      escalate)." No further analysis required at this site.

  - construct_key: dart.enum.two_valued_no_payload
    source_form: >-
      "enum BodyKernelResult { success, abort } — a plain Dart
      enum with two value-tags (`success`, `abort`) and no
      payload, no doc-commented annotations other than the
      surface triple-slash doc comments on the enum and on each
      tag."
    target_decision: >-
      Emit a C# `public enum BodyKernelResult { Success, Abort }`
      in the same namespace as the converted file. Two enum
      members PascalCased (Dart `success`/`abort` → C# `Success`
      /`Abort`) — faithful one-to-one to the Dart shape. No
      backing type override (default `int` is correct — the enum
      has only two tags). NO `record`, NO sealed-class
      discriminated-union — the enum is genuinely payload-free.
      Triple-slash doc comments preserved as C# XML-doc comments
      (`/// <summary>...</summary>`).
    idiom_id: null
    research_finding_id: rf-dart-plain-enum-to-csharp-enum
    nuance: >-
      Plain-enum nuance (carry-forward idiom from machine_state
      .dart.md / cells.dart.md): a payload-free Dart `enum {
      a, b }` is shape-identical to a C# `enum { A, B }`. The
      faithful render preserves: declaration order (so default-
      assigned underlying values match: `Success = 0`, `Abort
      = 1`), case style (PascalCased), and the binary
      exhaustiveness (no `default:` case is needed at switch
      sites because there are only two tags). No null-safety,
      async, reference-vs-value, or other surface implicated.

  - construct_key: dart.typedef.function_signature_two_arg_returning_enum
    source_form: >-
      "typedef BodyKernel = BodyKernelResult Function(GlpRuntime
      rt, List<Object?> args);" -- a Dart function-type alias
      declaring the kernel-function signature. Two parameters
      (`GlpRuntime` reference + `List<Object?>` heterogeneous
      argument list), one return value (`BodyKernelResult` enum).
    target_decision: >-
      Emit a C# `public delegate BodyKernelResult BodyKernel(
      GlpRuntime rt, IReadOnlyList<object?> args);` in the same
      namespace. Dart function-type `typedef` → C# `delegate`
      (the C# delegate is the .NET first-class equivalent of a
      function reference). Parameter shape: `GlpRuntime rt`
      preserved verbatim; `List<Object?> args` → `IReadOnlyList<
      object?> args` (idiom carry-forward from external_io.dart
      .md and boot_loader.dart.md: read-only-view for the caller
      surface where the callee does NOT mutate the list — every
      kernel iterates `args` without mutation, faithful match).
      Return type `BodyKernelResult` faithful one-to-one.
      ALTERNATIVE REJECTED: `Func<GlpRuntime, IReadOnlyList<
      object?>, BodyKernelResult>` would be a structurally
      equivalent generic delegate, but the named `delegate
      BodyKernel(...)` form is the faithful render of Dart's
      named `typedef`: the name appears in registry signatures
      (`Map<String, BodyKernel>` → `Dictionary<string, BodyKernel
      >`), and a named delegate gives strictly better diagnostic
      messages than `Func<...>` on lookup failures.
    idiom_id: null
    research_finding_id: rf-dart-typedef-function-to-csharp-delegate
    nuance: >-
      Typedef-function nuance (NEW, LOAD-BEARING): Dart
      `typedef X = R Function(A a, B b);` declares a named
      function-type alias. C# has TWO candidate renders: (a) a
      named `delegate R X(A a, B b);` declaration, OR (b) a
      `using X = System.Func<A, B, R>;` alias. The faithful
      render is (a) `delegate` because Dart `typedef` of a
      function type is semantically a NAMED type with its own
      identity (not a structural alias); .NET `delegate` is the
      same — named type with its own identity, distinct from
      `Func<A, B, R>` even at identical shape. Microsoft Learn
      "delegate (C# Reference)" pins delegates as "a type that
      represents references to methods with a particular
      parameter list and return type" — directly analogous to
      Dart `typedef` of a function. Nullability nuance: Dart
      `List<Object?>` (NON-nullable list of nullable `Object?`
      elements) → C# `IReadOnlyList<object?>` (non-nullable
      list, nullable element). The kernel parameter `args[i]`
      is then a nullable `object?` value in the C# body —
      faithful 1:1 to Dart `args[i]`. Reference-vs-value: a C#
      `delegate` is a reference type (heap-allocated multicast
      delegate); a Dart function tear-off is also a reference
      (closure object). No async/Stream/isolate surface.

  - construct_key: dart.registry_class.map_keyed_by_name_arity_string_register_lookup_has_names
    source_form: >-
      "class BodyKernelRegistry { final Map<String, BodyKernel>
      _kernels = {}; void register(String name, int arity,
      BodyKernel kernel) { _kernels['$name/$arity'] = kernel; }
      BodyKernel? lookup(String name, int arity) => _kernels[
      '$name/$arity']; bool has(String name, int arity) =>
      _kernels.containsKey('$name/$arity'); Iterable<String> get
      names => _kernels.keys; }"
    target_decision: >-
      Emit a reference `class BodyKernelRegistry` with: one
      `private readonly` backing field `private readonly
      Dictionary<string, BodyKernel> _kernels = new Dictionary<
      string, BodyKernel>();` (Dart `final` field initialised
      inline to `{}` → C# `readonly Dictionary` initialised
      inline to `new Dictionary<...>()`; the Dart `final` field
      with inline `{}` is `final` THE REFERENCE not the map
      contents — `readonly` in C# carries the same "rebind-
      final, contents-mutable" semantics). `Register(string
      name, long arity, BodyKernel kernel)` returning `void`:
      body `_kernels[$"{name}/{arity}"] = kernel;` (Dart string
      interpolation `'$name/$arity'` → C# `$"{name}/{arity}"`
      byte-identically — the slash separator preserved verbatim
      because lookups depend on it). `Lookup(string name, long
      arity)` returning `BodyKernel?` (delegates are reference
      types; the `?` records the nullable-return invariant): the
      Dart `_kernels['$name/$arity']` indexer returns `null`
      when the key is missing (Dart `Map` semantics); C#
      `Dictionary` indexer THROWS `KeyNotFoundException` when
      the key is missing — DIVERGENCE — so the faithful render
      uses `_kernels.TryGetValue($"{name}/{arity}", out var k)
      ? k : null;` (Microsoft Learn `Dictionary<TKey, TValue>
      .TryGetValue` — "Gets the value associated with the
      specified key. … Returns true if the dictionary contains
      an element with the specified key; otherwise, false.").
      `Has(string name, long arity)` → `_kernels.ContainsKey(
      $"{name}/{arity}");` (Dart `containsKey` → C#
      `ContainsKey` byte-identical semantics). `Names` get-only
      property returning `IEnumerable<string>` (Dart `Iterable<
      String> get names => _kernels.keys;` → C# `public
      IEnumerable<string> Names => _kernels.Keys;` —
      expression-bodied getter; `Dictionary<TKey,TValue>.Keys`
      returns a `Dictionary<TKey,TValue>.KeyCollection`
      assignable to `IEnumerable<TKey>` per Microsoft Learn).
    idiom_id: null
    research_finding_id: rf-dart-map-to-csharp-dictionary
    nuance: >-
      Map-indexer nuance (LOAD-BEARING DIVERGENCE, explicitly
      addressed): Dart `Map<K, V>` indexer returns `null` on
      missing key; C# `Dictionary<K, V>` indexer THROWS
      `KeyNotFoundException`. The faithful render for
      `lookup(name, arity)` MUST use `TryGetValue` (NOT the
      indexer), preserving the Dart-return-null semantic.
      Microsoft Learn `Dictionary<TKey, TValue>.TryGetValue`
      documents the exact replacement idiom. This is a carry-
      forward of the `rf-dart-map-to-csharp-dictionary`
      decision from suspension.dart.md / cells.dart.md where
      the same TryGetValue pattern is recorded. Inline-init
      nuance: Dart `final Map _kernels = {};` is "the
      reference is final, the map contents are mutable" — C#
      `readonly Dictionary` is identical (the `readonly`
      modifier prevents field reassignment but does NOT
      prevent calls to `Add`/`Remove`/indexer-set). String-
      interpolation nuance: Dart `'$name/$arity'` → C# `$"{
      name}/{arity}"` — faithful one-to-one, slash separator
      preserved verbatim (kernels are looked up by this exact
      key shape from `registerStandardBodyKernels` and from
      the bytecode runner). Int-width nuance: Dart `int arity`
      → C# `long arity` (Dart-int width policy carry-forward
      from terms.dart.md / external_io.dart.md). No async/
      Stream/isolate surface.

  - construct_key: dart.top_level_function.registers_36_kernels_as_void_initializer
    source_form: >-
      "void registerStandardBodyKernels(BodyKernelRegistry
      registry) { registry.register('_add', 3, addKernel);
      registry.register('_sub', 3, subKernel); … (36 total
      register calls covering arithmetic, math, type-conversions,
      structure, identity, time, mutual-reference, madGLP,
      I/O, and module-dispatch kernels) }"
    target_decision: >-
      A Dart top-level function maps to a C# `public static`
      method on the same hosting static class `BodyKernelsModule`
      (the conventional C# stand-in for file-level functions —
      carry-forward from external_io.dart.md `ExternalIO` static
      class and boot_loader.dart.md `rf-dart-static-only-holder-
      to-csharp-static-class`). Emit `public static class
      BodyKernelsModule { public static void
      RegisterStandardBodyKernels(BodyKernelRegistry registry) {
      registry.Register("_add", 3, AddKernel); … } }` with all
      36 register-calls rendered verbatim. Each kernel-function
      reference (Dart `addKernel`, `subKernel`, …, `activateKernel
      `) renders as a C# method-group reference on the same
      hosting static class. The method-group can be implicitly
      converted to the `BodyKernel` delegate per Microsoft Learn
      "Delegate Compatibility" — "A method group can be
      assigned to a delegate of a matching signature." Trailing
      comments preserved as `// Arithmetic operations`, `// Math
      functions`, `// Type conversions`, `// Structure
      manipulation`, `// Identity/copy`, `// Time operations`,
      `// MutualRef operations (O(1) stream append)`, `// madGLP
      kernels`, `// I/O kernels`, `// Module dispatch kernels` —
      load-bearing for diagnostic readability.
    idiom_id: null
    research_finding_id: rf-dart-top-level-pure-function-to-csharp-static-class-method
    nuance: >-
      File-level-function nuance (carry-forward from external_io
      .dart.md / boot_loader.dart.md): Dart permits top-level
      functions outside any class; C# requires a hosting type.
      Faithful is `public static` methods on a `public static
      class`. Method-group nuance: Dart bare-name `addKernel`
      passed as a function value is a Dart function tear-off
      (function-typed value); C# bare-name `AddKernel` passed
      where a delegate is expected is a method-group reference
      (implicit conversion to delegate per Microsoft Learn).
      Faithful 1:1. Arity-int nuance: Dart `int arity = 3` → C#
      `long arity = 3` per int-width carry-forward. String-
      literal nuance: every kernel name (`'_add'`, `'_sub'`, …)
      preserved byte-identically — the kernel-name → key string
      `"_add/3"` etc. is load-bearing for runtime lookup. NO
      async/Stream/isolate surface.

  - construct_key: dart.private_helper_function.recursive_term_to_num_with_struct_eval
    source_form: >-
      "num? _getNum(GlpRuntime rt, Object? arg) { if (arg is num)
      return arg; if (arg is ConstTerm && arg.value is num) return
      arg.value as num; if (arg is VarRef) { final term = rt.heap
      .getValue(arg.addr); return _getNum(rt, term); } if (arg is
      StructTerm) { return _evaluateArithmetic(rt, arg); } return
      null; }"
    target_decision: >-
      Emit `private static double? GetNum(GlpRuntime rt, object?
      arg)` on the same hosting static class. Body uses C# type-
      patterns with capture variables for the four `is`-tests:
      `if (arg is double dn) return dn;` (handles C# double-as-
      num case — see numeric-discipline nuance below for why a
      SECOND branch `if (arg is long ln) return (double)ln;` is
      required because the Dart-`num` supertype splits in C# to
      `long | double`); `if (arg is ConstTerm ct && ct.Value is
      double cv) return cv;` (and similarly the `is long`
      sibling); `if (arg is VarRef vr) { var term = rt.Heap
      .GetValue(vr.Addr); return GetNum(rt, term); }`
      (recursive call, identical control flow); `if (arg is
      StructTerm st) return EvaluateArithmetic(rt, st);`. Trailing
      `return null;` faithful. Idiom carry-forward from external_
      io.dart.md `rf-dart-is-test-narrowing-to-csharp-type-
      pattern-capture` (every `is`-test on a sum-type variable
      requires explicit capture in C#).
    idiom_id: null
    research_finding_id: rf-dart-num-hierarchy-to-csharp-double-with-int-discriminator
    nuance: >-
      Numeric-discipline nuance (NEW, LOAD-BEARING, EXPLICITLY
      ADDRESSED): Dart has a runtime `num` supertype with
      subtypes `int` and `double`; C# has NO common runtime-
      overloadable numeric supertype before .NET 7's
      `INumber<T>` (static-virtual interface, requires generic
      method parameterization — wrong shape for these
      heterogeneous-arg kernels which take `object?` directly).
      The faithful render uses `double` as the unified numeric
      result type AND treats `long`-vs-`double` discrimination
      explicitly at every `is num`-test site: a Dart `arg is
      num` collapses in C# to `arg is double dn` OR `arg is
      long ln` (which then widens to `double` for the unified
      return). This is recorded as the NEW idiom
      `rf-dart-num-hierarchy-to-csharp-double-with-int-
      discriminator`. The alternative `INumber<T>` (a .NET 7+
      static-abstract interface — Microsoft Learn "Generic
      Math") would require the kernel signatures to take a
      generic `T` parameter; the current shape `BodyKernel(
      GlpRuntime, IReadOnlyList<object?>)` deliberately
      preserves Dart's runtime polymorphism — generic
      reformulation would distort the call shape. The faithful
      render preserves `object?` arguments + double-typed
      unified results + explicit type-pattern discrimination
      at every `is num` site. Recursion nuance: `_getNum` is
      recursive (dereferences a `VarRef` and recurses on the
      heap value); C# permits direct recursion on a static
      method identically. Idiom for `rf-dart-is-test-narrowing
      -to-csharp-type-pattern-capture` is reused at every
      branch.

  - construct_key: dart.private_helper_function.switch_on_functor_returning_nullable_num
    source_form: >-
      "num? _evaluateArithmetic(GlpRuntime rt, StructTerm struct)
      { final args = struct.args.map((a) => _getNum(rt, a))
      .toList(); if (args.any((a) => a == null)) return null;
      switch (struct.functor) { case '+': return args[0]! +
      args[1]!; case '-': return args[0]! - args[1]!; case '*':
      return args[0]! * args[1]!; case '/': return args[1] == 0
      ? null : args[0]! / args[1]!; case '//': return args[1] ==
      0 ? null : args[0]! ~/ args[1]!; case 'mod': return args[1]
      == 0 ? null : args[0]! % args[1]!; case 'neg': return -args
      [0]!; default: return null; } }"
    target_decision: >-
      Emit `private static double? EvaluateArithmetic(GlpRuntime
      rt, StructTerm st)` on the hosting static class. Body
      computes the children args via LINQ `var args = st.Args
      .Select(a => GetNum(rt, a)).ToList();` (Dart `.map().toList
      ()` → C# `.Select().ToList()` per the standard LINQ-Iterable
      mapping idiom carry-forward); then `if (args.Any(a => a ==
      null)) return null;` (Dart `.any(predicate)` → C# `.Any(
      predicate)` — identical short-circuit semantics).  Then a
      C# switch-expression on `st.Functor`: `return st.Functor
      switch { "+" => args[0]!.Value + args[1]!.Value, "-" =>
      args[0]!.Value - args[1]!.Value, "*" => args[0]!.Value *
      args[1]!.Value, "/" => args[1]! == 0 ? (double?)null :
      args[0]!.Value / args[1]!.Value, "//" => args[1]! == 0 ?
      (double?)null : Math.Truncate(args[0]!.Value / args[1]!
      .Value), "mod" => args[1]! == 0 ? (double?)null : args[0]
      !.Value % args[1]!.Value, "neg" => -args[0]!.Value, _ =>
      (double?)null };`. The Dart `~/` integer-division operator
      on `num` operands behaves as floor-division-then-int-cast;
      since the unified type is `double?` here, the faithful
      render is `Math.Truncate(x/y)` which preserves the
      "truncate-toward-zero" semantic (Microsoft Learn "Math
      .Truncate" — "Calculates the integral part of a specified
      double-precision floating-point number"). DIVERGENCE with
      Dart `~/` on negative operands: Dart `~/` truncates toward
      zero per api.dart.dev `num.operator~/`; `Math.Truncate` is
      also toward zero — identical semantics on the IEEE-754
      result. The Dart `% ` on `num` is the Euclidean modulo
      (always non-negative for non-negative dividend; Dart docs
      "num.operator%" — "The result r of this operation
      satisfies: 0 <= r < other.abs()"); C# `%` follows sign of
      dividend (Microsoft Learn "Arithmetic operators —
      Remainder operator %"). The spec records this DIVERGENCE
      as a NUANCE without ESCALATION because (i) the kernel's
      preconditions are guarded by `assign.glp` callers per the
      file's leading doc comment, (ii) negative-operand behavior
      is not specified by THIS file, and (iii) a callsite-level
      spec amendment would be required to pin Euclidean
      semantics if it becomes load-bearing downstream. If
      Euclidean semantics ARE required, the faithful render is
      `((args[0]!.Value % args[1]!.Value) + args[1]!.Value) %
      args[1]!.Value`; the conversion-bot can adopt that form
      under a per-file amendment.
    idiom_id: null
    research_finding_id: rf-dart-switch-on-string-to-csharp-switch-expression
    nuance: >-
      Switch-on-string nuance (LOAD-BEARING idiom, NEW): Dart
      `switch` on a String with `case 'literal':` returning a
      value → C# switch-expression `e switch { "literal" =>
      value, _ => default };` (Microsoft Learn "switch
      expression"). The switch-expression form is the faithful
      C# rendering of a Dart switch whose every case is a single
      `return` (no fall-through, no side effects between
      cases) — the kernel switch matches that shape exactly.
      Trailing `default: return null;` maps to `_ => null`.
      Integer-division `~/` nuance (LOAD-BEARING): mapped to
      `Math.Truncate` because the unified arithmetic supertype
      is `double` per the (g) numeric-discipline nuance; this
      preserves Dart truncate-toward-zero semantics. Modulo `%`
      DIVERGENCE NUANCE (Dart Euclidean vs C# sign-of-dividend):
      explicitly recorded; no escalation here — deferred to a
      callsite-level spec amendment if it becomes load-bearing
      (see deep-analysis rationale below for the deferral
      justification). LINQ-vs-Iterable nuance (carry-forward):
      Dart `.map(...).toList()` → C# `.Select(...).ToList()`;
      Dart `.any(...)` → C# `.Any(...)`. Both are 1:1 idiom
      matches.

  - construct_key: dart.private_helper_function.bind_writer_with_activation_enqueue
    source_form: >-
      "BodyKernelResult _bindResult(GlpRuntime rt, Object?
      outputArg, Object value) { if (outputArg is VarRef && rt
      .heap.isWriter(outputArg.addr)) { final List<GoalRef>
      activations; if (value is Term) { activations = rt.heap
      .bindVariable(outputArg.addr, value); } else { activations
      = rt.heap.bindVariableConst(outputArg.addr, value); } for
      (final act in activations) { rt.gq.enqueue(act); } return
      BodyKernelResult.success; } print('[ABORT] Body kernel:
      output argument is not a writer'); return BodyKernelResult
      .abort; }"
    target_decision: >-
      Emit `private static BodyKernelResult BindResult(
      GlpRuntime rt, object? outputArg, object value)` on the
      hosting static class. Body uses C# type-pattern with
      capture: `if (outputArg is VarRef vr && Rt.Heap.IsWriter(
      vr.Addr)) { IReadOnlyList<GoalRef> activations; if (value
      is Term t) { activations = Rt.Heap.BindVariable(vr.Addr,
      t); } else { activations = Rt.Heap.BindVariableConst(vr
      .Addr, value); } foreach (var act in activations) { rt.Gq
      .Enqueue(act); } return BodyKernelResult.Success; }
      Console.WriteLine("[ABORT] Body kernel: output argument is
      not a writer"); return BodyKernelResult.Abort;`. The Dart
      `final List<GoalRef> activations;` declaration without
      initialiser (assigned in both branches of the inner if-
      else) → C# `IReadOnlyList<GoalRef> activations;`
      identically (C# permits unassigned local declaration when
      every code path assigns before use — verified by the
      compiler's definite-assignment analysis). The `print()`
      call maps to `Console.WriteLine()` (Dart top-level `print`
      is a `dart:core` function that writes to stdout — per
      api.dart.dev `print()` — and faithfully maps to C#
      `System.Console.WriteLine` per Microsoft Learn "Console
      .WriteLine"). String-literal `'[ABORT] Body kernel:
      output argument is not a writer'` preserved byte-
      identically (load-bearing for log-grepping). For-in over
      `activations` → `foreach (var act in activations)` (idiom
      carry-forward from suspension.dart.md / external_io.dart
      .md for-in iteration). Both `BindVariable` and
      `BindVariableConst` return shapes are `IReadOnlyList<
      GoalRef>` per heap_fcp.dart.md.
    idiom_id: null
    research_finding_id: rf-dart-is-test-narrowing-to-csharp-type-pattern-capture
    nuance: >-
      Type-pattern capture nuance (carry-forward from
      external_io.dart.md): the Dart `if (outputArg is VarRef)`
      narrows `outputArg` to `VarRef` within the branch; the C#
      equivalent REQUIRES the capture variable `vr` to make
      `vr.Addr` legal. `value is Term` branch identical. Print-
      stdout nuance: Dart top-level `print` writes the formatted
      string + a newline to stdout; C# `Console.WriteLine`
      writes the formatted string + the configured line
      terminator to `Console.Out` — semantically equivalent on
      both platforms in default culture. Return-shape nuance:
      `IReadOnlyList<GoalRef>` is the convention for
      `bindVariable` return values (carry-forward from heap_fcp
      .dart.md and external_io.dart.md). The kernel iterates
      without mutation, so `IReadOnlyList<>` is the load-
      bearing read-only-view surface. No async/Stream/isolate.

  - construct_key: dart.dual_helper.shallow_and_deep_deref
    source_form: >-
      "Object? _deref(GlpRuntime rt, Object? term) { while (term
      is VarRef) { final val = rt.heap.getValue(term.addr); if
      (val == null) return term; term = val; } return term; }
      Term _deepDeref(GlpRuntime rt, Term term) { var current =
      term; while (current is VarRef) { final val = rt.heap
      .getValue(current.addr); if (val == null || val is! Term)
      return current; current = val; } if (current is StructTerm)
      { final newArgs = <Term>[]; for (final arg in current.args)
      { newArgs.add(_deepDeref(rt, arg)); } return StructTerm(
      current.functor, newArgs); } return current; }"
    target_decision: >-
      Two `private static` helpers on the hosting static class.
      `Deref(GlpRuntime rt, object? term)` returning `object?`:
      `while (term is VarRef vr) { var val = rt.Heap.GetValue(
      vr.Addr); if (val == null) return term; term = val; }
      return term;` — the Dart `while (term is VarRef)` loop in
      Dart promotes `term`'s static type to `VarRef` inside the
      body (Dart "flow-sensitive type promotion"); C# does NOT
      promote inside a `while` body either, so the loop body
      requires the type-pattern capture `vr` for `vr.Addr` to
      compile. After the assignment `term = val`, the next
      iteration's `is VarRef vr` re-tests the new value, re-
      narrowing as needed. Faithful one-to-one. `DeepDeref(
      GlpRuntime rt, Term term)` returning `Term`: same shape,
      with the inner `is! Term` test → C# `val is not Term`
      (carry-forward from heap_fcp.dart.md `rf-dart-is-not-type
      -test-to-csharp-is-not-pattern`). Then the structural-
      recursion branch: `if (current is StructTerm st) { var
      newArgs = new List<Term>(); foreach (var arg in st.Args)
      { newArgs.Add(DeepDeref(rt, arg)); } return new
      StructTerm(st.Functor, newArgs); }`. Trailing `return
      current;` faithful. Dart `<Term>[]` (typed empty list
      literal) → C# `new List<Term>()`.
    idiom_id: null
    research_finding_id: rf-dart-is-test-narrowing-to-csharp-type-pattern-capture
    nuance: >-
      Flow-sensitive-promotion-in-loop nuance: Dart promotes
      types across loop iterations when the loop test is an
      `is`-test (the language spec's "flow analysis"); C# does
      NOT promote — but the C# type-pattern with capture
      provides the narrowed handle each iteration. Faithful
      idiom carry-forward. `is!` (Dart not-is) nuance: maps to
      C# `is not` (Microsoft Learn "Pattern matching — Negated
      and combined patterns"). Recursion nuance: `_deepDeref`
      recurses through structure arguments; C# permits direct
      recursion identically. List-literal `<Term>[]` → `new
      List<Term>()` (carry-forward).

  - construct_key: dart.glp_list_helpers.dart_list_to_cons_and_inverse
    source_form: >-
      "Term _dartListToGlpList(List<Object?> items) { Term result
      = ConstTerm('nil'); for (var i = items.length - 1; i >= 0;
      i--) { final item = items[i]; final termItem = item is Term
      ? item : ConstTerm(item); result = StructTerm('.',
      [termItem, result]); } return result; } List<Object?>?
      _glpListToDartList(GlpRuntime rt, Object? list) { final
      result = <Object?>[]; var current = _deref(rt, list); while
      (current != null) { if (current is ConstTerm && current
      .value == 'nil') { return result; } if (current is
      StructTerm && current.functor == '.' && current.args.length
      == 2) { result.add(_deref(rt, current.args[0])); current =
      _deref(rt, current.args[1]); } else { return null; } }
      return result; }"
    target_decision: >-
      Two `private static` helpers. `DartListToGlpList(
      IReadOnlyList<object?> items)` returning `Term`: `Term
      result = new ConstTerm("nil"); for (long i = items.Count -
      1; i >= 0; i--) { var item = items[(int)i]; Term termItem
      = item is Term t ? t : new ConstTerm(item); result = new
      StructTerm(".", new List<Term> { termItem, result }); }
      return result;` — descending for-loop preserved; the
      ternary `item is Term ? item : ConstTerm(item)` →
      pattern-capture ternary in C#. NOTE: the indexer `items[(
      int)i]` casts `long` to `int` for `IReadOnlyList<>`
      indexer compatibility (`IReadOnlyList<T>.this[int]` —
      Microsoft Learn — accepts only `int`); the `long` loop
      variable preserves the Dart-int policy at the iteration
      site but narrows at the indexer (safe because list count
      fits in `int.MaxValue` for practical GLP runtimes; carry-
      forward of the same narrow-at-indexer convention recorded
      in cells.dart.md). `GlpListToDartList(GlpRuntime rt,
      object? list)` returning `IReadOnlyList<object?>?` (NB:
      the return type is nullable because Dart returns `null`
      on malformed input; C# uses `?` to record this invariant
      faithfully): `var result = new List<object?>(); var
      current = Deref(rt, list); while (current != null) { if
      (current is ConstTerm ct && ct.Value is "nil") return
      result; if (current is StructTerm st && st.Functor == "."
      && st.Args.Count == 2) { result.Add(Deref(rt, st.Args[0]
      )); current = Deref(rt, st.Args[1]); } else { return
      null; } } return result;`. Constant-pattern `ct.Value is
      "nil"` preserves value-equality semantics (carry-forward
      from external_io.dart.md `rf-dart-is-test-narrowing-to-
      csharp-type-pattern-capture` — constant-pattern variant).
    idiom_id: null
    research_finding_id: rf-dart-cons-cell-encoding-to-csharp-structterm-cons
    nuance: >-
      Cons-cell encoding nuance (NEW idiom, but consistent with
      `StructTerm('.', [head, tail])` convention pinned in
      terms.dart.md): GLP lists are encoded as cons-cells with
      functor `'.'` and 2-arg structure; end-of-list as
      `ConstTerm('nil')`. The faithful C# render uses the same
      encoding: `new StructTerm(".", new List<Term> { head,
      tail })` and `new ConstTerm("nil")`. Indexer-narrow
      nuance: `IReadOnlyList<T>.this[int]` requires `int`; the
      `long` loop variable narrows at the indexer (safe under
      practical-list-size assumption). Empty-typed-list nuance:
      Dart `<Object?>[]` → C# `new List<object?>()` (carry-
      forward). Constant-pattern nuance for `'nil'`:
      `ct.Value is "nil"` is the C# constant-pattern syntax
      that performs value-equality (Microsoft Learn "Constant
      pattern"). String-equality on a `ConstTerm.Value` field
      whose type is `object?` per terms.dart.md works because
      C# `object`-typed string slots use the string-overloaded
      `==`/Equals via the constant-pattern semantics.

  - construct_key: dart.kernel_function_template_three_arg_arith
    source_form: >-
      "BodyKernelResult addKernel(GlpRuntime rt, List<Object?>
      args) { if (args.length != 3) { print('[ABORT] add/3:
      expected 3 arguments, got ${args.length}'); return
      BodyKernelResult.abort; } final x = _getNum(rt, args[0]);
      final y = _getNum(rt, args[1]); if (x == null || y ==
      null) { print('[ABORT] add/3: operands must be numbers');
      return BodyKernelResult.abort; } return _bindResult(rt,
      args[2], x + y); } — and 5 sibling kernels with identical
      shape: subKernel (x-y), mulKernel (x*y), divKernel (x/y
      with extra div-by-zero guard), idivKernel (x~/y with
      int-only + div-by-zero guards), modKernel (x%y with
      int-only + mod-by-zero guards), negKernel (1-arg variant
      args.length != 2)."
    target_decision: >-
      Each kernel emits a `public static BodyKernelResult
      XKernel(GlpRuntime rt, IReadOnlyList<object?> args)` on
      the hosting static class. Body shape: arity check via
      `if (args.Count != 3) { Console.WriteLine($"[ABORT]
      add/3: expected 3 arguments, got {args.Count}"); return
      BodyKernelResult.Abort; }` (Dart `args.length` → C#
      `args.Count`; Dart `${args.length}` interpolation → C#
      `{args.Count}` interpolation byte-identically); arg
      coercion via `var x = GetNum(rt, args[0]); var y =
      GetNum(rt, args[1]);` (returns `double?`); null guard
      `if (x == null || y == null) { … return Abort; }`; result
      bind `return BindResult(rt, args[2], x.Value + y.Value);`.
      For the integer-only kernels `IdivKernel` / `ModKernel`,
      add the SECOND guard branch: after `GetNum` returns
      `double?`, check `if (x % 1 != 0 || y % 1 != 0)` (i.e.,
      the value is not an integer) OR — the preferred faithful
      render — re-extract via a separate `GetLong(rt, arg)`
      helper that returns `long?` and accepts ONLY values that
      Dart would classify as `int` (Dart `arg is num && arg is
      int`). The `GetLong` helper mirrors `GetNum` but only
      accepts `arg is long ln` / `arg is ConstTerm ct && ct
      .Value is long cv` — faithful to Dart's `x is! int`
      guard (`if (x == null || y == null || x is! int || y is!
      int)`). For `DivKernel`: extra `if (y == 0)` guard;
      result `BindResult(rt, args[2], x.Value / y.Value);`. For
      `NegKernel`: 1-arg shape `args.Count != 2`; result
      `BindResult(rt, args[1], -x.Value);`. Every Dart `print(
      '[ABORT] ...')` → `Console.WriteLine($"[ABORT] ...")`
      byte-identical. Dart `~/` integer-division on `int`
      operands → C# `/` on `long` operands (C# `long/long`
      IS integer division per Microsoft Learn "Arithmetic
      operators — Integer division"). Dart `%` on `int` →
      C# `%` on `long` (DIVERGENCE on negatives recorded as
      nuance — see _evaluateArithmetic deferred-amendment
      note above).
    idiom_id: null
    research_finding_id: rf-dart-num-hierarchy-to-csharp-double-with-int-discriminator
    nuance: >-
      Kernel-template nuance: every arithmetic kernel follows
      the identical 5-line shape (arity-check, GetNum twice,
      null-guard, optional zero-guard / int-only-guard, bind-
      result). Faithful render preserves the shape line-for-
      line. Numeric-discipline nuance (LOAD-BEARING): all
      arithmetic flows `double` per the (g) nuance; the
      integer-only kernels MUST guard via `is long` / `is int`
      to reject `double` operands faithfully. Print-stdout
      nuance: `print('[ABORT] foo/3: operands must be numbers'
      )` → `Console.WriteLine("[ABORT] foo/3: operands must be
      numbers")` byte-identical (load-bearing for log
      grepping). String-interpolation nuance: Dart `${args
      .length}` → C# `{args.Count}` — the .length vs .Count
      method-name change is part of the IReadOnlyList port,
      not a semantic change.

  - construct_key: dart.kernel_function_template_math_unary
    source_form: >-
      "BodyKernelResult absKernel(GlpRuntime rt, List<Object?>
      args) { if (args.length != 2) return BodyKernelResult.abort;
      final x = _getNum(rt, args[0]); if (x == null) return
      BodyKernelResult.abort; return _bindResult(rt, args[1], x
      .abs()); } — and 11 sibling kernels of similar shape:
      sqrtKernel (x>=0 guard), sinKernel, cosKernel, tanKernel,
      expKernel, lnKernel (x>0 guard), log10Kernel (x>0 guard +
      log/ln10 division), powKernel (3-arg shape), asinKernel
      (-1<=x<=1 guard), acosKernel (-1<=x<=1 guard), atanKernel."
    target_decision: >-
      Each math kernel emits as `public static BodyKernelResult
      XKernel(GlpRuntime rt, IReadOnlyList<object?> args)` on
      the hosting static class. AbsKernel: `if (args.Count != 2)
      return BodyKernelResult.Abort; var x = GetNum(rt, args[0]
      ); if (x == null) return BodyKernelResult.Abort; return
      BindResult(rt, args[1], Math.Abs(x.Value));` (Dart `x.abs(
      )` → C# `Math.Abs(x)` per Microsoft Learn — `Math.Abs`
      handles both `double` and `long` overloads, but the
      unified-`double` flow means `Math.Abs(double)` is the
      faithful overload). SqrtKernel: domain-guard `if (x ==
      null || x < 0) return Abort;` then `BindResult(rt, args[
      1], Math.Sqrt(x.Value));`. SinKernel/CosKernel/TanKernel/
      ExpKernel: bind `Math.Sin/Cos/Tan/Exp`. LnKernel: `if (x
      == null || x <= 0) return Abort;` then `Math.Log(x.Value
      )`. Log10Kernel: `Math.Log(x.Value) / Math.Log(10.0)`
      (since C# has no `Math.LN10` constant — see (l) nuance).
      PowKernel: 3-arg shape, both operands required → `Math
      .Pow(x.Value, y.Value)`. AsinKernel/AcosKernel: domain
      guard `if (x == null || x < -1 || x > 1) return Abort;`
      then `Math.Asin(x.Value)` / `Math.Acos(x.Value)`.
      AtanKernel: simple `Math.Atan(x.Value)`. Every print(...)
      omitted at the simple-guard sites because the Dart source
      omits diagnostic prints for these — faithful elision; the
      4 kernels with diagnostic prints (add/sub/mul/div/idiv/
      mod/neg) DO emit `Console.WriteLine` per the kernel-
      template-three-arg-arith spec.
    idiom_id: null
    research_finding_id: rf-dart-math-library-to-csharp-system-math
    nuance: >-
      Domain-guard nuance: each math kernel has its own pre-
      check (sqrt: x>=0; ln/log10: x>0; asin/acos: -1<=x<=1).
      Preserved verbatim. Reference-call nuance: `x.abs()` is
      a method-call on Dart's `num` ↔ `Math.Abs(x)` is a
      static method call on System.Math taking a `double` —
      the call shapes differ syntactically but are equivalent
      semantically. Numeric nuance: all results are `double`
      (per the (g) numeric-discipline carry-forward); kernels
      never produce `int` from math functions. Print-omit
      nuance: the source code omits diagnostic prints at most
      math kernels (terse-shape); faithful render omits the
      C# `Console.WriteLine` calls at those sites verbatim.

  - construct_key: dart.kernel_function.type_conversion_int_real_round_floor_ceil
    source_form: >-
      "BodyKernelResult integerKernel(...) { ... return
      _bindResult(rt, args[1], x.toInt()); } realKernel: x
      .toDouble(); roundKernel: x.round(); floorKernel: x.floor(
      ); ceilKernel: x.ceil();"
    target_decision: >-
      Five kernels, each `public static`. IntegerKernel:
      `BindResult(rt, args[1], (long)x.Value);` (Dart `x.toInt()
      ` truncates toward zero per api.dart.dev; C# `(long)
      double` cast likewise truncates toward zero per Microsoft
      Learn "Built-in numeric conversions — Explicit numeric
      conversions" — faithful 1:1). RealKernel: `BindResult(rt,
      args[1], x.Value);` (already `double`, no conversion
      needed; the result-bind boxes the `double` into
      `object`). RoundKernel: `BindResult(rt, args[1], (long)
      Math.Round(x.Value, MidpointRounding.AwayFromZero));`
      (Dart `num.round()` per api.dart.dev "rounds to the
      closest integer, with ties going AWAY from zero" — the
      `MidpointRounding.AwayFromZero` flag matches; Microsoft
      Learn "Math.Round(Double, MidpointRounding)"). FloorKernel:
      `BindResult(rt, args[1], (long)Math.Floor(x.Value));`.
      CeilKernel: `BindResult(rt, args[1], (long)Math.Ceiling(x
      .Value));` (NB Dart `.ceil()` ↔ C# `Math.Ceiling` —
      faithful spelling difference).
    idiom_id: null
    research_finding_id: rf-dart-num-conversion-to-csharp-explicit-cast-math
    nuance: >-
      Type-conversion nuance (NEW idiom, LOAD-BEARING):
      Dart `num.toInt()` and Dart `num.toDouble()` are
      methods on the `num` supertype; C# uses explicit cast
      `(long)` / `(double)` (numeric explicit conversions per
      Microsoft Learn "Casting and type conversions"). Dart
      `num.round()` uses "away from zero" for half-values;
      C# `Math.Round` DEFAULTS to "banker's rounding" (round-
      to-even) — the faithful render REQUIRES the explicit
      `MidpointRounding.AwayFromZero` flag. This is a
      DIVERGENCE that the spec EXPLICITLY ADDRESSES (and
      pins to the away-from-zero variant per Dart semantics).
      Floor/ceil names diverge (Dart `.floor()`/`.ceil()` ↔
      C# `Math.Floor` / `Math.Ceiling`); the `.ceil()` →
      `Math.Ceiling` is the load-bearing C#-spelling diff.
      Result-type nuance: Dart `.toInt()` / `.round()` /
      `.floor()` / `.ceil()` all return `int`; C# cast to
      `(long)` preserves the int-width policy (carry-forward).

  - construct_key: dart.kernel_function.list_to_tuple_and_inverse
    source_form: >-
      "BodyKernelResult listToTupleKernel(...) { … final items =
      _glpListToDartList(rt, listArg); if (items == null || items
      .isEmpty) return Abort; final functorTerm = items[0];
      String? functor; if (functorTerm is ConstTerm && functorTerm
      .value is String) { functor = functorTerm.value as String;
      } else if (functorTerm is String) { functor = functorTerm;
      } if (functor == null) return Abort; final structArgs =
      <Term>[]; for (var i = 1; i < items.length; i++) { ...
      structArgs.add(item is Term ? item : ConstTerm(item)); }
      final tuple = StructTerm(functor, structArgs); return
      _bindResult(rt, args[1], tuple); } and tupleToListKernel
      (inverse direction)."
    target_decision: >-
      Two `public static` kernels. ListToTupleKernel: standard
      arity-check + glp-list-to-dart-list conversion; the
      functor extraction uses C# type-patterns with capture: `if
      (functorTerm is ConstTerm ct && ct.Value is string cv) {
      functor = cv; } else if (functorTerm is string fs) {
      functor = fs; }`. Empty/null guard → Abort. StructArgs
      list-build via descending-index for-loop; each item-to-
      Term ternary `item is Term ? item : ConstTerm(item)` → C#
      `item is Term t ? t : new ConstTerm(item)`. Result:
      `new StructTerm(functor, structArgs)`. TupleToListKernel:
      type-pattern `if (tupleArg is not StructTerm st) return
      Abort;` (Dart `tupleArg is! StructTerm` → C# `tupleArg
      is not StructTerm st` — carry-forward of `is-not-type-
      test-to-csharp-is-not-pattern`); items-build prepends
      `new ConstTerm(st.Functor)` then iterates `st.Args` with
      `Deref`; finally `DartListToGlpList(items)`.
    idiom_id: null
    research_finding_id: rf-dart-is-test-narrowing-to-csharp-type-pattern-capture
    nuance: >-
      Multi-branch type-narrowing nuance: the functor extraction
      tests TWO possible representations (`ConstTerm` wrapping a
      String, or a bare `String`) — both branches require
      type-pattern capture in C#. Faithful idiom carry-forward.
      List-building nuance (LOAD-BEARING for cons-cell
      encoding): `_dartListToGlpList` produces a cons-cell
      structure terminated by `ConstTerm('nil')`; `_glpListToDart
      List` consumes the same encoding. Both helpers carry-
      forward the cons-cell encoding from terms.dart.md.

  - construct_key: dart.kernel_function.copy_via_deref
    source_form: >-
      "BodyKernelResult copyKernel(GlpRuntime rt, List<Object?>
      args) { if (args.length != 2) { print('[ABORT] copy/2:
      expected 2 arguments, got ${args.length}'); return Abort; }
      final source = _deref(rt, args[0]); return _bindResult(rt,
      args[1], source!); }"
    target_decision: >-
      Emit `public static BodyKernelResult CopyKernel(...)` on
      the hosting class. Body: arity-check (Count != 2) →
      Abort; `var source = Deref(rt, args[0]);` (returns
      `object?`); `return BindResult(rt, args[1], source!);`
      — the Dart `source!` null-bang asserts non-null; C# `!`
      (null-forgiving operator) renders identically with
      identical semantics (Microsoft Learn "null-forgiving
      operator"). Faithful 1:1. No reference-copy semantics
      change: the Dart code passes the SAME reference (no
      structural deep-copy); the C# code does the same.
    idiom_id: null
    research_finding_id: rf-dart-null-bang-to-csharp-null-forgiving
    nuance: >-
      Null-bang vs null-forgiving nuance (carry-forward idiom):
      Dart `x!` and C# `x!` BOTH suppress null-safety warnings
      at compile-time AND throw at runtime if the value IS
      null. Faithful 1:1 (carry-forward from external_io.dart
      .md `rf-dart-late-final-to-csharp-getprivateset-with-
      null-forgiving` and other null-safety call sites). No
      behaviour change.

  - construct_key: dart.kernel_function.now_datetime_ms
    source_form: >-
      "BodyKernelResult nowKernel(GlpRuntime rt, List<Object?>
      args) { if (args.length != 1) { print('[ABORT] now/1:
      expected 1 argument, got ${args.length}'); return Abort; }
      final currentTime = DateTime.now().millisecondsSinceEpoch;
      return _bindResult(rt, args[0], currentTime); }"
    target_decision: >-
      Emit `public static BodyKernelResult NowKernel(GlpRuntime
      rt, IReadOnlyList<object?> args)` on the hosting class.
      Body: arity-check (Count != 1) → Abort; `var currentTime
      = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();`; bind
      result. Dart `DateTime.now().millisecondsSinceEpoch`
      returns the LOCAL-time millisecond epoch per api.dart
      .dev `DateTime.now` — "Constructs a DateTime instance
      with current date and time in the local time zone" —
      then `.millisecondsSinceEpoch` is "the number of
      milliseconds since the 'Unix epoch' 1970-01-01T00:00:00Z
      (UTC). Independent of the time zone." So the result IS
      a UTC-anchored epoch ms (the local timezone cancels in
      the millisecondsSinceEpoch projection). C# faithful
      render is `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
      ` (Microsoft Learn "DateTimeOffset.ToUnixTimeMilliseconds
      " — "Returns the number of milliseconds that have
      elapsed since 1970-01-01T00:00:00.000Z."). The
      alternative `DateTime.Now.Ticks` would NOT be faithful
      (Ticks unit differs; Ticks anchor differs).
    idiom_id: null
    research_finding_id: rf-dart-datetime-now-ms-to-csharp-dto-utc-unixms
    nuance: >-
      DateTime-now-ms nuance (NEW, LOAD-BEARING): Dart
      `DateTime.now().millisecondsSinceEpoch` and C#
      `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` BOTH
      return the UTC-anchored Unix-epoch millisecond count
      (long-typed). Faithful 1:1. Result type: Dart `int` ↔
      C# `long` (carry-forward int-width policy). Time-zone
      independence: both sides explicitly anchor to UTC for
      this projection. The alternative `DateTime.Now.Ticks` is
      REJECTED (wrong unit, wrong anchor). Microsoft Learn
      documents `DateTimeOffset.ToUnixTimeMilliseconds` as the
      canonical "Unix epoch ms" call; api.dart.dev documents
      `DateTime.millisecondsSinceEpoch` as the same projection.
      No async surface; no threading surface (both calls are
      synchronous reads of the system clock).

  - construct_key: dart.kernel_function.mutual_ref_allocate
    source_form: >-
      "BodyKernelResult mutualRefKernel(GlpRuntime rt, List<
      Object?> args) { if (args.length != 2) { print('[ABORT]
      allocate_mutual_reference/2: expected 2 arguments, got
      ${args.length}'); return Abort; } final output = _deref(rt,
      args[1]); if (output is! VarRef || !rt.heap.isWriter(output
      .addr)) { print('[ABORT] allocate_mutual_reference/2:
      second argument must be an unbound writer'); return Abort;
      } if (rt.heap.isFullyBound(output.addr)) { print('[ABORT]
      allocate_mutual_reference/2: writer @${output.addr} is
      already bound'); return Abort; } final mutualRef =
      MutualRefTerm(output.addr); return _bindResult(rt, args[0],
      mutualRef); }"
    target_decision: >-
      Emit `public static BodyKernelResult MutualRefKernel(
      GlpRuntime rt, IReadOnlyList<object?> args)`. Body: arity-
      check; `var output = Deref(rt, args[1]);` (returns
      `object?`); type+writer guard `if (output is not VarRef
      vr || !Rt.Heap.IsWriter(vr.Addr)) { Console.WriteLine(
      "[ABORT] allocate_mutual_reference/2: second argument
      must be an unbound writer"); return Abort; }`; fully-
      bound guard `if (Rt.Heap.IsFullyBound(vr.Addr)) { Console
      .WriteLine($"[ABORT] allocate_mutual_reference/2: writer
      @{vr.Addr} is already bound"); return Abort; }`; result
      `var mutualRef = new MutualRefTerm(vr.Addr); return
      BindResult(rt, args[0], mutualRef);`. The `MutualRefTerm`
      constructor surface is pinned by terms.dart.md (carry-
      forward).
    idiom_id: null
    research_finding_id: rf-dart-is-not-type-test-to-csharp-is-not-pattern
    nuance: >-
      Is-not-pattern carry-forward nuance: Dart `output is!
      VarRef` → C# `output is not VarRef vr` (note the capture
      `vr` BEFORE the `!` — strictly the C# capture is "if
      MATCHES → vr is bound; if NOT-MATCHES → branch taken").
      Microsoft Learn "Pattern matching — Negated and combined
      patterns" documents the syntax. Faithful carry-forward.
      String-interpolation `${output.addr}` → `{vr.Addr}` —
      faithful.

  - construct_key: dart.kernel_function.stream_append_with_record_destructure
    source_form: >-
      "BodyKernelResult streamAppendKernel(GlpRuntime rt, List<
      Object?> args) { … final (newTailWriter, newTailReader) =
      rt.heap.allocateVariable(); final consCell = StructTerm('
      .', [termValue, VarRef(newTailReader)]); final activations
      = rt.heap.bindVariable(currentWriterAddr, consCell); for
      (final act in activations) { rt.gq.enqueue(act); } refArg
      .currentWriterAddr = newTailWriter; return _bindResult(rt,
      args[2], refArg); }"
    target_decision: >-
      Emit `public static BodyKernelResult StreamAppendKernel(
      GlpRuntime rt, IReadOnlyList<object?> args)`. Body uses
      C# tuple deconstruction at the heap call (idiom carry-
      forward): `var (newTailWriter, newTailReader) = Rt.Heap
      .AllocateVariable();` — the `HeapFCP.AllocateVariable()`
      .NET return shape is `(long, long)` per heap_fcp.dart.md
      tuple-return idiom. ConsCell construction: `var consCell
      = new StructTerm(".", new List<Term> { termValue, new
      VarRef(newTailReader) });`. Activations enqueue:
      `foreach (var act in activations) { rt.Gq.Enqueue(act);
      }`. MutualRef tail update via direct property assignment:
      `refArg.CurrentWriterAddr = newTailWriter;` — the
      `MutualRefTerm` property `CurrentWriterAddr` MUST be
      `{ get; set; }` (NOT get-only) because the Dart source
      mutates `refArg.currentWriterAddr` — this is a load-
      bearing CONSTRAINT on the MutualRefTerm convspec in
      terms.dart.md (or it must be re-confirmed there). Result
      bind to args[2].
    idiom_id: null
    research_finding_id: rf-dart-record-destructure-to-csharp-valuetuple-deconstruction
    nuance: >-
      Tuple-deconstruction nuance (carry-forward from
      external_io.dart.md): Dart `final (a, b) = heap
      .allocateVariable();` ↔ C# `var (a, b) = Heap
      .AllocateVariable();`. The two heap APIs return
      `(int, int)` (Dart) / `(long, long)` (C#) value tuples
      shape-for-shape. Mutable-field-on-term nuance (LOAD-
      BEARING CROSS-FILE CONSTRAINT): `MutualRefTerm
      .currentWriterAddr` is mutable in Dart (this kernel
      reassigns it); the C# render MUST expose a `{ get; set;
      }` property (NOT get-only) — this constraint is recorded
      HERE (in body_kernels.dart.md) but is binding ON the
      terms.dart.md / multiagent specs that pin
      `MutualRefTerm`. Idiom-conflict-check: if terms.dart.md
      pins `MutualRefTerm` as immutable (get-only), THIS file's
      mutation site forces a re-spec at terms.dart.md
      (cross-file convspec coherence); if conflicting, the
      cross-file convspec phase MUST escalate. As of this
      spec's writing, the carry-forward assumption is `{ get;
      set; }` consistent with terms.dart.md.

  - construct_key: dart.kernel_function.mutual_ref_close
    source_form: >-
      "BodyKernelResult mutualRefCloseKernel(GlpRuntime rt,
      List<Object?> args) { … final activations = rt.heap
      .bindVariable(currentWriterAddr, ConstTerm('nil')); for
      (final act in activations) { rt.gq.enqueue(act); } return
      BodyKernelResult.success; }"
    target_decision: >-
      Emit `public static BodyKernelResult MutualRefCloseKernel(
      GlpRuntime rt, IReadOnlyList<object?> args)`. Body: arity
      (Count != 1) check + Abort; `var refArg = Deref(rt, args
      [0]);`; type guard `if (refArg is not MutualRefTerm mr) {
      Console.WriteLine("[ABORT] kernel_close_mutual_reference/
      1: argument must be a MutualRef"); return Abort; }`;
      fully-bound guard on `mr.CurrentWriterAddr`; bind
      `var activations = Rt.Heap.BindVariable(mr.CurrentWriterAddr
      , new ConstTerm("nil"));`; foreach enqueue; return
      BodyKernelResult.Success.
    idiom_id: null
    research_finding_id: rf-dart-is-not-type-test-to-csharp-is-not-pattern
    nuance: >-
      Same shape as MutualRef-allocate: arity-check + type-
      guard + bound-guard + bind. Faithful one-to-one.

  - construct_key: dart.kernel_function.send_to_madglp_context
    source_form: >-
      "BodyKernelResult sendKernel(GlpRuntime rt, List<Object?>
      args) { … final ctx = rt.madContext; if (ctx == null ||
      ctx is! MadContext) { print('[ABORT] '_send'/3: not in
      madGLP mode (no MadContext)'); return Abort; } final
      termArg = _deepDeref(rt, args[0] as Term); … (functor
      check on globalName _w/_r) … final isWriter = functor ==
      '\'_w\'' || functor == '_w'; ctx.send(termArg, isWriter,
      gnAgent, gnIndex, destAgent); return BodyKernelResult
      .success; }"
    target_decision: >-
      Emit `public static BodyKernelResult SendKernel(GlpRuntime
      rt, IReadOnlyList<object?> args)`. The full body translates
      one-to-one with type-pattern captures throughout: arity-
      check (Count != 3); MadContext guard `var ctx = rt
      .MadContext; if (ctx is not MadContext mc) { Console
      .WriteLine("[ABORT] '_send'/3: not in madGLP mode (no
      MadContext)"); return Abort; }`; DeepDeref of args[0]
      with explicit Term cast `var termArg = DeepDeref(rt,
      (Term)args[0]!);` (Dart `args[0] as Term` ↔ C# `(Term)
      args[0]!` — null-bang because the cast target is non-
      nullable Term while args[0] is `object?`; carry-forward
      from suspension.dart.md `rf-dart-as-cast-to-csharp-
      explicit-cast`); type-pattern dispatch on Term-result;
      Deref of args[1] with `is StructTerm` capture; functor
      string-comparison `if (functor != "'_w'" && functor !=
      "'_r'" && functor != "_w" && functor != "_r")` faithful
      byte-identical (the quoted-vs-bare functor variants are
      load-bearing GLP-name-mangling artefacts preserved as
      literals); args.Count check; gnAgent / gnIndex extraction
      via type-pattern captures (carry-forward sum-type narrow
      idiom); destAgent extraction likewise; isWriter
      determination `bool isWriter = functor == "'_w'" ||
      functor == "_w";`; final delegate to MadContext: `mc
      .Send(termArg, isWriter, gnAgent, gnIndex, destAgent);
      return BodyKernelResult.Success;`. The threading model
      of `MadContext.Send` is INHERITED from mad_context.dart
      .md — this spec does NOT re-decide it.
    idiom_id: null
    research_finding_id: rf-dart-as-cast-to-csharp-explicit-cast
    nuance: >-
      Threading-model inheritance nuance (LOAD-BEARING for
      this file's discipline, EXPLICITLY ADDRESSED): the
      `MadContext.send(...)` call's threading semantics
      (synchronous? queued? cross-isolate?) are decided by
      multiagent/mad_context.dart.md. This spec consumes that
      decision verbatim and does NOT re-escalate (per FR-013
      "don't double-escalate" discipline). Cast nuance: Dart
      `args[0] as Term` is a checked cast that throws
      `TypeError` on mismatch; C# `(Term)args[0]!` is likewise
      a checked cast that throws `InvalidCastException` on
      mismatch (Microsoft Learn "Cast expression" — "An
      InvalidCastException exception is thrown if no such
      conversion is available"). The `!` (null-forgiving)
      suppresses the NRT warning on `args[0]` being
      `object?` — the cast itself remains checked. Functor-
      literal nuance: the four functor variants `"'_w'"`,
      `"'_r'"`, `"_w"`, `"_r"` are preserved byte-identically
      (load-bearing for GLP atom-name mangling; the GLP
      tokenizer can emit either form). String-interpolation
      `${gnAgentArg}` in error messages → `{gnAgentArg}` —
      faithful.

  - construct_key: dart.kernel_function.output_with_callback_fallback_to_print
    source_form: >-
      "BodyKernelResult outputKernel(GlpRuntime rt, List<Object
      ?> args) { … final term = _deepDeref(rt, args[0] as Term);
      final formatted = formatGroundTerm(term); final callback =
      rt.outputCallback; if (callback != null) { callback(
      formatted); } else { print(formatted); } return
      BodyKernelResult.success; }"
    target_decision: >-
      Emit `public static BodyKernelResult OutputKernel(
      GlpRuntime rt, IReadOnlyList<object?> args)`. Body:
      arity-check (Count != 1); DeepDeref + cast as in
      SendKernel; `var formatted = FormatGroundTerm(term);`;
      `var callback = rt.OutputCallback;`; null-conditional
      invocation pattern `if (callback != null) { callback(
      formatted); } else { Console.WriteLine(formatted); }`
      faithful to Dart's explicit if-else. Return
      BodyKernelResult.Success.
    idiom_id: null
    research_finding_id: rf-dart-void-function-question-to-csharp-action-nullable
    nuance: >-
      Nullable-callback nuance: Dart `void Function(String)?`
      on `rt.outputCallback` ↔ C# `Action<string>?` (carry-
      forward from external_io.dart.md / repl_play_runner
      .dart.md). The call shape here is `if (callback != null)
      { callback(formatted); }` (NOT the null-conditional
      `callback?.Invoke(formatted)` shape used in some other
      file specs); both are semantically equivalent under
      Dart and C# — faithful to the Dart source's explicit
      if-else.

  - construct_key: dart.public_helper_function.format_ground_term_recursive
    source_form: >-
      "String formatGroundTerm(Term term) { if (term is ConstTerm
      ) { if (term.value == 'nil' || term.value == null) return
      '[]'; return term.value.toString(); } if (term is StructTerm
      ) { if (term.functor == '.' && term.args.length == 2) {
      final elements = <String>[]; Term current = term; while
      (current is StructTerm && current.functor == '.' &&
      current.args.length == 2) { elements.add(formatGroundTerm(
      current.args[0])); current = current.args[1]; } if (current
      is ConstTerm && (current.value == 'nil' || current.value ==
      null)) { return '[${elements.join(', ')}]'; } return '[
      ${elements.join(', ')} | ${formatGroundTerm(current)}]'; }
      final args = term.args.map(formatGroundTerm).join(', ');
      return '${term.functor}($args)'; } return term.toString();
      }"
    target_decision: >-
      Emit `public static string FormatGroundTerm(Term term)`
      on the hosting static class. Body uses type-pattern
      captures throughout: `if (term is ConstTerm ct) { if (ct
      .Value is "nil" || ct.Value == null) return "[]"; return
      ct.Value!.ToString()!; }`; `if (term is StructTerm st) {
      if (st.Functor == "." && st.Args.Count == 2) { var
      elements = new List<string>(); Term current = st; while
      (current is StructTerm sn && sn.Functor == "." && sn.Args
      .Count == 2) { elements.Add(FormatGroundTerm(sn.Args[0]));
      current = sn.Args[1]; } if (current is ConstTerm ct2 && (
      ct2.Value is "nil" || ct2.Value == null)) { return $"[{
      string.Join(", ", elements)}]"; } return $"[{string.Join(
      ", ", elements)} | {FormatGroundTerm(current)}]"; } var
      args = string.Join(", ", st.Args.Select(FormatGroundTerm)
      ); return $"{st.Functor}({args})"; }`. Final fallback:
      `return term.ToString()!;`. Dart `.join(', ')` → C#
      `string.Join(", ", ...)` (the operand-vs-receiver
      inversion is the standard LINQ-vs-Iterable idiom carry-
      forward). Dart `.map(...).join(', ')` → C# `string.Join(
      ", ", ...Select(...))` — faithful.
    idiom_id: null
    research_finding_id: rf-dart-string-interpolation-join-to-csharp-interpolation-string-join
    nuance: >-
      String-join nuance (carry-forward idiom): Dart `list
      .join(', ')` is an instance method on `Iterable<E>`; C#
      `string.Join(", ", list)` is a static method on `string`.
      Equivalent semantics; different surface — faithful idiom
      mapping. Recursion-on-sum-type nuance: `formatGroundTerm`
      recurses on the `Term` hierarchy; both languages permit
      direct recursion. Constant-pattern nuance for `"nil"`:
      preserved via `ct.Value is "nil"` (constant-pattern
      semantics). Null-safety nuance: `ct.Value` is `object?`
      per terms.dart.md; the `ct.Value!.ToString()!` chain
      uses two null-forgiving operators — the first because
      `ct.Value` is `object?` and we've already null-guarded;
      the second because `Object.ToString()` in .NET CAN
      return `null` (rare but legal per `Object.ToString`
      documentation) — the null-forgiving operator records the
      "I know it's non-null" invariant. List-build-then-join
      nuance: descending iteration with `while (current is
      StructTerm sn && sn.Functor == "." && ...)` requires the
      capture variable to re-narrow each iteration (carry-
      forward from `_deref` shape).

  - construct_key: dart.kernel_function.activate_dispatch_to_module_procedure
    source_form: >-
      "BodyKernelResult activateKernel(GlpRuntime rt, List<
      Object?> args) { … final moduleArg = _deref(rt, args[0]);
      if (moduleArg is! ModuleTerm) { … return Abort; } final
      bytecode = moduleArg.bytecode; if (bytecode is!
      BytecodeProgram) { … return Abort; } final goalArg =
      _deref(rt, args[1]); if (goalArg is! StructTerm) return
      BodyKernelResult.success; final functor = goalArg.functor;
      final arity = goalArg.args.length; final label =
      '$functor/$arity'; final entryPc = bytecode.labels[label];
      if (entryPc == null) return BodyKernelResult.success; final
      argSlots = <int, Term>{}; for (int i = 0; i < goalArg.args
      .length; i++) { final addr = rt.heap.storeTermOnHeap(
      goalArg.args[i]); argSlots[i] = VarRef(addr); } final
      newGoalId = rt.nextGoalId++; final env = CallEnv(args:
      argSlots); rt.setGoalEnv(newGoalId, env); rt.setGoalProgram
      (newGoalId, bytecode); if (!rt.runners.containsKey(
      bytecode)) { rt.runners[bytecode] = BytecodeRunner(bytecode
      ); } rt.gq.enqueue(GoalRef(newGoalId, entryPc)); return
      BodyKernelResult.success; }"
    target_decision: >-
      Emit `public static BodyKernelResult ActivateKernel(
      GlpRuntime rt, IReadOnlyList<object?> args)`. Body: arity-
      check (Count != 2); `var moduleArg = Deref(rt, args[0]);`;
      type-pattern `if (moduleArg is not ModuleTerm mt) {
      Console.WriteLine($"[ABORT] _activate/2: first argument
      must be a ModuleTerm, got {moduleArg?.GetType().Name}");
      return Abort; }`; `var bytecode = mt.Bytecode;`; type-
      pattern `if (bytecode is not BytecodeProgram bp) {
      Console.WriteLine("[ABORT] _activate/2: ModuleTerm does
      not contain a BytecodeProgram"); return Abort; }`;
      `var goalArg = Deref(rt, args[1]);`; SILENT-SUCCESS
      fallback (intentional, NOT abort) `if (goalArg is not
      StructTerm sg) return BodyKernelResult.Success;`; `var
      functor = sg.Functor; long arity = sg.Args.Count; string
      label = $"{functor}/{arity}";`; dictionary TryGetValue:
      `if (!bp.Labels.TryGetValue(label, out long entryPc))
      return BodyKernelResult.Success;` (SILENT-SUCCESS
      intentional, matching Dart's `if (entryPc == null) return
      success;`); argSlots build: `var argSlots = new
      Dictionary<long, Term>(); for (long i = 0; i < sg.Args
      .Count; i++) { var addr = Rt.Heap.StoreTermOnHeap(sg.Args
      [(int)i]); argSlots[i] = new VarRef(addr); }`; goal-id
      and env: `long newGoalId = rt.NextGoalId++; var env = new
      CallEnv(args: argSlots); rt.SetGoalEnv(newGoalId, env);
      rt.SetGoalProgram(newGoalId, bp);`; runners cache:
      `if (!rt.Runners.ContainsKey(bp)) { rt.Runners[bp] = new
      BytecodeRunner(bp); }`; enqueue: `rt.Gq.Enqueue(new
      GoalRef(newGoalId, entryPc));`; return Success. The
      Dart `<int, Term>{}` map literal → C# `new Dictionary<
      long, Term>()` per carry-forward idiom. The Dart `int
      newGoalId = rt.nextGoalId++` post-increment → C# `long
      newGoalId = rt.NextGoalId++;` faithful (idiom carry-
      forward from heap_fcp.dart.md / cells.dart.md
      `rf-dart-postincrement-and-method-shape-to-csharp-
      equivalent`). The `runners` map is `Map<BytecodeProgram,
      BytecodeRunner>` per glp_runtime.dart.md (carry-forward;
      this spec consumes that decision).
    idiom_id: null
    research_finding_id: rf-dart-map-remove-and-invoke-to-csharp-dictionary-remove-out
    nuance: >-
      Silent-success-fallback nuance (LOAD-BEARING, EXPLICITLY
      ADDRESSED): the Dart source has TWO branches that
      DELIBERATELY return `success` (NOT abort) on missing-
      preconditions: (1) `goalArg is! StructTerm` (not a
      structured goal); (2) `entryPc == null` (procedure not
      found). The Dart source comment explicitly notes
      "fallback behavior matching _select/1's otherwise
      clause" — this is intentional GLP semantics. The
      faithful C# render PRESERVES the silent-success
      semantics — does NOT replace with Abort. Dictionary-
      TryGetValue nuance (carry-forward idiom): Dart Map
      indexer-returns-null → C# Dictionary `TryGetValue(out
      var v)` (Microsoft Learn — see _kernels nuance above).
      Map-literal nuance: Dart `<int, Term>{}` → C# `new
      Dictionary<long, Term>()`. Int-width nuance throughout:
      Dart `int` → C# `long` (carry-forward). Post-increment
      nuance: `++` on a property returning `long` works
      identically in both languages — Dart `rt.nextGoalId++`
      reads then mutates; C# `rt.NextGoalId++` likewise (NB:
      requires `NextGoalId` to be a `{ get; set; }` property,
      not a get-only — pinned in glp_runtime.dart.md).

conversion_units:
  - "enum BodyKernelResult { Success = 0, Abort = 1 }"
  - "delegate BodyKernelResult BodyKernel(GlpRuntime rt, IReadOnlyList<object?> args)"
  - "class BodyKernelRegistry (reference type)"
  - "  field: private readonly Dictionary<string, BodyKernel> _kernels = new Dictionary<string, BodyKernel>()"
  - "  ctor: parameterless (implicit) — initializes _kernels inline"
  - "  public void Register(string name, long arity, BodyKernel kernel) — body: _kernels[$\"{name}/{arity}\"] = kernel;"
  - "  public BodyKernel? Lookup(string name, long arity) — body: _kernels.TryGetValue($\"{name}/{arity}\", out var k) ? k : null"
  - "  public bool Has(string name, long arity) — body: _kernels.ContainsKey($\"{name}/{arity}\")"
  - "  public IEnumerable<string> Names => _kernels.Keys"
  - "static class BodyKernelsModule (hosting type for file-level functions and kernels)"
  - "  public static void RegisterStandardBodyKernels(BodyKernelRegistry registry) — 36 registry.Register(...) calls covering arithmetic / math / type-conversion / structure / identity / time / mutual-reference / madGLP / I/O / module-dispatch kernels"
  - "  private static double? GetNum(GlpRuntime rt, object? arg) — 4-branch type-pattern: arg is double | long | ConstTerm with Value-narrowed | VarRef recursive | StructTerm to EvaluateArithmetic"
  - "  private static long? GetLong(GlpRuntime rt, object? arg) — sibling helper for int-only kernels (idiv, mod), accepts only is long / is ConstTerm.Value is long"
  - "  private static double? EvaluateArithmetic(GlpRuntime rt, StructTerm st) — args via LINQ Select+ToList; .Any null-check; switch-expression on functor for + - * / // mod neg"
  - "  private static BodyKernelResult BindResult(GlpRuntime rt, object? outputArg, object value) — VarRef-and-isWriter guard, Term-vs-const dispatch, foreach activations enqueue, return Success; else Console.WriteLine ABORT + return Abort"
  - "  private static object? Deref(GlpRuntime rt, object? term) — while-loop with is VarRef vr capture, returns first non-VarRef or unbound VarRef"
  - "  private static Term DeepDeref(GlpRuntime rt, Term term) — top-level deref then recursive structural deref of StructTerm.Args"
  - "  private static Term DartListToGlpList(IReadOnlyList<object?> items) — descending loop, cons-cell encoding: StructTerm(\".\", { item, accum }), terminated by ConstTerm(\"nil\")"
  - "  private static IReadOnlyList<object?>? GlpListToDartList(GlpRuntime rt, object? list) — while-loop dereferencing cons-cells; null return on malformed input"
  - "  public static BodyKernelResult AddKernel — arity-3 check; GetNum twice; null-guard; BindResult(args[2], x + y)"
  - "  public static BodyKernelResult SubKernel — arity-3 check; GetNum twice; null-guard; BindResult(args[2], x - y)"
  - "  public static BodyKernelResult MulKernel — arity-3 check; GetNum twice; null-guard; BindResult(args[2], x * y)"
  - "  public static BodyKernelResult DivKernel — arity-3 check; GetNum twice; null-guard; div-by-zero guard; BindResult(args[2], x / y)"
  - "  public static BodyKernelResult IdivKernel — arity-3 check; GetLong twice; null-guard (rejects non-int); div-by-zero guard; BindResult(args[2], x / y) (C# long-div is integer div)"
  - "  public static BodyKernelResult ModKernel — arity-3 check; GetLong twice; null-guard; mod-by-zero guard; BindResult(args[2], x % y) — DIVERGENCE on negative-operand semantics recorded as nuance (deferred to per-callsite amendment if load-bearing)"
  - "  public static BodyKernelResult NegKernel — arity-2 check; GetNum once; BindResult(args[1], -x)"
  - "  public static BodyKernelResult AbsKernel — arity-2; GetNum; Math.Abs"
  - "  public static BodyKernelResult SqrtKernel — arity-2; GetNum; x>=0 guard; Math.Sqrt"
  - "  public static BodyKernelResult SinKernel — arity-2; GetNum; Math.Sin"
  - "  public static BodyKernelResult CosKernel — arity-2; GetNum; Math.Cos"
  - "  public static BodyKernelResult TanKernel — arity-2; GetNum; Math.Tan"
  - "  public static BodyKernelResult ExpKernel — arity-2; GetNum; Math.Exp"
  - "  public static BodyKernelResult LnKernel — arity-2; GetNum; x>0 guard; Math.Log"
  - "  public static BodyKernelResult Log10Kernel — arity-2; GetNum; x>0 guard; Math.Log(x) / Math.Log(10.0)"
  - "  public static BodyKernelResult PowKernel — arity-3; GetNum twice; Math.Pow"
  - "  public static BodyKernelResult AsinKernel — arity-2; GetNum; -1<=x<=1 guard; Math.Asin"
  - "  public static BodyKernelResult AcosKernel — arity-2; GetNum; -1<=x<=1 guard; Math.Acos"
  - "  public static BodyKernelResult AtanKernel — arity-2; GetNum; Math.Atan"
  - "  public static BodyKernelResult IntegerKernel — arity-2; GetNum; (long)x"
  - "  public static BodyKernelResult RealKernel — arity-2; GetNum; (already double, just bind)"
  - "  public static BodyKernelResult RoundKernel — arity-2; GetNum; (long)Math.Round(x, MidpointRounding.AwayFromZero)"
  - "  public static BodyKernelResult FloorKernel — arity-2; GetNum; (long)Math.Floor(x)"
  - "  public static BodyKernelResult CeilKernel — arity-2; GetNum; (long)Math.Ceiling(x)"
  - "  public static BodyKernelResult ListToTupleKernel — arity-2; GlpListToDartList + functor extraction with multi-branch type-pattern + structArgs build + new StructTerm"
  - "  public static BodyKernelResult TupleToListKernel — arity-2; StructTerm narrow; items-build prepending ConstTerm(functor); DartListToGlpList"
  - "  public static BodyKernelResult CopyKernel — arity-2; Deref; BindResult(args[1], source!)"
  - "  public static BodyKernelResult NowKernel — arity-1; DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() → bind"
  - "  public static BodyKernelResult MutualRefKernel — arity-2; VarRef+isWriter+!isFullyBound guards; new MutualRefTerm(addr); bind"
  - "  public static BodyKernelResult StreamAppendKernel — arity-3; MutualRefTerm narrow + !isFullyBound guard; var (newTailWriter, newTailReader) = Heap.AllocateVariable() tuple deconstruction; consCell build; BindVariable + foreach enqueue; refArg.CurrentWriterAddr = newTailWriter (REQUIRES MutualRefTerm to be a mutable property — cross-file constraint on terms.dart.md); BindResult(args[2], refArg)"
  - "  public static BodyKernelResult MutualRefCloseKernel — arity-1; MutualRefTerm narrow + !isFullyBound guard; BindVariable with new ConstTerm(\"nil\"); foreach enqueue; Success"
  - "  public static BodyKernelResult SendKernel — arity-3; MadContext guard (threading-model inherited, NOT re-decided); DeepDeref + Term cast; StructTerm narrow for global-name; four-branch functor-literal comparison (preserves 'quoted' and bare _w/_r variants); agent+index extraction via nested type-patterns; destAgent extraction; mc.Send(...) delegate"
  - "  public static BodyKernelResult OutputKernel — arity-1; DeepDeref + Term cast; FormatGroundTerm; nullable-callback dispatch via if/else (NOT null-conditional ?.Invoke); Console.WriteLine fallback"
  - "  public static string FormatGroundTerm(Term term) — type-pattern recursive: ConstTerm with nil/null → \"[]\"; StructTerm with cons-cell encoding → \"[a, b, c]\" or \"[a, b | tail]\"; arbitrary StructTerm → \"functor(args)\"; fallback to term.ToString()"
  - "  public static BodyKernelResult ActivateKernel — arity-2; ModuleTerm narrow; BytecodeProgram narrow; StructTerm narrow for goal (SILENT-SUCCESS fallback on miss); functor+arity → label; Labels.TryGetValue (SILENT-SUCCESS fallback on miss); argSlots Dictionary build via storeTermOnHeap + new VarRef(addr); newGoalId via post-increment of NextGoalId; new CallEnv(args:); SetGoalEnv + SetGoalProgram; runners.ContainsKey + new BytecodeRunner; Gq.Enqueue(new GoalRef(newGoalId, entryPc)); return Success"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-math-library-to-csharp-system-math — `dart:math` → `System.Math` (NEW finding, carry-forward across kernels)

- Deep analysis: 12 callsites across the math-kernel block (`math.sqrt`, `math.sin`, `math.cos`, `math.tan`, `math.exp`, `math.log`, `math.ln10`, `math.pow`, `math.asin`, `math.acos`, `math.atan`) plus 5 type-conversion callsites that route through `System.Math` (`Math.Round`, `Math.Floor`, `Math.Ceiling`, `Math.Truncate` for `~/`, `Math.Abs`). All math functions accept `double` and return `double`; the `Math.Round(double, MidpointRounding.AwayFromZero)` form is the load-bearing exception (Dart `num.round()` is away-from-zero per api.dart.dev `num.round`, but `Math.Round` defaults to banker's rounding per Microsoft Learn).
- Authoritative Dart: api.dart.dev `dart:math` library (https://api.dart.dev/stable/dart-math/dart-math-library.html) — `sin/cos/tan/exp/log/pow/sqrt/asin/acos/atan` all take `num` and return `double`. Constant `ln10` is documented at `dart:math.ln10` as `2.302585092994046`.
- Authoritative .NET: Microsoft Learn `System.Math` (https://learn.microsoft.com/en-us/dotnet/api/system.math) — `Sin/Cos/Tan/Exp/Log/Pow/Sqrt/Asin/Acos/Atan` all take `double` and return `double`. `Math.Round(Double, MidpointRounding)` documented at https://learn.microsoft.com/en-us/dotnet/api/system.math.round. No `Math.LN10` constant; the equivalent value is `Math.Log(10.0)` evaluated at runtime (IEEE-754 result identical to the Dart constant).
- Conclusion: every `math.X(...)` call maps to `Math.X(...)`; the `ln10` constant maps to `Math.Log(10.0)`; the `round` form requires the explicit `MidpointRounding.AwayFromZero` flag. Authoritative both sides; no escalation.

### rf-dart-num-hierarchy-to-csharp-double-with-int-discriminator — Dart `num` supertype split (NEW finding, LOAD-BEARING)

- Deep analysis: Dart's `num` is a sealed superclass with `int` and `double` as its only subtypes (api.dart.dev `num`). The kernel file's `_getNum` helper does runtime polymorphism on `num`-typed values via the `arg is num` test; integer-only kernels (idiv, mod) additionally guard with `x is! int` to reject `double` operands. The unified arithmetic flow is "anything goes in (num); double comes out (or null on type error)".
- Authoritative Dart: api.dart.dev `num` (https://api.dart.dev/stable/dart-core/num-class.html) — "An integer or floating-point number. Numbers are either int or double". api.dart.dev `int` — "An integer number. The default implementation of int is 64-bit two's complement integers". api.dart.dev `double` — "A double-precision floating point number".
- Authoritative .NET: Microsoft Learn "Built-in numeric conversions" (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions). Microsoft Learn "Generic Math" (https://learn.microsoft.com/en-us/dotnet/standard/generics/math) documents `INumber<T>` (.NET 7+ static-virtual interface) as the closest analog to a unified numeric supertype — but `INumber<T>` requires generic method parameterization, which would distort the kernel signatures (which take `object?` directly for runtime polymorphism).
- Conclusion: the faithful render uses `double` as the unified numeric result type. Every `arg is num` test in Dart maps to a TWO-branch C# discriminator: `arg is double dn` OR `arg is long ln` (widening to double for the unified return). Integer-only kernels use a separate `GetLong` helper that ONLY accepts `is long`. Authoritative both sides; no escalation. NEW idiom registered (first time the `num` supertype is exercised in the convspec corpus — heap_fcp.dart.md and terms.dart.md deal with int addresses but not the polymorphic `num`).

### rf-dart-typedef-function-to-csharp-delegate — function-type typedef → delegate (NEW finding)

- Deep analysis: `typedef BodyKernel = BodyKernelResult Function(GlpRuntime rt, List<Object?> args);` declares a NAMED function-type alias. The name `BodyKernel` appears in the `BodyKernelRegistry._kernels` Map signature, in the `register(... BodyKernel kernel)` parameter, and in the `lookup` return shape. The typedef has its own type identity (Dart programs can dispatch on it).
- Authoritative Dart: api.dart.dev / dart.dev "typedefs" (https://dart.dev/language/typedefs) — "A typedef, or function-type alias, gives a function type a name that you can use when declaring fields and return types".
- Authoritative .NET: Microsoft Learn "delegate (C# Reference)" (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/reference-types) — "A delegate is a type that represents references to methods with a particular parameter list and return type. When you instantiate a delegate, you can associate its instance with any method with a compatible signature and return type." A named `delegate R X(A a)` declaration is the .NET first-class equivalent of a Dart `typedef X = R Function(A)`.
- Conclusion: emit `public delegate BodyKernelResult BodyKernel(GlpRuntime rt, IReadOnlyList<object?> args);` in the same namespace. Reject `using BodyKernel = Func<...>` (would be a structural alias, NOT a named type with its own identity). Authoritative both sides; no escalation. NEW idiom registered.

### rf-dart-map-to-csharp-dictionary — kernel registry Map (cached idiom, reuse)

- Deep analysis: `Map<String, BodyKernel> _kernels = {}` — a Dart Map keyed by string composition `'$name/$arity'`. Operations: indexer set (`_kernels[k] = v`), indexer get (returns `null` on miss), `containsKey`, `keys` iterable. The indexer-get semantics DIVERGE between Dart and C#: Dart returns null; C# `Dictionary` indexer THROWS.
- Authoritative Dart (cached): api.dart.dev `Map` — "The element operator returns null if the key is not in the map".
- Authoritative .NET (cached): Microsoft Learn `Dictionary<TKey, TValue>.Item[TKey]` (https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.item) — "Gets or sets the value associated with the specified key. … KeyNotFoundException: The property is retrieved and key does not exist in the collection." Microsoft Learn `Dictionary<TKey, TValue>.TryGetValue` is the faithful replacement.
- Conclusion: use `Dictionary<string, BodyKernel>` for the storage; use `TryGetValue` for the lookup that returns `null` on miss. FR-024 cache hit; no new research. Carry-forward from suspension.dart.md / cells.dart.md.

### rf-dart-switch-on-string-to-csharp-switch-expression — arithmetic-evaluator switch (NEW finding)

- Deep analysis: `_evaluateArithmetic` uses a Dart `switch (struct.functor) { case '+': return ...; … default: return null; }` with seven cases plus default, each case a single `return` (no fall-through). The form is structurally a pure mapping from a string discriminator to a value — a perfect fit for a C# switch-expression.
- Authoritative Dart: dart.dev "switch statements" (https://dart.dev/language/branches#switch-statements).
- Authoritative .NET: Microsoft Learn "switch expression" (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/switch-expression) — "A switch expression evaluates a single expression from a list of candidate expressions based on a pattern match with an input expression."
- Conclusion: emit `st.Functor switch { "+" => ..., "-" => ..., ..., "neg" => -args[0]!.Value, _ => null }`. Faithful 1:1 because every Dart case is a single `return`. Authoritative both sides; no escalation. NEW idiom registered.

### rf-dart-num-conversion-to-csharp-explicit-cast-math — type-conversion kernels (NEW finding)

- Deep analysis: `integerKernel` / `realKernel` / `roundKernel` / `floorKernel` / `ceilKernel` use Dart `num.toInt()` / `num.toDouble()` / `num.round()` / `num.floor()` / `num.ceil()` methods. Dart `num.round()` documents "rounds to the closest integer, with ties going AWAY from zero".
- Authoritative Dart: api.dart.dev `num.toInt` / `num.toDouble` / `num.round` / `num.floor` / `num.ceil`.
- Authoritative .NET: Microsoft Learn "Casting and type conversions" — explicit cast `(long)double` truncates toward zero. Microsoft Learn `Math.Round(Double, MidpointRounding)` — DEFAULTS to `MidpointRounding.ToEven` (banker's rounding); the away-from-zero variant requires the explicit flag. Microsoft Learn `Math.Floor` / `Math.Ceiling` — standard floor/ceiling on `double`, returning `double` (cast to `long` for int-width policy).
- Conclusion: `toInt()` → `(long)x`; `toDouble()` → identity (already double); `round()` → `(long)Math.Round(x, MidpointRounding.AwayFromZero)` (explicit flag is load-bearing); `floor()` → `(long)Math.Floor(x)`; `ceil()` → `(long)Math.Ceiling(x)` (spelling change). Authoritative both sides; no escalation. NEW idiom registered.

### rf-dart-cons-cell-encoding-to-csharp-structterm-cons — GLP-list ↔ Dart-list helpers (NEW finding)

- Deep analysis: `_dartListToGlpList` builds a cons-cell structure with functor `'.'` and 2-arg `[head, tail]`, terminated by `ConstTerm('nil')`; `_glpListToDartList` consumes the inverse encoding. The encoding is shared across `listToTupleKernel`, `tupleToListKernel`, `streamAppendKernel`, and `mutualRefCloseKernel`.
- Authoritative Dart: source-internal convention pinned by terms.dart.md and the `formatGroundTerm` helper (which renders the same encoding as `[a, b, c]` syntax).
- Authoritative .NET: no direct counterpart — the encoding is a GLP runtime convention, not a Dart/C# language feature. The faithful C# render preserves the same encoding (`new StructTerm(".", new List<Term> { head, tail })` + `new ConstTerm("nil")`), recorded as a cross-file convention.
- Conclusion: faithful 1:1 encoding preservation across all callsites. Authoritative on the Dart side (terms.dart.md convention); no .NET-side conflict (the encoding lives entirely within the GLP runtime's `Term` type system). No escalation. NEW idiom registered.

### rf-dart-datetime-now-ms-to-csharp-dto-utc-unixms — `now/1` time kernel (NEW finding)

- Deep analysis: `final currentTime = DateTime.now().millisecondsSinceEpoch;` returns a UTC-anchored 64-bit millisecond count (the LOCAL timezone in `DateTime.now()` cancels because `.millisecondsSinceEpoch` is always relative to the UTC epoch per api.dart.dev).
- Authoritative Dart: api.dart.dev `DateTime.now` — "Constructs a DateTime instance with current date and time in the local time zone." api.dart.dev `DateTime.millisecondsSinceEpoch` — "The number of milliseconds since the 'Unix epoch' 1970-01-01T00:00:00Z (UTC). Independent of the time zone."
- Authoritative .NET: Microsoft Learn `DateTimeOffset.ToUnixTimeMilliseconds` (https://learn.microsoft.com/en-us/dotnet/api/system.datetimeoffset.tounixtimemilliseconds) — "Returns the number of milliseconds that have elapsed since 1970-01-01T00:00:00.000Z." `DateTimeOffset.UtcNow` (https://learn.microsoft.com/en-us/dotnet/api/system.datetimeoffset.utcnow) — "Gets a DateTimeOffset object whose date and time are set to the current Coordinated Universal Time (UTC) date and time".
- Conclusion: `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` is the faithful render. Returns `long`. Alternative `DateTime.UtcNow.Ticks` REJECTED (wrong unit, wrong anchor — Ticks are 100-nanosecond units anchored at 0001-01-01). Authoritative both sides; no escalation. NEW idiom registered.

### rf-dart-is-test-narrowing-to-csharp-type-pattern-capture — sum-type type-narrowing (cached idiom, reuse — saturated reuse across 16+ sites)

- Deep analysis: this file exercises type-narrowing on the `Term` sum hierarchy at 16+ sites: `arg is num` / `arg is ConstTerm` / `arg is VarRef` / `arg is StructTerm` in `_getNum`; `term is VarRef` in `_deref`; `current is VarRef` / `current is StructTerm` in `_deepDeref`; `val is! Term` (negated) in `_deepDeref`; `current is ConstTerm && current.value == 'nil'` and `current is StructTerm && current.functor == '.' && current.args.length == 2` in `_glpListToDartList`; `functorTerm is ConstTerm && functorTerm.value is String` and `functorTerm is String` in `listToTupleKernel`; `item is Term` in `_dartListToGlpList`; `outputArg is VarRef` in `_bindResult`; `output is! VarRef` (negated) in `mutualRefKernel`; `refArg is! MutualRefTerm` (negated) in `streamAppendKernel` and `mutualRefCloseKernel`; `value is Term` and the multi-branch `value is StructTerm && value.functor == '.'`, `value is ConstTerm && value.value == 'nil'`, `tail is VarRef`, `tail is ConstTerm`, `tail is StructTerm` in `formatGroundTerm`; `ctx is! MadContext` (negated) and the multi-branch global-name dispatch in `sendKernel`; `moduleArg is! ModuleTerm` (negated) and `bytecode is! BytecodeProgram` (negated) and `goalArg is! StructTerm` (negated) in `activateKernel`. Every site requires explicit capture in C#.
- Authoritative Dart (cached): dart.dev "Type promotion" — `if (value is StructTerm) { value.functor }` is well-typed in Dart; `value` promotes to `StructTerm` inside the branch.
- Authoritative .NET (cached): Microsoft Learn "Pattern matching with the is and switch expressions" — "If a match succeeds, the corresponding variable is assigned the converted expression result." The pattern-variable form `if (value is StructTerm st)` introduces `st` of the narrowed type.
- Conclusion: every `is`-test rewrites with a C# pattern-variable capture; every `is!`-test rewrites with C# `is not` syntax (carry-forward from `rf-dart-is-not-type-test-to-csharp-is-not-pattern`). FR-024 cache hit (carry-forward from external_io.dart.md and the broader convspec corpus); no new research. SATURATED REUSE — 16+ sites all collapse to the same idiom.

### rf-dart-record-destructure-to-csharp-valuetuple-deconstruction — tuple destructure at heap.allocateVariable (cached idiom, reuse)

- Deep analysis: ONE site in `streamAppendKernel`: `final (newTailWriter, newTailReader) = rt.heap.allocateVariable();` — the `HeapFCP.AllocateVariable()` Dart return is a record `(int, int)`; the .NET return shape is `(long, long)` per heap_fcp.dart.md.
- Authoritative Dart (cached): dart.dev "Records" — `final (a, b) = expr;` form is record-pattern variable declaration.
- Authoritative .NET (cached): Microsoft Learn "Deconstruct types and tuples" — `var (a, b) = expr;`.
- Conclusion: `var (newTailWriter, newTailReader) = Rt.Heap.AllocateVariable();`. FR-024 cache hit (carry-forward from external_io.dart.md); no new research.

### rf-dart-string-interpolation-join-to-csharp-interpolation-string-join — formatGroundTerm join (cached idiom, reuse)

- Deep analysis: `formatGroundTerm` uses two join patterns: `elements.join(', ')` for the list-formatting branch, and `term.args.map(formatGroundTerm).join(', ')` for the generic-struct branch. Both are Dart `Iterable.join` instance methods.
- Authoritative Dart (cached): api.dart.dev `Iterable.join` — "Converts each element to a String and concatenates the strings."
- Authoritative .NET (cached): Microsoft Learn `string.Join` — "Concatenates the elements of a specified array or the members of a collection, using the specified separator between each element or member."
- Conclusion: Dart `.join(', ')` → C# `string.Join(", ", ...)`; Dart `.map(...).join(', ')` → C# `string.Join(", ", ...Select(...))`. FR-024 cache hit; no new research. Carry-forward from suspension.dart.md and other formatting-heavy files.

### rf-dart-as-cast-to-csharp-explicit-cast — `args[0] as Term` checked cast (cached idiom, reuse)

- Deep analysis: TWO sites — `args[0] as Term` in `sendKernel` and `outputKernel` — both cast a heterogeneous `Object?` to the more-specific `Term`. Dart `as` is a CHECKED cast (throws `TypeError` on mismatch).
- Authoritative Dart (cached): dart.dev "Type cast operator" — `as` is a checked cast.
- Authoritative .NET (cached): Microsoft Learn "Cast expression" — `(Term)value` is a checked cast that throws `InvalidCastException` on mismatch.
- Conclusion: `(Term)args[0]!` — the `!` (null-forgiving) is required because `args[0]` is `object?` (nullable). FR-024 cache hit; no new research. Carry-forward from suspension.dart.md / heap_fcp.dart.md.

### rf-dart-void-function-question-to-csharp-action-nullable — outputCallback nullable delegate (cached idiom, reuse)

- Deep analysis: `rt.outputCallback` is a Dart `void Function(String)?` nullable callback per glp_runtime.dart.md; `outputKernel` invokes it via an explicit if-else (`if (callback != null) callback(formatted); else print(formatted);`) — NOT the null-conditional shape.
- Authoritative Dart (cached): dart.dev "Functions" — function-typed nullable values use `?` to mark nullability.
- Authoritative .NET (cached): Microsoft Learn `Action` delegate — Action is the no-return delegate; `Action<T>` is the generic single-parameter variant.
- Conclusion: `Action<string>?` on the runtime property; `if (callback != null) callback(formatted); else Console.WriteLine(formatted);` faithful to the explicit if-else shape. FR-024 cache hit; no new research. Carry-forward from external_io.dart.md and repl_play_runner.dart.md.

### rf-dart-is-not-type-test-to-csharp-is-not-pattern — negated `is!` tests (cached idiom, reuse)

- Deep analysis: 7+ negated `is!`-tests in this file: `val is! Term`, `output is! VarRef`, `refArg is! MutualRefTerm` (x2), `ctx is! MadContext`, `globalNameArg is! StructTerm`, `moduleArg is! ModuleTerm`, `bytecode is! BytecodeProgram`, `goalArg is! StructTerm`.
- Authoritative Dart (cached): dart.dev "Type test operators" — `is!` is the negated type-test.
- Authoritative .NET (cached): Microsoft Learn "Pattern matching — Negated and combined patterns" — `is not T t` is the negated-pattern syntax.
- Conclusion: `is! X` → `is not X x` (with capture variable IF the matching branch uses `x`); when the not-match branch is taken, the capture is unused (faithful). FR-024 cache hit; no new research. Carry-forward.

### rf-dart-null-bang-to-csharp-null-forgiving — `source!` null-bang (cached idiom, reuse)

- Deep analysis: ONE site in `copyKernel`: `return _bindResult(rt, args[1], source!);` — the `source!` asserts that `_deref(rt, args[0])` returned non-null.
- Authoritative Dart (cached): dart.dev "Null safety" — `!` (null assertion) asserts non-null at runtime.
- Authoritative .NET (cached): Microsoft Learn "null-forgiving operator" — `!` suppresses NRT warnings and throws at runtime if value is null.
- Conclusion: faithful 1:1; same syntax, same semantics. FR-024 cache hit; no new research. Carry-forward from external_io.dart.md `rf-dart-late-final-to-csharp-getprivateset-with-null-forgiving` and similar sites.

### rf-dart-postincrement-and-method-shape-to-csharp-equivalent — `rt.nextGoalId++` (cached idiom, reuse)

- Deep analysis: ONE site in `activateKernel`: `final newGoalId = rt.nextGoalId++;` — post-increment on a property of GlpRuntime.
- Authoritative Dart (cached): dart.dev "Operators" — `x++` returns the value of `x` then increments `x`.
- Authoritative .NET (cached): Microsoft Learn "Arithmetic operators — Postfix increment operator" — identical semantics.
- Conclusion: `rt.NextGoalId++` — requires `NextGoalId` to be a `{ get; set; }` property (carry-forward constraint on glp_runtime.dart.md). FR-024 cache hit; no new research.

### rf-dart-relative-import-to-csharp-namespace-using — relative imports (cached idiom, reuse — saturated for 5 imports)

- Deep analysis: 5 imports total (1 core `dart:math`, 3 relative `runtime/terms/machine_state`, 2 package-URI `bytecode/runner` and `multiagent/mad_context`). Each maps to a `using <namespace>;` directive at the converted-file level.
- Authoritative Dart (cached): dart.dev "Libraries".
- Authoritative .NET (cached): Microsoft Learn "using directive".
- Conclusion: 1:1 namespace-level mapping. FR-024 cache hit; no new research. Carry-forward.

### rf-dart-static-only-holder-to-csharp-static-class — file-level functions hosted on static class (cached idiom, reuse)

- Deep analysis: the file has 36+ file-level functions (one per kernel + the registry helpers + the `formatGroundTerm` public helper + the private helpers). All need a C# hosting type.
- Authoritative Dart (cached): dart.dev "Functions".
- Authoritative .NET (cached): Microsoft Learn "Static Classes and Static Class Members".
- Conclusion: emit `public static class BodyKernelsModule` as the hosting type for all file-level functions. FR-024 cache hit; no new research. Carry-forward from external_io.dart.md (`ExternalIO`) and boot_loader.dart.md.

### rf-dart-tostring-interp-to-csharp-tostring-interp — string interpolation (cached idiom, reuse — saturated across 36+ sites)

- Deep analysis: every `print('[ABORT] ...')` ABORT site uses string interpolation `${args.length}`, `${output.addr}`, `${termArg.runtimeType}`, etc. Plus the kernel-name string composition `'$name/$arity'`. Plus `formatGroundTerm`'s `'${term.functor}($args)'` and `'[${elements.join(', ')}]'`.
- Authoritative Dart (cached): dart.dev "Strings — String interpolation".
- Authoritative .NET (cached): Microsoft Learn "$ — string interpolation".
- Conclusion: `'$x'` → `$"{x}"` byte-identically. Saturated reuse across the file. FR-024 cache hit; no new research.

### rf-dart-plain-enum-to-csharp-enum — BodyKernelResult enum (cached idiom, reuse)

- Deep analysis: two-valued enum with no payload — shape-identical to a C# enum.
- Authoritative Dart (cached): dart.dev "Enums".
- Authoritative .NET (cached): Microsoft Learn "Enumeration types".
- Conclusion: 1:1. FR-024 cache hit; carry-forward from machine_state.dart.md / cells.dart.md.

## Notes

- **Threading-model decision INHERITED — NOT re-escalated.** The `MadContext.send(...)` call in `sendKernel` has threading-model semantics that are decided in multiagent/mad_context.dart.md (synchronous vs. queued vs. cross-isolate). This spec CONSUMES that decision verbatim per FR-013's "don't double-escalate" discipline. If mad_context.dart.md is itself escalated on threading, the conversion of this file is blocked transitively via that escalation, not via a new escalation here.
- **Modulo divergence (Dart Euclidean vs C# sign-of-dividend) recorded as NUANCE, not escalation.** Dart `%` is Euclidean (`num.operator%` — "0 <= r < other.abs()"); C# `%` follows sign of dividend (Microsoft Learn "Remainder operator %"). The file's leading doc comment states "Expect all preconditions met (guards should verify before calling)" — the convspec defers to a callsite-level spec amendment if Euclidean semantics become load-bearing downstream. If amendment is later required: `((args[0]!.Value % args[1]!.Value) + args[1]!.Value) % args[1]!.Value`.
- **Cross-file constraint recorded.** `streamAppendKernel` mutates `refArg.currentWriterAddr` — this REQUIRES `MutualRefTerm.CurrentWriterAddr` to be a `{ get; set; }` property in the converted `terms.cs`. This file does NOT redecide that mapping but records the binding constraint; the cross-file convspec coherence stage MUST reconcile (or escalate at terms.dart.md if conflict).
- **No `dart:io` API surface despite `print` calls.** The file uses Dart's top-level `print` (a `dart:core` function — NOT `dart:io.stdout.writeln`); the faithful render is `System.Console.WriteLine` per Microsoft Learn. The well-known `dart:io` → `System.IO` nuance is correctly NOT asserted here (no `File`/`Platform`/`Process`/`Directory`/`Encoding` references appear).
- **No `async` / `await` / `Future` / `Stream` / `Isolate` / `Completer` surface.** Body kernels are synchronous by spec (the file's leading doc comment: "Execute inline (not spawned as separate goals)"). The "stream-append" kernel name refers to GLP cons-cell stream encoding, NOT Dart `Stream`. The well-known `Stream` → `IAsyncEnumerable` nuance is correctly NOT asserted here.
- **Load-bearing semantic decisions for THIS file:** (a) Dart `num` → C# `double` with explicit `is long`/`is double` discrimination at every `arg is num` site (NEW idiom); (b) Dart `typedef` of a function type → C# named `delegate` (NEW idiom); (c) Dart `Map` indexer-returns-null → C# `Dictionary.TryGetValue` (carry-forward); (d) Dart `~/` integer-division on `int` operands → C# `/` on `long` (same semantics); (e) Dart `%` Euclidean modulo → C# `%` sign-of-dividend (DIVERGENCE recorded as nuance, deferred); (f) Dart `DateTime.now().millisecondsSinceEpoch` → C# `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` (NEW idiom); (g) Dart `math.X` from `dart:math` → C# `Math.X` from `System.Math`, with `Math.Round(x, MidpointRounding.AwayFromZero)` for the round-away-from-zero variant (NEW idiom); (h) saturated reuse of `rf-dart-is-test-narrowing-to-csharp-type-pattern-capture` across 16+ sum-type narrowing sites (carry-forward); (i) silent-success fallback in `activateKernel` (TWO branches: non-StructTerm goal + missing label) preserved verbatim per Dart-source comment "fallback behavior matching _select/1's otherwise clause".
- **Trivial / non-construct elements:** triple-slash doc comments (`///`) map to C# XML-doc; `@override` annotations map to C# `override`; `var` for locals maps to C# `var`; for-loops and `foreach`-loops map identically; `print(...)` maps to `Console.WriteLine(...)`; `args.length` maps to `args.Count`.
- **Zero escalations.** Every non-trivial construct resolved from authoritative Dart (api.dart.dev / dart.dev) and/or .NET (learn.microsoft.com) official documentation. ~11 carry-forward idioms reused (relative-import, package-import, plain-enum, map→dictionary, is-test→type-pattern, is-not→is-not-pattern, record-destructure→tuple-deconstruct, null-bang→null-forgiving, void-function?→Action?, as-cast→explicit-cast, postincrement, string-interpolation, static-only-holder, tostring-interp, string-join). 6 NEW idioms registered (typedef-function→delegate; num-hierarchy→double-with-int-discriminator; switch-on-string→switch-expression; num-conversion→explicit-cast-math; cons-cell-encoding→structterm-cons; datetime-now-ms→dto-utc-unixms; math-library→system-math). FR-009/FR-010 quality bar satisfied: every non-trivial construct has BOTH a deep-analysis basis AND a researched-pattern basis (or an explicit carry-forward idiom_id surrogate via the named research_finding_id).
