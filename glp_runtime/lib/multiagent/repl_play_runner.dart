/// ReplPlayRunner — runs simulated dGLP plays via REPL subprocess.
///
/// Spawns the REPL as a subprocess, pipes load commands and a play goal,
/// parses tagged output lines, and delivers them via callbacks.
///
/// Tagged output format from GLP: tagged(alice, cmd(connect(bob)))
/// Parsed into: agentId="alice", kind="cmd", content="connect(bob)"
///
/// Narrative kinds (play 12): friend, say, act, event
/// e.g. tagged(alice, friend(bob)) → agentId="alice", kind="friend", content="bob"
library;

import 'dart:async';
import 'dart:convert';
import 'dart:io';

/// Parsed output line from a simulated play.
class PlayOutput {
  final String agentId; // e.g. "alice"
  final String kind; // "cmd", "notify", "friend", "say", "act", or "event"
  final String content; // e.g. "connect(bob)"

  PlayOutput(this.agentId, this.kind, this.content);
}

/// Runs simulated dGLP plays via a REPL subprocess.
///
/// Usage:
///   final runner = ReplPlayRunner(repoRoot: '/Users/udi/Grassroots/GLP');
///   runner.onOutput = (output) { /* route to UI panel */ };
///   runner.onLog = (line) { /* trace log */ };
///   runner.onError = (error) { /* display error */ };
///   runner.onDone = (exitCode) { /* play finished */ };
///   await runner.run(1); // runs fplay1
///   runner.kill(); // abort if needed
class ReplPlayRunner {
  final String repoRoot;

  /// Called for each parsed tagged output line.
  void Function(PlayOutput output)? onOutput;

  /// Called for non-tagged REPL output (banner, load messages, etc.).
  void Function(String line)? onLog;

  /// Called for stderr lines and exceptions.
  void Function(String error)? onError;

  /// Called when the REPL process exits.
  void Function(int exitCode)? onDone;

  Process? _process;

  /// Which GLP files to load (relative to glp_runtime/).
  final List<String> glpFiles;

  /// CSSG project (child-safe social graph, plays 1–7).
  /// Uses the modules_v2 project which loads cleanly (typed_book/cssg has
  /// an unresolved IntroChannel/FriendChannel typing issue).
  static const cssgFiles = [
    '../programs/cssg_modules_v2',
  ];

  /// Bonds project (grassroots bonds, plays 1–11).
  /// Uses bonds_v2 which loads cleanly (typed_book/bonds is missing
  /// procedure declarations).
  static const bondsFiles = [
    '../programs/bonds_v2',
  ];

  /// Bonds GLP files for play 12 — same project, fplay12 is part of the boot.
  static const bondsPlay12Files = [
    ...bondsFiles,
  ];

  /// CSSN project (child-safe social networking, plays 1–10).
  /// Uses cssn_modules_v2 which loads cleanly (typed_book/cssn has
  /// undefined Channel and X type references).
  static const cssnFiles = [
    '../programs/cssn_modules_v2',
  ];

  /// CSSN v2 modules project (village scenario, fplay13).
  /// Loaded as a project directory — the REPL treats the path as a project root.
  static const cssnVillageFiles = [
    '../programs/cssn_modules_v2',
  ];

  /// Regex for parsing tagged output lines.
  /// Matches cmd, notify (protocol), and friend, say, act, event (narrative).
  static final _taggedRegex =
      RegExp(r'^tagged\((\w+), (cmd|notify|friend|say|act|event)\((.+)\)\)$');

  ReplPlayRunner({required this.repoRoot, this.glpFiles = cssgFiles});

  bool get isRunning => _process != null;

