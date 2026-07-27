//// glp/link/primitives/link_terms — GLP ground-term ↔ host link value mapping
//// (feature 059, T076 — port of glp_runtime/lib/link/primitives/link_terms.dart,
//// mirror csharp/glp_link/primitives/LinkTerms.cs).
////
//// The GLP surface (025 link-primitives.md §1, prelude self.glp):
////   LinkId     ::= link_id(Scheme, Endpoint, Nonce)
////   Endpoint   ::= String ; ep(String, Integer)
////   Nonce      ::= Integer ; String
////   Role       ∈ {listener, connector}
////   Rendezvous ::= rendezvous(Scheme, Endpoint)
////   RequestMsg ::= request(LinkId, AgentId)
////   Fault      ::= ok | closed(LinkId,Reason) | tempFail(LinkId,Reason)
////                     | permFail(LinkId,Reason)   (Reason ::= String)
////
//// Parsing expects an already-GROUND term: the GLP wrappers guard `ground/1`
//// before the kernel runs, so a malformed / unbound term reaching here is a caller
//// bug (an `Error(String)`), NOT something to tolerate (CLAUDE.md "robustness is a
//// workaround in disguise"). `ground_resolve` deep-derefs first so the nested struct
//// args (each a bound `VarRef` cell at compile time) are seen as their constants.
////
//// GLEAM MAPPING NOTE (vs the Dart source): the Dart `ConstTerm(Object?)` erases the
//// atom/string/int distinction, so Dart re-quotes strings (`_requote`) to rebuild a
//// source-identical term. Gleam's `Constant` keeps `ConstAtom`/`ConstString`/`ConstInt`
//// DISTINCT (terms.gleam), so a string component round-trips as `ConstString` and an
//// atom (role/reason/`ok`) as `ConstAtom` with NO quote juggling — the reconstructed
//// term is structurally identical to the source literal by construction. A token that
//// may appear either quoted (a `Scheme`/host `String`) or bare is read by
//// `atom_or_string`, faithful to the two legitimate constant kinds.

import gleam/int
import gleam/list
import gleam/option.{None, Some}
import gleam/result
import glp/link/seam/link_address.{type LinkAddress}
import glp/link/seam/link_fault.{
  type LinkFaultSignal, Closed, Permanent, Transient,
}
import glp/link/seam/link_id.{type LinkId, type LinkNonce, LinkId, NonceInt, NonceStr}
import glp/link/seam/link_scheme.{type LinkScheme}
import glp/runtime/heap.{type Heap, Bound, Unbound}
import glp/runtime/terms.{
  type Constant, type Term, ConstAtom, ConstInt, ConstReal, ConstString,
  ConstTerm, StructTerm, VarRef,
}

/// The graceful stream-end close reason (`Out = []`) — the terminal
/// `closed(LinkId, eos)` fault (rulings-log "graceful reason eos").
pub const graceful_reason = "eos"

/// The default `link_close/1` reason (abrupt teardown).
pub const abrupt_reason = "abrupt"

const link_id_functor = "link_id"

const endpoint_functor = "ep"

const request_functor = "request"

const rendezvous_functor = "rendezvous"

// ---- deep ground-resolve ---------------------------------------------------

