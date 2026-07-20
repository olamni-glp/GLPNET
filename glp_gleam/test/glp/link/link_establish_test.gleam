//// T050.C1 — establish-core + registry.
////
//// These tests pin the two properties C1 exists to guarantee:
////   * **R-5 / FR-002** — both establishment paths converge on ONE registry and yield an
////     indistinguishable established link.
////   * **FR-007** — establishment is idempotent at ground link identity: a repeat setup
////     REUSES and never dials a second time.
//// plus the admission gate (fail-closed) and the no-transport case.
////
//// The transport is a fake leaf that records how many times it was opened, because
//// "did we dial twice?" is precisely what idempotency is about and a handle comparison
//// alone cannot see it.

import gleam/erlang/process
import gleam/option.{None}
import gleeunit/should
import glp/link/primitives/capability_gate
import glp/link/primitives/link_establish
import glp/link/primitives/link_registry
import glp/link/primitives/link_runtime
import glp/link/primitives/transport_registry
import glp/link/seam/endpoint.{type Endpoint, Endpoint}
import glp/link/seam/link_address
import glp/link/seam/link_fault.{LinkFaultSignal, Permanent}
import glp/link/seam/link_id.{type LinkId, LinkId, NonceInt, NonceStr}
import glp/link/seam/link_scheme
import glp/link/seam/transport.{type Transport, Transport}

// ── fixtures ────────────────────────────────────────────────────────────────

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

/// A leaf that always succeeds, counting opens in a subject so a test can assert how
/// many times the transport was actually dialled.
fn counting_transport(counter: process.Subject(String)) -> Transport {
  Transport(
    supported_schemes: [link_scheme.tcp()],
    listen: fn(_scheme, _addr, _opts) {
      process.send(counter, "listen")
      Ok(a_dead_endpoint(an_id(1)))
    },
    connect: fn(_scheme, _addr, _opts) {
      process.send(counter, "connect")
      Ok(a_dead_endpoint(an_id(1)))
    },
  )
}

/// A leaf whose every open fails — for the transport-failure path.
fn failing_transport() -> Transport {
  let signal =
    LinkFaultSignal(link: an_id(1), kind: Permanent, reason: "refused")
  Transport(
    supported_schemes: [link_scheme.tcp()],
    listen: fn(_s, _a, _o) { Error(signal) },
    connect: fn(_s, _a, _o) { Error(signal) },
  )
}

fn drain(counter: process.Subject(String), acc: List(String)) -> List(String) {
  case process.receive(counter, 0) {
    Ok(msg) -> drain(counter, [msg, ..acc])
    Error(_) -> acc
  }
}

fn state_with(leaf: Transport) -> link_runtime.LinkState {
  link_runtime.new() |> link_runtime.with_transport(leaf)
}

// ── R-5: both establishment paths converge ──────────────────────────────────

