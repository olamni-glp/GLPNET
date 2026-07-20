//// glp/link/primitives/link_registry — THE single canonical established-link registry
//// (feature 050, T050.C1).
////
//// Port of `csharp/glp_link/primitives/LinkRegistry.cs` (mirror
//// `glp_runtime/lib/link/primitives/link_registry.dart`).
////
//// **This module is the R-5 mitigation.** 025 `link-primitives.md` §8 R-5: *"two
//// establishment paths, one registry — FR-002 'equivalent established link' requires
//// listen/connect AND request/accept to converge on the same `'_link_setup'`
//// registry/handle. If they diverge (e.g. different LinkId normalization), idempotency
//// (FR-007) and origin-auth (FR-026) keying break. Single canonical registry keyed by
//// ground LinkId is the mitigation."*
////
//// Both paths reach it through ONE funnel — `link_establish.wire_established_link` —
//// so an established link is indistinguishable regardless of how it was established.
//// Nothing else in the tree may hold its own map of links.
////
//// **Keyed by the ground `LinkId` record**, whose Gleam structural equality is exactly
//// the FR-007 idempotency-at-identity basis. `link_id` deliberately keeps `NonceInt(5)`
//// distinct from `NonceStr("5")`, so identity matches the GLP term rather than a lossy
//// canonical text — two links differing only in nonce type stay two links.
////
//// **Immutability difference from the oracles.** C#/Dart mutate a `Dictionary` in
//// place; a Gleam `Dict` is persistent, so every mutating operation returns a new
//// registry that the caller threads forward (ultimately on `link_runtime`). Same
//// semantics, no shared mutable state.

import gleam/dict.{type Dict}
import gleam/result
import glp/link/primitives/link_handle.{type LinkHandle}
import glp/link/seam/link_id.{type LinkId}

/// The per-instance LinkId → handle map. Opaque: callers must go through the
/// operations below, so there is no way to grow a second, divergent index.
pub opaque type LinkRegistry {
  LinkRegistry(handles: Dict(LinkId, LinkHandle))
}

/// An empty registry (one per `link_runtime` / per engine instance).
pub fn new() -> LinkRegistry {
  LinkRegistry(handles: dict.new())
}

/// Look up an established link by identity.
pub fn try_get(registry: LinkRegistry, id: LinkId) -> Result(LinkHandle, Nil) {
  dict.get(registry.handles, id)
}

/// Is a link with this identity established?
pub fn contains(registry: LinkRegistry, id: LinkId) -> Bool {
  case dict.get(registry.handles, id) {
    Ok(_) -> True
    Error(_) -> False
  }
}

/// Number of established links.
pub fn count(registry: LinkRegistry) -> Int {
  dict.size(registry.handles)
}

/// Store (or replace) a handle. Used to thread back a handle mutated during a
/// reduction — an advanced sequence number, newly wired cursors, an added monitor
/// cursor. Establishment itself must go through `get_or_establish`.
pub fn put(registry: LinkRegistry, handle: LinkHandle) -> LinkRegistry {
  LinkRegistry(handles: dict.insert(registry.handles, handle.id, handle))
}

/// Remove a link's handle — distributed GC on `permFail` / `link_close` (C8, FR-024).
pub fn remove(registry: LinkRegistry, id: LinkId) -> LinkRegistry {
  LinkRegistry(handles: dict.delete(registry.handles, id))
}

/// Whether `get_or_establish` reused an existing link or opened a new one. Callers
/// need this because the two differ observably: a REUSED link must NOT re-open a
/// transport, re-wire cursors, or re-arm a pump (that would double-consume the peer's
/// frames), while a NEW one must do all three.
pub type Establishment {
  /// The identity was already established — the existing handle is returned untouched
  /// and `establish` was never run (FR-007 idempotency at link identity).
  Reused(handle: LinkHandle)
  /// First establishment for this identity: `establish` ran and its handle is stored.
  Established(handle: LinkHandle)
}

/// The handle for `id`, establishing one only if the identity is not already present
/// (FR-007). `establish` runs **at most once** and only on first setup — that is what
/// makes a repeated `link_setup/4` with the same ground LinkId idempotent rather than
/// opening a conflicting duplicate connection.
///
/// `establish` returns a `Result` because opening a transport can genuinely fail
/// (rendezvous timeout, refused connect); the failure is passed through untouched so
/// the caller can turn it into a fault term rather than a crash.
///
/// **Identity invariant (ported from the C# oracle, which throws here):** the handle
/// `establish` produces MUST carry the same `LinkId` it was asked for. A mismatch would
/// silently file a link under the wrong key and break exactly the FR-007/FR-026 keying
/// R-5 exists to protect, so it is surfaced as `Error(IdentityMismatch(..))` — never
/// stored, never swallowed.
pub fn get_or_establish(
  registry: LinkRegistry,
  id: LinkId,
  establish: fn() -> Result(LinkHandle, e),
) -> Result(#(LinkRegistry, Establishment), RegistryError(e)) {
  case dict.get(registry.handles, id) {
    Ok(existing) -> Ok(#(registry, Reused(existing)))
    Error(_) -> {
      use handle <- result.try(
        establish() |> result.map_error(EstablishFailed),
      )
      case handle.id == id {
        False -> Error(IdentityMismatch(requested: id, produced: handle.id))
        True -> Ok(#(put(registry, handle), Established(handle)))
      }
    }
  }
}

/// Why `get_or_establish` could not establish a link.
pub type RegistryError(e) {
  /// The `establish` callback failed (transport open error, capability refusal, …).
  /// Carries the underlying reason unchanged.
  EstablishFailed(reason: e)
  /// `establish` produced a handle whose identity differs from the one requested —
  /// an internal invariant break (see `get_or_establish`), never a peer's fault.
  IdentityMismatch(requested: LinkId, produced: LinkId)
}