/// Deep-dereference `term` against `heap` into a `VarRef`-free ground tree, so the
/// parse helpers see the bound constants at every depth (REQUIRED: the compiler
/// lowers a ground `link_id(Scheme, …)` with each struct ARG as a `VarRef` into a
/// bound cell — a shallow deref resolves only the outer struct). An unbound cell at
/// any depth is a caller bug (the `ground/1` guard should have excluded it), so it
/// is `Error(String)`, never a placeholder on the wire.
pub fn ground_resolve(heap: Heap, term: Term) -> Result(Term, String) {
  case term {
    ConstTerm(_) -> Ok(term)
    StructTerm(functor, args) -> {
      use resolved <- result.try(
        list.try_map(args, fn(a) { ground_resolve(heap, a) }),
      )
      Ok(StructTerm(functor, resolved))
    }
    VarRef(addr) ->
      case heap.deref(heap, addr) {
        Ok(#(_, Bound(v))) -> ground_resolve(heap, v)
        Ok(#(_, Unbound(w))) ->
          Error(
            "unbound cell "
            <> int.to_string(w)
            <> " in a term expected ground (the ground/1 guard should have excluded it)",
          )
        Error(_) ->
          Error("cannot deref cell " <> int.to_string(addr) <> " (ground-resolve)")
      }
  }
}

// ---- parse: term → host value ----------------------------------------------

/// Parse a ground `link_id(Scheme, Endpoint, Nonce)` term.
pub fn parse_link_id(term: Term) -> Result(LinkId, String) {
  case term {
    StructTerm(functor, [scheme_t, endpoint_t, nonce_t]) if functor == link_id_functor -> {
      use scheme <- result.try(parse_scheme(scheme_t))
      use endpoint <- result.try(parse_endpoint(endpoint_t))
      use nonce <- result.try(parse_nonce(nonce_t))
      Ok(LinkId(scheme, endpoint, nonce))
    }
    _ -> Error("expected link_id/3 struct, got " <> describe(term))
  }
}

/// Parse a ground `Scheme` string (LinkId component / `rendezvous(Scheme, …)`).
pub fn parse_scheme(term: Term) -> Result(LinkScheme, String) {
  use s <- result.try(const_token(term, "Scheme"))
  Ok(link_scheme.of(s))
}

/// Parse a ground `Endpoint ::= String ; ep(String, Integer)` term.
pub fn parse_endpoint(term: Term) -> Result(LinkAddress, String) {
  case term {
    StructTerm(functor, [host_t, port_t]) if functor == endpoint_functor -> {
      use host <- result.try(const_token(host_t, "ep host"))
      use port <- result.try(const_int(port_t, "ep port"))
      Ok(link_address.endpoint(host, port))
    }
    _ -> {
      use host <- result.try(const_token(term, "Endpoint"))
      Ok(link_address.path(host))
    }
  }
}

fn parse_nonce(term: Term) -> Result(LinkNonce, String) {
  case term {
    ConstTerm(ConstInt(v)) -> Ok(NonceInt(v))
    ConstTerm(ConstString(v)) -> Ok(NonceStr(v))
    ConstTerm(ConstAtom(v)) -> Ok(NonceStr(v))
    _ -> Error("Nonce must be Integer or String, got " <> describe(term))
  }
}

/// Parse the ground establishment role atom (`listener` / `connector`) into the
/// role token string the transport seam consumes (listen vs connect).
pub fn parse_role(term: Term) -> Result(String, String) {
  use s <- result.try(const_token(term, "Role"))
  case s {
    "listener" -> Ok("listener")
    "connector" -> Ok("connector")
    _ -> Error("Role must be listener|connector, got '" <> s <> "'")
  }
}

/// Parse the ground close `Reason` (`Reason ::= String`): the atom `abrupt`
/// (`link_close/1`) or a user reason (`link_close/2`).
pub fn parse_reason(term: Term) -> Result(String, String) {
  const_token(term, "Reason")
}

/// Parse a ground `rendezvous(Scheme, Endpoint)` term (path-B `request_listener`).
pub fn parse_rendezvous(term: Term) -> Result(#(LinkScheme, LinkAddress), String) {
  case term {
    StructTerm(functor, [scheme_t, endpoint_t]) if functor == rendezvous_functor -> {
      use scheme <- result.try(parse_scheme(scheme_t))
      use endpoint <- result.try(parse_endpoint(endpoint_t))
      Ok(#(scheme, endpoint))
    }
    _ -> Error("expected rendezvous/2 struct, got " <> describe(term))
  }
}

/// Parse a ground `request(LinkId, FromPeer)` token (path-B in-band first frame).
/// `FromPeer` is returned as its raw (ground) term.
pub fn parse_request_token(term: Term) -> Result(#(LinkId, Term), String) {
  case term {
    StructTerm(functor, [id_t, from_peer]) if functor == request_functor -> {
      use id <- result.try(parse_link_id(id_t))
      Ok(#(id, from_peer))
    }
    _ -> Error("expected request/2 token, got " <> describe(term))
  }
}

// ---- build: host value → term ----------------------------------------------

/// Build the GLP `link_id(Scheme, Endpoint, Nonce)` term. String components are
/// `ConstString`, so the reconstructed term is structurally identical to the source
/// literal (Gleam keeps atom/string distinct — no Dart-style re-quoting).
pub fn to_term(id: LinkId) -> Term {
  StructTerm(link_id_functor, [
    ConstTerm(ConstString(link_scheme.name(id.scheme))),
    endpoint_to_term(id.endpoint),
    nonce_to_term(id.nonce),
  ])
}

fn endpoint_to_term(addr: LinkAddress) -> Term {
  case addr.port {
    Some(p) ->
      StructTerm(endpoint_functor, [
        ConstTerm(ConstString(addr.host)),
        ConstTerm(ConstInt(p)),
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

/// Build the in-band `request(LinkId, FromPeer)` handshake token.
pub fn request_token(id: LinkId, from_peer: Term) -> Term {
  StructTerm(request_functor, [to_term(id), from_peer])
}

// ---- fault lattice terms (FR-043/045; rulings-log fault vocab) --------------

/// The `ok` monitor term.
pub fn ok() -> Term {
  ConstTerm(ConstAtom("ok"))
}

/// `closed(LinkId, Reason)` — the INTENTIONAL terminal (graceful `eos` or a user
/// close reason), distinct from tempFail/permFail.
pub fn closed(id: LinkId, reason: String) -> Term {
  fault2("closed", id, reason)
}

/// `tempFail(LinkId, Reason)` — a recoverable transport fault.
pub fn temp_fail(id: LinkId, reason: String) -> Term {
  fault2("tempFail", id, reason)
}

/// `permFail(LinkId, Reason)` — an unrecoverable transport fault (→ distributed GC).
pub fn perm_fail(id: LinkId, reason: String) -> Term {
  fault2("permFail", id, reason)
}

/// Map a seam-level `LinkFaultSignal` to its GLP monitor term (the reliability
/// sublayer's coarse kind → the refined lattice term the program reads).
pub fn from_signal(s: LinkFaultSignal) -> Term {
  case s.kind {
    Closed -> closed(s.link, s.reason)
    Transient -> temp_fail(s.link, s.reason)
    Permanent -> perm_fail(s.link, s.reason)
  }
}

fn fault2(functor: String, id: LinkId, reason: String) -> Term {
  StructTerm(functor, [to_term(id), ConstTerm(ConstString(reason))])
}

// ---- const extraction helpers ----------------------------------------------

/// A token that may be carried as a `String` literal or a bare atom (a `Scheme`, a
/// host, a `Role`/`Reason`): return the inner text of either. An `Int` or non-const
/// is a caller bug.
fn const_token(term: Term, what: String) -> Result(String, String) {
  case term {
    ConstTerm(ConstString(s)) -> Ok(s)
    ConstTerm(ConstAtom(s)) -> Ok(s)
    _ -> Error(what <> ": expected a String/atom constant, got " <> describe(term))
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
    ConstTerm(c) -> "const(" <> describe_const(c) <> ")"
    StructTerm(functor, args) ->
      functor <> "/" <> int.to_string(list.length(args))
    VarRef(addr) -> "var@" <> int.to_string(addr)
  }
}

fn describe_const(c: Constant) -> String {
  case c {
    ConstAtom(s) -> s
    ConstString(s) -> "\"" <> s <> "\""
    ConstInt(v) -> int.to_string(v)
    ConstReal(_) -> "real"
  }
}
