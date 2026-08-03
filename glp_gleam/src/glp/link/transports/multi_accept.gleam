//// glp/link/transports/multi_accept — N-concurrent-inbound TCP listener
//// (feature 064, T010; FR-012).
////
//// The T049 tcp transport's `listen` is one-accept: it accepts a single client
//// and closes the listener, so a second concurrent dial to the same address is
//// dropped. This module keeps ONE bound address open and accepts N concurrent
//// inbound links on it, none dropped: a dedicated accept-pump process loops
//// `gen_tcp:accept`, wraps every accepted socket in the SAME length-prefixed
//// framing endpoint as tcp.gleam (`tcp.make_endpoint` — reused, not duplicated),
//// and hands it to a broker process that buffers established endpoints until a
//// consumer takes them. Each accepted link gets a DISTINCT per-accept nonce, so
//// N links on one address satisfy the registry's idempotency-at-identity rule
//// (FR-007) instead of colliding on the port nonce.
////
//// Mandatory norms (FR-012, spec + fleet record): every socket rides the same
//// `glp_link_tcp_ffi` listen/accept path as tcp.gleam, so `{exit_on_close,
//// false}` is set on the listener and inherited by every accepted socket — a
//// peer that half-closes at establishment (a pure consumer FIN-ing its `Out =
//// []` immediately) never kills our still-draining write side (D-9 half-close
//// parity). The connector half of `transport(listener)` delegates to the plain
//// tcp leaf, so the D-9 dial-retry norm is the existing one, not a re-
//// implementation. The D-9 run-termination barrier itself lives above the seam
//// (link_loop/link_runtime) and applies unchanged to endpoints produced here.
////
//// Process/ownership model (BEAM): a gen_tcp socket closes when its controlling
//// (accepting) process exits, so the accept-pump must OUTLIVE the links it
//// accepted — `stop` ceases accepting (closes the listen socket, releasing the
//// port) but parks the pump instead of exiting it. At stop the pump reclaims the
//// accepted-but-UNTAKEN sockets from the broker and closes them in its own
//// (controlling) process: once the listener is down nobody can ever take them,
//// so buffering them would leak a BEAM port per idle client for the node's
//// lifetime (codexreview 064 cycle-2 socket-fd-leak). Links already TAKEN by a
//// consumer are untouched and stay live. Passive-mode
//// `gen_tcp:recv`/`send` work from any process, so consumers (tests, the T050
//// pump) drive accepted endpoints from their own processes. The broker is a
//// request/reply server (loopback-hub style), so `accept`/`transport(..).listen`
//// may be called from ANY process — not just the listener's creator.

import gleam/dynamic.{type Dynamic}
import gleam/erlang/process.{type Subject}
import gleam/int
import gleam/list
import gleam/option.{type Option, None, Some}
import gleam/string
import glp/link/seam/endpoint.{type Endpoint}
import glp/link/seam/link_address.{type LinkAddress}
import glp/link/seam/link_fault.{
  type LinkFaultSignal, LinkFaultSignal, Permanent, Transient,
}
import glp/link/seam/link_id.{LinkId, NonceStr}
import glp/link/seam/link_options.{type LinkOptions}
import glp/link/seam/link_scheme.{type LinkScheme}
import glp/link/seam/transport.{type Transport, Transport}
import glp/link/transports/tcp.{type ListenSocket, type Socket}

// ---------------------------------------------------------------------------
// gen_tcp FFI (glp_link_tcp_ffi.erl) — same module as tcp.gleam, declared
// against tcp.gleam's socket types so `tcp.make_endpoint` accepts our sockets.
// The listen path sets {exit_on_close, false}; accepted sockets inherit it.
// ---------------------------------------------------------------------------

@external(erlang, "glp_link_tcp_ffi", "tcp_listen")
fn ffi_listen(port: Int) -> Result(ListenSocket, Dynamic)

@external(erlang, "glp_link_tcp_ffi", "tcp_accept")
fn ffi_accept(sock: ListenSocket, timeout: Int) -> Result(Socket, Dynamic)

@external(erlang, "glp_link_tcp_ffi", "tcp_accept_error_kind")
fn ffi_accept_error_kind(reason: Dynamic) -> String

@external(erlang, "glp_link_tcp_ffi", "tcp_close_listener")
fn ffi_close_listener(sock: ListenSocket) -> Nil

