/// The two IL op-verifiers — the "#11 obligation" (DECISIONS.md Obligation 2),
/// here built for real over the lightweight IL. They run BEFORE bytecode
/// emission, so a non-faithful clause is rejected at the IL layer rather than
/// surfacing as a runtime fault.
library;

import 'il.dart';

class VerifyResult {
  final bool ok;
  final List<String> violations;
  VerifyResult(this.ok, this.violations);

  static VerifyResult pass() => VerifyResult(true, const []);
  static VerifyResult fail(List<String> v) => VerifyResult(false, v);

  @override
  String toString() => ok
      ? 'PASS'
      : 'FAIL:\n${violations.map((v) => "    - $v").join("\n")}';
}

/// **V1 — phase order.** Every head_unify precedes every guard_test precedes
/// every body_spawn (HEAD < GUARD < BODY). Checked on the linear op stream by
/// requiring the phase index to be non-decreasing.
VerifyResult verifyPhaseOrder(IlClause c) {
  final violations = <String>[];
  int maxSeen = -1;
  String maxName = '(start)';
  for (int i = 0; i < c.ops.length; i++) {
    final op = c.ops[i];
    final p = op.phase.index;
    if (p < maxSeen) {
      violations.add(
          'op #$i $op is in phase "${op.phase.name}" but a later phase '
          '"$maxName" already appeared — HEAD<GUARD<BODY violated');
    } else {
      maxSeen = p;
      maxName = op.phase.name;
    }
  }
  return violations.isEmpty ? VerifyResult.pass() : VerifyResult.fail(violations);
}

/// **V2 — single-writer / SRSW.** Over the IL's variable occurrences:
///  (a) each writer name occurs exactly once,
///  (b) each reader name occurs exactly once,
///  (c) a variable occurs iff its paired variable occurs (no lone writer, no
///      lone reader) — anonymous `_` exempt (never collected).
VerifyResult verifySingleWriterSRSW(IlClause c) {
  final occ = collectClauseVars(c);
  final writers = <String, int>{};
  final readers = <String, int>{};
  for (final o in occ) {
    if (o.isReader) {
      readers[o.name] = (readers[o.name] ?? 0) + 1;
    } else {
      writers[o.name] = (writers[o.name] ?? 0) + 1;
    }
  }

  final violations = <String>[];
  final names = {...writers.keys, ...readers.keys};
  final sorted = names.toList()..sort();
  for (final n in sorted) {
    final w = writers[n] ?? 0;
    final r = readers[n] ?? 0;
    // (a) single writer
    if (w > 1) {
      violations.add('writer "$n" occurs $w times (must be exactly 1)');
    }
    // (b) single reader
    if (r > 1) {
      violations.add('reader "$n?" occurs $r times (must be exactly 1)');
    }
    // (c) paired occurrence
    if (w > 0 && r == 0) {
      violations.add('writer "$n" occurs but its paired reader "$n?" is absent');
    }
    if (r > 0 && w == 0) {
      violations.add('reader "$n?" occurs but its paired writer "$n" is absent');
    }
  }
  return violations.isEmpty ? VerifyResult.pass() : VerifyResult.fail(violations);
}
