import 'dart:async';

import '../../runtime/body_kernels.dart' show BodyKernelResult;
import '../../runtime/runtime.dart';
import '../../runtime/terms.dart';
import '../seam/i_link_endpoint.dart';
import '../seam/link_id.dart';
import '../seam/link_options.dart';
import '../seam/link_role.dart';
import 'link_establish.dart';
import 'link_runtime.dart';
import 'link_terms.dart';

/// The `'_link_setup'/5` body kernel (feature 025, T030) — the host body of the
/// GLP wrapper
/// `link_setup(LinkId, Role, ch(In?, Out), Faults?) :- ground(LinkId?), ground(Role?) | '_link_setup'(LinkId?, Role?, In, Out?, Faults).`
/// It establishes-or-reuses a transport leaf for a ground [LinkId] in a given
/// [LinkRole] (listen/connect, FR-002 path A) and converges on the shared
/// [LinkEstablish.wireEstablishedLink] core — the same core the path-B handshake
/// (`'_link_request'`/`'_link_accept'`) uses, so both paths yield an
/// indistinguishable established link (FR-001/002/003/004/007).
///
/// Arguments (already ground-guarded by the GLP wrapper): `args[0]` LinkId,
/// `args[1]` Role (`listener`|`connector`), `args[2]` In writer, `args[3]` Out?
/// reader, `args[4]` Faults writer.
///
/// ASYNC-AWARE (Dart single-isolate adaptation of the C# blocking
/// `endpointTask.GetAwaiter().GetResult()`): the transport rendezvous is handed to
/// [LinkEstablish.wireEstablishedLink] as a `Future<ILinkEndpoint> Function()`,
/// which wires the heap cursors + registry SYNCHRONOUSLY and kicks the connect off
/// without awaiting.
class LinkSetupKernel {
  LinkSetupKernel._();

  static const String _who = "'_link_setup'/5";

  static BodyKernelResult linkSetup(
      GlpRuntime rt, List<Object?> args, LinkRuntime link) {
    if (args.length != 5) {
      return LinkEstablish.abort(_who, 'expected 5 arguments, got ${args.length}');
    }

    final heap = rt.heap;
    LinkId id;
    LinkRole role;
    try {
      id = LinkTerms.parseLinkId(LinkTerms.groundResolve(heap, args[0]! as Term));
      role = LinkTerms.parseRole(LinkTerms.groundResolve(heap, args[1]! as Term));
    } on ArgumentError catch (ex) {
      return LinkEstablish.abort(_who, ex.message.toString());
    }

    return LinkEstablish.wireEstablishedLink(
        rt, link, id, () => _establish(link, id, role),
        args[2], args[3], args[4], _who);
  }

  /// Open the transport leaf for [id] in [role] and build the endpoint. Bounded by
  /// [LinkOptions.connectTimeout] via a cancel Future the transport listen/connect
  /// races against (the Dart mirror of the C# `CancellationTokenSource`).
  static Future<ILinkEndpoint> _establish(
      LinkRuntime link, LinkId id, LinkRole role) {
    final transport = link.transports.select(id.scheme);
    final opts = LinkOptions.default_;
    final cancel = Future<void>.delayed(opts.connectTimeout);
    return role == LinkRole.listener
        ? transport.listen(id.scheme, id.endpoint, opts, cancel: cancel)
        : transport.connect(id.scheme, id.endpoint, opts, cancel: cancel);
  }
}