/// Full close (NOT the endpoint's half-close): only for a socket no consumer
/// ever took, and only from the pump, its controlling process.
@external(erlang, "glp_link_tcp_ffi", "tcp_close")
fn ffi_close(sock: Socket) -> Nil

// ---------------------------------------------------------------------------
// Public surface
// ---------------------------------------------------------------------------

/// A live multi-accept listener: one bound tcp address, N concurrent inbound
/// links. Take established endpoints with `accept`; cease accepting with
/// `stop`; adapt to the seam with `transport`.
pub opaque type Listener {
  Listener(
    addr: LinkAddress,
    lsock: ListenSocket,
    broker: Subject(BrokerMsg),
    pump_ctl: Subject(Nil),
  )
}

/// Bind `addr` and start accepting inbound links continuously. The options are
/// accepted for seam-shape parity but carry no listener-level tunable yet (the
/// per-accept wait budget is `accept`'s argument).
pub fn listen(
  scheme: LinkScheme,
  addr: LinkAddress,
  _opts: LinkOptions,
) -> Result(Listener, LinkFaultSignal) {
  use <- require_tcp(scheme, addr)
  use port <- require_port(scheme, addr)
  case ffi_listen(port) {
    Error(r) ->
      Error(fault_addr(
        scheme,
        addr,
        Transient,
        "multi-accept listen failed: " <> ins(r),
      ))
    Ok(lsock) -> {
      let broker = start_broker()
      let pump_ctl = start_accept_pump(lsock, broker, addr, port)
      Ok(Listener(addr: addr, lsock: lsock, broker: broker, pump_ctl: pump_ctl))
    }
  }
}

/// The next accepted inbound link, waiting up to `timeout_ms` for one to
/// arrive. Callable from any process. Once stopped and drained — or once the
/// accept pump has hit a permanent fault (`emfile`/`enfile`/…) — a Permanent
/// fault reports the listener down, carrying the underlying accept reason
/// rather than a generic budget timeout.
pub fn accept(
  listener: Listener,
  timeout_ms: Int,
) -> Result(Endpoint, LinkFaultSignal) {
  take_next(listener, timeout_ms)
}

/// Cease accepting: close the listen socket (releasing the port) and mark the
/// broker stopped. Links a consumer already TOOK stay alive — the parked
/// accept-pump remains their sockets' controlling process. Accepted-but-untaken
/// sockets are reclaimed by the pump and CLOSED (nothing can take them once the
/// listener is down; leaving them buffered leaks a BEAM port each).
pub fn stop(listener: Listener) -> Nil {
  process.send(listener.pump_ctl, Nil)
  process.send(listener.broker, BrokerStop)
  ffi_close_listener(listener.lsock)
}

/// Adapt a listener to the uniform transport seam: `listen` pops the next
/// accepted inbound link for the listener's own address (so the canonical
/// establish core — link_establish/link_runtime — can establish N listener-role
/// links over one address); `connect` is the plain tcp dial with its existing
/// D-9 retry behaviour, delegated untouched.
pub fn transport(listener: Listener) -> Transport {
  let dial = tcp.new()
  Transport(
    supported_schemes: [link_scheme.tcp()],
    listen: fn(scheme, addr, opts: LinkOptions) {
      case link_scheme.name(scheme) == "tcp" && addr == listener.addr {
        True -> take_next(listener, opts.connect_timeout_ms)
        False ->
          Error(fault_addr(
            scheme,
            addr,
            Permanent,
            "multi-accept listener serves only its bound tcp address",
          ))
      }
    },
    connect: dial.connect,
  )
}

// ---------------------------------------------------------------------------
// Accept pump — blocks in gen_tcp:accept, one process per listener. It is the
// controlling process of every socket it accepts, so on stop it PARKS (exiting
// would close all the live links it accepted).
// ---------------------------------------------------------------------------

fn start_accept_pump(
  lsock: ListenSocket,
  broker: Subject(BrokerMsg),
  addr: LinkAddress,
  port: Int,
) -> Subject(Nil) {
  let ready = process.new_subject()
  process.spawn(fn() {
    let ctl = process.new_subject()
    process.send(ready, ctl)
    accept_pump(lsock, ctl, broker, addr, port, 0)
  })
  let assert Ok(ctl) = process.receive(ready, 5000)
  ctl
}