// The headline C1 property. Path A (listener role) establishes; then the path-B shape
// (pre_gated, as `accept_link` arrives) asks for the SAME ground LinkId. It must find
// the existing link and reuse it — one registry, one handle, indistinguishable.
pub fn both_paths_converge_on_one_registry_test() {
  let counter = process.new_subject()
  let state = state_with(counting_transport(counter))
  let ctx = link_runtime.establish_context(state)
  let id = an_id(1)
  let addr = link_address.endpoint("127.0.0.1", 9000)

  // Path A — listen/connect, gated normally.
  let assert Ok(#(registry, first)) =
    link_establish.wire_established_link(
      ctx,
      state.links,
      id,
      link_establish.Listener,
      addr,
      False,
    )
  let assert link_registry.Established(handle_a) = first

  // Path B — request/accept, already gated at its own kernel.
  let assert Ok(#(registry, second)) =
    link_establish.wire_established_link(
      ctx,
      registry,
      id,
      link_establish.Connector,
      addr,
      True,
    )
  let assert link_registry.Reused(handle_b) = second

  // Same handle, one entry, and the transport was dialled EXACTLY ONCE — the second
  // path did not open a competing connection to the same identity.
  handle_b |> should.equal(handle_a)
  link_registry.count(registry) |> should.equal(1)
  drain(counter, []) |> should.equal(["listen"])
}

// ── FR-007: idempotent at link identity ─────────────────────────────────────

pub fn repeat_setup_reuses_and_does_not_redial_test() {
  let counter = process.new_subject()
  let state = state_with(counting_transport(counter))
  let ctx = link_runtime.establish_context(state)
  let id = an_id(1)
  let addr = link_address.endpoint("127.0.0.1", 9000)

  let assert Ok(#(r1, link_registry.Established(_))) =
    link_establish.wire_established_link(
      ctx,
      state.links,
      id,
      link_establish.Connector,
      addr,
      False,
    )
  let assert Ok(#(r2, link_registry.Reused(_))) =
    link_establish.wire_established_link(
      ctx,
      r1,
      id,
      link_establish.Connector,
      addr,
      False,
    )
  let assert Ok(#(r3, link_registry.Reused(_))) =
    link_establish.wire_established_link(
      ctx,
      r2,
      id,
      link_establish.Connector,
      addr,
      False,
    )

  link_registry.count(r3) |> should.equal(1)
  drain(counter, []) |> should.equal(["connect"])
}

// A DIFFERENT identity is a different link — and `link_id` keeps nonce types distinct,
// so `NonceInt(1)` and `NonceStr("1")` must not collide into one entry.
pub fn distinct_identities_are_distinct_links_test() {
  let counter = process.new_subject()
  let state = state_with(counting_transport(counter))
  let ctx = link_runtime.establish_context(state)
  let addr = link_address.endpoint("127.0.0.1", 9000)
  let int_id = an_id(1)
  let str_id = LinkId(..int_id, nonce: NonceStr("1"))

  let assert Ok(#(r1, link_registry.Established(_))) =
    link_establish.wire_established_link(
      ctx,
      state.links,
      int_id,
      link_establish.Connector,
      addr,
      False,
    )
  let assert Ok(#(r2, link_registry.Established(_))) =
    link_establish.wire_established_link(
      ctx,
      r1,
      str_id,
      link_establish.Connector,
      addr,
      False,
    )

  link_registry.count(r2) |> should.equal(2)
}

// ── admission gate ──────────────────────────────────────────────────────────

// A refusal must happen BEFORE the transport is touched: no dial, nothing registered.
pub fn refused_link_opens_no_transport_test() {
  let counter = process.new_subject()
  let deny =
    capability_gate.Gate(evaluate: fn(_r) {
      Ok(capability_gate.Refused("policy says no"))
    })
  let state =
    state_with(counting_transport(counter))
    |> link_runtime.with_gates(capability_gate.new() |> capability_gate.with_default(deny))
  let ctx = link_runtime.establish_context(state)

  let assert Error(link_establish.Refused(_, reason)) =
    link_establish.wire_established_link(
      ctx,
      state.links,
      an_id(1),
      link_establish.Connector,
      link_address.endpoint("127.0.0.1", 9000),
      False,
    )

  reason |> should.equal("policy says no")
  drain(counter, []) |> should.equal([])
  link_registry.count(state.links) |> should.equal(0)
}

// A gate that cannot reach a verdict FAILS CLOSED — it must refuse, never fall open.
pub fn unevaluable_gate_fails_closed_test() {
  let counter = process.new_subject()
  let broken =
    capability_gate.Gate(evaluate: fn(_r) { Error("authority unreachable") })
  let state =
    state_with(counting_transport(counter))
    |> link_runtime.with_gates(
      capability_gate.new() |> capability_gate.with_default(broken),
    )
  let ctx = link_runtime.establish_context(state)

  let assert Error(link_establish.Refused(_, _)) =
    link_establish.wire_established_link(
      ctx,
      state.links,
      an_id(1),
      link_establish.Connector,
      link_address.endpoint("127.0.0.1", 9000),
      False,
    )
  drain(counter, []) |> should.equal([])
}

// Path B is pre-gated: it must NOT be refused by a gate that would deny it, because
// admission already happened at its own kernel.
pub fn pre_gated_path_skips_admission_test() {
  let counter = process.new_subject()
  let deny =
    capability_gate.Gate(evaluate: fn(_r) { Ok(capability_gate.Refused("no")) })
  let state =
    state_with(counting_transport(counter))
    |> link_runtime.with_gates(
      capability_gate.new() |> capability_gate.with_default(deny),
    )
  let ctx = link_runtime.establish_context(state)

  let assert Ok(#(_r, link_registry.Established(_))) =
    link_establish.wire_established_link(
      ctx,
      state.links,
      an_id(1),
      link_establish.Listener,
      link_address.endpoint("127.0.0.1", 9000),
      True,
    )
  drain(counter, []) |> should.equal(["listen"])
}

// ── failure paths ───────────────────────────────────────────────────────────

// A transport that refuses surfaces as TransportFailed and registers nothing — a
// failed dial must not leave a phantom entry behind.
pub fn transport_failure_registers_nothing_test() {
  let state = state_with(failing_transport())
  let ctx = link_runtime.establish_context(state)

  let assert Error(link_establish.TransportFailed(_, signal)) =
    link_establish.wire_established_link(
      ctx,
      state.links,
      an_id(1),
      link_establish.Connector,
      link_address.endpoint("127.0.0.1", 9000),
      False,
    )
  signal.reason |> should.equal("refused")
  link_registry.count(state.links) |> should.equal(0)
}

// `quic` is a reserved scheme token with no leaf (T055 unbuilt). It must report
// NoTransport rather than silently falling back to tcp — a wrong-transport connection
// would masquerade as success.
pub fn unserved_scheme_reports_no_transport_test() {
  let counter = process.new_subject()
  let state = state_with(counting_transport(counter))
  let ctx = link_runtime.establish_context(state)
  let quic_id =
    LinkId(
      scheme: link_scheme.quic(),
      endpoint: link_address.endpoint("127.0.0.1", 9000),
      nonce: NonceInt(1),
    )

  let assert Error(link_establish.NoTransport(
    transport_registry.NoTransportForScheme(_),
  )) =
    link_establish.wire_established_link(
      ctx,
      state.links,
      quic_id,
      link_establish.Connector,
      link_address.endpoint("127.0.0.1", 9000),
      False,
    )
  drain(counter, []) |> should.equal([])
}
