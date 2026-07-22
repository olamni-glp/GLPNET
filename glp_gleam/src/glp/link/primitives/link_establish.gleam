//// glp/link/primitives/link_establish — THE shared establish-core both establishment
//// paths converge on (feature 050, T050.C1).
////
//// Port of `csharp/glp_link/primitives/LinkEstablish.cs` `WireEstablishedLink` (mirror
//// `glp_runtime/lib/link/primitives/link_establish.dart`).
////
//// **This function IS the R-5 mitigation, and it is the reason C1 comes first.**
//// FR-002 requires that a link established by path A (`server_listener`/`client_connector`
//// → `'_link_setup'`) be *indistinguishable* from one established by path B
//// (`request_listener`/`request_link`/`accept_link`). The design guarantee is structural
//// rather than aspirational: **there is exactly one code path that can produce an
//// established link**, and all four establishment kernels reach it. If a second
//// establishment path is ever added elsewhere, R-5 is broken no matter how carefully it
//// mirrors this one — so don't; call this.
////
//// Ordering here is load-bearing, and it is the C# oracle's order:
////   1. **Admission first** (`capability_gate.admit`, fail-closed) — BEFORE any endpoint
////      is opened, so a refusal costs no socket, leaks no connection and is observable
////      as a clean refusal rather than a dangling half-open link.
////   2. **Idempotency check + establish** through `link_registry.get_or_establish`, so a
////      repeat setup at the same ground identity REUSES rather than dialling again
////      (FR-007).
////   3. **Open the transport** only on genuine first establishment.
////
//// **Pre-gated paths (`pre_gated: True`).** Path B gates at the *request*/*accept*
//// kernel, before the in-band token is exchanged — by the time it reaches this core the
//// admission decision is already made and the endpoint already open. Re-gating would
//// consult policy twice for one link (and, with a stateful policy, could refuse a link
//// it just admitted). This mirrors the C# `preGated:` parameter exactly.
////
//// **Cursor wiring is NOT done here.** The C#/Dart core also wires heap cursors, arms
//// the egress drainer and starts the ingress pump. Those need the heap and the effect
//// seam, which arrive with the kernels (C2) and the pump/egress (C5/C6). C1 delivers the
//// convergence + registry half, so `wire_established_link` returns the handle with
//// cursors unset for its caller to wire. Keeping that split explicit is deliberate: the
//// registry convergence is testable now, without a heap.

import gleam/option.{type Option, None, Some}
import glp/link/primitives/capability_gate.{type CapabilityGateRegistry}
import glp/link/primitives/link_handle.{type LinkHandle}
import glp/link/primitives/link_registry.{type LinkRegistry, type Establishment}
import glp/link/primitives/transport_registry.{type TransportRegistry}
import glp/link/seam/endpoint.{type Endpoint}
import glp/link/seam/link_address.{type LinkAddress}
import glp/link/seam/link_fault.{type LinkFaultSignal}
import glp/link/seam/link_id.{type LinkId}
import glp/link/seam/link_options.{type LinkOptions}

/// Which side of the transport rendezvous this end takes.
///
/// Independent of data direction (FR-004): a listener end may be the writer end and
/// vice versa. Which side listens is a deployment/NAT concern only.
pub type Role {
  Listener
  Connector
}

/// Everything establishment needs that is not per-call.
pub type EstablishContext {
  EstablishContext(
    transports: TransportRegistry,
    gates: CapabilityGateRegistry,
    options: LinkOptions,
  )
}

/// Why establishment failed. Each maps to a distinct GLP-visible outcome; C2+ turns
/// these into fault terms / kernel aborts. Kept separate rather than collapsed to a
/// string so a refusal is never mistaken for a transport failure.
pub type EstablishError {
  /// The capability gate refused (or could not evaluate, and failed closed). No
  /// endpoint was opened.
  Refused(id: LinkId, reason: String)
  /// No transport leaf serves the scheme — for `quic` today, expected (T055 unbuilt).
  NoTransport(error: transport_registry.TransportError)
  /// The transport leaf failed to listen/connect (timeout, refused, unreachable).
  TransportFailed(id: LinkId, signal: LinkFaultSignal)
  /// The registry rejected the produced handle — an internal invariant break.
  Registry(error: link_registry.RegistryError(LinkFaultSignal))
}

