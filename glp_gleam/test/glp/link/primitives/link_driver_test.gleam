//// Tests for glp/link/primitives/link_driver (T075) — establishment-failure
//// decoration: a transport rendezvous that cannot be established surfaces a `permFail`
//// on the establishment Faults monitor (FR-044), NOT a fatal abort — the fault is
//// ordinary bound data.

import gleeunit/should
import glp/link/primitives/link_driver.{LinkAbort, LinkEffect}
import glp/link/primitives/link_registry
import glp/link/primitives/link_runtime
import glp/link/seam/link_address
import glp/link/seam/link_id.{LinkId, NonceInt}
import glp/link/seam/link_scheme
import glp/runtime/heap.{Bound}
import glp/runtime/terms.{
  ConstAtom, ConstInt, ConstString, ConstTerm, StructTerm, VarRef,
}

/// Build a heap with the three `_link_setup` output cells (In writer, Out reader,
/// Faults writer) and return their VarRef terms + the heap.
fn setup_cells() {
  let #(h, in_w, _in_r) = heap.allocate_variable(heap.new())
  let #(h, _out_w, out_r) = heap.allocate_variable(h)
  let #(h, faults_w, _faults_r) = heap.allocate_variable(h)
  #(h, VarRef(in_w), VarRef(out_r), VarRef(faults_w), faults_w)
}

fn tcp_link_id_term() {
  StructTerm("link_id", [
    ConstTerm(ConstString("tcp")),
    StructTerm("ep", [ConstTerm(ConstString("127.0.0.1")), ConstTerm(ConstInt(9100))]),
    ConstTerm(ConstInt(1)),
  ])
}

pub fn establishment_failure_decorates_the_monitor_test() {
  // A runtime with NO transport registered for `tcp` → establishment fails.
  let runtime = link_runtime.new()
  let #(heap, in_t, out_t, faults_t, faults_w) = setup_cells()
  let outcome =
    link_driver.dispatch(heap, runtime, "_link_setup", 5, [
      tcp_link_id_term(),
      ConstTerm(ConstAtom("listener")),
      in_t,
      out_t,
      faults_t,
    ])

  // FR-044: NOT a fatal abort — the kernel proceeds with a faulted, registered link.
  case outcome {
    LinkEffect(heap, runtime, _woken) -> {
      // The link is registered (as a closed/faulted handle).
      let id =
        LinkId(
          link_scheme.tcp(),
          link_address.endpoint("127.0.0.1", 9100),
          NonceInt(1),
        )
      link_registry.contains(runtime.links, id) |> should.be_true
      // The Faults monitor carries a `permFail(...)` head (a bound ground term).
      case heap.deref(heap, faults_w) {
        Ok(#(_h, Bound(StructTerm("permFail", _)))) -> should.be_true(True)
        Ok(#(_h, Bound(StructTerm(".", [StructTerm("permFail", _), _])))) ->
          should.be_true(True)
        _ -> should.be_true(False)
      }
    }
    LinkAbort(_) -> should.be_true(False)
  }
}
