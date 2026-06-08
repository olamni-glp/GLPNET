import '../../runtime/body_kernels.dart' show BodyKernelResult;
import '../../runtime/runtime.dart';
import '../../runtime/terms.dart';
import '../seam/link_id.dart';
import 'link_establish.dart';
import 'link_runtime.dart';
import 'link_teardown.dart';
import 'link_terms.dart';

/// The `'_link_close'/2` system-predicate (feature 025, T035) — the host body of the
/// GLP wrappers
/// `link_close(LinkId) :- ground(LinkId?) | '_link_close'(LinkId?, abrupt).` and
/// `link_close(LinkId, Reason) :- ground(LinkId?), ground(Reason?) | '_link_close'(LinkId?, Reason?).`
/// Given a ground, already-established [LinkId] and a ground close reason, it performs
/// an ABRUPT teardown (RST_STREAM-equiv) regardless of stream state: it emits a
/// terminal `closed(LinkId, Reason)` on every monitor stream, ends those streams, tears
/// the transport endpoint down, and runs distributed GC (FR-024). Graceful close is the
/// separate stream-end `[]` path on `Out`; both converge on [LinkTeardown.teardown]
/// (rulings-log "link_close — 9th base primitive").
///
/// Arguments (ground-guarded by the wrapper): `args[0]` LinkId, `args[1]` the close
/// Reason (the atom `abrupt` from `/1`, or a user reason from `/2`; graceful uses
/// `eos`). Closing an unestablished link is a caller bug surfaced as an abort, not
/// tolerated.
class LinkCloseKernel {
  LinkCloseKernel._();

  static const String _who = "'_link_close'/2";

  static BodyKernelResult linkClose(
      GlpRuntime rt, List<Object?> args, LinkRuntime link) {
    if (args.length != 2) {
      return LinkEstablish.abort(_who, 'expected 2 arguments, got ${args.length}');
    }

    final heap = rt.heap;
    LinkId id;
    String reason;
    try {
      id = LinkTerms.parseLinkId(LinkTerms.groundResolve(heap, args[0]! as Term));
      reason = LinkTerms.parseReason(LinkTerms.groundResolve(heap, args[1]! as Term));
    } on ArgumentError catch (ex) {
      return LinkEstablish.abort(_who, ex.message.toString());
    }

    return LinkTeardown.close(rt, link, id, reason);
  }
}
