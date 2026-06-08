/// GLP REPL - Command Line Interface
///
/// Thin CLI wrapper around GlpEngine.
/// This is the user-facing REPL; all execution logic is in GlpEngine.
library;

import 'dart:io';
import 'package:glp_runtime/engine/glp_engine.dart';
import 'package:glp_runtime/link/primitives/link_kernels.dart';
import 'package:glp_runtime/link/transports/loopback_transport.dart';
import 'package:glp_runtime/link/transports/tcp_transport.dart';
import 'package:glp_runtime/multiagent/boot_loader.dart';
import 'package:glp_runtime/multiagent/isolate_manager.dart';
import 'package:glp_runtime/runtime/scheduler.dart';
import 'package:glp_runtime/runtime/terms.dart' as rt;

/// Resolve the absolute path to programs/self.glp by walking up from
/// Platform.script until a directory named `glp_runtime` is found, then
/// taking its parent (which is the GLP repo root) and joining `programs/self.glp`.
///
/// This handles all known entry-point shapes:
///   - JIT:    glp_runtime/bin/glp_repl.dart       (script's grandparent is repo root)
///   - Kernel: glp_runtime/.dart_tool/repl.dill    (script's grandparent is repo root)
///   - AOT:    glp_runtime/glp_repl.exe            (script's parent is repo root)
///   - Any other location under glp_runtime/       (walks up until found)
///
/// Throws StateError if `glp_runtime` cannot be located in the script's ancestor
/// chain — that indicates a serious deployment defect (executable copied outside
/// the project tree).
String _resolveRootSelfGlpPath() {
  final scriptPath = Platform.script.toFilePath();
  Directory dir = File(scriptPath).parent;
  // Walk up to a stable terminating condition.
  while (dir.path != dir.parent.path) {
    final basename = dir.path.split(Platform.pathSeparator).where((s) => s.isNotEmpty).last;
    if (basename == 'glp_runtime') {
      final repoRoot = dir.parent;
      return File('${repoRoot.path}${Platform.pathSeparator}programs${Platform.pathSeparator}self.glp')
          .absolute.path;
    }
    dir = dir.parent;
  }
  throw StateError(
      'GLP REPL could not locate the enclosing glp_runtime/ directory '
      'starting from Platform.script = $scriptPath. The REPL must run from '
      'within a checkout of the GLP repository (the .dart source, the .dill '
      'snapshot, or the compiled .exe must all reside under glp_runtime/).');
}

/// Composition-root seam (feature 025): invoked once, right after the REPL constructs
/// its [GlpEngine], so an outer host can install host kernels + register transports on
/// the live engine. The link layer lives in the hand-authored link package, which
/// DEPENDS ON the runtime/engine — so the wiring is injected here at the composition
/// root rather than referenced by the engine (the engine never references the link
/// layer; FR-057). The host sets this hook to call `LinkKernels.install(engine.runtime)`
/// + register `TcpTransport`/`LoopbackTransport` into the returned `LinkRuntime.transports`
/// and set `engine.runtime.inboundPump` to the runtime's pump. Null for every other host
/// (tests, the feature-020 oracle), so those runs are byte-for-byte unchanged.
void Function(GlpEngine)? afterEngineCreated;

