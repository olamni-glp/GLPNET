# Conversion Spec — bin/glp_repl.dart

```yaml
schema_version: 1
source_path: bin/glp_repl.dart
source_sha256: 1c0283feec16728c939939a25cb7c3000a9ec9d80404e3e759b6b756167cf636
target_code_unit: GlpRuntime.Cli/Program.cs
constructs:
  - construct_key: dart.cli.entrypoint_main_async_repl_loop
    source_form: >-
      void main() async { ... while (true) { stdout.write('GLP> '); final input
      = stdin.readLineSync(); if (input == null) break; ... dispatch eleven
      commands; directory-load; .glp file-load; await engine.runGoal(trimmed) } }
    target_decision: >-
      The procedural top-level `main` becomes `public static async Task
      Main(string[] args)` on a `Program` static class in project
      `GlpRuntime.Cli`; the Dart `library;` directive is dropped. The
      `while (true)` REPL loop is preserved verbatim. `stdout.write('GLP> ')` →
      `Console.Write("GLP> ")`; `stdin.readLineSync()` → `Console.ReadLine()`
      (returns `string?`, `null` on EOF — identical break semantics);
      `input.trim()` → `input.Trim()`; the trailing-`.` strip
      `trimmed.substring(0, trimmed.length - 1).trim()` →
      `trimmed[..^1].Trim()` (range/index-from-end operators). The eleven
      command branches use C# 9 `or` patterns for the equality commands
      (`trimmed is ":quit" or ":q"`) and `StartsWith(":xxx",
      StringComparison.Ordinal)` for the prefix commands. Engine members are
      PascalCased per `glp_engine.dart.md` (`engine.DebugTrace`,
      `engine.MaxCycles`, `engine.Clear()`, `engine.LoadProject`,
      `engine.LoadFile`, `engine.RunGoal`, `engine.LoadedPrograms`). The
      directory-existence test `Directory(dirCandidate).existsSync()` →
      static `Directory.Exists(dirCandidate)`; bytecode dump `'=' * 60` →
      `new string('=', 60)` and `i.toString().padLeft(4)` →
      `i.ToString().PadLeft(4)`.
    idiom_id: null
    research_finding_id: rf-dart-main-async-to-csharp-static-async-task-main
    nuance: >-
      Async entrypoint: Dart `void main() async` permits top-level `await`;
      C# requires `static async Task Main` to legalise `await` in the
      entrypoint. Null-safety: `stdin.readLineSync()` and `Console.ReadLine()`
      both return a nullable string and both signal EOF with `null` — the
      `if (input == null) break` maps one-to-one with no semantic drift. The
      trailing-`.` strip is guarded so `.glp` filenames are NOT truncated
      (`endsWith('.') && !endsWith('.glp')`), preserved exactly. String
      comparisons are pinned to `StringComparison.Ordinal` so command/extension
      recognition is culture-invariant, matching Dart's ordinal `==`/`endsWith`.
  - construct_key: dart.idiom.platform_script_resolve_to_appcontext_basedirectory
    source_form: >-
      Platform.script.resolve('../../programs/self.glp').toFilePath()
    target_decision: >-
      Map to `Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..",
      "..", "programs", "self.glp"))`. `AppContext.BaseDirectory` is the
      documented .NET equivalent of "the directory of the running application";
      `Path.Combine` joins the two-levels-up relative segments and
      `Path.GetFullPath` collapses the `..` into a canonical absolute path. The
      resolved string is passed straight to the `GlpEngine` constructor via the
      named argument `new GlpEngine(rootSelfGlpPath: rootSelfGlpPath)`
      (named-argument syntax preserved per `glp_engine.dart.md`).
    idiom_id: null
    research_finding_id: rf-dart-platform-script-resolve-to-appcontext-basedirectory
    nuance: >-
      Semantics shift made explicit: `Platform.script` is a `Uri` of the
      executing Dart script file; `AppContext.BaseDirectory` is the host
      assembly's directory — for a published/compiled console exe these denote
      the same on-disk anchor, so the two-levels-up-to-repo-root computation is
      preserved. URI-vs-path: Dart resolves on a `Uri` then `.toFilePath()`
      decodes percent-escapes and applies the platform separator; the C#
      `Path.*` APIs operate directly on native path strings, so no URI
      round-trip or percent-decoding is needed. `Path.GetFullPath` performs the
      `..` normalisation that `Uri.resolve` did implicitly.
  - construct_key: dart.printer.print_status_exhaustive_switch
    source_form: >-
      void _printStatus(ExecutionStatus status) { switch (status) { case
      ExecutionStatus.succeeded: print('→ succeeds'); case
      ExecutionStatus.failed: print('→ failed'); case
      ExecutionStatus.suspended: print('→ suspended'); } }
    target_decision: >-
      `private static void PrintStatus(ExecutionStatus status)` wrapping a C#
      `switch` expression whose arms map the three PascalCased enum members
      (`ExecutionStatus.Succeeded => "→ succeeds"`, `Failed => "→ failed"`,
      `Suspended => "→ suspended"`) and emit through `Console.WriteLine`. A
      throwing discard arm `_ => throw new
      ArgumentOutOfRangeException(nameof(status))` is added. The `→` glyph is
      U+2192 kept verbatim in the UTF-8 source. Enum members PascalCased per
      `scheduler.dart.md`.
    idiom_id: null
    research_finding_id: rf-dart3-exhaustive-switch-to-csharp-switch-throwing-discard
    nuance: >-
      Exhaustive-switch totality: Dart 3 statically guarantees the `switch`
      over a sealed/finite enum is exhaustive WITHOUT a `default` arm. C#'s
      `switch` expression does NOT prove enum exhaustiveness (an out-of-range
      cast int could reach it), so a throwing `_` discard arm reproduces Dart's
      totality guarantee — preserving "every value handled or it is a bug" with
      a fail-fast instead of silently returning a default.
  - construct_key: dart.printer.print_help_console_glue
    source_form: >-
      void _printHelp() { print(''); print('GLP REPL Usage:'); ... }
    target_decision: >-
      `private static void PrintHelp()`; each `print('...')` →
      `Console.WriteLine("...")`, verbatim string sequence including the
      `# Load typed program` / `# Execute goal` example comments.
    idiom_id: null
    research_finding_id: null
    trivial: true
  - construct_key: dart.printer.format_term_recursive_with_cycle_visitset
    source_form: >-
      String _formatTerm(rt.Term? term, [GlpEngine? engine, Set<int>? path]) {
      path ??= <int>{}; if (term is rt.ConstTerm) ...; if (term is rt.StructTerm
      && term.functor == '.' && term.args.length == 2) { while(true) { ...
      VarRef deref + isReader labels + path.contains/add/remove } } if (term is
      rt.StructTerm) { term.args.map(...).join(', ') } return term.toString(); }
    target_decision: >-
      `private static string FormatTerm(Term? term, GlpEngine? engine = null,
      HashSet<int>? path = null)` — Dart positional-optional params become C#
      named-optional params with `= null` defaults. `path ??= <int>{};` →
      `path ??= new HashSet<int>();` (null-coalescing assignment). Type tests
      use C# `is` declaration patterns: `if (term is ConstTerm c)`, `if (term
      is StructTerm s && s.Functor == "." && s.Args.Count == 2)`,
      `if (head is VarRef vr && engine is not null)`. The visit-set
      `path.contains/add/remove(addr)` → `HashSet<int>.Contains/Add/Remove` and
      the set is passed by reference into recursive `FormatTerm(...)` calls so
      mutation is shared across frames (matching Dart `Set<int>` reference
      semantics). Heap access is PascalCased: `engine.Runtime.Heap.Dereference`,
      `engine.Runtime.Heap.IsReader(addr)`. Interpolation `'X$displayId?'` →
      `$"X{displayId}?"`; `[${elements.join(', ')}]` →
      `$"[{string.Join(", ", elements)}]"`; the other-StructTerm arm's
      `term.args.map((arg) => …).join(', ')` →
      `string.Join(", ", term.Args.Select(arg => { … }))` (LINQ over
      `IReadOnlyList<Term>`). Final fallthrough `return term.toString();` →
      `return term.ToString() ?? string.Empty;`.
    idiom_id: null
    research_finding_id: rf-dart-recursive-term-printer-hashset-visitset
    nuance: >-
      Null-safety load-bearing: `ConstTerm.Value` is `object?` per
      `terms.dart.md`, so Dart's loose `term.value == 'nil'` (which works via
      operator==) becomes the explicit `c.Value is null || (c.Value is string
      str && str == "nil")` — no implicit object/string coercion in C#. The
      fallthrough `term.ToString()` needs `?? string.Empty` because C#
      `object?.ToString()` is nullable whereas Dart `Object.toString()` is
      non-null. Reference-vs-value: `HashSet<int>` is a reference type passed
      by reference, exactly mirroring Dart `Set<int>`, so the
      add-before-recurse / remove-after-recurse cycle-detection protocol is
      preserved bit-for-bit (no copy is taken). The reader/writer label
      (`Xn?` vs `Xn`) keyed off `IsReader` is preserved verbatim.
  - construct_key: dart.idiom.pathsegments_lastornull_to_path_getfilename
    source_form: >-
      parent.uri.pathSegments.where((s) => s.isNotEmpty).lastOrNull ==
      'mad_boot' ? parent.parent.path : parent.path
    target_decision: >-
      The Dart expression asks "is the last non-empty path segment of the
      boot-file's parent directory equal to `mad_boot`?". Map to
      `Path.GetFileName(parentDir) == "mad_boot" ? (Path.GetDirectoryName(
      parentDir) ?? parentDir) : parentDir`, where `parentDir =
      Path.GetDirectoryName(bootPathFull) ?? string.Empty`.
      `Path.GetFileName` returns the leaf segment of a directory path;
      `Path.GetDirectoryName` climbs one level (the `parent.parent` case).
    idiom_id: null
    research_finding_id: rf-dart-pathsegments-lastornull-to-path-getfilename
    nuance: >-
      Collection-extension vs path-API: Dart's
      `pathSegments.where(isNotEmpty).lastOrNull` filters out empty trailing
      segments (a trailing `/`) before taking the last; `Path.GetFileName`
      likewise returns empty for a path ending in a separator and otherwise the
      leaf — equivalent trailing-separator robustness on .NET 10. Null-safety:
      Dart `lastOrNull` yields `String?` (compared against the literal);
      `Path.GetFileName` returns a non-null string for a non-null input, and
      `Path.GetDirectoryName` returns `string?` (handled by `?? parentDir`).
  - construct_key: dart.idiom.int_tryparse_to_int_tryparse_out
    source_form: >-
      final limit = int.tryParse(parts[1]); if (limit == null || limit <= 0) {
      ... }
    target_decision: >-
      Map to `if (!int.TryParse(parts[1], out var limit) || limit <= 0) { ... }`.
      The `:limit`, `:boot` timeout (`int.tryParse(parts[2]) ?? 10`), and any
      numeric-argument parse all use the `out`-parameter form;
      `int.tryParse(parts[2]) ?? 10` becomes `int.TryParse(parts[2], out var t)
      ? t : 10`.
    idiom_id: null
    research_finding_id: rf-dart-int-tryparse-to-csharp-tryparse-out
    nuance: >-
      Return-value vs out-parameter: Dart `int.tryParse` returns `int?` (null
      on failure); C# `int.TryParse` returns `bool` and writes the parsed value
      to an `out` parameter (which is `0`/default on failure). The combined
      guard `limit == null || limit <= 0` is rewritten as
      `!int.TryParse(...) || limit <= 0` so the failure case short-circuits
      before the `<= 0` test ever reads an undefined value — semantically
      equivalent, idiomatic in the Dart→C# pair.
  - construct_key: dart.process.run_git_to_process_argumentlist_async
    source_form: >-
      Future<String?> _getGitCommit() async { try { final result = await
      Process.run('git', ['log', '-1', '--format=%h %s']); if (result.exitCode
      == 0) return result.stdout.toString().trim(); } catch (e) {} return null; }
    target_decision: >-
      `private static async Task<string?> GetGitCommitAsync()` constructing a
      `Process` whose `ProcessStartInfo("git")` sets `RedirectStandardOutput =
      true`, `RedirectStandardError = true`, `UseShellExecute = false`,
      `CreateNoWindow = true`, and supplies arguments through
      `StartInfo.ArgumentList.Add("log")`, `.Add("-1")`,
      `.Add("--format=%h %s")` (the space-containing format passed as ONE
      argument, no shell quoting). After `p.Start()`: `var stdout = await
      p.StandardOutput.ReadToEndAsync(); await p.WaitForExitAsync();` and return
      `stdout.Trim()` when `p.ExitCode == 0`. The whole body is wrapped in
      `try { … } catch { return null; }` to mirror Dart's silent
      git-missing/not-a-repo swallow.
    idiom_id: null
    research_finding_id: rf-dart-process-run-to-process-argumentlist-waitforexitasync
    nuance: >-
      Async-process model: Dart `Process.run` is a single awaitable that
      buffers stdout and waits for exit; the .NET equivalent is two awaits —
      `StandardOutput.ReadToEndAsync()` then `WaitForExitAsync()` (the
      documented .NET 5+ non-blocking pattern) — reading the stream before
      awaiting exit avoids the classic full-pipe deadlock. Argument passing:
      Dart takes a `List<String>` (each element a discrete argv entry, no shell
      involved); `ProcessStartInfo.ArgumentList` is the whitespace-safe
      equivalent (`%h %s` stays one argument), preferred over the legacy
      `Arguments` string which would require manual quote-escaping.
  - construct_key: dart.async.future_delayed_to_task_delay
    source_form: >-
      await Future.delayed(Duration(seconds: timeoutSec));
    target_decision: >-
      Map to `await Task.Delay(TimeSpan.FromSeconds(timeoutSec))` inside the
      `_runBoot` lifecycle (between `manager.Start()` and the `finally`
      `manager.Shutdown()`).
    idiom_id: null
    research_finding_id: rf-dart-future-delayed-to-task-delay
    nuance: >-
      Name-only async-sleep translation: both are non-blocking awaitable
      delays that yield the calling context for the given duration; `Duration(
      seconds: n)` → `TimeSpan.FromSeconds(n)`. No thread is blocked in either
      runtime. No semantic difference beyond the type/method name.
  - construct_key: dart.cli.run_boot_isolate_manager_lifecycle
    source_form: >-
      Future<void> _runBoot(String bootPath, String rootSelfGlpPath, int
      timeoutSec) async { ... bootFile.readAsStringSync(); BootLoader().load(
      source); ... final manager = IsolateManager(); manager.onUIOutput =
      (agentId, message) { print('[$agentId] $message'); }; await
      manager.boot(config); manager.start(); await Future.delayed(...);
      finally { await manager.shutdown(); } }
    target_decision: >-
      `private static async Task RunBoot(string bootPath, string
      rootSelfGlpPath, int timeoutSec)`. The Dart `File(bootPath)`
      construct-then-validate becomes `try { bootPathFull =
      Path.GetFullPath(bootPath); } catch (Exception e) { Console.WriteLine(
      $"Error: invalid boot path: {e.Message}"); return; }` (path-syntax
      validation). `bootFile.existsSync()` → `File.Exists(bootPathFull)`;
      `bootFile.readAsStringSync()` → `File.ReadAllText(bootPathFull,
      Encoding.UTF8)`. `BootLoader().load(source)` → `new BootLoader().Load(
      source)` per `boot_loader.dart.md`. `config.projectDir` /
      `config.rootSelfGlpPath` → `config.ProjectDir` / `config.RootSelfGlpPath`
      (mutable on `BootConfig`). The directives banner
      `config.directives.map((d) => d.agentId).join(', ')` →
      `string.Join(", ", config.Directives.Select(d => d.AgentId))`. The
      `IsolateManager` field `onUIOutput` is `Action<string, Term>?` per
      `isolate_manager.dart.md`, so `manager.OnUIOutput = (agentId, message) =>
      Console.WriteLine($"[{agentId}] {message}");`. Lifecycle:
      `await manager.Boot(config); manager.Start(); await Task.Delay(...);`
      with `catch (Exception e) { Console.WriteLine($"Boot failed:
      {e.Message}"); Console.WriteLine(e.StackTrace); }` and
      `finally { await manager.Shutdown(); }`.
    idiom_id: null
    research_finding_id: rf-dart-runboot-file-bootloader-isolatemanager-lifecycle
    nuance: >-
      Two-arg catch: Dart `catch (e, st)` captures both exception and
      stack-trace; C# `catch (Exception e)` exposes the trace via
      `e.StackTrace`, and the Dart `print(st)` of the trace is preserved.
      Dynamic-vs-typed callback param: the Dart lambda's `message` is
      statically `dynamic` and reaches `print` (calls `.toString()`); the C#
      `Action<string, Term>` types it as `Term`, rendered via `Term.ToString()`
      — semantically equivalent. `try/finally` over `await` has identical
      ordering in both runtimes (the `finally` Shutdown runs after the awaited
      delay or after a thrown boot error). Error-type names retain Dart names
      (e.g. `BootLoaderException`) per the 018 CompileError precedent — no
      `*Error`→`*Exception` rename invented here.
  - construct_key: dart.cli.console_utf8_output_encoding
    source_form: >-
      print('╔════...╗'); print('→ succeeds'); print('✓ Loaded: $filename');
    target_decision: >-
      The box-drawing characters (`╔ ║ ╚`), the success check `✓` (U+2713), and
      the status arrow `→` (U+2192) are preserved verbatim in the UTF-8 source.
      As the VERY FIRST statement of `Main`, before any banner output, set
      `Console.OutputEncoding = System.Text.Encoding.UTF8;`.
    idiom_id: null
    research_finding_id: rf-dotnet-console-outputencoding-utf8
    nuance: >-
      Encoding parity: Dart writes UTF-8 to stdout by default on every
      platform, so the box/check/arrow glyphs render correctly with no setup. A
      Windows console defaults to a legacy OEM/ANSI code page (e.g. 1252) that
      renders these multibyte glyphs as `?`. Setting
      `Console.OutputEncoding = Encoding.UTF8` at program start is REQUIRED for
      behavioural parity — it is not cosmetic; without it the REPL banner and
      status markers regress on Windows.
conversion_units:
  - project GlpRuntime.Cli (OutputType=Exe, TargetFramework=net10.0, ProjectReference to GlpRuntime)
  - static class Program (with private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled))
  - public static async Task Main(string[] args) — UTF-8 encoding set first; banner; AppContext.BaseDirectory self.glp resolve; GlpEngine ctor; while(true) REPL loop; eleven command branches; directory-load; .glp file-load; goal-execution with FormatTerm bindings + PrintStatus
  - private static void PrintStatus(ExecutionStatus status) — switch expression with throwing discard arm
  - private static void PrintHelp() — verbatim Console.WriteLine sequence
  - private static string FormatTerm(Term? term, GlpEngine? engine = null, HashSet<int>? path = null) — recursive term printer, HashSet visit-set, three is-pattern branches
  - private static async Task RunBoot(string bootPath, string rootSelfGlpPath, int timeoutSec) — file load + BootLoader + mad_boot leaf check + IsolateManager Boot/Start/Shutdown + Task.Delay
  - private static async Task<string?> GetGitCommitAsync() — Process + ArgumentList + ReadToEndAsync + WaitForExitAsync + catch-swallow
escalations: []
```

## Rationale & Research Provenance

`bin/glp_repl.dart` is the lone executable entry-point of `glp_runtime_net/bin/` — a thin, class-free procedural CLI wrapper around `GlpEngine`. Its conversion turns the Dart `library;` + top-level `main` + four private helpers into a C# console project `GlpRuntime.Cli` with a single `Program` static class. Every API touched is either a member of a RATIFIED dependency convspec (`glp_engine`, `scheduler`, `terms`, `boot_loader`, `isolate_manager`) or a universally-known .NET BCL surface (`Console`, `File`, `Path`, `Process`, `Regex`, `Task`, `HashSet`, `string.Join`). The non-mechanical decisions all turn on Dart→C# semantics — async entrypoint, URI-vs-path script resolution, exhaustive-switch totality, recursive cycle-detection by shared reference, `tryParse` return-vs-out, async process I/O, and console encoding — each grounded below. Per plan §4, no web research was required; the two non-obvious mappings (`Platform.script.resolve` and `pathSegments.lastOrNull`) are documented Microsoft Learn idioms.

### rf-dart-main-async-to-csharp-static-async-task-main

**Deep analysis.** Dart's `void main() async` is the program entrypoint and may `await` directly (the first awaited call is `_getGitCommit()`). C# entrypoints that need top-level `await` must be declared `static async Task Main` (or `Task<int>`); a plain `void Main` cannot legally `await`. The REPL loop, the trailing-`.` strip, the eleven command branches, and the directory/file/goal dispatch all translate as straight control-flow.

**Research (authoritative).** Microsoft Learn "Main() and command-line arguments" (`https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/program-structure/main-command-line`) documents the `async Task Main` signature as the supported asynchronous entrypoint form. `Console.ReadLine` (Microsoft Learn) "Reads the next line of characters … Returns … `null` if no more lines are available" — the documented EOF-as-null behaviour that makes `if (input == null) break` a one-to-one map for Dart's `stdin.readLineSync()` nullable return.

**Conclusion.** `public static async Task Main(string[] args)`; loop and command dispatch preserved verbatim; equality commands use C# 9 `or` patterns and prefix commands use `StartsWith(..., StringComparison.Ordinal)` so recognition is culture-invariant. The trailing-`.` strip stays guarded against `.glp` filenames.

### rf-dart-platform-script-resolve-to-appcontext-basedirectory

**Deep analysis.** `Platform.script` is a `Uri` to the running script; `.resolve('../../programs/self.glp')` climbs two levels to the repo root then descends to the prelude, and `.toFilePath()` turns the resolved URI into a native filesystem path. The C# anchor for "where the application lives" is `AppContext.BaseDirectory`.

**Research (authoritative).** Microsoft Learn `AppContext.BaseDirectory` (`https://learn.microsoft.com/en-us/dotnet/api/system.appcontext.basedirectory`): "Gets the file path of the base directory that the assembly resolver uses to probe for assemblies" — documented in .NET guidance as "the directory of the application that hosts the AppDomain". Microsoft Learn `Path.GetFullPath` returns "the absolute path for the specified path string", collapsing relative `..` segments, and `Path.Combine` joins path segments using the platform separator.

**Conclusion.** `Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "programs", "self.glp"))`. No URI round-trip or percent-decoding is needed because the `Path.*` APIs operate on native path strings directly; `Path.GetFullPath` performs the `..` normalisation that `Uri.resolve` did implicitly. The result feeds the named-argument `GlpEngine` constructor.

### rf-dart3-exhaustive-switch-to-csharp-switch-throwing-discard

**Deep analysis.** `_printStatus` is a Dart 3 `switch` over the three-member `ExecutionStatus` enum with NO `default` arm — Dart's flow analysis statically proves exhaustiveness over the finite enum. C# does not extend the same compile-time exhaustiveness proof to enum `switch` expressions (an out-of-range cast `int` could in principle reach it).

**Research (authoritative).** Microsoft Learn "Pattern matching — switch expression" (`https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching`): a `switch` expression evaluates "the first matching pattern" and the compiler "throws an exception if the input … doesn't match any of the switch arms" when no discard arm is present — but the idiomatic, explicit equivalent of Dart's totality is a `_ => throw …` discard arm, which documents intent and fails fast.

**Conclusion.** A C# `switch` expression with the three PascalCased enum arms (`scheduler.dart.md`) and a throwing `_ => throw new ArgumentOutOfRangeException(nameof(status))` discard arm, emitted through `Console.WriteLine`, reproducing Dart's "every value handled or it is a bug" guarantee. The `→` (U+2192) glyph is kept verbatim in UTF-8 source.

### rf-dart-recursive-term-printer-hashset-visitset

**Deep analysis.** `_formatTerm` is a recursive, cycle-detecting term printer. It carries a `Set<int>? path` of visited heap addresses, defaulting it to an empty set on first call (`path ??= <int>{}`), adding an address before recursing into it and removing it afterwards, and emitting `<circular>` when an address re-occurs. It dispatches on `ConstTerm` / list-shaped `StructTerm` (functor `.`, arity 2) / other `StructTerm`, dereferencing `VarRef`s through `engine.runtime.heap` and labelling unbound vars `Xn?` (reader) or `Xn` (writer).

**Research (authoritative).** Microsoft Learn "Pattern matching" (same doc) — the declaration/type pattern "test[s] the type of an expression and, if it matches, assign[s] it to a new variable", fusing Dart's `is`+`as` into one arm (`term is StructTerm s`). Microsoft Learn `HashSet<T>` (`https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1`) is a reference type with `Add`/`Remove`/`Contains`; passing it into a recursive call shares the same instance, exactly mirroring Dart `Set<int>` reference semantics. Microsoft Learn `??=` null-coalescing assignment maps `path ??= <int>{}`.

**Conclusion.** `private static string FormatTerm(Term? term, GlpEngine? engine = null, HashSet<int>? path = null)` with `path ??= new HashSet<int>()`, three `is`-pattern branches, LINQ `Select` for the multi-arg struct case, and PascalCased `Runtime.Heap.Dereference`/`IsReader`. The load-bearing nuances are null-safety (`ConstTerm.Value` is `object?`, so the Dart loose `== 'nil'` becomes an explicit `is string str && str == "nil"`, and the final `term.ToString() ?? string.Empty` covers C#'s nullable `object.ToString()`), and reference identity of the visit-set (shared by reference, never copied, so the add-before / remove-after cycle protocol is preserved bit-for-bit).

### rf-dart-pathsegments-lastornull-to-path-getfilename

**Deep analysis.** `_runBoot` computes the project directory by asking whether the boot-file parent's last non-empty path segment is `mad_boot`; if so the project directory is the grandparent, else the parent. The Dart form filters empty segments (`where((s) => s.isNotEmpty)`) before `lastOrNull` to be robust to a trailing separator.

**Research (authoritative).** Microsoft Learn `Path.GetFileName` (`https://learn.microsoft.com/en-us/dotnet/api/system.io.path.getfilename`): "Returns the file name and extension of the specified path string" — for a directory path this is the leaf segment, and the documented behaviour returns `Empty` when the path ends in a directory separator (matching Dart's empty-segment filtering). Microsoft Learn `Path.GetDirectoryName` "Returns the directory information for the specified path" — the one-level-up climb for the `parent.parent` case.

**Conclusion.** `Path.GetFileName(parentDir) == "mad_boot" ? (Path.GetDirectoryName(parentDir) ?? parentDir) : parentDir`, with `parentDir = Path.GetDirectoryName(bootPathFull) ?? string.Empty`. The collection-extension `lastOrNull` (nullable) is replaced by the non-null leaf-segment API; trailing-separator robustness is equivalent on .NET 10.

### rf-dart-int-tryparse-to-csharp-tryparse-out

**Deep analysis.** The `:limit` and `:boot` branches use `int.tryParse`, which returns `int?` (null on failure). The limit guard is `if (limit == null || limit <= 0)`; the boot timeout uses `int.tryParse(parts[2]) ?? 10`.

**Research (authoritative).** Microsoft Learn `int.TryParse` (`https://learn.microsoft.com/en-us/dotnet/api/system.int32.tryparse`): "Converts the … string representation of a number to its … integer equivalent. A return value indicates whether the conversion succeeded." It returns `bool` and writes the result to an `out` parameter — the Try-pattern that replaces Dart's nullable return.

**Conclusion.** `if (!int.TryParse(parts[1], out var limit) || limit <= 0)` for the limit guard (the parse-failure short-circuits before `<= 0` reads the value); `int.TryParse(parts[2], out var t) ? t : 10` for the `?? 10` default. A deliberate, semantically-equivalent C# idiom shift.

### rf-dart-process-run-to-process-argumentlist-waitforexitasync

**Deep analysis.** `_getGitCommit` runs `git log -1 --format=%h %s`, returns trimmed stdout on exit 0, and swallows all exceptions (git absent / not a repo) returning `null`. Dart's `Process.run` takes a `List<String>` argv (no shell) and is a single await that buffers stdout and waits for exit.

**Research (authoritative).** Microsoft Learn `Process.WaitForExitAsync` (`https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexitasync`) and `StreamReader.ReadToEndAsync` are the documented .NET 5+ asynchronous process pattern — read the redirected stream to end, then await exit, to avoid the full-pipe deadlock. Microsoft Learn `ProcessStartInfo.ArgumentList` (`https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.argumentlist`): "a collection … used … to pass arguments" with the runtime "properly quot[ing] each argument" — the whitespace-safe replacement for the legacy `Arguments` string, so `--format=%h %s` is passed as one argument verbatim.

**Conclusion.** `private static async Task<string?> GetGitCommitAsync()` building a `Process` with `RedirectStandardOutput`, `UseShellExecute = false`, `CreateNoWindow = true`, args via `ArgumentList.Add(...)`, reading `await StandardOutput.ReadToEndAsync()` then `await WaitForExitAsync()`, returning `stdout.Trim()` when `ExitCode == 0`, all under a `try { … } catch { return null; }` that mirrors Dart's silent git-missing swallow.

### rf-dart-future-delayed-to-task-delay

**Deep analysis.** The boot lifecycle sleeps `await Future.delayed(Duration(seconds: timeoutSec))` between starting the isolates and the `finally` shutdown.

**Research (authoritative).** Microsoft Learn `Task.Delay` (`https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.delay`): "Creates a task that completes after a specified time interval" — a non-blocking awaitable delay that does not consume a thread, the direct analog of Dart's `Future.delayed`. `Duration(seconds: n)` maps to `TimeSpan.FromSeconds(n)`.

**Conclusion.** `await Task.Delay(TimeSpan.FromSeconds(timeoutSec))`. Name-only translation; identical async-sleep semantics, no thread blocked in either runtime.

### rf-dart-runboot-file-bootloader-isolatemanager-lifecycle

**Deep analysis.** `_runBoot` validates the boot path (Dart's `File()` may throw on illegal Windows characters), reads the file, parses via `BootLoader.load`, computes the `mad_boot` project directory, sets two mutable `BootConfig` fields, then drives the `IsolateManager` lifecycle (`boot`/`start`/`shutdown`) with a `Future.delayed` in the middle, a two-arg `catch (e, st)` that prints the stack-trace, and a `finally` shutdown.

**Research (authoritative).** The `BootLoader`/`BootConfig` API (`Load`, mutable `ProjectDir`/`RootSelfGlpPath`) is taken from RATIFIED `lib/multiagent/boot_loader.dart.md`; the `IsolateManager` API (`OnUIOutput: Action<string, Term>?`, `Boot`/`Start`/`Shutdown`, no-Isolate.kill shutdown contract) from RATIFIED `lib/multiagent/isolate_manager.dart.md`. Microsoft Learn `Path.GetFullPath` documents that it "may throw" `ArgumentException`/`PathTooLongException` on illegal paths — the equivalent of Dart's invalid-path branch; Microsoft Learn `File.ReadAllText` reads the whole file (UTF-8 specified). No external lookup beyond these ratified specs and standard BCL.

**Conclusion.** `private static async Task RunBoot(...)` mapping `File()`-construct-then-validate to a `Path.GetFullPath` try/catch, `existsSync`→`File.Exists`, `readAsStringSync`→`File.ReadAllText(..., Encoding.UTF8)`, the `mad_boot` leaf check via `Path.GetFileName` (see that finding), `OnUIOutput` as an `Action<string, Term>` lambda, and the `try / catch (Exception e) { … e.StackTrace } / finally { await manager.Shutdown(); }` lifecycle. Dart's `dynamic` callback parameter renders via `Term.ToString()` — equivalent. Error-type names retain their Dart names per the 018 CompileError precedent (no `*Error`→`*Exception` rename invented).

### rf-dotnet-console-outputencoding-utf8

**Deep analysis.** The banner uses box-drawing characters (`╔ ║ ╚`), the load path prints `✓` (U+2713), and `_printStatus` prints `→` (U+2192). Dart writes UTF-8 to stdout by default on all platforms, so these render correctly with no setup.

**Research (authoritative).** Microsoft Learn `Console.OutputEncoding` (`https://learn.microsoft.com/en-us/dotnet/api/system.console.outputencoding`): "Gets or sets the encoding the console uses to write output." A Windows console defaults to a legacy OEM/ANSI code page that cannot represent these multibyte glyphs (rendered as `?`); setting `Console.OutputEncoding = Encoding.UTF8` switches the console to UTF-8.

**Conclusion.** Set `Console.OutputEncoding = System.Text.Encoding.UTF8` as the FIRST statement of `Main`, before any output. This is REQUIRED for behavioural parity — without it the box-drawing banner, the `✓` check, and the `→` arrow regress to `?` on Windows.

### Trivial constructs

`_printHelp` is pure I/O glue: each `print('...')` maps mechanically to
`Console.WriteLine("...")` with the help text and example comments preserved
verbatim — `trivial: true`, no deep-analysis or research basis required. All
other constructs carry both a deep-analysis basis and an authoritative
`research_finding_id` above.
