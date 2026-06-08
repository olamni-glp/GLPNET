import 'dart:typed_data';

import '../../multiagent/payload_serializer.dart';
import '../../runtime/body_kernels.dart' show BodyKernelResult;
import '../../runtime/runtime.dart';
import '../../runtime/terms.dart';
import '../reliability/frame_codec.dart';
import '../seam/i_link_endpoint.dart';
import '../seam/link_id.dart';
import '../seam/link_options.dart';
import 'link_establish.dart';
import 'link_runtime.dart';
import 'link_terms.dart';

/// The `'_link_request'/5` body kernel (feature 025, T033) — the host body of
/// `request_link(LinkId, ToPeer, ch(In?, Out), Faults?) :- ground(LinkId?), ground(ToPeer?) | '_link_request'(LinkId?, ToPeer?, In, Out?, Faults).`
/// (path-B establishment, FR-002). It is the CONNECTOR half of the handshake: it
/// connects to the peer's rendezvous over the transport (OQ-A3), emits the in-band
/// `request(LinkId, FromPeer)` token as a raw out-of-band frame, then establishes the
/// data link through the shared [LinkEstablish.wireEstablishedLink] core — so the
/// resulting link is indistinguishable from a listen/connect one (R-5).
///
/// Arguments (ground-guarded by the wrapper): `args[0]` LinkId, `args[1]` ToPeer (the
/// peer AgentId; validated ground), `args[2]` In writer, `args[3]` Out? reader,
/// `args[4]` Faults writer. The token's `FromPeer` is a base placeholder
/// (`requester`) — authenticated origin binding is FR-026/T075.
///
/// ASYNC-AWARE (Dart single-isolate adaptation of the C# blocking connect +
/// `GetAwaiter().GetResult()` handshake): the connect-and-ship-token is folded INTO
/// the `Future<ILinkEndpoint> Function()` establish closure handed to
/// [LinkEstablish.wireEstablishedLink], which wires the heap cursors + registry
/// SYNCHRONOUSLY and kicks the connect off without awaiting. A connect/handshake
/// FAILURE surfaces as a transport fault on the establishment `Faults` monitor (the
/// establish-core's onError path; FR-008) rather than as a synchronous Abort, since
/// the isolate cannot block on the rendezvous to observe it before returning.
class LinkRequestKernel {
  LinkRequestKernel._();

  static const String _who = "'_link_request'/5";

  static BodyKernelResult linkRequest(
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
      return LinkEstablish.abort(_who, 'arg 2 (ToPeer) must be a ground peer id');
    }

    return LinkEstablish.wireEstablishedLink(
        rt, link, id, () => _connectAndHandshake(link, id),
        args[2], args[3], args[4], _who);
  }

  /// Connect to the peer's rendezvous (bounded by [LinkOptions.connectTimeout]),
  /// ship the in-band `request(LinkId, FromPeer)` token as a raw out-of-band frame
  /// (messageId 0, NOT via the link sequencer, so the data sequence still starts at
  /// 0; the listener consumes it before the data pump), and adopt the SAME connection
  /// the handshake rode for the data link.
  static Future<ILinkEndpoint> _connectAndHandshake(
      LinkRuntime link, LinkId id) async {
    final transport = link.transports.select(id.scheme);
    final opts = LinkOptions.default_;
    final cancel = Future<void>.delayed(opts.connectTimeout);
    final endpoint =
        await transport.connect(id.scheme, id.endpoint, opts, cancel: cancel);

    final token = LinkTerms.requestToken(id, ConstTerm('requester'));
    final List<int> payload = PayloadSerializer('').serializeAgentMessage(token);
    for (final Uint8List frame in FrameCodec.encode(payload, 0)) {
      await endpoint.sendBytes(frame);
    }
    return endpoint;
  }
}