void main() async {
  final gitCommit = await _getGitCommit();
  final buildTime = '2026-02-01 (GlpEngine refactor)';

  print('╔════════════════════════════════════════╗');
  print('║  GLP REPL - With Type Checking         ║');
  print('╚════════════════════════════════════════╝');
  print('');
  if (gitCommit != null) {
    print('Build: $gitCommit');
  }
  print('Compiled: $buildTime');
  print('Working directory: ${Directory.current.path}');
  print('');
  print('Input: filename.glp to load, or goal to execute');
  print('Commands: :quit, :help, :trace, :debug, :limit, :activate, :boot');
  print('');

  // Resolve programs/self.glp by anchoring on the enclosing glp_runtime/ directory.
  // This is entry-point-agnostic: works for the .dart source under bin/, the kernel
  // snapshot under .dart_tool/, and the AOT-compiled .exe directly under glp_runtime/.
  // The bug it fixes: a literal `../../programs/self.glp` overshoots by one directory
  // when invoked through the AOT exe (which lives one level shallower than the source).
  // See test/aot_self_glp_path_test.dart for the regression coverage.
  final rootSelfGlpPath = _resolveRootSelfGlpPath();
  // feature 025: default REPL host wiring — install the native-Dart link layer onto
  // the live engine. link/ is in the same Dart package as the runtime/engine, so the
  // composition root imports it directly. The setup kernel installs the inbound pump
  // lazily (`rt.inboundPump ??= link.pump`); we only register the kernels + transports.
  afterEngineCreated ??= (engine) {
    final link = LinkKernels.install(engine.runtime);
    link.transports.register(TcpTransport());
    link.transports.register(LoopbackTransport());
  };
  final engine = GlpEngine(rootSelfGlpPath: rootSelfGlpPath);
  afterEngineCreated
      ?.call(engine); // feature 025: outer host installs link kernels + transports
  print('Loaded root self.glp from: $rootSelfGlpPath');
  print('');

  while (true) {
    stdout.write('GLP> ');
    final input = stdin.readLineSync();

    if (input == null) {
      break;
    }

    if (input.trim().isEmpty) {
      continue;
    }

    var trimmed = input.trim();
    if (trimmed.endsWith('.') && !trimmed.endsWith('.glp')) {
      trimmed = trimmed.substring(0, trimmed.length - 1).trim();
    }

    // Handle commands
    if (trimmed == ':quit' || trimmed == ':q') {
      print('Goodbye!');
      break;
    }

    if (trimmed == ':help' || trimmed == ':h') {
      _printHelp();
      continue;
    }

    if (trimmed == ':trace' || trimmed == ':t') {
      engine.debugTrace = !engine.debugTrace;
      print('Trace ${engine.debugTrace ? "enabled" : "disabled"}');
      continue;
    }

    if (trimmed == ':debug' || trimmed == ':d') {
      engine.debugOutput = !engine.debugOutput;
      print('Debug output ${engine.debugOutput ? "enabled" : "disabled"}');
      continue;
    }

    if (trimmed == ':strict' || trimmed == ':s') {
      engine.strictTypes = !engine.strictTypes;
      print('Strict type checking ${engine.strictTypes ? "enabled" : "disabled"}');
      continue;
    }

    if (trimmed == ':clear' || trimmed == ':c') {
      engine.clear();
      print('Cleared loaded programs (root self.glp retained)');
      continue;
    }

    if (trimmed.startsWith(':limit')) {
      final parts = trimmed.split(RegExp(r'\s+'));
      if (parts.length != 2) {
        print('Usage: :limit <number>');
        continue;
      }
      final limit = int.tryParse(parts[1]);
      if (limit == null || limit <= 0) {
        print('Error: limit must be a positive integer');
        continue;
      }
      engine.maxCycles = limit;
      print('Goal reduction limit set to ${engine.maxCycles}');
      continue;
    }

    if (trimmed.startsWith(':activate')) {
      final parts = trimmed.split(RegExp(r'\s+'));
      if (parts.length != 2) {
        print('Usage: :activate <module_name>');
        continue;
      }
      final moduleName = parts[1];
      try {
        engine.activateDynamicModule(moduleName);
        print('Activated module: $moduleName');
      } catch (e) {
        print('Error activating $moduleName: $e');
      }
      continue;
    }

    if (trimmed.startsWith(':boot')) {
      final parts = trimmed.split(RegExp(r'\s+'));
      if (parts.length < 2) {
        print('Usage: :boot <bootfile.glp> [timeoutSec]');
        continue;
      }
      final bootPath = parts[1];
      final timeoutSec = parts.length > 2
          ? (int.tryParse(parts[2]) ?? 10)
          : 10;
      await _runBoot(bootPath, rootSelfGlpPath, timeoutSec);
      continue;
    }

    if (trimmed.startsWith(':bytecode') || trimmed.startsWith(':bc')) {
      if (engine.loadedPrograms.isEmpty) {
        print('No programs loaded');
        continue;
      }
      for (final entry in engine.loadedPrograms.entries) {
        print('\nBytecode for ${entry.key}:');
        print('=' * 60);
        final prog = entry.value;
        for (int i = 0; i < prog.ops.length; i++) {
          print('  ${i.toString().padLeft(4)}: ${prog.ops[i]}');
        }
      }
      continue;
    }

    // Check if input is a project directory to load.
    // Supports: <dir> or <dir> <top_module>
    {
      final parts = trimmed.split(' ');
      final dirCandidate = parts[0];
      bool dirExists = false;
      try {
        dirExists = !dirCandidate.endsWith('.glp') &&
            Directory(dirCandidate).existsSync();
      } catch (e) {
        // Invalid path syntax (e.g. illegal characters on Windows).
        // Fall through and let later branches treat input as a goal.
        dirExists = false;
      }
      if (dirExists) {
        final topModule = parts.length > 1 ? parts[1] : null;
        try {
          engine.loadProject(dirCandidate, topModuleName: topModule);
          print('✓ Loaded project: $dirCandidate');
        } catch (e) {
          print('Error loading project $dirCandidate: $e');
        }
        continue;
      }
    }

    // Check if input is a .glp file to load
    if (trimmed.endsWith('.glp')) {
      String filename;
      if (trimmed.startsWith('load ')) {
        filename = trimmed.substring(5).trim();
      } else {
        // Treat the whole input as the filename. This supports paths with
        // spaces (e.g. "programs/book 2/...") and Windows drive letters.
        filename = trimmed;
      }
      // Strip surrounding quotes if user wrapped the path
      if ((filename.startsWith('"') && filename.endsWith('"')) ||
          (filename.startsWith("'") && filename.endsWith("'"))) {
        filename = filename.substring(1, filename.length - 1);
      }
      if (filename.isNotEmpty) {
        try {
          // Resolve path
          final File sourceFile;
          // Treat as absolute/relative path if it has a drive letter, leading
          // slash/dot, or contains a path separator. Otherwise look under glp/.
          final hasPathish = filename.startsWith('/') ||
              filename.startsWith('../') ||
              filename.startsWith('./') ||
              filename.startsWith('\\') ||
              filename.contains('/') ||
              filename.contains('\\') ||
              (filename.length >= 2 && filename[1] == ':');
          if (hasPathish) {
            sourceFile = File(filename);
          } else {
            sourceFile = File('glp/$filename');
          }

          if (!sourceFile.existsSync()) {
            print('Error loading ${sourceFile.path}: File not found');
            continue;
          }

          engine.loadFile(sourceFile.path);
          print('✓ Loaded: $filename');
        } catch (e) {
          print('Error loading $filename: $e');
        }
        continue;
      }
    }

    // Run goal
    try {
      final result = await engine.runGoal(trimmed);

      // Print bindings
      if (result.bindings.isNotEmpty) {
        for (final entry in result.bindings.entries) {
          final varName = entry.key;
          final value = entry.value;
          if (value != null) {
            print('$varName = ${_formatTerm(value, engine)}');
          } else {
            print('$varName = <unbound>');
          }
        }
      }

      // Print status
      _printStatus(result.status);

      if (result.error != null) {
        print('Error: ${result.error}');
      }
    } catch (e) {
      print('Error: $e');
    }

    print('');
  }
}

