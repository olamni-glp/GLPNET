//// US4 link-primitives tests (wave-3, T032–T038, T040–T042): the amended
//// contracts/link-handshake.md mechanisms end-to-end over loopback + TCP —
//// gated establishment (either role), ordered ship/pump exchange,
//// fragmentation reassembly, version-mismatch and partial-frame rejection,
//// peer close, bounded peer-loss classification, and seam reachability of the
//// unproven schemes.

import gleam/erlang/process
import gleam/option.{None, Some}
import gleam/string
import gleeunit/should
import glp/link/reliability/frame_codec
import glp/link/primitives/capability_gate
import glp/link/primitives/link_establish.{Connector, Listener}
import glp/link/primitives/link_egress
import glp/link/primitives/link_faults
import glp/link/primitives/link_handle.{type LinkHandle}
import glp/link/primitives/link_pump.{Delivered, PeerClosed, PumpFault}
import glp/link/primitives/link_registry
import glp/link/seam/link_address
import glp/link/seam/link_fault.{Closed, Permanent, Transient}
import glp/link/seam/link_id.{LinkId, NonceStr}
import glp/link/seam/link_options.{type LinkOptions, LinkOptions}
import glp/link/seam/link_scheme
import glp/link/seam/transport
import glp/link/transports/loopback
import glp/link/transports/tcp
import glp/link/transports/zmq

