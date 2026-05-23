---
path: bin/glp_repl.dart
cycle_group_id: 51
scc_siblings: []
generated_at: 2026-05-21T16:51:08Z
source_sha256: 1c0283feec16728c939939a25cb7c3000a9ec9d80404e3e759b6b756167cf636
schema_version: 1
---

# Conversion Plan: bin/glp_repl.dart

## 1. Source Analysis

`bin/glp_repl.dart` is a **thin CLI wrapper around `GlpEngine`** — the user-facing GLP REPL. It is the lone executable entry-point in `glp_runtime_net/bin/`. The Dart file declares `library;` and a top-level `Future<void> main()` plus four private helpers. It has zero classes of its own; it is procedural CLI glue.

**Imports (5):**
- `dart:io` — `stdin`, `stdout`, `Directory`, `File`, `Platform`, `Process`.
- `package:glp_runtime/engine/glp_engine.dart` — `GlpEngine` (the runtime façade).
- `package:glp_runtime/runtime/scheduler.dart` — `ExecutionStatus` enum (succeeded/failed/suspended).
- `package:glp_runtime/runtime/terms.dart as rt` — `Term`, `ConstTerm`, `StructTerm`, `VarRef` (printer needs them).
- `package:glp_runtime/multiagent/boot_loader.dart` — `BootLoader`, `BootConfig` (for `:boot`).
- `package:glp_runtime/multiagent/isolate_manager.dart` — `IsolateManager`.

**Top-level constructs (5):**

(a) `Future<void> main() async` — banner print, resolves `programs/self.glp` via `Platform.script.resolve('../../programs/self.glp').toFilePath()`, instantiates `GlpEngine`, enters an infinite `while (true)` REPL loop reading from `stdin.readLineSync()`. Dispatches eleven REPL commands by prefix/equality match: `:quit`/`:q`, `:help`/`:h`, `:trace`/`:t`, `:debug`/`:d`, `:strict`/`:s`, `:clear`/`:c`, `:limit <n>`, `:activate <module>`, `:bytecode`/`:bc`, `:boot <file> [secs]`. After commands, tries directory-load, then `.glp` file-load, then runs the line as a goal via `await engine.runGoal(trimmed)`. Trailing `.` is stripped (but NOT for `.glp` filenames). Bindings are printed via `_formatTerm`; status via `_printStatus`.

(b) `void _printStatus(ExecutionStatus status)` — pattern-matches the three-variant enum, prints `→ succeeds`, `→ failed`, or `→ suspended` (note: `→` is U+2192).

(c) `void _printHelp()` — multi-line `print()` of the help text. Pure I/O.

(d) `String _formatTerm(rt.Term? term, [GlpEngine? engine, Set<int>? path])` — recursive term printer with cycle detection. Three branches: `ConstTerm` (renders `null`/`'nil'` as `[]`, otherwise `.toString()`); list-shaped `StructTerm` (functor=`.`, arity=2 → Prolog-style `[h1, h2 | T]`); other `StructTerm` (renders `functor(arg1, arg2, …)`). For `VarRef` heads/tails/args: dereferences via `engine.runtime.heap.dereference(...)`; renders unbound vars as `Xn?` (reader) or `Xn` (writer) using `engine.runtime.heap.isReader(addr)`; tracks visited addresses in `path: Set<int>` to detect `<circular>` references. The `path ??= <int>{};` default-init initialises an empty `HashSet<int>` on first call.

(e) `Future<void> _runBoot(String bootPath, String rootSelfGlpPath, int timeoutSec)` — reads boot file via `bootFile.readAsStringSync()`, parses via `BootLoader().load(source)`, computes `projectDir` (the `mad_boot/` directory check uses `parent.uri.pathSegments.where((s) => s.isNotEmpty).lastOrNull == 'mad_boot'`), sets `config.projectDir` and `config.rootSelfGlpPath`, creates `IsolateManager`, attaches `onUIOutput` callback, calls `await manager.boot(config)`, `manager.start()`, sleeps `await Future.delayed(Duration(seconds: timeoutSec))`, then `finally { await manager.shutdown(); }`. Catches errors and prints stack-trace.