void _printStatus(ExecutionStatus status) {
  switch (status) {
    case ExecutionStatus.succeeded:
      print('→ succeeds');
    case ExecutionStatus.failed:
      print('→ failed');
    case ExecutionStatus.suspended:
      print('→ suspended');
  }
}

void _printHelp() {
  print('');
  print('GLP REPL Usage:');
  print('  filename.glp           Load and compile glp/<filename>');
  print('  goal.                  Execute a goal (must end with .)');
  print('');
  print('Commands:');
  print('  :help, :h              Show this help');
  print('  :quit, :q              Exit REPL');
  print('  :clear, :c             Clear loaded programs (keep stdlib)');
  print('  :trace, :t             Toggle trace output (reductions)');
  print('  :debug, :d             Toggle DEBUG output');
  print('  :strict, :s            Toggle strict type checking (default: on)');
  print('  :limit <n>             Set goal reduction limit to <n>');
  print('  :activate <module>     Activate module for dynamic dispatch');
  print('  :boot <file> [secs]    Run multi-isolate play via IsolateManager');
  print('  :bytecode, :bc         Show loaded bytecode');
  print('');
  print('Type Checking:');
  print('  Programs with procedure declarations are type-checked');
  print('  Type errors abort loading by default (use :strict to toggle)');
  print('');
  print('Examples:');
  print('  GLP> merge.glp                        # Load typed program');
  print('  GLP> merge([1,2],[a,b],X).            # Execute goal');
  print('');
}

