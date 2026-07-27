# meshtest-recure-ring — blind-Builder findings + cross-singleton synthesis

Companion to `olamnit/handoff.md` seq 21. Author: OLAMNIT (lead). Date: 2026-07-15.
Run `20260715T152146Z-0455` · method `method-20260715T152146Z-0455` (20 elements, frozen).
Evidence pin: **`02bcc20`** (isolated worktree `D:\bstdev\research\olamnit-wt-ring`).
4 blind Builders · **113 claims · 0 unattributed** · independence audit **6 inputs / 0 violations**.

> **Reading the merge count honestly:** merge reports **0 corroborated / 113 singleton / 0 conflict**.
> That is a fact about PHRASING, not evidence. Merge is pure set-ops over a normalized claim-TEXT hash
> (FR-003, "never a judgment call"). Builders 1 and 2 both found the no-ACK gap from DISJOINT sources but
> wrote different sentences, so the hash sees two singletons. Semantic agreement is invisible to the
> mechanical layer BY DESIGN. The synthesis below is the Curator's job, not the merge's.

---

## 4 SHARED GAPS — one root defect each, wearing several masks. Fix ACROSS, once.

### SG-1 — There is no delivery/receipt concept ANYWHERE in the stack
Found independently at three layers:
- `@mesh` is outbound-only; no inbound/receive/delivery tag exists (`MeshService.cs:29`).
- Mesh `_pending` is written at origination and cleared **only** at TTL expiry — no delivery path ever
  clears it (`grep _pending Mesh/` → 4 hits).
- The link layer documents outright that **a raw link has no ack channel** (`LinkSinkAdapter.cs:15-19`).

**Four "separate" bugs are symptoms of this ONE absence:** (a) no ACK to pay coins for; (b) retransmit
until TTL regardless of delivery — **100× at defaults, 2400× at the 059 bench's 120s TTL**, so 1M cycles
is NOT 1M link sends; (c) `Delivered + Dropped > Originated` — the documented conservation invariant is
violated once TTL elapses, because a delivered message ALSO counts as dropped at the source; (d) the coin
has no earning trigger.

**Fix locally ⇒ four incompatible ack semantics in four layers** — the exact drift the layering law
forbids. It **cannot** live at the link layer (honest at-least-once by design), so it is END-TO-END by
necessity. `MeshSendOutcome.Accepted` means "handed to the next hop" — reading it as an ACK pays for
un-acknowledged sends.

### SG-2 — The trust stack is single-device by construction (**REFUTED at repo scope — see below**)
- Device Ed25519 key **regenerates every launch**, never persisted (`IDeviceSealKey.cs:40-44`).
- `Ed25519AmuletVerifier` returns "untrusted key" for any key but the ONE pinned anchor, which pins the
  device's OWN key — "one device / one session" (`AmuletVerifier.cs:100-101,41-47`).
- Macaroon HMAC chain is per-credential attenuation under one device's key — NOT a cross-device hop chain.
- `EndorserKey` is domain separation on ONE host, not a separate trust domain.
- `@mesh` performs **no authorization at all** (asymmetric with `@kv`, which refuses ungranted ops).

> **⚠ PARTIALLY REFUTED (lead, verified at pin after the builders reported).** Builder-3 correctly flagged
> "I could not verify whether a multi-key anchor exists elsewhere; that is outside my slice." **It does
> exist:** `Kv/Capabilities/PeerSetTrustAnchor.cs` — "the extension of the 021 single-key
> `IAmuletTrustAnchor` pinning to a peer set", multi-key, fail-closed on empty, constant-time try-each with
> NO early exit. **`PbftElection` already consumes it.** So cross-device verification is NOT refused at repo
> scope — only within `Seal/`. The builder's honesty is what made this findable. **The ephemeral-identity
> half of SG-2 STANDS and is BLOCKER-2.**

### SG-3 — Nothing in this stack was built for 10⁶, at any layer
Three slices, three layers, same root:
- `DurableExecution`: unbounded retention, **no eviction site at all** (only Add sites) — 1M commit records
  + 1M sink payloads in RAM per node ⇒ first-order **OOM risk on tablet/phone**.
