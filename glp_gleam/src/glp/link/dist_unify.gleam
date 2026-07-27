//// glp/link/dist_unify — distributed unification over the link layer (feature 050,
//// T051; FR-014, data-model "RemoteVarRef / dist-unify state").
////
//// **Wire shape ratified by Gabi 2026-07-27: `'_assign'(NameTerm, Value)`.** A madGLP
//// assignment crosses instances as ONE ground term over the EXISTING ground-relay
//// wire (`link_egress.ship_ground` — the single ship path, R-5). No new codec, no
//// embedded variables. The rejected alternative — porting the Dart irma-era
//// `payload_serializer` assignment wire (creator:localId strings + variable-import
//// callbacks) — contradicts TWO ratified decisions: E3 (pure local-pair model, no
//// `bindAny`/variable-import) and D-4 (ground-relay base, nothing non-ground on the
//// wire). The C# oracle's base wire agrees (`DefaultPayloadCodec` = the 025
//// ground-relay blob; no assignment-specific codec exists in its base either).
////
//// **Why this is already deferred-local-assignment (FR-014).** The madGLP machinery
//// T050.A built IS the dist-unify model the data-model sketches:
////   * `RemoteVarRef`'s "(instance id, writer id)" ≡ `GlobalName(agent, index)` —
////     one namespace, not two (the D-6 lesson applied again); this module adds only
////     the CARRIER: which link a name travels by.
////   * "globalize/localize on `known/1`" ≡ the `global_send/3` goal guarded on
////     `known(Y?)` (spec §4/§5), unchanged.
////   * "binding happens on the owning side" ≡ the Receive transaction (spec §8.3):
////     the arriving `'_assign'` NEVER binds directly — `mad_engine.receive` looks the
////     name up in W_p and binds a LOCAL writer, waking local goals through the
////     ordinary machinery. A reader-holding side owns no writer and can never bind;
////     assignment is DEFERRED to the side that does. Both sides converge because
////     each binds only its own cell to the same shipped ground value.
////   * Deref chains cannot cross the seam at all: global names substitute for
////     variables BEFORE shipping (globalize), so no wire term ever points into a
////     remote heap — the FORK-1 "no cycles across the seam" discriminator holds by
////     construction. Convergence at scale is proof obligation PI:17 (gates M2),
////     recorded in the P4 PROOFS index — a proof artifact, not code here.
////
//// Ground-relay note: a `GlobalName` term (`_w(p,i)`/`_r(p,i)`) is itself ground, so
//// an `'_assign'` whose Value embeds further global names (the serializer cold-call
//// `[req(_r(p,1)) | _w(q,0)]`) ships whole — exactly why globalize precedes Send.

import gleam/result
import glp/link/primitives/link_egress
import glp/link/primitives/link_handle.{type LinkHandle}
import glp/link/seam/link_id.{type LinkId}
import glp/mad/global_name.{type GlobalName}
import glp/mad/mad_engine.{type MadEngine}
import glp/mad/message.{type Message, Message}
import glp/runtime/heap.{type Heap}
import glp/runtime/terms.{type Term, StructTerm}

/// The data-model's cross-instance variable reference, made concrete: the logical
/// identity is the `GlobalName` (owning agent + writer index — spec §2/§3); the
/// `link` is the CARRIER it travels by. Identity never derives from the carrier
/// (a LinkId nonce is a transport fact — D-6), which is why this is a pair rather
/// than a merged id.
pub type RemoteVarRef {
  RemoteVarRef(link: LinkId, name: GlobalName)
}

const assign_functor = "_assign"

/// Encode one madGLP assignment as the ratified wire term `'_assign'(Name, Value)`.
/// `dest` is dropped deliberately: the link is bilateral (FR-005), so the carrier
/// names the one far end — shipping the dest would duplicate routing state the
/// registry already owns.
pub fn encode_assignment(msg: Message) -> Term {
  let Message(name, value, _dest) = msg
  StructTerm(assign_functor, [global_name.to_term(name), value])
}

/// Recognize + decode an inbound `'_assign'(Name, Value)`. `Error(Nil)` means the
/// term is NOT an assignment — ordinary link data for the program's `In` stream,
/// not a protocol violation; the caller routes it accordingly.
pub fn decode_assignment(term: Term) -> Result(#(GlobalName, Term), Nil) {
  case term {
    StructTerm(f, [name_term, value]) if f == assign_functor ->
      case global_name.of_term(name_term) {
        Ok(name) -> Ok(#(name, value))
        Error(Nil) -> Error(Nil)
      }
    _ -> Error(Nil)
  }
}

/// Ship one assignment over an established link: encode, then the ONE ground-relay
/// ship path (resolve → codec → frame → sequence → send, FR-010/018). Returns the
/// ADVANCED handle for the caller to thread back into its registry — the same
/// contract as every other sender (a dropped handle would reuse a message id).
pub fn ship_assignment(
  heap: Heap,
  handle: LinkHandle,
  msg: Message,
) -> Result(#(Heap, LinkHandle), link_egress.EgressError) {
  link_egress.ship_ground(heap, handle, encode_assignment(msg))
}

/// Apply one inbound assignment to the local agent: decode, then the UNCHANGED
/// spec §8.3 Receive transaction — W_p lookup, LOCAL writer bind, entry removal,
/// reactivation through the ordinary suspension machinery. This is the
/// deferred-local-assignment landing point: the only bind happens here, on the
/// side that owns the cell. `Error` carries the Receive diagnostic (a missing W_p
/// entry is a sender protocol violation — duplicate-delivery dedup is the
/// reliability sublayer's future concern, spec-v5.3-PURE) or names a non-assignment
/// term the caller misrouted.
pub fn apply_assignment(me: MadEngine, term: Term) -> Result(MadEngine, String) {
  case decode_assignment(term) {
    Error(Nil) ->
      Error("dist_unify: not an '_assign'/2 term — misrouted ordinary data")
    Ok(#(name, value)) -> mad_engine.receive(me, name, value)
  }
}

/// Convenience for a driver draining one link's inbound data: assignments go to the
/// mad engine, anything else is handed back as ordinary program data. Keeps the
/// discrimination in ONE place so a driver cannot half-route.
pub type Inbound {
  /// The term was an assignment and was applied; the advanced engine.
  Applied(me: MadEngine)
  /// Ordinary link data for the program's `In` stream — untouched.
  ProgramData(term: Term)
}

pub fn route_inbound(me: MadEngine, term: Term) -> Result(Inbound, String) {
  case decode_assignment(term) {
    Error(Nil) -> Ok(ProgramData(term))
    Ok(#(name, value)) ->
      mad_engine.receive(me, name, value) |> result.map(Applied)
  }
}
