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
import gleam/list
import glp/link/primitives/capability_gate.{type CapabilityGateRegistry}
import glp/link/primitives/link_establish.{type EstablishContext}
import glp/link/primitives/link_pump.{type Inbox}
import glp/link/primitives/link_registry.{type LinkRegistry}
import glp/link/primitives/transport_registry.{type TransportRegistry}
import glp/link/seam/endpoint.{type Endpoint}
import glp/link/seam/link_id.{type LinkId}
import glp/link/seam/link_options.{type LinkOptions}
import glp/link/seam/transport.{type Transport}
import glp/runtime/terms.{type Term}

/// One pending request to lower the egress DRAINER for a newly established link
/// (T050.C5, deviation D-2 option (a)). It is a *request*, not the goal itself, because
/// only the scheduler owns goal identity and the run queue — the kernels run deep inside
/// `runner.reduce` and cannot enqueue. Exactly the `globalize.Spawn` → `lower_mad_spawns`
/// shape A3 established for `global_send/3`.
///
/// - `out_reader`: the heap address of the channel `Out` READER (`'_link_setup'/5` arg 4).
///   `link_drain/3`'s first argument is `Stream(X)?`, so reading it SUSPENDS until the
///   program conses via `link_send/3` — which is precisely why no `heap.onBind` is needed
///   (the C#/Dart oracle's mechanism, absent in Gleam).
/// - `link_id` / `to_peer`: the two ground terms the drainer's guards require, prebuilt
///   here so the scheduler needs no link-layer term knowledge to lower the goal.
pub type DrainRequest {
  DrainRequest(out_reader: Int, link_id: Term, to_peer: Term)
}

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
/// - `drains`: egress drainers awaiting lowering into runnable `link_drain/3` goals
///   (C5). Accumulated by the establishing kernels — which cannot enqueue — and taken by
///   the scheduler's `step_link` after the reduction, mirroring `MadState.mad_spawns`.
///   Always empty on the `LinkState` a driver hands back out.
/// - `inbox`: the ONE ingress queue every link's pump process sends to (C6). 🔴 Its
///   receiving end belongs to the process that called `new()` — the runner — because a
///   BEAM `Subject` may only be received on by its owner. Create the `LinkState` on the
///   process that will drive `step_link`, or `drain` silently returns nothing.
pub type LinkState {
  LinkState(
    links: LinkRegistry,
    transports: TransportRegistry,
    gates: CapabilityGateRegistry,
    options: LinkOptions,
    pending: Dict(LinkId, Endpoint),
    drains: List(DrainRequest),
    inbox: Inbox,
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
    drains: [],
    inbox: link_pump.new_inbox(),
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

/// Record that a newly established link needs its egress drainer lowered (C5).
///
/// 🔴 Call this ONLY on a genuine first establishment (`link_registry.Established`).
/// A repeat `link_setup` at the same ground identity returns `Reused` (FR-007) and must
/// NOT arm a second drainer: two `link_drain/3` goals reading one `Out` stream is a
/// double-read of a non-constant stream — each cons would be shipped twice, or worse the
/// two goals would race for the same head. Idempotency at identity means idempotency of
/// the egress arming too.
pub fn request_drain(
  state: LinkState,
  out_reader: Int,
  link_id: Term,
  to_peer: Term,
) -> LinkState {
  LinkState(..state, drains: [
    DrainRequest(out_reader, link_id, to_peer),
    ..state.drains
  ])
}

/// Take the accumulated drain requests in establishment order, leaving the state with
/// none. The scheduler calls this once per reduction and lowers what it gets; a request
/// is therefore lowered exactly once.
pub fn take_drains(state: LinkState) -> #(LinkState, List(DrainRequest)) {
  #(LinkState(..state, drains: []), list.reverse(state.drains))
}

/// The establish-context view of this state, for `link_establish.wire_established_link`.
pub fn establish_context(state: LinkState) -> EstablishContext {
  link_establish.EstablishContext(
    transports: state.transports,
    gates: state.gates,
    options: state.options,
  )
}