fn accept_pump(
  lsock: ListenSocket,
  ctl: Subject(Nil),
  broker: Subject(BrokerMsg),
  addr: LinkAddress,
  port: Int,
  n: Int,
) -> Nil {
  case process.receive(ctl, 0) {
    // Stopped: release the accepted-but-untaken sockets (nothing can take them
    // now), then park — this process owns every socket it accepted, and a
    // gen_tcp socket closes when its controlling process exits.
    Ok(Nil) -> {
      reclaim_untaken(broker)
      park(ctl, broker)
    }
    Error(Nil) ->
      // 500ms accept slices so the stop signal is observed promptly even while
      // no client is dialing.
      case ffi_accept(lsock, 500) {
        Ok(sock) -> {
          // A DISTINCT nonce per accepted link: N links on one (scheme, addr)
          // stay distinct identities for the registry (FR-007), unlike the
          // one-accept leaf's port nonce.
          let id =
            LinkId(
              link_scheme.tcp(),
              addr,
              NonceStr(
                "accept:" <> int.to_string(port) <> ":" <> int.to_string(n),
              ),
            )
          process.send(broker, Accepted(sock, tcp.make_endpoint(id, sock)))
          accept_pump(lsock, ctl, broker, addr, port, n + 1)
        }
        Error(reason) ->
          case ffi_accept_error_kind(reason) {
            // {error, timeout} = an idle slice; {error, closed} = stop() closed
            // the listener (the ctl check above ends the loop next spin).
            "timeout" | "closed" -> {
              process.sleep(10)
              accept_pump(lsock, ctl, broker, addr, port, n)
            }
            // A permanent OS fault (emfile/enfile/…): retrying it at ~100Hz
            // forever is a silent hot spin no consumer can observe. Report it
            // as a REASONED Permanent signal (link_establish rule 7 — never
            // silence) and cease accepting; the process still parks, since it
            // controls the sockets it already accepted.
            _ -> {
              process.send(
                broker,
                AcceptFailed(fault_addr(
                  link_scheme.tcp(),
                  addr,
                  Permanent,
                  "multi-accept: accept failed permanently: " <> ins(reason),
                )),
              )
              park(ctl, broker)
            }
          }
      }
  }
}

/// Alive but no longer accepting: the pump may never exit (it controls every
/// socket it accepted), but it still honours `stop` so the accepted-but-untaken
/// sockets are released.
fn park(ctl: Subject(Nil), broker: Subject(BrokerMsg)) -> Nil {
  case process.receive(ctl, 60_000) {
    Ok(Nil) -> {
      reclaim_untaken(broker)
      park(ctl, broker)
    }
    Error(Nil) -> park(ctl, broker)
  }
}

/// Take back every accepted-but-untaken socket from the broker and close it.
/// Runs in the pump — the sockets' controlling process — and only after the
/// pump has ceased accepting, so no further socket can enter the buffer.
fn reclaim_untaken(broker: Subject(BrokerMsg)) -> Nil {
  let reply = process.new_subject()
  process.send(broker, ReclaimUntaken(reply))
  case process.receive(reply, 1000) {
    Ok(sockets) -> list.each(sockets, ffi_close)
    Error(Nil) -> Nil
  }
}

// ---------------------------------------------------------------------------
// Broker — buffers accepted endpoints until a consumer takes them (FIFO), as a
// request/reply server so any process can take. Never parks a request: the
// consumer polls (the same bounded-poll style as tcp's connect_retry), so an
// accepted endpoint only ever leaves the buffer toward a live requester.
// ---------------------------------------------------------------------------

type BrokerMsg {
  /// One accepted link: the endpoint a consumer takes, plus its raw socket so
  /// the pump can close it if nobody ever takes it (stop-time reclamation).
  Accepted(socket: Socket, endpoint: Endpoint)
  TryNext(reply: Subject(TakeReply))
  /// The accept pump hit a permanent fault — consumers must learn (FR-003).
  AcceptFailed(fault: LinkFaultSignal)
  /// The pump takes the untaken sockets back to close them (it ceased
  /// accepting first, so the buffer cannot grow again).
  ReclaimUntaken(reply: Subject(List(Socket)))
  BrokerStop
}

type TakeReply {
  Taken(Endpoint)
  Empty
  Stopped
  Failed(LinkFaultSignal)
}

