//// glp/link/primitives/link_driver — effectful `_link_*` kernel dispatch over the
//// link runtime (feature 059, T074 — the driver the runner routes `_link_*` labels
//// to when a `LinkRuntime` is threaded; port of the synchronous body of
//// glp_runtime/lib/link/primitives/link_setup_kernel.dart + link_establish.dart).
////
//// This is the LINK analogue of `mad_kernels.mad_dispatch`: the runner calls
//// `dispatch` at the BODY Spawn label-miss with the threaded `LinkRuntime`, and the
//// returned `LinkOutcome` threads the updated runtime + heap + reactivations forward
//// (runner `link_spawn`). Establishment is SYNCHRONOUS here (the Gleam transport seam
//// is blocking `Result`, not Dart's async `Future`): `_link_setup` selects a
//// transport, BLOCKS in listen/connect until the rendezvous resolves, registers the
//// handle idempotently (FR-007), and wires the In/Out/Faults heap cursors onto it.
//// The async data-flow — extending `In` as inbound frames arrive, draining `Out` —
//// is the pump's job (`link_pump`), not the kernel's.
////
//// `LinkOutcome` lives HERE (not in the runner) so the runner→link_driver dependency
//// stays one-way (link_driver never imports the runner).

import gleam/int
import gleam/option.{Some}
import glp/link/primitives/link_handle
import glp/link/primitives/link_registry
import glp/link/primitives/link_runtime.{type LinkRuntime}
import glp/link/primitives/link_terms
import glp/link/primitives/link_wire
import glp/link/primitives/transport_registry
import glp/link/seam/endpoint.{type Endpoint}
import glp/link/seam/link_id.{type LinkId}
import glp/link/seam/link_options
import glp/link/seam/link_scheme.{type LinkScheme}
import glp/link/seam/link_address.{type LinkAddress}
import glp/runtime/heap.{type Heap}
import glp/runtime/suspension.{type GoalRef}
import glp/runtime/terms.{type Term, ConstAtom, ConstTerm, VarRef, cons}

/// The two-valued outcome of an effectful link kernel — parallel to
/// `mad_kernels.MadOutcome`, but over `LinkRuntime` not `MadState`.
pub type LinkOutcome {
  /// Success: the (possibly heap-updated) `LinkRuntime` + any reactivated goals.
  LinkEffect(heap: Heap, runtime: LinkRuntime, woken: List(GoalRef))
  /// A non-fatal abort — the enclosing goal fails, the engine continues (NOT the
  /// fatal `RunnerError` path). The ground/establish/idempotency gate failed.
  LinkAbort(detail: String)
}

/// Dispatch an effectful `_link_*` kernel over `args`, given the current
/// `LinkRuntime`. The runner has already checked `is_link_kernel`, so an unhandled
/// name/arity here is a not-yet-ported kernel (a `LinkAbort`, never silent).
pub fn dispatch(
  heap: Heap,
  runtime: LinkRuntime,
  name: String,
  arity: Int,
  args: List(Term),
) -> LinkOutcome {
  case name, arity, args {
    "_link_setup", 5, [id_t, role_t, in_t, out_t, faults_t] ->
      link_setup(heap, runtime, id_t, role_t, in_t, out_t, faults_t)
    // Path-B handshake (FR-002 Option A): connector / rendezvous-listener / accept.
    "_link_request", 5, [id_t, to_peer_t, in_t, out_t, faults_t] ->
      link_request(heap, runtime, id_t, to_peer_t, in_t, out_t, faults_t)
    "_link_listen", 3, [scheme_t, endpoint_t, requests_t] ->
      link_listen(heap, runtime, scheme_t, endpoint_t, requests_t)
    "_link_accept", 5, [id_t, from_peer_t, in_t, out_t, faults_t] ->
      link_accept(heap, runtime, id_t, from_peer_t, in_t, out_t, faults_t)
    // The LinkId-keyed `_link_send`/`_link_monitor`/`_link_close` kernels are not yet
    // ported (no acceptance program exercises them: the base send/close/monitor rides
    // the self.glp channel wrappers over the pump).
    _, _, _ ->
      LinkAbort(
        "link kernel " <> name <> "/" <> int.to_string(arity) <> " not yet ported",
      )
  }
}

// ── path-B handshake (T033, faithful port) ────────────────────────────────────