- `OpenAsync` replays the **entire** journal from offset 0 ⇒ the kill-one restart at cycle ~900k replays
  ~900k records **before accepting its next hop**. No measurement bounds this.
- Retransmit loop copies the whole `_pending` set every 50ms ⇒ O(rate × TTL) per tick.
- Coin: 1M `CoinDag`s + 1M leaves in unbounded dictionaries; `FindLiveLeavesOf` is an O(N) LINQ scan per
  aging op (~1M entries); ONE global lock across all mints; ~20-25 PGlite round trips per hop (~2×10⁷).
- ≥2 **unbatched** fsyncs per relayed hop (`FsyncAlways` default); **no group-commit exists anywhere** in
  `DurableExecution/` — the mechanism M-05 relies on **must still be written**.

### SG-4 — The documentation asserts security properties the code does not implement
**This is WHY both of us were wrong today. We read the docs and believed them.**
- `RewardClaimProcessor` docstring claims endorsements are "rejected when the endorser resolves to the
  claimant's own binding" — **the code never consults `_bindings`**; it is a raw string compare (`:154-157`).
  *(I ruled E-B partly by citing this docstring.)*
- An in-code comment names the pool audit as what "convicts" a forged merge — **it provably does not**
  (see C-4).
- `Dropped` counter doc omits the TTL path that increments it.
- `LinkScheme` enum calls `BleL2cap` a "SCHEME STUB, no transport" — `L2capLinkTransport` implements it.
- A `#020` worker-services contract line about the route service is refuted by its own validator (V6/D7).
- `#020` FR-007 ("no service-owned Task.Run pumps survive conversion") **directly contradicts** `#021`
  ("pumps stay inside"). MeshService follows #021 ⇒ the ledger's "converted" status for mesh does not meet
  #020's own definition.

⇒ **One audit pass over security-relevant doc claims. Not five local fixes.**

---

## IMPLIED DEPENDENCIES — undeclared coupling, nobody owns it

- **ID-1 · My E-B ruling silently depends on a mesh receive path that does not exist.** I ruled on the
  coin's endorsement surface and never checked the transport. No slice owned that seam; I never declared it.
- **ID-2 · The coin cannot build on ANY device without an out-of-repo GLPNET checkout at an ABSOLUTE path**
  (`$(GlpnetRoot)` default `D:/bstdev/glp/GLPNET`, pinned to GLPNET commit `5adfa0e3…`, packaging deferred).
  **The tablet and phone cannot have that path** ⇒ possibly fatal to the coin leg on the handsets. It is
  also why my "dot-keyed" claim is unverifiable — `PgliteOpWal.Merge` lives out there, asserted only in a
  doc comment.
- **ID-3 · Mint authorization evidence does NOT replicate with the coin ops.** `reward_signal/claim/
  endorsement/mint` are per-host PGlite; the op-WAL is what replicates. Anti-entropy carries minted ops
  **without the evidence that authorized them** — a peer folding a mint has nothing local to re-check
  against **even if re-validation were added**. This guts E-A as I specified it.
- **ID-4 · Signing primitive and durable chain are in different assemblies** with no reference between
  them; the ring needs both.
- **ID-5 · The bench MANDATES an encrypted link** (`secure:false` refused at parse, no opt-out) while mesh
  frames cross in clear and the secure transport has **never been composed on a radio path** (059 D5,
  DEVICE-SPIKE).

---

## CONTRADICTIONS

- **C-1 · "Pinned ring" and "rides the real DV mesh" are MUTUALLY EXCLUSIVE — this answers M-17's honesty
  gate.** The frame carries no path/source-route field (`MeshFrameCodec.cs:14-16`). Forwarding is solely
  `_router.TryNextHop(dest)`; originator intent is ignored. Two mutually exclusive relay impls exist:
  `MeshRouter` pins statically but holds NO DV router; `MeshNodeRuntime` uses DV but exposes NO pinning API.
  A ring realized the only way DV permits — adjacent-neighbour hops — resolves to seeded neighbour routes
  and takes the **deliver-local branch, so the multi-hop relay path NEVER EXECUTES**. **Such a soak proves
  link transport + dedup, NOT mesh routing.** That is materially less than the operator expects.
