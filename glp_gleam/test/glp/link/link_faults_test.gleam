//// T050.C7 — fault monitor: K6 `'_link_monitor'/2` + the `link_faults` delivery core.
////
//// What C7 guarantees, and what these tests pin:
////   * K6 registers an INDEPENDENT observer cursor on an already-established link and
////     pushes exactly one `ok` — the healthy baseline (025 contracts §2.7) — on the NEW
////     stream only. Monitoring an unestablished link is a non-fatal abort.
////   * Establishment registers its `Faults` stream as a live cursor with NO `ok`: the
////     cell stays lazily unbound until a real fault, so an unmonitored goal stays
////     safely suspended (FR-044).
////   * `deliver_fault` fans one ground term out to EVERY cursor — the establishment
////     stream and each monitor stream see it on their OWN streams (FR-008), and each
////     cursor advances so a second fault lands on the tail.
////   * `from_signal` refines the coarse seam signal to the D-1 lattice: `Closed` →
////     `closed/2`, `Transient` → `tempFail/2`, `Permanent` → `permFail/2`; `ok` is
////     BARE (arity 0) — `architecture-context.md`'s `ok(LinkId)` is superseded.
////   * A pump `Faulted` item reaches the monitor streams through `apply_item`, and
////     does NOT touch the `In` data stream (a fault is never a logical Fail — FR-044;
////     terminal is C8's call).

import gleam/erlang/process
import gleam/option.{None, Some}
import gleeunit/should
import glp/link/primitives/link_faults
import glp/link/primitives/link_kernels
import glp/link/primitives/link_pump
import glp/link/primitives/link_registry
import glp/link/primitives/link_runtime
import glp/link/seam/endpoint.{type Endpoint, Endpoint}
import glp/link/seam/link_address
import glp/link/seam/link_fault.{Closed, LinkFaultSignal, Permanent, Transient}
import glp/link/seam/link_id.{type LinkId, LinkId, NonceInt}
import glp/link/seam/link_scheme
import glp/link/seam/transport.{type Transport, Transport}
import glp/runtime/heap.{type Heap}
import glp/runtime/terms.{
  type Term, ConstAtom, ConstInt, ConstString, ConstTerm, StructTerm, VarRef,
}

// ── fixtures (the C2 shape: a fake always-ok transport) ─────────────────────

fn an_id(nonce: Int) -> LinkId {
  LinkId(
    scheme: link_scheme.tcp(),
    endpoint: link_address.endpoint("127.0.0.1", 9000),
    nonce: NonceInt(nonce),
  )
}

fn a_dead_endpoint(id: LinkId) -> Endpoint {
  Endpoint(
    id: id,
    send: fn(_frame) { Ok(Nil) },
    recv: fn() { Ok(None) },
    close: fn() { Nil },
    faults: process.new_subject(),
  )
}

fn always_ok_transport() -> Transport {
  Transport(
    supported_schemes: [link_scheme.tcp()],
    listen: fn(_scheme, _addr, _opts) { Ok(a_dead_endpoint(an_id(1))) },
    connect: fn(_scheme, _addr, _opts) { Ok(a_dead_endpoint(an_id(1))) },
  )
}

fn tcp_id_term(nonce: Int) -> Term {
  StructTerm("link_id", [
    ConstTerm(ConstAtom("tcp")),
    StructTerm("ep", [
      ConstTerm(ConstString("127.0.0.1")),
      ConstTerm(ConstInt(9000)),
    ]),
    ConstTerm(ConstInt(nonce)),
  ])
}

