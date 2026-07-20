//// glp/link/primitives/capability_gate — verify-before-act admission control for link
//// establishment (feature 050, T050.C1).
////
//// Port of `csharp/glp_link/primitives/CapabilityGateRegistry.cs` + `seam/ICapabilityGate.cs`.
////
//// **Why this matters more than the base MVP implied.** `contracts/link-primitives-port.md`
//// §5 recorded this as **D7: "base MVP uses the default allow-all gate"** — a benign
//// simplification. Gabi's policy ruling of 2026-07-20 changes that reading:
////
//// > "GLP is the config and control and policy language for all mesh and internal msg
//// > traffic … policies for accepting and rejecting inbound and outbound connections."
//// > "Mesh ring enables mesh traffic, while GLP enforces acceptance policies etc and
//// > routing from mesh endpoint to a service mailbox."
////
//// GLP **enforces** acceptance — so this gate is the enforcement point for a ruled
//// responsibility, not an optional hook. It stays allow-all by DEFAULT (an unconfigured
//// engine must still establish links, and no policy language exists over it yet), but
//// the seam is here so a real policy can be installed without touching establishment.
//// Wiring GLP-expressed policy INTO this gate is later work; if expressing such policy
//// needs anything the language cannot say today, that is a §1.14 item for Gabi.
////
//// **Fail-closed.** A gate that errors REFUSES. An admission check that cannot reach a
//// verdict must never fall open — that is the whole point of verify-before-act, and it
//// is the C# oracle's behaviour (`LinkEstablish.CapabilityRefusal`, fail-closed on
//// evaluation error). The gate is consulted BEFORE any endpoint is opened, so a refusal
//// costs no socket and leaks no connection.

import gleam/dict.{type Dict}
import glp/link/seam/link_id.{type LinkId}
import glp/link/seam/link_scheme.{type LinkScheme}

/// Which direction of establishment is being admitted. Gabi's ruling names BOTH —
/// "accepting and rejecting inbound **and outbound** connections" — so an outbound
/// dial is gated exactly like an inbound accept, and a policy can refuse either.
pub type Direction {
  /// This end is opening the link (connector / `request_link`).
  Outbound
  /// This end is admitting a peer (listener / `accept_link`).
  Inbound
}

/// What a gate is asked to admit. Everything here is ground by the time the host sees
/// it (the GLP wrappers guard `ground/1`), so a policy never has to reason about
/// unbound cells.
pub type Request {
  Request(id: LinkId, scheme: LinkScheme, direction: Direction)
}

/// A gate's verdict. `Refused` carries a reason so the refusal can surface as a fault
/// term the program can read, rather than an opaque failure.
pub type Verdict {
  Allowed
  Refused(reason: String)
}

/// One policy. Returns `Result` so a gate that cannot evaluate (missing credential,
/// unreachable authority) reports that distinctly from a clean refusal — and the
/// registry then fails it CLOSED rather than treating it as permission.
pub type Gate {
  Gate(evaluate: fn(Request) -> Result(Verdict, String))
}

/// Per-scheme gates plus the default applied when a scheme has none registered.
pub opaque type CapabilityGateRegistry {
  CapabilityGateRegistry(gates: Dict(LinkScheme, Gate), default: Gate)
}

/// The permissive default gate: admits everything.
///
/// Correct for the base MVP over `loopback`/`tcp`, where there is no policy to enforce
/// and no credential to check — an engine with no policy configured must still be able
/// to establish links. It is NOT a security control, and must not be mistaken for one:
/// the moment real acceptance policy exists (per Gabi's ruling above), a scheme that
/// needs it registers a real gate here.
pub fn allow_all() -> Gate {
  Gate(evaluate: fn(_request) { Ok(Allowed) })
}

/// A registry with no per-scheme gates and the allow-all default.
pub fn new() -> CapabilityGateRegistry {
  CapabilityGateRegistry(gates: dict.new(), default: allow_all())
}

/// Install a gate for one scheme (e.g. a macaroon gate for `"quic"`, as the C#
/// reference does). Replaces any gate already registered for that scheme.
pub fn register(
  registry: CapabilityGateRegistry,
  scheme: LinkScheme,
  gate: Gate,
) -> CapabilityGateRegistry {
  CapabilityGateRegistry(
    ..registry,
    gates: dict.insert(registry.gates, scheme, gate),
  )
}

/// Replace the default gate applied to schemes with no gate of their own — e.g. to make
/// an engine deny-by-default.
pub fn with_default(
  registry: CapabilityGateRegistry,
  gate: Gate,
) -> CapabilityGateRegistry {
  CapabilityGateRegistry(..registry, default: gate)
}

/// The gate governing `scheme` (its own, else the default).
pub fn select(registry: CapabilityGateRegistry, scheme: LinkScheme) -> Gate {
  case dict.get(registry.gates, scheme) {
    Ok(gate) -> gate
    Error(_) -> registry.default
  }
}

/// Evaluate admission for `request`, FAIL-CLOSED.
///
/// A gate that returns `Error(why)` — it could not reach a verdict — is converted to
/// `Refused`, never to `Allowed`. Called before any endpoint is opened.
pub fn admit(registry: CapabilityGateRegistry, request: Request) -> Verdict {
  let gate = select(registry, request.scheme)
  case gate.evaluate(request) {
    Ok(verdict) -> verdict
    // Fail-closed: an unevaluable policy denies. Never fall open.
    Error(why) ->
      Refused(reason: "capability gate evaluation failed (fail-closed): " <> why)
  }
}
