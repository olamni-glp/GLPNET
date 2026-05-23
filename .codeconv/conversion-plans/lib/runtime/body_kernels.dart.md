---
path: lib/runtime/body_kernels.dart
cycle_group_id: 36
scc_siblings: [lib/bytecode/runner.dart, lib/multiagent/mad_context.dart, lib/runtime/glp_activation.dart, lib/runtime/runtime.dart, lib/runtime/system_predicates.dart]
generated_at: 2026-05-21T17:42:00Z
source_sha256: 9d360613abdb60c46d883ad215633020e879fefa7f3d422f319dac02fb7063ba
schema_version: 1
---

# Conversion Plan: lib/runtime/body_kernels.dart

## 1. Source Analysis

`lib/runtime/body_kernels.dart` (882 lines, sha256
`9d360613abdb60c46d883ad215633020e879fefa7f3d422f319dac02fb7063ba`) defines
the body-kernel infrastructure for GLP: a registry plus 36 named runtime-
implemented predicates that execute inline with two-valued semantics
(`success`/`abort`). The file is divided into ten functional blocks delimited
by `// ===` banners:

- Imports (5): `dart:math as math`, `runtime.dart`, `terms.dart`,
  `machine_state.dart show GoalRef`, `package:glp_runtime/bytecode/runner.dart
  show BytecodeRunner, BytecodeProgram, CallEnv`, `package:glp_runtime/
  multiagent/mad_context.dart`.
- Enum: `BodyKernelResult { success, abort }` — payload-free two-tag enum.
- Typedef: `BodyKernel = BodyKernelResult Function(GlpRuntime rt, List<Object?>
  args)` — function-type alias.
- Registry: `class BodyKernelRegistry` with `Map<String, BodyKernel> _kernels`,
  keyed by `'$name/$arity'`; methods `register`/`lookup`/`has`/`names`.
- Registration entry-point: `void registerStandardBodyKernels(BodyKernelRegistry
  registry)` — 36 `registry.register(...)` calls covering arithmetic / math /
  type-conversion / structure / identity / time / mutual-reference / madGLP /
  I/O / module-dispatch kernels.
- Private helpers: `_getNum` (recursive num-extractor over the Term sum),
  `_evaluateArithmetic` (switch-on-functor returning nullable num),
  `_bindResult` (writer-bind with activation enqueue), `_deref` (shallow
  while-loop dereference), `_deepDeref` (recursive structural dereference),
  `_dartListToGlpList` (cons-cell encoder), `_glpListToDartList` (cons-cell
  decoder).
- Arithmetic kernels (7): `add`/`sub`/`mul`/`div`/`idiv`/`mod`/`neg`.
- Math kernels (12): `abs`/`sqrt`/`sin`/`cos`/`tan`/`exp`/`ln`/`log10`/`pow`/
  `asin`/`acos`/`atan`.
- Type-conversion kernels (5): `integer`/`real`/`round`/`floor`/`ceil`.
- Structure kernels (3): `listToTupleKernel`/`tupleToListKernel`/`copyKernel`.
- Time kernel (1): `nowKernel` — `DateTime.now().millisecondsSinceEpoch`.
- MutualRef kernels (3): `mutualRefKernel`/`streamAppendKernel`/
  `mutualRefCloseKernel` (the sole record-destructure site
  `final (newTailWriter, newTailReader) = rt.heap.allocateVariable()`).
- madGLP kernel (1): `sendKernel` — calls into `MadContext.send(...)`.
- I/O kernel (1): `outputKernel` + public helper `formatGroundTerm` —
  recursive Term→string with list-syntax sugaring.