/// Establish a gated loopback pair (Listener in the test process, Connector
/// spawned — either side may initiate, rule 1).
fn establish_pair(channel: String, opts: LinkOptions) -> #(LinkHandle, LinkHandle) {
  let t = loopback.new()
  let addr = link_address.path(channel)
  let gates = capability_gate.new()
  let back = process.new_subject()
  process.spawn(fn() {
    let result =
      link_establish.establish(
        link_registry.new(),
        gates,
        t,
        Connector,
        link_scheme.loopback(),
        addr,
        opts,
      )
    process.send(back, result)
  })
  let assert Ok(#(_registry, server)) =
    link_establish.establish(
      link_registry.new(),
      gates,
      t,
      Listener,
      link_scheme.loopback(),
      addr,
      opts,
    )
  let assert Ok(Ok(#(_registry2, client))) = process.receive(back, 5000)
  #(server, client)
}

// ── FR-020/FR-021: gated establishment + ordered exchange both directions ────

pub fn establish_and_exchange_both_directions_test() {
  let #(server, client) = establish_pair("prim-both", link_options.default())

  let assert Ok(client) = link_egress.ship(client, <<1, 2, 3>>)
  let #(server, event) = link_pump.pump_once(server)
  event |> should.equal(Delivered([<<1, 2, 3>>]))

  let assert Ok(_server) = link_egress.ship(server, <<9, 8>>)
  let #(_client, event2) = link_pump.pump_once(client)
  event2 |> should.equal(Delivered([<<9, 8>>]))
}

pub fn ship_order_preserved_test() {
  let #(server, client) = establish_pair("prim-fifo", link_options.default())
  let assert Ok(client) = link_egress.ship(client, <<0xA1>>)
  let assert Ok(client) = link_egress.ship(client, <<0xB2>>)
  let assert Ok(_client) = link_egress.ship(client, <<0xC3>>)
  let #(server, e1) = link_pump.pump_once(server)
  let #(server, e2) = link_pump.pump_once(server)
  let #(_server, e3) = link_pump.pump_once(server)
  e1 |> should.equal(Delivered([<<0xA1>>]))
  e2 |> should.equal(Delivered([<<0xB2>>]))
  e3 |> should.equal(Delivered([<<0xC3>>]))
}

// ── FR-021/rule 5: a fragmented payload reassembles; partials never surface ──

pub fn fragmented_payload_reassembles_test() {
  // MTU 40 = 22-byte header + 18-byte chunks → a 60-byte payload fragments.
  let opts =
    LinkOptions(..link_options.default(), max_frame_bytes: Some(40))
  let #(server, client) = establish_pair("prim-frag", opts)
  let payload = <<
    0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20,
    21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39,
    40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58,
    59,
  >>
  let assert Ok(_client) = link_egress.ship(client, payload)
  let delivered = pump_until_delivery(server, 8)
  delivered |> should.equal([payload])
}

fn pump_until_delivery(handle: LinkHandle, budget: Int) -> List(BitArray) {
  case budget {
    0 -> panic as "fragmented payload never completed"
    _ -> {
      let #(handle, event) = link_pump.pump_once(handle)
      case event {
        Delivered([]) -> pump_until_delivery(handle, budget - 1)
        Delivered(payloads) -> payloads
        other ->
          panic as {
            "expected fragment delivery, got a terminal pump event: "
            <> case other {
              PeerClosed -> "PeerClosed"
              _ -> "PumpFault"
            }
          }
      }
    }
  }
}

// ── T033/T034: a denying gate refuses BEFORE the transport opens ─────────────

pub fn deny_gate_refuses_before_transport_opens_test() {
  let t = loopback.new()
  let gates =
    capability_gate.new()
    |> capability_gate.register(
      link_scheme.loopback(),
      capability_gate.CapabilityGate(gate_establish: fn(_id) { False }),
    )
  // No peer is spawned: if the gate did NOT run before the rendezvous, this
  // listen would block until the connect timeout. The immediate reasoned
  // refusal proves verify-before-act ordering.
  let result =
    link_establish.establish(
      link_registry.new(),
      gates,
      t,
      Listener,
      link_scheme.loopback(),
      link_address.path("prim-denied"),
      link_options.default(),
    )
  let assert Error(signal) = result
  signal.kind |> should.equal(Permanent)
  let link_fault.LinkFaultSignal(_, _, reason) = signal
  reason |> string_contains("capability refused")
}

// ── rules 2/5: nonconforming frames are rejected, never delivered ────────────

pub fn version_mismatch_frame_rejected_test() {
  let #(server, client) = establish_pair("prim-badver", link_options.default())
  // A valid frame with its version byte forged: rejected before any parse.
  let assert Ok([frame]) = frame_codec.encode(<<1, 2, 3>>, 0, None)
  let assert <<_version:8, rest:bits>> = frame
  let bad = <<0x7F, rest:bits>>
  let assert Ok(Nil) = client.endpoint.send(bad)
  let #(_server, event) = link_pump.pump_once(server)
  let assert PumpFault(signal) = event
  signal.kind |> should.equal(Transient)
}

pub fn truncated_frame_never_delivered_test() {
  let #(server, client) = establish_pair("prim-trunc", link_options.default())
  // Shorter than the fixed header: discarded with a fault, never surfaced.
  let assert Ok(Nil) = client.endpoint.send(<<0x01, 0x00, 0x01>>)
  let #(_server, event) = link_pump.pump_once(server)
  let assert PumpFault(_) = event
}

// ── bilateral close: the peer's end-of-stream surfaces cleanly ───────────────

pub fn peer_close_surfaces_test() {
  let #(server, client) = establish_pair("prim-close", link_options.default())
  client.endpoint.close()
  let #(server, event) = link_pump.pump_once(server)
  event |> should.equal(PeerClosed)
  server.closed |> should.be_true
}

// ── T038 / rule 6: bounded peer-loss classification (≤ 30 s) ─────────────────

pub fn silence_classification_bounds_test() {
  let opts = link_options.default()
  let id =
    LinkId(
      link_scheme.loopback(),
      link_address.path("prim-silence"),
      NonceStr("t"),
    )
  link_faults.classify_silence(id, 1000, opts)
  |> should.equal(link_faults.FaultOk)
  let assert link_faults.FaultTempFail(_, _) =
    link_faults.classify_silence(id, 5000, opts)
  let assert link_faults.FaultTempFail(_, _) =
    link_faults.classify_silence(id, 29_999, opts)
  // The default permanent bound IS the contract's 30 s ceiling.
  let assert link_faults.FaultPermFail(_, _) =
    link_faults.classify_silence(id, 30_000, opts)
}

pub fn refine_lattice_test() {
  let id =
    LinkId(
      link_scheme.loopback(),
      link_address.path("prim-refine"),
      NonceStr("t"),
    )
  let assert link_faults.FaultClosed(_, "eos") =
    link_faults.refine(link_fault.LinkFaultSignal(id, Closed, "eos"))
  let assert link_faults.FaultTempFail(_, _) =
    link_faults.refine(link_fault.LinkFaultSignal(id, Transient, "reset"))
  let assert link_faults.FaultPermFail(_, _) =
    link_faults.refine(link_fault.LinkFaultSignal(id, Permanent, "refused"))
}

// ── T041: the same primitives over real TCP sockets ──────────────────────────

pub fn tcp_establish_and_exchange_test() {
  let t = tcp.new()
  let addr = link_address.endpoint("127.0.0.1", 34_741)
  let opts = link_options.default()
  let gates = capability_gate.new()
  let back = process.new_subject()
  process.spawn(fn() {
    let result =
      link_establish.establish(
        link_registry.new(),
        gates,
        t,
        Connector,
        link_scheme.tcp(),
        addr,
        opts,
      )
    case result {
      Ok(#(_registry, client)) -> {
        let shipped = link_egress.ship(client, <<42, 43>>)
        process.send(back, shipped)
      }
      Error(_) -> Nil
    }
  })
  let assert Ok(#(_registry, server)) =
    link_establish.establish(
      link_registry.new(),
      gates,
      t,
      Listener,
      link_scheme.tcp(),
      addr,
      opts,
    )
  let assert Ok(Ok(_client)) = process.receive(back, 5000)
  let #(_server, event) = link_pump.pump_once(server)
  event |> should.equal(Delivered([<<42, 43>>]))
}

// ── T042 / FR-025: unproven schemes stay reachable through the seam ──────────

pub fn unproven_schemes_reachable_through_seam_test() {
  // Scheme tokens construct (normalized) without link-layer changes.
  link_scheme.name(link_scheme.quic()) |> should.equal("quic")
  link_scheme.name(link_scheme.zmq()) |> should.equal("zmq")
  link_scheme.name(link_scheme.of("WS")) |> should.equal("ws")
  // The zmq transport leaf CONSTRUCTS through the same seam shape (not merely
  // selectable): a real Transport value serving its scheme.
  let z = zmq.new()
  transport.serves(z, link_scheme.zmq()) |> should.be_true
  transport.serves(z, link_scheme.tcp()) |> should.be_false
}

// ── registry idempotency (FR-007-025) ────────────────────────────────────────

pub fn registry_idempotency_test() {
  let #(server, _client) = establish_pair("prim-idem", link_options.default())
  let registry = link_registry.new() |> link_registry.put(server)
  link_registry.contains(registry, server.id) |> should.be_true
  let registry = link_registry.remove(registry, server.id)
  link_registry.contains(registry, server.id) |> should.be_false
}

// ── helpers ──────────────────────────────────────────────────────────────────

fn string_contains(haystack: String, needle: String) -> Nil {
  case string.contains(haystack, needle) {
    True -> Nil
    False -> panic as { "expected '" <> needle <> "' in: " <> haystack }
  }
}
