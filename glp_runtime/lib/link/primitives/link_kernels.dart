import '../../runtime/body_kernels.dart' show BodyKernelRegistry;
import '../../runtime/runtime.dart';
import 'link_accept_kernel.dart';
import 'link_close_kernel.dart';
import 'link_listen_kernel.dart';
import 'link_monitor_kernel.dart';
import 'link_request_kernel.dart';
import 'link_runtime.dart';
import 'link_send_kernel.dart';
import 'link_setup_kernel.dart';

/// The composition-root entry point that wires the link layer into a [GlpRuntime]
/// (feature 025). Registers the host body kernels on the engine's
/// [BodyKernelRegistry] via the injection seam — NO edit to the runtime's
/// `body_kernels.dart`, so the dependency stays `link -> runtime` (FR-057). Called by
/// a test, or by the REPL/isolate boot path, AFTER transports are registered into the
/// returned [LinkRuntime.transports].
///
/// Body-kernel names are registered WITHOUT the surrounding quotes the GLP source
/// writes (`'_link_setup'` compiles to the label `_link_setup`, split on '/' to the
/// bare proc name at the dispatch site), matching the standard-kernel convention
/// (`_send`, `_add`, …).
class LinkKernels {
  LinkKernels._();

  /// The registered (quote-stripped) name of the link-setup kernel.
  static const String linkSetupName = '_link_setup';

  /// The kernel arity of `'_link_setup'/5`.
  static const int linkSetupArity = 5;

  /// The registered (quote-stripped) name of the LinkId-keyed sender kernel.
  static const String linkSendName = '_link_send';

  /// The kernel arity of `'_link_send'/3`.
  static const int linkSendArity = 3;

  /// Path-B handshake kernels (T033): connector / rendezvous-listener / accept.
  static const String linkRequestName = '_link_request';
  static const int linkRequestArity = 5;
  static const String linkListenName = '_link_listen';
  static const int linkListenArity = 3;
  static const String linkAcceptName = '_link_accept';
  static const int linkAcceptArity = 5;

  /// The registered (quote-stripped) name of the per-link fault-monitor kernel (T034).
  static const String linkMonitorName = '_link_monitor';

  /// The kernel arity of `'_link_monitor'/2`.
  static const int linkMonitorArity = 2;

  /// The registered (quote-stripped) name of the link-close kernel (T035).
  static const String linkCloseName = '_link_close';

  /// The kernel arity of `'_link_close'/2`.
  static const int linkCloseArity = 2;

  /// Build a [LinkRuntime] for [engine] and register the link body kernels on it.
  /// Register transports into the returned runtime's [LinkRuntime.transports] before
  /// the first `'_link_setup'` goal.
  static LinkRuntime install(GlpRuntime engine) {
    // ignore: unnecessary_null_comparison, dead_code
    if (engine == null) throw ArgumentError.notNull('engine');
    final link = LinkRuntime(engine);
    register(engine.bodyKernels, link);
    return link;
  }

  /// Register the link body kernels on an existing registry against a given runtime.
  static void register(BodyKernelRegistry registry, LinkRuntime link) {
    // ignore: unnecessary_null_comparison, dead_code
    if (registry == null) throw ArgumentError.notNull('registry');
    // ignore: unnecessary_null_comparison, dead_code
    if (link == null) throw ArgumentError.notNull('link');
    registry.register(linkSetupName, linkSetupArity,
        (rt, args) => LinkSetupKernel.linkSetup(rt, args, link));
    registry.register(linkSendName, linkSendArity,
        (rt, args) => LinkSendKernel.linkSend(rt, args, link));
    registry.register(linkRequestName, linkRequestArity,
        (rt, args) => LinkRequestKernel.linkRequest(rt, args, link));
    registry.register(linkListenName, linkListenArity,
        (rt, args) => LinkListenKernel.linkListen(rt, args, link));
    registry.register(linkAcceptName, linkAcceptArity,
        (rt, args) => LinkAcceptKernel.linkAccept(rt, args, link));
    registry.register(linkMonitorName, linkMonitorArity,
        (rt, args) => LinkMonitorKernel.linkMonitor(rt, args, link));
    registry.register(linkCloseName, linkCloseArity,
        (rt, args) => LinkCloseKernel.linkClose(rt, args, link));
  }
}