- **C-2 · The D7 trust boundary FORBIDS the ring's core mechanic.** Relays are contractually forbidden from
  reading the inner payload; only the destination interprets it (`MeshRouter.cs:14-16`). An Ed25519-chained
  relay requires every relay to read and sign what it forwards ⇒ **breaches** the boundary rather than
  extending it. The one telemetry peek is explicitly observe-only and cannot carry a reward decision.
- **C-3 · "1M hops ≈ 1 coin/node" is ARITHMETICALLY IMPOSSIBLE.** 1-µcoin rewards are unsplittable
  (`TryFloorSplit` false below k) AND unmergeable (`ValidateMerge` → `RejectedMalformed` for parents
  spanning different mint DAGs; each hop reward is its own mint/DAG) ⇒ **1M unmergeable dust leaves, never
  one coin**. Separately "1 µcoin" is 10⁶ off the shipped stage rate (which pays 1,000,000 µcoin = 1 coin).
- **C-4 · The audit does NOT convict a forged mint — the deepest security finding of the run.** The ledger
  **never verifies a mint's `WitnessCert`** (verification exists ONLY on the spend path, `SpendGate.cs:139`).
  `PoolAudit`'s identity `circulating + pool == fresh` **still HOLDS** for a forged mint, because it adds V
  to BOTH sides. It catches only pool overdraw. `Project` deliberately CLAMPS a forged overdraw to zero with
  a comment naming the pool audit as the conviction mechanism (`:289-293`). **The engine's designed answer to
  a hostile merged mint provably does not detect supply inflation.**
- **C-5 · Bad-sig HALT is unimplementable; link-drop REROUTE is free.** No signature code exists in
  `Mesh/`/`Link/` (one TODO says frames cross in clear); the closed `MeshSendOutcome` has 4 members and none
  expresses integrity failure; HALT is opposed to the never-throw pump discipline. **M-04's premise clause
  anticipated exactly this.** The link-drop half needs NO new mechanism.
- **C-6 · "fsync binds, not BLE" is UNSUPPORTED and probably INVERTED.** Three candidate floors, **zero
  measurements**: radio ~33.9h (from the only measured number, 122ms p50 — a LOWER bound), mailbox ~69h
  (from a CEILING, not a throughput figure), fsync unknown. And **no benchmark record exists at all**:
  `evidence/runs/` + `evidence/sessions/` contain only the empty git blob `e69de29b`; the ledger says
  "Status: pre-bench. No bench session has run."; every hardware cell is DEVICE-SPIKE; the subject's ENTIRE
  vocabulary (ucoin|coin|reward|soak|1,000,000|ed25519) returns **ZERO matches** across both evidence
  sources; the only PASS records are hand-authored fixtures with `commit:'dry-run'` and an invented
  `atMs:10`; `bench.py`'s scenario set is closed with no soak/ring; and the profile parser **fails closed on
  `shape:"ring"`** (vocabulary: line|triangle|diamond).

---

## TENSIONS

- **T-1 · Additive vs what the ring needs.** GOOD: a new inbound mesh tag is **registration-only** and would
  NOT violate #020 FR-010's additive rule; a new auto-endorsed reward class is **additive**, no engine change.
  BAD: extending `MeshSendOutcome` is a **breaking change to a frozen closed set**; the macaroon caveat set
  is closed + fail-closed, so hop-chain data cannot be threaded **without editing `Macaroon.cs`**; and the
  #021 conversion ledger is formally CLOSED, so extending mesh **reopens a discharged capability** and needs
  its own ledger row. ⇒ **The plan must NOT claim "all additive".**
- **T-2 · Kill-one vs ring order.** Killing one node makes a declared ring order **unsatisfiable** rather
  than merely rerouted — DV can reroute around a dead RELAY toward a live DESTINATION, but cannot deliver to
  a destination that no longer exists. Jointly satisfiable only if the ring **re-forms** over survivors —
  no shipped support found. **This is the SAME SHAPE as the M-02 defect the codex critic found** (fixed
  membership + kill-one = contradiction unless membership is epoch-versioned).
- **T-3 · Kill-one restart breaks THREE layers at once:** coin replays ~900k records; seal **changes
  identity**; mesh re-floods from scratch (TODO, no rehydration of routing identity or in-flight state).

---

## REINFORCING LINKS — one fix, several wins (NOT dependencies)

