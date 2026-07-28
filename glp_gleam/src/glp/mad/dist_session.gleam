//// glp/mad/dist_session — a madGLP engine's distribution session: the destination
//// agent → established-link routing table (feature 059, T066
//// `close-distribution-engine-sessions`, slice S2).
////
//// One agent p's outbound routing state s_p carries, per peer q it can reach, the
//// established transport `Endpoint` over which q's assignment messages travel. This is
//// the Gleam mirror of the Dart oracle's `IsolateManager._agentPorts`
//// (`Map<String, SendPort>` — glp_runtime/lib/multiagent/isolate_manager.dart): the
//// map from a mad destination agent to the channel that reaches it. Where the Dart
//// mesh runs each agent in an isolate wired by SendPorts, the Gleam session is a value
//// threaded beside the `MadEngine`, and each peer link is a link-seam `Endpoint`
//// (loopback / TCP / QUIC) — one point-to-point end per peer (link primitives are
//// point-to-point; multi-party is composed above, D-9).
////
//// Send drains M_p (spec §8.2): `send` encodes each `Message` (S1 `message_codec`) and
//// ships it over the endpoint registered for its `dest`. Receive is the inverse: `recv`
//// blocks on a peer's endpoint for the next frame and decodes it to the `#(name, value)`
//// that `mad_engine.receive` binds (spec §8.3). The drive loop that alternates the two
//// across engines mirrors `link_pump.drive` (run to quiescence → egress → ingress →
//// re-drive), lifted from the link-goal engine to the MadEngine (T066 slice S3).
////
//// Host-side plumbing over the existing transport seam — NOT a language change.

import gleam/dict.{type Dict}
import gleam/option.{type Option, None, Some}
import glp/link/seam/endpoint.{type Endpoint}
import glp/mad/global_name.{type GlobalName}
import glp/mad/message.{type Message, Message}
import glp/mad/message_codec
import glp/runtime/terms.{type Term}

/// One engine's outbound routing table: the endpoint per reachable peer, plus a
/// per-peer send sequence counter (seeds each frame's `message_id`, so the link's
/// ordering sublayer sees a monotone data sequence per peer). Opaque; `connect`/`send`
/// return a new value.
pub opaque type DistSession {
  DistSession(endpoints: Dict(Term, Endpoint), seqs: Dict(Term, Int))
}

/// A fresh session with no peers connected.
pub fn new() -> DistSession {
  DistSession(dict.new(), dict.new())
}

/// Register `endpoint` as the established route to peer `agent` (the ground agent-id
/// term). A later `connect` for the same peer replaces the route (re-establishment is
/// the caller's concern — the link registry enforces LinkId idempotency below).
pub fn connect(session: DistSession, agent: Term, endpoint: Endpoint) -> DistSession {
  DistSession(..session, endpoints: dict.insert(session.endpoints, agent, endpoint))
}

/// True once a route to `agent` is registered.
pub fn is_connected(session: DistSession, agent: Term) -> Bool {
  dict.has_key(session.endpoints, agent)
}

/// Encode `msg` (S1) and ship it over the endpoint registered for its destination
/// agent, advancing that peer's send sequence. `Error` for an unrouted destination, a
/// non-ground value (S1 gate), or a transport send fault — each surfaced, never a
/// silent drop.
pub fn send(session: DistSession, msg: Message) -> Result(DistSession, String) {
  let Message(_name, _term, dest) = msg
  case dict.get(session.endpoints, dest) {
    Error(Nil) -> Error("no distribution route for destination agent")
    Ok(ep) -> {
      let seq = case dict.get(session.seqs, dest) {
        Ok(n) -> n
        Error(Nil) -> 0
      }
      case message_codec.encode(msg, seq) {
        Error(why) -> Error(why)
        Ok(frame) ->
          case ep.send(frame) {
            Error(signal) ->
              Error("distribution send failed: " <> signal.reason)
            Ok(Nil) ->
              Ok(
                DistSession(
                  ..session,
                  seqs: dict.insert(session.seqs, dest, seq + 1),
                ),
              )
          }
      }
    }
  }
}

/// Block on `peer`'s endpoint for the next arriving assignment and decode it to the
/// `#(name, value)` that `mad_engine.receive` binds. `Ok(None)` = the peer cleanly
/// ended the stream (endpoint eos). `Error` for an unrouted peer, a transport fault, or
/// an undecodable / malformed frame (S1 surfaces the last two loudly).
pub fn recv(
  session: DistSession,
  peer: Term,
) -> Result(Option(#(GlobalName, Term)), String) {
  case dict.get(session.endpoints, peer) {
    Error(Nil) -> Error("no distribution route for peer agent")
    Ok(ep) ->
      case ep.recv() {
        Error(signal) -> Error("distribution recv failed: " <> signal.reason)
        Ok(None) -> Ok(None)
        Ok(Some(bytes)) ->
          case message_codec.decode(bytes) {
            Ok(pair) -> Ok(Some(pair))
            Error(why) -> Error(why)
          }
      }
  }
}
