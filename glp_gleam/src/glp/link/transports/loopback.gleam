//// glp/link/transports/loopback — deterministic in-process loopback transport
//// (feature 050, T048).
////
//// Port of `glp_runtime/lib/link/transports/loopback_transport.dart` (mirror
//// `csharp/glp_link/transports/{LoopbackTransport,LoopbackEndpoint}.cs`) for
//// `LinkScheme.loopback`. A `listen` and a `connect` on the same channel name (the
//// `LinkAddress.host`) rendezvous into one bilateral link; the two ends share an
//// equivalent `LinkId` (FR-002) and exchange frames over two ordered in-memory
//// channels — FIFO / in-order / exactly-once by construction (FR-018/020). It is the
//// deterministic substrate for the hermetic round-trip tests (T056); a T052 fault
//// decorator will later wrap the produced endpoints.
////
//// BEAM mapping (the seam is a HOST interface below GLP, so these are language-mapping
//// choices, not GLP-semantic deviations — link-parity.md):
////  - The Dart in-memory `_Channel` (buffer + parked waiter) becomes an ordered
////    **channel process** (`channel_loop`); the Dart rendezvous `Map` becomes a
////    **hub process** (`hub_loop`) shared by all links of one transport instance.
////  - The Dart async `Future<ILinkEndpoint>` from listen/connect becomes a SYNCHRONOUS
////    `Result` (the seam has no futures): the first arrival BLOCKS in `process.receive`
////    on its reply subject until the peer appears. A single OS process therefore
////    cannot rendezvous with itself; a test spawns one side (see loopback_test).
////  - No `gleam_otp` — only `gleam_erlang` `spawn`/`Subject` (the AtomVM subset the
////    endpoint seam commits to). Faults arrive out-of-band on the caller-owned
////    `faults` Subject.
////
//// The seam omits Dart's `cancel` future, so a parked rendezvous has no cancellation;
//// `connect_timeout_ms` bounds the caller's wait and surfaces a `Transient` fault on
//// expiry (an unpaired pending side simply waits for a peer — hermetic tests pair).

import gleam/dict.{type Dict}
import gleam/erlang/process.{type Subject}
import gleam/list
import gleam/option.{type Option, None, Some}
import glp/link/seam/endpoint.{type Endpoint, Endpoint}
import glp/link/seam/link_address.{type LinkAddress}
import glp/link/seam/link_fault.{
  type LinkFaultSignal, Closed, LinkFaultSignal, Transient,
}
import glp/link/seam/link_id.{type LinkId, LinkId, NonceInt, NonceStr}
import glp/link/seam/link_options.{type LinkOptions}
import glp/link/seam/link_scheme.{type LinkScheme}
import glp/link/seam/transport.{type Transport, Transport}

// ---------------------------------------------------------------------------
// Ordered channel process — the Dart `_Channel` (FIFO buffer + one parked reader).
// ---------------------------------------------------------------------------

type PushReply {
  PushOk
  PushClosed
}

type PullReply {
  PullItem(BitArray)
  PullClosed
}

type ChanMsg {
  /// Enqueue one frame; the writer learns whether the channel was already completed.
  Push(frame: BitArray, reply: Subject(PushReply))
  /// Dequeue the next frame, parking if the buffer is empty and not yet completed.
  Pull(reply: Subject(PullReply))
  /// Complete the channel (the writer's `close`): drained then end-of-stream.
  Complete
}

type ChanState {
  ChanState(
    buffer: List(BitArray),
    completed: Bool,
    pending: Option(Subject(PullReply)),
  )
}

/// Spawn an ordered channel process and return its inbox subject (owned by that
/// process). Bootstraps the subject back to the caller over a one-shot `ready`.
fn start_channel() -> Subject(ChanMsg) {
  let ready = process.new_subject()
  process.spawn(fn() {
    let inbox = process.new_subject()
    process.send(ready, inbox)
    channel_loop(inbox, ChanState(buffer: [], completed: False, pending: None))
  })
  let assert Ok(inbox) = process.receive(ready, 5000)
  inbox
}

fn channel_loop(inbox: Subject(ChanMsg), state: ChanState) -> Nil {
  case process.receive_forever(inbox) {
    Push(frame, reply) ->
      case state.completed {
        // Writing to a completed channel → the Dart `_ChannelClosedException` a
        // send-after-close surfaces (mapped to a Closed fault by the endpoint).
        True -> {
          process.send(reply, PushClosed)
          channel_loop(inbox, state)
        }
        False -> {
          process.send(reply, PushOk)
          case state.pending {
            // A parked reader is waiting: hand the frame straight over (FIFO — the
            // buffer is empty whenever a reader is parked).
            Some(puller) -> {
              process.send(puller, PullItem(frame))
              channel_loop(
                inbox,
                ChanState(buffer: [], completed: False, pending: None),
              )
            }
            None ->
              channel_loop(
                inbox,
                ChanState(
                  buffer: list.append(state.buffer, [frame]),
                  completed: False,
                  pending: None,
                ),
              )
          }
        }
      }

    Pull(reply) ->
      case state.buffer {
        [head, ..tail] -> {
          process.send(reply, PullItem(head))
          channel_loop(inbox, ChanState(..state, buffer: tail))
        }
        [] ->
          case state.completed {
            // Buffer drained + writer completed → graceful end-of-stream.
            True -> {
              process.send(reply, PullClosed)
              channel_loop(inbox, state)
            }
            // Park this (single) reader until a frame or completion arrives.
            False -> channel_loop(inbox, ChanState(..state, pending: Some(reply)))
          }
      }

    Complete ->
      case state.pending {
        Some(puller) -> {
          process.send(puller, PullClosed)
          channel_loop(
            inbox,
            ChanState(buffer: state.buffer, completed: True, pending: None),
          )
        }
        None -> channel_loop(inbox, ChanState(..state, completed: True))
      }
  }
}

