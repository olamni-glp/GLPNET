//// glp/runtime/suspension — shared suspension records + the activation hand-off.
////
//// F4 owns suspension STORAGE + activation-list PRODUCTION only — it never
//// consumes/schedules the goals (that is the F5 runner). `armed` mirrors the Dart
//// `armed`/`disarm()` guard against double-activation (data-model §4 / R-007).
////
//// Feature 050 (T010) extends this module — additively, 034 surface unchanged —
//// with the engine-level generation-scoped suspension table (data-model.md
//// §SuspensionRecord): unbound writer id → set of (goal_id, generation) waiters,
//// consumed atomically on writer binding so no waiter can be woken twice
//// (FR-005 pairs this with the run-queue dedup in glp/engine/types).
////
//// Dart source of truth: glp_runtime/lib/runtime/suspension.dart

import gleam/dict.{type Dict}
import gleam/set.{type Set}

/// A suspended goal recorded on an unbound writer. `armed` collapses Dart's nullable
/// `goalId` (null == disarmed): an armed record can activate exactly once.
pub type Suspension {
  Suspension(goal_id: Int, resume_pc: Int, armed: Bool)
}

/// The activation hand-off to the future F5 scheduler (← machine_state.dart GoalRef).
pub type GoalRef {
  GoalRef(goal_id: Int, resume_pc: Int)
}

// ==============================================================================
// Generation-scoped suspension table (050 · T010)
// ==============================================================================

/// One generation-scoped waiter: the (goal identity, suspension generation) pair
/// that keys reactivation dedup (FR-005).
pub type Waiter {
  Waiter(goal_id: Int, generation: Int)
}

/// The engine-level suspension table: unbound writer id → the set of waiters
/// suspended on it. Immutable — every operation returns a new table.
pub opaque type SuspensionTable {
  SuspensionTable(by_writer: Dict(Int, Set(Waiter)))
}

/// An empty table.
pub fn new_table() -> SuspensionTable {
  SuspensionTable(by_writer: dict.new())
}

/// Record `waiter` as suspended on `writer`. Set semantics: recording the same
/// (goal_id, generation) twice on one writer stores one waiter (a
/// multi-variable suspension registers on several writers, not several times
/// on one).
pub fn suspend(
  table: SuspensionTable,
  writer: Int,
  waiter: Waiter,
) -> SuspensionTable {
  let waiters = case dict.get(table.by_writer, writer) {
    Ok(existing) -> set.insert(existing, waiter)
    Error(Nil) -> set.insert(set.new(), waiter)
  }
  SuspensionTable(by_writer: dict.insert(table.by_writer, writer, waiters))
}

/// Atomically consume the entry for `writer` on binding: the waiters become
/// reactivation candidates and the entry is REMOVED in the same step, so a
/// second consume of the same writer yields the empty set — no double-wake.
pub fn consume(
  table: SuspensionTable,
  writer: Int,
) -> #(SuspensionTable, Set(Waiter)) {
  case dict.get(table.by_writer, writer) {
    Ok(waiters) -> #(
      SuspensionTable(by_writer: dict.delete(table.by_writer, writer)),
      waiters,
    )
    Error(Nil) -> #(table, set.new())
  }
}

/// The waiters currently suspended on `writer` (inspection only — does not
/// consume).
pub fn waiters_on(table: SuspensionTable, writer: Int) -> Set(Waiter) {
  case dict.get(table.by_writer, writer) {
    Ok(waiters) -> waiters
    Error(Nil) -> set.new()
  }
}
