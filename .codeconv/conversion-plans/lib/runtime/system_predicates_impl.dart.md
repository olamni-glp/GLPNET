---
path: lib/runtime/system_predicates_impl.dart
cycle_group_id: 47
scc_siblings: []
generated_at: 2026-05-21T16:19:41Z
source_sha256: f375832b51bddc0746bf5c13c0702986723948125e255459e20f206af7f7f50e
schema_version: 1
---

# Conversion Plan: lib/runtime/system_predicates_impl.dart

## 1. Source Analysis

Verified directly against `glp_runtime_net/lib/runtime/system_predicates_impl.dart` (1928 lines, sha256 `f375832b…`). The file is the concrete implementation of the registry-and-call-context machinery declared in `system_predicates.dart`. Its surface is:

- **Imports (4):** one SDK import `dart:io` (brings `File`, `Directory`, `RandomAccessFile`, `FileMode`, `stdin`, `String.fromCharCodes`); three relative imports `runtime.dart` (→ `GlpRuntime`), `system_predicates.dart` (→ `SystemPredicate`, `SystemCall`, `SystemPredicateRegistry`, `SystemResult`), `terms.dart` (→ `Term`, `VarRef`, `ConstTerm`, `StructTerm`). No `show`/`hide`.
- **Top-of-file doc-comment (lines 1–7):** triple-slash block enumerating "Arithmetic evaluation / File I/O operations / System information (time, IDs, etc.)". No `library;` directive.
- **One top-level void function `registerStandardPredicates`** (lines 16–48): 16 `registry.register(<literal>, <predFn>)` calls in 7 comment-delimited family groups (`// Arithmetic`, `// Utilities`, `// File I/O`, `// Directory operations`, `// Terminal I/O`, `// Module loading`, `// Channel primitives`). Pure dispatch table; only side effect is mutating the passed registry.
- **One mutable file-private top-level int** `int _uniqueIdCounter = 1;` (line 428). Single-threaded post-increment (`_uniqueIdCounter++`) by `uniqueIdPredicate`.
- **Sixteen predicate functions** all matching the `SystemPredicate` delegate `SystemResult <name>(GlpRuntime rt, SystemCall call)`:
  1. `evaluatePredicate` (l.70) — arity 2; ground-check walk `collectUnbound`; recursive `_evaluate`; bind-or-verify of result via `bindWriterConst` (primitive) or `bindVariable` (Term).
  2. `currentTimePredicate` (l.379) — arity 1; `DateTime.now().millisecondsSinceEpoch`.
  3. `uniqueIdPredicate` (l.430) — arity 1; `_uniqueIdCounter++`.
  4. `fileReadPredicate` (l.480) — arity 2; `File(path).existsSync` + `readAsStringSync`.
  5. `fileWritePredicate` (l.579) — arity 2; `File(path).writeAsStringSync(contents)`.
  6. `readPredicate` (l.680) — arity 1; `stdin.readLineSync()`.
  7. `fileExistsPredicate` (l.743) — arity 1; `File(path).existsSync`.
  8. `fileOpenPredicate` (l.806) — arity 3; 4-arm `switch (mode)` to `FileMode.read|write|append|writeOnly`; allocates handle via `rt.allocateFileHandle`.
  9. `fileClosePredicate` (l.951) — arity 1; `rt.isValidHandle` + `rt.closeFileHandle`.
  10. `fileReadHandlePredicate` (l.1014) — arity 2; `file.lengthSync()`, `file.positionSync()`, `file.readSync(remaining)`, `String.fromCharCodes(bytes)`.
  11. `fileWriteHandlePredicate` (l.1117) — arity 2; `file.writeStringSync(contents)`.
  12. `directoryListPredicate` (l.1223) — arity 2; `Directory(path).existsSync` + `listSync().map((e) => e.path.split('/').last).toList()` (POSIX-locked).
  13. `variableNamePredicate` (l.1311) — arity 2; `'W${addr}'` or `'R${addr}'`.
  14. `copyTermPredicate` (l.1374) — arity 2; `_deepCopyTerm` with `visited` Map for cycle detection.
  15. `linkPredicate` (l.1516) — arity 2; `List.every((e)=>e is String)` + `value.cast<String>()` validation; `rt.loadLibrary(modulePaths.first)`.
  16. `loadModulePredicate` (l.1634) — arity 2; `File.existsSync` + `readAsStringSync`; builds mixed-value `Map<String,Object>` literal `{ 'path': filePath, 'contents': contents, 'loaded_at': DateTime.now().millisecondsSinceEpoch }`.
  17. `distributeStreamPredicate` (l.1740) — arity 2; iterates output writers, `_deepCopyValue` per writer.
  18. `copyTermMultiPredicate` (l.1831) — arity 3; two independent `_deepCopyValue` outputs.
- **Three private helpers:**
  - `_evaluate(GlpRuntime rt, Object? term) → Object?` (l.212) — recursive arithmetic; dereferences VarRef writer/reader via `rt.heap.getValue` / `rt.heap.getReaderValue`; unwraps `ConstTerm`; switch on functor with 10 arms (`+`, `-`, `*`, `/`, `mod`, `<`, `>`, `=<`, `>=`, `=`); returns `null` on type-mismatch / div-by-zero / unknown-operator.
  - `_deepCopyTerm(Object? term, GlpRuntime rt, Map<int, Object?> visited) → Object?` (l.1432) — cycle-aware deep copy; `visited` keyed by `VarRef.addr`; placeholder-then-overwrite (`visited[varId] = null` then `visited[varId] = copiedValue`); StructTerm rebuilds new `<Term>[]` with `ConstTerm(null)` for null and `ConstTerm(raw)` for non-Term raw values.
  - `_deepCopyValue(Object? value) → Object?` (l.1911) — shallow-recursive over `List`/`Map`/`Set`; shares primitives; **no** cycle detection; **no** heap deref.
