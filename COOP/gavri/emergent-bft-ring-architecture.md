# Emergent BFT Secure Ring — architecture (operator direction, 2026-07-15)

**Operator ruling (settles olamnit's option 1/2/3):** it's **option 2-and-beyond** — build the shared L1 core,
the ring becomes its demonstrator. The static ring is DEAD (refuted on 6 counts). The replacement is an
**emergent, consensus-ratified, dynamically-membered, Byzantine-fault-tolerant secure ring over the DV mesh.**

This is a real distributed-systems feature (BFT consensus + emergent topology optimization + dynamic membership
+ adversarial security monitoring), NOT a soak. It CONSUMES exactly the shared primitives olamnit's synthesis
found missing (RL-1/2/4) — so it adds no new foundational dependency, it makes the existing ones mandatory.

## ⚠️ VERIFIED CORRECTION (2026-07-15, repo-scope — supersedes the NEW/SHIPPED bands below where they conflict)
I first wrote this doc banding the consensus + multi-key anchor as **NEW**. That was WRONG — my 3rd scope-error
today (E-C, coin arithmetic, now this), same root cause: I read branch **023** all session; this stack landed on
**develop** after 023 branched, in `Kv/Election/` + `Kv/Capabilities/`. olamnit caught it at repo scope; I then
**read the develop code myself** and confirm:
- **RL-4 multi-key anchor = SHIPPED** — `Olamnit.Shared/Kv/Capabilities/PeerSetTrustAnchor.cs` (+ `PeerSetAmuletVerifier`):
  ordered trusted Ed25519 set, **publish-once immutable ROOT**, idempotent `TryAddPeerKey`, constant-time fail-closed
  `IsTrusted`; docstring: "Shared build-once with PBFT." SG-2 was slice-scoped (Builder 3 saw only `Seal/`).
- **L2 consensus = SHIPPED** — `Olamnit.Shared/Kv/Election/{PbftElection,RaftElection}.cs`: per-domain Raft (2f+1) /
  PBFT (3f+1), era/term + durable CAS decision log, `QuorumUnattainable` refusal (never a downgrade), **3f+1 EXPLICIT
  membership (never DHT-learned)**. The N=4→f=1→quorum=3 (kill-one succeeds, kill-two refuses) in "Honest guarantee"
  below is **CONFIRMED against shipped code**, not a derivation. (Built for the feature-056 KV fabric — reuse for the
  ring = integration, not new consensus.)
- **Episodic/periodic seam = SHIPPED** — `LinkCostInputs{Base,Quality,Load,Period,Event}` (`Mesh/ILinkCostModel.cs`).

**So the directive is largely REACHABLE BY REUSE.** The genuinely-NEW work is a bounded set of 4 gaps (VERIFIED):
1. **No exclude/revoke API on `PeerSetTrustAnchor`** (I read the interface — `TrustedKeys/TryPublishRootKey/`
   `TryAddPeerKey/IsTrusted`, keys add-only, root publish-once/immutable). ⇒ **the authority-driven PERMANENT
   EXCLUSION requirement is unsupported.** Because root is immutable + keys are add-only by design, revocation must be
   **additive** — an epoch-scoped exclusion set checked ALONGSIDE `IsTrusted` (never a mutation of the anchor). Design
   decision for olamnit (seal domain).
2. **Emergent ring optimizer = NEW** — the `Period/Event` contribution seam ships, but NO history/EWMA/flap/percentile
   aggregation and NO optimal-feasible-ring computation exist in `Mesh/`.
3. **Mesh delivery-receipt (SG-1/RL-1) = likely still NEW** — the consensus layer has its own quorum accounting
   (prepares), distinct from an end-to-end MESH delivery-receipt for the coin trigger / retransmit termination.
   Recheck precisely before building.
4. **Integration/binding = NEW** — wire the shipped election to elect the RING leader (it was built for KV domains);
   bind membership-epoch ↔ elected-ring ↔ mesh; safe rejoin.

The layered bands below stand for the mesh substrate (L0 SHIPPED) and L3/L4 (mostly NEW); treat L0-RL-4 and all of L2
as **SHIPPED-reuse** per this correction.

## Honest guarantee (no theater)
- **Safety:** with N nodes, tolerate **f < N/3** Byzantine nodes (equivocation, forgery, withholding, replay).
  **N=4 ⇒ f=1** (4 = 3·1+1). "≥4-client mesh" from the original feature is the floor; more resilience ⇒ more nodes.
- **Model:** PBFT-style safety under partial synchrony; **liveness** needs eventual synchrony (a stable enough
  network to elect a leader). "Extremely hostile / dynamic intelligent threat vectors" is addressed *up to f* by
  BFT + defense-in-depth (authority exclusion, witness checkpoints, roster pin, fail-closed seals); **beyond f it
  is NOT covered by the safety proof** — that boundary must be stated in the spec, not papered over.
- **Life-critical framing:** demands the assumptions (synchrony bound, f, key custody, authority trust) be
  EXPLICIT and tested, incl. the adversarial paths — not asserted.

## Layered architecture (banded SHIPPED | NEW | EXTENDS-shipped)

### L0 — Transport, identity, crypto
- **[SHIPPED]** `MeshNodeRuntime` + `DistanceVectorRouter` — the mesh substrate: multi-hop forward, DV reconverge
  on link change (split-horizon/poison-reverse), exactly-once (`{origin}:{seq}` + `IdempotentSink`), closed
  outcomes never-throw, reroute-on-`MarkDown`. CI-green. This is the routability/liveness engine.
- **[SHIPPED]** `SealSet.Seal/Verify` (crdtmsg) + `Ed25519Signer` — message authentication.
- **[NEW · RL-2]** stable per-node Ed25519 identity (durable, survives restart). gavri DONE (seq-15 pubkey,
  seed `0600` at `~/.olamnit-ring/`). Needed so votes are attributable across restarts. Others TODO.
- **[NEW · RL-4]** **multi-key trust anchor** — the load-bearing crypto unblock. Today `Ed25519AmuletVerifier`
  refuses any key but ONE pinned device key (`AmuletVerifier.cs:97-101`, "one device/one session"). BFT REQUIRES
  verifying peers' signatures ⇒ a `RosterTrustAnchor` that verifies "a key I trust but don't hold" against the
  current membership epoch's key set. Without this, no consensus vote can be verified. **HARD PREREQUISITE.**
- **[NEW · RL-1]** **delivery-receipt / failure detector** — origin-terminating receipt over `@mesh`. Doubles as
  the consensus heartbeat + liveness signal + coin trigger. Highest leverage (4 wins). gavri to prototype.

### L1 — Membership & views (epoch-versioned, authority-governed)
- **[NEW · RL-3]** `MembershipView(epoch, roster{nodeId→pubkey}, excluded_set, authority_grants)`, quorum
  co-signed, hash-pinned into the epoch genesis. Every add/drop/rejoin bumps the epoch (monotonic; anti-rollback
  via high-water epoch + seq — my seq-10 crypto-C1 witness).
- **[NEW]** **Authority-gated changes (the "higher player"):** add / drop / **permanent-exclude** arrive as
  **capability-scoped (macaroon) signed directives** from the authority, verified via the multi-key anchor, and
  folded into the NEXT epoch. Permanent cybersecurity exclusion = a revocation entry bound into epoch genesis so
  an excluded key can never re-enter without a fresh authority grant. (Reuses the shipped macaroon/amulet stack —
  once RL-4 makes it multi-key.)
- **[NEW]** **Safe rejoin:** a returning node re-syncs the current epoch view + chain head, proves identity
  (stable key), and is re-admitted ONLY via an epoch transition (never silently), anti-replay by epoch+seq.

### L2 — Consensus (leader election + view change) — the hard, subtle layer
- **[NEW]** **Leader (start/stop) election:** Raft-style lease for the benign/crash case — term numbers,
  randomized election timeout, heartbeat via the RL-1 failure detector. A crashed leader ⇒ new term, new leader.
- **[NEW]** **PBFT-style view-change for the Byzantine case:** a leader that equivocates / withholds / forges is
  detected (two conflicting signed messages for one (view,seq) = a cryptographic proof of misbehavior) and a
  **2f+1 view-change** elects a new leader. This is where BFT bugs live — it needs its own rigor pass.
- **[NEW]** **Non-leader failure (the "slightly different way"):** NOT an election — the mesh reroutes (DV
  reconverge) and, if the node stays down past a threshold, a membership-epoch bump drops it from the ring. Only
  the LEADER's failure triggers an election; a relay's failure triggers a reroute + possible epoch update.

### L3 — Emergent optimal ring formation
- **[EXTENDS-shipped]** each node contributes local observations — the DV router already tracks per-link cost
  (`ILinkCostModel`/`LayeredLinkCostModel`), stability (`RouteEntry.Stable`), and aging. EXTEND the cost model to
  fold **episodic/periodic reliability** (time-bucketed EWMA of drop-rate / RTT / availability) so the ring
  avoids routes with known bad periods.
- **[NEW]** nodes gossip these into a shared **cost/reliability matrix** of the current membership graph (ride the
  existing DV advert channel + reliability metadata).
- **[NEW]** the ring = an agreed **minimum-cost feasible Hamiltonian cycle** over that matrix: *feasible* = each
  consecutive pair is actually routable in the live DV table; *optimal* = min total cost / max reliability.
  Deterministic given the matrix (all honest nodes compute the same tour); N≤~10 exact, larger N heuristic
  (nearest-neighbour + 2-opt). **Quorum co-signs the elected ring**, bound to the membership epoch, re-elected on
  reconvergence / membership change / reliability shift. ⇒ the ring is DV-DERIVED and RE-ELECTED, never pinned
  (kills C-1); consensus ratification stops a Byzantine node imposing a bad ring.

### L4 — Security monitoring, liveness, integrity
- **[NEW]** continuous liveness from RL-1 receipts + mesh `MarkDown`.
- **[NEW]** **Byzantine-behavior detection:** equivocation, invalid seals, roster violations, replay → signed
  evidence that (a) triggers the L2 view-change and (b) feeds the authority's exclusion decision.
- **[NEW]** **witness checkpoints** (my seq-10 crypto-C1): all-node co-signed periodic `{epoch, max_seq, head_fp}`
  so no single node — incl. a Byzantine leader — can truncate/rollback undetected.
- **[SHIPPED discipline]** fail-closed everywhere; closed outcomes; never-throw.

## How this maps to the six refutations (it answers, not ignores)
- **C-1** (ring ⊥ DV mesh): the ring is now a consensus-ratified overlay COMPUTED from the DV table + re-elected → resolved.
- **C-2** (D7 relay-opacity): relays still forward opaque payloads; consensus/vote messages are their OWN addressed
  messages, not per-hop reads of a transiting token → boundary respected.
- **C-5** (bad-sig HALT vs link-drop reroute): formalized — Byzantine ⇒ view-change/exclude; crash ⇒ reroute/epoch → resolved.
- **RL-3** (fixed-N kill-one unsatisfiable): epoch-versioned membership + re-election over survivors → resolved.
- **SG-1/SG-2** (no receipt / single-device): now MANDATORY prerequisites (RL-1/RL-4), not optional.

## Phasing (primitives first — Raft AND PBFT both need them)
- **P0 (foundational core, shared L1):** RL-1 receipt/failure-detector · RL-4 multi-key roster anchor · RL-2 stable
  identity (finish for all nodes) · L1 membership epoch + witness checkpoints. **Nothing above builds without P0.**
- **P1 (crash-fault ring):** Raft leader lease + emergent ring election (L3) + non-leader reroute. Demonstrable on the
  diamond+kill mesh-test-v2 (SHIPPED test shape) — reroute-not-halt, exactly-once.
- **P2 (Byzantine):** PBFT view-change + equivocation detection + authority-gated exclusion/rejoin. The subtle part —
  own adversarial rigor pass (3rtask/codexreview) BEFORE and AFTER build.
- **P3 (adversarial soak):** the ≥4-node emergent-ring soak with injected leader-kills, relay-kills, a Byzantine
  node, and authority exclude/rejoin — measured (benchmarks that DON'T exist yet, C-6). Coin/reward accounting is
  olamnit's ledger domain (E-A/E-B, blocked as previously noted).

## Division of labor (proposal — olamnit LEADS mesh/kernel/consensus/ledger)
- **gavri:** RL-1 receipt/failure-detector (end-to-end, mine); the emergent-ring election over the DV matrix (L3);
  the fault-aware adapter + seal-on-egress decorator; the handset node-agents; drives the soak.
- **olamnit (lead):** RL-4 multi-key anchor + L1 membership/epoch/authority (seal domain); L2 Raft+PBFT consensus;
  the ledger (E-A wallet replication, E-B corroborator); the frozen method.

## Open decisions that need the operator / olamnit (NOT inventing these)
1. **N & f target:** ratify N=4 → f=1 as the floor, or aim higher (more nodes for f≥2)?
2. **The "higher player" authority:** a human operator issuing signed directives, or an automated authority
   service? Where does its key live, and what's its own compromise story?
3. **Synchrony/threat model to spec:** the explicit timing bound + the adversary's assumed power (≤ f, adaptive?).
4. **Does this SUPERSEDE the "1M-cycle soak" as the acceptance gate,** or is the soak now P3 (adversarial, measured)?
5. Given the magnitude, this is arguably a NEW roadmap feature ("BFT emergent secure ring") with its own spec +
   adversarial rigor cycle before build — recommend yes.

— gavri
