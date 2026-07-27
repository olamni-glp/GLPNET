//// glp/link/primitives/link_registry — the idempotent LinkId → handle registry
//// (feature 059, T076 — port of glp_runtime/lib/link/primitives/link_registry.dart,
//// mirror csharp/glp_link/primitives/LinkRegistry.cs).
////
//// Makes `_link_setup` idempotent at link-identity (FR-007): re-invoking setup with
//// the same ground `LinkId` returns the already-established handle rather than
//// opening a conflicting duplicate. Keyed by the never-reused ground `LinkId`
//// (structural value-equality). One registry per link runtime.
////
//// GLEAM MAPPING NOTE: the Dart registry is a MUTABLE `Map` on the engine; here it
//// is an IMMUTABLE `Dict` threaded through the (T074) link driver. `put` refuses to
//// overwrite an existing identity (the FR-007 first-establishment invariant), so a
//// re-establishment is an `Error` the kernel surfaces (cell-aliasing on a second
//// setup is unspecified — surfaced, not guessed).

import gleam/dict.{type Dict}
import gleam/result
import glp/link/primitives/link_handle.{type LinkHandle}
import glp/link/seam/link_id.{type LinkId}

/// A LinkId → handle registry.
pub opaque type LinkRegistry {
  LinkRegistry(handles: Dict(LinkId, LinkHandle))
}

/// An empty registry.
pub fn new() -> LinkRegistry {
  LinkRegistry(dict.new())
}

/// Look up an established link by identity; `Error(Nil)` when none exists.
pub fn try_get(registry: LinkRegistry, id: LinkId) -> Result(LinkHandle, Nil) {
  dict.get(registry.handles, id)
}

/// True if a handle is registered for `id` (the idempotency check).
pub fn contains(registry: LinkRegistry, id: LinkId) -> Bool {
  case dict.get(registry.handles, id) {
    Ok(_) -> True
    Error(Nil) -> False
  }
}

/// Store a freshly-built handle under its identity. `Error(String)` if `handle.id`
/// disagrees with `id`, or if an entry already exists (idempotency is enforced by
/// the caller's `contains` check BEFORE establishment — a `put` over an existing id
/// is an FR-007 violation, surfaced not swallowed).
pub fn put(
  registry: LinkRegistry,
  id: LinkId,
  handle: LinkHandle,
) -> Result(LinkRegistry, String) {
  case handle.id == id {
    False -> Error("established handle id disagrees with requested LinkId")
    True ->
      case dict.get(registry.handles, id) {
        Ok(_) ->
          Error("put over an already-established LinkId (idempotency violated)")
        Error(Nil) ->
          Ok(LinkRegistry(dict.insert(registry.handles, id, handle)))
      }
  }
}

/// Replace an existing handle in place (the T074 driver threads an advanced handle
/// back — e.g. after `attach_endpoint` / `next_seq` / a monitor-cursor add).
/// `Error(String)` if no handle exists for `id` (a replace presupposes establishment).
pub fn replace(
  registry: LinkRegistry,
  id: LinkId,
  handle: LinkHandle,
) -> Result(LinkRegistry, String) {
  case dict.get(registry.handles, id) {
    Error(Nil) -> Error("replace of an unestablished LinkId")
    Ok(_) -> Ok(LinkRegistry(dict.insert(registry.handles, id, handle)))
  }
}

/// Remove a link's handle (distributed GC on permFail / close, FR-024). `True` if a
/// handle was removed.
pub fn remove(registry: LinkRegistry, id: LinkId) -> #(LinkRegistry, Bool) {
  let existed = contains(registry, id)
  #(LinkRegistry(dict.delete(registry.handles, id)), existed)
}

/// Number of established links.
pub fn count(registry: LinkRegistry) -> Int {
  dict.size(registry.handles)
}

/// Every established handle (clean-shutdown iteration — dispose each endpoint).
pub fn handles(registry: LinkRegistry) -> List(LinkHandle) {
  dict.values(registry.handles)
}

/// The existing handle for `id`, or one built by `establish` on first setup
/// (idempotent reuse, FR-007). `establish` runs only when no handle exists yet.
/// Returns the (possibly-updated) registry and the handle in force.
pub fn get_or_establish(
  registry: LinkRegistry,
  id: LinkId,
  establish: fn() -> LinkHandle,
) -> Result(#(LinkRegistry, LinkHandle), String) {
  case dict.get(registry.handles, id) {
    Ok(existing) -> Ok(#(registry, existing))
    Error(Nil) -> {
      let handle = establish()
      use registry <- result.try(put(registry, id, handle))
      Ok(#(registry, handle))
    }
  }
}
