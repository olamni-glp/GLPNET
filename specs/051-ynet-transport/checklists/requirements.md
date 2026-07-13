# Specification Quality Checklist: YNET `ynet-transport` (GLPNET transport/overlay tier)

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-07-13
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [ ] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- **[NEEDS CLARIFICATION] markers — status after `/bk-clarify` (Session 2026-07-13).** The high-impact
  **cycle-2 §5 mechanism-divergent choices** (the cycle-2 analog of cycle-1's D1–D6, engineer
  mechanism-choices explicitly handed to TASK 3) are **RESOLVED** and recorded in the spec's
  `## Clarifications` section + the affected FRs:
  1. **Relay-forward mechanism** (§5.2) → RESOLVED: hybrid by traffic class — libp2p circuit-relay-v2
     for most mesh traffic; Tor-style cell relay default for internet traffic + critical/workspace
     flows (FR-007).
  2. **DHT build-vs-consume** (§2 REFINE) → RESOLVED: build an embedded **S-Kademlia** curated DHT
     (FR-006).
  3. **Rendezvous mechanism** (§5.3) → RESOLVED: DHT-address rendezvous standard + hidden-service-style
     for internet circuits (FR-005).
  4. **Mix trust model** (§5.5) → RESOLVED: stake-weighted via the new **`057-yngenios-pocw-coin`**
     mechanism (standard) + Loopix semi-trusted fallback (FR-010a; new 057 dependency).
  The **crypto-envelope** choice (§5.4) was already **decided** by D2 (FR-003).
- **Residual markers DEFERRED to `/bk-plan`** (operational, lower architectural impact — appropriate to
  resolve with plan-level detail, not blocking):
  1. **Exit-abuse policy content** (US6 AS4) — what a curated trusted gate refuses and who administers
     it (D5 BUILD-NEW, no corpus reference).
  2. **Hole-punch success-rate target** (SC-002) — the measurable NAT-class success bar.
  3. **Relay revocation semantics** (Edge Cases) — tear down in-flight paths vs prevent new selection.
  4. **Per-node-keying migration sequencing** (Edge Cases) — transition off GLPNET's shared cert
     (the one D1 tension `decisions-D1-D6.md` left explicitly open).
- **Naming caveat**: the spec names concrete GLPNET/olamnit/qhstate seams (`QuicTransport`,
  `ICapability`/`CapabilityType.Udp`, `DistanceVectorRouter`/`MeshRelayRoute`, `EgressService`,
  amulet/AES-256-GCM, iroh/`noq`, Veilid `SafetySelection`). These are **architectural anchors carried
  from the locked tier boundary (D1–D6) and the external corpus** — the reuse-vs-build map IS this
  design-tier spec's subject (R10 de-dup mandate) — not premature implementation choices. Retained
  deliberately, consistent with the sibling 056 spec. Can be demoted to the plan if a stricter
  WHAT-only reading is preferred at `/bk-plan`.
- **Tier-boundary invariant** (FR-024 / SC-011): this feature owns the transport/overlay *mechanism*;
  qhstate 056 owns the service embed and the admission/leaf *policy*. The two are never merged.
- Items marked incomplete require spec updates before `/bk-clarify` or `/bk-plan`. Here the only
  incomplete item is the retained-by-design clarification set above.
