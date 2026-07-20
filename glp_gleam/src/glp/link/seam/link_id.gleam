//// glp/link/seam/link_id — the stable, never-reused link identity
//// (feature 050, T045).
////
//// Port of `glp_runtime/lib/link/seam/link_id.dart` (mirror
//// `csharp/glp_link/seam/LinkId.cs`). `LinkId ::= link_id(Scheme, Endpoint, Nonce)`
//// (025 link-primitives.md §1). Record value-equality is the idempotency-at-identity
//// basis (FR-007): a re-setup with the same LinkId returns the already-established
//// link. The `Integer` and `String` nonce forms are kept distinct — an integer `5`
//// is NOT the string `"5"` — so equality matches the GLP term exactly rather than a
//// lossy canonical text.

import glp/link/seam/link_address.{type LinkAddress}
import glp/link/seam/link_scheme.{type LinkScheme}

/// The per-establishment uniqueness nonce (`Nonce ::= Integer ; String`).
pub type LinkNonce {
  NonceInt(value: Int)
  NonceStr(value: String)
}

/// `link_id(Scheme, Endpoint, Nonce)` — all components ground (the GLP wrappers
/// guard `ground/1` before the host sees them), so a LinkId is always fully
/// determined.
pub type LinkId {
  LinkId(scheme: LinkScheme, endpoint: LinkAddress, nonce: LinkNonce)
}
