//// glp/link/reliability/cycle_guard — cycle detection for the link send-path term
//// traversal (feature 059, T077 — port of glp_runtime/lib/link/reliability/
//// cycle_guard.dart, mirror csharp/glp_link/reliability/CycleGuard.cs).
////
//// A cyclic term must terminate serialization with a CLEAN error rather than loop
//// forever or overflow the stack (FR-022/028); it is surfaced as a transport fault,
//// never a GLP Fail. The guard is a visited-set over the nodes currently on the active
//// recursion path: entering a node already on the path is a cycle. Sharing a subterm
//// via two parents (a DAG, not a cycle) is permitted — a node is removed from the
//// active set when its scope leaves, so only a node on the CURRENT path triggers the
//// error.
////
//// GLEAM MAPPING NOTE: the Dart guard keys on OBJECT IDENTITY (reference-identity set),
//// the natural mutable-heap analogue. On BEAM the ground-relay serializer walks the GLP
//// heap by CELL ADDRESS (`terms.VarRef(addr)`), so the faithful visited key here is the
//// heap address — a cyclic binding (a writer bound to a struct that reaches it) is
//// exactly a repeated address on the path. Immutable-value threaded: `enter` returns
//// the guard WITH the node added, `leave` removes it.

import gleam/int
import gleam/set.{type Set}

/// A cyclic term was reached during serialization (→ a transport fault, never a Fail).
pub type CyclicTermException {
  CyclicTermException(message: String)
}

pub opaque type CycleGuard {
  CycleGuard(active: Set(Int))
}

/// An empty guard.
pub fn new() -> CycleGuard {
  CycleGuard(active: set.new())
}

/// Enter `node` (a heap cell address) on the active recursion path. `Error` if it is
/// already on the path (a cycle); otherwise the guard with `node` added.
pub fn enter(guard: CycleGuard, node: Int) -> Result(CycleGuard, CyclicTermException) {
  case set.contains(guard.active, node) {
    True ->
      Error(CyclicTermException(
        "cyclic term detected at cell "
        <> int_to_string(node)
        <> " during serialization",
      ))
    False -> Ok(CycleGuard(active: set.insert(guard.active, node)))
  }
}

/// Leave `node` — remove it from the active path (the RAII-scope dispose).
pub fn leave(guard: CycleGuard, node: Int) -> CycleGuard {
  CycleGuard(active: set.delete(guard.active, node))
}

/// Number of nodes currently on the active recursion path.
pub fn depth(guard: CycleGuard) -> Int {
  set.size(guard.active)
}

fn int_to_string(i: Int) -> String {
  int.to_string(i)
}
