//// glp/link/seam/link_options — per-link tunables for listen/connect
//// (feature 050, T045).
////
//// Port of `glp_runtime/lib/link/seam/link_options.dart` (mirror
//// `csharp/glp_link/seam/LinkOptions.cs`). Every value is a tuning parameter, NOT a
//// correctness condition (architecture-context.md §5): defaults preserve the GLP
//// invariants; overrides only adjust resource bounds and timeouts. Dart `Duration`s
//// are expressed here as milliseconds.

import gleam/option.{type Option, None}

pub type LinkOptions {
  LinkOptions(
    /// Bounded outbound window before the producer suspends (FR-025). Default 8:
    /// the producer suspends (FCP suspension, not an unbounded buffer) so there is
    /// no OOM and no head-of-line blocking across independent links.
    backpressure_window: Int,
    /// Require transport-level encryption for inter-host links (FR-029). Default
    /// True — a plain inter-host link is refused; a loopback leaf MAY ignore it.
    require_tls: Bool,
    /// Max transport frame size in bytes, or `None` for no leaf-imposed limit.
    /// Constrained leaves (CoAP ~1 KB, BLE GATT 20 B) set this so the framing
    /// layer fragments/reassembles under it (FR-022).
    max_frame_bytes: Option(Int),
    /// Bounded silence (ms) after which the sublayer surfaces `tempFail`
    /// (FR-045, SC-010). A liveness tuning knob, not a correctness bound.
    temp_fail_after_ms: Int,
    /// Bounded silence (ms) after which the sublayer gives up with `permFail`
    /// (FR-045). A deliberate, possibly-wrong give-up (FLP/two-generals).
    perm_fail_after_ms: Int,
    /// Timeout (ms) for the initial listen/connect rendezvous.
    connect_timeout_ms: Int,
  )
}

/// The default for all fields (mirrors `LinkOptions.default_`).
pub fn default() -> LinkOptions {
  LinkOptions(
    backpressure_window: 8,
    require_tls: True,
    max_frame_bytes: None,
    temp_fail_after_ms: 5000,
    perm_fail_after_ms: 30_000,
    connect_timeout_ms: 15_000,
  )
}
