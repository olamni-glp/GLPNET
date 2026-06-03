// Phase 1 load-triage driver.
// Walks the programs/ tree, attempts loadProject() on each project dir
// (any dir containing self.glp) and loadFile() on standalone .glp files,
// recording success/error to a CSV.
//
// Usage:  dart run bin/triage_loader.dart <programs_root> <out_csv>

import 'dart:io';
import 'package:glp_runtime/engine/glp_engine.dart';

const Set<String> _excludedTop = {'lib', 'archive', 'OLD typed book'};

bool _isExcluded(String relPath) {
  final segs = relPath.replaceAll('\\', '/').split('/');
  if (segs.isEmpty) return false;
  return _excludedTop.contains(segs.first);
}

void main(List<String> args) async {
  final programsRoot = args.isNotEmpty
      ? args[0]
      : r'D:\BSTDEV\glp\GLP\programs';
  final outCsv = args.length > 1
      ? args[1]
      : r'D:\BSTDEV\glp\GLP\glp_runtime\build\triage.csv';

  final rootSelfGlp = '$programsRoot/self.glp';

  final root = Directory(programsRoot);
  if (!root.existsSync()) {
    stderr.writeln('No such dir: $programsRoot');
    exit(2);
  }

  // --- Discover project dirs (any dir containing self.glp) ---
  final projectDirs = <String>{};
  await for (final ent in root.list(recursive: true, followLinks: false)) {
    if (ent is File && ent.uri.pathSegments.last == 'self.glp') {
      final dir = ent.parent.path;
      final rel = dir.substring(programsRoot.length).replaceAll('\\', '/');
      if (rel.isEmpty || rel == '/') continue; // skip root self.glp
      if (_isExcluded(rel.startsWith('/') ? rel.substring(1) : rel)) continue;
      projectDirs.add(dir);
    }
  }

  // --- Discover all .glp files (non-excluded) ---
  final allGlp = <String>[];
  await for (final ent in root.list(recursive: true, followLinks: false)) {
    if (ent is File && ent.path.toLowerCase().endsWith('.glp')) {
      final rel = ent.path
          .substring(programsRoot.length)
          .replaceAll('\\', '/');
      final clean = rel.startsWith('/') ? rel.substring(1) : rel;
      if (_isExcluded(clean)) continue;
      if (clean == 'self.glp') continue;
      allGlp.add(ent.path);
    }
  }

  // Files that live inside a project dir get loaded as part of that project,
  // not individually.
  bool insideProject(String filePath) {
    for (final pd in projectDirs) {
      if (filePath.startsWith(pd)) return true;
    }
    return false;
  }

  final standaloneFiles =
      allGlp.where((p) => !insideProject(p)).toList()..sort();
  final projectList = projectDirs.toList()..sort();

  stdout.writeln('Project dirs: ${projectList.length}');
  stdout.writeln('Standalone files: ${standaloneFiles.length}');

  final out = StringBuffer();
  out.writeln('kind,path,status,error');

  int okProj = 0, failProj = 0, okFile = 0, failFile = 0;

  // --- Triage each project ---
  for (final pd in projectList) {
    final rel = pd.substring(programsRoot.length).replaceAll('\\', '/');
    stdout.write('PROJ ${rel.padRight(60)} ... ');
    try {
      final eng = GlpEngine(rootSelfGlpPath: rootSelfGlp);
      eng.strictTypes = false;
      eng.loadProject(pd);
      okProj++;
      stdout.writeln('OK');
      out.writeln('project,${_csv(rel)},ok,');
    } catch (e) {
      failProj++;
      final msg = e.toString().split('\n').first;
      stdout.writeln('FAIL: $msg');
      out.writeln('project,${_csv(rel)},fail,${_csv(msg)}');
    }
  }

  // --- Triage standalone files ---
  for (final f in standaloneFiles) {
    final rel = f.substring(programsRoot.length).replaceAll('\\', '/');
    stdout.write('FILE ${rel.padRight(60)} ... ');
    try {
      final eng = GlpEngine(rootSelfGlpPath: rootSelfGlp);
      eng.strictTypes = false;
      eng.loadFile(f);
      okFile++;
      stdout.writeln('OK');
      out.writeln('file,${_csv(rel)},ok,');
    } catch (e) {
      failFile++;
      final msg = e.toString().split('\n').first;
      stdout.writeln('FAIL: $msg');
      out.writeln('file,${_csv(rel)},fail,${_csv(msg)}');
    }
  }

  File(outCsv).writeAsStringSync(out.toString());
  stdout.writeln('\n=== SUMMARY ===');
  stdout.writeln('Projects:   $okProj ok, $failProj fail');
  stdout.writeln('Files:      $okFile ok, $failFile fail');
  stdout.writeln('Wrote:      $outCsv');
}

String _csv(String s) {
  if (s.contains(',') || s.contains('"') || s.contains('\n')) {
    return '"${s.replaceAll('"', '""')}"';
  }
  return s;
}
