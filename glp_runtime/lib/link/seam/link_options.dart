import 'link_scheme.dart';

/// Per-link tunables passed to `ILinkTransport.listen` /
/// `ILinkTransport.connect`. Every value is a tuning parameter,
/// NOT a correctness condition (architecture-context.md §5): defaults preserve the
/// GLP invariants; overrides only adjust resource bounds and timeouts.
class LinkOptions {
  /// The default for all fields.
  static final LinkOptions default_ = LinkOptions();

  /// Bounded outbound window before the producer suspends (FR-025). Default
  /// `8`. Scheme-overridable BELOW the seam; the producer suspends (FCP
  /// suspension, not an unbounded buffer) so there is no OOM and no
  /// head-of-line blocking across independent links.
  final int backpressureWindow;

  /// Require transport-level encryption for inter-host links (FR-029). Default
  /// `true`: a plain inter-host link is refused (TLS/DTLS by default). A
  /// leaf MAY ignore this for an intrinsically in-process scheme
  /// ([LinkScheme.loopback]).
  final bool requireTls;

  /// Maximum transport frame size in bytes, or `null` for no leaf-imposed
  /// limit. Constrained leaves (CoAP ~1 KB, BLE GATT 20 B) set this so the
  /// framing layer (T021) fragments/reassembles under it (FR-022).
  final int? maxFrameBytes;

  /// Bounded silence after which the sublayer surfaces `tempFail` (FR-045,
  /// SC-010). A liveness tuning knob, not a correctness bound.
  final Duration tempFailAfter;

  /// Bounded silence after which the sublayer gives up with `permFail`
  /// (FR-045). A deliberate, possibly-wrong give-up (FLP/two-generals): bounds
  /// blast radius, does not remove the wall.
  final Duration permFailAfter;

  /// Timeout for the initial listen/connect rendezvous.
  final Duration connectTimeout;

  LinkOptions({
    this.backpressureWindow = 8,
    this.requireTls = true,
    this.maxFrameBytes,
    this.tempFailAfter = const Duration(seconds: 5),
    this.permFailAfter = const Duration(seconds: 30),
    this.connectTimeout = const Duration(seconds: 15),
  });

  @override
  bool operator ==(Object other) =>
      other is LinkOptions &&
      backpressureWindow == other.backpressureWindow &&
      requireTls == other.requireTls &&
      maxFrameBytes == other.maxFrameBytes &&
      tempFailAfter == other.tempFailAfter &&
      permFailAfter == other.permFailAfter &&
      connectTimeout == other.connectTimeout;

  @override
  int get hashCode => Object.hash(backpressureWindow, requireTls, maxFrameBytes,
      tempFailAfter, permFailAfter, connectTimeout);
}
