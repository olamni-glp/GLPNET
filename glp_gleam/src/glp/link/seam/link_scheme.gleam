//// glp/link/seam/link_scheme — the transport scheme token (feature 050, T045).
////
//// Port of `glp_runtime/lib/link/seam/link_scheme.dart` (mirror
//// `csharp/glp_link/seam/LinkScheme.cs`). A normalized, case-insensitive scheme
//// token — the `Scheme ::= String` component of
//// `LinkId ::= link_id(Scheme, Endpoint, Nonce)`
//// (specs/025-multi-protocol-link-layer/contracts/link-primitives.md §1). It keys
//// the transport registry (T050); nothing protocol-specific leaks above the seam
//// (FR-006). Structural value-equality is automatic for Gleam custom types.
////
//// 050 scope (data-model.md §Transport): loopback | tcp | quic. The on-wire token
//// for QUIC-WS is "quic" — matching the C# reference peer `LinkScheme.Quic`, not
//// the colloquial "quicws".

import gleam/string

/// A normalized (trimmed, lower-cased) scheme token. Opaque so every value flows
/// through `of` or a named constructor and is therefore normalized.
pub opaque type LinkScheme {
  LinkScheme(name: String)
}

/// Deterministic in-memory transport for hermetic tests (T048).
pub fn loopback() -> LinkScheme {
  LinkScheme("loopback")
}

/// Raw TCP over IPv4 localhost — the first cross-process transport (T049).
pub fn tcp() -> LinkScheme {
  LinkScheme("tcp")
}

/// Genuine HTTP/3 (QUIC) carrying a WebSocket link (T055). Wire token "quic",
/// matching the C# reference peer.
pub fn quic() -> LinkScheme {
  LinkScheme("quic")
}

/// ZeroMQ (ZMTP) transport. Added to the transport contract by the owner ruling
/// of 2026-07-23 (docs/research/fullscope-gleam/phase2-verify/rulings.md — the
/// G5 `zmq-comm-base` out-of-scope disposition was OVERRULED; ZMQ is mandatory,
/// the contract is now {loopback, tcp, quic, zmq}). Wire token "zmq".
pub fn zmq() -> LinkScheme {
  LinkScheme("zmq")
}

/// Build a scheme from a raw GLP `Scheme` string, normalizing case so "WS" and
/// "ws" denote the same transport. A blank token is a caller bug — the GLP wrapper
/// guards `ground/1` before the host ever sees it (never a value to tolerate) — so
/// this panics rather than yield an empty scheme.
pub fn of(scheme: String) -> LinkScheme {
  case string.lowercase(string.trim(scheme)) {
    "" -> panic as "LinkScheme requires a non-blank scheme token."
    normalized -> LinkScheme(normalized)
  }
}

/// The normalized scheme token (never empty for a valid value).
pub fn name(scheme: LinkScheme) -> String {
  scheme.name
}
