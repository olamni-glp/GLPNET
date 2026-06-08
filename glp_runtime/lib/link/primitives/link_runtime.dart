import '../../runtime/runtime.dart';
import '../reliability/link_reclaimer.dart';
import '../seam/i_link_endpoint.dart';
import '../seam/link_id.dart';
import 'link_pump.dart';
import 'link_registry.dart';
import 'transport_registry.dart';

/// The per-engine link-layer state the `'_link_setup'` kernel operates on
/// (feature 025, T030): the scheme→leaf [TransportRegistry], the
/// idempotent LinkId→[LinkHandle] [LinkRegistry], and the
/// single [LinkPump] that bridges asynchronous inbound frames onto the
/// runner thread (Option B).
///
/// One [LinkRuntime] per [GlpRuntime]. It lives in the
/// hand-authored `glp_link` package, so the dependency flows
/// `glp_link → out/csharp` only (FR-057): the engine never references the link
/// layer; the link layer is wired in at the composition root (a test, or the REPL's
/// boot path) by `LinkKernels.install`, which registers the kernels and
/// hands the engine a pump to drain.
class LinkRuntime {
  /// Scheme→transport-leaf selection (FR-013); register leaves before setup.
  final TransportRegistry transports = TransportRegistry();

  /// Idempotent-at-identity LinkId→handle registry (FR-007).
  final LinkRegistry links = LinkRegistry();

  /// Distributed-GC coordinator (T024, FR-024). Each established link registers its
  /// reclamation hooks here (the registry entry today; send-registry goals + reply-table
  /// entries as those subsystems gain live per-link state); `'_link_close'` and the
  /// graceful stream-end `[]` close run them via `LinkClose.Teardown` so the
  /// runtime returns to its pre-link baseline (T035). One per instance; not thread-safe.
  final LinkReclaimer reclaimer = LinkReclaimer();

  /// The one inbound pump this engine's run-to-quiescence driver services (Option B).
  late final LinkPump pump;

  /// Accepted-but-not-yet-adopted inbound connections, keyed by the ground
  /// [LinkId] carried in the request token (T033 Option A). Populated by
  /// `'_link_listen'` when it reads an in-band request frame; drained by
  /// `'_link_accept'`, which adopts the endpoint into [links] via the
  /// shared establish-core. The two-step split (listen produces requests; accept
  /// establishes one) is the ratified separation of concerns.
  final Map<LinkId, ILinkEndpoint> pending = <LinkId, ILinkEndpoint>{};

  /// The engine whose heap the kernel binds and whose goal queue the pump feeds.
  final GlpRuntime engine;

  LinkRuntime(GlpRuntime engine)
      // ignore: unnecessary_null_comparison, prefer_initializing_formals
      : engine = engine {
    // ignore: unnecessary_null_comparison, dead_code
    if (engine == null) throw ArgumentError.notNull('engine');
    pump = LinkPump(engine);
  }

  /// Release every link-layer OS resource (feature 025 clean shutdown): cancel the
  /// pump's background recv loops (completing its cancel signal unparks each
  /// `await recvBytes`), then dispose every established and every parked-but-unadopted
  /// transport endpoint — close + destroy the socket and cancel its reader
  /// subscription. After this the isolate holds no link sockets through the registry,
  /// so a one-shot REPL / embedded host can terminate promptly instead of being held
  /// alive by parked recv Futures (which is what hung the back-to-back link suite's
  /// `wait`). Best-effort and idempotent; a still-pending transport `listen`/`connect`
  /// future (its socket not yet in the registry) is the REPL's `exit(0)` backstop.
  Future<void> dispose() async {
    pump.dispose();
    for (final handle in links.handles) {
      final ep = handle.endpoint;
      if (ep != null) {
        try {
          await ep.dispose();
        } on Object {
          /* best-effort: process is shutting down */
        }
      }
    }
    for (final ep in pending.values) {
      try {
        await ep.dispose();
      } on Object {
        /* best-effort */
      }
    }
    pending.clear();
  }
}
