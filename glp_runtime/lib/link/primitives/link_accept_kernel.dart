import '../../runtime/body_kernels.dart' show BodyKernelResult;
import '../../runtime/runtime.dart';
import '../../runtime/terms.dart';
import '../seam/i_link_endpoint.dart';
import '../seam/link_id.dart';
import 'link_establish.dart';
import 'link_runtime.dart';
import 'link_terms.dart';

/// The `'_link_accept'/5` body kernel (feature 025, T033) — the host body of
/// `accept_link(LinkId, [request(LinkId2, FromPeer)|_], ch(In?, Out), Faults?) :- ground(LinkId?), LinkId? =?= LinkId2? | '_link_accept'(LinkId?, FromPeer?, In, Out?, Faults).`
/// It is the ACCEPT concern, cleanly separated from the LISTEN concern
/// (`'_link_listen'`): the GLP `=?=` guard has already matched the requested LinkId
/// against one this end serves, so the kernel simply ADOPTS the pending connection
/// that `'_link_listen'` parked for that ground LinkId and establishes the data link
/// through the shared [LinkEstablish.wireEstablishedLink] core — converging on the
/// same T030 registry as listen/connect (FR-002, R-5).
///
/// Arguments: `args[0]` LinkId, `args[1]` FromPeer (matched origin; validated ground),
/// `args[2]` In writer, `args[3]` Out? reader, `args[4]` Faults writer. "Accept before
/// listen" (no pending connection for the LinkId) is a caller bug, surfaced as Abort.
class LinkAcceptKernel {
  LinkAcceptKernel._();

  static const String _who = "'_link_accept'/5";

  static BodyKernelResult linkAccept(
      GlpRuntime rt, List<Object?> args, LinkRuntime link) {
    if (args.length != 5) {
      return LinkEstablish.abort(_who, 'expected 5 arguments, got ${args.length}');
    }

    final heap = rt.heap;
    LinkId id;
    try {
      id = LinkTerms.parseLinkId(LinkTerms.groundResolve(heap, args[0]! as Term));
    } on ArgumentError catch (ex) {
      return LinkEstablish.abort(_who, 'arg 1 (LinkId): ${ex.message}');
    }
    if (heap.dereference(args[1]! as Term) is VarRef) {
      return LinkEstablish.abort(_who, 'arg 2 (FromPeer) must be a ground peer id');
    }

    final ILinkEndpoint? endpoint = link.pending.remove(id);
    if (endpoint == null) {
      return LinkEstablish.abort(
          _who, 'no pending request for $id — request_listener must surface it first');
    }

    // The connection was already accepted + handshook by '_link_listen'; the establish
    // closure resolves it immediately (the Dart-async signature of the shared core).
    return LinkEstablish.wireEstablishedLink(
        rt, link, id, () => Future<ILinkEndpoint>.value(endpoint),
        args[2], args[3], args[4], _who);
  }
}
