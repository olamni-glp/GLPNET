//// glp/link/primitives/link_terms — GLP ground terms ↔ host link value-types
//// (T050.C1; contracts/link-primitives-port.md §4).
////
//// Port of `glp_runtime/lib/link/primitives/link_terms.dart` (mirror C#
//// `LinkTerms`). The GLP surface is `LinkId ::= link_id(Scheme, Endpoint, Nonce)`,
//// `Endpoint ::= String ; ep(String, Integer)`, `Nonce ::= Integer ; String`,
//// `Role ∈ {listener, connector}`, and the fault lattice
//// `ok | closed(LinkId,Reason) | tempFail(LinkId,Reason) | permFail(LinkId,Reason)`
//// (self.glp:430-461; fault vocab D-1: bare `ok`, arity 0).
////
//// Parsing expects GROUND terms — the GLP wrappers guard `ground/1` before the
//// kernel runs, so an unbound cell reaching `ground_resolve` is a caller bug,
//// surfaced as `Error(reason)` and turned into a kernel abort (never tolerated —
//// CLAUDE.md "robustness is a workaround in disguise").
////
//// Quote mapping (deviation from the Dart oracle, by construction): Dart stores a
//// GLP string constant WITH surrounding quotes inside one `ConstTerm(String)` kind
//// and needs requote/unquote so rebuilt terms match `=?=`. The Gleam term model
//// keeps `ConstString` and `ConstAtom` as DISTINCT variants storing the raw text
//// (parser.gleam LitString → ConstString(s)), so terms rebuilt here with the same
//// variant are structurally identical to source literals with no quote juggling.

import gleam/int
import gleam/list
import gleam/option.{None, Some}
import gleam/result
import glp/link/primitives/link_faults.{
  type LinkFault, FaultClosed, FaultOk, FaultPermFail, FaultTempFail,
}
import glp/link/seam/link_address.{type LinkAddress}
import glp/link/seam/link_fault.{type LinkFaultSignal, Closed, Permanent, Transient}
import glp/link/seam/link_id.{
  type LinkId, type LinkNonce, LinkId, NonceInt, NonceStr,
}
import glp/link/seam/link_scheme.{type LinkScheme}
import glp/runtime/heap.{type Heap, Bound, Unbound, deref}
import glp/runtime/terms.{
  type Term, ConstAtom, ConstInt, ConstReal, ConstString, ConstTerm, StructTerm,
  VarRef,
}

/// The graceful stream-end close reason (`Out = []` teardown).
pub const graceful_reason = "eos"

/// The `link_close/1` default reason.
pub const abrupt_reason = "abrupt"

// ---- resolve: heap → VarRef-free ground tree ----

