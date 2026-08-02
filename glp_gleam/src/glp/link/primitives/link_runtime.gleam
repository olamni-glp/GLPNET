//// glp/link/primitives/link_runtime — the per-run link effect state + the
//// per-link BEAM processes (T050.C1/C6; contracts/link-primitives-port.md D-3/D-5).
////
//// Port of `glp_runtime/lib/link/primitives/link_runtime.dart` (mirror C#
//// `LinkRuntime`), adapted to the Gleam engine's effect-state discipline: the
//// aggregate is an immutable `LinkState` threaded through reductions exactly
//// like `MadState` (the E5-ratified parallel-effect precedent), and the
//// genuinely-blocking work — the transport rendezvous and the recv loop — runs
//// in ONE spawned BEAM process per link that reports back to the engine loop on
//// a `Subject` (D-3; no gleam_otp).
////
//// The establishment process BECOMES the pump process: a gen_tcp socket closes
//// when its controlling (creating) process exits, so the process that ran
//// `listen`/`connect` must stay alive for the link's lifetime, looping
//// `link_pump.pump_once` and forwarding decoded payloads. This also replaces
//// the Dart deferred-endpoint machinery (`endpointReady`/`attachEndpoint`):
//// the kernel returns immediately (never blocks the scheduler — a symmetric
//// bidi establishment would deadlock otherwise), and `LinkEstablished` arrives
//// on the subject when the rendezvous resolves.

import gleam/dict.{type Dict}
import gleam/erlang/process.{type Subject}
import gleam/list
import gleam/option.{type Option}
import glp/link/primitives/capability_gate.{type GateRegistry}
import glp/link/primitives/link_handle
import glp/link/primitives/link_pump.{Delivered, PeerClosed, PumpFault}
import glp/link/primitives/link_registry.{type LinkRegistry}
import glp/link/primitives/transport_registry.{type TransportRegistry}
import glp/link/seam/endpoint.{type Endpoint}
import glp/link/seam/link_fault.{type LinkFaultSignal}
import glp/link/seam/link_id.{type LinkId}
import glp/link/seam/link_options
import glp/link/seam/transport.{type Transport}

/// What a per-link process reports back to the engine loop's subject.
pub type LinkMsg {
  /// The transport rendezvous resolved — the link is live; the reporting
  /// process is now this link's pump. `release` is the pump's own exit gate
  /// (D-9): the pump is the socket's controlling process, so it must NOT exit
  /// — which closes the socket — until the engine loop confirms the link is
  /// terminal in BOTH directions. The loop sends on `release` at every
  /// terminal transition; the pump exits (closing the socket orderly) only
  /// then.
  LinkEstablished(id: LinkId, endpoint: Endpoint, release: Subject(Nil))
  /// The rendezvous failed (refused / timed out) — terminal for the attempt.
  LinkEstablishFailed(id: LinkId, signal: LinkFaultSignal)
  /// In-order decoded payloads became deliverable (the pump's `Delivered`).
  LinkInbound(id: LinkId, payloads: List(BitArray))
  /// The peer cleanly ended the stream (graceful close upstream).
  LinkPeerClosed(id: LinkId)
  /// A transport/framing fault — the offending frame was NOT delivered.
  LinkPumpFault(id: LinkId, signal: LinkFaultSignal)
}

/// The heap cursors one established link is wired to (the Dart handle's
/// `inWriterAddr`/`outReaderAddr`/`faultsWriterAddr` + T034 monitor cursors,
/// kept beside the substrate `LinkHandle` so that module stays untouched).
pub type Cursors {
  Cursors(
    /// The writer the host extends with inbound terms (the program's In tail).
    in_writer: Int,
    /// The WRITER paired with the program's `Out?` reader — the egress drain
    /// follows the cons chain from here as the program binds it.
    out_writer: Int,
    /// Live fault-monitor cursor writers (the establishment Faults + every
    /// later `link_monitor` stream); a fault fans out to ALL of them (FR-008).
    faults_cursors: List(Int),
    /// The rendezvous resolved and the handle holds a live endpoint.
    established: Bool,
    /// Our own sender gracefully closed (`Out = []` — TCP half-close): the
    /// egress drain stops, but RECEIVING CONTINUES — closing our sender never
    /// stops the inbound side of a bilateral link (the pump-loop rule).
    out_closed: Bool,
    /// The PEER's sender gracefully closed (their end-of-stream — the symmetric
    /// half-close, D-9): our In ended, but SENDING CONTINUES — the peer closing
    /// their sender never suppresses our un-drained egress. The link is terminal
    /// only when BOTH directions have ended (or on `_link_close` / a permanent
    /// fault), never on this flag alone.
    in_ended: Bool,
    /// The link is fully down (both directions ended, `_link_close`, or a failed
    /// establishment) — terminal for the link.
    closed: Bool,
    /// The pump's exit gate (D-9): send `Nil` here when the link goes terminal
    /// so the pump — the socket's controlling process — exits and the socket
    /// closes ORDERLY. `None` until `LinkEstablished` delivers it.
    release: Option(Subject(Nil)),
  )
}

/// The link effect state threaded through reductions (D-5 option (a) — a
/// parallel `Option(LinkState)` beside `mad`, never a widened `KernelOutcome`).
pub type LinkState {
  LinkState(
    /// The engine loop's receive port for every per-link process. Created by
    /// the engine loop's own process (Subjects are owner-received).
    subject: Subject(LinkMsg),
    transports: TransportRegistry,
    gates: GateRegistry,
    /// Established links only (a handle requires a live endpoint).
    registry: LinkRegistry,
    /// Every set-up link's heap cursors, keyed by ground LinkId — membership
    /// here is the FR-007 idempotency basis (pre- AND post-establishment).
    cursors: Dict(LinkId, Cursors),
  )
}

