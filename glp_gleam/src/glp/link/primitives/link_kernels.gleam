//// glp/link/primitives/link_kernels — the link-layer effectful kernels + the parallel
//// effect seam `LinkOutcome` (feature 050, T050.C2).
////
//// Mirrors `glp/mad/mad_kernels` exactly. The pure engine's body kernels are
//// `kernels.KernelOutcome` over the HEAP alone; the link kernels are effectful over the
//// per-engine `LinkState` (`link_runtime`), so they get a PARALLEL outcome type
//// (`LinkOutcome`) rather than a widened `KernelOutcome` — the same call the operator
//// ratified for madGLP's `_send` (E5, 2026-07-14), for the same reason (widening
//// `KernelOutcome` touches ~30 dispatch arms + runner + scheduler; see
//// `contracts/link-primitives-port.md` §3/§5 D-5). The runner dispatches these at the
//// BODY Spawn label-miss, AFTER the pure `kernels.dispatch` misses and only when the
//// reduction carries a `LinkState` (`runner.link_spawn`).
////
//// **C2 scope: ONLY K1 `'_link_setup'/5`.** The other six ratified kernels
//// (`_link_send` / `_link_request` / `_link_listen` / `_link_accept` / `_link_monitor` /
//// `_link_close`) land in C3–C8; `link_is_kernel` and `link_dispatch` grow with each step
//// and are kept in lockstep, so a recognized kernel always has a dispatch arm.
////
//// The K1 arm is the whole of `'_link_setup'/5` (self.glp:475/489-492): ground-resolve +
//// parse the `LinkId` and the `LinkRole` (the ground gate — the wrappers already guarded
//// `ground/1`, so a non-ground here is an upstream invariant break, surfaced not
//// defaulted), converge on `link_establish.wire_established_link` — the ONE funnel that
//// produces an established link (R-5) — and record the `In`/`Out`/`Faults` heap stream
//// cursors on the handle. Arming the egress drainer (C5) and starting the ingress pump
//// (C6) come later; C2 establishes the link and wires the cursors those steps consume.

import gleam/option
import gleam/result
import gleam/string
import glp/link/primitives/capability_gate
import glp/link/primitives/link_egress
import glp/link/primitives/link_establish
import glp/link/primitives/link_handle
import glp/link/primitives/link_pump
import glp/link/primitives/link_registry.{Established, Reused}
import glp/link/primitives/link_runtime.{type LinkState}
import glp/link/primitives/link_terms
import glp/link/primitives/link_wire
import glp/link/primitives/transport_registry
import glp/link/seam/endpoint.{type Endpoint}
import glp/link/seam/link_id.{type LinkId}
import glp/runtime/heap.{type Heap}
import glp/runtime/suspension.{type GoalRef}
import glp/runtime/terms.{type Term, VarRef}

/// The two-valued outcome of a link effectful kernel — parallel to
/// `kernels.KernelOutcome` and `mad_kernels.MadOutcome`, but over `LinkState`.
pub type LinkOutcome {
  /// Success: the (possibly heap-updated) `LinkState` + any reactivated goals.
  LinkEffect(heap: Heap, state: LinkState, woken: List(GoalRef))
  /// A non-fatal abort — the enclosing goal FAILS, the engine keeps running (NOT the
  /// fatal `KAbort → RunnerError` path). Mirrors `MadAbort` / spec-v5.3-PURE.
  LinkAbort(detail: String)
}

/// Is `name/arity` a link effectful kernel this slice implements? C2 added
/// `_link_setup/5` (path A); C4 adds the three path-B kernels.
pub fn link_is_kernel(name: String, arity: Int) -> Bool {
  case name, arity {
    "_link_setup", 5 -> True
    "_link_listen", 3 -> True
    "_link_request", 5 -> True
    "_link_accept", 5 -> True
    "_link_send", 3 -> True
    _, _ -> False
  }
}