/// Recursively dereference `term` into a VarRef-free ground tree, so the parsers
/// below see bound constants at every depth (the compiler represents a ground
/// `link_id(...)` with each struct arg as a VarRef into a bound cell). An unbound
/// cell at any depth is a caller bug — the `ground/1` guard should have excluded
/// it — reported as `Error`, and the kernel aborts.
pub fn ground_resolve(heap: Heap, term: Term) -> Result(Term, String) {
  case term {
    ConstTerm(_) -> Ok(term)
    StructTerm(f, args) ->
      list.try_map(args, fn(a) { ground_resolve(heap, a) })
      |> result.map(fn(rargs) { StructTerm(f, rargs) })
    VarRef(addr) ->
      case deref(heap, addr) {
        Ok(#(_, Bound(v))) -> ground_resolve(heap, v)
        Ok(#(_, Unbound(_))) ->
          Error(
            "unbound cell in a term expected ground (the ground/1 guard should have excluded it)",
          )
        Error(_) -> Error("dangling VarRef in a term expected ground")
      }
  }
}

// ---- parse: ground term → host value ----

/// Parse a ground `link_id(Scheme, Endpoint, Nonce)` term.
pub fn parse_link_id(term: Term) -> Result(LinkId, String) {
  case term {
    StructTerm("link_id", [scheme_t, endpoint_t, nonce_t]) -> {
      use scheme <- result.try(parse_scheme(scheme_t))
      use endpoint <- result.try(parse_endpoint(endpoint_t))
      use nonce <- result.try(parse_nonce(nonce_t))
      Ok(LinkId(scheme, endpoint, nonce))
    }
    _ -> Error("expected link_id/3 struct, got " <> describe(term))
  }
}

/// Parse a ground `Scheme` string (also the first component of
/// `rendezvous(Scheme, Endpoint)`).
pub fn parse_scheme(term: Term) -> Result(LinkScheme, String) {
  use s <- result.try(const_string(term, "Scheme"))
  Ok(link_scheme.of(s))
}

/// Parse a ground `Endpoint ::= String ; ep(String, Integer)` term.
pub fn parse_endpoint(term: Term) -> Result(LinkAddress, String) {
  case term {
    StructTerm("ep", [host_t, port_t]) -> {
      use host <- result.try(const_string(host_t, "ep host"))
      use port <- result.try(const_int(port_t, "ep port"))
      Ok(link_address.endpoint(host, port))
    }
    _ ->
      const_string(term, "Endpoint")
      |> result.map(link_address.path)
  }
}

fn parse_nonce(term: Term) -> Result(LinkNonce, String) {
  case term {
    ConstTerm(ConstInt(v)) -> Ok(NonceInt(v))
    ConstTerm(ConstAtom(v)) -> Ok(NonceStr(v))
    ConstTerm(ConstString(v)) -> Ok(NonceStr(v))
    _ -> Error("Nonce must be Integer or String, got " <> describe(term))
  }
}

/// Parse the ground close `Reason` (`Reason ::= String`): the atom `abrupt` from
/// `link_close/1`, or a user reason from `link_close/2`.
pub fn parse_reason(term: Term) -> Result(String, String) {
  const_string(term, "Reason")
}

/// Parse the ground establishment role atom (`listener`/`connector`). Returned as
/// the establish core's `Role` (link_establish.{Listener,Connector} is layered
/// above — the kernel maps this token there); kept as a plain tagged value here so
/// link_terms depends only on seam types.
pub type ParsedRole {
  RoleListener
  RoleConnector
}

pub fn parse_role(term: Term) -> Result(ParsedRole, String) {
  use s <- result.try(const_string(term, "Role"))
  case s {
    "listener" -> Ok(RoleListener)
    "connector" -> Ok(RoleConnector)
    _ -> Error("Role must be listener|connector, got '" <> s <> "'")
  }
}

// ---- build: host value → term ----

/// Build the GLP `link_id(Scheme, Endpoint, Nonce)` term, structurally identical
/// to the source literal (`link_id("tcp", ep("127.0.0.1", 9100), 1)`): String
/// components rebuild as `ConstString`, so a path-B request-token round-trip
/// matches the served LinkId under `=?=` in `accept_link`.
pub fn to_term(id: LinkId) -> Term {
  StructTerm("link_id", [
    ConstTerm(ConstString(link_scheme.name(id.scheme))),
    endpoint_to_term(id.endpoint),
    nonce_to_term(id.nonce),
  ])
}

fn endpoint_to_term(addr: LinkAddress) -> Term {
  case addr.port {
    Some(port) ->
      StructTerm("ep", [
        ConstTerm(ConstString(addr.host)),
        ConstTerm(ConstInt(port)),
      ])
    None -> ConstTerm(ConstString(addr.host))
  }
}

fn nonce_to_term(nonce: LinkNonce) -> Term {
  case nonce {
    NonceInt(v) -> ConstTerm(ConstInt(v))
    NonceStr(v) -> ConstTerm(ConstString(v))
  }
}

// ---- in-band request token (path-B handshake) ----

/// Build the in-band `request(LinkId, FromPeer)` handshake token.
pub fn request_token(id: LinkId, from_peer: Term) -> Term {
  StructTerm("request", [to_term(id), from_peer])
}

/// Parse a ground `request(LinkId, FromPeer)` token (the listener's in-band
/// first frame).
pub fn parse_request_token(term: Term) -> Result(#(LinkId, Term), String) {
  case term {
    StructTerm("request", [id_t, from_peer]) ->
      parse_link_id(id_t)
      |> result.map(fn(id) { #(id, from_peer) })
    _ -> Error("expected request/2 token, got " <> describe(term))
  }
}

// ---- fault lattice terms (D-1: bare ok/0; closed/tempFail/permFail carry
// link_id + reason) ----

pub fn ok_term() -> Term {
  ConstTerm(ConstAtom("ok"))
}

pub fn closed_term(id: LinkId, reason: String) -> Term {
  fault2("closed", id, reason)
}

pub fn temp_fail_term(id: LinkId, reason: String) -> Term {
  fault2("tempFail", id, reason)
}

pub fn perm_fail_term(id: LinkId, reason: String) -> Term {
  fault2("permFail", id, reason)
}

/// Map a seam-level `LinkFaultSignal` to its GLP monitor term.
pub fn from_signal(s: LinkFaultSignal) -> Term {
  case s.kind {
    Closed -> closed_term(s.link, s.reason)
    Transient -> temp_fail_term(s.link, s.reason)
    Permanent -> perm_fail_term(s.link, s.reason)
  }
}

/// Map a refined `LinkFault` lattice value to its GLP monitor term.
pub fn from_fault(fault: LinkFault) -> Term {
  case fault {
    FaultOk -> ok_term()
    FaultClosed(id, reason) -> closed_term(id, reason)
    FaultTempFail(id, reason) -> temp_fail_term(id, reason)
    FaultPermFail(id, reason) -> perm_fail_term(id, reason)
  }
}

fn fault2(functor: String, id: LinkId, reason: String) -> Term {
  StructTerm(functor, [to_term(id), ConstTerm(ConstString(reason))])
}

// ---- const extraction helpers ----

/// A String from either constant variant: a GLP `String` literal (`ConstString`)
/// or a bare atom (`ConstAtom`) — the Dart oracle accepted both (its single
/// String-valued ConstTerm kind erased the distinction; here both variants carry
/// the raw text).
fn const_string(term: Term, what: String) -> Result(String, String) {
  case term {
    ConstTerm(ConstString(s)) -> Ok(s)
    ConstTerm(ConstAtom(s)) -> Ok(s)
    _ -> Error(what <> ": expected a String constant, got " <> describe(term))
  }
}

fn const_int(term: Term, what: String) -> Result(Int, String) {
  case term {
    ConstTerm(ConstInt(v)) -> Ok(v)
    _ -> Error(what <> ": expected an Integer constant, got " <> describe(term))
  }
}

fn describe(term: Term) -> String {
  case term {
    ConstTerm(ConstAtom(s)) -> "atom(" <> s <> ")"
    ConstTerm(ConstString(s)) -> "string(\"" <> s <> "\")"
    ConstTerm(ConstInt(_)) -> "integer"
    ConstTerm(ConstReal(_)) -> "real"
    StructTerm(f, args) -> f <> "/" <> int.to_string(list.length(args))
    VarRef(_) -> "unbound var"
  }
}
