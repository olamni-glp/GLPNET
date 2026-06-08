import '../../runtime/body_kernels.dart' show BodyKernelResult;
import '../../runtime/runtime.dart';
import '../../runtime/terms.dart';
import '../seam/link_id.dart';
import 'link_egress.dart';
import 'link_runtime.dart';
import 'link_terms.dart';

/// The `'_link_send'/3` body kernel (feature 025, T031) — the host body of the
/// LinkId-keyed sender face
/// `out_relay(Msg, LinkId, ToPeer) :- ground(Msg?), ground(LinkId?), ground(ToPeer?) | '_link_send'(Msg?, LinkId?, ToPeer?).`
/// (contracts/link-primitives.md §3, OQ-3 ruling). It is the LinkId-keyed twin of the
/// channel face `link_send/3` (which conses onto the `Out` stream and is shipped by
/// the `'_link_setup'` egress drainer): instead of riding the stream, it resolves the
/// link by its ground [LinkId] and ships the ground payload directly (FR-010
/// ground-relay). Both faces converge on the one [LinkEgress.shipGround] routine
/// (risk R-5).
///
/// Arguments — all three already certified ground by the GLP wrapper's `ground/1`
/// guards, so a malformed/unbound term here is a caller bug (surfaced as
/// [BodyKernelResult.abort], never tolerated; FR-010):
///   1. `args[0]` Msg — the ground term to relay (a COPY crosses the cut, FR-040).
///   2. `args[1]` LinkId — ground `link_id(Scheme, Endpoint, Nonce)`; selects the link.
///   3. `args[2]` ToPeer — ground peer AgentId; the link is bilateral (FR-005) so it
///      names the single far end (validated ground; routing is by LinkId).
///
/// The link MUST already be established (a prior `'_link_setup'` / request/accept for
/// the same ground LinkId): "send before setup" is a caller bug, surfaced as Abort.
class LinkSendKernel {
  LinkSendKernel._();

  static const String _who = "'_link_send'/3";

  static BodyKernelResult linkSend(
      GlpRuntime rt, List<Object?> args, LinkRuntime link) {
    if (args.length != 3) {
      return _abort('expected 3 arguments, got ${args.length}');
    }

    final heap = rt.heap;

    // --- parse + validate the two ground addresses (LinkId, ToPeer) ---
    LinkId id;
    try {
      id = LinkTerms.parseLinkId(LinkTerms.groundResolve(heap, args[1]! as Term));
    } on ArgumentError catch (ex) {
      return _abort('arg 2 (LinkId): ${ex.message}');
    }

    // ToPeer is ground-guarded by the wrapper; the bilateral link (FR-005) has a
    // single far end, so it is validated-ground for the relay record, not routed on
    // (routing is by LinkId). An unbound ToPeer here is a caller bug.
    if (heap.dereference(args[2]! as Term) is VarRef) {
      return _abort('arg 3 (ToPeer) must be a ground peer id');
    }

    // --- resolve the established link (idempotent registry, FR-007) ---
    final handle = link.links.tryGet(id);
    if (handle == null) {
      return _abort('no established link for $id — setup before send');
    }

    // --- ground-relay ship (FR-010), shared with the Out-stream egress drainer ---
    try {
      LinkEgress.shipGround(heap, handle, args[0]! as Term);
    } on StateError catch (ex) {
      // The ground(Msg?) guard should have excluded this; a non-ground payload
      // reaching the kernel is a caller bug, surfaced rather than partly shipped.
      return _abort('arg 1 (Msg) is not ground: ${ex.message}');
    }

    return BodyKernelResult.success;
  }

  /// Mirrors the C# kernel's own private `Abort` (which prints with the kernel's own
  /// `Who`), so the `[ABORT] '_link_send'/3: …` line is byte-identical.
  static BodyKernelResult _abort(String why) {
    // ignore: avoid_print
    print('[ABORT] $_who: $why');
    return BodyKernelResult.abort;
  }
}
