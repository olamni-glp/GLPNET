# Curator report — run `20260715T235300Z-235d` (meshtest-recure-ring, (b)-default design)

**Verdict: CONVERGED at cycle 2.** Task type: plan. Evidence pin: worktree `02bcc20`, read-only.
Critic: codex (cross-provider), holding the M-21 wildcard repo scope.

## What this run did, and what it proves

The frozen method (20 elements) froze the (b)-default design as a set of **decisions with named
factual premises**, and routed each premise to one of five **blind, pairwise-disjoint** Builders to
establish the facts by `file:line` evidence at the pin. Five Builders produced **173 attributed
claims**; the merge is 173 singleton (a phrasing artifact of disjoint slices — no two Builders can
write the same normalized text — not weak evidence); the codex Critic adjudicated **all 173 at repo
scope**: **147 CONFIRM / 25 REFUTE / 1 ESCALATE**.

**Honesty bound (M-31):** this run proves facts about code at a pinned commit. It is NOT a hardware
measurement, a throughput floor, or a delivery soak. Nothing here should be read as "kill-one
survived" or "the ring works" — those remain unmeasured.

## The method fix worked end-to-end (the single highest-value outcome)

**24 of the 25 REFUTEs are the M-20 absence-defect, now caught by construction.** In every one, a blind
Builder correctly scoped an absence/open-question to its own slice, and the wildcard-scope Critic
overturned it by finding the mechanism **shipped elsewhere in the repo** — precisely the region the
Builder was forbidden to read. This is the defect that, in the prior run, corrupted 11 of 14
refutations and survived to the synthesis. Here it was caught **inside the run**, by the role that
holds the wildcard. The remaining REFUTE (builder-4, `41a91cd…`→`3f3fd5c…`) is a genuine factual
correction: the advert record is 18 bytes (2+2+4+8+2, `MeshNodeRuntime.cs:480`), not 16. The lone
ESCALATE (`e37c63f…`) is correctly an evidentiary-standard judgment ("should an adversarial test be
written before this counterexample is treated as established") — the underlying code argument is
repo-verifiable; the standard is the engineer's call.

## The design got CHEAPER — premises that collapsed to wiring

- **M-25 stable identity — the prior run's "hard prerequisite, build first" is SUBSTANTIALLY REDUCED.**
  builder-3 found no signing identity in `Kernel/Mailbox`+`DurableExecution`; the Critic REFUTED at
  repo scope: `HostIdentity.LoadOrCreate` persists/loads a stable 32-byte seed
  (`Olamnit.Coin.Rewards/Identity/HostIdentity.cs:38`) and `StableDeviceSealKey` derives the Ed25519
  device key (`:25`); `YngeniosRegistration.cs:45` registers `StableDeviceSealKey`, **not** the
  in-memory default, when PGlite is present (overturns builder-2's `de30b1a…`). **A durable identity
  already exists → M-25 collapses to wiring**, not a first-thing-built prerequisite.
- **M-01(a)(b) durability primitives = REUSE.** builder-3 CONFIRMS group commit (many appends / one
  fsync) + `FsyncAlways`/`FsyncEveryNms` selector + crc32 framing + torn-tail truncation +
  crash-resume all **EXIST AND ARE WIRED** in the Mailbox WAL. Its "no snapshot/compaction" absence was
  REFUTED: `ISnapshotStore` + `RehydrationHost` ship (`Olamnit.Kernel/Persistence/RehydrationHost.cs:34`).
- **M-01(c) multi-key trust anchor = REUSE.** `PeerSetTrustAnchor` ships (grow-only, publish-once root,
  constant-time `IsTrusted`). builder-2's "single-key by contract" was slice-scoped to `Seal/` and
  REFUTED — the multi-key anchor is in `Kv/Capabilities/`.
- **M-01(d) runtime membership = REUSE.** `AddNeighbor`/`RemoveNeighborAsync` ship (edge granularity).
- **M-01(e) in-repo ring proof surface = REUSE.** `MeshInvariants` includes `DeliveryExactlyOnce` +
  `CheckPaths`; `FakeFabric.Cut` is bidirectional; named scenarios line-4/ring-4/diamond/star-5/dense-5.
  builder-4's open-question about those types was REFUTED (`Olamnit.Kernel/Verification/`).

## BUILD targets that survive, with sharpened specs

- **M-24 genesis-rooted elected roster epoch — still a BUILD target, premise HOLDS.** The trust anchor
  is grow-only with **no revoke/remove/version** (builder-1, CONFIRMED) → exclusion must be an additive
  elected roster epoch, never anchor mutation. But `DecisionRecord` is exactly
  (domain, term, decision, winner, protocol) with **no roster / nonce / genesis carrier** (CONFIRMED) →
  the (g1)–(g7) genesis manifest has **no shipped shape** and must be built. The single-winner-across-
  terms CAS **is** shipped and bindable, and its durable arbiter index **exists**
  (`KvSubstrateSchema.cs:88-89`) — overturning builder-1's two doubts about it (`f626d2c…`, `2aafe32…`).
  Election IS capability-gated at the keyspace boundary (`KeyspaceService.cs:198-244`), overturning
  builder-1's "not capability-gated" (`930f680…`).
- **M-27 emergent ring algorithm — the single largest genuinely-NEW build, and NOT Byzantine-safe.**
  The contribution seam (`LayeredLinkCostModel`, bounded against LOCAL inputs) ships, but the runtime
  never injects `ILinkCostModel`/`IRouteClock` (aging OFF, seam unreachable live), and **no history /
  EWMA / flap-tracking exists** (CONFIRMED). The election adjudicates WHICH ring, never whether inputs
  were honest.

## Two NEW security findings the blind slices surfaced (Critic verified as repo-readable)

- **`advert.Cost` is unbounded BELOW.** Remote-supplied, bounded only above (`>=16 ⇒ Infinity`); a
  link-authenticated neighbour can advertise `Cost=0` for every destination and elect itself next hop
  for the whole mesh (`DistanceVectorRouter.cs:97-100`). The elected-ring optimizer inherits this as its
  attack surface.
- **The seq ceiling is unrecoverable.** `_selfSeq` is never incremented; a hostile `Seq=ulong.MaxValue`
  advert permanently out-ranks every legitimate advert for a destination, even after the hostile link
  drops. Doc-vs-code disagreement (the interface asserts an ever-increasing seq; the code follows the
  inline comment that keeps it). Derived by reading, not executed — hence the ESCALATE on whether to
  require the adversarial test first.

## M-26 elected-ring steering — premise HOLDS

The frame is exactly `(dest, src, hopLimit, flags, inner)` — **no path field**; the forwarder consults
`flags`/`dest`/`hop`, and `src` is copied but never consulted (builder-4, CONFIRMED). Consensus can
decide a ring; the transport cannot enforce one → steer the route table, never pin a path. The honest
consequence (an adjacent-neighbour ring may take the deliver-local branch and never execute the
multi-hop path) stands, and the delivery-vs-routing split is now precise: `FailoverTests` asserts
**routing only** (next-hop/cost, no delivery); the delivery-asserting kill-one test is a **separate**
file (`MeshNodeRuntimeTests`, `atC.Count==1` + durable sink).

## M-30 mint authorization — REFINED, not blanket

The prior run's blanket "mint authorization is NEVER verified on the mint path" is **too strong at repo
scope**. builder-5 CONFIRMS the `ValidateMint`/`CommutativeOpProcessor` path verifies nothing
(`GenesisGrantIssuer` mints with an empty cert and gets `Applied`; `PoolAudit`'s conservation identity
holds for a forged fresh-funded mint because it adds to both sides; the independence check consults no
bindings and reduces to raw string equality — **M-36 both halves: the equality CAN fire because
`wallet_id` is arbitrary**). BUT the Critic REFUTED the spend-only claim: **`MintPipeline.cs:124`
verifies a reward-mint `WitnessCert` before `Admit`, and `VerificationSurface.cs:76` re-verifies mint
certs.** So: verification **exists** on the `MintPipeline` path and is **absent** on the
`CommutativeOpProcessor`/`MergeFrom` (OE-4) WAL path. `QuorumCertVerifier` checks scheme/subject/
signer-count/quorum/duplicate-signers/Ed25519 sigs but **does not pin signers to an enrolled trust
anchor** — a real, named gap. Under M-13(1) a BFT supply claim is still REFUTED for the unverified
path until that path verifies the elected decision.

## Rulings OWED BY THE ENGINEER — not manufactured here

1. **M-29 — the E-B ruling.** Does a next-hop-signed ACK satisfy the shipped corroboration contract?
   Facts established: no shipped corroborator verifies a signature; auto-endorse requires four
   non-cryptographic things; the corroborators ARE constructed (`YngeniosRegistration.cs:374,392`,
   overturning builder-5's "nothing constructs them"). The sufficiency ruling gates minting and is the
   engineer's, re-argued from code per M-36.
2. **M-34 — the coin straddle / L1.** Accepted planning ESCALATE; **no slice owned it**, correctly named
   an uncovered gap. `Olamnit.Coin` references BOTH `Olamnit.Kernel` (host) AND `Olamnit.Shared` (MAUI)
   by ProjectReference. Forbidden straddle to factor, or de-facto L1? Blocks implementation.
3. **The lone run ESCALATE** — whether to require an adversarial advert test before the seq-poisoning /
   cost=0 counterexamples are treated as established. A lighter evidentiary-standard call.

## Bottom line

The (b)-default design stands and is **mostly a wiring exercise on shipped 057/kernel surfaces**, not an
authoring exercise. Two genuine BUILD targets remain (the genesis-rooted elected roster epoch; the
emergent, input-bounded ring algorithm), two new mesh security bounds are needed (advert cost floor;
seq monotonicity), and the mint-verification gap is now localized to one path rather than blanket. Three
rulings are owed by the engineer before implementation. Nothing here is a measured hardware result.

---
## Run footer

- run: `20260715T235300Z-235d`  verdict: **converged**  cycles: 2
- critic: codex
- terminal review: skipped — task_type=plan: terminal codexreview is code-only; this is a planning-evidence run, cross-provider codex Critic already adjudicated all 173 claims at repo scope
