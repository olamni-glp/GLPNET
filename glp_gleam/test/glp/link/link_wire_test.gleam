//// T050.C4 — the out-of-band request-token wire round-trip.
////
//// A `request(LinkId, FromPeer)` token must survive encode → frame → decode intact, so
//// that the listener parses the SAME ground LinkId the connector shipped (path B keys the
//// pending-park and the `=?=` match on it). Both ends are Gleam, so this is the only
//// agreement that matters for base C4.

import gleeunit/should
import glp/link/primitives/link_terms
import glp/link/primitives/link_wire
import glp/link/seam/link_address
import glp/link/seam/link_id.{LinkId, NonceInt}
import glp/link/seam/link_scheme
import glp/runtime/terms.{ConstAtom, ConstTerm}

pub fn request_token_round_trips_through_the_wire_test() {
  let id =
    LinkId(
      scheme: link_scheme.tcp(),
      endpoint: link_address.endpoint("127.0.0.1", 9000),
      nonce: NonceInt(7),
    )
  let token = link_terms.request_token(id, ConstTerm(ConstAtom("requester")))

  let assert Ok(frame) = link_wire.encode_token(token)
  let assert Ok(decoded) = link_wire.decode_token(frame)

  // Byte-exact round-trip, AND the decoded token re-parses to the same LinkId + peer.
  decoded |> should.equal(token)
  let assert Ok(#(id2, from_peer)) = link_terms.parse_request_token(decoded)
  id2 |> should.equal(id)
  from_peer |> should.equal(ConstTerm(ConstAtom("requester")))
}
