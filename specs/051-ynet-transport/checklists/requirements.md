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

- **Open [NEEDS CLARIFICATION] markers (retained by design for `/bk-clarify` / `/bk-plan`).** They
  are more than the usual ≤3 because this transport tier is where the **cycle-2 §5 mechanism-divergent
  choices** land — those five are the cycle-2 analog of cycle-1's D1–D6, explicitly *handed to TASK 3*
  by the external cross-verification (they are engineer mechanism-choices, not spec defects). Mapped:
  1. **Rendezvous mechanism** (US2 AS3) — DHT-address vs NAT-signaling vs hidden-service (cycle-2 §5.3).
  2. **DHT build-vs-consume** (US3 AS3) — build embedded Kademlia/S-Kademlia vs consume external
     Pkarr/Mainline like iroh (cycle-2 §2 dht-store REFINE).
  3. **Relay-forward mechanism** (US4 AS3) — Tor cell vs libp2p circuit-relay-v2 vs TURN/WebRTC
     (cycle-2 §5.2).
  4. **Mix trust model + routing objective** (US5 AS3) — Loopix semi-trusted vs Nym stake-weighted
     (cycle-2 §5.5); and whether `sealed` optimizes for privacy while `normal` optimizes for latency
     (§5.1 divergent routing objectives).
  5. **Exit-abuse policy content** (US6 AS4) — what a curated trusted gate refuses and who administers
     it (D5 BUILD-NEW, no corpus reference).
  6. **Hole-punch success-rate target** (SC-002) — the measurable NAT-class success bar.
  7. **Relay revocation semantics** (Edge Cases) — tear down in-flight paths vs prevent new selection
     (mirrors the 056 US3 revocation-race question).
  8. **Per-node-keying migration sequencing** (Edge Cases) — how a node transitions off GLPNET's
     shared cert to per-node keying (the one D1 tension `decisions-D1-D6.md` left explicitly open).
  The **crypto-envelope** choice (cycle-2 §5.4) is treated as **decided** by D2 (olamnit AES-256-GCM
  baseline + H2/H3 hardening, adopting Veilid's whole-envelope-signature property) — FR-003 — so it is
  not carried as a marker. `/bk-clarify` will resolve the highest-impact of the above (its ≤5 question
  budget) and later stages absorb the rest.
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