/// Dispatch a link effectful kernel over `args`, given the current `LinkState`.
/// `Error(Nil)` means `name/arity` is not a link kernel this slice implements (the runner
/// then falls through to the unresolved-Spawn report, exactly as for a missed mad kernel).
pub fn link_dispatch(
  heap: Heap,
  state: LinkState,
  name: String,
  arity: Int,
  args: List(Term),
) -> Result(LinkOutcome, Nil) {
  case name, arity, args {
    "_link_setup", 5, [link_id, role, in_arg, out_arg, faults_arg] ->
      Ok(link_setup_kernel(
        heap,
        state,
        link_id,
        role,
        in_arg,
        out_arg,
        faults_arg,
      ))
    "_link_listen", 3, [scheme, endpoint_arg, requests] ->
      Ok(link_listen_kernel(heap, state, scheme, endpoint_arg, requests))
    "_link_request", 5, [link_id, to_peer, in_arg, out_arg, faults_arg] ->
      Ok(link_request_kernel(
        heap,
        state,
        link_id,
        to_peer,
        in_arg,
        out_arg,
        faults_arg,
      ))
    "_link_accept", 5, [link_id, from_peer, in_arg, out_arg, faults_arg] ->
      Ok(link_accept_kernel(
        heap,
        state,
        link_id,
        from_peer,
        in_arg,
        out_arg,
        faults_arg,
      ))
    "_link_send", 3, [msg, link_id, to_peer] ->
      Ok(link_send_kernel(heap, state, msg, link_id, to_peer))
    _, _, _ -> Error(Nil)
  }
}

/// `'_link_setup'(LinkId?, Role?, In, Out?, Faults)` — K1 (self.glp:475/489-492).
/// path-A establish-or-reuse, idempotent at ground identity (FR-007). See module header.
fn link_setup_kernel(
  heap: Heap,
  state: LinkState,
  link_id_arg: Term,
  role_arg: Term,
  in_arg: Term,
  out_arg: Term,
  faults_arg: Term,
) -> LinkOutcome {
  case
    try_link_setup(heap, state, link_id_arg, role_arg, in_arg, out_arg, faults_arg)
  {
    Ok(outcome) -> outcome
    Error(detail) -> LinkAbort(detail)
  }
}

fn try_link_setup(
  heap: Heap,
  state: LinkState,
  link_id_arg: Term,
  role_arg: Term,
  in_arg: Term,
  out_arg: Term,
  faults_arg: Term,
) -> Result(LinkOutcome, String) {
  // 1. Ground-resolve + parse the LinkId and the Role (the ground gate).
  use #(heap, id_term) <- result.try(resolve(heap, link_id_arg, "LinkId"))
  use id <- result.try(link_terms.parse_link_id(id_term) |> term_err("LinkId"))
  use #(heap, role_term) <- result.try(resolve(heap, role_arg, "Role"))
  use role <- result.try(link_terms.parse_role(role_term) |> term_err("Role"))
  // 2. The In/Out/Faults heap stream cursors are the arg writer/reader addresses.
  use in_addr <- result.try(cursor_addr(in_arg, "In"))
  use out_addr <- result.try(cursor_addr(out_arg, "Out"))
  use faults_addr <- result.try(cursor_addr(faults_arg, "Faults"))
  // 3. Converge on the ONE establish funnel. Path A: NOT pre-gated, NO pre-opened
  //    endpoint (`adopt: None`) — the leaf opens by role inside the funnel. Path A has
  //    NO peer argument, so the drainer's `ToPeer` is derived from the LinkId (FR-005).
  wire_and_record(
    heap,
    state,
    id,
    role,
    option.None,
    False,
    in_addr,
    out_addr,
    faults_addr,
    link_terms.bilateral_peer(id),
  )
}

