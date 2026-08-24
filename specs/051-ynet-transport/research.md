# Phase 0 Research: YNET `ynet-transport`

Resolves the residual operational unknowns deferred from `/bk-clarify` to `/bk-plan`, plus
best-practice decisions for the clarify-selected mechanisms. Grounded in `curator_report_cycle2.md`
(run `20260712T223008Z-c2a2`), `decisions-D1-D6.md`, and the 050 native-QUIC precedent.

## R1 — Hole-punch success-rate target (resolves SC-002 marker)

- **Decision**: Target **≥ 90% direct-punch success for the cone/endpoint-independent NAT class**
  and **100% relay-fallback (zero frame loss) for the symmetric/non-punchable class**, measured over
  the NAT-class test matrix. Bounded punch budget **≤ 5 s** before deterministic relay fallback.
- **Rationale**: iroh/libp2p field data put endpoint-independent NAT punch success in the high-90s;
  symmetric NAT is not reliably punchable → relay is the correct, not degraded, path. The 5 s budget
  matches the cycle-2 path-selection-latency envelope and keeps US2 AS3 deterministic.
- **Alternatives**: a single blended rate (rejected — hides the symmetric-NAT reality and would make
  the SC untestable); unbounded punch retries (rejected — violates the deterministic-fallback AS).

## R2 — Exit-abuse policy content & administration (resolves US6 AS4 marker)

- **Decision**: A curated trusted gate enforces, in order: **(1) destination allow/deny lists**
  (curated per gate), **(2) per-caller rate/volume caps**, **(3) declared egress-class filters**
  (e.g. protocol/port classes). Policy is **administered by the gate operator** and expressed as
  signed policy records the gate loads; default-deny when no policy authorizes the egress.
- **Rationale**: D5's trusted-gate model is BUILD-NEW with **no drop-in corpus reference** (cycle-2
  §4) — Tor's volunteer-exit is explicitly rejected. Allow/deny + rate-caps + class filters are the
  minimum abuse controls that keep a *curated* gate from becoming an open relay, and signing keeps
  them tamper-evident (consistent with self-certified records).
- **Alternatives**: reputation-scored open egress (rejected — reintroduces the volunteer-exit arms
  race); no policy / operator trust only (rejected — a compromised caller could weaponize the gate).

## R3 — Relay revocation semantics (resolves Edge-Case marker; mirrors 056 US3)

- **Decision**: Revocation is **fail-safe immediate for *new* path selection** and **tears down
  in-flight paths at the next frame boundary** (graceful, bounded drain), not mid-frame. A torn path
  surfaces the distinct `authorized-but-unreachable` reason (FR-018) so the caller can re-path.
- **Rationale**: Immediate new-selection block is required for security; instantaneous mid-frame
  teardown would corrupt in-flight frames and contradict the 050 graceful-teardown discipline
  (close-after-collect). Next-frame-boundary teardown is the safe middle. Must reconcile with the
  056 US3 revocation-race clarification (kept identical on both sides of the tier boundary).
- **Alternatives**: only-prevent-new-selection (rejected — a revoked relay keeps carrying live
  traffic indefinitely); hard mid-frame kill (rejected — frame corruption, teardown race).

## R4 — Per-node-keying migration sequencing (resolves Edge-Case marker; D1 open tension)

- **Decision**: **Dual-identity during transition** — a node presents both its GLPNET club-wide cert
  and its Ed25519 per-node key; peers prefer the per-node key when both verify. Cutover to
  per-node-only is **authorized per-node by an operator-signed migration record** (not automatic),
  after which the shared-cert path is refused. GLPNET `QuicTransport` remains a harvest source until
  every peer has cut over.
- **Rationale**: D1 mandates converge-to-single-owner without an ambiguous/duplicated identity
  (FR-019/FR-020). A hard flag-day cutover is infeasible across an evolving mesh; a per-node signed
  cutover is auditable and monotonic (no downgrade back to shared cert once cut over).
- **Alternatives**: flag-day cutover (rejected — coordinated downtime across the whole mesh);
  keep both indefinitely (rejected — perpetual dual-leaf violates D1's single-owner end-state).

## R5 — Native leaf QUIC: harvest strategy (best practice for FR-001/FR-002)

- **Decision**: Build `csharp/ynet_transport` by **harvesting `csharp/glp_link`** (MsQuic setup,
  per-scheme registries, reliability sublayer, `IPayloadCodec`/`ICapabilityGate` seams, the 050
  codexreview robustness fixes) and replacing the SPKI-pin identity with **Ed25519-key-as-TLS-identity**
  (iroh/`noq` model). Keep the 050 `IsSupported`-gate-and-refuse posture.
- **Rationale**: 050 shipped a hardened MsQuic link; re-deriving it would discard the 8 codexreview
  robustness fixes. Key-as-identity is the one corpus-proven QUIC-P2P identity model (cycle-2 §1.1).
- **Alternatives**: a fresh Rust `noq` leaf (deferred — larger surface, no in-tree reuse); keep SPKI
  pin (rejected — conflicts with per-node keying FR-020).

## R6 — Sealed routes & mix-trust (best practice for FR-009/FR-010/FR-010a)

- **Decision**: Adopt **Veilid private-route + I2P-style garlic bundling** (no fixed 3-hop) with
  **`SafetySelection`** (hop count / stability / sequencing) as the anonymity API. Node selection is
  **stake-weighted via `057-yngenios-pocw-coin`**, degrading to **Loopix semi-trusted-provider**
  selection when the pocw signal is absent, and **fabricating nothing** when neither is available.
- **Rationale**: cycle-2 §2/§5.5 — Veilid proves embeddable metadata protection; the clarify decision
  pins 057-pocw as standard with Loopix fallback (curated-node model ≈ Loopix semi-trusted).
- **Alternatives**: Tor fixed circuits (rejected — brief's "no rigid 3-hop"); Nym stake without 057
  (rejected — duplicates 057's mechanism).

## R7 — Multi-runtime split (best practice for FR-014/FR-015)

- **Decision**: Three impls sharing one `ICapability` design owner: **C#/.NET MsQuic** (native leaf,
  MVP), **Gleam/BEAM** (services/workstation tier), **JS/WASM** (browser: WebRTC datachannel +
  WebTransport uplink). No single cross-tier binary.
- **Rationale**: cycle-2 §3b — Gleam JS-target concurrency model is incompatible; browser cannot run
  MsQuic. Two+ implementations are structural, not incidental.
- **Alternatives**: Gleam-only across all tiers (rejected — JS concurrency incompatibility);
  native-only (rejected — abandons browser/edge reach, R9).

## Open (honestly deferred, not blocking this plan)

- **Human-memorable decentralized naming** — unsolved in the whole corpus (cycle-2 §6); the transport
  serves self-certified key→record only and names the further resolver (ties to mstack R9).
- **Mobile background/battery-budget** — corpus-integrity defect (mislabeled file); revisit once a
  real mobile-P2P energy reference is fetched. Both are in the spec Out-of-Scope and slated for the
  roadmap (engineer-approved: log both gaps).
