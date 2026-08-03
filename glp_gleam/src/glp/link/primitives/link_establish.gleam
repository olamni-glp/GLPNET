//// glp/link/primitives/link_establish — the ONE canonical establish-and-wire
//// core every establishment path converges on (wave-3 US4, T032/T033/T034/T035;
//// amended contracts/link-handshake.md).
////
//// Port of `glp_runtime/lib/link/primitives/link_establish.dart` (mirror C#
//// `LinkEstablish`), adapted to the synchronous Gleam seam: the capability
//// gate runs verify-before-act BEFORE the transport endpoint is opened
//// (amended contract "Capability enforcement"); the transport rendezvous
//// (`listen` for the acceptor role, `connect` for the initiator — either side
//// may initiate, rule 1) BLOCKS until the peer appears or the connect timeout
//// expires; the established endpoint is wrapped in a handle and registered
//// idempotently (FR-007-025: a same-identity re-establishment is refused —
//// cell-aliasing unspecified, surfaced not guessed). A gate refusal or
//// transport failure is a REASONED `LinkFaultSignal` (rule 7: terminal for the
//// attempt), never silence, never best-effort continuation.

import glp/link/primitives/capability_gate.{type GateRegistry}
import glp/link/primitives/link_handle.{type LinkHandle}
import glp/link/primitives/link_registry.{type LinkRegistry}
import glp/link/seam/link_address.{type LinkAddress}
import glp/link/seam/link_fault.{type LinkFaultSignal, LinkFaultSignal, Permanent}
import glp/link/seam/link_id.{LinkId, NonceStr}
import glp/link/seam/link_options.{type LinkOptions}
import glp/link/seam/link_scheme.{type LinkScheme}
import glp/link/seam/transport.{type Transport}

/// The establishment role: `Listener` binds and awaits the peer (inbound
/// acceptance, rule 1 — an instance that can only dial is non-conforming);
/// `Connector` initiates. Establishment role is independent of data direction.
pub type Role {
  Listener
  Connector
}

/// Establish one link end: gate → transport rendezvous → handle → register.
/// Returns the advanced registry + the established handle, or the reasoned
/// refusal/fault. The returned handle's endpoint is READY (the seam rendezvous
/// is synchronous), so no program traffic can precede establishment.
pub fn establish(
  registry: LinkRegistry,
  gates: GateRegistry,
  transport: Transport,
  role: Role,
  scheme: LinkScheme,
  addr: LinkAddress,
  options: LinkOptions,
) -> Result(#(LinkRegistry, LinkHandle), LinkFaultSignal) {
  // Capability gate (verify-before-act): decided on the pre-establishment
  // identity — the scheme + address that determine the attempt; the transport
  // mints the established nonce only after the gate passes (C# gates before
  // GetOrEstablish opens the endpoint, exactly this ordering).
  let provisional = LinkId(scheme, addr, NonceStr("pre-establish"))
  let gate = capability_gate.gate_for(gates, scheme)
  case gate.gate_establish(provisional) {
    False ->
      // Fail-closed reasoned refusal (amended contract "Refusal surface";
      // rule 7: terminal — a fresh link must be established).
      Error(LinkFaultSignal(
        provisional,
        Permanent,
        "capability refused: establishment gate denied "
          <> link_scheme.name(scheme),
      ))
    True -> {
      let rendezvous = case role {
        Listener -> transport.listen
        Connector -> transport.connect
      }
      case rendezvous(scheme, addr, options) {
        Error(signal) -> Error(signal)
        Ok(endpoint) ->
          // Idempotency at link-identity (FR-007-025): re-establishment of an
          // already-registered identity is refused, never a silent duplicate.
          case link_registry.contains(registry, endpoint.id) {
            True ->
              Error(LinkFaultSignal(
                endpoint.id,
                Permanent,
                "re-establishment of already-established link: "
                  <> "cell-aliasing unspecified (FR-007) — first only",
              ))
            False ->
              case link_handle.new(endpoint.id, options, endpoint) {
                Error(reason) ->
                  Error(LinkFaultSignal(endpoint.id, Permanent, reason))
                Ok(handle) ->
                  Ok(#(link_registry.put(registry, handle), handle))
              }
          }
      }
    }
  }
}