/// `'_link_request'(LinkId?, ToPeer?, In, Out?, Faults)` — the CONNECTOR half. Connect
/// to the peer's rendezvous, ship the in-band `request(LinkId, requester)` token as a
/// raw out-of-band frame (message_id 0, so the data sequence still starts at 0), then
/// wire the SAME connection as the data link (converging on the shared registry, R-5).
/// The token's `FromPeer` is the base placeholder `requester` (authenticated origin is
/// T075), faithful to link_request_kernel.dart.
fn link_request(
  heap: Heap,
  runtime: LinkRuntime,
  id_t: Term,
  to_peer_t: Term,
  in_t: Term,
  out_t: Term,
  faults_t: Term,
) -> LinkOutcome {
  case parse_id(heap, id_t), require_ground(heap, to_peer_t, "ToPeer") {
    Ok(id), Ok(_) ->
      case cursors(heap, in_t, out_t, faults_t) {
        Error(why) -> LinkAbort(why)
        Ok(#(in_writer, out_reader, faults_writer)) ->
          case link_registry.contains(runtime.links, id) {
            True -> LinkAbort("re-establishment of already-established LinkId (FR-007)")
            False ->
              case connect_and_handshake(runtime, id) {
                Error(why) -> LinkAbort(why)
                Ok(ep) ->
                  wire(runtime, id, ep, in_writer, out_reader, faults_writer, heap)
              }
          }
      }
    Error(why), _ -> LinkAbort(why)
    _, Error(why) -> LinkAbort(why)
  }
}

/// Connect to `id`'s rendezvous and ship the in-band request token, returning the
/// connection to adopt as the data link.
fn connect_and_handshake(runtime: LinkRuntime, id: LinkId) -> Result(Endpoint, String) {
  case transport_registry.select(runtime.transports, id.scheme) {
    Error(why) -> Error(why)
    Ok(t) ->
      case t.connect(id.scheme, id.endpoint, link_options.default()) {
        Error(signal) -> Error("path-B connect failed: " <> signal.reason)
        Ok(ep) -> {
          let token = link_terms.request_token(id, ConstTerm(ConstAtom("requester")))
          case link_wire.encode_term_frame(token, 0) {
            Error(why) -> Error("request-token encode failed: " <> why)
            Ok(frame) -> {
              let _ = ep.send(frame)
              Ok(ep)
            }
          }
        }
      }
  }
}

/// `'_link_listen'(Scheme?, Endpoint?, Requests)` — the LISTENER concern (Option A).
/// Bind the rendezvous, read the inbound connection's in-band first frame as a
/// `request(LinkId, FromPeer)` token, PARK that connection keyed by the ground LinkId
/// (for `_link_accept`), and surface the token on the `Requests` stream (where
/// `accept_link/4` matches the LinkId with `=?=`). Base-MVP: one request per rendezvous.
fn link_listen(
  heap: Heap,
  runtime: LinkRuntime,
  scheme_t: Term,
  endpoint_t: Term,
  requests_t: Term,
) -> LinkOutcome {
  case parse_scheme_endpoint(heap, scheme_t, endpoint_t) {
    Error(why) -> LinkAbort(why)
    Ok(#(scheme, addr)) ->
      case writer_addr(heap, requests_t, "Requests") {
        Error(why) -> LinkAbort(why)
        Ok(requests_writer) ->
          case rendezvous(runtime, scheme, addr) {
            Error(why) -> LinkAbort(why)
            Ok(#(id, token, ep)) -> {
              // Park the accepted connection for `_link_accept` to adopt.
              let runtime = link_runtime.park_pending(runtime, id, ep)
              // Surface `[token | Requests']` on the Requests stream.
              let #(h2, _fresh_writer, fresh_reader) = heap.allocate_variable(heap)
              case heap.bind_writer(h2, requests_writer, cons(token, VarRef(fresh_reader))) {
                Ok(#(h3, woken)) -> LinkEffect(h3, runtime, woken)
                Error(_) -> LinkAbort("Requests writer already bound")
              }
            }
          }
      }
  }
}

/// Bind the rendezvous, accept one connection, and read its in-band request token.
/// Returns the requested LinkId, the token term (to surface), and the connection.
fn rendezvous(
  runtime: LinkRuntime,
  scheme: LinkScheme,
  addr: LinkAddress,
) -> Result(#(LinkId, Term, Endpoint), String) {
  case transport_registry.select(runtime.transports, scheme) {
    Error(why) -> Error(why)
    Ok(t) ->
      case t.listen(scheme, addr, link_options.default()) {
        Error(signal) -> Error("path-B listen failed: " <> signal.reason)
        Ok(ep) ->
          case ep.recv() {
            Error(signal) -> Error("request-token read failed: " <> signal.reason)
            Ok(option.None) -> Error("peer closed before sending a request token")
            Ok(Some(bytes)) ->
              case link_wire.decode_term_frame(bytes) {
                Error(why) -> Error("request-token decode failed: " <> why)
                Ok(token) ->
                  case link_terms.parse_request_token(token) {
                    Error(why) -> Error("malformed request token: " <> why)
                    Ok(#(id, _from_peer)) -> Ok(#(id, token, ep))
                  }
              }
          }
      }
  }
}

/// `'_link_accept'(LinkId?, FromPeer?, In, Out?, Faults)` — the ACCEPT concern. The
/// GLP `=?=` guard has already matched the requested LinkId against one this end
/// serves; ADOPT the pending connection `_link_listen` parked and wire it as the data
/// link (converging on the same registry as listen/connect, R-5).
fn link_accept(
  heap: Heap,
  runtime: LinkRuntime,
  id_t: Term,
  from_peer_t: Term,
  in_t: Term,
  out_t: Term,
  faults_t: Term,
) -> LinkOutcome {
  case parse_id(heap, id_t), require_ground(heap, from_peer_t, "FromPeer") {
    Ok(id), Ok(_) ->
      case cursors(heap, in_t, out_t, faults_t) {
        Error(why) -> LinkAbort(why)
        Ok(#(in_writer, out_reader, faults_writer)) ->
          case link_runtime.take_pending(runtime, id) {
            Error(Nil) ->
              LinkAbort(
                "no pending request for LinkId — request_listener must surface it first",
              )
            Ok(#(runtime, ep)) ->
              case link_registry.contains(runtime.links, id) {
                True -> LinkAbort("re-establishment of already-established LinkId (FR-007)")
                False ->
                  wire(runtime, id, ep, in_writer, out_reader, faults_writer, heap)
              }
          }
      }
    Error(why), _ -> LinkAbort(why)
    _, Error(why) -> LinkAbort(why)
  }
}

/// Parse a ground LinkId (deep-resolved).
fn parse_id(heap: Heap, id_t: Term) -> Result(LinkId, String) {
  use id_g <- result_try(link_terms.ground_resolve(heap, id_t))
  link_terms.parse_link_id(id_g)
}

/// Parse a ground Scheme + Endpoint (path-B rendezvous, deep-resolved).
fn parse_scheme_endpoint(
  heap: Heap,
  scheme_t: Term,
  endpoint_t: Term,
) -> Result(#(LinkScheme, LinkAddress), String) {
  use scheme_g <- result_try(link_terms.ground_resolve(heap, scheme_t))
  use scheme <- result_try(link_terms.parse_scheme(scheme_g))
  use endpoint_g <- result_try(link_terms.ground_resolve(heap, endpoint_t))
  use addr <- result_try(link_terms.parse_endpoint(endpoint_g))
  Ok(#(scheme, addr))
}

/// Assert a term is ground (the wrapper's `ground/1` guard should have ensured it).
fn require_ground(heap: Heap, t: Term, what: String) -> Result(Nil, String) {
  case link_terms.ground_resolve(heap, t) {
    Ok(_) -> Ok(Nil)
    Error(_) -> Error(what <> " must be a ground peer id")
  }
}

/// `'_link_setup'(LinkId?, Role?, In, Out?, Faults)` (path A, FR-002). Establish or
/// reject-as-duplicate a link for a ground LinkId in a role, wire the three heap
/// cursors, and register the handle.
fn link_setup(
  heap: Heap,
  runtime: LinkRuntime,
  id_t: Term,
  role_t: Term,
  in_t: Term,
  out_t: Term,
  faults_t: Term,
) -> LinkOutcome {
  case parse_id_role(heap, id_t, role_t) {
    Error(why) -> LinkAbort(why)
    Ok(#(id, role)) ->
      case cursors(heap, in_t, out_t, faults_t) {
        Error(why) -> LinkAbort(why)
        Ok(#(in_writer, out_reader, faults_writer)) ->
          // Idempotency at link-identity (FR-007): re-establishment of the same
          // ground LinkId is surfaced (cell-aliasing unspecified — first only).
          case link_registry.contains(runtime.links, id) {
            True ->
              LinkAbort(
                "re-establishment of already-established LinkId (FR-007): first only",
              )
            False ->
              case establish(runtime, id, role) {
                Error(why) -> LinkAbort(why)
                Ok(ep) ->
                  wire(runtime, id, ep, in_writer, out_reader, faults_writer, heap)
              }
          }
      }
  }
}

fn wire(
  runtime: LinkRuntime,
  id: LinkId,
  ep: Endpoint,
  in_writer: Int,
  out_reader: Int,
  faults_writer: Int,
  heap: Heap,
) -> LinkOutcome {
  // Build the handle, wire the cursors, register the establishment Faults stream as
  // a live monitor cursor (FR-008), and attach the resolved endpoint.
  let base = link_handle.new(id, link_options.default())
  let handle =
    link_handle.LinkHandle(
      ..base,
      in_writer: Some(in_writer),
      out_reader: Some(out_reader),
      faults_writer: Some(faults_writer),
    )
    |> link_handle.add_monitor_cursor(faults_writer)
    |> link_handle.attach_endpoint(ep)
  case link_registry.put(runtime.links, id, handle) {
    Error(why) -> LinkAbort(why)
    Ok(links) -> LinkEffect(heap, link_runtime.with_links(runtime, links), [])
  }
}

/// Parse the ground LinkId + Role (deep-resolved against the heap).
fn parse_id_role(
  heap: Heap,
  id_t: Term,
  role_t: Term,
) -> Result(#(LinkId, String), String) {
  use id_g <- result_try(link_terms.ground_resolve(heap, id_t))
  use id <- result_try(link_terms.parse_link_id(id_g))
  use role_g <- result_try(link_terms.ground_resolve(heap, role_t))
  use role <- result_try(link_terms.parse_role(role_g))
  Ok(#(id, role))
}

/// Resolve the three output cells to their RAW heap addresses (the bare VarRef addr,
/// NOT a deref — a reader canonicalizes to its writer and erases the Out? reader
/// identity, the Dart caveat). Validate polarity: In/Faults writers, Out a reader.
fn cursors(
  heap: Heap,
  in_t: Term,
  out_t: Term,
  faults_t: Term,
) -> Result(#(Int, Int, Int), String) {
  use in_writer <- result_try(writer_addr(heap, in_t, "In"))
  use out_reader <- result_try(reader_addr(heap, out_t, "Out?"))
  use faults_writer <- result_try(writer_addr(heap, faults_t, "Faults"))
  Ok(#(in_writer, out_reader, faults_writer))
}

fn writer_addr(heap: Heap, t: Term, what: String) -> Result(Int, String) {
  case t {
    VarRef(addr) ->
      case heap.is_writer(heap, addr) {
        True -> Ok(addr)
        False -> Error(what <> " must be an unbound writer cell")
      }
    _ -> Error(what <> " must be an unbound writer cell")
  }
}

fn reader_addr(heap: Heap, t: Term, what: String) -> Result(Int, String) {
  case t {
    VarRef(addr) ->
      case heap.is_reader(heap, addr) {
        True -> Ok(addr)
        False -> Error(what <> " must be an unbound reader cell")
      }
    _ -> Error(what <> " must be an unbound reader cell")
  }
}

/// Open the transport leaf for `id` in `role` and BLOCK until the rendezvous
/// resolves (the Gleam seam is synchronous). A transport fault becomes a `LinkAbort`
/// reason (establishment-failure fault decoration on the monitor is T075).
fn establish(
  runtime: LinkRuntime,
  id: LinkId,
  role: String,
) -> Result(Endpoint, String) {
  case transport_registry.select(runtime.transports, id.scheme) {
    Error(why) -> Error(why)
    Ok(t) -> {
      let opts = link_options.default()
      let result = case role {
        "listener" -> t.listen(id.scheme, id.endpoint, opts)
        _ -> t.connect(id.scheme, id.endpoint, opts)
      }
      case result {
        Ok(ep) -> Ok(ep)
        Error(signal) -> Error("transport establishment failed: " <> signal.reason)
      }
    }
  }
}

fn result_try(r: Result(a, e), then: fn(a) -> Result(b, e)) -> Result(b, e) {
  case r {
    Ok(v) -> then(v)
    Error(e) -> Error(e)
  }
}
