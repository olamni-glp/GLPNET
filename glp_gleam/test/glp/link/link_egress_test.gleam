//// T050.C5 — the `'_link_send'/3` (K2) kernel arm + `link_egress.ship_ground`.
////
//// C5 delivers the LinkId-keyed sender face (`out_relay/3` → K2) and the one
//// ground-relay ship routine both sender faces converge on. The channel-face Out-drainer
//// (deviation D-2) is NOT here — it is escalated, see the C5 commit message.
////
//// What these tests pin:
////   * a ground term is serialized, framed and written to the endpoint, and what lands
////     on the wire round-trips back to exactly the term that was sent (byte-level, via
////     the same shipped 038 codec the C4 handshake token uses);
////   * the outbound sequence number is monotone across sends and threaded back into the
////     registry — the bug a "handle mutated but not stored" port would have;
////   * the ground-relay gate (FR-010 / deviation D-4): a term carrying an unbound cell
////     FAILS the goal and puts NOTHING on the wire — no partial frame, no placeholder;
////   * a rejected term does not burn a sequence number (no hole on the wire);
////   * "send before setup" is a non-fatal abort, not an invented suspend;
////   * a transport refusal is surfaced with the failing fragment index, never swallowed;
////   * base MTU (`max_frame_bytes: None`) ships one `Whole` frame — D-8's assumption
////     that the base needs no reassembler on the far side.
////
//// The transport is a capturing fake: a real loopback would need a live peer, and "what
//// exactly went on the wire?" is precisely what C5 has to observe.

import gleam/erlang/process
import gleam/list
import gleam/option.{None}
import gleeunit/should
import glp/link/primitives/link_egress
import glp/link/primitives/link_handle
import glp/link/primitives/link_kernels
import glp/link/primitives/link_registry
import glp/link/primitives/link_runtime
import glp/link/primitives/link_wire
import glp/link/reliability/frame_codec
import glp/link/seam/endpoint.{type Endpoint, Endpoint}
import glp/link/seam/link_address
import glp/link/seam/link_fault
import glp/link/seam/link_id.{type LinkId, LinkId, NonceInt}
import glp/link/seam/link_options
import glp/link/seam/link_scheme
import glp/runtime/heap
import glp/runtime/terms.{
  type Term, ConstAtom, ConstInt, ConstString, ConstTerm, StructTerm, VarRef,
}

// ── fixtures ─────────────────────────────────────────────────────────────────

