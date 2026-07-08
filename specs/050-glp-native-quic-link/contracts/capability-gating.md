# Contract: Macaroon Gating — Verify-Before-Act (FR-008, FR-009)

## Where
- Verifier: `csharp/glp_crdtmsg/cap/Macaroon.cs` `Verify(rootKey, context, understoodKeys)` (REUSE).
- Carriage: `csharp/glp_crdtmsg/header/CapabilitySlot.cs` section `0x20`, additive-optional v2 (REUSE; on-wire-surface per research D-2).
- Refusal recording: `csharp/glp_crdtmsg/cap/Provenance.cs` `ProvenanceLog` / `ProvenanceOutcome.Refused` (REUSE).
- Gate point: `csharp/glp_link/primitives/LinkEstablish.cs` (establishment) + the egress/gated-action path.

## Contract

- **Establishment (FR-008)**: opening a `"quic"` link requires a valid static macaroon presented in the envelope capability slot. The gate calls `Macaroon.Verify(rootKey, context, understoodKeys)` **before** the link is wired. Establishment proceeds only when every caveat is understood AND satisfied AND the HMAC chain verifies (`FixedTimeEquals`).
- **Maintenance (FR-009)**: gated actions during an established session re-verify capability. An **absent / tampered / expired / unsatisfiable / un-understood** macaroon or caveat ⇒ **fail closed**: the action is refused, a **distinct refusal outcome is recorded** (`ProvenanceOutcome.Refused`), the run stays graceful, and the process does not crash. Never a silent drop.
- **Root secret**: the static-macaroon root key is distributed out-of-band alongside `glpquick-cert/` (beacon static-macaroon model). It is not minted per-session.

## Guarantees

- **G1**: valid macaroon, all caveats satisfied ⇒ action proceeds (SC-003 success half).
- **G2**: invalid/absent/tampered/expired ⇒ fail closed + recorded refusal + zero crashes (SC-003 refusal half).
- **G3 (FR-010/FR-011)**: capability is layered **above** the mutual-pin QUIC handshake — the pin authenticates the peer, the macaroon authorizes the action; neither substitutes for the other.
- **G4 (scope)**: verification is host-side; no new GLP guard/kernel/primitive. If gating turns out to need a GLP-visible predicate, STOP and propose-first (FR-019).

## Tests
- xUnit `csharp/glp_crdtmsg.tests/CapabilityTests.cs` (existing) covers `Verify` fail-closed + refusal recording.
- NEW `csharp/glp_link.tests/`: open a quic link with a valid macaroon (succeeds); with absent/tampered/expired (fails closed, refusal recorded, no crash); present a gated action mid-session with an invalid capability (refused + recorded, run stays graceful).