String _formatTerm(rt.Term? term, [GlpEngine? engine, Set<int>? path]) {
  if (term == null) return '[]';

  path ??= <int>{};

  if (term is rt.ConstTerm) {
    if (term.value == null || term.value == 'nil') return '[]';
    return term.value.toString();
  }

  if (term is rt.StructTerm && term.functor == '.' && term.args.length == 2) {
    final elements = <String>[];
    rt.Term? current = term;

    while (true) {
      if (current is! rt.StructTerm || current.functor != '.') break;

      final head = current.args[0];
      final tail = current.args[1];

      String headStr;
      if (head is rt.VarRef && engine != null) {
        final addr = head.addr;
        if (path.contains(addr)) {
          headStr = '<circular>';
        } else {
          path.add(addr);
          final derefHead = engine.runtime.heap.dereference(head);
          if (derefHead is rt.VarRef) {
            final displayId = derefHead.addr;
            headStr = engine.runtime.heap.isReader(derefHead.addr)
                ? 'X$displayId?'
                : 'X$displayId';
          } else {
            headStr = _formatTerm(derefHead, engine, path);
          }
          path.remove(addr);
        }
      } else {
        headStr = _formatTerm(head, engine, path);
      }

      elements.add(headStr);

      if (tail is rt.VarRef && engine != null) {
        final addr = tail.addr;
        if (path.contains(addr)) {
          final displayId = addr;
          final label = engine.runtime.heap.isReader(tail.addr)
              ? 'X$displayId?'
              : 'X$displayId';
          return '[${elements.join(', ')} | <circular $label>]';
        }
        path.add(addr);
        final derefTail = engine.runtime.heap.dereference(tail);
        if (derefTail is rt.VarRef) {
          path.remove(addr);
          final displayId = derefTail.addr;
          final label = engine.runtime.heap.isReader(derefTail.addr)
              ? 'X$displayId?'
              : 'X$displayId';
          return '[${elements.join(', ')} | $label]';
        }
        current = derefTail;
        path.remove(addr);
        if (current is! rt.StructTerm) break;
      } else if (tail is rt.ConstTerm &&
          (tail.value == 'nil' || tail.value == null)) {
        break;
      } else if (tail is rt.StructTerm && tail.functor == '.') {
        current = tail;
      } else {
        break;
      }
    }

    return '[${elements.join(', ')}]';
  }

  if (term is rt.StructTerm) {
    final currentPath = path;
    final formattedArgs = term.args.map((arg) {
      if (arg is rt.VarRef && engine != null) {
        final addr = arg.addr;
        if (currentPath.contains(addr)) {
          return '<circular>';
        }
        currentPath.add(addr);
        final deref = engine.runtime.heap.dereference(arg);
        String result;
        if (deref is rt.VarRef) {
          final displayId = deref.addr;
          result = engine.runtime.heap.isReader(deref.addr)
              ? 'X$displayId?'
              : 'X$displayId';
        } else {
          result = _formatTerm(deref, engine, currentPath);
        }
        currentPath.remove(addr);
        return result;
      }
      return _formatTerm(arg, engine, currentPath);
    }).join(', ');
    return '${term.functor}($formattedArgs)';
  }

  return term.toString();
}

/// Run a multi-isolate boot file via IsolateManager.
///
/// Auto-detects the project directory: if the boot file is under a
/// `mad_boot/` directory, projectDir = boot file's grandparent;
/// otherwise projectDir = boot file's parent.
Future<void> _runBoot(
    String bootPath, String rootSelfGlpPath, int timeoutSec) async {
  File bootFile;
  try {
    bootFile = File(bootPath);
  } catch (e) {
    print('Error: invalid boot path: $e');
    return;
  }
  if (!bootFile.existsSync()) {
    print('Error: boot file not found: $bootPath');
    return;
  }

  final source = bootFile.readAsStringSync();
  final loader = BootLoader();
  final BootConfig config;
  try {
    config = loader.load(source);
  } catch (e) {
    print('Error parsing boot file: $e');
    return;
  }

  final parent = bootFile.parent;
  final projectDir = parent.uri.pathSegments
              .where((s) => s.isNotEmpty)
              .lastOrNull ==
          'mad_boot'
      ? parent.parent.path
      : parent.path;
  config.projectDir = projectDir;
  config.rootSelfGlpPath = rootSelfGlpPath;

  print('Booting ${config.directives.length} isolates from '
      '${bootFile.path}');
  print('  projectDir: $projectDir');
  print('  agents: ${config.directives.map((d) => d.agentId).join(', ')}');

  final manager = IsolateManager();
  manager.onUIOutput = (agentId, message) {
    print('[$agentId] $message');
  };
  try {
    await manager.boot(config);
    manager.start();
    print('✓ Started — running for ${timeoutSec}s');
    await Future.delayed(Duration(seconds: timeoutSec));
  } catch (e, st) {
    print('Boot failed: $e');
    print(st);
  } finally {
    await manager.shutdown();
    print('✓ Shutdown complete');
  }
}

Future<String?> _getGitCommit() async {
  try {
    final result = await Process.run('git', ['log', '-1', '--format=%h %s']);
    if (result.exitCode == 0) {
      return result.stdout.toString().trim();
    }
  } catch (e) {
    // Git not available or not a git repo
  }
  return null;
}