/// The shared tail of every establishing kernel (K1/K3/K5): funnel through
/// `wire_established_link` — the ONE code path that produces an established link (R-5) —
/// and on genuine first establishment record the In/Out/Faults cursors the pump / egress
/// / faults steps (C5–C7) consume. On idempotent reuse (FR-007) thread the registry back
/// without re-wiring — the existing handle already drives the peer's frames. `adopt`
/// (path B) adopts the endpoint the kernel already opened; `pre_gated` skips the funnel's
/// gate for a kernel that gated itself. The address passed is the LinkId's own endpoint.
///
/// **Egress arming (C5, deviation D-2 option (a) — Gabi §1.14 approval 2026-07-27).** On
/// genuine first establishment this also REQUESTS the Out-drainer: the scheduler lowers a
/// runnable `link_drain(Out?, LinkId, ToPeer)` goal whose suspension on the `Out` reader
/// replaces the oracle's `heap.OnBind`. It is requested here, in the ONE convergence tail,
/// for the same reason establishment itself converges here — a second arming site would
/// break R-5 no matter how carefully it mirrored this one. The `Reused` arm deliberately
/// does NOT arm: FR-007 idempotency would otherwise put two drainers on one `Out` stream.
///
/// `to_peer` is the ground `AgentId` the lowered drainer's `ground(ToPeer?)` guard needs.
/// **Gabi's ruling 2026-07-27: the path-B kernels pass their REAL peer** — K3's `ToPeer`,
/// K5's `FromPeer`, both already ground-resolved at the kernel — while K1 (path A), which
/// has no peer argument at all, derives one from the LinkId (`link_terms.bilateral_peer`).
/// So the two paths' drainer goals DIFFER in this third argument. That was flagged against
/// FR-002/R-5 and ruled acceptable: `ToPeer` is inert on the wire (`'_link_send'/3`
/// validates it ground and routes by `LinkId`, FR-005), so nothing downstream — registry,
/// handle, frames — can tell the two paths apart. Do not "restore symmetry" by discarding
/// the real peer; path B knows who it is talking to and says so.
fn wire_and_record(
  heap: Heap,
  state: LinkState,
  id: LinkId,
  role: link_establish.Role,
  adopt: option.Option(Endpoint),
  pre_gated: Bool,
  in_addr: Int,
  out_addr: Int,
  faults_addr: Int,
  to_peer: Term,
) -> Result(LinkOutcome, String) {
  let ctx = link_runtime.establish_context(state)
  case
    link_establish.wire_established_link(
      ctx,
      state.links,
      id,
      role,
      id.endpoint,
      adopt,
      pre_gated,
    )
  {
    Error(e) -> Error("establishment failed: " <> string.inspect(e))
    Ok(#(registry, Established(handle))) -> {
      let wired = link_handle.with_cursors(handle, in_addr, out_addr, faults_addr)
      let state = link_runtime.with_links(state, link_registry.put(registry, wired))
      // Arm egress ONCE, on first establishment only (see the doc comment above).
      let state =
        link_runtime.request_drain(
          state,
          out_addr,
          link_terms.link_id_to_term(id),
          to_peer,
        )
      // Start ingress (C6) — AFTER the cursors are wired, which is what `start` checks:
      // a frame arriving before `in_writer` exists would have nowhere to go. Same
      // once-per-link rule as egress; the `Reused` arm below starts no second loop.
      case link_pump.start(state.inbox, wired) {
        Error(detail) -> Error(detail)
        Ok(Nil) -> Ok(LinkEffect(heap, state, []))
      }
    }
    Ok(#(registry, Reused(_handle))) ->
      Ok(LinkEffect(heap, link_runtime.with_links(state, registry), []))
  }
}

/// Deep-dereference an argument to a ground `VarRef`-free term, mapping the failure to a
/// kernel-abort detail. Threads the heap (deref path-compresses).
fn resolve(heap: Heap, term: Term, what: String) -> Result(#(Heap, Term), String) {
  link_terms.ground_resolve(heap, term)
  |> result.map_error(fn(e) {
    "_link_setup: " <> what <> " not ground: " <> string.inspect(e)
  })
}

fn term_err(r: Result(a, e), what: String) -> Result(a, String) {
  result.map_error(r, fn(e) {
    "_link_setup: bad " <> what <> ": " <> string.inspect(e)
  })
}

/// The heap cursor for a stream argument is its `VarRef` address. A non-`VarRef` here
/// means the wrapper passed something other than an unbound stream variable — surfaced.
fn cursor_addr(term: Term, what: String) -> Result(Int, String) {
  case term {
    VarRef(addr) -> Ok(addr)
    _ ->
      Error(
        "kernel: "
        <> what
        <> " must be an unbound stream variable, got "
        <> string.inspect(term),
      )
  }
}

// ── K4 `'_link_listen'/3` (Scheme?, Endpoint?, Requests) — path-B LISTEN half ──
//
// Bind a transport rendezvous, raw-read the inbound connection's first frame as a
// `request(LinkId, FromPeer)` token (BEFORE the data pump, so the data sequence still
// starts at 0), PARK the accepted-but-unmatched endpoint in `LinkState.pending` keyed by
// the token's ground LinkId, and surface `[request(LinkId, FromPeer) | Tail]` on the
// Requests stream — where `accept_link/4` reads it and `=?=`-matches the LinkId. Base
// MVP: ONE request per rendezvous (the seam returns a single endpoint; a multi-request
// accept-loop is a transport-leaf Phase-6 concern). This does NOT gate — admission is the
// `accept_link` end's concern (K5), since the LinkId to serve is chosen there.

fn link_listen_kernel(
  heap: Heap,
  state: LinkState,
  scheme_arg: Term,
  endpoint_arg: Term,
  requests_arg: Term,
) -> LinkOutcome {
  case try_link_listen(heap, state, scheme_arg, endpoint_arg, requests_arg) {
    Ok(outcome) -> outcome
    Error(detail) -> LinkAbort(detail)
  }
}

fn try_link_listen(
  heap: Heap,
  state: LinkState,
  scheme_arg: Term,
  endpoint_arg: Term,
  requests_arg: Term,
) -> Result(LinkOutcome, String) {
  use #(heap, scheme_term) <- result.try(resolve(heap, scheme_arg, "Scheme"))
  use scheme <- result.try(
    link_terms.parse_scheme(scheme_term) |> term_err("Scheme"),
  )
  use #(heap, endpoint_term) <- result.try(resolve(heap, endpoint_arg, "Endpoint"))
  use addr <- result.try(
    link_terms.parse_endpoint(endpoint_term) |> term_err("Endpoint"),
  )
  use req_addr <- result.try(writer_addr(heap, requests_arg, "Requests"))
  case transport_registry.select(state.transports, scheme) {
    Error(e) -> Error("_link_listen: no transport: " <> string.inspect(e))
    Ok(leaf) ->
      // listen + the token read both block until a requester connects + sends; the
      // requester runs in another engine/process, so this is not a self-deadlock.
      case leaf.listen(scheme, addr, state.options) {
        Error(sig) -> Error("_link_listen: rendezvous failed: " <> string.inspect(sig))
        Ok(ep) ->
          case read_token(ep) {
            Error(d) -> Error("_link_listen: " <> d)
            Ok(token) ->
              case link_terms.parse_request_token(token) |> term_err("request token") {
                Error(d) -> Error(d)
                Ok(#(id, from_peer)) -> {
                  // Park the accepted endpoint for '_link_accept' to adopt.
                  let state = link_runtime.park_pending(state, id, ep)
                  // Surface [request(id, FromPeer) | Tail?] on the Requests writer.
                  let #(heap, _tail_w, tail_r) = heap.allocate_variable(heap)
                  let cons =
                    terms.cons(link_terms.request_token(id, from_peer), VarRef(tail_r))
                  case heap.bind_writer(heap, req_addr, cons) {
                    Error(e) ->
                      Error("_link_listen: cannot bind Requests: " <> string.inspect(e))
                    Ok(#(heap, woken)) -> Ok(LinkEffect(heap, state, woken))
                  }
                }
              }
          }
      }
  }
}

// ── K3 `'_link_request'/5` (LinkId?, ToPeer?, In, Out?, Faults) — path-B CONNECT half ──
//
// Verify-before-act (gate on the ground LinkId BEFORE opening anything), connect to the
// peer's rendezvous, ship the in-band `request(LinkId, requester)` token as a RAW frame,
// then ADOPT that same connection through the shared establish funnel (pre-gated) — so
// the resulting link is indistinguishable from a listen/connect one (R-5, FR-002).

fn link_request_kernel(
  heap: Heap,
  state: LinkState,
  id_arg: Term,
  to_peer_arg: Term,
  in_arg: Term,
  out_arg: Term,
  faults_arg: Term,
) -> LinkOutcome {
  case
    try_link_request(heap, state, id_arg, to_peer_arg, in_arg, out_arg, faults_arg)
  {
    Ok(outcome) -> outcome
    Error(detail) -> LinkAbort(detail)
  }
}

fn try_link_request(
  heap: Heap,
  state: LinkState,
  id_arg: Term,
  to_peer_arg: Term,
  in_arg: Term,
  out_arg: Term,
  faults_arg: Term,
) -> Result(LinkOutcome, String) {
  use #(heap, id_term) <- result.try(resolve(heap, id_arg, "LinkId"))
  use id <- result.try(link_terms.parse_link_id(id_term) |> term_err("LinkId"))
  // ToPeer must be ground (base placeholder; authenticated origin is FR-026/T075). Kept,
  // not discarded: this is the peer the C5 drainer is lowered with (Gabi 2026-07-27).
  use #(heap, to_peer) <- result.try(resolve(heap, to_peer_arg, "ToPeer"))
  use in_addr <- result.try(cursor_addr(in_arg, "In"))
  use out_addr <- result.try(cursor_addr(out_arg, "Out"))
  use faults_addr <- result.try(cursor_addr(faults_arg, "Faults"))
  // Verify-before-act: gate BEFORE the connection or token ship (fail-closed on refusal).
  use _ <- result.try(pre_gate(state, id, capability_gate.Outbound))
  case transport_registry.select(state.transports, id.scheme) {
    Error(e) -> Error("_link_request: no transport: " <> string.inspect(e))
    Ok(leaf) ->
      case leaf.connect(id.scheme, id.endpoint, state.options) {
        Error(sig) -> Error("_link_request: connect failed: " <> string.inspect(sig))
        Ok(ep) -> {
          let token =
            link_terms.request_token(id, terms.ConstTerm(terms.ConstAtom("requester")))
          case ship_token(ep, token) {
            Error(d) -> Error("_link_request: " <> d)
            // Adopt the SAME handshake connection (path B: adopt + pre-gated).
            Ok(Nil) ->
              wire_and_record(
                heap,
                state,
                id,
                link_establish.Connector,
                option.Some(ep),
                True,
                in_addr,
                out_addr,
                faults_addr,
                to_peer,
              )
          }
        }
      }
  }
}

// ── K5 `'_link_accept'/5` (LinkId?, FromPeer?, In, Out?, Faults) — path-B ACCEPT half ──
//
// The GLP `=?=` guard already matched the requested LinkId against one this end serves,
// so the kernel ADOPTS the connection `'_link_listen'` parked for that ground LinkId and
// establishes through the shared funnel (pre-gated) — converging on the same registry as
// listen/connect (R-5, FR-002). Verify-before-act: gate before wiring; a refused peer's
// parked connection is disposed rather than left half-open.

fn link_accept_kernel(
  heap: Heap,
  state: LinkState,
  id_arg: Term,
  from_peer_arg: Term,
  in_arg: Term,
  out_arg: Term,
  faults_arg: Term,
) -> LinkOutcome {
  case
    try_link_accept(heap, state, id_arg, from_peer_arg, in_arg, out_arg, faults_arg)
  {
    Ok(outcome) -> outcome
    Error(detail) -> LinkAbort(detail)
  }
}

fn try_link_accept(
  heap: Heap,
  state: LinkState,
  id_arg: Term,
  from_peer_arg: Term,
  in_arg: Term,
  out_arg: Term,
  faults_arg: Term,
) -> Result(LinkOutcome, String) {
  use #(heap, id_term) <- result.try(resolve(heap, id_arg, "LinkId"))
  use id <- result.try(link_terms.parse_link_id(id_term) |> term_err("LinkId"))
  // FromPeer — the requester `'_link_listen'` surfaced in the token. Kept, not discarded:
  // it is the peer the C5 drainer is lowered with (Gabi 2026-07-27).
  use #(heap, from_peer) <- result.try(resolve(heap, from_peer_arg, "FromPeer"))
  use in_addr <- result.try(cursor_addr(in_arg, "In"))
  use out_addr <- result.try(cursor_addr(out_arg, "Out"))
  use faults_addr <- result.try(cursor_addr(faults_arg, "Faults"))
  // Adopt the parked endpoint '_link_listen' surfaced for this LinkId.
  case link_runtime.take_pending(state, id) {
    Error(_) ->
      Error(
        "_link_accept: no pending request for the link — request_listener must surface it first",
      )
    Ok(#(state, ep)) ->
      // Verify-before-act: gate AFTER adopting but BEFORE establishing; a refused peer's
      // connection is disposed, never wired.
      case pre_gate(state, id, capability_gate.Inbound) {
        Error(reason) -> {
          let _ = ep.close()
          Error("_link_accept: " <> reason)
        }
        Ok(Nil) ->
          wire_and_record(
            heap,
            state,
            id,
            link_establish.Listener,
            option.Some(ep),
            True,
            in_addr,
            out_addr,
            faults_addr,
            from_peer,
          )
      }
  }
}

// ── K2 `'_link_send'/3` (Msg?, LinkId?, ToPeer?) — the LinkId-keyed SENDER face ──
//
// The host body of `out_relay/3` (self.glp:546-549), whose guards already certified all
// three arguments ground. The LinkId-keyed twin of the channel face `link_send/3` (which
// conses onto `Out` for the egress drainer); BOTH converge on the one
// `link_egress.ship_ground` routine, so the two faces can never diverge on the wire
// (R-5's egress form).
//
// GROUND-RELAY, NOT globalize (deviation D-4, RATIFIED): a deep-dereferenced ground copy
// crosses the cut (FR-040) — no globalize, no `_w`/`_r` minting, no open structure.
//
// "Send before setup" is a CALLER BUG, surfaced as a non-fatal abort — the base does not
// invent a suspend-until-established semantics the spec is silent on (C# oracle, verbatim
// rationale). Nothing is bound and no goal is woken: a send is a pure host effect.

fn link_send_kernel(
  heap: Heap,
  state: LinkState,
  msg_arg: Term,
  id_arg: Term,
  to_peer_arg: Term,
) -> LinkOutcome {
  case try_link_send(heap, state, msg_arg, id_arg, to_peer_arg) {
    Ok(outcome) -> outcome
    Error(detail) -> LinkAbort(detail)
  }
}

fn try_link_send(
  heap: Heap,
  state: LinkState,
  msg_arg: Term,
  id_arg: Term,
  to_peer_arg: Term,
) -> Result(LinkOutcome, String) {
  // arg 2 — the ground LinkId selects the established link.
  use #(heap, id_term) <- result.try(resolve(heap, id_arg, "LinkId"))
  use id <- result.try(link_terms.parse_link_id(id_term) |> term_err("LinkId"))
  // arg 3 — ToPeer is validated-ground for the relay record, NOT routed on: the link is
  // bilateral (FR-005) so the LinkId already names the single far end.
  use #(heap, _to_peer) <- result.try(resolve(heap, to_peer_arg, "ToPeer"))
  // The link must already be established (FR-007 registry).
  case link_registry.try_get(state.links, id) {
    Error(_) ->
      Error(
        "_link_send: no established link for "
        <> string.inspect(id)
        <> " — setup before send",
      )
    Ok(handle) ->
      // arg 1 — the ground-relay ship, shared with the Out-stream egress drainer.
      case link_egress.ship_ground(heap, handle, msg_arg) {
        Error(e) -> Error("_link_send: Msg not shipped: " <> string.inspect(e))
        // Thread the advanced handle (its sequence number was consumed) back into the
        // registry, or the next send would reuse this message id.
        Ok(#(heap, advanced)) -> {
          let state =
            link_runtime.with_links(state, link_registry.put(state.links, advanced))
          Ok(LinkEffect(heap, state, []))
        }
      }
  }
}

// ── shared path-B helpers ─────────────────────────────────────────────────────

/// Run the scheme's capability gate on the ground LinkId (verify-before-act, FR-008). A
/// refusal — or a gate that cannot even be evaluated — fails CLOSED (`admit` returns
/// `Refused`). Base schemes resolve to the allow-all default (D-7).
fn pre_gate(
  state: LinkState,
  id: LinkId,
  direction: capability_gate.Direction,
) -> Result(Nil, String) {
  let request =
    capability_gate.Request(id: id, scheme: id.scheme, direction: direction)
  case capability_gate.admit(state.gates, request) {
    capability_gate.Allowed -> Ok(Nil)
    capability_gate.Refused(reason) ->
      Error("capability refused (fail-closed): " <> reason)
  }
}

/// A stream OUTPUT argument (the Requests writer) must be an unbound WRITER cell — its
/// paired reader would dereference to it and erase the writer identity.
fn writer_addr(heap: Heap, term: Term, what: String) -> Result(Int, String) {
  case term {
    VarRef(addr) ->
      case heap.is_writer(heap, addr) {
        True -> Ok(addr)
        False -> Error(what <> " must be an unbound writer cell")
      }
    _ -> Error(what <> " must be an unbound variable")
  }
}

/// Blocking raw read of one whole ground token off the endpoint (the out-of-band request
/// frame, consumed BEFORE the data pump engages). A clean EOS before any frame is a peer
/// that closed without requesting — surfaced, never swallowed.
fn read_token(ep: Endpoint) -> Result(Term, String) {
  case ep.recv() {
    Error(sig) -> Error("token recv failed: " <> string.inspect(sig))
    Ok(option.None) -> Error("peer closed before sending a request token")
    Ok(option.Some(frame)) ->
      case link_wire.decode_token(frame) {
        Error(e) -> Error("malformed request token: " <> string.inspect(e))
        Ok(term) -> Ok(term)
      }
  }
}

/// Ship one ground token as a raw out-of-band frame on the endpoint.
fn ship_token(ep: Endpoint, token: Term) -> Result(Nil, String) {
  case link_wire.encode_token(token) {
    Error(e) -> Error("cannot encode request token: " <> string.inspect(e))
    Ok(frame) ->
      case ep.send(frame) {
        Error(sig) -> Error("cannot send request token: " <> string.inspect(sig))
        Ok(Nil) -> Ok(Nil)
      }
  }
}
