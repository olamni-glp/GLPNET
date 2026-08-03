//// glp/link/reliability/send_window — per-link bounded send window
//// (wave-3 US4; FR-021 backpressure, 025 FR-025/SC-013).
////
//// Port of `glp_runtime/lib/link/reliability/send_window.dart` (mirror C#
//// `SendWindow` over `SemaphoreSlim`): the egress drainer acquires one credit
//// per in-flight frame and releases it when the frame is consumed. When all
//// credits are out the PRODUCER suspends (the caller parks on `try_acquire`
//// returning `False` — the host-side reflection of FCP producer suspension), so
//// the outbound queue stays bounded. Each link owns its own window, so a full
//// window on one link never blocks an independent link. Over-releasing (acking
//// a frame never in flight) errors loudly — surfacing the bug at its cause.
//// Immutable — acquire/release return the advanced window.

import gleam/int

pub opaque type SendWindow {
  SendWindow(capacity: Int, current: Int)
}

/// A window of `capacity` credits (reference default 8; must be >= 1 — a
/// non-positive capacity is a caller bug, surfaced loudly).
pub fn new(capacity: Int) -> Result(SendWindow, String) {
  case capacity >= 1 {
    True -> Ok(SendWindow(capacity, capacity))
    False ->
      Error("window must be >= 1, got " <> int.to_string(capacity))
  }
}

/// Credits currently free (frames that may be sent without suspending).
pub fn available(window: SendWindow) -> Int {
  window.current
}

/// Frames currently in flight (credits acquired, not yet released).
pub fn in_flight(window: SendWindow) -> Int {
  window.capacity - window.current
}

/// Try to acquire a credit. `Ok(window)` with one fewer credit, or
/// `Error(Nil)` when the window is full — the caller suspends/backs off (the
/// backpressure point).
pub fn try_acquire(window: SendWindow) -> Result(SendWindow, Nil) {
  case window.current > 0 {
    True -> Ok(SendWindow(..window, current: window.current - 1))
    False -> Error(Nil)
  }
}

/// Release a credit (the frame was consumed by the peer). Over-releasing is a
/// loud error, never a silent count corruption.
pub fn release(window: SendWindow) -> Result(SendWindow, String) {
  case window.current < window.capacity {
    True -> Ok(SendWindow(..window, current: window.current + 1))
    False -> Error("send window over-release: no frame in flight")
  }
}