  /// Run a simulated play (1, 2, or 3).
  Future<void> run(int playNumber) async {
    final runtimeDir = '$repoRoot/glp_runtime';

    onLog?.call('REPL: repoRoot=$repoRoot');
    onLog?.call('REPL: runtimeDir=$runtimeDir');

    if (!Directory(runtimeDir).existsSync()) {
      onError?.call('Directory not found: $runtimeDir');
      return;
    }

    // Prefer AOT-compiled glp_repl.exe (no Dart SDK dependency, faster startup).
    final compiledRepl = Platform.isWindows
        ? '$runtimeDir/bin/glp_repl.exe'
        : '$runtimeDir/bin/glp_repl';
    final replScript = '$runtimeDir/bin/glp_repl.dart';

    String executable;
    List<String> arguments;
    if (File(compiledRepl).existsSync()) {
      executable = compiledRepl;
      arguments = const [];
      onLog?.call('REPL: exe=$compiledRepl (AOT)');
    } else if (File(replScript).existsSync()) {
      executable = _findDart();
      arguments = ['run', 'bin/glp_repl.dart'];
      onLog?.call('REPL: dart=$executable (JIT via "dart run")');
    } else {
      onError?.call(
          'No REPL found: neither $compiledRepl nor $replScript exists');
      return;
    }

    try {
      final process = await Process.start(
        executable,
        arguments,
        workingDirectory: runtimeDir,
        runInShell: Platform.isWindows,
      );
      _process = process;
      onLog?.call('REPL: process started (pid=${process.pid})');

      // Feed load commands + play goal + quit
      final commands = [
        for (final f in glpFiles) f,
        'fplay$playNumber.',
        ':quit',
      ].join('\n');
      process.stdin.writeln(commands);
      await process.stdin.close();

      // Parse stdout
      process.stdout
          .transform(utf8.decoder)
          .transform(const LineSplitter())
          .listen(_parseLine);

      // Log stderr
      process.stderr
          .transform(utf8.decoder)
          .transform(const LineSplitter())
          .listen((line) {
        onError?.call('REPL stderr: $line');
      });

      // Wait for exit
      final exitCode = await process.exitCode;
      _process = null;
      onLog?.call('REPL: exited with code $exitCode');
      onDone?.call(exitCode);
    } on ProcessException catch (e) {
      _process = null;
      onError?.call(
          'REPL: ProcessException starting [$executable ${arguments.join(' ')}] in $runtimeDir: ${e.message} (errorCode=${e.errorCode})');
    } catch (e) {
      _process = null;
      onError?.call(
          'REPL: failed to start [$executable ${arguments.join(' ')}] in $runtimeDir: $e');
    }
  }

  /// Kill the REPL subprocess if running.
  void kill() {
    _process?.kill();
    _process = null;
  }

  /// Parse a single stdout line from the REPL.
  /// The REPL may prefix the first output line with "GLP> ", so strip it.
  void _parseLine(String line) {
    final stripped = line.startsWith('GLP> ') ? line.substring(5) : line;
    final match = _taggedRegex.firstMatch(stripped);
    if (match == null) {
      onLog?.call('REPL: $line');
      return;
    }

    final agentId = match.group(1)!;
    final kind = match.group(2)!;
    final content = match.group(3)!;
    onOutput?.call(PlayOutput(agentId, kind, content));
  }

  /// Find the dart executable. Prefer the one next to the Flutter SDK,
  /// fall back to PATH.
  String _findDart() {
    if (Platform.isWindows) {
      // Resolve dart.exe via PATH using `where`.
      try {
        final result = Process.runSync('where', ['dart.exe']);
        if (result.exitCode == 0) {
          final lines = (result.stdout as String)
              .split(RegExp(r'\r?\n'))
              .where((l) => l.trim().isNotEmpty)
              .toList();
          if (lines.isNotEmpty) return lines.first.trim();
        }
      } catch (_) {/* fall through */}
      // Common Flutter SDK locations on Windows
      final userProfile = Platform.environment['USERPROFILE'] ?? '';
      final winCandidates = [
        if (userProfile.isNotEmpty) '$userProfile\\flutter\\bin\\dart.exe',
        if (userProfile.isNotEmpty)
          '$userProfile\\development\\flutter\\bin\\dart.exe',
        'C:\\flutter\\bin\\dart.exe',
        'C:\\src\\flutter\\bin\\dart.exe',
      ];
      for (final path in winCandidates) {
        if (File(path).existsSync()) return path;
      }
      return 'dart.exe';
    }
    // Check common macOS Flutter/Dart locations
    final candidates = [
      '/usr/local/bin/dart',
      '${Platform.environment['HOME']}/flutter/bin/dart',
      '${Platform.environment['HOME']}/development/flutter/bin/dart',
      '${Platform.environment['HOME']}/.pub-cache/bin/dart',
    ];
    for (final path in candidates) {
      if (File(path).existsSync()) return path;
    }
    // Fall back to PATH (works from terminal, may not from app bundle)
    return 'dart';
  }
}
