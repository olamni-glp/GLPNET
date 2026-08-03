//// glp/link/primitives/capability_gate — host-side, below-GLP capability gate
//// (wave-3 US4, T033; amended contracts/link-handshake.md "Capability
//// enforcement").
////
//// Port of C# `ICapabilityGate` + `CapabilityGateRegistry` (mirror shape; the
//// macaroon-backed concrete gate lives outside the link layer and is injected
//// per scheme at the composition root, so the link layer stays
//// capability-agnostic). Verify-before-act: the gate runs BEFORE the transport
//// endpoint is opened or the link wired; `False` is a fail-closed refusal the
//// gate has already recorded — the caller surfaces it as a reasoned refusal,
//// never a crash, never a silent drop. Schemes with no registered gate resolve
//// to the allow-all default (loopback/tcp keep their ungated behaviour).

import gleam/dict.{type Dict}
import glp/link/seam/link_id.{type LinkId}
import glp/link/seam/link_scheme.{type LinkScheme}

/// The gate: `gate_establish` decides link establishment for an identity.
pub type CapabilityGate {
  CapabilityGate(gate_establish: fn(LinkId) -> Bool)
}

/// Allow-all default (C# `DefaultCapabilityGate`): a scheme with no registered
/// gate keeps its ungated behaviour.
pub fn allow_all() -> CapabilityGate {
  CapabilityGate(gate_establish: fn(_id) { True })
}

/// Per-scheme gate registry (C# `CapabilityGateRegistry`), keyed by the
/// normalized scheme token.
pub opaque type GateRegistry {
  GateRegistry(by_scheme: Dict(String, CapabilityGate))
}

pub fn new() -> GateRegistry {
  GateRegistry(dict.new())
}

/// Inject `gate` for `scheme` (composition-root seam).
pub fn register(
  registry: GateRegistry,
  scheme: LinkScheme,
  gate: CapabilityGate,
) -> GateRegistry {
  GateRegistry(dict.insert(
    registry.by_scheme,
    link_scheme.name(scheme),
    gate,
  ))
}

/// The gate for `scheme` — the allow-all default when none is registered.
pub fn gate_for(registry: GateRegistry, scheme: LinkScheme) -> CapabilityGate {
  case dict.get(registry.by_scheme, link_scheme.name(scheme)) {
    Ok(gate) -> gate
    Error(_) -> allow_all()
  }
}
