//// glp/link/primitives/link_terms — GLP term ↔ host value marshalling for the link
//// kernels (feature 050, T050.C2).
////
//// Port of `csharp/glp_link/primitives/LinkTerms.cs` (mirror
//// `glp_runtime/lib/link/primitives/link_terms.dart`).
////
//// Two jobs:
////   1. **`ground_resolve`** — deep-dereference a term to a `VarRef`-free tree, or fail.
////      This is the GROUND GATE at the host boundary. The GLP wrappers already guard
////      `ground/1`/`ground(LinkId?)` before a kernel is reached, so an unbound cell here
////      means an invariant was broken upstream — it is surfaced, never defaulted.
////      Necessary because a *ground* struct argument still arrives as `VarRef`s pointing
////      at bound cells; the C# oracle notes this exact trap (its xUnit tests used ground
////      `ConstTerm`s and hid the nested-VarRef bug until `LinkTerms.GroundResolve` was
////      threaded through all seven kernels).
////   2. **Parsers** — the resolved tree → `LinkId` / `Role` / `Rendezvous` / `Reason`,
////      matching the shipped type definitions in `programs/self.glp:430-461` EXACTLY.
////
//// Type shapes this must honour (self.glp, authoritative — NOT the C# reference):
//// ```prolog
//// Scheme   ::= String.
//// Endpoint ::= String ; ep(String, Integer).
//// Nonce    ::= Integer ; String.
//// LinkId   ::= link_id(Scheme, Endpoint, Nonce).
//// LinkRole ::= listener ; connector.
//// Rendezvous ::= rendezvous(Scheme, Endpoint).
//// ```
//// Note `Nonce ::= Integer ; String` maps to `NonceInt`/`NonceStr` and the two stay
//// DISTINCT — an integer 5 is not the string "5" — because that distinction is part of
//// link identity (FR-007 keying).

import gleam/list
import gleam/option
import gleam/result
import gleam/string
import glp/link/primitives/link_establish.{type Role, Connector, Listener}
import glp/link/seam/link_address.{type LinkAddress}
import glp/link/seam/link_id.{type LinkId, LinkId, NonceInt, NonceStr}
import glp/link/seam/link_scheme.{type LinkScheme}
import glp/runtime/heap.{type Heap}
import glp/runtime/terms.{
  type Constant, type Term, ConstAtom, ConstInt, ConstReal, ConstString,
  ConstTerm, StructTerm, VarRef,
}

/// Why a term could not be marshalled. Each is a distinct GLP-visible outcome; the
/// kernels turn these into a non-fatal abort rather than a crash.
pub type TermError {
  /// A `VarRef` that is not bound to a value — the ground gate. Should be impossible
  /// given the wrappers' `ground/1` guards, so it means an upstream invariant broke.
  NotGround(addr: Int)
  /// The heap refused the dereference (cycle, writer→writer). Surfaced, never swallowed.
  HeapFailure(error: heap.HeapError)
  /// The term is well-formed but not the shape this position requires.
  BadShape(expected: String, got: Term)
}

