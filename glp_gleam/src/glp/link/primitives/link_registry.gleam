//// glp/link/primitives/link_registry — the per-instance table of established
//// links (wave-3 US4, T035).
////
//// Port of `glp_runtime/lib/link/primitives/link_registry.dart` (mirror C#
//// `LinkRegistry`): keyed by `LinkId` structural equality — the
//// idempotency-at-identity basis (FR-007-025: re-establishment of the same
//// ground identity is refused, never a silent duplicate). Immutable.

import gleam/dict.{type Dict}
import glp/link/primitives/link_handle.{type LinkHandle}
import glp/link/seam/link_id.{type LinkId}

pub opaque type LinkRegistry {
  LinkRegistry(links: Dict(LinkId, LinkHandle))
}

pub fn new() -> LinkRegistry {
  LinkRegistry(dict.new())
}

pub fn contains(registry: LinkRegistry, id: LinkId) -> Bool {
  dict.has_key(registry.links, id)
}

pub fn get(registry: LinkRegistry, id: LinkId) -> Result(LinkHandle, Nil) {
  dict.get(registry.links, id)
}

pub fn put(registry: LinkRegistry, handle: LinkHandle) -> LinkRegistry {
  LinkRegistry(dict.insert(registry.links, handle.id, handle))
}

/// Remove a torn-down link (the distributed-GC reclaim — the runtime returns
/// to baseline after close/permFail).
pub fn remove(registry: LinkRegistry, id: LinkId) -> LinkRegistry {
  LinkRegistry(dict.delete(registry.links, id))
}

pub fn size(registry: LinkRegistry) -> Int {
  dict.size(registry.links)
}