/// A fresh link state over the composition root's transport leaves. Must be
/// called from the process that will `process.receive` the subject (the engine
/// loop).
pub fn new_state(leaves: List(Transport)) -> LinkState {
  LinkState(
    subject: process.new_subject(),
    transports: list.fold(
      leaves,
      transport_registry.new(),
      transport_registry.register,
    ),
    gates: capability_gate.new(),
    registry: link_registry.new(),
    cursors: dict.new(),
  )
}

/// Any link set up and not yet torn down (the engine loop keeps waiting for
/// subject traffic while one exists).
pub fn has_live(state: LinkState) -> Bool {
  state.cursors
  |> dict.values
  |> list.any(fn(c) { !c.closed })
}

/// Any link still between `'_link_setup'` and its `LinkEstablished` /
/// `LinkEstablishFailed` report (the engine loop must wait for these even when
/// every goal already reduced — the producer side's egress can only drain once
/// the rendezvous resolves).
pub fn has_pending_establish(state: LinkState) -> Bool {
  state.cursors
  |> dict.values
  |> list.any(fn(c) { !c.closed && !c.established })
}

/// Any link whose graceful close handshake is still in flight: our sender
/// closed (`Out = []`, FIN shipped) but the peer's end-of-stream has not yet
/// arrived. The engine loop must NOT return here (D-9 run-termination barrier):
/// the pump — the socket's controlling process — must stay in `recv` until the
/// peer's FIN, so the socket closes ORDERLY on the pump's normal exit. A VM
/// halt while the pump still blocks in `recv` closes the socket abortively
/// (RST) and silently truncates the just-shipped tail — the exact loss the
/// graceful-close contract (025 DESIGN-DOSSIER §4) forbids. Bounded by the
/// loop's `wait_timeout_ms` give-up (SC-007).
pub fn has_unfinished_close(state: LinkState) -> Bool {
  state.cursors
  |> dict.values
  |> list.any(fn(c) { c.established && c.out_closed && !c.closed })
}

/// The establishment role (mirrors the substrate's two rendezvous halves;
/// independent of data direction, FR-004).
pub type Role {
  Listener
  Connector
}

/// Spawn the per-link process: run the (blocking) transport rendezvous, report
/// the outcome, and on success become the link's pump (see module header).
/// The kernel calls this and returns immediately.
pub fn spawn_establish(
  state: LinkState,
  transport: Transport,
  id: LinkId,
  role: Role,
) -> Nil {
  let subject = state.subject
  let opts = link_options.default()
  process.spawn(fn() {
    let rendezvous = case role {
      Listener -> transport.listen
      Connector -> transport.connect
    }
    case rendezvous(id.scheme, id.endpoint, opts) {
      Error(signal) -> process.send(subject, LinkEstablishFailed(id, signal))
      Ok(endpoint) ->
        // The pump keeps a process-local handle for the ingress reliability
        // state (reassembler/ordering); the engine-side registry handle owns
        // the disjoint egress state (sequencer/window). Report the endpoint
        // first so egress can start shipping while the pump blocks in recv.
        case link_handle.new(id, opts, endpoint) {
          Error(reason) ->
            process.send(
              subject,
              LinkEstablishFailed(id, link_fault.LinkFaultSignal(
                id,
                link_fault.Permanent,
                "handle construction failed: " <> reason,
              )),
            )
          Ok(handle) -> {
            // The pump's exit gate lives in the pump process (Subjects are
            // owner-received): the engine loop sends on it once the link is
            // terminal in both directions.
            let release = process.new_subject()
            process.send(subject, LinkEstablished(id, endpoint, release))
            pump_loop(subject, id, handle, release)
          }
        }
    }
  })
  Nil
}

/// How long the pump lingers for its release after the peer's end-of-stream.
/// Longer than the engine loop's own 30 s bounded give-up: if the loop gave
/// up and the VM is halting anyway, the pump's exit no longer matters.
const release_timeout_ms = 60_000

fn pump_loop(
  subject: Subject(LinkMsg),
  id: LinkId,
  handle: link_handle.LinkHandle,
  release: Subject(Nil),
) -> Nil {
  case link_pump.pump_once(handle) {
    #(handle, Delivered([])) -> pump_loop(subject, id, handle, release)
    #(handle, Delivered(payloads)) -> {
      process.send(subject, LinkInbound(id, payloads))
      pump_loop(subject, id, handle, release)
    }
    #(_, PeerClosed) -> {
      process.send(subject, LinkPeerClosed(id))
      // D-9: the peer ended THEIR sender — but this process owns the socket,
      // and exiting here closes it under our possibly-undrained egress (the
      // graceful-close truncation). Linger until the engine loop confirms the
      // link terminal (or the bounded give-up passes), THEN exit — an orderly
      // close with every shipped frame already in the kernel.
      let _ = process.receive(release, release_timeout_ms)
      Nil
    }
    #(handle, PumpFault(signal)) -> {
      // The frame was rejected, never delivered; the link may still recover
      // (transient) — keep pumping, the monitor observed the fault (FR-008).
      process.send(subject, LinkPumpFault(id, signal))
      pump_loop(subject, id, handle, release)
    }
  }
}