/// Establish `an_id(nonce)` through K1 over a fresh state; returns the state plus the
/// establishment Faults READER (the program's half) for stream assertions.
fn established(
  nonce: Int,
) -> #(Heap, link_runtime.LinkState, Int) {
  let state = link_runtime.new() |> link_runtime.with_transport(always_ok_transport())
  let #(h, in_w, _) = heap.allocate_variable(heap.new())
  let #(h, _, out_r) = heap.allocate_variable(h)
  let #(h, faults_w, faults_r) = heap.allocate_variable(h)
  let assert Ok(link_kernels.LinkEffect(h, state, _)) =
    link_kernels.link_dispatch(h, state, "_link_setup", 5, [
      tcp_id_term(nonce),
      ConstTerm(ConstAtom("connector")),
      VarRef(in_w),
      VarRef(out_r),
      VarRef(faults_w),
    ])
  #(h, state, faults_r)
}

/// One-level deref: what the cell holds now, or `None` if unbound (a monitor stream is
/// `[term | UnboundTail]` by construction, so a ground resolve would always fail).
fn value_at(heap: Heap, addr: Int) -> option.Option(Term) {
  case heap.deref(heap, addr) {
    Ok(#(_h, heap.Bound(term))) -> Some(term)
    _ -> None
  }
}

// ── from_signal / the D-1 lattice ────────────────────────────────────────────

pub fn from_signal_refines_the_lattice_test() {
  let id = an_id(1)
  let id_term = link_faults.closed(id, "bye")
  let assert StructTerm("closed", [_, ConstTerm(ConstAtom("bye"))]) = id_term

  let assert StructTerm("closed", _) =
    link_faults.from_signal(LinkFaultSignal(id, Closed, "peer closed"))
  let assert StructTerm("tempFail", [_, ConstTerm(ConstAtom("timeout"))]) =
    link_faults.from_signal(LinkFaultSignal(id, Transient, "timeout"))
  let assert StructTerm("permFail", [_, ConstTerm(ConstAtom("cert"))]) =
    link_faults.from_signal(LinkFaultSignal(id, Permanent, "cert"))

  // D-1: ok is BARE — arity 0, no LinkId argument.
  link_faults.ok() |> should.equal(ConstTerm(ConstAtom("ok")))
}

// ── K6: register + ok baseline; independence; unestablished aborts ──────────

/// K6 pushes the healthy baseline on ITS stream, and the establishment `Faults` stream
/// stays UNBOUND — no `ok` is pushed there (FR-044: an unmonitored goal must be able to
/// stay suspended on it forever).
pub fn monitor_pushes_ok_and_establishment_stream_stays_lazy_test() {
  let #(h, state, est_faults_r) = established(1)

  let #(h, mon_w, mon_r) = heap.allocate_variable(h)
  let assert Ok(link_kernels.LinkEffect(h, state, _woken)) =
    link_kernels.link_dispatch(h, state, "_link_monitor", 2, [
      tcp_id_term(1),
      VarRef(mon_w),
    ])

  // The monitor stream: [ok | UnboundTail].
  let assert Some(StructTerm(".", [ConstTerm(ConstAtom("ok")), VarRef(_)])) =
    value_at(h, mon_r)
  // The establishment stream: still unbound.
  value_at(h, est_faults_r) |> should.equal(None)

  // The handle now carries BOTH cursors (establishment + this observer).
  let assert Ok(handle) = link_registry.try_get(state.links, an_id(1))
  case handle.monitor_cursors {
    [_, _] -> True
    _ -> False
  }
  |> should.be_true
}

/// Monitor of a link that was never established is a caller bug: non-fatal abort,
/// nothing registered, nothing bound.
pub fn monitor_of_unestablished_link_aborts_test() {
  let state = link_runtime.new() |> link_runtime.with_transport(always_ok_transport())
  let #(h, mon_w, mon_r) = heap.allocate_variable(heap.new())
  let assert Ok(link_kernels.LinkAbort(_)) =
    link_kernels.link_dispatch(h, state, "_link_monitor", 2, [
      tcp_id_term(9),
      VarRef(mon_w),
    ])
  value_at(h, mon_r) |> should.equal(None)
}

// ── fan-out: every observer, own stream, cursors advance ────────────────────

/// One fault reaches the establishment stream AND the `link_monitor` stream — each on
/// its own cells (FR-008) — and a second fault lands on both TAILS, proving every
/// cursor advanced.
pub fn deliver_fault_fans_to_every_observer_and_advances_test() {
  let #(h, state, est_faults_r) = established(1)
  let #(h, mon_w, mon_r) = heap.allocate_variable(h)
  let assert Ok(link_kernels.LinkEffect(h, state, _)) =
    link_kernels.link_dispatch(h, state, "_link_monitor", 2, [
      tcp_id_term(1),
      VarRef(mon_w),
    ])
  let assert Ok(handle) = link_registry.try_get(state.links, an_id(1))

  let fault1 = link_faults.temp_fail(an_id(1), "reset")
  let #(h, handle, _woken) = link_faults.deliver_fault(h, handle, fault1)

  // Establishment stream head: the fault. Monitor stream: [ok, fault | _].
  let assert Some(StructTerm(".", [f_est, VarRef(_)])) = value_at(h, est_faults_r)
  f_est |> should.equal(fault1)
  let assert Some(StructTerm(".", [ConstTerm(ConstAtom("ok")), VarRef(mon_tail)])) =
    value_at(h, mon_r)
  let assert Some(StructTerm(".", [f_mon, VarRef(_)])) = value_at(h, mon_tail)
  f_mon |> should.equal(fault1)

  // Second fault: both tails extend — the cursors really advanced.
  let fault2 = link_faults.perm_fail(an_id(1), "gone")
  let #(h, _handle, _) = link_faults.deliver_fault(h, handle, fault2)
  let assert Some(StructTerm(".", [_, VarRef(est_tail)])) = value_at(h, est_faults_r)
  let assert Some(StructTerm(".", [f_est2, _])) = value_at(h, est_tail)
  f_est2 |> should.equal(fault2)
}

/// `end_all` closes every monitor stream with `[]` and clears the cursor list, so a
/// later delivery is a no-op — the C8 terminal-close contract.
pub fn end_all_closes_every_stream_and_clears_test() {
  let #(h, state, est_faults_r) = established(1)
  let assert Ok(handle) = link_registry.try_get(state.links, an_id(1))

  let #(h, handle, _) = link_faults.end_all(h, handle)
  value_at(h, est_faults_r)
  |> should.equal(Some(ConstTerm(ConstAtom("nil"))))
  handle.monitor_cursors |> should.equal([])

  // Delivery after end: nothing to deliver to, nothing bound, no crash.
  let #(h2, _, woken) =
    link_faults.deliver_fault(h, handle, link_faults.temp_fail(an_id(1), "late"))
  woken |> should.equal([])
  value_at(h2, est_faults_r) |> should.equal(Some(ConstTerm(ConstAtom("nil"))))
}

// ── the pump path: a Faulted item lands on the monitors, not on In ──────────

/// `apply_item(Faulted)` fans the refined term to the monitor streams and leaves the
/// `In` data stream untouched — a fault never fails or ends the data path (FR-044;
/// terminal is C8's call).
pub fn pump_faulted_reaches_monitors_and_spares_in_test() {
  let #(h, state, est_faults_r) = established(1)
  let assert Ok(handle) = link_registry.try_get(state.links, an_id(1))
  let assert Some(in_w) = handle.in_writer
  let in_r = heap.paired_reader(h, in_w)

  let fault = link_faults.from_signal(LinkFaultSignal(an_id(1), Transient, "reset"))
  let link_pump.Applied(h, links, _woken) =
    link_pump.apply_item(h, state.links, link_pump.Faulted(an_id(1), fault))

  let assert Some(StructTerm(".", [f, VarRef(_)])) = value_at(h, est_faults_r)
  f |> should.equal(fault)
  // In: still unbound — data path untouched.
  value_at(h, in_r) |> should.equal(None)
  // The cursor-advanced handle went back into the registry.
  let assert Ok(after) = link_registry.try_get(links, an_id(1))
  { after.monitor_cursors == handle.monitor_cursors } |> should.be_false
}
