//// T050.C2 — GLP term ↔ host marshalling for the link kernels.
////
//// Pins the ground gate and the `self.glp:430-461` type shapes. The nested-VarRef case
//// is the one that matters most: a *ground* struct argument still arrives as `VarRef`s
//// into bound cells, and the C# oracle records that its own xUnit suite hid this bug by
//// testing with ground `ConstTerm`s only.

import gleeunit/should
import glp/link/primitives/link_establish
import glp/link/primitives/link_terms
import glp/link/seam/link_address
import glp/link/seam/link_id.{LinkId, NonceInt, NonceStr}
import glp/link/seam/link_scheme
import glp/runtime/heap
import glp/runtime/terms.{ConstAtom, ConstInt, ConstString, ConstTerm, StructTerm}

fn atom(s: String) -> terms.Term {
  ConstTerm(ConstAtom(s))
}

// ── ground gate ─────────────────────────────────────────────────────────────

// A ground struct whose args are VarRefs into BOUND cells must resolve to a
// VarRef-free tree — this is the trap the C# oracle flags.
pub fn ground_resolve_follows_bound_varrefs_test() {
  let h = heap.new()
  let #(h, writer, _reader) = heap.allocate_variable(h)
  let assert Ok(#(h, _woken)) = heap.bind_writer(h, writer, atom("tcp"))

  let assert Ok(#(_h, resolved)) =
    link_terms.ground_resolve(h, StructTerm("wrap", [terms.VarRef(writer)]))

  resolved |> should.equal(StructTerm("wrap", [atom("tcp")]))
}

// An UNBOUND cell trips the gate. The wrappers' ground/1 guards should make this
// unreachable, so it means an upstream invariant broke — surfaced, never defaulted.
pub fn ground_resolve_rejects_unbound_test() {
  let h = heap.new()
  let #(h, writer, _reader) = heap.allocate_variable(h)

  let assert Error(link_terms.NotGround(_)) =
    link_terms.ground_resolve(h, terms.VarRef(writer))
}

// ── link_id(Scheme, Endpoint, Nonce) ────────────────────────────────────────

pub fn parses_link_id_with_ep_endpoint_test() {
  let term =
    StructTerm("link_id", [
      atom("tcp"),
      StructTerm("ep", [atom("127.0.0.1"), ConstTerm(ConstInt(9000))]),
      ConstTerm(ConstInt(7)),
    ])

  link_terms.parse_link_id(term)
  |> should.equal(
    Ok(LinkId(
      scheme: link_scheme.tcp(),
      endpoint: link_address.endpoint("127.0.0.1", 9000),
      nonce: NonceInt(7),
    )),
  )
}

pub fn parses_link_id_with_bare_endpoint_test() {
  let term =
    StructTerm("link_id", [atom("loopback"), atom("chan-a"), atom("n1")])

  link_terms.parse_link_id(term)
  |> should.equal(
    Ok(LinkId(
      scheme: link_scheme.loopback(),
      endpoint: link_address.path("chan-a"),
      nonce: NonceStr("n1"),
    )),
  )
}

// Nonce forms stay DISTINCT — integer 5 is not the string "5". This is part of link
// identity (FR-007 keying), so collapsing them would merge two different links.
pub fn nonce_int_and_string_are_distinct_test() {
  link_terms.parse_nonce(ConstTerm(ConstInt(5)))
  |> should.equal(Ok(NonceInt(5)))

  link_terms.parse_nonce(ConstTerm(ConstString("5")))
  |> should.equal(Ok(NonceStr("5")))
}

pub fn rejects_malformed_link_id_test() {
  let assert Error(link_terms.BadShape(_, _)) =
    link_terms.parse_link_id(StructTerm("link_id", [atom("tcp")]))
}

// A blank scheme must FAIL THE GOAL, not panic the engine. `link_scheme.of` panics on
// blank — correct for internal callers, fatal for user-supplied kernel arguments.
pub fn blank_scheme_fails_rather_than_panics_test() {
  let assert Error(link_terms.BadShape(_, _)) =
    link_terms.parse_scheme(ConstTerm(ConstString("  ")))
}

// ── role ────────────────────────────────────────────────────────────────────

pub fn parses_both_roles_test() {
  link_terms.parse_role(atom("listener"))
  |> should.equal(Ok(link_establish.Listener))

  link_terms.parse_role(atom("connector"))
  |> should.equal(Ok(link_establish.Connector))
}

pub fn rejects_unknown_role_test() {
  let assert Error(link_terms.BadShape(_, _)) =
    link_terms.parse_role(atom("bystander"))
}

// ── rendezvous (path B, C4) — NOT a link_id: no nonce ───────────────────────

pub fn parses_rendezvous_test() {
  link_terms.parse_rendezvous(
    StructTerm("rendezvous", [atom("tcp"), atom("host-a")]),
  )
  |> should.equal(Ok(#(link_scheme.tcp(), link_address.path("host-a"))))
}

// ── round-trip: rebuilt terms must be =?=-comparable with source literals ────

pub fn link_id_round_trips_test() {
  let id =
    LinkId(
      scheme: link_scheme.tcp(),
      endpoint: link_address.endpoint("127.0.0.1", 9000),
      nonce: NonceInt(7),
    )

  link_terms.link_id_to_term(id)
  |> link_terms.parse_link_id
  |> should.equal(Ok(id))
}