- **Eleven `try { … } catch (e) { print('[ERROR] …: $e'); return SystemResult.failure; }` blocks** (lines 525, 532, 653, 783, 879, 900, 1063, 1196, 1265, 1583, 1679, 1715 — eleven distinct sites covering the I/O-bearing predicates).
- **~30 diagnostic `print('[ERROR]/[WARN]/[EVALUATE]/[DEBUG] …')` emissions.** The 9 `[EVALUATE]` prints in `evaluatePredicate` are debug traces; the `[ERROR]` and `[WARN]` prints route to stderr-channel intent.
- **Host-side runtime helpers referenced** (declared in `runtime.dart`): `rt.heap.isWriter(addr)`, `rt.heap.isReader(addr)`, `rt.heap.isWriterBound(wid)`, `rt.heap.isReaderBound(rid)`, `rt.heap.getValue(wid)`, `rt.heap.getReaderValue(rid)`, `rt.heap.bindWriterConst(wid, value)`, `rt.heap.bindVariable(wid, term)`, `rt.allocateFileHandle(file) → int`, `rt.closeFileHandle(handle)`, `rt.isValidHandle(handle) → bool`, `rt.getFile(handle) → RandomAccessFile?`, `rt.loadLibrary(path) → int`. None are declared in this file.
- **Repeating templates** (LOAD-BEARING shapes, account for ~70% of file bulk):
  - "Writer-or-Reader-or-Const ladder" for argument extraction — appears verbatim in ~14 call sites.
  - "bind-or-verify" branch pair — appears verbatim in ~12 call sites.

