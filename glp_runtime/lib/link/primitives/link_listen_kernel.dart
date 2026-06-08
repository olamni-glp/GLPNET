import 'dart:typed_data';

import '../../multiagent/payload_serializer.dart';
import '../../runtime/body_kernels.dart' show BodyKernelResult;
import '../../runtime/runtime.dart';
import '../../runtime/terms.dart';
import '../reliability/frame_codec.dart';
import '../reliability/frame_reassembler.dart';
import '../seam/i_link_endpoint.dart';
import '../seam/link_address.dart';
import '../seam/link_id.dart';
import '../seam/link_options.dart';
import '../seam/link_scheme.dart';
import 'link_establish.dart';
import 'link_runtime.dart';
import 'link_terms.dart';

/// The `'_link_listen'/3` body kernel (feature 025, T033) — the host body of the
/// EXPLICIT request producer
/// `request_listener(rendezvous(Scheme, Endpoint), Requests?) :- ground(Scheme?), ground(Endpoint?) | '_link_listen'(Scheme?, Endpoint?, Requests).`
/// (Option A, ratified 2026-06-07). It is the LISTENER concern, cleanly separated from
/// the ACCEPT concern (`'_link_accept'`): it binds a transport rendezvous, reads the
/// inbound connection's in-band first frame as a `request(LinkId, FromPeer)` token
/// (OQ-A3), parks that accepted connection in [LinkRuntime.pending] keyed by the
/// ground LinkId, and surfaces the token on the `Requests` stream — where
/// `accept_link/4` reads it and matches the LinkId with `=?=`.
///
/// Base-MVP: ONE request per rendezvous (the `ILinkTransport.listen` seam returns a
/// single endpoint; a multi-request accept-loop is a transport-leaf concern, Phase 6).
/// Args: `args[0]` Scheme, `args[1]` Endpoint, `args[2]` Requests writer.
///
/// ASYNC-AWARE (Dart single-isolate adaptation of the C# synchronously-blocking
/// listen + read + bind): the shape validation runs synchronously and the kernel
/// returns success immediately; the genuinely-async rendezvous + token read run in the
/// background, and on completion the endpoint is parked and `Requests` is bound ON THE
/// RUNNER ISOLATE (the `.then` continuation runs on the same single isolate as the
/// runner — the same discipline [LinkEstablish.wireEstablishedLink] uses for its
/// deferred connect). A rendezvous/handshake FAILURE leaves `Requests` suspended (no
/// link/monitor exists yet to fault onto); it is surfaced as a diagnostic, never a
/// partial/forged request element.
class LinkListenKernel {
  LinkListenKernel._();

  static const String _who = "'_link_listen'/3";

  static BodyKernelResult linkListen(
      GlpRuntime rt, List<Object?> args, LinkRuntime link) {
    if (args.length != 3) {
      return LinkEstablish.abort(_who, 'expected 3 arguments, got ${args.length}');
    }

    final heap = rt.heap;
    LinkScheme scheme;
    LinkAddress endpointAddr;
    try {
      scheme = LinkTerms.parseScheme(LinkTerms.groundResolve(heap, args[0]! as Term));
      endpointAddr =
          LinkTerms.parseEndpoint(LinkTerms.groundResolve(heap, args[1]! as Term));
    } on ArgumentError catch (ex) {
      return LinkEstablish.abort(_who, ex.message.toString());
    }
    final reqArg = args[2];
    if (reqArg is! VarRef || !heap.isWriter(reqArg.addr)) {
      return LinkEstablish.abort(_who, 'arg 3 (Requests) must be an unbound writer cell');
    }
    final reqWriterAddr = reqArg.addr;

    // Register the pump as the run-to-quiescence inbound driver and keep it LIVE across
    // the genuinely-async rendezvous (the single isolate cannot block on listen/read).
    // Unlike path A's '_link_setup', listen here does NOT yet establish a link — so
    // without this nothing would keep the engine's pump-driver loop alive while
    // serve_one is suspended on Requests, and the goal would report `suspended` before
    // the rendezvous ever completed. The tracked rendezvous accepts the connection,
    // reads the in-band token, parks the connection for '_link_accept', and surfaces the
    // request THROUGH the inbox (pump discipline) so the runner-thread driver re-drives
    // the suspended serve_one when it lands.
    rt.inboundPump ??= link.pump;
    link.pump.trackIo(_rendezvous(link, scheme, endpointAddr, reqWriterAddr));

    return BodyKernelResult.success;
  }

  static Future<void> _rendezvous(LinkRuntime link, LinkScheme scheme,
      LinkAddress endpointAddr, int reqWriterAddr) async {
    final LinkId id;
    final Term fromPeer;
    final ILinkEndpoint endpoint;
    try {
      final transport = link.transports.select(scheme);
      final opts = LinkOptions.default_;
      final cancel = Future<void>.delayed(opts.connectTimeout);
      endpoint = await transport.listen(scheme, endpointAddr, opts, cancel: cancel);
      final token = await _readOneGroundMessage(endpoint, opts.connectTimeout);
      (id, fromPeer) = LinkTerms.parseRequestToken(token);
    } on Object catch (ex) {
      // No link/monitor stream exists yet to surface a fault onto; the rendezvous
      // simply produced no request. Surface as a diagnostic (a caller bug or a peer
      // that never connected), leaving Requests suspended.
      LinkEstablish.abort(
          _who, 'rendezvous/handshake on $scheme:$endpointAddr failed: $ex');
      return;
    }

    // Park the accepted connection for '_link_accept' to adopt, then surface the request
    // THROUGH the inbox: this continuation runs off the runner-thread drive loop, so per
    // the §1.3 pump discipline it must not bind the heap itself. The runner-thread
    // [LinkPump.tryApplyNext] extends Requests + reactivates serve_one, and the driver
    // re-drains.
    link.pending[id] = endpoint;
    link.pump.enqueueRequestSurface(reqWriterAddr, LinkTerms.requestToken(id, fromPeer));
  }

  /// Read one whole ground message off [endpoint] (reassembling fragments) — the
  /// out-of-band request token, consumed BEFORE the data pump engages so the data
  /// sequence still starts at 0. Throws on a non-ground payload (the base wire is
  /// ground-relay, FR-010) or peer close before a frame.
  static Future<Term> _readOneGroundMessage(
      ILinkEndpoint endpoint, Duration timeout) async {
    final reassembler = FrameReassembler();
    final deserializer = PayloadSerializer('');
    final cancel = Future<void>.delayed(timeout);
    while (true) {
      final Uint8List? frame = await endpoint.recvBytes(cancel: cancel);
      if (frame == null) {
        throw StateError('peer closed before sending a request token');
      }
      final Uint8List? payload = reassembler.accept(FrameCodec.parseFrame(frame));
      if (payload == null) {
        continue; // awaiting more fragments
      }
      return deserializer.deserializeAgentMessagePayload(
        payload,
        (_) => throw StateError(
            'request token carried a non-ground term (embedded variable)'),
      );
    }
  }
}
