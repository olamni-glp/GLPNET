//// glp/link/primitives/link_runtime — the per-engine link state aggregate, and the
//// `LinkState` effect-seam shape (feature 050, T050.C1; deviation D5).
////
//// Port of `csharp/glp_link/primitives/LinkRuntime.cs` (mirror
//// `glp_runtime/lib/link/primitives/link_runtime.dart`).
////
//// This is the value the link kernels read and update — the link-layer analogue of
//// madGLP's `MadState`. **D5, settled here as C1's second deliverable:** the link
//// kernels attach to the engine as a PARALLEL effect-outcome threaded as an `Option`
//// on `RunnerContext`/`Reduced` and dispatched at the runner's label-miss
//// (`runner.gleam:1910` → `1922`), exactly as `MadState`/`MadOutcome` do. The
//// alternative — widening the pure `KernelOutcome` — was rejected by the operator for
//// madGLP (E5, ratified 2026-07-14) because it touches ~30 dispatch arms plus runner
//// and scheduler; that reasoning applies unchanged here, so the same shape is used.
//// The wiring itself lands with the first kernel (C2); C1 fixes the shape and the
//// state it carries.
////
//// **Distinct from the madGLP channel registry (D6).** This registry is keyed by ground
//// `LinkId` — one physical bilateral link. madGLP's distinguished-channel registry
//// (`contracts/madglp-port.md`) is a separate `(role, channel-tag)` namespace ABOVE
//// transports. They are different structures at different layers; do not merge them.
//// A LinkId's nonce is a carrier fact, meaningless as a logical channel identity.

import gleam/dict.{type Dict}
import glp/link/primitives/capability_gate.{type CapabilityGateRegistry}
import glp/link/primitives/link_establish.{type EstablishContext}
import glp/link/primitives/link_registry.{type LinkRegistry}
import glp/link/primitives/transport_registry.{type TransportRegistry}
import glp/link/seam/endpoint.{type Endpoint}
import glp/link/seam/link_id.{type LinkId}
import glp/link/seam/link_options.{type LinkOptions}
import glp/link/seam/transport.{type Transport}

/// All link-layer host state for ONE engine instance.
///
/// - `links`: THE canonical registry (R-5) — the only map of established links.
/// - `transports` / `gates`: the leaves available and the admission policy.
/// - `options`: default per-link options for establishment.
/// - `pending`: endpoints accepted by `'_link_listen'` and parked awaiting a matching
///   `'_link_accept'` (path B). Keyed by the ground LinkId carried in the in-band
///   request token. A parked endpoint is OPEN but not yet an established link — it is
///   deliberately NOT in `links`, so a half-completed handshake can never be mistaken
///   for an established link. C4 populates and drains this; C1 only carries it.
pub type LinkState {
  LinkState(
    links: LinkRegistry,
    transports: TransportRegistry,
    gates: CapabilityGateRegistry,
    options: LinkOptions,
    pending: Dict(LinkId, Endpoint),
  )
}

/// A runtime with no transports registered and the permissive default gate. Callers
/// register the leaves they want (`with_transport`) at the composition root — the
/// engine facade, mirroring the C# `LinkKernels.Install`.
pub fn new() -> LinkState {
  LinkState(
    links: link_registry.new(),
    transports: transport_registry.new(),
    gates: capability_gate.new(),
    options: link_options.default(),
    pending: dict.new(),
  )
}

/// Register a transport leaf (loopback / tcp).
pub fn with_transport(state: LinkState, leaf: Transport) -> LinkState {
  LinkState(
    ..state,
    transports: transport_registry.register(state.transports, leaf),
  )
}

/// Replace the capability-gate registry (install real acceptance policy).
pub fn with_gates(
  state: LinkState,
  gates: CapabilityGateRegistry,
) -> LinkState {
  LinkState(..state, gates: gates)
}

/// Replace the default per-link options.
pub fn with_options(state: LinkState, options: LinkOptions) -> LinkState {
  LinkState(..state, options: options)
}

/// Thread an updated registry back in after establishment.
pub fn with_links(state: LinkState, links: LinkRegistry) -> LinkState {
  LinkState(..state, links: links)
}

/// Park an accepted-but-unmatched endpoint awaiting `accept_link` (path B, C4).
pub fn park_pending(
  state: LinkState,
  id: LinkId,
  endpoint: Endpoint,
) -> LinkState {
  LinkState(..state, pending: dict.insert(state.pending, id, endpoint))
}

/// Take a parked endpoint, removing it (one accept per request — a parked endpoint is
/// adopted exactly once).
pub fn take_pending(
  state: LinkState,
  id: LinkId,
) -> Result(#(LinkState, Endpoint), Nil) {
  case dict.get(state.pending, id) {
    Error(_) -> Error(Nil)
    Ok(endpoint) -> Ok(#(
      LinkState(..state, pending: dict.delete(state.pending, id)),
      endpoint,
    ))
  }
}

/// The establish-context view of this state, for `link_establish.wire_established_link`.
pub fn establish_context(state: LinkState) -> EstablishContext {
  link_establish.EstablishContext(
    transports: state.transports,
    gates: state.gates,
    options: state.options,
  )
}