fn start_broker() -> Subject(BrokerMsg) {
  let ready = process.new_subject()
  process.spawn(fn() {
    let inbox = process.new_subject()
    process.send(ready, inbox)
    broker_loop(inbox, [], False, None)
  })
  let assert Ok(inbox) = process.receive(ready, 5000)
  inbox
}

fn broker_loop(
  inbox: Subject(BrokerMsg),
  buffer: List(#(Socket, Endpoint)),
  stopped: Bool,
  fault: Option(LinkFaultSignal),
) -> Nil {
  case process.receive_forever(inbox) {
    Accepted(sock, ep) ->
      broker_loop(inbox, list.append(buffer, [#(sock, ep)]), stopped, fault)
    TryNext(reply) ->
      case buffer {
        [#(_sock, ep), ..tail] -> {
          process.send(reply, Taken(ep))
          broker_loop(inbox, tail, stopped, fault)
        }
        [] -> {
          // A permanent accept fault outranks the generic empty/stopped
          // replies: it is the reason no link will ever arrive.
          process.send(reply, case fault, stopped {
            Some(signal), _ -> Failed(signal)
            None, True -> Stopped
            None, False -> Empty
          })
          broker_loop(inbox, [], stopped, fault)
        }
      }
    AcceptFailed(signal) -> broker_loop(inbox, buffer, stopped, Some(signal))
    ReclaimUntaken(reply) -> {
      process.send(reply, list.map(buffer, fn(entry) { entry.0 }))
      broker_loop(inbox, [], stopped, fault)
    }
    // Stopped brokers keep serving the remaining buffer — an accepted link is
    // never dropped from under a consumer that is still taking.
    BrokerStop -> broker_loop(inbox, buffer, True, fault)
  }
}

fn take_next(
  listener: Listener,
  remaining_ms: Int,
) -> Result(Endpoint, LinkFaultSignal) {
  let reply = process.new_subject()
  process.send(listener.broker, TryNext(reply))
  case process.receive(reply, 1000) {
    Error(Nil) ->
      Error(fault_here(listener, Permanent, "multi-accept broker unresponsive"))
    Ok(Taken(ep)) -> Ok(ep)
    // The pump's own permanent fault, surfaced verbatim instead of a generic
    // "no inbound link within budget".
    Ok(Failed(signal)) -> Error(signal)
    Ok(Stopped) ->
      Error(fault_here(listener, Permanent, "multi-accept listener stopped"))
    Ok(Empty) ->
      case remaining_ms <= 0 {
        True ->
          Error(fault_here(
            listener,
            Transient,
            "multi-accept: no inbound link within budget",
          ))
        False -> {
          process.sleep(50)
          take_next(listener, remaining_ms - 50)
        }
      }
  }
}

// ---------------------------------------------------------------------------
// Guards / helpers (same shapes as tcp.gleam's, typed to this module's results)
// ---------------------------------------------------------------------------

fn require_tcp(
  scheme: LinkScheme,
  addr: LinkAddress,
  k: fn() -> Result(Listener, LinkFaultSignal),
) -> Result(Listener, LinkFaultSignal) {
  case link_scheme.name(scheme) == "tcp" {
    True -> k()
    False ->
      Error(fault_addr(
        scheme,
        addr,
        Permanent,
        "multi_accept does not serve scheme '"
          <> link_scheme.name(scheme)
          <> "'",
      ))
  }
}

fn require_port(
  scheme: LinkScheme,
  addr: LinkAddress,
  k: fn(Int) -> Result(Listener, LinkFaultSignal),
) -> Result(Listener, LinkFaultSignal) {
  case addr.port {
    Some(port) -> k(port)
    None ->
      Error(fault_addr(
        scheme,
        addr,
        Permanent,
        "tcp requires an ep(Host, Port) endpoint",
      ))
  }
}

fn fault_addr(
  scheme: LinkScheme,
  addr: LinkAddress,
  kind,
  reason: String,
) -> LinkFaultSignal {
  LinkFaultSignal(LinkId(scheme, addr, NonceStr("unestablished")), kind, reason)
}

fn fault_here(listener: Listener, kind, reason: String) -> LinkFaultSignal {
  fault_addr(link_scheme.tcp(), listener.addr, kind, reason)
}

fn ins(d: Dynamic) -> String {
  string.inspect(d)
}
