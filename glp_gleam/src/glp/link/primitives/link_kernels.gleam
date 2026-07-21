//// glp/link/primitives/link_kernels — the link-layer effectful kernels + the parallel
//// effect seam `LinkOutcome` (feature 050, T050.C2).
////
//// Mirrors `glp/mad/mad_kernels` exactly. The pure engine's body kernels are
//// `kernels.KernelOutcome` over the HEAP alone; the link kernels are effectful over the
//// per-engine `LinkState` (`link_runtime`), so they get a PARALLEL outcome type
//// (`LinkOutcome`) rather than a widened `KernelOutcome` — the same call the operator
//// ratified for madGLP's `_send` (E5, 2026-07-14), for the same reason (widening
//// `KernelOutcome` touches ~30 dispatch arms + runner + scheduler; see
//// `contracts/link-primitives-port.md` §3/§5 D-5). The runner dispatches these at the
//// BODY Spawn label-miss, AFTER the pure `kernels.dispatch` misses and only when the
//// reduction carries a `LinkState` (`runner.link_spawn`).
////
//// **C2 scope: ONLY K1 `'_link_setup'/5`.** The other six ratified kernels
//// (`_link_send` / `_link_request` / `_link_listen` / `_link_accept` / `_link_monitor` /
//// `_link_close`) land in C3–C8; `link_is_kernel` and `link_dispatch` grow with each step
//// and are kept in lockstep, so a recognized kernel always has a dispatch arm.
////
//// The K1 arm is the whole of `'_link_setup'/5` (self.glp:475/489-492): ground-resolve +
//// parse the `LinkId` and the `LinkRole` (the ground gate — the wrappers already guarded
//// `ground/1`, so a non-ground here is an upstream invariant break, surfaced not
//// defaulted), converge on `link_establish.wire_established_link` — the ONE funnel that
//// produces an established link (R-5) — and record the `In`/`Out`/`Faults` heap stream
//// cursors on the handle. Arming the egress drainer (C5) and starting the ingress pump
//// (C6) come later; C2 establishes the link and wires the cursors those steps consume.

import gleam/result
import gleam/string
import glp/link/primitives/link_establish
import glp/link/primitives/link_handle
import glp/link/primitives/link_registry.{Established, Reused}
import glp/link/primitives/link_runtime.{type LinkState}
import glp/link/primitives/link_terms
import glp/runtime/heap.{type Heap}
import glp/runtime/suspension.{type GoalRef}
import glp/runtime/terms.{type Term, VarRef}

/// The two-valued outcome of a link effectful kernel — parallel to
/// `kernels.KernelOutcome` and `mad_kernels.MadOutcome`, but over `LinkState`.
pub type LinkOutcome {
  /// Success: the (possibly heap-updated) `LinkState` + any reactivated goals.
  LinkEffect(heap: Heap, state: LinkState, woken: List(GoalRef))
  /// A non-fatal abort — the enclosing goal FAILS, the engine keeps running (NOT the
  /// fatal `KAbort → RunnerError` path). Mirrors `MadAbort` / spec-v5.3-PURE.
  LinkAbort(detail: String)
}

/// Is `name/arity` a link effectful kernel this slice implements? C2 = `_link_setup/5`.
pub fn link_is_kernel(name: String, arity: Int) -> Bool {
  case name, arity {
    "_link_setup", 5 -> True
    _, _ -> False
  }
}

/// Dispatch a link effectful kernel over `args`, given the current `LinkState`.
/// `Error(Nil)` means `name/arity` is not a link kernel this slice implements (the runner
/// then falls through to the unresolved-Spawn report, exactly as for a missed mad kernel).
pub fn link_dispatch(
  heap: Heap,
  state: LinkState,
  name: String,
  arity: Int,
  args: List(Term),
) -> Result(LinkOutcome, Nil) {
  case name, arity, args {
    "_link_setup", 5, [link_id, role, in_arg, out_arg, faults_arg] ->
      Ok(link_setup_kernel(
        heap,
        state,
        link_id,
        role,
        in_arg,
        out_arg,
        faults_arg,
      ))
    _, _, _ -> Error(Nil)
  }
}