// ---------------------------------------------------------------------------
// Endpoint over a pair of channels (writes to_peer, reads from_peer).
// ---------------------------------------------------------------------------

fn make_endpoint(
  id: LinkId,
  to_peer: Subject(ChanMsg),
  from_peer: Subject(ChanMsg),
  faults: Subject(LinkFaultSignal),
) -> Endpoint {
  Endpoint(
    id: id,
    send: fn(frame: BitArray) {
      let reply = process.new_subject()
      process.send(to_peer, Push(frame, reply))
      case process.receive_forever(reply) {
        PushOk -> Ok(Nil)
        PushClosed -> {
          let signal = LinkFaultSignal(id, Closed, "peer closed")
          process.send(faults, signal)
          Error(signal)
        }
      }
    },
    recv: fn() {
      let reply = process.new_subject()
      process.send(from_peer, Pull(reply))
      case process.receive_forever(reply) {
        PullItem(frame) -> Ok(Some(frame))
        PullClosed -> Ok(None)
      }
    },
    close: fn() {
      process.send(to_peer, Complete)
      Nil
    },
    faults: faults,
  )
}

// ---------------------------------------------------------------------------
// Rendezvous hub process — the Dart `_pending` map keyed by channel name.
// ---------------------------------------------------------------------------

type PendingSide {
  PendingSide(
    faults: Subject(LinkFaultSignal),
    reply: Subject(Result(Endpoint, LinkFaultSignal)),
  )
}

type HubMsg {
  Rendezvous(
    addr: LinkAddress,
    faults: Subject(LinkFaultSignal),
    reply: Subject(Result(Endpoint, LinkFaultSignal)),
  )
}

fn hub_loop(
  inbox: Subject(HubMsg),
  pending: Dict(String, PendingSide),
  nonce: Int,
) -> Nil {
  case process.receive_forever(inbox) {
    Rendezvous(addr, faults, reply) -> {
      let channel = addr.host
      case dict.get(pending, channel) {
        // Second arrival: pair the two ends over two ordered channels.
        Ok(other) -> {
          let a2b = start_channel()
          let b2a = start_channel()
          let n = nonce + 1
          let id = LinkId(link_scheme.loopback(), addr, NonceInt(n))
          let first_end = make_endpoint(id, a2b, b2a, other.faults)
          let second_end = make_endpoint(id, b2a, a2b, faults)
          process.send(other.reply, Ok(first_end))
          process.send(reply, Ok(second_end))
          hub_loop(inbox, dict.delete(pending, channel), n)
        }
        // First arrival: park until the peer shows up.
        Error(Nil) ->
          hub_loop(
            inbox,
            dict.insert(pending, channel, PendingSide(faults, reply)),
            nonce,
          )
      }
    }
  }
}

// ---------------------------------------------------------------------------
// Public constructor — one hub per transport instance (share it between ends).
// ---------------------------------------------------------------------------

/// Create a loopback transport backed by a fresh rendezvous hub. Share one value
/// between the two ends of a link (the hub is the pairing point).
pub fn new() -> Transport {
  let ready = process.new_subject()
  process.spawn(fn() {
    let inbox = process.new_subject()
    process.send(ready, inbox)
    hub_loop(inbox, dict.new(), 0)
  })
  let assert Ok(hub) = process.receive(ready, 5000)
  Transport(
    supported_schemes: [link_scheme.loopback()],
    listen: fn(scheme, addr, opts) { rendezvous(hub, scheme, addr, opts) },
    connect: fn(scheme, addr, opts) { rendezvous(hub, scheme, addr, opts) },
  )
}

fn rendezvous(
  hub: Subject(HubMsg),
  scheme: LinkScheme,
  addr: LinkAddress,
  opts: LinkOptions,
) -> Result(Endpoint, LinkFaultSignal) {
  case link_scheme.name(scheme) == "loopback" {
    False ->
      Error(LinkFaultSignal(
        LinkId(scheme, addr, NonceStr("unestablished")),
        link_fault.Permanent,
        "LoopbackTransport does not serve scheme '"
          <> link_scheme.name(scheme)
          <> "'",
      ))
    True -> {
      let faults = process.new_subject()
      let reply = process.new_subject()
      process.send(hub, Rendezvous(addr, faults, reply))
      case process.receive(reply, opts.connect_timeout_ms) {
        Ok(result) -> result
        Error(Nil) ->
          Error(LinkFaultSignal(
            LinkId(scheme, addr, NonceStr("unestablished")),
            Transient,
            "loopback rendezvous timed out",
          ))
      }
    }
  }
}