(f) `Future<String?> _getGitCommit()` — invokes `Process.run('git', ['log', '-1', '--format=%h %s'])`, returns trimmed stdout if exit 0, otherwise `null`. Swallows all exceptions silently (git absent / not a repo).

**Notable Dart-isms:**
- `Platform.script.resolve(...).toFilePath()` — URI-resolution of the running script's path. Two levels up from `bin/glp_repl.dart` to repo root, then `programs/self.glp`.
- `stdin.readLineSync()` returns `String?`; `null` (EOF) breaks the loop.
- `print()` writes to stdout with newline.
- `RegExp(r'\s+').split(trimmed)` — Dart raw-string regex.
- `int.tryParse(parts[1])` — returns `int?`.
- `await Future.delayed(Duration(seconds: …))` — async sleep.
- Pattern-matching `switch` over enum without `default` (Dart 3 exhaustive switch).
- `parent.uri.pathSegments.where(...).lastOrNull == 'mad_boot'` — collection extension `lastOrNull`.

**Convspec status:** A ratified convspec now exists at `.codeconv/conversion-specs/bin/glp_repl.dart.md` (`specced`, 0 escalations — generated retroactively 2026-05-23 to close the lone `blocked_on_deps=1` slot in the 018 inventory). Convspecs for all five dependencies are also RATIFIED (`lib/engine/glp_engine.dart.md`, `lib/runtime/scheduler.dart.md`, `lib/runtime/terms.dart.md`, `lib/multiagent/boot_loader.dart.md`, `lib/multiagent/isolate_manager.dart.md`) — plan §2 is derived from the source and consistent with those ratified APIs. See §6 (prior escalation E1, now resolved).

## 2. Dart → C#/.NET Conversion Plan

The file becomes the C# console-app entry-point: `GlpRuntime.Cli/Program.cs` (the `bin/` working-directory convention for the Dart→C# pair maps to a `*.Cli` console project under `out/csharp/`). Per the langpair convention, the `library;` directive is dropped and the procedural `main` becomes `static async Task Main(string[] args)` on a `Program` static class.

**Project layout (mirror of bin/):** `GlpRuntime.Cli/Program.cs` with `<OutputType>Exe</OutputType>` and `<TargetFramework>net10.0</TargetFramework>`. Project references `GlpRuntime` (the converted `lib/` library project). `using` directives mirror the Dart imports.

**Construct-by-construct mapping:**

(a) **`Future<void> main()` → `public static async Task Main(string[] args)`**
- Banner `print(...)` lines → `Console.WriteLine(...)`. Box-drawing characters (`╔ ║ ╚`) are preserved verbatim (UTF-8). Console output encoding MUST be set to UTF-8 at program start: `Console.OutputEncoding = System.Text.Encoding.UTF8;` (Windows console defaults to a code page that mangles the `→` and box chars).
- `Directory.current.path` → `Environment.CurrentDirectory`.
- `Platform.script.resolve('../../programs/self.glp').toFilePath()` →
  `Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "programs", "self.glp"))`.
  Microsoft Learn precedent: `AppContext.BaseDirectory` is the documented .NET equivalent of "the directory of the executing assembly" — Microsoft Learn: "The directory of the application that hosts the AppDomain". `Path.GetFullPath` collapses the `..` segments.
