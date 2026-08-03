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
//// port) but parks the pump instead of exiting it, and the broker keeps serving
//// already-accepted endpoints so none is dropped even at stop. Passive-mode
//// `gen_tcp:recv`/`send` work from any process, so consumers (tests, the T050
//// pump) drive accepted endpoints from their own processes. The broker is a
//// request/reply server (loopback-hub style), so `accept`/`transport(..).listen`
//// may be called from ANY process — not just the listener's creator.

import gleam/dynamic.{type Dynamic}
import gleam/erlang/process.{type Subject}
import gleam/int
import gleam/list
import gleam/option.{None, Some}
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

@external(erlang, "glp_link_tcp_ffi", "tcp_close_listener")
fn ffi_close_listener(sock: ListenSocket) -> Nil

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
/// arrive. Callable from any process. Already-accepted links are handed out
/// even after `stop` (none dropped); once stopped AND drained, a Permanent
/// fault reports the listener down.
pub fn accept(
  listener: Listener,
  timeout_ms: Int,
) -> Result(Endpoint, LinkFaultSignal) {
  take_next(listener, timeout_ms)
}

/// Cease accepting: close the listen socket (releasing the port) and mark the
/// broker stopped. Established and already-accepted-but-untaken links stay
/// alive — the parked accept-pump remains their sockets' controlling process.
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
    // Stopped: park forever — this process owns every accepted socket, and a
    // gen_tcp socket closes when its controlling process exits.
    Ok(Nil) -> process.sleep_forever()
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
          process.send(broker, Accepted(tcp.make_endpoint(id, sock)))
          accept_pump(lsock, ctl, broker, addr, port, n + 1)
        }
        Error(_) -> {
          // {error, timeout} = an idle slice; {error, closed} = stop() closed
          // the listener (the ctl check above exits the loop next spin). The
          // small sleep keeps any persistent non-timeout fault from spinning
          // hot.
          process.sleep(10)
          accept_pump(lsock, ctl, broker, addr, port, n)
        }
      }
  }
}

// ---------------------------------------------------------------------------
// Broker — buffers accepted endpoints until a consumer takes them (FIFO), as a
// request/reply server so any process can take. Never parks a request: the
// consumer polls (the same bounded-poll style as tcp's connect_retry), so an
// accepted endpoint only ever leaves the buffer toward a live requester.
// ---------------------------------------------------------------------------

type BrokerMsg {
  Accepted(endpoint: Endpoint)
  TryNext(reply: Subject(TakeReply))
  BrokerStop
}

type TakeReply {
  Taken(Endpoint)
  Empty
  Stopped
}

fn start_broker() -> Subject(BrokerMsg) {
  let ready = process.new_subject()
  process.spawn(fn() {
    let inbox = process.new_subject()
    process.send(ready, inbox)
    broker_loop(inbox, [], False)
  })
  let assert Ok(inbox) = process.receive(ready, 5000)
  inbox
}

fn broker_loop(
  inbox: Subject(BrokerMsg),
  buffer: List(Endpoint),
  stopped: Bool,
) -> Nil {
  case process.receive_forever(inbox) {
    Accepted(ep) -> broker_loop(inbox, list.append(buffer, [ep]), stopped)
    TryNext(reply) ->
      case buffer {
        [head, ..tail] -> {
          process.send(reply, Taken(head))
          broker_loop(inbox, tail, stopped)
        }
        [] -> {
          process.send(reply, case stopped {
            True -> Stopped
            False -> Empty
          })
          broker_loop(inbox, [], stopped)
        }
      }
    // Stopped brokers keep serving the remaining buffer — accepted links are
    // never dropped, even at stop.
    BrokerStop -> broker_loop(inbox, buffer, True)
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