/// Establish-or-reuse the link identified by ground `id`, in `role`, at `address`.
///
/// Returns the updated registry plus an `Establishment` telling the caller whether the
/// link was `Reused` (do NOT re-wire cursors or re-arm a pump — the peer's frames are
/// already being consumed) or newly `Established` (wire everything).
///
/// `pre_gated` skips step 1 for path B, which already gated at its own kernel.
///
/// `adopt` is the endpoint-provenance switch and the single reason path B can reuse this
/// ONE funnel (R-5): path A passes `None` and the leaf is opened here by `role`; the
/// path-B kernels have ALREADY opened/adopted the transport connection to run the in-band
/// handshake, so they pass `Some(endpoint)` and this core adopts that same connection
/// rather than dialling a second one. Mirrors the C# oracle's `Func<ILinkEndpoint>`
/// factory (`() => transport.Connect/Listen` vs `() => endpoint`).
pub fn wire_established_link(
  ctx: EstablishContext,
  registry: LinkRegistry,
  id: LinkId,
  role: Role,
  address: LinkAddress,
  adopt: Option(Endpoint),
  pre_gated: Bool,
) -> Result(#(LinkRegistry, Establishment), EstablishError) {
  // 1. Admission — BEFORE opening anything. Fail-closed inside `admit`.
  case pre_gated {
    True -> Ok(Nil)
    False -> {
      let direction = case role {
        Listener -> capability_gate.Inbound
        Connector -> capability_gate.Outbound
      }
      let request =
        capability_gate.Request(
          id: id,
          scheme: id.scheme,
          direction: direction,
        )
      case capability_gate.admit(ctx.gates, request) {
        capability_gate.Allowed -> Ok(Nil)
        capability_gate.Refused(reason) -> Error(Refused(id, reason))
      }
    }
  }
  |> then_establish(ctx, registry, id, role, address, adopt)
}

/// Steps 2–3: idempotent reuse, else open the transport and store the new handle.
/// `establish` runs at most once and ONLY when the identity is not already present, so
/// a repeated setup never dials twice (FR-007).
fn then_establish(
  admitted: Result(Nil, EstablishError),
  ctx: EstablishContext,
  registry: LinkRegistry,
  id: LinkId,
  role: Role,
  address: LinkAddress,
  adopt: Option(Endpoint),
) -> Result(#(LinkRegistry, Establishment), EstablishError) {
  case admitted {
    Error(e) -> Error(e)
    Ok(Nil) ->
      // Build the handle factory ONCE, then funnel through `get_or_establish` — the
      // single code path that produces an established link (R-5). Path B adopts the
      // pre-opened endpoint; path A selects the leaf and opens by role. `get_or_establish`
      // runs the factory at most once, and ONLY on genuine first establishment (FR-007).
      case handle_factory(ctx, id, role, address, adopt) {
        Error(e) -> Error(e)
        Ok(open) ->
          case link_registry.get_or_establish(registry, id, open) {
            Ok(pair) -> Ok(pair)
            Error(link_registry.EstablishFailed(signal)) ->
              Error(TransportFailed(id, signal))
            Error(other) -> Error(Registry(other))
          }
      }
  }
}

/// The endpoint→handle factory for `get_or_establish`. `Some(endpoint)` (path B) adopts
/// that connection; `None` (path A) selects the scheme's leaf and opens by role — the
/// only place `NoTransport` can arise. Identity is always the CALLER's ground LinkId, not
/// the endpoint's own carrier id, so the registry keys on the GLP-visible identity.
fn handle_factory(
  ctx: EstablishContext,
  id: LinkId,
  role: Role,
  address: LinkAddress,
  adopt: Option(Endpoint),
) -> Result(fn() -> Result(LinkHandle, LinkFaultSignal), EstablishError) {
  case adopt {
    Some(endpoint) -> Ok(fn() { Ok(link_handle.new(id, endpoint, ctx.options)) })
    None ->
      case transport_registry.select(ctx.transports, id.scheme) {
        Error(e) -> Error(NoTransport(e))
        Ok(leaf) ->
          Ok(fn() {
            let opened = case role {
              Listener -> leaf.listen(id.scheme, address, ctx.options)
              Connector -> leaf.connect(id.scheme, address, ctx.options)
            }
            case opened {
              Error(signal) -> Error(signal)
              Ok(endpoint) -> Ok(link_handle.new(id, endpoint, ctx.options))
            }
          })
      }
  }
}