- Module-dispatch kernel (1): `activateKernel` — dispatches a Goal term
  against a ModuleTerm-wrapped BytecodeProgram; preserves writer/reader
  polarity via `storeTermOnHeap`; SILENT-SUCCESS on missing-procedure
  (matches `_select/1`'s otherwise clause).

Surface count: 1 enum, 1 typedef, 1 reference class, 36 kernel functions
+ 8 private/public helpers + 1 registration entry-point = 47 distinct
public/private constructs. 16+ `is`-test type-narrowing sites on the Term
sum hierarchy; saturated reuse of `rf-dart-is-test-narrowing-to-csharp-
type-pattern-capture`. No `async`/`await`/`Future`/`Stream`/`Isolate`/
`Completer` surface (body kernels are synchronous by spec). No `dart:io`
references (the `print` calls reach Dart's top-level `dart:core` print).

## 2. Dart → C#/.NET Conversion Plan

For each construct in the ratified convspec, the target decision below is
the MIRROR of the convspec's `target_decision` field. The convspec is
ratified (FR-013 discipline); this plan COPIES, does not re-derive.

- `import 'dart:math' as math;` → `using System;` (covers `System.Math`).
  Every `math.X(...)` call site reaches `Math.X(...)` directly; the Dart
  prefix alias is dropped (no .NET per-namespace alias is required because
  `System.Math` is the only math import). `math.ln10` → `Math.Log(10.0)`
  (no `Math.LN10` constant in .NET; equivalent IEEE-754 value).

- `import 'runtime.dart';` → `using <root>.Runtime;` (namespace name fixed
  by downstream depgraph step). Consumes the `GlpRuntime` surface pinned
  by glp_runtime.dart.md: `Heap`, `Gq`, `MadContext`, `OutputCallback`,
  `Runners`, `NextGoalId`, `SetGoalEnv`, `SetGoalProgram`.

- `import 'terms.dart';` → `using <root>.Runtime;`. Consumes the `Term`
  sealed-abstract hierarchy and the leaves `ConstTerm`, `StructTerm`,
  `VarRef`, `MutualRefTerm`, `ModuleTerm` pinned by terms.dart.md.

- `import 'machine_state.dart' show GoalRef;` → `using <root>.Runtime;`
  (the `show` allow-list is ELIDED — C# has no per-symbol import).

- `import 'package:glp_runtime/bytecode/runner.dart' show BytecodeRunner,
  BytecodeProgram, CallEnv;` → `using <root>.Bytecode;`. Show-list elided.

- `import 'package:glp_runtime/multiagent/mad_context.dart';` →
  `using <root>.MultiAgent;`. Consumes `MadContext` and its `Send(...)`
  surface pinned by multiagent/mad_context.dart.md. **Threading-model
  decision inherited — NOT re-decided here.**

- `enum BodyKernelResult { success, abort }` → `public enum BodyKernelResult
  { Success, Abort }`. PascalCased members; default `int` backing; XML-doc
  comments preserved from the Dart triple-slash comments.

- `typedef BodyKernel = BodyKernelResult Function(GlpRuntime rt,
  List<Object?> args);` → `public delegate BodyKernelResult BodyKernel(
  GlpRuntime rt, IReadOnlyList<object?> args);`. Named delegate (NOT
  `Func<...>` alias) preserves Dart typedef's named-type identity. The
  `List<Object?>` → `IReadOnlyList<object?>` choice is the read-only-view
  carry-forward from external_io.dart.md — kernels iterate without
  mutating.

- `class BodyKernelRegistry { final Map<String, BodyKernel> _kernels = {};
  ... }` → `public class BodyKernelRegistry { private readonly
  Dictionary<string, BodyKernel> _kernels = new Dictionary<string,
  BodyKernel>(); public void Register(string name, long arity, BodyKernel
  kernel) => _kernels[$"{name}/{arity}"] = kernel; public BodyKernel?
  Lookup(string name, long arity) => _kernels.TryGetValue($"{name}/
  {arity}", out var k) ? k : null; public bool Has(string name, long
  arity) => _kernels.ContainsKey($"{name}/{arity}"); public
  IEnumerable<string> Names => _kernels.Keys; }`. Indexer-vs-TryGetValue
  divergence handled (C# Dictionary indexer throws on miss; Dart Map
  returns null — `TryGetValue` preserves the Dart return-null contract).
  Int-width: `int arity` → `long arity` (carry-forward).

- `void registerStandardBodyKernels(BodyKernelRegistry registry)` → hosted
  as `public static void RegisterStandardBodyKernels(BodyKernelRegistry
  registry)` on a `public static class BodyKernelsModule`. The 36
  `registry.register('_X', N, XKernel)` calls render as
  `registry.Register("_X", N, XKernel)`; each kernel-name is a method-
  group reference that implicitly converts to the `BodyKernel` delegate.
  All 10 functional-block banner comments preserved as `// ` comments.

- `num? _getNum(GlpRuntime rt, Object? arg)` → `private static double?
  GetNum(GlpRuntime rt, object? arg)`. 4-branch C# type-pattern dispatch
  using the new `rf-dart-num-hierarchy-to-csharp-double-with-int-
  discriminator` idiom: `arg is double dn` → return `dn`; `arg is long
  ln` → return `(double)ln`; `arg is ConstTerm ct && ct.Value is double
  cv` → return `cv` (and sibling `is long` branch widens to double);
  `arg is VarRef vr` → recursive `GetNum(rt, Rt.Heap.GetValue(vr.Addr))`;
  `arg is StructTerm st` → `EvaluateArithmetic(rt, st)`; trailing
  `return null;`.

- `_evaluateArithmetic` (switch on functor) → `private static double?
  EvaluateArithmetic(GlpRuntime rt, StructTerm st)`. LINQ for the args
  array (`st.Args.Select(a => GetNum(rt, a)).ToList()`) + `.Any(a => a
  == null)` early-return null. Then a C# switch-expression on
  `st.Functor`: `"+"`/`"-"`/`"*"`/`"/"` arithmetic; `"//"` →
  `Math.Truncate(x/y)` for the `~/` truncate-toward-zero semantic; `"mod"`
  → `x % y` (DIVERGENCE on negative operands recorded as NUANCE,
  deferred to a callsite-level amendment if load-bearing); `"neg"` →
  `-args[0]!.Value`; `_` → null.

- `_bindResult(GlpRuntime rt, Object? outputArg, Object value)` →
  `private static BodyKernelResult BindResult(GlpRuntime rt, object?
  outputArg, object value)`. C# type-pattern `if (outputArg is VarRef
  vr && Rt.Heap.IsWriter(vr.Addr)) { IReadOnlyList<GoalRef>
  activations; if (value is Term t) activations = Rt.Heap.BindVariable(
  vr.Addr, t); else activations = Rt.Heap.BindVariableConst(vr.Addr,
  value); foreach (var act in activations) rt.Gq.Enqueue(act); return
  BodyKernelResult.Success; } Console.WriteLine("[ABORT] Body kernel:
  output argument is not a writer"); return BodyKernelResult.Abort;`.
  Both `BindVariable`/`BindVariableConst` returns are
  `IReadOnlyList<GoalRef>` per heap_fcp.dart.md.

- `_deref(GlpRuntime rt, Object? term)` → `private static object?
  Deref(GlpRuntime rt, object? term) { while (term is VarRef vr) { var
  val = rt.Heap.GetValue(vr.Addr); if (val == null) return term; term
  = val; } return term; }`. C# requires the `vr` capture inside the
  loop body (no flow-sensitive promotion across iterations).

- `_deepDeref(GlpRuntime rt, Term term)` → `private static Term
  DeepDeref(GlpRuntime rt, Term term)`. Same while-loop dereference,
  with the inner `val is! Term` test → C# `val is not Term`. Then the
  structural recursion branch: `if (current is StructTerm st) { var
  newArgs = new List<Term>(); foreach (var arg in st.Args) newArgs.Add(
  DeepDeref(rt, arg)); return new StructTerm(st.Functor, newArgs); }`.

- `_dartListToGlpList(List<Object?> items)` → `private static Term
  DartListToGlpList(IReadOnlyList<object?> items)`. Descending
  for-loop, cons-cell encoding `new StructTerm(".", new List<Term> {
  termItem, result })`, terminated by `new ConstTerm("nil")`.

- `_glpListToDartList(GlpRuntime rt, Object? list)` → `private static
  IReadOnlyList<object?>? GlpListToDartList(GlpRuntime rt, object?
  list)`. While-loop with `is ConstTerm ct && ct.Value is "nil"` (nil
  terminator) and `is StructTerm st && st.Functor == "." && st.Args
  .Count == 2` (cons cell) discriminators; malformed-input returns
  null (preserves Dart return-null contract).

- Arithmetic kernels `addKernel`/`subKernel`/`mulKernel`/`divKernel`/
  `idivKernel`/`modKernel`/`negKernel` → `public static
  BodyKernelResult AddKernel/...` on `BodyKernelsModule`. Each follows
  the convspec's kernel-template-three-arg-arith shape: arity-check
  (`args.Count != 3`) → Abort + Console.WriteLine; `GetNum` twice;
  null-guard; result-bind. For `idiv`/`mod`: secondary `GetLong`
  helper for int-only operands (rejects `double` per Dart's `x is!
  int` guard); `idiv` uses C# `/` on `long` operands (which IS
  integer division per Microsoft Learn); `mod` uses C# `%` (DIVERGENCE
  on negative operands recorded as nuance). `div` adds the
  div-by-zero guard. `neg` is the 1-arg variant (arity-check Count
  != 2).

- Math kernels `absKernel`/`sqrtKernel`/`sinKernel`/`cosKernel`/
  `tanKernel`/`expKernel`/`lnKernel`/`log10Kernel`/`powKernel`/
  `asinKernel`/`acosKernel`/`atanKernel` → `public static
  BodyKernelResult AbsKernel/...` each calling `Math.Abs`/`Math.Sqrt`/
  `Math.Sin`/`Math.Cos`/`Math.Tan`/`Math.Exp`/`Math.Log`/`Math.Log(x)
  / Math.Log(10.0)`/`Math.Pow`/`Math.Asin`/`Math.Acos`/`Math.Atan`.
  Domain pre-guards (sqrt: x>=0; ln/log10: x>0; asin/acos: -1<=x<=1)
  preserved verbatim. Diagnostic `Console.WriteLine` omitted at math
  kernels (faithful to Dart's terse-shape).

- Type-conversion kernels `integerKernel`/`realKernel`/`roundKernel`/
  `floorKernel`/`ceilKernel` → `public static BodyKernelResult
  IntegerKernel/RealKernel/RoundKernel/FloorKernel/CeilKernel`.
  Mappings: `x.toInt()` → `(long)x`; `x.toDouble()` → identity (`x`
  already double); `x.round()` → `(long)Math.Round(x,
  MidpointRounding.AwayFromZero)` (explicit flag load-bearing —
  C# default is banker's rounding); `x.floor()` → `(long)Math.Floor(
  x)`; `x.ceil()` → `(long)Math.Ceiling(x)` (spelling change).

- `listToTupleKernel` / `tupleToListKernel` → `public static
  BodyKernelResult ListToTupleKernel(...)` / `TupleToListKernel(...)`.
  Functor extraction uses TWO-branch type-pattern: `functorTerm is
  ConstTerm ct && ct.Value is string cv` OR `functorTerm is string
  fs`. StructArgs built via descending for-loop with pattern-capture
  ternary. `tupleToListKernel` uses `if (tupleArg is not StructTerm
  st) return Abort;` then prepends `new ConstTerm(st.Functor)` to the
  iterated args and routes through `DartListToGlpList`.

- `copyKernel` → `public static BodyKernelResult CopyKernel`. `var
  source = Deref(rt, args[0]); return BindResult(rt, args[1],
  source!);` (Dart `source!` → C# `source!` null-forgiving operator,
  identical semantics).

- `nowKernel` → `public static BodyKernelResult NowKernel`. `var
  currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
  return BindResult(rt, args[0], currentTime);`. Returns `long`.
  Alternative `DateTime.UtcNow.Ticks` REJECTED (wrong unit, wrong
  anchor).

- `mutualRefKernel` → `public static BodyKernelResult MutualRefKernel`.
  Arity-check; `var output = Deref(rt, args[1]);`; `if (output is not
  VarRef vr || !Rt.Heap.IsWriter(vr.Addr)) ...`; fully-bound guard;
  `var mutualRef = new MutualRefTerm(vr.Addr); return BindResult(rt,
  args[0], mutualRef);`.

- `streamAppendKernel` → `public static BodyKernelResult
  StreamAppendKernel`. C# tuple-deconstruction `var (newTailWriter,
  newTailReader) = Rt.Heap.AllocateVariable();` (the `HeapFCP
  .AllocateVariable()` .NET return is `(long, long)` per
  heap_fcp.dart.md). ConsCell `new StructTerm(".", new List<Term> {
  termValue, new VarRef(newTailReader) })`. Activations enqueue via
  `foreach`. Tail-update `refArg.CurrentWriterAddr = newTailWriter;`
  — **REQUIRES `MutualRefTerm.CurrentWriterAddr` to be a mutable
  `{ get; set; }` property in terms.cs** (cross-file constraint
  recorded; reconciled in §5 / §7).

- `mutualRefCloseKernel` → `public static BodyKernelResult
  MutualRefCloseKernel`. Same shape as MutualRef-allocate: arity-
  check + `is not MutualRefTerm mr` guard + fully-bound guard +
  `BindVariable(mr.CurrentWriterAddr, new ConstTerm("nil"))` +
  foreach enqueue + return Success.

- `sendKernel` → `public static BodyKernelResult SendKernel`. Arity-
  check (Count != 3); `var ctx = rt.MadContext; if (ctx is not
  MadContext mc) ...`; DeepDeref of args[0] with `(Term)args[0]!`
  cast; Deref of args[1] with `is StructTerm` capture; four-branch
  functor literal comparison `if (functor != "'_w'" && functor !=
  "'_r'" && functor != "_w" && functor != "_r") ...` preserved
  byte-identically (the quoted-vs-bare variants are load-bearing
  GLP-atom mangling artefacts); args.Count check; gnAgent /
  gnIndex extraction via type-pattern captures; destAgent
  extraction likewise; `bool isWriter = functor == "'_w'" ||
  functor == "_w";`; final `mc.Send(termArg, isWriter, gnAgent,
  gnIndex, destAgent); return BodyKernelResult.Success;`.
  **Threading-model of `mc.Send` inherited from mad_context.dart.md
  — NOT re-decided here.**

- `outputKernel` → `public static BodyKernelResult OutputKernel`.
  Arity-check; DeepDeref + Term cast; `var formatted =
  FormatGroundTerm(term); var callback = rt.OutputCallback; if
  (callback != null) callback(formatted); else Console.WriteLine(
  formatted); return BodyKernelResult.Success;`. Explicit if-else
  shape (NOT null-conditional `callback?.Invoke(...)`).

- `formatGroundTerm` → `public static string FormatGroundTerm(Term
  term)`. Recursive type-pattern dispatch on `Term` hierarchy:
  ConstTerm with nil/null → `"[]"`; StructTerm with cons-cell
  (functor `"."` + 2 args) → list-syntax `"[a, b, c]"` or
  `"[a, b | tail]"`; general StructTerm → `"functor(a, b)"`;
  fallback `term.ToString()!`. Dart `.map(f).join(", ")` → C#
  `string.Join(", ", ...Select(f))`.

- `activateKernel` → `public static BodyKernelResult ActivateKernel`.
  Arity-check; `var moduleArg = Deref(rt, args[0]);`; `if (moduleArg
  is not ModuleTerm mt) ...`; `var bytecode = mt.Bytecode;`; `if
  (bytecode is not BytecodeProgram bp) ...`; `var goalArg = Deref(rt,
  args[1]);`; **SILENT-SUCCESS fallback** `if (goalArg is not
  StructTerm sg) return BodyKernelResult.Success;` (intentional —
  matches `_select/1`'s otherwise clause); `string label =
  $"{sg.Functor}/{sg.Args.Count}";`; `if (!bp.Labels.TryGetValue(
  label, out long entryPc)) return BodyKernelResult.Success;`
  (SILENT-SUCCESS fallback again); argSlots Dictionary build via
  `Rt.Heap.StoreTermOnHeap` + `new VarRef(addr)` preserving writer/
  reader polarity; `long newGoalId = rt.NextGoalId++;` (REQUIRES
  `NextGoalId` to be `{ get; set; }` in glp_runtime.dart.md — see
  §7); `var env = new CallEnv(args: argSlots);` (C# named-argument
  syntax identical to Dart); `rt.SetGoalEnv(newGoalId, env);
  rt.SetGoalProgram(newGoalId, bp);`; runners-cache `if (!rt.Runners
  .ContainsKey(bp)) rt.Runners[bp] = new BytecodeRunner(bp);`;
  `rt.Gq.Enqueue(new GoalRef(newGoalId, entryPc)); return
  BodyKernelResult.Success;`.

## 3. Decomposed Task Units

- T1: emit file-level `using` directives (System, runtime, terms,
  bytecode/runner, multiagent/mad_context namespaces) — done one-line.
- T2: emit `public enum BodyKernelResult { Success, Abort }` — done.
- T3: emit `public delegate BodyKernelResult BodyKernel(GlpRuntime rt,
  IReadOnlyList<object?> args);` — done.
- T4: emit `public class BodyKernelRegistry` with `Dictionary<string,
  BodyKernel> _kernels` + `Register`/`Lookup`/`Has`/`Names` — done.
- T5: open `public static class BodyKernelsModule` hosting type — done.
- T6: emit `RegisterStandardBodyKernels(BodyKernelRegistry registry)`
  with all 36 register-calls and the 10 banner comments preserved —
  done.
- T7: emit `private static double? GetNum(GlpRuntime rt, object? arg)`
  with 4-branch type-pattern dispatch — done.
- T8: emit `private static long? GetLong(GlpRuntime rt, object? arg)`
  helper for int-only kernels (idiv, mod) — done.
- T9: emit `private static double? EvaluateArithmetic(GlpRuntime rt,
  StructTerm st)` with LINQ + switch-expression — done.
- T10: emit `private static BodyKernelResult BindResult(GlpRuntime rt,
  object? outputArg, object value)` with writer-bind + activation
  enqueue + abort fallback — done.
- T11: emit `private static object? Deref(GlpRuntime rt, object? term)`
  shallow while-loop dereference — done.
- T12: emit `private static Term DeepDeref(GlpRuntime rt, Term term)`
  recursive structural dereference — done.
- T13: emit `private static Term DartListToGlpList(IReadOnlyList<object?>
  items)` cons-cell encoder — done.
- T14: emit `private static IReadOnlyList<object?>? GlpListToDartList(
  GlpRuntime rt, object? list)` cons-cell decoder — done.
- T15: emit 7 arithmetic kernels (Add/Sub/Mul/Div/Idiv/Mod/Neg) per
  the kernel-template-three-arg-arith shape, with diagnostic Console
  .WriteLine + abort/zero/int-only guards — done.
- T16: emit 12 math kernels (Abs/Sqrt/Sin/Cos/Tan/Exp/Ln/Log10/Pow/
  Asin/Acos/Atan) routing through `System.Math` — done.
- T17: emit 5 type-conversion kernels (Integer/Real/Round/Floor/Ceil)
  with explicit casts + `MidpointRounding.AwayFromZero` for Round —
  done.
- T18: emit 3 structure kernels (ListToTuple/TupleToList/Copy) with
  functor extraction + cons-cell helpers — done.
- T19: emit NowKernel using `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds
  ()` — done.
- T20: emit 3 mutual-reference kernels (MutualRef/StreamAppend/
  MutualRefClose) with tuple-deconstruction on `AllocateVariable()`
  + tail-update on `MutualRefTerm.CurrentWriterAddr` — done.
- T21: emit SendKernel with MadContext guard + DeepDeref + functor-
  literal four-branch comparison + agent/index extraction +
  delegation to `mc.Send(...)` — done.
- T22: emit OutputKernel + public helper `FormatGroundTerm` with
  recursive type-pattern + list-sugar — done.
- T23: emit ActivateKernel with ModuleTerm/BytecodeProgram/StructTerm
  guards + SILENT-SUCCESS fallbacks + argSlots build + post-increment
  on NextGoalId + runners-cache + Gq.Enqueue — done.
- T24: close `BodyKernelsModule` class — done.

## 4. Research Findings

None required. The ratified convspec carries the full research provenance
section (lines 1538–1687 of the convspec). Idiom inventory used by this
plan, all already in the convspec:

- Cached (carry-forward; 11 idioms): `rf-dart-relative-import-to-csharp-
  namespace-using`, `rf-dart-import-relative-to-csharp-using-namespace`
  (package URIs), `rf-dart-plain-enum-to-csharp-enum`, `rf-dart-map-to-
  csharp-dictionary`, `rf-dart-is-test-narrowing-to-csharp-type-pattern-
  capture` (saturated reuse — 16+ sites), `rf-dart-is-not-type-test-to-
  csharp-is-not-pattern`, `rf-dart-record-destructure-to-csharp-
  valuetuple-deconstruction`, `rf-dart-null-bang-to-csharp-null-
  forgiving`, `rf-dart-void-function-question-to-csharp-action-nullable`,
  `rf-dart-as-cast-to-csharp-explicit-cast`, `rf-dart-postincrement-and-
  method-shape-to-csharp-equivalent`, `rf-dart-tostring-interp-to-csharp-
  tostring-interp`, `rf-dart-static-only-holder-to-csharp-static-class`,
  `rf-dart-string-interpolation-join-to-csharp-interpolation-string-
  join`, `rf-dart-top-level-pure-function-to-csharp-static-class-method`.
- New (registered by this convspec; 7 idioms): `rf-dart-math-library-to-
  csharp-system-math`, `rf-dart-num-hierarchy-to-csharp-double-with-int-
  discriminator`, `rf-dart-typedef-function-to-csharp-delegate`, `rf-
  dart-switch-on-string-to-csharp-switch-expression`, `rf-dart-num-
  conversion-to-csharp-explicit-cast-math`, `rf-dart-cons-cell-encoding-
  to-csharp-structterm-cons`, `rf-dart-datetime-now-ms-to-csharp-dto-utc-
  unixms`.

## 5. Consistency Pass

Cross-checks against the ratified convspec, sibling specs, and SCC
coherence:

- **Convspec mirror.** Every kernel, helper, enum, typedef, registry
  method, and import mirrored from `.codeconv/conversion-specs/lib/
  runtime/body_kernels.dart.md` verbatim — no novel decisions.

- **Threading-model discipline.** `MadContext.Send(...)` in SendKernel
  is the only cross-context call site in this file. The threading-model
  decision is INHERITED from mad_context.dart.md per FR-013's "don't
  double-escalate" discipline. Per CLAUDE.md context, the threading
  model is single-owning-context for `MadContext` (escalation #4,
  commit `497428c8`); the kernel's synchronous in-process `mc.Send(...)`
  call is consistent with that decision — no new escalation here.

- **Single-owning-context coherence (SCC sibling: runtime.dart).** All
  helper functions (`Deref`, `DeepDeref`, `BindResult`, `GetNum`,
  `EvaluateArithmetic`, `DartListToGlpList`, `GlpListToDartList`) and
  every kernel access `rt.Heap` / `rt.Gq` / `rt.MadContext` /
  `rt.OutputCallback` / `rt.Runners` / `rt.NextGoalId` directly with
  NO lock/Interlocked/ConcurrentDictionary — consistent with the
  single-owning-context invariant inherited from heap_fcp.dart.md
  (escalation #4) and propagated through runtime.dart.md.

- **Cross-file mutability constraint: `MutualRefTerm.CurrentWriterAddr`
  MUST be `{ get; set; }`.** `streamAppendKernel` mutates
  `refArg.currentWriterAddr` after binding; the .NET render REQUIRES
  the `MutualRefTerm.CurrentWriterAddr` property in terms.cs to be a
  mutable property (NOT get-only, NOT `init`). Constraint recorded
  here; reconciled against terms.dart.md. (Per the convspec's
  cross-file note, the carry-forward assumption is `{ get; set; }` —
  if terms.dart.md pins it as immutable, the cross-file convspec
  coherence stage MUST re-spec terms.cs. As of this plan, the
  assumption stands.)

- **Cross-file mutability constraint: `GlpRuntime.NextGoalId` MUST be
  `{ get; set; }`.** `activateKernel` does `rt.NextGoalId++`. Pinned
  in glp_runtime.dart.md (SCC sibling: runtime.dart) — carry-forward
  constraint.

- **Cross-file label-store constraint: `BytecodeProgram.Labels` MUST be
  a `Dictionary<string, long>` (or equivalent map) with `TryGetValue`
  support.** `activateKernel` does `bp.Labels.TryGetValue(label, out
  long entryPc)`. Pinned in bytecode/runner.dart.md (SCC sibling:
  runner.dart) — carry-forward constraint.

- **Cross-file BytecodeRunner-cache constraint: `GlpRuntime.Runners`
  MUST be a `Dictionary<BytecodeProgram, BytecodeRunner>` with indexer
  + `ContainsKey` support.** Pinned in glp_runtime.dart.md (SCC
  sibling: runtime.dart) — carry-forward constraint.

- **Cross-file `GoalRef` shape.** `GoalRef(newGoalId, entryPc)` →
  `new GoalRef(newGoalId, entryPc)`. Pinned in machine_state.dart.md
  — carry-forward constraint.

- **Cross-file `CallEnv` shape.** `CallEnv(args: argSlots)` → C# named-
  argument `new CallEnv(args: argSlots)`. The `CallEnv.Args` field
  must be `Dictionary<long, Term>` per the int-width policy. Pinned
  in bytecode/runner.dart.md (SCC sibling: runner.dart) — carry-
  forward constraint.

- **Cross-file `HeapFCP` surface.** `BindVariable` / `BindVariableConst`
  / `IsWriter` / `IsFullyBound` / `GetValue` / `AllocateVariable` /
  `StoreTermOnHeap` are all consumed here. Pinned in heap_fcp.dart.md
  — carry-forward constraint (single-owning-context invariant).

- **Cross-file `Term` hierarchy.** `Term`, `ConstTerm`, `StructTerm`,
  `VarRef`, `MutualRefTerm`, `ModuleTerm` are all reference types
  (sealed `class`, NOT `record`, NOT `struct`) per terms.dart.md —
  carry-forward constraint.

- **No async/await/Future/Stream/Isolate/Completer surface.** Body
  kernels are synchronous by spec (leading doc comment: "Execute
  inline (not spawned as separate goals)"). The "stream-append"
  kernel name refers to GLP cons-cell stream encoding, NOT Dart
  `Stream`. Consistent across the SCC.

- **No `dart:io` surface despite `print` calls.** Dart top-level
  `print` is `dart:core`, not `dart:io`. Maps to `Console.WriteLine`
  per Microsoft Learn — consistent with external_io.dart.md decisions.

- **Modulo divergence (Dart Euclidean vs C# sign-of-dividend) recorded
  as NUANCE, not escalation.** The leading file doc comment "Expect
  all preconditions met (guards should verify before calling)"
  authorises this deferral. If a callsite later requires Euclidean
  semantics, a per-file amendment specifies the rewrite
  `((x % y) + y) % y` — recorded here for future reference, NOT
  escalated.

## 6. Escalations

None.

## 7. Cycle Siblings

This file is in a 6-member SCC. The following cross-references record
co-dependent decisions:

### lib/bytecode/runner.dart

- `BytecodeRunner`, `BytecodeProgram`, `CallEnv` types consumed in
  `activateKernel`. `BytecodeProgram.Labels` must be
  `Dictionary<string, long>` (or equivalent) supporting `TryGetValue`
  — pinned there.
- `CallEnv` constructor must accept named argument `args:` of type
  `Dictionary<long, Term>` (per int-width carry-forward) — pinned
  there.
- `BytecodeRunner` constructor must accept a single `BytecodeProgram`
  argument — pinned there.
- No threading-model interaction (synchronous in-process call).

### lib/multiagent/mad_context.dart

- `MadContext` type + its `Send(Term term, bool isWriter, string
  gnAgent, long gnIndex, string destAgent)` method are consumed in
  `sendKernel`. The 5-argument shape, parameter types, and return
  shape (void) MUST match what's pinned in mad_context.dart.md.
- **Threading-model decision INHERITED — NOT re-decided here.** The
  single-owning-context invariant (escalation #4, commit `497428c8`)
  governs how `Send(...)` interacts with the agent's mailbox; this
  file is correct AS LONG AS the convspec for mad_context.dart pins
  a synchronous in-process method shape that the agent's owning Task
  invokes. If mad_context.dart later escalates threading semantics,
  body_kernels.dart is blocked transitively via that escalation, NOT
  via a new escalation here.

### lib/runtime/glp_activation.dart

- No direct symbol consumption in this file. Indirect coupling via
  shared `GlpRuntime` instance threading through `activateKernel`'s
  `setGoalEnv` / `setGoalProgram` / `runners` map (which
  `glp_activation.dart` may also touch when activating modules). The
  single-owning-context invariant — only the owning task mutates
  these — must hold across both files.
- No threading-model interaction beyond the inherited single-owning-
  context discipline.

### lib/runtime/runtime.dart

- `GlpRuntime` reference-type with surface `Heap` (HeapFCP), `Gq`
  (goal queue), `MadContext` (nullable MadContext), `OutputCallback`
  (`Action<string>?`), `Runners` (`Dictionary<BytecodeProgram,
  BytecodeRunner>`), `NextGoalId` (`{ get; set; long }`),
  `SetGoalEnv(long, CallEnv)`, `SetGoalProgram(long,
  BytecodeProgram)`. ALL pinned in runtime.dart.md.
- **`NextGoalId` MUST be `{ get; set; }` (NOT `init`-only) — REQUIRED
  by `activateKernel`'s `rt.NextGoalId++` post-increment.**
- **`OutputCallback` MUST be a nullable delegate (`Action<string>?`)
  with both null-check and direct-invocation legal — REQUIRED by
  `outputKernel`.**
- Single-owning-context invariant on all GlpRuntime field accesses
  inherited from heap_fcp.dart.md (escalation #4).

### lib/runtime/system_predicates.dart

- No direct symbol consumption in this file. Indirect coupling: the
  body kernels registered by `registerStandardBodyKernels` are
  LOOKED UP from system_predicates.dart (the kernel-dispatch
  consumers — `_add`, `_send`, `_activate`, etc.). The
  string-key shape `"$name/$arity"` is the cross-file contract.
- The 36 kernel names + arities registered here MUST match the
  lookup-site expectations in system_predicates.dart byte-
  identically (the slash separator and kernel-name spelling are
  load-bearing). Cross-file string-key coherence MUST be verified
  during cross-file convspec coherence.