fn an_id(nonce: Int) -> LinkId {
  LinkId(
    scheme: link_scheme.tcp(),
    endpoint: link_address.endpoint("127.0.0.1", 9000),
    nonce: NonceInt(nonce),
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

/// An endpoint that captures every frame handed to `send` on a Subject, so a test can
/// read the actual wire bytes back.
fn capturing_endpoint(
  id: LinkId,
  wire: process.Subject(BitArray),
) -> Endpoint {
  Endpoint(
    id: id,
    send: fn(frame) {
      process.send(wire, frame)
      Ok(Nil)
    },
    recv: fn() { Ok(None) },
    close: fn() { Nil },
    faults: process.new_subject(),
  )
}

/// An endpoint whose `send` always refuses — the transport-fault path.
fn refusing_endpoint(id: LinkId) -> Endpoint {
  Endpoint(
    id: id,
    send: fn(_frame) {
      Error(link_fault.LinkFaultSignal(
        link: id,
        kind: link_fault.Transient,
        reason: "peer gone",
      ))
    },
    recv: fn() { Ok(None) },
    close: fn() { Nil },
    faults: process.new_subject(),
  )
}

fn drain(wire: process.Subject(BitArray), acc: List(BitArray)) -> List(BitArray) {
  case process.receive(wire, 0) {
    Ok(frame) -> drain(wire, [frame, ..acc])
    Error(_) -> list.reverse(acc)
  }
}

/// A LinkState whose registry already holds one established link on `ep` — C5 tests the
/// SEND path, so establishment is a precondition, not the subject.
fn state_with_link(
  nonce: Int,
  ep: Endpoint,
) -> link_runtime.LinkState {
  let handle = link_handle.new(an_id(nonce), ep, link_options.default())
  let state = link_runtime.new()
  link_runtime.with_links(state, link_registry.put(state.links, handle))
}

fn a_message() -> Term {
  StructTerm("hello", [ConstTerm(ConstString("world")), ConstTerm(ConstInt(42))])
}

// ── ship_ground: the wire ────────────────────────────────────────────────────

// A ground term ships as ONE Whole frame whose payload decodes back to exactly the
// term that was sent. Round-trip through the shipped 038 codec, not a re-implementation.
pub fn ship_ground_round_trips_the_term_test() {
  let wire = process.new_subject()
  let ep = capturing_endpoint(an_id(1), wire)
  let handle = link_handle.new(an_id(1), ep, link_options.default())

  let assert Ok(#(_heap, advanced)) =
    link_egress.ship_ground(heap.new(), handle, a_message())

  // Exactly one frame on the wire (base MTU = None → single Whole frame, D-8).
  let assert [frame] = drain(wire, [])
  let assert Ok(parsed) = frame_codec.parse_frame(frame)
  parsed.kind |> should.equal(frame_codec.Whole)

  // And it decodes back to the term we sent.
  let assert Ok(decoded) = link_wire.decode_token(frame)
  decoded |> should.equal(a_message())

  // The sequence number was consumed.
  advanced.next_seq |> should.equal(1)
}

// The outbound sequence is monotone and starts at 0 — the frame message id carries it.
pub fn ship_ground_sequences_monotonically_test() {
  let wire = process.new_subject()
  let ep = capturing_endpoint(an_id(1), wire)
  let h0 = link_handle.new(an_id(1), ep, link_options.default())

  let assert Ok(#(_, h1)) = link_egress.ship_ground(heap.new(), h0, a_message())
  let assert Ok(#(_, h2)) = link_egress.ship_ground(heap.new(), h1, a_message())
  let assert Ok(#(_, h3)) = link_egress.ship_ground(heap.new(), h2, a_message())

  h3.next_seq |> should.equal(3)

  let ids =
    drain(wire, [])
    |> list.map(fn(f) {
      let assert Ok(p) = frame_codec.parse_frame(f)
      p.message_id
    })
  ids |> should.equal([0, 1, 2])
}

// FR-010 / deviation D-4 — the ground-relay gate. An unbound cell anywhere in the term
// FAILS and puts NOTHING on the wire: no partial frame, no `_w`/`_r` placeholder.
pub fn ship_ground_refuses_a_non_ground_term_test() {
  let wire = process.new_subject()
  let ep = capturing_endpoint(an_id(1), wire)
  let handle = link_handle.new(an_id(1), ep, link_options.default())

  // A struct whose second argument is an unbound variable.
  let #(h, w, _r) = heap.allocate_variable(heap.new())
  let open_term =
    StructTerm("hello", [ConstTerm(ConstString("world")), VarRef(w)])

  let assert Error(link_egress.NotGround(_)) =
    link_egress.ship_ground(h, handle, open_term)

  // Nothing crossed the seam.
  drain(wire, []) |> should.equal([])
}

// A refused term must not burn a sequence number — otherwise the peer sees a hole.
pub fn a_refused_term_does_not_consume_a_sequence_number_test() {
  let wire = process.new_subject()
  let ep = capturing_endpoint(an_id(1), wire)
  let h0 = link_handle.new(an_id(1), ep, link_options.default())

  let #(h, w, _r) = heap.allocate_variable(heap.new())
  let assert Error(_) =
    link_egress.ship_ground(h, h0, StructTerm("bad", [VarRef(w)]))

  // The next GOOD send is still message 0.
  let assert Ok(#(_, h1)) = link_egress.ship_ground(heap.new(), h0, a_message())
  h1.next_seq |> should.equal(1)
  let assert [frame] = drain(wire, [])
  let assert Ok(p) = frame_codec.parse_frame(frame)
  p.message_id |> should.equal(0)
}

// A transport refusal is surfaced with the failing fragment index, never swallowed.
pub fn a_transport_refusal_is_surfaced_test() {
  let handle =
    link_handle.new(an_id(1), refusing_endpoint(an_id(1)), link_options.default())

  let assert Error(link_egress.Transport(0, sig)) =
    link_egress.ship_ground(heap.new(), handle, a_message())
  sig.kind |> should.equal(link_fault.Transient)
}

// Base MTU ships one frame — the far side needs no reassembler in base scope (D-8).
pub fn base_mtu_ships_a_single_frame_test() {
  let wire = process.new_subject()
  let handle =
    link_handle.new(an_id(1), capturing_endpoint(an_id(1), wire), link_options.default())

  link_egress.mtu(handle) |> should.equal(None)
  link_egress.fragment_count(heap.new(), handle, a_message())
  |> should.equal(Ok(1))
}

// ── K2 `'_link_send'/3` through the kernel dispatch ──────────────────────────

// The kernel ships the term and threads the ADVANCED handle back into the registry —
// the bug a port that mutated a local copy would have.
pub fn link_send_ships_and_threads_the_handle_back_test() {
  let wire = process.new_subject()
  let state = state_with_link(1, capturing_endpoint(an_id(1), wire))

  let args = [
    a_message(),
    tcp_id_term(1),
    ConstTerm(ConstAtom("peer_b")),
  ]

  let assert Ok(link_kernels.LinkEffect(_h, s1, [])) =
    link_kernels.link_dispatch(heap.new(), state, "_link_send", 3, args)

  // One frame out, and the registry's handle advanced.
  let assert [_frame] = drain(wire, [])
  let assert Ok(handle) = link_registry.try_get(s1.links, an_id(1))
  handle.next_seq |> should.equal(1)

  // A second send off the THREADED state gets the next message id — proof the advance
  // survived in the registry rather than being dropped on the floor.
  let assert Ok(link_kernels.LinkEffect(_, s2, [])) =
    link_kernels.link_dispatch(heap.new(), s1, "_link_send", 3, args)
  let assert Ok(handle2) = link_registry.try_get(s2.links, an_id(1))
  handle2.next_seq |> should.equal(2)

  let assert [f2] = drain(wire, [])
  let assert Ok(p2) = frame_codec.parse_frame(f2)
  p2.message_id |> should.equal(1)
}

// "Send before setup" is a caller bug: a non-fatal abort, NOT an invented
// suspend-until-established the spec is silent on (C# oracle rationale, verbatim).
pub fn link_send_before_setup_aborts_non_fatally_test() {
  let state = link_runtime.new()

  let args = [
    a_message(),
    tcp_id_term(99),
    ConstTerm(ConstAtom("peer_b")),
  ]

  let assert Ok(link_kernels.LinkAbort(detail)) =
    link_kernels.link_dispatch(heap.new(), state, "_link_send", 3, args)
  { detail != "" } |> should.be_true
}

// The ground gate reaches through the kernel too: a non-ground Msg fails the goal and
// leaves the wire untouched. The GLP `ground(Msg?)` guard should have excluded it, so
// this is an upstream invariant break — surfaced, never partly shipped.
pub fn link_send_refuses_a_non_ground_message_test() {
  let wire = process.new_subject()
  let state = state_with_link(1, capturing_endpoint(an_id(1), wire))

  let #(h, w, _r) = heap.allocate_variable(heap.new())
  let args = [
    StructTerm("hello", [VarRef(w)]),
    tcp_id_term(1),
    ConstTerm(ConstAtom("peer_b")),
  ]

  let assert Ok(link_kernels.LinkAbort(_)) =
    link_kernels.link_dispatch(h, state, "_link_send", 3, args)
  drain(wire, []) |> should.equal([])
}

// A non-ground ToPeer is a caller bug too (the wrapper guards `ground(ToPeer?)`).
pub fn link_send_refuses_a_non_ground_to_peer_test() {
  let wire = process.new_subject()
  let state = state_with_link(1, capturing_endpoint(an_id(1), wire))

  let #(h, w, _r) = heap.allocate_variable(heap.new())
  let args = [a_message(), tcp_id_term(1), VarRef(w)]

  let assert Ok(link_kernels.LinkAbort(_)) =
    link_kernels.link_dispatch(h, state, "_link_send", 3, args)
  drain(wire, []) |> should.equal([])
}

// K2 is a recognized kernel, and the arity is exact.
pub fn link_send_is_registered_test() {
  link_kernels.link_is_kernel("_link_send", 3) |> should.be_true
  link_kernels.link_is_kernel("_link_send", 2) |> should.be_false
}

// A send binds nothing and wakes no goal — it is a pure host effect. (Guarding against a
// port that "helpfully" bound an ack variable.)
pub fn link_send_wakes_no_goals_test() {
  let wire = process.new_subject()
  let state = state_with_link(1, capturing_endpoint(an_id(1), wire))
  let args = [a_message(), tcp_id_term(1), ConstTerm(ConstAtom("peer_b"))]

  let assert Ok(link_kernels.LinkEffect(_, _, woken)) =
    link_kernels.link_dispatch(heap.new(), state, "_link_send", 3, args)
  woken |> should.equal([])
}

// The handle's cursors are irrelevant to K2 — the LinkId face does not ride the Out
// stream. A link established with no cursors wired still sends.
pub fn link_send_does_not_need_wired_cursors_test() {
  let wire = process.new_subject()
  let state = state_with_link(1, capturing_endpoint(an_id(1), wire))
  let assert Ok(handle) = link_registry.try_get(state.links, an_id(1))
  handle.out_reader |> should.equal(None)

  let args = [a_message(), tcp_id_term(1), ConstTerm(ConstAtom("peer_b"))]
  let assert Ok(link_kernels.LinkEffect(_, _, _)) =
    link_kernels.link_dispatch(heap.new(), state, "_link_send", 3, args)
  let assert [_] = drain(wire, [])
}

// Two links, two sequence spaces: sending on one must not advance the other (per-link
// FIFO, FR-018 — not a global counter).
pub fn sequence_spaces_are_per_link_test() {
  let wire_a = process.new_subject()
  let wire_b = process.new_subject()
  let state = link_runtime.new()
  let state =
    link_runtime.with_links(
      state,
      link_registry.put(
        state.links,
        link_handle.new(an_id(1), capturing_endpoint(an_id(1), wire_a), link_options.default()),
      ),
    )
  let state =
    link_runtime.with_links(
      state,
      link_registry.put(
        state.links,
        link_handle.new(an_id(2), capturing_endpoint(an_id(2), wire_b), link_options.default()),
      ),
    )

  let send_on = fn(st, nonce) {
    let args = [a_message(), tcp_id_term(nonce), ConstTerm(ConstAtom("peer_b"))]
    let assert Ok(link_kernels.LinkEffect(_, out, _)) =
      link_kernels.link_dispatch(heap.new(), st, "_link_send", 3, args)
    out
  }

  let state = send_on(state, 1)
  let state = send_on(state, 1)
  let state = send_on(state, 2)

  let assert Ok(a) = link_registry.try_get(state.links, an_id(1))
  let assert Ok(b) = link_registry.try_get(state.links, an_id(2))
  a.next_seq |> should.equal(2)
  b.next_seq |> should.equal(1)

  // Link 2's single frame is message 0, not 2.
  let assert [fb] = drain(wire_b, [])
  let assert Ok(pb) = frame_codec.parse_frame(fb)
  pb.message_id |> should.equal(0)
}

// Cross-check: the C4 out-of-band token path still rides message id 0 and is unaffected
// by the `link_wire.encode_frames` refactor C5 introduced (behaviour-preserving).
pub fn token_path_still_rides_message_zero_test() {
  let assert Ok(frame) = link_wire.encode_token(a_message())
  let assert Ok(p) = frame_codec.parse_frame(frame)
  p.message_id |> should.equal(0)
  p.kind |> should.equal(frame_codec.Whole)
  let assert Ok(decoded) = link_wire.decode_token(frame)
  decoded |> should.equal(a_message())
}