/// `'_link_setup'(LinkId?, Role?, In, Out?, Faults)` — K1 (self.glp:475/489-492).
/// path-A establish-or-reuse, idempotent at ground identity (FR-007). See module header.
fn link_setup_kernel(
  heap: Heap,
  state: LinkState,
  link_id_arg: Term,
  role_arg: Term,
  in_arg: Term,
  out_arg: Term,
  faults_arg: Term,
) -> LinkOutcome {
  case
    try_link_setup(heap, state, link_id_arg, role_arg, in_arg, out_arg, faults_arg)
  {
    Ok(outcome) -> outcome
    Error(detail) -> LinkAbort(detail)
  }
}

fn try_link_setup(
  heap: Heap,
  state: LinkState,
  link_id_arg: Term,
  role_arg: Term,
  in_arg: Term,
  out_arg: Term,
  faults_arg: Term,
) -> Result(LinkOutcome, String) {
  // 1. Ground-resolve + parse the LinkId and the Role (the ground gate).
  use #(heap, id_term) <- result.try(resolve(heap, link_id_arg, "LinkId"))
  use id <- result.try(link_terms.parse_link_id(id_term) |> term_err("LinkId"))
  use #(heap, role_term) <- result.try(resolve(heap, role_arg, "Role"))
  use role <- result.try(link_terms.parse_role(role_term) |> term_err("Role"))
  // 2. The In/Out/Faults heap stream cursors are the arg writer/reader addresses.
  use in_addr <- result.try(cursor_addr(in_arg, "In"))
  use out_addr <- result.try(cursor_addr(out_arg, "Out"))
  use faults_addr <- result.try(cursor_addr(faults_arg, "Faults"))
  // 3. Converge on the ONE establish funnel (path A → not pre-gated). The address is the
  //    LinkId's own endpoint — identity and address are the same ground fact here.
  let ctx = link_runtime.establish_context(state)
  case
    link_establish.wire_established_link(
      ctx,
      state.links,
      id,
      role,
      id.endpoint,
      False,
    )
  {
    Error(e) -> Error("_link_setup: establishment failed: " <> string.inspect(e))
    Ok(#(registry, Established(handle))) -> {
      // First establishment: record the cursors the pump / egress / faults steps
      // (C5–C7) consume. The writers stay unbound here — extending `In`/`Faults` and
      // draining `Out` is those steps' work, not establishment's.
      let wired =
        link_handle.with_cursors(handle, in_addr, out_addr, faults_addr)
      let state = link_runtime.with_links(state, link_registry.put(registry, wired))
      Ok(LinkEffect(heap, state, []))
    }
    Ok(#(registry, Reused(_handle))) -> {
      // Idempotent reuse (FR-007): do NOT re-wire cursors or re-open — the existing
      // handle already drives the peer's frames. The reuse data semantics for a second
      // owner's In/Out/Faults are a C5/C6 + T052 concern, not establishment's.
      let state = link_runtime.with_links(state, registry)
      Ok(LinkEffect(heap, state, []))
    }
  }
}

/// Deep-dereference an argument to a ground `VarRef`-free term, mapping the failure to a
/// kernel-abort detail. Threads the heap (deref path-compresses).
fn resolve(heap: Heap, term: Term, what: String) -> Result(#(Heap, Term), String) {
  link_terms.ground_resolve(heap, term)
  |> result.map_error(fn(e) {
    "_link_setup: " <> what <> " not ground: " <> string.inspect(e)
  })
}

fn term_err(r: Result(a, e), what: String) -> Result(a, String) {
  result.map_error(r, fn(e) {
    "_link_setup: bad " <> what <> ": " <> string.inspect(e)
  })
}

/// The heap cursor for a stream argument is its `VarRef` address. A non-`VarRef` here
/// means the wrapper passed something other than an unbound stream variable — surfaced.
fn cursor_addr(term: Term, what: String) -> Result(Int, String) {
  case term {
    VarRef(addr) -> Ok(addr)
    _ ->
      Error(
        "_link_setup: "
        <> what
        <> " must be an unbound stream variable, got "
        <> string.inspect(term),
      )
  }
}