- **RL-1 · A delivery-receipt primitive is the highest-leverage fix in the run.** It gives the coin its
  earning trigger, terminates the 2400× amplification, repairs the conservation invariant, AND supplies the
  reason for `@mesh`'s inbound surface. **Four wins, one primitive.**
- **RL-2 · A durable stable node identity** serves seal (stable key), mesh (restart rehydration), election
  (roster), and coin. **Four beneficiaries, one primitive.** Hard prerequisite for reintegration.
- **RL-3 · Versioned membership.** Builder-2's "ring must re-form over survivors" and the critic's M-02
  "unanimity vs kill-one" are **the same defect at two layers**. One epoch mechanism serves both.
- **RL-4 · A multi-key trust anchor** serves the signature-verifying corroborator (E-B), witness-cert
  verification on mint (C-4), and `@mesh` authorization. **⇒ ALREADY SHIPPED as `PeerSetTrustAnchor`.**
- **RL-5 · Batch/epoch minting** would fix BOTH the PBFT-throughput conflict AND the unmergeable-dust
  problem (C-3) — a reinforcing link, not a compromise.

---

## What this does to the lead's rulings (E-A / E-B)

**E-A — confirmed in existence, REFUTED in scope. It is BIGGER than I told you, not smaller.**
Builder-4 confirmed `MergeFrom` exists + is implemented (`CoinProvenanceStore.cs:35,:104`), performs **zero
validation**, and that `Append` **throws** on a Spend to force the E3PC gate while **`MergeFrom` writes the
same WAL with no such guard** (a sharper statement of the bypass than mine). It confirmed OE-4 is already
code-documented (`CommutativeOpProcessor.cs:60-65`) — noting a plan premised on *discovering* this is
re-treading a known ledger entry. **But my scope claim fails on four counts:** (1) "dot-keyed" is
**unverifiable** from this repo (ID-2); (2) a naive validate-before-project **BREAKS CONVERGENCE** —
`Validate` is order-dependent (`RejectedUnknownRef` on unseen refs), so a peer delta in non-causal order
**rejects legitimate ops**; correct re-validation needs **causal/dependency ordering**; (3) `Validate` maps
`SpendBody → RejectedMalformed` **unconditionally**, so `Replay` rejects EVERY committed spend — `Validate`
itself must change; (4) **ID-3**: the authorization evidence does not replicate, so there is nothing to
re-validate against. Also my "re-validate witness-cert" is **misleading** — that check **never existed** on
the mint path (C-4); this is ADDING verification, not re-running it.

**E-B — mechanism INDEPENDENTLY CONFIRMED, but unimplementable today.**
Builder-4, blind, reached my exact finding: the independence check "can never fire on the auto path **by
construction**" because `EndorserKey` derives from the same host seed with domain separation. It further
found **both shipped corroborators are set-membership lookups with NO signature verification** — so
"evidence the claimant cannot forge" is only as strong as the injected function. It confirms a
**peer-signed-ACK corroborator WOULD satisfy the contract** and is **new code** — "the single load-bearing
addition on which an auto-endorsed hop reward's integrity rests". **But:** there is no ACK to build it on
(SG-1), and the authorization evidence does not replicate (ID-3).

---

## Honest limits of this run

- **0 corroborated** is a phrasing artifact (see the top note) — do not read it as weak evidence.
- Builders declared what they could NOT verify, and those non-verifications are load-bearing: the DI
  registration state of `MeshNodeRuntime`; `PgliteOpWal`'s dot-keying (out of repo); what `FsyncAlways`
  actually costs; whether a multi-key anchor existed elsewhere (**it did** — SG-2 note); Android↔Windows
  RFCOMM transports; `Olamnit.Coin.Rewards.Tests` coverage (16 files incl. `Soaks/`, not opened).
- Cycle 2 was NOT run (`converged=False`, `min_cycles=2`). Judgement: cross-querying singletons would
  sharpen claims about a design already refuted on six counts. Recorded as a deliberate stop, not a pass.
- **SG-2 was partially refuted by the lead AFTER the builders reported.** Treat every gap above as
  "true within the slice that found it" until checked at repo scope — that is exactly how SG-2 fell.

— olamnit (lead)
