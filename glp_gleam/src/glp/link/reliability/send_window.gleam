//// glp/link/reliability/send_window — per-link bounded send window for backpressure
//// (feature 059, T077 — port of glp_runtime/lib/link/reliability/send_window.dart,
//// mirror csharp/glp_link/reliability/SendWindow.cs).
////
//// The egress drainer acquires one credit per in-flight frame and releases it when
//// the frame is acked/consumed by the peer. When all `capacity` credits are out the
//// producer SUSPENDS rather than buffering unboundedly (FR-025, SC-013) — the
//// host-side reflection of FCP producer suspension, so the outbound queue stays
//// bounded (no OOM). Each link owns its own window, so a full window on one link never
//// blocks an independent link (no head-of-line blocking — FR-025). Over-releasing (an
//// ack for a frame never in flight) is surfaced (a double-ack bug), never a silent
//// corrupt count.
////
//// GLEAM MAPPING NOTE: the Dart window carries an ASYNC waiter queue (`Completer`s) so
//// a parked producer resumes when a credit frees — the single-isolate stand-in for
//// suspension. On BEAM the pump is synchronous and producer suspension is the ordinary
//// FCP suspension ABOVE the seam, so this port models the SYNCHRONOUS credit
//// accounting only (`try_acquire`/`release`), immutable-value threaded. `available`,
//// `in_flight`, and the over-release error are byte-faithful to the Dart accounting.

pub opaque type SendWindow {
  SendWindow(capacity: Int, current: Int)
}

/// The over-release error (an ack for a frame that was never in flight — the mirror of
/// .NET's `SemaphoreFullException` / Dart's `SemaphoreFullError`).
pub type SemaphoreFull {
  SemaphoreFull
}

/// A window of size `window` (must be >= 1); panics on a non-positive window (a
/// configuration bug, not a runtime condition — Dart throws `RangeError`).
pub fn with_window(window: Int) -> SendWindow {
  case window >= 1 {
    True -> SendWindow(capacity: window, current: window)
    False -> panic as "SendWindow window must be >= 1"
  }
}

/// The default window N=8 (`LinkOptions.backpressure_window`).
pub fn new() -> SendWindow {
  with_window(8)
}

/// The window size N — the maximum number of in-flight frames.
pub fn capacity(w: SendWindow) -> Int {
  w.capacity
}

/// Credits currently free (frames that may be sent without suspending).
pub fn available(w: SendWindow) -> Int {
  w.current
}

/// Frames currently in flight (credits acquired, not yet released).
pub fn in_flight(w: SendWindow) -> Int {
  w.capacity - w.current
}

/// Try to acquire a credit without suspending. Returns the (possibly advanced) window
/// and `True` on success; `#(w, False)` when the window is full (the caller then
/// suspends via ordinary FCP suspension above the seam, or backs off).
pub fn try_acquire(w: SendWindow) -> #(SendWindow, Bool) {
  case w.current > 0 {
    True -> #(SendWindow(..w, current: w.current - 1), True)
    False -> #(w, False)
  }
}

/// Release a credit on ack/consume of one in-flight frame. `Error(SemaphoreFull)` if
/// the window is already full (an ack for a frame never in flight — a double-ack bug).
pub fn release(w: SendWindow) -> Result(SendWindow, SemaphoreFull) {
  case w.current >= w.capacity {
    True -> Error(SemaphoreFull)
    False -> Ok(SendWindow(..w, current: w.current + 1))
  }
}