/// Deep-dereference `term` to a `VarRef`-free tree.
///
/// Threads the heap through (deref does path compression, R-006), so callers MUST use
/// the returned heap rather than the one they passed in.
pub fn ground_resolve(
  heap: Heap,
  term: Term,
) -> Result(#(Heap, Term), TermError) {
  case term {
    ConstTerm(_) -> Ok(#(heap, term))
    VarRef(addr) ->
      case heap.deref(heap, addr) {
        Error(e) -> Error(HeapFailure(e))
        // An unbound writer is the ground gate tripping.
        Ok(#(_h, heap.Unbound(writer))) -> Error(NotGround(writer))
        // Follow through: the bound value may itself contain VarRefs.
        Ok(#(h, heap.Bound(value))) -> ground_resolve(h, value)
      }
    StructTerm(functor, args) -> {
      use #(heap, resolved) <- result.try(ground_resolve_all(heap, args, []))
      Ok(#(heap, StructTerm(functor, resolved)))
    }
  }
}

fn ground_resolve_all(
  heap: Heap,
  args: List(Term),
  acc: List(Term),
) -> Result(#(Heap, List(Term)), TermError) {
  case args {
    [] -> Ok(#(heap, list.reverse(acc)))
    [head, ..rest] -> {
      use #(heap, resolved) <- result.try(ground_resolve(heap, head))
      ground_resolve_all(heap, rest, [resolved, ..acc])
    }
  }
}

/// `link_id(Scheme, Endpoint, Nonce)` → `LinkId`. Expects an ALREADY-resolved term.
pub fn parse_link_id(term: Term) -> Result(LinkId, TermError) {
  case term {
    StructTerm("link_id", [scheme, endpoint, nonce]) -> {
      use scheme <- result.try(parse_scheme(scheme))
      use endpoint <- result.try(parse_endpoint(endpoint))
      use nonce <- result.try(parse_nonce(nonce))
      Ok(LinkId(scheme: scheme, endpoint: endpoint, nonce: nonce))
    }
    other -> Error(BadShape("link_id(Scheme, Endpoint, Nonce)", other))
  }
}

/// `Scheme ::= String`. GLP string literals and atoms both surface as text here — the
/// lexer's quoting choice is not part of link identity, so both are accepted.
///
/// A BLANK scheme is rejected as `BadShape` rather than passed to `link_scheme.of`,
/// which panics on blank. That panic is a correct host-side invariant for internal
/// callers, but kernel arguments come from user programs: `link_id('', …)` must fail
/// the goal, not abort the engine.
pub fn parse_scheme(term: Term) -> Result(LinkScheme, TermError) {
  case term {
    ConstTerm(ConstString(s)) | ConstTerm(ConstAtom(s)) ->
      case string.trim(s) {
        "" -> Error(BadShape("Scheme (non-blank String)", term))
        _ -> Ok(link_scheme.of(s))
      }
    other -> Error(BadShape("Scheme (String)", other))
  }
}

/// `Endpoint ::= String ; ep(String, Integer)` — a bare host/path, or host+port.
/// Blank hosts are rejected for the same reason as blank schemes (see `parse_scheme`).
pub fn parse_endpoint(term: Term) -> Result(LinkAddress, TermError) {
  case term {
    ConstTerm(ConstString(s)) | ConstTerm(ConstAtom(s)) ->
      case string.trim(s) {
        "" -> Error(BadShape("Endpoint (non-blank String)", term))
        _ -> Ok(link_address.path(s))
      }
    StructTerm("ep", [host, ConstTerm(ConstInt(port))]) ->
      case host {
        ConstTerm(ConstString(h)) | ConstTerm(ConstAtom(h)) ->
          case string.trim(h) {
            "" -> Error(BadShape("ep host (non-blank String)", host))
            _ -> Ok(link_address.endpoint(h, port))
          }
        other -> Error(BadShape("ep(String, Integer) host", other))
      }
    other -> Error(BadShape("Endpoint (String | ep(String, Integer))", other))
  }
}

/// `Nonce ::= Integer ; String`. The two forms stay DISTINCT — part of link identity.
pub fn parse_nonce(term: Term) -> Result(link_id.LinkNonce, TermError) {
  case term {
    ConstTerm(ConstInt(i)) -> Ok(NonceInt(i))
    ConstTerm(ConstString(s)) -> Ok(NonceStr(s))
    ConstTerm(ConstAtom(s)) -> Ok(NonceStr(s))
    other -> Error(BadShape("Nonce (Integer | String)", other))
  }
}

/// `LinkRole ::= listener ; connector`. Establishment role only — independent of data
/// direction (FR-004).
pub fn parse_role(term: Term) -> Result(Role, TermError) {
  case term {
    ConstTerm(ConstAtom("listener")) -> Ok(Listener)
    ConstTerm(ConstAtom("connector")) -> Ok(Connector)
    ConstTerm(ConstString("listener")) -> Ok(Listener)
    ConstTerm(ConstString("connector")) -> Ok(Connector)
    other -> Error(BadShape("LinkRole (listener | connector)", other))
  }
}

/// `Rendezvous ::= rendezvous(Scheme, Endpoint)` — path B's address term (C4). NOT a
/// LinkId: a rendezvous has no nonce, because it is not a link.
pub fn parse_rendezvous(
  term: Term,
) -> Result(#(LinkScheme, LinkAddress), TermError) {
  case term {
    StructTerm("rendezvous", [scheme, endpoint]) -> {
      use scheme <- result.try(parse_scheme(scheme))
      use endpoint <- result.try(parse_endpoint(endpoint))
      Ok(#(scheme, endpoint))
    }
    other -> Error(BadShape("rendezvous(Scheme, Endpoint)", other))
  }
}

/// `RequestMsg ::= request(LinkId, AgentId)` (self.glp:467) — the in-band token the
/// path-B connector ships and the listener surfaces on `accept_link`'s RequestStream.
/// `from_peer` is a ground AgentId term; the base ships the placeholder `requester`
/// (authenticated origin binding is FR-026/T075), kept as a raw term here.
pub fn request_token(id: LinkId, from_peer: Term) -> Term {
  StructTerm("request", [link_id_to_term(id), from_peer])
}

/// Parse an in-band `request(LinkId, FromPeer)` token → the LinkId + the raw ground
/// FromPeer AgentId term (the base does not interpret it beyond ground identity).
pub fn parse_request_token(term: Term) -> Result(#(LinkId, Term), TermError) {
  case term {
    StructTerm("request", [link_id_term, from_peer]) -> {
      use id <- result.try(parse_link_id(link_id_term))
      Ok(#(id, from_peer))
    }
    other -> Error(BadShape("request(LinkId, AgentId)", other))
  }
}

/// `Reason ::= String` — a close/fault reason (C7/C8).
pub fn parse_reason(term: Term) -> Result(String, TermError) {
  case term {
    ConstTerm(ConstString(s)) -> Ok(s)
    ConstTerm(ConstAtom(s)) -> Ok(s)
    other -> Error(BadShape("Reason (String)", other))
  }
}

// ── host → term (building GLP terms the program reads) ──────────────────────

/// `LinkId` → `link_id(Scheme, Endpoint, Nonce)`.
///
/// Rebuilt terms must be `=?=`-comparable with the source literal the program wrote,
/// so the component forms are chosen to round-trip: a scheme/endpoint built from text
/// goes back as an ATOM (the form a GLP source literal like `tcp` lexes to), and a
/// nonce goes back in the form it arrived. The C# oracle solves the same problem with
/// its `Requote`/`Unquote` pair.
pub fn link_id_to_term(id: LinkId) -> Term {
  StructTerm("link_id", [
    ConstTerm(ConstAtom(link_scheme.name(id.scheme))),
    link_address_to_term(id.endpoint),
    case id.nonce {
      NonceInt(i) -> ConstTerm(ConstInt(i))
      NonceStr(s) -> ConstTerm(ConstAtom(s))
    },
  ])
}

/// The ground `AgentId` naming the SINGLE far end of a bilateral link (FR-005), derived
/// from the link's own ground `LinkId`. Used where the host must supply a `ToPeer` it was
/// not handed — the C5 egress drainer, lowered as `link_drain(Out?, LinkId?, ToPeer?)`,
/// whose `ground(ToPeer?)` guard needs a ground `AgentId` while `'_link_setup'/5` (path A)
/// carries no peer argument at all.
///
/// The mapping is total and stays inside the shipped type (`self.glp:447`):
/// `Endpoint ::= String` → `AgentId ::= String`, and `ep(Host, Port)` → `peer(Host, Port)`.
///
/// **K1 (path A) ONLY.** The path-B kernels have a real peer — K3's `ToPeer`, K5's
/// `FromPeer` — and pass it (Gabi's ruling 2026-07-27), so a path-B drainer names the
/// actual counterparty and a path-A drainer names the endpoint. Do not extend this to
/// path B for symmetry's sake. The asymmetry was raised against FR-002/R-5 and ruled
/// acceptable because `ToPeer` is inert on the wire: `'_link_send'/3` validates it ground
/// and routes by `LinkId` alone, so no observable artefact distinguishes the two paths.
pub fn bilateral_peer(id: LinkId) -> Term {
  case id.endpoint.port {
    option.None -> ConstTerm(ConstAtom(id.endpoint.host))
    option.Some(port) ->
      StructTerm("peer", [
        ConstTerm(ConstAtom(id.endpoint.host)),
        ConstTerm(ConstInt(port)),
      ])
  }
}

fn link_address_to_term(address: LinkAddress) -> Term {
  case address.port {
    option.None -> ConstTerm(ConstAtom(address.host))
    option.Some(port) ->
      StructTerm("ep", [
        ConstTerm(ConstAtom(address.host)),
        ConstTerm(ConstInt(port)),
      ])
  }
}

/// Render a constant for diagnostics (kernel abort details).
pub fn describe_constant(c: Constant) -> String {
  case c {
    ConstAtom(s) -> s
    ConstString(s) -> "\"" <> s <> "\""
    ConstInt(_) -> "<integer>"
    ConstReal(_) -> "<real>"
  }
}
