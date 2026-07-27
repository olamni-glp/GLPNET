//// glp/link/reliability/fencing_registry — split-brain defense via fencing tokens
//// (feature 059, T077 — port of glp_runtime/lib/link/reliability/fencing_registry.dart,
//// mirror csharp/glp_link/reliability/FencingRegistry.cs).
////
//// Each link establishment carries a monotonically increasing `epoch` (the fencing
//// token, from `EpochAllocator`). This registry — the "resource" in the Kleppmann
//// fencing model — remembers the highest epoch admitted per global name and FENCES any
//// later write carrying a lower epoch (FR-047, SC-011). So a partitioned writer that
//// resumes after a newer writer took over has its stale (lower-epoch) binding rejected
//// rather than silently overwriting the live one; exactly one writer wins.
////
//// This is IN ADDITION TO global-name idempotency (the madGLP Receive dedup gate):
//// idempotency absorbs a re-delivered SAME binding; fencing rejects a CONFLICTING stale
//// one. The link layer surfaces `permFail` for a fenced writer — never a silent
//// overwrite, never an error verdict (FR-047).
////
//// GLEAM MAPPING NOTE: the Dart registry + allocator mutate in place; here they are
//// IMMUTABLE values — `admit`/`next` return the verdict/epoch WITH the advanced value.

import gleam/dict.{type Dict}

/// The fencing decision for a writer attempting to bind a global name.
pub type FenceVerdict {
  /// This writer holds the highest epoch seen for the name; it may bind.
  Admit
  /// A newer (higher-epoch) writer has already taken over; this stale writer is
  /// fenced out (→ `permFail`, never a silent overwrite).
  Fenced
}

pub opaque type FencingRegistry {
  FencingRegistry(highest: Dict(String, Int))
}

/// An empty registry.
pub fn new() -> FencingRegistry {
  FencingRegistry(highest: dict.new())
}

/// Decide whether a writer carrying `epoch` may bind `global_name`, returning the
/// (possibly updated) registry and the verdict. Admits the first writer and any writer
/// whose epoch is >= the highest admitted (equal = the same/idempotent writer; higher =
/// a legitimate takeover). Fences a lower epoch (state unchanged on a fence).
pub fn admit(
  registry: FencingRegistry,
  global_name: String,
  epoch: Int,
) -> #(FencingRegistry, FenceVerdict) {
  case dict.get(registry.highest, global_name) {
    Ok(highest) if epoch < highest -> #(registry, Fenced)
    _ -> #(
      FencingRegistry(highest: dict.insert(registry.highest, global_name, epoch)),
      Admit,
    )
  }
}

/// The highest epoch admitted for a name, or `Error(Nil)` if none yet.
pub fn highest_epoch_for(
  registry: FencingRegistry,
  global_name: String,
) -> Result(Int, Nil) {
  dict.get(registry.highest, global_name)
}

/// Drop all fencing state for a name (distributed GC after the link's writers are
/// reclaimed). After this the name is fresh again.
pub fn forget(registry: FencingRegistry, global_name: String) -> FencingRegistry {
  FencingRegistry(highest: dict.delete(registry.highest, global_name))
}

/// Number of global names currently tracked.
pub fn tracked_count(registry: FencingRegistry) -> Int {
  dict.size(registry.highest)
}

// ── EpochAllocator ────────────────────────────────────────────────────────────

/// Monotone source of per-establishment fencing epochs (FR-047). Each new link
/// establishment (or writer takeover) draws the next epoch; higher always means newer.
pub opaque type EpochAllocator {
  EpochAllocator(next: Int)
}

/// An allocator starting at `start` (default 1 via `new_allocator`).
pub fn allocator_from(start: Int) -> EpochAllocator {
  EpochAllocator(next: start)
}

/// An allocator starting at 1.
pub fn new_allocator() -> EpochAllocator {
  EpochAllocator(next: 1)
}

/// The next establishment epoch, with the advanced allocator.
pub fn next_epoch(alloc: EpochAllocator) -> #(EpochAllocator, Int) {
  #(EpochAllocator(next: alloc.next + 1), alloc.next)
}
