//// glp/link/reliability/resource_snapshot — a count of the per-link runtime resources
//// distributed GC must reclaim (feature 059, T077 — port of glp_runtime/lib/link/
//// reliability/resource_snapshot.dart, mirror csharp/glp_link/reliability/
//// ResourceSnapshot.cs).
////
//// The global-writers table (`W_p`) entries, the global-send registry goals, the heap
//// `onBind` callbacks, and the request/reply table entries. The reclamation probe
//// (SC-014) asserts that after N `permFail`s every counter returns to its pre-link
//// baseline — no leak on peer death (FR-024, SC-014).
////
//// GLEAM MAPPING NOTE: the Dart `IResourceProbe` interface (which reads the live
//// runtime structures) is a runtime-binding concern, not a value; this port is the
//// transport-agnostic SNAPSHOT value the probe reports in. Gleam records have
//// structural equality, so `is_baseline` is a plain `==`.

pub type ResourceSnapshot {
  ResourceSnapshot(
    global_writers: Int,
    send_registry_goals: Int,
    bind_callbacks: Int,
    reply_table_entries: Int,
  )
}

/// The all-zero baseline.
pub fn zero() -> ResourceSnapshot {
  ResourceSnapshot(0, 0, 0, 0)
}

/// True when every counter equals the supplied baseline (reclaimed to baseline).
pub fn is_baseline(snapshot: ResourceSnapshot, baseline: ResourceSnapshot) -> Bool {
  snapshot == baseline
}
