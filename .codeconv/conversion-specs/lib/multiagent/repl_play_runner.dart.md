# Conversion Spec — lib/multiagent/repl_play_runner.dart

> Conversion-spec artifact for lib/multiagent/repl_play_runner.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/multiagent/repl_play_runner.dart
source_sha256: ebe529f88b605e1c33e3158837c2a5a09599572cfe7c195a3f5712e5846dc169
target_code_unit: lib/multiagent/repl_play_runner.cs
constructs:
  - construct_key: dart.data_class.immutable_three_final_string_positional_ctor.PlayOutput
    source_form: >-
      "class PlayOutput { final String agentId; final String kind; final String content;
      PlayOutput(this.agentId, this.kind, this.content); }"
    target_decision: >-
      Emit a reference type "class PlayOutput" (NOT a record struct, NOT a struct)
      with three get-only auto-properties of type "string" — AgentId, Kind, Content
      — initialised by a single positional constructor mirroring the Dart
      parameter order: PlayOutput(string agentId, string kind, string content). A
      reference class is chosen rather than a record because PlayOutput is
      delivered through a callback (onOutput) and Dart instance identity may be
      observed by downstream UI panel routing (a record's synthesised value
      equality would change observed semantics if any consumer dictionary-keyed
      or reference-compared instances). A struct is rejected because the value
      escapes through a captured delegate (boxing on every callback would be a
      silent allocation regression) and PlayOutput is conceptually a small
      message object, not a primitive. The Dart triple-slash doc-comment is
      preserved verbatim as XML-doc on the type and on each property.
    idiom_id: null
    research_finding_id: rf-dart-final-field-class-to-csharp-getonly-class
    nuance: >-
      Immutability nuance: Dart "final String" fields are write-once and map to
      get-only auto-properties (NOT readonly fields — properties preserve the
      public field-access surface the Dart side exposes through "output.agentId"
      etc.). Reference-vs-value nuance: must remain a reference "class" because
      the instance is delivered through a delegate (Action<PlayOutput>) and any
      reference-identity comparison or boxed dictionary key in a consumer would
      regress under a value-type translation. Null-safety nuance: all three
      fields are non-nullable Dart "String"; under an enabled NRT context the
      ctor parameters and properties are non-nullable "string" — the codegen
      pass MUST NOT widen any of them to "string?".
  - construct_key: dart.nullable_function_field.callback_void_function_T_question.assigned_from_outside_invoked_with_question_call
    source_form: >-
      "void Function(PlayOutput output)? onOutput; void Function(String line)? onLog;
      void Function(String error)? onError; void Function(int exitCode)? onDone;"
      with call sites of the form "onOutput?.call(PlayOutput(...))" and
      "onLog?.call('REPL: ...')".
    target_decision: >-
      Emit four nullable delegate fields on the class — public Action<PlayOutput>?
      OnOutput; public Action<string>? OnLog; public Action<string>? OnError;
      public Action<long>? OnDone; — preserving the "set from outside, invoked
      from inside" pattern verbatim. "Action<T>" matches the Dart "void
      Function(T)" shape: one in-parameter, no return value. C# "event" is
      REJECTED because the Dart callbacks are single-subscriber assignment-style
      ("runner.onOutput = (...) { ... }" replaces, does not subscribe); a C#
      event would change the public API to "+= subscribe" with multicast
      semantics and synthesised add/remove accessors. The Dart "?.call(x)"
      invocation maps to the C# null-conditional "OnOutput?.Invoke(x)" pattern:
      atomic null-check + dispatch with no race on a captured local. The
      onDone(int) parameter follows the Dart-int → C#-long width decision; an
      OS process exit code in .NET is Int32 but the SPEC choice for Dart int
      width fidelity is "long" (a later codegen pass MAY down-map to int with a
      recorded per-field justification).
    idiom_id: null
    research_finding_id: rf-dart-void-function-question-to-csharp-action-nullable
    nuance: >-
      Null-safety nuance (load-bearing): Dart "void Function(T)?" is a nullable
      function type, ABSENT by default — "T?" annotation maps directly to C#
      "Action<T>?" under an enabled NRT context. Invocation nuance: Dart
      "onOutput?.call(x)" is a null-conditional method invocation on a field;
      C# "OnOutput?.Invoke(x)" is its faithful counterpart — both perform an
      atomic null-check + dispatch with no double-evaluation hazard. The
      common C# multicast-event race ("snapshot the delegate to a local before
      invoke") is irrelevant here because the field is single-subscriber by
      design — replacement assignment ("runner.onOutput = ...") publishes the
      whole new delegate atomically. Delegate-identity nuance: Action<T> is a
      reference type, equality is reference identity unless wrapped; consumers
      that re-assign rather than subscribe inherit the Dart semantics.
  - construct_key: dart.nullable_reference_field.Process_question.subprocess_handle
    source_form: >-
      "Process? _process;" plus "_process = process;", "_process = null;",
      and the getter "bool get isRunning => _process != null;", and the kill()
      method "void kill() { _process?.kill(); _process = null; }".
    target_decision: >-
      Emit a private nullable field "private System.Diagnostics.Process?
      _process;" of the .NET subprocess-handle type. The "bool get isRunning =>
      _process != null" Dart expression-bodied getter maps to a C# expression-
      bodied property "public bool IsRunning => _process is not null;" (the "is
      not null" form engages the C# null-state analyser and is the recommended
      shape on a nullable reference; semantically identical to "!= null"). The
      kill() method maps to a public void Kill() that performs the null-
      conditional invocation "_process?.Kill();" then assigns "_process = null;".
      Concurrency nuance: in Dart this field is mutated from the single async
      method run() and read from kill(); in .NET the equivalent visibility is
      not naturally atomic for a nullable reference under all memory models —
      since the source treats this as a single-thread-of-control by-convention
      (one play at a time per runner instance), the C# spec preserves that
      assumption and does NOT introduce a lock around the field; a future
      hardening pass MAY introduce volatile/Interlocked.Exchange with a recorded
      per-field justification.
    idiom_id: null
    research_finding_id: rf-dart-process-to-dotnet-system-diagnostics-process
    nuance: >-
      Null-safety nuance: Dart "Process?" is a nullable reference; C#
      "System.Diagnostics.Process?" under enabled NRT is its counterpart. The
      "_process?.kill()" null-conditional invocation maps directly. Resource
      nuance (explicitly addressed): .NET System.Diagnostics.Process is
      IDisposable — the Dart Process handle is GC-managed and "kill()" does NOT
      dispose; the faithful translation leaves Dispose() OUT of kill() too so
      the externally-observable semantics match (the OS process is killed; the
      .NET handle is dropped on next assignment / runner GC). A separate cleanup
      construct could add IDisposable on the runner, but that is a behaviour
      addition (currently absent in Dart) and is INTENTIONALLY not specified
      here — keeping line-for-line fidelity. Process-handle-vs-pid nuance:
      Dart "process.pid" returns int (the OS PID) — same in .NET via
      Process.Id (Int32); the Dart-int → C#-long width policy applies.
  - construct_key: dart.compile_time_const_list_string_literal.static_const_with_spread_and_collection_if
    source_form: >-
      "static const cssgFiles = [ '../programs/cssg_modules_v2', ];"
      and similar bondsFiles, cssnFiles, cssnVillageFiles, and the
      spread-only "static const bondsPlay12Files = [ ...bondsFiles, ];".
      All five are public static const List<String> exposed as default seeds
      for the runner's glpFiles parameter.
    target_decision: >-
      Emit each as "public static readonly IReadOnlyList<string> CssgFiles = new[]
      { \"../programs/cssg_modules_v2\" };" (and analogous BondsFiles,
      CssnFiles, CssnVillageFiles), with the spread-only "BondsPlay12Files = new[]
      { ..BondsFiles[0] };" expanded at the C# spec level (spread inside an
      array initialiser is NOT C# syntax — the spread is materialised as
      "BondsFiles.ToArray()" or, for this trivial single-element case, a
      duplicated literal). The Dart "static const" indicates compile-time
      constant + canonicalised + deeply-immutable; .NET has no compile-time
      constant List<T> (its "const" is reserved for primitive/string compile-
      time literals), so the SPEC choice is "static readonly" exposing an
      IReadOnlyList<string> surface so consumers cannot mutate the seed list.
      An ImmutableArray<string> initialised via ImmutableArray.Create("...")
      is a stronger alternative and is RECOMMENDED for any future hardening
      pass; the baseline is "static readonly" array typed as IReadOnlyList for
      review parity. The default-parameter wiring (this.glpFiles = cssgFiles)
      is addressed by the constructor construct below.
    idiom_id: null
    research_finding_id: rf-dart-static-const-list-to-csharp-static-readonly-readonlylist
    nuance: >-
      Constant-vs-readonly nuance (explicitly addressed, load-bearing): Dart
      "static const" on a list literal yields a compile-time-canonicalised,
      deeply-immutable List<String> — every reader sees the same canonical
      instance and CANNOT mutate it. C# "const" is reserved for compile-time-
      constant primitive/string fields, so the only honest mapping is "static
      readonly" + an immutable surface (IReadOnlyList<string> or
      ImmutableArray<string>); the field reference itself is not assignable
      after class initialisation. Spread-operator nuance (explicitly addressed):
      Dart "[...bondsFiles]" is a list-literal spread that copies elements into
      a new const list. C# 12 collection expressions support "[..source]" inside
      a collection expression but ONLY for non-const targets and require a
      target-typed expression; the SPEC choice keeps it deterministic — expand
      to the explicit element list at the C# layer for the spread-only case.
      Collection-if nuance: this file's static lists do not use Dart collection-
      if (those appear later in _findDart winCandidates); recorded here for
      completeness.
  - construct_key: dart.static_final.RegExp_with_raw_string_pattern.precompiled_class_field
    source_form: >-
      "static final _taggedRegex = RegExp(r'^tagged\\((\\w+), (cmd|notify|friend|say|act|event)\\((.+)\\)\\)\\$');"
      with consumption "_taggedRegex.firstMatch(stripped)" and capture-group
      reads "match.group(1)!", "match.group(2)!", "match.group(3)!".
    target_decision: >-
      Emit a private static readonly Regex field with the pattern preserved
      byte-identically as a verbatim string literal: "private static readonly
      System.Text.RegularExpressions.Regex TaggedRegex = new(@\"^tagged\\((\\w+),
      (cmd|notify|friend|say|act|event)\\((.+)\\)\\)$\", RegexOptions.Compiled);"
      — the C# verbatim "@\"...\"" string is the counterpart to Dart raw
      "r'...'" (no backslash escaping in either). RegexOptions.Compiled is
      RECOMMENDED to mirror the Dart "static final" precompilation intent —
      Regex is JIT-compiled to IL once at first use. A modern alternative is a
      source-generated regex with [GeneratedRegex("...")] on a partial method,
      which produces equivalent code at compile time and is preferred for new
      code; the baseline choice is RegexOptions.Compiled for review parity.
      Match consumption: "_taggedRegex.firstMatch(s)" returns Match? in Dart;
      maps to "var match = TaggedRegex.Match(s)" returning System.Text.
      RegularExpressions.Match (never null — non-success is "match.Success ==
      false"). Capture-group reads "match.group(1)!" map to
      "match.Groups[1].Value" (1-based indexing preserved; ".Value" is non-null
      when match.Success is true).
    idiom_id: null
    research_finding_id: rf-dart-regexp-to-csharp-regex-precompiled-verbatim
    nuance: >-
      Match-failure model nuance (explicitly addressed, load-bearing): Dart
      RegExp.firstMatch returns "Match?" — null on no match — and the source
      handles that with "if (match == null) { onLog?.call('REPL: $line');
      return; }". C# Regex.Match returns a never-null Match whose ".Success"
      flag indicates no-match; the faithful translation is "if (!match.Success)
      { OnLog?.Invoke($\"REPL: {line}\"); return; }". This is NOT a behaviour
      change; the boundary is structurally different but the observable branch
      is identical. Capture-group nuance: Dart "match.group(n)!" asserts non-
      null on a successful match; C# "match.Groups[n].Value" is non-null on a
      successful match but the indexer accepts both int and string keys. Raw-
      string nuance (explicitly addressed): Dart raw "r'...'" disables backslash
      escapes; C# verbatim "@\"...\"" does the same — both preserve "\\w" and
      "\\(" as two-character regex tokens without lexical re-escaping.
      Precompilation nuance: Dart "static final" pre-compiles the regex once on
      first class load; the .NET counterpart MUST also be "static readonly" +
      RegexOptions.Compiled (or source-generated) so allocation/compilation cost
      is paid once per process, not per call.
  - construct_key: dart.ctor.required_named_parameter_plus_default_named_pointing_at_static_const
    source_form: >-
      "ReplPlayRunner({required this.repoRoot, this.glpFiles = cssgFiles});"
    target_decision: >-
      Emit a public constructor "public ReplPlayRunner(string repoRoot,
      IReadOnlyList<string>? glpFiles = null) { this.RepoRoot = repoRoot;
      this.GlpFiles = glpFiles ?? CssgFiles; }". C# does not have Dart's
      "required" named-parameter syntax outside of class-member "required"
      properties (C# 11+) — and the Dart "required this.repoRoot" is enforcing
      caller-supplied non-null, not a property-init requirement on the field —
      so the faithful spec choice is a non-default positional/named parameter
      whose absence is a compile-time error. The default "this.glpFiles =
      cssgFiles" uses a Dart compile-time-const default value; C# requires
      compile-time constants in optional-parameter defaults, which "new[] { ... }"
      is NOT — therefore the SPEC choice is "= null" with a null-coalescing
      fallback to the CssgFiles static field in the body. RepoRoot and GlpFiles
      are get-only properties initialised once in the ctor (matching Dart
      "final" on the implicit this-promotion of repoRoot, though the source
      does NOT actually declare repoRoot as final — Dart "final String
      repoRoot;" IS declared at the class top; the C# property is therefore
      get-only with private set or { get; init; }).
    idiom_id: null
    research_finding_id: rf-dart-named-required-default-to-csharp-nullcoalesced-default
    nuance: >-
      Required-named-vs-positional nuance (explicitly addressed): Dart
      "required this.repoRoot" is a callsite contract — callers must pass
      "repoRoot:" by name. C# named arguments are caller-discretionary on a
      positional parameter; the spec does not preserve the "by name only"
      contract because C# has no equivalent (C# named-parameter syntax is
      callsite-optional). This is recorded as a deliberate semantic narrowing,
      not silently dropped. Default-value nuance (load-bearing): Dart accepts
      a "static const List" reference as a parameter default; C# does not
      accept a non-primitive constant in a default-value slot — every C#
      default-parameter value MUST be a compile-time constant. The "= null
      then ??=" pattern is the standard idiom for substituting a non-constant
      default. Null-safety nuance: GlpFiles is published as non-null
      "IReadOnlyList<string>" because the fallback always materialises a non-
      null value; the parameter type is nullable "IReadOnlyList<string>?" to
      enable the default-substitution dance.
  - construct_key: dart.async_method.future_void_run_with_process_spawn
    source_form: >-
      "Future<void> run(int playNumber) async { ... }" — single async method
      that does directory/file existsSync checks, spawns the REPL subprocess,
      pipes commands via stdin.writeln + close, subscribes to stdout/stderr
      Streams via .transform(utf8.decoder).transform(const LineSplitter()).listen,
      then "await process.exitCode" and dispatches onLog/onError/onDone
      callbacks.
    target_decision: >-
      Emit "public async Task RunAsync(long playNumber)" — Dart "Future<void>"
      maps to C# "Task" (NOT "Task<void>" — "void" is not a type argument in
      C#), and Dart's bare "async" maps to C# "async" with awaitable continuations.
      The Dart "int" parameter maps to C# "long" per the repo's Dart-int width
      policy. Method naming follows the .NET "RunAsync" convention (the
      "Async" suffix on awaitable methods is the Microsoft Framework Design
      Guideline). Internals decomposed by the dedicated constructs below
      (subprocess spawn, stdin pipe, async-line-iteration on stdout/stderr,
      exit-code await, exception handling). Cancellation nuance: the Dart
      source has NO CancellationToken — the only abort path is the separate
      kill() method that kills the subprocess; the C# baseline preserves this
      (no CancellationToken parameter on RunAsync). A future hardening pass
      MAY add a CancellationToken parameter and a registration that calls
      _process?.Kill() on cancellation; that is a behaviour addition, not a
      faithful translation, and is INTENTIONALLY not specified here.
    idiom_id: null
    research_finding_id: rf-dart-future-void-async-to-csharp-task-async
    nuance: >-
      Async-method-shape nuance (explicitly addressed, load-bearing): Dart
      "Future<void>" maps to C# "Task" (the non-generic awaitable for "no
      value"); Dart "Future<T>" maps to C# "Task<T>"; NEVER use "async void"
      in C# — async void is reserved for event handlers and silently swallows
      faults into the synchronization context. ConfigureAwait nuance: this is
      a library-style method with no UI thread affinity; per Microsoft
      Framework Design Guidelines, awaits in library code SHOULD use
      ConfigureAwait(false) to avoid recapturing a captured synchronization
      context — RECOMMENDED for every await in the translated body. Exception-
      propagation nuance: the Dart try/catch wraps the whole spawn+pipe+await;
      C# "async Task" propagates a single faulted exception (or
      AggregateException for multiple) to the awaiter — the existing try/catch
      with onError?.Invoke routing preserves the source's observable handling.
  - construct_key: dart.platform.file_directory_existsSync_path_join_string_interpolation
    source_form: >-
      "if (!Directory(runtimeDir).existsSync()) { ... }" and "if (File(compiledRepl).
      existsSync()) { ... }" and "if (File(replScript).existsSync()) { ... }" and
      path composition "'$repoRoot/glp_runtime'", "'$runtimeDir/bin/glp_repl.exe'",
      and the Windows-only File.existsSync candidate loop.
    target_decision: >-
      Emit synchronous filesystem existence checks via the .NET BCL:
      "if (!System.IO.Directory.Exists(runtimeDir)) { ... }" and
      "if (System.IO.File.Exists(compiledRepl)) { ... }". Dart's Directory(path).
      existsSync() and File(path).existsSync() are zero-arg sync calls returning
      bool; the .NET counterparts Directory.Exists(string) and File.Exists(string)
      have identical shape and semantics (return false on access errors,
      non-existence, or permission denial — same as Dart's synchronous variant).
      Path composition: Dart string-interpolation "'$repoRoot/glp_runtime'"
      maps to C# interpolation "$\"{repoRoot}/glp_runtime\"" verbatim — the
      forward-slash separator is preserved because the source ALREADY uses
      forward slashes on Windows (the Dart Windows shell accepts them and so
      does .NET on Win32). Using System.IO.Path.Combine is REJECTED for
      baseline parity (it normalises to OS-native separators which would
      change the log/onLog text observable in tests). A future hardening pass
      MAY swap to Path.Combine with a recorded per-callsite justification.
    idiom_id: null
    research_finding_id: rf-dart-fs-existssync-to-dotnet-directory-file-exists
    nuance: >-
      Sync-vs-async nuance (explicitly addressed): Dart "existsSync" is the
      synchronous variant (the file-system roundtrip happens on the calling
      thread, blocking); Dart also has "exists()" returning Future<bool>. The
      .NET BCL Directory.Exists/File.Exists are likewise synchronous. There is
      a Dart-async-aware alternative in .NET 8+ (DirectoryInfo / async
      enumerations) but the faithful 1:1 mapping for a "Sync" Dart call is the
      .NET synchronous BCL method. Path-separator nuance (explicitly addressed):
      Dart uses forward-slash separators on Windows and .NET accepts them on
      Win32 APIs — preserved verbatim so log lines and onLog text remain
      byte-identical. Existence-vs-permission nuance: both Dart existsSync and
      .NET File.Exists return false on permission errors and on non-existence
      (they do NOT throw on either); the SPEC preserves this — no try/catch
      wrapping is added around the existence checks.
  - construct_key: dart.platform.is_windows_environment_lookup
    source_form: >-
      "Platform.isWindows" used as a boolean and as a runInShell argument;
      "Platform.environment['USERPROFILE'] ?? ''" and "Platform.environment['HOME']"
      used as nullable env-var lookups; "Process.runSync('where', ['dart.exe'])"
      on Windows to resolve dart.exe via PATH.
    target_decision: >-
      Emit "Platform.isWindows" → "System.Runtime.InteropServices.RuntimeInformation.
      IsOSPlatform(OSPlatform.Windows)" — the documented OS-detection API since
      .NET Core 3.0 (Environment.OSVersion.Platform is legacy and discouraged
      for new code per Microsoft Learn). Cache the result in a "private static
      readonly bool IsWindows" if used more than once for review readability,
      but the spec semantics are identical. "Platform.environment[k]" maps to
      "System.Environment.GetEnvironmentVariable(string)" which returns "string?"
      (null when unset) — identical surface shape to Dart's "String?" return.
      The "?? ''" null-coalescing operator is identical in C# ("?? \"\""). The
      "where dart.exe" PATH resolution maps to a System.Diagnostics.Process
      ProcessStartInfo invocation with FileName="where", Arguments="dart.exe",
      RedirectStandardOutput=true, UseShellExecute=false — a synchronous
      Start() + WaitForExit() + StandardOutput.ReadToEnd() pair preserves the
      Dart Process.runSync semantics.
    idiom_id: null
    research_finding_id: rf-dart-platform-to-dotnet-runtimeinformation-environment
    nuance: >-
      Platform-detection nuance (explicitly addressed, load-bearing): Dart
      "Platform.isWindows" is a compile-against-the-Dart-VM check that returns
      true on a Win32 host. .NET has TWO families: Environment.OSVersion (legacy,
      can mis-report on .NET Core under certain shims) and RuntimeInformation.
      IsOSPlatform (the documented, recommended API since .NET Core 3.0). The
      faithful mapping is RuntimeInformation; consumers writing #if WINDOWS
      preprocessor directives would be an architectural change, not a faithful
      translation. Environment-variable nuance: both Dart Platform.environment
      and .NET Environment.GetEnvironmentVariable return a nullable string;
      they read a snapshot at first access (Dart) or a fresh OS read (.NET),
      which is observably identical for this file because the env-var values
      are read once per RunAsync call and not mutated mid-call. Null-coalesce
      nuance: Dart "?? ''" and C# "?? \"\"" are identical operators with
      identical short-circuit semantics.
  - construct_key: dart.process_start.async_subprocess_spawn_workingdirectory_runinshell
    source_form: >-
      "final process = await Process.start( executable, arguments,
      workingDirectory: runtimeDir, runInShell: Platform.isWindows, );" with
      subsequent "process.stdin.writeln(commands); await process.stdin.close();"
      and "final exitCode = await process.exitCode;".
    target_decision: >-
      Emit a System.Diagnostics.Process spawn via ProcessStartInfo: "var psi
      = new ProcessStartInfo { FileName = executable, WorkingDirectory =
      runtimeDir, RedirectStandardInput = true, RedirectStandardOutput = true,
      RedirectStandardError = true, UseShellExecute = !IsWindows ? false :
      false, CreateNoWindow = true, }; foreach (var a in arguments)
      psi.ArgumentList.Add(a); var process = Process.Start(psi)!;". Key
      decisions: (a) ArgumentList is used rather than the legacy "Arguments"
      string property — the Dart Process.start argument-list API passes args as
      a List<String> with no shell quoting, and ArgumentList is the .NET
      equivalent (each entry is escaped per the Windows CreateProcess rules,
      no shell involvement). (b) UseShellExecute MUST be false to enable
      RedirectStandardInput/Output/Error; "runInShell: Platform.isWindows" in
      Dart is a different concept (it spawns through cmd.exe for batch-file
      resolution and PATHEXT) — under .NET, the equivalent of Dart's
      "runInShell: true" is launching the .exe directly with UseShellExecute=
      false because .NET resolves PATHEXT itself when no extension is given,
      and the source already supplies ".exe" explicitly for the AOT path. The
      SPEC choice is UseShellExecute=false unconditionally, matching the
      observable behaviour of the Dart side which only sets runInShell to
      smooth the where-style PATH lookup the source has already handled in
      _findDart. (c) Process.Start returns Process? when UseShellExecute is
      true (no diagnostic mode); with UseShellExecute=false it returns a non-
      null Process for a successful spawn — the "!" null-forgiveness is
      documented in Microsoft Learn under ProcessStartInfo.RedirectStandardInput.
      The stdin pipe (next construct) and stdout/stderr streams (the construct
      after) are covered separately.
    idiom_id: null
    research_finding_id: rf-dart-process-start-to-dotnet-processstartinfo-argumentlist
    nuance: >-
      Argument-list nuance (explicitly addressed, load-bearing): Dart
      Process.start(executable, List<String> arguments) passes arguments as a
      list with NO shell tokenisation — each list element is a single argv
      slot. The .NET counterpart for this exact semantics is ProcessStartInfo.
      ArgumentList (List<string>), NOT the legacy "Arguments" string property
      which space-joins and is vulnerable to quoting bugs. UseShellExecute
      nuance (load-bearing): RedirectStandardInput/Output/Error are mutually
      exclusive with UseShellExecute=true — the .NET docs are explicit that
      redirection requires the executable be spawned directly, not via
      ShellExecute. The Dart "runInShell" flag is a DIFFERENT concept (Windows
      cmd.exe wrapping for batch / PATHEXT); for .exe spawning with
      redirection it is intentionally NOT translated. CreateNoWindow nuance:
      Dart Process.start hides the console by default on Windows when not
      attached to a tty; the .NET counterpart is "CreateNoWindow = true" —
      preserved so test/dogfood runs don't flash a console window. Async vs
      sync nuance: Process.Start itself is synchronous in .NET (the await in
      Dart's Process.start is a microtask boundary, not a real I/O wait); the
      faithful translation does NOT need to "await" Process.Start — the awaits
      that matter are on stdout/stderr line iteration and on
      WaitForExitAsync().
  - construct_key: dart.process_stdin.writeln_then_close_with_joined_command_list
    source_form: >-
      "final commands = [ for (final f in glpFiles) f, 'fplay$playNumber.',
      ':quit', ].join('\\n'); process.stdin.writeln(commands); await process.
      stdin.close();" — feeds load commands + play goal + quit through the
      subprocess stdin.
    target_decision: >-
      Build the joined command string the same way: "var commands =
      string.Join(\"\\n\", GlpFiles.Append($\"fplay{playNumber}.\").Append(\":quit\"));"
      (or an explicit StringBuilder loop for review parity with the Dart
      collection-for). Pipe it through StandardInput: "await process.
      StandardInput.WriteLineAsync(commands).ConfigureAwait(false); process.
      StandardInput.Close();". The Dart "writeln" suffixes a newline, matching
      C# WriteLineAsync; the join('\\n') is preserved verbatim so the REPL on
      the other side sees an identical byte stream. Close() on the .NET
      StandardInput sends EOF to the subprocess (equivalent to Dart's
      process.stdin.close()).
    idiom_id: null
    research_finding_id: rf-dart-stdin-writeln-close-to-dotnet-standardinput-writeline-close
    nuance: >-
      EOF-signalling nuance (explicitly addressed, load-bearing): a subprocess
      that reads from stdin in a "read until EOF" loop will block forever if
      stdin is never closed; both Dart process.stdin.close() and .NET
      Process.StandardInput.Close() send the OS-level EOF — the SPEC MUST
      preserve the close() call. Sync-vs-async-write nuance: Dart's writeln
      returns void (the buffered write is fire-and-forget into the OS pipe);
      .NET StandardInput is a StreamWriter that exposes both WriteLine
      (sync) and WriteLineAsync (async) — the async variant is the
      Microsoft Framework Guidelines recommendation for libraries since it
      cooperates with the I/O completion port and does not block the await
      chain on a full pipe buffer. Line-ending nuance: Dart writeln uses "\\n"
      (LF only) by default; .NET WriteLineAsync uses Environment.NewLine which
      on Windows is "\\r\\n" — this is a behaviour change visible to the REPL
      parser. The SPEC choice is to call WriteAsync(commands + "\\n") (NOT
      WriteLineAsync) to preserve the exact LF-only byte stream the Dart side
      emits — a load-bearing fidelity decision because the REPL's line parser
      tokenises on "\\n".
  - construct_key: dart.stream_listen.utf8_decoder_linesplitter_async_per_line_callback
    source_form: >-
      "process.stdout.transform(utf8.decoder).transform(const LineSplitter()).
      listen(_parseLine);" and "process.stderr.transform(utf8.decoder).
      transform(const LineSplitter()).listen((line) { onError?.call('REPL
      stderr: $line'); });" — async iteration of subprocess stdout/stderr as
      decoded lines, each line dispatched through a callback.
    target_decision: >-
      The Dart pipeline "byte stream → utf8.decoder → LineSplitter → listen"
      maps to a .NET async read loop on Process.StandardOutput and
      Process.StandardError (both are StreamReader instances over the redirected
      pipe). The faithful translation is "while ((var line = await process.
      StandardOutput.ReadLineAsync().ConfigureAwait(false)) is not null)
      ParseLine(line);" run on a fire-and-forget Task; analogously for
      StandardError dispatching to OnError?.Invoke($\"REPL stderr: {line}\").
      Three architecturally-equivalent forms exist and the SPEC picks ONE for
      review parity: (1) "ReadLineAsync loop on a Task.Run / fire-and-forget"
      — preferred baseline because it matches Dart's "listen" subscription
      one-line-at-a-time semantics; (2) "OutputDataReceived / ErrorDataReceived
      event + BeginOutputReadLine()" — the legacy .NET subprocess-line API,
      callback-style, eligible but its event raises on a ThreadPool thread and
      a re-entrant cancel/race surface is observable on shutdown; (3)
      "IAsyncEnumerable<string> from StandardOutput.ReadAllLinesAsync()" —
      .NET 7+ extension; cleanest modern shape but requires .NET 7+. The
      baseline SPEC choice is (1) — ReadLineAsync loop on a non-awaited Task
      — because it preserves Dart's stream-listen semantics (subscriber runs
      per-line as the line is decoded) and works on every supported .NET
      target. UTF-8 decoding is the default for ReadLineAsync because the
      Process class uses StandardOutputEncoding (left at Encoding.UTF8 in the
      ProcessStartInfo construct so byte-equivalence with Dart's utf8.decoder
      is preserved). LineSplitter is implicit in ReadLineAsync (it splits on
      \\n and \\r\\n and \\r, identical to Dart LineSplitter).
    idiom_id: null
    research_finding_id: rf-dart-stream-utf8-linesplitter-to-dotnet-streamreader-readlineasync
    nuance: >-
      Async-line-iteration nuance (EXPLICITLY ADDRESSED, US2 AS4 load-bearing):
      Dart Stream<List<int>> .transform(utf8.decoder).transform(const
      LineSplitter()).listen(cb) decomposes into three steps — (a) decode UTF-8
      bytes to a String stream, (b) split on line endings, (c) subscribe with
      a per-line callback. .NET has three equivalent surfaces and the SPEC
      records the trade-off: (1) StreamReader.ReadLineAsync in a while-loop on
      a fire-and-forget Task — matches the per-line subscription model and is
      the baseline choice; (2) Process.OutputDataReceived / BeginOutputReadLine
      — event-driven, raises on ThreadPool, predates IAsyncEnumerable and is
      callback-style same as Dart but with a re-entrant cancellation surface
      that needs care on shutdown; (3) IAsyncEnumerable<string> via
      StandardOutput.ReadAllLinesAsync() (.NET 7+) — the modern equivalent of
      Dart's "await for (var line in stream)" pattern, cleanest for new code
      but adds a framework-version constraint. The baseline preserves the
      Dart "listen"-style subscription verbatim. UTF-8-encoding nuance
      (explicitly addressed): Dart utf8.decoder is fixed-encoding UTF-8;
      .NET's ProcessStartInfo.StandardOutputEncoding/StandardErrorEncoding
      defaults vary (legacy code pages on some Windows locales), so the SPEC
      MUST set them to System.Text.Encoding.UTF8 explicitly to match Dart's
      byte-decoding. Backpressure nuance: Dart's listen() respects the source
      Stream's pause/resume; .NET's StreamReader.ReadLineAsync does not — it
      reads as fast as the subprocess writes. For this file's workload
      (interactive REPL emitting short tagged lines) there is no
      backpressure regime; the spec records the absence as a deliberate
      non-assertion.
  - construct_key: dart.exit_code.await_future_int_terminal_handshake
    source_form: >-
      "final exitCode = await process.exitCode;" followed by "_process =
      null; onLog?.call('REPL: exited with code $exitCode'); onDone?.call(exitCode);"
    target_decision: >-
      Emit "await process.WaitForExitAsync().ConfigureAwait(false); var exitCode
      = process.ExitCode;" — Dart's "process.exitCode" is a Future<int> that
      completes when the OS process terminates; the .NET counterpart is
      Process.WaitForExitAsync (.NET 5+) returning Task, followed by reading
      Process.ExitCode (a property, not a Future). The Dart-int → C#-long
      width policy applies for the captured local; ExitCode is Int32 in .NET
      and the assignment "long exitCode = process.ExitCode;" is a widening
      conversion. The terminal handshake (_process = null; OnLog?.Invoke; OnDone?.
      Invoke(exitCode);) is preserved verbatim — the field reset MUST happen
      BEFORE OnDone fires so a callback that calls IsRunning observes the
      runner as not-running, matching the Dart ordering.
    idiom_id: null
    research_finding_id: rf-dart-process-exitcode-future-to-dotnet-waitforexitasync
    nuance: >-
      Async-wait nuance (explicitly addressed, load-bearing): Dart's
      "process.exitCode" is a Future<int> — awaiting it suspends the calling
      async method until the OS process exits; .NET's pre-.NET-5 equivalent
      was a blocking Process.WaitForExit() or a tap-on-Exited-event dance,
      both with sharp edges. .NET 5+ added Process.WaitForExitAsync(), which
      is the faithful counterpart — it cooperates with the async state
      machine, supports cancellation, and reports completion exactly when the
      OS reports termination. Two-step nuance: the .NET API splits "wait" and
      "read ExitCode" into two calls, whereas Dart fuses them in
      Future<int>.exitCode; preserved as a "two-line C# / one-line Dart" shape
      with no semantic difference. Field-reset ordering nuance: _process =
      null MUST precede OnDone callback dispatch so re-entrant checks of
      IsRunning from inside OnDone observe the runner as idle, matching the
      Dart observable ordering.
  - construct_key: dart.try_on_processexception_catch_typed_exception_filter
    source_form: >-
      "try { ... } on ProcessException catch (e) { _process = null;
      onError?.call('REPL: ProcessException starting [$executable
      ${arguments.join(' ')}] in $runtimeDir: ${e.message} (errorCode=${e.errorCode})'); }
      catch (e) { _process = null; onError?.call('REPL: failed to start [...]: $e'); }"
    target_decision: >-
      Emit a C# try/catch with TWO catch clauses preserving the Dart "on
      ProcessException" filter as a specific catch on the .NET counterpart of
      Dart's ProcessException — the closest .NET type is System.ComponentModel.
      Win32Exception (thrown by Process.Start on spawn failure, carrying a
      native Win32 error code in NativeErrorCode). On Unix, Process.Start
      throws System.IO.IOException with the OS errno wrapped; for parity with
      Dart's "on ProcessException" semantic (any subprocess-spawn failure with
      a numeric error code), the SPEC catches Win32Exception first (Windows
      path) then falls through to a general Exception catch (Unix /
      everything else): "catch (System.ComponentModel.Win32Exception e) {
      _process = null; OnError?.Invoke($\"REPL: Win32Exception starting
      [{executable} {string.Join(' ', arguments)}] in {runtimeDir}: {e.Message}
      (errorCode={e.NativeErrorCode})\"); } catch (Exception e) { _process =
      null; OnError?.Invoke($\"REPL: failed to start [{executable} {string.Join(' ',
      arguments)}] in {runtimeDir}: {e}\"); }". String interpolation preserves
      the bracket/space/colon punctuation byte-identically for log parity.
    idiom_id: null
    research_finding_id: rf-dart-on-processexception-to-dotnet-win32exception
    nuance: >-
      Typed-catch nuance (explicitly addressed, load-bearing): Dart "on T
      catch (e)" is a typed exception filter — only catches T or subclasses;
      C# "catch (T e)" has identical semantics. The DartProcessException-to-
      .NETWin32Exception mapping is the load-bearing decision — both carry a
      numeric error code (Dart ProcessException.errorCode / .NET Win32Exception.
      NativeErrorCode) and both are thrown specifically on subprocess spawn
      failure (executable not found, permission denied, invalid working dir).
      The general catch-Exception fallthrough mirrors Dart's bare "catch (e)"
      which catches Object — narrowed to Exception in C# (C# does not catch
      "object" — only Exception subtypes can be thrown). Field-reset nuance:
      _process = null MUST be set in BOTH catch clauses (matching Dart's
      duplicated assignment) before OnError fires, so any re-entrant IsRunning
      probe from inside OnError observes the runner as idle. Verbatim-text
      nuance: the bracket/space/colon punctuation of the error message MUST
      be preserved byte-identically because tests/log scrapers may match on
      the exact format.
  - construct_key: dart.process_runsync.synchronous_external_with_stdout_capture
    source_form: >-
      "final result = Process.runSync('where', ['dart.exe']); if (result.exitCode
      == 0) { final lines = (result.stdout as String).split(RegExp(r'\\r?\\n')).
      where((l) => l.trim().isNotEmpty).toList(); if (lines.isNotEmpty)
      return lines.first.trim(); }" — used inside _findDart to resolve dart.exe
      via Windows "where".
    target_decision: >-
      Emit a synchronous .NET Process invocation with stdout capture: "var psi
      = new ProcessStartInfo { FileName = \"where\", ArgumentList = { \"dart.
      exe\" }, RedirectStandardOutput = true, UseShellExecute = false,
      CreateNoWindow = true }; using var p = Process.Start(psi)!; string
      stdout = p.StandardOutput.ReadToEnd(); p.WaitForExit(); if (p.ExitCode ==
      0) { var lines = stdout.Split(new[] {\"\\r\\n\", \"\\n\"},
      StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).Where(l =>
      l.Length > 0).ToList(); if (lines.Count > 0) return lines[0]; }". Key
      decisions: (a) using-statement around Process to ensure the IDisposable
      handle is released (Dart Process.runSync auto-closes — no equivalent of
      using in Dart, but .NET requires explicit Dispose for handle hygiene);
      (b) ReadToEnd BEFORE WaitForExit to avoid the classic "child-blocked-on-
      full-stdout-pipe / parent-blocked-on-WaitForExit" deadlock documented in
      Microsoft Learn; (c) RegExp(r'\\r?\\n') on the Dart side maps to a
      multi-separator string.Split for review clarity (regex is acceptable but
      the explicit separators are faster and clearer).
    idiom_id: null
    research_finding_id: rf-dart-process-runsync-to-dotnet-process-start-readtoend-waitforexit
    nuance: >-
      Sync-subprocess nuance (explicitly addressed, load-bearing): Dart's
      Process.runSync is genuinely synchronous (blocks the calling thread
      until the child terminates and returns stdout/stderr as captured
      strings); the .NET equivalent is NOT a single API — it is the four-call
      dance (ProcessStartInfo → Start → ReadToEnd → WaitForExit). Deadlock-
      avoidance nuance: Microsoft Learn's Process.WaitForExit docs are
      explicit: "if the redirected stream has not been written to / read
      from, WaitForExit can hang" — the SPEC MUST read stdout to end BEFORE
      WaitForExit (or before reading ExitCode), matching the documented
      pattern; this is invisible in the Dart side because runSync handles it
      internally. IDisposable nuance: .NET Process implements IDisposable and
      "using" is the documented hygiene; Dart has no equivalent and relies
      on GC; the spec adds "using" because failing to do so leaks OS process
      handles on the Win32 side. The empty-catch-all "catch (_)" in the Dart
      source (which falls through to the candidate-paths search) preserves
      its swallow semantics — in C# "catch { /* fall through */ }" or "catch
      (Exception) { /* fall through */ }" both work; the SPEC uses the
      explicit form for clarity and to engage the C# code analyser.
conversion_units:
  - "namespace declaration mirroring lib/multiagent/ per the workspace's pair-specific namespace convention"
  - "class PlayOutput (reference type)"
  - "  property: string AgentId { get; }"
  - "  property: string Kind { get; }"
  - "  property: string Content { get; }"
  - "  ctor: PlayOutput(string agentId, string kind, string content)"
  - "class ReplPlayRunner (reference type)"
  - "  property: string RepoRoot { get; }"
  - "  property: IReadOnlyList<string> GlpFiles { get; }"
  - "  static readonly IReadOnlyList<string> CssgFiles, BondsFiles, BondsPlay12Files, CssnFiles, CssnVillageFiles"
  - "  static readonly Regex TaggedRegex (precompiled, verbatim pattern, RegexOptions.Compiled)"
  - "  private Process? _process (System.Diagnostics.Process)"
  - "  public Action<PlayOutput>? OnOutput"
  - "  public Action<string>? OnLog"
  - "  public Action<string>? OnError"
  - "  public Action<long>? OnDone"
  - "  ctor: ReplPlayRunner(string repoRoot, IReadOnlyList<string>? glpFiles = null) — null-coalesces glpFiles to CssgFiles"
  - "  property: bool IsRunning => _process is not null (expression-bodied)"
  - "  async Task RunAsync(long playNumber) — main entry: existence checks → spawn → pipe stdin → async-line-iterate stdout/stderr → await exit → dispatch onDone"
  - "    pre-flight: Directory.Exists(runtimeDir), File.Exists(compiledRepl), File.Exists(replScript)"
  - "    spawn: ProcessStartInfo with FileName, WorkingDirectory, RedirectStandardInput/Output/Error, UseShellExecute=false, CreateNoWindow=true, StandardOutputEncoding=UTF8, StandardErrorEncoding=UTF8, ArgumentList populated from arguments list"
  - "    stdin pipe: StandardInput.WriteAsync(commands + \"\\n\") then StandardInput.Close()"
  - "    stdout async-line-iteration: fire-and-forget Task running while ReadLineAsync loop on Process.StandardOutput, dispatching to ParseLine(line)"
  - "    stderr async-line-iteration: fire-and-forget Task running while ReadLineAsync loop on Process.StandardError, dispatching to OnError?.Invoke($\"REPL stderr: {line}\")"
  - "    exit handshake: await process.WaitForExitAsync(); _process = null; OnLog?.Invoke($\"REPL: exited with code {exitCode}\"); OnDone?.Invoke(exitCode)"
  - "    exception handling: catch (Win32Exception) — preserves Dart on-ProcessException-with-errorCode; catch (Exception) — preserves Dart bare-catch fallthrough; both reset _process and dispatch OnError"
  - "  public void Kill() — _process?.Kill(); _process = null;"
  - "  private void ParseLine(string line) — strip 'GLP> ' prefix, Regex.Match(stripped), if !match.Success → OnLog?.Invoke; else read match.Groups[1..3].Value and dispatch OnOutput?.Invoke(new PlayOutput(...))"
  - "  private string FindDart() — on Windows: where dart.exe via synchronous Process.Start+ReadToEnd+WaitForExit; fall through to candidate-paths File.Exists loop; default 'dart.exe'. On Unix: candidate-paths File.Exists loop reading HOME env-var; default 'dart'."
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-final-field-class-to-csharp-getonly-class — immutable PlayOutput data class

- Deep analysis. `PlayOutput` is a three-field immutable carrier (`final
  String agentId; final String kind; final String content;`) with a single
  positional constructor (`PlayOutput(this.agentId, this.kind, this.content)`).
  No methods, no overrides. It is delivered through a single-subscriber
  callback (`runner.onOutput = (output) { ... }`) to a UI panel or trace
  logger; instances are produced one-per-stdout-line and never compared,
  hashed, or used as dictionary keys in this file.
- Authoritative .NET. Microsoft Learn — properties / auto-implemented
  properties documentation: get-only auto-properties (`{ get; }`) are
  initialised in the constructor and have no setter, exposing a read-only
  surface that matches Dart `final`-field public access. Microsoft Learn —
  records reference: synthesised value equality on records would be a
  behaviour addition relative to the Dart side (Dart `class` instances have
  reference equality by default, not value equality), so a record is rejected
  for review parity.
- Authoritative Dart. dart.dev/language/classes — `final` instance fields are
  write-once and assigned in the constructor; class instances have reference
  identity unless `==` is overridden.
- Conclusion. Reference `class PlayOutput` with three get-only string
  properties initialised by a single positional ctor mirroring Dart parameter
  order. No record (would add value equality not present in the source). No
  struct (would force boxing through the `Action<PlayOutput>?` delegate
  invocation). Authoritative both sides; no escalation.

### rf-dart-void-function-question-to-csharp-action-nullable — single-subscriber nullable callbacks

- Deep analysis. Four sibling fields: `void Function(PlayOutput output)?
  onOutput;`, `void Function(String line)? onLog;`, `void Function(String
  error)? onError;`, `void Function(int exitCode)? onDone;`. Each is assigned
  by external consumers (`runner.onOutput = (...) { ... }`) and invoked
  internally via the null-conditional `onOutput?.call(x)` pattern. The
  assignment model is single-subscriber replacement, NOT additive subscription.
- Authoritative .NET. Microsoft Learn — Action<T> Delegate reference:
  `Action<T>` is the canonical .NET delegate for a one-parameter void
  function. Microsoft Learn — null-conditional operators: `obj?.Member` and
  `obj?.Invoke(args)` perform an atomic null-check + dispatch with no
  re-evaluation. Microsoft Learn — events vs delegates: events add
  add/remove accessors and multicast semantics, designed for additive
  subscription (`+=`) — using events here would change the assignment-
  replaces-replace semantics of the Dart source.
- Authoritative Dart. dart.dev/language/functions — function types as
  first-class values; `void Function(T)?` is a nullable function-type field;
  `f?.call(x)` is the documented null-conditional call shape.
- Conclusion. Four `public Action<T>?` fields with `Invoke` through `?.` at
  every call site. Reject `event`. Authoritative both sides; no escalation.

### rf-dart-process-to-dotnet-system-diagnostics-process — nullable subprocess handle

- Deep analysis. A single private nullable field `Process? _process` is set
  on successful spawn, read in two places (`isRunning` getter and `kill`
  method), and reset to null on completion / error / explicit kill. The
  source treats the field as effectively single-thread-of-control (one play
  at a time per runner instance) — there is no synchronisation primitive.
- Authoritative .NET. Microsoft Learn — System.Diagnostics.Process class:
  represents an OS process, IDisposable, exposes Kill(), WaitForExitAsync(),
  StandardInput/Output/Error redirected streams when started with
  ProcessStartInfo. Microsoft Learn — nullable reference types under enabled
  context: `Process?` is the nullable annotation; `_process is not null` is
  the recommended null-state-analyser-friendly check.
- Authoritative Dart. dart.dev — dart:io Process API: a Process handle is GC-
  managed; `process.kill()` sends SIGTERM (or terminates on Windows) and is
  idempotent.
- Conclusion. Private `Process? _process` field; `IsRunning => _process is
  not null` expression-bodied property; `Kill()` performs `_process?.Kill();
  _process = null;` in two statements. No lock around the field for baseline
  parity (Dart source has none). Authoritative both sides; no escalation.

### rf-dart-static-const-list-to-csharp-static-readonly-readonlylist — static const project-file seeds

- Deep analysis. Five public static const string lists (one with a spread)
  expose default-seed file lists for the runner. The runtime usage is
  read-only — they appear in the constructor default and could be passed by
  consumers verbatim. Dart `static const` means the list is canonicalised,
  deeply immutable, and a compile-time constant.
- Authoritative .NET. Microsoft Learn — const vs static readonly: `const` is
  reserved for compile-time-constant primitive/string fields; `static
  readonly` is the canonical surface for class-level constants of reference
  types initialised in the type initialiser. Microsoft Learn —
  IReadOnlyList<T>: the read-only view of a List<T> that prevents external
  mutation while preserving indexer + Count + foreach.
- Authoritative Dart. dart.dev/language/collections — list literals;
  dart.dev/language/built-in-types — String. dart.dev — `const` constructors
  and collection literals.
- Conclusion. `public static readonly IReadOnlyList<string> Cssg/Bonds/...
  Files = new[] { ... };`. The single spread-only list (`bondsPlay12Files`)
  has its spread materialised explicitly at the SPEC layer because C# `const`
  cannot reference another non-primitive const, and the C# 12 collection-
  expression spread `[..]` requires a non-const target. Authoritative both
  sides; no escalation.

### rf-dart-regexp-to-csharp-regex-precompiled-verbatim — precompiled tagged-output regex

- Deep analysis. `static final _taggedRegex = RegExp(r'^tagged\((\w+),
  (cmd|notify|friend|say|act|event)\((.+)\)\)$');` — a class-level
  precompiled regex that parses lines of the form `tagged(<agentId>, <kind>
  (<content>))`. Three capture groups; consumed by `firstMatch` returning
  `Match?` which is null-tested before reading `.group(n)!`.
- Authoritative .NET. Microsoft Learn — System.Text.RegularExpressions.Regex
  class: an instance is JIT-compiled to IL on first use; `RegexOptions.
  Compiled` makes that explicit and amortises the compile cost across all
  matches. Microsoft Learn — Match.Success / Match.Groups[int].Value: a
  Match instance is never null; Success indicates whether the input matched;
  Groups[n].Value is the captured substring. Microsoft Learn — verbatim
  string literals `@"..."`: disable backslash escapes, preserving the
  regex's `\w`/`\(` tokens byte-identically. Microsoft Learn —
  [GeneratedRegex] source generator (.NET 7+): the modern alternative
  producing equivalent compiled code at build time.
- Authoritative Dart. dart.dev — dart:core RegExp documentation and
  api.dart.dev — RegExp.firstMatch returns `Match?`; raw string `r'...'` is
  the canonical regex-pattern carrier disabling Dart string escapes.
- Conclusion. `private static readonly Regex TaggedRegex = new(@"...",
  RegexOptions.Compiled);` with verbatim pattern preservation; consumed via
  `Match.Success` + `Groups[n].Value`. Source-generated regex recommended
  for future hardening. Authoritative both sides; no escalation.

### rf-dart-named-required-default-to-csharp-nullcoalesced-default — constructor with named-required + default

- Deep analysis. `ReplPlayRunner({required this.repoRoot, this.glpFiles =
  cssgFiles});` — two named parameters, one `required` (no default, callsite
  must supply), one with a Dart compile-time-const default referencing
  `cssgFiles`. `repoRoot` has Dart type `String` (non-nullable); `glpFiles`
  has type `List<String>` (non-nullable, defaulted).
- Authoritative .NET. Microsoft Learn — named and optional arguments: C#
  optional-parameter defaults MUST be compile-time constants (primitives,
  strings, `null`, `default(T)`, or `nameof(...)` — NOT arbitrary array
  references). Microsoft Learn — null-coalescing operator `??`: the standard
  pattern to substitute a runtime default for a `null` argument. Microsoft
  Learn — `required` modifier (C# 11+): applies to class members
  (properties/fields), NOT method/constructor parameters — so it cannot
  enforce the Dart-style "named-only required" contract.
- Authoritative Dart. dart.dev/language/functions — named parameters and
  the `required` keyword; dart.dev — compile-time-const default values for
  optional parameters reference `const` lists by name.
- Conclusion. C# constructor `ReplPlayRunner(string repoRoot,
  IReadOnlyList<string>? glpFiles = null)` with a body that null-coalesces
  `glpFiles ??= CssgFiles` then assigns to the two get-only / `{ get; init;
  }` properties. The "named only" callsite contract is intentionally not
  preserved (C# has no equivalent). Authoritative both sides; no
  escalation.

### rf-dart-future-void-async-to-csharp-task-async — Future<void> async method

- Deep analysis. `Future<void> run(int playNumber) async { ... }` — Dart
  async method declared with the `async` keyword, returning `Future<void>`,
  containing `await` expressions. The method body owns the full subprocess
  lifecycle (spawn, pipe, stream-listen, await-exit, terminal callback).
- Authoritative .NET. Microsoft Learn — async / await pattern (TAP): an
  `async` method returning `Task` (non-generic) is the C# counterpart for
  "asynchronous work that produces no value"; `Task<T>` for asynchronous
  work that produces a value. The `Async` suffix on awaitable methods is
  the Microsoft Framework Design Guideline (`Async` Suffix Convention).
  Microsoft Learn — `ConfigureAwait(false)` in library code: avoids
  recapturing a synchronization context not relevant to library work.
  Microsoft Learn — async-void is reserved for event handlers; never
  return `void` from an async non-event-handler method.
- Authoritative Dart. dart.dev/language/async — `Future<void>` for async
  work without a value; `async` modifier on a function body; `await`
  suspends until a Future completes.
- Conclusion. `public async Task RunAsync(long playNumber)` with
  ConfigureAwait(false) recommended on every await. No CancellationToken
  parameter (Dart source has none — addition would be a behaviour change).
  Authoritative both sides; no escalation.

### rf-dart-fs-existssync-to-dotnet-directory-file-exists — synchronous filesystem checks

- Deep analysis. Three synchronous existence checks (`Directory(runtimeDir).
  existsSync()`, two `File(...).existsSync()` calls) and a candidate-path
  loop with `File(path).existsSync()` in `_findDart`. None of these is on
  a hot path — they run once per spawn attempt.
- Authoritative .NET. Microsoft Learn — System.IO.Directory.Exists(string)
  and System.IO.File.Exists(string): return `false` on non-existence,
  permission denial, or any path-resolution error; do NOT throw. These are
  the documented synchronous existence checks.
- Authoritative Dart. dart.dev — dart:io Directory.existsSync and
  File.existsSync: synchronous variants of the asynchronous Future-returning
  forms; same false-on-error semantics.
- Conclusion. 1:1 mapping; path-separator (forward slash on Windows)
  preserved verbatim. Authoritative both sides; no escalation.

### rf-dart-platform-to-dotnet-runtimeinformation-environment — Platform.isWindows / Platform.environment

- Deep analysis. Two distinct Platform queries: `Platform.isWindows` (a
  boolean predicate driving branch selection between AOT-exe vs `dart run`
  script vs `where dart.exe` PATH lookup) and `Platform.environment['KEY']`
  (a Map<String, String> lookup returning `String?`).
- Authoritative .NET. Microsoft Learn — System.Runtime.InteropServices.
  RuntimeInformation.IsOSPlatform(OSPlatform): the documented OS-detection
  API since .NET Core 3.0; recommended over Environment.OSVersion which is
  legacy and can mis-report under shims. Microsoft Learn —
  System.Environment.GetEnvironmentVariable(string): returns `string?`
  (null when unset); reads a fresh OS snapshot per call.
- Authoritative Dart. dart.dev — dart:io Platform class: `isWindows` /
  `isLinux` / `isMacOS` are static getters; `environment` is a `Map<String,
  String>` snapshot.
- Conclusion. `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` for the
  predicate; `Environment.GetEnvironmentVariable(key) ?? ""` for the
  null-coalesced lookup. Authoritative both sides; no escalation.

### rf-dart-process-start-to-dotnet-processstartinfo-argumentlist — async subprocess spawn

- Deep analysis. `await Process.start(executable, arguments,
  workingDirectory: runtimeDir, runInShell: Platform.isWindows)` — spawns
  the REPL with an argument list (no shell tokenisation), a working
  directory, and a Windows-only `runInShell` flag. The returned `process`
  is held in the `_process` field, fed via stdin, drained via stdout/stderr
  streams, and awaited via `process.exitCode`.
- Authoritative .NET. Microsoft Learn —
  System.Diagnostics.ProcessStartInfo.ArgumentList: a `Collection<string>`
  where each entry is an argv slot, escaped per Windows CreateProcess rules
  — the documented modern equivalent of Dart's `List<String> arguments`.
  Microsoft Learn — ProcessStartInfo.UseShellExecute: explicitly states that
  RedirectStandardInput/Output/Error are MUTUALLY EXCLUSIVE with
  UseShellExecute=true (must be `false` for redirection). Microsoft Learn
  — ProcessStartInfo.CreateNoWindow: hides the console window on Windows.
  Microsoft Learn — ProcessStartInfo.StandardOutputEncoding /
  StandardErrorEncoding: defaults vary by .NET version and console code
  page; must be set explicitly to UTF8 for cross-platform byte-equivalence
  with Dart's `utf8.decoder`.
- Authoritative Dart. dart.dev — dart:io Process.start documentation:
  argument list semantics (no shell quoting), workingDirectory parameter,
  runInShell parameter (Windows cmd.exe wrap for batch/PATHEXT).
- Conclusion. ProcessStartInfo with ArgumentList, WorkingDirectory,
  RedirectStandardInput/Output/Error=true, UseShellExecute=false,
  CreateNoWindow=true, StandardOutputEncoding=Encoding.UTF8,
  StandardErrorEncoding=Encoding.UTF8. `Process.Start(psi)!` returns a
  non-null Process when UseShellExecute=false. The Dart `runInShell` flag
  is not translated (different semantic — for redirection we must use
  UseShellExecute=false). Authoritative both sides; no escalation.

### rf-dart-stdin-writeln-close-to-dotnet-standardinput-writeline-close — subprocess stdin pipe

- Deep analysis. `process.stdin.writeln(commands); await process.stdin.
  close();` — feeds a newline-joined command block to the subprocess and
  signals EOF.
- Authoritative .NET. Microsoft Learn — Process.StandardInput: a
  StreamWriter over the redirected pipe (requires RedirectStandardInput=
  true and UseShellExecute=false). Microsoft Learn — StreamWriter.
  WriteLineAsync / WriteAsync: async writes that cooperate with the I/O
  completion port. Microsoft Learn — StreamWriter.Close: flushes and closes
  the stream, sending EOF to the subprocess.
- Authoritative Dart. dart.dev — dart:io IOSink (the type of process.stdin):
  `writeln` writes a string + `\n`; `close()` flushes and closes the sink.
- Conclusion. `await process.StandardInput.WriteAsync(commands + "\n").
  ConfigureAwait(false); process.StandardInput.Close();` — explicitly use
  `WriteAsync` with `"\n"` (NOT `WriteLineAsync` which on Windows uses
  Environment.NewLine = "\r\n", changing the byte stream the REPL parser
  tokenises on). Authoritative both sides; no escalation. The LF-only
  decision is load-bearing.

### rf-dart-stream-utf8-linesplitter-to-dotnet-streamreader-readlineasync — async-line-iteration of subprocess stdout/stderr

- Deep analysis. `process.stdout.transform(utf8.decoder).transform(const
  LineSplitter()).listen(_parseLine);` and analogously for stderr — a
  three-stage stream pipeline (decode bytes → split lines → subscribe with
  callback) that delivers each decoded line through a per-line callback as
  it arrives.
- Authoritative .NET. Microsoft Learn — Process.StandardOutput / .
  StandardError: each is a `StreamReader` over the redirected pipe.
  Microsoft Learn — StreamReader.ReadLineAsync(): returns `Task<string?>`
  (`null` at end-of-stream); reads the next line, splitting on `\n`,
  `\r\n`, or `\r` (matching Dart LineSplitter). Microsoft Learn —
  Process.OutputDataReceived / BeginOutputReadLine: the legacy event-driven
  alternative, callback-style, raises on a ThreadPool thread. Microsoft
  Learn — `IAsyncEnumerable<T>` and `await foreach`: the modern .NET 7+
  alternative with `ReadAllLinesAsync()` returning an asynchronous
  enumerable — closest to Dart's `await for (var line in stream)` pattern.
- Authoritative Dart. dart.dev — dart:convert utf8.decoder and LineSplitter:
  documented stream transformers for byte → string → line decomposition.
  dart.dev — Stream.listen: subscribes a callback to a stream, invoked
  per-event.
- Conclusion. Baseline: fire-and-forget `Task.Run` (or simply a
  non-awaited `async` lambda) running a `while ((line = await
  reader.ReadLineAsync().ConfigureAwait(false)) is not null) ParseLine(line);`
  loop on each of StandardOutput and StandardError. This matches Dart's
  per-line subscription semantics most faithfully and works on every
  supported .NET target. Three equivalent alternatives recorded (event-
  driven legacy / IAsyncEnumerable .NET 7+ / explicit await loop) — the
  baseline picks the explicit await loop for review parity. The
  StandardOutputEncoding/StandardErrorEncoding MUST be set to
  Encoding.UTF8 in ProcessStartInfo so the decoded bytes match Dart's
  fixed-UTF-8 decoder. Authoritative both sides; no escalation. This is
  the load-bearing async-line-iteration nuance called out in the brief.

### rf-dart-process-exitcode-future-to-dotnet-waitforexitasync — async wait for subprocess exit

- Deep analysis. `final exitCode = await process.exitCode;` — awaits the
  OS-process termination Future and reads the exit code in one expression.
  Followed by a terminal handshake that resets `_process = null` and
  dispatches onLog/onDone callbacks.
- Authoritative .NET. Microsoft Learn — Process.WaitForExitAsync(): added
  in .NET 5; returns `Task` that completes when the OS process exits;
  cooperates with the async state machine; supports a CancellationToken
  overload. Microsoft Learn — Process.ExitCode property: readable AFTER
  the process has exited; throws InvalidOperationException if read before
  exit.
- Authoritative Dart. dart.dev — dart:io Process.exitCode: a Future<int>
  that completes with the OS exit code when the process terminates.
- Conclusion. `await process.WaitForExitAsync().ConfigureAwait(false); long
  exitCode = process.ExitCode;` — two-statement form. Terminal handshake
  ordering (`_process = null` before OnDone) preserved verbatim.
  Authoritative both sides; no escalation.

### rf-dart-on-processexception-to-dotnet-win32exception — typed catch on subprocess-spawn failure

- Deep analysis. `try { ... } on ProcessException catch (e) { ... } catch
  (e) { ... }` — a typed catch on the Dart subprocess-spawn-failure
  exception type (carrying `e.message` and `e.errorCode`), with a bare
  fallback catch. Both branches reset `_process = null` and dispatch
  OnError with a formatted message.
- Authoritative .NET. Microsoft Learn — System.ComponentModel.
  Win32Exception: thrown by Process.Start on Windows when the underlying
  CreateProcess call fails; carries `NativeErrorCode` (the Win32 error
  code) and `Message`. Microsoft Learn — Process.Start exceptions: on
  Unix, IOException wraps the errno. Microsoft Learn — typed catch
  clauses: `catch (T e) { ... }` matches T or subclasses; `catch
  (Exception e)` matches all CLR exceptions.
- Authoritative Dart. dart.dev — dart:io ProcessException: thrown by
  Process.start on spawn failure with errorCode and message; not thrown
  by stream-read errors which surface through the stream's error channel.
- Conclusion. Two C# catch clauses: `catch
  (System.ComponentModel.Win32Exception e) { ... }` (Dart-ProcessException-
  equivalent; reads `e.Message` and `e.NativeErrorCode`) and `catch
  (Exception e) { ... }` (Dart bare-catch fallback). Both reset
  `_process = null` and dispatch OnError with byte-identical bracket/colon
  punctuation. Authoritative both sides; no escalation.

### rf-dart-process-runsync-to-dotnet-process-start-readtoend-waitforexit — synchronous subprocess invocation

- Deep analysis. `Process.runSync('where', ['dart.exe'])` — a blocking
  invocation that returns a ProcessResult with `exitCode` (int) and
  `stdout` (Object, cast to String). Used inside `_findDart` only on
  Windows to resolve `dart.exe` via the PATH `where` command.
- Authoritative .NET. Microsoft Learn — Process.WaitForExit: documents the
  "fully read stdout/stderr BEFORE WaitForExit" requirement to avoid a
  deadlock when the child blocks on a full output pipe and the parent
  blocks on WaitForExit. Microsoft Learn — StreamReader.ReadToEnd():
  synchronously reads the full stream to EOF. Microsoft Learn —
  Process.Dispose (via using): the documented hygiene to release the OS
  process handle.
- Authoritative Dart. dart.dev — dart:io Process.runSync: blocks the
  calling thread until the subprocess terminates and returns captured
  stdout/stderr as String (default UTF-8 decode).
- Conclusion. `using var p = Process.Start(psi)!; string stdout =
  p.StandardOutput.ReadToEnd(); p.WaitForExit(); if (p.ExitCode == 0)
  { ... }` — read-to-end BEFORE WaitForExit; using-statement for handle
  hygiene; string.Split with explicit newline separators (or a Regex
  for parity with the Dart `RegExp(r'\r?\n')`). The Dart empty-catch
  `catch (_) { /* fall through */ }` swallowing exceptions to fall
  through to the candidate-paths search is preserved as
  `catch (Exception) { /* fall through */ }` for clarity. Authoritative
  both sides; no escalation.

## Notes

- Async-line-iteration nuance (load-bearing, explicitly addressed): see the
  `rf-dart-stream-utf8-linesplitter-to-dotnet-streamreader-readlineasync`
  construct above. Three architecturally-equivalent .NET surfaces exist
  (ReadLineAsync loop on a Task / OutputDataReceived event /
  IAsyncEnumerable in .NET 7+); the baseline picks the explicit ReadLineAsync
  loop for review parity and broad target-framework support, with
  StandardOutput/StandardErrorEncoding pinned to UTF8 to match Dart's
  fixed-encoding `utf8.decoder`.

- Sync-vs-async file existence: this file uses the `existsSync` variants
  exclusively (one-off pre-flight checks before spawn), so the asynchronous
  Dart variants (`exists()` returning Future<bool>) and the .NET async
  Directory/FileInfo variants are not exercised here. Recorded as a
  deliberate non-assertion.

- The Dart `runInShell: Platform.isWindows` argument to Process.start is
  semantically different from .NET's `UseShellExecute`. The Dart flag
  wraps the spawn in cmd.exe for batch/PATHEXT resolution; the .NET flag
  controls whether ShellExecute or CreateProcess is used and is mutually
  exclusive with stream redirection. The SPEC maps the Dart runInShell
  flag to "no translation" — the .NET equivalent of the observable
  behaviour (spawning a .exe with redirected streams) is
  UseShellExecute=false unconditionally.

- The Dart source's line-ending byte stream into the subprocess MUST be
  LF-only (matching `writeln` on a LF-newline default IOSink). The C# spec
  uses `WriteAsync(commands + "\n")` rather than `WriteLineAsync` so the
  Windows-default `\r\n` from WriteLineAsync does not change the bytes
  reaching the subprocess REPL parser. This is load-bearing for parser
  fidelity.

- Doc-comment preservation. Every triple-slash Dart doc-comment on
  PlayOutput / ReplPlayRunner / public callback fields / project-file
  constants / run() / kill() / `_parseLine` / `_findDart` is preserved
  verbatim as XML-doc comments on the corresponding .NET type / member —
  provenance is review-load-bearing.

- Trivial / non-construct elements: file-level doc-comment (`/// ReplPlayRunner
  — runs simulated dGLP plays via REPL subprocess.`) and the `library;`
  directive map mechanically (the directive has no .NET counterpart; the
  doc-comment becomes file-leading or namespace-level XML-doc). The
  `import 'dart:async';` / `import 'dart:convert';` / `import 'dart:io';`
  directives map to .NET `using System.Threading.Tasks;` / `using
  System.Text;` / `using System.IO;` / `using System.Diagnostics;` /
  `using System.Runtime.InteropServices;` / `using System.Text.
  RegularExpressions;` — trivial directive translation, no research basis
  needed.

- Zero escalations. Every non-trivial construct in this file is resolved
  from authoritative Dart documentation (dart.dev / api.dart.dev) and
  .NET documentation (learn.microsoft.com); no undecidable construct, no
  idiom/research conflict. The async-line-iteration alternatives are
  recorded as explicit trade-offs with a clear baseline choice, not as an
  escalation.