- `new GlpEngine(rootSelfGlpPath: rootSelfGlpPath)` → `new GlpEngine(rootSelfGlpPath: rootSelfGlpPath)` (mirrors glp_engine.dart.md ratified ctor; named-argument syntax preserved).
- `while (true)` → `while (true)` (identical).
- `stdout.write('GLP> ')` → `Console.Write("GLP> ")`.
- `stdin.readLineSync()` returns `String?`; → `Console.ReadLine()` (returns `string?`, returns `null` on EOF — identical semantics).
- `input.trim()` → `input.Trim()`.
- `trimmed.endsWith('.')` → `trimmed.EndsWith('.')` (`char` overload; allocates no string).
- `trimmed.endsWith('.glp')` → `trimmed.EndsWith(".glp", StringComparison.Ordinal)`.
- `trimmed.substring(0, trimmed.length - 1).trim()` → `trimmed[..^1].Trim()` (range operator; `^1` = end-minus-one).
- `trimmed == ':quit' || trimmed == ':q'` → `trimmed is ":quit" or ":q"` (C# 9 pattern-matching `or` — concise, ordinal-equal, no allocation).
- `engine.debugTrace = !engine.debugTrace;` → `engine.DebugTrace = !engine.DebugTrace;` (PascalCase per glp_engine.dart.md convention).
- `engine.debugTrace ? "enabled" : "disabled"` → ternary preserved.
- `engine.clear()` → `engine.Clear()`.
- `trimmed.startsWith(':limit')` → `trimmed.StartsWith(":limit", StringComparison.Ordinal)`.
- `trimmed.split(RegExp(r'\s+'))` → `System.Text.RegularExpressions.Regex.Split(trimmed, @"\s+")`. Cache the `Regex` as `private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);` at the top of `Program` (Microsoft Learn: "If you are going to use the same regular expression repeatedly, you should declare a Regex object").
- `int.tryParse(parts[1])` → `int.TryParse(parts[1], out var limit)` — note the IDIOM CHANGE: Dart's `int.tryParse` returns `int?`; C#'s `int.TryParse` returns `bool` with `out` parameter. The check `if (limit == null || limit <= 0)` becomes `if (!int.TryParse(parts[1], out var limit) || limit <= 0)`.
- `engine.maxCycles = limit;` → `engine.MaxCycles = limit;`.
- `engine.activateDynamicModule(moduleName)` → `engine.ActivateDynamicModule(moduleName)`.
- `try { … } catch (e) { print('Error: $e'); }` → `try { … } catch (Exception e) { Console.WriteLine($"Error: {e.Message}"); }`. Dart's `'$e'` interpolation calls `Exception.toString()`; C# default is also `Exception.ToString()` (full stack). Per convspec idiom (glp_engine.dart.md line 151 "`try`/`catch` with `e.toString()` interpolation in `runGoal`") use `e.Message` for one-line user-visible errors and reserve full `e.ToString()` for the boot-failure trace branch.
- `engine.loadedPrograms.isEmpty` → `engine.LoadedPrograms.Count == 0`.
- `for (final entry in engine.loadedPrograms.entries)` → `foreach (var entry in engine.LoadedPrograms)` (IEnumerable<KeyValuePair<string, BytecodeProgram>>).
- `'=' * 60` (Dart string repeat) → `new string('=', 60)`.
- `i.toString().padLeft(4)` → `i.ToString().PadLeft(4)` (identical name).
- `prog.ops[i]` → `prog.Ops[i]`.
- `Directory(dirCandidate).existsSync()` → `Directory.Exists(dirCandidate)` (static).
- `engine.loadProject(dirCandidate, topModuleName: topModule)` → `engine.LoadProject(dirCandidate, topModuleName: topModule)`.
- `filename.startsWith('/') || filename.startsWith('../') || filename.startsWith('./')` → identical with `StringComparison.Ordinal`.
- `File(filename)` (Dart constructor) → handled as a path string; absolute/relative resolution done with `Path.GetFullPath`. Use `File.Exists(path)` for existence; do NOT construct a `FileInfo` for the no-op style — the Dart code uses `File` purely as a path wrapper.
- `engine.loadFile(sourceFile.path)` → `engine.LoadFile(sourcePath)`.
- `await engine.runGoal(trimmed)` → `await engine.RunGoal(trimmed)` returns `ExecutionResult` per glp_engine.dart.md.
- `result.bindings.isNotEmpty` → `result.Bindings.Count > 0`.
- `result.bindings.entries` → `result.Bindings` (Dictionary as IEnumerable<KeyValuePair<,>>).
- `result.error != null` → `result.Error is not null`.
- The check-mark glyph (`✓` U+2713) is preserved verbatim. Console output MUST be UTF-8 (set at program start).

(b) **`void _printStatus(ExecutionStatus status)` → `private static void PrintStatus(ExecutionStatus status)`**
- Dart 3 exhaustive `switch` becomes a C# 8 `switch` expression with `Console.WriteLine`:
  ```csharp
  Console.WriteLine(status switch
  {
      ExecutionStatus.Succeeded => "→ succeeds",
      ExecutionStatus.Failed    => "→ failed",
      ExecutionStatus.Suspended => "→ suspended",
      _ => throw new ArgumentOutOfRangeException(nameof(status)),
  });
  ```
- The `_` arm with `throw` preserves Dart's exhaustive-switch invariant. PascalCase enum members per scheduler.dart.md.
- The `→` arrow is U+2192; encoded in C# source as `→` for portability OR kept as a literal arrow in a UTF-8 source file.

(c) **`void _printHelp()` → `private static void PrintHelp()`**
- Trivial: every `print('...')` → `Console.WriteLine("...")`. The `# Load typed program` and `# Execute goal` examples are preserved verbatim.

(d) **`String _formatTerm(rt.Term? term, [GlpEngine? engine, Set<int>? path])` → `private static string FormatTerm(Term? term, GlpEngine? engine = null, HashSet<int>? path = null)`**
- Three-arg method with C# optional defaults (Dart positional optional `[…]` → C# named optional).
- `path ??= <int>{};` → `path ??= new HashSet<int>();` (null-coalescing assignment).
- `if (term == null) return '[]';` → `if (term is null) return "[]";`.
- Type tests via C# `is` pattern: `if (term is ConstTerm c) { … }`, `if (term is StructTerm s && s.Functor == "." && s.Args.Count == 2)`.
  - `term.value == null || term.value == 'nil'` → `c.Value is null || (c.Value is string str && str == "nil")` (Dart's loose `==` on `Object?` vs `String` works because of operator overloading; C# requires explicit string check since `ConstTerm.Value` is `object?` per terms.dart.md).
  - `term.value.toString()` → `c.Value.ToString() ?? string.Empty`.
- List-shaped `StructTerm` branch: the `while (true)` loop translates literally. `current is! rt.StructTerm` → `current is not StructTerm st` (negated pattern; bind on success requires re-test or use `var temp` + `is`); cleanest: `if (current is not StructTerm st || st.Functor != ".") break;`.
- `final head = current.args[0]; final tail = current.args[1];` → `var head = st.Args[0]; var tail = st.Args[1];`.
- `if (head is rt.VarRef && engine != null)` → `if (head is VarRef vr && engine is not null)`. The `path.contains(addr)` / `path.add(addr)` / `path.remove(addr)` calls map directly to `HashSet<int>.Contains/Add/Remove`.
- `engine.runtime.heap.dereference(head)` → `engine.Runtime.Heap.Dereference(head)` (PascalCase per glp_engine.dart.md and heap convspec).
- `engine.runtime.heap.isReader(derefHead.addr)` → `engine.Runtime.Heap.IsReader(derefHead.Addr)`.
- Recursion `_formatTerm(derefHead, engine, path)` → `FormatTerm(derefHead, engine, path)` (positional preserved; `HashSet<int>` is by-reference so visit-set mutation is shared between callers — verbatim Dart semantics, since `Set<int>` in Dart is also by-reference).
- String interpolation `'X$displayId?'` → `$"X{displayId}?"`.
- `[${elements.join(', ')}]` → `$"[{string.Join(", ", elements)}]"`.
- The "other `StructTerm`" branch uses `term.args.map((arg) => …).join(', ')`:
  → `string.Join(", ", term.Args.Select(arg => { … }))` (LINQ `Select` over `IReadOnlyList<Term>`).
- The lambda captures `currentPath` (an alias for `path`) — preserved as `var currentPath = path!;` (null-forgiving since we entered the StructTerm branch after the `path ??=` init).
- Final fallthrough `return term.toString();` → `return term.ToString() ?? string.Empty;` (Dart's `Object.toString` is non-null; C# nullable reference type `object?.ToString()` returns `string?`, hence the `?? string.Empty`).

(e) **`Future<void> _runBoot(...)` → `private static async Task RunBoot(string bootPath, string rootSelfGlpPath, int timeoutSec)`**
- `File bootFile; try { bootFile = File(bootPath); } catch (e) { … }` — Dart's `File()` constructor only validates path syntax on some platforms; on Windows, illegal characters can throw `FileSystemException`. The C# equivalent: `string bootPathFull; try { bootPathFull = Path.GetFullPath(bootPath); } catch (Exception e) { Console.WriteLine($"Error: invalid boot path: {e.Message}"); return; }`. Microsoft Learn: `Path.GetFullPath` "may throw" `ArgumentException`/`NotSupportedException`/`PathTooLongException` on illegal paths — equivalent to Dart's invalid-path branch.
- `bootFile.existsSync()` → `File.Exists(bootPathFull)`.
- `bootFile.readAsStringSync()` → `File.ReadAllText(bootPathFull, Encoding.UTF8)`.
- `final loader = BootLoader(); final BootConfig config; try { config = loader.load(source); } catch (e) { … }` → mirror per boot_loader.dart.md (`new BootLoader()`, `loader.Load(source)`).
- `final parent = bootFile.parent;` → `var parentDir = Path.GetDirectoryName(bootPathFull) ?? string.Empty;`.
- `parent.uri.pathSegments.where((s) => s.isNotEmpty).lastOrNull == 'mad_boot'` → the Dart code is asking: "is the LAST non-empty path segment of the parent directory equal to `mad_boot`?". In C#: `var projectDir = Path.GetFileName(parentDir) == "mad_boot" ? Path.GetDirectoryName(parentDir) ?? parentDir : parentDir;`. `Path.GetFileName` on a directory path returns the leaf segment. (Note: trailing-slash robustness — Dart's `pathSegments.where(isNotEmpty)` skips empty trailing segments; `Path.GetFileName` likewise skips a trailing directory separator on .NET 10.)
- `config.projectDir = projectDir; config.rootSelfGlpPath = rootSelfGlpPath;` → `config.ProjectDir = projectDir; config.RootSelfGlpPath = rootSelfGlpPath;`. Per boot_loader.dart.md these two are mutable on `BootConfig`.
- `config.directives.map((d) => d.agentId).join(', ')` → `string.Join(", ", config.Directives.Select(d => d.AgentId))`.
- `final manager = IsolateManager(); manager.onUIOutput = (agentId, message) { print('[$agentId] $message'); };` → per isolate_manager.dart.md the `OnUIOutput` field type is `Action<string, Term>?`. So: `var manager = new IsolateManager(); manager.OnUIOutput = (agentId, message) => Console.WriteLine($"[{agentId}] {message}");`. NOTE: the Dart code's lambda parameter `message` is statically `dynamic` (passes through to `print` which calls `.toString()`); the C# `Term` will be rendered via `Term.ToString()` per terms.dart.md — semantically equivalent.
- `await manager.boot(config); manager.start();` → `await manager.Boot(config); manager.Start();`.
- `await Future.delayed(Duration(seconds: timeoutSec))` → `await Task.Delay(TimeSpan.FromSeconds(timeoutSec))`.
- `catch (e, st)` (Dart's two-arg catch capturing stack-trace) → `catch (Exception e) { Console.WriteLine($"Boot failed: {e.Message}"); Console.WriteLine(e.StackTrace); }`. The Dart `print(st)` of the stack-trace is preserved.
- `finally { await manager.shutdown(); }` → `finally { await manager.Shutdown(); }`. C# `try/finally` over `await` is identical in semantics.

(f) **`Future<String?> _getGitCommit()` → `private static async Task<string?> GetGitCommitAsync()`**
- `Process.run('git', ['log', '-1', '--format=%h %s'])` →
  ```csharp
  using var p = new Process
  {
      StartInfo = new ProcessStartInfo("git", "log -1 --format=\"%h %s\"")
      {
          RedirectStandardOutput = true,
          RedirectStandardError  = true,
          UseShellExecute        = false,
          CreateNoWindow         = true,
      }
  };
  p.Start();
  var stdout = await p.StandardOutput.ReadToEndAsync();
  await p.WaitForExitAsync();
  if (p.ExitCode == 0) return stdout.Trim();
  ```
  Microsoft Learn: `Process.WaitForExitAsync` and `StandardOutput.ReadToEndAsync` are the documented async pattern (.NET 5+). The `try { … } catch { return null; }` swallow-all mirrors the Dart silent-failure for git-missing.
- Argument quoting: the `%h %s` format string contains a space; pass as a SINGLE argument via `ProcessStartInfo.ArgumentList.Add("--format=%h %s")` (cleaner than embedded-quote escaping) — Microsoft Learn recommends `ArgumentList` over the legacy `Arguments` string for whitespace-safe argument passing.

**Naming / file layout summary:**
- Target file: `out/csharp/GlpRuntime.Cli/Program.cs`.
- Target type: `public static class Program` in namespace `GlpRuntime.Cli` (or the project-default namespace).
- Method renames: `main` → `Main`, `_printStatus` → `PrintStatus`, `_printHelp` → `PrintHelp`, `_formatTerm` → `FormatTerm`, `_runBoot` → `RunBoot`, `_getGitCommit` → `GetGitCommitAsync`.
- The Dart file has no testable surface beyond the help text and the term-printer; the printer is the only meaningful unit-testable function (the rest is `Console.WriteLine`-glue). However per CLAUDE.md spec-first discipline, NO new tests are added during conversion unless requested — only behavioural parity.

**Threading model:** inherited from the parent project (single-owning-context per heap_fcp.dart.md). No `lock` / `Interlocked` / `ConcurrentDictionary` in this file — there is no concurrent state. `Main` runs on a single thread; the `await` continuations resume on the synchronisation context (none — console app).

**`async Main` UTF-8 setup:** `Console.OutputEncoding = Encoding.UTF8;` MUST be the very first statement in `Main` (before any banner print) — otherwise the box-drawing chars, the `✓` check, and the `→` arrow will render as `?` on a code-page-1252 Windows console. This is REQUIRED for behavioural parity with Dart (which writes UTF-8 to stdout by default on all platforms).

## 3. Decomposed Task Units

- T1: Create `out/csharp/GlpRuntime.Cli/GlpRuntime.Cli.csproj` with `<OutputType>Exe</OutputType>`, `<TargetFramework>net10.0</TargetFramework>`, project reference to `GlpRuntime.csproj`.
- T2: Create `out/csharp/GlpRuntime.Cli/Program.cs` with `using` directives (`System`, `System.IO`, `System.Diagnostics`, `System.Linq`, `System.Text`, `System.Text.RegularExpressions`, `System.Threading.Tasks`, `GlpRuntime.Engine`, `GlpRuntime.Runtime`, `GlpRuntime.Multiagent`).
- T3: Implement static `Program` class with `private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);`.
- T4: Translate `Future<void> main()` → `public static async Task Main(string[] args)`. First line sets `Console.OutputEncoding = Encoding.UTF8`.
- T5: Translate banner block (lines 18–29) — `Console.WriteLine` mirrors verbatim, preserving box-drawing chars and the `✓` glyph.
- T6: Translate `Platform.script.resolve(...)` → `Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "programs", "self.glp"))`.
- T7: Translate the `while (true)` REPL loop control flow (input read, EOF break, empty-line continue, trailing-`.` strip).
- T8: Translate the eleven REPL command branches using `is "…" or "…"` pattern-match for the equality commands and `StartsWith(":xxx", StringComparison.Ordinal)` for the prefix commands.
- T9: Translate the directory-load branch (`Directory.Exists` + `engine.LoadProject`).
- T10: Translate the `.glp` file-load branch (path-prefix tests + relative `"glp/"` join + `File.Exists` + `engine.LoadFile`).
- T11: Translate the goal-execution branch (`await engine.RunGoal`, bindings loop using `FormatTerm`, status print, error print).
- T12: Translate `_printStatus` → `PrintStatus` (switch expression with `default: throw` arm).
- T13: Translate `_printHelp` → `PrintHelp` (verbatim string sequence).
- T14: Translate `_formatTerm` → `FormatTerm` (recursive term printer with `HashSet<int>` visit-set, three pattern-match branches, `engine?.Runtime.Heap.Dereference` derefs, `IsReader`-aware var labels, list-shape special-case loop).
- T15: Translate `_runBoot` → `RunBoot` (file existence + `BootLoader.Load` + `mad_boot`-leaf check via `Path.GetFileName` + `IsolateManager.Boot/Start/Shutdown` lifecycle + `Task.Delay`).
- T16: Translate `_getGitCommit` → `GetGitCommitAsync` (`Process` with `ArgumentList`, `WaitForExitAsync`, `try/catch` swallow).
- T17: Verify the C# project builds in isolation (`dotnet build`) — depends only on `GlpRuntime` (no other CLI projects).
- T18: Smoke-test: `dotnet run --project GlpRuntime.Cli` with stdin `:help\n:quit\n` should print the help and exit clean; no exceptions.

## 4. Research Findings

None required. Every API mapping is derived from a RATIFIED convspec or from .NET BCL APIs that are universally known (`Console`, `File`, `Path`, `Process`, `Regex`, `Task`, `HashSet`, `string.Join`). The two non-trivial mappings — `Platform.script.resolve` → `AppContext.BaseDirectory` and Dart `pathSegments.lastOrNull` → `Path.GetFileName` — are documented Microsoft Learn idioms not requiring web lookup.

## 5. Consistency Pass

Fixed — derived from:

- **`GlpEngine` API** (ctor, `DebugTrace`, `DebugOutput`, `StrictTypes`, `MaxCycles`, `LoadedPrograms`, `Clear`, `ActivateDynamicModule`, `LoadProject`, `LoadFile`, `RunGoal`, `ExecutionResult { Status, Bindings, Error }`, `Runtime.Heap.Dereference/IsReader`): `lib/engine/glp_engine.dart.md` (RATIFIED).
- **`ExecutionStatus` enum** with PascalCase members `Succeeded/Failed/Suspended`: `lib/runtime/scheduler.dart.md` line 1314 — `"public enum ExecutionStatus { Succeeded, Failed, Suspended }"`.
- **`Term` / `ConstTerm` / `StructTerm` / `VarRef`** sealed class hierarchy and PascalCase fields (`Value`, `Functor`, `Args`, `Addr`): `lib/runtime/terms.dart.md` lines 293–296 (RATIFIED). `IReadOnlyList<Term> Args` per line 295; `Args.Count` (not `.Length`) per `IReadOnlyList<T>`.
- **`BootLoader` / `BootConfig` / `SpawnDirective`**: `lib/multiagent/boot_loader.dart.md` lines 471–488 (RATIFIED). Mutable `ProjectDir` and `RootSelfGlpPath` on `BootConfig` per line 480 ctor with default values (`projectDir = null`, `rootSelfGlpPath = ""`).
- **`IsolateManager`** with `OnUIOutput: Action<string, Term>?`, `Boot/Start/Shutdown`: `lib/multiagent/isolate_manager.dart.md` lines 133–134 (RATIFIED). No-Isolate.kill shutdown contract preserved (line 278).
- **Threading model** (single-owning-context, plain non-concurrent collections): inherited per heap_fcp.dart.md per glp_engine.dart.md line 544.
- **CLAUDE.md preservation rules**: error-type names retain Dart names (e.g. `BootLoaderException` — not `BootLoaderError`-style rename); per CompileError precedent committed in 018 escalation E1 (commit `e3abe921`).

Two minor IDIOM CHANGES called out:
1. `int.tryParse` → `int.TryParse` (return-value vs `out` parameter): a deliberate C#-idiomatic shift, semantically equivalent.
2. `Future.delayed(Duration(...))` → `Task.Delay(TimeSpan.FromSeconds(...))`: name-only translation, identical async-sleep semantics.

Neither requires escalation — both are universally idiomatic in the Dart→C# pair.

## 6. Escalations

None. (Prior E1 — convspec absent for `bin/glp_repl.dart`, the lone `blocked_on_deps=1` slot — resolved by Gabi 2026-05-23 via the hybrid path: the best-effort plan above is accepted AND a retroactive convspec was generated and ingested at `.codeconv/conversion-specs/bin/glp_repl.dart.md` (`specced`, 0 escalations), formally closing the 018 `blocked_on_deps` slot. This plan is consistent with that ratified convspec.)