No `Future`/`Stream`/`async`/`await`/`Isolate`/`Completer` surface anywhere; every I/O call is the `*Sync` variant. The threading-model decision (synchronous predicate dispatch, single-threaded mutable counter) is inherited from prior runtime/* specs and from escalation #4 (heap_fcp single-owning-context) — NOT re-decided here.

## 2. Dart → C#/.NET Conversion Plan

Each construct row mirrors the ratified convspec's structured-YAML construct list. The C# target file is `lib/runtime/system_predicates_impl.cs` per the tombstone `target_path`. All renderings hosted on an `internal static class SystemPredicatesImpl` (file-level holder, NOT instantiable) per the static-only-holder carry-forward.

### C1. Import directives → `using` namespaces

- **Dart:** `import 'dart:io';` + three relative imports.
- **C#:** Collapse imports into a using-directive family:
  - `using System;` — for `Console`, `DateTimeOffset`.
  - `using System.Collections.Generic;` — for `Dictionary`, `HashSet`, `List`.
  - `using System.IO;` — for `File`, `Directory`, `FileStream`, `FileMode`, `FileAccess`, `Path`.
  - `using System.Linq;` — for `.All`, `.Cast<T>`, `.Select`, `.ToList`.
  - `using System.Text;` — for `Encoding.UTF8`.
  - `using <root>.Runtime;` — covers `terms.cs` / `runtime.cs` / `system_predicates.cs` sibling-file imports (one `using` per the cached carry-forward `rf-dart-import-relative-to-csharp-using-namespace`).
- **Idiom / research:** `rf-dart-dartio-to-csharp-system-io-family` (NEW family, official-docs grounded across nine `dart:io` surfaces) + `rf-dart-import-relative-to-csharp-using-namespace` (cached saturation).
- **Nuance:** Dart `dart:io` is one library; .NET splits the same surfaces across five .NET namespaces. Faithful 1:1 — same call surfaces, target-language-canonical grouping.

### C2. Top-of-file doc-comment → XML-doc on namespace

- **Dart:** 7-line `///` block; no `library;` directive.
- **C#:** XML-doc on the namespace declaration: `<summary>Standard system predicate implementations for GLP</summary><remarks>This module provides the built-in system predicates that can be called via the Execute instruction. These predicates handle:<list type="bullet"><item><description>Arithmetic evaluation</description></item><item><description>File I/O operations</description></item><item><description>System information (time, IDs, etc.)</description></item></list></remarks>`.
- **Idiom / research:** `rf-dart-library-directive-to-csharp-namespace-elision` (cached carry-forward, saturated).
- **Nuance:** Library-directive elision; doc-content faithful 1:1.

### C3. `registerStandardPredicates` → `public static void RegisterStandardPredicates(SystemPredicateRegistry registry)`

- **Dart:** Single void top-level function with 16 `registry.register('name', predFn)` calls grouped into 7 `//`-comment family blocks. Bare predicate names are tear-offs to the `SystemPredicate` typedef.
- **C#:** Public static method on `SystemPredicatesImpl`:
  - Body: 16 `registry.Register("name", MethodGroup);` statements preserving the 7 family `// Comment` groups verbatim (e.g. `// Arithmetic`, `// Utilities`, `// File I/O`, `// Directory operations`, `// Terminal I/O`, `// Module loading`, `// Channel primitives`).
  - Bare-method references are method-group conversions to the `SystemPredicate` delegate from `system_predicates.cs` (Microsoft Learn: "A method group can be assigned to a delegate of a matching signature.").
- **Idiom / research:** `rf-dart-static-only-holder-to-csharp-static-class` (cached) + `rf-dart-typedef-function-to-csharp-delegate` (cached).
- **Nuance:** Static-only-holder + method-group both carry-forward saturated; no new research.

### C4. Predicate-function template → 16× `public static SystemResult <Name>Predicate(GlpRuntime rt, SystemCall call)`

- **Dart:** Each predicate body follows the same five-step template: arity check → 3-arm extraction ladder per input arg → post-extraction null-check + diagnostic → side effect → bind-or-verify on output writer → return `SystemResult.success/failure/suspend`.
- **C#:** 16 public static methods on `SystemPredicatesImpl`, each preserving the OBSERVABLE contract surface (same arity-check, same suspend-on-unbound-reader semantics, same bind-or-verify semantics, same diagnostic prefixes). The repetitive three-arm extraction ladder and bind-or-verify branch pair are CONSOLIDATED into private helpers (C5 below) — the call sites shrink from ~30 lines to ~3 lines each while preserving observable semantics.
  - `SystemResult.success` → `SystemResult.Success`, `SystemResult.failure` → `SystemResult.Failure`, `SystemResult.suspend` → `SystemResult.Suspend` (per the convspec enum mapping in system_predicates.dart.md).
  - `call.suspendedReaders.add(rid)` → `call.SuspendedReaders.Add(rid)`.
  - `rt.heap.<method>` → `rt.Heap.<Method>` (PascalCase, per the heap_fcp.dart.md surface).
- **Idiom / research:** `rf-dart-repeated-three-arm-term-extraction-to-csharp-helper` (NEW, LOAD-BEARING — applies to 14 sites) + `rf-dart-bind-or-verify-writer-to-csharp-helper-method` (NEW, LOAD-BEARING — applies to 12 sites).
- **Nuance:** Template-consolidation is faithful to source CONTRACT, not a refactor; without it the converted file is ~3× source size.

### C5. Consolidation helpers (emitted once, called 14+/12+ times)

- **Dart:** Not present in source (the source repeats the template inline).
- **C#:** Five private static helpers on `SystemPredicatesImpl`:
  - `private static bool TryExtractString(SystemCall call, int argIndex, GlpRuntime rt, out string? value, out SystemResult earlyReturn)` — three-arm ladder for `ConstTerm<string>` / `VarRef`-writer-bound / `VarRef`-reader-suspend-on-unbound; returns `true` on extracted, `false` with `earlyReturn` populated otherwise.
  - `private static bool TryExtractInt(SystemCall call, int argIndex, GlpRuntime rt, out long? value, out SystemResult earlyReturn)` — same shape typed for `long` (int-width carry-forward).
  - `private static bool TryExtractTerm(SystemCall call, int argIndex, GlpRuntime rt, out object? value, out SystemResult earlyReturn)` — same shape for `Object?`-typed extraction (used by `EvaluatePredicate`, `CopyTermPredicate`, channel predicates).
  - `private static SystemResult BindOrVerifyConst(GlpRuntime rt, long wid, object? value)` — `if (rt.Heap.IsWriterBound(wid)) { var existing = rt.Heap.GetValue(wid); bool matches = existing is ConstTerm c && Equals(c.Value, value) || Equals(existing, value); return matches ? SystemResult.Success : SystemResult.Failure; } rt.Heap.BindWriterConst(wid, value); return SystemResult.Success;`.
  - `private static SystemResult BindOrVerifyTerm(GlpRuntime rt, long wid, Term term)` — dual to `BindOrVerifyConst` with `rt.Heap.BindVariable(wid, term)` on unbound (covers `EvaluatePredicate`'s `result is Term` arm and `CopyTermPredicate`'s `copy is Term` arm).
- **Idiom / research:** `rf-dart-repeated-three-arm-term-extraction-to-csharp-helper` + `rf-dart-bind-or-verify-writer-to-csharp-helper-method` (both NEW, LOAD-BEARING).
- **Nuance:** Microsoft Learn `out` parameter modifier — TryX-pattern with `bool` retval is .NET-canonical for "result-or-early-return"; `Equals(object, object)` boxed-equality matches Dart `==` semantics for primitives.

### C6. `_uniqueIdCounter` mutable top-level int → `private static long`

- **Dart:** `int _uniqueIdCounter = 1;` + `final newId = _uniqueIdCounter++;` (post-increment).
- **C#:** `private static long _uniqueIdCounter = 1L;` on the holder class + `long newId = _uniqueIdCounter++;`.
- **Idiom / research:** `rf-dart-mutable-toplevel-counter-postincrement-to-csharp-static-field` (NEW) + `rf-dart-postincrement-and-method-shape-to-csharp-equivalent` (cached for `++`).
- **Nuance:** NOT `Interlocked.Increment(ref _uniqueIdCounter)` — atomicity is not a source contract; the runtime is single-threaded per the inherited threading-model decision (escalation #4 carry-forward). Int-width: Dart `int` is 64-bit on the VM; C# `int` is 32-bit; faithful render is `long`.

### C7. `_evaluate` recursive arithmetic switch → `private static object? Evaluate(GlpRuntime rt, object? term)`

- **Dart:** Recursive evaluator; dereferences `VarRef` writers/readers via heap; unwraps `ConstTerm.value`; `switch (functor)` with 10 arms (`+`, `-`, `*`, `/`, `mod`, `<`, `>`, `=<`, `>=`, `=`); each arm checks `args.length == 2` then `left is num && right is num` (or `int` for `mod`); returns `null` on type-mismatch / div-by-zero / unknown operator.
- **C#:** Private static method on holder class. Body: faithful 1:1 — recursive `rt.Heap.GetValue` / `rt.Heap.GetReaderValue`, `ConstTerm.Value` unwrap, then C# `switch (functor)` on `string` with the same 10 arms. Each arithmetic arm narrows operands to `double`; the `'mod'` arm narrows to `long`; relational arms return `bool`; div-by-zero returns `null`.
  - Arithmetic arms shape: `case "+": if (args.Count == 2 && Evaluate(rt, args[0]) is double l && Evaluate(rt, args[1]) is double r) return l + r; break;`.
  - `mod` arm: `case "mod": if (args.Count == 2 && Evaluate(rt, args[0]) is long li && Evaluate(rt, args[1]) is long ri) { if (ri == 0L) return null; return li % ri; } break;`.
  - `/` arm: explicit `right == 0` check before `return left / right;` (faithful Dart shape) — division by zero returns `null`, NOT `DivideByZeroException` (the narrowing to `double` would yield `Infinity` in CLR, but the source's explicit guard pre-empts that — preserved verbatim).
- **Idiom / research:** `rf-dart-switch-on-string-to-csharp-switch-expression` (cached) + `rf-dart-num-hierarchy-to-csharp-double-with-int-discriminator` (cached).
- **Nuance:** Result type is `object?` (heterogeneous — arithmetic arms return `double`, `mod` returns `long`, relational arms return `bool`). The `'='` arm is numeric equality (NOT Dart structural equality) — preserved verbatim. No async/Stream surface.

### C8. `_deepCopyTerm` cycle-aware deep copy → `private static object? DeepCopyTerm(object? term, GlpRuntime rt, Dictionary<long, object?> visited)`

- **Dart:** Cycle detection via `visited.containsKey(varId)`; placeholder write `visited[varId] = null;` pre-recursion then overwrite `visited[varId] = copiedValue;` post-recursion. Shares `ConstTerm` / `num` / `String` immutables. StructTerm rebuilds new `<Term>[]` accumulator with `ConstTerm(null)` for null and `ConstTerm(raw)` for non-Term raw values, returns `new StructTerm(functor, copiedArgs)`.
- **C#:** Private static method on holder class. Body: faithful 1:1.
  - VarRef branch: `if (visited.ContainsKey(varId)) return visited[varId];` then heap-deref of bound value; `visited[varId] = null;` placeholder; `var copiedValue = DeepCopyTerm(value, rt, visited);`; `visited[varId] = copiedValue;`; `return copiedValue;`.
  - StructTerm branch: `var copiedArgs = new List<Term>(); foreach (var arg in term.Args) { var copied = DeepCopyTerm(arg, rt, visited); if (copied is Term t) copiedArgs.Add(t); else if (copied != null) copiedArgs.Add(new ConstTerm(copied)); else copiedArgs.Add(new ConstTerm(null)); } return new StructTerm(term.Functor, copiedArgs);`.
  - Immutable share-arms: `if (term is ConstTerm) return term; if (term is double) return term; if (term is long) return term; if (term is string) return term;`.
- **Idiom / research:** `rf-dart-cycle-aware-deepcopy-visited-map-to-csharp-equivalent` (NEW, LOAD-BEARING).
- **Nuance:** `Dictionary<long, object?>` indexer-get is safe because preceded by `ContainsKey` (Microsoft Learn `Dictionary<TKey,TValue>.Item[TKey]` cites the ContainsKey-guard as the safe-usage pattern). DIFFERENTIATING from system_predicates.dart.md's registry case which required `TryGetValue` (unguarded get). Int-width: `Map<int, Object?>` → `Dictionary<long, object?>`. Null-safety: `Dictionary<long, object?>` accepts null values.

### C9. `_deepCopyValue` shallow-recursive collection copy → `private static object? DeepCopyValue(object? value)`

- **Dart:** Recursive over `List` / `Map` / `Set`; shares primitives (`String`, `num`, `bool`); no cycle detection; no heap deref.
- **C#:** Private static method. Body:
  - `if (value is null) return null;`
  - `if (value is List<object?> list) return list.Select(DeepCopyValue).ToList();` (mirrors Dart `value.map((e) => _deepCopyValue(e)).toList()`).
  - `if (value is Dictionary<object, object?> map) return map.ToDictionary(kv => kv.Key, kv => DeepCopyValue(kv.Value));` (mirrors Dart `value.map((k,v) => MapEntry(k, _deepCopyValue(v)))`).
  - `if (value is HashSet<object?> set) return new HashSet<object?>(set.Select(DeepCopyValue));` (mirrors Dart `value.map((e) => _deepCopyValue(e)).toSet()`).
  - `if (value is string || value is double || value is long || value is bool) return value;` (immutable share).
  - Default: `return value;` (unknown type, share-as-is — matches Dart fallthrough).
- **Idiom / research:** `rf-dart-cycle-aware-deepcopy-visited-map-to-csharp-equivalent` (companion to `DeepCopyTerm`, same family).
- **Nuance:** No cycle detection (faithful to Dart — the source explicitly omits the `visited` parameter for this helper). LINQ projection idioms are the .NET-canonical render for the Dart `.map(...).toList/Set` chains.

### C10. `dart:io` synchronous file/dir/handle/terminal I/O family

- **Dart:** Nine API surfaces converge: `File(path).existsSync()`, `File.readAsStringSync()`, `File.writeAsStringSync()`, `File.openSync(mode: FileMode.X)` returning `RandomAccessFile` with `lengthSync`/`positionSync`/`readSync(n)`/`writeStringSync`, `Directory(path).existsSync()` + `.listSync()`, `stdin.readLineSync()`, `String.fromCharCodes(bytes)`, `DateTime.now().millisecondsSinceEpoch`.
- **C#:** Faithful family render (all blocking-synchronous, NOT `*Async`):
  - `File(path).existsSync()` → `File.Exists(path)`.
  - `File(path).readAsStringSync()` → `File.ReadAllText(path, Encoding.UTF8)`.
  - `File(path).writeAsStringSync(contents)` → `File.WriteAllText(path, contents, Encoding.UTF8)`.
  - `File(path).openSync(mode: FileMode.X)` → `new FileStream(path, <FileMode-arm>, <FileAccess-arm>)` per the 4-case switch (C11 below).
  - `RandomAccessFile.lengthSync()` → `FileStream.Length` (property; `long`).
  - `RandomAccessFile.positionSync()` → `FileStream.Position` (property; `long`).
  - `RandomAccessFile.readSync(remaining)` → `var bytes = new byte[remaining]; var read = file.Read(bytes, 0, (int)remaining);` (read into byte buffer).
  - `RandomAccessFile.writeStringSync(contents)` → `var bytes = Encoding.UTF8.GetBytes(contents); file.Write(bytes, 0, bytes.Length);`.
  - `Directory(path).existsSync()` → `Directory.Exists(path)`.
  - `Directory(path).listSync().map((e) => e.path.split('/').last).toList()` → `Directory.EnumerateFileSystemEntries(path).Select(Path.GetFileName).ToList()` (cross-platform-correct — see C13).
  - `stdin.readLineSync()` → `Console.In.ReadLine()` (returns `string?`).
  - `String.fromCharCodes(bytes)` → `Encoding.UTF8.GetString(bytes, 0, read)` (UTF-8 round-trip — see nuance).
  - `DateTime.now().millisecondsSinceEpoch` → `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` (cached `rf-dart-datetime-now-ms-to-csharp-dto-utc-unixms`).
- **Idiom / research:** `rf-dart-dartio-to-csharp-system-io-family` (NEW LOAD-BEARING family, FR-024 official-docs grounded across all nine surfaces) + `rf-dart-datetime-now-ms-to-csharp-dto-utc-unixms` (cached).
- **Nuance:** All `*Sync` Dart calls map to blocking-synchronous .NET calls — NOT `*Async` — per the inherited threading-model decision. UTF-8 encoding default matches Dart `writeStringSync` + round-trips bytes produced by `writeStringSync` to/from `String.fromCharCodes` correctly. `File` / `Directory` are static classes in .NET — calls are `File.X(path)` not `new File(path).X()` (DIFFERENTIATING from Dart instantiate-then-call shape).

### C11. `FileMode` 4-value enum → `(FileMode, FileAccess)` PAIR switch (in `FileOpenPredicate`)

- **Dart:** `switch (mode) { case 'read': fileObj.openSync(mode: FileMode.read); … case 'read_write': fileObj.openSync(mode: FileMode.writeOnly); … default: print('[ERROR] …'); return SystemResult.failure; }` — 4 string-key arms + default.
- **C#:** `FileStream file; switch (mode) { case "read": file = new FileStream(path, FileMode.Open, FileAccess.Read); break; case "write": file = new FileStream(path, FileMode.Create, FileAccess.Write); break; case "append": file = new FileStream(path, FileMode.Append, FileAccess.Write); break; case "read_write": file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite); break; default: Console.Error.WriteLine($"[ERROR] file_open/3: invalid mode: {mode} (must be read/write/append/read_write)"); return SystemResult.Failure; }`.
- **Idiom / research:** `rf-dart-filemode-to-csharp-filemode-fileaccess-pair` (NEW, LOAD-BEARING DIVERGENCE).
- **Nuance:** Dart `FileMode` collapses access intent and create-or-open intent into ONE enum; .NET splits across TWO orthogonal enums (`System.IO.FileMode` + `System.IO.FileAccess`). The `FileStream(string, FileMode, FileAccess)` ctor requires both — the PAIR is mandatory per Microsoft Learn "FileStream Constructor". The Dart `'read_write'`-arm mapping to `FileMode.writeOnly` is a likely source typo; the .NET render emits `(FileMode.Create, FileAccess.ReadWrite)` for the apparent INTENT — recorded as a divergence note, NOT escalated (the spec faithfully captures source surface + target intent per convspec discipline).

### C12. `try { … } catch (e) { print('[ERROR] …: $e'); return SystemResult.failure; }` family (11 sites)

- **Dart:** Untyped `catch (e)` + `print('[ERROR] <name>/<arity>: <verb> <param>: $e')` + `return SystemResult.failure;`.
- **C#:** `try { … } catch (Exception ex) { Console.Error.WriteLine($"[ERROR] <name>/<arity>: <verb> {<param>}: {ex.Message}"); return SystemResult.Failure; }`.
- **Idiom / research:** `rf-dart-trycatch-untyped-with-error-print-to-csharp-catch-exception-stderr` (NEW family, applies 11 times in this file).
- **Nuance:** Dart untyped catch catches any thrown object; .NET `catch (Exception)` catches any CLR exception (faithful 1:1 — .NET has no non-Exception throwables in practice). `[ERROR]`-prefixed prints route to `Console.Error.WriteLine` (stderr-channel intent per repl_play_runner.dart.md `rf-dart-print-to-stderr-on-error`). The `~30` non-try/catch `[ERROR]`-prefixed prints (arity-fail diagnostics, type-mismatch diagnostics, unbound-writer diagnostics) ALSO route to `Console.Error.WriteLine` for consistency. The `[WARN]`-prefixed prints (2 sites: `directory_list/2` and `load_module/2`) also route to `Console.Error.WriteLine`. The 9 `[EVALUATE]` prints in `EvaluatePredicate` are debug traces — route to `Console.Out.WriteLine` and may be gated behind `#if DEBUG` at codegen discretion (downstream codegen decision, not an escalation).

### C13. `dir.listSync().map((e) => e.path.split('/').last).toList()` → `Directory.EnumerateFileSystemEntries + Path.GetFileName`

- **Dart:** POSIX-locked filename extraction.
- **C#:** `Directory.EnumerateFileSystemEntries(path).Select(Path.GetFileName).ToList()` (cross-platform-correct).
- **Idiom / research:** `rf-dart-path-split-slash-last-to-csharp-path-getfilename` (NEW, LOAD-BEARING DIVERGENCE — INTENT-preserving fix).
- **Nuance:** Microsoft Learn "Path.GetFileName(String) Method" — "Returns the file name and extension of the specified path string." Cross-platform separator handling per `Path.DirectorySeparatorChar`. The Dart source's `.path.split('/').last` silently breaks on Windows-returned backslash paths; the .NET render is intent-preserving (the Dart source clearly intends "the filename portion"; only the MECHANISM is POSIX-locked). Recorded as an explicit divergence nuance, not a behavioural change in INTENT.

### C14. Mixed-value `Map<String, Object>` literal in `LoadModulePredicate` → `Dictionary<string, object?>` initializer

- **Dart:** `final module = { 'path': filePath, 'contents': contents, 'loaded_at': DateTime.now().millisecondsSinceEpoch };`.
- **C#:** `var module = new Dictionary<string, object?> { ["path"] = filePath, ["contents"] = contents, ["loaded_at"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };`.
- **Idiom / research:** `rf-dart-mixed-value-map-literal-to-csharp-dictionary-init` (NEW) + `rf-dart-datetime-now-ms-to-csharp-dto-utc-unixms` (cached).
- **Nuance:** Microsoft Learn "Object and Collection Initializers — Dictionary Initializers" prescribes the `["key"] = value` indexer initialiser syntax as canonical. `object?` value-type is faithful to Dart `Object?` for the heterogeneous values (string / string / long). No reference-vs-value complication beyond the Dictionary-is-reference-type carry-forward.

### C15. `value.every((e) => e is String) && value.cast<String>()` → `list.All(e => e is string) && list.Cast<string>().ToList()` (in `LinkPredicate`)

- **Dart:** `if (value.every((e) => e is String)) modulePaths = value.cast<String>(); else …`.
- **C#:** `if (list.All(e => e is string)) modulePaths = list.Cast<string>().ToList();`.
- **Idiom / research:** `rf-dart-list-every-cast-to-csharp-linq-all-cast` (NEW).
- **Nuance:** Microsoft Learn `Enumerable.All<TSource>` (short-circuits on first false) + `Enumerable.Cast<TResult>` (throws InvalidCastException on bad element — but pre-validated by `.All` here so the cast is safe). `.ToList()` materialises the lazy `IEnumerable<string>` into a `List<string>`.

### C16. Variable-name string interpolation → `$"W{addr}"` / `$"R{addr}"`

- **Dart:** `name = 'W${varTerm.addr}';` / `name = 'R${varTerm.addr}';`.
- **C#:** `name = $"W{varTerm.Addr}";` / `name = $"R{varTerm.Addr}";`.
- **Idiom / research:** `rf-dart-string-interpolation-to-csharp-interpolation` (cached from runner.dart.md and others, saturated). No new research.
- **Nuance:** Dart `${expr}` → C# `{expr}` inside interpolated string; faithful 1:1.

### C17. Heap host-side helper surface (PascalCase)

- **Dart:** `rt.heap.isWriter`, `isReader`, `isWriterBound`, `isReaderBound`, `getValue`, `getReaderValue`, `bindWriterConst`, `bindVariable`; `rt.allocateFileHandle`, `closeFileHandle`, `isValidHandle`, `getFile`, `loadLibrary`.
- **C#:** `rt.Heap.IsWriter`, `IsReader`, `IsWriterBound`, `IsReaderBound`, `GetValue`, `GetReaderValue`, `BindWriterConst`, `BindVariable`; `rt.AllocateFileHandle`, `CloseFileHandle`, `IsValidHandle`, `GetFile`, `LoadLibrary`.
- **Idiom / research:** Carry-forward from heap_fcp.dart.md and runtime.dart.md surface mappings. No new research.
- **Nuance:** All targets are declared in sibling files (`heap_fcp.cs`, `runtime.cs`) — referenced via the `using <root>.Runtime;` directive. `rt.Heap` is a property exposing the heap (per heap_fcp.dart.md ratified surface).

### C18. `addr` int-width carry-forward

- **Dart:** `VarRef.addr` is `int` (64-bit on the VM).
- **C#:** `VarRef.Addr` is `long`.
- **Idiom / research:** Carry-forward from terms.dart.md / heap_fcp.dart.md int-width nuance.
- **Nuance:** All local variables holding `addr` values (e.g. `wid`, `rid`, `handle`) typed `long` in C#. Saturated; no new research.

## 3. Decomposed Task Units

- **T1.** Emit file-header XML-doc comment block on the namespace declaration mirroring the Dart 7-line doc-comment (lines 1–7) — see C2.
- **T2.** Emit using-directive family: `using System; using System.Collections.Generic; using System.IO; using System.Linq; using System.Text; using <root>.Runtime;` — see C1.
- **T3.** Open `internal static class SystemPredicatesImpl` and emit `private static long _uniqueIdCounter = 1L;` field — see C3, C6.
- **T4.** Emit `public static void RegisterStandardPredicates(SystemPredicateRegistry registry)` with 16 `registry.Register("name", MethodGroup);` statements preserving the 7 `// family-comment` groups — see C3.
- **T5.** Emit `public static SystemResult EvaluatePredicate(GlpRuntime rt, SystemCall call)` — arity 2; `collectUnbound` walk (local function or extracted helper) to detect unbound-writer/reader; suspend on unbound readers; delegate to `Evaluate(rt, exprTerm)` on ground; `BindOrVerifyConst` (primitive result) or `BindOrVerifyTerm` (Term result) dispatch on `resultTerm`. Preserve `[EVALUATE]` debug prints (routed to `Console.Out.WriteLine`).
- **T6.** Emit `public static SystemResult CurrentTimePredicate(GlpRuntime rt, SystemCall call)` — arity 1; `var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();`; `BindOrVerifyConst(rt, wid, now)` for the writer arm; `ConstTerm` arm via `Equals(constTerm.Value, now)`.
- **T7.** Emit `public static SystemResult UniqueIdPredicate(GlpRuntime rt, SystemCall call)` — arity 1; `long newId = _uniqueIdCounter++;`; same bind-or-verify shape as T6.
- **T8.** Emit `public static SystemResult FileReadPredicate(GlpRuntime rt, SystemCall call)` — arity 2; `TryExtractString(call, 0, rt, out var path, out var er)` then `File.Exists(path)` + `File.ReadAllText(path, Encoding.UTF8)` wrapped in `try/catch (Exception ex) { Console.Error.WriteLine(...); return SystemResult.Failure; }`; then `BindOrVerifyConst(rt, wid, contents)`.
- **T9.** Emit `public static SystemResult FileWritePredicate(GlpRuntime rt, SystemCall call)` — arity 2; two `TryExtractString` calls (path, contents); `File.WriteAllText(path, contents, Encoding.UTF8)` in try/catch; returns Success.
- **T10.** Emit `public static SystemResult FileExistsPredicate(GlpRuntime rt, SystemCall call)` — arity 1; `TryExtractString` then `File.Exists(path) ? SystemResult.Success : SystemResult.Failure`.
- **T11.** Emit `public static SystemResult FileOpenPredicate(GlpRuntime rt, SystemCall call)` — arity 3; `TryExtractString` (path, mode); 4-arm `switch (mode)` per C11 producing `(FileMode, FileAccess)` pair → `new FileStream(path, fmode, faccess)` in try/catch; `var handle = rt.AllocateFileHandle(file);` then `BindOrVerifyConst(rt, wid, handle)` (with failure-cleanup `rt.CloseFileHandle(handle)` on verify-mismatch).
- **T12.** Emit `public static SystemResult FileClosePredicate(GlpRuntime rt, SystemCall call)` — arity 1; `TryExtractInt` for handle; `if (rt.IsValidHandle(handle)) { rt.CloseFileHandle(handle); return SystemResult.Success; } else return SystemResult.Failure;`.
- **T13.** Emit `public static SystemResult FileReadHandlePredicate(GlpRuntime rt, SystemCall call)` — arity 2; `TryExtractInt` for handle; `rt.GetFile(handle)` returning `FileStream?`; `file.Length - file.Position` → remaining bytes; `var bytes = new byte[remaining]; var read = file.Read(bytes, 0, (int)remaining); var contents = Encoding.UTF8.GetString(bytes, 0, read);` in try/catch; then `BindOrVerifyConst(rt, wid, contents)`.
- **T14.** Emit `public static SystemResult FileWriteHandlePredicate(GlpRuntime rt, SystemCall call)` — arity 2; `TryExtractInt` (handle), `TryExtractString` (contents); `rt.GetFile(handle)`; `var bytes = Encoding.UTF8.GetBytes(contents); file.Write(bytes, 0, bytes.Length);` in try/catch; returns Success.
- **T15.** Emit `public static SystemResult DirectoryListPredicate(GlpRuntime rt, SystemCall call)` — arity 2; `TryExtractString` (path); `if (!Directory.Exists(path)) return SystemResult.Failure;`; `var entries = Directory.EnumerateFileSystemEntries(path).Select(Path.GetFileName).ToList();` in try/catch; then `if (rt.Heap.IsWriterBound(wid)) { Console.Error.WriteLine("[WARN] directory_list/2: List verification not fully implemented"); return SystemResult.Failure; } rt.Heap.BindWriterConst(wid, entries); return SystemResult.Success;` (preserve the source's deliberate "verification not implemented" failure).
- **T16.** Emit `public static SystemResult ReadPredicate(GlpRuntime rt, SystemCall call)` — arity 1; `var line = Console.In.ReadLine();` in try/catch (returns `string?`; `if (line == null) return SystemResult.Failure;`); then `BindOrVerifyConst(rt, wid, line)`.
- **T17.** Emit `public static SystemResult VariableNamePredicate(GlpRuntime rt, SystemCall call)` — arity 2; build `name = rt.Heap.IsWriter(varTerm.Addr) ? $"W{varTerm.Addr}" : $"R{varTerm.Addr}"`; then `BindOrVerifyConst(rt, wid, name)`.
- **T18.** Emit `public static SystemResult CopyTermPredicate(GlpRuntime rt, SystemCall call)` — arity 2; `TryExtractTerm` for original (with suspend-on-unbound-reader); `var visited = new Dictionary<long, object?>(); var copy = DeepCopyTerm(original, rt, visited);`; then `if (copy is Term t) BindOrVerifyTerm(rt, wid, t); else BindOrVerifyConst(rt, wid, copy);`.
- **T19.** Emit `public static SystemResult LinkPredicate(GlpRuntime rt, SystemCall call)` — arity 2; extract module list (per the C15 LINQ `.All`+`.Cast` validation OR single-string fast-path); `var handle = rt.LoadLibrary(modulePaths[0]);` in try/catch; then `BindOrVerifyConst(rt, wid, handle)`.
- **T20.** Emit `public static SystemResult LoadModulePredicate(GlpRuntime rt, SystemCall call)` — arity 2; `TryExtractString` (filePath); `if (!File.Exists(filePath)) return SystemResult.Failure;`; `var contents = File.ReadAllText(filePath, Encoding.UTF8);` in try/catch; build C14 dictionary literal `var module = new Dictionary<string, object?> { ["path"] = filePath, ["contents"] = contents, ["loaded_at"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };`; then `if (rt.Heap.IsWriterBound(wid)) { Console.Error.WriteLine("[WARN] load_module/2: Module writer already bound, cannot verify"); return SystemResult.Failure; } rt.Heap.BindWriterConst(wid, module); return SystemResult.Success;`.
- **T21.** Emit `public static SystemResult DistributeStreamPredicate(GlpRuntime rt, SystemCall call)` — arity 2; extract input value (Const / Reader-deref / Writer-deref with suspend-on-unbound-reader); extract output writer list from `ConstTerm` whose `Value` is `List<object?>` of `VarRef` items; iterate writers — `foreach (var wid in outputWriters) { if (rt.Heap.IsWriterBound(wid)) return SystemResult.Failure; var copy = DeepCopyValue(inputValue); rt.Heap.BindWriterConst(wid, copy); }` — returns Success.
- **T22.** Emit `public static SystemResult CopyTermMultiPredicate(GlpRuntime rt, SystemCall call)` — arity 3; same source extraction as `CopyTermPredicate` (with suspend-on-unbound-reader); two output writers must be unbound; `var copy1 = DeepCopyValue(sourceValue); var copy2 = DeepCopyValue(sourceValue); rt.Heap.BindWriterConst(w1, copy1); rt.Heap.BindWriterConst(w2, copy2);` — returns Success.
- **T23.** Emit `private static object? Evaluate(GlpRuntime rt, object? term)` — see C7. Recursive deref + 10-arm `switch (functor)`.
- **T24.** Emit `private static object? DeepCopyTerm(object? term, GlpRuntime rt, Dictionary<long, object?> visited)` — see C8.
- **T25.** Emit `private static object? DeepCopyValue(object? value)` — see C9.
- **T26.** Emit `private static bool TryExtractString(SystemCall call, int argIndex, GlpRuntime rt, out string? value, out SystemResult earlyReturn)` — see C5. Three-arm ladder; suspend on unbound reader; failure on unbound writer or wrong type; success with `value` populated.
- **T27.** Emit `private static bool TryExtractInt(SystemCall call, int argIndex, GlpRuntime rt, out long? value, out SystemResult earlyReturn)` — see C5. Same shape typed for `long`.
- **T28.** Emit `private static bool TryExtractTerm(SystemCall call, int argIndex, GlpRuntime rt, out object? value, out SystemResult earlyReturn)` — see C5. Same shape for object-typed.
- **T29.** Emit `private static SystemResult BindOrVerifyConst(GlpRuntime rt, long wid, object? value)` — see C5. Dual `IsWriterBound`-branch.
- **T30.** Emit `private static SystemResult BindOrVerifyTerm(GlpRuntime rt, long wid, Term term)` — see C5. Dual branch with `BindVariable` on unbound.
- **T31.** Close the holder class and namespace.

## 4. Research Findings

None required — every construct row in the convspec is grounded in either an authoritative Dart citation (dart.dev / api.dart.dev), an authoritative .NET citation (learn.microsoft.com), or a cached carry-forward `research_finding_id` from a prior runtime/* convspec. The convspec already records ten NEW idioms and seven cached carry-forwards with explicit official-docs grounding:

- NEW idioms (10): `rf-dart-dartio-to-csharp-system-io-family`, `rf-dart-filemode-to-csharp-filemode-fileaccess-pair`, `rf-dart-repeated-three-arm-term-extraction-to-csharp-helper`, `rf-dart-bind-or-verify-writer-to-csharp-helper-method`, `rf-dart-cycle-aware-deepcopy-visited-map-to-csharp-equivalent`, `rf-dart-mutable-toplevel-counter-postincrement-to-csharp-static-field`, `rf-dart-trycatch-untyped-with-error-print-to-csharp-catch-exception-stderr`, `rf-dart-path-split-slash-last-to-csharp-path-getfilename`, `rf-dart-mixed-value-map-literal-to-csharp-dictionary-init`, `rf-dart-list-every-cast-to-csharp-linq-all-cast`.
- Cached carry-forwards (7+): `rf-dart-static-only-holder-to-csharp-static-class`, `rf-dart-library-directive-to-csharp-namespace-elision`, `rf-dart-import-relative-to-csharp-using-namespace`, `rf-dart-typedef-function-to-csharp-delegate`, `rf-dart-switch-on-string-to-csharp-switch-expression`, `rf-dart-num-hierarchy-to-csharp-double-with-int-discriminator`, `rf-dart-datetime-now-ms-to-csharp-dto-utc-unixms`, `rf-dart-postincrement-and-method-shape-to-csharp-equivalent`, `rf-dart-string-interpolation-to-csharp-interpolation`, `rf-dart-print-to-stderr-on-error`.

## 5. Consistency Pass

- C1 (using-directive family) — fixed — derived from convspec construct `dart.import_directive.dartio_plus_relatives_to_using_namespace_plus_systemio`.
- C2 (XML-doc on namespace) — fixed — derived from convspec construct `dart.toplevel_docs_with_dartio_mention_to_csharp_xmldoc_namespace`.
- C3 (`RegisterStandardPredicates`) — fixed — derived from convspec construct `dart.toplevel_void_function.registry_dispatch_table_16_register_calls`.
- C4 (predicate-function template, 16×) — fixed — derived from convspec construct `dart.predicate_function_template.arity_check_extract_three_arm_ladder_side_effect_bind_or_verify_return_systemresult`.
- C5 (consolidation helpers, 5 of them) — fixed — derived from convspec constructs `…three-arm-term-extraction-to-csharp-helper` and `…bind-or-verify-writer-to-csharp-helper-method`.
- C6 (`_uniqueIdCounter`) — fixed — derived from convspec construct `dart.toplevel_mutable_int_counter_postincrement_dual_use_in_predicate`.
- C7 (`Evaluate`) — fixed — derived from convspec construct `dart.private_arithmetic_evaluator_recursive_switch_on_functor_string_returning_nullable_object`.
- C8 (`DeepCopyTerm`) — fixed — derived from convspec construct `dart.private_cycle_aware_deep_copy_visited_map_var_refs_struct_term_recurse`.
- C9 (`DeepCopyValue`) — fixed — derived from the same construct row (companion within `rf-dart-cycle-aware-deepcopy-visited-map-to-csharp-equivalent`).
- C10 (dart:io family) — fixed — derived from convspec construct `dart.file_io_sync_family_File_Directory_RandomAccessFile_FileMode_stdin`.
- C11 (`FileMode` → `(FileMode, FileAccess)` pair) — fixed — derived from convspec construct `dart.filemode_4_value_enum_to_csharp_filemode_fileaccess_pair_switch`.
- C12 (try/catch family, 11 sites) — fixed — derived from convspec construct `dart.try_catch_untyped_with_error_print_and_failure_return_family`.
- C13 (`Path.GetFileName`) — fixed — derived from convspec construct `dart.path_split_slash_last_to_csharp_path_getfilename`.
- C14 (mixed-value map literal) — fixed — derived from convspec construct `dart.mixed_value_map_literal_string_keyed_dict_for_load_module`.
- C15 (LINQ `.All` + `.Cast`) — fixed — derived from convspec construct `dart.list_string_cast_and_validate_every_string`.
- C16 (string interpolation) — fixed — derived from cached `rf-dart-string-interpolation-to-csharp-interpolation` (CLAUDE.md carry-forward across runtime/*).
- C17 (heap host-side helpers PascalCase) — fixed — derived from heap_fcp.dart.md and runtime.dart.md surface naming.
- C18 (int-width carry-forward) — fixed — derived from terms.dart.md / heap_fcp.dart.md int-width nuance.
- Threading model — fixed — inherited from escalation #4 (heap_fcp single-owning-context); single-threaded `_uniqueIdCounter` is faithful to source and NOT retrofitted with `Interlocked`.
- Decomposed tasks T1–T31 — fixed — each task points to its construct row (C1–C18) and the convspec's `conversion_units` line for the corresponding method.
- Frontmatter `cycle_group_id: 47` — used the task-message value as authoritative (tombstone records `56`; the task instruction supersedes for the plan artefact's frontmatter).

## 6. Escalations

None.
