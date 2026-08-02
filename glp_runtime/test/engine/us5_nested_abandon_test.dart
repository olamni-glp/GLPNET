/// US5 §1.14 regression pins (feature 062 wave-4): nested-structure HEAD-phase
/// matching (arbitrary-depth READ + WRITE, soft-fail on functor/arity mismatch)
/// and the abandon-operation (anonymous-writer discard: the writer binds and the
/// goal proceeds without suspension).
///
/// These assert the OBSERVABLE behavior through the public GlpEngine API. The
/// private suspension set (si/U) and the WRITE-mode _TentativeStruct skeleton are
/// pinned indirectly and faithfully:
///   - abandon SUCCEEDS and is NOT suspended  => no suspension entry was added;
///   - a correct arbitrary-depth binding       => the nested WRITE skeleton is built;
///   - a nested mismatch FAILS (soft-fail)      => the tentative substitution is discarded.
///
/// Source proposals (verified 2026-07-30, no Dart structural change per T028 ruling):
///   specs/062-wave-4-consolidated-parallel-safe-fillers/proposals/
///     abandon-operation.md §7  and  nested-structure-head-matching.md §7.
import 'dart:io';
import 'package:test/test.dart';
import 'package:glp_runtime/engine/glp_engine.dart';

void main() {
  // Fresh engine per goal (runGoal can leave residual state — mirrors the
  // "need fresh engine for clean state" idiom in glp_engine_test.dart).
  GlpEngine freshWith(List<String> fixtures) {
    final engine = GlpEngine(
        rootSelfGlpPath: File('../programs/self.glp').absolute.path);
    for (final f in fixtures) {
      engine.loadFile(File('../programs/tests/typed/$f').absolute.path);
    }
    return engine;
  }

  group('US5 nested-structure HEAD-phase matching', () {
    test('nested WRITE builds the arbitrary-depth term', () async {
      final r = await freshWith(['struct_demo.glp'])
          .runGoal('make_person(alice, thirty, seattle, P)');
      expect(r.succeeded, isTrue, reason: 'error: ${r.error}');
      // runGoal returns the tentative skeleton (leaves are reader-holes Var@N;
      // the REPL deep-resolves them for display). Assert the doubly-nested
      // skeleton person(_, age(_), city(_)) — the _TentativeStruct build.
      final p = r.bindings['P'].toString();
      expect(p, contains('person('));
      expect(p, contains('age('));
      expect(p, contains('city('));
    });

    test('nested READ extracts through a doubly-nested structure', () async {
      final ra = await freshWith(['struct_demo.glp']).runGoal(
          'get_age(person(alice, age(thirty), city(seattle)), A)');
      expect(ra.succeeded, isTrue, reason: 'error: ${ra.error}');
      expect(ra.bindings['A'].toString(), contains('thirty'));

      final rc = await freshWith(['struct_demo.glp']).runGoal(
          'get_city(person(alice, age(thirty), city(seattle)), C)');
      expect(rc.succeeded, isTrue, reason: 'error: ${rc.error}');
      expect(rc.bindings['C'].toString(), contains('seattle'));
    });

    test('nested functor mismatch soft-fails (tentative subst discarded)',
        () async {
      // age(_) in the clause head does not match weight(_) at the nested
      // position; no clause matches -> the goal fails.
      final r = await freshWith(['struct_demo.glp']).runGoal(
          'get_age(person(alice, weight(eighty), city(seattle)), A)');
      expect(r.failed, isTrue, reason: 'expected soft-fail; status=${r.status}');
    });
  });

  group('US5 abandon-operation (anonymous-writer discard)', () {
    test('abandon stream tail: writer binds, goal succeeds and does NOT suspend',
        () async {
      final r =
          await freshWith(['abandon_stream.glp']).runGoal('first_only([a, b, c], Y)');
      expect(r.succeeded, isTrue, reason: 'error: ${r.error}; status=${r.status}');
      expect(r.suspended, isFalse, reason: 'abandon must raise no suspension');
      // Skeleton first(_): the head element is retained; the abandoned tail is
      // absent from the result (it was captured-and-dropped by the anon writer).
      expect(r.bindings['Y'].toString(), contains('first('));
    });

    test('empty-stream coverage clause succeeds', () async {
      final r =
          await freshWith(['abandon_stream.glp']).runGoal('first_only([], Z)');
      expect(r.succeeded, isTrue, reason: 'error: ${r.error}');
      expect(r.bindings['Z'].toString(), contains('empty'));
    });

    test('negative: _? (anonymous reader) is rejected at load', () {
      final engine = GlpEngine(
          rootSelfGlpPath: File('../programs/self.glp').absolute.path);
      expect(
        () => engine.loadFile(
            File('../programs/tests/typed/abandon_reader_bad.glp').absolute.path),
        throwsA(isA<Exception>()),
      );
    });
  });
}
